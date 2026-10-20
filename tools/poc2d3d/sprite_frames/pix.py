"""Pixel art as text: one character per palette color ('.' = transparent), and back.

  python pix.py dump IMAGE [frame]     print the image as text rows (with row numbers)
  python pix.py palette                print the character for each color
Used to read the old frames and to write the new ones exactly.
"""
import sys
from PIL import Image, ImageSequence
import numpy as np

from gen_common import SG, NEW, CH

PALETTE = {
    (18, 18, 30): "K",      # ink outline
    (0, 0, 0): "0",         # pure black (clip letter)
    (25, 25, 36): "q",
    (35, 35, 45): "k",
    (36, 36, 46): "D",      # dark (skirt, boots)
    (58, 58, 64): "J",      # jacket
    (95, 90, 92): "j",      # jacket light / gray
    (160, 150, 152): "g",   # light gray
    (241, 226, 188): "Y",   # hair
    (200, 186, 160): "c",   # hair mid shade
    (157, 140, 120): "y",   # hair shadow
    (75, 122, 232): "B",    # blue
    (60, 95, 190): "b",     # dark blue
    (40, 60, 135): "n",     # navy
    (249, 246, 244): "S",   # skin / white legs
    (232, 214, 210): "p",   # skin shade
    (255, 255, 255): "W",   # white
    (254, 27, 109): "P",    # pink line under the eyes
}


def load(path, frame=0):
    if path in ("1", "2", "3", "4"):
        path = NEW + path + ".png"
    im = Image.open(path)
    frames = [f.convert("RGBA") for f in ImageSequence.Iterator(im)]
    return np.array(frames[frame])


def to_text(a, extra=None):
    pal = dict(PALETTE)
    rows = []
    unknown = {}
    for y in range(a.shape[0]):
        line = []
        for x in range(a.shape[1]):
            r, g, b, al = (int(v) for v in a[y, x])
            if al == 0:
                line.append(".")
            else:
                ch = pal.get((r, g, b))
                if ch is None:
                    ch = unknown.setdefault((r, g, b), "?")
                line.append(ch)
        rows.append("".join(line))
    return rows, unknown


def from_text(rows, width=None):
    inv = {v: k for k, v in PALETTE.items()}
    h = len(rows)
    w = width or max(len(r) for r in rows)
    a = np.zeros((h, w, 4), np.uint8)
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch == "." or ch == " ":
                continue
            a[y, x, :3] = inv[ch]
            a[y, x, 3] = 255
    return a


if __name__ == "__main__":
    if sys.argv[1] == "palette":
        for k, v in PALETTE.items():
            print(v, k)
    elif sys.argv[1] == "dump":
        a = load(sys.argv[2], int(sys.argv[3]) if len(sys.argv) > 3 else 0)
        rows, unknown = to_text(a)
        lo = int(sys.argv[4]) if len(sys.argv) > 4 else 0
        hi = int(sys.argv[5]) if len(sys.argv) > 5 else len(rows)
        print("    " + "".join(str(x // 10) for x in range(a.shape[1])))
        print("    " + "".join(str(x % 10) for x in range(a.shape[1])))
        for y in range(lo, hi):
            print(f"{y:3d} {rows[y]}")
        if unknown:
            print("UNKNOWN", unknown)
