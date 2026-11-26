# -*- coding: utf-8 -*-
"""特效组件：精细羽毛 / 透视照片墙 / Y 轴翻转 / 3D 倾斜卡片"""
import math
from PIL import Image, ImageDraw, ImageFilter, ImageEnhance

W, H = 1920, 1080


# ---------------- 羽毛 ----------------
def feather_layer(size=820, sweep=-0.42, tint=(255, 255, 255), tip_alpha=255):
    """画一根有羽片、绒毛、羽轴高光的羽毛。
    sweep: 羽轴倾斜，负值向右上；羽片宽度沿轴按 sin 曲线变化。
    """
    S = size
    im = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    cx = cy = S / 2
    L = S * 0.78
    x0, y0 = cx - L * 0.42, cy + L * 0.36
    x1, y1 = cx + L * 0.30, cy - L * 0.40
    dx, dy = x1 - x0, y1 - y0
    ln = math.hypot(dx, dy)
    ux, uy = dx / ln, dy / ln
    nx, ny = -uy, ux

    def P(t):
        return x0 + dx * t, y0 + dy * t

    def halfwidth(t):
        return max(2.0, math.sin(math.pi * (t ** 0.78)) * L * 0.155)

    N = 90
    # 羽片：左右各一条多边形带
    for side in (-1, 1):
        pts_out, pts_in = [], []
        for i in range(N + 1):
            t = i / N
            px, py = P(t)
            hw = halfwidth(t) * side
            pts_out.append((px + nx * hw, py + ny * hw))
            pts_in.append((px + nx * hw * 0.012, py + ny * hw * 0.012))
        poly = pts_out + pts_in[::-1]
        d.polygon(poly, fill=tint + (int(tip_alpha * 0.72),))
        # 羽枝：边缘每隔几点甩出一条
        for i in range(0, N + 1, 3):
            t = i / N
            px, py = P(t)
            hw = halfwidth(t) * side
            ex, ey = px + nx * hw, py + ny * hw
            # 羽枝斜向前方，越靠中段越长
            skew = hw * 0.34
            tx, ty = ex + ux * skew, ey + uy * skew
            d.line([(px, py), (tx, ty)], fill=tint + (int(tip_alpha * 0.30),), width=2)
            d.line([(tx, ty), (tx + ux * 6, ty + uy * 6)],
                   fill=tint + (int(tip_alpha * 0.20),), width=1)

    # 羽轴：锥形 + 高光
    for i in range(N):
        t0, t1 = i / N, (i + 1) / N
        w0 = max(2, int(10 * (1 - abs(t0 - 0.45) * 1.35)))
        d.line([P(t0), P(t1)], fill=tint + (min(255, tip_alpha),), width=w0)
    for i in range(N):
        t0, t1 = i / N, (i + 1) / N
        d.line([P(t0), P(t1)], fill=(255, 255, 255, int(tip_alpha * 0.55)), width=1)

    # 根部羽柄
    r0 = (x0 - ux * L * 0.10, y0 - uy * L * 0.10)
    d.line([r0, (x0, y0)], fill=tint + (int(tip_alpha * 0.9),), width=4)

    return im.rotate(math.degrees(sweep), resample=Image.BICUBIC, expand=False)


# ---------------- 透视照片墙 ----------------
def wall_perspective(im, top_inset=0.20, bg=(9, 12, 18), curve=1.35):
    """把平铺网格压成「向远处倒」的墙：顶部窄、底部宽。
    逐行缩放，比 QUAD 可控，也不会有方向搞反的问题。
    """
    w, h = im.size
    out = Image.new("RGB", (w, h), bg)
    max_in = int(w * top_inset)
    for y in range(h):
        t = y / h
        inset = int(max_in * ((1 - t) ** curve))
        src = im.crop((inset, y, w - inset, y + 1))
        nw = w - inset * 2
        if nw < 4:
            continue
        out.paste(src.resize((nw, 1), Image.BILINEAR), (inset, y))
    # 顶部远端轻微虚化
    far = out.crop((0, 0, w, int(h * 0.30))).filter(ImageFilter.GaussianBlur(1.4))
    out.paste(far, (0, 0))
    return out


def wall_fade(im, top_a=210, bot_a=235):
    """墙的上下做渐隐，接画面底色"""
    w, h = im.size
    ov = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(ov)
    for y in range(h):
        t = y / h
        a = int(255 * (1 - (1 - t) ** 0.6)) if t < 0.5 else int(255 * (t ** 1.4))
        d.line([(0, y), (w, y)], fill=(9, 12, 18, int(a * 0.9)))
    return Image.alpha_composite(im.convert("RGBA"), ov)


# ---------------- Y 轴翻转 ----------------
def flip_h(im, k):
    """k ∈ [0,1]：0 正常 → 0.5 侧面朝你（宽度归零）→ 1 完全翻面"""
    ang = k * math.pi
    c = abs(math.cos(ang))
    w = max(2, int(im.width * c))
    out = im.resize((w, im.height), Image.LANCZOS)
    if math.cos(ang) < 0:
        out = out.transpose(Image.FLIP_LEFT_RIGHT)
    # 中间偏暗，模拟翻面时的光照
    if c < 0.35:
        out = ImageEnhance.Brightness(out).enhance(0.45 + c * 1.5)
    return out


# ---------------- 3D 倾斜卡片 ----------------
def tilt_card(im, ax=0.0, ay=0.0, persp=0.28):
    """把卡片做 3D 倾斜：ax 绕水平轴（上下翻），ay 绕垂直轴（左右转），单位弧度"""
    w, h = im.size
    pad = int(max(w, h) * persp)
    canvas = Image.new("RGBA", (w + pad * 2, h + pad * 2), (0, 0, 0, 0))
    canvas.alpha_composite(im.convert("RGBA"), (pad, pad))
    dx = math.sin(ay) * pad
    dy = math.sin(ax) * pad
    data = (pad + dx, pad + dy, pad + w - dx, pad + dy,
            pad + w + dx, pad + h - dy, pad - dx, pad + h - dy)
    out = canvas.transform(canvas.size, Image.QUAD,
                           (pad, pad, pad + w, pad, pad + w, pad + h, pad, pad + h),
                           Image.BICUBIC)
    return out


def shadow_under(im, blur=18, alpha=150, dy=16):
    sh = Image.new("RGBA", im.size, (0, 0, 0, 0))
    a = im.getchannel("A").point(lambda v: int(v * alpha / 255))
    sh.putalpha(a)
    sh = sh.filter(ImageFilter.GaussianBlur(blur))
    out = Image.new("RGBA", (im.width, im.height + dy), (0, 0, 0, 0))
    out.alpha_composite(sh, (0, dy))
    out.alpha_composite(im, (0, 0))
    return out


def kenburns(im, z, px=0.0, py=0.0):
    """对已铺满的图做取景（z>1 推近）"""
    w, h = im.size
    vw, vh = w / z, h / z
    x = max(0, min(w - vw, w / 2 + px * (w - vw) / 2 - vw / 2))
    y = max(0, min(h - vh, h / 2 + py * (h - vh) / 2 - vh / 2))
    return im.crop((int(x), int(y), int(x + vw), int(y + vh))).resize((w, h), Image.LANCZOS)
