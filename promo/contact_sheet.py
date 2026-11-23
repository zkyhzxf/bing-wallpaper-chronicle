# -*- coding: utf-8 -*-
"""联系表：每个 Scene 取 3 个时间点渲染，竖排拼成一张检查图。
用法：python contact_sheet.py [场景序号...]
"""
import os, sys, time
from PIL import Image, ImageDraw, ImageFont
import scenes

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "_sheet")
os.makedirs(OUT, exist_ok=True)
TS = [0.18, 0.55, 0.90]
TILE = (640, 360)
F = r"C:\Windows\Fonts\Noto Sans SC Bold (TrueType).otf"


def label(im, s):
    d = ImageDraw.Draw(im)
    d.rectangle([0, 0, 200, 40], fill=(0, 0, 0, 210))
    d.text((10, 6), s, font=ImageFont.truetype(F, 24), fill=(255, 255, 255))


def main():
    idx = [int(x) - 1 for x in sys.argv[1:]] or range(len(scenes.SCENES))
    for i in idx:
        cls = scenes.SCENES[i]
        t0 = time.time()
        sc = cls()
        canvas = Image.new("RGBA", (scenes.W, scenes.H), (0, 0, 0, 255))
        frames = []
        for tt in TS:
            sc.render(canvas, tt)
            frames.append(canvas.convert("RGB").resize(TILE, Image.LANCZOS))
        sheet = Image.new("RGB", (TILE[0] * 3 + 16, TILE[1] + 48), (20, 20, 20))
        for k, fr in enumerate(frames):
            label(fr, f"{cls.__name__}  t={TS[k]}")
            sheet.paste(fr, (k * (TILE[0] + 8), 44))
        sheet.save(os.path.join(OUT, f"scene_{i+1:02d}.png"))
        print(f"{i+1:02d} {cls.__name__:14s} build+3frames {time.time()-t0:.2f}s")


if __name__ == "__main__":
    main()
