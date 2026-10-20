"""New bench_sit.png (the seated picture used on the bench and the sofa).
Head and upper body: the new front base, unchanged pixels, placed like the old seated picture
placed the old head (4 px left, 2 rows lower than the new standing head; one jacket row less, so the
jacket ends where the old seated jacket ended). Below it: the old seated pose (knees forward, one leg
back) with its colours mapped to the new palette (its off-palette pixels snapped first; the cream
used for skin in the old picture becomes skin white, the old skin shade becomes the new shade)."""
import os
import numpy as np
from PIL import Image
import pix
from gen_common import base, clean

HERE = os.path.dirname(os.path.abspath(__file__))
INV = {v: k for k, v in pix.PALETTE.items()}


def old_snapped():
    a = np.array(Image.open(pix.CH + "bench_sit.png").convert("RGBA")).astype(int)
    pal = list(pix.PALETTE.keys())
    out = np.zeros_like(a)
    for y in range(64):
        for x in range(64):
            if a[y, x, 3] == 0:
                continue
            c = a[y, x, :3]
            p = min(pal, key=lambda q: sum((c - np.array(q)) ** 2))
            out[y, x, :3] = p
            out[y, x, 3] = 255
    return out.astype(np.uint8)


def recolor_lower(a):
    """Old seated legs to the new style: skin in the old picture was cream (Y) / hair shade (c)."""
    rows, _ = pix.to_text(a)
    m = {"Y": "S", "c": "p", "0": "K", "y": "p"}
    new = ["".join(m.get(ch, ch) for ch in r) for r in rows]
    return pix.from_text(new, 64)


def main(drop_row=40, dx=-4, dy=2, seam=45):
    nb = base("front")
    old = old_snapped()
    out = np.zeros((64, 64, 4), np.uint8)
    # head and upper body (new rows 3..43 without drop_row), shifted
    src_rows = [r for r in range(0, 44) if r != drop_row]
    for i, r in enumerate(src_rows):
        y = r + dy if r < drop_row else r + dy - 1
        if 0 <= y < seam:
            for x in range(64):
                sx = x - dx
                if 0 <= sx < 64 and nb[r, sx, 3]:
                    out[y, x] = nb[r, sx]
    lower = recolor_lower(old)
    out[seam:] = lower[seam:]
    Image.fromarray(clean(out), "RGBA").save(os.path.join(HERE, "out", "bench_sit.png"))
    print("bench_sit written")


if __name__ == "__main__":
    os.makedirs(os.path.join(HERE, "out"), exist_ok=True)
    main()
