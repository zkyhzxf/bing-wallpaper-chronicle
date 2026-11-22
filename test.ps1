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
