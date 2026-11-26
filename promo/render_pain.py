# -*- coding: utf-8 -*-
"""痛点段独立渲染：只出证据那 23.5 秒，用来快速看节奏与打码效果。
用法：python render_pain.py [--audio]
"""
import os, subprocess, sys, time
from PIL import Image, ImageDraw, ImageFont

import scenes
import evidence_scenes as E

W, H, FPS = 1920, 1080, 30
FF = r"D:\tools\ffmpeg\ffmpeg.exe"
BASE = os.path.dirname(os.path.abspath(__file__))
TMP = os.path.join(BASE, "_pain")
OUT = os.path.join(BASE, "out", "pain-segment.mp4")
FONT_B = r"C:\Windows\Fonts\Noto Sans SC Bold (TrueType).otf"
os.makedirs(TMP, exist_ok=True)
os.makedirs(os.path.dirname(OUT), exist_ok=True)

# (场景类, 时长秒, [(起, 止, 字幕)])
SHOTS = [
    (E.EP1Campaign,   3.0, [(0.30, 2.85, "很多人是冲着大会员装的吧？")]),
    (E.EP2AdFull,     3.5, [(0.45, 3.35, "甚至把广告当成了壁纸。")]),
    (E.EP3Press,      3.5, []),
    (E.EP4TopRight,   5.0, [(0.35, 4.70, "关不掉的浮窗。")]),
    (E.EP5Eight,      4.5, [(0.40, 4.20, "昨天的图，今天就没了。")]),
    (E.EP6ThreeGone,  4.0, [(0.25, 1.95, "想找上周那张雪山，翻不到；"),
                            (1.95, 3.75, "想找去年冬天那张海，也翻不到。")]),
]
TOTAL = sum(s[1] for s in SHOTS)


def make_sub_png(text, path):
    SB_W, SB_H = 1920, 220
    f = ImageFont.truetype(FONT_B, 54)
    im = Image.new("RGBA", (SB_W, SB_H), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    b = d.textbbox((0, 0), text, font=f)
    x = (SB_W - (b[2] - b[0])) // 2
    y = (SB_H - (b[3] - b[1])) // 2 - b[1]
    for k in range(10):
        d.rectangle([0, SB_H - 46 - k * 5, SB_W, SB_H], fill=(0, 0, 0, 14))
    for ox, oy in ((-4, 0), (4, 0), (0, -4), (0, 4), (-3, 3), (3, 3), (-3, -3), (3, -3)):
        d.text((x + ox, y + oy), text, font=f, fill=(6, 8, 12, 255))
    d.text((x, y + 4), text, font=f, fill=(0, 0, 0, 130))
    d.text((x, y), text, font=f, fill=(255, 255, 255, 255))
    im.save(path)
    return path


def main():
    # 1. 逐帧渲染
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
    n_all = 0
    for cls, dur, _ in SHOTS:
        inst = cls()
        n = max(2, round(dur * FPS))
        n_all += n
        for k in range(n):
            inst.render(canvas, k / (n - 1) if n > 1 else 1.0)
            p.stdin.write(canvas.convert("RGB").tobytes())
            canvas.paste((0, 0, 0, 255), (0, 0, W, H))
        print(f"  {cls.__name__:14s} {dur:4.1f}s  {n:4d}f")
    p.stdin.close()
    p.wait()
    errf.close()
    if p.returncode != 0:
        print(open(errlog, encoding="utf-8", errors="replace").read()[-1500:])
        sys.exit(1)
    print(f"逐帧完成 {n_all} 帧 {time.time()-t0:.1f}s")

    # 2. 字幕
    subs = []
    pos = 0.0
    for cls, dur, items in SHOTS:
        for a, b, txt in items:
            subs.append((pos + a, pos + b, txt))
        pos += dur
    pngs = [make_sub_png(t, os.path.join(TMP, f"sub_{i:02d}.png")) for i, (_, _, t) in enumerate(subs, 1)]

    cmd = [FF, "-y", "-loglevel", "error", "-i", raw]
    for pth in pngs:
        cmd += ["-loop", "1", "-framerate", "2", "-i", pth]
    fc = []
    cur = "[0:v]"
    for i, (a, b, _) in enumerate(subs):
        nxt = f"[v{i+1}]" if i < len(subs) - 1 else "[vout]"
        fc.append(f"{cur}[{i+1}:v]overlay=x=0:y=760:enable='between(t,{a:.3f},{b:.3f})':eof_action=repeat{nxt}")
        cur = nxt
    cmd += ["-filter_complex", ";".join(fc), "-map", "[vout]",
            "-t", f"{TOTAL}", "-r", str(FPS),
            "-c:v", "libx264", "-crf", "18", "-preset", "medium",
            "-pix_fmt", "yuv420p", "-an", OUT]
    r = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", errors="replace")
    if r.returncode != 0:
        print((r.stderr or "")[-1500:])
        sys.exit(1)
    print("出片:", OUT)

    info = subprocess.run([r"D:\tools\ffmpeg\ffprobe.exe", "-v", "error",
                           "-show_entries", "format=duration,size", "-of", "default=nw=1", OUT],
                          capture_output=True, text=True)
    print(info.stdout.strip())


if __name__ == "__main__":
    main()
