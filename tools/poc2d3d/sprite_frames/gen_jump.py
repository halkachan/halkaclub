"""New front_jump (21 frames, 62 x 87) from the new front base, by the old jump's own operations
(measured in analyze_jump.py / fit_jump3.py):
  every frame   the front base, cropped 62 wide, at a height per frame
  squash        a centre column repeated, some rows taken out (hair, chest, hands, sock, boot)
  stretch       the centre column taken out, the same rows repeated
  deepest squash (frames 3, 17)  the upper body widened at three columns, the legs at one
The rows the old jump takes out or repeats are mapped to the same places in the new drawing;
each frame keeps the feet where the old frame had them, one row higher (like the new idle).
Self-check: the same rules applied to the old base give the old jump back."""
import json
import os
import numpy as np
from gen_common import base, old_frames, key
from gen_basic import save

HERE = os.path.dirname(os.path.abspath(__file__))
OPS = json.load(open(os.path.join(HERE, "jump_ops.json")))
W, H = 62, 87
SPECIAL = {3, 17}                     # deepest squash
SPECIAL_DROPS = [11, 29, 40, 45, 52, 55]


def old_events(i):
    """(op, ox, feet_frame_row, drops, dups) of old frame i."""
    if i in SPECIAL:
        f = old_frames("front_jump")[i]
        feet = max(np.nonzero(f[..., 3].any(axis=1))[0])
        return "special", 2, int(feet), list(SPECIAL_DROPS), []
    o = OPS[str(i)]
    rows = {int(k): v for k, v in o["rows"].items()}
    seq = [rows[k] for k in sorted(rows)]
    drops = [r for r in range(1, 63) if r not in seq]
    dups = sorted({r for r in seq if seq.count(r) > 1})
    feet = max(k for k, v in rows.items() if v == 62)
    op = "none" if o["op"][0] == "none" else f"{o['op'][0]}{o['op'][1]}"
    return op, o["ox"], feet, drops, dups


def widen(img, cols):
    out = []
    for x in range(img.shape[1]):
        out.append(img[:, x])
        if x in cols:
            out.append(img[:, x])
    return np.stack(out, axis=1)


def remove_col(img, c):
    return np.concatenate([img[:, :c], img[:, c + 1:]], axis=1)


def build(b, top, bottom, op, ox, feet, drops, dups, upper_last):
    seq = []
    for r in range(top, bottom + 1):
        if r in drops:
            continue
        seq.append(r)
        if r in dups:
            seq.append(r)
    out = np.zeros((H, W, 4), np.uint8)
    if op == "special":
        upper = widen(b, {22, 32, 42})[:, 2:2 + W]
        lower = widen(b, {32})[:, 1:1 + W]
    elif op.startswith("dup"):
        upper = lower = widen(b, {int(op[3:])})[:, ox:ox + W]
    elif op.startswith("del"):
        upper = lower = remove_col(b, int(op[3:]))[:, ox:ox + W]
    else:
        upper = lower = b[:, ox:ox + W]
    fr = feet
    for r in reversed(seq):
        out[fr] = (upper if r <= upper_last else lower)[r]
        fr -= 1
    return out


def frames(b, top, bottom, mapping, upper_last, feet_shift):
    result = []
    for i in range(21):
        op, ox, feet, drops, dups = old_events(i)
        nd = [mapping(r, "drop") for r in drops]
        nu = [mapping(r, "dup") for r in dups]
        result.append(build(b, top, bottom, op, ox, feet + feet_shift, nd, nu, upper_last))
    return result


def new_row(r, kind):
    table = {11: 12, 12: 13, 29: 30, 40: 41, 45: 46, 55: 55}
    if r == 52:
        return 53 if kind == "drop" else 52
    return table[r]


def main():
    old = old_frames("front_jump")
    ob = old_frames("front_idle")[0]
    check = frames(ob, 1, 62, lambda r, k: r, 48, 0)
    bad = [int(np.sum(key(a) != key(b))) for a, b in zip(check, old)]
    print("self-check (old rules on the old base vs the old jump), differing pixels per frame:", bad)
    nb = base("front")
    save("front_jump", frames(nb, 3, 61, new_row, 49, -1))
    print("front_jump written")


if __name__ == "__main__":
    main()
