"""Link approved v0.4 object art to the stable Map Editor definitions.

This edits catalog metadata and Unity import sidecars; it never edits maps.
"""

from __future__ import annotations

import json
from pathlib import Path


PROJECT = Path(__file__).resolve().parents[1]
CATALOG = PROJECT / "Assets/Content/Maps/Authoring/object_catalog.hwcatalog.json"
WORLD = PROJECT / "Assets/Content/World"
STONE_META = (WORLD / "stone.png.meta").read_text(encoding="utf-8")

SPRITES = {
    "cushion": (32, 32, "c1e4da56bc7f4e939d2b39ac787b4301"),
    "desk": (64, 32, "b5f495831aac4e64a743c97f9530c1e2"),
    "bench": (64, 32, "a30335fd0b1d4eb2b117457f389316e5"),
    "sign": (32, 32, "e6ed6756d9f54e72a8cd2082c99eb7b0"),
    "well": (32, 32, "a1794f83bd9346538fb369cf141f93c3"),
}


def main() -> None:
    source = json.loads(CATALOG.read_text(encoding="utf-8"))
    definitions = {entry["definitionId"]: entry for entry in source["objects"]}
    if "sign_basic" not in definitions:
        old = next(entry for entry in source["objects"] if entry["definitionId"] == "sign_north")
        old["definitionId"] = "sign_basic"
        old["displayName"] = "看板"
        old["actionPoints"][0]["interactionText"] = "かんばん。"
    source["objects"] = [entry for entry in source["objects"] if entry["definitionId"] not in
        ("sign_north", "sign_east", "sign_south", "sign_west")]
    definitions = {entry["definitionId"]: entry for entry in source["objects"]}
    for entry in source["surfaces"]:
        entry["allowedMapTypes"] = []
        if entry["definitionId"] == "base_ground":
            entry["displayName"] = "草"
            entry["editorSelectable"] = True
            entry["growsGrass"] = True
    for entry in source["objects"]:
        entry["allowedMapTypes"] = []
    assignment = {
        "cushion_basic": "cushion",
        "desk_basic": "desk",
        "bench_basic": "bench",
        "sign_basic": "sign",
        "well_basic": "well",
    }
    for definition_id, sprite_name in assignment.items():
        entry = definitions[definition_id]
        width, height, _ = SPRITES[sprite_name]
        entry["previewSpritePath"] = f"Assets/Content/World/{sprite_name}.png"
        entry["visualWidthPixels"] = width
        entry["visualHeightPixels"] = height
        entry["footprint"] = {"width": width // 32, "height": 1}
        entry["rootAnchor"] = "bottom-left" if width == 64 else "bottom-center"
        if entry["blocksMovement"]:
            entry["blockedCellOffsets"] = [{"x": x, "y": 0} for x in range(width // 32)]
        else:
            entry["blockedCellOffsets"] = []
    bench_points = definitions["bench_basic"]["actionPoints"]
    bench_points[0]["playerCellOffset"] = {"x": 0, "y": -1}
    bench_points[1]["playerCellOffset"] = {"x": 1, "y": -1}
    for definition_id in ("stone_basic", "flower_basic", "tree_basic"):
        definitions[definition_id]["category"] = "nature"
    for definition_id in ("house_main", "bench_basic", "sign_basic", "well_basic"):
        definitions[definition_id]["category"] = "fixture"
    definitions["bed_basic"]["category"] = "furniture"

    CATALOG.write_text(json.dumps(source, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    old_guid = "58c7027453f0af846815c6302b8b75d7"
    for name, (_, _, guid) in SPRITES.items():
        target = WORLD / f"{name}.png.meta"
        expected = STONE_META.replace(old_guid, guid, 1)
        if target.exists():
            if target.read_text(encoding="utf-8") != expected:
                raise FileExistsError(f"Refusing to replace an existing Unity meta file: {target}")
        else:
            target.write_text(expected, encoding="utf-8")
    print("Linked 5 new object types to 5 sprites; unified sign and palette. Maps unchanged.")


if __name__ == "__main__":
    main()
