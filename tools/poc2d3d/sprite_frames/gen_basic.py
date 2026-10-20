"""New frames that follow mechanically from the four base images:
idle (2 frames per direction), turn, the front / back walk, and the HP profile GIF.
The rules are the old GIFs' own (measured in analyze.py):
  idle frame 2   the body 2 px lower, two leg rows taken out (feet stay)
  walk 1 / 3     the base image
  walk 0 / 2     the body 1 px lower; one leg lifted 2 px under the skirt, the other planted;
                 the hand on the lifted side 1 px lower, the other hand where it was
  turn           front, left, back, right base images
"""
import os
import numpy as np
from PIL import Image
from gen_common import base, rowmap, clean

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "out")


def save(name, frames):
    d = os.path.join(OUT, name)
    os.makedirs(d, exist_ok=True)
    for f in os.listdir(d):
        os.remove(os.path.join(d, f))
    for i, f in enumerate(frames):
        Image.fromarray(clean(f), "RGBA").save(os.path.join(d, f"{i:02d}.png"))


def idle_bob(b, direction):
    if direction in ("front", "back"):
        # rows 0..52 down 2, row 53 (sock shade) out, row 54 down 1, row 55 (boot repeat) out,
        # rows 56..61 (lower boot, sole) stay
        m = {r + 2: r for r in range(0, 53)}
        m[55] = 54
        m.update({r: r for r in range(56, 64)})
    else:
        # side: rows 0..50 down 2, two of the three white leg rows out, rows 53.. stay
        m = {r + 2: r for r in range(0, 51)}
        m.update({r: r for r in range(53, 64)})
    return rowmap(b, m)


LEFT_LEG = slice(24, 32)
RIGHT_LEG = slice(32, 40)
LEFT_HAND = (slice(21, 24), 22, 23)     # columns, outer column, inner column (skirt edge)
RIGHT_HAND = (slice(40, 43), 41, 40)


def stride(b, lifted):
    out = rowmap(b, {r + 1: r for r in range(0, 51)})          # body and skirt 1 px lower
    for leg in (LEFT_LEG, RIGHT_LEG):
        out[52:64, leg] = 0
        if (leg is LEFT_LEG) == (lifted == "left"):
            out[52:60, leg] = b[54:62, leg]                     # lifted 2 px (slides under the skirt)
        else:
            out[52:62, leg] = b[52:62, leg]                     # planted
    for hand, side in ((LEFT_HAND, "left"), (RIGHT_HAND, "right")):
        cols, outer, inner = hand
        if side == lifted:
            # 1 px lower than the body: the hand top repeated so the arm stays joined
            out[45, cols] = b[44, cols]
            out[46:51, cols] = b[44:49, cols]
        else:
            # where it was (1 px higher than the body)
            out[44:49, cols] = b[44:49, cols]
            out[49, outer] = 0
            out[49, inner] = b[49, inner] if b[49, inner, 3] else b[48, inner]
    return out


def main():
    bases = {d: base(d) for d in ("front", "back", "right", "left")}
    for d in ("front", "back", "left", "right"):
        save(f"{d}_idle", [bases[d], idle_bob(bases[d], d)])
    save("turn", [bases["front"], bases["left"], bases["back"], bases["right"]])
    for d in ("front", "back"):
        b = bases[d]
        save(f"walk_{d}", [stride(b, "left"), b, stride(b, "right"), b])
    save("halgif1", [bases["front"], idle_bob(bases["front"], "front")])
    print("written to", OUT)


if __name__ == "__main__":
    main()
