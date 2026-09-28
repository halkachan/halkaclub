"""Convert the supplied original GIFs to Unity Sprite PNGs without redrawing them."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

from PIL import Image, ImageSequence


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "SourceGifs"
DESTINATION = ROOT / "Assets" / "Content" / "Character"
NAMES = ("front_idle", "back_idle", "left_idle", "right_idle", "turn", "front_jump")


def main() -> None:
    manifest = {}
    for name in NAMES:
        source = SOURCE / f"{name}.gif"
        with Image.open(source) as gif:
            folder = DESTINATION / name
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
    (SOURCE / "manifest.json").write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )


if __name__ == "__main__":
    main()
