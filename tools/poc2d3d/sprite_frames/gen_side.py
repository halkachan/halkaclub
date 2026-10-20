"""New right / left walk frames from the new side base images, following the old walk exactly:
  frames 1 / 3   the base image
  frames 0 / 2   body 1 px lower; the back hair swings (measured per row in analyze_side.py);
                 the near arm swings back (frame 0) or forward (frame 2) about the shoulder;
                 the legs are the old stride drawings with the two rows the new legs dropped
                 taken out of each leg (the new side leg is the old leg minus its top shade row
                 and one boot row).
The new side art sits one row lower than the old from the hair down to the skirt, so an old
frame row r corresponds to the new frame row r + 1 there.
Left frames: the right frames mirrored, with the pixels where the left base is not the mirror
of the right base (the face side and a jacket line) taken from the left base.
"""
import numpy as np
from gen_common import base, old_frames, rowmap
from gen_basic import save

OLD = {0: old_frames("walk_right")[0], 2: old_frames("walk_right")[2]}

# Back hair: (new frame rows, last column of the swinging part, shift) per frame.
HAIR = {
    0: [(range(16, 25), 16, -1), (range(25, 27), 13, -2), (range(27, 28), 26, -1), (range(28, 29), 24, -1),
        (range(29, 31), 26, -1), (range(31, 37), 26, -1)],
    2: [(range(21, 27), 18, +1), (range(27, 29), 15, +1)],
}
# frame 2 also moves the lower back hair (rows 27-36, columns 16..26) one pixel back
HAIR_BACK2 = [(range(27, 29), 16, 26, -1), (range(29, 37), 13, 26, -1)]

# Near arm in the new base: rows 41..49, columns per row; shift per frame row (base row + 1).
ARM_COLS = {41: (24, 29), 42: (24, 29), 43: (24, 29), 44: (24, 29), 45: (23, 29), 46: (24, 29),
            47: (24, 29), 48: (24, 29), 49: (23, 29)}
ARM_DX = {0: {41: -2, 42: -3, 43: -4, 44: -5, 45: -5, 46: -5, 47: -5, 48: -5, 49: -5},
          2: {41: +2, 42: +3, 43: +4, 44: +5, 45: +5, 46: +5, 47: +5, 48: +5, 49: +5}}
SHOULDER = {0: -1, 2: +1}   # the shoulder line (base row 40, columns 24..28)

# Old stride legs (old frame rows 51..62): far-leg columns; the rest of a row is the near leg.
FAR = {0: {52: (26, 29), 53: (26, 29), 54: (25, 29), 55: (24, 29), 56: (23, 31), 57: (23, 30),
           58: (22, 31), 59: (22, 31), 60: (21, 32)},
       2: {51: (33, 33), 52: (31, 34), 53: (31, 34), 54: (30, 35), 55: (32, 35), 56: (32, 36),
           57: (33, 38), 58: (32, 40), 59: (32, 41), 60: (32, 42), 61: (32, 42), 62: (33, 43)}}
NEAR = {0: {51: (27, 33), 52: (29, 34), 53: (29, 34), 54: (30, 35), 55: (30, 35), 56: (31, 36),
            57: (31, 38), 58: (31, 40), 59: (31, 41), 60: (32, 42), 61: (32, 42), 62: (33, 43)},
        2: {51: (27, 32), 52: (26, 31), 53: (26, 31), 54: (25, 30), 55: (24, 32), 56: (23, 32),
            57: (23, 33), 58: (22, 32), 59: (22, 32), 60: (21, 32)}}
# Rows taken out of each leg (old frame rows), like the base leg lost its shade and boot rows.
DROP = {0: {"near": {53, 59}, "far": {53, 57}}, 2: {"near": {53, 57}, "far": {53, 59}}}


def legs(fi):
    old = OLD[fi]
    out = np.zeros_like(old)
    for layer in ("far", "near"):                      # near leg drawn over the far leg
        cols = (FAR if layer == "far" else NEAR)[fi]
        drop = DROP[fi][layer]
        for r, (a, b) in cols.items():
            if r in drop:
                continue
            nr = r + 1 - sum(1 for d in drop if d < r)
            seg = old[r, a:b + 1]
            m = seg[..., 3] > 0
            out[nr, a:b + 1][m] = seg[m]
    return out


def stride_right(b, fi):
    old = OLD[fi]
    out = rowmap(b, {r + 1: r for r in range(0, 51)})   # 1 px lower (rows 0..50, skirt at 51)
    bob = out.copy()
    # back hair
    for rows, hi, dx in HAIR[fi]:
        for r in rows:
            row = bob[r].copy()
            for x in range(0, hi + 1):
                sx = x - dx
                out[r, x] = row[sx] if 0 <= sx < 64 else 0
    if fi == 2:
        for rows, lo, hi, dx in HAIR_BACK2:
            for r in rows:
                row = bob[r].copy()
                for x in range(lo, hi + 1):
                    out[r, x] = row[x - dx]
    # shoulder line
    r = 41
    row = bob[r].copy()
    for x in range(24, 29):
        out[r, x] = row[x - SHOULDER[fi]]
    # near arm: take it out (fill = the old frame's body behind it, one row up), then draw it moved
    for br, (a, c) in ARM_COLS.items():
        fr = br + 1
        for x in range(a, c + 1):
            out[fr, x] = old[fr - 1, x]
    for br, (a, c) in ARM_COLS.items():
        fr = br + 1
        dx = ARM_DX[fi][br]
        for x in range(a, c + 1):
            px = b[br, x]
            if px[3]:
                out[fr, x + dx] = px
    # legs (rows 52..61) under the skirt row
    out[52:64] = 0
    lg = legs(fi)
    out[52:64] = lg[52:64]
    return out


def mirror_left(right_frame, rb, lb, dy):
    """The right frame mirrored; pixels where the left base is not the mirror of the right base
    taken from the left base, moved down dy (the frame's body offset)."""
    out = right_frame[:, ::-1].copy()
    mr = rb[:, ::-1]
    diff = np.any(mr != lb, axis=2) & ((mr[..., 3] > 0) | (lb[..., 3] > 0))
    for y, x in zip(*np.nonzero(diff)):
        if y + dy < 64:
            out[y + dy, x] = lb[y, x]
    return out


def main():
    rb, lb = base("right"), base("left")
    f0, f2 = stride_right(rb, 0), stride_right(rb, 2)
    save("walk_right", [f0, rb, f2, rb])
    save("walk_left", [mirror_left(f0, rb, lb, 1), lb, mirror_left(f2, rb, lb, 1), lb])
    print("side walks written")


if __name__ == "__main__":
    main()
