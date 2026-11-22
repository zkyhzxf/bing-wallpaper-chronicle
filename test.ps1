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
        if ($d -lt '20090712') { return ('日期早于下界: ' + $d) }
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
    $r = Run-Script @('-Date', '20090711')
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
    if ($j.index.minDate -ne '20090712') { return ('minDate 为 ' + $j.index.minDate + '，与代码下界 20090712 不符') }
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
        '20090712' = 'GreenTurtle_'
        '20091231' = 'ANewDay_'
        '20100101' = 'ANewLife_'
        '20100104' = 'Harbin_'
        '20100327' = 'EarthNight_'
        '20100414' = 'Cuscus_'
        '20100417' = 'ClareIsland_'
        '20100420' = 'TurtleHatchling_'
        '20100515' = 'Cuscus_'
        '20100522' = 'TurtleHatchling_'
        '20120831' = 'GoldenMonkey_'
        '20141129' = 'ScottishWildcat_'
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

Check '四天确认空缺' {
    $m = @{}
    foreach ($e in $oldArr) { $m[[string]$e.d] = $true }
    foreach ($d in @('20100415', '20100416', '20120813', '20141130')) {
        if ($m.ContainsKey($d)) { return ($d + ' 不该有记录') }
    }
    if (-not $m.ContainsKey('20141129')) { return '20141129 该有记录（苏格兰野猫）；口径切换点可能退回 20141130' }
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
