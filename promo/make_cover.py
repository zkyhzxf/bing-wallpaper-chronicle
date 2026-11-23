# -*- coding: utf-8 -*-
"""B站封面 1146x717：大字 6339 + 时间轴 + 角标"""
import os
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageEnhance

BASE = os.path.dirname(os.path.abspath(__file__))
IMG = r"D:\Tools\bing-wallpaper\archive\images"
FB = r"C:\Windows\Fonts\Noto Sans SC Bold (TrueType).otf"
W, H = 1146, 717


def font(s):
    return ImageFont.truetype(FB, s)


im = Image.open(os.path.join(IMG, "20220424_OHR.YosemiteNightSky.jpg")).convert("RGB")
sc = max(W / im.width, H / im.height)
im = im.resize((int(im.width * sc + 1), int(im.height * sc + 1)), Image.LANCZOS)
im = im.crop(((im.width - W) // 2, (im.height - H) // 2,
              (im.width - W) // 2 + W, (im.height - H) // 2 + H))
im = ImageEnhance.Brightness(im).enhance(0.52).convert("RGBA")

gr = Image.new("RGBA", (W, H), (0, 0, 0, 0))
gd = ImageDraw.Draw(gr)
for y in range(H):
    gd.line([(0, y), (W, y)], fill=(4, 6, 12, int(200 * (0.15 + 0.85 * (y / H) ** 1.2))))
im = Image.alpha_composite(im, gr)

ov = Image.new("RGBA", (W, H), (0, 0, 0, 0))
d = ImageDraw.Draw(ov)

# 时间轴
x0, x1, yy = 62, W - 62, 626
d.line([(x0, yy), (x1, yy)], fill=(255, 255, 255, 60), width=3)
for i in range(18):
    x = x0 + (x1 - x0) * i / 17
    d.line([(x, yy - 11), (x, yy + 11)], fill=(86, 182, 255, 200), width=3)

# 主标题
d.text((60, 44), "17 年 · 6339 天", font=font(40), fill=(152, 164, 182, 255))
d.text((54, 92), "6339", font=font(196), fill=(255, 255, 255, 255),
       stroke_width=0)
d.text((60, 336), "天，一天不缺", font=font(62), fill=(86, 182, 255, 255))
d.text((60, 420), "必应壁纸编年史  ·  可翻 可搜 可考古", font=font(30), fill=(226, 232, 240, 235))

# 右上角标
badge_f = font(26)
for i, (txt, c) in enumerate([("3 天写的", (255, 199, 102)), ("免费开源", (140, 230, 170))]):
    bb = d.textbbox((0, 0), txt, font=badge_f)
    w = bb[2] - bb[0]
    x = W - 54 - w - 32
    y = 40 + i * 58
    d.rounded_rectangle([x, y, W - 54, y + 48], radius=10, fill=(10, 14, 22, 220), outline=c + (220,), width=2)
    d.text((x + 16, y + 9), txt, font=badge_f, fill=c)

out = Image.alpha_composite(im, ov).convert("RGB")
p = os.path.join(BASE, "cover.png")
out.save(p, "PNG", optimize=True)
print("cover ->", p, out.size)
