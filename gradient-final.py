# -*- coding: utf-8 -*-
"""
重排渐变色卡 ✗
  ① 全部渐变放一起（常规 → 蓝系 → 彩虹）✗ 不再分散到文末 ✓
  ② 每条都有 emoji 色块 + 十六进制条 ✗ 两种色卡都在 ✓
  ③ 附录（剪辑软件用）留在最后 ✓
"""
import io, os, colorsys

def hsl(h, s, l):
    r, g, b = colorsys.hls_to_rgb((h % 360) / 360.0, l / 100.0, s / 100.0)
    return (int(round(r * 255)), int(round(g * 255)), int(round(b * 255)))

def tag(c): return "<c=FF%02X%02X%02X>" % c
def hx(c):  return "#%02X%02X%02X" % c

def mark(c):
    r, g, b = [v / 255.0 for v in c]
    mx, mn = max(r, g, b), min(r, g, b)
    l = (mx + mn) / 2.0
    s = 0.0 if mx == mn else (mx - mn) / (1 - abs(2 * l - 1))
    if l > 0.93: return "\u2B1C"
    if l < 0.12: return "\u2B1B"
    if s < 0.20:
        if l > 0.72: return "\u2B1C"
        if l > 0.35: return "\U0001F7EB"
        return "\u2B1B"
    h = 0.0
    if mx == r:   h = (60 * ((g - b) / (mx - mn))) % 360
    elif mx == g: h = 60 * ((b - r) / (mx - mn)) + 120
    else:         h = 60 * ((r - g) / (mx - mn)) + 240
    if h < 8 or h >= 325: return "\U0001F7E5"
    if h < 45:            return "\U0001F7E7"
    if h < 70:            return "\U0001F7E8"
    if h < 160:           return "\U0001F7E9"
    if h < 255:           return "\U0001F7E6"
    return "\U0001F7EA"

def lerp(a, b, t): return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))
def ramp(st, n):
    if n <= 1: return [st[0]]
    segs = len(st) - 1; out = []
    for i in range(n):
        t = i / float(n - 1) * segs; k = min(int(t), segs - 1)
        out.append(lerp(st[k], st[k + 1], t - k))
    return out

C = {
 "\u4e3b\u7c89":(0xF0,0x62,0x92), "\u7c89\u4eae":(0xF7,0x7E,0xA8), "\u7c89\u6df1":(0xD4,0x44,0x76),
 "\u5976\u6cb9\u9ec4":(0xF7,0xC8,0x73), "\u8584\u8377\u7eff":(0x6F,0xCF,0x97), "\u5929\u84dd":(0x6B,0xB8,0xE8),
 "\u85d5\u7d2b":(0xB3,0x9D,0xDB), "\u73ca\u745a\u6a59":(0xFF,0x8A,0x65), "\u80ed\u8102":(0x9D,0x29,0x33),
 "\u4e01\u9999\u8272":(0xCC,0xA4,0xE3), "\u7af9\u9752":(0x78,0x92,0x62), "\u975b\u9752":(0x17,0x7C,0xB0),
 "\u674f\u9ec4":(0xFF,0xA6,0x31), "\u6731\u7802":(0xFF,0x46,0x1F), "\u6708\u767d":(0xD6,0xEC,0xF0),
 "\u9edb\u84dd":(0x42,0x50,0x66), "\u8367\u5149\u9752":hsl(180,100,55), "\u7535\u5149\u84dd":hsl(218,100,56),
 "\u4eae\u7d2b":hsl(288,100,50), "\u8367\u5149\u9ec4":hsl(60,100,55), "\u8367\u5149\u7c89":hsl(330,100,58),
 "\u7070\u7c89":hsl(345,22,72), "\u7070\u84dd":hsl(205,20,68), "\u7070\u7eff":hsl(120,16,64),
 "\u6a31\u82b1":hsl(348,90,88), "\u82cf\u6253\u84dd":hsl(200,80,84), "\u9999\u828b":hsl(265,70,84),
 "\u5976\u6cb9":hsl(40,80,88), "\u6d77\u76d0":hsl(185,60,84),
 "\u51b0\u84dd":hsl(198,85,88), "\u5929\u7a7a":hsl(210,85,86), "\u6e56\u84dd":hsl(190,80,55),
 "\u9752\u78a7":hsl(175,58,55), "\u5b54\u96c0\u7eff":hsl(174,100,39), "\u84dd\u8393\u5976":hsl(250,60,82),
 "\u7fa4\u9752":hsl(202,40,50), "\u5b9d\u84dd":hsl(230,62,53), "\u975b":hsl(231,48,48),
 "\u85cf\u9752":hsl(232,62,22), "\u591c\u84dd":hsl(225,68,12), "\u96fe\u973e\u84dd":hsl(205,16,62),
}

def r2(a, b, note=""):   return (a, b, note)
def r3(a, b, c, note=""): return (a, b, c, note)

SECTION_A = [
 ("\u8f6f\u4ef6\u4e3b\u7c89 \u2192 \u85d5\u7d2b", ["\u4e3b\u7c89","\u85d5\u7d2b"], "\u548c\u8f6f\u4ef6\u540c\u8272 \u2727 \u6700\u7a33"),
 ("\u8f6f\u4ef6\u4e3b\u7c89 \u2192 \u5929\u84dd", ["\u4e3b\u7c89","\u5929\u84dd"], "\u7c89\u84dd \u2727 \u5bf9\u6bd4\u8272"),
 ("\u8f6f\u4ef6\u4e3b\u7c89 \u2192 \u73ca\u745a\u6a59", ["\u4e3b\u7c89","\u73ca\u745a\u6a59"], "\u6e29\u6696"),
 ("\u8f6f\u4ef6\u4e3b\u7c89 \u2192 \u5976\u6cb9\u9ec4", ["\u4e3b\u7c89","\u5976\u6cb9\u9ec4"], "\u6d3b\u6cfc"),
 ("\u8f6f\u4ef6\u4e3b\u7c89 \u2192 \u8584\u8377\u7eff", ["\u4e3b\u7c89","\u8584\u8377\u7eff"], "\u6e05\u65b0"),
 ("\u7c89\u4eae \u2192 \u7c89\u6df1", ["\u7c89\u4eae","\u7c89\u6df1"], "\u540c\u8272\u7cfb \u2727 \u6700\u4e0d\u4f1a\u51fa\u9519"),
 ("\u80ed\u8102 \u2192 \u4e01\u9999\u8272", ["\u80ed\u8102","\u4e01\u9999\u8272"], "\u4e2d\u56fd\u98ce"),
 ("\u7af9\u9752 \u2192 \u975b\u9752", ["\u7af9\u9752","\u975b\u9752"], "\u4e2d\u56fd\u98ce \u2727 \u6e05\u51b7"),
 ("\u674f\u9ec4 \u2192 \u6731\u7802", ["\u674f\u9ec4","\u6731\u7802"], "\u4e2d\u56fd\u98ce \u2727 \u70ed\u70c8"),
 ("\u6708\u767d \u2192 \u9edb\u84dd", ["\u6708\u767d","\u9edb\u84dd"], "\u4e2d\u56fd\u98ce \u2727 \u6de1\u96c5"),
 ("\u8367\u5149\u9752 \u2192 \u7535\u5149\u84dd \u2192 \u4eae\u7d2b", ["\u8367\u5149\u9752","\u7535\u5149\u84dd","\u4eae\u7d2b"], "\u8d5b\u535a\u611f"),
 ("\u8367\u5149\u9ec4 \u2192 \u8367\u5149\u7c89", ["\u8367\u5149\u9ec4","\u8367\u5149\u7c89"], "\u8d85\u62a2\u773c"),
 ("\u5976\u6cb9\u9ec4 \u2192 \u73ca\u745a\u6a59 \u2192 \u4e3b\u7c89", ["\u5976\u6cb9\u9ec4","\u73ca\u745a\u6a59","\u4e3b\u7c89"], "\u4e09\u8272"),
 ("\u8584\u8377\u7eff \u2192 \u5929\u84dd \u2192 \u85d5\u7d2b", ["\u8584\u8377\u7eff","\u5929\u84dd","\u85d5\u7d2b"], "\u4e09\u8272"),
 ("\u7070\u7c89 \u2192 \u7070\u84dd \u2192 \u7070\u7eff", ["\u7070\u7c89","\u7070\u84dd","\u7070\u7eff"], "\u83ab\u5170\u8fea \u2727 \u9ad8\u7ea7\u611f"),
 ("\u6a31\u82b1 \u2192 \u82cf\u6253\u84dd \u2192 \u9999\u828b", ["\u6a31\u82b1","\u82cf\u6253\u84dd","\u9999\u828b"], "\u9a6c\u5361\u9f99 \u2727 \u5c11\u5973\u611f"),
 ("\u5976\u6cb9 \u2192 \u6d77\u76d0", ["\u5976\u6cb9","\u6d77\u76d0"], "\u5976\u8336\u8272"),
]
SECTION_B = [
 ("\u6708\u767d \u2192 \u5929\u84dd \u2192 \u85cf\u9752", ["\u6708\u767d","\u5929\u84dd","\u85cf\u9752"], "\u4ece\u767d\u5230\u6df1\u84dd \u2727 \u6700\u5b9e\u7528"),
 ("\u51b0\u84dd \u2192 \u6e56\u84dd \u2192 \u975b", ["\u51b0\u84dd","\u6e56\u84dd","\u975b"], "\u6e05\u51c9"),
 ("\u82cf\u6253\u84dd \u2192 \u5929\u84dd \u2192 \u5b9d\u84dd", ["\u82cf\u6253\u84dd","\u5929\u84dd","\u5b9d\u84dd"], "\u871c\u6c34\u611f"),
 ("\u5929\u84dd \u2192 \u975b\u9752 \u2192 \u9edb\u84dd", ["\u5929\u84dd","\u975b\u9752","\u9edb\u84dd"], "\u5b89\u9759"),
 ("\u9752\u78a7 \u2192 \u5b54\u96c0\u7eff \u2192 \u975b\u9752", ["\u9752\u78a7","\u5b54\u96c0\u7eff","\u975b\u9752"], "\u9752\u7eff\u5230\u84dd \u2727 \u6e05\u65b0"),
 ("\u7535\u5149\u84dd \u2192 \u8367\u5149\u9752", ["\u7535\u5149\u84dd","\u8367\u5149\u9752"], "\u8d5b\u535a\u611f"),
 ("\u84dd\u8393\u5976 \u2192 \u5929\u7a7a \u2192 \u82cf\u6253\u84dd", ["\u84dd\u8393\u5976","\u5929\u7a7a","\u82cf\u6253\u84dd"], "\u9a6c\u5361\u9f99 \u2727 \u5976\u84dd\u8272"),
 ("\u96fe\u973e\u84dd \u2192 \u975b\u9752 \u2192 \u9edb\u84dd", ["\u96fe\u973e\u84dd","\u975b\u9752","\u9edb\u84dd"], "\u83ab\u5170\u8fea \u2727 \u4f4e\u9971\u548c"),
 ("\u8584\u8377\u7eff \u2192 \u9752\u78a7 \u2192 \u5929\u84dd", ["\u8584\u8377\u7eff","\u9752\u78a7","\u5929\u84dd"], "\u51b7\u8c03 \u2727 \u6e05\u723d"),
 ("\u5929\u84dd \u2192 \u591c\u84dd", ["\u5929\u84dd","\u591c\u84dd"], "\u4ece\u767d\u5929\u5230\u591c\u91cc"),
]
def std(h):   return hsl(h, 85, 60)
def soft(h):  return hsl(h, 65, 76)
def neon(h):  return hsl(h, 100, 55)
def deep(h):  return hsl(h, 80, 42)
def pastel(h):return hsl(h, 55, 84)
def candy(h): return hsl(h, 88, 68)
def pinkish(h): return hsl(330 + (h - 330) * 0.75, 75, 62)
def bi(h):    return hsl(360 - (h if h <= 180 else 360 - h) * 1.6, 85, 58)
RAINBOWS = [
 ("\u6807\u51c6\u5f69\u8679", "\u7ea2\u6a59\u9ec4\u7eff\u9752\u84dd\u7d2b \u2727 \u6700\u5e38\u89c1", std),
 ("\u67d4\u548c\u5f69\u8679", "\u4f4e\u9971\u548c \u2727 \u4e0d\u523a\u773c", soft),
 ("\u8367\u5149\u5f69\u8679", "\u9ad8\u9971\u548c \u2727 \u6700\u4eae\u773c", neon),
 ("\u6df1\u8272\u5f69\u8679", "\u6697\u4e00\u70b9 \u2727 \u9002\u5408\u6d45\u8272\u80cc\u666f", deep),
 ("\u5976\u6cb9\u5f69\u8679", "\u975e\u5e38\u6de1 \u2727 \u9002\u5408\u5c0f\u5b57", pastel),
 ("\u7cd6\u679c\u5f69\u8679", "\u9c9c\u4eae\u4e0d\u523a\u773c \u2727 \u53ef\u7231\u98ce", candy),
 ("\u7c89\u8c03\u5f69\u8679", "\u4ece\u7c89\u8272\u51fa\u53d1 \u2727 \u548c\u8f6f\u4ef6\u66f4\u642d", pinkish),
 ("\u53cc\u5411\u5f69\u8679", "\u53bb\u4e00\u8d9f\u518d\u56de\u6765 \u2727 \u4e24\u5934\u90fd\u662f\u7ea2", bi),
]

N = 16
L = []
def w(x=""): L.append(x)

w("=" * 86)
w("        喵化助手 \u00b7 \u6e10\u53d8\u8272\u5361")
w("        \u683c\u5f0f\uff1a<c=FFRRGGBB>\uff08FF = \u4e0d\u900f\u660e\uff09")
w("=" * 86)
w()
w("  \u2605 \u6bcf\u6761\u6709\u4e09\u79cd\u770b\u6cd5\uff1a")
w("      \u2460 \u3010\u8272\u5757\u3011\u4e00\u6392 emoji \u2727 \u8bb0\u4e8b\u672c\u91cc\u4e5f\u80fd\u770b\u5230 \u2713")
w("      \u2461 \u3010\u6e10\u53d8\u6761\u3011\u4e00\u6392 #RRGGBB \u2727 \u7f16\u8f91\u5668\u4f1a\u753b\u6210\u4e00\u6761\u5f69\u5e26 \u2713")
w("      \u2462 \u3010\u73b0\u6210\u4e32\u30114/6/8 \u5b57 \u2727 \u6574\u884c\u590d\u5236\u5c31\u80fd\u7528 \u2713")
w()
w("  \u2605 \u2605 \u2605 \u53ea\u590d\u5236 <c=...> \u5f00\u5934\u7684\u90a3\u4e00\u4e32 \u2727 \u65b9\u5757\u548c # \u90fd\u4e0d\u8981 \u2605 \u2605 \u2605")
w()

def emit_group(title, ramps, is_rainbow=False):
    w("\u2500" * 86)
    w("  %s" % title)
    w("\u2500" * 86)
    w()
    for item in ramps:
        if is_rainbow:
            name, note, fn = item
            stops = None
            cols = [fn(i / float(N - 1) * 360.0) for i in range(N)]
            chars = None
        else:
            name, keys, note = item
            stops = [C[k] for k in keys]
            cols = ramp(stops, N)
        w("  \u25a0 %s%s" % (name, ("\u3000\u3000" + note) if note else ""))
        w()
        w("    \u8272\u5757    " + "".join(mark(c) for c in cols))
        w("    \u6e10\u53d8\u6761  " + " ".join(hx(c) for c in cols))
        if stops:
            w("    \u8d77\u6b62    " + "   ".join(
                ("\u8d77\u59cb " if i == 0 else ("\u7ed3\u675f " if i == len(stops)-1 else "\u4e2d\u95f4 ")) + hx(c)
                for i, c in enumerate(stops)))
        w()
        for n, sample in ((4, "\u55b5\u5316\u52a9\u624b"), (6, "\u55b5\u5316\u52a9\u624b\u66f4\u65b0"),
                          (8, "\u55b5\u5316\u52a9\u624b\u66f4\u65b0\u5566\u55b5")):
            if is_rainbow:
                cs = [fn(i / float(n - 1) * 360.0) for i in range(n)]
            else:
                cs = ramp(stops, n)
            w("    %d\u5b57     %s" % (n, "".join(tag(cs[i]) + sample[i] for i in range(n))))
        w()
        if is_rainbow:
            cs12 = [fn(i / 11.0 * 360.0) for i in range(12)]
        else:
            cs12 = ramp(stops, 12)
        w("    12\u6bb5    " + " ".join(tag(c) for c in cs12))
        w("    \u5bf9\u7167    " + "".join(mark(c) for c in cs12))
        w()

emit_group("\u4e00\u3001\u5e38\u89c1\u6e10\u53d8\uff08%d \u6761\uff09" % len(SECTION_A), SECTION_A)
emit_group("\u4e8c\u3001\u84dd\u8272\u7cfb\uff08%d \u6761\uff09" % len(SECTION_B), SECTION_B)
emit_group("\u4e09\u3001\u5f69\u8679\u8272\uff08%d \u79cd\uff09" % len(RAINBOWS), RAINBOWS, True)

w("=" * 86)
w("  \u9644\u3001\u526a\u8f91\u8f6f\u4ef6\u7528 \u00b7 \u6e10\u53d8\u7684\u8d77\u6b62\u8272\u53f7")
w("=" * 86)
w()
w("  \u2605 <c=\u2026> \u6807\u7b7e \u2727 \u4e00\u4e2a\u5b57\u53ea\u80fd\u6709\u4e00\u79cd\u989c\u8272 \u2713")
w("  \u2605 \u3010\u4e00\u4e2a\u5b57\u91cc\u9762\u5728\u53d8\u8272\u3011\u2727 \u662f\u526a\u8f91\u8f6f\u4ef6\u7684\u6587\u5b57\u6e10\u53d8\u505a\u7684 \u2713")
w("    \u6e10\u53d8\u94fa\u5728\u6574\u4e2a\u6587\u5b57\u56fe\u5c42\u4e0a \u2727 \u6bcf\u4e2a\u5b57\u53ea\u622a\u53d6\u5b83\u6240\u5728\u4f4d\u7f6e\u7684\u90a3\u4e00\u6bb5 \u2713")
w("  \u2605 \u5728\u8f6f\u4ef6\u91cc\u627e\u3010\u989c\u8272 \u2192 \u6e10\u53d8\u3011\u2727 \u586b\u8d77\u59cb\u8272\u548c\u7ed3\u675f\u8272 \u2713")
w()
for name, keys, note in SECTION_A + SECTION_B:
    stops = [C[k] for k in keys]
    parts = "   ".join(
        ("\u8d77\u59cb " if i == 0 else ("\u7ed3\u675f " if i == len(stops)-1 else "\u4e2d\u95f4 ")) + hx(c)
        for i, c in enumerate(stops))
    w("  %-30s %s" % (name, parts))
for name, note, fn in RAINBOWS:
    w("  %-30s \u8d77\u59cb %s   \u7ed3\u675f %s\uff08\u5f69\u8679\u7ed5\u4e00\u5708 \u2727 \u4e24\u5934\u4e00\u6837\u2713\uff09"
      % (name, hx(fn(0)), hx(fn(360))))
w()
w("=" * 86)

F = os.path.join("D:\\", "\u55b5\u5316\u52a9\u624b", "\u89c6\u9891\u5b57\u5e55\u6e10\u53d8\u8272.txt")
io.open(F, "w", encoding="utf-8").write("\n".join(L))
content = "\n".join(L)
P = r"D:\harness gzq\009\miaohua"
Bk = r"D:\喵化助手_备份_20261007_PC2.15.1_安卓3.4.4"
io.open(os.path.join(P, "\u89c6\u9891\u5b57\u5e55\u6e10\u53d8\u8272.txt"), "w", encoding="utf-8").write(content)
io.open(os.path.join(Bk, "\u89c6\u9891\u5b57\u5e55\u6e10\u53d8\u8272.txt"), "w", encoding="utf-8").write(content)
share = content.replace("\u55b5\u5316\u52a9\u624b \u00b7 \u6e10\u53d8\u8272\u5361", "\u6e10\u53d8\u8272\u5361")
io.open(os.path.join("D:\\", "\u55b5\u5316\u52a9\u624b", "\u6e10\u53d8\u8272\u5361_\u5206\u4eab\u7248.txt"), "w", encoding="utf-8").write(share)
io.open(os.path.join(P, "\u6e10\u53d8\u8272\u5361_\u5206\u4eab\u7248.txt"), "w", encoding="utf-8").write(share)
io.open(os.path.join(Bk, "\u6e10\u53d8\u8272\u5361_\u5206\u4eab\u7248.txt"), "w", encoding="utf-8").write(share)
print("sections: %d + %d + %d = %d ramps" % (len(SECTION_A), len(SECTION_B), len(RAINBOWS),
      len(SECTION_A)+len(SECTION_B)+len(RAINBOWS)))
