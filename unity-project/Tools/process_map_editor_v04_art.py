"""Prepare GPT Image sprite sources for the v0.4 Map Editor catalog.

This script performs technical post-processing only: alpha thresholding,
transparent crop, nearest-neighbor scaling, palette reduction and placement on
the final canvas. The source images contain all object artwork.
"""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

from PIL import Image


PROJECT = Path(__file__).resolve().parents[1]
SOURCE = PROJECT / "SourceGeneratedArt" / "map_editor_v04_objects"
OUTPUT = PROJECT / "Assets" / "Content" / "World"

# name: (canvas, content, maximum foreground colours)
ASSETS = {
    "cushion": ((32, 32), (28, 25), 10),
    "desk": ((64, 32), (58, 27), 12),
    "bench": ((64, 32), (58, 29), 12),
    "sign": ((32, 32), (30, 28), 10),
    "well": ((32, 32), (28, 25), 10),
}


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def process(name: str, canvas_size: tuple[int, int],
            content_size: tuple[int, int], colors: int) -> dict:
    source_name = "sign_one_tile_imagegen_source.png" if name == "sign" else f"{name}_imagegen_source.png"
    source_path = SOURCE / source_name
    output_path = OUTPUT / f"{name}.png"
    source = Image.open(source_path).convert("RGBA")
    alpha = source.getchannel("A").point(lambda value: 255 if value >= 128 else 0)
    bbox = alpha.getbbox()
    if bbox is None:
        raise ValueError(f"{name}: source has no opaque artwork")

    rgb = source.convert("RGB").crop(bbox).resize(content_size, Image.Resampling.NEAREST)
    clean_alpha = alpha.crop(bbox).resize(content_size, Image.Resampling.NEAREST)
    rgb = rgb.quantize(colors=colors, method=Image.Quantize.MEDIANCUT,
                       dither=Image.Dither.NONE).convert("RGB")
    sprite = rgb.convert("RGBA")
    sprite.putalpha(clean_alpha)

    canvas = Image.new("RGBA", canvas_size, (0, 0, 0, 0))
    left = (canvas_size[0] - content_size[0]) // 2
    top = (canvas_size[1] - content_size[1]) // 2
    canvas.alpha_composite(sprite, (left, top))
    OUTPUT.mkdir(parents=True, exist_ok=True)
    canvas.save(output_path, optimize=True)

    opaque = [pixel for pixel in canvas.get_flattened_data() if pixel[3] == 255]
    alpha_values = sorted({pixel[3] for pixel in canvas.get_flattened_data()})
    border = [*list(canvas.crop((0, 0, canvas.width, 1)).get_flattened_data()),
              *list(canvas.crop((0, canvas.height - 1, canvas.width, canvas.height)).get_flattened_data()),
              *list(canvas.crop((0, 0, 1, canvas.height)).get_flattened_data()),
              *list(canvas.crop((canvas.width - 1, 0, canvas.width, canvas.height)).get_flattened_data())]
    if alpha_values != [0, 255] or any(pixel[3] for pixel in border):
        raise ValueError(f"{name}: invalid alpha or occupied border")
    return {
        "source": str(source_path.relative_to(PROJECT)),
        "sourceSha256": sha256(source_path),
        "sourceSize": list(source.size),
        "sourceOpaqueCrop": list(bbox),
        "output": str(output_path.relative_to(PROJECT)),
        "outputSha256": sha256(output_path),
        "canvasSize": list(canvas_size),
        "contentSize": list(content_size),
        "foregroundColors": len({pixel[:3] for pixel in opaque}),
        "opaquePixels": len(opaque),
        "alphaValues": alpha_values,
    }


def main() -> None:
    manifest = [process(name, *parameters) for name, parameters in ASSETS.items()]
    manifest_path = SOURCE / "asset_manifest.json"
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n",
                             encoding="utf-8")
    print(json.dumps(manifest, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
