# -*- coding: utf-8 -*-
"""渐变色卡 ✗ 带 emoji 色块 ✗ 写到 视频字幕渐变色.txt。"""
import io, os, colorsys

def hsl(h, s, l):
    r, g, b = colorsys.hls_to_rgb(h / 360.0, l / 100.0, s / 100.0)
    return (int(round(r * 255)), int(round(g * 255)), int(round(b * 255)))

def tag(c):
    return "<c=FF%02X%02X%02X>" % c

def mark(c):
    """按色相 + 明度选方块。"""
    r, g, b = [v / 255.0 for v in c]
    mx, mn = max(r, g, b), min(r, g, b)
    l = (mx + mn) / 2.0
    s = 0.0 if mx == mn else (mx - mn) / (1 - abs(2 * l - 1))
    if l > 0.93: return "\u2B1C"
    if l < 0.12: return "\u2B1B"
    if s < 0.20:
        if l > 0.90: return "\u2B1C"
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
    if h < 325:           return "\U0001F7EA"
    return "\U0001F7E5"

def bar(c):
    lum = (0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2]) / 255.0
    n = int(round(lum * 9)) + 1
    return "\u2588" * n + "\u2591" * (10 - n)

def lerp(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))

def ramp(stops, n):
    if n <= 1: return [stops[0]]
    segs = len(stops) - 1
    out = []
    for i in range(n):
        t = i / float(n - 1) * segs
        k = min(int(t), segs - 1)
        out.append(lerp(stops[k], stops[k + 1], t - k))
    return out

C = {
    "\u4e3b\u7c89": (0xF0, 0x62, 0x92), "\u7c89\u4eae": (0xF7, 0x7E, 0xA8),
    "\u7c89\u6df1": (0xD4, 0x44, 0x76), "\u6d45\u7c89": (0xFC, 0xE4, 0xEE),
    "\u5976\u6cb9\u9ec4": (0xF7, 0xC8, 0x73), "\u8584\u8377\u7eff": (0x6F, 0xCF, 0x97),
    "\u5929\u84dd": (0x6B, 0xB8, 0xE8), "\u85d5\u7d2b": (0xB3, 0x9D, 0xDB),
    "\u73ca\u745a\u6a59": (0xFF, 0x8A, 0x65), "\u6b63\u6587": (0x3A, 0x2A, 0x34),
    "\u80ed\u8102": (0x9D, 0x29, 0x33), "\u4e01\u9999\u8272": (0xCC, 0xA4, 0xE3),
    "\u7af9\u9752": (0x78, 0x92, 0x62), "\u975b\u9752": (0x17, 0x7C, 0xB0),
    "\u674f\u9ec4": (0xFF, 0xA6, 0x31), "\u6731\u7802": (0xFF, 0x46, 0x1F),
    "\u6708\u767d": (0xD6, 0xEC, 0xF0), "\u9edb\u84dd": (0x42, 0x50, 0x66),
    "\u8367\u5149\u9752": hsl(180, 100, 55), "\u7535\u5149\u84dd": hsl(218, 100, 56),
    "\u4eae\u7d2b": hsl(288, 100, 50), "\u8367\u5149\u9ec4": hsl(60, 100, 55),
    "\u8367\u5149\u7c89": hsl(330, 100, 58), "\u9713\u8679\u7ea2": hsl(348, 100, 55),
    "\u7070\u7c89": hsl(345, 22, 72), "\u7070\u84dd": hsl(205, 20, 68),
    "\u7070\u7eff": hsl(120, 16, 64), "\u6a31\u82b1": hsl(348, 90, 88),
    "\u82cf\u6253\u84dd": hsl(200, 80, 84), "\u9999\u828b": hsl(265, 70, 84),
    "\u5976\u6cb9": hsl(40, 80, 88), "\u6d77\u76d0": hsl(185, 60, 84),
}

RAMPS = [
    ("\u8f6f\u4ef6\u4e3b\u7c89 \u2192 \u85d5\u7d2b", ["\u4e3b\u7c89", "\u85d5\u7d2b"], "\u548c\u8f6f\u4ef6\u540c\u8272 \u2727 \u6700\u7a33"),
    ("\u8f6f\u4ef6\u4e3b\u7c89 \u2192 \u5929\u84dd", ["\u4e3b\u7c89", "\u5929\u84dd"], ""),
    ("\u8f6f\u4ef6\u4e3b\u7c89 \u2192 \u73ca\u745a\u6a59", ["\u4e3b\u7c89", "\u73ca\u745a\u6a59"], "\u6e29\u6696"),
    ("\u8f6f\u4ef6\u4e3b\u7c89 \u2192 \u5976\u6cb9\u9ec4", ["\u4e3b\u7c89", "\u5976\u6cb9\u9ec4"], "\u6d3b\u6cfc"),
    ("\u8f6f\u4ef6\u4e3b\u7c89 \u2192 \u8584\u8377\u7eff", ["\u4e3b\u7c89", "\u8584\u8377\u7eff"], "\u6e05\u65b0"),
    ("\u7c89\u4eae \u2192 \u7c89\u6df1", ["\u7c89\u4eae", "\u7c89\u6df1"], "\u540c\u8272\u7cfb \u2727 \u6700\u4e0d\u4f1a\u51fa\u9519"),
    ("\u80ed\u8102 \u2192 \u4e01\u9999\u8272", ["\u80ed\u8102", "\u4e01\u9999\u8272"], "\u4e2d\u56fd\u98ce"),
    ("\u7af9\u9752 \u2192 \u975b\u9752", ["\u7af9\u9752", "\u975b\u9752"], "\u4e2d\u56fd\u98ce \u2727 \u6e05\u51b7"),
    ("\u674f\u9ec4 \u2192 \u6731\u7802", ["\u674f\u9ec4", "\u6731\u7802"], "\u4e2d\u56fd\u98ce \u2727 \u70ed\u70c8"),
    ("\u6708\u767d \u2192 \u9edb\u84dd", ["\u6708\u767d", "\u9752", "\u9edb\u84dd"] if "\u9752" in C else ["\u6708\u767d", "\u9edb\u84dd"], "\u4e2d\u56fd\u98ce \u2727 \u6de1\u96c5"),
    ("\u8367\u5149\u9752 \u2192 \u7535\u5149\u84dd \u2192 \u4eae\u7d2b", ["\u8367\u5149\u9752", "\u7535\u5149\u84dd", "\u4eae\u7d2b"], "\u8d5b\u535a\u611f"),
    ("\u8367\u5149\u9ec4 \u2192 \u8367\u5149\u7c89", ["\u8367\u5149\u9ec4", "\u8367\u5149\u7c89"], "\u8d85\u62a2\u773c"),
    ("\u5976\u6cb9\u9ec4 \u2192 \u73ca\u745a\u6a59 \u2192 \u4e3b\u7c89", ["\u5976\u6cb9\u9ec4", "\u73ca\u745a\u6a59", "\u4e3b\u7c89"], "\u4e09\u8272"),
    ("\u8584\u8377\u7eff \u2192 \u5929\u84dd \u2192 \u85d5\u7d2b", ["\u8584\u8377\u7eff", "\u5929\u84dd", "\u85d5\u7d2b"], "\u4e09\u8272"),
    ("\u7070\u7c89 \u2192 \u7070\u84dd \u2192 \u7070\u7eff", ["\u7070\u7c89", "\u7070\u84dd", "\u7070\u7eff"], "\u83ab\u5170\u8fea \u2727 \u9ad8\u7ea7\u611f"),
    ("\u6a31\u82b1 \u2192 \u82cf\u6253\u84dd \u2192 \u9999\u828b", ["\u6a31\u82b1", "\u82cf\u6253\u84dd", "\u9999\u828b"], "\u9a6c\u5361\u9f99 \u2727 \u5c11\u5973\u611f"),
    ("\u5976\u6cb9 \u2192 \u6d77\u76d0", ["\u5976\u6cb9", "\u6d77\u76d0"], "\u5976\u8336\u8272"),
]

L = []
def w(x=""):
    L.append(x)

w("=" * 78)
w("        喵化助手 \u00b7 \u6e10\u53d8\u8272\u5361\uff08%d \u6761\uff09" % len(RAMPS))
w("        \u683c\u5f0f\uff1a<c=FFRRGGBB>\uff08FF = \u4e0d\u900f\u660e\uff09")
w("=" * 78)
w()
w("  \u2605 \u6bcf\u6761\u4e0a\u9762\u90a3\u4e00\u6392\u5f69\u8272\u65b9\u5757\u5c31\u662f\u6e10\u53d8\u7684\u6837\u5b50 \u2713")
w("  \u2605 \u4e0b\u9762\u662f\u30104 / 6 / 8 \u5b57\u3011\u7684\u73b0\u6210\u4e32 \u2727 \u6574\u884c\u590d\u5236\u5c31\u80fd\u7528 \u2713")
w("  \u2605 \u6700\u540e\u4e00\u884c\u662f 12 \u6bb5\u8868 \u2727 \u81ea\u5df1\u62fc\u957f\u53e5\u5b50\u65f6\u7528 \u2713")
w()
w("  \u2605 \u2605 \u2605 \u4e0d\u8981\u628a\u65b9\u5757\u548c\u660e\u6697\u6761\u4e00\u8d77\u590d\u5236\u8fdb\u53bb \u2605 \u2605 \u2605")
w("      \u53ea\u590d\u5236 <c=...> \u5f00\u5934\u7684\u90a3\u4e00\u4e32 \u2713")
w()

for name, keys, note in RAMPS:
    stops = [C[k] for k in keys]
    blocks = ramp(stops, 14)
    w("\u2500" * 78)
    w("  \u25a0 %s%s" % (name, ("\u3000\u3000" + note) if note else ""))
    w("\u2500" * 78)
    w()
    w("  \u8272\u5757  " + "".join(mark(c) for c in blocks))
    w("  \u8d77\u6b62  %s %s  \u2192  %s %s"
      % (mark(stops[0]), bar(stops[0]), mark(stops[-1]), bar(stops[-1])))
    w()
    for n, sample in ((4, "\u55b5\u5316\u52a9\u624b"), (6, "\u55b5\u5316\u52a9\u624b\u66f4\u65b0"),
                      (8, "\u55b5\u5316\u52a9\u624b\u66f4\u65b0\u5566\u55b5")):
        cs = ramp(stops, n)
        w("  %d\u5b57   %s" % (n, "".join(tag(cs[i]) + sample[i] for i in range(n))))
    w()
    w("  12\u6bb5  " + " ".join(tag(c) for c in ramp(stops, 12)))
    w()
    w("  \u5bf9\u7167  " + "".join(mark(c) for c in ramp(stops, 12)))
    w()

w("=" * 78)
w("  \u7528\u8272\u5efa\u8bae")
w("=" * 78)
w()
w("  \u2605 \u5168\u7247\u63a7\u5236\u5728 3\u301c4 \u79cd\u989c\u8272\u4ee5\u5185 \u2727 \u591a\u4e86\u4f1a\u82b1 \u2713")
w("  \u2605 \u6e10\u53d8\u53ea\u7528\u5728\u3010\u7247\u5934\u6807\u9898\u3011\u548c\u3010\u6bcf\u6bb5\u5c0f\u6807\u9898\u3011\u2713")
w("  \u2605 \u6b63\u6587\u4e0d\u8981\u7528\u6e10\u53d8 \u2727 \u770b\u7740\u7d2f \u2713")
w("  \u2605 4 \u5b57\u4ee5\u4e0b\u5efa\u8bae\u7528\u5355\u8272 \u2727 \u592a\u77ed\u7684\u6e10\u53d8\u770b\u4e0d\u51fa\u6765 \u2713")
w()
w("  \u2605 \u5355\u8272\u5728\u53e6\u4e00\u4e2a\u6587\u4ef6\uff1a\u89c6\u9891\u5b57\u5e55\u989c\u8272\u4ee3\u7801.txt\uff08262 \u4e2a\uff09")
w("  \u2605 \u56fe\u7247\u7248\uff1a\u914d\u8272\u88681.png \u301c \u914d\u8272\u88686.png")
w()
w("=" * 78)

out = os.path.join("D:\\", "\u55b5\u5316\u52a9\u624b", "\u89c6\u9891\u5b57\u5e55\u6e10\u53d8\u8272.txt")
io.open(out, "w", encoding="utf-8").write("\n".join(L))
print("ramps:", len(RAMPS), "lines:", len(L))
