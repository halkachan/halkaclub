"""Rasterize a limited UI glyph set from the official private ZIP.

The ZIP/TTF stays outside the repository. Only the rendered PNG and metrics text
are written to Assets/Resources; neither output contains a reusable font file.
"""

import argparse
import io
import math
import zipfile
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont
from fontTools.ttLib import TTFont


HIRAGANA = "".join(chr(code) for code in range(0x3041, 0x3097))
KATAKANA = "".join(chr(code) for code in range(0x30A1, 0x30FB))
ASCII = "".join(chr(code) for code in range(0x20, 0x7F))
EXTRAS = "。、！？・「」『』ー～：；（）【】…漢字東西南北石花木机家井戸草土床壁羽歩時数間今入出上"
OPTIONAL_CHARACTERS = "".join(dict.fromkeys(HIRAGANA + KATAKANA + ASCII + EXTRAS))
REQUIRED = "メニューAUTOONOFFせいかつきろくあるいたかずプレイじかんもどるベッド。つくえ。かんばん。いし。はな。き。カァ。ver2.30123456789:"
CELL = 64
COLUMNS = 32
SIZE = 50


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("official_zip", type=Path)
    parser.add_argument("resources", type=Path)
    args = parser.parse_args()
    with zipfile.ZipFile(args.official_zip) as archive:
        name = next(name for name in archive.namelist() if name.lower().endswith(".ttf"))
        source = archive.read(name)
    cmap = TTFont(io.BytesIO(source)).getBestCmap()
    missing = [character for character in REQUIRED if ord(character) not in cmap]
    if missing:
        raise SystemExit("Official font lacks required glyphs: " + "".join(missing))
    characters = "".join(character for character in OPTIONAL_CHARACTERS if ord(character) in cmap)
    font = ImageFont.truetype(io.BytesIO(source), SIZE)
    rows = math.ceil(len(characters) / COLUMNS)
    # White RGB even under transparent texels avoids dark fringes if a
    # different renderer samples outside opaque texels.
    atlas = Image.new("RGBA", (COLUMNS * CELL, rows * CELL), (255, 255, 255, 0))
    drawing = ImageDraw.Draw(atlas)
    advances = []
    for index, character in enumerate(characters):
        x = index % COLUMNS * CELL
        y = index // COLUMNS * CELL
        # The two-pixel same-colour stroke retains the approved readable width.
        # Binary alpha and Point sampling below remove the muddy gray fringe.
        drawing.text((x + 4, y + 53), character, font=font,
                     fill=(255, 255, 255, 255), stroke_width=2,
                     stroke_fill=(255, 255, 255, 255), anchor="ls")
        advances.append(round(min(60.0, max(10.0, font.getlength(character))), 3))
    # The UI is drawn on the same nearest-scaled WebGL canvas as the world.
    # Intermediate atlas alpha produces the gray specks visible around strokes.
    atlas.putalpha(atlas.getchannel("A").point(
        [255 if value >= 128 else 0 for value in range(256)]))
    args.resources.mkdir(parents=True, exist_ok=True)
    atlas.save(args.resources / "asobi_ui_atlas.png")
    (args.resources / "asobi_ui_map.txt").write_text(
        characters + "\n" + ",".join(str(value) for value in advances) + "\n" +
        str(rows) + "\n", encoding="utf-8")
    print(f"{len(characters)} glyphs, {atlas.width}x{atlas.height}, no raw font output")


if __name__ == "__main__":
    main()
