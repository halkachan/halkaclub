"""Technical preparation of user-approved house/crow art and GPT Image edits.

No artwork is drawn here. Operations are alpha crop, aspect-preserving nearest
resize, fixed-canvas placement, alpha cleanup, and horizontal reflection.
"""

from pathlib import Path
from PIL import Image, ImageOps


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "SourceGeneratedArt" / "v20"
WORLD = ROOT / "Assets" / "Content" / "World"
NEAREST = Image.Resampling.NEAREST


def rgba(path: Path) -> Image.Image:
    return Image.open(path).convert("RGBA")


def opaque_bounds(image: Image.Image):
    return image.getchannel("A").point(lambda value: 255 if value >= 128 else 0).getbbox()


def clean_alpha(image: Image.Image) -> Image.Image:
    image.putalpha(image.getchannel("A").point(lambda value: 255 if value >= 128 else 0))
    return image


def fit(source: Image.Image, size: tuple[int, int], *, bottom_margin: int = 0):
    box = opaque_bounds(source)
    if box is None:
        raise ValueError("Source has no visible artwork")
    cropped = source.crop(box)
    scale = min(size[0] / cropped.width, (size[1] - bottom_margin) / cropped.height)
    width = round(cropped.width * scale)
    height = round(cropped.height * scale)
    resized = cropped.resize((width, height), NEAREST)
    canvas = Image.new("RGBA", size, (0, 0, 0, 0))
    canvas.paste(resized, ((size[0] - width) // 2, size[1] - bottom_margin - height))
    return clean_alpha(canvas)


def save(name: str, image: Image.Image) -> None:
    image.save(WORLD / name, optimize=True)
    print(f"{name}: {image.size}")


# The user's exact approved house is the only source of exterior design.
save("house_exterior.png", fit(rgba(SOURCE / "house_exterior_user_source.png"), (160, 128)))
save("house_bed.png", fit(rgba(SOURCE / "house_bed_simple_source.png"), (64, 96)))

# Use one crop for both frames of a direction. This keeps body/feet from
# jumping as the walking sprite changes. Frame zero is the idle image.
crow = SOURCE / "crow"
directions = {
    "right": (
        rgba(SOURCE / "crow_user_reference.png"),
        rgba(crow / "walk_right_1_source.png"),
    ),
    "down": (
        rgba(crow / "idle_down_source.png"),
        rgba(crow / "walk_down_1_source.png"),
    ),
    "up": (
        rgba(crow / "idle_up_source.png"),
        rgba(crow / "walk_up_1_source.png"),
    ),
}


def make_pair(first: Image.Image, second: Image.Image):
    boxes = [opaque_bounds(first), opaque_bounds(second)]
    if any(box is None for box in boxes):
        raise ValueError("Crow frame has no visible artwork")
    union = (
        min(box[0] for box in boxes),
        min(box[1] for box in boxes),
        max(box[2] for box in boxes),
        max(box[3] for box in boxes),
    )
    width = union[2] - union[0]
    height = union[3] - union[1]
    scale = min(30 / width, 26 / height)
    target = (round(width * scale), round(height * scale))
    output = []
    for source in (first, second):
        part = source.crop(union).resize(target, NEAREST)
        canvas = Image.new("RGBA", (32, 32), (0, 0, 0, 0))
        canvas.paste(part, ((32 - target[0]) // 2, 31 - target[1]))
        output.append(clean_alpha(canvas))
    return output


for direction, frames in directions.items():
    idle, stride = make_pair(*frames)
    save(f"crow_idle_{direction}.png", idle)
    save(f"crow_walk_{direction}_0.png", idle)
    save(f"crow_walk_{direction}_1.png", stride)
    if direction == "right":
        save("crow_idle_left.png", ImageOps.mirror(idle))
        save("crow_walk_left_0.png", ImageOps.mirror(idle))
        save("crow_walk_left_1.png", ImageOps.mirror(stride))
