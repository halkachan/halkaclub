"""ver4.0: the 2D sprites of Rin, Len and the four passers-by, made from their 3D renders
(blender/scripts/render_v40_character_sprites.py) so the 2D and 3D looks are the same character.

  python v40_sprites.py <renders_root> <unity Assets/Content/Character/V40>

For each character: the 12 renders (front / back / left / right x frame 0 / 10 / 20) are cropped to
their common box, scaled down (box filter) so the character is HEIGHT pixels tall, cut to hard
alpha, reduced to at most COLORS colours, outlined with the game's line colour (#252b39, the outline
of the 2D view) and placed bottom-centre on a square canvas. Frame 0 is the idle frame; 10 and 20
are the two walk frames. Same density as ﾊﾙｶﾁｬﾝ's sprites (64 pixels per unit, 32 per cell).
"""
import json
import os
import sys

from PIL import Image

OUTLINE = (0x25, 0x2B, 0x39, 255)
CHARACTERS = {
    # id: (render folder, character height in pixels, canvas, colours)
    "rin": ("spr_rin", 42, 48, 28),
    "len": ("spr_len", 42, 48, 28),
    "fan_male": ("spr_fan_male", 56, 64, 20),
    "fan_female": ("spr_fan_female", 56, 64, 20),
    "anti_male": ("spr_anti_male", 56, 64, 20),
    "anti_female": ("spr_anti_female", 56, 64, 20),
}
DIRECTIONS = ("front", "back", "left", "right")
FRAMES = (0, 10, 20)


def build(root, out_root, cid, folder, height, canvas_size, colors):
    images = {(d, f): Image.open(os.path.join(root, folder, "%s_%02d.png" % (d, f))).convert("RGBA")
              for d in DIRECTIONS for f in FRAMES}
    box = None
    for im in images.values():
        alpha = im.getchannel("A").point(lambda a: 255 if a >= 128 else 0)
        b = alpha.getbbox()
        if b is None:
            continue
        box = b if box is None else (min(box[0], b[0]), min(box[1], b[1]), max(box[2], b[2]), max(box[3], b[3]))
    scale = height / float(box[3] - box[1])
    out_dir = os.path.join(out_root, cid)
    os.makedirs(out_dir, exist_ok=True)
    written = []
    # one palette for the character (from all its frames), so frames never flicker in colour
    small = {}
    for key, im in images.items():
        crop = im.crop(box)
        w = max(1, round(crop.width * scale))
        h = max(1, round(crop.height * scale))
        # premultiplied box filter: colours of transparent texels never bleed in
        r, g, bch, a = crop.split()
        pre = Image.merge("RGBA", (Image.composite(r, Image.new("L", r.size, 0), a),
                                   Image.composite(g, Image.new("L", g.size, 0), a),
                                   Image.composite(bch, Image.new("L", bch.size, 0), a), a))
        reduced = pre.resize((w, h), Image.BOX)
        px = reduced.load()
        for y in range(h):
            for x in range(w):
                pr, pg, pb, pa = px[x, y]
                if pa >= 110:
                    k = 255.0 / pa
                    px[x, y] = (min(255, int(pr * k)), min(255, int(pg * k)), min(255, int(pb * k)), 255)
                else:
                    px[x, y] = (0, 0, 0, 0)
        small[key] = reduced
    strip = Image.new("RGB", (sum(im.width for im in small.values()), max(im.height for im in small.values())), (0, 0, 0))
    mask = Image.new("L", strip.size, 0)
    x = 0
    for im in small.values():
        strip.paste(im.convert("RGB"), (x, 0))
        mask.paste(im.getchannel("A"), (x, 0))
        x += im.width
    opaque = [strip.getpixel((i, j)) for j in range(strip.height) for i in range(strip.width) if mask.getpixel((i, j)) > 0]
    sample = Image.new("RGB", (len(opaque), 1))
    sample.putdata(opaque)
    palette_image = sample.quantize(colors=colors, method=Image.Quantize.MEDIANCUT)
    for (d, f), im in small.items():
        rgb = im.convert("RGB").quantize(palette=palette_image, dither=Image.Dither.NONE).convert("RGB")
        out = Image.new("RGBA", im.size, (0, 0, 0, 0))
        alpha = im.getchannel("A")
        out.paste(rgb, (0, 0), alpha)
        # outline: transparent texels next to the character (4-neighbourhood), on a 1-texel border
        framed = Image.new("RGBA", (out.width + 2, out.height + 2), (0, 0, 0, 0))
        framed.paste(out, (1, 1))
        fp = framed.load()
        src = [[fp[i, j][3] > 0 for i in range(framed.width)] for j in range(framed.height)]
        for j in range(framed.height):
            for i in range(framed.width):
                if src[j][i]:
                    continue
                if any(0 <= i + di < framed.width and 0 <= j + dj < framed.height and src[j + dj][i + di]
                       for di, dj in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                    fp[i, j] = OUTLINE
        canvas = Image.new("RGBA", (canvas_size, canvas_size), (0, 0, 0, 0))
        cx = (canvas_size - framed.width) // 2
        cy = canvas_size - framed.height       # feet on the canvas's bottom row
        canvas.paste(framed, (cx, cy), framed)
        state = "idle" if f == 0 else "walk"
        index = 0 if f in (0, 10) else 1
        name = "%s_%s_%d.png" % (d, state, index)
        canvas.save(os.path.join(out_dir, name))
        written.append(name)
    return {"id": cid, "height_px": height, "canvas": canvas_size, "colours": colors, "scale": round(scale, 4),
            "files": sorted(written)}


def main():
    root, out_root = sys.argv[1], sys.argv[2]
    report = [build(root, out_root, cid, *spec) for cid, spec in CHARACTERS.items()]
    with open(os.path.join(out_root, "sprites_manifest.json"), "w", encoding="utf-8") as f:
        json.dump(report, f, ensure_ascii=False, indent=2)
    print("sprites", [(r["id"], len(r["files"])) for r in report])


if __name__ == "__main__":
    main()
