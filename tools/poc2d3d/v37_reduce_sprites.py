"""はるかわーるど ver3.7 (West area): reduce the 8x sprite renders (blender/scripts/render_v37_previews.py) to the
Object Catalog's pixel sizes and write them next to the existing sprites, with import settings
copied from desk.png.meta (Point filter, no mipmaps, uncompressed, 64 PPU) and a new GUID.
Existing files are never overwritten (new sprites only).

  python tools/poc2d3d/v37_reduce_sprites.py
"""
import json
import os
import re
import sys
import uuid

from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
HIRES = os.path.join(ROOT, "blender", "exports", "previews", "v37", "sprites_hires")
WORLD = os.path.join(ROOT, "unity-project", "Assets", "Content", "World")
TEMPLATE = os.path.join(WORLD, "desk.png.meta")


def reduce(spec):
    src = Image.open(os.path.join(HIRES, spec["sprite"] + ".png")).convert("RGBA")
    w, h = spec["width"], spec["height"]
    # The render has square pixels; the sprite is square ground (a cell as deep as it is wide).
    big = src.resize((w * 8, h * 8), Image.Resampling.NEAREST)
    small = big.resize((w, h), Image.Resampling.BOX)
    px = small.load()
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            px[x, y] = (r, g, b, 255) if a >= 128 else (0, 0, 0, 0)
    return small


def main():
    replace_names = set()
    with open(os.path.join(HIRES, "sprites.json"), encoding="utf-8") as f:
        specs = json.load(f)
    template = open(TEMPLATE, encoding="utf-8").read()
    for spec in specs:
        png = os.path.join(WORLD, spec["sprite"] + ".png")
        if os.path.exists(png) and spec["sprite"] not in replace_names:
            print("kept (exists):", png)
            continue
        reduce(spec).save(png)
        meta = png + ".meta"
        if not os.path.exists(meta):
            text = re.sub(r"^guid: [0-9a-f]{32}$", "guid: " + uuid.uuid4().hex, template, count=1, flags=re.M)
            with open(meta, "w", encoding="utf-8", newline="\n") as f:
                f.write(text)
        print("wrote", png, spec["width"], "x", spec["height"])


if __name__ == "__main__":
    main()
