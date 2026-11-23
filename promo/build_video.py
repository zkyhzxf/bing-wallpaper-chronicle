# -*- coding: utf-8 -*-
"""必应壁纸编年史 · 30s animatic 渲染管线
读 promo/tts/durations.json -> 算时间轴 -> 出字幕 -> 跑 ffmpeg -> ffprobe 验收
"""
import json, os, re, subprocess, sys, time

BASE = os.path.dirname(os.path.abspath(__file__))
SCR = os.path.join(BASE, "screens")
TTS = os.path.join(BASE, "tts")
OUT = os.path.join(BASE, "out")
TMP = os.path.join(BASE, "_build")
FF = r"D:\tools\ffmpeg\ffmpeg.exe"
FP = r"D:\tools\ffmpeg\ffprobe.exe"
FONT_B = r"C:\Windows\Fonts\Noto Sans SC Bold (TrueType).otf"

W, H, FPS = 1920, 1080, 30
SUB_Y = 760   # 字幕带顶部，对应静帧安全区下限 700
_CFG = json.load(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "config.json"), encoding="utf-8"))
LEAD = _CFG.get("lead", 0.400)
GAP = _CFG.get("gap", 0.900)
TAIL = None
TR_DUR = [0.20, 0.24, 0.18, 0.20]          # 4 个段间转场
TR_NAME = ["fadewhite", "pixelize", "hlslice", "fade"]

# 每个画面段内的静帧（顺序即播放顺序）
SHOTS = _CFG.get("shots", [
    ["s01", "s02", "s03"], ["s04", "s05", "s06"], ["s07", "s08"],
    ["s09", "s10", "s11"], ["s12", "s13", "s14"],
])

# 段内切换点（全局秒），与台词断句对齐；None 表示用平均分
INNER = _CFG.get("inner", [[0.28, 0.60], [0.36, 0.69], [0.55], [0.34, 0.68], [0.36, 0.63]])

# 字幕（每段拆几条），与 tts/0N.vtt 的分句一致
SUBS = [
    ["必应官方只留 8 天", "我留了 6339 天，一天不缺"],
    ["大会员到账，浮窗也到账", "昨天的图，今天就没了"],
    ["必应壁纸编年史，必年代记", "每张壁纸是一根羽毛"],
    ["往左是昨天，往上翻是上个月", "去年的今天，菊石还在"],
    ["DSH 三天做的，免费开源", "GitHub 搜必应壁纸编年史"],
]

os.makedirs(OUT, exist_ok=True)
os.makedirs(TMP, exist_ok=True)


def run(cmd, tag=""):
    r = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", errors="replace")
    if r.returncode != 0:
        print(f"[FAIL {tag}] rc={r.returncode}")
        print((r.stderr or "")[-1800:])
        sys.exit(1)
    return r


def vtt_spans(n):
    """从 tts/0N.vtt 取分句相对时间，作为字幕区间依据"""
    p = os.path.join(TTS, "0%d.vtt" % n)
    spans = []
    if not os.path.exists(p):
        return spans
    for m in re.finditer(r"(\d{2}):(\d{2}):(\d{2})[,.](\d{3})\s*-->\s*(\d{2}):(\d{2}):(\d{2})[,.](\d{3})", open(p, encoding="utf-8").read()):
        g = [int(x) for x in m.groups()]
        a = g[0] * 3600 + g[1] * 60 + g[2] + g[3] / 1000
        b = g[4] * 3600 + g[5] * 60 + g[6] + g[7] / 1000
        spans.append([a, b])
    return spans


# ---------- 1. 时间轴 ----------
dur = json.load(open(os.path.join(TTS, "durations.json"), encoding="utf-8"))
segs = dur["segments"]
a_start, t = [], LEAD
for s in segs:
    a_start.append(round(t, 3))
    t += s["dur"] + GAP
speech_end = round(a_start[-1] + segs[-1]["dur"], 3)
total = _CFG.get("total", 30.0)

centers = []
for i in range(len(segs) - 1):
    gap_mid = (a_start[i] + segs[i]["dur"] + a_start[i + 1]) / 2
    centers.append(round(gap_mid, 3))
offsets = [round(c - d / 2, 3) for c, d in zip(centers, TR_DUR)]

# 每个画面段素材长度
clip_len = []
clip_len.append(round(offsets[0] + TR_DUR[0], 3))
for i in range(1, len(segs) - 1):
    clip_len.append(round(offsets[i] + TR_DUR[i] - offsets[i - 1], 3))
clip_len.append(round(total - offsets[-1], 3))

print("音频起点:", a_start)
print("转场中心:", centers)
print("xfade offset:", offsets)
print("段素材长:", clip_len, "| 预估总长", round(sum(clip_len) - sum(TR_DUR), 3))

timeline = {
    "total": total, "fps": FPS, "size": [W, H],
    "audio": [{"i": i + 1, "start": a_start[i], "dur": segs[i]["dur"], "text": segs[i]["text"]} for i in range(len(segs))],
    "transitions": [{"i": i + 1, "at": centers[i], "offset": offsets[i], "dur": TR_DUR[i], "name": TR_NAME[i]} for i in range(len(TR_DUR))],
    "clips": [],
}

# ---------- 2. 段内静帧时长 ----------
plan = []
for i in range(len(SHOTS)):
    c0 = 0.0 if i == 0 else offsets[i - 1]
    shots = SHOTS[i]
    cuts = [round(c0 + clip_len[i] * r, 3) for r in INNER[i]]
    bounds = [c0] + cuts + [c0 + clip_len[i]]
    durs = [round(bounds[k + 1] - bounds[k], 3) for k in range(len(shots))]
    plan.append({"clip": i + 1, "start": c0, "len": clip_len[i], "items": list(zip(shots, durs))})
    timeline["clips"].append({"clip": i + 1, "start": c0, "len": clip_len[i],
                              "shots": [{"img": s, "dur": d} for s, d in zip(shots, durs)]})

json.dump(timeline, open(os.path.join(BASE, "timeline.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=2)
json.dump({"transitions": timeline["transitions"]}, open(os.path.join(BASE, "transitions.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=2)

# ---------- 3. 字幕时间 ----------
subs = []
for i, lines in enumerate(SUBS):
    base = a_start[i]
    spans = vtt_spans(i + 1)
    if len(spans) >= len(lines):
        marks = [s[0] for s in spans[: len(lines)]] + [segs[i]["dur"]]
    else:
        n = len(lines)
        marks = [segs[i]["dur"] * k / n for k in range(n)] + [segs[i]["dur"]]
    for k, ln in enumerate(lines):
        a = base + marks[k]
        b = base + (marks[k + 1] - 0.02 if k + 1 < len(lines) else segs[i]["dur"] - 0.04)
        subs.append({"a": round(a, 3), "b": round(min(b, speech_end + 0.1), 3), "text": ln})

# SRT
def ts_srt(x):
    h = int(x // 3600); m = int(x % 3600 // 60); s = int(x % 60); ms = int(round((x - int(x)) * 1000))
    return f"{h:02d}:{m:02d}:{s:02d},{ms:03d}"

def ts_ass(x):
    h = int(x // 3600); m = int(x % 3600 // 60); s = int(x % 60); cs = int(round((x - int(x)) * 100))
    return f"{h:d}:{m:02d}:{s:02d}.{cs:02d}"

with open(os.path.join(BASE, "subtitle.srt"), "w", encoding="utf-8") as f:
    for i, s in enumerate(subs, 1):
        f.write(f"{i}\n{ts_srt(s['a'])} --> {ts_srt(s['b'])}\n{s['text']}\n\n")

ass_head = """[Script Info]
ScriptType: v4.00+
PlayResX: 1920
PlayResY: 1080
WrapStyle: 0

[V4+ Styles]
Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding
Style: Main,Noto Sans SC,54,&H00FFFFFF,&H00FFFFFF,&H00101010,&H96000000,-1,0,0,0,100,100,1,0,1,4,2,2,120,120,92,1

[Events]
Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
"""
with open(os.path.join(BASE, "subtitle.ass"), "w", encoding="utf-8") as f:
    f.write(ass_head)
    for s in subs:
        f.write(f"Dialogue: 0,{ts_ass(s['a'])},{ts_ass(s['b'])},Main,,0,0,0,,{s['text']}\n")

print("字幕条数:", len(subs))

# ---------- 4. 字幕条 PNG ----------
from PIL import Image, ImageDraw, ImageFont, ImageFilter
SB_W, SB_H = 1920, 220
sb_font = ImageFont.truetype(FONT_B, 54)


def make_sub_png(text, path):
    im = Image.new("RGBA", (SB_W, SB_H), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    b = d.textbbox((0, 0), text, font=sb_font)
    tw, th = b[2] - b[0], b[3] - b[1]
    x = (SB_W - tw) // 2
    y = (SB_H - th) // 2 - b[1]
    # 底部渐变衬底，保证任何画面上都读得清
    for k in range(10):
        d.rectangle([0, SB_H - 46 - k * 5, SB_W, SB_H], fill=(0, 0, 0, 14))
    for ox, oy in ((-4, 0), (4, 0), (0, -4), (0, 4), (-3, 3), (3, 3), (-3, -3), (3, -3)):
        d.text((x + ox, y + oy), text, font=sb_font, fill=(6, 8, 12, 255))
    d.text((x, y + 4), text, font=sb_font, fill=(0, 0, 0, 130))
    d.text((x, y), text, font=sb_font, fill=(255, 255, 255, 255))
    im.save(path)
    return path


sub_pngs = [make_sub_png(s["text"], os.path.join(TMP, f"sub_{i:02d}.png")) for i, s in enumerate(subs, 1)]

# ---------- 5. 场景 -> 逐帧渲染片段（每个镜头独立运动） ----------
import scenes
from PIL import Image

print("场景类:", [c.__name__ for grp in scenes.CLIP_SCENES for c in grp])
concat_files = []
for ci, p in enumerate(plan):
    classes = scenes.CLIP_SCENES[ci]
    if len(classes) != len(p["items"]):
        raise SystemExit(f"clip{p['clip']} 场景数 {len(classes)} 与静帧数 {len(p['items'])} 不符")
    durations = [round(x[1], 3) for x in p["items"]]
    clip_out = os.path.join(TMP, f"clip{p['clip']}.mp4")
    cmd = [FF, "-y", "-loglevel", "error", "-f", "rawvideo", "-pix_fmt", "rgb24",
           "-s", f"{W}x{H}", "-r", str(FPS),
           "-i", "-", "-c:v", "libx264", "-crf", "18", "-preset", "veryfast",
           "-pix_fmt", "yuv420p", "-an", clip_out]
    # stderr 必须落文件：管道写满会让 ffmpeg 停止读 stdin，与写入端互锁
    errlog = os.path.join(TMP, f"enc{ci}.log")
    errf = open(errlog, "wb")
    proc = subprocess.Popen(cmd, stdin=subprocess.PIPE, stdout=subprocess.DEVNULL, stderr=errf)
    canvas = Image.new("RGBA", (W, H), (0, 0, 0, 255))
    instances = [c() for c in classes]
    t_start = time.time()
    n_frames = 0
    for inst, d in zip(instances, durations):
        n = max(2, round(d * FPS))
        n_frames += n
        for k in range(n):
            tint = k / (n - 1) if n > 1 else 1.0
            inst.render(canvas, tint)
            proc.stdin.write(canvas.convert("RGB").tobytes())
            canvas.paste((0, 0, 0, 255), (0, 0, W, H))
    proc.stdin.close()
    proc.wait()
    errf.close()
    if proc.returncode != 0:
        print("[FAIL render]", open(errlog, encoding="utf-8", errors="replace").read()[-1500:])
        sys.exit(1)
    print(f"  clip{p['clip']}: {p['len']}s  {durations}  {n_frames}f  {time.time()-t_start:.1f}s")
    concat_files.append(clip_out)

# ---------- 6. xfade 串联 ----------
inputs = []
for c in concat_files:
    inputs += ["-i", c]
fc = []
prev = "[0:v]"
for i in range(1, len(concat_files)):
    out = f"[vx{i}]" if i < len(concat_files) - 1 else "[vmain]"
    fc.append(f"{prev}[{i}:v]xfade=transition={TR_NAME[i-1]}:duration={TR_DUR[i-1]}:offset={offsets[i-1]}{out}")
    prev = out

main_v = os.path.join(TMP, "main.mp4")
run([FF, "-y"] + inputs + ["-filter_complex", ";".join(fc), "-map", "[vmain]",
                           "-r", str(FPS), "-c:v", "libx264", "-crf", "18", "-preset", "medium",
                           "-pix_fmt", "yuv420p", "-an", main_v], tag="xfade")
print("主体合成完成")

# ---------- 7. 音频 + 字幕 + 出片 ----------
cmd = [FF, "-y", "-i", main_v]
for i in range(len(segs)):
    cmd += ["-i", os.path.join(TTS, "0%dc.mp3" % (i + 1))]
for p in sub_pngs:
    cmd += ["-loop", "1", "-framerate", "2", "-i", p]

n_aud = len(segs)
n_sub = len(sub_pngs)
af = []
for i in range(n_aud):
    ms = int(round(a_start[i] * 1000))
    af.append(f"[{1+i}:a]adelay={ms}|{ms},volume=1.0[a{i}]")
mix = "".join(f"[a{i}]" for i in range(n_aud))
af.append(f"{mix}amix=inputs={n_aud}:normalize=0:duration=longest,apad=whole_dur={total},"
          f"atrim=0:{total},asetpts=N/SR/TB[aout]")

vf = []
cur = "[0:v]"
for si in range(n_sub):
    idx = 1 + n_aud + si
    en = f"between(t,{subs[si]['a']:.3f},{subs[si]['b']:.3f})"
    out = f"[v{si+1}]" if si < n_sub - 1 else "[vfinal]"
    vf.append(f"{cur}[{idx}:v]overlay=x=0:y={SUB_Y}:enable='{en}':eof_action=repeat{out}")
    cur = out

final = os.path.join(OUT, "animatic-30s.mp4")
cmd += ["-filter_complex", ";".join(af + vf),
        "-map", "[vfinal]", "-map", "[aout]",
        "-t", f"{total}", "-r", str(FPS),
        "-c:v", "libx264", "-crf", "18", "-preset", "slow", "-pix_fmt", "yuv420p",
        "-c:a", "aac", "-b:a", "192k", "-ar", "48000",
        "-movflags", "+faststart", final]
run(cmd, tag="final")
print("出片:", final)

# ---------- 8. 验收 ----------
probe = run([FP, "-v", "error", "-show_entries",
             "format=duration,size:stream=codec_name,width,height,r_frame_rate,pix_fmt",
             "-of", "json", final], tag="probe")
info = json.loads(probe.stdout)
print(json.dumps(info, ensure_ascii=False, indent=2))
