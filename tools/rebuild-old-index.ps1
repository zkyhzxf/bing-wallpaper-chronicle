# 重建 archive\old-index.json 的 2009-07-13 ~ 2014-12-31 段
# 基准：_wilii-raw.json（逐日有序日表），官方日期 = wilii 日期本身，不做任何偏移
#
# 为什么不做偏移：wilii 的文件名自带语义，日期与语义一一对齐，七条独立锚点全部吻合：
#   Harbin_                       2010-01-05  第26届哈尔滨国际冰雪节开幕日（外部新闻三源一致）
#   TeacherDay_                   2011-09-10  教师节
#   MidAutumnDay_                 2011-09-12  2011 年中秋节
#   DragonBoatFestival_           2013-06-12  2013 年端午节
#   OlympicsClosingCeremonies_    2012-08-13  伦敦奥运闭幕式次日凌晨发布
#   MaoBirthday_                  2009-12-26  毛泽东诞辰 116 周年
#   SantaClaus_                   2009-12-24  平安夜
# 2016 年后的 all-index（官方接口，口径无争议）也证明必应节日图在节日当天发布。
# 因此「官方日期 = wilii 日期」，老索引在 2010-2014 段的 −1 偏移是错的。
#
# wilii 里空 slug 的占位记录跳过，对应日期留空（不猜图）
# 2015 年之后（含 2015-01-01）的段落原样保留，它们本来就是 wilii 原日期
#
# 用法：
#   pwsh -File tools\rebuild-old-index.ps1 -DryRun    # 只出清单
#   pwsh -File tools\rebuild-old-index.ps1            # 真写

param(
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$wiliiPath = Join-Path $root '_wilii-raw.json'
$targetPath = Join-Path $root 'archive\old-index.json'

foreach ($p in @($wiliiPath, $targetPath)) {
    if (-not (Test-Path $p)) { throw "缺少文件：$p" }
}

# 检测原文件是否带 BOM，写回时保持一致
$bytes = [byte[]](Get-Content $targetPath -AsByteStream -TotalCount 3)
$hasBom = ($bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)
$enc = New-Object System.Text.UTF8Encoding($hasBom)

function Get-SlugName([string]$f) {
    $b = ($f -split '/')[-1]
    if ($b -match '^_\d{3,4}x\d{3,4}\.jpg$') { return $null }
    $key = $b -replace '_\d{3,4}x\d{3,4}\.jpg$', '' -replace '\.jpg$', ''
    if (-not $key) { return $null }
    return ($key + '_1920x1080.jpg')
}

$wilii = Get-Content $wiliiPath -Raw -Encoding UTF8 | ConvertFrom-Json
$cur = @(Get-Content $targetPath -Raw -Encoding UTF8 | ConvertFrom-Json)

# 1) 保留当前索引里 2010-2014 之外的部分
$kept = New-Object System.Collections.ArrayList
foreach ($x in $cur) {
    $d = [string]$x.d
    if ($d -ge '20090712' -and $d -le '20141231') { continue }
    [void]$kept.Add([pscustomobject]@{ d = $d; t = [string]$x.t; c = [string]$x.c; n = [string]$x.n; a = [string]$x.a })
}

# 2) 用 wilii 重建 2009-2014 段（wilii 从 2009-07-13 起有记录）
#    官方日期 = wilii 日期，不做偏移（依据见文件头）
$rebuilt = New-Object System.Collections.ArrayList
$skipped = New-Object System.Collections.ArrayList
foreach ($x in $wilii) {
    $slug = Get-SlugName $x.f
    $wd = $x.d -replace '-', ''
    if ($wd -lt '20090713' -or $wd -gt '20141231') { continue }
    if ($null -eq $slug) {
        [void]$skipped.Add($wd)
        continue
    }
    [void]$rebuilt.Add([pscustomobject]@{ d = $wd; t = [string]$x.t; c = [string]$x.c; n = $slug; a = '' })
}

# 3) 合并 + 去重（同日期只留一条，按 wilii 优先）
$merged = @{}
foreach ($x in $kept) { $merged[$x.d] = $x }
foreach ($x in $rebuilt) { $merged[$x.d] = $x }
$final = @($merged.Values | Sort-Object d -Descending)

# 4) 差异清单（拿当前文件当基准）
$oldMap = @{}
foreach ($x in $cur) { $oldMap[[string]$x.d] = $x }
$changes = New-Object System.Collections.ArrayList
foreach ($x in $final) {
    if (-not $oldMap.ContainsKey($x.d)) { [void]$changes.Add("ADD  $($x.d)  $($x.n)"); continue }
    $o = $oldMap[$x.d]
    if ([string]$o.n -ne $x.n) { [void]$changes.Add("CHG  $($x.d)  $($o.n) -> $($x.n)") }
}
foreach ($x in $cur) {
    $d = [string]$x.d
    if (-not $merged.ContainsKey($d)) { [void]$changes.Add("GONE $d  $($x.n)") }
}

$chg = @($changes | Where-Object { $_ -like 'CHG*' }).Count
$add = @($changes | Where-Object { $_ -like 'ADD*' }).Count
$gon = @($changes | Where-Object { $_ -like 'GONE*' }).Count

Write-Host ''
Write-Host '老索引重建（2009-07-13 ~ 2014-12-31，wilii 原日期）' -ForegroundColor Cyan
Write-Host ("  当前条数     : " + $cur.Count)
Write-Host ("  重建后条数   : " + $final.Count)
Write-Host ("  换图 CHG     : " + $chg)
Write-Host ("  新增 ADD     : " + $add)
Write-Host ("  空出 GONE    : " + $gon)
Write-Host ("  空 slug 跳过 : " + (($skipped | Sort-Object) -join ', ')) -ForegroundColor DarkGray
if ($final.Count -gt 0) {
    Write-Host ("  日期范围     : " + $final[-1].d + ' ~ ' + $final[0].d)
}

$logPath = Join-Path $root 'cache\_rebuild_changes.txt'
$changes | Set-Content $logPath -Encoding UTF8
Write-Host ("  差异清单     : " + $logPath) -ForegroundColor DarkGray

if ($DryRun) {
    Write-Host ''
    Write-Host '[DryRun] 未写入任何文件' -ForegroundColor Yellow
    Write-Host '--- 差异样本（前 15）---' -ForegroundColor DarkGray
    $changes | Select-Object -First 15 | ForEach-Object { Write-Host ('  ' + $_) }
    exit 0
}

$json = ConvertTo-Json -InputObject $final -Depth 4 -Compress
[System.IO.File]::WriteAllText($targetPath, $json, $enc)
Write-Host ''
Write-Host ("已写入：" + $targetPath + "（" + (Get-Item $targetPath).Length + " 字节）") -ForegroundColor Green
