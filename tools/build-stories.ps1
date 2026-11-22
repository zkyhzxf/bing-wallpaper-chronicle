param(
    [int]$Limit = 0,
    [string]$FromDate = '20131201',
    [string]$OutDir = 'D:\tools\bing-wallpaper\archive\stories'
)
$ErrorActionPreference = 'Stop'
$ua = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/125.0 Safari/537.36'
$src = 'D:\tools\todaybing\data\bing-all-index.json'

if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir -Force | Out-Null }

$j = @(Get-Content $src -Raw -Encoding UTF8 | ConvertFrom-Json)
$cn = @($j | Where-Object { $_.a -eq 'cn' -and [string]$_.d -ge $FromDate })
$byDate = [ordered]@{}
foreach ($x in $cn) {
    $d = [string]$x.d
    if ($d.Length -eq 8 -and -not $byDate.Contains($d)) { $byDate[$d] = [string]$x.i }
}
Write-Host ('待处理日期: ' + $byDate.Count + '  (>= ' + $FromDate + ')')

function UnEsc($s) {
    $r = [string]$s
    $r = $r -replace '&quot;', '"'
    $r = $r -replace '&#39;', "'"
    $r = $r -replace '&lt;', '<'
    $r = $r -replace '&gt;', '>'
    $r = $r -replace '&amp;', '&'
    $r = $r -replace '&nbsp;', ' '
    return $r.Trim()
}

$done = 0; $skip = 0; $fail = 0; $noStory = 0
$failList = New-Object System.Collections.Generic.List[string]
$n = 0
foreach ($d in $byDate.Keys) {
    $n++
    $f = Join-Path $OutDir ($d + '.txt')
    if (Test-Path $f) { $skip++; continue }
    if ($Limit -gt 0 -and $done -ge $Limit) { break }

    $slug = $byDate[$d]
    $ok = $false
    for ($try = 1; $try -le 2; $try++) {
        try {
            $resp = Invoke-WebRequest -Uri ('https://www.todaybing.com/bing/' + $slug) -UseBasicParsing -TimeoutSec 25 `
                -Headers @{'User-Agent' = $ua; 'Referer' = 'https://www.todaybing.com/'}
            $html = $resp.Content
            $m = [regex]::Match($html, 'data-text-zh="([^"]{10,})"')
            if (-not $m.Success) { $m = [regex]::Match($html, 'data-text="([^"]{10,})"') }
            if ($m.Success) {
                $story = UnEsc $m.Groups[1].Value
                if ($story.Length -ge 10) {
                    [System.IO.File]::WriteAllText($f, $story, (New-Object System.Text.UTF8Encoding($false)))
                    $done++
                    $ok = $true
                } else { $noStory++ ; $ok = $true }
            } else {
                # 页面没有故事字段 = 该日期本来就没有故事，记一个空标记避免反复重试
                [System.IO.File]::WriteAllText($f, '', (New-Object System.Text.UTF8Encoding($false)))
                $noStory++
                $ok = $true
            }
            break
        } catch {
            Start-Sleep -Milliseconds 800
        }
    }
    if (-not $ok) { $fail++; $failList.Add($d) }
    if ($n % 100 -eq 0) {
        Write-Host ("  {0}/{1}  新增 {2}  跳过 {3}  无故事 {4}  失败 {5}" -f $n, $byDate.Count, $done, $skip, $noStory, $fail)
    }
    Start-Sleep -Milliseconds 150
}

Write-Host ''
Write-Host ("完成：新增 " + $done + " / 跳过 " + $skip + " / 无故事 " + $noStory + " / 失败 " + $fail)
if ($failList.Count -gt 0) {
    $fp = 'D:\tools\bing-wallpaper\_stories-failed.txt'
    [System.IO.File]::WriteAllLines($fp, $failList.ToArray(), (New-Object System.Text.UTF8Encoding($false)))
    Write-Host ('失败清单: ' + $fp)
}
$cnt = @(Get-ChildItem $OutDir -Filter '*.txt' -File)
$nonEmpty = @($cnt | Where-Object { $_.Length -gt 10 })
Write-Host ('目录内文件 ' + $cnt.Count + " 个，其中有内容的 " + $nonEmpty.Count + ' 个')
