"""Shared helpers for building the ﾊﾙｶﾁｬﾝ frames from the user's four base pictures (bases/) by the
motion of the old GIFs (templates/)."""
import numpy as np
from PIL import Image, ImageSequence

import os
HERE = os.path.dirname(os.path.abspath(__file__))
NEW = os.path.join(HERE, "bases") + "/"            # the user's four pictures: 1 front, 2 back, 3 right, 4 left
SG = os.path.join(HERE, "templates") + "/"         # the old GIFs: the motion to follow
CH = SG                                            # templates/bench_sit.png: the old seated picture

EMPTY = np.zeros(4, np.uint8)


def clean(a):
    """Transparent pixels all (0,0,0,0); opaque stay as they are."""
    a = a.copy()
    a[a[..., 3] == 0] = 0
    return a


def base(direction):
    n = {"front": "1", "back": "2", "right": "3", "left": "4"}[direction]
    return clean(np.array(Image.open(NEW + n + ".png").convert("RGBA")))


def old_frames(name):
    return [clean(np.array(f.convert("RGBA"))) for f in ImageSequence.Iterator(Image.open(SG + name + ".gif"))]


def rowmap(src, mapping, height=None):
    """mapping: {dest_row: src_row}; rows not in mapping are empty."""
    h = height or src.shape[0]
    out = np.zeros((h, src.shape[1], 4), np.uint8)
    for d, s in mapping.items():
        if 0 <= d < h and 0 <= s < src.shape[0]:
            out[d] = src[s]
    return out


def shift_rows(src, lo, hi, dy):
    """Rows lo..hi (inclusive) of src moved by dy into a new empty image."""
    return rowmap(src, {r + dy: r for r in range(lo, hi + 1)})


def paste(dst, src, mask=None):
    """Opaque pixels of src over dst."""
    m = src[..., 3] > 0 if mask is None else mask
    dst = dst.copy()
    dst[m] = src[m]
    return dst


def key(a):
    a = a.astype(np.int64)
    k = (a[..., 0] << 24) | (a[..., 1] << 16) | (a[..., 2] << 8) | a[..., 3]
    k[a[..., 3] == 0] = 0
    return k


def same(a, b):
    return np.array_equal(key(a), key(b))
