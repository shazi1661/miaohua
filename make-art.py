"""Build the app artwork from a supplied illustration.

Reads every PNG/JPG in the reference folder (sorted by name) and can:

  --list                       show the index of each reference image
  --icon  <i> l t r b          crop image i to the fractional box l,t,r,b
                               (0..1 of the source) and write every icon size
  --bg    <i> [blur] [light]   build a soft wallpaper from image i

Outputs land in the miaohua project: pc/Miaohua.ico, pc/logo.png,
android mipmaps, pc/bg.png, and previews so the result can be eyeballed.
"""
import os
import sys
from PIL import Image, ImageDraw, ImageFilter

ROOT = r'D:\harness gzq\009\miaohua'
REF = r'D:\harness gzq\009\图片参考'

DENSITIES = {
    'mipmap-mdpi': 48,
    'mipmap-hdpi': 72,
    'mipmap-xhdpi': 96,
    'mipmap-xxhdpi': 144,
    'mipmap-xxxhdpi': 192,
}
ICO_SIZES = [(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)]


def refs():
    if not os.path.isdir(REF):
        return []
    out = []
    for name in sorted(os.listdir(REF)):
        if name.lower().endswith(('.png', '.jpg', '.jpeg', '.webp', '.bmp')):
            out.append(os.path.join(REF, name))
    return out


def rounded(im, radius_frac=0.22):
    """Round the corners into a transparent square, like the drawn mark."""
    w, h = im.size
    mask = Image.new('L', (w, h), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, w - 1, h - 1],
                                           radius=int(min(w, h) * radius_frac), fill=255)
    out = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    out.paste(im.convert('RGBA'), (0, 0), mask)
    return out


def crop_square(im, box):
    """Cover-crop the fractional box to a square, then resize."""
    W, H = im.size
    l, t, r, b = box
    x0, y0 = int(W * l), int(H * t)
    x1, y1 = int(W * r), int(H * b)
    x0, y0 = max(0, x0), max(0, y0)
    x1, y1 = min(W, x1), min(H, y1)
    sub = im.crop((x0, y0, x1, y1))
    w, h = sub.size
    side = min(w, h)
    sub = sub.crop(((w - side) // 2, (h - side) // 2,
                    (w - side) // 2 + side, (h - side) // 2 + side))
    return sub


def make_icon(src, box, label='', supersample=2):
    big = int(256 * supersample)
    sub = crop_square(src, box)
    hi = sub.resize((big, big), Image.LANCZOS)
    hi = rounded(hi)
    for folder, size in DENSITIES.items():
        path = os.path.join(ROOT, 'android', 'res', folder)
        os.makedirs(path, exist_ok=True)
        hi.resize((size, size), Image.LANCZOS).save(
            os.path.join(path, 'ic_launcher.png'), 'PNG')
    hi.resize((256, 256), Image.LANCZOS).save(
        os.path.join(ROOT, 'pc', 'Miaohua.ico'), format='ICO', sizes=ICO_SIZES)
    hi.resize((128, 128), Image.LANCZOS).save(os.path.join(ROOT, 'pc', 'logo.png'), 'PNG')

    # size strip so legibility at 16/32/48 can be judged
    cell = 160
    strip = Image.new('RGBA', (cell * 4, cell), (250, 250, 252, 255))
    for i, s in enumerate([256, 48, 32, 16]):
        im = hi.resize((s, s), Image.LANCZOS)
        if s != cell:
            im = im.resize((cell - 16, cell - 16), Image.NEAREST)
        strip.paste(im, (i * cell + 8, 8), im)
    strip.save(os.path.join(ROOT, 'preview-sizes.png'), 'PNG')
    hi.resize((256, 256), Image.LANCZOS).save(os.path.join(ROOT, 'preview-icon.png'), 'PNG')
    print('icons written from', label, 'box', box)


def make_bg(src, blur=22, lighten=0.58, w=900, h=730, label=''):
    im = src.convert('RGB')
    sw, sh = im.size
    scale = max(w / float(sw), h / float(sh))
    im = im.resize((int(sw * scale + 1), int(sh * scale + 1)), Image.LANCZOS)
    nw, nh = im.size
    left = (nw - w) // 2
    top = int((nh - h) * 0.28)          # favour the upper part, where the face is
    im = im.crop((left, top, left + w, top + h))
    im = im.filter(ImageFilter.GaussianBlur(blur))
    im = Image.blend(im, Image.new('RGB', (w, h), (255, 255, 255)), lighten)
    im.save(os.path.join(ROOT, 'pc', 'bg.png'), 'PNG')
    im.save(os.path.join(ROOT, 'preview-bg.png'), 'PNG')
    print('wallpaper written from', label, w, 'x', h, 'blur', blur, 'lighten', lighten)


def main():
    items = refs()
    if not items:
        print('no reference images found in', REF)
        return 1

    if not sys.argv[1:]:
        for i, p in enumerate(items):
            with Image.open(p) as im:
                print(i, im.size, os.path.basename(p))
        return 0

    cmd = sys.argv[1]

    if cmd == '--list':
        for i, p in enumerate(items):
            with Image.open(p) as im:
                print(i, im.size, os.path.basename(p))
        return 0

    if cmd == '--icon':
        idx = int(sys.argv[2])
        box = [float(v) for v in sys.argv[3:7]]
        with Image.open(items[idx]) as im:
            make_icon(im, box, os.path.basename(items[idx]))
        return 0

    if cmd == '--bg':
        idx = int(sys.argv[2])
        blur = float(sys.argv[3]) if len(sys.argv) > 3 else 22
        lighten = float(sys.argv[4]) if len(sys.argv) > 4 else 0.58
        with Image.open(items[idx]) as im:
            make_bg(im, blur, lighten, label=os.path.basename(items[idx]))
        return 0

    print('unknown command', cmd)
    return 1


sys.exit(main())
