# 必应壁纸编年史 · 30s animatic 构建入口
# 用法：pwsh -File D:\Tools\bing-wallpaper\promo\build.ps1
#       加 -Frames 只重出静帧；-Render 只重渲染；都不加则全跑

param(
    [switch]$Frames,
    [switch]$Render,
    [switch]$All
)

$ErrorActionPreference = 'Stop'
$Promo = 'D:\Tools\bing-wallpaper\promo'
$PyWB  = 'D:\WorkBuddy\resources\vendor\python\python.exe'                                  # 有 edge_tts
$PyDSH = 'C:\Users\81218\.dsh\dsh-runtimes\dsh-primary-runtime\dependencies\python\python.exe' # 有 qrcode
$FF    = 'D:\tools\ffmpeg\ffmpeg.exe'
$FP    = 'D:\tools\ffmpeg\ffprobe.exe'

function Need($p, $what) {
    if (-not (Test-Path $p)) { throw "$what 不存在：$p" }
}

Write-Host '--- 0. 环境自检 ---' -ForegroundColor Cyan
Need $FF 'ffmpeg'
Need $FP 'ffprobe'
Need $PyWB 'WorkBuddy python'
Need $PyDSH 'DSH python'
Need "$Promo\make_frames.py" 'make_frames.py'
Need "$Promo\build_video.py" 'build_video.py'
& $FF -version | Select-Object -First 1
& $FP -version | Select-Object -First 1

if ($All -or (-not $Frames -and -not $Render)) { $Frames = $true; $Render = $true }

if ($Frames) {
    Write-Host '--- 1. 静帧（需要 qrcode）---' -ForegroundColor Cyan
    Push-Location $Promo
    & $PyDSH make_frames.py
    if ($LASTEXITCODE -ne 0) { Pop-Location; throw '静帧生成失败' }
    & $PyDSH make_cover.py
    Pop-Location
}

if ($Render) {
    Write-Host '--- 2. 配音（首次或改稿时；已存在会跳过）---' -ForegroundColor Cyan
    Push-Location "$Promo\tts"
    if (-not (Test-Path 'durations.json')) {
        & $PyWB make_tts.py
        if ($LASTEXITCODE -ne 0) { Pop-Location; throw 'TTS 失败' }
    }
    if (-not (Test-Path '01c.mp3')) {
        & $PyWB trim_silence.py
        if ($LASTEXITCODE -ne 0) { Pop-Location; throw '静音修剪失败' }
    }
    Pop-Location

    Write-Host '--- 3. 渲染 + 验收 ---' -ForegroundColor Cyan
    Push-Location $Promo
    # 中间件全部 -y 覆盖，无需先删除整个 _build
    & $PyWB build_video.py
    if ($LASTEXITCODE -ne 0) { Pop-Location; throw '渲染失败' }
    Pop-Location
}

$final = "$Promo\out\animatic-30s.mp4"
Need $final '成片'
Write-Host '--- 4. 验收 ---' -ForegroundColor Cyan
& $FP -v error -select_streams v:0 -show_entries stream=codec_name,width,height,r_frame_rate,pix_fmt -of default=nw=1 $final
& $FP -v error -select_streams a:0 -show_entries stream=codec_name,sample_rate,channels -of default=nw=1 $final
& $FP -v error -show_entries format=duration,size -of default=nw=1 $final
Write-Host "成片：$final" -ForegroundColor Green
