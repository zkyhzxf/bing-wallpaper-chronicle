# -*- coding: utf-8 -*-
"""全片渲染：30 句 / 30 镜 / 2 分 05 秒
逐帧合成 -> rawvideo -> ffmpeg；字幕在渲染时直接叠，不另走 overlay。
"""
import json, os, subprocess, sys, time
from PIL import Image, ImageDraw, ImageFont

import scenes, film, film2

W, H, FPS = 1920, 1080, 30
FF = r"D:\tools\ffmpeg\ffmpeg.exe"
FP = r"D:\tools\ffmpeg\ffprobe.exe"
BASE = os.path.dirname(os.path.abspath(__file__))
TMP = os.path.join(BASE, "_full")
OUT = os.path.join(BASE, "out", "chronicle-2m05.mp4")
FONT_B = r"C:\Windows\Fonts\Noto Sans SC Bold (TrueType).otf"
AUD = os.path.join(BASE, "tts")
os.makedirs(TMP, exist_ok=True)

SCENES = [
    film.F01Opening, film.F02Desktop, film.F03Daily, film.F04PopupAd, film.F05ContextMenu,
    film.F06Campaign, film.F07OfficialDesk, film.F08Eight, film.F09ThreeGone, film.F10TakeAway,
    film.F11Timeline, film.F12VsOfficial, film.F13Title, film.F14Tsubasa, film.F15Collect,
    film.F16Book, film2.F17TodayCard, film2.F18Story, film2.F19SameDay, film2.F20Drag,
    film2.F21Wall, film2.F22Festival, film2.F23Search, film2.F24FreshVsMemory, film2.F25NoPopup,
    film2.F26ThreeDays, film2.F26bOpen, film2.F27SearchRepo, film2.F28Like,
    film2.F29Bookmark, film2.F30End,
]


def make_sub(text, path, dark=False):
    SB_W, SB_H = 1920, 220
    f = ImageFont.truetype(FONT_B, 54)
    im = Image.new("RGBA", (SB_W, SB_H), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    b = d.textbbox((0, 0), text, font=f)
    x = (SB_W - (b[2] - b[0])) // 2
    y = (SB_H - (b[3] - b[1])) // 2 - b[1]
    if dark:
        for k in range(10):
            d.rectangle([0, SB_H - 46 - k * 5, SB_W, SB_H], fill=(255, 255, 255, 10))
        for ox, oy in ((-3, 0), (3, 0), (0, -3), (0, 3), (2, 2), (-2, 2), (2, -2), (-2, -2)):
            d.text((x + ox, y + oy), text, font=f, fill=(255, 255, 255, 235))
        d.text((x, y), text, font=f, fill=(26, 26, 26, 255))
    else:
        for k in range(10):
            d.rectangle([0, SB_H - 46 - k * 5, SB_W, SB_H], fill=(0, 0, 0, 13))
        for ox, oy in ((-4, 0), (4, 0), (0, -4), (0, 4), (-3, 3), (3, 3), (-3, -3), (3, -3)):
            d.text((x + ox, y + oy), text, font=f, fill=(6, 8, 12, 255))
        d.text((x, y + 4), text, font=f, fill=(0, 0, 0, 130))
        d.text((x, y), text, font=f, fill=(255, 255, 255, 255))
    im.save(path)
    return im


def main():
    tl = json.load(open(os.path.join(BASE, "timeline30.json"), encoding="utf-8"))
    lines = tl["lines"]
    total = tl["total"]
    print(f"总长 {total}s  镜头 {len(SCENES)}  句 {len(lines)}")
    assert len(lines) == len(SCENES), "句数与镜头数不符"

    # 每镜时长 = 下一句起点 - 本句起点
    durs = []
    for i, r in enumerate(lines):
        end = lines[i + 1]["start"] if i + 1 < len(lines) else total
        durs.append(round(end - r["start"], 3))
    print("镜头时长:", [round(d, 1) for d in durs], "合计", round(sum(durs), 2))

    # 字幕
    LIGHT_SCENES = {"F01Opening"}          # 白底场景，字幕走深色
    sub_imgs = []
    for i, r in enumerate(lines):
        p = os.path.join(TMP, f"s{i+1:02d}.png")
        dark = SCENES[i].__name__ in LIGHT_SCENES
        sub_imgs.append(make_sub(r["text"], p, dark=dark))
        r["end"] = round(r["start"] + r["dur"] + 0.30, 3)

    # 逐帧渲染
    raw = os.path.join(TMP, "raw.mp4")
    cmd = [FF, "-y", "-loglevel", "error", "-f", "rawvideo", "-pix_fmt", "rgb24",
           "-s", f"{W}x{H}", "-r", str(FPS), "-i", "-",
           "-c:v", "libx264", "-crf", "18", "-preset", "veryfast",
           "-pix_fmt", "yuv420p", "-an", raw]
    errlog = os.path.join(TMP, "enc.log")
    errf = open(errlog, "wb")
    p = subprocess.Popen(cmd, stdin=subprocess.PIPE, stdout=subprocess.DEVNULL, stderr=errf)
    canvas = Image.new("RGBA", (W, H), (0, 0, 0, 255))
    t0 = time.time()
    frame_no = 0
    for si, (cls, dur) in enumerate(zip(SCENES, durs)):
        inst = cls()
        n = max(2, round(dur * FPS))
        for k in range(n):
            tnorm = k / (n - 1) if n > 1 else 1.0
            inst.render(canvas, tnorm)
            gt = frame_no / FPS
            for r, sim in zip(lines, sub_imgs):
                if r["start"] - 0.06 <= gt <= r["end"]:
                    canvas.alpha_composite(sim, (0, 760))
                    break
            p.stdin.write(canvas.convert("RGB").tobytes())
            canvas.paste((0, 0, 0, 255), (0, 0, W, H))
            frame_no += 1
        print(f"  {si+1:2d} {cls.__name__:16s} {dur:5.2f}s {n:4d}f")
    p.stdin.close()
    p.wait()
    errf.close()
    if p.returncode != 0:
        print(open(errlog, encoding="utf-8", errors="replace").read()[-2000:])
        sys.exit(1)
    print(f"逐帧完成 {frame_no} 帧 {time.time()-t0:.1f}s")

    # 音频 + 封装
    auds = [os.path.join(AUD, f"{i+1:02d}c.mp3") for i in range(len(lines))]
    missing = [a for a in auds if not os.path.exists(a)]
    if missing:
        print("缺音频:", missing[:3])
        sys.exit(1)
    cmd = [FF, "-y", "-loglevel", "error", "-i", raw]
    for a in auds:
        cmd += ["-i", a]
    bgm = os.path.join(BASE, "bgm", "mix.wav")
    has_bgm = os.path.exists(bgm)
    if has_bgm:
        cmd += ["-i", bgm]
    fc = []
    for i, r in enumerate(lines):
        ms = int(round(r["start"] * 1000))
        fc.append(f"[{i+1}:a]adelay={ms}|{ms}[a{i}]")
    mix = "".join(f"[a{i}]" for i in range(len(lines)))
    if has_bgm:
        bi = len(lines) + 1
        fc.append(f"{mix}amix=inputs={len(lines)}:normalize=0:duration=longest,asplit[vA][vB]")
        # BGM 被侧链压缩：人声一出现自动退让，出画再抬回来
        fc.append(f"[{bi}:a]atrim=0:{total},volume=1.9[bg0]")
        fc.append(f"[bg0][vB]sidechaincompress=threshold=0.055:ratio=5:attack=12:release=320[bgd]")
        fc.append(f"[vA][bgd]amix=inputs=2:weights=1 0.62:normalize=0:duration=longest,"
                  f"apad=whole_dur={total},atrim=0:{total},asetpts=N/SR/TB[aout]")
    else:
        fc.append(f"{mix}amix=inputs={len(lines)}:normalize=0:duration=longest,"
                  f"apad=whole_dur={total},atrim=0:{total},asetpts=N/SR/TB[aout]")
    cmd += ["-filter_complex", ";".join(fc), "-map", "0:v", "-map", "[aout]",
            "-t", f"{total}", "-r", str(FPS),
            "-c:v", "copy", "-c:a", "aac", "-b:a", "192k", "-ar", "48000",
            "-movflags", "+faststart", OUT]
    r = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", errors="replace")
    if r.returncode != 0:
        print((r.stderr or "")[-2000:])
        sys.exit(1)
    print("出片:", OUT)
    info = subprocess.run([FP, "-v", "error", "-show_entries",
                           "format=duration,size:stream=codec_name,width,height,r_frame_rate",
                           "-of", "default=nw=1", OUT], capture_output=True, text=True)
    print(info.stdout.strip())


if __name__ == "__main__":
    main()
