"""Prepare GPT Image source files for the fixed HALKA WORLD sprite canvases.

This only crops transparent margins, scales with nearest-neighbor sampling,
reduces palette size, cleans alpha, and aligns animation frames. It does not draw art.
"""

from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
SOURCES = ROOT / "SourceGeneratedArt" / "v20"
OUTPUT = ROOT / "Assets" / "Content" / "World"
RESAMPLE = Image.Resampling.NEAREST


def open_rgba(name):
    return Image.open(SOURCES / f"{name}_source.png").convert("RGBA")


def solid_bbox(image):
    return image.getchannel("A").point(lambda a: 255 if a >= 128 else 0).getbbox()


def crop_to_canvas(image, size, *, anchor_bottom=False):
    """Fit the nontransparent image within the desired canvas without distortion."""
    box = solid_bbox(image)
    if not box:
        raise ValueError("Generated source has no opaque pixels")
    artwork = image.crop(box)
    target_w, target_h = size
    factor = min(target_w / artwork.width, target_h / artwork.height)
    scaled = artwork.resize(
        (round(artwork.width * factor), round(artwork.height * factor)), RESAMPLE
    )
    result = Image.new("RGBA", size, (0, 0, 0, 0))
    x = (target_w - scaled.width) // 2
    y = target_h - scaled.height if anchor_bottom else (target_h - scaled.height) // 2
    result.paste(scaled, (x, y))
    return result


def clean_alpha(image):
    """Remove soft transparent fringe without modifying opaque RGB artwork."""
    alpha = image.getchannel("A").point(lambda a: 255 if a >= 128 else 0)
    image.putalpha(alpha)
    return image


def limit_palette(image):
    """Keep generated RGB artwork, but limit the final game sprite to 64 colors."""
    reduced = image.quantize(
        colors=64, method=Image.Quantize.FASTOCTREE, dither=Image.Dither.NONE
    ).convert("RGBA")
    return clean_alpha(reduced)


def save(name, image):
    image.save(OUTPUT / f"{name}.png", optimize=True)
    print(f"{name}.png: {image.width}x{image.height}")


save(
    "house_exterior",
    limit_palette(crop_to_canvas(open_rgba("house_exterior"), (160, 128), anchor_bottom=True)),
)
save(
    "house_bed",
    limit_palette(crop_to_canvas(open_rgba("house_bed"), (64, 64))),
)
for name in ("house_floor", "house_wall", "house_door_inside"):
    image = open_rgba(name)
    if name == "house_wall":
        # Only the generated plaster center belongs in a repeating wall tile.
        # Its surrounding timber frame is intended for a whole wall, not 18 copies.
        cx, cy = image.width // 2, image.height // 2
        image = image.crop((cx - 260, cy - 260, cx + 260, cy + 260))
    image = image.resize((32, 32), RESAMPLE)
    save(name, limit_palette(image))

# The same source rectangle and canvas anchor apply to every pose to prevent
# sprite scale and foot position from jumping during the hop animation.
crow_names = ("crow_idle", "crow_hop_1", "crow_hop_2")
crow_sources = [open_rgba(name) for name in crow_names]
boxes = [solid_bbox(image) for image in crow_sources]
crow_box = (
    min(box[0] for box in boxes),
    min(box[1] for box in boxes),
    max(box[2] for box in boxes),
    max(box[3] for box in boxes),
)
for name, source in zip(crow_names, crow_sources):
    frame = source.crop(crow_box).resize((28, 26), RESAMPLE)
    canvas = Image.new("RGBA", (32, 32), (0, 0, 0, 0))
    canvas.paste(frame, (2, 3))
    alpha = canvas.getchannel("A").point(lambda a: 255 if a >= 128 else 0)
    canvas.putalpha(alpha)
    save(name, canvas)
