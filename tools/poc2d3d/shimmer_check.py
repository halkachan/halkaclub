"""Pixel-shimmer check for 2D Pixel View screenshot bursts.

For each consecutive frame pair it finds the integer screen translation that best aligns a
static region (above the player, away from HUD and touch controls) and reports how many
pixels still differ after that shift. With whole-pixel camera snapping, static scenery must
match exactly (0 differing pixels) even while the camera follows.

  python tools/poc2d3d/shimmer_check.py FRAME_PREFIX [--region x0,y0,x1,y1] [--step 6]

--step is the integer upscale of the pixel view (whole-pixel camera moves are multiples of it).
"""
import argparse
import glob

import numpy as np
from PIL import Image


def main():
    p = argparse.ArgumentParser()
    p.add_argument("prefix")
    p.add_argument("--region", default="0,380,780,1150")
    p.add_argument("--exclude", default="220,480,600,960", help="player and its shadow (they follow the camera)")
    p.add_argument("--search-x", type=int, default=150)
    p.add_argument("--search-y", type=int, default=24)
    p.add_argument("--step", type=int, default=6)
    a = p.parse_args()
    files = sorted(glob.glob(a.prefix + "_*.png"))
    x0, y0, x1, y1 = map(int, a.region.split(","))
    ex0, ey0, ex1, ey1 = map(int, a.exclude.split(","))
    frames = [np.asarray(Image.open(f).convert("RGB"), dtype=np.int16) for f in files]
    sx, sy = a.search_x, a.search_y
    mask = np.ones((y1 - y0 - 2 * sy, x1 - x0 - 2 * sx), dtype=bool)
    mask[max(0, ey0 - y0 - sy):max(0, ey1 - y0 - sy), max(0, ex0 - x0 - sx):max(0, ex1 - x0 - sx)] = False
    worst = 0
    moved = 0
    for i in range(1, len(frames)):
        prev, cur = frames[i - 1], frames[i]
        base = prev[y0 + sy:y1 - sy, x0 + sx:x1 - sx]
        best = None
        for dy in range(-sy, sy + 1, a.step):
            for dx in range(-sx, sx + 1, a.step):
                shifted = cur[y0 + sy + dy:y1 - sy + dy, x0 + sx + dx:x1 - sx + dx]
                diff = np.any(np.abs(shifted - base) > 2, axis=2) & mask
                count = int(diff.sum())
                if best is None or count < best[0]:
                    best = (count, dx, dy)
        worst = max(worst, best[0])
        moved += best[1] != 0 or best[2] != 0
        print(f"{files[i - 1][-7:-4]}->{files[i][-7:-4]} shift=({best[1]:+d},{best[2]:+d}) differing_pixels={best[0]}")
    print(f"SUMMARY pairs={len(frames) - 1} camera_moved_pairs={moved} worst_differing_pixels={worst}")


if __name__ == "__main__":
    main()
