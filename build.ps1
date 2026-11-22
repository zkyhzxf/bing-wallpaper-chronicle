#Requires -Version 5.1
<#
.SYNOPSIS
    编译 必应壁纸Lite 的托盘程序。

.DESCRIPTION
    用系统自带的 csc.exe 把 TrayProgram.cs 编译成单文件 winexe，
    并把 PowerShell 脚本与图标作为嵌入资源打包进去。
    不需要 MSBuild，不需要 NuGet，不需要 Visual Studio。

    源码位置自动识别：
      · 如果存在 src\ 目录，从 src\ 里取源文件和资源
      · 否则用仓库根目录

.PARAMETER Version
    覆盖版本号，例如 -Version 1.20.0。不传则沿用 TrayProgram.cs 里写死的。

.PARAMETER Output
    输出目录，默认 dist\

.PARAMETER Portable
    额外打一个绿色版 zip

.EXAMPLE
    .\build.ps1

.EXAMPLE
    .\build.ps1 -Version 1.20.0 -Portable

.NOTES
    目标框架 .NET Framework 4.x，编译产物依赖系统自带的运行时。
#>
[CmdletBinding()]
param(
    [string]$Version = '',
    [string]$Output = 'dist',
    [switch]$Portable
)

$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------- 路径

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$srcDir   = if (Test-Path (Join-Path $repoRoot 'src')) { Join-Path $repoRoot 'src' } else { $repoRoot }
$resDir   = $repoRoot   # 资源与脚本始终在仓库根

# 单文件模式的源码位置（多文件模式下改用 $srcDir 下所有 .cs）
$csFile    = if (Test-Path (Join-Path $repoRoot 'TrayProgram.cs')) { Join-Path $repoRoot 'TrayProgram.cs' } else { Join-Path $srcDir 'Program.cs' }
$psFile    = Join-Path $resDir 'bing-wallpaper.ps1'
$iconFile  = Join-Path $resDir 'bing.ico'
$iconBusy  = Join-Path $resDir 'bing-busy.ico'
$cfgFile   = Join-Path $resDir 'config.json'
$snapFile  = Join-Path $resDir 'assets\all-index.json'
$snapOld   = Join-Path $resDir 'assets\old-index.json'

if (-not (Test-Path $csFile)) { throw "找不到源文件：$csFile" }
if (-not (Test-Path $psFile)) { throw "找不到核心脚本：$psFile" }

# ---------------------------------------------------------------- 版本号

if ($Version) {
    Write-Host "覆盖版本号：$Version" -ForegroundColor Cyan
    $txt = [System.IO.File]::ReadAllText($csFile, [System.Text.Encoding]::UTF8)
    $three = $Version
    $four  = if ($Version.Split('.').Count -eq 3) { "$Version.0" } else { $Version }
    $txt = $txt -replace 'AssemblyVersion\("[0-9.]+"\)',              "AssemblyVersion(`"$four`")"
    $txt = $txt -replace 'AssemblyFileVersion\("[0-9.]+"\)',          "AssemblyFileVersion(`"$four`")"
    $txt = $txt -replace 'AssemblyInformationalVersion\("[0-9.]+"\)', "AssemblyInformationalVersion(`"$three`")"
    $txt = $txt -replace 'AppVersion\s*=\s*"[0-9.]+"',                "AppVersion  = `"$three`""
    [System.IO.File]::WriteAllText($csFile, $txt, (New-Object System.Text.UTF8Encoding($true)))
    Write-Host "  已写回 TrayProgram.cs" -ForegroundColor DarkGray
}

# 从源码里读出当前版本，用于命名产物
$verMatch = [regex]::Match([System.IO.File]::ReadAllText($csFile, [System.Text.Encoding]::UTF8),
                           'AppVersion\s*=\s*"([0-9.]+)"')
$ver = if ($verMatch.Success) { $verMatch.Groups[1].Value } else { '0.0.0' }
Write-Host "构建版本：$ver" -ForegroundColor Cyan

# ---------------------------------------------------------------- 编译器

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) {
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path $csc)) {
    throw '找不到 csc.exe。需要 .NET Framework 4.x（Windows 自带）。'
}

# ---------------------------------------------------------------- 输出

$outDir = if ([System.IO.Path]::IsPathRooted($Output)) { $Output } else { Join-Path $repoRoot $Output }
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }

$exeName = "BingWallpaperLite.exe"
$exePath = Join-Path $outDir $exeName

# ---------------------------------------------------------------- 编译

$buildArgs = @(
    '/nologo'
    '/target:winexe'
    '/optimize+'
    '/utf8output'
    "/out:$exePath"
    "/win32icon:$iconFile"
    "/resource:$psFile,bing-wallpaper.ps1"
    "/resource:$iconFile,bing.ico"
    "/resource:$iconBusy,bing-busy.ico"
    '/reference:System.dll'
    '/reference:System.Drawing.dll'
    '/reference:System.Windows.Forms.dll'
    '/reference:System.Web.Extensions.dll'
    '/reference:Microsoft.VisualBasic.dll'
)

# 离线索引快照：本地有更新的运行时索引时同步到 assets，保证发布产物带最新数据
$liveIdx = Join-Path $repoRoot 'archive\all-index.json'
if ((Test-Path $liveIdx) -and (Test-Path $snapFile)) {
    $liveTime = (Get-Item $liveIdx).LastWriteTime
    $snapTime = (Get-Item $snapFile).LastWriteTime
    if ($liveTime -gt $snapTime) {
        Copy-Item $liveIdx $snapFile -Force
        Write-Host '已刷新 assets\all-index.json 快照' -ForegroundColor DarkGray
    }
} elseif ((Test-Path $liveIdx) -and -not (Test-Path $snapFile)) {
    Copy-Item $liveIdx $snapFile -Force
    Write-Host '已生成 assets\all-index.json 快照' -ForegroundColor DarkGray
}

# 老存档索引（2009-08 ~ 2016-02）：同样以运行时数据为准刷新快照
$liveOld = Join-Path $repoRoot 'archive\old-index.json'
if ((Test-Path $liveOld) -and (Test-Path $snapOld)) {
    if ((Get-Item $liveOld).LastWriteTime -gt (Get-Item $snapOld).LastWriteTime) {
        Copy-Item $liveOld $snapOld -Force
        Write-Host '已刷新 assets\old-index.json 快照' -ForegroundColor DarkGray
    }
} elseif ((Test-Path $liveOld) -and -not (Test-Path $snapOld)) {
    Copy-Item $liveOld $snapOld -Force
    Write-Host '已生成 assets\old-index.json 快照' -ForegroundColor DarkGray
}

# 源码：优先用 src\ 下的多文件；没有则退回单文件 TrayProgram.cs
$srcDir = Join-Path $repoRoot 'src'
if (Test-Path $srcDir) {
    $csFiles = @(Get-ChildItem $srcDir -Recurse -Filter '*.cs' | Sort-Object FullName | ForEach-Object { $_.FullName })
    Write-Host ("源码：src\ 下 " + $csFiles.Count + " 个 .cs 文件") -ForegroundColor DarkGray
} else {
    $csFiles = @($csFile)
    Write-Host '源码：单文件 TrayProgram.cs' -ForegroundColor DarkGray
}

$buildArgs += $csFiles
if (Test-Path $cfgFile) { $buildArgs += @("/resource:$cfgFile,config.json") }
if (Test-Path $snapFile) { $buildArgs += @("/resource:$snapFile,all-index.json") }
if (Test-Path $snapOld)  { $buildArgs += @("/resource:$snapOld,old-index.json") }

Write-Host "编译中…" -ForegroundColor Cyan
$log = & $csc $buildArgs 2>&1 | Out-String

if ($log.Trim()) {
    Write-Host "编译失败：" -ForegroundColor Red
    ($log.Trim() -split "`n") | Select-Object -First 20 | ForEach-Object { Write-Host ('  ' + $_.Trim()) }
    exit 1
}

$exeInfo = Get-Item $exePath
Write-Host ("编译成功：{0}  {1:N1} KB  v{2}" -f $exeName, ($exeInfo.Length / 1KB), $exeInfo.VersionInfo.FileVersion) -ForegroundColor Green

# ---------------------------------------------------------------- 发布包

$setupName = "BingWallpaperLite-v$ver-Setup.exe"
Copy-Item $exePath (Join-Path $outDir $setupName) -Force
Write-Host "安装包：$setupName" -ForegroundColor Green

if ($Portable) {
    $pkgName = "BingWallpaperLite-v$ver"
    $pkgDir = Join-Path $outDir $pkgName
    if (Test-Path $pkgDir) { Remove-Item -LiteralPath $pkgDir -Recurse -Force }

    New-Item -ItemType Directory -Path $pkgDir -Force | Out-Null
    Copy-Item $exePath  (Join-Path $pkgDir 'BingWallpaperLite.exe') -Force
    Copy-Item $psFile   (Join-Path $pkgDir 'bing-wallpaper.ps1')   -Force
    Copy-Item $iconFile (Join-Path $pkgDir 'bing.ico')             -Force
    Copy-Item $iconBusy (Join-Path $pkgDir 'bing-busy.ico')        -Force
    if (Test-Path $cfgFile) { Copy-Item $cfgFile (Join-Path $pkgDir 'config.json') -Force }

    $zipPath = Join-Path $outDir "$pkgName-Portable.zip"
    Compress-Archive -Path $pkgDir -DestinationPath $zipPath -CompressionLevel Optimal -Force
    Write-Host ("绿色版：{0}  {1:N1} KB" -f (Split-Path $zipPath -Leaf), ((Get-Item $zipPath).Length / 1KB)) -ForegroundColor Green
}

Write-Host ''
Write-Host '完成。' -ForegroundColor Green
