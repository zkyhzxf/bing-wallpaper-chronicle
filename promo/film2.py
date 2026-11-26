# -*- coding: utf-8 -*-
"""F17-F30：演示段与 CTA 段（接 file part 1）"""
import math
import os
import random
from PIL import Image, ImageDraw, ImageFilter, ImageEnhance

import scenes as S
from scenes import (W, H, WHITE, ACC, MUT, WARN, GOLD, GREEN, DARK,
                    font, text_layer, rect_layer, place, atmosphere,
                    scrim_layer, vignette_layer, ease_out, ease_inout, ease_in,
                    ease_out_back, seg, clamp01, BR)
from film import thumb, files_by_date, img_card, panel, center_text, FULL, EV, photo_bg
import evidence_scenes as E
import fx


# ============ F17 · 今日详情卡 ============
class F17TodayCard(S.Scene):
    dur = 4.7

    def setup(self):
        self.photo = thumb("20261123", (720, 480))
        self.date = text_layer("2026-11-23", font(32), ACC, shadow=False)
        self.title = text_layer("自然中的几何之美", font(56), WHITE)
        self.loc1 = text_layer("这处玄武岩柱群在高地风化千年", font(32), MUT, shadow=False)
        self.loc2 = text_layer("断面像被尺子量过一样整齐", font(32), MUT, shadow=False)
        self.desc = text_layer("岩柱一根根立着，从高处看是规律的六边形。", font(30), (222, 228, 238), shadow=False)
        self.b1 = text_layer("设为壁纸", font(28), WHITE, shadow=False)
        self.b2 = text_layer("收进收藏", font(28), WHITE, shadow=False)

    def render(self, c, t):
        c.alpha_composite(photo_bg(key="F17TodayCard"))
        e = ease_out(seg(t, 0.0, 0.38))
        cx, cy = W // 2, 430 + 40 * (1 - e)
        panel(c, cx, cy, 1620, 600, 20, (16, 22, 32), (255, 255, 255, 46), clamp01(e * 1.4))
        place(c, self.photo.convert("RGBA"), cx - 420, cy, alpha=e)
        d = ImageDraw.Draw(c)
        d.rectangle([cx - 780, cy - 240, cx - 60, cy + 240], outline=(255, 255, 255, int(48 * e)), width=2)
        x = cx - 20
        for i, (lay, yy, a0) in enumerate([(self.date, cy - 218, 0.18), (self.title, cy - 168, 0.24),
                                          (self.loc1, cy - 80, 0.32), (self.loc2, cy - 38, 0.36),
                                          (self.desc, cy + 30, 0.44)]):
            ea = ease_out(seg(t, a0, a0 + 0.3))
            place(c, lay, x, yy, alpha=ea, anchor="lm")
        d.line([(x, cy + 74), (cx + 760, cy + 74)], fill=(255, 255, 255, int(34 * e)), width=2)
        eb = ease_out(seg(t, 0.50, 0.78))
        place(c, rect_layer(210, 58, 12, fill=(0, 103, 192, 255)), x + 105, cy + 150, alpha=eb)
        place(c, self.b1, x + 105, cy + 150, alpha=eb)
        place(c, rect_layer(210, 58, 12, outline=(255, 255, 255, 92), width=2), x + 340, cy + 150, alpha=eb)
        place(c, self.b2, x + 340, cy + 150, alpha=eb)
        place(c, BR, 1812, 58, anchor="rm")


# ============ F18 · 这张图是什么（故事卡） ============
class F18Story(S.Scene):
    dur = 4.3

    def setup(self):
        self.photo = thumb("20261123", (620, 400))
        self.q = text_layer("这张图是哪儿？", font(52), WHITE)
        self.head = text_layer("当天的故事", font(34), ACC, shadow=False)
        self.body = [text_layer(s, font(31), (232, 238, 246), shadow=False) for s in
                     ["几千万年前的岩浆慢慢冷却，",
                      "收缩时裂成一根根六棱柱。",
                      "风吹雨打把周围削掉，",
                      "剩下这些立着的几何体。"]]

    def render(self, c, t):
        c.alpha_composite(photo_bg(key="F18Story"))
        e0 = ease_out(seg(t, 0.0, 0.3))
        place(c, self.q, W // 2, 200, alpha=e0)
        cx, cy = W // 2, 520
        e1 = ease_out_back(seg(t, 0.14, 0.46), 1.4)
        panel(c, cx, cy, 1500, 520, 20, (16, 22, 32), (255, 255, 255, 50), clamp01(e1 * 1.3))
        place(c, self.photo.convert("RGBA"), cx - 380, cy, alpha=clamp01(e1 * 1.3))
        d = ImageDraw.Draw(c)
        d.rectangle([cx - 690, cy - 200, cx - 70, cy + 200], outline=(255, 255, 255, int(48 * clamp01(e1 * 1.3))), width=2)
        ea = ease_out(seg(t, 0.34, 0.58))
        place(c, self.head, cx - 20, cy - 170, alpha=ea, anchor="lm")
        for i, lay in enumerate(self.body):
            e = ease_out(seg(t, 0.42 + i * 0.08, 0.62 + i * 0.08))
            place(c, lay, cx - 20, cy - 100 + i * 52, alpha=e, anchor="lm")
        place(c, BR, 1812, 58, anchor="rm")


# ============ F19 · 往年的今天 ============
class F19SameDay(S.Scene):
    dur = 4.2

    def setup(self):
        idx = files_by_date()
        self.rows = []
        for y in range(2009, 2027):
            if f"{y}1125" in idx:
                self.rows.append((y, None))
        self.head = text_layer("往年的今天", font(56), WHITE)
        self.sub = text_layer("11 月 25 日", font(32), ACC, shadow=False)

    def render(self, c, t):
        c.alpha_composite(photo_bg(key="F19SameDay"))
        e0 = ease_out(seg(t, 0.0, 0.3))
        place(c, self.head, 96, 150, alpha=e0, anchor="lm")
        place(c, self.sub, 100, 208, alpha=ease_out(seg(t, 0.1, 0.4)), anchor="lm")
        cols, tw, th, gx, gy = 9, 196, 110, 22, 168
        start_x = (W - (cols * tw + (cols - 1) * gx)) // 2 + tw // 2
        for i, (yr, _im) in enumerate(self.rows):
            r, k = divmod(i, cols)
            x = start_x + k * (tw + gx)
            y = 300 + r * gy
            e = ease_out(seg(t, 0.05 + i * 0.022, 0.26 + i * 0.022))
            if e <= 0.01:
                continue
            im = thumb(str(yr) + "1125", (tw, th))
            img_card(c, im, x, y + 16 * (1 - e), a=e, border=True)
            place(c, text_layer(str(yr), font(24), ACC, shadow=False), x, y + th / 2 + 22, alpha=e, anchor="mm")
        place(c, BR, 1812, 58, anchor="rm")


# ============ F20 · 时间轴拖动 ============
class F20Drag(S.Scene):
    dur = 4.2

    def setup(self):
        self.years = ["2024", "2025", "2026"]
        self.lay = {y: text_layer(y, font(56), WHITE, shadow=False) for y in self.years}
        self.lay_off = {y: text_layer(y, font(44), MUT, shadow=False) for y in self.years}
        self.hi = rect_layer(260, 110, 14, fill=(86, 182, 255, 52), outline=ACC + (255,), width=3)

    def render(self, c, t):
        c.alpha_composite(photo_bg(key="F20Drag"))
        swipe = ease_inout(seg(t, 0.15, 0.62))
        hx = 1180 + 130 + (760 - 1180) * swipe
        d = ImageDraw.Draw(c)
        for i, yr in enumerate(self.years):
            x = 340 + i * 420 + 130
            d.rounded_rectangle([x - 130, 210, x + 130, 320], radius=14, outline=(255, 255, 255, 70), width=3)
        place(c, self.hi, hx, 265)
        for i, yr in enumerate(self.years):
            x = 340 + i * 420 + 130
            on = 1.0 - min(1.0, abs(hx - x) / 260)
            place(c, self.lay[yr], x, 265, alpha=clamp01(0.3 + 0.7 * on))
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
        place(c, BR, 1812, 58, anchor="rm")


# ============ F21 · 照片墙 ============
class F21Wall(S.Scene):
    dur = 3.9

    def setup(self):
        random.seed(11)
        idx = sorted(files_by_date().keys())
        picks = random.sample(idx, 88)
        self.tiles = [thumb(d, (192, 108)) for d in picks]
        self.head = text_layer("照片墙", font(54), WHITE)
        self.sub = text_layer("6339 天，一整面铺满", font(30), MUT, shadow=False)

    def render(self, c, t):
        c.alpha_composite(photo_bg(key="F21Wall"))
        cols, rows = 10, 6
        tw, th = 192, 90
        gap = 6
        off = ease_inout(t) * 130
        if not hasattr(self, "_wall_cache"):
            grid = Image.new("RGB", (W, (rows + 1) * (th + gap) + 60), (9, 12, 18))
            for r in range(rows + 1):
                for k in range(cols):
                    i = r * cols + k
                    if i >= len(self.tiles):
                        break
                    grid.paste(self.tiles[i], (k * (tw + gap) + 20, r * (th + gap) + 10))
            self._wall_cache = fx.wall_perspective(grid, 0.20)
            self._wall_h = grid.height
        d = ImageDraw.Draw(c)
        wall = self._wall_cache
        crop_y = int(min(max(0, off), max(0, self._wall_h - H)))
        c.alpha_composite(wall.crop((0, crop_y, W, crop_y + H)).convert("RGBA"))
        # 顶部渐变遮住滚动边
        top = Image.new("RGBA", (W, 200), (0, 0, 0, 0))
        dd = ImageDraw.Draw(top)
        for yy in range(200):
            dd.line([(0, yy), (W, yy)], fill=(9, 12, 18, int(232 * (1 - yy / 200) ** 1.2)))
        c.alpha_composite(top, (0, 90))
        e = ease_out(seg(t, 0.0, 0.3))
        band = Image.new("RGBA", (W, 150), (0, 0, 0, 0))
        ImageDraw.Draw(band).rectangle([0, 0, W, 150], fill=(9, 12, 18, 225))
        c.alpha_composite(band, (0, 60))
        place(c, self.head, 96, 110, alpha=e, anchor="lm")
        place(c, self.sub, 100, 164, alpha=ease_out(seg(t, 0.1, 0.4)), anchor="lm")
        place(c, BR, 1812, 58, anchor="rm")


# ============ F22 · 节日照片墙 ============
class F22Festival(S.Scene):
    dur = 4.7

    def setup(self):
        mid = [("20160915", "各式各样的月饼模具"), ("20180924", "桨声灯影里的秦淮河"),
               ("20190913", "中秋"), ("20201001", "庆祝中秋的灯笼"), ("20210921", "中秋快乐")]
        spr = [("20180216", "写春联庆新春"), ("20190205", "春节"), ("20200125", "春节"),
               ("20220201", "春节灯笼"), ("20230122", "兔年大吉"), ("20250129", "春节快乐")]
        self.sets = [("中秋", [(d, thumb(d, (330, 186))) for d, _ in mid]),
                     ("春节", [(d, thumb(d, (330, 186))) for d, _ in spr])]

    def render(self, c, t):
        c.alpha_composite(photo_bg(key="F22Festival"))
        idx = 0 if t < 0.55 else 1
        name, imgs = self.sets[idx]
        k0 = ease_out(seg(t, 0.0 if idx == 0 else 0.52, 0.30 if idx == 0 else 0.68))
        n = len(imgs)
        for i, (d8, im) in enumerate(imgs):
            x = W / 2 + (i - (n - 1) / 2) * 360
            y = 470
            e = ease_out(seg(t, (0.02 if idx == 0 else 0.56) + i * 0.04,
                             (0.24 if idx == 0 else 0.74) + i * 0.04))
            if e <= 0.01:
                continue
            img_card(c, im, x, y + 26 * (1 - e), a=e, border=True)
        place(c, text_layer(name, font(72), GOLD), W // 2, 230, alpha=k0, anchor="mm")
        place(c, text_layer("历年这一天，铺满一屏", font(30), MUT, shadow=False),
              W // 2, 296, alpha=k0, anchor="mm")
        place(c, BR, 1812, 58, anchor="rm")


# ============ F23 · 搜索 ============
class F23Search(S.Scene):
    dur = 5.9

    def setup(self):
        idx = files_by_date()
        self.head = text_layer("6339 条本地索引  ·  断网也能搜", font(38), MUT, shadow=False)
        import json as _json
        _idx = _json.load(open(r"D:\Tools\bing-wallpaper\archive\all-index.json", encoding="utf-8"))
        _t = {r["d"]: r.get("t", "") for r in _idx if isinstance(r, dict) and r.get("d")}
        self.hits = []
        for d8 in sorted(idx):
            if "樱" in _t.get(d8, "") and len(self.hits) < 12:
                self.hits.append(thumb(d8, (240, 135)))
        self.q = "樱"

    def render(self, c, t):
        c.alpha_composite(photo_bg(key="F23Search"))
        place(c, self.head, 96, 150, alpha=ease_out(seg(t, 0.0, 0.28)), anchor="lm")
        e = ease_out(seg(t, 0.04, 0.30))
        panel(c, 96 + 552, 250 - 20 * (1 - e), 1104, 96, 16, (18, 23, 33), (86, 182, 255, 255), e)
        cut = int(round(len(self.q) * seg(t, 0.18, 0.34)))
        if cut > 0:
            place(c, text_layer(self.q[:cut], font(46), WHITE, shadow=False), 128, 250, anchor="lm")
        if cut >= len(self.q) and int(t * 24) % 2 == 0:
            ImageDraw.Draw(c).line([(1260, 226), (1260, 274)], fill=ACC + (255,), width=3)
        eh = ease_out(seg(t, 0.36, 0.56))
        place(c, text_layer("命中 9 张  ·  断网也能搜", font(30), ACC, shadow=False), 100, 330, alpha=eh, anchor="lm")
        cols = 6
        for i, im in enumerate(self.hits):
            a0 = 0.38 + i * 0.045
            ea = ease_out(seg(t, a0, a0 + 0.22))
            r, k = divmod(i, cols)
            x = 96 + k * 268 + 120
            y = 400 + r * 160
            if ea > 0.01:
                c.alpha_composite(im.convert("RGBA"), (int(x - 120), int(y - 67 + 16 * (1 - ea))))
        place(c, BR, 1812, 58, anchor="rm")


# ============ F24 · 新鲜感 vs 回忆 ============
class F24FreshVsMemory(S.Scene):
    dur = 2.9

    def setup(self):
        random.seed(23)
        idx = sorted(files_by_date().keys())
        self.a = [thumb(d, (300, 169)) for d in random.sample(idx, 4)]

    def render(self, c, t):
        c.alpha_composite(photo_bg(key="F24FreshVsMemory"))
        d = ImageDraw.Draw(c)
        d.line([(W // 2, 220), (W // 2, 820)], fill=(255, 255, 255, 40), width=2)
        # 左：每天换一张（快速切换后消失）
        ph = int(t * 10) % len(self.a)
        e = ease_out(seg(t, 0.0, 0.2))
        if e > 0.01:
            c.alpha_composite(self.a[ph].convert("RGBA"), (W // 4 - 150, 380))
        d.text((W // 4, 300), "新鲜感", font=font(44), fill=MUT + (int(255 * e),), anchor="mm")
        d.text((W // 4, 620), "第二天就没了", font=font(30), fill=(255, 92, 92) + (int(230 * e),), anchor="mm")
        # 右：不断累积
        for i in range(6):
            ea = ease_out(seg(t, 0.20 + i * 0.10, 0.36 + i * 0.10))
            if ea <= 0.01:
                continue
            x = W * 3 // 4 - 150 + (i % 3) * 104
            y = 380 + (i // 3) * 100
            im = self.a[i % len(self.a)].resize((100, 56), Image.LANCZOS)
            c.alpha_composite(im.convert("RGBA"), (x, y))
        d.text((W * 3 // 4, 300), "回忆", font=font(44), fill=ACC + (255,), anchor="mm")
        d.text((W * 3 // 4, 620), "越攒越多", font=font(30), fill=ACC + (230,), anchor="mm")
        place(c, BR, 1812, 58, anchor="rm")


# ============ F25 · 浮窗消失 ============
class F25NoPopup(S.Scene):
    dur = 4.3

    def setup(self):
        self.base = E.load_evidence("E3-official.png")
        # 从 E3 原图裁出右上角那两个浮空件
        raw = Image.open(os.path.join(EV, "E3-official.png")).convert("RGB")
        crop = raw.crop((1700, 0, 1766, 90))
        self.pop = crop.resize((crop.width * 5, crop.height * 5), Image.LANCZOS).convert("RGBA")
        self.cap = text_layer("不弹窗，不推广告", font(56), WHITE)

    def render(self, c, t):
        c.alpha_composite(E.view(self.base, 0.5, 0.5, 1.0, bright=0.55))
        c.alpha_composite(scrim_layer(0.20, 0.80))
        # 浮窗先出现，再被推远淡出
        appear = ease_out(seg(t, 0.0, 0.18))
        gone = ease_out(seg(t, 0.42, 0.72))
        sc = 1.0 - 0.75 * gone
        a = appear * (1 - gone)
        if a > 0.01:
            place(c, self.pop, 1660, 200, scale=sc, alpha=a * 0.95)
        d = ImageDraw.Draw(c)
        if 0.10 < t < 0.42:
            k = ease_out(seg(t, 0.10, 0.30))
            d.rectangle([1470, 40, 1850, 360], outline=WARN + (int(200 * k * (1 - gone)),), width=5)
        place(c, self.cap, 96, 480, alpha=ease_out(seg(t, 0.46, 0.74)), anchor="lm")
        place(c, BR, 1812, 58, anchor="rm")


# ============ F26 · 三天写出来 ============
class F26ThreeDays(S.Scene):
    dur = 6.7

    def setup(self):
        self.days = [text_layer(f"第 {i+1} 天", font(30), MUT, shadow=False) for i in range(3)]
        self.big = text_layer("三天", font(180), WHITE)
        self.sub = text_layer("我和大肥鱼", font(44), MUT)
        self.repo = text_layer("github.com/zkyhzxf/bing-wallpaper-chronicle", font(34), ACC, shadow=False)

    def render(self, c, t):
        c.alpha_composite(photo_bg(key="F26ThreeDays"))
        # 三根竖条依次涨满
        for i in range(3):
            k = ease_out(seg(t, 0.02 + i * 0.13, 0.20 + i * 0.13))
            if k <= 0.01:
                continue
            x = W / 2 - 200 + i * 200
            h = 320 * k
            d = ImageDraw.Draw(c)
            d.rounded_rectangle([x - 46, 620 - h, x + 46, 620], radius=8,
                                fill=(86, 182, 255, int(220 * k)) if i < 2 else (255, 199, 102, 245))
            place(c, self.days[i], x, 660, alpha=k)
        e = ease_out(seg(t, 0.30, 0.62))
        place(c, self.big, W // 2, 200, alpha=e, dy=20 * (1 - e))
        place(c, self.sub, W // 2, 372, alpha=ease_out(seg(t, 0.44, 0.72)))
        place(c, self.repo, W // 2, 760, alpha=ease_out(seg(t, 0.62, 0.88)))
        place(c, BR, 1812, 58, anchor="rm")




# ============ F26b · 免费开源（句 27） ============
class F26bOpen(S.Scene):
    dur = 3.3

    def setup(self):
        self.title = text_layer("zkyhzxf / bing-wallpaper-chronicle", font(44), WHITE)
        self.tags = [("PowerShell", ACC), ("Windows", (140, 200, 255)), ("MIT", GOLD)]
        self.line1 = text_layer("免费开源  ·  解压即用  ·  不用注册", font(40), WHITE)
        self.line2 = text_layer("6339 天索引  ·  102 项回归断言", font(28), MUT, shadow=False)

    def render(self, c, t):
        c.alpha_composite(photo_bg(key="F26bOpen", blur=24, bright=0.16))
        e = ease_out(seg(t, 0.0, 0.34))
        cx, cy = W // 2, 470 + 30 * (1 - e)
        panel(c, cx, cy, 1520, 400, 20, (16, 22, 32), (255, 255, 255, 48), clamp01(e * 1.4))
        d = ImageDraw.Draw(c)
        d.rounded_rectangle([cx - 740, cy - 178, cx + 740, cy - 118], radius=16, fill=(30, 36, 45, 255))
        for i, cc in enumerate([(255, 95, 86), (255, 189, 46), (39, 201, 63)]):
            d.ellipse([cx - 712 + i * 26, cy - 160, cx - 696 + i * 26, cy - 144], fill=cc)
        place(c, self.title, cx - 690, cy - 60, alpha=e, anchor="lm")
        x = cx - 690
        for tt, cc in self.tags:
            bw = 34 + len(tt) * 15
            place(c, rect_layer(bw, 44, 22, fill=cc + (44,), outline=cc + (170,), width=2),
                  x + bw // 2, cy + 20, alpha=e)
            place(c, text_layer(tt, font(22), cc, shadow=False), x + bw // 2, cy + 20, alpha=e)
            x += bw + 18
        place(c, self.line1, cx - 690, cy + 96, alpha=ease_out(seg(t, 0.28, 0.62)), anchor="lm")
        place(c, self.line2, cx - 690, cy + 148, alpha=ease_out(seg(t, 0.42, 0.76)), anchor="lm")
        place(c, BR, 1812, 58, anchor="rm")


# ============ F27 · 搜仓库 ============
class F27SearchRepo(S.Scene):
    dur = 3.8

    def setup(self):
        self.q = "必应壁纸编年史"
        self.hit = text_layer("zkyhzxf / bing-wallpaper-chronicle", font(40), WHITE, shadow=False)
        self.sub = text_layer("必应壁纸编年史 · 6339 天档案", font(28), MUT, shadow=False)

    def render(self, c, t):
        c.alpha_composite(photo_bg(key="F27SearchRepo"))
        e = ease_out(seg(t, 0.0, 0.24))
        panel(c, W // 2, 300, 1200, 96, 48, (22, 27, 34), (255, 255, 255, 80), e)
        cut = int(round(len(self.q) * seg(t, 0.10, 0.52)))
        if cut > 0:
            place(c, text_layer(self.q[:cut], font(44), WHITE, shadow=False), 400, 300, anchor="lm")
        eh = ease_out(seg(t, 0.54, 0.78))
        if eh > 0.01:
            panel(c, W // 2, 500, 1200, 130, 14, (16, 22, 32), (255, 255, 255, 44), eh)
            place(c, self.hit, 400, 478, alpha=eh, anchor="lm")
            place(c, self.sub, 400, 528, alpha=eh, anchor="lm")
        place(c, BR, 1812, 58, anchor="rm")


# ============ F28 · 点赞 ============
class F28Like(S.Scene):
    dur = 3.2

    def setup(self):
        self.cap = text_layer("让更多被浮窗烦过的人看见", font(52), WHITE)
        self.btn = rect_layer(220, 110, 55, fill=(30, 36, 46), outline=(255, 255, 255, 70), width=3)
        self.thumb = text_layer("赞", font(56), WHITE, shadow=False)

    def render(self, c, t):
        c.alpha_composite(photo_bg(key="F28Like"))
        # 背景掠过一堆浮窗图标，表示"被烦过的人"
        d = ImageDraw.Draw(c)
        for i in range(14):
            x = (i * 240 + t * 700) % (W + 300) - 150
            y = 220 + (i % 4) * 150
            a = 16 + (i % 3) * 7
            d.rounded_rectangle([x, y, x + 110, y + 76], radius=12, fill=(120, 132, 150, a))
        e = ease_out(seg(t, 0.0, 0.3))
        place(c, self.cap, W // 2, 420, alpha=e, dy=18 * (1 - e))
        # 点赞按钮脉冲
        k = ease_out_back(seg(t, 0.24, 0.52), 2.0)
        if k > 0.02:
            pulse = 0.0
            for ph in (0.55, 0.66, 0.78):
                p = seg(t, ph, ph + 0.08)
                if 0 < p < 1:
                    pulse = max(pulse, math.sin(p * math.pi) * 0.06)
            sc = (0.7 + 0.3 * k) * (1 + pulse)
            place(c, self.btn, W // 2, 620, scale=sc, alpha=clamp01(k * 1.5))
            place(c, self.thumb, W // 2, 620, scale=sc, alpha=clamp01(k * 1.5))
        place(c, BR, 1812, 58, anchor="rm")


# ============ F29 · 收藏 ============
class F29Bookmark(S.Scene):
    dur = 3.9

    def setup(self):
        self.cap = text_layer("收藏可以收一个", font(56), WHITE)
        self.sub = text_layer("就像编年史帮你收藏那张图", font(40), ACC)
        self.card = thumb("20251207", (360, 202))

    def render(self, c, t):
        c.alpha_composite(photo_bg(key="F29Bookmark"))
        e = ease_out(seg(t, 0.0, 0.3))
        place(c, self.cap, W // 2, 360, alpha=e, dy=16 * (1 - e))
        place(c, self.sub, W // 2, 450, alpha=ease_out(seg(t, 0.22, 0.52)))
        # 图从中心飞向左上角并缩小（被收藏进册子）
        k = ease_inout(seg(t, 0.46, 0.92))
        x = W / 2 + (220 - W / 2) * k
        y = 620 + (240 - 620) * k
        sc = 1 - 0.72 * k
        a = 1 - 0.92 * k
        if a > 0.01:
            img_card(c, self.card, x, y, a=a, border=True, shadow=k < 0.5)
        d = ImageDraw.Draw(c)
        if k > 0.85:
            kk = (k - 0.85) / 0.15
            d.rectangle([180, 200, 260, 280], outline=ACC + (int(220 * kk),), width=3)
            d.text((220, 300), "已收藏", font=font(26), fill=ACC + (int(230 * kk),), anchor="mm")
        place(c, BR, 1812, 58, anchor="rm")


# ============ F30 · 片尾 ============
class F30End(S.Scene):
    dur = 3.8

    def setup(self):
        import qrcode
        q = qrcode.QRCode(box_size=5, border=1)
        q.add_data("https://github.com/zkyhzxf/bing-wallpaper-chronicle")
        q.make(fit=True)
        qr = q.make_image(fill_color="black", back_color="white").convert("RGB").resize((240, 240), Image.NEAREST)
        card = Image.new("RGBA", (276, 276), WHITE + (255,))
        card.paste(qr, (18, 18))
        self.qr = card
        self.t1 = text_layer("必年代记", font(150), WHITE)
        self.t2 = text_layer("把必应每天散落的那张图，捡回来", font(46), WHITE)
        self.t3 = text_layer("免费开源  ·  解压即用  ·  不用注册", font(30), MUT, shadow=False)

    def render(self, c, t):
        c.alpha_composite(scrim_layer(0.34, 0.94))
        c.alpha_composite(photo_bg(key="F30End"))
        e1 = ease_out(seg(t, 0.0, 0.36))
        place(c, self.t1, 108, 330, alpha=e1, anchor="lm")
        e2 = ease_out(seg(t, 0.14, 0.50))
        place(c, self.t2, 112, 470, alpha=e2, anchor="lm")
        d = ImageDraw.Draw(c)
        k = ease_out(seg(t, 0.28, 0.58))
        if k > 0.01:
            d.line([(112, 530), (112 + 760 * k, 530)], fill=ACC + (255,), width=5)
        place(c, self.t3, 112, 578, alpha=ease_out(seg(t, 0.42, 0.70)), anchor="lm")
        eq = ease_out_back(seg(t, 0.30, 0.70), 2.2)
        place(c, self.qr, 1600, 470, scale=0.55 + 0.45 * eq, alpha=clamp01(seg(t, 0.28, 0.42) * 1.4))
        place(c, text_layer("扫码打开仓库", font(26), WHITE, shadow=False), 1600, 320,
              alpha=ease_out(seg(t, 0.55, 0.80)), anchor="mm")
        place(c, BR, 1812, 58, anchor="rm")
