<#
.SYNOPSIS
    必应每日壁纸轮换 —— 拉取官方当日图的无水印高画质原图，设置为桌面壁纸。

.DESCRIPTION
    数据源（日期标签与微软 Bing Wallpaper 应用完全一致）:
        https://bingwallpaper.microsoft.com/api/BWC/getHPImages

    图片地址按以下顺序回退:
        1. https://www.bing.com/th?id={ID}_UHD.jpg        3840x2160  约 3.4-3.6 MB
        2. https://www.bing.com/th?id={ID}&w=3840&h=2160  3840x2160  约 0.8 MB
        3. 微软 CDN url2  .../Latest/3840x2400/{date}.jpg  3840x2400  约 0.8 MB
        4. https://www.bing.com/th?id={ID}_1920x1080.jpg  1920x1080  约 0.33 MB
        5. https://img.nanxiongnandi.com/{YYYYMM}/{slug}.jpg  2009-08 起  老图 1366x768

    并发安全：脚本用 cache\.running 做独占锁。抢锁失败时退出码为 2（表示"已有实例在跑"），
    调用方（托盘程序）可据此显示"正在更新"。成功/跳过为 0，出错为 1。

.PARAMETER Index
    绝对位置：0 = 今天，1 = 昨天……不指定时默认 0。

.PARAMETER Step
    相对切换：以 state.json 记录的当前日期为基准，+1 = 更早一张，-1 = 更新一张。
    越界自动夹在可获取范围内。与 -Index 同时给出时 -Step 优先。

.PARAMETER Use
    直接用指定的本地图片文件设壁纸，不联网、不下载。适合从缓存里挑图。

.PARAMETER Force
    忽略幂等判断，强制重新下载并重设壁纸。

.PARAMETER DryRun
    只打印将要做什么，不下载、不改壁纸。

.PARAMETER Clean
    只清理超过 keepDays 天的缓存文件，不换图。

.EXAMPLE
    bing-wallpaper.ps1                  # 换成今天
    bing-wallpaper.ps1 -Step 1          # 往前一张
    bing-wallpaper.ps1 -Step -1         # 往后一张（更新的）
    bing-wallpaper.ps1 -Use "D:\tools\bing-wallpaper\cache\20261117_OHR.TrailCedars.jpg"
#>
[CmdletBinding()]
param(
    [int]$Index = 0,
    [int]$Step = 0,
    [string]$Use = '',
    [switch]$Force,
    [switch]$DryRun,
    [switch]$Clean,
    [int]$Keep = 0,
    [switch]$CleanAll,
    [switch]$Favorite,
    [switch]$Unfavorite,
    [string]$Date = '',
    [switch]$Random,
    [switch]$CleanArchive,
    [switch]$WithIndex,
    [string]$CleanMode = 'recycle',
    [switch]$CleanArchiveIndex,
    [switch]$CleanFavorites,
    [string]$Query = '',
    [int]$PickRandom = 0,
    [string]$DateInfo = '',
    [switch]$UpdateIndex,
    [switch]$ExportRange,
    [string]$RangeFrom = '',
    [string]$RangeTo = '',
    [string]$DestDir = '',
    [string]$FavDate = '',
    [switch]$ExportLinks,
    [string]$UnfavDate = '',
    [string]$KwFile = '',
    [string]$Search = '',
    [switch]$Json,
    [switch]$Gallery,
    [switch]$GalleryClear,
    [switch]$IncludeOld,
    [string]$PrefetchDate = '',
    [switch]$PrefetchClear,
    [string]$StoryFor = '',
    [switch]$CleanStories,
    [switch]$Status,
    [int]$BudgetSec = 0,
    [int]$ProbeLockWait = 0,
    [switch]$RebuildCacheIndex,
    [switch]$AuditDates,
    [string]$WallDate = '',
    [switch]$WallKeep,
    [switch]$WallDrop,
    [switch]$WallArchive,
    [switch]$AuditOld,
    [switch]$FixLegacyFav,
    [switch]$FixLegacyDates
)

$ErrorActionPreference = 'Stop'

# 预取模式：只下载到归档，不设壁纸、不写 state、不登记保护名单
$script:NoApply = $false
if ($PrefetchDate) {
    $PrefetchDate = ([string]$PrefetchDate) -replace '[^0-9]', ''
    if ($PrefetchDate.Length -eq 8) {
        $Date = $PrefetchDate
        $script:NoApply = $true
    }
}

# 照片墙模式：下载只落 cache\wall，不设壁纸、不写 state 与索引
$script:WallMode = $false
# 注意：$WallArchive 是 switch 参数，脚本作用域里 $script:WallArchive 就是它本人，别在这里重置成 false
if ($WallDate) {
    $WallDate = ([string]$WallDate) -replace '[^0-9]', ''
    if ($WallDate.Length -eq 8) {
        $Date = $WallDate
        $script:NoApply = $true
        $script:WallMode = $true
        if ($WallArchive) { $script:WallArchive = $true }
    }
}

# 单次进程的下载总预算：交互指令 100 秒、预取 20 秒，可用 -BudgetSec 覆盖
if ($BudgetSec -le 0) { $BudgetSec = 100 }
if ($script:NoApply -and -not $PSBoundParameters.ContainsKey('BudgetSec')) { $BudgetSec = 20 }
$script:deadline = (Get-Date).AddSeconds($BudgetSec)

# 交互指令（翻页 / 指定日期 / 收藏 / 随机）值得为另一实例的锁等一会儿：
# 信息窗口一打开就会起后台预取，预取持锁期间翻页若立刻判失败，用户看到的就是「这张图取不到」
$script:LockWaitSec = 0
if (-not $script:NoApply) {
    if ($Step -ne 0 -or $Date -or $Use -or $Favorite -or $Unfavorite -or $Random -or $PSBoundParameters.ContainsKey('Index')) {
        $script:LockWaitSec = 25
    }
}

# 输出编码固定 UTF-8：被托盘捕获标准输出时，中文不会变成乱码
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }

# Windows PowerShell 5.1 默认可能只启用 TLS 1.0，需显式打开 1.2
try {
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
} catch { }
$ProgressPreference = 'SilentlyContinue'

# ---------------------------------------------------------------- 路径

$Root       = Split-Path -Parent $PSCommandPath
$ConfigPath = Join-Path $Root 'config.json'
$StatePath  = Join-Path $Root 'state.json'
$IndexPath  = Join-Path $Root 'cache\index.json'
$CacheDir   = Join-Path $Root 'cache'
$LockPath   = Join-Path $CacheDir '.running'
$LogPath    = Join-Path $env:TEMP 'BingWallpaperLite.log'
$FavDir     = Join-Path $Root 'favorites'
$FavIndex   = Join-Path $FavDir 'index.json'
$ArchiveDir = Join-Path $Root 'archive'
$ArchiveImgDir = Join-Path $ArchiveDir 'images';
$OldDir        = Join-Path $ArchiveDir 'old'
$OldIndexPath  = Join-Path $ArchiveDir 'old-index.json'
$WiliiIndexPath = Join-Path $ArchiveDir 'wilii-index.json'
$StoriesDir    = Join-Path $ArchiveDir 'stories'
$PrefetchDir   = Join-Path $CacheDir 'prefetch'
$WallDir       = Join-Path $CacheDir 'wall'
if ($script:NoApply -and $PrefetchDir -and -not (Test-Path $PrefetchDir)) {
    try { New-Item -ItemType Directory -Path $PrefetchDir -Force | Out-Null } catch { }
}
$StoryMapPath  = Join-Path $ArchiveDir 'story-map.json'
$GalleryDir    = Join-Path $CacheDir 'gallery'
$PinnedPath = Join-Path $Root 'pinned.json'
$PinnedLegacyPath = Join-Path $Root 'cache\pinned.json'

# ---------------------------------------------------------------- 接管模式（沙盒）
# 有 takeover\config.json 且 root 可用时，只读写 takeover\ 与 root：
# 安装目录的 cache / favorites / state / pinned / config 一律不碰，退出时整目录删掉即可回退。
$TakeoverDir     = Join-Path $Root 'takeover'
$TakeoverCfg     = Join-Path $TakeoverDir 'config.json'
$script:Takeover = $false
$script:TakeoverRoot = ''
$script:TakeoverBroken = ''
if (Test-Path $TakeoverCfg) {
    try {
        $tc = Get-Content $TakeoverCfg -Raw -Encoding UTF8 | ConvertFrom-Json
        $tr = [string]$tc.root
        if ($tr -and (Test-Path -LiteralPath $tr)) {
            $script:Takeover = $true
            $script:TakeoverRoot = (Resolve-Path -LiteralPath $tr).Path
        } else {
            $script:TakeoverBroken = $tr
        }
    } catch {
        $script:TakeoverBroken = '配置损坏'
    }
}
if ($script:Takeover) {
    $CacheDir      = Join-Path $TakeoverDir 'cache'
    $IndexPath     = Join-Path $TakeoverDir 'cache-index.json'
    $LockPath      = Join-Path $CacheDir '.running'
    $FavDir        = Join-Path $TakeoverDir 'favorites'
    $FavIndex      = Join-Path $FavDir 'index.json'
    $StatePath     = Join-Path $TakeoverDir 'state.json'
    $PrefetchDir   = Join-Path $CacheDir 'prefetch'
    $GalleryDir    = Join-Path $CacheDir 'gallery'
    $WallDir       = Join-Path $CacheDir 'wall'
    $ArchiveImgDir = $script:TakeoverRoot
    $PinnedPath    = Join-Path $TakeoverDir 'pinned.json'
    $PinnedLegacyPath = $PinnedPath
    foreach ($dT in @($TakeoverDir, $CacheDir, $FavDir)) {
        if (-not (Test-Path $dT)) { try { New-Item -ItemType Directory -Path $dT -Force | Out-Null } catch { } }
    }
}

if (-not (Test-Path $CacheDir)) { New-Item -ItemType Directory -Path $CacheDir -Force | Out-Null }


# 画廊模式：下载落点改到 cache\gallery，退出时整个清掉，不登记保护名单
# 下载落点统一解析：预取 -> cache\prefetch，画廊 -> cache\gallery，其余 -> cache 根
function Get-DownloadDir {
    if ($script:WallMode -and $script:WallArchive) { $d = $ArchiveImgDir }
    elseif ($script:WallMode) { $d = $WallDir }
    elseif ($script:NoApply) { $d = $PrefetchDir }
    elseif ($Gallery) { $d = $GalleryDir }
    elseif ($script:Takeover) { $d = $script:TakeoverRoot }
    else { $d = $CacheDir }
    if ($d -and -not (Test-Path $d)) { try { New-Item -ItemType Directory -Path $d -Force | Out-Null } catch { } }
    return $d
}
$DownDir = Get-DownloadDir

# 把 cache 根目录里的一张图补进 cache\index.json；文件不在 cache 根就跳过。
# 命中本地 / 预取转正 / 复用旧文件这三条路径以前不写索引，cache 里就会出现查不到标题的孤儿文件。
function Update-CacheIndex {
    param([string]$Date, [string]$Path, [string]$Title, [string]$Copyright, [string]$ImageId)
    try {
        if ($script:NoApply -or $Gallery) { return }
        if ($Date.Length -ne 8 -or -not $Path -or -not (Test-Path $Path)) { return }
        $dir = Split-Path -Parent $Path
        if (-not $dir) { return }
        $di = New-Object System.IO.DirectoryInfo($dir)
        $ci = New-Object System.IO.DirectoryInfo($CacheDir)
        if ($di.FullName -ne $ci.FullName) { return }
        $catalog = @{}
        if (Test-Path $IndexPath) {
            try {
                $old = Get-Content $IndexPath -Raw -Encoding UTF8 | ConvertFrom-Json
                foreach ($pp in $old.PSObject.Properties) { $catalog[$pp.Name] = $pp.Value }
            } catch { }
        }
        $ttl = [string]$Title
        $cpr = [string]$Copyright
        $iid = [string]$ImageId
        if (-not $ttl) {
            try {
                $rec = Get-ArchiveRecord -Date $Date
                if ($rec) {
                    if (-not $ttl) { $ttl = [string]$rec.title }
                    if (-not $cpr) { $cpr = [string]$rec.copyright }
                    if (-not $iid) { $iid = [string]$rec.id }
                }
            } catch { }
        }
        $catalog[$Date] = [ordered]@{
            title     = $ttl
            titleEn   = ''
            copyright = $cpr
            file      = (Split-Path $Path -Leaf)
            imageId   = $iid
            sizeBytes = (Get-Item $Path).Length
        }
        [System.IO.File]::WriteAllText($IndexPath, ($catalog | ConvertTo-Json -Depth 6), (New-Object System.Text.UTF8Encoding($false)))
        Write-Log ("已补写 cache 索引：$Date")
    } catch {
        Write-Log ("补写 cache 索引失败：" + $_.Exception.Message) 'WARN'
    }
}

# ---------------------------------------------------------------- 日志

function Write-Log {
    param([string]$Message, [string]$Level = 'INFO')
    $line = '{0}  [{1}]  {2}' -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $Level, $Message
    if ($DryRun) { Write-Host $line }
    try {
        [System.IO.File]::AppendAllText($LogPath, $line + "`r`n", (New-Object System.Text.UTF8Encoding($false)))
    } catch { }
    try {
        $fi = Get-Item $LogPath -ErrorAction SilentlyContinue
        if ($fi -and $fi.Length -gt 512KB) {
            $txt = [System.IO.File]::ReadAllText($LogPath, [System.Text.Encoding]::UTF8)
            $tail = $txt.Substring([Math]::Max(0, $txt.Length - 131072))
            [System.IO.File]::WriteAllText($LogPath, $tail, (New-Object System.Text.UTF8Encoding($false)))
        }
    } catch { }
}

# 接管模式：只读。清理、移动这类会改动 root 内容的指令一律拒绝（DryRun 放行，只打印计划）
if ($script:Takeover -and -not $DryRun) {
    $roBlocked = @()
    if ($Clean) { $roBlocked += '-Clean' }
    if ($CleanAll) { $roBlocked += '-CleanAll' }
    if ($CleanArchive) { $roBlocked += '-CleanArchive' }
    if ($CleanArchiveIndex) { $roBlocked += '-CleanArchiveIndex' }
    if ($CleanFavorites) { $roBlocked += '-CleanFavorites' }
    if ($WallKeep) { $roBlocked += '-WallKeep' }
    if ($roBlocked.Count -gt 0) {
        Write-Log ("接管模式只读，拒绝执行：" + ($roBlocked -join ' / ')) 'ERROR'
        exit 10
    }
}

# ---------------------------------------------------------------- 并发锁

function Enter-RunLock {
    param([int]$WaitSec = 0)
    $deadline = (Get-Date).AddSeconds([Math]::Max(0, $WaitSec))
    $announced = $false
    while ($true) {
        $busy = $true
        try {
            $fs = [System.IO.File]::Open($LockPath, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
            $bytes = [System.Text.Encoding]::UTF8.GetBytes("$PID|" + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
            $fs.Write($bytes, 0, $bytes.Length)
            $fs.Close()
            return $true
        } catch {
            try {
                $txt = [System.IO.File]::ReadAllText($LockPath, [System.Text.Encoding]::UTF8)
                $parts = $txt -split '\|'
                if ($parts.Count -lt 2) { throw 'bad lock' }
                $otherPid = [int]$parts[0]
                $startAt = [datetime]::ParseExact($parts[1], 'yyyy-MM-dd HH:mm:ss', $null)
                $alive = $null -ne (Get-Process -Id $otherPid -ErrorAction SilentlyContinue)
                if ($alive -and ((Get-Date) - $startAt).TotalMinutes -lt 10) {
                    if (-not $announced) {
                        Write-Log ("已有实例在跑（PID " + $otherPid + "），等待其完成（最多 " + $WaitSec + " 秒）") 'WARN'
                        $announced = $true
                    }
                } else {
                    Write-Log ("清理僵尸锁（PID " + $otherPid + " 已不存在或超时）") 'WARN'
                    Move-Item -LiteralPath $LockPath -Destination ($LockPath + '.stale') -Force -ErrorAction SilentlyContinue
                    $busy = $false
                }
            } catch {
                # 读不到内容 = 正被独占 = 确实在跑
                $busy = $true
            }
        }
        if (-not $busy) { continue }
        if ((Get-Date) -ge $deadline) {
            if ($announced) { Write-Log '等待锁超时，本次退出' 'WARN' } else { Write-Log '锁文件被占用，本次退出' 'WARN' }
            return $false
        }
        Start-Sleep -Milliseconds 300
    }
}

function Exit-RunLock {
    try { [System.IO.File]::Delete($LockPath) } catch { }
}

# ---------------------------------------------------------------- 配置

# 测试用：-ProbeLockWait N 只测一次抢锁等待耗时后退出，不参与业务
if ($ProbeLockWait -gt 0) {
    $swProbe = [System.Diagnostics.Stopwatch]::StartNew()
    $probeGot = Enter-RunLock -WaitSec $ProbeLockWait
    $swProbe.Stop()
    Write-Output ("probeLock=" + $probeGot + " ms=" + $swProbe.ElapsedMilliseconds)
    if ($probeGot) { Exit-RunLock }
    exit 0
}
$defaults = [ordered]@{
    source         = 'bing'
    titleLanguage  = 'zh'
    mkt            = 'zh-CN'
    resolution     = 'UHD'
    keepDays       = 30
    autoClean      = $true
    notify         = $false
    archive        = $true
    locked         = $false
    wallpaperStyle = 'Fill'
    proxy          = ''
    archiveMaxMB   = 500
}

$config = @{}
foreach ($k in $defaults.Keys) { $config[$k] = $defaults[$k] }
if (Test-Path $ConfigPath) {
    try {
        $loaded = Get-Content $ConfigPath -Raw -Encoding UTF8 | ConvertFrom-Json
        foreach ($p in $loaded.PSObject.Properties) { $config[$p.Name] = $p.Value }
    } catch {
        Write-Log "config.json 解析失败，使用默认配置: $($_.Exception.Message)" 'WARN'
    }
}

$keepDays = [int]$config['keepDays']
if ($keepDays -lt 8) { $keepDays = 8 }
$proxy = [string]$config['proxy']

function Invoke-Web {
    param([hashtable]$Params)
    if ($proxy) { $Params['Proxy'] = $proxy }
    $Params['Headers'] = @{ 'User-Agent' = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64)' }
    return $Params
}

# ---------------------------------------------------------------- 壁纸样式

$styleMap = @{
    'Center'  = @{ WallpaperStyle = '0';  TileWallpaper = '0' }
    'Tile'    = @{ WallpaperStyle = '0';  TileWallpaper = '1' }
    'Stretch' = @{ WallpaperStyle = '2';  TileWallpaper = '0' }
    'Fit'     = @{ WallpaperStyle = '6';  TileWallpaper = '0' }
    'Fill'    = @{ WallpaperStyle = '10'; TileWallpaper = '0' }
    'Span'    = @{ WallpaperStyle = '22'; TileWallpaper = '0' }
}
$styleName = [string]$config['wallpaperStyle']
if (-not $styleMap.ContainsKey($styleName)) { $styleName = 'Fill' }

function Set-WallpaperStyle {
    param([string]$Name)
    $m = $styleMap[$Name]
    try {
        Set-ItemProperty 'HKCU:\Control Panel\Desktop' -Name 'WallpaperStyle' -Value $m.WallpaperStyle -ErrorAction Stop
        Set-ItemProperty 'HKCU:\Control Panel\Desktop' -Name 'TileWallpaper'  -Value $m.TileWallpaper  -ErrorAction Stop
    } catch {
        Write-Log "写入壁纸样式失败: $($_.Exception.Message)" 'WARN'
    }
}

# ---------------------------------------------------------------- 第三方归档（完整记录索引）

# 数据源：Zhu-junwei/bing-wallpaper-archive（2016-03-05 至今，含中文标题与版权）
# 备用源：niumoo/bing-wallpaper（2021-02 起，只有 ID，无标题）
$ArchivePrimary = 'https://raw.githubusercontent.com/Zhu-junwei/bing-wallpaper-archive/master'
$ArchiveBackup  = 'https://raw.githubusercontent.com/niumoo/bing-wallpaper/main/picture'
$ArchiveMinDate = '20090712'

# 归档抓取通道。实测（无代理直连）：
#   raw.githubusercontent.com  → 100% 失败（TLS 阻断）
#   gh-proxy.com               → 6/6 成功，平均 800 ms
#   api.github.com             → 可通，未认证 60 次/小时
# 顺序：记忆通道 → gh-proxy → raw 直连 → api.github.com → 本地代理
$ArchiveProxyBase = 'https://gh-proxy.com/'
$ArchiveRawBase   = 'https://raw.githubusercontent.com/'

function Get-ArchiveCandidateUrls {
    param([string]$RelPath)   # 形如 Zhu-junwei/bing-wallpaper-archive/master/2026/11/all.json
    $urls = @()
    $last = [string]$config['archiveVia']
    if ($last -eq 'ghproxy')  { $urls += @{ tag='ghproxy';  url = $ArchiveProxyBase + $ArchiveRawBase + $RelPath } }
    if ($last -eq 'raw')      { $urls += @{ tag='raw';      url = $ArchiveRawBase + $RelPath } }
    if ($last -eq 'api')      { $urls += @{ tag='api';      url = '__API__' + $RelPath } }

    $urls += @{ tag='ghproxy'; url = $ArchiveProxyBase + $ArchiveRawBase + $RelPath }
    $urls += @{ tag='raw';     url = $ArchiveRawBase + $RelPath }
    $urls += @{ tag='api';     url = '__API__' + $RelPath }

    # 本地代理端口（有 Clash 时最快）
    foreach ($px in @('http://127.0.0.1:7897', 'http://127.0.0.1:7890', 'http://127.0.0.1:10809')) {
        $urls += @{ tag=('proxy:' + $px); url = ($ArchiveRawBase + $RelPath); proxy = $px }
    }

    # 去重，保持顺序
    $seen = @{}
    $out = @()
    foreach ($c in $urls) {
        $k = $c.tag
        if (-not $seen.ContainsKey($k)) { $seen[$k] = $true; $out += $c }
    }
    return $out
}

function Invoke-ArchiveFetch {
    param([string]$RelPath)
    $reqHeaders = @{'User-Agent' = 'Mozilla/5.0'}
    $cands = Get-ArchiveCandidateUrls -RelPath $RelPath
    $lastErr = ''

    foreach ($c in $cands) {
        $tag = $c.tag
        $u = $c.url
        try {
            if ($u.StartsWith('__API__')) {
                # 用 GitHub contents API 取 base64
                $apiPath = $u.Substring(7)     # owner/repo/branch/path...
                $parts = $apiPath.Split('/')
                if ($parts.Length -lt 4) { continue }
                $owner = $parts[0]; $repo = $parts[1]; $branch = $parts[2]
                $filePath = ($parts[3..($parts.Length - 1)] -join '/')
                $apiUrl = "https://api.github.com/repos/$owner/$repo/contents/$filePath" + "?ref=$branch"
                $r = Invoke-WebRequest -Uri $apiUrl -TimeoutSec 8 -UseBasicParsing -Headers $reqHeaders
                $obj = $r.Content | ConvertFrom-Json
                if (-not $obj.content) { throw 'API 无 content 字段' }
                $txt = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String(($obj.content -replace '\s', '')))
                if ($txt.Length -gt 0) {
                    Write-Log ("归档索引取到了（via GitHub API）")
                    Save-ArchiveVia 'api'
                    return [pscustomobject]@{ Content = $txt }
                }
            } else {
                $ht = $reqHeaders
                if ($c.proxy) {
                    $r = Invoke-WebRequest -Uri $u -TimeoutSec 8 -UseBasicParsing -Headers $ht -Proxy $c.proxy
                } else {
                    $r = Invoke-WebRequest -Uri $u -TimeoutSec 8 -UseBasicParsing -Headers $ht
                }
                if ($r) {
                    Write-Log ("归档索引取到了（via " + $tag + "）")
                    if ($tag -eq 'ghproxy') { Save-ArchiveVia 'ghproxy' }
                    elseif ($tag -eq 'raw') { Save-ArchiveVia 'raw' }
                    return $r
                }
            }
        } catch {
            $lastErr = $_.Exception.Message
            Write-Log ("  通道 " + $tag + " 失败：" + $lastErr) 'WARN'
        }
    }
    Write-Log ("所有通道失败，最后错误：" + $lastErr) 'WARN'
    return $null
}

function Save-ArchiveVia {
    param([string]$Via)
    try {
        if ([string]$config['archiveVia'] -eq $Via) { return }
        $config['archiveVia'] = $Via
        [System.IO.File]::WriteAllText($ConfigPath, ($config | ConvertTo-Json -Depth 4), (New-Object System.Text.UTF8Encoding($false)))
    } catch { }
}

# wilii 全量文件名表（2016-03-05 起）：字段 d/n，n 是不带分辨率后缀的基础名
# 用途：Bing 官方与 CDN 都取不到时的最后一道兜底
$script:WiliiMap = $null
function Get-WiliiMap {
    if ($null -ne $script:WiliiMap) { return $script:WiliiMap }
    $script:WiliiMap = @{}
    if (-not (Test-Path $WiliiIndexPath)) { return $script:WiliiMap }
    try {
        $arr = Get-Content $WiliiIndexPath -Raw -Encoding UTF8 | ConvertFrom-Json
        foreach ($x in $arr) {
            $k = [string]$x.d
            if ($k.Length -eq 8) { $script:WiliiMap[$k] = [string]$x.n }
        }
    } catch {
        Write-Log "wilii 索引解析失败: $($_.Exception.Message)" 'WARN'
    }
    return $script:WiliiMap
}

function Get-WiliiCandidates {
    param([string]$Date)
    $Date = ([string]$Date) -replace '[^0-9]', ''
    if ($Date.Length -ne 8) { return @() }
    $map = Get-WiliiMap
    if (-not $map.ContainsKey($Date)) { return @() }
    $base = [string]$map[$Date]
    if (-not $base) { return @() }
    $root = 'https://img.wilii.cn/bingimages/' + $Date.Substring(0,4) + '/' + $Date.Substring(4,2) + '/' + $Date.Substring(6,2) + '/'
    return @(
        ($root + $base + '_1920x1080.jpg'),
        ($root + $base + '.jpg')
    )
}

# 每日故事：正文按需抓取并缓存在 archive\stories\{YYYYMMDD}.txt
# 映射表 story-map.json 只有「日期 -> todaybing slug」，正文不进安装包
$script:StoryMap = $null
function Get-StoryMap {
    if ($null -ne $script:StoryMap) { return $script:StoryMap }
    $script:StoryMap = @{}
    if (-not (Test-Path $StoryMapPath)) { return $script:StoryMap }
    try {
        $o = Get-Content $StoryMapPath -Raw -Encoding UTF8 | ConvertFrom-Json
        foreach ($pp in $o.PSObject.Properties) { $script:StoryMap[$pp.Name] = [string]$pp.Value }
    } catch {
        Write-Log "故事映射表解析失败: $($_.Exception.Message)" 'WARN'
    }
    return $script:StoryMap
}

function Get-Story {
    param([string]$Date, [switch]$NoFetch)
    $Date = ([string]$Date) -replace '[^0-9]', ''
    if ($Date.Length -ne 8) { return '' }
    $f = Join-Path $StoriesDir ($Date + '.txt')
    if (Test-Path $f) {
        try {
            $s = (Get-Content $f -Raw -Encoding UTF8)
            if ($s) { return $s.Trim() }
            return ''
        } catch { return '' }
    }
    if ($NoFetch) { return '' }
    $map = Get-StoryMap
    if (-not $map.ContainsKey($Date)) { return '' }
    $slug = [string]$map[$Date]
    if (-not $slug) { return '' }
    try {
        $resp = Invoke-WebRequest -Uri ('https://www.todaybing.com/bing/' + $slug) -TimeoutSec 20 -UseBasicParsing `
            -Headers @{'User-Agent' = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64)'; 'Referer' = 'https://www.todaybing.com/'}
        $html = [string]$resp.Content
        $m = [regex]::Match($html, 'data-text-zh="([^"]{10,})"')
        if (-not $m.Success) { $m = [regex]::Match($html, 'data-text="([^"]{10,})"') }
        if (-not $m.Success) { return '' }
        $story = $m.Groups[1].Value
        $story = $story -replace '&quot;', '"' -replace '&#39;', "'" -replace '&lt;', '<' -replace '&gt;', '>' -replace '&amp;', '&' -replace '&nbsp;', ' '
        $story = $story.Trim()
        if ($story.Length -lt 10) { return '' }
        if (-not (Test-Path $StoriesDir)) { New-Item -ItemType Directory -Path $StoriesDir -Force | Out-Null }
        [System.IO.File]::WriteAllText($f, $story, (New-Object System.Text.UTF8Encoding($false)))
        return $story
    } catch {
        Write-Log ("故事抓取失败（" + $Date + "）：" + $_.Exception.Message) 'WARN'
        return ''
    }
}

# 增量刷新 wilii 兜底索引：只拉最近 3 页（48 条），按日期合并进现有文件
function Update-WiliiIndex {
    $map = @{}
    if (Test-Path $WiliiIndexPath) {
        try {
            $arrW = Get-Content $WiliiIndexPath -Raw -Encoding UTF8 | ConvertFrom-Json
            foreach ($x in $arrW) {
                $k = [string]$x.d
                if ($k.Length -eq 8) { $map[$k] = [string]$x.n }
            }
        } catch {
            Write-Log ("兜底索引读取失败，将重建：" + $_.Exception.Message) 'WARN'
        }
    }
    $added = 0
    for ($p = 1; $p -le 3; $p++) {
        try {
            $pwUri = "https://bing.wilii.cn/api/bing?page=$p&pageSize=16"
            $pwParams = @{ Uri = $pwUri; TimeoutSec = 20; UseBasicParsing = $true; Headers = @{'User-Agent' = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64)'} }
            if ($proxy) { $pwParams['Proxy'] = $proxy }
            $pw = Invoke-WebRequest @pwParams
            if (-not $pw -or -not $pw.Content) { throw ('第 ' + $p + ' 页响应为空') }
            $jw = $pw.Content | ConvertFrom-Json
            if (-not $jw -or -not $jw.response) { throw ('第 ' + $p + ' 页 JSON 无 response') }
            foreach ($it in @($jw.response.data)) {
                $d = ([string]$it.date) -replace '[^0-9]', ''
                if ($d.Length -ne 8) { continue }
                $fn = ([string]$it.filepath -split '/')[-1]
                if (-not $fn) { continue }
                if ($fn -match '^_\d+x\d+\.jpg$') { continue }
                $base = $fn -replace '_\d+x\d+\.jpg$', ''
                if (-not $map.ContainsKey($d)) { $added++ }
                $map[$d] = $base
            }
        } catch {
            Write-Log ("兜底索引第 " + $p + " 页失败：" + $_.Exception.Message) 'WARN'
        }
        Start-Sleep -Milliseconds 120
    }
    if ($map.Count -eq 0) { throw '兜底索引为空，保留原文件不动' }
    $outW = @()
    foreach ($k in ($map.Keys | Sort-Object -Descending)) { $outW += [ordered]@{ d = $k; n = $map[$k] } }
    [System.IO.File]::WriteAllText($WiliiIndexPath, ($outW | ConvertTo-Json -Depth 2 -Compress), (New-Object System.Text.UTF8Encoding($false)))
    $script:WiliiMap = $null    # 清内存缓存，下次重读
    return $added
}

# 老存档索引（2009-07 ~ 2016-03）：字段 d/t/c/n/a，n 是 wilii 文件名，a 是镜像站 slug
# 数据来自 bingwallpaper.anerg.com，按月度页从新到旧反推日期，条数必须等于当月天数才收
$script:OldMap = $null
function Get-OldIndexMap {
    if ($null -ne $script:OldMap) { return $script:OldMap }
    $script:OldMap = @{}
    if (-not (Test-Path $OldIndexPath)) { return $script:OldMap }
    try {
        $arr = Get-Content $OldIndexPath -Raw -Encoding UTF8 | ConvertFrom-Json
        foreach ($x in $arr) {
            $k = [string]$x.d
            if ($k.Length -eq 8) { $script:OldMap[$k] = $x }
        }
        Write-Log ("老存档索引已载入：" + $script:OldMap.Count + " 天")
    } catch {
        Write-Log "老存档索引解析失败: $($_.Exception.Message)" 'WARN'
    }
    return $script:OldMap
}

function Get-OldIndexRecord {
    param([string]$Date)
    $Date = ([string]$Date) -replace '[^0-9]', ''
    if ($Date.Length -ne 8) { return $null }
    $map = Get-OldIndexMap
    if (-not $map.ContainsKey($Date)) { return $null }
    $x = $map[$Date]
    $slug = [string]$x.a           # anerg 的图片 slug
    $fn   = [string]$x.n           # wilii 的文件名
    if (-not $slug -and -not $fn) { return $null }

    $y4 = $Date.Substring(0, 4)
    $m2 = $Date.Substring(4, 2)
    $d2 = $Date.Substring(6, 2)
    $ym = $y4 + $m2

    $anergUrl = ''
    if ($slug) { $anergUrl = 'https://img.nanxiongnandi.com/' + $ym + '/' + $slug + '.jpg' }
    $wiliiUrl = ''
    if ($fn) { $wiliiUrl = 'https://img.wilii.cn/bingimages/' + $y4 + '/' + $m2 + '/' + $d2 + '/' + $fn }

    # 文件名取短的：优先 anerg slug，没有就用 wilii 文件名去掉分辨率后缀
    $core = $slug
    if (-not $core) {
        $core = $fn -replace '_\d+x\d+\.jpg$', ''
        $core = $core -replace '\.jpg$', ''
    }

    return @{
        id        = ''
        title     = [string]$x.t
        copyright = [string]$x.c
        url       = $wiliiUrl
        slug      = $core
        anergUrl  = $anergUrl
        wiliiUrl  = $wiliiUrl
        source    = 'old'
        searchUrl = ''
    }
}

function Get-ArchiveRecord {
    param([string]$Date)
    # 返回 @{ id=...; title=...; copyright=...; url=... } 或 $null
    # 归一化：允许 2016-03-05 / 2016/03/05 等写法，非 8 位数字一律视为无效
    $Date = ([string]$Date) -replace '[^0-9]', ''
    if ($Date.Length -ne 8) { return $null }
    if ($Date -lt $ArchiveMinDate) { return $null }

    # 老存档优先：2016-03 之前主源没有数据，直接查镜像索引，省一次联网
    if ($Date -lt '20160305') {
        $oldRec = Get-OldIndexRecord -Date $Date
        if ($oldRec) { return $oldRec }
    }

    $y  = $Date.Substring(0, 4)
    $m  = $Date.Substring(4, 2)
    $ym = $y + '-' + $m
    if (-not (Test-Path $ArchiveDir)) {
        try { New-Item -ItemType Directory -Path $ArchiveDir -Force | Out-Null } catch { }
    }
    $cacheFile = Join-Path $ArchiveDir "$ym.json"

    # 先读本地缓存
    if (Test-Path $cacheFile) {
        try {
            $m0 = Get-Content $cacheFile -Raw -Encoding UTF8 | ConvertFrom-Json
            $v0 = $m0.$Date
            if ($v0) {
                if ($v0 -is [string]) {
                    # 极老格式：只有 ID 字符串，视为无标题，继续联网补全
                } elseif ($v0.id) {
                    $tt = [string]$v0.t
                    $cc = [string]$v0.c
                    $uu = [string]$v0.u
                    # 只有带标题或直链的才算新格式缓存；否则（旧 niumoo 格式）重新抓
                    if ($tt -or $uu) {
                        return @{
                            id        = [string]$v0.id
                            title     = $tt
                            copyright = $cc
                            url       = $uu
                            searchUrl = [string]$v0.s
                        }
                    }
                }
            }
        } catch { }
    }

    $map = @{}

    # 主源：Zhu-junwei 月度 all.json（含标题、版权、直链）
    $rel1 = "Zhu-junwei/bing-wallpaper-archive/master/$y/$m/all.json"
    Write-Log ("归档索引: " + $rel1)
    $r1 = Invoke-ArchiveFetch -RelPath $rel1
    if ($r1) {
        try {
            $txt1 = $r1.Content
            if ($txt1 -is [byte[]]) { $txt1 = [System.Text.Encoding]::UTF8.GetString($txt1) }
            $obj1 = $txt1 | ConvertFrom-Json
            foreach ($im in @($obj1.images)) {
                $d = ([string]$im.enddate) -replace '-', ''
                if (-not $d) { continue }
                $ub = [string]$im.urlbase
                $id = ($ub -replace '^/th\?id=', '')
                if (-not $id) { continue }
                if (-not $map.ContainsKey($d)) {
                    $map[$d] = [ordered]@{
                        id = $id
                        t  = [string]$im.title
                        c  = [string]$im.copyright
                        u  = [string]$im.url
                        s  = [string]$im.copyrightlink
                    }
                }
            }
        } catch { Write-Log "主源解析失败（$ym）: $($_.Exception.Message)" 'WARN' }
    }

    # 备用源：niumoo 月度 README（只有 ID）
    if ($map.Count -eq 0) {
        $r2 = Invoke-ArchiveFetch -RelPath "niumoo/bing-wallpaper/main/picture/$ym/README.md"
        if ($r2) {
            try {
                $txt2 = $r2.Content
                if ($txt2 -is [byte[]]) { $txt2 = [System.Text.Encoding]::UTF8.GetString($txt2) }
                $rx = [regex]'th\?id=(OHR\.[A-Za-z0-9_\-]+)_UHD\.jpg[^|]*?(\d{4}-\d{2}-\d{2})'
                foreach ($mm in $rx.Matches($txt2)) {
                    $d = $mm.Groups[2].Value -replace '-', ''
                    if (-not $map.ContainsKey($d)) {
                        $map[$d] = [ordered]@{ id = $mm.Groups[1].Value; t = ''; c = ''; u = '' }
                    }
                }
            } catch { }
        }
    }

    if ($map.Count -eq 0) {
        Write-Log "归档索引取不到（$ym）" 'WARN'
        return $null
    }

    try {
        $out = [ordered]@{}
        foreach ($k in ($map.Keys | Sort-Object)) { $out[$k] = $map[$k] }
        [System.IO.File]::WriteAllText($cacheFile, ($out | ConvertTo-Json -Depth 4), (New-Object System.Text.UTF8Encoding($false)))
    } catch { }

    if ($map.ContainsKey($Date)) {
        $r = $map[$Date]
        return @{
            id        = [string]$r.id
            title     = [string]$r.t
            copyright = [string]$r.c
            url       = [string]$r.u
            searchUrl = [string]$r.s
        }
    }
    return $null
}

# 兼容旧调用：只要 ID
function Get-ArchiveId {
    param([string]$Date)
    $rec = Get-ArchiveRecord -Date $Date
    if ($rec) { return $rec.id }
    return ''
}
# ---------------------------------------------------------------- 取图列表

# 把 HPImageArchive 的 fullstartdate（UTC）折算成北京时间展示日
function Convert-BingDate {
    param([string]$FullStartDate, [string]$StartDate)
    # fullstartdate 形如 202611201600（UTC 2026-11-20 16:00 = 北京 11-21 00:00）
    if ($FullStartDate -and $FullStartDate.Length -ge 12) {
        try {
            $y = [int]$FullStartDate.Substring(0, 4)
            $mo = [int]$FullStartDate.Substring(4, 2)
            $d = [int]$FullStartDate.Substring(6, 2)
            $hh = [int]$FullStartDate.Substring(8, 2)
            $mi = [int]$FullStartDate.Substring(10, 2)
            $utc = New-Object DateTime($y, $mo, $d, $hh, $mi, 0, [DateTimeKind]::Utc)
            return $utc.AddHours(8).ToString('yyyyMMdd')
        } catch { }
    }
    if ($StartDate) { return ([string]$StartDate) -replace '-', '' }
    return ''
}

# 返回 JSON 字符串，避免 PowerShell 数组展开的怪癖
function Get-WallpaperListJson {
    param([int]$Count)
    $uri = "https://www.bing.com/HPImageArchive.aspx?format=js&idx=0&n=$([Math]::Max($Count, 8))&mkt=$($config['mkt'])"
    $p = Invoke-Web @{ Uri = $uri; TimeoutSec = 30; UseBasicParsing = $true }
    $resp = Invoke-RestMethod @p
    if (-not $resp.images) { throw 'Bing 接口返回空' }
    $out = @()
    foreach ($im in @($resp.images)) {
        $show = Convert-BingDate -FullStartDate ([string]$im.fullstartdate) -StartDate ([string]$im.startdate)
        $ub = [string]$im.urlbase
        $id = ($ub -replace '^/th\?id=', '')
        $out += [ordered]@{
            startdate     = $show
            enddate       = $show
            title         = [string]$im.title
            copyright     = [string]$im.copyright
            copyrightlink = [string]$im.copyrightlink
            urlbase       = $ub
            imageId       = $id
            rawStart      = [string]$im.startdate
        }
    }
    if ($out.Count -gt $Count) { $out = $out[0..($Count - 1)] }
    return ($out | ConvertTo-Json -Depth 3 -Compress)
}

# 兼容旧调用：返回对象数组
function Get-WallpaperList {
    param([int]$Count)
    $json = Get-WallpaperListJson -Count $Count
    # PS 5.1 的 ConvertFrom-Json 在管道里不展开数组，必须先落到变量
    $obj = $json | ConvertFrom-Json
    $arr = @($obj)
    # 运行时护栏：真拿到「装着一个数组的数组」时强制展开，避免 urlbase 被拼成一串
    if ($arr.Count -eq 1 -and $arr[0] -is [array]) {
        Write-Log 'Get-WallpaperList 返回嵌套数组，已强制展开' 'WARN'
        $arr = @($arr[0])
    }
    return $arr
}

function Get-ZhTitleMap {
    $map = @{}
    try {
        $p = Invoke-Web @{ Uri = "https://www.bing.com/HPImageArchive.aspx?format=js&idx=0&n=8&mkt=$($config['mkt'])"; TimeoutSec = 25; UseBasicParsing = $true }
        $resp = Invoke-RestMethod @p
        foreach ($i in @($resp.images)) {
            $id = ([string]$i.urlbase -replace '^/th\?id=', '') -replace '_(UHD|1920x1080).*$', ''
            $base = $id -replace '_[A-Za-z]{2}-[A-Za-z]{2}\d+$', ''
            if ($base) { $map[$base] = @{ title = [string]$i.title; copyright = [string]$i.copyright } }
        }
    } catch {
        Write-Log "取中文标题失败（回退英文）: $($_.Exception.Message)" 'WARN'
    }
    return $map
}

# ---------------------------------------------------------------- 下载

function Save-BingImage {
    param([string]$Url, [string]$Dest, [int]$MinKB = 300, [int]$Retry = 2)
    $tmp = "$Dest.tmp"
    $attempts = [Math]::Max(1, $Retry + 1)
    $lastErr = ''
    for ($i = 0; $i -lt $attempts; $i++) {
        if ($script:deadline -and (Get-Date) -gt $script:deadline) { throw '本次预算已用尽' }
        try {
            $p = Invoke-Web @{ Uri = $Url; OutFile = $tmp; TimeoutSec = 60; UseBasicParsing = $true }
            Invoke-WebRequest @p
            $size = (Get-Item $tmp).Length
            if ($size -lt ($MinKB * 1024)) { throw "体积异常 $size 字节" }
            Move-Item -LiteralPath $tmp -Destination $Dest -Force
            return $size
        } catch {
            $lastErr = $_.Exception.Message
            # 半成品 .tmp 留在原地，下一次同名下载会被 OutFile 直接覆盖
            if ($i -lt ($attempts - 1)) {
                $pause = [int](800 * [Math]::Pow(2, $i))
                Write-Log ("下载失败，重试 " + ($i + 1) + "/" + $Retry + "（" + $pause + " ms 后）：" + $lastErr) 'WARN'
                Start-Sleep -Milliseconds $pause
            }
        }
    }
    throw $lastErr
}# ---------------------------------------------------------------- 设壁纸

function Set-DesktopWallpaper {
    param([string]$Path)
    if ($script:NoApply) { Write-Log '预取模式：跳过设置壁纸'; return $true }
    if (-not ('Native.Wallpaper' -as [type])) {
        Add-Type -Namespace Native -Name Wallpaper -MemberDefinition @'
[DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
public static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);
'@
    }
    $r = [Native.Wallpaper]::SystemParametersInfo(0x0014, 0, $Path, 0x0001 -bor 0x0002)
    if ($r -eq 0) { Write-Log 'SystemParametersInfo 返回 0，走 rundll32 兜底' 'WARN' }
    Start-Sleep -Milliseconds 300
    if ((Get-ItemProperty 'HKCU:\Control Panel\Desktop' -Name Wallpaper -ErrorAction SilentlyContinue).Wallpaper -ne $Path) {
        & rundll32.exe user32.dll,UpdatePerUserSystemParameters 3,True
        Start-Sleep -Milliseconds 500
    }
    return ((Get-ItemProperty 'HKCU:\Control Panel\Desktop' -Name Wallpaper).Wallpaper -eq $Path)
}

# ---------------------------------------------------------------- 关键词搜索

# 全量索引缓存：1.6 MB，含 2016 至今所有日期的标题与版权
$AllIndexPath = Join-Path $ArchiveDir 'all-index.json'
$AllIndexUrl  = 'https://gh-proxy.com/https://raw.githubusercontent.com/Zhu-junwei/bing-wallpaper-archive/master/Bing_zh-CN_all.json'

function Get-FullIndex {
    param([switch]$Force)

    # 本地缓存 7 天内直接用（-Force 时跳过这段，强制联网）
    if ((-not $Force) -and (Test-Path $AllIndexPath)) {
        try {
            $age = (Get-Date) - (Get-Item $AllIndexPath).LastWriteTime
            if ($age.TotalDays -lt 7) {
                $arrC = Get-Content $AllIndexPath -Raw -Encoding UTF8 | ConvertFrom-Json
                return @($arrC)
            }
        } catch { }
    }

    Write-Log '正在下载全量索引（约 1.6 MB）…'
    if (-not (Test-Path $ArchiveDir)) {
        try { New-Item -ItemType Directory -Path $ArchiveDir -Force | Out-Null } catch { }
    }
    $resp = $null
    $try = @($AllIndexUrl, 'https://raw.githubusercontent.com/Zhu-junwei/bing-wallpaper-archive/master/Bing_zh-CN_all.json')
    foreach ($u in $try) {
        try {
            $resp = Invoke-WebRequest -Uri $u -TimeoutSec 40 -UseBasicParsing -Headers @{'User-Agent' = 'Mozilla/5.0'}
            if ($resp) { break }
        } catch {
            Write-Log ("  索引下载失败（" + $u + "）：" + $_.Exception.Message) 'WARN'
            $resp = $null
        }
    }
    if (-not $resp) {
        # 降级：有旧索引就用旧的，不阻断搜索
        if (Test-Path $AllIndexPath) {
            $oldAge = [math]::Round(((Get-Date) - (Get-Item $AllIndexPath).LastWriteTime).TotalDays, 1)
            Write-Log ('索引下载失败，改用本地旧索引（' + $oldAge + ' 天前）') 'WARN'
            try {
                $arrD = Get-Content $AllIndexPath -Raw -Encoding UTF8 | ConvertFrom-Json
                return @($arrD)
            } catch { }
        }
        throw '全量索引下载失败，且本地没有可用索引'
    }

    $txt = $resp.Content
    if ($txt -is [byte[]]) { $txt = [System.Text.Encoding]::UTF8.GetString($txt) }
    $obj = $txt | ConvertFrom-Json
    $arr = @($obj.images)

    # 转成精简格式再落盘，省一半体积
    $slim = @()
    foreach ($im in $arr) {
        $ub = [string]$im.urlbase
        $id = ($ub -replace '^/th\?id=', '')
        $slim += [ordered]@{
            d = ([string]$im.enddate) -replace '-', ''
            t = [string]$im.title
            c = [string]$im.copyright
            l = [string]$im.copyrightlink
            i = $id
        }
    }
    try {
        [System.IO.File]::WriteAllText($AllIndexPath, ($slim | ConvertTo-Json -Depth 3 -Compress), (New-Object System.Text.UTF8Encoding($false)))
        Write-Log ("全量索引已缓存：" + $slim.Count + " 条")
    } catch { }
    return $slim
}

function Search-Archive {
    param([string]$Keywords, [int]$Limit = 20)
    # 关键词用空格或逗号分隔
    $kws = @($Keywords -split '[\s,，、]+' | Where-Object { $_.Trim().Length -gt 0 } | ForEach-Object { $_.Trim() })
    if ($kws.Count -eq 0) { return @() }

    $idx = Get-FullIndex

    # 索引缓存可能是几天前的（惰性过期 7 天），直接搜会漏掉最新的图。
    # 这里用实时接口补最近 8 天，接口免费且本来就在调，等于给搜索加个「永不过期」的头。
    try {
        $recentJson = Get-WallpaperListJson -Count 8
        $recentObj = $recentJson | ConvertFrom-Json
        $recent = @($recentObj)
        if ($recent.Count -gt 0) {
            $seen = @{}
            $merged = @()
            foreach ($it in $recent) {
                $dd = [string]$it.startdate
                $seen[$dd] = $true
                $merged += [ordered]@{
                    d = $dd
                    t = [string]$it.title
                    c = [string]$it.copyright
                    l = [string]$it.copyrightlink
                    i = [string]$it.imageId
                }
            }
            foreach ($it in $idx) {
                if (-not $seen.ContainsKey([string]$it.d)) { $merged += $it }
            }
            $idx = $merged
            Write-Log ("搜索索引：最近 " + $recent.Count + " 天实时补齐，合计 " + $idx.Count + " 条")
        }
    } catch {
        Write-Log ("实时补齐失败，仅用本地索引：" + $_.Exception.Message) 'WARN'
    }

    $hits = @()
    foreach ($item in $idx) {
        $text = [string]$item.t + ' ' + [string]$item.c
        $matched = @()
        foreach ($kw in $kws) {
            if ($text.IndexOf($kw, [StringComparison]::OrdinalIgnoreCase) -ge 0) { $matched += $kw }
        }
        if ($matched.Count -gt 0) {
            $hits += [pscustomobject]@{
                date      = [string]$item.d
                title     = [string]$item.t
                copyright = [string]$item.c
                imageId   = [string]$item.i
                matched   = $matched
                score     = $matched.Count
            }
        }
    }
    # 命中词多的排前面，同分按日期新到旧
    $sorted = $hits | Sort-Object -Property score, date -Descending
    if ($sorted.Count -gt $Limit) { $sorted = $sorted[0..($Limit - 1)] }
    return @($sorted)
}

# ---------------------------------------------------------------- 邻近日

# 从全量索引里找紧邻的一天（Step=1 更早，Step=-1 更新），用于翻页越界续接
function Get-NeighborDate {
    param([string]$From, [int]$Step)
    if (-not $From) { return '' }
    $dates = @()
    try {
        $idx = Get-FullIndex
        if ($idx) { foreach ($it in $idx) { $dates += [string]$it.d } }
    } catch { }
    # 老存档（2009-08 ~ 2016-02）：主索引够不着这一段，翻页要能接上
    try {
        $oldMap = Get-OldIndexMap
        if ($oldMap -and $oldMap.Count -gt 0) { $dates += @($oldMap.Keys) }
    } catch { }
    if ($dates.Count -eq 0) { return '' }

    $best = ''
    foreach ($d in $dates) {
        if ($d.Length -ne 8) { continue }
        if ($Step -gt 0) {
            # 找比 From 更早的里最新的
            if ($d -lt $From -and ($best -eq '' -or $d -gt $best)) { $best = $d }
        } else {
            # 找比 From 更新的里最旧的
            if ($d -gt $From -and ($best -eq '' -or $d -lt $best)) { $best = $d }
        }
    }
    return $best
}

# ---------------------------------------------------------------- 主动保留名单

function Read-Pinned {
    $set = @{}
    # 新位置优先；旧版放在 cache\ 下的名单继续兼容读取
    $srcP = $PinnedPath
    if (-not (Test-Path $srcP) -and (Test-Path $PinnedLegacyPath)) { $srcP = $PinnedLegacyPath }
    if (Test-Path $srcP) {
        try {
            $o = Get-Content $srcP -Raw -Encoding UTF8 | ConvertFrom-Json
            foreach ($q in $o.PSObject.Properties) { if ($q.Value) { $set[$q.Name] = $true } }
        } catch { }
    }
    return $set
}

# 移动文件；若是当前桌面壁纸，先复制并切过去再删源，避免桌面指向不存在的文件
function Move-FileKeepWallpaper {
    param([string]$Src, [string]$Dest, [switch]$IsCurrentWallpaper)

    if ($IsCurrentWallpaper) {
        Copy-Item -LiteralPath $Src -Destination $Dest -Force
        [void](Set-DesktopWallpaper -Path $Dest)
        try { [System.IO.File]::Delete($Src) }
        catch { Write-Log ("复制后未能删除源文件（保留两份）：" + $Src) 'WARN' }
    } else {
        Move-Item -LiteralPath $Src -Destination $Dest -Force
    }
}

# 把指定文件移入收藏夹并维护两份索引；不给 -SetWallpaper 时不动桌面
function Move-ToFavorite {
    param([string]$SrcPath, [switch]$SetWallpaper)

    if (-not (Test-Path $SrcPath)) { throw '文件不存在' }
    $nameF = Split-Path $SrcPath -Leaf
    $dateKeyF = ($nameF -split '_')[0]
    if (-not $dateKeyF) { throw '文件名不含日期' }

    if (-not (Test-Path $FavDir)) { New-Item -ItemType Directory -Path $FavDir -Force | Out-Null }
    $destF = Join-Path $FavDir $nameF

    $favIdxF = @{}
    if (Test-Path $FavIndex) {
        try {
            $fiF = Get-Content $FavIndex -Raw -Encoding UTF8 | ConvertFrom-Json
            foreach ($q in $fiF.PSObject.Properties) { $favIdxF[$q.Name] = $q.Value }
        } catch { }
    }

    $isCurF = $false
    try {
        $curWF = [string](Get-ItemProperty 'HKCU:\Control Panel\Desktop' -Name Wallpaper -ErrorAction SilentlyContinue).Wallpaper
        if ($curWF -and $curWF -ieq $SrcPath) { $isCurF = $true }
    } catch { }
    Move-FileKeepWallpaper -Src $SrcPath -Dest $destF -IsCurrentWallpaper:$isCurF

    # 元信息：缓存索引 → state 兜底
    $metaF = $null
    if (Test-Path $IndexPath) {
        try {
            $ciF = Get-Content $IndexPath -Raw -Encoding UTF8 | ConvertFrom-Json
            if ($ciF.PSObject.Properties.Name -contains $dateKeyF) { $metaF = $ciF.$dateKeyF }
        } catch { }
    }
    if ((-not $metaF -or [string]$metaF.title -eq '')) {
        $stF = Read-State
        if ($stF -and [string]$stF.date -eq $dateKeyF) {
            $metaF = [pscustomobject]@{
                title     = [string]$stF.title
                titleEn   = ''
                copyright = [string]$stF.copyright
                imageId   = [string]$stF.imageId
            }
        }
    }

    $ttlF = ''
    $enF = ''
    $cprF = ''
    $idF = ''
    if ($metaF) {
        $ttlF = [string]$metaF.title
        $cprF = [string]$metaF.copyright
        $idF = [string]$metaF.imageId
        try { $enF = [string]$metaF.titleEn } catch { }
    }

    $favIdxF[$dateKeyF] = [ordered]@{
        title     = $ttlF
        titleEn   = $enF
        copyright = $cprF
        file      = $nameF
        imageId   = $idF
        sizeBytes = (Get-Item $destF).Length
    }
    [System.IO.File]::WriteAllText($FavIndex, ($favIdxF | ConvertTo-Json -Depth 6), (New-Object System.Text.UTF8Encoding($false)))

    # 只有「收藏当前壁纸」才改桌面与 state
    if ($SetWallpaper) {
        Set-WallpaperStyle -Name $styleName
        [void](Set-DesktopWallpaper -Path $destF)
        $stF2 = Read-State
        if ($stF2 -and [string]$stF2.date -eq $dateKeyF) {
            Write-State -Date $dateKeyF -File $destF -Title $ttlF -ImageId $idF -AppliedDate $dateKeyF
        }
    }

    # 文件已离开缓存，缓存索引里的条目同步删掉
    if (Test-Path $IndexPath) {
        try {
            $ciCleanF = Get-Content $IndexPath -Raw -Encoding UTF8 | ConvertFrom-Json
            if ($ciCleanF.PSObject.Properties.Name -contains $dateKeyF) {
                $tmpIdxF = [ordered]@{}
                foreach ($q in $ciCleanF.PSObject.Properties) {
                    if ($q.Name -ne $dateKeyF) { $tmpIdxF[$q.Name] = $q.Value }
                }
                [System.IO.File]::WriteAllText($IndexPath, ($tmpIdxF | ConvertTo-Json -Depth 6), (New-Object System.Text.UTF8Encoding($false)))
            }
        } catch { }
    }
    Write-Log "已收藏（移入收藏夹）：$nameF"
}

# 把收藏夹里的图移回缓存并维护两份索引（不删文件）
function Move-ToCache {
    param([string]$SrcPath)

    if (-not (Test-Path $SrcPath)) { throw '文件不存在' }
    $nameU = Split-Path $SrcPath -Leaf
    $dateKeyU = ($nameU -split '_')[0]
    if (-not $dateKeyU) { throw '文件名不含日期' }

    if (-not (Test-Path $CacheDir)) { New-Item -ItemType Directory -Path $CacheDir -Force | Out-Null }
    $destU = Join-Path $CacheDir $nameU

    $favIdxU = @{}
    if (Test-Path $FavIndex) {
        try {
            $fiU = Get-Content $FavIndex -Raw -Encoding UTF8 | ConvertFrom-Json
            foreach ($q in $fiU.PSObject.Properties) { $favIdxU[$q.Name] = $q.Value }
        } catch { }
    }
    $metaU = $favIdxU[$dateKeyU]

    # 先从收藏索引里删掉
    if ($favIdxU.ContainsKey($dateKeyU)) {
        $tmpU = [ordered]@{}
        foreach ($q in $favIdxU.Keys) {
            if ($q -ne $dateKeyU) { $tmpU[$q] = $favIdxU[$q] }
        }
        [System.IO.File]::WriteAllText($FavIndex, ($tmpU | ConvertTo-Json -Depth 6), (New-Object System.Text.UTF8Encoding($false)))
    }

    $isCurU = $false
    try {
        $curWU = [string](Get-ItemProperty 'HKCU:\Control Panel\Desktop' -Name Wallpaper -ErrorAction SilentlyContinue).Wallpaper
        if ($curWU -and $curWU -ieq $SrcPath) { $isCurU = $true }
    } catch { }
    Move-FileKeepWallpaper -Src $SrcPath -Dest $destU -IsCurrentWallpaper:$isCurU

    # 补回缓存索引
    if ($metaU) {
        $ciU = [ordered]@{}
        if (Test-Path $IndexPath) {
            try {
                $jU = Get-Content $IndexPath -Raw -Encoding UTF8 | ConvertFrom-Json
                foreach ($q in $jU.PSObject.Properties) { $ciU[$q.Name] = $q.Value }
            } catch { }
        }
        $ttlU = ''
        $enU = ''
        $cprU = ''
        $idU = ''
        try { $ttlU = [string]$metaU.title; $enU = [string]$metaU.titleEn; $cprU = [string]$metaU.copyright; $idU = [string]$metaU.imageId } catch { }
        $ciU[$dateKeyU] = [ordered]@{
            title     = $ttlU
            titleEn   = $enU
            copyright = $cprU
            file      = $nameU
            imageId   = $idU
            sizeBytes = (Get-Item $destU).Length
        }
        [System.IO.File]::WriteAllText($IndexPath, ($ciU | ConvertTo-Json -Depth 6), (New-Object System.Text.UTF8Encoding($false)))
    }

    # 当前壁纸指向收藏那份时，改指缓存
    $curU = ''
    try { $curU = [string](Get-ItemProperty 'HKCU:\Control Panel\Desktop' -Name Wallpaper -ErrorAction SilentlyContinue).Wallpaper } catch { }
    if ($curU -and $curU -ieq $SrcPath) {
        [void](Set-DesktopWallpaper -Path $destU)
        $stU = Read-State
        if ($stU -and [string]$stU.date -eq $dateKeyU) {
            Write-State -Date $dateKeyU -File $destU -Title ([string]$metaU.title) -ImageId ([string]$metaU.imageId) -AppliedDate $dateKeyU
        }
    }
    Write-Log "已移出收藏（退回缓存）：$nameU"
}

function Add-Pinned {
    param([string]$Date)
    if (-not $Date) { return }
    if ($script:NoApply) { return }
    # 画廊模式下看过的图是临时的，退出即清，不进保护名单
    if ($Gallery) { return }
    $set = Read-Pinned
    if ($set.ContainsKey($Date)) { return }
    $set[$Date] = $true
    try {
        [System.IO.File]::WriteAllText($PinnedPath, ($set | ConvertTo-Json -Depth 3), (New-Object System.Text.UTF8Encoding($false)))
    } catch { }
}

# ---------------------------------------------------------------- 缓存清理

function Clear-OldCache {
    param([int]$Days)
    $cut = (Get-Date).AddDays(-$Days)
    $cur = (Get-ItemProperty 'HKCU:\Control Panel\Desktop' -Name Wallpaper -ErrorAction SilentlyContinue).Wallpaper
    $removed = 0
    $protected = 0
    $pinnedSet = Read-Pinned
    Get-ChildItem $CacheDir -Filter '*.jpg' -File -ErrorAction SilentlyContinue | ForEach-Object {
        # 收藏里已有同名副本：这份是安全移动的残留，直接清掉
        if (Test-Path -LiteralPath (Join-Path $FavDir $_.Name)) {
            if ($DryRun) { Write-Log "[DryRun] 将删除重复副本 $($_.Name)" }
            else { try { [System.IO.File]::Delete($_.FullName); $removed++ } catch { } }
            return
        }
        # 正在使用的壁纸永不清理（锁定中的那张就是靠这个保住）
        if ($cur -and $_.FullName -ieq $cur) { $protected++; return }
        # 用户主动挑的历史图，按日期登记，永不被自动清理
        $dm = [regex]::Match($_.BaseName, '^(\d{8})_')
        if ($dm.Success -and $pinnedSet.ContainsKey($dm.Groups[1].Value)) { $protected++; return }
        $m = [regex]::Match($_.BaseName, '^(\d{8})_')
        $keep = $true
        if ($m.Success) {
            try { $d = [datetime]::ParseExact($m.Groups[1].Value, 'yyyyMMdd', $null); if ($d -lt $cut) { $keep = $false } } catch { }
        } elseif ($_.LastWriteTime -lt $cut) { $keep = $false }
        if (-not $keep) {
            if ($DryRun) { Write-Log "[DryRun] 将删除缓存 $($_.Name)" }
            else { [System.IO.File]::Delete($_.FullName); $removed++ }
        }
    }
    $script:PinProtected = $protected
    if ($removed -gt 0 -or $protected -gt 0) {
        Write-Log "清理缓存：删除 $removed 个（保留 $Days 天），保护 $protected 个"
    }

    # 同步清理索引里的失效条目（文件已不在的）
    if (-not $DryRun -and (Test-Path $IndexPath)) {
        try {
            $ciA = Get-Content $IndexPath -Raw -Encoding UTF8 | ConvertFrom-Json
            $tmpA = [ordered]@{}
            $changed = $false
            foreach ($q in $ciA.PSObject.Properties) {
                if (Test-Path (Join-Path $CacheDir ([string]$q.Value.file))) { $tmpA[$q.Name] = $q.Value }
                else { $changed = $true }
            }
            if ($changed) {
                [System.IO.File]::WriteAllText($IndexPath, ($tmpA | ConvertTo-Json -Depth 6), (New-Object System.Text.UTF8Encoding($false)))
                Write-Log '索引已同步，移除失效条目'
            }
        } catch { }
    }
    return $removed
}

# 归档图片总量超限时，从最旧的开始删；收藏过的、pinned 的、当前在用的都跳过
# 按指定方式删除文件：recycle 进回收站，permanent 彻底删
function Remove-OneFile {
    param([string]$Path, [string]$Mode = 'recycle')
    if (-not (Test-Path -LiteralPath $Path)) { return $false }
    if ($Mode -eq 'permanent') {
        try { [System.IO.File]::Delete($Path); return $true } catch { return $false }
    }
    try {
        [Microsoft.VisualBasic.FileIO.FileSystem]::DeleteFile(
            $Path,
            [Microsoft.VisualBasic.FileIO.UIOption]::OnlyErrorDialogs,
            [Microsoft.VisualBasic.FileIO.RecycleOption]::SendToRecycleBin,
            [Microsoft.VisualBasic.FileIO.UICancelOption]::DoNothing)
        return $true
    } catch {
        # 回收站不可用（被禁用/网络盘）时降级为直接删
        try { [System.IO.File]::Delete($Path); return $true } catch { return $false }
    }
}

function Clear-ArchiveBySize {
    param([int]$MaxMB)
    if ($MaxMB -le 0) { return 0 }
    if (-not (Test-Path $ArchiveImgDir)) { return 0 }

    $files = @(Get-ChildItem $ArchiveImgDir -Filter '*.jpg' -File -ErrorAction SilentlyContinue | Sort-Object Name)
    if ($files.Count -eq 0) { return 0 }
    $total = ($files | Measure-Object -Property Length -Sum).Sum
    $cap = [int64]$MaxMB * 1MB
    if ($total -le $cap) { return 0 }

    # 保护名单：收藏夹里的日期 + pinned 里的日期 + 当前壁纸文件
    $favDates = @{}
    Get-ChildItem $FavDir -Filter '*.jpg' -File -ErrorAction SilentlyContinue | ForEach-Object {
        if ($_.Name -match '^(\d{8})') { $favDates[$matches[1]] = $true }
    }
    $pinnedSet = Read-Pinned
    $curWp = ''
    try { $curWp = [string](Get-ItemProperty 'HKCU:\Control Panel\Desktop' -Name Wallpaper -ErrorAction SilentlyContinue).Wallpaper } catch { }

    $removed = 0
    $freed = 0
    $gone = @{}

    # 第一轮：先删「从没登记过」的，从旧到新；收藏与当前壁纸始终跳过
    foreach ($f in $files) {
        if ($total -le $cap) { break }
        $d = ''
        if ($f.Name -match '^(\d{8})') { $d = $matches[1] }
        if ($d -and $favDates.ContainsKey($d)) { continue }
        if ($d -and $pinnedSet.ContainsKey($d)) { continue }
        if ($curWp -and $curWp -eq $f.FullName) { continue }

        $szF = $f.Length
        if ($DryRun) {
            Write-Log "[DryRun] 归档超限，将删除 $($f.Name)"
            $removed++
            $freed += $szF
            $total -= $szF
            $gone[$f.FullName] = $true
        } else {
            try {
                [System.IO.File]::Delete($f.FullName)
                $removed++
                $freed += $szF
                $total -= $szF
                $gone[$f.FullName] = $true
            } catch {
                Write-Log ("归档清理失败：" + $f.Name + " - " + $_.Exception.Message) 'WARN'
            }
        }
    }

    # 第二轮：还没降到上限，才动「登记过」的（收藏与当前壁纸仍不动）
    if ($total -gt $cap) {
        foreach ($f in $files) {
            if ($total -le $cap) { break }
            if ($gone.ContainsKey($f.FullName)) { continue }
            $d = ''
            if ($f.Name -match '^(\d{8})') { $d = $matches[1] }
            if ($d -and $favDates.ContainsKey($d)) { continue }
            if ($curWp -and $curWp -eq $f.FullName) { continue }

            $szF = $f.Length
            if ($DryRun) {
                Write-Log "[DryRun] 归档超限（含已登记），将删除 $($f.Name)"
                $removed++
                $freed += $szF
                $total -= $szF
                $gone[$f.FullName] = $true
            } else {
                try {
                    [System.IO.File]::Delete($f.FullName)
                    $removed++
                    $freed += $szF
                    $total -= $szF
                    $gone[$f.FullName] = $true
                } catch {
                    Write-Log ("归档清理失败：" + $f.Name + " - " + $_.Exception.Message) 'WARN'
                }
            }
        }
    }

    if ($removed -gt 0) {
        $mode = if ($DryRun) { '[DryRun] ' } else { '' }
        Write-Log ($mode + '归档超限清理：删除 ' + $removed + ' 张，释放 ' + [math]::Round($freed / 1MB, 1) + ' MB，现存 ' + [math]::Round($total / 1MB, 1) + ' MB（上限 ' + $MaxMB + ' MB）')
    }
    if ($total -gt $cap) {
        Write-Log ('归档仍超限：' + [math]::Round($total / 1MB, 1) + ' MB（上限 ' + $MaxMB + ' MB），剩余全部受收藏或当前壁纸保护') 'WARN'
    }
    return $removed
}
function Clear-AllCache {
    param([string]$Mode = 'recycle')
    $cur = (Get-ItemProperty 'HKCU:\Control Panel\Desktop' -Name Wallpaper -ErrorAction SilentlyContinue).Wallpaper
    $removed = 0
    $kept = 0
    $freed = 0
    Get-ChildItem $CacheDir -Filter '*.jpg' -File -ErrorAction SilentlyContinue | ForEach-Object {
        if ($cur -and $_.FullName -ieq $cur) { $kept++; return }
        if ($DryRun) { Write-Log "[DryRun] 将删除 $($_.Name)"; $removed++; $freed += $_.Length; return }
        $sz = $_.Length
        if (Remove-OneFile -Path $_.FullName -Mode $Mode) { $removed++; $freed += $sz }
    }
    if (-not $DryRun) {
        # 同步清理 index.json 里指向已删文件的条目
        if (Test-Path $IndexPath) {
            try {
                $cat = Get-Content $IndexPath -Raw -Encoding UTF8 | ConvertFrom-Json
                $new = @{}
                foreach ($pp in $cat.PSObject.Properties) {
                    if (Test-Path (Join-Path $CacheDir $pp.Value.file)) { $new[$pp.Name] = $pp.Value }
                }
                [System.IO.File]::WriteAllText($IndexPath, ($new | ConvertTo-Json -Depth 6), (New-Object System.Text.UTF8Encoding($false)))
            } catch { }
        }
    }
    Write-Log "清空缓存：删除 $removed 个，保留当前壁纸 $kept 个"
    return $removed
}

# ---------------------------------------------------------------- 状态读写

function Read-State {
    if (Test-Path $StatePath) {
        try { return Get-Content $StatePath -Raw -Encoding UTF8 | ConvertFrom-Json } catch { }
    }
    return $null
}

function Write-State {
    param([string]$Date, [string]$File, [string]$Title, [string]$ImageId, [string]$AppliedDate,
          [string]$Copyright = '', [string]$SearchUrl = '')
    if ($script:NoApply) { return }
    $o = [ordered]@{
        date        = $Date
        file        = $File
        title       = $Title
        copyright   = $Copyright
        searchUrl   = $SearchUrl
        imageId     = $ImageId
        appliedDate = $AppliedDate
        updatedAt   = (Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
    }
    [System.IO.File]::WriteAllText($StatePath, ($o | ConvertTo-Json), (New-Object System.Text.UTF8Encoding($false)))
}

# ---------------------------------------------------------------- 主流程

# ---------------------------------------------------------------- 统一退出

# 所有退出都走这里：-Json 时输出结构化结果，否则静默退出
function Exit-Run {
    param([int]$Code)
    if ($Json) {
        $st = $null
        # 只有成功时才读 state；失败时 state 是上一次的图，不能冒充本次结果
        if ($Code -eq 0) { try { $st = Read-State } catch { } }
        $sizeMB = 0
        try {
            if ($st -and $st.file -and (Test-Path ([string]$st.file))) {
                $sizeMB = [math]::Round((Get-Item ([string]$st.file)).Length / 1MB, 2)
            }
        } catch { }
        $obj = [ordered]@{
            ok      = ($Code -eq 0)
            code    = $Code
            date    = $(if ($st) { [string]$st.date } else { '' })
            title   = $(if ($st) { [string]$st.title } else { '' })
            file    = $(if ($st) { [string]$st.file } else { '' })
            sizeMB  = $sizeMB
            imageId = $(if ($st) { [string]$st.imageId } else { '' })
        }
        try { Write-Output ($obj | ConvertTo-Json -Depth 4) } catch { }
    }
    exit $Code
}

# ---------------------------------------------------------------- 照片墙暂存 / 丢弃

if ($WallKeep) {
    if (-not (Test-Path $WallDir)) { Write-Host '照片墙临时目录是空的'; exit 0 }
    if (-not (Test-Path $ArchiveImgDir)) { New-Item -ItemType Directory -Path $ArchiveImgDir -Force | Out-Null }
    $movedW = 0
    foreach ($f in @(Get-ChildItem $WallDir -Filter '*.jpg' -File -ErrorAction SilentlyContinue)) {
        try {
            $destW = Join-Path $ArchiveImgDir $f.Name
            if (Test-Path -LiteralPath $destW) { $destW = Join-Path $ArchiveImgDir ($f.BaseName + '_' + (Get-Date -Format 'HHmmss') + '.jpg') }
            Move-Item -LiteralPath $f.FullName -Destination $destW -Force
            $mW = [regex]::Match($f.Name, '^(\d{8})_')
            if ($mW.Success) { Add-Pinned -Date $mW.Groups[1].Value }
            $movedW++
        } catch {
            Write-Log ("照片墙暂存失败：" + $f.Name + " " + $_.Exception.Message) 'WARN'
        }
    }
    Write-Host ("已暂存 $movedW 张到历史归档")
    exit 0
}

if ($WallDrop) {
    $droppedW = 0
    if (Test-Path $WallDir) {
        foreach ($f in @(Get-ChildItem $WallDir -Filter '*.jpg' -File -ErrorAction SilentlyContinue)) {
            try { Remove-Item -LiteralPath $f.FullName -Force -ErrorAction SilentlyContinue; $droppedW++ } catch { }
        }
    }
    Write-Host ("已丢弃 $droppedW 张照片墙临时图")
    exit 0
}

# ---------------------------------------------------------------- 重建缓存索引

if ($RebuildCacheIndex) {
    $catalog = @{}
    if (Test-Path $IndexPath) {
        try {
            $old = Get-Content $IndexPath -Raw -Encoding UTF8 | ConvertFrom-Json
            foreach ($pp in $old.PSObject.Properties) { $catalog[$pp.Name] = $pp.Value }
        } catch { }
    }
    $added = 0
    $kept = 0
    $skipped = 0
    foreach ($f in @(Get-ChildItem $CacheDir -Filter '*.jpg' -File -ErrorAction SilentlyContinue)) {
        $m = [regex]::Match($f.Name, '^(\d{8})_')
        if (-not $m.Success) { $skipped++; continue }
        $d8 = $m.Groups[1].Value
        if ($catalog.ContainsKey($d8) -and [string]$catalog[$d8].title) { $kept++; continue }
        $ttl = ''
        $cpr = ''
        $iid = ''
        try {
            $rec = Get-ArchiveRecord -Date $d8
            if ($rec) {
                $ttl = [string]$rec.title
                $cpr = [string]$rec.copyright
                $iid = [string]$rec.id
            }
        } catch { }
        $catalog[$d8] = [ordered]@{
            title     = $ttl
            titleEn   = ''
            copyright = $cpr
            file      = $f.Name
            imageId   = $iid
            sizeBytes = $f.Length
        }
        $added++
    }
    if ($DryRun) {
        Write-Host ("[DryRun] 需补写 $added 条，已有 $kept 条，跳过 $skipped 个文件")
        exit 0
    }
    [System.IO.File]::WriteAllText($IndexPath, ($catalog | ConvertTo-Json -Depth 6), (New-Object System.Text.UTF8Encoding($false)))
    Write-Host ("补写 $added 条，已有 $kept 条，跳过 $skipped 个文件")
    exit 0
}

# ---------------------------------------------------------------- 日期自查

if ($AuditDates) {
    $allArr = @()
    $oldArr = @()
    $wilArr = @()
    # PS 5.1 的 ConvertFrom-Json 在管道里不展开数组，必须先落变量再包装
    try { $tmpAll = Get-Content (Join-Path $ArchiveDir 'all-index.json') -Raw -Encoding UTF8 | ConvertFrom-Json; $allArr = @($tmpAll) } catch { }
    try { $tmpOld = Get-Content (Join-Path $ArchiveDir 'old-index.json') -Raw -Encoding UTF8 | ConvertFrom-Json; $oldArr = @($tmpOld) } catch { }
    try { $tmpWil = Get-Content (Join-Path $ArchiveDir 'wilii-index.json') -Raw -Encoding UTF8 | ConvertFrom-Json; $wilArr = @($tmpWil) } catch { }

    $allMap = @{}
    $dupAll = 0
    foreach ($e in $allArr) { $k = [string]$e.d; if ($k.Length -eq 8) { if ($allMap.ContainsKey($k)) { $dupAll++ }; $allMap[$k] = [string]$e.t } }
    $oldMap = @{}
    foreach ($e in $oldArr) { $k = [string]$e.d; if ($k.Length -eq 8) { $oldMap[$k] = [string]$e.t } }
    $wilMap = @{}
    foreach ($e in $wilArr) { $k = [string]$e.d; if ($k.Length -eq 8) { $wilMap[$k] = [string]$e.n } }

    Write-Host ('all-index {0} 条，old-index {1} 条，wilii-index {2} 条' -f $allMap.Count, $oldMap.Count, $wilMap.Count)

    # 显式 foreach：Hashtable.Keys 在管道里会被当成一个集合对象，Where-Object 只求值一次
    $below = 0
    foreach ($k in $allMap.Keys) { if ($k -lt $ArchiveMinDate) { $below++ } }
    foreach ($k in $oldMap.Keys) { if ($k -lt $ArchiveMinDate) { $below++ } }
    foreach ($k in $wilMap.Keys) { if ($k -lt $ArchiveMinDate) { $below++ } }
    Write-Host ('早于下界 {0} 的条目：{1} 个' -f $ArchiveMinDate, $below)

    $onlyAll = New-Object System.Collections.ArrayList
    foreach ($k in $allMap.Keys) { if (-not $wilMap.ContainsKey($k)) { [void]$onlyAll.Add($k) } }
    $onlyWil = New-Object System.Collections.ArrayList
    foreach ($k in $wilMap.Keys) { if (-not $allMap.ContainsKey($k)) { [void]$onlyWil.Add($k) } }
    Write-Host ('只在 all-index 里：{0} 个；只在 wilii-index 里：{1} 个' -f $onlyAll.Count, $onlyWil.Count)
    if ($onlyAll.Count -gt 0) { Write-Host ('  样本: ' + (($onlyAll | Sort-Object | Select-Object -First 5) -join ', ')) }
    if ($onlyWil.Count -gt 0) { Write-Host ('  样本: ' + (($onlyWil | Sort-Object | Select-Object -First 5) -join ', ')) }

    $conflict = 0
    $samples = New-Object System.Collections.ArrayList
    foreach ($f in @(Get-ChildItem $ArchiveDir -Filter '20??-??.json' -File -ErrorAction SilentlyContinue)) {
        try {
            $mu = Get-Content $f.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
            foreach ($pp in $mu.PSObject.Properties) {
                $d8 = [string]$pp.Name
                $mt = [string]$pp.Value.t
                $at = ''
                if ($allMap.ContainsKey($d8)) { $at = [string]$allMap[$d8] }
                if ($mt -and $at -and $at -ne $mt) {
                    $conflict++
                    if ($samples.Count -lt 5) { [void]$samples.Add(($d8 + ' 月份[' + $mt + '] 全量[' + $at + ']')) }
                }
            }
        } catch { }
    }
    Write-Host ('月份缓存与全量索引的标题冲突：{0} 条' -f $conflict)
    foreach ($s in $samples) { Write-Host ('  ' + $s) }

    Write-Host ('全量索引重复日期：{0} 组' -f $dupAll)
    Write-Host '自查完成（只读，未改动任何文件）'
    exit 0
}


# ---------------------------------------------------------------- 老索引体检

if ($AuditOld) {
    $oo = @()
    try {
        $tmpO = Get-Content (Join-Path $ArchiveDir 'old-index.json') -Raw -Encoding UTF8 | ConvertFrom-Json
        $oo = @($tmpO)
    } catch { }
    if ($oo.Count -eq 0) { Write-Host '老索引读不出来'; exit 1 }
    $ds = @()
    $nm = @{}
    $emptyT = 0
    $outOfRange = 0
    foreach ($e in $oo) {
        $d = [string]$e.d
        if ($d.Length -ne 8) { continue }
        $ds += $d
        if ($d -lt $ArchiveMinDate -or $d -gt '20160304') { $outOfRange++ }
        if (-not [string]$e.t) { $emptyT++ }
        $n8 = [string]$e.n
        if ($n8) {
            if ($nm.ContainsKey($n8)) { $nm[$n8] = $nm[$n8] + ',' + $d } else { $nm[$n8] = $d }
        }
    }
    $dsSorted = @($ds | Sort-Object -Unique)
    Write-Host ('老索引 {0} 条，范围 {1} ~ {2}' -f $dsSorted.Count, $dsSorted[0], $dsSorted[-1])
    $startD = [datetime]::ParseExact('20090712', 'yyyyMMdd', $null)
    $endD = [datetime]::ParseExact('20160304', 'yyyyMMdd', $null)
    $need = [int]($endD - $startD).TotalDays + 1
    Write-Host ('应有 {0} 天，缺 {1} 天' -f $need, ($need - $dsSorted.Count))
    Write-Host ('标题为空 {0} 条；日期越界 {1} 条' -f $emptyT, $outOfRange)
    # 已知的「同一张图被必应发布两次」名单。逐个回查过 wilii 原始日表：这四组在 wilii 里
    # 本来就占了两个不同日期，是必应自己的重发（部分还改了标题），不是索引标错。
    # 名单外的同图多日期才是要人工过目的新冲突。改动时与 test.ps1 的白名单保持一致。
    $KnownDupFiles = @(
        'Cuscus_ZH-CN2223740503_1920x1080.jpg',
        'TurtleHatchling_ZH-CN407919738_1920x1080.jpg',
        'DivingPlatform_ZH-CN1344034380_1920x1080.jpg',
        'LondonAquaticsCentre_ZH-CN1563351439_1920x1080.jpg'
    )
    $known = @()
    $fresh = @()
    foreach ($k in $nm.Keys) {
        if ($nm[$k] -notmatch ',') { continue }
        $line = $k + ' → ' + $nm[$k]
        if ($KnownDupFiles -contains $k) { $known += $line } else { $fresh += $line }
    }
    Write-Host ('同一张图出现在多个日期：{0} 组（已知重复发布 {1} 组 / 新增冲突 {2} 组）' -f ($known.Count + $fresh.Count), $known.Count, $fresh.Count)
    foreach ($x in ($known | Select-Object -First 8)) { Write-Host ('  [已知] ' + $x) }
    foreach ($x in ($fresh | Select-Object -First 8)) { Write-Host ('  [新增] ' + $x) -ForegroundColor Yellow }
    $segCnt = @($dsSorted | Where-Object { $_ -ge '20090712' -and $_ -le '20141231' }).Count
    $segNeed = [int](([datetime]::ParseExact('20141231', 'yyyyMMdd', $null) - [datetime]::ParseExact('20090712', 'yyyyMMdd', $null)).TotalDays) + 1
    Write-Host ('2009-07-12 ~ 2014-12-31：{0} 条 / 应有 {1} 天 / 缺 {2} 天' -f $segCnt, $segNeed, ($segNeed - $segCnt))
    Write-Host '体检完成（只读）'
    exit 0
}

# ---------------------------------------------------------------- 修历史收藏的日期



if ($FixLegacyFav -or $FixLegacyDates) {
    # 名字以日期开头的文件，靠「slug → 新索引日期」纠正；只有日期的（故事缓存、保留名单）靠旧索引反查 slug
    $newBySlug = @{}
    $oo2 = @()
    try {
        $tmpO2 = Get-Content (Join-Path $ArchiveDir 'old-index.json') -Raw -Encoding UTF8 | ConvertFrom-Json
        $oo2 = @($tmpO2)
    } catch { }
    if ($oo2.Count -eq 0) { Write-Host '老索引读不出来，先正常启动一次让它从 exe 释放'; exit 1 }
    foreach ($e in $oo2) {
        $n8 = [string]$e.n
        if (-not $n8) { continue }
        $key = $n8 -replace '\.jpg$', ''
        if (-not $key) { continue }
        if (-not $newBySlug.ContainsKey($key)) { $newBySlug[$key] = New-Object System.Collections.ArrayList }
        [void]$newBySlug[$key].Add([string]$e.d)
    }

    # 同一张图可能被必应在两个日期各发布一次，取离原日期最近的那个
    function Get-NearestNewDate([string]$key, [string]$refD) {
        $ds = @($newBySlug[$key])
        if ($ds.Count -eq 0) { return '' }
        if ($ds.Count -eq 1) { return [string]$ds[0] }
        $ref = [datetime]::ParseExact($refD, 'yyyyMMdd', $null)
        $best = ''
        $bestDiff = [double]::MaxValue
        foreach ($dd in $ds) {
            $t = [datetime]::ParseExact([string]$dd, 'yyyyMMdd', $null)
            $diff = [math]::Abs(($t - $ref).TotalDays)
            if ($diff -lt $bestDiff) { $bestDiff = $diff; $best = [string]$dd }
        }
        return $best
    }

    $oldIdxPath = Join-Path $Root '_old-index-v3637.json'
    $oldSlugByDate = @{}
    if (Test-Path $oldIdxPath) {
        try {
            $oldArr = @(Get-Content $oldIdxPath -Raw -Encoding UTF8 | ConvertFrom-Json)
            foreach ($e in $oldArr) {
                $n8 = [string]$e.n
                if (-not $n8) { continue }
                $oldSlugByDate[[string]$e.d] = ($n8 -replace '\.jpg$', '')
            }
        } catch { }
    }

    $logPath = Join-Path $Root '_rename-log.txt'
    $log = New-Object System.Collections.ArrayList
    $fixed = 0
    $scanned = 0
    $skipped = 0

    # 1) 收藏 / 缓存 / 归档老图：文件名 = 日期_slug.jpg
    $jpgDirs = @($FavDir, $CacheDir)
    $oldRoot = Join-Path $ArchiveDir 'old'
    if (Test-Path $oldRoot) {
        foreach ($md in @(Get-ChildItem $oldRoot -Directory -ErrorAction SilentlyContinue)) { $jpgDirs += $md.FullName }
    }
    foreach ($dir in $jpgDirs) {
        if (-not (Test-Path $dir)) { continue }
        foreach ($f in @(Get-ChildItem $dir -Filter '*.jpg' -File -ErrorAction SilentlyContinue)) {
            $scanned++
            $m2 = [regex]::Match($f.Name, '^(\d{8})_(.+)\.jpg$')
            if (-not $m2.Success) { continue }
            $curD = $m2.Groups[1].Value
            $slug = $m2.Groups[2].Value
            # 归档老图文件名不带分辨率后缀，收藏/缓存带，两种都要能查到
            $key = $slug
            if (-not $newBySlug.ContainsKey($key)) { $key = $slug + '_1920x1080' }
            if (-not $newBySlug.ContainsKey($key)) { $skipped++; continue }
            $rightD = Get-NearestNewDate $key $curD
            if ($rightD.Length -ne 8 -or $rightD -eq $curD) { continue }
            $newName = $rightD + '_' + $slug + '.jpg'
            $destDir = $dir
            if ($dir.StartsWith($oldRoot)) { $destDir = Join-Path $oldRoot $rightD.Substring(0, 6) }
            $newPath = Join-Path $destDir $newName
            if (Test-Path -LiteralPath $newPath) { $skipped++; continue }
            $relOld = $f.FullName.Replace($Root + '\', '')
            $relNew = $newPath.Replace($Root + '\', '')
            if ($DryRun) {
                Write-Host ('[DryRun] 改名：' + $relOld + '  →  ' + $relNew)
                [void]$log.Add($relOld + '  ->  ' + $relNew)
            } else {
                if (-not (Test-Path $destDir)) { New-Item -ItemType Directory -Path $destDir -Force | Out-Null }
                try {
                    Move-Item -LiteralPath $f.FullName -Destination $newPath -Force
                    [void]$log.Add($relOld + '  ->  ' + $relNew)
                } catch {
                    Write-Host ('改名失败：' + $f.Name + ' ' + $_.Exception.Message) -ForegroundColor Yellow
                    continue
                }
            }
            $fixed++
        }
    }

    # 2) 每日故事缓存：文件名 = 日期.txt，没有 slug，靠旧索引反查
    $storyDir = Join-Path $ArchiveDir 'stories'
    if ((Test-Path $storyDir) -and $oldSlugByDate.Count -gt 0) {
        foreach ($f in @(Get-ChildItem $storyDir -Filter '*.txt' -File -ErrorAction SilentlyContinue)) {
            $scanned++
            $oldD = $f.BaseName
            if ($oldD -notmatch '^\d{8}$') { continue }
            if (-not $oldSlugByDate.ContainsKey($oldD)) { $skipped++; continue }
            $slug = $oldSlugByDate[$oldD]
            if (-not $newBySlug.ContainsKey($slug)) { $skipped++; continue }
            $rightD = Get-NearestNewDate $slug $oldD
            if ($rightD.Length -ne 8 -or $rightD -eq $oldD) { continue }
            $newPath = Join-Path $storyDir ($rightD + '.txt')
            if (Test-Path -LiteralPath $newPath) { $skipped++; continue }
            if ($DryRun) {
                Write-Host ('[DryRun] 故事：' + $f.Name + '  →  ' + ($rightD + '.txt'))
                [void]$log.Add($f.Name + '  ->  ' + ($rightD + '.txt'))
            } else {
                try {
                    Move-Item -LiteralPath $f.FullName -Destination $newPath -Force
                    [void]$log.Add($f.Name + '  ->  ' + ($rightD + '.txt'))
                } catch { continue }
            }
            $fixed++
        }
    }

    # 3) 保留名单：pinned.json 的键是日期
    if ((Test-Path $PinnedPath) -and $oldSlugByDate.Count -gt 0) {
        try {
            $pinRaw = Get-Content $PinnedPath -Raw -Encoding UTF8 | ConvertFrom-Json
            $pinNew = [ordered]@{}
            $pinChanged = 0
            $pinDrop = 0
            foreach ($p in $pinRaw.PSObject.Properties) {
                $k = $p.Name
                $rightD = $k
                if ($k -match '^\d{8}$' -and $oldSlugByDate.ContainsKey($k)) {
                    $slug = $oldSlugByDate[$k]
                    if ($newBySlug.ContainsKey($slug)) { $rightD = Get-NearestNewDate $slug $k }
                }
                if ($pinNew.Contains($rightD)) { $pinDrop++; continue }
                $pinNew[$rightD] = $true
                if ($rightD -ne $k) { $pinChanged++ }
            }
            if ($pinChanged -gt 0 -or $pinDrop -gt 0) {
                if ($DryRun) {
                    Write-Host ('[DryRun] 保留名单：改键 ' + $pinChanged + ' 个，合并重复 ' + $pinDrop + ' 个')
                } else {
                    ($pinNew | ConvertTo-Json) | Set-Content $PinnedPath -Encoding UTF8
                    [void]$log.Add('pinned.json  改键 ' + $pinChanged + ' 个，合并重复 ' + $pinDrop + ' 个')
                }
                $fixed += $pinChanged
            }
        } catch {
            Write-Host '保留名单改写失败，已跳过' -ForegroundColor Yellow
        }
    }

    if (-not $DryRun -and $log.Count -gt 0) { $log | Set-Content $logPath -Encoding UTF8 }
    Write-Host ('扫描 ' + $scanned + ' 个文件，修正 ' + $fixed + ' 个日期' + $(if ($DryRun) { '（DryRun：本次不改任何文件）' } else { '' }))
    if ($skipped -gt 0) { Write-Host ('跳过 ' + $skipped + ' 个：新索引里查不到 slug  或  目标文件名已存在') -ForegroundColor DarkGray }
    if (-not (Test-Path $oldIdxPath)) { Write-Host '提示：缺 _old-index-v3637.json，故事缓存与保留名单未参与' -ForegroundColor DarkGray }
    if (-not $DryRun) { Write-Host ('改名记录：' + $logPath) -ForegroundColor DarkGray }
    exit 0
}


# ---------------------------------------------------------------- 状态查询

if ($Status) {
    # 版本号从 exe 读，避免和 src/Program.cs 里的 AssemblyVersion 两处维护
    $appVer = '0.0.0'
    $exeForVer = Join-Path $Root 'BingWallpaperLite.exe'
    if (Test-Path $exeForVer) {
        try {
            $fv = (Get-Item $exeForVer).VersionInfo.FileVersion
            if ($fv) { $appVer = ($fv -replace '\.0$', '') }
        } catch { }
    }
    if ($appVer -eq '0.0.0') { $appVer = '3.2.2' }   # exe 不在时的兜底

    $st = Read-State
    $cfgNow = $config
    $idxCount = 0
    $idxAgeDays = -1
    if (Test-Path $AllIndexPath) {
        try {
            $idxTmp = Get-Content $AllIndexPath -Raw -Encoding UTF8 | ConvertFrom-Json
            $idxCount = @($idxTmp).Count
            $idxAgeDays = [math]::Round(((Get-Date) - (Get-Item $AllIndexPath).LastWriteTime).TotalDays, 2)
        } catch { }
    }
    $favCount = @(Get-ChildItem $FavDir -Filter '*.jpg' -File -ErrorAction SilentlyContinue).Count
    $cacheCount = @(Get-ChildItem $CacheDir -Filter '*.jpg' -File -ErrorAction SilentlyContinue).Count
    $archCount = @(Get-ChildItem $ArchiveImgDir -Filter '*.jpg' -File -ErrorAction SilentlyContinue).Count

    $obj = [ordered]@{
        current = [ordered]@{
            date      = $(if ($st) { [string]$st.date } else { '' })
            title     = $(if ($st) { [string]$st.title } else { '' })
            file      = $(if ($st) { [string]$st.file } else { '' })
            appliedAt = $(if ($st) { [string]$st.updatedAt } else { '' })
        }
        counts = [ordered]@{
            cache     = $cacheCount
            favorites = $favCount
            archive   = $archCount
        }
        config = [ordered]@{
            locked     = [bool]$cfgNow['locked']
            archive    = [bool]$cfgNow['archive']
            keepDays   = [int]$cfgNow['keepDays']
            autoClean  = [bool]$cfgNow['autoClean']
            updateTime = $(if ($cfgNow['updateTime']) { [string]$cfgNow['updateTime'] } else { '00:10' })
            resolution = [string]$cfgNow['resolution']
            style      = [string]$cfgNow['wallpaperStyle']
        }
        index = [ordered]@{
            cached   = ($idxCount -gt 0)
            entries  = $idxCount
            ageDays  = $idxAgeDays
            minDate  = $ArchiveMinDate
        }
        version = $appVer
    }
    $txt = $obj | ConvertTo-Json -Depth 5
    if ($Json) {
        Write-Output $txt
    } else {
        Write-Output ('当前：' + $obj.current.title + '（' + $obj.current.date + '）')
        Write-Output ('收藏 ' + $favCount + ' / 缓存 ' + $cacheCount + ' / 归档 ' + $archCount)
        Write-Output ('索引 ' + $idxCount + ' 条，' + $idxAgeDays + ' 天前')
    }
    # -Status 自己已输出结果，直接退出，不走 Exit-Run（否则 -Json 会再输出一段）
    exit 0
}

$locked = $false


if ($PickRandom -gt 0) {
    # 从全量索引里随机抽 N 条，输出格式与 -Query 完全一致
    $idx = @()
    try { $idx = @(Get-FullIndex) } catch { }
    if ($idx.Count -eq 0) {
        Write-Log '索引为空，无法随机抽取' 'WARN'
        Write-Output '[]'
        Exit-Run 5
    }
    $n = [Math]::Min($PickRandom, $idx.Count)
    $picked = @($idx | Get-Random -Count $n)

    $parts = New-Object System.Collections.ArrayList
    foreach ($h in $picked) {
        $t = ([string]$h.t) -replace '\\', '\\\\' -replace '"', '\"'
        $c = ([string]$h.c) -replace '\\', '\\\\' -replace '"', '\"'
        [void]$parts.Add('  {"date":"' + ([string]$h.d) + '","title":"' + $t + '","copyright":"' + $c + '","matched":[]}')
    }
    $jsonText = "[" + [Environment]::NewLine + ($parts -join ("," + [Environment]::NewLine)) + [Environment]::NewLine + "]"
    Write-Log ("随机抽取 " + $n + " 条")
    Write-Output $jsonText
    exit 0
}

if ($DateInfo) {
    # 按日期查标题：只读索引，不下载不换图
    $d8 = ([string]$DateInfo) -replace '[^0-9]', ''
    if ($d8.Length -ne 8) { Write-Output '{}'; Exit-Run 1 }

    $idx = @()
    try { $idx = @(Get-FullIndex) } catch { }
    $hit = @($idx | Where-Object { [string]$_.d -eq $d8 }) | Select-Object -First 1
    if (-not $hit) { Write-Output '{}'; Exit-Run 5 }

    $t = ([string]$hit.t) -replace '\\', '\\\\' -replace '"', '\"'
    $c = ([string]$hit.c) -replace '\\', '\\\\' -replace '"', '\"'
    Write-Output ('{"date":"' + $d8 + '","title":"' + $t + '","copyright":"' + $c + '"}')
    exit 0
}

if ($UpdateIndex) {
    # 强制刷新全量索引，供「归档 → 更新索引」菜单调用
    try {
        $idx = @(Get-FullIndex -Force)
        $n = $idx.Count
        Write-Log ("索引已更新，共 " + $n + " 条")
        # 兜底索引用同一入口增量刷新，失败不影响主索引结果
        try {
            $addedW = Update-WiliiIndex
            Write-Log ("兜底索引已刷新，新增 " + $addedW + " 天")
        } catch {
            Write-Log ("兜底索引刷新失败：" + $_.Exception.Message) 'WARN'
        }
        Write-Output ('{"ok":true,"entries":' + $n + '}')
        exit 0
    } catch {
        Write-Log ("索引更新失败：" + $_.Exception.Message) 'ERROR'
        Write-Output ('{"ok":false,"error":"' + (($_.Exception.Message) -replace '"', '\"') + '"}')
        Exit-Run 7
    }
}

if ($FavDate) {
    $listF = @(([string]$FavDate) -split ',' |
        ForEach-Object { $_ -replace '[^0-9]', '' } |
        Where-Object { $_.Length -eq 8 })
    if ($listF.Count -eq 0) { Write-Output '{"ok":0,"skip":0,"fail":0,"error":"badarg"}'; Exit-Run 1 }

    $okF = 0
    $skipF = 0
    $failF = 0
    foreach ($d8f in $listF) {
        if (Test-Path $FavDir) {
            $hadF = @(Get-ChildItem $FavDir -Filter "${d8f}_*.jpg" -File -ErrorAction SilentlyContinue) | Select-Object -First 1
            if ($hadF) { $skipF++; continue }
        }
        $srcF = $null
        foreach ($dirF in @($CacheDir, $ArchiveImgDir)) {
            if ($srcF) { break }
            if (-not (Test-Path $dirF)) { continue }
            $hitF = @(Get-ChildItem $dirF -Filter "${d8f}_*.jpg" -File -ErrorAction SilentlyContinue) | Select-Object -First 1
            if ($hitF) { $srcF = $hitF.FullName }
        }
        if (-not $srcF) { $failF++; continue }
        try { Move-ToFavorite -SrcPath $srcF; $okF++ } catch { $failF++ }
    }
    Write-Output ('{"ok":' + $okF + ',"skip":' + $skipF + ',"fail":' + $failF + '}')
    exit 0
}

if ($UnfavDate) {
    $listU = @(([string]$UnfavDate) -split ',' |
        ForEach-Object { $_ -replace '[^0-9]', '' } |
        Where-Object { $_.Length -eq 8 })
    if ($listU.Count -eq 0) { Write-Output '{"ok":0,"skip":0,"fail":0,"error":"badarg"}'; Exit-Run 1 }

    $okU = 0
    $skipU = 0
    $failU = 0
    foreach ($d8u in $listU) {
        $srcU = $null
        if (Test-Path $FavDir) {
            $hitU = @(Get-ChildItem $FavDir -Filter "${d8u}_*.jpg" -File -ErrorAction SilentlyContinue) | Select-Object -First 1
            if ($hitU) { $srcU = $hitU.FullName }
        }
        if (-not $srcU) { $skipU++; continue }
        try { Move-ToCache -SrcPath $srcU; $okU++ } catch { $failU++ }
    }
    Write-Output ('{"ok":' + $okU + ',"skip":' + $skipU + ',"fail":' + $failU + '}')
    exit 0
}

if ($ExportLinks) {
    $d1L = ([string]$RangeFrom) -replace '[^0-9]', ''
    $d2L = ([string]$RangeTo)   -replace '[^0-9]', ''
    if ($d1L.Length -ne 8 -or $d2L.Length -ne 8) { Write-Output '{"ok":0,"miss":0,"links":[],"error":"badarg"}'; Exit-Run 1 }
    if ($d1L -lt $ArchiveMinDate) { $d1L = $ArchiveMinDate }
    $todayL = (Get-Date).ToString('yyyyMMdd')
    if ($d2L -gt $todayL) { $d2L = $todayL }
    if ($d1L -gt $d2L) { Write-Output '{"ok":0,"miss":0,"links":[],"error":"range"}'; Exit-Run 1 }

    $idxL = @()
    try { $idxL = @(Get-FullIndex) } catch { }
    $byDateL = @{}
    foreach ($it in $idxL) { $byDateL[[string]$it.d] = $it }

    $linksL = @()
    $missL = 0
    $dayL = [datetime]::ParseExact($d1L, 'yyyyMMdd', $null)
    $endL = [datetime]::ParseExact($d2L, 'yyyyMMdd', $null)
    while ($dayL -le $endL) {
        $curL = $dayL.ToString('yyyyMMdd')
        $dayL = $dayL.AddDays(1)
        $recL = $byDateL[$curL]
        if (-not $recL -or -not $recL.i) { $missL++; continue }
        $linksL += ('https://www.bing.com/th?id=' + [string]$recL.i + '_UHD.jpg')
    }
    $arrL = (($linksL | ForEach-Object { '"' + $_ + '"' }) -join ',')
    Write-Output ('{"ok":' + $linksL.Count + ',"miss":' + $missL + ',"links":[' + $arrL + ']}')
    exit 0
}

if ($ExportRange) {
    # 按日期范围批量下载：目标目录已有同名则跳过，索引里没有记失败
    $d1 = ([string]$RangeFrom) -replace '[^0-9]', ''
    $d2 = ([string]$RangeTo)   -replace '[^0-9]', ''
    if ($d1.Length -ne 8 -or $d2.Length -ne 8) {
        Write-Output '{"ok":0,"skip":0,"fail":0,"error":"日期格式错误"}'
        Exit-Run 1
    }
    if ($d1 -lt $ArchiveMinDate) { $d1 = $ArchiveMinDate }
    $todayD = (Get-Date).ToString('yyyyMMdd')
    if ($d2 -gt $todayD) { $d2 = $todayD }
    if ($d1 -gt $d2) {
        Write-Output '{"ok":0,"skip":0,"fail":0,"error":"起始日期晚于结束日期"}'
        Exit-Run 1
    }
    if (-not $DestDir) {
        Write-Output '{"ok":0,"skip":0,"fail":0,"error":"未指定下载目录"}'
        Exit-Run 1
    }
    try {
        if (-not (Test-Path $DestDir)) { New-Item -ItemType Directory -Path $DestDir -Force | Out-Null }
    } catch {
        Write-Output '{"ok":0,"skip":0,"fail":0,"error":"目录不可写"}'
        Exit-Run 1
    }

    $idx = @()
    try { $idx = @(Get-FullIndex) } catch { }
    $byDate = @{}
    foreach ($it in $idx) { $byDate[[string]$it.d] = $it }

    $okN = 0
    $skipN = 0
    $failN = 0
    $firstErr = ''

    $day = [datetime]::ParseExact($d1, 'yyyyMMdd', $null)
    $end = [datetime]::ParseExact($d2, 'yyyyMMdd', $null)
    while ($day -le $end) {
        $cur = $day.ToString('yyyyMMdd')
        $day = $day.AddDays(1)

        $had = @(Get-ChildItem $DestDir -Filter "${cur}_*.jpg" -File -ErrorAction SilentlyContinue) | Select-Object -First 1
        if ($had) { $skipN++; continue }

        $rec = $byDate[$cur]
        if (-not $rec) {
            $failN++
            if (-not $firstErr) { $firstErr = "$cur 索引里没有这一天" }
            continue
        }
        $imgId = [string]$rec.i
        if (-not $imgId) {
            $failN++
            if (-not $firstErr) { $firstErr = "$cur 缺少 imageId" }
            continue
        }

        $shortId = $imgId -replace '_[A-Za-z]{2}-[A-Za-z]{2}\d+$', ''
        $destFile = Join-Path $DestDir "${cur}_$shortId.jpg"
        try {
            [void](Save-BingImage -Url "https://www.bing.com/th?id=${imgId}_UHD.jpg" -Dest $destFile -MinKB 300)
            $okN++
        } catch {
            $failN++
            if (-not $firstErr) { $firstErr = ($_.Exception.Message) -replace '"', "'" }
        }
        Start-Sleep -Milliseconds 350
    }

    Write-Log ("批量下载完成：成功 $okN，跳过 $skipN，失败 $failN")
    Write-Output ('{"ok":' + $okN + ',"skip":' + $skipN + ',"fail":' + $failN + ',"error":"' + $firstErr + '"}')
    exit 0
}

    if ($PSBoundParameters.ContainsKey('Query') -or $PSBoundParameters.ContainsKey('Search') -or $KwFile) {
    # ---- 关键词搜索
        # 关键词来源：-FindFile（UTF-8 文件，最可靠）> -Find > -Search
        $kw = ''
        if ($KwFile) {
            if (-not (Test-Path $KwFile)) { throw "关键词文件不存在: $KwFile" }
            $kw = (Get-Content $KwFile -Raw -Encoding UTF8).Trim()
        } elseif ($Query) {
            $kw = $Query.ToString().Trim()
        } else {
            $kw = $Search.ToString().Trim()
        }
        if (-not $kw) {
            Write-Log '关键词为空' 'ERROR'
            Exit-Run 1
        }
        try {
            $hits = Search-Archive -Keywords $kw -Limit 30
        } catch {
            Write-Log ("搜索失败: " + $_.Exception.Message) 'ERROR'
            Exit-Run 7
        }
        if ($hits.Count -eq 0) {
            Write-Log ("没有匹配「" + $kw + "」的壁纸") 'WARN'
            Write-Output '[]'
            Exit-Run 5
        }
        Write-Log ("搜索「" + $kw + "」命中 " + $hits.Count + " 条")
        if (($Query -or $KwFile) -and -not $Search) {
            # 手工拼 JSON，避免 ConvertTo-Json 在字典数组上的类型问题
            $parts = New-Object System.Collections.ArrayList
            foreach ($h in $hits) {
                $t = ([string]$h.title) -replace '\\', '\\\\' -replace '"', '\"'
                $c = ([string]$h.copyright) -replace '\\', '\\\\' -replace '"', '\"'
                $ms = @()
                foreach ($m in @($h.matched)) {
                    $ms += '"' + (([string]$m) -replace '"', '\"') + '"'
                }
                [void]$parts.Add('  {"date":"' + ([string]$h.date) + '","title":"' + $t + '","copyright":"' + $c + '","matched":[' + ($ms -join ',') + ']}')
            }
            $jsonText = "[" + [Environment]::NewLine + ($parts -join ("," + [Environment]::NewLine)) + [Environment]::NewLine + "]"
            try {
                $outFile = Join-Path $env:TEMP 'bwl-output.json'
                [System.IO.File]::WriteAllText($outFile, $jsonText, ([System.Text.UTF8Encoding]::new($false)))
            } catch { }
            Write-Output $jsonText
            exit 0
        }
        $Date = [string]$hits[0].date
        Write-Log ("搜索命中，换这张: " + $Date + "  " + $hits[0].title)
    }
try {
    if ($CleanAll) {
        Write-Log '--- 清空缓存（保留当前壁纸）---'
        $allN = [int](Clear-AllCache -Mode $CleanMode)
        Write-Output ('{"removed":' + $allN + ',"protected":' + $kept + '}')
        Exit-Run 0
    }

    # ---- 收藏 / 取消收藏
    if ($Favorite -or $Unfavorite) {
        $cur = (Get-ItemProperty 'HKCU:\Control Panel\Desktop' -Name Wallpaper -ErrorAction SilentlyContinue).Wallpaper
        if (-not $cur -or -not (Test-Path $cur)) {
            Write-Log '当前没有可收藏的壁纸' 'ERROR'
            Exit-Run 1
        }
        $name = Split-Path $cur -Leaf
        $srcDir = (Split-Path $cur -Parent).TrimEnd('\')
        # 画廊里的图也算缓存：看完想留下就收藏，收藏后退出画廊不会被清
        $inGallery = ($srcDir -ieq $GalleryDir.TrimEnd('\'))
        $inCache = ($srcDir -ieq $CacheDir.TrimEnd('\')) -or $inGallery
        $inFav   = ($srcDir -ieq $FavDir.TrimEnd('\'))
        if ($Favorite -and -not $inCache) {
            Write-Log "只有缓存或画廊里的壁纸才能收藏：$cur" 'ERROR'
            Exit-Run 3
        }
        if ($Unfavorite -and -not $inFav) {
            Write-Log "当前壁纸不在收藏夹里，无需取消：$cur" 'ERROR'
            Exit-Run 3
        }

        if (-not (Test-Path $FavDir)) { New-Item -ItemType Directory -Path $FavDir -Force | Out-Null }
        $dest = Join-Path $FavDir $name

        # 读收藏索引
        $favIdx = @{}
        if (Test-Path $FavIndex) {
            try {
                $fi = Get-Content $FavIndex -Raw -Encoding UTF8 | ConvertFrom-Json
                foreach ($q in $fi.PSObject.Properties) { $favIdx[$q.Name] = $q.Value }
            } catch { }
        }
        $dateKey = ($name -split '_')[0]

        if ($Favorite) {
            # 正常模式移动而非复制：一份文件，缓存清理碰不到收藏
            # 接管模式只复制：root 里的原图一根手指都不能动
            if ($script:Takeover) { Copy-Item -LiteralPath $cur -Destination $dest -Force }
            else { Move-Item -LiteralPath $cur -Destination $dest -Force }
            # 从缓存索引捞元信息
            $meta = $null
            if (Test-Path $IndexPath) {
                try {
                    $ci = Get-Content $IndexPath -Raw -Encoding UTF8 | ConvertFrom-Json
                    if ($ci.PSObject.Properties.Name -contains $dateKey) { $meta = $ci.$dateKey }
                } catch { }
            }
            # 缓存索引里查不到就退回接口再查一次，别写空标题
            if (-not $meta -or [string]$meta.title -eq '') {
                try {
                    $l8obj = (Get-WallpaperListJson -Count 8) | ConvertFrom-Json
                    $l8 = @($l8obj)
                    $hit8 = $l8 | Where-Object { [string]$_.startdate -eq $dateKey } | Select-Object -First 1
                    if ($hit8) {
                        $zhMap = Get-ZhTitleMap
                        $bid = ([string]$hit8.urlbase) -replace '.*id=', '' -replace '_UHD\.jpg$', '' -replace '_[A-Za-z]{2}-[A-Za-z]{2}\d+$', ''
                        $zt = [string]$hit8.title
                        if ($zhMap.ContainsKey($bid)) { $zt = $zhMap[$bid] }
                        $meta = [pscustomobject]@{
                            title     = $zt
                            titleEn   = [string]$hit8.title
                            copyright = [string]$hit8.copyright
                            imageId   = $bid
                        }
                    }
                } catch { }
            }
            # 还查不到就用 state 里的记录兜底
            if ((-not $meta -or [string]$meta.title -eq '') -and $state -and [string]$state.date -eq $dateKey) {
                $meta = [pscustomobject]@{
                    title     = [string]$state.title
                    titleEn   = ''
                    copyright = ''
                    imageId   = [string]$state.imageId
                }
            }

            if ($meta -and [string]$meta.title -ne '') {
                $favIdx[$dateKey] = [ordered]@{
                    title     = [string]$meta.title
                    titleEn   = [string]$meta.titleEn
                    copyright = [string]$meta.copyright
                    file      = $name
                    imageId   = [string]$meta.imageId
                    sizeBytes = (Get-Item $dest).Length
                }
            } else {
                $favIdx[$dateKey] = [ordered]@{
                    title     = ''
                    titleEn   = ''
                    copyright = ''
                    file      = $name
                    imageId   = ''
                    sizeBytes = (Get-Item $dest).Length
                }
            }
            [System.IO.File]::WriteAllText($FavIndex, ($favIdx | ConvertTo-Json -Depth 6), (New-Object System.Text.UTF8Encoding($false)))
            # 文件已经挪走，壁纸路径要跟着改到收藏夹，否则桌面指向一个不存在的文件
            Set-WallpaperStyle -Name $styleName
            [void](Set-DesktopWallpaper -Path $dest)
            if ($state -and [string]$state.date -eq $dateKey) {
                Write-State -Date $dateKey -File $dest -Title ([string]$favIdx[$dateKey].title) -ImageId ([string]$favIdx[$dateKey].imageId) -AppliedDate $dateKey
            }
            Write-Log "已收藏（移入收藏夹）：$name"
            # 文件已离开缓存，缓存索引里的条目要同步删掉，避免越积越多的失效记录
            if (Test-Path $IndexPath) {
                try {
                    $ciClean = Get-Content $IndexPath -Raw -Encoding UTF8 | ConvertFrom-Json
                    if ($ciClean.PSObject.Properties.Name -contains $dateKey) {
                        $tmpIdx = [ordered]@{}
                        foreach ($q in $ciClean.PSObject.Properties) {
                            if ($q.Name -ne $dateKey) { $tmpIdx[$q.Name] = $q.Value }
                        }
                        [System.IO.File]::WriteAllText($IndexPath, ($tmpIdx | ConvertTo-Json -Depth 6), (New-Object System.Text.UTF8Encoding($false)))
                    }
                } catch { }
            }
            Exit-Run 0
        } else {
            $back = Join-Path $CacheDir $name
            # 先取标题等信息再删索引，否则取消收藏后标题会丢
            $keptTitle = ''
            $keptId = ''
            $keptCpr = ''
            $keptSearch = ''
            if ($favIdx.ContainsKey($dateKey)) {
                $keptTitle = [string]$favIdx[$dateKey].title
                $keptId = [string]$favIdx[$dateKey].imageId
                $keptCpr = [string]$favIdx[$dateKey].copyright
            }
            if (-not $keptTitle -and $state -and [string]$state.date -eq $dateKey) {
                $keptTitle = [string]$state.title
                if (-not $keptId) { $keptId = [string]$state.imageId }
                if (-not $keptCpr) { $keptCpr = [string]$state.copyright }
            }
            if (Test-Path $dest) { Move-Item -LiteralPath $dest -Destination $back -Force }
            if ($favIdx.ContainsKey($dateKey)) { $favIdx.Remove($dateKey) }
            [System.IO.File]::WriteAllText($FavIndex, ($favIdx | ConvertTo-Json -Depth 6), (New-Object System.Text.UTF8Encoding($false)))
            if (Test-Path $back) {
                Set-WallpaperStyle -Name $styleName
                [void](Set-DesktopWallpaper -Path $back)
                if ($state -and [string]$state.date -eq $dateKey) {
                    Write-State -Date $dateKey -File $back -Title $keptTitle -Copyright $keptCpr -ImageId $keptId -AppliedDate $dateKey
                }
            }
            Write-Log "已取消收藏（移回缓存）：$name"
            Exit-Run 0
        }
    }

    if ($Clean) {
        $keepArg = if ($Keep -gt 0) { $Keep } else { $keepDays }
        Write-Log "--- 仅执行缓存清理（保留 $keepArg 天）---"
        $cleanN = [int](Clear-OldCache -Days $keepArg)
        Write-Output ('{"removed":' + $cleanN + ',"protected":' + [int]$script:PinProtected + '}')
        Exit-Run 0
    }

    # ---- 清理历史归档（保留当前壁纸）
    if ($CleanFavorites) {
        # 清空收藏：当前壁纸如果在收藏里，先移回缓存，避免桌面指向不存在的文件
        $curF = ''
        try { $curF = [string](Get-ItemProperty 'HKCU:\Control Panel\Desktop' -Name Wallpaper -ErrorAction SilentlyContinue).Wallpaper } catch { }
        $favFiles = @(Get-ChildItem $FavDir -Filter '*.jpg' -File -ErrorAction SilentlyContinue)
        $removedF = 0
        $keptF = 0
        $freedF = 0
        foreach ($ff in $favFiles) {
            if ($curF -and $curF -ieq $ff.FullName) {
                # 当前壁纸：移回缓存，不删
                $backF = Join-Path $CacheDir $ff.Name
                if (-not $DryRun) {
                    try {
                        Move-FileKeepWallpaper -Src $ff.FullName -Dest $backF -IsCurrentWallpaper
                        [void](Set-DesktopWallpaper -Path $backF)
                        $stF = Read-State
                        $dF = ($ff.Name -split '_')[0]
                        if ($stF -and [string]$stF.date -eq $dF) {
                            Write-State -Date $dF -File $backF -Title ([string]$stF.title) -Copyright ([string]$stF.copyright) -ImageId ([string]$stF.imageId) -AppliedDate $dF
                        }
                    } catch { }
                }
                $keptF++
                continue
            }
            $szF = $ff.Length
            if ($DryRun) { Write-Log "[DryRun] 将删除收藏 $($ff.Name)"; $removedF++; $freedF += $szF; continue }
            if (Remove-OneFile -Path $ff.FullName -Mode $CleanMode) { $removedF++; $freedF += $szF }
        }
        if (-not $DryRun) {
            try {
                [System.IO.File]::WriteAllText($FavIndex, '{}', (New-Object System.Text.UTF8Encoding($false)))
            } catch { }
        }
        $modeF = if ($CleanMode -eq 'permanent') { '彻底删除' } else { '进回收站' }
        if ($DryRun) { $modeF = 'DryRun，未删除' }
        Write-Log ("清空收藏（" + $modeF + "）：删除 " + $removedF + " 张，释放 " + [math]::Round($freedF / 1MB, 1) + " MB，保留当前壁纸 " + $keptF + " 张")
        Write-Output ('{"removed":' + $removedF + ',"moved":' + $keptF + '}')
        Exit-Run 0
    }

    if ($GalleryClear) {
        $nG = 0
        $curG = ''
        try { $curG = [string](Get-ItemProperty 'HKCU:\Control Panel\Desktop' -Name Wallpaper -ErrorAction SilentlyContinue).Wallpaper } catch { }
        $curInGallery = ($curG -and $curG.StartsWith($GalleryDir, [StringComparison]::OrdinalIgnoreCase))

        # 当前壁纸就在画廊里：先切回一张正常的图，否则清完桌面会空
        if ($curInGallery) {
            $pool = @()
            $pool += @(Get-ChildItem $CacheDir -Filter '*.jpg' -File -ErrorAction SilentlyContinue | Sort-Object Name -Descending)
            if (Test-Path $ArchiveImgDir) {
                $pool += @(Get-ChildItem $ArchiveImgDir -Filter '*.jpg' -File -ErrorAction SilentlyContinue | Sort-Object Name -Descending)
            }
            if (Test-Path $FavDir) {
                $pool += @(Get-ChildItem $FavDir -Filter '*.jpg' -File -ErrorAction SilentlyContinue | Sort-Object Name -Descending)
            }
            if ($pool.Count -gt 0) {
                $fb = $pool[0]
                if (-not $DryRun) {
                    [void](Set-DesktopWallpaper -Path $fb.FullName)
                    $fbDate = ($fb.BaseName -split '_')[0]
                    if ($fbDate -notmatch '^\d{8}$') { $fbDate = (Get-Date -Format 'yyyyMMdd') }
                    Write-State -Date $fbDate -File $fb.FullName -Title '' -Copyright '' -ImageId '' -AppliedDate $fbDate
                    Write-Log ("画廊退出：壁纸切回 " + $fb.Name)
                }
                $curG = $fb.FullName
            } else {
                Write-Log '画廊退出：没有可切回的图，保留当前这一张不删' 'WARN'
            }
        }

        if (Test-Path $GalleryDir) {
            Get-ChildItem $GalleryDir -Filter '*.jpg' -File -ErrorAction SilentlyContinue | ForEach-Object {
                # 兜底：任何情况下都不删当前壁纸
                if ($curG -and $_.FullName -ieq $curG) { return }
                if ($DryRun) { Write-Log "[DryRun] 将删除画廊 $($_.Name)"; $nG++ }
                elseif (Remove-OneFile -Path $_.FullName -Mode $CleanMode) { $nG++ }
            }
        }
        Write-Log ("退出画廊模式：清理 " + $nG + " 张")
        Write-Output ('{"removed":' + $nG + '}')
        Exit-Run 0
    }

    if ($PrefetchClear) {
        $nP = 0
        if (Test-Path $PrefetchDir) {
            Get-ChildItem $PrefetchDir -Filter '*.jpg' -File -ErrorAction SilentlyContinue | ForEach-Object {
                if ($DryRun) { Write-Log "[DryRun] 将删除预取 $($_.Name)"; $nP++ }
                elseif (Remove-OneFile -Path $_.FullName -Mode $CleanMode) { $nP++ }
            }
        }
        Write-Log ("清理预取缓存：" + $nP + " 张")
        Write-Output ('{"removed":' + $nP + '}')
        Exit-Run 0
    }

    if ($StoryFor) {
        $sv = Get-Story -Date $StoryFor
        $objS = [ordered]@{ date = ([string]$StoryFor) -replace '[^0-9]', ''; story = $sv }
        Write-Output ($objS | ConvertTo-Json -Compress)
        Exit-Run 0
    }

    if ($CleanStories) {
        $nS = 0
        if (Test-Path $StoriesDir) {
            Get-ChildItem $StoriesDir -Filter '*.txt' -File -ErrorAction SilentlyContinue | ForEach-Object {
                if ($DryRun) { Write-Log "[DryRun] 将删除故事 $($_.Name)"; $nS++ }
                elseif (Remove-OneFile -Path $_.FullName -Mode $CleanMode) { $nS++ }
            }
        }
        Write-Log ("清理每日故事：" + $nS + " 条")
        Write-Output ('{"removed":' + $nS + '}')
        Exit-Run 0
    }

    if ($CleanArchiveIndex) {
        $nIdx = 0
        Get-ChildItem $ArchiveDir -Filter '*.json' -File -ErrorAction SilentlyContinue | ForEach-Object {
            if ($_.Name -eq 'all-index.json' -or $_.Name -match '^\d{4}-\d{2}\.json$') {
                if ($DryRun) { Write-Log "[DryRun] 将删除索引 $($_.Name)"; $nIdx++ }
                elseif (Remove-OneFile -Path $_.FullName -Mode $CleanMode) { $nIdx++ }
            }
        }
        Write-Log ("清理归档索引：删除 " + $nIdx + " 个文件")
        Write-Output ('{"removed":' + $nIdx + ',"protected":0}')
        Exit-Run 0
    }

    if ($CleanArchive) {
        if (-not (Test-Path $ArchiveImgDir)) {
            Write-Log '历史归档目录不存在，无需清理'
            Exit-Run 0
        }
        # 保护：收藏过的 / pinned 里的 / 当前壁纸，一律不删
        $favA = @{}
        Get-ChildItem $FavDir -Filter '*.jpg' -File -ErrorAction SilentlyContinue | ForEach-Object {
            if ($_.Name -match '^(\d{8})') { $favA[$matches[1]] = $true }
        }
        $pinA = Read-Pinned
        $curA = ''
        try { $curA = [string](Get-ItemProperty 'HKCU:\Control Panel\Desktop' -Name Wallpaper -ErrorAction SilentlyContinue).Wallpaper } catch { }
        $removedA = 0
        $keptA = 0
        $freedA = 0
        $filesA = @(Get-ChildItem $ArchiveImgDir -Filter '*.jpg' -File -ErrorAction SilentlyContinue | Sort-Object Name)
        $keptFavA = 0
        $keptPinA = 0
        $keptCurA = 0
        foreach ($fa in $filesA) {
            $dA = ''
            if ($fa.Name -match '^(\d{8})') { $dA = $matches[1] }
            # 手动清理归档是用户明确操作，只保留收藏与当前壁纸；保护名单不再拦
            if ($dA -and $favA.ContainsKey($dA)) { $keptFavA++; $keptA++; continue }
            if ($curA -and $curA -eq $fa.FullName) { $keptCurA++; $keptA++; continue }
            $szA = $fa.Length
            if ($DryRun) {
                Write-Log "[DryRun] 将删除归档图 $($fa.Name)"
                $removedA++; $freedA += $szA
                continue
            }
            if (Remove-OneFile -Path $fa.FullName -Mode $CleanMode) { $removedA++; $freedA += $szA }
        }
        if (-not $DryRun) {
            # 同步清掉 pinned 名单里已经没图的日期
            $setA = Read-Pinned
            $leftA = @{}
            foreach ($k in $setA.Keys) {
                $still = Get-ChildItem $ArchiveImgDir -Filter "${k}_*.jpg" -File -ErrorAction SilentlyContinue | Select-Object -First 1
                if ($still) { $leftA[$k] = $true }
            }
            try {
                [System.IO.File]::WriteAllText($PinnedPath, ($leftA | ConvertTo-Json -Depth 3), (New-Object System.Text.UTF8Encoding($false)))
            } catch { }
        }
        $modeTxt = if ($CleanMode -eq 'permanent') { '彻底删除' } else { '进回收站' }
        Write-Log ("清理历史归档（" + $modeTxt + "）：删除 " + $removedA + " 张，释放 " + [math]::Round($freedA / 1MB, 1) + " MB，保留 " + $keptA + " 张")
        $nIdxA = 0
        if ($WithIndex) {
            if (Test-Path $ArchiveDir) {
                Get-ChildItem $ArchiveDir -Filter '*.json' -File -ErrorAction SilentlyContinue | ForEach-Object {
                    if ($_.Name -eq 'all-index.json' -or $_.Name -match '^\d{4}-\d{2}\.json$') {
                        if ($DryRun) { Write-Log "[DryRun] 将删除归档索引 $($_.Name)"; $nIdxA++ }
                        elseif (Remove-OneFile -Path $_.FullName -Mode $CleanMode) { $nIdxA++ }
                    }
                }
                Write-Log ("同时清理归档索引：删除 " + $nIdxA + " 个文件")
            }
        }
        # 老存档（2009-08 ~ 2016-02）单独一块，默认不动，要清得显式带 -IncludeOld
        $removedO = 0
        $freedO = 0
        if ($IncludeOld -and (Test-Path $OldDir)) {
            Get-ChildItem $OldDir -Recurse -Filter '*.jpg' -File -ErrorAction SilentlyContinue | ForEach-Object {
                $dO = ''
                if ($_.Name -match '^(\d{8})') { $dO = $matches[1] }
                if ($dO -and $favA.ContainsKey($dO)) { $keptFavA++; $keptA++; return }
                if ($curA -and $curA -eq $_.FullName) { $keptCurA++; $keptA++; return }
                $szO2 = $_.Length
                if ($DryRun) { Write-Log "[DryRun] 将删除老存档图 $($_.Name)"; $removedO++; $freedO += $szO2; return }
                if (Remove-OneFile -Path $_.FullName -Mode $CleanMode) { $removedO++; $freedO += $szO2 }
            }
            Write-Log ("清理老存档：删除 " + $removedO + " 张，释放 " + [math]::Round($freedO / 1MB, 1) + " MB")
        }
        Write-Output ('{"removed":' + $removedA + ',"keptFav":' + $keptFavA + ',"keptCur":' + $keptCurA + ',"idxRemoved":' + $nIdxA + ',"oldRemoved":' + $removedO + '}')
        Exit-Run 0
    }

    # ---- 随机一张（收藏优先，排除当前这张）
    if ($Random) {
        $pool = @(Get-ChildItem $FavDir -Filter *.jpg -File -ErrorAction SilentlyContinue)
        $from = '收藏'
        if ($pool.Count -eq 0) {
            $pool = @(Get-ChildItem $CacheDir -Filter *.jpg -File -ErrorAction SilentlyContinue)
            $from = '缓存'
        }
        if ($pool.Count -eq 0) { Write-Log '没有可随机的图（收藏与缓存都是空的）' 'ERROR'; exit 1 }

        $curWp = (Get-ItemProperty 'HKCU:\Control Panel\Desktop' -Name Wallpaper -ErrorAction SilentlyContinue).Wallpaper
        $filtered = @($pool | Where-Object { $_.FullName -ne $curWp })
        if ($filtered.Count -gt 0) { $pool = $filtered }

        $pick = $pool | Get-Random
        $pickDate = ($pick.BaseName -split '_')[0]

        $locked = Enter-RunLock -WaitSec $script:LockWaitSec
        if (-not $locked) { exit 2 }

        Set-WallpaperStyle -Name $styleName
        [void](Set-DesktopWallpaper -Path $pick.FullName)
        Write-Log "随机切换（$from，共 $($pool.Count) 张候选）：$($pick.Name)"

        $ttl = ''
        $iid = ''
        foreach ($ix in @($FavIndex, $IndexPath)) {
            if (Test-Path $ix) {
                try {
                    $obj = Get-Content $ix -Raw -Encoding UTF8 | ConvertFrom-Json
                    if ($obj.PSObject.Properties.Name -contains $pickDate) {
                        $ttl = [string]$obj.$pickDate.title
                        $iid = [string]$obj.$pickDate.imageId
                        break
                    }
                } catch { }
            }
        }
        Write-State -Date $pickDate -File $pick.FullName -Title $ttl -ImageId $iid -AppliedDate $pickDate
        Write-Log '--- 完成 ---'
        if ([bool]$config['autoClean']) {
            $capMB2 = 0
            try { $capMB2 = [int]$config['archiveMaxMB'] } catch { }
            if ($capMB2 -gt 0) { [void](Clear-ArchiveBySize -MaxMB $capMB2) }
        }
        Exit-Run 0
    }

    # ---- 指定日期
    if ($Date) {
        # 归一化：允许 2016-03-05 / 2016/03/05 等写法，非 8 位数字一律视为格式错误
        $Date = ([string]$Date) -replace '[^0-9]', ''
        if ($Date.Length -ne 8) { Write-Log "日期格式错误（要 YYYYMMDD）：$Date" 'ERROR'; exit 1 }
        $dt = $null
        try { $dt = [datetime]::ParseExact($Date, 'yyyyMMdd', $null) } catch { }
        if (-not $dt) { Write-Log "日期格式错误（要 YYYYMMDD）：$Date" 'ERROR'; exit 1 }
        if ($dt.Date -gt (Get-Date).Date) { Write-Log '不能选择未来日期' 'ERROR'; exit 5 }

        Write-Log "--- 指定日期：$Date ---"

        # 1) 本地已有（收藏优先）
        $hit = Get-ChildItem $FavDir -Filter "${Date}_*.jpg" -File -ErrorAction SilentlyContinue | Select-Object -First 1
        if (-not $hit) {
            $hit = Get-ChildItem $CacheDir -Filter "${Date}_*.jpg" -File -ErrorAction SilentlyContinue | Select-Object -First 1
        }
        if (-not $hit -and (Test-Path $ArchiveImgDir)) {
            $hit = Get-ChildItem $ArchiveImgDir -Filter "${Date}_*.jpg" -File -ErrorAction SilentlyContinue | Select-Object -First 1
        }
        if (-not $hit) {
            $omDir = Join-Path $OldDir $Date.Substring(0, 6)
            if (Test-Path $omDir) {
                $hit = Get-ChildItem $omDir -Filter "${Date}_*.jpg" -File -ErrorAction SilentlyContinue | Select-Object -First 1
            }
        }
        # 预取目录：命中就转正到 cache 根目录，否则退出清理时壁纸文件会消失
        if (-not $hit -and (Test-Path $PrefetchDir)) {
            $pfHit = Get-ChildItem $PrefetchDir -Filter "${Date}_*.jpg" -File -ErrorAction SilentlyContinue | Select-Object -First 1
            if ($pfHit) {
                $pfDest = Join-Path $CacheDir $pfHit.Name
                if ($script:WallMode) { $pfDest = Join-Path $WallDir $pfHit.Name }
                if ((-not $script:NoApply -or $script:WallMode) -and -not (Test-Path -LiteralPath $pfDest)) {
                    try { Move-Item -LiteralPath $pfHit.FullName -Destination $pfDest -Force; $pfHit = Get-Item -LiteralPath $pfDest } catch { }
                }
                $hit = $pfHit
            }
        }
        if (-not $hit -and $script:WallMode -and (Test-Path $WallDir)) {
            $hit = Get-ChildItem $WallDir -Filter "${Date}_*.jpg" -File -ErrorAction SilentlyContinue | Select-Object -First 1
        }
        if ($hit) {
            if ($DryRun) {
                Write-Log ("[DryRun] 命中本地，将设为壁纸：" + $hit.FullName)
                Exit-Run 0
            }
            $locked = Enter-RunLock -WaitSec $script:LockWaitSec
            if (-not $locked) { exit 2 }
            Set-WallpaperStyle -Name $styleName
            [void](Set-DesktopWallpaper -Path $hit.FullName)
            # 标题优先级：state（同一天）→ 归档索引 → 空
            $st = Read-State
            $tTitle = ''
            $tId = ''
            $tCpr = ''
            $tSearch = ''
            if ($st -and [string]$st.date -eq $Date) {
                $tTitle = [string]$st.title
                $tId = [string]$st.imageId
                $tCpr = [string]$st.copyright
                $tSearch = [string]$st.searchUrl
            }
            if (-not $tTitle -and $config['archive']) {
                try {
                    $recHit = Get-ArchiveRecord -Date $Date
                    if ($recHit) {
                        if (-not $tTitle) { $tTitle = [string]$recHit.title }
                        if (-not $tCpr) { $tCpr = [string]$recHit.copyright }
                        if (-not $tSearch) { $tSearch = [string]$recHit.searchUrl }
                        if (-not $tId -and $recHit.id) { $tId = [string]$recHit.id }
                    }
                } catch { }
            }
            # 翻页/取历史图命中本地时也要进保留名单，否则会被自动清理删掉
            Update-CacheIndex -Date $Date -Path $hit.FullName -Title $tTitle -Copyright $tCpr -ImageId $tId
            Add-Pinned -Date $Date
            if ($tTitle.Length -gt 60) { $tTitle = $tTitle.Substring(0, 60) }
            Write-State -Date $Date -File $hit.FullName -Title $tTitle -Copyright $tCpr -SearchUrl $tSearch -ImageId $tId -AppliedDate $Date
            Write-Log "命中本地：$($hit.Name)"
            if ([bool]$config['autoClean']) {
                $capMB1 = 0
                try { $capMB1 = [int]$config['archiveMaxMB'] } catch { }
                if ($capMB1 -gt 0) { [void](Clear-ArchiveBySize -MaxMB $capMB1) }
            }
            Exit-Run 0
        }

        # 2) 接口 8 天内 → 走正常下载（带元信息）
        $list8obj = (Get-WallpaperListJson -Count 8) | ConvertFrom-Json
        $list8 = @($list8obj)
        $pos8 = -1
        for ($i = 0; $i -lt $list8.Count; $i++) {
            if ([string]$list8[$i].startdate -eq $Date) { $pos8 = $i; break }
        }
        if ($pos8 -ge 0) {
            $Index = $pos8
            $modeDesc = "date $Date"
            Write-Log "该日期在接口范围内，走高质量下载（第 $pos8 位）"
        } else {
            # 3) 先试第三方归档拿原图 ID（能拿到就是 3.5 MB 以上的原图）
            $useArchive = [bool]$config['archive']
            if ($useArchive) {
                $rec = Get-ArchiveRecord -Date $Date

                # 老存档（2009-08 ~ 2016-02）：只有镜像站 slug，没有 Bing id
                if ($rec -and $rec.slug) {
                    $slugO = [string]$rec.slug
                    $ymO   = $Date.Substring(0, 6)
                    $dirO  = Join-Path $OldDir $ymO
                    $dirO = Get-DownloadDir
                    if (-not (Test-Path $dirO)) { New-Item -ItemType Directory -Path $dirO -Force | Out-Null }
                    $destO = Join-Path $dirO ("${Date}_" + $slugO + '.jpg')
                    if ($DryRun) {
                        Write-Log ("[DryRun] 将下载老存档：$slugO → $destO（不下载、不设壁纸）")
                        Exit-Run 0
                    }
                    $locked = Enter-RunLock -WaitSec $script:LockWaitSec
                    if (-not $locked) { exit 2 }
                    $szO = 0
                    $okO = $false
                    # 2013-2015：wilii 那段图基本没有，镜像优先；其余年份 wilii 优先
                    $candO = @()
                    # wilii 有两种命名：2014 前不带分辨率后缀，2015 起带。两种都试。
                    $wList = @()
                    if ($rec.wiliiUrl) {
                        $wList += @{ url = $rec.wiliiUrl; tag = '老存档·wilii' }
                        if ($rec.wiliiUrl -match '_\d+x\d+\.jpg$') {
                            $wList += @{ url = ($rec.wiliiUrl -replace '_\d+x\d+\.jpg$', '.jpg'); tag = '老存档·wilii(无后缀)' }
                        }
                    }
                    $preferAnerg = ($Date -ge '20130101' -and $Date -le '20151231')
                    if ($preferAnerg) {
                        if ($rec.anergUrl) { $candO += @{ url = $rec.anergUrl; tag = '老存档·镜像' } }
                        foreach ($w in $wList) { $candO += $w }
                    } else {
                        foreach ($w in $wList) { $candO += $w }
                        if ($rec.anergUrl) { $candO += @{ url = $rec.anergUrl; tag = '老存档·镜像' } }
                    }
                    foreach ($cO in $candO) {
                        if ($okO) { break }
                        try {
                            Write-Log ("下载 " + $cO.url)
                            $szO = Save-BingImage -Url $cO.url -Dest $destO -MinKB 30
                            $okO = $true
                            Write-Log ("下载完成 " + [math]::Round($szO / 1KB) + ' KB（' + $cO.tag + '）')
                        } catch {
                            Write-Log ("老存档候选失败（" + $cO.tag + "）：" + $_.Exception.Message) 'WARN'
                        }
                    }
                    if ($okO) {
                        Set-WallpaperStyle -Name $styleName
                        if (Set-DesktopWallpaper -Path $destO) { Write-Log "壁纸已设置: $destO" }
                        $tO = [string]$rec.title
                        if ($tO.Length -gt 60) { $tO = $tO.Substring(0, 60) }
                        Add-Pinned -Date $Date
                        Write-State -Date $Date -File $destO -Title $tO -Copyright '' -ImageId '' -AppliedDate $Date
                        Write-Log '--- 完成（老存档 1366x768）---'
                        Exit-Run 0
                    }
                    $locked = $false
                    Write-Log '老存档取图失败，继续尝试其他通道' 'WARN'
                }

                if ($rec -and $rec.id) {
                    $archId  = [string]$rec.id
                    $archTtl = [string]$rec.title
                    $archCpr = [string]$rec.copyright
                    $shortId = $archId -replace '_[A-Za-z]{2}-[A-Za-z]{2}\d+$', ''
                    $fileName = "${Date}_$shortId.jpg"
                    $dest = Join-Path $ArchiveImgDir $fileName
                    if ($script:WallMode -and $script:WallArchive) { $dest = Join-Path $ArchiveImgDir $fileName }
                    elseif ($script:WallMode) { $dest = Join-Path $WallDir $fileName }
                    elseif ($script:NoApply) { $dest = Join-Path $PrefetchDir $fileName }
                    if ($DryRun) {
                        Write-Log ("[DryRun] 将下载归档原图：" + $archId + " → " + $dest + "（不下载、不设壁纸）")
                        Exit-Run 0
                    }
                    $locked = Enter-RunLock -WaitSec $script:LockWaitSec
                    if (-not $locked) { exit 2 }

                    # 候选地址：UHD 原图 > 归档给出的直链
                    $cand = @()
                    $cand += @{ url = "https://www.bing.com/th?id=${archId}_UHD.jpg"; tag = '归档原图'; minKb = 300 }
                    if ($rec.url) {
                        $u2 = [string]$rec.url
                        if ($u2.StartsWith('/')) { $u2 = 'https://www.bing.com' + $u2 }
                        $cand += @{ url = $u2; tag = '归档直链'; minKb = 60 }
                    }

                    $got = $null
                    foreach ($c in $cand) {
                        try {
                            $sz = Save-BingImage -Url $c.url -Dest $dest -MinKB $c.minKb
                            $got = $c.tag
                            Write-Log ("下载完成 " + [math]::Round($sz / 1MB, 2) + ' MB（' + $c.tag + '）')
                            break
                        } catch {
                            Write-Log "候选失败（$($c.tag)）：$($_.Exception.Message)" 'WARN'
                        }
                    }

                    if ($got) {
                        Set-WallpaperStyle -Name $styleName
                        if (Set-DesktopWallpaper -Path $dest) { Write-Log "壁纸已设置: $dest" }
                        if ($archTtl.Length -gt 60) { $archTtl = $archTtl.Substring(0, 60) }
                        Add-Pinned -Date $Date
                        Write-State -Date $Date -File $dest -Title $archTtl -Copyright $archCpr -SearchUrl ([string]$rec.searchUrl) -ImageId $archId -AppliedDate $Date
                        Write-Log '--- 完成 ---'
        if ([bool]$config['autoClean']) {
            $capMB2 = 0
            try { $capMB2 = [int]$config['archiveMaxMB'] } catch { }
            if ($capMB2 -gt 0) { [void](Clear-ArchiveBySize -MaxMB $capMB2) }
        }
                        Exit-Run 0
                    }
                    $locked = $false
                    Write-Log '归档所有通道都失败（gh-proxy / raw / api.github.com / 本地代理），回退 CDN' 'WARN'
                }
            }

            # 4) 兜底：微软 CDN 按日期直取，再不行用 wilii
            $fileName = "${Date}_CDN.jpg"
            $dest = Join-Path $DownDir $fileName
            if ($DryRun) {
                Write-Log ("[DryRun] 将兜底下载（微软 CDN / wilii）→ " + $dest + "（不下载、不设壁纸）")
                Exit-Run 0
            }
            $locked = Enter-RunLock -WaitSec $script:LockWaitSec
            if (-not $locked) { exit 2 }
            $tailCand = @()
            $tailCand += @{ url = "https://bingwallpaperimages.microsoft.com/Latest/3840x2400/$Date.jpg"; tag = '微软 CDN' }
            foreach ($wu in @(Get-WiliiCandidates -Date $Date)) { $tailCand += @{ url = $wu; tag = 'wilii' } }
            $tailOk = $false
            $size = 0
            foreach ($tc in $tailCand) {
                if ($tailOk) { break }
                try {
                    $size = Save-BingImage -Url $tc.url -Dest $dest
                    $tailOk = $true
                    Write-Log ("下载完成 " + [math]::Round($size / 1MB, 2) + ' MB（' + $tc.tag + '）')
                } catch {
                    Write-Log ("兜底候选失败（" + $tc.tag + "）：" + $_.Exception.Message) 'WARN'
                }
            }
            if (-not $tailOk) {
                if ($Date -lt '20250301') {
                    if (-not $useArchive) {
                        Write-Log "该日期需要增强数据源（归档已关闭）：$Date" 'ERROR'
                        Exit-Run 6
                    } else {
                        Write-Log "归档源不可用，且 CDN 无此日期：$Date" 'ERROR'
                        Exit-Run 7
                    }
                }
                Write-Log "该日期没有可用壁纸：$Date" 'ERROR'
                Exit-Run 5
            }
            Set-WallpaperStyle -Name $styleName
            if (Set-DesktopWallpaper -Path $dest) { Write-Log "壁纸已设置: $dest" }
            Add-Pinned -Date $Date
            # 降级 CDN 时也尽量带上标题，避免托盘显示空白
            $cdTtl = [string]$archTtl
            $cdCpr = [string]$archCpr
            $cdId = [string]$archId
            if (-not $cdTtl -and $Date.Length -eq 8) {
                try {
                    $cdRec = Get-ArchiveRecord -Date $Date
                    if ($cdRec) {
                        $cdTtl = [string]$cdRec.title
                        $cdCpr = [string]$cdRec.copyright
                        $cdId = [string]$cdRec.id
                    }
                } catch { }
            }
            Write-State -Date $Date -File $dest -Title $cdTtl -Copyright $cdCpr -ImageId $cdId -AppliedDate $Date
            if ($archTried) {
                Write-Log '--- 完成（降级 CDN 压缩版）---'
                Exit-Run 8
            }
            Write-Log '--- 完成 ---'
            Exit-Run 0
        }
    }

    $modeDesc = 'today'
    if ($Use) { $modeDesc = "use $Use" }
    elseif ($Step -ne 0) { $modeDesc = "step $Step" }
    elseif ($PSBoundParameters.ContainsKey('Index')) { $modeDesc = "index $Index" }

    Write-Log "--- 开始 mode=$modeDesc KeepDays=$keepDays Style=$styleName Source=$($config['source']) Lang=$($config['titleLanguage']) DryRun=$($DryRun.IsPresent) ---"

    # ---- 纯本地模式：-Use
    if ($Use) {
        if (-not (Test-Path $Use)) { throw "文件不存在: $Use" }
        $file = (Resolve-Path -LiteralPath $Use).Path

        # 照片墙的临时图：换成壁纸时立刻转正进历史归档。
        # 否则关窗选「不保留」会把正在用的这张一起清掉，等于白换。
        try
        {
            if ($WallDir -and (Test-Path $WallDir)) {
                $parentW = (New-Object System.IO.DirectoryInfo((Split-Path -Parent $file))).FullName
                $wallFull = (New-Object System.IO.DirectoryInfo($WallDir)).FullName
                if ($parentW -eq $wallFull) {
                    if (-not (Test-Path $ArchiveImgDir)) { New-Item -ItemType Directory -Path $ArchiveImgDir -Force | Out-Null }
                    $arcDest = Join-Path $ArchiveImgDir (Split-Path $file -Leaf)
                    if (Test-Path -LiteralPath $arcDest) {
                        $arcDest = Join-Path $ArchiveImgDir ([System.IO.Path]::GetFileNameWithoutExtension($file) + '_' + (Get-Date -Format 'HHmmss') + '.jpg')
                    }
                    Move-Item -LiteralPath $file -Destination $arcDest -Force
                    $file = $arcDest
                    Write-Log ("照片墙的图已转正进归档：" + $arcDest)
                }
            }
        }
        catch {
            Write-Log ("照片墙转正失败：" + $_.Exception.Message) 'WARN'
        }

        $ext = [System.IO.Path]::GetExtension($file).ToLower()
        if ($ext -notin @('.jpg', '.jpeg', '.png', '.bmp')) { throw "不支持的格式: $ext" }

        $mDate = [regex]::Match([System.IO.Path]::GetFileName($file), '^(\d{8})')
        $dateStr = if ($mDate.Success) { $mDate.Groups[1].Value } else { (Get-Date -Format 'yyyyMMdd') }
        Write-Log "目标(本地): $dateStr  $([System.IO.Path]::GetFileName($file))"

        if ($DryRun) {
            Write-Log "[DryRun] 将设置壁纸: $file"
            Exit-Run 0
        }

        $locked = Enter-RunLock -WaitSec $script:LockWaitSec
        if (-not $locked) { exit 2 }

        Set-WallpaperStyle -Name $styleName
        if (Set-DesktopWallpaper -Path $file) { Write-Log "壁纸已设置: $file" }
        else { Write-Log '壁纸设置后注册表未更新' 'WARN' }
        # 本地图也尽量补标题：先查缓存索引，再查归档索引
        $uTtl = ''
        $uCpr = ''
        $uId = ''
        try {
            $uIdx = @{}
            if (Test-Path $IndexPath) {
                $uRaw = Get-Content $IndexPath -Raw -Encoding UTF8 | ConvertFrom-Json
                if ($uRaw.PSObject.Properties.Name -contains $dateStr) {
                    $uTtl = [string]$uRaw.$dateStr.title
                    $uCpr = [string]$uRaw.$dateStr.copyright
                    $uId = [string]$uRaw.$dateStr.imageId
                }
            }
            if (-not $uTtl -and $dateStr.Length -eq 8) {
                $uRec = Get-ArchiveRecord -Date $dateStr
                if ($uRec) {
                    $uTtl = [string]$uRec.title
                    $uCpr = [string]$uRec.copyright
                    $uId = [string]$uRec.id
                }
            }
        } catch { }
        # 手动换的那张要进保护名单，否则 30 天清理会把它删掉，"临时换的图"就真的只是临时
        Add-Pinned -Date $dateStr
        Write-State -Date $dateStr -File $file -Title $uTtl -Copyright $uCpr -ImageId $uId
        Write-Log '--- 完成 ---'
        if ([bool]$config['autoClean']) {
            $capMB2 = 0
            try { $capMB2 = [int]$config['archiveMaxMB'] } catch { }
            if ($capMB2 -gt 0) { [void](Clear-ArchiveBySize -MaxMB $capMB2) }
        }
        Exit-Run 0
    }

    # 列表只用来定位 8 天窗口；取不到时降级成「按本地索引相对移动」，别让断网把翻页变成失败
    $wallpapers = @()
    try {
        $wpJson = Get-WallpaperListJson -Count ([Math]::Max($keepDays, 8))
        $wpObj = $wpJson | ConvertFrom-Json
        $wallpapers = @($wpObj)
        # ---- 联网模式
        Write-Log "微软接口返回 $($wallpapers.Count) 天"
    } catch {
        Write-Log ("微软接口不可用，改按本地索引相对移动：" + $_.Exception.Message) 'WARN'
    }

    $state = Read-State

    # -Step：以 state.date 为基准相对移动
    if ($Step -ne 0) {
        $curDate = if ($state) { [string]$state.date } else { '' }
        $pos = -1
        for ($i = 0; $i -lt $wallpapers.Count; $i++) {
            if ([string]$wallpapers[$i].startdate -eq $curDate) { $pos = $i; break }
        }
        if ($pos -lt 0) {
            # 当前日期不在 8 天窗口里（可能已经翻到归档区），直接从索引找邻近的一天
            $outOfWindow = Get-NeighborDate -From $curDate -Step $Step
            if ($outOfWindow) {
                Write-Log ("当前日期不在窗口内（" + $curDate + "），从归档接续：" + $outOfWindow)
                & $PSCommandPath -Date $outOfWindow -Gallery:$Gallery -BudgetSec $BudgetSec
                exit $LASTEXITCODE
            }
            if ($wallpapers.Count -eq 0) {
                Write-Log "列表为空，且索引里没有相邻日期：$curDate" 'ERROR'
                Exit-Run 5
            }
            Write-Log "state 里没有当前日期（$curDate），从最新一张起算" 'WARN'
            $pos = 0
        }
        $target = $pos + $Step
        $hitEdge = 0
        $beyondDate = ''
        if ($target -lt 0) {
            # 已经到窗口最新端，尝试从归档索引找更新的一天
            $beyondDate = Get-NeighborDate -From $curDate -Step -1
            if ($beyondDate) {
                Write-Log "越过 8 天窗口，接续归档：$beyondDate"
            } else {
                $target = 0; $hitEdge = -1; Write-Log '已经是最新的一张（归档里也没有更新的了）' 'WARN'; $Index = $target
            }
        }
        if (-not $beyondDate -and $target -ge $wallpapers.Count) {
            # 已经到窗口最旧端，尝试从归档索引找更早的一天
            $beyondDate = Get-NeighborDate -From $curDate -Step 1
            if ($beyondDate) {
                Write-Log "越过 8 天窗口，接续归档：$beyondDate"
            } else {
                $target = $wallpapers.Count - 1; $hitEdge = 1; Write-Log '已经是归档里最旧的一张' 'WARN'; $Index = $target
            }
        }
        if ($beyondDate) {
            Write-Log ("相对移动（跨窗口）: " + $curDate + " Step=" + $Step + " -> " + $beyondDate)
            # 越界了：交给 -Date 分支处理（会走归档下载）
            & $PSCommandPath -Date $beyondDate -Gallery:$Gallery -BudgetSec $BudgetSec
            exit $LASTEXITCODE
        } else {
            Write-Log "相对移动: 当前第 $pos 位($curDate) Step=$Step -> 第 $target 位"
            $Index = $target
        }
    }

    if ($Index -lt 0 -or $Index -ge $wallpapers.Count) { throw "Index=$Index 超出范围（0..$($wallpapers.Count - 1)）" }

    # 自动模式（today）才做「新图发了吗」的判断；手动翻页不受影响
    $isAuto = ($modeDesc -eq 'today')
    if ($isAuto -and $Index -eq 0) {
        $todayB = Get-Date -Format 'yyyyMMdd'
        $topDate = [string]$wallpapers[0].startdate
        if ($topDate -lt $todayB) {
            Write-Log ("Bing 尚未发布 " + $todayB + " 的图，最新是 " + $topDate + "（等待中）") 'WARN'
            Exit-Run 9
        }
    }

    $img = $wallpapers[$Index]
    $dateStr = [string]$img.startdate

    $m = [regex]::Match([string]$img.urlbase, 'id=([^&]+)')
    if (-not $m.Success) { throw '无法从 urlbase 解析图片 ID' }
    $fullId = $m.Groups[1].Value -replace '_UHD\.jpg$', ''
    $baseId = $fullId -replace '_[A-Za-z]{2}-[A-Za-z]{2}\d+$', ''
    $fileName = "${dateStr}_${baseId}.jpg"
    $dest = Join-Path $DownDir $fileName

    # 同一天只可能有一张 Bing 图：不管本地存的是 _OHR.Xxx 还是 _CDN，都复用同一份
    # 优先用非 _CDN（原图质量更高），没有再退回 _CDN
    $cands = @()
    $cands += Get-ChildItem $FavDir -Filter "${dateStr}_*.jpg" -File -ErrorAction SilentlyContinue
    $cands += Get-ChildItem $CacheDir -Filter "${dateStr}_*.jpg" -File -ErrorAction SilentlyContinue
    $existing = $null
    if ($cands.Count -gt 0) {
        $existing = $cands | Where-Object { $_.Name -notlike '*_CDN*' } | Select-Object -First 1
        if (-not $existing) { $existing = $cands | Select-Object -First 1 }
    }
    if ($existing) { $dest = $existing.FullName }

    # 标题：HPImageArchive 返回的已经是 mkt 对应的语言，直接用
    $title = [string]$img.title
    $copy  = [string]$img.copyright
    Write-Log "目标: $dateStr  $title"

    # 幂等（DryRun 也要能提前退出）
    $curWp = (Get-ItemProperty 'HKCU:\Control Panel\Desktop' -Name Wallpaper -ErrorAction SilentlyContinue).Wallpaper
    if ($isAuto -and -not $Force -and $state -and $state.date -eq $dateStr -and (Test-Path $dest) -and $curWp -eq $dest) {
        Write-Log ("当天壁纸已就位（" + $dateStr + "），无需重复设置")
        if ([bool]$config['autoClean']) {
            [void](Clear-OldCache -Days $keepDays)
            $capMB = 0
            try { $capMB = [int]$config['archiveMaxMB'] } catch { }
            if ($capMB -gt 0) { [void](Clear-ArchiveBySize -MaxMB $capMB) }
        }
        Exit-Run 0
    }
    if (-not $Force -and $state -and $state.date -eq $dateStr -and (Test-Path $dest) -and $curWp -eq $dest) {
        if ($hitEdge -ne 0) {
            Write-Log ('已经在边界，无可切换：edge=' + $hitEdge)
            Exit-Run 4
        }
        Write-Log '已是最新且壁纸已就位，跳过'
        if ([bool]$config['autoClean']) {
            [void](Clear-OldCache -Days $keepDays)
            $capMB = 0
            try { $capMB = [int]$config['archiveMaxMB'] } catch { }
            if ($capMB -gt 0) { [void](Clear-ArchiveBySize -MaxMB $capMB) }
        }
        Exit-Run 0
    }

    # 候选地址
    $bingUhd  = "https://www.bing.com/th?id=${fullId}_UHD.jpg"
    $bingWh   = "https://www.bing.com/th?id=${fullId}&w=3840&h=2160"
    $bing1080 = "https://www.bing.com/th?id=${fullId}_1920x1080.jpg"
    $msUrl2   = [string]$img.url2
    $msUrl    = [string]$img.url

    $res = [string]$config['resolution']
    if ([string]$config['source'] -eq 'microsoft') {
        $candidates = @($msUrl2, $msUrl, $bingUhd, $bing1080)
    } elseif ($res -eq '1920x1080') {
        $candidates = @($bing1080, $bingWh, $bingUhd, $msUrl2, $msUrl)
    } else {
        $candidates = @($bingUhd, $bingWh, $msUrl2, $msUrl, $bing1080)
    }
    $candidates = @($candidates) + @(Get-WiliiCandidates -Date $dateStr)
    $candidates = $candidates | Where-Object { $_ } | Select-Object -Unique

    $lockedNow = [bool]$config['locked']
    $autoMode = ($modeDesc -eq 'today')

    if ($DryRun) {
        Write-Log "[DryRun] 分辨率档位 $res，首选 $($candidates[0])"
        Write-Log "[DryRun] 将下载 -> $dest"
        if ($autoMode -and $lockedNow) {
            Write-Log '[DryRun] 锁定中，只下载不切换壁纸'
        } else {
            Write-Log "[DryRun] 将设置壁纸: $dest （样式 $styleName）"
        }
        Exit-Run 0
    }

    $locked = Enter-RunLock -WaitSec $script:LockWaitSec
    if (-not $locked) { exit 2 }

    $size = 0
    $needDownload = $true
    if (Test-Path $dest) {
        $existSize = (Get-Item $dest).Length
        if ($existSize -ge 300KB) {
            $needDownload = $false
        } elseif ($existSize -lt 300KB) {
            Write-Log "缓存文件异常（$existSize 字节 < 300 KB），将重新下载" 'WARN'
        }
    }
    if ($Force) { $needDownload = $true }

    if ($needDownload) {
        $ok = $false
        foreach ($u in $candidates) {
            try {
                Write-Log "下载 $u"
                $size = Save-BingImage -Url $u -Dest $dest
                Write-Log ("下载完成 " + [math]::Round($size / 1MB, 2) + ' MB')
                $ok = $true
                break
            } catch {
                Write-Log "失败($u): $($_.Exception.Message)" 'WARN'
            }
        }
        if (-not $ok) { Write-Log '所有候选地址都下载失败' 'ERROR'; Exit-Run 7 }
    } else {
        $size = (Get-Item $dest).Length
        Write-Log ("缓存已存在，复用 " + (Split-Path $dest -Leaf) + " (" + [math]::Round($size / 1MB, 2) + " MB)")
    }

    # 索引：画廊模式的图不落在 cache 根目录，不写进 cache\index.json（否则会留下指向不存在文件的条目）
    if (-not $Gallery -and -not $script:NoApply) {
        $catalog = @{}
        if (Test-Path $IndexPath) {
            try {
                $old = Get-Content $IndexPath -Raw -Encoding UTF8 | ConvertFrom-Json
                foreach ($pp in $old.PSObject.Properties) { $catalog[$pp.Name] = $pp.Value }
            } catch { }
        }
        $catalog[$dateStr] = [ordered]@{
            title     = $title
            titleEn   = $titleEn
            copyright = $copy
            file      = $fileName
            imageId   = $fullId
            sizeBytes = $size
        }
        [System.IO.File]::WriteAllText($IndexPath, ($catalog | ConvertTo-Json -Depth 6), (New-Object System.Text.UTF8Encoding($false)))
    }

    # 设壁纸（锁定 + 自动触发时只下载不切换）
    if ($autoMode -and $lockedNow) {
        $prevApplied = $dateStr
        if ($state -and $state.appliedDate) { $prevApplied = [string]$state.appliedDate }
        Write-Log "锁定中：已下载 $fileName，不切换壁纸（保持 $prevApplied）"
        Write-State -Date $dateStr -File $dest -Title $title -Copyright $copy -SearchUrl ([string]$img.copyrightlink) -ImageId $fullId -AppliedDate $prevApplied
    } else {
        Set-WallpaperStyle -Name $styleName
        if (Set-DesktopWallpaper -Path $dest) { Write-Log "壁纸已设置: $dest" }
        else { Write-Log '壁纸设置后注册表未更新' 'WARN' }
        Write-State -Date $dateStr -File $dest -Title $title -Copyright $copy -SearchUrl ([string]$img.copyrightlink) -ImageId $fullId -AppliedDate $dateStr
        if ($lockedNow) { Write-Log "锁定中，手动切换已更新保持对象：$title" }
    }

    if ([bool]$config['autoClean']) {
        $capMB = 0
        try { $capMB = [int]$config['archiveMaxMB'] } catch { }
        if ($capMB -gt 0) { [void](Clear-ArchiveBySize -MaxMB $capMB) }
        [void](Clear-OldCache -Days $keepDays)
    } else {
        Write-Log '自动清理已关闭，跳过缓存清理'
    }
    Write-Log '--- 完成 ---'
    Exit-Run 0
}
catch {
    Write-Log ("失败: " + $_.Exception.Message) 'ERROR'
    Write-Log ("位置: " + $_.InvocationInfo.PositionMessage) 'ERROR'
    Exit-Run 1
}
finally {
    if ($locked) { Exit-RunLock }
}

