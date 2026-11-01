"""ver4.0: the 2D sprites of Rin, Len and the four passers-by, made from their 3D renders
(blender/scripts/render_v40_character_sprites.py) so the 2D and 3D looks are the same character.

  python v40_sprites.py <renders_root> <unity Assets/Content/Character/V40>

For each character: the 12 renders (front / back / left / right x frame 0 / 10 / 20) are cropped to
their common box, scaled down (box filter) so the character is HEIGHT pixels tall, cut to hard
alpha, reduced to at most COLORS colours, outlined with the game's line colour (#252b39, the outline
of the 2D view) and placed bottom-centre on a square canvas. Frame 0 is the idle frame; 10 and 20
are the two walk frames. Same density as ﾊﾙｶﾁｬﾝ's sprites (64 pixels per unit, 32 per cell).

ver4.0 (the user's look after the second round: Rin and Len looked too 3D-real in 2D; the user chose
"B 2トーン"): Rin and Len are drawn from a FLAT render (render_v40_character_sprites.py with
HALKA_SPRITE_FLAT=1: each material's own colour, unlit) in two tones: where the lit render is darker
than the flat colour by more than CEL_CUT, the colour times CEL_SHADOW, else the colour itself (anime
cel shading, no gradients). When scaling down, a texel whose block is a quarter or more very dark
keeps that dark colour (their eyes and mouths would otherwise blur away).

After the user's next look ("more shade on them, and the outline unlike the others'"; the user chose
I2): the shade is warm (hair amber, skin peach: CEL_WARM) and covers about four tenths of the hair
(CEL_CUT 1.1 against the lit / flat brightness); skin is shaded only where it is much darker (under
the bangs: SKIN_CUT). Gaps of one to three texels between a strand and the face are filled with the
colour beside them (outlined, they became black dots). The outline is drawn as on ﾊﾙｶﾁｬﾝ's sprites:
on the character's own edge texels, in her line colour (#12121e), not as an extra ring outside.
Rin's front frames: scaled down, her left eye fell half a texel off and took an extra row (a black
blob beside the cheek); her eyes are drawn as one pair there, the left a mirror of the right
(MIRROR_EYES).
"""
import json
import os
import sys

from PIL import Image

OUTLINE = (0x25, 0x2B, 0x39, 255)
CEL_SHADOW = 0.8          # (the first two-tone version; kept for reference)
CEL_WARM = (0.9, 0.78, 0.68)
CEL_CUT = 1.1
SKIN_CUT = 0.85
INNER_OUTLINE = (18, 18, 30, 255)   # ﾊﾙｶﾁｬﾝ's line colour
MIRROR_EYES = {"rin"}               # front frames: the left eye a mirror of the right
DARK_LUMA = 70
DARK_SHARE = 0.25
CHARACTERS = {
    # id: (render folder, character height in pixels, canvas, colours[, flat render folder for two tones])
    "rin": ("spr_rin", 42, 48, 24, "flat_spr_rin"),
    "len": ("spr_len", 42, 48, 24, "flat_spr_len"),
    "fan_male": ("spr_fan_male", 56, 64, 20),
    "fan_female": ("spr_fan_female", 56, 64, 20),
    "anti_male": ("spr_anti_male", 56, 64, 20),
    "anti_female": ("spr_anti_female", 56, 64, 20),
}
DIRECTIONS = ("front", "back", "left", "right")
FRAMES = (0, 10, 20)


def luma(c):
    return 0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2]


def is_skin(c):
    return c[0] > 200 and c[1] > 170 and c[2] > 130 and 15 < c[0] - c[2] < 90


def two_tone(lit, flat):
    """The flat colours, one warm shade step darker where the lit render is in shade."""
    lp, fp = lit.load(), flat.load()
    out = Image.new("RGBA", flat.size, (0, 0, 0, 0))
    op = out.load()
    for y in range(flat.height):
        for x in range(flat.width):
            f = fp[x, y]
            if f[3] < 128:
                continue
            ratio = (luma(lp[x, y]) + 1) / (luma(f) + 1)
            if ratio < (SKIN_CUT if is_skin(f) else CEL_CUT):
                op[x, y] = (int(f[0] * CEL_WARM[0]), int(f[1] * CEL_WARM[1]), int(f[2] * CEL_WARM[2]), 255)
            else:
                op[x, y] = (f[0], f[1], f[2], 255)
    return out


def close_gaps(im):
    """Fill transparent gaps of one to three texels between opaque texels (left-right or up-down)
    with the colour of the texel beside them."""
    p = im.load()
    w, h = im.size
    fill = {}
    for y in range(h):
        for x in range(w):
            if p[x, y][3]:
                continue
            for dx, dy in ((1, 0), (0, 1)):
                for gap in (1, 2, 3):
                    a = (x - dx, y - dy)
                    b = (x + dx * gap, y + dy * gap)
                    if not (0 <= a[0] < w and 0 <= a[1] < h and 0 <= b[0] < w and 0 <= b[1] < h):
                        continue
                    if p[a][3] and p[b][3] and all(p[x + dx * k, y + dy * k][3] == 0 for k in range(gap)):
                        fill[(x, y)] = p[a]
                        break
                if (x, y) in fill:
                    break
    for xy, c in fill.items():
        p[xy] = c
    return im


def mirror_eyes(im):
    """Front view: the dark texels of the right eye (at least 4 texels right of the head's middle)
    are copied, mirrored, onto the left half of the same rows; the left half's own dark texels there
    take the colour of their mirror texel."""
    p = im.load()
    w, h = im.size
    rows = [y for y in range(h) if any(p[x, y][3] for x in range(w))]
    spans = [(min(x for x in range(w) if p[x, y][3]), max(x for x in range(w) if p[x, y][3])) for y in rows]
    widest = max(b - a for a, b in spans)
    head = [(a, b) for a, b in spans if b - a >= widest - 2]
    centre = round(sum(a + b for a, b in head) / (2.0 * len(head)))

    def dark(c):
        return c[3] and luma(c) < 90

    eye_rows = sorted({y for y in range(h) for x in range(centre + 4, min(w, centre + 9)) if dark(p[x, y])})
    if not eye_rows:
        return im
    for y in range(eye_rows[0] - 1, eye_rows[-1] + 1):
        for x in range(centre - 8, centre - 3):
            m = 2 * centre - x
            if 0 <= x < w and 0 <= m < w and (dark(p[x, y]) or dark(p[m, y])) and p[x, y][3] and p[m, y][3]:
                p[x, y] = p[m, y]
    return im


def inner_outline(im):
    """ﾊﾙｶﾁｬﾝ's way: the character's own edge texels become the line (4-neighbourhood)."""
    src = im.copy().load()
    p = im.load()
    w, h = im.size
    for y in range(h):
        for x in range(w):
            if src[x, y][3] == 0:
                continue
            if any(not (0 <= x + dx < w and 0 <= y + dy < h) or src[x + dx, y + dy][3] == 0
                   for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                p[x, y] = INNER_OUTLINE
    return im


def build(root, out_root, cid, folder, height, canvas_size, colors, flat_folder=None):
    images = {(d, f): Image.open(os.path.join(root, folder, "%s_%02d.png" % (d, f))).convert("RGBA")
              for d in DIRECTIONS for f in FRAMES}
    if flat_folder:
        images = {key: two_tone(im, Image.open(os.path.join(root, flat_folder, "%s_%02d.png" % key)).convert("RGBA"))
                  for key, im in images.items()}
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
        if flat_folder:
            # small dark features (eyes, mouth) keep their dark colour
            src = crop.load()
            bw, bh = crop.width / w, crop.height / h
            for y in range(h):
                for x in range(w):
                    if px[x, y][3] == 0:
                        continue
                    total = dark = 0
                    acc = [0, 0, 0]
                    for yy in range(int(y * bh), max(int(y * bh) + 1, int((y + 1) * bh))):
                        for xx in range(int(x * bw), max(int(x * bw) + 1, int((x + 1) * bw))):
                            c = src[xx, yy]
                            if c[3] < 128:
                                continue
                            total += 1
                            if luma(c) < DARK_LUMA:
                                dark += 1
                                acc = [acc[0] + c[0], acc[1] + c[1], acc[2] + c[2]]
                    if total and dark >= total * DARK_SHARE:
                        px[x, y] = (acc[0] // dark, acc[1] // dark, acc[2] // dark, 255)
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
        if flat_folder:
            # Rin and Len: no ring outside; their own edge is the line, as on ﾊﾙｶﾁｬﾝ
            if d == "front" and cid in MIRROR_EYES:
                mirror_eyes(out)
            inner_outline(close_gaps(out))
            canvas = Image.new("RGBA", (canvas_size, canvas_size), (0, 0, 0, 0))
            canvas.paste(out, ((canvas_size - out.width - 2) // 2 + 1, canvas_size - 1 - out.height), out)   
            state = "idle" if f == 0 else "walk"
            index = 0 if f in (0, 10) else 1
            name = "%s_%s_%d.png" % (d, state, index)
            canvas.save(os.path.join(out_dir, name))
            written.append(name)
            continue
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
            "two_tone": bool(flat_folder), "files": sorted(written)}


def main():
    root, out_root = sys.argv[1], sys.argv[2]
    report = [build(root, out_root, cid, *spec) for cid, spec in CHARACTERS.items()]
    with open(os.path.join(out_root, "sprites_manifest.json"), "w", encoding="utf-8") as f:
        json.dump(report, f, ensure_ascii=False, indent=2)
    print("sprites", [(r["id"], len(r["files"])) for r in report])


if __name__ == "__main__":
    main()
