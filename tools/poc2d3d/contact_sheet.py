"""Builds labelled contact sheets from Phase 1 capture PNGs (nearest-neighbour resize).

  python tools/poc2d3d/contact_sheet.py OUT.png --cols 2 --width 512 IMG[=label] ...
"""
import argparse

from PIL import Image, ImageDraw, ImageFont


def font(size):
    for path in ("C:/Windows/Fonts/meiryo.ttc", "C:/Windows/Fonts/YuGothM.ttc", "C:/Windows/Fonts/arial.ttf"):
        try:
            return ImageFont.truetype(path, size)
        except OSError:
            continue
    return ImageFont.load_default()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("out")
    parser.add_argument("items", nargs="+")
    parser.add_argument("--cols", type=int, default=2)
    parser.add_argument("--width", type=int, default=512)
    args = parser.parse_args()
    cells = []
    for item in args.items:
        path, _, label = item.partition("=")
        image = Image.open(path).convert("RGB")
        height = round(image.height * args.width / image.width)
        resample = Image.NEAREST if args.width >= image.width else Image.LANCZOS
        cells.append((image.resize((args.width, height), resample), label))
    label_h = 30 if any(label for _, label in cells) else 0
    cell_h = max(image.height for image, _ in cells) + label_h
    rows = (len(cells) + args.cols - 1) // args.cols
    sheet = Image.new("RGB", (args.cols * args.width, rows * cell_h), (24, 24, 28))
    draw = ImageDraw.Draw(sheet)
    text_font = font(18)
    for index, (image, label) in enumerate(cells):
        x = (index % args.cols) * args.width
        y = (index // args.cols) * cell_h
        sheet.paste(image, (x, y + label_h))
        if label:
            draw.text((x + 8, y + 5), label, fill=(240, 240, 240), font=text_font)
    sheet.save(args.out)


if __name__ == "__main__":
    main()
