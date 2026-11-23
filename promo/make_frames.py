# -*- coding: utf-8 -*-
"""必应壁纸编年史 · 30s animatic 静帧生成器 v2
版式规则（硬约束）：
  - 主体信息区 y ∈ [110, 700]，避开底部字幕带
  - 字幕带 y ∈ [760, 980] 由渲染管线 overlay 烧入，静帧不得占用
  - 品牌角标固定右上 (1812, 58)，右对齐
  - 画面文字只给数据与视觉，不与口播逐字重复
"""
import os, math
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageEnhance

BASE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(BASE, "screens")
IMG = r"D:\Tools\bing-wallpaper\archive\images"
os.makedirs(OUT, exist_ok=True)

W, H = 1920, 1080
FB = r"C:\Windows\Fonts\Noto Sans SC Bold (TrueType).otf"
FR = r"C:\Windows\Fonts\Noto Sans SC Medium (TrueType).otf"

BG_DARK = (9, 12, 18)
WHITE = (255, 255, 255)
ACCENT = (86, 182, 255)
MUTED = (152, 164, 182)
WARN = (255, 92, 92)
GOLD = (255, 199, 102)
SAFE_B = 700          # 主体下限
SUB_TOP = 760         # 字幕带起点


def font(size, bold=True):
    return ImageFont.truetype(FB if bold else FR, size)


def tw(d, s, f):
    b = d.textbbox((0, 0), s, font=f)
    return b[2] - b[0], b[3] - b[1]


def new_layer():
    ov = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    return ov, ImageDraw.Draw(ov)


def finish(im, ov):
    return Image.alpha_composite(im, ov)


def text(d, xy, s, f, fill=WHITE, anchor="la", shadow=True, sh=(0, 4)):
    if shadow:
        d.text((xy[0] + sh[0], xy[1] + sh[1]), s, font=f, fill=(0, 0, 0, 185), anchor=anchor)
    d.text(xy, s, font=f, fill=fill, anchor=anchor)


def load_bg(name, darken=0.42, blur=0, gray=False):
    p = name if os.path.isabs(name) else os.path.join(IMG, name)
    im = Image.open(p).convert("RGB")
    s = max(W / im.width, H / im.height)
    im = im.resize((int(im.width * s + 1), int(im.height * s + 1)), Image.LANCZOS)
    l, t = (im.width - W) // 2, (im.height - H) // 2
    im = im.crop((l, t, l + W, t + H))
    if blur:
        im = im.filter(ImageFilter.GaussianBlur(blur))
    if gray:
        im = ImageEnhance.Color(im).enhance(0.05)
    im = ImageEnhance.Brightness(im).enhance(darken)
    return im.convert("RGBA")


def vignette(im, strength=165):
    ov = Image.new("L", (W, H), 0)
    d = ImageDraw.Draw(ov)
    for i in range(60):
        a = int(strength * (i / 60) ** 2.1)
        d.rectangle([i * 12, i * 7, W - i * 12, H - i * 7], outline=a, width=14)
    grad = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    grad.putalpha(ov.filter(ImageFilter.GaussianBlur(38)))
    return Image.alpha_composite(im, grad)


def scrim(im, top=0.0, bottom=0.86):
    ov = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(ov)
    for y in range(H):
        a = int(255 * (top + (bottom - top) * ((y / H) ** 1.35)))
        d.line([(0, y), (W, y)], fill=(4, 6, 10, a))
    return Image.alpha_composite(im, ov)


def brand(ov):
    d = ImageDraw.Draw(ov)
    d.text((1812, 58), "必应壁纸编年史  ·  v3.6.56", font=font(28), fill=(190, 200, 214, 190), anchor="ra")


def badge(d, xy, s):
    f = font(28)
    w, _ = tw(d, s, f)
    x, y = xy
    d.rounded_rectangle([x, y, x + w + 36, y + 52], radius=9, fill=(255, 92, 92, 228))
    d.text((x + 18, y + 9), s, font=f, fill=WHITE)


def rrect(d, box, r, fill=None, outline=None, wdt=3):
    d.rounded_rectangle(box, radius=r, fill=fill, outline=outline, width=wdt)


# ---------- 视觉件 ----------
def timeline(d, x0, x1, y, years, lit, labels=True):
    d.line([(x0, y), (x1, y)], fill=(255, 255, 255, 46), width=3)
    n = len(years)
    for i, yr in enumerate(years):
        x = x0 + (x1 - x0) * i / (n - 1)
        on = yr in lit
        col = ACCENT if on else (255, 255, 255, 62)
        h = 24 if on else 11
        d.line([(x, y - h // 2), (x, y + h // 2)], fill=col, width=5 if on else 2)
        if labels:
            d.text((x, y + 26), str(yr), font=font(25 if on else 19),
                   fill=ACCENT if on else (255, 255, 255, 88), anchor="ma")


def feather(d, cx, cy, scale=1.0, color=(255, 255, 255, 232)):
    L = int(470 * scale)
    d.line([(cx - L * 0.42, cy - L * 0.30), (cx + L * 0.30, cy + L * 0.42)], fill=color, width=max(2, int(7 * scale)))
    N = 28
    for i in range(N):
        t = i / (N - 1)
        px = cx - L * 0.42 + L * 0.72 * t
        py = cy - L * 0.30 + L * 0.72 * t
        half = math.sin(math.pi * t) * L * 0.19 + 3
        for sgn in (-1, 1):
            d.line([(px, py), (px - half * 0.707 * sgn, py + half * 0.707 * sgn)],
                   fill=color, width=max(1, int(4.2 * scale)))
    mx, my = cx - L * 0.06, cy + L * 0.06
    d.ellipse([mx - 6, my - 6, mx + 6, my + 6], fill=color)


def qr_code(size, data):
    try:
        import qrcode
        q = qrcode.QRCode(box_size=4, border=1)
        q.add_data(data)
        q.make(fit=True)
        return q.make_image(fill_color="black", back_color="white").convert("RGB").resize((size, size), Image.NEAREST)
    except Exception:
        im = Image.new("RGB", (size, size), "white")
        d = ImageDraw.Draw(im)
        d.rectangle([0, 0, size - 1, size - 1], outline="black", width=6)
        d.text((size / 2, size / 2), "QR", font=font(int(size * .2)), fill=(0, 0, 0), anchor="mm")
        return im


def save(img, name):
    img.convert("RGB").save(os.path.join(OUT, name), "PNG", optimize=True)
    print("  ->", name)


YEARS = list(range(2009, 2027))

# ---------- 14 张 ----------
def s01():
    """钩子 A：时间轴起点，只说接口窗口有多短"""
    im = vignette(scrim(load_bg("20161122_OHR.KuanmiaoNoodles.jpg", darken=0.28, blur=2), 0.34, 0.92))
    ov, d = new_layer()
    d.text((96, 130), "官方接口窗口", font=font(40), fill=MUTED, )
    text(d, (92, 178), "最近 8 天", font(112))
    text(d, (96, 320), "第 9 天起，官方那边就没有了", font(44), MUTED)
    timeline(d, 96, W - 96, 600, YEARS, {2009, 2026}, labels=False)
    text(d, (96, 520), "2009", font(34), ACCENT)
    text(d, (W - 96, 520), "2026", font(34), ACCENT, anchor="ra")
    brand(ov); save(finish(im, ov), "s01.png")


def s02():
    """钩子 B：17 年跨度铺满"""
    im = vignette(scrim(load_bg("20220424_OHR.YosemiteNightSky.jpg", darken=0.32, blur=1), 0.32, 0.90))
    ov, d = new_layer()
    timeline(d, 96, W - 96, 580, YEARS, set(YEARS), labels=True)
    text(d, (96, 180), "17 年", font(132))
    text(d, (100, 348), "2009-07-13 → 2026-11-22", font(42), MUTED)
    brand(ov); save(finish(im, ov), "s02.png")


def s03():
    """钩子 C：6339 数字砸下来"""
    im = scrim(Image.new("RGBA", (W, H), BG_DARK + (255,)), 0.0, 0.55)
    ov, d = new_layer()
    d.ellipse([W - 480, -300, W + 300, 480], fill=(86, 182, 255, 30))
    d.text((W // 2, 400), "6339", font=font(400), fill=WHITE, anchor="mm")
    text(d, (W // 2, 672), "天，一天不缺", font(88), ACCENT, anchor="mm")
    brand(ov); save(finish(im, ov), "s03.png")


def s04():
    """痛点 A：弹窗实物 + 问题清单"""
    im = vignette(scrim(load_bg("20261122_OHR.JustaHermitage.jpg", darken=0.30, blur=6), 0.30, 0.80))
    ov, d = new_layer()
    rrect(d, [1090, 150, 1780, 410], 18, fill=(248, 250, 253, 242), outline=(200, 208, 220, 255), wdt=2)
    d.rectangle([1090, 150, 1780, 202], fill=(232, 238, 246, 255))
    for i, c in enumerate([(255, 95, 86), (255, 189, 46), (39, 201, 63)]):
        d.ellipse([1108 + i * 26, 166, 1124 + i * 26, 182], fill=c)
    d.text((1130, 230), "必应壁纸助手", font=font(32), fill=(28, 34, 44))
    d.text((1130, 278), "今日美图已更新，点击查看 →", font=font(26), fill=(90, 100, 116))
    rrect(d, [1130, 330, 1272, 376], 10, fill=(0, 103, 192, 255))
    d.text((1201, 353), "立即查看", font=font(22), fill=WHITE, anchor="mm")
    badge(d, (1090, 426), "模拟演示 · 非真实品牌")

    text(d, (96, 190), "装完之后", font(74), MUTED)
    items = [("3", "个推图弹窗"), ("1", "个托盘常驻图标"), ("0", "个「另存为」")]
    for i, (num, lab) in enumerate(items):
        y = 292 + i * 96
        text(d, (96, y), num, font(72), WARN if i == 2 else WHITE)
        text(d, (96 + 62, y + 22), lab, font(38), MUTED, shadow=False)
    brand(ov); save(finish(im, ov), "s04.png")


def s05():
    """痛点 B：昨天 vs 今天"""
    im = vignette(scrim(load_bg("20261121_OHR.RedNice.jpg", darken=0.34, gray=True, blur=3), 0.30, 0.92))
    ov, d = new_layer()
    d.line([(W // 2, 120), (W // 2, 660)], fill=(255, 255, 255, 26), width=2)
    text(d, (W // 4, 200), "昨天", font(58), MUTED, anchor="ma")
    text(d, (W * 3 // 4, 200), "今天", font(58), WARN, anchor="ma")
    rrect(d, [W // 4 - 200, 290, W // 4 + 200, 590], 8, outline=(255, 255, 255, 55), wdt=3)
    d.line([(W // 4 - 220, 290), (W // 4 + 220, 590)], fill=(255, 92, 92, 200), width=8)
    rrect(d, [W * 3 // 4 - 200, 290, W * 3 // 4 + 200, 590], 8, outline=ACCENT, wdt=3)
    text(d, (W // 4, 540), "已消失", font(46), WARN, anchor="ma")
    text(d, (W * 3 // 4, 540), "今日一张", font(46), ACCENT, anchor="ma")
    brand(ov); save(finish(im, ov), "s05.png")


def s06():
    """痛点 C：8 vs 6339"""
    im = scrim(Image.new("RGBA", (W, H), BG_DARK + (255,)), 0.0, 0.5)
    ov, d = new_layer()
    d.text((96, 120), "官方窗口  vs  本地档案", font=font(46), fill=MUTED)
    rrect(d, [96, 210, 620, 450], 16, fill=(255, 92, 92, 42), outline=(255, 92, 92, 200), wdt=3)
    d.text((358, 330), "8", font=font(150), fill=WARN, anchor="mm")
    d.text((358, 418), "天", font=font(36), fill=WARN, anchor="mm")
    d.text((96, 478), "接口上限", font=font(30), fill=MUTED)
    rrect(d, [720, 210, 1824, 450], 16, fill=(86, 182, 255, 32), outline=ACCENT, wdt=3)
    d.text((1272, 330), "6339", font=font(160), fill=WHITE, anchor="mm")
    d.text((1272, 424), "天", font=font(36), fill=ACCENT, anchor="mm")
    d.text((720, 478), "本地索引 · 全量可翻", font=font(30), fill=MUTED)
    bx0, bx1, by = 96, 1824, 590
    d.line([(bx0, by), (bx1, by)], fill=ACCENT, width=8)
    d.line([(bx0, by), (bx0 + 26, by)], fill=WARN, width=14)
    d.text((96, 616), "8 天", font=font(28), fill=WARN)
    d.text((1824, 616), "6339 天", font=font(28), fill=ACCENT, anchor="ra")
    brand(ov); save(finish(im, ov), "s06.png")


def s07():
    """产品：今日壁纸详情卡（y 压到 700 以内）"""
    im = vignette(scrim(load_bg("20261122_OHR.JustaHermitage.jpg", darken=0.60, blur=10), 0.22, 0.86))
    ov, d = new_layer()
    rrect(d, [150, 96, 1770, 700], 20, fill=(18, 23, 33, 236), outline=(255, 255, 255, 42), wdt=2)
    thumb = load_bg("20261122_OHR.JustaHermitage.jpg", darken=1.0).crop((180, 140, 1060, 660)).resize((720, 480), Image.LANCZOS)
    ov.paste(thumb, (186, 136))
    d.rectangle([186, 136, 906, 616], outline=(255, 255, 255, 48), width=2)
    x = 966
    d.text((x, 150), "2026-11-22", font=font(32), fill=ACCENT)
    text(d, (x, 200), "海浪间的庇护所", font(60))
    text(d, (x, 288), "西班牙坎塔布里亚自治区", font(34), MUTED)
    text(d, (x, 330), "圣胡斯塔隐修院与小教堂", font(34), MUTED)
    d.line([(x, 392), (1730, 392)], fill=(255, 255, 255, 32), width=2)
    for i, ln in enumerate(["潮水在礁石之间涨落，一座小教堂立在海岸上。",
                            "每天只在这个窗口里存在一天。"]):
        text(d, (x, 418 + i * 48), ln, font(32), (226, 232, 240), shadow=False)
    rrect(d, [x, 566, x + 226, 626], 12, fill=(0, 103, 192, 255))
    d.text((x + 113, 596), "设为壁纸", font=font(30), fill=WHITE, anchor="mm")
    rrect(d, [x + 250, 566, x + 476, 626], 12, outline=(255, 255, 255, 90), wdt=2)
    d.text((x + 363, 596), "收进收藏", font=font(30), fill=WHITE, anchor="mm")
    brand(ov); save(finish(im, ov), "s07.png")


def s08():
    """产品：羽毛 + 品牌"""
    im = scrim(Image.new("RGBA", (W, H), BG_DARK + (255,)), 0.0, 0.45)
    ov, d = new_layer()
    d.ellipse([170, 120, 730, 680], fill=(86, 182, 255, 32))
    feather(d, 450, 400, 0.92, (255, 255, 255, 230))
    text(d, (850, 250), "必年代记", font(132))
    d.text((856, 400), "HISTORY OF BING WALLPAPER", font=font(28), fill=ACCENT)
    text(d, (856, 470), "羽毛 = 每天那张图", font(52), MUTED)
    text(d, (856, 546), "册子 = 编年史", font(52), MUTED)
    brand(ov); save(finish(im, ov), "s08.png")


def s09():
    """演示 A：时间轴控件"""
    im = vignette(scrim(load_bg("20261114_OHR.BlueHK.jpg", darken=0.32, blur=2), 0.28, 0.90))
    ov, d = new_layer()
    text(d, (96, 130), "时间轴", font(48), MUTED)
    for i, (yr, xx) in enumerate([("2024", 340), ("2025", 760), ("2026", 1180)]):
        act = yr == "2025"
        rrect(d, [xx, 210, xx + 260, 320], 14,
              fill=(86, 182, 255, 52) if act else None,
              outline=ACCENT if act else (255, 255, 255, 70), wdt=3)
        d.text((xx + 130, 265), yr, font=font(56 if act else 44),
               fill=WHITE if act else MUTED, anchor="mm")
    d.polygon([(1560, 265), (1618, 236), (1618, 294)], fill=ACCENT)
    d.polygon([(1676, 265), (1618, 236), (1618, 294)], fill=ACCENT)
    x0, x1, y = 140, W - 140, 470
    d.line([(x0, y), (x1, y)], fill=(255, 255, 255, 40), width=3)
    for i in range(12):
        x = x0 + (x1 - x0) * i / 11
        on = i == 10
        d.line([(x, y - 18), (x, y + 18)], fill=ACCENT if on else (255, 255, 255, 60), width=6 if on else 2)
        d.text((x, y + 34), f"{i+1}月", font=font(24 if on else 18),
               fill=ACCENT if on else (255, 255, 255, 88), anchor="ma")
    text(d, (96, 590), "← 昨天     ↑ 上个月", font(44), ACCENT)
    brand(ov); save(finish(im, ov), "s09.png")


def s10():
    """演示 B：往年的今天"""
    im = vignette(scrim(load_bg(os.path.join(OUT, "ammonite.jpg"), darken=0.72), 0.26, 0.92))
    ov, d = new_layer()
    d.text((96, 140), "2025-11-23", font=font(42), fill=ACCENT)
    text(d, (92, 200), "大自然的秘密代码", font(88))
    text(d, (96, 320), "马达加斯加菊石化石", font(40), MUTED)
    d.line([(96, 404), (700, 404)], fill=(255, 199, 102, 210), width=5)
    d.text((96, 432), "往年的今天 · 第 327 天", font=font(32), fill=GOLD)
    brand(ov); save(finish(im, ov), "s10.png")


def s11():
    """演示 C：离线搜索"""
    im = vignette(scrim(load_bg("20261101_OHR.SchwarzwaldAutumn.jpg", darken=0.28, blur=12), 0.28, 0.86))
    ov, d = new_layer()
    d.text((96, 120), "离线可搜 · 6339 条本地索引", font=font(42), fill=MUTED)
    rrect(d, [96, 190, 1200, 288], 16, fill=(18, 23, 33, 242), outline=ACCENT, wdt=3)
    d.text((128, 239), "菊石", font=font(48), fill=WHITE, anchor="lm")
    d.line([(1120, 215), (1120, 263)], fill=ACCENT, width=3)
    d.text((96, 306), "命中 6 条  ·  断网也能搜", font=font(28), fill=ACCENT)
    rows = [("2025-11-23", "大自然的秘密代码", "马达加斯加菊石化石"),
            ("2023-08-14", "螺旋的时间", "侏罗纪海岸的菊石"),
            ("2019-04-02", "化石里的海", "多塞特郡海岸线")]
    for i, (dt, t1, t2) in enumerate(rows):
        y = 370 + i * 110
        rrect(d, [96, y, 1400, y + 94], 12, fill=(86, 182, 255, 46) if i == 0 else (255, 255, 255, 16))
        d.text((128, y + 26), dt, font=font(30), fill=ACCENT)
        d.text((390, y + 20), t1, font=font(34), fill=WHITE)
        d.text((390, y + 60), t2, font=font(26), fill=MUTED)
    brand(ov); save(finish(im, ov), "s11.png")


def s12():
    """CTA A：仓库页"""
    im = scrim(Image.new("RGBA", (W, H), BG_DARK + (255,)), 0.0, 0.45)
    ov, d = new_layer()
    rrect(d, [120, 110, 1800, 690], 18, fill=(22, 27, 34, 246), outline=(255, 255, 255, 42), wdt=2)
    d.rectangle([120, 110, 1800, 186], fill=(30, 36, 45, 255))
    for i, c in enumerate([(255, 95, 86), (255, 189, 46), (39, 201, 63)]):
        d.ellipse([148 + i * 28, 136, 166 + i * 28, 154], fill=c)
    d.text((300, 130), "github.com/zkyhzxf/bing-wallpaper-chronicle", font=font(28), fill=MUTED)
    text(d, (160, 222), "zkyhzxf / bing-wallpaper-chronicle", font(48))
    text(d, (160, 300), "必应壁纸编年史 · 6339 天档案，一天不缺", font(32), MUTED)
    rrect(d, [1420, 212, 1770, 282], 12, fill=(35, 134, 54, 255))
    d.text((1490, 247), "★  Star", font=font(32), fill=WHITE, anchor="lm")
    d.text((1592, 247), "1.2k", font=font(28), fill=(200, 255, 210), anchor="lm")
    tags = [("PowerShell", ACCENT), ("Windows", (140, 200, 255)), ("MIT", (255, 199, 102))]
    x = 160
    for t, c in tags:
        w, _ = tw(d, t, font(24))
        rrect(d, [x, 366, x + w + 40, 418], 22, fill=c + (42,), outline=c + (165,), wdt=2)
        d.text((x + 20, 392), t, font=font(24), fill=c, anchor="lm")
        x += w + 58
    text(d, (160, 460), "免费开源 · 解压即用 · 不用注册", font(36))
    d.text((160, 530), "v3.6.56   ·   102 项回归断言全绿   ·   MIT", font=font(28), fill=MUTED)
    brand(ov); save(finish(im, ov), "s12.png")


def s13():
    """CTA B：Star = 收藏"""
    im = scrim(Image.new("RGBA", (W, H), BG_DARK + (255,)), 0.0, 0.45)
    ov, d = new_layer()
    d.text((W // 2, 190), "Star 不是打赏", font=font(80), fill=MUTED, anchor="mm")
    d.text((W // 2, 312), "是收藏", font=font(140), fill=WHITE, anchor="mm")
    rrect(d, [W // 2 - 290, 430, W // 2 + 290, 566], 24, fill=(35, 134, 54, 255))
    d.text((W // 2, 498), "★  Star", font=font(60), fill=WHITE, anchor="mm")
    d.text((W // 2, 620), "点一下，相当于给这个项目留个书签", font=font(40), fill=ACCENT, anchor="mm")
    brand(ov); save(finish(im, ov), "s13.png")


def s14():
    """CTA C：二维码 + 片名"""
    im = vignette(scrim(load_bg("20261115_OHR.FirstFrost.jpg", darken=0.26, blur=4), 0.34, 0.94))
    ov, d = new_layer()
    q = qr_code(250, "https://github.com/zkyhzxf/bing-wallpaper-chronicle")
    card = Image.new("RGBA", (286, 286), (255, 255, 255, 255))
    card.paste(q, (18, 18))
    im.paste(card, (1490, 330), card)
    d.text((1490, 292), "扫码打开仓库", font=font(28), fill=WHITE)
    text(d, (108, 210), "必年代记", font(160))
    text(d, (112, 410), "把必应每天散落的那张图，捡回来", font(48))
    d.line([(112, 500), (900, 500)], fill=ACCENT, width=5)
    d.text((112, 532), "免费开源  ·  解压即用  ·  不用注册", font=font(32), fill=MUTED)
    brand(ov); save(finish(im, ov), "s14.png")


if __name__ == "__main__":
    print("生成静帧 ->", OUT)
    for fn in [s01, s02, s03, s04, s05, s06, s07, s08, s09, s10, s11, s12, s13, s14]:
        fn()
    print("done:", len([f for f in os.listdir(OUT) if f.startswith("s") and f.endswith(".png")]), "frames")
