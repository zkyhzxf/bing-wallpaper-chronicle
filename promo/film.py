# -*- coding: utf-8 -*-
"""全片 30 个镜头（2 分 05 秒版）。
素材一律取自 D:\BingWall-全量（6322 张真图）+ promo\evidence 的四张证据截图。
"""
import math
import os
import random
from PIL import Image, ImageDraw, ImageFilter, ImageEnhance

import scenes as S
from scenes import (W, H, WHITE, ACC, MUT, WARN, GOLD, GREEN, DARK,
                    font, text_layer, rect_layer, place, atmosphere,
                    scrim_layer, vignette_layer, ease_out, ease_inout, ease_in,
                    ease_out_back, seg, clamp01, BR)
import evidence_scenes as E
import fx

FULL = r"D:\BingWall-全量"
EV = E.EV
_IDX = None


def files_by_date():
    global _IDX
    if _IDX is None:
        _IDX = {}
        for f in os.listdir(FULL):
            if f.endswith(".jpg"):
                _IDX[f[:8]] = f
    return _IDX


def thumb(date8_or_file, size, cover=True):
    """按日期取全量图，裁成缩略图"""
    idx = files_by_date()
    fn = idx.get(date8_or_file) if len(str(date8_or_file)) == 8 else date8_or_file
    if not fn:
        return Image.new("RGB", size, (34, 38, 46))
    try:
        im = Image.open(os.path.join(FULL, fn)).convert("RGB")
    except Exception:
        return Image.new("RGB", size, (34, 38, 46))
    tw, th = size
    k = max(tw / im.width, th / im.height)
    im = im.resize((max(1, round(im.width * k)), max(1, round(im.height * k))), Image.LANCZOS)
    l, t = (im.width - tw) // 2, (im.height - th) // 2
    return im.crop((l, t, l + tw, t + th))


def center_text(c, s, size, y, color=WHITE, a=1.0, dy=0.0):
    place(c, text_layer(s, font(size), color), W // 2, y + dy, alpha=a)


def panel(c, x, y, w, h, r=16, fill=(16, 20, 28), line=(255, 255, 255, 44), a=1.0, anchor="mm"):
    if a <= 0.01:
        return
    place(c, rect_layer(w, h, r, fill=fill + (240,), outline=line, width=2), x, y, alpha=a, anchor=anchor)


def img_card(c, im, cx, cy, w=None, a=1.0, border=True, shadow=True):
    """把一张缩略图当卡片贴上去，带边框与投影"""
    if a <= 0.01:
        return
    if shadow:
        sh = Image.new("RGBA", (im.width + 40, im.height + 40), (0, 0, 0, 0))
        ImageDraw.Draw(sh).rectangle([20, 24, im.width + 20, im.height + 24], fill=(0, 0, 0, 130))
        place(c, sh.filter(ImageFilter.GaussianBlur(12)), cx, cy, alpha=a * 0.9)
    place(c, im.convert("RGBA"), cx, cy, alpha=a)
    if border:
        d = ImageDraw.Draw(c)
        d.rectangle([cx - im.width / 2, cy - im.height / 2,
                     cx + im.width / 2, cy + im.height / 2],
                    outline=(255, 255, 255, int(60 * a)), width=2)



_BG_CACHE2 = {}


def photo_bg(date8=None, key=None, blur=26, bright=0.17, vig=190):
    """用全量真图做底：模糊 + 压暗 + 暗角。同一个 key 永远同一张，画面稳定。"""
    ck = (date8, key, blur, bright)
    if ck in _BG_CACHE2:
        return _BG_CACHE2[ck]
    idx = sorted(files_by_date().keys())
    if date8 is None:
        h = sum(ord(ch) * (i + 7) for i, ch in enumerate(key or "x"))
        date8 = idx[h % len(idx)]
    im = thumb(date8, (W, H))
    im = im.filter(ImageFilter.GaussianBlur(blur))
    im = ImageEnhance.Brightness(im).enhance(bright)
    im = Image.alpha_composite(im.convert("RGBA"), vignette_layer(vig))
    _BG_CACHE2[ck] = im
    return im


# ============ F01 · 定型文起字 ============
class F01Opening(S.Scene):
    """原神启动页二创：米白底 + 黑字 + 细金线 + 假加载条"""
    dur = 4.9
    PAPER = (244, 242, 236)
    INK = (26, 26, 26)
    GILT = (200, 169, 106)

    def setup(self):
        self.l1 = text_layer("你说的对，但是", font(92), self.INK, shadow=False)
        self.l2 = text_layer("《必应壁纸》", font(116), self.INK, shadow=False)
        self.l3 = text_layer("是一款开放世界风景壁纸软件", font(52), (90, 88, 84), shadow=False)
        self.ver = text_layer("v3.6.56", font(26), (150, 146, 138), shadow=False)
        self.loading = text_layer("正在加载必年代记…", font(24), (150, 146, 138), shadow=False)

    def render(self, c, t):
        c.alpha_composite(Image.new("RGBA", (W, H), self.PAPER + (255,)))
        d = ImageDraw.Draw(c)
        # 四角细金线
        m, ln = 56, 90
        for (x, y, sx, sy) in [(m, m, 1, 1), (W - m, m, -1, 1), (m, H - m, 1, -1), (W - m, H - m, -1, -1)]:
            d.line([(x, y), (x + sx * ln, y)], fill=self.GILT + (150,), width=2)
            d.line([(x, y), (x, y + sy * ln)], fill=self.GILT + (150,), width=2)
        place(c, self.ver, W - 70, 70, alpha=ease_out(seg(t, 0.0, 0.2)), anchor="rm")
        e1 = ease_out(seg(t, 0.06, 0.34))
        place(c, self.l1, W // 2, 360, alpha=e1, dy=14 * (1 - e1))
        e2 = ease_out(seg(t, 0.22, 0.52))
        place(c, self.l2, W // 2, 500, alpha=e2, dy=18 * (1 - e2))
        k = ease_out(seg(t, 0.34, 0.66))
        if k > 0.01:
            d.line([(W // 2 - 320 * k, 588), (W // 2 + 320 * k, 588)], fill=self.GILT + (200,), width=2)
        e3 = ease_out(seg(t, 0.46, 0.74))
        place(c, self.l3, W // 2, 640, alpha=e3)
        # 假加载条
        e4 = ease_out(seg(t, 0.58, 0.86))
        if e4 > 0.01:
            bw, bh = 420, 6
            bx, by = W // 2 - bw // 2, 820
            d.rounded_rectangle([bx, by, bx + bw, by + bh], radius=3, fill=(210, 206, 198, 255))
            d.rounded_rectangle([bx, by, bx + bw * 0.37 * e4, by + bh], radius=3, fill=self.GILT + (255,))
            place(c, self.loading, W // 2, 862, alpha=e4, anchor="mm")


# ============ F02 · 桌面幻想世界 ============
class F02Desktop(S.Scene):
    dur = 2.4

    def setup(self):
        random.seed(7)
        idx = sorted(files_by_date().keys())
        picks = random.sample(idx, 12)
        self.tiles = [thumb(d, (300, 169)) for d in picks]

    def render(self, c, t):
        c.alpha_composite(photo_bg(key="F02Desktop"))
        e = ease_out(seg(t, 0.0, 0.55))
        for i, im in enumerate(self.tiles):
            ang = i / len(self.tiles) * math.tau
            r = 180 + 420 * e
            x = W / 2 + math.cos(ang) * r * 1.7
            y = 430 + math.sin(ang) * r * 0.85
            img_card(c, im, x, y, a=clamp01(e * 1.3) * 0.85, border=False, shadow=False)
        e2 = ease_out(seg(t, 0.25, 0.70))
        center_text(c, "桌面", 150, 430, WHITE, e2, dy=20 * (1 - e2))
        center_text(c, "幻想世界", 48, 530, MUT, ease_out(seg(t, 0.45, 0.85)))
        place(c, BR, 1812, 58, anchor="rm")


# ============ F03 · 每日一图 ============
class F03Daily(S.Scene):
    """每日一图：真实壁纸缩略图铺满，一格一天，逐格点亮"""
    dur = 4.3

    def setup(self):
        idx = sorted(files_by_date().keys())
        cols, rows = 12, 6
        tw, th = 138, 78
        gap = 8
        picks = [idx[(i * 821) % len(idx)] for i in range(cols * rows)]
        self.tiles = []
        for d8 in picks:
            self.tiles.append(thumb(d8, (tw, th)))
        self.cols, self.rows, self.tw, self.th, self.gap = cols, rows, tw, th, gap
        self.total_w = cols * (tw + gap) - gap
        self.x0 = (W - self.total_w) // 2
        self.y0 = 300
        self.cap = text_layer("每日一图  ·  2009-07-13 → 今天", font(44), WHITE)
        self.sub = text_layer("一天一格，一格不缺", font(30), MUT, shadow=False)

    def render(self, c, t):
        c.alpha_composite(photo_bg(key="F03Daily", blur=36, bright=0.12))
        e0 = ease_out(seg(t, 0.0, 0.28))
        place(c, self.cap, W // 2, 210, alpha=e0)
        place(c, self.sub, W // 2, 262, alpha=ease_out(seg(t, 0.15, 0.45)))
        lit = ease_out(seg(t, 0.08, 0.88)) * (self.cols * self.rows)
        for i, im in enumerate(self.tiles):
            r, k = divmod(i, self.cols)
            x = self.x0 + k * (self.tw + self.gap)
            y = self.y0 + r * (self.th + self.gap)
            on = i <= lit
            if on:
                fade = clamp01((lit - i) * 2.2)
                c.alpha_composite(im.convert("RGBA"), (x, y))
                if fade < 1:
                    ov = Image.new("RGBA", (self.tw, self.th), (0, 0, 0, int(200 * (1 - fade))))
                    c.alpha_composite(ov, (x, y))
            else:
                d = ImageDraw.Draw(c)
                d.rounded_rectangle([x, y, x + self.tw, y + self.th], radius=3,
                                    fill=(70, 78, 92, 120))
        place(c, BR, 1812, 58, anchor="rm")


class F04PopupAd(S.Scene):
    dur = 6.2

    def setup(self):
        self.a = E.EP4TopRight()
        self.b = E.EP2AdFull()

    def render(self, c, t):
        if t < 0.52:
            self.a.render(c, min(1.0, t / 0.52))
        else:
            self.b.render(c, min(1.0, (t - 0.52) / 0.48))


# ============ F05 · 右键菜单没有另存为 ============
class F05ContextMenu(S.Scene):
    dur = 2.4

    def setup(self):
        items = ["查看", "排序方式", "刷新", "—", "粘贴", "粘贴快捷方式", "—",
                 "新建", "显示设置", "个性化", "—", "在终端中打开"]
        self.items = items

    def render(self, c, t):
        c.alpha_composite(thumb("20261122", (W, H)).convert("RGBA"))
        ov = Image.new("RGBA", (W, H), (4, 6, 10, 170))
        c.alpha_composite(ov)
        e = ease_out(seg(t, 0.05, 0.30))
        x, y = 900, 150
        wid, row = 460, 56
        hgt = row * len(self.items) + 24
        panel(c, x + wid / 2, y + hgt / 2, wid, hgt, 12, (28, 32, 40), (255, 255, 255, 60), e)
        d = ImageDraw.Draw(c)
        # 光标扫过各项
        cy = e * len(self.items)
        for i, it in enumerate(self.items):
            ry = y + 12 + i * row
            if it == "—":
                d.line([(x + 16, ry + row / 2), (x + wid - 16, ry + row / 2)],
                       fill=(255, 255, 255, 40), width=1)
                continue
            hot = abs(i - cy) < 0.6
            if hot:
                d.rounded_rectangle([x + 6, ry + 4, x + wid - 6, ry + row - 4], radius=6,
                                    fill=(86, 182, 255, 70))
            d.text((x + 26, ry + 16), it, font=font(26), fill=(232, 236, 244, 235))
        # 灰掉的「另存为」位置
        gy = y + 12 + 8 * row
        k = ease_out(seg(t, 0.45, 0.75))
        if k > 0.01:
            d.rounded_rectangle([x + 6, gy + 4, x + wid - 6, gy + row - 4], radius=6,
                                outline=WARN + (int(210 * k),), width=2)
            d.text((x + 26, gy + 16), "另存为（没有这一项）", font=font(26),
                   fill=WARN + (int(240 * k),))
        place(c, BR, 1812, 58, anchor="rm")


# ============ F06 · 活动页 ============
class F06Campaign(S.Scene):
    dur = 2.2

    def setup(self):
        self.a = E.EP1Campaign()

    def render(self, c, t):
        self.a.render(c, t)


# ============ F07 · 官方桌面全景 ============
class F07OfficialDesk(S.Scene):
    dur = 2.5

    def setup(self):
        self.base = E.load_evidence("E3-official.png")

    def render(self, c, t):
        e = ease_out(seg(t, 0.0, 0.5))
        c.alpha_composite(E.view(self.base, 0.5, 0.5, 1.0 + 0.03 * e, bright=0.62 + 0.18 * e))
        c.alpha_composite(scrim_layer(0.16, 0.72))
        center_text(c, "官方客户端", 52, 190, (238, 242, 248), e)
        place(c, BR, 1812, 58, anchor="rm")


# ============ F08 · 7 of 8（复用证据场景，压到 2.8 秒） ============
class F08Eight(S.Scene):
    dur = 2.8

    def setup(self):
        self.a = E.EP5Eight()

    def render(self, c, t):
        self.a.render(c, t)


# ============ F09 · 三格翻不到 ============
class F09ThreeGone(S.Scene):
    dur = 5.1

    def setup(self):
        self.a = E.EP6ThreeGone()

    def render(self, c, t):
        self.a.render(c, t)


# ============ F10 · 每天给一张，旧的收走 ============
class F10TakeAway(S.Scene):
    dur = 2.9

    def setup(self):
        random.seed(3)
        idx = sorted(files_by_date().keys())
        self.seq = [thumb(d, (420, 236)) for d in random.sample(idx, 6)]

    def render(self, c, t):
        c.alpha_composite(photo_bg(key="F10TakeAway"))
        cyc = 3
        for i in range(cyc):
            p = (t * cyc) - i
            if p < 0 or p > 1.15:
                continue
            a = 1.0
            sc = 1.0
            yoff = 0
            if p > 0.72:
                k = (p - 0.72) / 0.43
                a = 1 - k
                sc = 1 - 0.35 * k
                yoff = -180 * k
            im = self.seq[i % len(self.seq)]
            img_card(c, im, W / 2, 470 + yoff, a=clamp01(a), border=True)
            # 空位
            d = ImageDraw.Draw(c)
            if p > 0.72:
                kk = (p - 0.72) / 0.43
                d.rectangle([W / 2 - 210, 352, W / 2 + 210, 588], outline=WARN + (int(160 * kk),), width=3)
        center_text(c, "每天一张，旧的收走", 42, 700, MUT, ease_out(seg(t, 0.1, 0.5)))
        place(c, BR, 1812, 58, anchor="rm")


# ============ F11 · 时间轴 2009→今 ============
class F11Timeline(S.Scene):
    dur = 4.2

    def setup(self):
        self.big = text_layer("6339", font(260), WHITE, shadow=False)
        self.cap = text_layer("天，一天不缺", font(60), ACC)

    def render(self, c, t):
        c.alpha_composite(photo_bg(key="F11Timeline"))
        c.alpha_composite(scrim_layer(0.0, 0.5))
        d = ImageDraw.Draw(c)
        x0, x1, y = 96, W - 96, 560
        g = ease_out(seg(t, 0.05, 0.72))
        n = 18
        lit = g * (n - 1)
        if g > 0.01:
            d.line([(x0, y), (x0 + (x1 - x0) * g, y)], fill=(255, 255, 255, 70), width=3)
        for i in range(n):
            x = x0 + (x1 - x0) * i / (n - 1)
            on = i <= lit + 0.01
            if not on and g < 0.02:
                continue
            d.line([(x, y - (22 if on else 10)), (x, y + (22 if on else 10))],
                   fill=(ACC if on else (255, 255, 255)) + (255 if on else 80,), width=5 if on else 2)
            d.text((x, y + 28), str(2009 + i), font=font(24 if on else 18),
                   fill=(ACC if on else (255, 255, 255)) + (255 if on else 90,), anchor="ma")
        e = ease_out(seg(t, 0.10, 0.62))
        place(c, self.big, W // 2, 360, scale=1.06 - 0.06 * e, alpha=clamp01(e * 1.4))
        place(c, self.cap, W // 2, 470, alpha=ease_out(seg(t, 0.42, 0.78)))
        place(c, BR, 1812, 58, anchor="rm")


# ============ F12 · 官方几天 vs 6339 ============
class F12VsOfficial(S.Scene):
    dur = 3.0

    def setup(self):
        self.left = rect_layer(524, 240, 16, fill=(255, 92, 92, 42), outline=(255, 92, 92, 205), width=3)
        self.right = rect_layer(1104, 240, 16, fill=(86, 182, 255, 32), outline=ACC + (255,), width=3)
        self.n8 = text_layer("几天", font(96), WARN, shadow=False)
        self.n6339 = text_layer("6339 天", font(120), WHITE, shadow=False)
        self.l1 = text_layer("官方客户端", font(30), MUT, shadow=False)
        self.l2 = text_layer("本地索引 · 全量可翻", font(30), MUT, shadow=False)

    def render(self, c, t):
        c.alpha_composite(photo_bg(key="F12VsOfficial"))
        el = ease_out(seg(t, 0.02, 0.34))
        place(c, self.left, 358 - 260 * (1 - el), 330, alpha=el)
        place(c, self.n8, 358 - 260 * (1 - el), 330, alpha=el)
        place(c, self.l1, 100, 484, alpha=el, anchor="lm")
        er = ease_out(seg(t, 0.18, 0.52))
        place(c, self.right, 1272 + 300 * (1 - er), 330, alpha=er)
        place(c, self.n6339, 1272 + 300 * (1 - er), 330, alpha=er)
        place(c, self.l2, 724, 484, alpha=er, anchor="lm")
        place(c, BR, 1812, 58, anchor="rm")


# ============ F13 · 标题卡 ============
class F13Title(S.Scene):
    dur = 5.5

    def setup(self):
        self.t1 = text_layer("必应壁纸编年史", font(118), WHITE)
        self.t2 = text_layer("必年代记", font(72), ACC)
        self.t3 = text_layer("HISTORY OF BING WALLPAPER", font(26), (120, 134, 152), shadow=False)

    def render(self, c, t):
        c.alpha_composite(photo_bg(key="F13Title", blur=30, bright=0.14))
        d = ImageDraw.Draw(c)
        k = ease_out(seg(t, 0.10, 0.55))
        if k > 0.01:
            d.rectangle([W // 2 - 900 * k, 200, W // 2 + 900 * k, 700], fill=(12, 18, 28, 90))
        e1 = ease_out(seg(t, 0.10, 0.48))
        place(c, self.t1, W // 2, 400, alpha=e1, dy=28 * (1 - e1))
        e2 = ease_out(seg(t, 0.36, 0.70))
        place(c, self.t2, W // 2, 520, alpha=e2, dy=20 * (1 - e2))
        e3 = ease_out(seg(t, 0.52, 0.82))
        place(c, self.t3, W // 2, 590, alpha=e3)
        place(c, BR, 1812, 58, anchor="rm")


# ============ F14 · 翼年代记：羽毛散落 ============
class F14Tsubasa(S.Scene):
    """玖楼国：翼年代记原画做背景（压暗），上面走羽毛路径"""
    dur = 5.4

    def setup(self):
        self.worlds = [(360, 300), (1560, 280), (420, 760), (1520, 760), (960, 200), (960, 820)]
        self.label = text_layer("玖楼国", font(52), GOLD)
        self.cap = text_layer("小狼他们去各个世界，捡小樱散落的羽毛", font(38), WHITE)
        bg = Image.open(r"D:\Tools\bing-wallpaper\promo\assets\tsubasa_bg.jpg").convert("RGB")
        bg = bg.resize((W, H), Image.LANCZOS)
        bg = ImageEnhance.Brightness(bg).enhance(0.42)
        bg = bg.filter(ImageFilter.GaussianBlur(2.2))
        self.bg = Image.alpha_composite(bg.convert("RGBA"), vignette_layer(170))
        self.fly = Image.open(r"D:\Tools\bing-wallpaper\promo\assets\feather_long.png").convert("RGBA")

    def render(self, c, t):
        c.alpha_composite(self.bg)
        c.alpha_composite(scrim_layer(0.30, 0.86))
        d = ImageDraw.Draw(c)
        cx, cy = W / 2, 470
        e0 = ease_out(seg(t, 0.0, 0.3))
        d.ellipse([cx - 96, cy - 96, cx + 96, cy + 96], outline=GOLD + (int(170 * e0),), width=3)
        d.ellipse([cx - 96, cy - 96, cx + 96, cy + 96], fill=(10, 14, 22, int(190 * e0)))
        place(c, self.label, cx, cy, alpha=e0, anchor="mm")
        for i, (x, y) in enumerate(self.worlds):
            k = ease_out(seg(t, 0.16 + i * 0.06, 0.52 + i * 0.06))
            if k <= 0.01:
                continue
            ex, ey = cx + (x - cx) * k, cy + (y - cy) * k
            d.line([(cx, cy), (ex, ey)], fill=(255, 199, 102, int(110 * k)), width=2)
            d.ellipse([x - 44, y - 44, x + 44, y + 44], outline=(255, 255, 255, int(70 * k)), width=2)
            fa = clamp01((k - 0.55) * 3)
            if fa > 0:
                place(c, self.fly, x, y, scale=0.10 * fa, rot=-24, alpha=fa)
        place(c, self.cap, W // 2, 950, alpha=ease_out(seg(t, 0.55, 0.85)))
        place(c, BR, 1812, 58, anchor="rm")


class F15Collect(S.Scene):
    dur = 6.1

    def setup(self):
        self.t1 = text_layer("从今天出发", font(72), ACC)
        self.t2 = text_layer("往回捡", font(72), WHITE)

    def render(self, c, t):
        c.alpha_composite(photo_bg(key="F15Collect"))
        d = ImageDraw.Draw(c)
        # 时间轴从右扫到左
        y = 560
        x0, x1 = 140, W - 140
        g = ease_inout(seg(t, 0.12, 0.85))
        d.line([(x1, y), (x1 - (x1 - x0) * g, y)], fill=(255, 255, 255, 60), width=3)
        n = 18
        for i in range(n):
            x = x0 + (x1 - x0) * i / (n - 1)
            on = (1 - (x - x0) / (x1 - x0)) <= g + 0.02
            if on:
                d.line([(x, y - 14), (x, y + 14)], fill=ACC + (255,), width=4)
                d.text((x, y + 24), str(2009 + i), font=font(20), fill=ACC + (255,), anchor="ma")
        # 羽毛从右往左飞
        fx = x1 - (x1 - x0) * g
        fa = clamp01(seg(t, 0.08, 0.2))
        if fa > 0:
            d.line([(fx - 20, y - 46), (fx + 20, y - 86)], fill=(255, 255, 255, int(240 * fa)), width=5)
            for j in range(-4, 5):
                d.line([(fx + j * 5, y - 66 + j * 5), (fx + j * 5 - 10, y - 66 + j * 5 + 3)],
                       fill=(255, 255, 255, int(180 * fa)), width=2)
        e1 = ease_out(seg(t, 0.0, 0.35))
        place(c, self.t1, 140, 300, alpha=e1, anchor="lm")
        place(c, self.t2, 140, 380, alpha=ease_out(seg(t, 0.25, 0.6)), anchor="lm")
        place(c, BR, 1812, 58, anchor="rm")


# ============ F16 · 羽毛落进册子 ============
class F16Book(S.Scene):
    dur = 4.4

    def setup(self):
        halo = Image.new("RGBA", (820, 820), (0, 0, 0, 0))
        ImageDraw.Draw(halo).ellipse([0, 0, 819, 819], fill=(255, 199, 102, 34))
        self.halo = halo
        self.feather = Image.open(r"D:\Tools\bing-wallpaper\promo\assets\feather_long.png").convert("RGBA")
        self.t1 = text_layer("一根羽毛", font(64), WHITE)
        self.t2 = text_layer("一本收羽毛的册子", font(64), ACC)

    def render(self, c, t):
        c.alpha_composite(atmosphere())
        e = ease_out(seg(t, 0.0, 0.45))
        place(c, self.halo, 560, 470, scale=0.86 + 0.14 * e, alpha=clamp01(e * 2.0))
        place(c, self.feather, 560, 470, scale=0.86 + 0.14 * e, rot=-10 * (1 - e), alpha=clamp01(e * 2.2))
        e1 = ease_out(seg(t, 0.12, 0.48))
        place(c, self.t1, 1080, 420, alpha=e1, anchor="lm")
        e2 = ease_out(seg(t, 0.34, 0.72))
        place(c, self.t2, 1080, 510, alpha=e2, anchor="lm")
        place(c, BR, 1812, 58, anchor="rm")
