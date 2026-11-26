﻿﻿﻿﻿# 必应壁纸编年史 · 回归测试
# 用法：pwsh -File test.ps1
# 设计原则：幂等、不碰桌面壁纸、不依赖网络（用本地索引与缓存）

param(
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Continue'
$Root = Split-Path -Parent $PSCommandPath
$Script = Join-Path $Root 'bing-wallpaper.ps1'
$Pwsh = 'C:\Program Files\PowerShell\7\pwsh.exe'
if (-not (Test-Path $Pwsh)) { $Pwsh = 'powershell.exe' }

# 接管时“当前生效的缓存目录”不在安装目录：锁与照片墙暂存都落在 takeover\ 沙盒里。
# 测试必须跟着实际生效目录走，否则开着接管跑会假失败。
$TkActive = Test-Path (Join-Path $Root 'takeover\config.json')
$ActiveCache = $(if ($TkActive) { Join-Path $Root 'takeover\cache' } else { Join-Path $Root 'cache' })
$ActiveCacheIndex = $(if ($TkActive) { Join-Path $Root 'takeover\cache-index.json' } else { Join-Path $Root 'cache\index.json' })

$pass = 0
$fail = 0
$failed = @()

function Check {
    param([string]$Name, [scriptblock]$Test)
    try {
        $r = & $Test
        if ($r -eq $true -or $r -eq $null) {
            Write-Host ("  OK   " + $Name) -ForegroundColor Green
            $script:pass++
        } else {
            Write-Host ("  FAIL " + $Name + "  -> " + $r) -ForegroundColor Red
            $script:fail++
            $script:failed += $Name
        }
    } catch {
        Write-Host ("  FAIL " + $Name + "  -> 异常: " + $_.Exception.Message) -ForegroundColor Red
        $script:fail++
        $script:failed += $Name
    }
}

# 关键：参数必须用数组传，字符串会被当成单个参数
function Run-Script {
    param([string[]]$ArgList)
    if (-not $ArgList) { $ArgList = @() }
    $out = & $Pwsh -NoProfile -ExecutionPolicy Bypass -File $Script @ArgList 2>&1
    return @{
        Output = (($out | ForEach-Object { [string]$_ }) -join "`n")
        Code   = $LASTEXITCODE
    }
}

Write-Host ''
Write-Host '必应壁纸编年史 · 回归测试' -ForegroundColor Cyan
Write-Host ('  脚本: ' + $Script)
Write-Host ('  PS:   ' + $Pwsh)
Write-Host ''

# ---------------------------------------------------------------- 1. 语法与编译
Write-Host '[1] 语法与编译' -ForegroundColor Yellow

Check '脚本语法检查' {
    $err = $null
    [System.Management.Automation.Language.Parser]::ParseFile($Script, [ref]$null, [ref]$err) | Out-Null
    if ($err -and $err.Count -gt 0) { return ('语法错误 ' + $err.Count + ' 处') }
    return $true
}

if (-not $SkipBuild) {
    Check 'TrayProgram.cs 编译' {
        $log = & $Pwsh -NoProfile -Command "Set-Location '$Root'; & '.\build.ps1'" 2>&1
        $text = ($log | ForEach-Object { [string]$_ }) -join "`n"
        if ($LASTEXITCODE -ne 0 -or $text -match '编译失败') {
            return ('编译失败: ' + (($log | Select-Object -Last 3) -join ' '))
        }
        return $true
    }
}

# ---------------------------------------------------------------- 2. 状态查询
Write-Host ''
Write-Host '[2] 状态查询' -ForegroundColor Yellow

Check '-Status -Json 返回合法 JSON' {
    $r = Run-Script @('-Status', '-Json')
    if (-not $r.Output.Trim()) { return ('无输出，退出码 ' + $r.Code) }
    $json = $r.Output | ConvertFrom-Json -ErrorAction Stop
    foreach ($k in @('current', 'counts', 'config', 'index')) {
        if (-not $json.PSObject.Properties.Name.Contains($k)) { return ('缺 ' + $k + ' 键') }
    }
    return $true
}

Check '-Status 人读输出至少 3 行' {
    $r = Run-Script @('-Status')
    $lines = @($r.Output -split "`n" | Where-Object { $_.Trim() })
    if ($lines.Count -lt 3) { return ('只有 ' + $lines.Count + ' 行') }
    if ($r.Output -notmatch '当前') { return '缺「当前」' }
    if ($r.Output -notmatch '索引') { return '缺「索引」' }
    return $true
}

# ---------------------------------------------------------------- 3. 关键词搜索
Write-Host ''
Write-Host '[3] 关键词搜索' -ForegroundColor Yellow

Check '-Query 返回 JSON 数组' {
    $r = Run-Script @('-Query', '樱花')
    $s = $r.Output.Trim()
    if (-not $s.StartsWith('[')) { return ('输出不是数组: ' + $s.Substring(0, [Math]::Min(40, $s.Length))) }
    $arr = $s | ConvertFrom-Json -ErrorAction Stop
    if (@($arr).Count -eq 0) { return '结果为空' }
    if (-not $arr[0].date) { return '条目缺 date' }
    return $true
}

Check '-Query 无结果返回 [] 且退出码 5' {
    $r = Run-Script @('-Query', 'zzzz不存在zzzz')
    if ($r.Code -ne 5) { return ('退出码 ' + $r.Code + '，应为 5') }
    if ($r.Output.Trim() -ne '[]') { return ('输出: ' + $r.Output.Trim()) }
    return $true
}

Check '-Query 空关键词应报错（退出码 1，不换图）' {
    $before = (Get-Content (Join-Path $Root 'state.json') -Raw -Encoding UTF8 | ConvertFrom-Json).date
    $r = Run-Script @('-Query', '')
    $after = (Get-Content (Join-Path $Root 'state.json') -Raw -Encoding UTF8 | ConvertFrom-Json).date
    if ($r.Code -ne 1) { return ('退出码 ' + $r.Code + '，应为 1') }
    if ($before -ne $after) { return ('不该换图，state 从 ' + $before + ' 变成 ' + $after) }
    return $true
}

Check '-Query 命中项内容真的含关键词' {
    $r = Run-Script @('-Query', '长城')
    $s = $r.Output.Trim()
    if (-not $s.StartsWith('[')) { return ('输出不是数组') }
    $arr = @($s | ConvertFrom-Json -ErrorAction Stop)
    if ($arr.Count -lt 3) { return ('只返回 ' + $arr.Count + ' 条，至少应 3 条') }
    foreach ($it in $arr) {
        $txt = [string]$it.title + ' ' + [string]$it.copyright
        if ($txt.IndexOf('长城') -lt 0) { return ('条目不含关键词: ' + $it.date + ' ' + $it.title) }
    }
    return $true
}

Check '-Query 结果的日期全部合法' {
    $r = Run-Script @('-Query', '长城')
    $arr = @($r.Output.Trim() | ConvertFrom-Json -ErrorAction Stop)
    foreach ($it in $arr) {
        $d = [string]$it.date
        if ($d -notmatch '^\d{8}$') { return ('日期格式错误: [' + $d + ']') }
        if ($d -lt '20090713') { return ('日期早于下界: ' + $d) }
    }
    return $true
}

Check 'current.date 必须是单个 8 位日期' {
    $r = Run-Script @('-Status', '-Json')
    $j = $r.Output | ConvertFrom-Json
    $d = [string]$j.current.date
    if (-not $d) { return 'state 里没有 date' }
    if ($d -notmatch '^\d{8}$') { return ('date 异常（可能是数组被拼接）: [' + $d + ']') }
    return $true
}

Check 'current.file 指向的文件真实存在' {
    $r = Run-Script @('-Status', '-Json')
    $j = $r.Output | ConvertFrom-Json
    $f = [string]$j.current.file
    if (-not $f) { return 'state 里没有 file' }
    if (-not (Test-Path $f)) { return ('记录的文件不存在: ' + $f) }
    return $true
}

Check '-Status -Json 的 version 与 exe 一致' {
    $r = Run-Script @('-Status', '-Json')
    $j = $r.Output | ConvertFrom-Json
    $exe = Get-Item (Join-Path $Root 'BingWallpaperLite.exe') -ErrorAction SilentlyContinue
    if (-not $exe) { return 'exe 不存在，跳过' }
    $fv = $exe.VersionInfo.FileVersion -replace '\.0$', ''
    if ($j.version -ne $fv) { return ('脚本 ' + $j.version + ' vs exe ' + $fv) }
    return $true
}

Check '-KwFile 从文件读关键词' {
    $tmp = Join-Path $env:TEMP 'bwl-test-kw.txt'
    [System.IO.File]::WriteAllText($tmp, '樱花', (New-Object System.Text.UTF8Encoding($false)))
    try {
        $r = Run-Script @('-KwFile', $tmp)
        if (-not $r.Output.Trim().StartsWith('[')) { return '输出不是数组' }
        return $true
    } finally {
        Remove-Item $tmp -Force -ErrorAction SilentlyContinue
    }
}

# ---------------------------------------------------------------- 4. 换图与翻页
Write-Host ''
Write-Host '[4] 换图与翻页' -ForegroundColor Yellow

Check '-Date 有效日期（用归档里已有的那天，DryRun）' {
    # 必须带 -DryRun：不带的话这条测试会真的换掉桌面壁纸并改写 state
    $stFile = $(if ($TkActive) { Join-Path $Root 'takeover\state.json' } else { Join-Path $Root 'state.json' })
    if (-not (Test-Path $stFile)) { return '找不到 state.json' }
    $st = Get-Content $stFile -Raw -Encoding UTF8 | ConvertFrom-Json
    $r = Run-Script @('-Date', $st.date, '-DryRun')
    if ($r.Code -ne 0 -and $r.Code -ne 4) { return ('退出码 ' + $r.Code) }
    return $true
}

Check '-Date 无效日期退出码 5/7' {
    $r = Run-Script @('-Date', '20000101')
    if ($r.Code -ne 5 -and $r.Code -ne 7) { return ('退出码 ' + $r.Code + '，应为 5 或 7') }
    return $true
}

Check '-Date 下界前一天应被拒绝' {
    $stFileB = $(if ($TkActive) { Join-Path $Root 'takeover\state.json' } else { Join-Path $Root 'state.json' })
    $st = Get-Content $stFileB -Raw -Encoding UTF8 | ConvertFrom-Json
    $before = $st.date
    $r = Run-Script @('-Date', '20090712')
    $after = (Get-Content $stFileB -Raw -Encoding UTF8 | ConvertFrom-Json).date
    if ($r.Code -eq 0 -or $r.Code -eq 4) { return ('退出码 ' + $r.Code + '，越界不该成功') }
    if ($before -ne $after) { return ('state 从 ' + $before + ' 变成 ' + $after) }
    return $true
}

Check '-Status -Json 的 index.minDate 与代码下界一致' {
    $r = Run-Script @('-Status', '-Json')
    $j = $r.Output | ConvertFrom-Json
    if (-not $j.index) { return '缺 index 键' }
    if (-not $j.index.minDate) { return '缺 index.minDate 字段' }
    if ($j.index.minDate -notmatch '^[0-9]{8}$') { return ('minDate 不是 8 位数字: ' + $j.index.minDate) }
    if ($j.index.minDate -ne '20090713') { return ('minDate 为 ' + $j.index.minDate + '，与代码下界 20090713 不符') }
    return $true
}

Check '-Date 位数不足应被拒绝' {
    $stFileC = $(if ($TkActive) { Join-Path $Root 'takeover\state.json' } else { Join-Path $Root 'state.json' })
    $st = Get-Content $stFileC -Raw -Encoding UTF8 | ConvertFrom-Json
    $before = $st.date
    $r = Run-Script @('-Date', '2016030')
    $after = (Get-Content $stFileC -Raw -Encoding UTF8 | ConvertFrom-Json).date
    if ($r.Code -eq 0 -or $r.Code -eq 4) { return ('退出码 ' + $r.Code + '，位数不足不该成功') }
    if ($before -ne $after) { return ('state 从 ' + $before + ' 变成 ' + $after) }
    return $true
}
Check '-Step -1 执行成功（DryRun）' {
    # 加 -DryRun：翻页会真的换掉桌面壁纸
    $r = Run-Script @('-Step', '-1', '-DryRun')
    if ($r.Code -ne 0 -and $r.Code -ne 4) { return ('退出码 ' + $r.Code) }
    return $true
}

Check '-Step 1 执行成功（DryRun）' {
    $r = Run-Script @('-Step', '1', '-DryRun')
    if ($r.Code -ne 0 -and $r.Code -ne 4) { return ('退出码 ' + $r.Code) }
    return $true
}

# ---------------------------------------------------------------- 4.5 取图鲁棒性
Write-Host ''
Write-Host '[4.5] 取图鲁棒性' -ForegroundColor Yellow

Check '-ProbeLockWait 空闲时立刻拿到锁' {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $r = Run-Script @('-ProbeLockWait', '3')
    $sw.Stop()
    if ($r.Code -ne 0) { return ('退出码 ' + $r.Code) }
    if ($r.Output -notmatch 'probeLock=True') { return ('输出没有拿到锁: ' + $r.Output) }
    if ($sw.ElapsedMilliseconds -gt 5000) { return ('耗时 ' + $sw.ElapsedMilliseconds + ' ms，不该等这么久') }
    return $true
}

Check '锁被占时 -ProbeLockWait 会等到超时才放弃' {
    $lock = Join-Path $ActiveCache '.running'
    $holder = Get-Process -Id $PID -ErrorAction SilentlyContinue
    if (-not $holder) { return '拿不到自身进程，无法造锁' }
    $stamp = (Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
    [System.IO.File]::WriteAllText($lock, ("$PID|" + $stamp), (New-Object System.Text.UTF8Encoding($false)))
    try {
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        $r = Run-Script @('-ProbeLockWait', '3')
        $sw.Stop()
        if ($r.Code -ne 0) { return ('退出码 ' + $r.Code) }
        if ($r.Output -notmatch 'probeLock=False') { return ('锁被占却报成功: ' + $r.Output) }
        if ($sw.ElapsedMilliseconds -lt 2800) { return ('只等了 ' + $sw.ElapsedMilliseconds + ' ms，没有真正等待') }
        return $true
    } finally {
        Remove-Item -LiteralPath $lock -Force -ErrorAction SilentlyContinue
    }
}

Check '下载总预算参数与落点函数就位' {
    $src = Get-Content $Script -Raw -Encoding UTF8
    foreach ($need in @('function Get-DownloadDir', '$DownDir = Get-DownloadDir', '$BudgetSec',
                        '-not $Gallery -and -not $script:NoApply')) {
        if ($src.IndexOf($need) -lt 0) { return ('脚本里缺少: ' + $need) }
    }
    return $true
}

Check '-RebuildCacheIndex -DryRun 不改索引文件' {
    $idx = Join-Path $Root 'cache\index.json'
    $beforeTicks = (Get-Item $idx).LastWriteTimeUtc.Ticks
    $beforeLen = (Get-Item $idx).Length
    $r = Run-Script @('-RebuildCacheIndex', '-DryRun')
    if ($r.Code -ne 0) { return ('退出码 ' + $r.Code) }
    if ($r.Output -notmatch 'DryRun') { return '输出里没有 DryRun 标记' }
    if ((Get-Item $idx).LastWriteTimeUtc.Ticks -ne $beforeTicks) { return '索引文件被改动了' }
    if ((Get-Item $idx).Length -ne $beforeLen) { return '索引文件长度变了' }
    return $true
}

Check '-AuditDates 输出三方索引统计' {
    $r = Run-Script @('-AuditDates')
    if ($r.Code -ne 0) { return ('退出码 ' + $r.Code) }
    foreach ($need in @('all-index', 'old-index', 'wilii-index', '标题冲突', '自查完成')) {
        if ($r.Output.IndexOf($need) -lt 0) { return ('输出缺少: ' + $need) }
    }
    if ($r.Output -match 'all-index 0 条') { return '三方索引都解析成 0 条' }
    return $true
}

Check '往年的今天不再拼后缀字，且用（无标题）兜底' {
    $str = Get-Content (Join-Path $Root 'src\Strings.cs') -Raw -Encoding UTF8
    foreach ($bad in @('ClickFetchSfx', 'NeedArchiveSfx')) {
        if ($str.IndexOf($bad) -ge 0) { return ('Strings.cs 里还有 ' + $bad) }
    }
    $ref = Get-Content (Join-Path $Root 'src\Refresh.cs') -Raw -Encoding UTF8
    if ($ref.IndexOf('ClickFetchSfx') -ge 0) { return 'Refresh.cs 还在用 ClickFetchSfx' }
    if ($ref.IndexOf('S.NoTitle') -lt 0) { return 'Refresh.cs 没有用 S.NoTitle 兜底' }
    return $true
}

Check '照片墙取图落当前 cache\wall 且不写索引' {
    $wall = Join-Path $ActiveCache 'wall'
    $idx = $ActiveCacheIndex
    $before = if (Test-Path $idx) { Get-Content $idx -Raw -Encoding UTF8 } else { '' }
    $probe = '20180315'
    $r = Run-Script @('-WallDate', $probe)
    if ($r.Code -ne 0 -and $r.Code -ne 4 -and $r.Code -ne 8) { return ('退出码 ' + $r.Code) }
    $hit = @(Get-ChildItem $wall -Filter ($probe + '_*.jpg') -ErrorAction SilentlyContinue)
    if ($hit.Count -eq 0)
    {
        # 本地已有这一天时不会下载，落点断言不适用；
        # 接管图库动辖几千天，2018 年的日子很可能已经在库里。
        $probeDirs = @((Join-Path $Root 'archive\images'), (Join-Path $Root 'favorites'), $ActiveCache)
        if ($TkActive)
        {
            try
            {
                $tr = [string]((Get-Content (Join-Path $Root 'takeover\config.json') -Raw | ConvertFrom-Json).root)
                if ($tr -and (Test-Path $tr)) { $probeDirs += $tr }
            }
            catch { }
        }
        $localHit = 0
        foreach ($pd in $probeDirs)
        {
            if (Test-Path $pd) { $localHit += @(Get-ChildItem $pd -Filter ($probe + '_*.jpg') -File -ErrorAction SilentlyContinue).Count }
        }
        if ($localHit -eq 0) { return 'wall 目录里没落这张图，本地也没有' }
    }
    $after = if (Test-Path $idx) { Get-Content $idx -Raw -Encoding UTF8 } else { '' }
    if ($before -ne $after) { return '照片墙取图把 cache 索引写了' }
    Run-Script @('-WallDrop') | Out-Null
    return $true
}

Check '-Date 归档日期 -DryRun 不下载也不改桌面' {
    $imgDir = Join-Path $Root 'archive\images'
    $beforeImgs = @(Get-ChildItem $imgDir -Filter '*.jpg' -ErrorAction SilentlyContinue).Count
    $beforeWp = ''
    try { $beforeWp = [string](Get-ItemProperty 'HKCU:\Control Panel\Desktop' -Name Wallpaper -ErrorAction SilentlyContinue).Wallpaper } catch { }
    $r = Run-Script @('-Date', '20200315', '-DryRun')
    if ($r.Code -ne 0) { return ('退出码 ' + $r.Code) }
    if ($r.Output -notmatch '\[DryRun\]') { return '输出里没有 DryRun 标记' }
    $afterImgs = @(Get-ChildItem $imgDir -Filter '*.jpg' -ErrorAction SilentlyContinue).Count
    if ($afterImgs -ne $beforeImgs) { return ('归档文件数变了：' + $beforeImgs + ' → ' + $afterImgs) }
    $afterWp = ''
    try { $afterWp = [string](Get-ItemProperty 'HKCU:\Control Panel\Desktop' -Name Wallpaper -ErrorAction SilentlyContinue).Wallpaper } catch { }
    if ($beforeWp -ne $afterWp) { return '桌面壁纸被改了' }
    return $true
}

# ---------------------------------------------------------------- 5. 数据完整性
Write-Host ''
Write-Host '[5] 数据完整性' -ForegroundColor Yellow

Check 'state.json 的 title 非空' {
    $st = Get-Content (Join-Path $Root 'state.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    if ([string]::IsNullOrEmpty($st.title)) { return ('title 为空（date=' + $st.date + '）') }
    return $true
}

Check '三个数据目录存在' {
    foreach ($d in @('cache', 'favorites', 'archive')) {
        if (-not (Test-Path (Join-Path $Root $d))) { return ($d + ' 缺失') }
    }
    return $true
}

Check '配置文件含 archiveMaxMB' {
    $c = Get-Content (Join-Path $Root 'config.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    if (-not ($c.PSObject.Properties.Name -contains 'archiveMaxMB')) { return '缺 archiveMaxMB' }
    return $true
}

# ---------------------------------------------------------------- 6. 清理保护
Write-Host ''
Write-Host '[6] 清理保护' -ForegroundColor Yellow

Check '-CleanArchive -DryRun 不删文件' {
    $dir = Join-Path $Root 'archive\images'
    $before = @(Get-ChildItem $dir -Filter *.jpg -File -ErrorAction SilentlyContinue).Count
    $r = Run-Script @('-CleanArchive', '-DryRun')
    $after = @(Get-ChildItem $dir -Filter *.jpg -File -ErrorAction SilentlyContinue).Count
    if ($before -ne $after) { return ('文件数变化 ' + $before + ' -> ' + $after) }
    return $true
}

Check '-CleanArchive -CleanMode recycle -DryRun' {
    $r = Run-Script @('-CleanArchive', '-CleanMode', 'recycle', '-DryRun')
    if ($r.Code -ne 0) { return ('退出码 ' + $r.Code) }
    return $true
}

Check '-CleanFavorites -DryRun 不删文件' {
    $dir = Join-Path $Root 'favorites'
    $before = @(Get-ChildItem $dir -Filter *.jpg -File -ErrorAction SilentlyContinue).Count
    $r = Run-Script @('-CleanFavorites', '-DryRun')
    $after = @(Get-ChildItem $dir -Filter *.jpg -File -ErrorAction SilentlyContinue).Count
    if ($before -ne $after) { return ('文件数变化 ' + $before + ' -> ' + $after) }
    return $true
}

# ---------------------------------------------------------------- 5. 老索引一致性（v3.6.38）

Write-Host '[6.5] 归档两轮清理' -ForegroundColor Yellow

$sbRoot = Join-Path $env:TEMP ('BWTEST-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
$sbArch = Join-Path $sbRoot 'archive'
$sbFav = Join-Path $sbRoot 'favorites'
$sbPin = Join-Path $sbRoot 'pinned.json'

function New-TestSandbox([string[]]$pinDates, [string[]]$favDates) {
    if (Test-Path $sbRoot) { Remove-Item $sbRoot -Recurse -Force -ErrorAction SilentlyContinue }
    New-Item -ItemType Directory -Path $sbArch -Force | Out-Null
    New-Item -ItemType Directory -Path $sbFav -Force | Out-Null
    $buf = New-Object byte[] 204800
    for ($i = 1; $i -le 8; $i++) {
        [System.IO.File]::WriteAllBytes((Join-Path $sbArch ('2020010' + $i + '.jpg')), $buf)
    }
    foreach ($fd in @($favDates)) {
        if (-not $fd) { continue }
        [System.IO.File]::WriteAllBytes((Join-Path $sbFav ($fd + '_FakeItem_ZH-CN1_1920x1080.jpg')), (New-Object byte[] 16))
    }
    $pin = [ordered]@{}
    foreach ($d in $pinDates) { $pin[$d] = $true }
    [System.IO.File]::WriteAllText($sbPin, (ConvertTo-Json -InputObject $pin -Compress), (New-Object System.Text.UTF8Encoding($false)))
}

function Invoke-Sweep([int]$mb) {
    $env:BING_TEST_SANDBOX = $sbRoot
    return Run-Script @('-TestArchiveDir', '-TestArchiveSweep', ([string]$mb), '-DryRun')
}

Check '第一轮只删未登记项，pinned 与收藏不动' {
    New-TestSandbox -pinDates @('20200102', '20200103') -favDates @('20200101')
    $r = Invoke-Sweep 1
    $o = $r.Output
    foreach ($d in @('20200104', '20200105', '20200106')) {
        if ($o -notmatch ('将删除 ' + $d)) { return ('第一轮该删 ' + $d + ' 却没删') }
    }
    foreach ($d in @('20200101', '20200102', '20200103')) {
        if ($o -match ('将删除 ' + $d)) { return ('第一轮误删受保护的 ' + $d) }
    }
    if ($o -match '含已登记') { return '第一轮就该停住，不该进第二轮' }
    return $true
}

Check '第二轮才动 pinned，收藏仍跳过' {
    New-TestSandbox -pinDates @('20200102', '20200103', '20200104', '20200105', '20200106', '20200107') -favDates @('20200101')
    $r = Invoke-Sweep 1
    $o = $r.Output
    if ($o -notmatch '含已登记') { return '没进第二轮（pinned 没被降级清理）' }
    if ($o -notmatch ('将删除 20200102')) { return '第二轮没删 20200102' }
    if ($o -match ('将删除 20200101')) { return '第二轮误删了收藏项 20200101' }
    return $true
}

Check '归档超限后打出「仍超限」警告' {
    New-TestSandbox -pinDates @() -favDates @('20200101', '20200102', '20200103', '20200104', '20200105', '20200106', '20200107', '20200108')
    $r = Invoke-Sweep 1
    if ($r.Output -notmatch '仍超限') { return '全部受保护时该打出仍超限警告' }
    return $true
}

Check '沙盒路径不带 BWTEST 标记应被拒绝' {
    $env:BING_TEST_SANDBOX = (Join-Path $env:TEMP 'not-a-sandbox')
    $r = Run-Script @('-TestArchiveDir', '-TestArchiveSweep', '1', '-DryRun')
    if ($r.Code -ne 4) { return ('退出码 ' + $r.Code + '，应为 4') }
    return $true
}

Check '沙盒清理不碰真实归档目录' {
    $real = Join-Path $Root 'archive\images'
    $n1 = @(Get-ChildItem $real -Filter '*.jpg' -File -ErrorAction SilentlyContinue).Count
    New-TestSandbox -pinDates @('20200102') -favDates @('20200101')
    $null = Invoke-Sweep 1
    $n2 = @(Get-ChildItem $real -Filter '*.jpg' -File -ErrorAction SilentlyContinue).Count
    if ($n1 -ne $n2) { return ('真实归档从 ' + $n1 + ' 变成 ' + $n2) }
    return $true
}

try { if (Test-Path $sbRoot) { Remove-Item $sbRoot -Recurse -Force -ErrorAction SilentlyContinue } } catch { }

Write-Host '[6.6] 按节日筛选' -ForegroundColor Yellow

Check '-Festivals 列出九个节日' {
    $r = Run-Script @('-Festivals')
    $lines = @($r.Output -split "`n" | Where-Object { $_ -match '天\s*$' })
    if ($lines.Count -ne 9) { return ('列出 ' + $lines.Count + ' 个，应为 9 个') }
    return $true
}

Check '中秋日期由农历算出且含关键年份' {
    $r = Run-Script @('-Festival', '中秋节')
    if ($r.Output -notmatch '"count":(\d+)') { return ('输出不是预期 JSON') }
    $n = [int]$Matches[1]
    if ($n -lt 15) { return ('只有 ' + $n + ' 天，农历算法可能算错') }
    foreach ($d in @('20100922', '20150927', '20201001', '20260925')) {
        if ($r.Output -notmatch $d) { return ('缺关键年份 ' + $d) }
    }
    return $true
}

Check '春节与除夕日期正确且除夕早一天' {
    $r = Run-Script @('-Festival', '春节')
    foreach ($d in @('20100214', '20250129', '20260217')) {
        if ($r.Output -notmatch $d) { return ('春节缺 ' + $d) }
    }
    $r2 = Run-Script @('-Festival', '除夕')
    foreach ($d in @('20100213', '20250128', '20260216')) {
        if ($r2.Output -notmatch $d) { return ('除夕缺 ' + $d) }
    }
    return $true
}

Check '-Festival 不存在的节日应报错' {
    $r = Run-Script @('-Festival', '圣诞节')
    if ($r.Code -ne 1) { return ('退出码 ' + $r.Code + '，应为 1') }
    return $true
}

Write-Host '[7] 老索引一致性' -ForegroundColor Yellow

$oldIdxPath = Join-Path $Root 'archive\old-index.json'
$snapIdxPath = Join-Path $Root 'assets\old-index.json'
$oldArr = @()
try { $oldArr = @(Get-Content $oldIdxPath -Raw -Encoding UTF8 | ConvertFrom-Json) } catch { }

Check '老索引与快照哈希一致' {
    if (-not (Test-Path $oldIdxPath)) { return '缺 archive\old-index.json' }
    if (-not (Test-Path $snapIdxPath)) { return '缺 assets\old-index.json' }
    $a = (Get-FileHash $oldIdxPath -Algorithm SHA256).Hash
    $b = (Get-FileHash $snapIdxPath -Algorithm SHA256).Hash
    if ($a -ne $b) { return '两处索引不一致：build.ps1 未刷新 assets 快照' }
    return $true
}

Check '老索引 2424 条且日期唯一' {
    if ($oldArr.Count -eq 0) { return '老索引读不出来' }
    if ($oldArr.Count -ne 2424) { return ('条数 ' + $oldArr.Count + '，应为 2424') }
    $uniq = @($oldArr | ForEach-Object { [string]$_.d } | Sort-Object -Unique)
    if ($uniq.Count -ne $oldArr.Count) { return ('日期重复：' + $oldArr.Count + ' 条只有 ' + $uniq.Count + ' 个日期') }
    return $true
}

Check '老索引关键锚点' {
    $m = @{}
    foreach ($e in $oldArr) { $m[[string]$e.d] = [string]$e.n }
    $want = [ordered]@{
        '20090713' = 'GreenTurtle_'
        '20091224' = 'SantaClaus_'
        '20091226' = 'MaoBirthday_'
        '20100105' = 'Harbin_'
        '20100327' = 'EarthDay_'
        '20110910' = 'TeacherDay_'
        '20110912' = 'MidAutumnDay_'
        '20120813' = 'OlympicsClosingCeremonies_'
        '20130612' = 'DragonBoatFestival_'
        '20141130' = 'ScottishWildcat_'
    }
    foreach ($k in $want.Keys) {
        if (-not $m.ContainsKey($k)) { return ('缺日期 ' + $k) }
        if (-not $m[$k].StartsWith($want[$k])) { return ($k + ' = ' + $m[$k] + '，应为 ' + $want[$k]) }
    }
    return $true
}

Check '同图多日期恰好 4 组且在白名单内' {
    $g = @{}
    foreach ($e in $oldArr) {
        $n = [string]$e.n
        if (-not $g.ContainsKey($n)) { $g[$n] = @() }
        $g[$n] += [string]$e.d
    }
    $dups = @($g.Keys | Where-Object { $g[$_].Count -gt 1 })
    $wl = @(
        'Cuscus_ZH-CN2223740503_1920x1080.jpg',
        'TurtleHatchling_ZH-CN407919738_1920x1080.jpg',
        'DivingPlatform_ZH-CN1344034380_1920x1080.jpg',
        'LondonAquaticsCentre_ZH-CN1563351439_1920x1080.jpg'
    )
    if ($dups.Count -ne $wl.Count) { return ('组数 ' + $dups.Count + '，应为 ' + $wl.Count) }
    foreach ($d in $dups) { if ($wl -notcontains $d) { return ('白名单外：' + $d) } }
    return $true
}

Check '三天确认空缺（wilii 空 slug 日）' {
    $m = @{}
    foreach ($e in $oldArr) { $m[[string]$e.d] = $true }
    foreach ($d in @('20100416', '20100417', '20120814')) {
        if ($m.ContainsKey($d)) { return ($d + ' 不该有记录') }
    }
    if (-not $m.ContainsKey('20120813')) { return '20120813 该有记录（伦敦奥运闭幕式），日期口径可能又被整体前移' }
    return $true
}

Check '2015 段未被本次改动波及' {
    $bakPath = Join-Path $Root '_old-index-v3637.json'
    if (-not (Test-Path $bakPath)) { return '缺备份 _old-index-v3637.json' }
    $m = @{}
    foreach ($e in $oldArr) { $m[[string]$e.d] = [string]$e.n }
    $bak = @(Get-Content $bakPath -Raw -Encoding UTF8 | ConvertFrom-Json)
    $diff = 0
    foreach ($e in $bak) {
        $d = [string]$e.d
        if ($d -lt '20150101' -or $d -gt '20160304') { continue }
        if (-not $m.ContainsKey($d)) { $diff++; continue }
        if ($m[$d] -ne [string]$e.n) { $diff++ }
    }
    if ($diff -ne 0) { return ('2015-01-01 起有 ' + $diff + ' 处变化') }
    return $true
}

Write-Host '[8] 照片墙：布局与取图档位' -ForegroundColor Yellow

$wallCs = Join-Path $Root 'src\Forms\PhotoWallForm.cs'

Check '卡片区不再被顶栏/底栏压住（Fill 排在 z 序首位）' {
    if (-not (Test-Path $wallCs)) { return '缺 src\Forms\PhotoWallForm.cs' }
    $t = Get-Content $wallCs -Raw
    if ($t -notmatch 'SetChildIndex\(host, 0\)') { return '没找到 SetChildIndex(host, 0)' }
    if ($t -match 'SetChildIndex\(host, Controls\.Count - 1\)') { return '旧的末尾写法还在' }
    return $true
}

Check '卡片区显式设置滚动范围' {
    $t = Get-Content $wallCs -Raw
    if ($t -notmatch 'host\.AutoScrollMinSize\s*=\s*new Size\(') { return '没找到 AutoScrollMinSize 赋值' }
    return $true
}

Check '取回的图逐张上屏' {
    $t = Get-Content $wallCs -Raw
    if ($t -notmatch 'private void RefreshCardFor\(string d8\)') { return '没找到 RefreshCardFor' }
    $m = [regex]::Match($t, 'private void FetchAll\(\)[\s\S]*?RefreshCardFor\(')
    if (-not $m.Success) { return 'FetchAll 里没有调用 RefreshCardFor' }
    return $true
}

Check '照片墙默认先取 1080p 预览' {
    $r = Run-Script @('-WallDate', '20240315', '-DryRun')
    if ($r.Output -notmatch '候选顺序：([^\r\n]*)') {
        # 那天已经在本地：退回源码校验分支顺序（幂等）
        $src = Get-Content $Script -Raw
        if ($src -notmatch '\$script:WallMode -and -not \$WallFull') { return '照片墙分支条件没找到' }
        $i1080 = $src.IndexOf('照片墙预览 1080p')
        if ($i1080 -lt 0) { return '源码里没有「照片墙预览 1080p」候选' }
        $iUhd = $src.IndexOf('归档原图 UHD', $i1080)
        if ($iUhd -lt 0 -or $iUhd -lt $i1080) { return '源码里 1080p 没排在 UHD 之前' }
        return $true
    }
    $order = $Matches[1]
    if ($order -notmatch '^照片墙预览 1080p') { return ('首项不是 1080p：' + $order) }
    return $true
}

Check '-WallFull 把 UHD 放回首位' {
    $r = Run-Script @('-WallDate', '20240315', '-DryRun', '-WallFull')
    if ($r.Output -notmatch '候选顺序：([^\r\n]*)') {
        $src = Get-Content $Script -Raw
        if ($src -notmatch 'if \(\$script:WallMode -and -not \$WallFull\)') { return 'WallFull 分支条件没找到' }
        return $true
    }
    $order = $Matches[1]
    if ($order -notmatch '^(cn4k 3840|归档原图 UHD)') { return ('首项不是 UHD：' + $order) }
    return $true
}

Check '-WallFull 会绕过墙里已有的预览档' {
    $src = Get-Content $Script -Raw
    if ($src -notmatch '-not \$WallFull -and \(Test-Path \$WallDir\)') { return 'wall 命中检查没加 -WallFull 条件' }
    return $true
}

Check '收藏与设为壁纸前会补原图' {
    $t = Get-Content $wallCs -Raw
    if ($t -notmatch 'private string EnsureFullQuality\(') { return '没找到 EnsureFullQuality' }
    $calls = ([regex]::Matches($t, 'src = EnsureFullQuality\(')).Count
    if ($calls -lt 3) { return ('只调用了 ' + $calls + ' 处，应为 3 处') }
    return $true
}

Check '2019 年前的日期不尝试升级' {
    $t = Get-Content $wallCs -Raw
    if ($t -notmatch 'FullQualityFrom\s*=\s*"20190101"') { return '没找到 20190101 门槛' }
    return $true
}

Check '日历与信息窗口能看到照片墙的图' {
    $t = Get-Content (Join-Path $Root 'src\Refresh.cs') -Raw
    if ($t -notmatch 'Directory\.GetFiles\(WallDirPath') { return 'FindImageFilePath 没查 wall 目录' }
    return $true
}

# ---------------------------------------------------------------- [9] 兜底链与失败上报
Write-Host ''
Write-Host '[9] 兜底链与失败上报'

Check '老存档按月份落盘，不再被 Get-DownloadDir 覆盖' {
    $t = Get-Content $Script -Raw
    if ($t -match '\$dirO\s*=\s*Get-DownloadDir') { return '老存档目录仍被 Get-DownloadDir 覆盖' }
    if ($t -notmatch '\$dirO\s*=\s*Join-Path \$OldDir \$ymO') { return '没找到按月归档的 $dirO 赋值' }
    return $true
}

Check '-Date 取 8 天列表有断网降级' {
    $t = Get-Content $Script -Raw
    if ($t -notmatch '\$list8obj = try \{ Get-WallpaperListJson') { return '8 天列表调用没有 try/catch' }
    if ($t -notmatch '\$list8 = @\(\$list8obj \| ConvertFrom-Json') { return 'list8 解析没有跟着改' }
    return $true
}

Check '设壁纸失败会返回 11 而不是报成功' {
    $t = Get-Content $Script -Raw
    $n = ([regex]::Matches($t, 'Exit-Run 11')).Count
    if ($n -lt 5) { return ('只有 ' + $n + ' 处上报 11，应不少于 5（命中本地/老存档/归档/降级/-Use/主流程）') }
    if ($t -match '\[void\]\(Set-DesktopWallpaper -Path \$hit\.FullName') { return '命中本地路径仍在丢弃返回值' }
    return $true
}

Check '未来日期不再伪装成「这一天没图」' {
    $t = Get-Content $Script -Raw
    if ($t -notmatch '不能选择未来日期[^\r\n]*exit 12') { return '未来日期仍返回 5' }
    return $true
}

Check '清理缓存先保当前壁纸再删重复副本' {
    $t = Get-Content $Script -Raw
    $curP = 'if ($cur -and $_.FullName -ieq $cur) { $protected++; return }'
    $favP = 'Join-Path $FavDir $_.Name'
    $iCur = $t.IndexOf($curP)
    $iFav = $t.IndexOf($favP)
    if ($iCur -lt 0) { return '没找到当前壁纸保护判断' }
    if ($iFav -lt 0) { return '没找到收藏副本判断' }
    if ($iCur -gt $iFav) { return '当前壁纸判断排在收藏副本之后' }
    return $true
}

Check '-WallDrop 删除失败不计数' {
    $t = Get-Content $Script -Raw
    if ($t -match 'Remove-Item -LiteralPath \$f\.FullName -Force -ErrorAction SilentlyContinue') {
        return '仍是 SilentlyContinue，删失败也会计数'
    }
    return $true
}

Check '照片墙关窗不冻结界面且会中断取图' {
    $t = Get-Content $wallCs -Raw
    if ($t -notmatch 'private bool closing') { return '没有 closing 标志' }
    if ($t -notmatch 'abortFetch = true') { return '关窗没有中断批量取图' }
    if ($t -notmatch 'S\.WallKeeping') { return '关窗补高清没有状态栏提示' }
    if ($t -notmatch 'if \(abortFetch\) \{ break; \}') { return '取图循环没有检查中断标志' }
    return $true
}

Check '照片墙取图结果分三类计数' {
    $t = Get-Content $wallCs -Raw
    if ($t -notmatch 'else if \(code == 4 \|\| code == 5\) \{ none\+\+; \}') { return '没把「这一天没有图」单独计数' }
    if ($t -notmatch 'WallFetchDone2') { return '没有用 WallFetchDone2' }
    if ($t -match 'if \(code == 0 \|\| code == 4 \|\| code == 8\) \{ ok\+\+; \}') { return '仍把边界码算成取图成功' }
    return $true
}

Check '帮助与关于都写了兜底链与三层封顶' {
    $t = Get-Content (Join-Path $Root 'src\Strings.cs') -Raw
    foreach ($k in @('十一、这张图是从哪来的', '十二、占用为什么不会一直涨', '5 级自动回退', '三层封顶', 'Where the image comes from', 'Why storage stays put')) {
        if ($t.IndexOf($k) -lt 0) { return ('Strings.cs 缺「' + $k + '」') }
    }
    return $true
}

# ---------------------------------------------------------------- [10] 编码与静默失败
Write-Host ''
Write-Host '[10] 编码与静默失败'

Check '所有源码文件带 UTF-8 BOM' {
    $bad = @()
    $list = @()
    $srcD = Join-Path $Root 'src'
    if (Test-Path $srcD) { $list += @(Get-ChildItem $srcD -Recurse -Filter *.cs -File) }
    foreach ($n in @('bing-wallpaper.ps1', 'test.ps1', 'build.ps1')) {
        $p = Join-Path $Root $n
        if (Test-Path $p) { $list += Get-Item $p }
    }
    if ($list.Count -eq 0) { return '没找到任何源文件' }
    foreach ($f in $list) {
        $h = [System.IO.File]::ReadAllBytes($f.FullName)
        if ($h.Length -lt 3 -or $h[0] -ne 0xEF -or $h[1] -ne 0xBB -or $h[2] -ne 0xBF) { $bad += $f.Name }
    }
    if ($bad.Count -gt 0) { return ('缺 BOM：' + ($bad -join '、')) }
    return $true
}

Check '空 catch 不超过 110 处' {
    $t = Get-Content $Script -Raw
    $n = ([regex]::Matches($t, 'catch \{ \}')).Count
    if ($n -gt 110) { return ('空 catch 已 ' + $n + ' 处，超上限 110') }
    return $true
}

Check '退出码有集中说明表' {
    $t = Get-Content $Script -Raw
    if ($t -notmatch '\$EXIT\s*=\s*@\{') { return '脚本里没有 $EXIT 常量表' }
    foreach ($k in @('WALL_FAIL = 11', 'FUTURE    = 12', 'DEGRADED  = 8', 'NO_IMAGE  = 5')) {
        if ($t.IndexOf($k) -lt 0) { return ('$EXIT 表缺 ' + $k) }
    }
    return $true
}

Check '关键失败路径有留痕日志' {
    $t = Get-Content $Script -Raw
    foreach ($k in @('壁纸设置后注册表未更新', '回收站不可用，已直接删除',
                     '保护名单写入失败', '缓存索引同步失败', '归档月度缓存读取失败',
                     '下载目录创建失败')) {
        if ($t.IndexOf($k) -lt 0) { return ('缺关键日志：' + $k) }
    }
    return $true
}

# ---------------------------------------------------------------- [11] 文案校准
Write-Host ''
Write-Host '[11] 文案校准'

Check 'AI 调用窗口的参数与脚本对齐' {
    $t = Get-Content (Join-Path $Root 'src\Strings.cs') -Raw
    if ($t -match '2016-03-05 至今') { return 'AI 窗口仍写 2016-03-05 边界' }
    foreach ($k in @('-MigrateOld', '-WallFull', '-WallKeep', '-PrefetchDate', '-Festivals', '-CleanFavorites', '-KwFile', '-Unfavorite')) {
        if ($t.IndexOf($k) -lt 0) { return ('AI 窗口缺参数：' + $k) }
    }
    return $true
}

Check '三处分辨率口径一致' {
    $s = Get-Content (Join-Path $Root 'src\Strings.cs') -Raw
    if ($s -match '2016 年 3 月之后才是') { return '帮助仍写「2016 年 3 月之后才是 UHD」' }
    if ($s -match '2016-03 至今') { return 'SrchHint 仍写索引从 2016-03 起' }
    if ($s -notmatch '2019 年起才是') { return '帮助没写「2019 年起才是 UHD」' }
    $r = Get-Content (Join-Path $Root 'README.md') -Raw
    if ($r -match '2016 年 3 月之后才是') { return 'README 仍写「2016 年 3 月之后才是 UHD」' }
    if ($r.IndexOf('| 11 |') -lt 0) { return 'README 退出码表缺 11' }
    if ($r.IndexOf('| 12 |') -lt 0) { return 'README 退出码表缺 12' }
    return $true
}

# ---------------------------------------------------------------- [12] 交互细节
Write-Host ''
Write-Host '[12] 交互细节'

Check '搜索与随机按钮常亮（靠 busy 拦重入）' {
    $t = Get-Content (Join-Path $Root 'src\Forms\SearchForm.cs') -Raw
    if ($t -match 'btnSearch\.Enabled\s*=\s*false') { return '搜索按钮仍会被禁用' }
    if ($t -match 'btnRandom\.Enabled\s*=\s*false') { return '随机按钮仍会被禁用' }
    if ($t -notmatch 'private bool busy') { return '没有 busy 重入保护' }
    return $true
}

Check '空关键词直接随机，不弹确认框' {
    $t = Get-Content (Join-Path $Root 'src\Forms\SearchForm.cs') -Raw
    if ($t -match 'HandleEmptyAction') { return '仍保留 HandleEmptyAction 确认流程' }
    if ($t -notmatch 'if \(kw\.Length == 0\) \{ PickRandomGroup\(\); return; \}') { return '空关键词没有直接走随机' }
    $s = Get-Content (Join-Path $Root 'src\Strings.cs') -Raw
    if ($s -match 'SrchEmptyAsk') { return '确认框文案还留着（对应行为已删）' }
    return $true
}

Check '日历双击用系统双击时间' {
    $t = Get-Content (Join-Path $Root 'src\Forms\CalendarForm.cs') -Raw
    if ($t -notmatch 'SystemInformation\.DoubleClickTime') { return '双击判定仍是硬编码 500ms' }
    if ($t -notmatch 'f\.AcceptPick\(\)') { return '双击没有触发换这张' }
    return $true
}

Check '日历重设加粗日期后会重挂双击钩子' {
    $t = Get-Content (Join-Path $Root 'src\Forms\CalendarForm.cs') -Raw
    if ($t -notmatch 'private void RefreshBolded') { return '没有 RefreshBolded' }
    if ($t -notmatch 'cal\.Handle != before') { return '没检测句柄变化 —— MonthCalendar 设 BoldedDates 会重建窗口' }
    if ($t -notmatch 'hook\.AssignHandle\(cal\.Handle\)') { return '没有重新挂钩子' }
    return $true
}

Check '日历双击不会叠加触发' {
    $t = Get-Content (Join-Path $Root 'src\Forms\CalendarForm.cs') -Raw
    if ($t -notmatch 'private void AcceptPick\(\)\s*\{\s*if \(busy\) \{ return; \}') { return 'AcceptPick 没有 busy 保护，连点会撞锁' }
    return $true
}

Check '信息窗口按钮不捕获构造参数' {
    $t = Get-Content (Join-Path $Root 'src\Forms\InfoForm.cs') -Raw
    if ($t -match 'btnLearn\.Click \+= delegate \{ Program\.OpenUrl\(searchUrl\)') { return '了解更多仍捕获构造参数 searchUrl —— 翻页后不会变' }
    if ($t -match 'btnFolder\.Click \+= delegate \{ Program\.ShowInFolder\(file\)') { return '在文件夹中显示仍捕获构造参数 file' }
    if ($t -notmatch 'private string curSearchUrl') { return '没有 curSearchUrl 字段' }
    if ($t -notmatch 'Reload\([^)]*\)\s*\{\s*curDate = date;\s*curSearchUrl =') { return 'Reload 里没更新 curSearchUrl' }
    return $true
}

Check '归档索引的 l 字段也被认' {
    $t = Get-Content (Join-Path $Root 'src\Info.cs') -Raw
    if ($t -notmatch 'fe\.ContainsKey\("l"\)') { return 'Info.cs 只认 link，索引里实际是 l，兜底一直是死代码' }
    return $true
}

Check '浏览更多壁纸改成打开本机照片墙' {
    $t = Get-Content (Join-Path $Root 'src\Forms\InfoForm.cs') -Raw
    if ($t -match 'wdbyte') { return '仍指向第三方站点 wdbyte' }
    if ($t -match 'MoreUrl') { return '仍保留 MoreUrl 常量' }
    if ($t -notmatch 'btnMore\.Click \+= delegate \{ OpenSameDayWall\(\); \}') { return '按钮没改成打开照片墙' }
    if ($t -notmatch 'new PhotoWallForm\(ds, S\.WallTitle\(mmdd\)\)') { return '没有按当前日期构造历年列表' }
    $s = Get-Content (Join-Path $Root 'src\Strings.cs') -Raw
    if ($s -notmatch 'InfoBtnMore[^}]*往年的今天') { return '按钮文案没改' }
    return $true
}

Check '换图记录：改名、挪位置、加类别列、双击换图' {
    $s = Get-Content (Join-Path $Root 'src\Strings.cs') -Raw
    if ($s -match '自动清理保留') { return 'Strings.cs 里还留着「自动清理保留」' }
    if ($s -notmatch 'PinMenu\s+\{ get \{ return T\("换图记录…"') { return '菜单项没改成「换图记录」' }
    if ($s -notmatch 'PinColWhere') { return '没有位置列文案' }
    if ($s -notmatch 'PinLocArch') { return '没有「归档」位置文案' }
    if ($s -match '随机一张收藏') { return '「随机一张收藏」没改' }

    $m = Get-Content (Join-Path $Root 'src\Menu.cs') -Raw
    if ($m -notmatch 'pinmgr') { return 'Menu.cs 里找不到 pinmgr' }
    $pinIdx = $m.IndexOf('pinMgrItem = MakeItem(S.PinMenu')
    $cleanIdx = $m.IndexOf('ToolStripMenuItem cleanMenu')
    if ($pinIdx -lt 0) { return '没有在主菜单创建 pinMgrItem' }
    if ($cleanIdx -gt 0 -and $pinIdx -gt $cleanIdx) { return 'pinMgrItem 仍在清理菜单里创建' }

    $p = Get-Content (Join-Path $Root 'src\Forms\PinnedForm.cs') -Raw
    if ($p -notmatch 'lst\.DoubleClick \+= delegate \{ ApplySelectedPinned\(\); \}') { return '双击没改成换这张' }
    if ($p -notmatch 'private void ApplySelectedPinned') { return '没有 ApplySelectedPinned' }
    if ($p -notmatch 'private static string LocationOf') { return '没有 LocationOf' }
    if ($p -notmatch 'ContextMenuStrip cm') { return '打开文件没移到右键菜单' }
    return $true
}

Check '帮助与 README 说明 2016 年前的老图不受自动清理' {
    $s = Get-Content (Join-Path $Root 'src\Strings.cs') -Raw
    if ($s -notmatch '2016 年前的老图（那时只有 1366×768）在以前换过的壁纸的额度之外单独保管') { return '帮助第四节没提老图不受自动清理' }
    $r = Get-Content (Join-Path $Root 'README.md') -Raw
    if ($r -notmatch '2016 年前的老图不受任何自动清理') { return 'README 没写老图不受自动清理' }
    return $true
}

Check '清理窗口把归档与老存档拆开显示' {
    $s = Get-Content (Join-Path $Root 'src\Strings.cs') -Raw
    if ($s -notmatch 'CleanArchRow') { return '没有「归档图片」行文案' }
    if ($s -notmatch 'CleanOldRow') { return '没有「老存档」行文案' }
    $c = Get-Content (Join-Path $Root 'src\Forms\CleanForm.cs') -Raw
    if ($c -match 'CleanOldInline\(oldN') { return '仍在用「含老存档」的旧写法（可清理数会大于总数）' }
    if ($c -notmatch 'S\.CleanOldRow') { return 'CleanForm 没有渲染老存档行' }
    return $true
}

Check '换过的图：位置分四种，移出时跳过老存档与收藏' {
    $s = Get-Content (Join-Path $Root 'src\Strings.cs') -Raw
    if ($s -notmatch 'PinLocArch') { return '缺「归档」位置文案' }
    if ($s -notmatch 'PinLocOld') { return '缺「老存档」位置文案' }
    $p = Get-Content (Join-Path $Root 'src\Forms\PinnedForm.cs') -Raw
    if ($p -notmatch 'loc == S\.PinLocOld') { return '移出名单没有跳过老存档' }
    if ($p -notmatch 'loc == S\.PinLocFav') { return '移出名单没有跳过收藏' }
    if ($p -notmatch 'S\.PinConfirm2') { return '没有用按类别说明的确认框' }
    return $true
}

Check '菜单入口带规模：换过的图与收藏管理都显示张数和体积' {
    $s = Get-Content (Join-Path $Root 'src\Strings.cs') -Raw
    if ($s -notmatch 'PinMenuN\(int n, double mb\)') { return '缺 PinMenuN（张数+体积）' }
    if ($s -notmatch 'FavManagerN\(int n, double mb\)') { return '缺 FavManagerN（张数+体积）' }
    $r = Get-Content (Join-Path $Root 'src\Refresh.cs') -Raw
    if ($r -notmatch 'void RefreshEntryCheck') { return '缺 RefreshEntryCheck' }
    if ($r -notmatch 'menu\.Items\["pinmgr"\]') { return 'pinmgr 没有从顶层菜单取（曾错取 clean 子项导致数字永不出现）' }
    if ($r -notmatch 'menu\.Items\["favmgr"\]') { return 'favmgr 没有刷新张数与体积' }
    if ($r -notmatch 'PinnedStat\(out int') { return '缺 PinnedStat' }
    if ($r -notmatch 'FavStat\(out int') { return '缺 FavStat' }
    return $true
}

Check '2016 年前的老图要删的去处写进了两个提示' {
    $s = Get-Content (Join-Path $Root 'src\Strings.cs') -Raw
    if ($s -notmatch '要删请走 清理 → 立即清理') { return '移出名单确认框没写去哪删老图' }
    if ($s -notmatch '2016 年前的老图要删：清理 → 立即清理') { return '移出无效弹窗没写去哪删老图' }
    return $true
}

Check '故事缓存合并进归档清理：脚本不再提前退出，改由勾选决定' {
    $ps = Get-Content (Join-Path $Root 'bing-wallpaper.ps1') -Raw
    if ($ps -notmatch 'function Clear-StoryCache') { return '缺 Clear-StoryCache 函数' }
    # 关键：带 -CleanArchive 时不能先被故事分支吃掉再 Exit-Run 0，否则归档一张都清不掉
    if ($ps -notmatch '\$CleanStories -and -not \$CleanArchive') { return '故事分支仍会无条件提前退出，归档清理会被架空' }
    if ($ps -notmatch 'Clear-StoryCache -Mode \$modeTemp') { return '归档分支没有调用 Clear-StoryCache' }
    if ($ps -notmatch '"storiesRemoved":') { return '清理结果里没有 storiesRemoved' }
    return $true
}

Check '归档窗口有「同时清理每日故事缓存」勾选，默认不勾' {
    $f = Get-Content (Join-Path $Root 'src\Forms\CleanForm.cs') -Raw
    if ($f -notmatch 'WithStories') { return 'CleanForm 没有 WithStories' }
    if ($f -notmatch 'cbStories\.Checked = false') { return '故事勾选默认被勾上了（应默认不勾）' }
    if ($f -notmatch 'Program\.StoriesDirPath') { return '没统计故事缓存规模' }
    $t = Get-Content (Join-Path $Root 'src\Tray.cs') -Raw
    if ($t -notmatch 'if \(f\.WithStories\) \{ extra \+= " -CleanStories"; \}') { return 'Tray 没把勾选拼成 -CleanStories' }
    $m = Get-Content (Join-Path $Root 'src\Menu.cs') -Raw
    if ($m -match '"cleanstories"') { return '菜单里还留着独立的清理故事入口' }
    return $true
}

Check '随机换一张：三档范围，any 档交给指定日期流程而不是自己下载' {
    $ps = Get-Content (Join-Path $Root 'bing-wallpaper.ps1') -Raw
    if ($ps -notmatch '\$RandomScope') { return '缺 -RandomScope 参数' }
    if ($ps -notmatch "RandomScope = 'local'") { return '默认范围不是 local' }
    if ($ps -notmatch 'scope -ne ''fav'' -and \$scope -ne ''any''') { return '非法范围没有回落 local' }
    # 关键：本地没有这张时必须把日期交给「指定日期」流程，否则 pinned / state / 索引全要重写一遍
    if ($ps -notmatch '\$Date = \$picked') { return 'any 档没把日期交给指定日期流程' }
    if ($ps -notmatch "\`$byDate\[\`$k0\] = @\{ file = \`$f0; from = \`$src.from \}") { return '本地池没有按日期去重' }
    $r = Get-Content (Join-Path $Root 'src\Refresh.cs') -Raw
    if ($r -notmatch 'internal static int LocalPoolCount') { return '缺 LocalPoolCount' }
    if ($r -notmatch 'internal static string RandomScopeValue') { return '缺 RandomScopeValue' }
    $m = Get-Content (Join-Path $Root 'src\Menu.cs') -Raw
    foreach ($k in @('randfav', 'randlocal', 'randany')) {
        if ($m -notmatch $k) { return ('随机子菜单缺 ' + $k) }
    }
    return $true
}

Check '接管目录放在增强数据源里，不在壁纸库末尾' {
    $m = Get-Content (Join-Path $Root 'src\Menu.cs') -Raw
    if ($m -notmatch 'S\.TkMenu') { return '增强数据源里没有接管目录' }
    $r = Get-Content (Join-Path $Root 'src\Refresh.cs') -Raw
    if ($r -match 'lib\.DropDownItems\.Add\(MakeItem\(S\.TkMenu') { return '壁纸库里还留着接管目录' }
    return $true
}

Check '接管模式只读：容量清理跳过、老存档不用、暂存发布' {
    $ps = Get-Content (Join-Path $Root 'bing-wallpaper.ps1') -Raw
    # 少了这句，「换一张壁纸」会顺带删掉用户几十 GB 的图库
    if ($ps -notmatch 'if \(\$script:Takeover\) \{ return 0 \}') { return 'Clear-ArchiveBySize 没在接管下短路' }
    if ($ps -notmatch 'old-not-used') { return '接管时老存档目录没被摘掉' }
    if ($ps -notmatch '\$script:LibStaging = Join-Path \$TakeoverDir') { return '缺暂存区' }
    if ($ps -notmatch 'function Publish-ToLibrary') { return '缺 Publish-ToLibrary' }
    if ($ps -notmatch 'Publish-ToLibrary -Staged \$realDest') { return '下载成功后没走暂存发布' }
    $mv = [regex]::Match($ps, '(?s)function Move-FileKeepWallpaper.*?\n\}')
    if (-not $mv.Success) { return '找不到 Move-FileKeepWallpaper' }
    if ($mv.Value -notmatch 'inLibrary') { return '搬运函数接管下没有只复制分支' }
    return $true
}

Check '随机撞到死日期：预检三态 + 重抽 + 记账' {
    $ps = Get-Content (Join-Path $Root 'bing-wallpaper.ps1') -Raw
    if ($ps -notmatch 'function Test-ArchiveReachable') { return '缺 Test-ArchiveReachable' }
    if ($ps -notmatch "return 'netfail'") { return '预检没区分网络失败' }
    if ($ps -notmatch "return 'missing'") { return '预检没区分确实没有' }
    if ($ps -notmatch 'function Read-DeadDates') { return '缺 Read-DeadDates' }
    if ($ps -notmatch 'function Note-DeadDate') { return '缺 Note-DeadDate' }
    if ($ps -notmatch 'NetFail|netFail') { return '记账没有排除网络失败' }
    $rd = [regex]::Match($ps, '(?s)if \(\$scope -eq .any.\).*?\n        \}')
    if (-not $rd.Success) { return '找不到 any 档分支' }
    if ($rd.Value -notmatch 'for \(\$k = 0; \$k -lt 3') { return 'any 档没有 3 次重抽' }
    if ($rd.Value -notmatch 'Read-DeadDates') { return 'any 档抽池没排除死日期' }
    return $true
}

Check '图库补齐：索引与 -FillLibrary 只增不覆盖' {
    $ps = Get-Content (Join-Path $Root 'bing-wallpaper.ps1') -Raw
    if ($ps -notmatch '_bing-chronicle-index.json') { return '缺图库索引文件名' }
    if ($ps -notmatch 'function Write-LibraryIndex') { return '缺 Write-LibraryIndex' }
    if ($ps -notmatch 'if \(\$FillLibrary\)') { return '缺 -FillLibrary 分支' }
    $fb = [regex]::Match($ps, '(?s)if \(\$FillLibrary\).*?Exit-Run 0')
    if (-not $fb.Success) { return '找不到 FillLibrary 主体' }
    if ($fb.Value -notmatch 'Get-LibraryDates') { return '补齐没先扫已有日期' }
    if ($fb.Value -notmatch 'if \(\$haveF\.ContainsKey\(\$dd\)\) \{ continue \}') { return '补齐没有跳过已有日期' }
    return $true
}

Check '按日期取不到时窗口说得清是哪一种失败' {
    $c = Get-Content (Join-Path $Root 'src\Forms\CalendarForm.cs') -Raw
    if ($c -notmatch 'code == 4' -or $c -notmatch 'S\.SrchNoSource') { return '日历窗口没区分源不可达' }
    if ($c -notmatch 'code == 5' -or $c -notmatch 'S\.SrchNoImage') { return '日历窗口没区分这天没图' }
    $s = Get-Content (Join-Path $Root 'src\Forms\SearchForm.cs') -Raw
    if ($s -notmatch 'S\.SrchNoSource' -or $s -notmatch 'S\.SrchNoImage') { return '搜索窗口没区分失败类型' }
    $st = Get-Content (Join-Path $Root 'src\Strings.cs') -Raw
    if ($st -notmatch 'SrchNoSource' -or $st -notmatch 'SrchNoImage') { return '缺失败文案' }
    return $true
}

Check '接管窗口统计递归、带覆盖天数、有补齐入口' {
    $t = Get-Content (Join-Path $Root 'src\Forms\TakeoverForm.cs') -Raw
    if ($t -notmatch 'SearchOption\.AllDirectories') { return '接管窗口统计仍是非递归' }
    if ($t -notmatch 'TkOnBody\(root, n, days') { return '没显示覆盖天数' }
    if ($t -notmatch 'DoFill') { return '缺补齐入口' }
    if ($t -notmatch '-FillLibrary -RangeFrom') { return '补齐没调 -FillLibrary' }
    $st = Get-Content (Join-Path $Root 'src\Strings.cs') -Raw
    if ($st -notmatch 'TkOnBody\(string root, int n, int days') { return 'TkOnBody 签名没改' }
    return $true
}

Check '全局异常兜底：UI 线程异常不退出，只记日志' {
    $p = Get-Content (Join-Path $Root 'src\Program.cs') -Raw
    if ($p -notmatch 'InstallCrashGuards') { return '缺 InstallCrashGuards' }
    if ($p -notmatch 'SetUnhandledExceptionMode\(UnhandledExceptionMode\.CatchException\)') { return '没设 CatchException 模式，ThreadException 不会触发' }
    if ($p -notmatch 'Application\.ThreadException') { return '没挂 ThreadException' }
    if ($p -notmatch 'AppDomain\.CurrentDomain\.UnhandledException') { return '没挂 AppDomain 兜底' }
    if ($p -notmatch '\[CRASH\]') { return '崩溃没写进日志' }
    if ($p -notmatch 'InstallCrashGuards\(\);') { return 'Main 里没调用' }
    return $true
}

Check '搜索/链接/收藏三处不再在 UI 线程同步跑脚本' {
    foreach ($f in @('src\Forms\SearchForm.cs', 'src\Forms\LinkForm.cs', 'src\Forms\FavForm.cs')) {
        $t = Get-Content (Join-Path $Root $f) -Raw
        if ($t -match 'Application\.DoEvents') { return ($f + ' 还在用 DoEvents 假解阻塞') }
        if ($t -notmatch 'ThreadPool\.QueueUserWorkItem') { return ($f + ' 没改成后台线程') }
    }
    $s = Get-Content (Join-Path $Root 'src\Forms\SearchForm.cs') -Raw
    if ($s -notmatch 'RunQueryAsync') { return '搜索窗口缺统一的异步查询入口' }
    $fz = Get-Content (Join-Path $Root 'src\Forms\FavForm.cs') -Raw
    if ($fz -notmatch 'busyUse') { return '收藏窗口没有防重入' }
    return $true
}

Check '补齐只在接管模式可用' {
    $ps = Get-Content (Join-Path $Root 'bing-wallpaper.ps1') -Raw
    $m = [regex]::Match($ps, '(?s)if \(\$FillLibrary\) \{.{0,600}')
    if (-not $m.Success) { return '找不到 FillLibrary 分支' }
    if ($m.Value -notmatch 'if \(-not \$script:Takeover\)') { return '没拦非接管模式（补了会被容量上限清掉）' }
    return $true
}

Check '命令行速查表抽到 Strings，内容与参数一致' {
    $a = Get-Content (Join-Path $Root 'src\Forms\AiForm.cs') -Raw
    if ($a -notmatch 'S\.AiCommands\(Program\.ScriptPathPublic\)') { return 'AiForm 没走 S.AiCommands' }
    $n = ([regex]::Matches($a, '"[^"]*[一-龥][^"]*"')).Count
    if ($n -gt 2) { return ('AiForm 还有 ' + $n + ' 处硬编码中文，应全部抽到 Strings') }
    $st = Get-Content (Join-Path $Root 'src\Strings.cs') -Raw
    foreach ($k in @('-RandomScope', '-FillLibrary', '-MigrateOld')) {
        if ($st -notmatch [regex]::Escape($k)) { return ('速查表缺 ' + $k) }
    }
    if ($st -match '\-Random             从收藏') { return '速查表还写着「从收藏随机」' }
    return $true
}

Check '数量单位抽到 Strings，窗体不再拼中文单位' {
    $st = Get-Content (Join-Path $Root 'src\Strings.cs') -Raw
    if ($st -notmatch 'UnitImg\(int n\)') { return '缺 UnitImg' }
    if ($st -notmatch 'UnitFile\(int n\)') { return '缺 UnitFile' }
    $c = Get-Content (Join-Path $Root 'src\Forms\CleanForm.cs') -Raw
    if ($c -match '\+ " 张 · "') { return 'CleanForm 还在拼中文单位' }
    if ($c -match '\+ " 张"') { return 'CleanForm 还有裸「张」' }
    return $true
}

Check '收藏后主动弹出「这张图是哪儿」，且窗口单例非模态' {
    $i = Get-Content (Join-Path $Root 'src\Info.cs') -Raw
    if ($i -notmatch 'private static InfoForm infoForm') { return '信息窗口不是单例' }
    if ($i -match 'using \(InfoForm f') { return '还在用 using + ShowDialog（模态）' }
    if ($i -notmatch 'infoForm\.Show\(anchor\)') { return '没改成非模态 Show' }
    if ($i -notmatch 'ReloadFromState\(\)') { return '已开窗口时不刷新内容' }
    if ($i -notmatch 'ShowInfoPublic') { return '缺给回调的公开入口' }
    $s = Get-Content (Join-Path $Root 'src\Script.cs') -Raw
    if ($s -notmatch 'ShowInfoPublic\(\)') { return '收藏成功后没弹窗' }
    $favBlock = [regex]::Match($s, '(?s)opMode == "fav".*?\n                        \}')
    if (-not $favBlock.Success) { return '找不到 fav 分支' }
    if ($favBlock.Value -notmatch 'ShowInfoPublic') { return 'fav 分支里没弹窗' }
    $uf = [regex]::Match($s, '(?s)opMode == "unfav".*?\n                        \}')
    if ($uf.Success -and $uf.Value -match 'ShowInfoPublic') { return '取消收藏不该弹窗' }
    $f = Get-Content (Join-Path $Root 'src\Forms\InfoForm.cs') -Raw
    if ($f -notmatch 'btnClose\.Click \+= delegate \{ Close\(\); \}') { return '非模态后关闭按钮仍是 DialogResult' }
    if ($f -notmatch 'internal void ReloadFromState') { return '缺 ReloadFromState' }
    return $true
}

Check '收藏会移动文件，必须把 state 里的路径改到新位置' {
    $ps = Get-Content (Join-Path $Root 'bing-wallpaper.ps1') -Raw
    $m = [regex]::Match($ps, '(?s)if \(\$Favorite -or \$Unfavorite\) \{.{0,400}')
    if (-not $m.Success) { return '找不到收藏分支' }
    # 全局 $state 的赋值在主流程后段，收藏分支里拿不到 —— 必须就地读一次
    if ($m.Value -notmatch '\$stateCur = Read-State') { return '收藏分支没有就地读 state' }
    $blk = [regex]::Match($ps, '(?s)if \(\$Favorite -or \$Unfavorite\) \{.*?if \(\$Random\) \{')
    if (-not $blk.Success) { return '找不到收藏分支主体' }
    if ($blk.Value -match '(?<![A-Za-z])\$state\s+-and') { return '分支里还在用全局 $state（它此时是 null，判断恒假）' }
    if ($blk.Value -notmatch 'Write-State -Date \$dateKey -File \$dest') { return '收藏后没把 state.file 改到收藏夹路径' }
    return $true
}

Check '收藏不再只认缓存来源，且不重复存两份' {
    $ps = Get-Content (Join-Path $Root 'bing-wallpaper.ps1') -Raw
    if ($ps -match '只有缓存或画廊里的壁纸才能收藏') { return '还在拒绝非缓存来源的收藏' }
    # 只有接管模式才复制（图库只读）；其余一律移动，避免归档与收藏各存一份
    if ($ps -notmatch '(?m)^\s*\$copyOnly = \$script:Takeover\s*$') { return '$copyOnly 应只对 Takeover 成立' }
    if ($ps -match '\$copyOnly = \$script:Takeover -or -not \$inCache') { return '还在按来源复制，会产生第二份' }
    if ($ps -notmatch 'if \(\$copyOnly\) \{\s*\r?\n\s*Copy-Item -LiteralPath \$cur') { return '缺接管模式的复制分支' }
    return $true
}

Check '每日故事按实测高度排版，不再按字数硬截' {
    $f = Get-Content (Join-Path $Root 'src\Forms\InfoForm.cs') -Raw
    if ($f -match 'Substring\(0, 150\)') { return '还在按 150 字符硬截' }
    if ($f -match 'story\.Length > 150') { return '还在用字数判断是否折叠' }
    if ($f -notmatch 'TextRenderer\.MeasureText') { return '没用实测高度，Label 会把正文裁掉' }
    if ($f -notmatch 'private void ApplyStoryLayout') { return '缺统一的排版入口' }
    if ($f -notmatch 'storyFull\.Length') { return '没有完整正文的判据' }
    # 展开与收起必须都走同一个排版函数，避免两套阈值互相打架
    $tg = [regex]::Match($f, '(?s)private void ToggleStory\(\).*?\n        \}')
    if (-not $tg.Success) { return '找不到 ToggleStory' }
    if ($tg.Value -notmatch 'ApplyStoryLayout\(storyExpanded\)') { return '展开/收起没走统一排版' }
    return $true
}

Check '常驻的信息窗口跟着任何换图刷新' {
    $i = Get-Content (Join-Path $Root 'src\Info.cs') -Raw
    if ($i -notmatch 'internal static void ReloadInfoIfOpen') { return '缺 ReloadInfoIfOpen' }
    $r = Get-Content (Join-Path $Root 'src\Refresh.cs') -Raw
    $ra = [regex]::Match($r, '(?s)private static void RefreshAll\(\).*?finally \{ refreshing = false; \}')
    if (-not $ra.Success) { return '找不到 RefreshAll' }
    if ($ra.Value -notmatch 'ReloadInfoIfOpen\(\)') { return 'RefreshAll 没有同步信息窗口' }
    $f = Get-Content (Join-Path $Root 'src\Forms\InfoForm.cs') -Raw
    if ($f -notmatch 'internal void ReloadIfChanged') { return '缺 ReloadIfChanged（同一张不该重载）' }
    if ($f -notmatch 'curDate == st\[0\] && curFile == st\[3\]') { return 'ReloadIfChanged 没做同图短路' }
    return $true
}

Check '每日故事正文随安装包分发' {
    $b = Get-Content (Join-Path $Root 'build.ps1') -Raw
    if ($b -notmatch 'stories\.zip') { return 'build.ps1 没打包故事正文' }
    if ($b -notmatch 'CreateEntryFromFile') { return 'build.ps1 没把 txt 压进 zip' }
    if ($b -notmatch '/resource:\$storyZip,stories\.zip') { return '故事包没作为资源嵌入' }
    if ($b -notmatch 'System\.IO\.Compression\.FileSystem\.dll') { return '缺压缩库引用' }
    $p = Get-Content (Join-Path $Root 'src\Program.cs') -Raw
    if ($p -notmatch 'GetManifestResourceStream\("stories\.zip"\)') { return 'Program.cs 没读故事包' }
    if ($p -notmatch 'if \(File\.Exists\(dest\)\) \{ continue; \}') { return '释放时没跳过已存在的故事（会覆盖联网抓的新版本）' }
    if ($p -match '"stories\.zip",') { return 'stories.zip 不该进 PayloadFiles（会解到程序根目录）' }
    return $true
}

Check '故事映射表可自动补齐，过期会报出来' {
    $ps = Get-Content (Join-Path $Root 'bing-wallpaper.ps1') -Raw
    if ($ps -notmatch '\[switch\]\$UpdateStoryMap') { return '缺 -UpdateStoryMap 参数' }
    $b = [regex]::Match($ps, '(?s)if \(\$UpdateStoryMap\) \{.*?Exit-Run 1')
    if (-not $b.Success) { return '找不到 UpdateStoryMap 主体' }
    # 靠图名对齐，不靠列表顺序猜 —— 首页只有 slug 没有日期
    if ($b.Value -notmatch 'OHR\\\.') { return '没有从 urlbase 提取图名' }
    if ($b.Value -notmatch 'creplace') { return '没有驼峰转中划线' }
    if ($b.Value -notmatch 'AddDays\(1\)') { return 'startdate 是 UTC，没换算到本地日期' }
    if ($b.Value -notmatch '\$mapS\.ContainsKey\(\$dS\)') { return '没有跳过已有条目' }
    # 自愈要有 30 天限制，否则查早期日期会反复触发网络请求
    if ($ps -notmatch 'recentS = \(\$Date -ge \(Get-Date\)\.AddDays\(-30\)') { return '自愈没有时间窗限制' }
    if ($ps -notmatch '\$script:StoryMap = \$null') { return '子进程更新后没有让缓存失效' }
    if ($ps -notmatch '故事映射') { return '-Status 没输出故事映射新鲜度' }
    return $true
}

Check '取图通道：跑分之前保留本地代理优先的初始顺序' {
    $ps = Get-Content (Join-Path $Root 'bing-wallpaper.ps1') -Raw
    if ($ps -notmatch 'function Get-LocalProxy') { return '缺本地代理探测' }
    if (-not $ps.Contains('$script:LocalProxyHit')) { return '探测结果没有缓存' }
    if (-not $ps.Contains('LocalProxyDay -eq $today')) { return '探测缓存没有按日失效' }
    $g = [regex]::Match($ps, '(?s)function Get-ArchiveCandidateUrls.*?Get-ViaStats')
    if (-not $g.Success) { return '找不到候选列表构造' }
    $ipx = $g.Value.IndexOf('Get-LocalProxy')
    $igh = $g.Value.IndexOf("tag='ghproxy'")
    if ($ipx -lt 0) { return '候选列表没先看本地代理' }
    if ($igh -lt 0 -or $ipx -gt $igh) { return '初始顺序里本地代理没排在 ghproxy 前面' }
    if (-not $ps.Contains("Invoke-ArchiveFetch -RelPath 'Zhu-junwei")) { return '全量索引没接统一候选' }
    return $true
}

Check '故事映射每日校准，已存在的条目也会核对更正' {
    $ps = Get-Content (Join-Path $Root 'bing-wallpaper.ps1') -Raw
    if ($ps -notmatch '\$fixedS\+\+') { return '没有校正计数' }
    if ($ps -notmatch '校正 ') { return '已存在条目没有核对逻辑' }
    if ($ps -notmatch 'TotalHours -ge 24') { return '没有每日校准的节流' }
    if ($ps -notmatch '& \$PSCommandPath -UpdateStoryMap \*> \$null') { return '没有自动调用更新' }
    return $true
}

Check '取图通道按实测耗时记账，每次取图重排' {
    $ps = Get-Content (Join-Path $Root 'bing-wallpaper.ps1') -Raw
    if ($ps -notmatch 'function Get-ViaStats') { return '缺 Get-ViaStats' }
    if ($ps -notmatch 'function Set-ViaStat') { return '缺 Set-ViaStat' }
    # 指数移动平均：不是累加分数，才不会撞上限饱和
    if (-not $ps.Contains('$curMs * 0.7 + [double]$Ms * 0.3')) { return '成功没有做指数平均' }
    # 失败封顶，否则连续失败会 1.5^N 爆炸
    if (-not $ps.Contains('[math]::Min(60000.0, $curMs * 1.5)')) { return '失败耗时不封顶' }
    # 三天没动过向默认值回归，坏通道能翻身
    if (-not $ps.Contains('$ms * 0.5 + 2500.0 * 0.5')) { return '陈旧记录没有向默认值回归' }
    if (-not $ps.Contains('return @($ranked')) { return '候选列表没有排序' }
    if (-not $ps.Contains('.eff }; Descending = $false')) { return '排序不是按有效耗时升序' }
    if (-not $ps.Contains('.ord };')) { return '同耗时没有按原始顺序稳定排列' }
    if (-not $ps.Contains('Set-ViaStat -Tag $tag -Ok $true')) { return '成功没有记账' }
    if (-not $ps.Contains('Set-ViaStat -Tag $tag -Ok $false')) { return '失败没有记账' }
    return $true
}

Check '全量索引也走同一套候选与记账' {
    $ps = Get-Content (Join-Path $Root 'bing-wallpaper.ps1') -Raw
    if (-not $ps.Contains("Invoke-ArchiveFetch -RelPath 'Zhu-junwei/bing-wallpaper-archive/master/Bing_zh-CN_all.json'")) {
        return '全量索引没走统一候选（或 RelPath 少了仓库前缀）'
    }
    if (-not $ps.Contains('-TimeoutSec 40')) { return '全量索引没有放宽超时（1.6 MB 用 8 秒不够）' }
    if (-not $ps.Contains('function Invoke-ArchiveFetch {')) { return '缺 Invoke-ArchiveFetch' }
    $g = [regex]::Match($ps, '(?s)function Invoke-ArchiveFetch.*?function Save-ArchiveVia')
    if (-not $g.Success) { return '找不到 Invoke-ArchiveFetch 主体' }
    if (-not $g.Value.Contains('[int]$TimeoutSec = 8')) { return '超时参数没有默认值' }
    if (-not $g.Value.Contains('Set-ViaStat -Tag $tag')) { return '全量索引路径没有记账' }
    # 没跑过的通道默认值要比实测快的更差，否则未知会压过已知
    if (-not $ps.Contains('$eff = 2500.0')) { return '未知通道的默认基准不是 2500' }
    return $true
}

Check '中文标题映射取值正确，不会写出 System.Collections.Hashtable' {
    $ps = Get-Content (Join-Path $Root 'bing-wallpaper.ps1') -Raw
    # Get-ZhTitleMap 的值是 @{title;copyright}，必须取 .title
    if ($ps -match 'if \(\$zhMap\.ContainsKey\(\$bid\)\) \{ \$zt = \$zhMap\[\$bid\] \}') {
        return 'zhMap 的值是哈希表，直接赋值会变成 System.Collections.Hashtable'
    }
    if (-not $ps.Contains('$zt = [string]$ze.title')) { return '没有从 zhMap 条目里取 .title' }
    # titleEn 不能用 zh-CN 接口的标题冒充
    if ($ps -match 'titleEn   = \[string\]\$hit8\.title') { return 'titleEn 用了中文接口的 title' }
    return $true
}

Check '壁纸路径失效就退回今天，接管下禁用画廊' {
    $r = Get-Content (Join-Path $Root 'src\Refresh.cs') -Raw
    if ($r -notmatch 'internal static void EnsureWallpaperAlive') { return '缺 EnsureWallpaperAlive' }
    $ra = [regex]::Match($r, '(?s)private static void RefreshAll\(\).*?finally \{ refreshing = false; \}')
    if (-not $ra.Success) { return '找不到 RefreshAll' }
    if ($ra.Value -notmatch 'EnsureWallpaperAlive\(\)') { return 'RefreshAll 没有调用自愈' }
    $al = [regex]::Match($r, '(?s)internal static void EnsureWallpaperAlive.*?\n        \}')
    if (-not $al.Success) { return '找不到 EnsureWallpaperAlive 主体' }
    if ($al.Value -notmatch 'File\.Exists\(wp\)') { return '没有检查文件是否存在' }
    if ($al.Value -notmatch '"-Index 0"') { return '失效后没有退回今天' }
    if ($al.Value -notmatch 'TotalMinutes < 10') { return '没有节流，今天取不到图时会反复重试' }

    $ps = Get-Content (Join-Path $Root 'bing-wallpaper.ps1') -Raw
    $rb = [regex]::Match($ps, '(?s)roBlocked = @\(\).*?exit 10')
    if (-not $rb.Success) { return '找不到接管只读拦截' }
    if ($rb.Value -notmatch '\$Gallery\b') { return '接管下没拦 -Gallery' }
    if ($rb.Value -notmatch '\$GalleryClear') { return '接管下没拦 -GalleryClear' }
    $m = Get-Content (Join-Path $Root 'src\Menu.cs') -Raw
    if ($m -notmatch 'GalleryTakeoverBlocked') { return '菜单里没有接管下的画廊拦截' }
    return $true
}

Check '勾选条目立刻刷新按钮状态（不用先点行）' {
    foreach ($f in @('src\Forms\FavForm.cs', 'src\Forms\PinnedForm.cs')) {
        $t = Get-Content (Join-Path $Root $f) -Raw
        # ItemChecked 里必须顺手更新按钮，否则单击勾选框后按钮还是灰的
        if ($t -match 'lst\.ItemChecked \+= delegate \{ RefreshSum\(\); \};') {
            return ($f + ' 的 ItemChecked 只刷了统计，没刷按钮')
        }
        $m = [regex]::Match($t, '(?s)lst\.ItemChecked \+= delegate.*?\};')
        if (-not $m.Success) { return ($f + ' 找不到 ItemChecked 绑定') }
        if ($m.Value -notmatch 'btnRemove\.Enabled') { return ($f + ' 勾选后没更新移出按钮') }
        if ($m.Value -notmatch 'CheckedCount\(\) > 0') { return ($f + ' 按钮状态判断不对') }
    }
    return $true
}

# ---------------------------------------------------------------- 13. 菜单可用性
Write-Host '[13] 菜单可用性' -ForegroundColor Yellow

Check 'SetBusy 不再逐项禁用菜单' {
    $t = Get-Content (Join-Path $Root 'src\Refresh.cs') -Raw
    $m = [regex]::Match($t, '(?s)private static void SetBusy\(bool on\).*?\r?\n        \}')
    if (-not $m.Success) { return 'Refresh.cs 里找不到 SetBusy' }
    if ($m.Value -match 'foreach \(ToolStripItem') { return 'SetBusy 仍在遍历 menu.Items' }
    if ($m.Value -match '\.Enabled = menu\.Visible') { return 'SetBusy 仍在按 menu.Visible 逐项判定' }
    if ($m.Value -notmatch 'ApplyMenuState\(\)') { return 'SetBusy 没走唯一的 ApplyMenuState 出口' }
    return $true
}

Check '菜单打开时重算可用性' {
    $t = Get-Content (Join-Path $Root 'src\Menu.cs') -Raw
    $m = [regex]::Match($t, '(?s)m\.Opening \+= delegate\s*\{.*?\};')
    if (-not $m.Success) { return 'Menu.cs 里找不到 Opening 绑定' }
    if ($m.Value -notmatch 'HealBusyIfStale\(\)') { return 'Opening 没有 busy 自愈' }
    if ($m.Value -notmatch 'ApplyMenuState\(\)') { return 'Opening 没有重算 Enabled' }
    return $true
}

Check '出口项永不禁用' {
    $t = Get-Content (Join-Path $Root 'src\MenuState.cs') -Raw
    $m = [regex]::Match($t, '(?s)MenuAlwaysOn = new string\[\]\s*\{(.*?)\};')
    if (-not $m.Success) { return 'MenuState.cs 里找不到 MenuAlwaysOn' }
    # gallerytoggle 从 v3.6.89 起不在这个名单里：编年史下必须能置灰，
    # 而 AlwaysOn 是「无条件可用」，放进来的东西盖不掉。它改由 ShouldEnable 专门判。
    foreach ($must in @('"exit"', '"lock"', '"rotate"', '"startup"', '"sys"', '"taskstate"')) {
        if ($m.Groups[1].Value -notmatch [regex]::Escape($must)) { return ('MenuAlwaysOn 缺 ' + $must) }
    }
    return $true
}

Check '所有顶级菜单项都有 Name' {
    $t = Get-Content (Join-Path $Root 'src\Menu.cs') -Raw
    if ($t -match 'm\.Items\.Add\(Make(Item|KeepOpenItem)\(') {
        return '仍有匿名顶级项，ApplyMenuState 无法按名判定'
    }
    return $true
}

Check 'MenuGate 初始一律可用' {
    $t = Get-Content (Join-Path $Root 'src\MenuState.cs') -Raw
    $m = [regex]::Match($t, '(?s)internal static MenuGate Ready.*?return g;')
    if (-not $m.Success) { return 'MenuState.cs 里找不到 MenuGate.Ready' }
    if ($m.Value -notmatch 'g\.LocalN = 1') { return 'Ready 的 LocalN 不是 1（初始就把随机换一张灰掉）' }
    if ($m.Value -match 'g\.Busy = true') { return 'Ready 里 Busy 竟然是 true' }
    return $true
}

Check '菜单判定表自测' {
    $exe = Join-Path $Root 'dist\BingWallpaperLite.exe'
    if (-not (Test-Path $exe)) { return 'dist\BingWallpaperLite.exe 不存在，先编译' }
    $out = Join-Path $env:TEMP 'BingWallpaperLite.menugate.txt'
    if (Test-Path $out) { Remove-Item $out -Force -ErrorAction SilentlyContinue }
    $p = Start-Process -FilePath $exe -ArgumentList '--menuselftest' -PassThru -Wait
    if ($p.ExitCode -ne 0) { return ('--menuselftest 退出码 ' + $p.ExitCode) }
    if (-not (Test-Path $out)) { return '--menuselftest 没写结果文件' }
    $line = (Get-Content $out -Raw).Trim()
    if ($line -notmatch '^MENUGATE OK') { return ('自测未通过：' + $line) }
    return $true
}

Check '计划任务是容器不是动作' {
    $t = Get-Content (Join-Path $Root 'src\Menu.cs') -Raw
    if ($t -notmatch 'taskState\.DropDownItems\.Add') { return '计划任务没做成子菜单' }
    if ($t -match 'taskState\.Click \+= ') { return '计划任务仍绑定了点击动作（一点就改系统）' }
    return $true
}

Check '重注册不再擅改开机自启' {
    $t = Get-Content (Join-Path $Root 'src\Refresh.cs') -Raw
    $m = [regex]::Match($t, '(?s)private static void DoRepairTask\(\).*?\n        \}')
    if (-not $m.Success) { return 'Refresh.cs 里找不到 DoRepairTask' }
    if ($m.Value -match 'SetStartup\(true\)') { return 'DoRepairTask 仍在强制打开自启' }
    if ($m.Value -notmatch 'TaskDoneLocked') { return 'DoRepairTask 没有区分暂停状态' }
    return $true
}

Check '改时间会强制重注册' {
    $t = Get-Content (Join-Path $Root 'src\Refresh.cs') -Raw
    $m = [regex]::Match($t, '(?s)internal static void ApplyUpdateTime\(string hm\).*?\n        \}')
    if (-not $m.Success) { return 'Refresh.cs 里找不到 ApplyUpdateTime' }
    if ($m.Value -notmatch 'ReregisterTask\(true\)') { return '改时间没强制重建任务（幂等分支会让触发时刻不变）' }
    return $true
}

Check '自定义时间窗体' {
    $p = Join-Path $Root 'src\Forms\TimeForm.cs'
    if (-not (Test-Path $p)) { return 'TimeForm.cs 不存在' }
    $t = Get-Content $p -Raw
    if ($t -notmatch 'ShowUpDown = true') { return '时间选择器不是上下微调模式' }
    if ($t -notmatch 'HH:mm') { return '时间格式不是 HH:mm' }
    return $true
}

Check '计划任务信息行恒灰' {
    $t = Get-Content (Join-Path $Root 'src\MenuState.cs') -Raw
    $m = [regex]::Match($t, '(?s)MenuInfoOnly = new string\[\]\s*\{(.*?)\};')
    if (-not $m.Success) { return 'MenuState.cs 里找不到 MenuInfoOnly' }
    foreach ($must in @('"taskinfo"', '"taskauto"', '"header"')) {
        if ($m.Groups[1].Value -notmatch [regex]::Escape($must)) { return ('MenuInfoOnly 缺 ' + $must) }
    }
    return $true
}

Check '提示语不再谎称恢复正常' {
    $t = Get-Content (Join-Path $Root 'src\Strings.cs') -Raw
    $m = [regex]::Match($t, 'BRepairedHint\s*\{ get \{ return T\("([^"]*)"')
    if (-not $m.Success) { return 'Strings.cs 里找不到 BRepairedHint' }
    if ($m.Groups[1].Value -match '恢复正常') { return 'BRepairedHint 仍声称「自动换图恢复正常」' }
    return $true
}

Check '搜索并入老存档' {
    $t = Get-Content (Join-Path $Root 'bing-wallpaper.ps1') -Raw
    $m = [regex]::Match($t, '(?s)function Search-Archive.*?\n\}')
    if (-not $m.Success) { return '找不到 Search-Archive' }
    if ($m.Value -notmatch 'Get-OldIndexMap') { return 'Search-Archive 没并入 old-index（2016-03 前的图搜不到）' }
    if ($m.Value -notmatch 'dead') { return 'Search-Archive 没输出 dead 标记' }
    return $true
}

Check '空缺日期在本地判定，不进网络' {
    $t = Get-Content (Join-Path $Root 'bing-wallpaper.ps1') -Raw
    $p1 = $t.IndexOf('Bing 官方未发布该日壁纸')
    $p2 = $t.IndexOf('Write-Log "--- 指定日期')
    if ($p1 -lt 0) { return '没有快速失败分支' }
    if ($p2 -lt 0) { return '找不到指定日期日志' }
    if ($p1 -gt $p2) { return '快速失败不在 -Date 分支开头（会先跑网络）' }
    return $true
}

Check '翻页跳过死日期' {
    $t = Get-Content (Join-Path $Root 'bing-wallpaper.ps1') -Raw
    $m = [regex]::Match($t, '(?s)function Get-NeighborDate.*?\n\}')
    if (-not $m.Success) { return '找不到 Get-NeighborDate' }
    if ($m.Value -notmatch 'Read-DeadDates') { return 'Get-NeighborDate 没过滤死日期' }
    if ($t -notmatch 'function Invoke-NeighborFallback') { return '没有越界接续的预检函数' }
    $m2 = [regex]::Match($t, '(?s)function Invoke-NeighborFallback.*?\n\}')
    if ($m2.Success -and $m2.Value -notmatch 'Test-ArchiveReachable') { return '越界接续没做可达性预检' }
    return $true
}

Check '日历随机抽过滤死日期' {
    $t = Get-Content (Join-Path $Root 'bing-wallpaper.ps1') -Raw
    $m = [regex]::Match($t, '(?s)if \(\$PickRandom -gt 0\).*?\n\}')
    if (-not $m.Success) { return '找不到 PickRandom 分支' }
    if ($m.Value -notmatch 'Read-DeadDates') { return 'PickRandom 没过滤死日期' }
    return $true
}

Check '预取改用索引邻近日' {
    $t = Get-Content (Join-Path $Root 'src\Forms\InfoForm.cs') -Raw
    $m = [regex]::Match($t, '(?s)private void PrefetchAround\(string date\).*?\n        \}')
    if (-not $m.Success) { return '找不到 PrefetchAround' }
    if ($m.Value -notmatch 'Program\.NeighborDate') { return '预取没用索引邻近日' }
    if ($m.Value -match 'AddDays\(') { return '预取仍在用日历 ±1 天（会预取空缺日期，实测白跑 22.8 秒）' }
    return $true
}

Check '程序侧已发布日期索引' {
    $t = Get-Content (Join-Path $Root 'src\Refresh.cs') -Raw
    if ($t -notmatch 'internal static bool IsDatePublished') { return '没有 IsDatePublished' }
    if ($t -notmatch 'internal static string NeighborDate') { return '没有 NeighborDate' }
    if ($t -notmatch 'old-index\.json') { return '索引集合没读 old-index' }
    return $true
}

Check '日历标出官方未发布日' {
    $t = Get-Content (Join-Path $Root 'src\Forms\CalendarForm.cs') -Raw
    if ($t -notmatch 'IsDatePublished') { return '日历没判断官方是否发布' }
    if ($t -notmatch 'SrchNoImage') { return '日历没提示「这一天必应没有发布壁纸」' }
    if ($t -notmatch 'ok\.Enabled = !blocked') { return '没禁用确定按钮' }
    return $true
}

Check '搜索列表标注失效原图' {
    $t = Get-Content (Join-Path $Root 'src\Forms\SearchForm.cs') -Raw
    if ($t -notmatch 'DeadDateSuffix') { return '搜索结果没标注「原图已失效」' }
    if ($t -notmatch 'DictBool') { return '没解析 dead 字段' }
    return $true
}

Check '时间窗跟随鼠标' {
    $t = Get-Content (Join-Path $Root 'src\Forms\TimeForm.cs') -Raw
    if ($t -notmatch 'FormStartPosition\.Manual') { return '不是 Manual 定位' }
    if ($t -notmatch 'WorkingArea') { return '没夹进工作区' }
    if ($t -match 'FormStartPosition\.CenterParent') { return '仍在用 CenterParent（托盘 owner 在 0,0，会贴左上角）' }
    return $true
}

Check '批量下载校验真实宽度' {
    $t = Get-Content (Join-Path $Root 'bing-wallpaper.ps1') -Raw
    $m = [regex]::Match($t, '(?s)if \(\$ExportRange\).*?\n    exit 0\r?\n\}')
    if (-not $m.Success) { return '找不到 ExportRange 分支' }
    if ($m.Value -notmatch 'Get-ImagePixelWidth -Path \$destFile') { return '下载后没量宽度（_UHD 可能返回 1080p）' }
    if ($m.Value -notmatch 'Note-NoUhd') { return '拿不到 UHD 时没记账' }
    if ($m.Value -notmatch '"lowres"') { return '结果 JSON 没 lowres 字段' }
    return $true
}

Check '重查条件用真实宽度' {
    $t = Get-Content (Join-Path $Root 'bing-wallpaper.ps1') -Raw
    if ($t -match '\$had\.Length -lt 3MB') { return '仍用体积 3MB 当前置判断，会漏检' }
    if ($t -notmatch 'Test-NoUhd -Date8 \$cur') { return '重查时没查 no-uhd 名单，会反复白试' }
    return $true
}

Check '只补非 UHD 模式' {
    $t = Get-Content (Join-Path $Root 'bing-wallpaper.ps1') -Raw
    if ($t -notmatch '\[switch\]\$OnlyLowRes') { return '没有 -OnlyLowRes 参数' }
    if ($t -notmatch 'elseif \(\$OnlyLowRes\)') { return '没处理「本地没有就跳过」' }
    return $true
}

Check '画质体检模式' {
    $t = Get-Content (Join-Path $Root 'bing-wallpaper.ps1') -Raw
    if ($t -notmatch '\[switch\]\$AuditQuality') { return '没有 -AuditQuality 参数' }
    if ($t -notmatch '_quality-audit\.csv') { return '没输出体检 CSV' }
    if ($t -notmatch '"lowlist"') { return '没输出低画质清单' }
    return $true
}

Check '下载源与档位落盘' {
    $t = Get-Content (Join-Path $Root 'bing-wallpaper.ps1') -Raw
    if ($t -notmatch '_export-res\.txt') { return 'resLog 没落盘' }
    if ($t -notmatch '_export-lowres\.txt') { return '低画质清单没落盘' }
    return $true
}

Check '下载窗有只补低画质' {
    $t = Get-Content (Join-Path $Root 'src\Forms\DownloadForm.cs') -Raw
    if ($t -notmatch 'OnlyLowRes') { return '下载窗没有 OnlyLowRes 属性' }
    if ($t -notmatch 'DlUhdNote') { return '没有「UHD 仅 2019 起」的说明' }
    return $true
}

Check '照片墙区分官方未发布' {
    $t = Get-Content (Join-Path $Root 'src\Forms\PhotoWallForm.cs') -Raw
    if ($t -notmatch 'IsDatePublished') { return '照片墙没判断官方是否发布' }
    if ($t -notmatch 'card\.OfficialGap') { return '卡片没标记 OfficialGap' }
    if ($t -notmatch 'gapCount\+\+') { return '空缺日没单独计数（会混进 missing）' }
    return $true
}

Check '空缺日不进拉图队列' {
    $t = Get-Content (Join-Path $Root 'src\Forms\PhotoWallForm.cs') -Raw
    $m = [regex]::Match($t, '(?s)private void FetchOne\(WallCard card\).*?\n        \}')
    if (-not $m.Success) { return '找不到 FetchOne' }
    if ($m.Value -notmatch 'OfficialGap') { return '单张拉取没拦截官方未发布日' }
    if ($t -notmatch 'if \(officialGap\) \{ gapCount\+\+; \}') { return '空缺日仍会被塞进 missing' }
    return $true
}

Check '接管目录被当作图库来源' {
    $t = Get-Content (Join-Path $Root 'src\Program.cs') -Raw
    $m = [regex]::Match($t, '(?s)private static bool SourceExists\(string d8\).*?\n        \}')
    if (-not $m.Success) { return '找不到 SourceExists' }
    if ($m.Value -notmatch 'CurrentTakeoverRoot') { return 'SourceExists 不认接管目录，缩略图会被当孤儿删' }
    return $true
}

Check '缩略图配额放大' {
    $t = Get-Content (Join-Path $Root 'src\Program.cs') -Raw
    if ($t -notmatch '"thumbsMaxCount", 2000') { return 'thumbsMaxCount 默认值仍是 800' }
    if ($t -notmatch '"thumbsMaxMB", 200') { return 'thumbsMaxMB 默认值仍是 100' }
    return $true
}

Check '下载器同源重试' {
    $p = Join-Path $Root 'tools\Downloader\Downloader.cs'
    if (-not (Test-Path $p)) { return '找不到 Downloader.cs' }
    $t = Get-Content $p -Raw
    $m = [regex]::Match($t, '(?s)private bool FetchOne\(DayEntry e, string destFile\).*?\n        \}')
    if (-not $m.Success) { return '找不到 FetchOne' }
    if ($m.Value -notmatch 'for \(int attempt = 1; attempt <= maxAttempts') { return 'FetchOne 没有重试循环' }
    if ($m.Value -notmatch 'DlResult\.NotFound\) \{ break; \}') { return '404 没有短路（会白重试）' }
    return $true
}

Check '下载器清半成品' {
    $t = Get-Content (Join-Path $Root 'tools\Downloader\Downloader.cs') -Raw
    $m = [regex]::Match($t, '(?s)private static DlResult DownloadTo.*?\n        \}')
    if (-not $m.Success) { return '找不到 DownloadTo' }
    if ($m.Value -notmatch 'finally') { return 'DownloadTo 没有 finally' }
    if ($m.Value -notmatch 'File\.Exists\(tmp\)') { return '异常路径没清 .part' }
    if ($t -notmatch 'CleanStaleParts') { return '没有启动时清理过期 .part' }
    return $true
}

Check '下载器失败实时落盘' {
    $t = Get-Content (Join-Path $Root 'tools\Downloader\Downloader.cs') -Raw
    if ($t -notmatch 'PersistFail') { return '没有实时写失败清单' }
    if ($t -notmatch 'File\.AppendAllText') { return '失败清单不是追加写' }
    return $true
}

Check '下载器校验画质' {
    $t = Get-Content (Join-Path $Root 'tools\Downloader\Downloader.cs') -Raw
    if ($t -notmatch 'CheckQuality') { return '没有画质校验' }
    if ($t -notmatch 'w >= 3000') { return '宽度阈值不是 3000' }
    if ($t -notmatch '_lowres\.txt') { return '没有输出无 UHD 清单' }
    return $true
}

Check '下载器可调并发' {
    $t = Get-Content (Join-Path $Root 'tools\Downloader\MainForm.cs') -Raw
    if ($t -notmatch 'cmbThreads') { return '界面没有并发送择' }
    if ($t -notmatch 'int\.Parse\(cmbThreads\.Text\)') { return '并发值没传到 Run' }
    return $true
}

Check '下载器有看门狗' {
    $t = Get-Content (Join-Path $Root 'tools\Downloader\Downloader.cs') -Raw
    if ($t -notmatch 'req\.Abort\(\)') { return '没有 Abort 兜底（半开连接会让线程一直挂着）' }
    if ($t -notmatch 'Timer dog') { return '没有看门狗定时器' }
    if ($t -notmatch '单日超时') { return '没有单日总预算' }
    return $true
}

Check '下载器有 cdn.bimg.cc 候选' {
    $t = Get-Content (Join-Path $Root 'tools\Downloader\Downloader.cs') -Raw
    if ($t -notmatch 'cdn\.bimg\.cc') { return '没有 cdn.bimg.cc 候选（2016-2019 会全失败）' }
    $p1 = $t.IndexOf('cdn.bimg.cc')
    $p2 = $t.IndexOf('img.wilii.cn')
    if ($p1 -lt 0) { return '找不到 cdn.bimg.cc' }
    if ($p2 -gt 0 -and $p1 -gt $p2) { return 'cdn.bimg.cc 排在 wilii 之后，应放在 bing 之后、wilii 之前' }
    return $true
}

Check '补画质也走 cdn.bimg.cc' {
    $t = Get-Content (Join-Path $Root 'bing-wallpaper.ps1') -Raw
    $m = [regex]::Match($t, '(?s)if \(\$ExportRange\).*?\n    exit 0\r?\n\}')
    if (-not $m.Success) { return '找不到 ExportRange 分支' }
    if ($m.Value -notmatch 'cdn\.bimg\.cc') { return 'ExportRange 没接 cdn.bimg.cc（2016-2019 补不动）' }
    if ($m.Value -notmatch "'bimg'") { return '没有 bimg 源分支' }
    return $true
}

Check '体检单列损坏文件' {
    $t = Get-Content (Join-Path $Root 'bing-wallpaper.ps1') -Raw
    if ($t -notmatch '"badlist"') { return '体检 JSON 没有 badlist' }
    if ($t -notmatch '\[void\]\$badListA\.Add') { return '读不出宽度的没收集进 badlist' }
    return $true
}

Check '只补低画质覆盖坏文件' {
    $t = Get-Content (Join-Path $Root 'bing-wallpaper.ps1') -Raw
    if ($t -notmatch '\$wChk -le 0') { return '读不出宽度的文件仍会被放过' }
    return $true
}

Check '清空缓存含照片墙缩略图' {
    $t = Get-Content (Join-Path $Root 'bing-wallpaper.ps1') -Raw
    $m = [regex]::Match($t, '(?s)function Clear-AllCache.*?\n    return \$removed')
    if (-not $m.Success) { return '找不到 Clear-AllCache' }
    if ($m.Value -notmatch "Join-Path \`$CacheDir 'thumbs'") { return '清空缓存没清照片墙缩略图' }
    if ($m.Value -match 'cache\\\\wall') { return '不该动 cache\\wall（那有关窗询问语义）' }
    return $true
}

Check '清空缓存文案说清范围' {
    $t = Get-Content (Join-Path $Root 'src\Strings.cs') -Raw
    $m = [regex]::Match($t, 'ConfirmCleanAll\s*\{ get \{ return T\("([^"]*)"')
    if (-not $m.Success) { return '找不到 ConfirmCleanAll' }
    if ($m.Groups[1].Value -notmatch '缩略图') { return '确认框没提缩略图' }
    if ($m.Groups[1].Value -match '所有图片') { return '仍写「所有图片」，与行为不符' }
    return $true
}

Check '单日取图也走 cdn.bimg.cc' {
    $t = Get-Content (Join-Path $Root 'bing-wallpaper.ps1') -Raw
    $m = [regex]::Match($t, '(?s)归档原图 1080p.*?if \(\$rec\.url\)')
    if (-not $m.Success) { return '找不到归档候选段' }
    if ($m.Value -notmatch 'cdn\.bimg\.cc') { return '归档候选没接 cdn.bimg.cc（2016-2019 单日取图会失败）' }
    return $true
}

Check '批量任务不吃交互预算' {
    $t = Get-Content (Join-Path $Root 'bing-wallpaper.ps1') -Raw
    if ($t -notmatch '\$ExportRange -or \$FillLibrary\) \{ \$BudgetSec = 7200 \}') {
        return 'ExportRange/FillLibrary 仍沿用交互的 100 秒预算，批量会假失败'
    }
    return $true
}

Check '升级失败不删原文件' {
    $t = Get-Content (Join-Path $Root 'bing-wallpaper.ps1') -Raw
    if ($t -notmatch 'if \(-not \$upRange -and \(Test-Path \$destFile\)\)') {
        return '升级失败仍会删掉原有文件（实测丢过 113 张）'
    }
    return $true
}

Check '补画质有 cn.bing.com 4K 源' {
    $t = Get-Content (Join-Path $Root 'bing-wallpaper.ps1') -Raw
    if ($t -notmatch "cn\.bing\.com/th\?id=") { return '没有 cn.bing.com 的 4K 候选' }
    if ($t -notmatch 'w=3840&h=2160') { return '缺少 w/h 缩放参数' }
    return $true
}

Check '下载器也有 cn.bing.com 4K' {
    $t = Get-Content (Join-Path $Root 'tools\Downloader\Downloader.cs') -Raw
    if ($t -notmatch 'cn\.bing\.com') { return '下载器没有 cn.bing.com 候选' }
    return $true
}

Check 'bingWh 候选不再是死链' {
    $t = Get-Content (Join-Path $Root 'bing-wallpaper.ps1') -Raw
    $m = [regex]::Match($t, '\$bingWh\s*=\s*"([^"]*)"')
    if (-not $m.Success) { return '找不到 $bingWh' }
    if ($m.Groups[1].Value -notmatch '_UHD\.jpg') { return '$bingWh 缺 _UHD.jpg 后缀（实测每次 404）' }
    if ($m.Groups[1].Value -notmatch 'w=3840') { return '$bingWh 缺 w/h 参数' }
    return $true
}

Check '日常候选链含跨域名 4K 备份' {
    $t = Get-Content (Join-Path $Root 'bing-wallpaper.ps1') -Raw
    if ($t -notmatch '\$bingCn4k') { return '没有 cn.bing.com 的 4K 备份候选' }
    if ($t -notmatch '\$candidates = @\(\$bingWh, \$bingCn4k, \$bingUhd') { return '默认候选链没把 www 的 4K 排到最前' }
    return $true
}

Check 'cdn 源取到 1080p 会继续试 4K' {
    $t = Get-Content (Join-Path $Root 'bing-wallpaper.ps1') -Raw
    $m = [regex]::Match($t, '(?s)if \(\$src -eq ''cdn''\).*?elseif \(\$src -eq ''cn4k''\)')
    if (-not $m.Success) { return '找不到 cdn 分支' }
    if ($m.Value -notmatch '\$cdnLow = \$true') { return 'cdn 拿到低画质仍会收工，cn4k 轮不到' }
    if ($m.Value -notmatch 'if \(\$cdnLow\) \{ continue \}') { return '没有 continue 到下一个源' }
    return $true
}

Check '有统一的画质验收函数' {
    $t = Get-Content (Join-Path $Root 'bing-wallpaper.ps1') -Raw
    if ($t -notmatch 'function Save-BingImageAtLeast') { return '没有 Save-BingImageAtLeast' }
    $m = [regex]::Match($t, '(?s)function Save-BingImageAtLeast.*?\n\}')
    if ($m.Value -notmatch 'MinWidth') { return '验收函数没有 MinWidth 参数' }
    if ($m.Value -notmatch 'Get-ImagePixelWidth') { return '验收函数没量宽度' }
    return $true
}

Check '日常取图拿到低画质会继续找' {
    $t = Get-Content (Join-Path $Root 'bing-wallpaper.ps1') -Raw
    $m = [regex]::Match($t, '(?s)if \(\$needDownload\).*?所有候选地址都下载失败')
    if (-not $m.Success) { return '找不到日常下载段' }
    if ($m.Value -notmatch 'Save-BingImageAtLeast') { return '日常取图没用验收函数' }
    if ($m.Value -notmatch '\$lowKept') { return '没有低画质回退标记' }
    return $true
}

Check '归档候选 cn4k 排在 1080p 之前' {
    $t = Get-Content (Join-Path $Root 'bing-wallpaper.ps1') -Raw
    $i1 = $t.IndexOf("tag = 'cn4k 3840'")
    $i2 = $t.IndexOf("tag = '归档原图 1080p'")
    if ($i1 -lt 0) { return '没有 cn4k 候选' }
    if ($i2 -lt 0) { return '没有归档原图 1080p 候选' }
    if ($i1 -gt $i2) { return 'cn4k 排在 1080p 之后，4K 永远轮不到' }
    return $true
}

Check '图库补齐也验画质且带 4K 候选' {
    $t = Get-Content (Join-Path $Root 'bing-wallpaper.ps1') -Raw
    $m = [regex]::Match($t, '(?s)\$candsF = @\(.*?\$doneF = \$true')
    if (-not $m.Success) { return '找不到补齐候选段' }
    if ($m.Value -notmatch 'cn\.bing\.com') { return '补齐没有 cn 的 4K 候选' }
    if ($m.Value -notmatch 'Save-BingImageAtLeast') { return '补齐没用验收函数' }
    return $true
}
Check '英文版已停用（界面只走中文）' {
    $s = Get-Content (Join-Path $Root 'src\Strings.cs') -Raw
    if ($s -notmatch 'Init\(bool en\) \{ _en = false; \}') { return 'Init 没把 _en 钉成 false' }
    $p = Get-Content (Join-Path $Root 'src\Program.cs') -Raw
    if ($p -notmatch 'S\.Init\(false\)') { return 'Program 没直接传 false' }
    if ($p -match 'c0\["uiLanguage"\]') { return 'Program 仍在读 uiLanguage 决定语言' }
    return $true
}

Check '自动清理文案不再写死 30 天' {
    $s = Get-Content (Join-Path $Root 'src\Strings.cs') -Raw
    if ($s -notmatch 'KeepDaysN') { return '没有 KeepDaysN 动态天数' }
    foreach ($k in @('PinNote', 'FavIntro', 'FavConfirm', 'AboutFeat3')) {
        $m = [regex]::Match($s, 'public static string ' + $k + '[^\r\n]*')
        if (-not $m.Success) { return ('找不到 ' + $k) }
        if ($m.Value -match '30 天') { return ($k + ' 仍写着 30 天') }
        if ($m.Value -notmatch 'KeepDaysN') { return ($k + ' 没用动态天数') }
    }
    return $true
}

Check '清理缓存三项合并为「立即清理缓存…」' {
    $m = Get-Content (Join-Path $Root 'src\Menu.cs') -Raw
    if ($m -match 'S\.Clean7') { return '菜单还在用「保留 7 天」' }
    if ($m -match 'S\.Clean30') { return '菜单还在用「保留 30 天」' }
    if ($m -match 'S\.CleanAll') { return '菜单还在用「清空缓存」' }
    if ($m -notmatch 'S\.CleanNow') { return '菜单没挂「立即清理缓存」' }
    if ($m -notmatch '"-Clean -Keep " \+ cf\.KeepDays') { return '天数没传给 -Keep' }
    return $true
}

Check '术语里不再有归档/老存档/保护名单这类说法' {
    $s = Get-Content (Join-Path $Root 'src\Strings.cs') -Raw
    foreach ($k in @('历史归档', '老存档', '增强数据源', '换过的图', '第三方归档', '保护名单', '长期保留', '我存下的图', '我手动换过的', '最近下载的图', '我选的壁纸')) {
        if ($s -match $k) { return ('Strings.cs 里还有旧说法：' + $k) }
    }
    if ($s -notmatch '以前换过的壁纸') { return '没有「以前换过的壁纸」这个新说法' }
    if ($s -notmatch '2016 年前的老图') { return '没有「2016 年前的老图」这个新说法' }
    if ($s -notmatch '自动换的壁纸') { return '没有「自动换的壁纸」这个新说法' }
    if ($s -notmatch '换图记录') { return '没有「换图记录」这个新说法' }
    return $true
}

Check '清理菜单合并成三项' {
    $m = Get-Content (Join-Path $Root 'src\Menu.cs') -Raw
    if ($m -notmatch 'S\.CleanNow') { return '没有「立即清理」' }
    if ($m -match 'S\.CleanArchive\b') { return '「清理长期保留」还单独挂着，应该并进立即清理' }
    if ($m -match 'S\.CleanFavMenu') { return '清理收藏还单独挂着，已并入立即清理' }
    if ($m -notmatch '"cleannow"') { return '立即清理项没有 Name' }
    return $true
}

Check '立即清理弹窗有六项勾选' {
    $f = Get-Content (Join-Path $Root 'src\Forms\CleanCacheForm.cs') -Raw
    foreach ($k in @('cache', 'thumbs', 'wall', 'stories', 'archive', 'old')) {
        if ($f -notmatch ('"' + $k + '"')) { return ('缺勾选项 ' + $k) }
    }
    if ($f -notmatch 'public string Items') { return '没有 Items 结果属性' }
    if ($f -notmatch 'archInfo') { return '没把长期保留的张数带进来' }
    return $true
}

Check '脚本 -Clean 认 -CleanItems 且不接整目录项' {
    $t = Get-Content (Join-Path $Root 'bing-wallpaper.ps1') -Raw
    if ($t -notmatch '\[string\]\$CleanItems') { return '没有 -CleanItems 参数' }
    if ($t -notmatch "'cache', 'thumbs', 'wall', 'stories'") { return '没把整目录清空的两项排除掉' }
    if ($t -notmatch 'Clear-AutoCache -Days \$keepArg -Items \$itemsArg') { return '-Clean 没走统一入口' }
    return $true
}

Check '任务指向判定认 exe --run 形态' {
    $t = Get-Content (Join-Path $Root 'src\Install.cs') -Raw
    $m = [regex]::Match($t, '(?s)private static bool IsTaskPointingHere\(\).*?catch \{ return false; \}')
    if (-not $m.Success) { return '找不到 IsTaskPointingHere' }
    if ($m.Value -notmatch 'Application\.ExecutablePath') { return '只认 ps1，任务实际是 exe --run，会误报修复失败' }
    if ($m.Value -notmatch 'scriptPath') { return '不认老版本的 ps1 形态了' }
    return $true
}

Check '清理缓存弹窗只收天数且夹工作区' {
    $f = Get-Content (Join-Path $Root 'src\Forms\CleanCacheForm.cs') -Raw
    if ($f -notmatch 'NumericUpDown') { return '没有天数输入框' }
    if ($f -notmatch 'public int KeepDays') { return '没有 KeepDays 结果属性' }
    if ($f -notmatch 'Minimum = 1') { return '天数下限不是 1' }
    if ($f -notmatch 'WorkingArea') { return '没夹工作区，会贴到屏幕角落' }
    return $true
}

Check '自动清理已改名（不再叫自动清理缓存）' {
    $s = Get-Content (Join-Path $Root 'src\Strings.cs') -Raw
    if ($s -notmatch '☑ 自动清理"') { return '菜单项没改成「自动清理」' }
    if ($s -match '自动清理缓存"') { return '旧文案「自动清理缓存」还在' }
    return $true
}

Check '清理菜单有自动清理设置入口' {
    $m = Get-Content (Join-Path $Root 'src\Menu.cs') -Raw
    if ($m -notmatch 'S\.AutoCleanSetMenu') { return '菜单没挂设置项' }
    if ($m -notmatch '"autocleanset"') { return '设置项没有 Name' }
    if ($m -notmatch 'AutoCleanForm') { return '没有打开设置窗口' }
    if ($m -notmatch '"autoCleanItems"') { return '窗口结果没写回 autoCleanItems' }
    return $true
}

Check '快速换图开关在开机启动上方且默认开' {
    $m = Get-Content (Join-Path $Root 'src\Menu.cs') -Raw
    $iFast = $m.IndexOf('"fastswitch"')
    $iStart = $m.IndexOf('"startup"')
    if ($iFast -lt 0) { return '没有 fastswitch 菜单项' }
    if ($iStart -lt 0) { return '没有 startup 菜单项' }
    if ($iFast -gt $iStart) { return '快速换图没排在开机启动之前' }
    $ps = Get-Content (Join-Path $Root 'bing-wallpaper.ps1') -Raw
    if ($ps -notmatch 'fastSwitch\s*=\s*\$true') { return '默认值不是 true' }
    if ($m -notmatch 'GetCfgBool\(c, "fastSwitch", true\)') { return '点击时没用默认 true' }
    return $true
}

Check '后台补图走专用入口，不碰落点逻辑' {
    $t = Get-Content (Join-Path $Root 'bing-wallpaper.ps1') -Raw
    if ($t -notmatch '\[string\]\$UpgradeFile') { return '没有 -UpgradeFile 参数' }
    $m = [regex]::Match($t, '(?s)if \(\$UpgradeFile\) \{.*?Exit-Run 7')
    if (-not $m.Success) { return '找不到补图分支' }
    if ($m.Value -match 'Get-DownloadDir|Add-Pinned|Write-State') { return '补图分支碰了落点或登记，画廊模式下会出错' }
    if ($m.Value -notmatch 'Set-DesktopWallpaper') { return '补完没重设壁纸，桌面不会变清晰' }
    return $true
}

Check '日期切换有快速分支且排除了画廊/预取/照片墙' {
    $t = Get-Content (Join-Path $Root 'bing-wallpaper.ps1') -Raw
    if ($t -notmatch '\$fastHit = \$true') { return '没有快速换图分支' }
    $m = [regex]::Match($t, '(?s)\$fastHit = \$false.*?\$fastHit = \$true')
    if (-not $m.Success) { return '找不到快速分支' }
    foreach ($k in @('-not $Gallery', '-not $script:NoApply', '-not $script:WallMode', '-not $script:Takeover')) {
        if ($m.Value -notmatch [regex]::Escape($k)) { return ('快速分支没排除 ' + $k) }
    }
    if ($m.Value -notmatch '_1920x1080') { return '快速分支没先取 1080p' }
    return $true
}

Check '容量上限可调且文案动态' {
    $s = Get-Content (Join-Path $Root 'src\Strings.cs') -Raw
    if ($s -notmatch 'private static int ArchCapMB') { return '没有 ArchCapMB' }
    if ($s -notmatch 'private static string CapText') { return '没有 CapText' }
    if ($s -match '上限 500 MB') { return '还有写死的「上限 500 MB」' }
    $f = Get-Content (Join-Path $Root 'src\Forms\AutoCleanForm.cs') -Raw
    if ($f -notmatch 'public int CapMB') { return '弹窗没有 CapMB' }
    if ($f -notmatch 'cap\.Minimum = 100') { return '容量上限控件缺失' }
    $m = Get-Content (Join-Path $Root 'src\Menu.cs') -Raw
    if ($m -notmatch 'SetCfg\(c2, "archiveMaxMB", f\.CapMB\)') { return '弹窗结果没写回 archiveMaxMB' }
    return $true
}

Check '自动清理设置窗口含天数与五个清理项目' {
    $f = Get-Content (Join-Path $Root 'src\Forms\AutoCleanForm.cs') -Raw
    if ($f -notmatch 'NumericUpDown') { return '没有天数输入框' }
    if ($f -notmatch 'KeepDays') { return '没有 KeepDays 结果属性' }
    if ($f -notmatch 'public string Items') { return '没有 Items 结果属性' }
    foreach ($k in @('cache', 'thumbs', 'wall', 'stories', 'archive')) {
        if ($f -notmatch ('"' + $k + '"')) { return ('缺少清理项目 ' + $k) }
    }
    if ($f -notmatch 'WorkingArea') { return '没夹工作区，会贴到屏幕角落' }
    return $true
}

Check '脚本有自动清理统一入口与按天清理函数' {
    $t = Get-Content (Join-Path $Root 'bing-wallpaper.ps1') -Raw
    foreach ($fn in @('Clear-AutoCache', 'Clear-OldCacheThumbs', 'Clear-OldCacheWall')) {
        if ($t -notmatch ('function ' + $fn)) { return ('缺少函数 ' + $fn) }
    }
    if ($t.IndexOf("Clear-StoryCache -Mode 'recycle'") -lt 0) { return '故事缓存没走回收站' }
    return $true
}

Check '自动清理调用点全部走 Clear-AutoCache' {
    $t = Get-Content (Join-Path $Root 'bing-wallpaper.ps1') -Raw
    $d = [char]36
    $newN = ([regex]::Matches($t, 'Clear-AutoCache -Days \$keepDays')).Count
    $oldN = ([regex]::Matches($t, 'Clear-OldCache -Days \$keepDays')).Count
    if ($newN -lt 6) { return ('只有 ' + $newN + ' 处走新入口，应为 7 处') }
    if ($oldN -ne 0) { return ('还有 ' + $oldN + ' 处绕过新入口直接调 Clear-OldCache') }
    return $true
}

Check 'autoCleanItems 有默认值且缺配置时兜底' {
    $t = Get-Content (Join-Path $Root 'bing-wallpaper.ps1') -Raw
    if ($t.IndexOf("autoCleanItems = 'cache,archive'") -lt 0) { return '配置默认值没加 autoCleanItems' }
    if ($t.IndexOf('if (-not $autoCleanItems)') -lt 0) { return '没有缺省兜底' }
    return $true
}

Check '选源按日期区间分段（实测矩阵）' {
    $t = Get-Content (Join-Path $Root 'bing-wallpaper.ps1') -Raw
    $d = [char]36
    if ($t.IndexOf("elseif (" + $d + "cur -ge '20190601') { " + $d + "order = @('www4k', 'cn4k'") -lt 0) { return '2019-06 起没把 www4k 放首选' }
    if ($t.IndexOf("elseif (" + $d + "cur -ge '20160301') { " + $d + "order = @('bimg'") -lt 0) { return '2016-03 起没把 bimg 放首选' }
    return $true
}

Check '日常取图 4K 带参数的排在裸链前' {
    $t = Get-Content (Join-Path $Root 'bing-wallpaper.ps1') -Raw
    $ms = [regex]::Matches($t, 'candidates = @\(([^)]*bingCn4k[^)]*)\)')
    if ($ms.Count -eq 0) { return '默认候选链没插入 cn 4K' }
    $lst = $ms[0].Groups[1].Value
    $d = [char]36
    $iCn = $lst.IndexOf($d + 'bingCn4k')
    $iBare = $lst.IndexOf($d + 'bingUhd')
    if ($iCn -lt 0 -or $iBare -lt 0) { return ('默认候选链缺 4K 或裸链：' + $lst) }
    if ($iCn -gt $iBare) { return '裸 _UHD.jpg 仍排在带参数的 4K 之前' }
    return $true
}

Check '接管下快速开关置灰并如实换文案' {
    $r = Get-Content (Join-Path $Root 'src\Refresh.cs') -Raw
    if ($r -notmatch 'if \(TakeoverActive\(\)\)') { return '刷新时没判接管' }
    if ($r.IndexOf('S.FastSwitchTkOff') -lt 0) { return '接管下仍显示原勾选文案' }
    if ($r.IndexOf('S.FastSwitchTkTip') -lt 0) { return '接管下没有对应提示' }
    $s = Get-Content (Join-Path $Root 'src\MenuState.cs') -Raw
    if ($s.IndexOf('"fastswitch"') -lt 0) { return 'gate 里没有 fastswitch' }
    if ($s.IndexOf('return !g.Takeover;') -lt 0) { return 'gate 没按接管置灰' }
    return $true
}

Check '接管下补图不把原图当重复副本丢掉' {
    $t = Get-Content (Join-Path $Root 'bing-wallpaper.ps1') -Raw
    $m = [regex]::Match($t, '(?s)if \(\$UpgradeFile\) \{.*?Exit-Run 7')
    if (-not $m.Success) { return '找不到补图分支' }
    if ($m.Value.IndexOf('$script:UpgradeDest = $ufPath') -lt 0) { return '补图没设 UpgradeDest，接管下会丢弃刚下好的原图' }
    return $true
}

Check '照片墙保留下失败要说真话' {
    $w = Get-Content (Join-Path $Root 'src\Forms\PhotoWallForm.cs') -Raw
    if ($w.IndexOf('int rcKeep = Program.RunScriptExit("-WallKeep")') -lt 0) { return '返回值仍被丢弃' }
    if ($w.IndexOf('bool okKeep = (rcKeep == 0)') -lt 0) { return '没判退出码' }
    if ($w.IndexOf('S.WallKeepBlocked') -lt 0) { return '失败时仍弹成功文案' }
    return $true
}

Check '接管只读语义未被放松' {
    $t = Get-Content (Join-Path $Root 'bing-wallpaper.ps1') -Raw
    $m = [regex]::Match($t, '(?s)if \(\$script:Takeover -and -not \$DryRun\) \{.*?exit 10')
    if (-not $m.Success) { return '找不到只读拦截块' }
    foreach ($k in @('-Clean', '-CleanAll', '-CleanArchive', '-CleanFavorites', '-WallKeep', '-Gallery')) {
        if ($m.Value.IndexOf("`$roBlocked += '$k'") -lt 0) { return ('只读拦截少了 ' + $k) }
    }
    if ($t.IndexOf('if ($script:Takeover) { return 0 }') -lt 0) { return '容量上限清理没在接管下短路' }
    return $true
}

Check '日更快速模式同样排除四种场景' {
    $t = Get-Content (Join-Path $Root 'bing-wallpaper.ps1') -Raw
    $i = $t.IndexOf('$fastMode = (')
    if ($i -lt 0) { return '没有日更快速模式判定' }
    $j = $t.IndexOf(')', $i)
    if ($j -le $i) { return '判定表达式不完整' }
    $seg = $t.Substring($i, $j - $i)
    foreach ($k in @('-not $Gallery', '-not $script:NoApply', '-not $script:WallMode', '-not $script:Takeover')) {
        if ($seg.IndexOf($k) -lt 0) { return ('日更快速模式没排除 ' + $k) }
    }
    return $true
}

Check '照片墙认接管图库（Active 路径）' {
    $w = Get-Content (Join-Path $Root 'src\Forms\PhotoWallForm.cs') -Raw
    if ($w.IndexOf('Program.ActiveArchiveImgPath') -lt 0) { return '照片墙还在用静态 ArchiveImgPath，接管下看不见图库' }
    if ($w -match 'FavDirPath, Program\.CacheDirPath') { return 'FindLocal 还在用静态 FavDir/CacheDir' }
    return $true
}

Check '编年史模式可点开文件夹' {
    $m = Get-Content (Join-Path $Root 'src\Menu.cs') -Raw
    if ($m -notmatch 'modeState\.Click \+= delegate') { return 'modestate 没绑点击' }
    if ($m.IndexOf('Program.OpenTakeoverRoot') -lt 0) { return '没调打开目录的方法' }
    if ($m -match 'modeState\.Enabled = false') { return 'modestate 还被写死禁用' }
    $s = Get-Content (Join-Path $Root 'src\MenuState.cs') -Raw
    $i = $s.IndexOf('MenuInfoOnly = new string[]')
    $seg = $s.Substring($i, 200)
    if ($seg.IndexOf('modestate') -ge 0) { return 'modestate 仍在 MenuInfoOnly 里，会恒灰' }
    if ($s.IndexOf('return g.Takeover;') -lt 0) { return 'gate 没按接管放行 modestate' }
    return $true
}

Check '接管窗口能刷新并带统计时间' {
    $t2 = Get-Content (Join-Path $Root 'src\Forms\TakeoverForm.cs') -Raw
    if ($t2.IndexOf('btnRefresh') -lt 0) { return '没有刷新按钮' }
    if ($t2.IndexOf('S.TkRefresh') -lt 0) { return '刷新按钮没文案' }
    if ($t2.IndexOf('S.TkStatAt') -lt 0) { return '状态行没带统计时间' }
    return $true
}

Check '接管窗口说明提到索引文件' {
    $s = Get-Content (Join-Path $Root 'src\Strings.cs') -Raw
    if ($s.IndexOf('_bing-chronicle-index.json') -lt 0) { return 'TkTip 没说明会往图库写索引文件' }
    return $true
}

Check '显示全部图片分页渲染' {
    $w = Get-Content (Join-Path $Root 'src\Forms\PhotoWallForm.cs') -Raw
    if ($w.IndexOf('private const int PageSize = 120;') -lt 0) { return '没有 PageSize' }
    if ($w.IndexOf('ToggleShowAll') -lt 0) { return '没有切换全部/默认' }
    if ($w.IndexOf('ScanLibraryDates') -lt 0) { return '没有扫图库日期' }
    if ($w.IndexOf('GetRange(skip') -lt 0) { return 'LoadData 没做分页切片' }
    if ($w.IndexOf('S.WallFetchMissing') -lt 0) { return '非接管分支的按钮文案被抹掉了' }
    return $true
}

Check '图库新鲜度出现在状态输出里' {
    $r = Run-Script @('-Status', '-Json')
    $j = $r.Output | ConvertFrom-Json
    if (-not $j.library) { return '状态输出没有 library 段' }
    $latest = [string]$j.library.latest
    if ($latest -notmatch '^\d{8}$') { return ('library.latest 不是 8 位日期: [' + $latest + ']') }
    $stale = $j.library.staleDays
    if ($null -eq $stale) { return '没有 staleDays' }
    if ([int]$stale -lt 0) { return 'staleDays 为负，说明没算出来' }
    return $true
}

Check '接管窗口统计带最新一天' {
    $f = Get-Content (Join-Path $Root 'src\Forms\TakeoverForm.cs') -Raw
    if ($f.IndexOf('S.TkOnBody(root, n, days, latest, stale)') -lt 0) { return 'TakeoverForm 没传最新日期' }
    $s = Get-Content (Join-Path $Root 'src\Strings.cs') -Raw
    if ($s.IndexOf('TkOnBody(string root, int n, int days, string latest, int staleDays)') -lt 0) { return 'TkOnBody 签名没扩展' }
    if ($s.IndexOf('最新 {2}') -lt 0) { return '文案里没有最新一天' }
    return $true
}

Check '菜单能标出图库停摆' {
    $s = Get-Content (Join-Path $Root 'src\Strings.cs') -Raw
    if ($s.IndexOf('LibStaleN') -lt 0) { return '没有停摆警告文案' }
    $r2 = Get-Content (Join-Path $Root 'src\Refresh.cs') -Raw
    if ($r2.IndexOf('S.LibStaleN(gapS)') -lt 0) { return '菜单没插告警行' }
    if ($r2.IndexOf('ActiveArchiveImgPath') -lt 0) { return '告警行没认接管图库' }
    if ($r2.IndexOf('gapS > 2') -lt 0) { return '没有 2 天阈值' }
    return $true
}

Check '计划任务入口写审计日志' {
    $p = Get-Content (Join-Path $Root 'src\Program.cs') -Raw
    if ($p.IndexOf('BingWallpaperLite.task.log') -lt 0) { return '没有审计日志文件' }
    if ($p.IndexOf('private static void TaskAudit(') -lt 0) { return '没有 TaskAudit 方法' }
    $m = [regex]::Match($p, '(?s)if \(arg0 == "--run".*?Environment\.Exit\(rc\)')
    if (-not $m.Success) { return '找不到 --run 分支' }
    if ($m.Value.IndexOf('TaskAudit("start"') -lt 0) { return '--run 没写 start' }
    if ($m.Value.IndexOf('TaskAudit("end"') -lt 0) { return '--run 没写 end' }
    if ($m.Value.IndexOf('rc=') -lt 0) { return 'end 行没记退出码' }
    return $true
}

Check '审计日志有体积上限' {
    $p = Get-Content (Join-Path $Root 'src\Program.cs') -Raw
    if ($p.IndexOf('TaskAuditTrim') -lt 0) { return '没有裁剪方法' }
    if ($p.IndexOf('256 * 1024') -lt 0) { return '没有 256 KB 阈值' }
    if ($p.IndexOf('64 * 1024') -lt 0) { return '没有 64 KB 保留量' }
    return $true
}

Check '修复任务幂等，不清空运行历史' {
    $r = Get-Content (Join-Path $Root 'src\Refresh.cs') -Raw
    if ($r.IndexOf('ReregisterTask(bool force)') -lt 0) { return '签名没加 force' }
    $m = [regex]::Match($r, '(?s)private static bool ReregisterTask\(bool force\).*?\n        \}')
    if (-not $m.Success) { return '找不到方法体' }
    $iShort = $m.Value.IndexOf('if (!force)')
    $iDel = $m.Value.IndexOf('/delete /tn')
    if ($iShort -lt 0) { return '没有 force 短路' }
    if ($iDel -lt 0) { return '没有 delete 调用' }
    if ($iShort -gt $iDel) { return 'force 短路排在 delete 之后，等于没生效' }
    if ($r.IndexOf('ReregisterTask(false)') -lt 0) { return '修复入口没走幂等分支' }
    if ($r.IndexOf('ReregisterTask(true)') -lt 0) { return '改时间入口没强制重建' }
    return $true
}

Check '四个共享状态文件都有接管版本' {
    $p = Get-Content (Join-Path $Root 'src\Program.cs') -Raw
    foreach ($k in @('StatePathNow', 'CacheIndexPathNow', 'LockPathNow', 'FavIndexPathNow')) {
        if ($p.IndexOf('internal static string ' + $k) -lt 0) { return ('缺 ' + $k) }
    }
    if ($p.IndexOf('takeover\state.json') -lt 0 -and $p.IndexOf('"state.json"') -lt 0) { return 'StatePathNow 没指向接管沙盒' }
    if ($p.IndexOf('cache-index.json') -lt 0) { return 'CacheIndexPathNow 没指向 cache-index.json' }
    if ($p.IndexOf('takeoverCached') -lt 0) { return '没有缓存变量' }
    if ($p.IndexOf('GetLastWriteTimeUtc') -lt 0) { return '缓存没按 mtime 失效' }
    return $true
}

Check 'UI 侧不再直接读裸静态路径' {
    $files = @('Program.cs', 'Refresh.cs', 'Script.cs', 'Tray.cs', 'Info.cs', 'MenuState.cs')
    $bad = @()
    foreach ($f in $files) {
        $path = Join-Path $Root ('src\' + $f)
        if (-not (Test-Path $path)) { continue }
        $c = Get-Content $path -Raw
        foreach ($pat in @('ReadJson(statePath)', 'ReadJson(indexPath)', 'ReadJson(favIndexPath)', 'File.Exists(lockPath)', 'File.Exists(statePath)')) {
            if ($c.IndexOf($pat) -ge 0) { $bad += ($f + ':' + $pat) }
        }
    }
    if ($bad.Count -gt 0) { return ('还在直接用裸路径：' + ($bad -join ', ')) }
    return $true
}

Check '回到今天在边界时给反馈' {
    $s = Get-Content (Join-Path $Root 'src\Script.cs') -Raw
    if ($s.IndexOf('code == 4') -lt 0) { return '没有 code==4 分支' }
    if ($s.IndexOf('S.AlreadyToday') -lt 0) { return '没有 AlreadyToday 提示' }
    $st = Get-Content (Join-Path $Root 'src\Strings.cs') -Raw
    if ($st.IndexOf('AlreadyTodayHint') -lt 0) { return '没有提示文案' }
    return $true
}

# ---------------------------------------------------------------- 3.6.89

Check '收藏家已全部改名为编年史' {
    $hits = @()
    foreach ($rel in @('src\Strings.cs', 'src\Menu.cs', 'src\Refresh.cs', 'README.md')) {
        $f = Join-Path $Root $rel
        if (-not (Test-Path $f)) { continue }
        if ((Get-Content $f -Raw).IndexOf([string][char]0x6536 + [char]0x85CF + [char]0x5BB6) -ge 0) { $hits += $rel }
    }
    if ($hits.Count -gt 0) { return ('还有旧名字：' + ($hits -join ', ')) }
    return $true
}

Check '轮播三态互斥只有一处实现' {
    $s = Get-Content (Join-Path $Root 'src\Program.cs') -Raw
    if ($s.IndexOf('internal static void SetWallMode(string mode)') -lt 0) { return '没有 SetWallMode' }
    # 写入口必须一次写完两个键，否则会出现「暂停」和「轮播」同时亮
    $body = $s.Substring($s.IndexOf('internal static void SetWallMode(string mode)'))
    $end = $body.IndexOf('internal static int RotateMinutes')
    if ($end -gt 0) { $body = $body.Substring(0, $end) }
    if ($body.IndexOf('"locked"') -lt 0) { return 'SetWallMode 没写 locked' }
    if ($body.IndexOf('"rotate"') -lt 0) { return 'SetWallMode 没写 rotate' }
    # lock 项与 rotate 项都只许调 SetWallMode，不许自己 SetCfg
    $m = Get-Content (Join-Path $Root 'src\Menu.cs') -Raw
    if ($m.IndexOf('SetWallMode(WallModePaused)') -lt 0) { return 'lock 项没走 SetWallMode' }
    if ($m.IndexOf('SetWallMode(WallModeRotate)') -lt 0) { return 'rotate 项没走 SetWallMode' }
    return $true
}

Check '轮播间隔夹在 1-720' {
    $s = Get-Content (Join-Path $Root 'src\Program.cs') -Raw
    if ($s.IndexOf('v < 1 ? 1 : (v > 720 ? 720 : v)') -lt 0) { return '没有夹取' }
    $f = Get-Content (Join-Path $Root 'src\Forms\MinutesForm.cs') -Raw
    if ($f.IndexOf('picker.Minimum = 1') -lt 0) { return '输入框没设下限' }
    if ($f.IndexOf('picker.Maximum = 720') -lt 0) { return '输入框没设上限' }
    return $true
}

Check '轮播等今天的图到位才开始' {
    $s = Get-Content (Join-Path $Root 'src\Refresh.cs') -Raw
    if ($s.IndexOf('private static void RotateTick()') -lt 0) { return '没有 RotateTick' }
    if ($s.IndexOf('RotateCatchupToday()') -lt 0) { return '没就位时不补今天的图' }
    if ($s.IndexOf('rotateCatchupTries >= 3') -lt 0) { return '补跑没有次数上限' }
    if ($s.IndexOf('string.Equals(stDate, today, StringComparison.Ordinal)') -lt 0) { return '没有把 state.date 和今天比对' }
    # any 会联网预检，轮播下必须降级
    if ($s.IndexOf('scope = "local"') -lt 0) { return 'any 没降级成本地池' }
    return $true
}

Check '轮播定时器独立于忙碌图标定时器' {
    $s = Get-Content (Join-Path $Root 'src\Program.cs') -Raw
    if ($s.IndexOf('rotateWatch.Interval = 60000') -lt 0) { return '没有 60 秒轮播定时器' }
    if ($s.IndexOf('rotateWatch.Tick') -lt 0) { return '没有挂 Tick' }
    if ($s.IndexOf('private static System.Windows.Forms.Timer rotateWatch;') -lt 0) { return '没有字段' }
    return $true
}

Check '编年史下画廊直接置灰' {
    $s = Get-Content (Join-Path $Root 'src\MenuState.cs') -Raw
    if ($s.IndexOf('"gallerytoggle", StringComparison.OrdinalIgnoreCase)) { return !g.Takeover; }') -lt 0) {
        return 'ShouldEnable 没有按接管置灰'
    }
    $m = [regex]::Match($s, '(?s)MenuAlwaysOn = new string\[\]\s*\{(.*?)\};')
    if (-not $m.Success) { return '找不到 MenuAlwaysOn' }
    if ($m.Groups[1].Value -match '"gallerytoggle"') { return '还在 AlwaysOn 名单里，盖不掉' }
    $t = Get-Content (Join-Path $Root 'src\Forms\TakeoverForm.cs') -Raw
    if ($t.IndexOf('Program.RunGalleryClear') -lt 0) { return '进接管时没清画廊' }
    return $true
}

Check '编年史窗口有今日与自动换图两行' {
    $s = Get-Content (Join-Path $Root 'src\Forms\TakeoverForm.cs') -Raw
    if ($s.IndexOf('lblToday') -lt 0) { return '没有今日那行' }
    if ($s.IndexOf('lblAuto') -lt 0) { return '没有自动换图那行' }
    if ($s.IndexOf('S.TkStatLine') -lt 0) { return '统计行没换成带跨度的' }
    if ($s.IndexOf('S.TkMissingDays') -lt 0) { return '没有断档提示' }
    $st = Get-Content (Join-Path $Root 'src\Strings.cs') -Raw
    if ($st.IndexOf('TkTodayMissing') -lt 0) { return '没有未就位文案' }
    return $true
}

Check '更新检查能解析安装包' {
    $s = Get-Content (Join-Path $Root 'src\Update.cs') -Raw
    if ($s.IndexOf('AssetUrl') -lt 0) { return 'UpdateProbe 没扩展 AssetUrl' }
    if ($s.IndexOf('browser_download_url') -lt 0) { return '没解析下载地址' }
    if ($s.IndexOf('AssetSize') -lt 0) { return '没解析体积' }
    $u = Get-Content (Join-Path $Root 'src\SelfUpdate.cs') -Raw
    if ($u.IndexOf('gh-proxy.com/') -lt 0) { return '下载没走 gh-proxy' }
    if ($u.IndexOf("(byte)'M'") -lt 0) { return '下载后没校验 PE 头' }
    if ($u.IndexOf('HttpStatusCode.PartialContent') -lt 0) { return '续传没判 206' }
    if ($u.IndexOf('skipVersion') -lt 0) { return '没有跳过版本' }
    if ($u.IndexOf('lastUpdateCheck') -lt 0) { return '没有每天一次的节流' }
    return $true
}

Check '作者主页按钮指向正确' {
    $u = Get-Content (Join-Path $Root 'src\Update.cs') -Raw
    if ($u.IndexOf('https://space.bilibili.com/14027543') -lt 0) { return 'URL 不对' }
    $a = Get-Content (Join-Path $Root 'src\Forms\AboutForm.cs') -Raw
    if ($a.IndexOf('Program.OpenAuthorPage()') -lt 0) { return '关于窗口没有按钮' }
    return $true
}

Check '版本比较按点分段' {
    $s = Get-Content (Join-Path $Root 'src\Update.cs') -Raw
    if ($s.IndexOf('TrimStart(' + "'v', 'V'" + ')') -lt 0) { return '没忽略 v 前缀' }
    if ($s.IndexOf('int.TryParse') -lt 0) { return '没按数字比较' }
    return $true
}

Check '没偷偷接入聚焦接口' {
    $bad = @()
    Get-ChildItem (Join-Path $Root 'src') -Recurse -Filter *.cs -File | ForEach-Object {
        if ((Get-Content $_.FullName -Raw).IndexOf('arc.msn.com') -ge 0) { $bad += $_.Name }
    }
    if ($bad.Count -gt 0) { return ('混进了聚焦接口：' + ($bad -join ', ')) }
    return $true
}

# ---------------------------------------------------------------- 3.6.90 编年史调整

Check '编年史文案写明只增不减' {
    $s = Get-Content (Join-Path $Root 'src\Strings.cs') -Raw
    if ($s.IndexOf([string][char]0x53EA + [char]0x589E + [char]0x4E0D + [char]0x51CF) -lt 0) {
        return '灰字里没有「只增不减」'
    }
    if ($s.IndexOf([string][char]0x9884 + [char]0x5148 + [char]0x4E0B + [char]0x8F7D) -lt 0) {
        return '标题没提到「预先下载」'
    }
    if ($s.IndexOf([string][char]0x7F16 + [char]0x5E74 + [char]0x53F2 + [char]0x6A21 + [char]0x5F0F) -lt 0) {
        return '没有「编年史模式」这个说法'
    }
    return $true
}

Check '接管中不能换目录' {
    $t = Get-Content (Join-Path $Root 'src\Forms\TakeoverForm.cs') -Raw
    if ($t.IndexOf('btnPick.Visible = false') -lt 0) { return '接管时没藏起选择目录按钮' }
    if ($t.IndexOf('S.TkChangeBlocked') -lt 0) { return 'DoPick 没拦接管' }
    $s = Get-Content (Join-Path $Root 'src\Strings.cs') -Raw
    if ($s.IndexOf('TkChangeBlocked') -lt 0) { return '没有拦截文案' }
    if ($s.IndexOf('public static string TkChange ') -ge 0) { return 'TkChange 还在，说明换目录入口没删净' }
    return $true
}

Check '随机池按接管目录取数' {
    $r = Get-Content (Join-Path $Root 'src\Refresh.cs') -Raw
    $i = $r.IndexOf('internal static int LocalPoolCount()')
    if ($i -lt 0) { return '找不到 LocalPoolCount' }
    $body = $r.Substring($i, 900)
    if ($body.IndexOf('ActiveArchiveImgPath') -lt 0) { return '归档没走接管目录' }
    if ($body.IndexOf('ActiveFavDirPath') -lt 0) { return '收藏没走接管目录' }
    if ($body.IndexOf('ActiveCacheDir') -lt 0) { return '缓存没走接管目录' }
    if ($body.IndexOf('if (!TakeoverActive())') -lt 0) { return '接管下没跳过老存档' }
    # 裸 favDir / cacheDir 参与计数就是老 bug 回来了
    if ($body -match 'CollectDates\(set, favDir') { return '还在数安装目录的收藏' }
    if ($body -match 'CollectDates\(set, cacheDir') { return '还在数安装目录的缓存' }
    if ($body -match 'CollectDates\(set, archiveImgDir') { return '还在数安装目录的归档' }
    if ($r.IndexOf('CollectDates(set, ActiveFavDirPath, false);' + "`r`n            return set.Count;") -lt 0) {
        return 'FavDateCount 没跟着改'
    }
    return $true
}

Check '关于窗口是传统布局' {
    $a = Get-Content (Join-Path $Root 'src\Forms\AboutForm.cs') -Raw
    if ($a.IndexOf('new TextBox()') -ge 0) { return '还在用白底 TextBox 装正文' }
    if ($a.IndexOf('PictureBox') -lt 0) { return '左上没有图标' }
    if ($a.IndexOf('bing.ico') -lt 0) { return '图标没取 bing.ico' }
    if ($a.IndexOf('new Label()') -lt 0) { return '正文没改成 Label' }
    if ($a.IndexOf('S.AboutTagline') -lt 0) { return '标题区没有一句话简介' }
    $i = Get-Content (Join-Path $Root 'src\Info.cs') -Raw
    if ($i.IndexOf('sb.AppendLine(AppName + "  v" + AppVersion);') -ge 0) { return '正文还留着重复的标题行' }
    return $true
}

Check '彩蛋：双击图标飘羽毛' {
    $a = Get-Content (Join-Path $Root 'src\Forms\AboutForm.cs') -Raw
    if ($a.IndexOf('DoubleClick') -lt 0) { return '图标没接双击' }
    if ($a.IndexOf('FeatherLayer') -lt 0) { return '没有羽毛层' }
    if ($a.IndexOf('ToggleEgg') -lt 0) { return '没有开关' }
    if ($a.IndexOf('StopEgg()') -lt 0) { return '关窗没撤动画' }

    $f = Get-Content (Join-Path $Root 'src\Forms\FeatherLayer.cs') -Raw
    if ($f.IndexOf('UpdateLayeredWindow') -lt 0) { return '没用逐像素 alpha 的分层窗口' }
    # 只看有没有真的设置，注释里解释「为什么不用」不算
    if ($f -match 'TransparencyKey\\s*=') { return '还在用抠色，柔和边缘会出粉边' }
    if ($f.IndexOf('WS_EX_TRANSPARENT') -lt 0) { return '鼠标不穿透，会挡住桌面图标' }
    if ($f.IndexOf('WS_EX_TOOLWINDOW') -lt 0) { return '会出现在 Alt+Tab 里' }
    if ($f.IndexOf('VirtualScreen') -lt 0) { return '没覆盖多显示器' }
    # 压到 Progman 后面：桌面之上、普通窗口之下。纯 HWND_BOTTOM 会掉到桌面底下看不见
    if ($f.IndexOf('FindWindow("Progman"') -lt 0) { return '没按 Progman 定位 z 序，羽毛会看不见' }
    # 边缘渐隐：不然飘到屏幕边上被硬切一刀
    if ($f.IndexOf('FadeAt') -lt 0) { return '没有边缘渐隐，羽毛会在边界被裁断' }
    if ($f.IndexOf('Matrix33') -lt 0) { return '渐隐没落到 alpha 矩阵上' }
    return $true
}

Check '彩蛋羽毛用素材图' {
    $png = Join-Path $Root 'assets\feather.png'
    if (-not (Test-Path $png)) { return 'assets\feather.png 不存在' }
    if ((Get-Item $png).Length -lt 20000) { return '素材太小，可能没内容' }

    $b = Get-Content (Join-Path $Root 'build.ps1') -Raw
    if ($b.IndexOf('feather.png') -lt 0) { return 'build.ps1 没把它嵌进 exe' }

    $fa = Get-Content (Join-Path $Root 'src\Forms\FeatherArt.cs') -Raw
    if ($fa.IndexOf('GetManifestResourceStream("feather.png")') -lt 0) { return 'FeatherArt 没读素材' }
    if ($fa.IndexOf('MakeHardAlpha(asset)') -ge 0) { return '素材还被硬阈值化，柔和边缘会丢' }

    $l = Join-Path $Root 'assets\logo.png'
    if (-not (Test-Path $l)) { return 'assets\logo.png 不存在' }
    return $true
}

Check '双击图标换成指定素材' {
    $png = Join-Path $Root 'assets\egg.png'
    if (-not (Test-Path $png)) { return 'assets\egg.png 不存在' }
    $b = Get-Content (Join-Path $Root 'build.ps1') -Raw
    if ($b.IndexOf('/resource:$eggPng,egg.png') -lt 0) { return 'build.ps1 没嵌入 egg.png' }
    $a = Get-Content (Join-Path $Root 'src\Forms\AboutForm.cs') -Raw
    if ($a.IndexOf('GetManifestResourceStream("egg.png")') -lt 0) { return '关于窗口没读 egg.png' }
    if ($a.IndexOf('LoadEggIcon') -lt 0) { return '没有换图入口' }
    if ($a.IndexOf('FeatherArt.Render(size)') -lt 0) { return '素材缺失时没有兜底' }
    return $true
}

Check '随机换图能取到全历史标题' {
    $s = Get-Content (Join-Path $Root 'bing-wallpaper.ps1') -Raw
    if ($s.IndexOf('function Get-LocalTitleMap') -lt 0) { return '没有本地标题索引' }
    # 两张本地索引都要读：老存档铺底、全量索引覆盖
    $i = $s.IndexOf('function Get-LocalTitleMap')
    $body = $s.Substring($i, 1600)
    if ($body.IndexOf('Get-OldIndexMap') -lt 0) { return '没合老存档' }
    if ($body.IndexOf('$AllIndexPath') -lt 0) { return '没合全量索引' }
    # 走 Get-FullIndex 会联网，随机换图不能这么干
    if ($body.IndexOf('Get-FullIndex') -ge 0) { return '用了会联网的 Get-FullIndex' }
    # 随机分支必须真的调用它
    $j = $s.IndexOf('随机切换（')
    if ($s.IndexOf('$tm = Get-LocalTitleMap') -lt 0) { return '随机分支没调本地索引' }
    return $true
}

Check '开轮播自动关掉换图通知' {
    $s = Get-Content (Join-Path $Root 'src\Program.cs') -Raw
    $i = $s.IndexOf('internal static void SetWallMode(string mode)')
    if ($i -lt 0) { return '找不到 SetWallMode' }
    $body = $s.Substring($i, 1200)
    if ($body.IndexOf('"notify", false') -lt 0) { return '切轮播时没关通知' }
    if ($body.IndexOf('"lastRotate"') -lt 0) { return '锚点也没写，可能改错地方了' }
    $m = Get-Content (Join-Path $Root 'src\Menu.cs') -Raw
    if ($m.IndexOf('S.RotateNotifOffHint') -lt 0) { return '关通知时没提示用户' }
    return $true
}

# ---------------------------------------------------------------- 汇总
Write-Host ''
Write-Host ('-' * 50) -ForegroundColor DarkGray
Write-Host ('通过 ' + $pass + ' 项，失败 ' + $fail + ' 项') -ForegroundColor $(if ($fail -eq 0) { 'Green' } else { 'Red' })
if ($fail -gt 0) {
    Write-Host '失败项：' -ForegroundColor Red
    $failed | ForEach-Object { Write-Host ('  - ' + $_) -ForegroundColor Red }
}
Write-Host ''

exit $(if ($fail -eq 0) { 0 } else { 1 })
