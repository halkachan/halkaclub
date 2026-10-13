"""Crops the same Map cells from 1280x720 screenshots of the current 2D game and of the
Phase 2 Pixel View (both show first_field centred at 32 CSS px per cell), enlarged with
nearest neighbour, into one labelled sheet per object.

  python tools/poc2d3d/compare_crops.py OUT_DIR CURRENT.png NEW16.png NEW32.png
"""
import os
import sys

from PIL import Image, ImageDraw, ImageFont

CELL = 32
TARGETS = {  # name: (cell x, cell y, left, top, right, bottom) in screen px around the root cell
    "tree": (-4, 3, -60, -118, 60, 22),
    "flower": (6, -3, -30, -46, 30, 20),
    "stone": (-1, -5, -30, -40, 30, 20),
    "grass_and_road": (-3, -1, -80, -60, 80, 60),
}


def font():
    for path in ("C:/Windows/Fonts/meiryo.ttc", "C:/Windows/Fonts/arial.ttf"):
        try:
            return ImageFont.truetype(path, 20)
        except OSError:
            pass
    return ImageFont.load_default()


def main():
    out, sources = sys.argv[1], sys.argv[2:5]
    labels = ["現行2D Sprite", "Phase 2 3D→2D 16px/マス", "Phase 2 3D→2D 32px/マス"]
    images = [Image.open(p).convert("RGB") for p in sources]
    os.makedirs(out, exist_ok=True)
    for name, (cx, cy, l, t, r, b) in TARGETS.items():
        x, y = 640 + cx * CELL, 360 - cy * CELL
        zoom = 4 if name != "grass_and_road" else 3
        crops = [im.crop((x + l, y + t, x + r, y + b)).resize(((r - l) * zoom, (b - t) * zoom), Image.NEAREST) for im in images]
        w, h = crops[0].size
        sheet = Image.new("RGB", (w * 3 + 40, h + 40), (24, 24, 28))
        draw = ImageDraw.Draw(sheet)
        for i, (crop, label) in enumerate(zip(crops, labels)):
            sheet.paste(crop, (i * (w + 20), 36))
            draw.text((i * (w + 20) + 6, 6), label, fill=(240, 240, 240), font=font())
        sheet.save(os.path.join(out, f"compare_2d_{name}.png"))
        print("saved", name)


if __name__ == "__main__":
    main()
