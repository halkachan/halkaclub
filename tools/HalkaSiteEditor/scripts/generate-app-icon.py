"""Generate the site editor's icon: a ruled notebook page with a yellow tape and a pencil."""

from pathlib import Path

from PIL import Image, ImageDraw

PAPER = "#FFFEF6"
INK = "#16150F"
YELLOW = "#FFDC18"
RULE = "#C8D5E0"
RED_LINE = "#E8A0A0"


def main() -> None:
    assets = Path(__file__).resolve().parents[1] / "HalkaSiteEditor" / "Assets"
    assets.mkdir(parents=True, exist_ok=True)

    image = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)

    # ノートの紙
    draw.rounded_rectangle((16, 10, 239, 245), radius=16, fill=PAPER, outline=INK, width=9)

    # 横罫と左の赤い縦線
    for y in range(104, 232, 30):
        draw.line((46, y, 214, y), fill=RULE, width=5)
    draw.line((44, 24, 44, 232), fill=RED_LINE, width=4)

    # 黄色いテープ
    draw.polygon([(6, 62), (250, 48), (250, 100), (6, 114)], fill=YELLOW, outline=INK)
    draw.line([(6, 62), (250, 48)], fill=INK, width=7)
    draw.line([(6, 114), (250, 100)], fill=INK, width=7)

    # 鉛筆
    draw.polygon([(196, 112), (226, 142), (134, 234), (96, 244), (106, 206)], fill=PAPER, outline=INK)
    draw.line([(196, 112), (226, 142), (134, 234), (96, 244), (106, 206), (196, 112)],
              fill=INK, width=8, joint="curve")
    draw.polygon([(106, 206), (134, 234), (96, 244)], fill=INK)
    draw.line([(180, 128), (210, 158)], fill=INK, width=7)

    image.save(assets / "site-editor-preview.png")
    image.save(assets / "site-editor.ico",
               sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])


if __name__ == "__main__":
    main()
