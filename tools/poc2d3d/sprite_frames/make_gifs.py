"""Write the new frames as GIFs with the old GIFs' timing (loop forever, frames replace each
other), then read them back and check every pixel survives (exact colours, transparency).
  python make_gifs.py OUT_DIR"""
import os
import sys
import numpy as np
from PIL import Image, ImageSequence
from gen_common import clean, key, SG

HERE = os.path.dirname(os.path.abspath(__file__))
FRAMES = os.path.join(HERE, "out")
NAMES = ("front_idle", "back_idle", "left_idle", "right_idle", "walk_front", "walk_back", "walk_left",
         "walk_right", "turn", "front_jump")


def load(name):
    d = os.path.join(FRAMES, name)
    return [clean(np.array(Image.open(os.path.join(d, f)).convert("RGBA"))) for f in sorted(os.listdir(d))]


def old_durations(name):
    im = Image.open(SG + name + ".gif")
    out = []
    for i in range(im.n_frames):
        im.seek(i)
        out.append(im.info.get("duration"))
    return out


def to_p(frames):
    colors = []
    for f in frames:
        for c in {tuple(int(v) for v in p[:3]) for p in f.reshape(-1, 4) if p[3]}:
            if c not in colors:
                colors.append(c)
    assert len(colors) <= 255, len(colors)
    pal = [(0, 0, 0)] + colors                 # index 0 = transparent
    index = {c: i + 1 for i, c in enumerate(colors)}
    flat = [v for c in pal for v in c] + [0] * (3 * (256 - len(pal)))
    out = []
    for f in frames:
        idx = np.zeros(f.shape[:2], np.uint8)
        for y, x in zip(*np.nonzero(f[..., 3])):
            idx[y, x] = index[tuple(int(v) for v in f[y, x, :3])]
        im = Image.fromarray(idx, "P")
        im.putpalette(flat)
        im.info["transparency"] = 0
        out.append(im)
    return out


def write(name, frames, durations, path):
    ps = to_p(frames)
    ps[0].save(path, save_all=True, append_images=ps[1:], duration=durations, loop=0, disposal=2,
               transparency=0, optimize=False)


def readback(path):
    return [clean(np.array(f.convert("RGBA"))) for f in ImageSequence.Iterator(Image.open(path))]


def main(out_dir):
    os.makedirs(out_dir, exist_ok=True)
    for name in NAMES:
        frames = load(name)
        durations = old_durations(name)
        assert len(durations) == len(frames), (name, len(durations), len(frames))
        path = os.path.join(out_dir, name + ".gif")
        write(name, frames, durations, path)
        back = readback(path)
        ok = len(back) == len(frames) and all(np.array_equal(key(a), key(b)) for a, b in zip(frames, back))
        im = Image.open(path)
        d = []
        for i in range(im.n_frames):
            im.seek(i)
            d.append(im.info.get("duration"))
        print(f"{name}: {len(frames)} frames {frames[0].shape[1]}x{frames[0].shape[0]} durations {d} round-trip {'OK' if ok else 'FAILED'}")
        if not ok:
            sys.exit(1)
    # the HP profile GIF (front idle, 400 ms): prepared only
    hp = load("halgif1")
    path = os.path.join(out_dir, "halgif1.gif")
    write("halgif1", hp, [400, 400], path)
    back = readback(path)
    print("halgif1 (HP profile, prepared):", "OK" if all(np.array_equal(key(a), key(b)) for a, b in zip(hp, back)) else "FAILED")


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, "gifs"))
