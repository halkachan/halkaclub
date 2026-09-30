"""Convert the supplied original GIFs to Unity Sprite PNGs without redrawing them."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

from PIL import Image, ImageSequence


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "SourceGifs"
DESTINATION = ROOT / "Assets" / "Content" / "Character"
WORLD_DESTINATION = ROOT / "Assets" / "Content" / "World"
NAMES = (
    "front_idle", "back_idle", "left_idle", "right_idle",
    "walk_front", "walk_back", "walk_left", "walk_right",
    "turn", "front_jump",
)
WORLD_NAMES = ("grass_rustle",)
GRASS_FRONT_PIXELS = 10


def make_grass_front(source: Path, destination: Path) -> None:
    with Image.open(source) as image:
        original = image.convert("RGBA")
    if original.size != (32, 32):
        raise ValueError(f"Grass front source must be 32x32: {source}")
    width, height = original.size
    front = Image.new("RGBA", original.size, (0, 0, 0, 0))
    bottom = (0, height - GRASS_FRONT_PIXELS, width, height)
    front.paste(original.crop(bottom), bottom[:2])
    destination.parent.mkdir(parents=True, exist_ok=True)
    front.save(destination)


def main() -> None:
    manifest = {}
    for name in (*NAMES, *WORLD_NAMES):
        source = SOURCE / f"{name}.gif"
        with Image.open(source) as gif:
            folder = (WORLD_DESTINATION if name in WORLD_NAMES else DESTINATION) / name
            folder.mkdir(parents=True, exist_ok=True)
            durations = []
            for index, frame in enumerate(ImageSequence.Iterator(gif)):
                frame.convert("RGBA").save(folder / f"{index:02d}.png")
                durations.append(frame.info.get("duration", 100))
            manifest[name] = {
                "source_sha256": hashlib.sha256(source.read_bytes()).hexdigest(),
                "size": list(gif.size),
                "frames": gif.n_frames,
                "duration_ms": durations,
            }
    make_grass_front(WORLD_DESTINATION / "grass.png", WORLD_DESTINATION / "grass_front.png")
    for index in range(manifest["grass_rustle"]["frames"]):
        make_grass_front(
            WORLD_DESTINATION / "grass_rustle" / f"{index:02d}.png",
            WORLD_DESTINATION / "grass_rustle_front" / f"{index:02d}.png",
        )
    manifest["grass_rustle"]["front_pixels"] = GRASS_FRONT_PIXELS
    (SOURCE / "manifest.json").write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )


if __name__ == "__main__":
    main()
