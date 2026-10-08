"""Technical postprocessing for the three unapproved GPT Image sitting-pose candidates.

The source PNGs contain the character design. This script only crops, reduces
with nearest-neighbor sampling, reduces palette, cleans alpha and makes previews.
It never produces the final Unity sprite without the user's selection.
"""

from pathlib import Path
from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parent
CANDIDATES = ROOT / "candidates"
PLAYER = ROOT.parents[1] / "Assets/Content/Character/front_idle/00.png"
BENCH = ROOT.parents[1] / "Assets/Content/World/bench.png"


def build_sprite(path: Path) -> Image.Image:
    source = Image.open(path).convert("RGBA")
    alpha = source.getchannel("A").point(lambda value: 255 if value >= 128 else 0)
    bounds = alpha.getbbox()
    if bounds is None:
        raise ValueError(f"Empty candidate: {path}")
    cropped = source.crop(bounds)
    cropped.putalpha(alpha.crop(bounds))
    width, height = cropped.size
    scale = min(50 / width, 58 / height)
    reduced = cropped.resize(
        (max(1, round(width * scale)), max(1, round(height * scale))),
        Image.Resampling.NEAREST,
    )
    reduced_alpha = reduced.getchannel("A").point(lambda value: 255 if value >= 128 else 0)
    # Palette cleanup is performed on the already generated image, without
    # drawing new shapes or changing the character's silhouette.
    limited = reduced.convert("RGB").quantize(colors=24, method=Image.Quantize.MEDIANCUT,
                                               dither=Image.Dither.NONE).convert("RGBA")
    limited.putalpha(reduced_alpha)
    canvas = Image.new("RGBA", (64, 64), (0, 0, 0, 0))
    canvas.alpha_composite(limited, ((64 - limited.width) // 2, 63 - limited.height))
    return canvas


def bench_preview(sprite: Image.Image, seat: str) -> Image.Image:
    bench = Image.open(BENCH).convert("RGBA")
    canvas = Image.new("RGBA", (160, 128), (215, 226, 186, 255))
    canvas.alpha_composite(bench, (48, 76))
    sprite_x = 32 if seat == "left" else 64
    canvas.alpha_composite(sprite, (sprite_x, 50))
    return canvas


def main() -> None:
    standing = Image.open(PLAYER).convert("RGBA")
    sprite_tiles = [("Current standing", standing)]
    bench_tiles = []
    for label in "ABC":
        source = CANDIDATES / f"candidate_{label}_source.png"
        sprite = build_sprite(source)
        sprite.save(CANDIDATES / f"candidate_{label}_64.png")
        sprite_tiles.append((f"Candidate {label}", sprite))
        sprite.resize((512, 512), Image.Resampling.NEAREST).save(
            CANDIDATES / f"candidate_{label}_8x.png")
        for seat in ("left", "right"):
            preview = bench_preview(sprite, seat).resize((640, 512), Image.Resampling.NEAREST)
            preview.save(CANDIDATES / f"candidate_{label}_bench_{seat}_4x.png")
            bench_tiles.append((f"Candidate {label} / {seat}", preview))

    comparison = Image.new("RGB", (2048, 560), (221, 229, 207))
    draw = ImageDraw.Draw(comparison)
    for index, (label, sprite) in enumerate(sprite_tiles):
        comparison.paste(sprite.resize((512, 512), Image.Resampling.NEAREST),
                         (index * 512, 48),
                         sprite.resize((512, 512), Image.Resampling.NEAREST))
        draw.text((index * 512 + 20, 15), label, fill=(30, 30, 30))
    comparison.save(ROOT / "candidate_comparison.png")

    bench_sheet = Image.new("RGB", (1280, 3 * 548), (221, 229, 207))
    draw = ImageDraw.Draw(bench_sheet)
    for index, (label, preview) in enumerate(bench_tiles):
        column, row = index % 2, index // 2
        bench_sheet.paste(preview.convert("RGB"), (column * 640, row * 548 + 36))
        draw.text((column * 640 + 20, row * 548 + 10), label, fill=(30, 30, 30))
    bench_sheet.save(ROOT / "bench_comparison.png")


if __name__ == "__main__":
    main()
