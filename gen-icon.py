"""Draws the Miaohua anime catgirl mark.

Everything here is generated from primitives, so the artwork carries no third-party
licence. Drawn at 4x and downsampled so the small sizes stay smooth.

If you would rather use your own picture instead: drop a PNG next to the exe named
`logo.png` (any size, square works best) and both the window header and the tray
icon will use it. See ui_logo() in Ui.cs.
"""
import os
from PIL import Image, ImageDraw, ImageFilter

ROOT = r'D:\harness gzq\009\miaohua'
DENSITIES = {
    'mipmap-mdpi': 48,
    'mipmap-hdpi': 72,
    'mipmap-xhdpi': 96,
    'mipmap-xxhdpi': 144,
    'mipmap-xxxhdpi': 192,
}

# ---- palette: pastel pink hair, cream skin, violet-pink eyes
HAIR      = (255, 178, 208, 255)
HAIR_DARK = (244, 146, 186, 255)
HAIR_LITE = (255, 214, 231, 255)
SKIN      = (255, 244, 236, 255)
SKIN_SH   = (255, 226, 219, 255)
DARK      = (74, 52, 66, 255)
IRIS      = (214, 92, 150, 255)
BLUSH     = (255, 160, 186, 170)
MOUTH     = (222, 104, 132, 255)
BELL      = (255, 214, 102, 255)
BELL_DARK = (222, 168, 40, 255)
WHITE     = (255, 255, 255, 250)


def gradient_bg(W, top, bot, radius_frac=0.235):
    grad = Image.new('RGBA', (W, W))
    gd = ImageDraw.Draw(grad)
    for y in range(W):
        t = y / float(W - 1)
        gd.line([(0, y), (W, y)],
                fill=(int(top[0] + (bot[0] - top[0]) * t),
                      int(top[1] + (bot[1] - top[1]) * t),
                      int(top[2] + (bot[2] - top[2]) * t), 255))
    mask = Image.new('L', (W, W), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, W - 1, W - 1],
                                           radius=int(W * radius_frac), fill=255)
    out = Image.new('RGBA', (W, W), (0, 0, 0, 0))
    out.paste(grad, (0, 0), mask)
    return out


def eye(d, W, cx, cy, w, h):
    """Big anime eye, kept deliberately simple: solid dark shape plus two highlights."""
    d.ellipse([cx - w / 2, cy - h / 2, cx + w / 2, cy + h / 2], fill=DARK)
    # iris glow low in the eye
    d.ellipse([cx - w * 0.30, cy + h * 0.06, cx + w * 0.30, cy + h * 0.46],
              fill=(226, 116, 168, 235))

    hr = w * 0.30
    d.ellipse([cx - w * 0.16 - hr, cy - h * 0.22 - hr,
               cx - w * 0.16 + hr, cy - h * 0.22 + hr], fill=WHITE)
    sr = w * 0.13
    d.ellipse([cx + w * 0.20 - sr, cy + h * 0.24 - sr,
               cx + w * 0.20 + sr, cy + h * 0.24 + sr], fill=(255, 255, 255, 210))

    # lash line along the top
    d.arc([cx - w * 0.58, cy - h * 0.60, cx + w * 0.58, cy + h * 0.58],
          190, 350, fill=DARK, width=max(2, int(W * 0.014)))


def draw_mark(size):
    K = 4
    W = size * K
    # lavender background so the pink character separates from it
    img = gradient_bg(W, (233, 228, 255), (255, 227, 243))
    d = ImageDraw.Draw(img)

    # ---------------- hair silhouette
    d.ellipse([W * 0.075, W * 0.150, W * 0.925, W * 1.030], fill=HAIR)

    # ---------------- cat ears
    d.polygon([(W * 0.190, W * 0.360), (W * 0.262, W * 0.062), (W * 0.452, W * 0.288)], fill=HAIR)
    d.polygon([(W * 0.810, W * 0.360), (W * 0.738, W * 0.062), (W * 0.548, W * 0.288)], fill=HAIR)
    d.polygon([(W * 0.246, W * 0.338), (W * 0.280, W * 0.152), (W * 0.398, W * 0.296)],
              fill=(255, 176, 206, 255))
    d.polygon([(W * 0.754, W * 0.338), (W * 0.720, W * 0.152), (W * 0.602, W * 0.296)],
              fill=(255, 176, 206, 255))

    # ---------------- face: deliberately large, it has to survive 16px
    d.ellipse([W * 0.150, W * 0.310, W * 0.850, W * 0.908], fill=SKIN)

    # ---------------- fringe: a dome (chord), because an ellipse bulges down in
    # the middle and swallows the forehead
    d.chord([W * 0.132, W * 0.236, W * 0.868, W * 0.664], 180, 360, fill=HAIR)

    # rounded strand tips so the fringe is not one hard horizontal line
    for fx in (0.300, 0.500, 0.700):
        d.ellipse([W * (fx - 0.062), W * 0.352, W * (fx + 0.062), W * 0.522], fill=HAIR)

    # ---------------- eyes
    eye(d, W, W * 0.355, W * 0.660, W * 0.165, W * 0.185)
    eye(d, W, W * 0.645, W * 0.660, W * 0.165, W * 0.185)

    # ---------------- blush
    d.ellipse([W * 0.196, W * 0.780, W * 0.318, W * 0.844], fill=BLUSH)
    d.ellipse([W * 0.682, W * 0.780, W * 0.804, W * 0.844], fill=BLUSH)

    # ---------------- cat mouth
    mw = max(2, int(W * 0.012))
    ny = W * 0.826
    ns = W * 0.024
    d.arc([W * 0.5 - ns * 3.2, ny - ns * 1.2, W * 0.5, ny + ns * 2.2], 0, 180,
          fill=MOUTH, width=mw)
    d.arc([W * 0.5, ny - ns * 1.2, W * 0.5 + ns * 3.2, ny + ns * 2.2], 0, 180,
          fill=MOUTH, width=mw)

    return img.resize((size, size), Image.LANCZOS)


for folder, size in DENSITIES.items():
    path = os.path.join(ROOT, 'android', 'res', folder)
    os.makedirs(path, exist_ok=True)
    draw_mark(size).save(os.path.join(path, 'ic_launcher.png'), 'PNG')
    print('android', folder, size)

ico_path = os.path.join(ROOT, 'pc', 'Miaohua.ico')
draw_mark(256).save(ico_path, format='ICO',
                    sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
print('windows ico ->', ico_path, os.path.getsize(ico_path), 'bytes')

draw_mark(256).save(os.path.join(ROOT, 'preview-icon.png'), 'PNG')

# a size strip: the icon mostly gets seen small, so check legibility at real sizes
strip_sizes = [256, 48, 32, 16]
cell = 160
strip = Image.new('RGBA', (cell * len(strip_sizes), cell), (250, 250, 252, 255))
for i, s in enumerate(strip_sizes):
    im = draw_mark(s)
    if s != cell:
        im = im.resize((cell - 16, cell - 16), Image.NEAREST)
    strip.paste(im, (i * cell + 8, 8), im)
strip.save(os.path.join(ROOT, 'preview-sizes.png'), 'PNG')
print('preview written (icon + size strip)')
