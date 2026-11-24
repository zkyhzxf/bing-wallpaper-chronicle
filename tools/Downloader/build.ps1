# 编译全量下载器（单文件 exe，无外部依赖）
# 用法：powershell -ExecutionPolicy Bypass -File tools\Downloader\build.ps1
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$root = Split-Path -Parent (Split-Path -Parent $here)
$dist = Join-Path $root 'dist'
if (-not (Test-Path $dist)) { New-Item -ItemType Directory -Path $dist -Force | Out-Null }

$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $csc)) { throw '找不到 csc.exe（需要 .NET Framework 4.x）' }

$out = Join-Path $dist 'BingWallDownloader.exe'
# 旧版还开着的时候覆盖不了它 —— 改输出到 -new.exe，别打断正在跑的下载任务
try {
    if (Test-Path $out) {
        $probe = [System.IO.File]::Open($out, 'Open', 'ReadWrite', 'None')
        $probe.Close()
    }
} catch {
    $out = Join-Path $dist 'BingWallDownloader-new.exe'
    Write-Host '旧版正在运行，本次输出到 BingWallDownloader-new.exe（停掉旧版后可改名替换）' -ForegroundColor Yellow
}
$src = @(
    (Join-Path $here 'Downloader.cs'),
    (Join-Path $here 'MainForm.cs')
)

$cscArgs = @(
    '/nologo',
    '/target:winexe',
    '/platform:anycpu',
    '/optimize+',
    '/warn:4',
    '/r:System.dll',
    '/r:System.Drawing.dll',
    '/r:System.Windows.Forms.dll',
    "/out:$out"
) + $src

Write-Host '编译中…'
$log = & $csc $cscArgs 2>&1
if ($LASTEXITCODE -ne 0) {
    $log | ForEach-Object { Write-Host $_ }
    throw "编译失败（退出码 $LASTEXITCODE）"
}
if ($log) { $log | ForEach-Object { Write-Host $_ } }

$fi = Get-Item $out
Write-Host ('编译成功：' + $fi.Name + '  ' + [math]::Round($fi.Length / 1KB, 1) + ' KB')
Write-Host ('输出：' + $out)
