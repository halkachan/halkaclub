"""はるかわーるど ver3.6 (North area): the water Surface's catalog sprite (WORLD EDITOR shows it; the game
draws the water itself): a 32x32 pixel tile in the pond's colours, with the import settings of
desk.png.meta (Point filter, no mipmaps, uncompressed, 64 PPU) and a new GUID. Never overwrites.

  python tools/poc2d3d/v36_water_sprite.py
"""
import os
import re
import uuid

from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
WORLD = os.path.join(ROOT, "unity-project", "Assets", "Content", "World")
DEEP, MID, LIGHT = (63, 108, 160, 255), (84, 132, 182, 255), (164, 200, 232, 255)


def main():
    png = os.path.join(WORLD, "water.png")
    if os.path.exists(png):
        print("kept (exists):", png)
        return
    img = Image.new("RGBA", (32, 32), MID)
    px = img.load()
    for y in range(32):
        for x in range(32):
            if (x + y * 3) % 23 == 0 or y >= 28:
                px[x, y] = DEEP
    for (x0, y0, w) in ((4, 6, 6), (18, 13, 5), (8, 21, 4), (22, 24, 6)):   # small glints
        for x in range(x0, x0 + w):
            px[x, y0] = LIGHT
    img.save(png)
    meta = png + ".meta"
    if not os.path.exists(meta):
        template = open(os.path.join(WORLD, "desk.png.meta"), encoding="utf-8").read()
        text = re.sub(r"^guid: [0-9a-f]{32}$", "guid: " + uuid.uuid4().hex, template, count=1, flags=re.M)
        with open(meta, "w", encoding="utf-8", newline="\n") as f:
            f.write(text)
    print("wrote", png)


if __name__ == "__main__":
    main()
