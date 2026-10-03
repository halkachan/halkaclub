"""Recolor the existing GPT Image wall tile without changing its pixel design."""

from pathlib import Path
from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "SourceGeneratedArt" / "v20" / "house_wall_light_tile.png"
TARGET = ROOT / "Assets" / "Content" / "World" / "house_wall.png"

# Only a palette swap. The original outlines, texture and pixel positions stay intact.
PALETTE = {
    (253, 244, 224, 255): (38, 38, 45, 255),
    (250, 226, 194, 255): (33, 33, 40, 255),
    (253, 243, 221, 255): (45, 45, 53, 255),
    (250, 224, 190, 255): (29, 29, 35, 255),
    (250, 222, 188, 255): (29, 29, 35, 255),
}


def main() -> None:
    with Image.open(SOURCE) as image:
        image = image.convert("RGBA")
        if image.size != (32, 32):
            raise ValueError(f"Wall tile must remain 32x32: {image.size}")
        colors = set(image.get_flattened_data())
        if colors != set(PALETTE):
            raise ValueError(f"Unexpected source wall palette: {colors - set(PALETTE)}")
        image.putdata([PALETTE[color] for color in image.get_flattened_data()])
        image.save(TARGET, optimize=True)


if __name__ == "__main__":
    main()
