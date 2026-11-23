# -*- coding: utf-8 -*-
"""逐帧动画引擎：每个镜头有自己的运动语言，不再全场统一缓推。
每个 Scene 在 __init__ 里预渲染好图层，render(t) 只做几何与合成，保证 900 帧在秒级跑完。
"""
import math
import os
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageEnhance

W, H = 1920, 1080
IMG = r"D:\Tools\bing-wallpaper\archive\images"
AMMONITE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "screens", "ammonite.jpg")
FB = r"C:\Windows\Fonts\Noto Sans SC Bold (TrueType).otf"
FR = r"C:\Windows\Fonts\Noto Sans SC Medium (TrueType).otf"

DARK = (9, 12, 18)
WHITE = (255, 255, 255)
ACC = (86, 182, 255)
MUT = (152, 164, 182)
WARN = (255, 92, 92)
GOLD = (255, 199, 102)
GREEN = (35, 134, 54)


# ---------------- 缓动 ----------------
def linear(t):
    return t


def ease_out(t):
    return 1 - (1 - t) ** 3


def ease_in(t):
    return t ** 3


def ease_inout(t):
    return 3 * t * t - 2 * t * t * t


def ease_out_back(t, s=1.9):
    return 1 + (s + 1) * (t - 1) ** 3 + s * (t - 1) ** 2


def clamp01(t):
    return max(0.0, min(1.0, t))


def seg(t, a, b):
    """把全局 t 映射到 [a,b] 区间内的 0→1"""
    if b <= a:
        return 1.0 if t >= b else 0.0
    return clamp01((t - a) / (b - a))


# ---------------- 图层 ----------------
def font(size, bold=True):
    return ImageFont.truetype(FB if bold else FR, size)


def text_layer(s, f, fill=WHITE, shadow=True, pad=8):
    """把文字预渲染成紧凑 RGBA 图层，之后每帧只 paste，不做排版"""
    tmp = Image.new("RGBA", (8, 8))
    d = ImageDraw.Draw(tmp)
    b = d.textbbox((0, 0), s, font=f)
    w, h = b[2] - b[0] + pad * 2, b[3] - b[1] + pad * 2
    im = Image.new("RGBA", (w + 8, h + 8), (0, 0, 0, 0))
    dd = ImageDraw.Draw(im)
    ox, oy = pad - b[0], pad - b[1]
    if shadow:
        dd.text((ox + 2, oy + 4), s, font=f, fill=(0, 0, 0, 185))
    dd.text((ox, oy), s, font=f, fill=fill)
    return im


def place(canvas, layer, cx, cy, scale=1.0, alpha=1.0, rot=0.0, anchor="mm"):
    if alpha <= 0.004 or scale <= 0.01:
        return
    im = layer
    if abs(scale - 1.0) > 0.002:
        nw, nh = max(1, int(im.width * scale)), max(1, int(im.height * scale))
        im = im.resize((nw, nh), Image.LANCZOS)
    if abs(rot) > 0.2:
        im = im.rotate(rot, resample=Image.BICUBIC, expand=True)
    if alpha < 0.999:
        a = im.getchannel("A").point(lambda v: int(v * alpha))
        im = im.copy()
        im.putalpha(a)
    if anchor == "mm":
        x, y = int(cx - im.width / 2), int(cy - im.height / 2)
    elif anchor == "lm":
        x, y = int(cx), int(cy - im.height / 2)
    elif anchor == "rm":
        x, y = int(cx - im.width), int(cy - im.height / 2)
    else:
        x, y = int(cx), int(cy)
    canvas.alpha_composite(im, (x, y))


def rect_layer(w, h, radius=0, fill=None, outline=None, width=3):
    im = Image.new("RGBA", (max(1, w), max(1, h)), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle([0, 0, w - 1, h - 1], radius=radius, fill=fill, outline=outline, width=width)
    return im


# ---------------- 背景 ----------------
_BG_CACHE = {}


def load_photo(path, zoom_src=1.22):
    key = (path, zoom_src)
    if key in _BG_CACHE:
        return _BG_CACHE[key]
    p = path if os.path.isabs(path) else os.path.join(IMG, path)
    im = Image.open(p).convert("RGB")
    tw, th = int(W * zoom_src), int(H * zoom_src)
    s = max(tw / im.width, th / im.height)
    im = im.resize((int(im.width * s + 1), int(im.height * s + 1)), Image.LANCZOS)
    im = im.crop(((im.width - tw) // 2, (im.height - th) // 2,
                  (im.width - tw) // 2 + tw, (im.height - th) // 2 + th))
    _BG_CACHE[key] = im
    return im


def bg_view(base, zoom=1.0, px=0.0, py=0.0, bright=1.0, gray=0.0, blur=0.0):
    """从放大过的底图里裁窗口：zoom>1 推近，px/py ∈ [-1,1] 是相对可移动范围"""
    vw, vh = W / zoom, H / zoom
    cx = base.width / 2 + px * (base.width - vw) / 2
    cy = base.height / 2 + py * (base.height - vh) / 2
    x0 = max(0, min(base.width - vw, cx - vw / 2))
    y0 = max(0, min(base.height - vh, cy - vh / 2))
    im = base.crop((int(x0), int(y0), int(x0 + vw), int(y0 + vh))).resize((W, H), Image.BILINEAR)
    if blur > 0.2:
        im = im.filter(ImageFilter.GaussianBlur(blur))
    if abs(bright - 1.0) > 0.01:
        im = ImageEnhance.Brightness(im).enhance(bright)
    if gray > 0.01:
        im = ImageEnhance.Color(im).enhance(1.0 - gray)
    return im.convert("RGBA")


def scrim_layer(top=0.0, bottom=0.86, tint=(4, 6, 10)):
    key = ("scrim", top, bottom, tint)
    if key in _BG_CACHE:
        return _BG_CACHE[key]
    ov = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(ov)
    for y in range(H):
        a = int(255 * (top + (bottom - top) * ((y / H) ** 1.35)))
        d.line([(0, y), (W, y)], fill=tint + (a,))
    _BG_CACHE[key] = ov
    return ov


def vignette_layer(strength=150):
    key = ("vig", strength)
    if key in _BG_CACHE:
        return _BG_CACHE[key]
    ov = Image.new("L", (W, H), 0)
    d = ImageDraw.Draw(ov)
    for i in range(56):
        a = int(strength * (i / 56) ** 2.1)
        d.rectangle([i * 13, i * 8, W - i * 13, H - i * 8], outline=a, width=16)
    g = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    g.putalpha(ov.filter(ImageFilter.GaussianBlur(42)))
    _BG_CACHE[key] = g
    return g


def brand_layer():
    return text_layer("必应壁纸编年史  ·  v3.6.56", font(28), (190, 200, 214), shadow=False)


BR = brand_layer()


# ---------------- 场景基类 ----------------
class Scene:
    dur = 2.0

    def __init__(self):
        self.setup()

    def setup(self):
        pass

    def render(self, canvas, t):
        pass


def solid_bg(bright=1.0):
    return Image.new("RGBA", (W, H), DARK + (255,))


# ============ 1 · 官方窗口 8 天：横向缓移，文字上浮 ============
class S01Window(Scene):
    dur = 1.61

    def setup(self):
        self.base = load_photo("20161122_OHR.KuanmiaoNoodles.jpg", 1.30)
        self.scrim = scrim_layer(0.34, 0.92)
        self.vig = vignette_layer(140)
        self.lead = text_layer("官方接口窗口", font(40), MUT)
        self.big = text_layer("最近 8 天", font(112), WHITE)
        self.sub = text_layer("第 9 天起，官方那边就没有了", font(44), MUT)
        self.y09 = text_layer("2009", font(34), ACC)
        self.y26 = text_layer("2026", font(34), ACC)

    def render(self, c, t):
        bg = bg_view(self.base, 1.0, px=-0.35 + 0.35 * ease_out(t), bright=0.44)
        c.alpha_composite(bg)
        c.alpha_composite(self.scrim)
        c.alpha_composite(self.vig)
        # 时间轴：从中间向两端生长
        g = ease_out(seg(t, 0.12, 0.85))
        d = ImageDraw.Draw(c)
        x0, x1, y = 96, W - 96, 600
        xm = (x0 + x1) / 2
        if g > 0.01:
            d.line([(xm - (xm - x0) * g, y), (xm + (x1 - xm) * g, y)], fill=(255, 255, 255, 70), width=3)
            n = 18
            for i in range(n):
                xx = xm - (xm - x0) * g + (x1 - x0) * g * i / (n - 1)
                on = i in (0, n - 1)
                d.line([(xx, y - (12 if on else 5)), (xx, y + (12 if on else 5))],
                       fill=ACC if on else (255, 255, 255, 70), width=4 if on else 2)
        el = ease_out(seg(t, 0.05, 0.55))
        place(c, self.lead, 100, 152 - 14 * (1 - el), alpha=el)
        e2 = ease_out(seg(t, 0.12, 0.62))
        place(c, self.big, 100, 246 - 22 * (1 - e2), alpha=e2)
        e3 = ease_out(seg(t, 0.22, 0.72))
        place(c, self.sub, 100, 336 - 16 * (1 - e3), alpha=e3)
        e4 = ease_out(seg(t, 0.45, 0.95))
        place(c, self.y09, 108, 546, alpha=e4)
        place(c, self.y26, 1812, 546, alpha=e4, anchor="rm")
        place(c, BR, 1812, 58, anchor="rm")


# ============ 2 · 17 年：拉远 + 年份逐格点亮 ============
class S02Span(Scene):
    dur = 1.84

    def setup(self):
        self.base = load_photo("20220424_OHR.YosemiteNightSky.jpg", 1.34)
        self.scrim = scrim_layer(0.32, 0.90)
        self.vig = vignette_layer(150)
        self.big = text_layer("17 年", font(132), WHITE)
        self.sub = text_layer("2009-07-13 → 2026-11-22", font(42), MUT)

    def render(self, c, t):
        z = 1.16 - 0.16 * ease_out(t)
        c.alpha_composite(bg_view(self.base, z, bright=0.42))
        c.alpha_composite(self.scrim)
        c.alpha_composite(self.vig)
        d = ImageDraw.Draw(c)
        x0, x1, y = 96, W - 96, 580
        d.line([(x0, y), (x1, y)], fill=(255, 255, 255, 54), width=3)
        n = 18
        lit = ease_out(seg(t, 0.08, 0.90)) * (n - 1)
        for i in range(n):
            xx = x0 + (x1 - x0) * i / (n - 1)
            on = i <= lit + 0.001
            a = 255 if on else 96
            d.line([(xx, y - (24 if on else 11)), (xx, y + (24 if on else 11))],
                   fill=(ACC if on else (255, 255, 255)) + (a,), width=5 if on else 2)
            d.text((xx, y + 26), str(2009 + i), font=font(25 if on else 19),
                   fill=(ACC if on else (255, 255, 255)) + (a if on else 88,), anchor="ma")
        el = ease_out(seg(t, 0.0, 0.5))
        place(c, self.big, 100, 232 - 18 * (1 - el), alpha=el)
        e2 = ease_out(seg(t, 0.15, 0.65))
        place(c, self.sub, 100, 348, alpha=e2)
        place(c, BR, 1812, 58, anchor="rm")


# ============ 3 · 6339：砸落 + 震颤 + 光晕扩散 ============
class S03Number(Scene):
    dur = 2.30

    def setup(self):
        self.big = text_layer("6339", font(400), WHITE, shadow=False)
        self.cap = text_layer("天，一天不缺", font(88), ACC)
        self.halo = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        d = ImageDraw.Draw(self.halo)
        d.ellipse([W - 520, -320, W + 320, 520], fill=ACC + (34,))

    def render(self, c, t):
        c.alpha_composite(solid_bg())
        c.alpha_composite(self.halo)
        c.alpha_composite(scrim_layer(0.0, 0.55))
        # 砸落：1.55 → 1.0，前 55% 完成，带过冲
        pz = seg(t, 0.02, 0.55)
        z = 1.0 + 0.55 * (1 - ease_out_back(pz))
        # 落地后的 3 帧震颤，幅度衰减
        shake = 0.0
        if pz >= 0.99:
            k = seg(t, 0.55, 0.80)
            if k < 1.0:
                shake = (1 - k) ** 2 * 9
        dx = math.sin(t * 260) * shake
        dy = math.cos(t * 310) * shake * 0.6
        place(c, self.big, W // 2 + dx, 400 + dy, scale=z, alpha=clamp01(pz * 3))
        ec = ease_out(seg(t, 0.40, 0.90))
        place(c, self.cap, W // 2, 672 + 18 * (1 - ec), alpha=ec)
        place(c, BR, 1812, 58, anchor="rm")


# ============ 4 · 浮窗：从右滑入，清单逐条进 ============
class S04Popup(Scene):
    dur = 1.95

    def setup(self):
        self.base = load_photo("20261122_OHR.JustaHermitage.jpg", 1.26)
        self.scrim = scrim_layer(0.30, 0.80)
        self.vig = vignette_layer(130)
        # 浮窗整块预渲染成一张图层
        fw, fh = 690, 260
        win = Image.new("RGBA", (fw, fh), (0, 0, 0, 0))
        d = ImageDraw.Draw(win)
        d.rounded_rectangle([0, 0, fw - 1, fh - 1], radius=18, fill=(248, 250, 253, 244),
                            outline=(200, 208, 220, 255), width=2)
        d.rectangle([0, 0, fw - 1, 52], fill=(232, 238, 246, 255))
        d.rounded_rectangle([0, 0, fw - 1, 52], radius=18, fill=(232, 238, 246, 255))
        d.rectangle([0, 30, fw - 1, 52], fill=(232, 238, 246, 255))
        for i, cc in enumerate([(255, 95, 86), (255, 189, 46), (39, 201, 63)]):
            d.ellipse([18 + i * 26, 16, 34 + i * 26, 32], fill=cc)
        d.text((40, 80), "必应壁纸助手", font=font(32), fill=(28, 34, 44))
        d.text((40, 128), "今日美图已更新，点击查看 →", font=font(26), fill=(90, 100, 116))
        d.rounded_rectangle([40, 180, 182, 226], radius=10, fill=(0, 103, 192, 255))
        d.text((111, 203), "立即查看", font=font(22), fill=WHITE, anchor="mm")
        self.win = win
        self.badge = text_layer("模拟演示 · 非真实品牌", font(28), WHITE)
        self.badge_bg = rect_layer(300, 52, 9, fill=WARN + (228,))
        self.h1 = text_layer("装完之后", font(74), MUT)
        self.items = []
        for num, lab, warn in [("3", "个推图弹窗", False), ("1", "个托盘常驻图标", False), ("0", "个「另存为」", True)]:
            self.items.append((text_layer(num, font(72), WARN if warn else WHITE, shadow=False),
                               text_layer(lab, font(38), MUT, shadow=False)))

    def render(self, c, t):
        c.alpha_composite(bg_view(self.base, 1.0, px=0.12 * (1 - ease_out(t)), bright=0.40, blur=6))
        c.alpha_composite(self.scrim)
        c.alpha_composite(self.vig)
        # 浮窗从右侧 520px 外滑入，带轻微过冲
        e = ease_out_back(seg(t, 0.10, 0.48), 1.3)
        wx = 1090 + 520 * (1 - e)
        place(c, self.win, wx, 150)
        ea = ease_out(seg(t, 0.42, 0.62))
        place(c, self.badge_bg, wx + 8, 432, anchor="lm")
        place(c, self.badge, wx + 8 + 18, 436, alpha=ea)
        place(c, self.h1, 100, 210 - 12 * (1 - ease_out(seg(t, 0.0, 0.4))), alpha=ease_out(seg(t, 0.0, 0.4)))
        for i, (num, lab) in enumerate(self.items):
            a0, a1 = 0.30 + i * 0.12, 0.55 + i * 0.12
            e = ease_out(seg(t, a0, a1))
            y = 300 + i * 96 + 18 * (1 - e)
            place(c, num, 100, y, alpha=e)
            place(c, lab, 168, y + 22, alpha=e, anchor="lm")
        place(c, BR, 1812, 58, anchor="rm")


# ============ 5 · 昨天 vs 今天：分割线扫入，左侧褪色划红 ============
class S05Split(Scene):
    dur = 1.79

    def setup(self):
        self.base = load_photo("20261121_OHR.RedNice.jpg", 1.24)
        self.scrim = scrim_layer(0.30, 0.92)
        self.vig = vignette_layer(140)
        self.l_y = text_layer("昨天", font(58), MUT)
        self.r_y = text_layer("今天", font(58), WARN)
        self.l_s = text_layer("已消失", font(46), WARN)
        self.r_s = text_layer("今日一张", font(46), ACC)

    def render(self, c, t):
        c.alpha_composite(bg_view(self.base, 1.0, px=0.10 * (1 - ease_out(t)), bright=0.36, gray=0.55, blur=3))
        c.alpha_composite(self.scrim)
        c.alpha_composite(self.vig)
        d = ImageDraw.Draw(c)
        cx = W // 2
        # 分割线从顶部拖下来
        sp = ease_out(seg(t, 0.0, 0.30))
        d.line([(cx, 120), (cx, 120 + 540 * sp)], fill=(255, 255, 255, 40), width=2)
        # 两侧框：左框先亮后暗，右框后来才亮
        bl = ease_out(seg(t, 0.10, 0.35))
        br_ = ease_out(seg(t, 0.45, 0.70))
        lbox = [W // 4 - 200, 290, W // 4 + 200, 590]
        rbox = [W * 3 // 4 - 200, 290, W * 3 // 4 + 200, 590]
        if bl > 0.01:
            d.rounded_rectangle(lbox, radius=8, outline=(255, 255, 255, int(55 * bl)), width=3)
        if br_ > 0.01:
            d.rounded_rectangle(rbox, radius=8, outline=ACC + (int(255 * br_),), width=3)
        # 红线从左框左上划到右下（表示被抹掉）
        k = ease_out(seg(t, 0.35, 0.75))
        if k > 0.01:
            x0, y0 = lbox[0] - 20, lbox[1]
            x1, y1 = lbox[2] + 20, lbox[3]
            d.line([(x0, y0), (x0 + (x1 - x0) * k, y0 + (y1 - y0) * k)],
                   fill=WARN + (215,), width=8)
        place(c, self.l_y, W // 4, 200, alpha=ease_out(seg(t, 0.05, 0.32)))
        place(c, self.r_y, W * 3 // 4, 200, alpha=br_)
        place(c, self.l_s, W // 4, 540, alpha=ease_out(seg(t, 0.40, 0.68)))
        place(c, self.r_s, W * 3 // 4, 540, alpha=br_)
        place(c, BR, 1812, 58, anchor="rm")


# ============ 6 · 8 vs 6339：左右对推，比例条生长 ============
class S06Versus(Scene):
    dur = 1.68

    def setup(self):
        self.cap = text_layer("官方窗口  vs  本地档案", font(46), MUT)
        self.left = rect_layer(524, 240, 16, fill=(255, 92, 92, 42), outline=(255, 92, 92, 205), width=3)
        self.right = rect_layer(1104, 240, 16, fill=(86, 182, 255, 32), outline=ACC + (255,), width=3)
        self.n8 = text_layer("8", font(150), WARN, shadow=False)
        self.n6339 = text_layer("6339", font(160), WHITE, shadow=False)
        self.u8 = text_layer("天", font(36), WARN, shadow=False)
        self.u6339 = text_layer("天", font(36), ACC, shadow=False)
        self.l1 = text_layer("接口上限", font(30), MUT, shadow=False)
        self.l2 = text_layer("本地索引 · 全量可翻", font(30), MUT, shadow=False)
        self.t8 = text_layer("8 天", font(28), WARN, shadow=False)
        self.t6339 = text_layer("6339 天", font(28), ACC, shadow=False)

    def render(self, c, t):
        c.alpha_composite(solid_bg())
        c.alpha_composite(scrim_layer(0.0, 0.5))
        place(c, self.cap, 100, 142, alpha=ease_out(seg(t, 0.0, 0.30)))
        # 左块从左外推进
        el = ease_out(seg(t, 0.06, 0.40))
        lx = 358 + (-320) * (1 - el)
        place(c, self.left, lx, 330)
        place(c, self.n8, lx, 322, alpha=el, scale=1.0 + 0.12 * (1 - el))
        place(c, self.u8, lx, 412, alpha=el)
        place(c, self.l1, 100, 478, alpha=el)
        # 右块从右外推进
        er = ease_out(seg(t, 0.20, 0.55))
        rx = 1272 + 360 * (1 - er)
        place(c, self.right, rx, 330)
        place(c, self.n6339, rx + 24, 322, alpha=er, scale=1.0 + 0.08 * (1 - er))
        place(c, self.u6339, rx + 340, 412, alpha=er)
        place(c, self.l2, 724, 478, alpha=er)
        # 比例条从左往右生长，红段在最左
        d = ImageDraw.Draw(c)
        g = ease_out(seg(t, 0.35, 0.92))
        if g > 0.01:
            bx0, bx1, by = 96, 1824, 590
            d.line([(bx0, by), (bx0 + (bx1 - bx0) * g, by)], fill=ACC + (255,), width=8)
            d.line([(bx0, by), (bx0 + min(26, (bx1 - bx0) * g), by)], fill=WARN + (255,), width=14)
            place(c, self.t8, 100, 620, alpha=g)
            place(c, self.t6339, 1824, 620, alpha=g, anchor="rm")
        place(c, BR, 1812, 58, anchor="rm")


# ============ 7 · 主界面：卡片上浮 + 背景极慢推 ============
class S07Card(Scene):
    dur = 2.99

    def setup(self):
        self.base = load_photo("20261122_OHR.JustaHermitage.jpg", 1.42)
        self.scrim = scrim_layer(0.22, 0.86)
        self.vig = vignette_layer(130)
        # 卡片整块
        cw, ch = 1620, 604
        card = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
        d = ImageDraw.Draw(card)
        d.rounded_rectangle([0, 0, cw - 1, ch - 1], radius=20, fill=(18, 23, 33, 238),
                            outline=(255, 255, 255, 44), width=2)
        thumb = load_photo("20261122_OHR.JustaHermitage.jpg", 1.0).crop((180, 140, 1060, 660)).resize((720, 480), Image.LANCZOS)
        card.paste(thumb.convert("RGBA"), (40, 44))
        d = ImageDraw.Draw(card)
        d.rectangle([40, 44, 760, 524], outline=(255, 255, 255, 48), width=2)
        x = 820
        d.text((x, 54), "2026-11-22", font=font(32), fill=ACC)
        d.text((x, 104), "海浪间的庇护所", font=font(60), fill=WHITE)
        d.text((x, 192), "西班牙坎塔布里亚自治区", font=font(34), fill=MUT)
        d.text((x, 234), "圣胡斯塔隐修院与小教堂", font=font(34), fill=MUT)
        d.line([(x, 296), (1560, 296)], fill=(255, 255, 255, 34), width=2)
        d.text((x, 320), "潮水在礁石之间涨落，一座小教堂立在海岸上。", font=font(32), fill=(226, 232, 240))
        d.text((x, 368), "每天只在这个窗口里存在一天。", font=font(32), fill=(226, 232, 240))
        d.rounded_rectangle([x, 470, x + 226, 530], radius=12, fill=(0, 103, 192, 255))
        d.text((x + 113, 500), "设为壁纸", font=font(30), fill=WHITE, anchor="mm")
        d.rounded_rectangle([x + 250, 470, x + 476, 530], radius=12, outline=(255, 255, 255, 92), width=2)
        d.text((x + 363, 500), "收进收藏", font=font(30), fill=WHITE, anchor="mm")
        self.card = card

    def render(self, c, t):
        c.alpha_composite(bg_view(self.base, 1.02 + 0.05 * ease_inout(t), bright=0.40, blur=10))
        c.alpha_composite(self.scrim)
        c.alpha_composite(self.vig)
        e = ease_out(seg(t, 0.0, 0.34))
        place(c, self.card, W // 2, 398 + 60 * (1 - e), alpha=clamp01(e * 1.3))
        place(c, BR, 1812, 58, anchor="rm")


# ============ 8 · 羽毛：从侧转正 + 文字滑入 ============
class S08Feather(Scene):
    dur = 2.45

    def setup(self):
        fw, fh = 760, 760
        im = Image.new("RGBA", (fw, fh), (0, 0, 0, 0))
        d = ImageDraw.Draw(im)
        d.ellipse([0, 0, fw - 1, fh - 1], fill=ACC + (30,))
        cx, cy, L = fw / 2, fh / 2, 520
        col = (255, 255, 255, 235)
        d.line([(cx - L * 0.42, cy - L * 0.30), (cx + L * 0.30, cy + L * 0.42)], fill=col, width=7)
        n = 30
        for i in range(n):
            tt = i / (n - 1)
            px = cx - L * 0.42 + L * 0.72 * tt
            py = cy - L * 0.30 + L * 0.72 * tt
            half = math.sin(math.pi * tt) * L * 0.19 + 3
            for sgn in (-1, 1):
                d.line([(px, py), (px - half * 0.707 * sgn, py + half * 0.707 * sgn)], fill=col, width=4)
        d.ellipse([cx - 6, cy - 6, cx + 6, cy + 6], fill=col)
        self.feather = im
        self.t1 = text_layer("必年代记", font(132), WHITE)
        self.t2 = text_layer("HISTORY OF BING WALLPAPER", font(28), ACC, shadow=False)
        self.t3 = text_layer("羽毛 = 每天那张图", font(52), MUT)
        self.t4 = text_layer("册子 = 编年史", font(52), MUT)

    def render(self, c, t):
        c.alpha_composite(solid_bg())
        c.alpha_composite(scrim_layer(0.0, 0.45))
        # 羽毛：从 -14° 转到 0°，同时 0.8 → 1.0
        e = ease_out(seg(t, 0.0, 0.50))
        place(c, self.feather, 450, 400, scale=0.82 + 0.18 * e, rot=-14 * (1 - e), alpha=clamp01(e * 1.6))
        e1 = ease_out(seg(t, 0.12, 0.50))
        place(c, self.t1, 850 + 60 * (1 - e1), 322, alpha=e1)
        e2 = ease_out(seg(t, 0.24, 0.60))
        place(c, self.t2, 856 + 60 * (1 - e2), 428, alpha=e2)
        e3 = ease_out(seg(t, 0.38, 0.72))
        place(c, self.t3, 856 + 50 * (1 - e3), 498, alpha=e3)
        e4 = ease_out(seg(t, 0.48, 0.82))
        place(c, self.t4, 856 + 50 * (1 - e4), 574, alpha=e4)
        place(c, BR, 1812, 58, anchor="rm")


# ============ 9 · 时间轴：横向拖动 ============
class S09Timeline(Scene):
    dur = 1.97

    def setup(self):
        self.base = load_photo("20261114_OHR.BlueHK.jpg", 1.30)
        self.scrim = scrim_layer(0.28, 0.90)
        self.vig = vignette_layer(140)
        self.head = text_layer("时间轴", font(48), MUT)
        self.arrow = text_layer("← 昨天     ↑ 上个月", font(44), ACC)
        self.years = [(y, text_layer(y, font(56), WHITE, shadow=False)) for y in ("2024", "2025", "2026")]
        self.years_off = [(y, text_layer(y, font(44), MUT, shadow=False)) for y in ("2024", "2025", "2026")]
        self.hi = rect_layer(260, 110, 14, fill=(86, 182, 255, 52), outline=ACC + (255,), width=3)

    def render(self, c, t):
        # 背景随拖动反向位移，制造"在翻"的感觉
        c.alpha_composite(bg_view(self.base, 1.0, px=0.30 - 0.30 * ease_inout(t), bright=0.40, blur=2))
        c.alpha_composite(self.scrim)
        c.alpha_composite(self.vig)
        place(c, self.head, 100, 152, alpha=ease_out(seg(t, 0.0, 0.3)))
        # 高亮框从 2026 滑到 2025
        swipe = ease_inout(seg(t, 0.18, 0.62))
        hx = 1180 + 130 + (760 - 1180) * swipe
        d = ImageDraw.Draw(c)
        for i, (yr, lay) in enumerate(self.years):
            x = 340 + i * 420 + 130
            d.rounded_rectangle([x - 130, 210, x + 130, 320], radius=14,
                                outline=(255, 255, 255, 70), width=3)
        place(c, self.hi, hx, 265)
        for i, (yr, lay) in enumerate(self.years):
            x = 340 + i * 420 + 130
            on = 1.0 - min(1.0, abs(hx - x) / 260)
            place(c, lay, x, 265, alpha=0.35 + 0.65 * on)
        # 月份刻度
        x0, x1, y = 140, W - 140, 470
        d.line([(x0, y), (x1, y)], fill=(255, 255, 255, 44), width=3)
        prog = ease_inout(t)
        for i in range(12):
            x = x0 + (x1 - x0) * i / 11
            on = abs((i / 11) - (0.86 - 0.30 * prog)) < 0.05
            d.line([(x, y - 18), (x, y + 18)], fill=ACC + (255,) if on else (255, 255, 255, 64),
                   width=6 if on else 2)
            d.text((x, y + 34), f"{i+1}月", font=font(24 if on else 18),
                   fill=ACC + (255,) if on else (255, 255, 255, 88), anchor="ma")
        place(c, self.arrow, 100, 596, alpha=ease_out(seg(t, 0.5, 0.85)))
        place(c, BR, 1812, 58, anchor="rm")


# ============ 10 · 菊石：由暗转亮 + 缓推 + 文字逐行 ============
class S10Fossil(Scene):
    dur = 1.97

    def setup(self):
        self.base = load_photo(AMMONITE, 1.30)
        self.vig = vignette_layer(160)
        self.date = text_layer("2025-11-23", font(42), ACC)
        self.title = text_layer("大自然的秘密代码", font(88), WHITE)
        self.loc = text_layer("马达加斯加菊石化石", font(40), MUT)
        self.tag = text_layer("往年的今天 · 第 327 天", font(32), GOLD, shadow=False)

    def render(self, c, t):
        # 亮度 0.30 → 0.80，像从暗房里浮出来
        br = 0.30 + 0.50 * ease_out_in(seg(t, 0.0, 0.65))
        c.alpha_composite(bg_view(self.base, 1.0 + 0.06 * ease_inout(t), bright=br))
        c.alpha_composite(self.vig)
        c.alpha_composite(scrim_layer(0.26, 0.92))
        e1 = ease_out(seg(t, 0.10, 0.45))
        place(c, self.date, 100 + 30 * (1 - e1), 162, alpha=e1)
        e2 = ease_out(seg(t, 0.18, 0.55))
        place(c, self.title, 100 + 40 * (1 - e2), 236, alpha=e2)
        e3 = ease_out(seg(t, 0.30, 0.65))
        place(c, self.loc, 100 + 40 * (1 - e3), 340, alpha=e3)
        # 金色下划线从左抽出
        d = ImageDraw.Draw(c)
        k = ease_out(seg(t, 0.42, 0.78))
        if k > 0.01:
            d.line([(96, 404), (96 + 604 * k, 404)], fill=GOLD + (210,), width=5)
        e4 = ease_out(seg(t, 0.55, 0.88))
        place(c, self.tag, 100, 440, alpha=e4)
        place(c, BR, 1812, 58, anchor="rm")


def ease_out_in(t):
    return ease_inout(t)


# ============ 11 · 搜索：输入 → 光标闪 → 结果逐条 ============
class S11Search(Scene):
    dur = 1.86

    def setup(self):
        self.base = load_photo("20261101_OHR.SchwarzwaldAutumn.jpg", 1.28)
        self.scrim = scrim_layer(0.28, 0.86)
        self.vig = vignette_layer(140)
        self.head = text_layer("离线可搜 · 6339 条本地索引", font(42), MUT)
        self.box = rect_layer(1104, 98, 16, fill=(18, 23, 33, 242), outline=ACC + (255,), width=3)
        self.q = text_layer("菊 石", font(48), WHITE, shadow=False)
        self.hit = text_layer("命中 6 条  ·  断网也能搜", font(28), ACC, shadow=False)
        self.rows = []
        for dt, t1, t2, act in [("2025-11-23", "大自然的秘密代码", "马达加斯加菊石化石", True),
                                ("2023-08-14", "螺旋的时间", "侏罗纪海岸的菊石", False),
                                ("2019-04-02", "化石里的海", "多塞特郡海岸线", False)]:
            rg = rect_layer(1304, 94, 12, fill=(86, 182, 255, 46) if act else (255, 255, 255, 16))
            self.rows.append((rg, text_layer(dt, font(30), ACC, shadow=False),
                              text_layer(t1, font(34), WHITE, shadow=False),
                              text_layer(t2, font(26), MUT, shadow=False)))

    def render(self, c, t):
        c.alpha_composite(bg_view(self.base, 1.0, py=0.10 * ease_out(t), bright=0.38, blur=12))
        c.alpha_composite(self.scrim)
        c.alpha_composite(self.vig)
        place(c, self.head, 100, 142, alpha=ease_out(seg(t, 0.0, 0.28)))
        # 搜索框从上落下
        e = ease_out(seg(t, 0.04, 0.32))
        place(c, self.box, 96 + 552, 240 - 24 * (1 - e), alpha=e)
        # 文字逐字打出
        reveal = seg(t, 0.18, 0.42)
        full = "菊 石"
        cut = int(round(len(full) * reveal))
        if cut > 0:
            place(c, text_layer(full[:cut], font(48), WHITE, shadow=False), 128, 240, anchor="lm")
        # 光标闪烁
        if cut >= len(full) and (int(t * 26) % 2 == 0):
            d = ImageDraw.Draw(c)
            d.line([(1120, 216), (1120, 264)], fill=ACC + (255,), width=3)
        place(c, self.hit, 100, 322, alpha=ease_out(seg(t, 0.40, 0.60)))
        for i, (bg, dt, t1, t2) in enumerate(self.rows):
            a0 = 0.44 + i * 0.14
            e = ease_out(seg(t, a0, a0 + 0.24))
            y = 370 + i * 110
            place(c, bg, 96 + 652, y + 47, alpha=e)
            place(c, dt, 130 + 40 * (1 - e), y + 47, alpha=e, anchor="lm")
            place(c, t1, 390 + 40 * (1 - e), y + 32, alpha=e, anchor="lm")
            place(c, t2, 390 + 40 * (1 - e), y + 68, alpha=e, anchor="lm")
        place(c, BR, 1812, 58, anchor="rm")


# ============ 12 · 仓库页：整页上滚 + 数字跳 ============
class S12Repo(Scene):
    dur = 3.03

    def setup(self):
        self.card = self._build(1220)
        self.card_hi = self._build(1220)

    def _build(self, h):
        im = Image.new("RGBA", (1680, h), (0, 0, 0, 0))
        d = ImageDraw.Draw(im)
        d.rounded_rectangle([0, 0, 1679, h - 1], radius=18, fill=(22, 27, 34, 248),
                            outline=(255, 255, 255, 44), width=2)
        d.rectangle([0, 0, 1679, 76], fill=(30, 36, 45, 255))
        d.rounded_rectangle([0, 0, 1679, 76], radius=18, fill=(30, 36, 45, 255))
        d.rectangle([0, 50, 1679, 76], fill=(30, 36, 45, 255))
        for i, cc in enumerate([(255, 95, 86), (255, 189, 46), (39, 201, 63)]):
            d.ellipse([28 + i * 28, 26, 46 + i * 28, 44], fill=cc)
        d.text((200, 22), "github.com/zkyhzxf/bing-wallpaper-chronicle", font=font(28), fill=MUT)
        d.text((40, 108), "zkyhzxf / bing-wallpaper-chronicle", font=font(48), fill=WHITE)
        d.text((40, 186), "必应壁纸编年史 · 6339 天档案，一天不缺", font=font(32), fill=MUT)
        d.rounded_rectangle([1300, 98, 1650, 168], radius=12, fill=GREEN + (255,))
        d.text((1370, 133), "★  Star", font=font(32), fill=WHITE, anchor="lm")
        d.text((1472, 133), "1.2k", font=font(28), fill=(200, 255, 210), anchor="lm")
        x = 40
        for tt, cc in [("PowerShell", ACC), ("Windows", (140, 200, 255)), ("MIT", GOLD)]:
            b = d.textbbox((0, 0), tt, font=font(24))
            w = b[2] - b[0]
            d.rounded_rectangle([x, 252, x + w + 40, 304], radius=22, fill=cc + (42,), outline=cc + (165,), width=2)
            d.text((x + 20, 278), tt, font=font(24), fill=cc, anchor="lm")
            x += w + 58
        d.text((40, 346), "免费开源 · 解压即用 · 不用注册", font=font(36), fill=WHITE)
        d.text((40, 416), "v3.6.56   ·   102 项回归断言全绿   ·   MIT", font=font(28), fill=MUT)
        d.text((40, 490), "★ 1.2k   ·   ⑂ 87   ·   6339 天索引   ·   52 张实图", font=font(26), fill=MUT)
        d.line([(40, 552), (1640, 552)], fill=(255, 255, 255, 26), width=2)
        d.text((40, 580), "README.md", font=font(26), fill=ACC)
        d.text((40, 626), "必应中国区 6339 天、一天不缺的每日壁纸档案", font=font(26), fill=MUT)
        d.text((40, 668), "2019 年起 3840×2160 无水印原图", font=font(26), fill=MUT)
        return im

    def render(self, c, t):
        c.alpha_composite(solid_bg())
        c.alpha_composite(scrim_layer(0.0, 0.45))
        e = ease_out(seg(t, 0.0, 0.35))
        # 整页从 y=+150 落到 0，之后继续缓慢上滚 60px
        scroll = -60 * ease_inout(seg(t, 0.35, 1.0))
        y = 720 + 150 * (1 - e) + scroll
        place(c, self.card, W // 2, 110 + (self.card.height / 2) + 150 * (1 - e) + scroll,
              alpha=clamp01(e * 1.4))
        place(c, BR, 1812, 58, anchor="rm")


# ============ 13 · Star：按钮脉冲 ============
class S13Star(Scene):
    dur = 2.27

    def setup(self):
        self.t1 = text_layer("Star 不是打赏", font(80), MUT)
        self.t2 = text_layer("是收藏", font(140), WHITE)
        self.btn = rect_layer(580, 136, 24, fill=GREEN + (255,))
        self.bt = text_layer("★  Star", font(60), WHITE, shadow=False)
        self.t3 = text_layer("点一下，相当于给这个项目留个书签", font(40), ACC)

    def render(self, c, t):
        c.alpha_composite(solid_bg())
        c.alpha_composite(scrim_layer(0.0, 0.45))
        place(c, self.t1, W // 2, 190, alpha=ease_out(seg(t, 0.0, 0.26)))
        e2 = ease_out_back(seg(t, 0.10, 0.44), 1.4)
        place(c, self.t2, W // 2, 312, scale=0.88 + 0.12 * e2, alpha=clamp01(e2 * 1.2))
        # 按钮入场后做三次脉冲
        e3 = ease_out(seg(t, 0.28, 0.50))
        pulse = 0.0
        if e3 > 0.98:
            for k, ph in enumerate((0.55, 0.66, 0.78)):
                p = seg(t, ph, ph + 0.075)
                if 0 < p < 1:
                    pulse = max(pulse, math.sin(p * math.pi) * (0.055 - k * 0.012))
        place(c, self.btn, W // 2, 498, scale=(0.9 + 0.1 * e3) * (1 + pulse), alpha=e3)
        place(c, self.bt, W // 2, 498, scale=(0.9 + 0.1 * e3) * (1 + pulse), alpha=e3)
        place(c, self.t3, W // 2, 620, alpha=ease_out(seg(t, 0.46, 0.72)))
        place(c, BR, 1812, 58, anchor="rm")


# ============ 14 · 二维码：弹出 + 背景拉远 ============
class S14End(Scene):
    dur = 3.11

    def setup(self):
        self.base = load_photo("20261115_OHR.FirstFrost.jpg", 1.34)
        self.vig = vignette_layer(150)
        import qrcode
        q = qrcode.QRCode(box_size=5, border=1)
        q.add_data("https://github.com/zkyhzxf/bing-wallpaper-chronicle")
        q.make(fit=True)
        qr = q.make_image(fill_color="black", back_color="white").convert("RGB").resize((250, 250), Image.NEAREST)
        card = Image.new("RGBA", (286, 286), WHITE + (255,))
        card.paste(qr, (18, 18))
        self.qr = card
        self.qtip = text_layer("扫码打开仓库", font(28), WHITE, shadow=False)
        self.t1 = text_layer("必年代记", font(160), WHITE)
        self.t2 = text_layer("把必应每天散落的那张图，捡回来", font(48), WHITE)
        self.t3 = text_layer("免费开源  ·  解压即用  ·  不用注册", font(32), MUT, shadow=False)

    def render(self, c, t):
        c.alpha_composite(bg_view(self.base, 1.10 - 0.10 * ease_out(seg(t, 0.0, 0.9)), bright=0.36, blur=4))
        c.alpha_composite(scrim_layer(0.34, 0.94))
        c.alpha_composite(self.vig)
        e1 = ease_out(seg(t, 0.0, 0.38))
        place(c, self.t1, 108 + 40 * (1 - e1), 300, alpha=e1)
        e2 = ease_out(seg(t, 0.14, 0.52))
        place(c, self.t2, 112 + 40 * (1 - e2), 440, alpha=e2)
        d = ImageDraw.Draw(c)
        k = ease_out(seg(t, 0.30, 0.60))
        if k > 0.01:
            d.line([(112, 500), (112 + 788 * k, 500)], fill=ACC + (255,), width=5)
        e3 = ease_out(seg(t, 0.44, 0.72))
        place(c, self.t3, 112, 548, alpha=e3)
        # 二维码：0.55 倍弹到 1.0，之后停住
        eq = ease_out_back(seg(t, 0.30, 0.72), 2.2)
        place(c, self.qr, 1633, 470, scale=0.55 + 0.45 * eq, alpha=clamp01(seg(t, 0.28, 0.42) * 1.4))
        place(c, self.qtip, 1633, 316, alpha=ease_out(seg(t, 0.55, 0.80)))
        place(c, BR, 1812, 58, anchor="rm")


SCENES = [S01Window, S02Span, S03Number, S04Popup, S05Split, S06Versus, S07Card,
          S08Feather, S09Timeline, S10Fossil, S11Search, S12Repo, S13Star, S14End]

# 每段（clip）内包含哪些场景，与 build_video.py 的 SHOTS 对齐
CLIP_SCENES = [SCENES[0:3], SCENES[3:6], SCENES[6:8], SCENES[8:11], SCENES[11:14]]
