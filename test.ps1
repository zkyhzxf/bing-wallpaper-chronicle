# 必应壁纸编年史 · 回归测试
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

Check '-Date 有效日期（用归档里已有的那天）' {
    $st = Get-Content (Join-Path $Root 'state.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $r = Run-Script @('-Date', $st.date)
    if ($r.Code -ne 0 -and $r.Code -ne 4) { return ('退出码 ' + $r.Code) }
    return $true
}

Check '-Date 无效日期退出码 5/7' {
    $r = Run-Script @('-Date', '20000101')
    if ($r.Code -ne 5 -and $r.Code -ne 7) { return ('退出码 ' + $r.Code + '，应为 5 或 7') }
    return $true
}

Check '-Date 下界前一天应被拒绝' {
    $st = Get-Content (Join-Path $Root 'state.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $before = $st.date
    $r = Run-Script @('-Date', '20090712')
    $after = (Get-Content (Join-Path $Root 'state.json') -Raw -Encoding UTF8 | ConvertFrom-Json).date
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
    $st = Get-Content (Join-Path $Root 'state.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $before = $st.date
    $r = Run-Script @('-Date', '2016030')
    $after = (Get-Content (Join-Path $Root 'state.json') -Raw -Encoding UTF8 | ConvertFrom-Json).date
    if ($r.Code -eq 0 -or $r.Code -eq 4) { return ('退出码 ' + $r.Code + '，位数不足不该成功') }
    if ($before -ne $after) { return ('state 从 ' + $before + ' 变成 ' + $after) }
    return $true
}
Check '-Step -1 执行成功' {
    $r = Run-Script @('-Step', '-1')
    if ($r.Code -ne 0 -and $r.Code -ne 4) { return ('退出码 ' + $r.Code) }
    return $true
}

Check '-Step 1 执行成功' {
    $r = Run-Script @('-Step', '1')
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
    $lock = Join-Path $Root 'cache\.running'
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

Check '照片墙取图落 cache\wall 且不写索引' {
    $wall = Join-Path $Root 'cache\wall'
    $idx = Join-Path $Root 'cache\index.json'
    $before = if (Test-Path $idx) { Get-Content $idx -Raw -Encoding UTF8 } else { '' }
    $r = Run-Script @('-WallDate', '20180315')
    if ($r.Code -ne 0 -and $r.Code -ne 4 -and $r.Code -ne 8) { return ('退出码 ' + $r.Code) }
    $hit = @(Get-ChildItem $wall -Filter '20180315_*.jpg' -ErrorAction SilentlyContinue)
    if ($hit.Count -eq 0) { return 'wall 目录里没落这张图' }
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
    if ($order -notmatch '^归档原图 UHD') { return ('首项不是 UHD：' + $order) }
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

Check '空 catch 不超过 84 处' {
    $t = Get-Content $Script -Raw
    $n = ([regex]::Matches($t, 'catch \{ \}')).Count
    if ($n -gt 84) { return ('空 catch 已 ' + $n + ' 处，超上限 84') }
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
    $t = Get-Content (Join-Path $Root 'src\Forms\AiForm.cs') -Raw
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

Check '换过的图：改名、挪位置、加位置列、双击换图' {
    $s = Get-Content (Join-Path $Root 'src\Strings.cs') -Raw
    if ($s -match '自动清理保留') { return 'Strings.cs 里还留着「自动清理保留」' }
    if ($s -notmatch 'PinMenu\s+\{ get \{ return T\("换过的图…"') { return '菜单项没改成「换过的图」' }
    if ($s -notmatch 'PinColWhere') { return '没有位置列文案' }
    if ($s -notmatch 'PinLocLib') { return '没有历史图库位置文案' }
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

Check '帮助与 README 说明老存档不受自动清理' {
    $s = Get-Content (Join-Path $Root 'src\Strings.cs') -Raw
    if ($s -notmatch '老存档（2016 年 3 月之前的镜像图）在归档额度之外单独保管') { return '帮助第四节没提老存档不受自动清理' }
    $r = Get-Content (Join-Path $Root 'README.md') -Raw
    if ($r -notmatch '老存档不受任何自动清理') { return 'README 没写老存档不受自动清理' }
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

Check '老存档要删的去处写进了两个提示' {
    $s = Get-Content (Join-Path $Root 'src\Strings.cs') -Raw
    if ($s -notmatch '要删请走 清理 → 清理归档') { return '移出名单确认框没写去哪删老存档' }
    if ($s -notmatch '老存档要删：清理 → 清理归档') { return '移出无效弹窗没写去哪删老存档' }
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
