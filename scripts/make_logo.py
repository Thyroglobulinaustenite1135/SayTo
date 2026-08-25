"""Generate SayTo logo assets (PNG master, ICO, WPF assets) + README banner.

Concept: microphone silhouette built from three text lines ("speech -> text")
on an indigo->violet gradient squircle.
"""
import os
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
LOGO_DIR = os.path.join(ROOT, "logo")
ASSETS = os.path.join(ROOT, "src", "SayTo", "Assets")
DOCS = os.path.join(ROOT, "docs")
os.makedirs(LOGO_DIR, exist_ok=True)
os.makedirs(ASSETS, exist_ok=True)
os.makedirs(DOCS, exist_ok=True)

S = 1024          # master canvas
R_BG = 230        # background corner radius

TOP = (123, 108, 246)    # #7B6CF6
BOT = (92, 66, 232)      # #5C42E8


def build_icon() -> Image.Image:
    grad = Image.linear_gradient("L").resize((S, S))
    bg = Image.new("RGBA", (S, S), TOP + (255,))
    bot_layer = Image.new("RGBA", (S, S), BOT + (255,))
    bg = Image.composite(bot_layer, bg, grad)

    # soft diagonal highlight top-left
    hi = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(hi)
    d.ellipse((-S * 0.45, -S * 0.55, S * 0.75, S * 0.35), fill=(255, 255, 255, 46))
    hi = hi.filter(ImageFilter.GaussianBlur(120))

    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    mask = Image.new("L", (S, S), 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, S - 1, S - 1), radius=R_BG, fill=255)
    base = Image.alpha_composite(bg, hi)
    img.paste(base, (0, 0), mask)

    d = ImageDraw.Draw(img)
    W = (255, 255, 255, 255)

    def bar(x, y, w, h):
        d.rounded_rectangle((x, y, x + w, y + h), radius=h // 2, fill=W)

    # microphone body made of three text lines (tall capsule proportions)
    bar(422, 280, 180, 76)
    bar(397, 382, 230, 76)
    bar(422, 484, 180, 76)

    # wide cradle arc clearing the capsule (sweeps through the bottom, 15°..165°)
    cx, cy, R, sw = 512, 420, 200, 32
    bbox = (cx - R, cy - R, cx + R, cy + R)
    d.arc(bbox, start=15, end=165, fill=W, width=sw)
    import math
    for ang in (15, 165):
        ex = cx + R * math.cos(math.radians(ang))
        ey = cy + R * math.sin(math.radians(ang))
        d.ellipse((ex - sw / 2, ey - sw / 2, ex + sw / 2, ey + sw / 2), fill=W)

    # stem + base
    bar(497, 625, 30, 65)
    bar(407, 705, 210, 40)
    return img


def svg() -> str:
    return """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512" width="512" height="512">
  <defs>
    <linearGradient id="bg" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="#7B6CF6"/>
      <stop offset="1" stop-color="#5C42E8"/>
    </linearGradient>
  </defs>
  <rect width="512" height="512" rx="115" fill="url(#bg)"/>
  <g fill="#FFFFFF">
    <rect x="211" y="140" width="90" height="38" rx="19"/>
    <rect x="198.5" y="191" width="115" height="38" rx="19"/>
    <rect x="211" y="242" width="90" height="38" rx="19"/>
    <path d="M 159.4 235.9 A 100 100 0 0 1 352.6 235.9"
          fill="none" stroke="#FFFFFF" stroke-width="16" stroke-linecap="round"/>
    <rect x="248.5" y="312.5" width="15" height="32.5" rx="7.5"/>
    <rect x="203.5" y="352.5" width="105" height="20" rx="10"/>
  </g>
</svg>
"""


icon = build_icon()
icon.save(os.path.join(LOGO_DIR, "sayto-icon.png"))
icon.resize((256, 256), Image.LANCZOS).save(os.path.join(ASSETS, "logo.png"))

# multi-size Windows ICO
ico_sizes = [(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)]
icon.resize((256, 256), Image.LANCZOS).save(os.path.join(ASSETS, "sayto.ico"), sizes=ico_sizes)

with open(os.path.join(LOGO_DIR, "sayto.svg"), "w", encoding="utf-8") as f:
    f.write(svg().strip())

print("logo assets written")


def banner() -> None:
    font_path = os.path.join(ASSETS, "fonts", "Vazirmatn-Bold.ttf")
    if not os.path.exists(font_path):
        print("font missing, skip banner")
        return
    W, H = 1400, 320
    b = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    ic = icon.resize((224, 224), Image.LANCZOS)
    b.alpha_composite(ic, (28, 48))
    f_big = ImageFont.truetype(font_path, 118)
    f_sub = ImageFont.truetype(font_path, 34)
    d = ImageDraw.Draw(b)
    tx = 292
    d.text((tx, 62), "SayTo", font=f_big, fill=(31, 35, 40, 255))
    d.text((tx + 6, 208), "Speech to text, offline · Persian & English",
           font=f_sub, fill=(87, 96, 106, 255))
    b.save(os.path.join(DOCS, "banner.png"))
    print("banner written")


banner()
