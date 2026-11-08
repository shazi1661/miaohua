# -*- coding: utf-8 -*-
"""
<c=YYXXXXXX> 逐字渐变生成器。

用法：
    python gradient.py                    输出预设渐变表
    python gradient.py "喵化助手" 主粉 藕紫   给指定文字生成渐变串
"""
import io, sys

# 软件自己的配色
PALETTE = {
    "主粉":   (0xF0, 0x62, 0x92),
    "粉柔":   (0xDE, 0x7C, 0x9E),
    "粉亮":   (0xF7, 0x7E, 0xA8),
    "粉深":   (0xD4, 0x44, 0x76),
    "浅粉":   (0xFC, 0xE4, 0xEE),
    "奶油黄": (0xF7, 0xC8, 0x73),
    "薄荷绿": (0x6F, 0xCF, 0x97),
    "天蓝":   (0x6B, 0xB8, 0xE8),
    "藕紫":   (0xB3, 0x9D, 0xDB),
    "珊瑚橙": (0xFF, 0x8A, 0x65),
    "正文":   (0x3A, 0x2A, 0x34),
}

# 预设渐变：(名字, 起点, 中点(可 None), 终点)
RAMPS = [
    ("粉 → 藕紫",   "主粉",   None,     "藕紫"),
    ("粉 → 天蓝",   "主粉",   None,     "天蓝"),
    ("粉 → 珊瑚橙", "主粉",   None,     "珊瑚橙"),
    ("粉 → 奶油黄", "主粉",   None,     "奶油黄"),
    ("粉 → 薄荷绿", "主粉",   None,     "薄荷绿"),
    ("粉亮 → 粉深", "粉亮",   None,     "粉深"),
    ("奶油黄 → 珊瑚橙 → 粉", "奶油黄", "珊瑚橙", "主粉"),
    ("薄荷绿 → 天蓝 → 藕紫", "薄荷绿", "天蓝",   "藕紫"),
]


def lerp(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


def ramp_colors(stops, n):
    """把若干停靠点插值成 n 个颜色。"""
    if n <= 1:
        return [stops[0]]
    segs = len(stops) - 1
    out = []
    for i in range(n):
        t = i / float(n - 1) * segs
        k = min(int(t), segs - 1)
        out.append(lerp(stops[k], stops[k + 1], t - k))
    return out


def tag(c):
    return "<c=00%02X%02X%02X>" % c


def apply(text, stops):
    cols = ramp_colors(stops, len(text))
    return "".join(tag(cols[i]) + ch for i, ch in enumerate(text))


def stops_of(name_a, name_b=None, name_c=None):
    s = [PALETTE[name_a]]
    if name_b:
        s.append(PALETTE[name_b])
    s.append(PALETTE[name_c] if name_c else PALETTE[name_a])
    return s


# ==================== 命令行模式 ====================
if len(sys.argv) >= 4:
    text = sys.argv[1]
    a, b = sys.argv[2], sys.argv[3]
    c = sys.argv[4] if len(sys.argv) >= 5 else None
    for k in (a, b) + ((c,) if c else ()):
        if k not in PALETTE:
            print("unknown color:", k)
            print("available:", " ".join(PALETTE.keys()))
            sys.exit(1)
    print(apply(text, stops_of(a, c, b) if c else stops_of(a, None, b)))
    sys.exit(0)

# ==================== 预设表 ====================
L = []
def w(x=""):
    L.append(x)

w("=" * 68)
w("        喵化助手 · 逐字渐变配色　<c=YYXXXXXX>")
w("=" * 68)
w()
w("【怎么用】")
w()
w("  把下面整串复制走 ✗ 每个字前面都带着自己的颜色 ✓")
w("  <c=00F06292>喵<c=00E87A9E>化<c=00E092AA>助<c=00D8AAB6>手")
w()
w("  ★ 字越多越顺滑 ✗ 4 个字会比较跳 ✗ 6~8 个字最好看 ✓")
w("  ★ YY 那一段固定写 00 ✗ 00 = 不透明 ✓")
w()

SAMPLE4 = "喵化助手"
SAMPLE6 = "喵化助手更新"
SAMPLE8 = "喵化助手更新啦喵"

for name, a, b, c in RAMPS:
    stops = stops_of(a, b, c)
    w("-" * 68)
    w("  %s" % name)
    w("-" * 68)
    w()
    w("  【4 字】%s" % SAMPLE4)
    w("    " + apply(SAMPLE4, stops))
    w()
    w("  【6 字】%s" % SAMPLE6)
    w("    " + apply(SAMPLE6, stops))
    w()
    w("  【8 字】%s" % SAMPLE8)
    w("    " + apply(SAMPLE8, stops))
    w()
    w("  【分段颜色表】（想自己拼长句子时用）")
    cols = ramp_colors(stops, 12)
    w("    " + "  ".join("#%02X%02X%02X" % c2 for c2 in cols))
    w("    " + "  ".join(tag(c2) for c2 in cols))
    w()

w("=" * 68)
w("【自己生成】")
w("=" * 68)
w()
w("  在这个脚本旁边开命令行 ✗ 跑：")
w()
w('    python gradient.py "要上色的文字" 主粉 藕紫')
w()
w("  可用的颜色名：")
for k in PALETTE:
    w("    %-8s #%02X%02X%02X" % (k, PALETTE[k][0], PALETTE[k][1], PALETTE[k][2]))
w()
w("  三种颜色也行（中间那个是中间色）：")
w()
w('    python gradient.py "文字" 奶油黄 珊瑚橙 主粉')
w()
w("=" * 68)

io.open(r"D:\喵化助手\视频字幕渐变色.txt", "w", encoding="utf-8").write("\n".join(L))
print("written: D:\\\u55b5\u5316\u52a9\u624b\\\u89c6\u9891\u5b57\u5e55\u6e10\u53d8\u8272.txt")
print("lines:", len(L))
