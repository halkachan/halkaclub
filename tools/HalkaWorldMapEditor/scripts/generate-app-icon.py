"""Generate the standalone editor's small, simple map icon."""

from pathlib import Path

from PIL import Image, ImageDraw


def main() -> None:
    assets = Path(__file__).resolve().parents[1] / "HalkaWorldMapEditor" / "Assets"
    assets.mkdir(parents=True, exist_ok=True)

    image = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    draw.rounded_rectangle((8, 8, 247, 247), radius=43, fill="#18252A", outline="#95B9A8", width=8)

    tile_size = 54
    gap = 7
    start = 40
    grass = ("#387554", "#44815A", "#32704F")
    dirt_cells = {(0, 1), (1, 1), (1, 2), (2, 2)}
    for row in range(3):
        for col in range(3):
            x = start + col * (tile_size + gap)
            y = start + row * (tile_size + gap)
            color = "#E5B54A" if (col, row) in dirt_cells else grass[(row + col) % len(grass)]
            draw.rectangle((x, y, x + tile_size - 1, y + tile_size - 1), fill=color)

    draw.polygon([(157, 137), (157, 205), (175, 190), (188, 220), (202, 213), (189, 183), (211, 181)],
                 fill="#F5F5E9")
    draw.line([(157, 137), (157, 205), (175, 190), (188, 220), (202, 213),
               (189, 183), (211, 181), (157, 137)], fill="#18252A", width=5, joint="curve")

    image.save(assets / "map-editor-preview.png")
    image.save(assets / "map-editor.ico", sizes=[(16, 16), (24, 24), (32, 32), (48, 48),
                                                  (64, 64), (128, 128), (256, 256)])


if __name__ == "__main__":
    main()
