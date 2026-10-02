"""Deterministic provisional pixel art for HALKA WORLD 2.0.

Run with a Python containing Pillow. No source images or resampling are used.
"""
from pathlib import Path
from PIL import Image, ImageDraw

DEST = Path(__file__).resolve().parents[1] / "Assets" / "Content" / "World"
DEST.mkdir(parents=True, exist_ok=True)

INK = (47, 48, 52, 255)
ROOF_DARK = (104, 64, 58, 255)
ROOF = (169, 89, 72, 255)
ROOF_LIT = (204, 121, 87, 255)
WALL = (228, 211, 169, 255)
WALL_SHADE = (189, 172, 137, 255)
WOOD = (114, 83, 66, 255)
WOOD_LIT = (156, 111, 76, 255)


def save(name, image):
    image.save(DEST / name, format="PNG", optimize=False)


# 5 x 4 cells; door occupies the bottom centre cell.
im = Image.new("RGBA", (160, 128))
d = ImageDraw.Draw(im)
d.rectangle((8, 55, 151, 125), fill=INK)
d.rectangle((11, 58, 148, 122), fill=WALL)
d.rectangle((11, 109, 148, 122), fill=WALL_SHADE)
for y in range(0, 57, 8):
    inset = max(2, 34 - y // 2)
    d.rectangle((inset, y + 1, 159 - inset, y + 7), fill=ROOF_DARK)
    d.rectangle((inset + 3, y + 1, 156 - inset, y + 4), fill=ROOF)
    if y >= 16:
        d.rectangle((inset + 10, y + 2, 149 - inset, y + 2), fill=ROOF_LIT)
d.rectangle((3, 54, 156, 62), fill=INK)
d.rectangle((6, 55, 153, 58), fill=ROOF_LIT)
for x in (22, 112):
    d.rectangle((x - 2, 78, x + 27, 106), fill=INK)
    d.rectangle((x, 80, x + 25, 103), fill=WOOD)
    d.rectangle((x + 4, 84, x + 21, 99), fill=(126, 169, 175, 255))
    d.rectangle((x + 12, 84, x + 13, 99), fill=INK)
    d.rectangle((x + 4, 91, x + 21, 92), fill=INK)
d.rectangle((63, 80, 96, 127), fill=INK)
d.rectangle((66, 83, 93, 127), fill=WOOD)
d.rectangle((70, 87, 88, 126), fill=WOOD_LIT)
d.rectangle((87, 106, 90, 109), fill=(232, 197, 94, 255))
d.rectangle((59, 76, 100, 82), fill=INK)
d.rectangle((62, 78, 97, 80), fill=WALL_SHADE)
save("house_exterior.png", im)

# Repeated 32 px interior floor and wall tiles.
im = Image.new("RGBA", (32, 32), (192, 164, 116, 255))
d = ImageDraw.Draw(im)
for y in (0, 15, 31):
    d.line((0, y, 31, y), fill=(139, 111, 80, 255))
d.line((15, 1, 15, 14), fill=(151, 123, 86, 255))
d.line((7, 16, 7, 30), fill=(151, 123, 86, 255))
d.rectangle((3, 5, 4, 6), fill=(213, 185, 135, 255))
d.rectangle((23, 22, 24, 23), fill=(213, 185, 135, 255))
save("house_floor.png", im)

im = Image.new("RGBA", (32, 32), WALL)
d = ImageDraw.Draw(im)
d.rectangle((0, 0, 31, 4), fill=WOOD)
d.rectangle((0, 5, 31, 7), fill=WOOD_LIT)
d.line((0, 22, 31, 22), fill=WALL_SHADE)
d.rectangle((0, 28, 31, 31), fill=WOOD)
d.rectangle((3, 15, 5, 17), fill=WALL_SHADE)
d.rectangle((24, 10, 26, 12), fill=WALL_SHADE)
save("house_wall.png", im)

im = Image.new("RGBA", (32, 32), (192, 164, 116, 255))
d = ImageDraw.Draw(im)
d.rectangle((2, 3, 29, 30), fill=INK)
d.rectangle((5, 5, 26, 29), fill=WOOD)
d.rectangle((8, 7, 23, 26), fill=WOOD_LIT)
d.rectangle((21, 16, 23, 18), fill=(232, 197, 94, 255))
d.rectangle((0, 29, 31, 31), fill=WALL_SHADE)
save("house_door_inside.png", im)

# 2 x 2 cell bed, viewed from above.
im = Image.new("RGBA", (64, 64))
d = ImageDraw.Draw(im)
d.rectangle((2, 14, 61, 52), fill=INK)
d.rectangle((5, 17, 58, 49), fill=WOOD)
d.rectangle((8, 19, 55, 47), fill=(217, 195, 148, 255))
d.rectangle((9, 21, 22, 45), fill=(245, 234, 202, 255))
d.rectangle((23, 21, 54, 45), fill=(112, 139, 135, 255))
d.rectangle((25, 23, 52, 43), fill=(151, 176, 162, 255))
d.line((23, 21, 23, 45), fill=INK)
d.rectangle((7, 50, 12, 57), fill=INK)
d.rectangle((51, 50, 56, 57), fill=INK)
save("house_bed.png", im)


def crow(frame):
    im = Image.new("RGBA", (32, 32))
    d = ImageDraw.Draw(im)
    d.rectangle((10, 26, 12, 30), fill=INK)
    d.rectangle((19, 26, 21, 30), fill=INK)
    d.rectangle((4, 19, 25, 26), fill=INK)
    d.rectangle((8, 11, 23, 24), fill=(40, 45, 62, 255))
    d.rectangle((11, 7, 23, 17), fill=(37, 43, 58, 255))
    d.rectangle((21, 11, 27, 14), fill=(194, 157, 90, 255))
    d.rectangle((18, 10, 19, 11), fill=(239, 233, 203, 255))
    if frame == 1:
        d.polygon([(7, 20), (1, 13), (1, 4), (13, 15), (18, 21)], fill=INK)
        d.rectangle((3, 10, 6, 14), fill=(71, 80, 103, 255))
    elif frame == 2:
        d.polygon([(8, 20), (3, 8), (8, 2), (16, 16), (17, 22)], fill=INK)
        d.rectangle((8, 8, 11, 12), fill=(71, 80, 103, 255))
    else:
        d.polygon([(9, 18), (3, 21), (1, 25), (14, 24)], fill=INK)
        d.rectangle((8, 18, 13, 22), fill=(71, 80, 103, 255))
    return im


save("crow_idle.png", crow(0))
save("crow_hop_1.png", crow(1))
save("crow_hop_2.png", crow(2))
