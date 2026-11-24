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
    [string]$RandomScope = 'local',
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
    [ValidateSet('auto','cdn','anerg')]
    [string]$Source = 'auto',
    [ValidateSet('uhd','fhd','hd')]
    [string]$MaxRes = 'uhd',
    [switch]$KeepExisting,
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
    [switch]$WallFull,
    [switch]$WallKeep,
    [switch]$WallDrop,
    [switch]$WallArchive,
    [switch]$AuditOld,
    [switch]$FixLegacyFav,
    [switch]$FixLegacyDates,
    [switch]$FixDatedFiles,
    [switch]$TestArchiveDir,
    [int]$TestArchiveSweep = 0,
    [switch]$Festivals,
    [string]$Festival = '',
    [switch]$MigrateOld,
    [switch]$FillLibrary,
    [switch]$UpdateStoryMap,
    [switch]$OnlyLowRes,
    [switch]$AuditQuality
)

# ---------------------------------------------------------------- 退出码约定
# 调用方（BingWallpaperLite.exe）按这张表解读返回值，C# 侧在 Script.cs / CalendarForm.cs /
# PhotoWallForm.cs 里有对应分支。加新码时两边一起改，别只改一边。
$EXIT = @{
    OK        = 0    # 成功
    ERR       = 1    # 一般错误，看日志定位
    BUSY      = 2    # 撞锁，已有实例在跑
    NO_FAV    = 3    # 没有可取消的收藏
    EDGE      = 4    # 翻到最早 / 最新边界
    NO_IMAGE  = 5    # 这一天没有图
    NEED_ARCH = 6    # 缺历史归档，需先取归档索引
    ARCH_DOWN = 7    # 归档源不可用
    DEGRADED  = 8    # 成功但画质降级（非 UHD）
    WAITING   = 9    # 今天的图还没发布
    WALL_FAIL = 11   # 图已就位，桌面壁纸未生效
    FUTURE    = 12   # 日期越界（未来日期）
}

$ErrorActionPreference = 'Stop'

# 命中本地但画质不足时，记下要被覆盖的那份文件（-Date 分支用）
$script:UpgradeDest = ''

# 临时件的删除方式：画廊图、预取图、故事缓存、两处索引、清空缓存 —— 都是可重建/可重抓的东西，
# 默认直接删，不进回收站（回收站有配额，塞满之后照样变永久删，等于假装安全）。
# 用户显式给 -CleanMode 时一律按用户说的来。
$modeTemp = if ($PSBoundParameters.ContainsKey('CleanMode')) { $CleanMode } else { 'permanent' }

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
# 已知取不到的日期：随机时跳过，窗口点了失败也记这里。30 天后重试（镜像可能修好）
$DeadDatesPath = Join-Path $Root 'cache\dead-dates.json'

# ---------------------------------------------------------------- 接管模式（沙盒）
# 有 takeover\config.json 且 root 可用时，只读写 takeover\ 与 root：
# 安装目录的 cache / favorites / state / pinned / config 一律不碰，退出时整目录删掉即可回退。
$TakeoverDir     = Join-Path $Root 'takeover'
$TakeoverCfg     = Join-Path $TakeoverDir 'config.json'
$script:Takeover = $false
$script:TakeoverRoot = ''
$script:TakeoverBroken = ''
# 接管时的下载暂存区：所有注定要进图库的文件先落这里，成功了再一次性搬进去。
# 非接管模式保持空串，所有相关判断短接，行为一字不变。
$script:LibStaging = ''
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
    # 老存档（2009-08~2016-02）在接管模式下整个不用：图库里可能已经有同一张，
    # 缺的也直接补进图库目录，再从 archive\old 兜底等于绕开了「只用这个目录」的约定。
    # 指向一个不存在的子目录，让所有 Test-Path 自然落空，不用在各处再加判断。
    $OldDir        = Join-Path $TakeoverDir 'old-not-used'
    $PinnedPath    = Join-Path $TakeoverDir 'pinned.json'
    $PinnedLegacyPath = $PinnedPath
    $script:LibStaging = Join-Path $TakeoverDir 'incoming'
    $DeadDatesPath = Join-Path $TakeoverDir 'dead-dates.json'
    foreach ($dT in @($TakeoverDir, $CacheDir, $FavDir, $script:LibStaging)) {
        if (-not (Test-Path $dT)) { try { New-Item -ItemType Directory -Path $dT -Force | Out-Null } catch { } }
    }
}

# ---------------------------------------------------------------- 测试沙盒
# 只给 test.ps1 用：把归档清理涉及的路径指到临时沙盒。
# 路径必须带 BWTEST 标记，防止误配到真实目录；这里只做变量赋值，不调函数。
if ($TestArchiveDir) {
    $sbRoot = [string]$env:BING_TEST_SANDBOX
    if (-not $sbRoot -or $sbRoot -notmatch 'BWTEST') {
        Write-Host '测试沙盒路径必须带 BWTEST 标记，已拒绝'
        exit 4
    }
    if (-not (Test-Path $sbRoot)) {
        Write-Host '测试沙盒不存在，已拒绝'
        exit 4
    }
    $ArchiveImgDir    = Join-Path $sbRoot 'archive'
    $FavDir           = Join-Path $sbRoot 'favorites'
    $FavIndex         = Join-Path $FavDir 'index.json'
    $PinnedPath       = Join-Path $sbRoot 'pinned.json'
    $PinnedLegacyPath = $PinnedPath
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
    if ($d -and -not (Test-Path $d)) { try { New-Item -ItemType Directory -Path $d -Force | Out-Null } catch { Write-Log ('下载目录创建失败：' + $d + ' - ' + $_.Exception.Message) 'WARN' } }
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
    # 画廊模式在接管下没有意义：接管是「用我自己的图库」，画廊是「临时试看随手清」，
    # 两套语义混在一起会让人分不清当前壁纸到底挂在哪个仓库
    if ($Gallery) { $roBlocked += '-Gallery' }
    if ($GalleryClear) { $roBlocked += '-GalleryClear' }
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
$ArchiveMinDate = '20090713'

# 归档抓取通道。实测（无代理直连）：
#   raw.githubusercontent.com  → 100% 失败（TLS 阻断）
#   gh-proxy.com               → 6/6 成功，平均 800 ms
#   api.github.com             → 可通，未认证 60 次/小时
# 顺序：记忆通道 → gh-proxy → raw 直连 → api.github.com → 本地代理
$ArchiveProxyBase = 'https://gh-proxy.com/'
$ArchiveRawBase   = 'https://raw.githubusercontent.com/'
$ViaScorePath     = Join-Path $CacheDir 'via-score.json'

# ---------------------------------------------------------------- 通道记账
# 固定优先级永远是错的：今天 gh-proxy 快、明天本地代理快、后天都不通。
# 记「指数平均耗时 + 失败次数」，按有效期耗时排序，每次取图都重排。
# 不用「分数」是因为分数会撞上限饱和 —— 一旦三条通道都满分就退化成固定顺序。
# 这里的值永远不饱和：快的更快、慢的一直慢。
function Get-ViaStats {
    $m = @{}
    try {
        if (Test-Path $ViaScorePath) {
            $o = Get-Content $ViaScorePath -Raw -Encoding UTF8 | ConvertFrom-Json
            $now = Get-Date
            foreach ($q in $o.PSObject.Properties) {
                $ms = 2500.0
                try { $ms = [double]$q.Value.ms } catch { }
                $fail = 0
                try { $fail = [int]$q.Value.fail } catch { }
                $days = 0.0
                try { $days = [math]::Max(0, ($now - [datetime]::Parse([string]$q.Value.at)).TotalDays) } catch { }
                # 三天没动过就向默认值回归：坏过的通道也该有翻身机会，
                # 否则一次故障会把它永远压在队尾
                if ($days -ge 3) {
                    $fail = [int][math]::Floor($fail / 2)
                    $ms = $ms * 0.5 + 2500.0 * 0.5
                }
                $eff = $ms * (1.0 + $fail * 0.5)
                $m[$q.Name] = [pscustomobject]@{ ms = $ms; fail = $fail; eff = $eff }
            }
        }
    } catch { }
    return $m
}

function Set-ViaStat {
    param([string]$Tag, [bool]$Ok, [int]$Ms)
    if (-not $Tag) { return }
    try {
        $raw = @{}
        if (Test-Path $ViaScorePath) {
            try {
                $o = Get-Content $ViaScorePath -Raw -Encoding UTF8 | ConvertFrom-Json
                foreach ($q in $o.PSObject.Properties) {
                    $raw[$q.Name] = @{ ms = [double]$q.Value.ms; fail = [int]$q.Value.fail }
                }
            } catch { }
        }
        $curMs = 2500.0
        $curFail = 0
        if ($raw.ContainsKey($Tag)) { $curMs = [double]$raw[$Tag].ms; $curFail = [int]$raw[$Tag].fail }
        if ($Ok) {
            # 指数移动平均：老印象占七成，新样本占三成
            $newMs = $curMs * 0.7 + [double]$Ms * 0.3
            $newFail = [math]::Max(0, $curFail - 1)
        } else {
            # 封顶 60 秒：不封顶的话连续失败会让数字指数爆炸（1.5^300）
            $newMs = [math]::Min(60000.0, $curMs * 1.5)
            $newFail = $curFail + 1
        }
        $raw[$Tag] = @{ ms = [math]::Round($newMs, 1); fail = $newFail; at = (Get-Date).ToString('s') }
        $dir = Split-Path $ViaScorePath -Parent
        if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
        [System.IO.File]::WriteAllText($ViaScorePath, ($raw | ConvertTo-Json -Depth 4), (New-Object System.Text.UTF8Encoding($false)))
    } catch { }
}


# 本地代理探测结果缓存。
# 脚本每次都是新进程，所以「进程内缓存」实际就是「本次调用内只用一次」；
# 再加一道按日失效：万一同一个进程被复用/长驻（比如预先拉起），
# 跨天后也会重新探一次，不会把昨天的网络状态带到今天。
$script:LocalProxyHit = $null
$script:LocalProxyDay = $null

# 返回可用的本地代理地址（'' 表示没有）。用户开着 Clash 时，
# 走 raw + 本地代理比绕 gh-proxy.com 这个第三方中转更快也更干净。
function Get-LocalProxy {
    $today = (Get-Date).ToString('yyyyMMdd')
    if ($null -ne $script:LocalProxyHit -and $script:LocalProxyDay -eq $today) {
        return $script:LocalProxyHit
    }
    $script:LocalProxyHit = ''
    foreach ($px in @('http://127.0.0.1:7897', 'http://127.0.0.1:7890', 'http://127.0.0.1:10809')) {
        try {
            $u = [Uri]$px
            $c = New-Object System.Net.Sockets.TcpClient
            try {
                $iar = $c.BeginConnect($u.Host, $u.Port, $null, $null)
                if ($iar.AsyncWaitHandle.WaitOne(300) -and $c.Connected) {
                    $script:LocalProxyHit = $px
                    $script:LocalProxyDay = $today
                    return $px
                }
            } finally { try { $c.Close() } catch { } }
        } catch { }
    }
    $script:LocalProxyDay = $today
    return ''
}

function Get-ArchiveCandidateUrls {
    param([string]$RelPath)   # 形如 Zhu-junwei/bing-wallpaper-archive/master/2026/11/all.json
    $urls = @()

    # 本地代理可用就排最前：最快，且不把请求交给第三方中转
    $px0 = Get-LocalProxy
    if ($px0) { $urls += @{ tag = ('proxy:' + $px0); url = ($ArchiveRawBase + $RelPath); proxy = $px0 } }

    $last = [string]$config['archiveVia']
    if ($last -eq 'ghproxy')  { $urls += @{ tag='ghproxy';  url = $ArchiveProxyBase + $ArchiveRawBase + $RelPath } }
    if ($last -eq 'raw')      { $urls += @{ tag='raw';      url = $ArchiveRawBase + $RelPath } }
    if ($last -eq 'api')      { $urls += @{ tag='api';      url = '__API__' + $RelPath } }

    $urls += @{ tag='ghproxy'; url = $ArchiveProxyBase + $ArchiveRawBase + $RelPath }
    $urls += @{ tag='raw';     url = $ArchiveRawBase + $RelPath }
    $urls += @{ tag='api';     url = '__API__' + $RelPath }

    # 兜底再试其余本地代理端口（有的机器开的是 7890 或 10809）
    foreach ($px in @('http://127.0.0.1:7897', 'http://127.0.0.1:7890', 'http://127.0.0.1:10809')) {
        if ($px -eq $px0) { continue }
        $urls += @{ tag=('proxy:' + $px); url = ($ArchiveRawBase + $RelPath); proxy = $px }
    }

    # 去重，保持顺序
    $seen = @{}
    $out = @()
    foreach ($c in $urls) {
        $k = $c.tag
        if (-not $seen.ContainsKey($k)) { $seen[$k] = $true; $out += $c }
    }

    # 按实测有效耗时排序：快的先试；没跑过的给默认 2500 ms，排在已知的之后
    $stats = Get-ViaStats
    $i0 = 0
    $ranked = foreach ($c in $out) {
        $eff = 2500.0
        if ($stats.ContainsKey([string]$c.tag)) { $eff = [double]$stats[[string]$c.tag].eff }
        [pscustomobject]@{ tag = $c.tag; url = $c.url; proxy = $c.proxy; eff = $eff; ord = $i0++ }
    }
    return @($ranked | Sort-Object -Property @{ Expression = { $_.eff }; Descending = $false },
                                             @{ Expression = { $_.ord }; Descending = $false })
}

function Invoke-ArchiveFetch {
    param([string]$RelPath, [int]$TimeoutSec = 8)
    $reqHeaders = @{'User-Agent' = 'Mozilla/5.0'}
    $cands = Get-ArchiveCandidateUrls -RelPath $RelPath
    $lastErr = ''

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    foreach ($c in $cands) {
        $tag = $c.tag
        $u = $c.url
        $sw.Restart()
        try {
            if ($u.StartsWith('__API__')) {
                # 用 GitHub contents API 取 base64
                $apiPath = $u.Substring(7)     # owner/repo/branch/path...
                $parts = $apiPath.Split('/')
                if ($parts.Length -lt 4) { continue }
                $owner = $parts[0]; $repo = $parts[1]; $branch = $parts[2]
                $filePath = ($parts[3..($parts.Length - 1)] -join '/')
                $apiUrl = "https://api.github.com/repos/$owner/$repo/contents/$filePath" + "?ref=$branch"
                $r = Invoke-WebRequest -Uri $apiUrl -TimeoutSec $TimeoutSec -UseBasicParsing -Headers $reqHeaders
                $obj = $r.Content | ConvertFrom-Json
                if (-not $obj.content) { throw 'API 无 content 字段' }
                $txt = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String(($obj.content -replace '\s', '')))
                if ($txt.Length -gt 0) {
                    Set-ViaStat -Tag $tag -Ok $true -Ms $sw.ElapsedMilliseconds
                    Write-Log ("归档索引取到了（via GitHub API，" + $sw.ElapsedMilliseconds + " ms）")
                    Save-ArchiveVia 'api'
                    return [pscustomobject]@{ Content = $txt }
                }
            } else {
                $ht = $reqHeaders
                if ($c.proxy) {
                    $r = Invoke-WebRequest -Uri $u -TimeoutSec $TimeoutSec -UseBasicParsing -Headers $ht -Proxy $c.proxy
                } else {
                    $r = Invoke-WebRequest -Uri $u -TimeoutSec $TimeoutSec -UseBasicParsing -Headers $ht
                }
                if ($r) {
                    Set-ViaStat -Tag $tag -Ok $true -Ms $sw.ElapsedMilliseconds
                    Write-Log ("归档索引取到了（via " + $tag + "，" + $sw.ElapsedMilliseconds + " ms）")
                    if ($tag -eq 'ghproxy') { Save-ArchiveVia 'ghproxy' }
                    elseif ($tag -eq 'raw') { Save-ArchiveVia 'raw' }
                    return $r
                }
            }
        } catch {
            $lastErr = $_.Exception.Message
            # 超时/连不上就扣分：下一次它会自动往后排
            Set-ViaStat -Tag $tag -Ok $false -Ms $sw.ElapsedMilliseconds
            Write-Log ("  通道 " + $tag + " 失败（" + $sw.ElapsedMilliseconds + " ms）：" + $lastErr) 'WARN'
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
    } catch { Write-Log ('归档通道偏好写入失败：' + $_.Exception.Message) 'WARN' }
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

    # 每天最多自动校准一次映射表：map 里可能有预填的未来日期，
    # 到那天用官方图名匹配校对一遍，不一致就改过来。
    try {
        $stale = $true
        if (Test-Path $StoryMapPath) {
            $stale = ((Get-Date) - (Get-Item $StoryMapPath).LastWriteTime).TotalHours -ge 24
        }
        if ($stale) {
            & $PSCommandPath -UpdateStoryMap *> $null
            $script:StoryMap = $null
        }
    } catch { }

    $map = Get-StoryMap
    if (-not $map.ContainsKey($Date)) {
        # 映射表是静态打包的，新日子没有 slug 就永远取不到故事。
        # 只对最近 30 天自愈一次 —— 早期日期没有就是没有，不该每次触发网络请求。
        $recentS = $false
        try { $recentS = ($Date -ge (Get-Date).AddDays(-30).ToString('yyyyMMdd')) } catch { }
        if ($recentS) {
            try {
                & $PSCommandPath -UpdateStoryMap *> $null
                $script:StoryMap = $null          # 子进程写过盘了，本进程缓存要失效
                $map = Get-StoryMap
            } catch { }
        }
        if (-not $map.ContainsKey($Date)) { return '' }
    }
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

function Get-ImageIdFromFileName {
    param([string]$Name)
    if (-not $Name) { return '' }
    $n = [System.IO.Path]::GetFileNameWithoutExtension($Name)
    $n = $n -replace '_(UHD|\d{3,4}x\d{3,4})$', ''
    return $n
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

    # 镜像的图片名有自己的规则：短 slug + 可选分辨率后缀，跟我们的 _ZH-CNxxxx 尾巴无关
    $aNames = @()
    $shortName = ''
    if ($core) { $shortName = ($core -split '_')[0] }
    if ($slug) { $aNames += $slug }
    if ($shortName) {
        $aNames += $shortName
        $aNames += ($shortName + '_1366x768')
        $aNames += ($shortName + '_1920x1080')
    }
    $aNames = @($aNames | Where-Object { $_ } | Select-Object -Unique)

    return @{
        id         = ''
        title      = [string]$x.t
        copyright  = [string]$x.c
        url        = $wiliiUrl
        slug       = $core
        anergUrl   = $anergUrl
        anergNames = $aNames
        wiliiUrl   = $wiliiUrl
        source     = 'old'
        searchUrl  = ''
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
        try { New-Item -ItemType Directory -Path $ArchiveDir -Force | Out-Null } catch { Write-Log ('归档目录创建失败：' + $_.Exception.Message) 'WARN' }
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
        } catch { Write-Log ('归档月度缓存读取失败：' + $_.Exception.Message) 'WARN' }
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
            } catch { Write-Log ('归档索引行解析失败：' + $_.Exception.Message) 'WARN' }
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

# 把暂存区里的文件搬进图库。图库已有同名文件时一律保留原件（画质升级除外），
# 保证「只增不减」；任何中间态都留在暂存区，不污染图库目录。
function Publish-ToLibrary {
    param([string]$Staged, [string]$Dest)
    if (-not $Staged -or -not $Dest) { return $false }
    if (-not (Test-Path -LiteralPath $Staged)) { return $false }
    $leaf = Split-Path $Dest -Leaf
    try {
        if (Test-Path -LiteralPath $Dest) {
            $isUpgrade = ($script:UpgradeDest -and ($Dest -ieq [string]$script:UpgradeDest))
            if ($isUpgrade) {
                Move-Item -LiteralPath $Staged -Destination $Dest -Force
                Write-Log ('画质升级：覆盖图库里的 ' + $leaf)
            } else {
                Remove-Item -LiteralPath $Staged -Force -ErrorAction SilentlyContinue
                Write-Log ('图库已有这一天，丢弃暂存副本：' + $leaf)
            }
            return $true
        }
        try {
            Move-Item -LiteralPath $Staged -Destination $Dest -Force
        } catch {
            # 跨卷 Move 会退化成复制+删除，中途失败会留半个文件。
            # 改成先写同目录 .new 再原子改名，失败就把 .new 清掉。
            $new = $Dest + '.new'
            try {
                Copy-Item -LiteralPath $Staged -Destination $new -Force
                [System.IO.File]::Move($new, $Dest)
            } finally {
                if (Test-Path -LiteralPath $new) { Remove-Item -LiteralPath $new -Force -ErrorAction SilentlyContinue }
            }
            Remove-Item -LiteralPath $Staged -Force -ErrorAction SilentlyContinue
        }
        return $true
    } catch {
        Write-Log ('写入图库失败（文件留在暂存区）：' + $_.Exception.Message) 'WARN'
        return $false
    }
}

function Save-BingImage {
    param([string]$Url, [string]$Dest, [int]$MinKB = 300, [int]$Retry = 2)
    # 接管模式：目的地落在图库时，先下到暂存区，成功后再一次性搬进去。
    # 这样断线 / 超时 / 403 只在沙盒里留 .tmp，图库目录永远看不到半成品。
    $realDest = $Dest
    $staged = $false
    if ($script:LibStaging -and $Dest -and $script:TakeoverRoot -and
        $Dest.StartsWith($script:TakeoverRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        try {
            if (-not (Test-Path $script:LibStaging)) { New-Item -ItemType Directory -Path $script:LibStaging -Force | Out-Null }
            $realDest = Join-Path $script:LibStaging (Split-Path $Dest -Leaf)
            $staged = $true
        } catch { $realDest = $Dest; $staged = $false }
    }
    $tmp = "$realDest.tmp"
    $attempts = [Math]::Max(1, $Retry + 1)
    $lastErr = ''
    for ($i = 0; $i -lt $attempts; $i++) {
        if ($script:deadline -and (Get-Date) -gt $script:deadline) { throw '本次预算已用尽' }
        try {
            $p = Invoke-Web @{ Uri = $Url; OutFile = $tmp; TimeoutSec = 60; UseBasicParsing = $true }
            Invoke-WebRequest @p
            $size = (Get-Item $tmp).Length
            if ($size -lt ($MinKB * 1024)) { throw "体积异常 $size 字节" }
            Move-Item -LiteralPath $tmp -Destination $realDest -Force
            if ($staged) { [void](Publish-ToLibrary -Staged $realDest -Dest $Dest) }
            return $size
        } catch {
            $lastErr = $_.Exception.Message
            # 半成品 .tmp 留在原地，下一次同名下载会被 OutFile 直接覆盖。
            # 接管模式下它在暂存区里，退出接管随沙盒一起消失。
            if ($i -lt ($attempts - 1)) {
                $pause = [int](800 * [Math]::Pow(2, $i))
                Write-Log ("下载失败，重试 " + ($i + 1) + "/" + $Retry + "（" + $pause + " ms 后）：" + $lastErr) 'WARN'
                Start-Sleep -Milliseconds $pause
            }
        }
    }
    throw $lastErr
}

# 已知取不到的日期账本。只记「确实是这天没有」，网络问题绝不记账 ——
# 否则断网时连点几次，好几天就被永久拉黑了。
function Read-DeadDates {
    $m = @{}
    if (-not (Test-Path $DeadDatesPath)) { return $m }
    try {
        $o = Get-Content $DeadDatesPath -Raw -Encoding UTF8 | ConvertFrom-Json
        $now = Get-Date
        foreach ($q in $o.PSObject.Properties) {
            $dAt = $null
            try { $dAt = [datetime]::Parse([string]$q.Value.at) } catch { }
            # 30 天后重新尝试：镜像可能修好了
            if ($dAt -and ($now - $dAt).TotalDays -lt 30) { $m[$q.Name] = $q.Value }
        }
    } catch { }
    return $m
}

function Note-DeadDate {
    param([string]$Date8, [string]$Reason)
    if (-not $Date8) { return }
    try {
        $m = Read-DeadDates
        $m[$Date8] = @{ reason = $Reason; at = (Get-Date).ToString('s') }
        $dir = Split-Path $DeadDatesPath -Parent
        if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
        [System.IO.File]::WriteAllText($DeadDatesPath, ($m | ConvertTo-Json -Depth 4), (New-Object System.Text.UTF8Encoding($false)))
    } catch { }
}

# 某一天的图还能不能取到。三态：
#   ok      = 至少一档拿得到
#   missing = Bing 明确 404，这天确实没有/已删除 —— 可以记账
#   netfail = 超时 / DNS / 代理失败 —— 网络问题，绝不能记账
function Test-ArchiveReachable {
    param([string]$Date8, [string]$ImageId)
    if (-not $ImageId) { return 'unknown' }
    $sawNetFail = $false
    foreach ($sfx in @(($ImageId + '_UHD.jpg'), ($ImageId + '_1920x1080.jpg'))) {
        $u = 'https://www.bing.com/th?id=' + $sfx
        try {
            $p = Invoke-Web @{ Uri = $u; TimeoutSec = 8; UseBasicParsing = $true; Method = 'Head' }
            $r = Invoke-WebRequest @p
            if ($r.StatusCode -ge 200 -and $r.StatusCode -lt 400) { return 'ok' }
        } catch {
            $msg = $_.Exception.Message
            if ($msg -notmatch '404|Not Found|找不到') { $sawNetFail = $true }
        }
    }
    if ($sawNetFail) { return 'netfail' }
    return 'missing'
}# ---------------------------------------------------------------- 图库索引与补齐
# 图库目录里只允许这一个非图片文件：它是目录册，删了不影响任何东西，下次重建。
$LibraryIndexName = '_bing-chronicle-index.json'

function Get-LibraryDates {
    param([string]$Dir)
    $set = @{}
    if (-not $Dir -or -not (Test-Path $Dir)) { return $set }
    Get-ChildItem $Dir -Filter '*.jpg' -File -Recurse -ErrorAction SilentlyContinue | ForEach-Object {
        if ($_.Name -match '^(\d{8})') { $set[$matches[1]] = $true }
    }
    return $set
}

function Read-LibraryIndex {
    param([string]$Dir)
    $m = @{}
    if (-not $Dir) { return $m }
    $p = Join-Path $Dir $LibraryIndexName
    if (-not (Test-Path $p)) { return $m }
    try {
        $o = Get-Content $p -Raw -Encoding UTF8 | ConvertFrom-Json
        foreach ($q in $o.days.PSObject.Properties) { $m[$q.Name] = $q.Value }
    } catch { }
    return $m
}

function Write-LibraryIndex {
    param([string]$Dir, $Days)
    if (-not $Dir -or -not (Test-Path $Dir)) { return }
    try {
        $obj = [ordered]@{ updated = (Get-Date).ToString('s'); days = $Days }
        $p = Join-Path $Dir $LibraryIndexName
        [System.IO.File]::WriteAllText($p, ($obj | ConvertTo-Json -Depth 5), (New-Object System.Text.UTF8Encoding($false)))
    } catch { Write-Log ('图库索引写入失败：' + $_.Exception.Message) 'WARN' }
}


# ---------------------------------------------------------------- 设壁纸

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
        try { New-Item -ItemType Directory -Path $ArchiveDir -Force | Out-Null } catch { Write-Log ('归档目录创建失败：' + $_.Exception.Message) 'WARN' }
    }
    $resp = $null
    # 有本地代理就先用它直连 raw：比绕 gh-proxy.com 这个第三方中转快，也不把请求交给它
    # 全量索引和月度索引走同一套候选与记账：1.6 MB 的文件
    # 哪条通道快差别更明显，这里最值得按实测排序。
    # 注意 RelPath 要带仓库前缀，候选构造只负责拼 主机 + RelPath
    $resp = Invoke-ArchiveFetch -RelPath 'Zhu-junwei/bing-wallpaper-archive/master/Bing_zh-CN_all.json' -TimeoutSec 40
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

    # 老存档（2009-07-13 ~ 2016-03-04，实测 2424 条）：主索引够不到这一段。
    # 不并进来的话这 6 年半的图关键词搜索一张都搜不到（实测搜「袋貂」返回空）。
    try {
        $seenOld = @{}
        foreach ($it in $idx) { $seenOld[[string]$it.d] = $true }
        $oldMap2 = Get-OldIndexMap
        if ($oldMap2 -and $oldMap2.Count -gt 0) {
            $mergedOld = @($idx)
            $addedOld = 0
            foreach ($ok2 in @($oldMap2.Keys)) {
                $kd = [string]$ok2
                if ($kd.Length -ne 8) { continue }
                if ($seenOld.ContainsKey($kd)) { continue }
                $ov = $oldMap2[$ok2]
                $mergedOld += [ordered]@{
                    d = $kd
                    t = [string]$ov.t
                    c = [string]$ov.c
                    l = ''
                    i = Get-ImageIdFromFileName ([string]$ov.n)
                }
                $addedOld++
            }
            $idx = $mergedOld
            Write-Log ('搜索索引：并入老存档 ' + $addedOld + ' 天，合计 ' + $idx.Count + ' 条')
        }
    } catch {
        Write-Log ('老存档并入失败：' + $_.Exception.Message) 'WARN'
    }

    $deadMap = Read-DeadDates
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
                dead      = $deadMap.ContainsKey([string]$item.d)
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

    # 死日期（索引里有、原图已 404）跳过；全被过滤掉就退回旧行为 ——
    # 宁可让用户点一次失败，也不能让翻页整体失效
    try {
        $deadN = Read-DeadDates
        if ($deadN -and $deadN.Count -gt 0) {
            $alive = @($dates | Where-Object { -not $deadN.ContainsKey([string]$_) })
            if ($alive.Count -gt 0) { $dates = $alive }
        }
    } catch { }

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

# 翻页越界接续：先预检再交棒，死日期自动往同方向再退一天（最多 3 次）。
# 不预检的话，撞上「索引里有、原图 404」的日期会直接给用户一个失败弹窗。
function Invoke-NeighborFallback {
    param([string]$From, [int]$Step, [switch]$Gallery, [int]$BudgetSec)

    $cur = $From
    $tried = @()
    for ($k = 0; $k -lt 3; $k++) {
        $nd = Get-NeighborDate -From $cur -Step $Step
        if (-not $nd) { break }
        if ($tried -contains $nd) { break }
        $tried += $nd

        # 本地已有这张就别发 HEAD 请求
        $haveLocal = $false
        foreach ($dirL in @($FavDir, $CacheDir, $ArchiveImgDir)) {
            if (-not $dirL -or -not (Test-Path $dirL)) { continue }
            $fL = Get-ChildItem $dirL -Filter "${nd}_*.jpg" -File -ErrorAction SilentlyContinue | Select-Object -First 1
            if ($fL) { $haveLocal = $true; break }
        }

        if (-not $haveLocal) {
            $iid = ''
            try {
                $recO = Get-OldIndexRecord -Date $nd
                if ($recO) { $iid = Get-ImageIdFromFileName ([string]$recO.n) }
                if (-not $iid) {
                    $hitI = @(@(Get-FullIndex) | Where-Object { [string]$_.d -eq $nd }) | Select-Object -First 1
                    if ($hitI) { $iid = [string]$hitI.i }
                }
            } catch { }

            $rv = Test-ArchiveReachable -Date8 $nd -ImageId $iid
            if ($rv -eq 'missing') {
                Note-DeadDate -Date8 $nd -Reason 'step-missing'
                Write-Log ('跳过 ' + $nd + '（原图已不存在），往同方向再找一天') 'WARN'
                $cur = $nd
                continue
            }
            if ($rv -eq 'netfail') {
                Write-Log ('预检 ' + $nd + ' 时网络不通，停止继续找') 'ERROR'
                Exit-Run 4
            }
        }

        Write-Log ('相对移动（跨窗口）: ' + $From + ' Step=' + $Step + ' -> ' + $nd)
        & $PSCommandPath -Date $nd -Gallery:$Gallery -BudgetSec $BudgetSec
        exit $LASTEXITCODE
    }

    Write-Log ('翻页接续失败：试过 ' + ($tried -join ' / ')) 'ERROR'
    Exit-Run 5
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

    # 接管模式只增不减：源就在图库里时只能复制一份出去，绝不移走或删除
    $inLibrary = ($script:TakeoverRoot -and $Src -and
                  $Src.StartsWith($script:TakeoverRoot, [System.StringComparison]::OrdinalIgnoreCase))
    if ($inLibrary) {
        Copy-Item -LiteralPath $Src -Destination $Dest -Force
        Write-Log ('接管模式：复制一份到 ' + (Split-Path $Dest -Leaf) + '，图库原件保留')
        return
    }

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
    } catch { Write-Log ('保护名单写入失败：' + $_.Exception.Message) 'WARN' }
}

# ---------------------------------------------------------------- 无 UHD 名单

# 判断画质只能看真实分辨率，不能看文件大小 —— 实测同一个 _UHD 地址有时返回 1920×1080
# （20230116、20230204），有时返回 3840×2160 但只有 2.3 MB（20230519），
# 还有同一地址两次请求体积不一样的情况（20230618：1.4 MB / 3.9 MB）。
function Get-ImagePixelWidth {
    param([string]$Path)
    if (-not $Path -or -not (Test-Path -LiteralPath $Path)) { return 0 }
    try {
        if (-not ('System.Drawing.Image' -as [type])) { Add-Type -AssemblyName System.Drawing }
        $img = [System.Drawing.Image]::FromFile($Path)
        $w = $img.Width
        $img.Dispose()
        return [int]$w
    } catch { Write-Log ('读取图片尺寸失败：' + $Path + ' - ' + $_.Exception.Message) 'WARN' }
    return 0
}

# 宽度不到 3000 就不是 UHD（3840 才算，1080p 是 1920）
function Test-LowResImage {
    param([string]$Path)
    $w = Get-ImagePixelWidth -Path $Path
    return ($w -gt 0 -and $w -lt 3000)
}

# 2019 年起 Bing 一般都有 UHD，但个别日期实测拿不到（_UHD 地址返回的是 1080p 内容）。
# 升级尝试失败后记下这一天，下次不再白等一次下载。
function Get-NoUhdSet {
    $p = Join-Path $CacheDir 'no-uhd.json'
    $set = @{}
    if (Test-Path $p) {
        try {
            $old = Get-Content $p -Raw -Encoding UTF8 | ConvertFrom-Json
            foreach ($k in @($old)) { $kk = [string]$k; if ($kk.Length -eq 8) { $set[$kk] = $true } }
        } catch { Write-Log ('无 UHD 名单读取失败：' + $_.Exception.Message) 'WARN' }
    }
    return $set
}

function Test-NoUhd {
    param([string]$Date8)
    if (-not $Date8) { return $false }
    $set = Get-NoUhdSet
    return $set.ContainsKey($Date8)
}

function Note-NoUhd {
    param([string]$Date8)
    if (-not $Date8 -or $Date8.Length -ne 8) { return }
    $set = Get-NoUhdSet
    if ($set.ContainsKey($Date8)) { return }
    $set[$Date8] = $true
    $p = Join-Path $CacheDir 'no-uhd.json'
    try {
        [System.IO.File]::WriteAllText($p, (@($set.Keys) | ConvertTo-Json), (New-Object System.Text.UTF8Encoding($false)))
    } catch { Write-Log ('无 UHD 名单写入失败：' + $_.Exception.Message) 'WARN' }
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
        # 当前壁纸优先于一切判断：先保住正在用的这张，再考虑删重复副本
        if ($cur -and $_.FullName -ieq $cur) { $protected++; return }
        if (Test-Path -LiteralPath (Join-Path $FavDir $_.Name)) {
            if ($DryRun) { Write-Log "[DryRun] 将删除重复副本 $($_.Name)" }
            else { try { [System.IO.File]::Delete($_.FullName); $removed++ } catch { Write-Log ('重复副本删除失败：' + $_.Name) 'WARN' } }
            return
        }
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
        } catch { Write-Log ('缓存索引同步失败：' + $_.Exception.Message) 'WARN' }
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
        # 回收站不可用（被禁用/网络盘）时降级为直接删，并留一行日志 ——
        # 否则用户会以为文件还在回收站里，实际已经永久没了
        try {
            [System.IO.File]::Delete($Path)
            Write-Log ("回收站不可用，已直接删除：" + $Path) 'WARN'
            return $true
        } catch { return $false }
    }
}

# 清掉每日故事的本地缓存，返回删除条数。
# 故事是可再生数据：删了只是下次查看那天时重抓一次，不影响壁纸与索引，
# 所以它既能单独清，也能挂在「清理历史归档」里当可选步骤一起清。
function Clear-StoryCache {
    param([string]$Mode = 'recycle')
    $n = 0
    if (-not (Test-Path $StoriesDir)) { return 0 }
    Get-ChildItem $StoriesDir -Filter '*.txt' -File -ErrorAction SilentlyContinue | ForEach-Object {
        if ($DryRun) { Write-Log "[DryRun] 将删除故事 $($_.Name)"; $n++ }
        elseif (Remove-OneFile -Path $_.FullName -Mode $Mode) { $n++ }
    }
    return $n
}

function Clear-ArchiveBySize {
    param([int]$MaxMB)
    if ($MaxMB -le 0) { return 0 }
    # 接管模式只读：$ArchiveImgDir 这时是用户自己的图库，容量上限是本软件的缓存策略，
    # 不能拿它去删别人的图 —— 何况这里用的是 File::Delete，永久删除，不进回收站。
    # 少了这段，「换一张壁纸」就会顺带清空一个几十 GB 的图库。
    if ($script:Takeover) { return 0 }
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

# 测试用入口：直接跑一次归档两轮清理，只配合 -TestArchiveDir 沙盒
if ($TestArchiveSweep -gt 0) {
    if (-not $TestArchiveDir) {
        Write-Host '需同时给 -TestArchiveDir 并设置 BING_TEST_SANDBOX 指向 BWTEST 沙盒'
        exit 4
    }
    $sweepN = Clear-ArchiveBySize -MaxMB $TestArchiveSweep
    Write-Host ('清理返回：' + $sweepN)
    exit 0
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
    # 照片墙缩略图也是缓存：纯派生数据，删了下次打开照片墙会按需重建，不动任何原图。
    # cache\wall（照片墙临时原图）不在此列 —— 那条路有关窗询问的语义，不该被顺手抹掉。
    $thumbDir = Join-Path $CacheDir 'thumbs'
    $thumbN = 0
    if (Test-Path $thumbDir) {
        foreach ($tf in @(Get-ChildItem $thumbDir -Filter '*.jpg' -File -ErrorAction SilentlyContinue)) {
            if ($DryRun) { Write-Log ("[DryRun] 将删除缩略图 " + $tf.Name); $removed++; $thumbN++; continue }
            $szT = $tf.Length
            if (Remove-OneFile -Path $tf.FullName -Mode $Mode) { $removed++; $freed += $szT; $thumbN++ }
        }
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
            } catch { Write-Log ('缓存索引同步失败：' + $_.Exception.Message) 'WARN' }
        }
    }
    Write-Log "清空缓存：删除 $removed 项，保留当前壁纸 $kept 个"
    if ($thumbN -gt 0) {
        Write-Log ("照片墙缩略图已清（$thumbN 张）：下次打开照片墙会按需重建")
    }
    return $removed
}

# ---------------------------------------------------------------- 状态读写

function Read-State {
    if (Test-Path $StatePath) {
        try { return Get-Content $StatePath -Raw -Encoding UTF8 | ConvertFrom-Json } catch { Write-Log ('state 读取失败：' + $_.Exception.Message) 'WARN' }
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
    $upW = 0
    # 补 UHD 的总时长封顶，超时就只搬现有的预览档
    $upDeadline = (Get-Date).AddSeconds(60)
    foreach ($f in @(Get-ChildItem $WallDir -Filter '*.jpg' -File -ErrorAction SilentlyContinue)) {
        $mW = [regex]::Match($f.Name, '^(\d{8})_')
        $destW = Join-Path $ArchiveImgDir $f.Name
        if (Test-Path -LiteralPath $destW) { $destW = Join-Path $ArchiveImgDir ($f.BaseName + '_' + (Get-Date -Format 'HHmmss') + '.jpg') }
        try {
            # 预览档先进归档（原目录随即变空，不涉及删除）
            Move-Item -LiteralPath $f.FullName -Destination $destW -Force
            # 墙里放的多半是 1080p 预览档：归档时补一张 UHD 覆盖过去，补不到就留着预览档
            if ($mW.Success -and (Get-Date) -lt $upDeadline -and $f.Length -lt 3MB -and (Test-LowResImage -Path $f.FullName)) {
                $d8W = $mW.Groups[1].Value
                try {
                    $recW = Get-ArchiveRecord -Date $d8W
                    if ($recW -and $recW.id) {
                        $tmpW = Join-Path $CacheDir ('.wallfull_' + $d8W + '.jpg')
                        $szW = Save-BingImage -Url ("https://www.bing.com/th?id=" + [string]$recW.id + '_UHD.jpg') -Dest $tmpW -MinKB 300
                        if ($szW -gt 0 -and (Test-Path -LiteralPath $tmpW)) {
                            Move-Item -LiteralPath $tmpW -Destination $destW -Force
                            $upW++
                            Write-Log ("照片墙补 UHD 成功：$d8W " + [math]::Round($szW / 1MB, 2) + ' MB')
                        }
                    }
                } catch {
                    Write-Log ("照片墙补 UHD 失败：" + $d8W + " " + $_.Exception.Message) 'WARN'
                }
            }
            if ($mW.Success) { Add-Pinned -Date $mW.Groups[1].Value }
            $movedW++
        } catch {
            Write-Log ("照片墙暂存失败：" + $f.Name + " " + $_.Exception.Message) 'WARN'
        }
    }
    Write-Host ("已暂存 $movedW 张到历史归档（其中 $upW 张补到 UHD）")
    exit 0
}

if ($WallDrop) {
    $droppedW = 0
    if (Test-Path $WallDir) {
        foreach ($f in @(Get-ChildItem $WallDir -Filter '*.jpg' -File -ErrorAction SilentlyContinue)) {
            try { Remove-Item -LiteralPath $f.FullName -Force -ErrorAction Stop; $droppedW++ } catch { }
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

# ---------------------------------------------------------------- 老存档归位

# 3.6.48 及更早版本有个 bug：老存档（2009-08 ~ 2016-02）的落盘目录被 Get-DownloadDir 覆盖，
# 图混进了 cache 根或 archive\images。这个开关把认得出的老存档图挪回 archive\old\<YYYYMM>\。
if ($MigrateOld) {
    $oldIdxP = Join-Path $ArchiveDir 'old-index.json'
    if (-not (Test-Path $oldIdxP)) { Write-Host '缺 archive\old-index.json，先正常启动一次让它释放'; exit 1 }
    $oldSet = @{}
    try {
        $tmpMig = Get-Content $oldIdxP -Raw -Encoding UTF8 | ConvertFrom-Json
        foreach ($e in @($tmpMig)) { $kM = [string]$e.d; if ($kM.Length -eq 8) { $oldSet[$kM] = $true } }
    } catch { }
    if ($oldSet.Count -eq 0) { Write-Host '老索引读不出来'; exit 1 }

    $movedM = 0
    $skipM = 0
    $dupM = 0
    $keepM = 0
    # 正在用的壁纸与 state 指向的那张原地不动：移走会让桌面和「这张图是哪儿」同时断掉
    $curM = ''
    try { $curM = [string](Get-ItemProperty 'HKCU:\Control Panel\Desktop' -Name Wallpaper -ErrorAction SilentlyContinue).Wallpaper } catch { }
    $curM2 = ''
    try { $stM = Read-State; if ($stM -and $stM.file) { $curM2 = [string]$stM.file } } catch { }
    foreach ($srcDir in @($CacheDir, $ArchiveImgDir)) {
        if (-not (Test-Path $srcDir)) { continue }
        foreach ($f in @(Get-ChildItem $srcDir -Filter '*.jpg' -File -ErrorAction SilentlyContinue)) {
            $mm = [regex]::Match($f.Name, '^(\d{8})_')
            if (-not $mm.Success) { $skipM++; continue }
            $d8m = $mm.Groups[1].Value
            if (-not $oldSet.ContainsKey($d8m)) { continue }
            if ($curM -and $f.FullName -ieq $curM) { $keepM++; continue }
            if ($curM2 -and $f.FullName -ieq $curM2) { $keepM++; continue }
            $dstDir = Join-Path $OldDir $d8m.Substring(0, 6)
            $dst = Join-Path $dstDir $f.Name
            if (Test-Path -LiteralPath $dst) { $dupM++; continue }
            if ($DryRun) {
                Write-Host ("[DryRun] " + $f.FullName + " → " + $dst)
                $movedM++
                continue
            }
            if (-not (Test-Path $dstDir)) { New-Item -ItemType Directory -Path $dstDir -Force | Out-Null }
            try { Move-Item -LiteralPath $f.FullName -Destination $dst; $movedM++ }
            catch { Write-Host ("移动失败：" + $f.Name + " - " + $_.Exception.Message) }
        }
    }
    $tagM = if ($DryRun) { '[DryRun] ' } else { '' }
    Write-Host ($tagM + "老存档归位：移动 $movedM 个，同名已存在 $dupM 个，正在使用跳过 $keepM 个，非日期文件名跳过 $skipM 个")
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


# ---------------------------------------------------------------- 按节日筛选
# 节日日期用农历算法现场算，不维护数据表 —— 明年自动就对了，不需要更新任何东西。
# 覆盖：春节/元宵/端午/中秋/重阳（农历）、除夕（腊月最后一天）、清明（节气）、元旦/国庆（公历）。

if ($Festivals -or $Festival) {
    $script:LunarInfo = @(
    0x04bd8, 0x04ae0, 0x0a570, 0x054d5, 0x0d260, 0x0d950, 0x16554, 0x056a0, 0x09ad0, 0x055d2,
    0x04ae0, 0x0a5b6, 0x0a4d0, 0x0d250, 0x1d255, 0x0b540, 0x0d6a0, 0x0ada2, 0x095b0, 0x14977,
    0x04970, 0x0a4b0, 0x0b4b5, 0x06a50, 0x06d40, 0x1ab54, 0x02b60, 0x09570, 0x052f2, 0x04970,
    0x06566, 0x0d4a0, 0x0ea50, 0x06e95, 0x05ad0, 0x02b60, 0x186e3, 0x092e0, 0x1c8d7, 0x0c950,
    0x0d4a0, 0x1d8a6, 0x0b550, 0x056a0, 0x1a5b4, 0x025d0, 0x092d0, 0x0d2b2, 0x0a950, 0x0b557,
    0x06ca0, 0x0b550, 0x15355, 0x04da0, 0x0a5b0, 0x14573, 0x052b0, 0x0a9a8, 0x0e950, 0x06aa0,
    0x0aea6, 0x0ab50, 0x04b60, 0x0aae4, 0x0a570, 0x05260, 0x0f263, 0x0d950, 0x05b57, 0x056a0,
    0x096d0, 0x04dd5, 0x04ad0, 0x0a4d0, 0x0d4d4, 0x0d250, 0x0d558, 0x0b540, 0x0b6a0, 0x195a6,
    0x095b0, 0x049b0, 0x0a974, 0x0a4b0, 0x0b27a, 0x06a50, 0x06d40, 0x0af46, 0x0ab60, 0x09570,
    0x04af5, 0x04970, 0x064b0, 0x074a3, 0x0ea50, 0x06b58, 0x055c0, 0x0ab60, 0x096d5, 0x092e0,
    0x0c960, 0x0d954, 0x0d4a0, 0x0da50, 0x07552, 0x056a0, 0x0abb7, 0x025d0, 0x092d0, 0x0cab5,
    0x0a950, 0x0b4a0, 0x0baa4, 0x0ad50, 0x055d9, 0x04ba0, 0x0a5b0, 0x15176, 0x052b0, 0x0a930,
    0x07954, 0x06aa0, 0x0ad50, 0x05b52, 0x04b60, 0x0a6e6, 0x0a4e0, 0x0d260, 0x0ea65, 0x0d530,
    0x05aa0, 0x076a3, 0x096d0, 0x04afb, 0x04ad0, 0x0a4d0, 0x1d0b6, 0x0d250, 0x0d520, 0x0dd45,
    0x0b5a0, 0x056d0, 0x055b2, 0x049b0, 0x0a577, 0x0a4b0, 0x0aa50, 0x1b255, 0x06d20, 0x0ada0,
    0x14b63, 0x09370, 0x049f8, 0x04970, 0x064b0, 0x168a6, 0x0ea50, 0x06b20, 0x1a6c4, 0x0aae0,
    0x092e0, 0x0d2e3, 0x0c960, 0x0d557, 0x0d4a0, 0x0da50, 0x05d55, 0x056a0, 0x0a6d0, 0x055d4,
    0x052d0, 0x0a9b8, 0x0a950, 0x0b4a0, 0x0b6a6, 0x0ad50, 0x055a0, 0x0aba4, 0x0a5b0, 0x052b0,
    0x0b273, 0x06930, 0x07337, 0x06aa0, 0x0ad50, 0x14b55, 0x04b60, 0x0a570, 0x054e4, 0x0d160,
    0x0e968, 0x0d520, 0x0daa0, 0x16aa6, 0x056d0, 0x04ae0, 0x0a9d4, 0x0a2d0, 0x0d150, 0x0f252,
    0x0d520
    )

    function Get-LunarMonthDays([int]$y, [int]$m) {
        $v = $script:LunarInfo[$y - 1900]
        if (($v -band (0x10000 -shr $m)) -ne 0) { return 30 }
        return 29
    }
    function Get-LunarLeapMonth([int]$y) { return ($script:LunarInfo[$y - 1900] -band 0xf) }
    function Get-LunarLeapDays([int]$y) {
        if ((Get-LunarLeapMonth $y) -eq 0) { return 0 }
        if (($script:LunarInfo[$y - 1900] -band 0x10000) -ne 0) { return 30 }
        return 29
    }
    function Get-LunarYearDays([int]$y) {
        $sum = 0
        for ($m = 1; $m -le 12; $m++) { $sum += (Get-LunarMonthDays $y $m) }
        return $sum + (Get-LunarLeapDays $y)
    }
    # 农历正月初一对应的公历日期；1900-01-31 是 1900 年的正月初一
    function Get-LunarNewYear([int]$y) {
        $off = 0
        for ($i = 1900; $i -lt $y; $i++) { $off += (Get-LunarYearDays $i) }
        return ([datetime]'1900-01-31').AddDays($off)
    }
    # 农历 y 年 m 月 d 日 -> 公历
    function Get-LunarToSolar([int]$y, [int]$m, [int]$d) {
        $add = 0
        for ($mm = 1; $mm -lt $m; $mm++) {
            $add += (Get-LunarMonthDays $y $mm)
            if ((Get-LunarLeapMonth $y) -eq $mm) { $add += (Get-LunarLeapDays $y) }
        }
        return (Get-LunarNewYear $y).AddDays($add + $d - 1)
    }
    # 清明：闰年与闰后一年在 4 月 4 日，其余 4 月 5 日（适用于 2000-2040）
    function Get-Qingming([int]$y) {
        $leap = [datetime]::IsLeapYear($y)
        $prev = [datetime]::IsLeapYear($y - 1)
        if ($leap -or $prev) { return (New-Object datetime($y, 4, 4)) }
        return (New-Object datetime($y, 4, 5))
    }
    # 某年某节日的公历日期；算不出来返回 $null
    function Get-FestivalDate([string]$name, [int]$y) {
        switch ($name) {
            '春节'   { return (Get-LunarToSolar $y 1 1) }
            '元宵节' { return (Get-LunarToSolar $y 1 15) }
            '清明节' { return (Get-Qingming $y) }
            '端午节' { return (Get-LunarToSolar $y 5 5) }
            '中秋节' { return (Get-LunarToSolar $y 8 15) }
            '重阳节' { return (Get-LunarToSolar $y 9 9) }
            '除夕'   { return (Get-LunarNewYear ($y + 1)).AddDays(-1) }
            '元旦'   { return (New-Object datetime($y, 1, 1)) }
            '国庆节' { return (New-Object datetime($y, 10, 1)) }
        }
        return $null
    }

    $idxTitles = @{}
    foreach ($idxName in @('old-index.json', 'all-index.json')) {
        $p2 = Join-Path $ArchiveDir $idxName
        if (-not (Test-Path $p2)) { continue }
        try {
            $arr2 = Get-Content $p2 -Raw -Encoding UTF8 | ConvertFrom-Json
            foreach ($e2 in @($arr2)) {
                $d2 = [string]$e2.d
                if ($d2.Length -eq 8) { $idxTitles[$d2] = [string]$e2.t }
            }
        } catch { }
    }
    if ($idxTitles.Count -eq 0) { Write-Host '索引读不出来，先正常启动一次让它释放'; exit 1 }

    $allNames = @('春节', '元宵节', '清明节', '端午节', '中秋节', '重阳节', '国庆节', '元旦', '除夕')
    $wantNames = $allNames
    if ($Festival) {
        $wantNames = @($allNames | Where-Object { $_ -eq $Festival -or $_ -like ((($Festival -replace '节$', '')) + '*') })
        if ($wantNames.Count -eq 0) { Write-Host ('没有这个节日：' + $Festival); exit 1 }
    }

    $yStart = 2009
    $yEnd = (Get-Date).Year
    $result = [ordered]@{}
    foreach ($nm in $wantNames) {
        $list = @()
        for ($y = $yStart; $y -le $yEnd; $y++) {
            $dt = $null
            try { $dt = Get-FestivalDate $nm $y } catch { }
            if (-not $dt) { continue }
            $k = $dt.ToString('yyyyMMdd')
            if ($idxTitles.ContainsKey($k)) { $list += $k }
        }
        $result[$nm] = @($list)
    }

    if ($Festival) {
        $one = $wantNames[0]
        $payload = [ordered]@{ name = $one; dates = @($result[$one]); count = @($result[$one]).Count }
        Write-Output ($payload | ConvertTo-Json -Compress)
        exit 0
    }
    foreach ($nm in $wantNames) {
        Write-Host ('{0}  {1} 天' -f $nm, @($result[$nm]).Count)
    }
    Write-Host '按节日筛选（只读）'
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
    $startD = [datetime]::ParseExact('20090713', 'yyyyMMdd', $null)
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
    $segCnt = @($dsSorted | Where-Object { $_ -ge '20090713' -and $_ -le '20141231' }).Count
    $segNeed = [int](([datetime]::ParseExact('20141231', 'yyyyMMdd', $null) - [datetime]::ParseExact('20090713', 'yyyyMMdd', $null)).TotalDays) + 1
    Write-Host ('2009-07-13 ~ 2014-12-31：{0} 条 / 应有 {1} 天 / 缺 {2} 天' -f $segCnt, $segNeed, ($segNeed - $segCnt))
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

    # 2) / 3) 只带日期、没有 slug 的文件（故事缓存、保留名单）：改写不幂等 ——
    #    改完一轮后文件名已在新日期上，再次执行会被旧索引反查推得更后。
    #    所以默认不动，需要时加 -FixDatedFiles 显式开启，且只跑一次。
    if ($FixDatedFiles) {

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
    }


    # 4) 收藏索引：键必须跟文件名里的日期一致，否则壁纸库按日期找不到文件
    if ((Test-Path $FavIndex) -and $FavDir -and (Test-Path $FavDir)) {
        $favChanged = 0
        try {
            $favRaw = Get-Content $FavIndex -Raw -Encoding UTF8 | ConvertFrom-Json
            if ($favRaw) {
                $favPair = @{}
                foreach ($prop in $favRaw.PSObject.Properties) { $favPair[$prop.Name] = $prop.Value }
                $favNew = [ordered]@{}
                foreach ($fk in @($favPair.Keys | Sort-Object)) {
                    $rec = $favPair[$fk]
                    $fName = [string]$rec.file
                    $nKey = $fk
                    if ($fName.Length -ge 8 -and $fName.Substring(0, 8) -match '^\d{8}$' -and $fName.Substring(0, 8) -ne $fk) {
                        $nKey = $fName.Substring(0, 8)
                        $favChanged++
                    }
                    $favNew[$nKey] = $rec
                }
                if ($favChanged -gt 0) {
                    if ($DryRun) {
                        Write-Host ('[DryRun] 收藏索引：同步 ' + $favChanged + ' 个日期键')
                    } else {
                        [System.IO.File]::WriteAllText($FavIndex, (ConvertTo-Json -InputObject $favNew -Depth 6), (New-Object System.Text.UTF8Encoding($false)))
                        Write-Host ('收藏索引：同步 ' + $favChanged + ' 个日期键')
                    }
                    [void]$log.Add('favorites\index.json  改键 ' + $favChanged + ' 个')
                }
            }
        } catch {
            Write-Host '收藏索引同步失败，已跳过' -ForegroundColor Yellow
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

    # 故事映射表新鲜度：静态打包的表会随时间过期，过期后新日子取不到故事
    $smNow = @{}
    $storyLast = ''
    $storyStale = 0
    try {
        $smNow = Get-StoryMap
        if ($smNow.Count -gt 0) {
            $storyLast = (@($smNow.Keys) | Sort-Object)[-1]
            $storyStale = [int]((Get-Date).Date - [datetime]::ParseExact($storyLast, 'yyyyMMdd', $null)).Days
        }
    } catch { }

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
        storyMap = [ordered]@{
            entries  = $(if ($storyLast) { $smNow.Count } else { 0 })
            lastDate = $storyLast
            staleDays = $storyStale
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
        if ($storyLast) {
            $staleMsg = if ($storyStale -gt 3) { ' ← 已过期 ' + $storyStale + ' 天，跑 -UpdateStoryMap 可补' } else { '' }
            Write-Output ('故事映射 ' + $smNow.Count + ' 条，最晚 ' + $storyLast + $staleMsg)
        }
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
    # 死日期（索引有、原图 404）不抽给用户
    try {
        $deadP = Read-DeadDates
        if ($deadP -and $deadP.Count -gt 0) {
            $aliveP = @($idx | Where-Object { -not $deadP.ContainsKey([string]$_.d) })
            if ($aliveP.Count -gt 0) { $idx = $aliveP }
        }
    } catch { }

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

if ($AuditQuality) {
    # 画质体检：只读不写，输出 JSON + _quality-audit.csv
    $dirA = [string]$DestDir
    if (-not $dirA -or -not (Test-Path $dirA)) {
        Write-Output '{"total":0,"uhd":0,"low":0,"lowlist":[],"error":"目录不存在"}'
        Exit-Run 1
    }
    if (-not ('System.Drawing.Image' -as [type])) { try { Add-Type -AssemblyName System.Drawing } catch { } }
    $filesA = @(Get-ChildItem $dirA -Filter *.jpg -File -ErrorAction SilentlyContinue | Sort-Object Name)
    $uhdA = 0
    $lowA = 0
    $badA = 0
    $lowListA = New-Object System.Collections.ArrayList
    $badListA = New-Object System.Collections.ArrayList
    $csvA = New-Object System.Collections.ArrayList
    [void]$csvA.Add('date,width,height,mb,verdict')
    foreach ($fA in $filesA) {
        $kA = $fA.BaseName
        if ($kA.Length -ge 8) { $kA = $kA.Substring(0, 8) }
        $wA = 0
        $hA = 0
        try {
            $imgA = [System.Drawing.Image]::FromFile($fA.FullName)
            $wA = $imgA.Width
            $hA = $imgA.Height
            $imgA.Dispose()
        } catch { }
        $verdict = 'uhd'
        if ($wA -le 0) { $verdict = 'unreadable'; $badA++; [void]$badListA.Add($kA) }
        elseif ($wA -lt 3000) { $verdict = 'low'; $lowA++; [void]$lowListA.Add($kA) }
        else { $uhdA++ }
        [void]$csvA.Add(('{0},{1},{2},{3},{4}' -f $kA, $wA, $hA, [math]::Round($fA.Length / 1MB, 2), $verdict))
    }
    try { $csvA | Set-Content (Join-Path $dirA '_quality-audit.csv') -Encoding UTF8 }
    catch { Write-Log ('体检 CSV 写入失败：' + $_.Exception.Message) 'WARN' }
    $lowJson = '[]'
    if ($lowListA.Count -gt 0) {
        $partsA = @()
        foreach ($x in $lowListA) { $partsA += ('"' + $x + '"') }
        $lowJson = '[' + ($partsA -join ',') + ']'
    }
    Write-Log ('画质体检：共 ' + $filesA.Count + ' 张，UHD ' + $uhdA + '，非 UHD ' + $lowA + '，读不出 ' + $badA)
    $badJson = '[]'
    if ($badListA.Count -gt 0) {
        $partsB = @()
        foreach ($x in $badListA) { $partsB += ('"' + $x + '"') }
        $badJson = '[' + ($partsB -join ',') + ']'
    }
    Write-Output ('{"total":' + $filesA.Count + ',"uhd":' + $uhdA + ',"low":' + $lowA + ',"bad":' + $badA + ',"lowlist":' + $lowJson + ',"badlist":' + $badJson + ',"error":""}')
    exit 0
}

if ($ExportRange) {
    # 按日期范围批量下载：目标已有同名则跳过（可断点续跑）
    # 源：cdn = Bing 官方（只有 2018-09 之后还有效）；anerg = bingwallpaper.anerg.com 的图库（2009-2018 都能用）
    # 命名统一为 {日期}_{slug}.jpg —— 接管模式靠 GetFiles(dir, "日期_*.jpg") 找图，前缀必须是 8 位日期
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

    # 老索引（2009-2016）：文件名 n、备用名 a
    $oldMap = @{}
    $pOld = Join-Path $ArchiveDir 'old-index.json'
    if (Test-Path $pOld) {
        try {
            $tmpOld = Get-Content $pOld -Raw -Encoding UTF8 | ConvertFrom-Json
            foreach ($x in @($tmpOld)) { $oldMap[[string]$x.d] = $x }
        } catch { }
    }

    function Get-ShortSlug([string]$date, $oldRec, $newRec) {
        $slug = ''
        if ($oldRec) {
            $slug = [string]$oldRec.n
            $slug = $slug -replace '_1920x1080\.jpg$', '' -replace '\.jpg$', ''
        }
        if (-not $slug -and $newRec) {
            $slug = [string]$newRec.i
            $slug = $slug -replace '_[A-Za-z]{2}-[A-Za-z]{2}\d+$', ''
        }
        if (-not $slug) { return '' }
        return ($slug -split '_')[0]
    }

    $okN = 0
    $skipN = 0
    $failN = 0
    $upN = 0
    $firstErr = ''
    $failList = New-Object System.Collections.ArrayList
    $resLog = @{}
    $uhdN = 0
    $lowN = 0
    $lowList = New-Object System.Collections.ArrayList

    $day = [datetime]::ParseExact($d1, 'yyyyMMdd', $null)
    $end = [datetime]::ParseExact($d2, 'yyyyMMdd', $null)
    while ($day -le $end) {
        $cur = $day.ToString('yyyyMMdd')
        $day = $day.AddDays(1)

        $had = @(Get-ChildItem $DestDir -Filter "${cur}_*.jpg" -File -ErrorAction SilentlyContinue) | Select-Object -First 1
        $upRange = $false
        if ($had) {
            # 已有同名文件。默认还要看画质：直接读宽度（2-5 ms，比体积可靠），
            # 确认不是 UHD 就重下覆盖这一份。
            #   - OnlyLowRes：专门补画质，等价于「只处理低画质」
            #   - KeepExisting：旧的「有就跳过」（-OnlyLowRes 优先）
            #   - no-uhd 名单内的日期不再白试：源头就没有 UHD，重下多少次都一样
            if ($OnlyLowRes -or (-not $KeepExisting)) {
                if ($MaxRes -eq 'uhd' -and $cur -ge '20190101' -and -not (Test-NoUhd -Date8 $cur)) {
                    # 读不出宽度（0）说明文件坏了/不是 JPEG，也要重下 —— 别当成"正常"放过
                    $wChk = Get-ImagePixelWidth -Path $had.FullName
                    if ($wChk -le 0 -or $wChk -lt 3000) { $upRange = $true }
                }
            }
            if (-not $upRange) { $skipN++; continue }
            $upN++
        } elseif ($OnlyLowRes) {
            # 只补低画质：本地没有的日子一律跳过，不新下
            $skipN++
            continue
        }

        $oldRec = $oldMap[$cur]
        $newRec = $byDate[$cur]
        if (-not $oldRec -and -not $newRec) {
            $failN++
            [void]$failList.Add("$cur`t索引里没有这一天")
            if (-not $firstErr) { $firstErr = "$cur 索引里没有这一天" }
            continue
        }
        $short = Get-ShortSlug $cur $oldRec $newRec
        if (-not $short) {
            $failN++
            [void]$failList.Add("$cur`t拿不到 slug")
            if (-not $firstErr) { $firstErr = "$cur 缺少 slug" }
            continue
        }

        # 选源：auto 时 2018-09 之后走 Bing，之前走 anerg，失败就换另一个
        $order = @()
        # bimg = cdn.bimg.cc 直链：2016-03 ~ 2019-12 这段 Bing 自己的 th?id 两档全 404，
        # 它实测能取到 1080p，是那个区间唯一的出路。放在 cdn 之后、anerg 之前。
        if ($MaxRes -eq 'hd') { $order = @('anerg', 'cdn') }
        elseif ($Source -eq 'cdn') { $order = @('cdn', 'bimg', 'anerg') }
        elseif ($Source -eq 'anerg') { $order = @('anerg', 'bimg', 'cdn') }
        else { if ($cur -ge '20180901') { $order = @('cdn', 'bimg', 'anerg') } else { $order = @('anerg', 'bimg', 'cdn') } }

        $destFile = Join-Path $DestDir "${cur}_$short.jpg"
        if ($upRange -and $had) { $destFile = $had.FullName }
        $saved = $false
        $lastMsg = ''
        foreach ($src in $order) {
            if ($saved) { break }
            if ($src -eq 'cdn') {
                if (-not $newRec -or -not ([string]$newRec.i)) { $lastMsg = '没有 imageId'; continue }
                # 2018 年的图只有 1920x1080，2019 起才有 UHD；按画质档位决定试哪些
                $sufsC = @()
                if ($MaxRes -eq 'uhd') { $sufsC = @('_UHD', '_1920x1080') }
                elseif ($MaxRes -eq 'fhd') { $sufsC = @('_1920x1080') }
                foreach ($suf in $sufsC) {
                    $u = "https://www.bing.com/th?id=$($newRec.i)$suf.jpg"
                    try {
                        [void](Save-BingImage -Url $u -Dest $destFile -MinKB 60 -Retry 1)
                        $saved = $true
                        $resLog[$cur] = ('cdn' + $suf)
                        break
                    } catch { $lastMsg = ($_.Exception.Message) -replace '"', "'" }
                }
            } elseif ($src -eq 'bimg') {
                if (-not $newRec -or -not ([string]$newRec.i)) { $lastMsg = '没有 imageId'; continue }
                $yb = $cur.Substring(0, 4)
                $u = "https://cdn.bimg.cc/bing/$yb/$($newRec.i)_1920x1080.jpg"
                try {
                    [void](Save-BingImage -Url $u -Dest $destFile -MinKB 60 -Retry 1)
                    $saved = $true
                    $resLog[$cur] = 'bimg_1080p'
                } catch { $lastMsg = ($_.Exception.Message) -replace '"', "'" }
            } else {
                $base = $cur.Substring(0, 6)
                $cands = @()
                if ($oldRec -and ([string]$oldRec.a)) { $cands += [string]$oldRec.a }
                if ($oldRec -and ([string]$oldRec.n)) { $cands += (([string]$oldRec.n) -replace '_1920x1080\.jpg$', '') }
                $cands += ($short + '_1366x768')
                $cands += ($short + '_1920x1080')
                $cands += $short
                $cands = @($cands | Where-Object { $_ } | Select-Object -Unique)
                foreach ($nm in $cands) {
                    $u = "https://img.nanxiongnandi.com/$base/$nm.jpg"
                    try {
                        [void](Save-BingImage -Url $u -Dest $destFile -MinKB 60 -Retry 1)
                        $saved = $true
                        $resLog[$cur] = ('anerg:' + $nm)
                        break
                    } catch { $lastMsg = ($_.Exception.Message) -replace '"', "'" }
                }
            }
        }

        if ($saved) {
            $okN++
            # 画质校验：_UHD 地址有可能返回 1080p 内容（实测 2023-02-04 就是），
            # 所以拿到文件之后必须量一下，不能拿「地址叫 UHD」当证据。
            if ($MaxRes -eq 'uhd' -and $cur -ge '20190101') {
                $wReal = Get-ImagePixelWidth -Path $destFile
                if ($wReal -gt 0 -and $wReal -lt 3000) {
                    $lowN++
                    Note-NoUhd -Date8 $cur
                    [void]$lowList.Add($cur)
                } else { $uhdN++ }
            }
        }
        else {
            $failN++
            [void]$failList.Add("$cur`t$lastMsg")
            if (-not $firstErr) { $firstErr = "$cur $lastMsg" }
            if (Test-Path $destFile) { try { Remove-Item -LiteralPath $destFile -Force -ErrorAction SilentlyContinue } catch { } }
        }
        Start-Sleep -Milliseconds 250
    }

    # 每天用了哪个源、哪个档位、实际拿到多大 —— 落盘，事后能核对
    try {
        $resLines = @()
        foreach ($kR in ($resLog.Keys | Sort-Object)) {
            $fR = @(Get-ChildItem $DestDir -Filter "${kR}_*.jpg" -File -ErrorAction SilentlyContinue) | Select-Object -First 1
            $wR = 0
            $mR = 0
            if ($fR) {
                $wR = Get-ImagePixelWidth -Path $fR.FullName
                $mR = [math]::Round($fR.Length / 1MB, 2)
            }
            $resLines += ("{0}`t{1}`t{2}px`t{3}MB" -f $kR, $resLog[$kR], $wR, $mR)
        }
        $resPath = Join-Path $DestDir '_export-res.txt'
        if ($resLines.Count -gt 0) { $resLines | Set-Content $resPath -Encoding UTF8 }
        elseif (Test-Path $resPath) { Remove-Item -LiteralPath $resPath -Force -ErrorAction SilentlyContinue }
    } catch { }

    try {
        if ($lowList.Count -gt 0) {
            $lowList | Sort-Object | Set-Content (Join-Path $DestDir '_export-lowres.txt') -Encoding UTF8
        } else {
            $pLow = Join-Path $DestDir '_export-lowres.txt'
            if (Test-Path $pLow) { Remove-Item -LiteralPath $pLow -Force -ErrorAction SilentlyContinue }
        }
    } catch { Write-Log ('低画质清单写入失败：' + $_.Exception.Message) 'WARN' }

    $failPath = Join-Path $DestDir '_export-fail.txt'
    if ($failList.Count -gt 0) {
        try { $failList | Set-Content $failPath -Encoding UTF8 } catch { }
    } elseif (Test-Path $failPath) {
        try { Remove-Item -LiteralPath $failPath -Force -ErrorAction SilentlyContinue } catch { }
    }

    Write-Log ("批量下载完成：成功 $okN（真 UHD $uhdN / 无 UHD $lowN / 升级 $upN），跳过 $skipN，失败 $failN（源：$Source）")
    Write-Output ('{"ok":' + $okN + ',"skip":' + $skipN + ',"fail":' + $failN + ',"lowres":' + $lowN + ',"error":"' + $firstErr + '"}')
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
        $allN = [int](Clear-AllCache -Mode $modeTemp)
        Write-Output ('{"removed":' + $allN + ',"protected":' + $kept + '}')
        Exit-Run 0
    }

    # ---- 收藏 / 取消收藏
    if ($Favorite -or $Unfavorite) {
        # 收藏会把文件移走，这里必须拿到最新 state 才能把路径改到新位置。
        # 全局的 $state 在读在 4000 行之后，这里拿不到 —— 原来那个判断恒为假，
        # 于是 state.file 一直指向被移走的旧路径，窗口预览就空白了。
        $stateCur = Read-State
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
        # 收藏是「换个地方存」，不是「再存一份」：非接管模式一律移动，避免同一张图
        # 在归档和收藏各占 3.5 MB。归档本来也不是完整档案（500 MB 上限会滚动删除），
        # 少这一张不影响它的定位。只有接管模式必须复制 —— 那是用户的图库，只读。
        $copyOnly = $script:Takeover
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
            # 缓存来源移动（一份文件，清理碰不到收藏）；
            # 归档 / 老存档 / 接管图库来源只复制（那是历史库，不能因为收藏就少一张）
            if ($copyOnly) {
                Copy-Item -LiteralPath $cur -Destination $dest -Force
                Write-Log ("收藏：接管模式，从 " + $srcDir + " 复制一份，图库原件保留")
            } else {
                Move-Item -LiteralPath $cur -Destination $dest -Force
            }
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
                        # Get-ZhTitleMap 的值是 @{title;copyright}，取 .title 而不是整个哈希表
                        # —— 直接赋哈希表会在 [string] 转换时变成 "System.Collections.Hashtable"
                        $zt = [string]$hit8.title
                        $zc = [string]$hit8.copyright
                        $en8 = ''
                        if ($zhMap.ContainsKey($bid)) {
                            $ze = $zhMap[$bid]
                            $zt = [string]$ze.title
                            if (-not $zc) { $zc = [string]$ze.copyright }
                        }
                        $meta = [pscustomobject]@{
                            title     = $zt
                            titleEn   = $en8
                            copyright = $zc
                            imageId   = $bid
                        }
                    }
                } catch { }
            }
            # 还查不到就用 state 里的记录兜底
            if ((-not $meta -or [string]$meta.title -eq '') -and $stateCur -and [string]$stateCur.date -eq $dateKey) {
                $meta = [pscustomobject]@{
                    title     = [string]$stateCur.title
                    titleEn   = ''
                    copyright = ''
                    imageId   = [string]$stateCur.imageId
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
            if ($stateCur -and [string]$stateCur.date -eq $dateKey) {
                Write-State -Date $dateKey -File $dest -Title ([string]$favIdx[$dateKey].title) -ImageId ([string]$favIdx[$dateKey].imageId) -AppliedDate $dateKey
            }
            if ($copyOnly) { Write-Log "已收藏（复制进收藏夹）：$name" }
            else { Write-Log "已收藏（移入收藏夹）：$name" }
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
            if (-not $keptTitle -and $stateCur -and [string]$stateCur.date -eq $dateKey) {
                $keptTitle = [string]$stateCur.title
                if (-not $keptId) { $keptId = [string]$stateCur.imageId }
                if (-not $keptCpr) { $keptCpr = [string]$stateCur.copyright }
            }
            if (Test-Path $dest) { Move-Item -LiteralPath $dest -Destination $back -Force }
            if ($favIdx.ContainsKey($dateKey)) { $favIdx.Remove($dateKey) }
            [System.IO.File]::WriteAllText($FavIndex, ($favIdx | ConvertTo-Json -Depth 6), (New-Object System.Text.UTF8Encoding($false)))
            if (Test-Path $back) {
                Set-WallpaperStyle -Name $styleName
                [void](Set-DesktopWallpaper -Path $back)
                if ($stateCur -and [string]$stateCur.date -eq $dateKey) {
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
                elseif (Remove-OneFile -Path $_.FullName -Mode $modeTemp) { $nG++ }
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
                elseif (Remove-OneFile -Path $_.FullName -Mode $modeTemp) { $nP++ }
            }
        }
        Write-Log ("清理预取缓存：" + $nP + " 张")
        Write-Output ('{"removed":' + $nP + '}')
        Exit-Run 0
    }

# 补齐图库：把范围内索引里有、图库里没有的日期一张张补进来。只增，不覆盖。
# 补齐故事映射表：story-map.json 是静态打包的「日期 → todaybing slug」，
# 不更新的话新日子即使源站有故事也取不到（没有 slug 就直接返回空）。
# 做法：官方 API 给图名（OHR.FibonacciFern），todaybing 的 slug 也含图名
# （fibonacci-fern-zh-233497），两边按图名对齐，不猜顺序。
if ($UpdateStoryMap) {
    $addedS = 0
    $fixedS = 0
    $matchedS = 0
    try {
        $pS = Invoke-Web @{ Uri = 'https://www.bing.com/HPImageArchive.aspx?format=js&idx=0&n=8&mkt=zh-CN'; TimeoutSec = 30; UseBasicParsing = $true }
        $rS = Invoke-WebRequest @pS
        $objS = $rS.Content | ConvertFrom-Json
        $pS2 = Invoke-Web @{ Uri = 'https://www.todaybing.com/'; TimeoutSec = 30; UseBasicParsing = $true }
        $rS2 = Invoke-WebRequest @pS2
        $slugs = @([regex]::Matches([string]$rS2.Content, '/bing/([a-z0-9-]+-zh-\d+)') |
                   ForEach-Object { $_.Groups[1].Value } | Select-Object -Unique)

        Write-Log ('故事映射：官方 ' + @($objS.images).Count + ' 天，站上 slug ' + $slugs.Count + ' 个')

        $mapS = Get-StoryMap
        foreach ($img in @($objS.images)) {
            $ub = [string]$img.urlbase
            $mS = [regex]::Match($ub, 'OHR\.([A-Za-z0-9]+)')
            if (-not $mS.Success) { continue }
            $nameS = $mS.Groups[1].Value
            # 驼峰转中划线小写：FibonacciFern -> fibonacci-fern
            $kebab = ($nameS -creplace '([a-z0-9])([A-Z])', '$1-$2').ToLowerInvariant()
            $hitS = $slugs | Where-Object { $_ -like ($kebab + '-zh-*') } | Select-Object -First 1
            if (-not $hitS) { continue }
            $matchedS++
            # Bing 的 startdate 是 UTC；国内这张图在次日 00:00 上线
            $dS = ''
            try { $dS = [datetime]::ParseExact([string]$img.startdate, 'yyyyMMdd', $null).AddDays(1).ToString('yyyyMMdd') } catch { continue }
            if ($mapS.ContainsKey($dS)) {
                # 已存在也要核对：map 里可能预填了未来日期，官方匹配结果才是准的
                if ([string]$mapS[$dS] -ne $hitS) {
                    Write-Log ('  校正 ' + $dS + '：' + $mapS[$dS] + ' → ' + $hitS)
                    $mapS[$dS] = $hitS
                    $fixedS++
                }
                continue
            }
            $mapS[$dS] = $hitS
            $addedS++
            Write-Log ('  补进 ' + $dS + ' → ' + $hitS)
        }

        if (($addedS + $fixedS) -gt 0) {
            [System.IO.File]::WriteAllText($StoryMapPath, ($mapS | ConvertTo-Json -Depth 3), (New-Object System.Text.UTF8Encoding($false)))
            Write-Log ('故事映射已更新：新增 ' + $addedS + ' 条，校正 ' + $fixedS + ' 条（匹配 ' + $matchedS + ' 天）')
        } else {
            Write-Log ('故事映射无需更新（匹配 ' + $matchedS + ' 天，全部一致）')
        }
        Write-Output ('{"added":' + $addedS + ',"fixed":' + $fixedS + ',"matched":' + $matchedS + '}')
        Exit-Run 0
    } catch {
        Write-Log ('故事映射更新失败：' + $_.Exception.Message) 'ERROR'
        Exit-Run 1
    }
}

if ($FillLibrary) {
    # 正常模式的归档受 archiveMaxMB 上限自动清理，补进去的图很快会被删掉，补了白补还耗流量。
    if (-not $script:Takeover) {
        Write-Log '补齐缺失日期只在接管模式下可用：普通归档受容量上限自动清理，补进去也会被删' 'ERROR'
        Exit-Run 1
    }
    $tgtDir = $ArchiveImgDir
    if (-not $tgtDir -or -not (Test-Path $tgtDir)) { Write-Log '补齐目标目录不存在' 'ERROR'; Exit-Run 5 }

    $d1F = ([string]$RangeFrom) -replace '[^0-9]', ''
    $d2F = ([string]$RangeTo) -replace '[^0-9]', ''
    $todayF = (Get-Date).ToString('yyyyMMdd')
    if ($d2F.Length -ne 8) { $d2F = $todayF }
    if ($d1F.Length -ne 8) { $d1F = (Get-Date).AddYears(-3).ToString('yyyyMMdd') }
    if ($d1F -lt $ArchiveMinDate) { $d1F = $ArchiveMinDate }
    if ($d2F -gt $todayF) { $d2F = $todayF }
    if ($d1F -gt $d2F) { Write-Log ('日期范围反了：' + $d1F + ' > ' + $d2F) 'ERROR'; Exit-Run 1 }

    $haveF = Get-LibraryDates -Dir $tgtDir
    $idxF = @()
    try { $idxF = @(Get-FullIndex) } catch { }
    if ($idxF.Count -eq 0) { Write-Log '索引为空，无法确定要补哪些日期' 'ERROR'; Exit-Run 5 }

    $todoF = @()
    foreach ($it in $idxF) {
        $dd = ([string]$it.d) -replace '[^0-9]', ''
        if ($dd.Length -ne 8) { continue }
        if ($dd -lt $d1F -or $dd -gt $d2F) { continue }
        if ($haveF.ContainsKey($dd)) { continue }
        $todoF += $it
    }

    Write-Log ("补齐图库：范围 $d1F ~ $d2F，缺 " + $todoF.Count + " 天")

    $deadF = Read-DeadDates
    $libIdxF = Read-LibraryIndex -Dir $tgtDir
    $okF = 0; $skipF = 0; $failF = 0
    foreach ($it in $todoF) {
        $dd = ([string]$it.d) -replace '[^0-9]', ''
        if ($deadF.ContainsKey($dd)) { $skipF++; continue }
        $imgId = [string]$it.i
        if (-not $imgId) { $skipF++; continue }
        $shortIdF = $imgId -replace '_[A-Za-z]{2}-[A-Za-z]{2}\d+$', ''
        $nameF = "${dd}_$shortIdF.jpg"
        $destF = Join-Path $tgtDir $nameF
        if (Test-Path -LiteralPath $destF) { $skipF++; continue }
        $szF = 0
        $doneF = $false
        foreach ($sfx in @(($imgId + '_UHD.jpg'), ($imgId + '_1920x1080.jpg'))) {
            try {
                $szF = Save-BingImage -Url ('https://www.bing.com/th?id=' + $sfx) -Dest $destF -MinKB 60 -Retry 1
                $doneF = $true
                break
            } catch { }
        }
        if ($doneF) {
            $okF++
            $libIdxF[$dd] = @{ file = $nameF; b = $szF; at = (Get-Date).ToString('s') }
            Write-LibraryIndex -Dir $tgtDir -Days $libIdxF
            Write-Log ("  补进 $dd（" + [math]::Round($szF / 1KB, 0) + " KB）")
        } else {
            # 失败要分清「这天确实没有」和「网络一时不通」：
            # 后者记账会把好日期永久拉黑，断网时补一次就毁一片
            $rvF = Test-ArchiveReachable -Date8 $dd -ImageId $imgId
            $failF++
            if ($rvF -eq 'missing') {
                Note-DeadDate -Date8 $dd -Reason 'fill-missing'
                Write-Log ("  取不到 $dd，已记入失效名单") 'WARN'
            } else {
                Write-Log ("  取不到 $dd（网络问题，未记入失效名单）") 'WARN'
            }
        }
        Start-Sleep -Milliseconds 400
    }

    Write-Log ("补齐完成：新增 $okF 天，跳过 $skipF 天，失败 $failF 天")
    Write-Output ('{"added":' + $okF + ',"skipped":' + $skipF + ',"failed":' + $failF + '}')
    Exit-Run 0
}

    if ($StoryFor) {
        $sv = Get-Story -Date $StoryFor
        $objS = [ordered]@{ date = ([string]$StoryFor) -replace '[^0-9]', ''; story = $sv }
        Write-Output ($objS | ConvertTo-Json -Compress)
        Exit-Run 0
    }

    # 单独清故事（没有同时清归档时才走这里，直接结束）。
    # 带 -CleanArchive 时由归档分支顺带处理，否则这里会先删完故事就退出，归档一张都清不掉。
    if ($CleanStories -and -not $CleanArchive) {
        $nS = [int](Clear-StoryCache -Mode $modeTemp)
        Write-Log ("清理每日故事：" + $nS + " 条")
        Write-Output ('{"removed":' + $nS + '}')
        Exit-Run 0
    }

    if ($CleanArchiveIndex) {
        $nIdx = 0
        Get-ChildItem $ArchiveDir -Filter '*.json' -File -ErrorAction SilentlyContinue | ForEach-Object {
            if ($_.Name -eq 'all-index.json' -or $_.Name -match '^\d{4}-\d{2}\.json$') {
                if ($DryRun) { Write-Log "[DryRun] 将删除索引 $($_.Name)"; $nIdx++ }
                elseif (Remove-OneFile -Path $_.FullName -Mode $modeTemp) { $nIdx++ }
            }
        }
        Write-Log ("清理归档索引：删除 " + $nIdx + " 个文件")
        Write-Output ('{"removed":' + $nIdx + ',"protected":0}')
        Exit-Run 0
    }

    if ($CleanArchive) {
        if (-not (Test-Path $ArchiveImgDir)) {
            Write-Log '历史归档目录不存在，无需清理'
            # 没有图可清时，故事缓存仍然按勾选处理 —— 两个目录互不依赖
            $nSX = 0
            if ($CleanStories) {
                $nSX = [int](Clear-StoryCache -Mode $modeTemp)
                Write-Log ("同时清理每日故事缓存：" + $nSX + " 条")
            }
            Write-Output ('{"removed":0,"keptFav":0,"keptCur":0,"idxRemoved":0,"oldRemoved":0,"storiesRemoved":' + $nSX + '}')
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
                        elseif (Remove-OneFile -Path $_.FullName -Mode $modeTemp) { $nIdxA++ }
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
        # 故事缓存：可选步骤，默认不动。故事删了可再生，图片删了不可再生，
        # 两者代价不同，所以由勾选框分开决定，不绑在归档按钮本体上
        $removedS = 0
        if ($CleanStories) {
            $removedS = [int](Clear-StoryCache -Mode $modeTemp)
            Write-Log ("同时清理每日故事缓存：" + $removedS + " 条")
        }
        Write-Output ('{"removed":' + $removedA + ',"keptFav":' + $keptFavA + ',"keptCur":' + $keptCurA + ',"idxRemoved":' + $nIdxA + ',"oldRemoved":' + $removedO + ',"storiesRemoved":' + $removedS + '}')
        Exit-Run 0
    }

    # ---- 随机一张：按随机范围分三档（fav 收藏 / local 本地全部 / any 任意一天）
    if ($Random) {
        $scope = ([string]$RandomScope).ToLowerInvariant()
        if ($scope -ne 'fav' -and $scope -ne 'any') { $scope = 'local' }

        $curWp = (Get-ItemProperty 'HKCU:\Control Panel\Desktop' -Name Wallpaper -ErrorAction SilentlyContinue).Wallpaper

        # 本地池：四个目录按日期去重，收藏优先登记（同一天既有收藏又有归档时用收藏那份）
        $byDate = @{}
        foreach ($src in @(
            @{ dir = $FavDir;        from = '收藏';   recurse = $false },
            @{ dir = $CacheDir;      from = '缓存';   recurse = $false },
            @{ dir = $ArchiveImgDir; from = '归档';   recurse = $true  },
            @{ dir = $OldDir;        from = '老存档'; recurse = $true  }
        )) {
            $d0 = $src.dir
            if (-not $d0 -or -not (Test-Path $d0)) { continue }
            $fs0 = @(Get-ChildItem $d0 -Filter '*.jpg' -File -Recurse:$src.recurse -ErrorAction SilentlyContinue)
            foreach ($f0 in $fs0) {
                if ($f0.Name -match '^(\d{8})') {
                    $k0 = $matches[1]
                    if (-not $byDate.ContainsKey($k0)) { $byDate[$k0] = @{ file = $f0; from = $src.from } }
                }
            }
        }

        # any：先从索引里随机取一天。本地没有就先做可达性预检 ——
        # 直接扔给「指定日期」流程的话，一条死链要拉索引、试三个源、重试两轮才知道没用。
        if ($scope -eq 'any') {
            $idxAny = @()
            try { $idxAny = @(Get-FullIndex) } catch { }
            if ($idxAny.Count -eq 0) { Write-Log '索引为空，取不到可随机的日期' 'ERROR'; Exit-Run 5 }

            $dead = Read-DeadDates
            $poolAny = @($idxAny | Where-Object { -not $dead.ContainsKey([string]$_.d) })
            if ($poolAny.Count -eq 0) { $poolAny = $idxAny }

            $tried = @()
            $netFail = 0
            $picked = ''
            for ($k = 0; $k -lt 3; $k++) {
                $cand = $poolAny | Get-Random
                if (-not $cand) { break }
                $dc = ([string]$cand.d) -replace '[^0-9]', ''
                if ($dc.Length -ne 8) { continue }
                if ($tried -contains $dc) { continue }
                $tried += $dc
                if ($byDate.ContainsKey($dc)) { $picked = $dc; break }
                $rv = Test-ArchiveReachable -Date8 $dc -ImageId ([string]$cand.i)
                if ($rv -eq 'ok' -or $rv -eq 'unknown') { $picked = $dc; break }
                if ($rv -eq 'missing') {
                    Note-DeadDate -Date8 $dc -Reason 'missing'
                    Write-Log ('随机到 ' + $dc + '，原图已不存在，换一天再抽') 'WARN'
                } else {
                    $netFail++
                    Write-Log ('随机到 ' + $dc + '，预检时网络不通，换一天再抽') 'WARN'
                }
            }

            if (-not $picked) {
                if ($netFail -ge $tried.Count -and $tried.Count -gt 0) {
                    Write-Log ('随机试了 ' + $tried.Count + ' 天，预检都没连上（网络问题）：' + ($tried -join ' / ')) 'ERROR'
                    Exit-Run 4
                }
                Write-Log ('随机试了 ' + $tried.Count + ' 天都取不到原图：' + ($tried -join ' / ')) 'ERROR'
                Exit-Run 5
            }

            if ($byDate.ContainsKey($picked)) {
                Write-Log ('随机到 ' + $picked + '，本地已有这张')
            } else {
                Write-Log ('随机到 ' + $picked + '，本地没有这张，改为下载')
                $Date = $picked
            }
        }

        if (-not $Date) {
        $cands = @($byDate.Values | Where-Object { $scope -ne 'fav' -or $_.from -eq '收藏' })
        if ($cands.Count -eq 0) {
            if ($scope -eq 'fav') { Write-Log '收藏是空的（随机范围：收藏）' 'ERROR'; Exit-Run 3 }
            Write-Log '没有可随机的图（收藏、缓存、归档、老存档都是空的）' 'ERROR'; Exit-Run 5
        }
        $filtered = @($cands | Where-Object { $_.file.FullName -ne $curWp })
        if ($filtered.Count -gt 0) { $cands = $filtered }

        $pick = $cands | Get-Random
        $pickDate = ($pick.file.BaseName -split '_')[0]

        $locked = Enter-RunLock -WaitSec $script:LockWaitSec
        if (-not $locked) { exit 2 }

        Set-WallpaperStyle -Name $styleName
        [void](Set-DesktopWallpaper -Path $pick.file.FullName)
        Write-Log ("随机切换（" + $pick.from + "，共 " + $cands.Count + " 张候选）：" + $pick.file.Name)

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
        Write-State -Date $pickDate -File $pick.file.FullName -Title $ttl -ImageId $iid -AppliedDate $pickDate
        Write-Log '--- 完成 ---'
        if ([bool]$config['autoClean']) {
            $capMB2 = 0
            try { $capMB2 = [int]$config['archiveMaxMB'] } catch { }
            if ($capMB2 -gt 0) { [void](Clear-ArchiveBySize -MaxMB $capMB2) }
        }
        Exit-Run 0
        }
    }

    # ---- 指定日期
    if ($Date) {
        # 归一化：允许 2016-03-05 / 2016/03/05 等写法，非 8 位数字一律视为格式错误
        $Date = ([string]$Date) -replace '[^0-9]', ''
        if ($Date.Length -ne 8) { Write-Log "日期格式错误（要 YYYYMMDD）：$Date" 'ERROR'; exit 1 }
        $dt = $null
        try { $dt = [datetime]::ParseExact($Date, 'yyyyMMdd', $null) } catch { }
        if (-not $dt) { Write-Log "日期格式错误（要 YYYYMMDD）：$Date" 'ERROR'; exit 1 }
        if ($dt.Date -gt (Get-Date).Date) { Write-Log '不能选择未来日期（要 YYYYMMDD 且不晚于今天）' 'ERROR'; exit 12 }

        # 官方索引里没有这一天，就不要去试网络了 —— 实测试一轮要 20 秒才失败。
        # 只在索引覆盖得到的时间段里判定；索引还没更新到今天时一律走原流程。
        try {
            $pubSet = @{}
            $idxMin = ''
            $idxMax = ''
            foreach ($it in @(Get-FullIndex)) {
                $kd = [string]$it.d
                if ($kd.Length -ne 8) { continue }
                $pubSet[$kd] = $true
                if (-not $idxMin -or $kd -lt $idxMin) { $idxMin = $kd }
                if (-not $idxMax -or $kd -gt $idxMax) { $idxMax = $kd }
            }
            foreach ($ok3 in @((Get-OldIndexMap).Keys)) {
                $kd = [string]$ok3
                if ($kd.Length -ne 8) { continue }
                $pubSet[$kd] = $true
                if (-not $idxMin -or $kd -lt $idxMin) { $idxMin = $kd }
                if (-not $idxMax -or $kd -gt $idxMax) { $idxMax = $kd }
            }
            if ($idxMin -and $idxMax -and $Date -ge $idxMin -and $Date -le $idxMax -and -not $pubSet.ContainsKey($Date)) {
                $prevD = ''
                $nextD = ''
                foreach ($kd in @($pubSet.Keys)) {
                    if ($kd -lt $Date -and ($prevD -eq '' -or $kd -gt $prevD)) { $prevD = $kd }
                    if ($kd -gt $Date -and ($nextD -eq '' -or $kd -lt $nextD)) { $nextD = $kd }
                }
                $hintD = @(@($prevD, $nextD) | Where-Object { $_ })
                Write-Log ("Bing 官方未发布该日壁纸：" + $Date + "（最近可用：" + ($hintD -join ' / ') + "）") 'ERROR'
                if ($DryRun) { Write-Log '[DryRun] 索引里没有这一天（Bing 官方未发布），实际会失败' }
                Exit-Run 5
            }
        } catch { }

        Write-Log "--- 指定日期：$Date ---\"

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
        # -WallFull 是「我要原图」：墙里已有的预览档不算命中，重新下一次覆盖它
        if (-not $hit -and $script:WallMode -and -not $WallFull -and (Test-Path $WallDir)) {
            $hit = Get-ChildItem $WallDir -Filter "${Date}_*.jpg" -File -ErrorAction SilentlyContinue | Select-Object -First 1
        }
        # 命中本地不等于命中最高画质。2019 年起 Bing 才提供 UHD，本地这份如果明显偏小，
        # 说明是当年只拿到 1080p 的预览档或旧镜像。现在能连上就重取一次，原地覆盖：
        # 文件名不变，收藏、索引、state 都不用动。收藏里的也照样升 —— 收藏存的是「这一天」，
        # 不是「当时那个画质」。
        if ($hit -and $Date -ge '20190101' -and -not (Test-NoUhd -Date8 $Date)) {
            $wUp = Get-ImagePixelWidth -Path $hit.FullName
            if ($wUp -gt 0 -and $wUp -lt 3000) {
                Write-Log ("命中本地但只有 " + $wUp + " 像素宽（不是 UHD），重取原图并覆盖")
                $script:UpgradeDest = $hit.FullName
                $hit = $null
            }
        }
        if ($hit) {
            if ($DryRun) {
                Write-Log ("[DryRun] 命中本地，将设为壁纸：" + $hit.FullName)
                Exit-Run 0
            }
            $locked = Enter-RunLock -WaitSec $script:LockWaitSec
            if (-not $locked) { exit 2 }
            Set-WallpaperStyle -Name $styleName
            if (-not (Set-DesktopWallpaper -Path $hit.FullName)) { Write-Log '壁纸设置后注册表未更新' 'WARN'; Exit-Run 11 }
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
        $list8obj = try { Get-WallpaperListJson -Count 8 } catch { Write-Log ("接口不可用，按归档索引继续：" + $_.Exception.Message) 'WARN'; '[]' }
        $list8 = @($list8obj | ConvertFrom-Json | Where-Object { $_ })
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
                    # 老存档按月单独放：不能用 Get-DownloadDir（会落到缓存根，30 天后被清掉）
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
                    # wilii 在前（2014 前不带后缀、2016 起带后缀，$wList 两种都给），镜像兜底
                    foreach ($w in $wList) { $candO += $w }
                    $aBase = $Date.Substring(0, 6)
                    foreach ($nm in @($rec.anergNames)) {
                        $candO += @{ url = ('https://img.nanxiongnandi.com/' + $aBase + '/' + $nm + '.jpg'); tag = '老存档·镜像' }
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
                        if (Set-DesktopWallpaper -Path $destO) { Write-Log "壁纸已设置: $destO" } else { Write-Log '壁纸设置后注册表未更新' 'WARN'; Exit-Run 11 }
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
                    # 本地那份是低画质的话，直接覆盖它，避免同一日期存两份
                    if ($script:UpgradeDest) { $dest = $script:UpgradeDest }
                    # 候选顺序：照片墙默认先拿 1080p —— 预览只有 320 宽，没必要为它下 5 MB 原图；
                    # 其余场景（常规换图 / 预取 / 照片墙暂存）仍然 UHD 优先。
                    $cand = @()
                    if ($script:WallMode -and -not $WallFull) {
                        $cand += @{ url = "https://www.bing.com/th?id=${archId}_1920x1080.jpg"; tag = '照片墙预览 1080p'; minKb = 60 }
                        $cand += @{ url = "https://www.bing.com/th?id=${archId}_UHD.jpg"; tag = '归档原图 UHD'; minKb = 300 }
                    } else {
                        $cand += @{ url = "https://www.bing.com/th?id=${archId}_UHD.jpg"; tag = '归档原图 UHD'; minKb = 300 }
                        # 2016-2018 的图 Bing 只留了 1080p，UHD 会 404
                        $cand += @{ url = "https://www.bing.com/th?id=${archId}_1920x1080.jpg"; tag = '归档原图 1080p'; minKb = 60 }
                    }
                    # 2016-03 ~ 2019-12：Bing 的 th?id 两档实测全 404，cdn.bimg.cc 是唯一稳定来源。
                    # 这里不补的话，「按日期搜索」「往年的今天」「照片墙拉图」点这几年都会失败。
                    if ($archId) {
                        $yb2 = $Date.Substring(0, 4)
                        $cand += @{ url = "https://cdn.bimg.cc/bing/$yb2/${archId}_1920x1080.jpg"; tag = 'bimg 1080p'; minKb = 60 }
                    }
                    if ($rec.url) {
                        $u2 = [string]$rec.url
                        if ($u2.StartsWith('/')) { $u2 = 'https://www.bing.com' + $u2 }
                        $cand += @{ url = $u2; tag = '归档直链'; minKb = 60 }
                    }

                    if ($DryRun) {
                        Write-Log ("[DryRun] 将下载归档原图：" + $archId + " → " + $dest + "（不下载、不设壁纸）")
                        Write-Log ("[DryRun] 候选顺序：" + (($cand | ForEach-Object { $_.tag }) -join ' → '))
                        Exit-Run 0
                    }
                    $locked = Enter-RunLock -WaitSec $script:LockWaitSec
                    if (-not $locked) { exit 2 }

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
                        if (Set-DesktopWallpaper -Path $dest) { Write-Log "壁纸已设置: $dest" } else { Write-Log '壁纸设置后注册表未更新' 'WARN'; Exit-Run 11 }
                        if ($archTtl.Length -gt 60) { $archTtl = $archTtl.Substring(0, 60) }
                        Add-Pinned -Date $Date
                        Write-State -Date $Date -File $dest -Title $archTtl -Copyright $archCpr -SearchUrl ([string]$rec.searchUrl) -ImageId $archId -AppliedDate $Date
                        if ($script:UpgradeDest) {
                            $wNew = Get-ImagePixelWidth -Path $dest
                            if ($wNew -gt 0 -and $wNew -lt 3000) {
                                Note-NoUhd -Date8 $Date
                                Write-Log ("这天拿不到 UHD（下载后仍只有 " + $wNew + " 像素宽），记入名单，下次不再白试")
                            }
                        }
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
            if ($script:UpgradeDest) { $dest = $script:UpgradeDest }
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
            if (Set-DesktopWallpaper -Path $dest) { Write-Log "壁纸已设置: $dest" } else { Write-Log '壁纸设置后注册表未更新' 'WARN'; Exit-Run 11 }
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
            if ($script:UpgradeDest) {
                $wNew2 = Get-ImagePixelWidth -Path $dest
                if ($wNew2 -gt 0 -and $wNew2 -lt 3000) {
                    Note-NoUhd -Date8 $Date
                    Write-Log ("这天拿不到 UHD（下载后仍只有 " + $wNew2 + " 像素宽），记入名单，下次不再白试")
                }
            }
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
        else { Write-Log '壁纸设置后注册表未更新' 'WARN'; Exit-Run 11 }
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
                Invoke-NeighborFallback -From $curDate -Step $Step -Gallery:$Gallery -BudgetSec $BudgetSec
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
            # 越界了：交给 -Date 分支处理（会走归档下载）；死日期由 helper 自动跳过
            Invoke-NeighborFallback -From $curDate -Step $Step -Gallery:$Gallery -BudgetSec $BudgetSec
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
        else { Write-Log '壁纸设置后注册表未更新' 'WARN'; Exit-Run 11 }
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

