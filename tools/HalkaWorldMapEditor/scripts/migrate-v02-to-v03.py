"""Migrate the current authoring map without replacing user-authored placements.

Run from the repository root after making a separate backup of first_field.
The v0.3 core also performs this migration in memory for older v1 maps.
"""

import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]
AUTHORING = ROOT / "unity-project/Assets/Content/Maps/Authoring"


def write(path: Path, data: dict) -> None:
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def cell(x: int, y: int) -> dict:
    return {"x": x, "y": y}


def main() -> None:
    field_path = AUTHORING / "first_field.hwmap.json"
    field = json.loads(field_path.read_text(encoding="utf-8"))
    if field["formatVersion"] != 1:
        raise SystemExit("Expected the current v1 first_field map; no files changed.")
    before_surfaces = list(field["surfaces"])
    before_objects = list(field["objects"])
    old = field["markers"]
    markers = [
        {"id": "player_start", "cell": old["playerSpawn"]},
        {"id": "crow_spawn", "cell": old["crowSpawn"]},
        *({"id": f"road_{direction}", "cell": position}
          for direction, position in old["roadEnds"].items()),
    ]
    field = {
        "format": field["format"], "formatVersion": 2,
        "mapId": field["mapId"], "displayName": field["displayName"],
        "mapType": "outdoor", "baseSurfaceDefinitionId": "base_ground",
        "grassMode": "auto", "backdropColor": "#101014",
        "bounds": field["bounds"], "surfaces": before_surfaces,
        "objects": before_objects + [{
            "instanceId": "obj_house_main_v03_migration",
            "definitionId": "house_main", "rootCell": old["houseDoor"]
        }],
        "markers": markers,
    }
    if field["surfaces"] != before_surfaces or field["objects"][:-1] != before_objects:
        raise SystemExit("Migration changed existing placements; no files changed.")

    walls = []
    for y in range(-4, 5):
        for x in range(-6, 7):
            if (x == -6 or x == 6 or y == -4 or y >= 3) and (x, y) != (0, -4):
                walls.append({"definitionId": "house_wall", "cell": cell(x, y)})
    house = {
        "format": "halka-world-map", "formatVersion": 2,
        "mapId": "halka_house", "displayName": "ﾊﾙｶﾁｬﾝの家",
        "mapType": "interior", "baseSurfaceDefinitionId": "house_floor",
        "grassMode": "none", "backdropColor": "#101014",
        "bounds": {"minX": -6, "maxX": 6, "minY": -4, "maxY": 4},
        "surfaces": walls,
        "objects": [{"instanceId": "obj_bed_basic_v03_migration",
                     "definitionId": "bed_basic", "rootCell": cell(-5, 0)}],
        "markers": [{"id": "interior_entry", "cell": cell(0, -3)},
                    {"id": "interior_exit", "cell": cell(0, -4)}],
    }
    house_path = AUTHORING / "halka_house.hwmap.json"
    if house_path.exists():
        raise SystemExit("halka_house already exists; no files changed.")

    catalog_path = AUTHORING / "object_catalog.hwcatalog.json"
    catalog = json.loads(catalog_path.read_text(encoding="utf-8"))
    if catalog["formatVersion"] != 1:
        raise SystemExit("Expected v1 catalog; no files changed.")
    catalog["formatVersion"] = 2
    catalog["surfaces"] = [
        {"definitionId": "base_ground", "displayName": "通常地面",
         "category": "base", "previewSpritePath": "Assets/Content/World/pixel.png",
         "visualWidthPixels": 1, "visualHeightPixels": 1,
         "blocksMovement": False, "allowedMapTypes": ["outdoor"], "editorSelectable": False},
        {**catalog["surfaces"][0], "blocksMovement": False,
         "allowedMapTypes": ["outdoor"]},
        {"definitionId": "house_floor", "displayName": "木床",
         "category": "base", "previewSpritePath": "Assets/Content/World/house_floor.png",
         "visualWidthPixels": 32, "visualHeightPixels": 32,
         "blocksMovement": False, "allowedMapTypes": ["interior"], "editorSelectable": True},
        {"definitionId": "house_wall", "displayName": "壁",
         "category": "surface", "previewSpritePath": "Assets/Content/World/house_wall.png",
         "visualWidthPixels": 32, "visualHeightPixels": 32,
         "blocksMovement": True, "allowedMapTypes": ["interior"], "editorSelectable": True},
    ]
    for entry in catalog["objects"]:
        entry["blockedCellOffsets"] = [cell(0, 0)]
        entry["allowedMapTypes"] = ["outdoor"]
    house_offsets = [cell(x, y) for y in range(2) for x in range(-2, 3)
                     if (x, y) != (0, 0)]
    catalog["objects"] += [
        {"definitionId": "house_main", "displayName": "家", "category": "world-object",
         "previewSpritePath": "Assets/Content/World/house_exterior.png",
         "visualWidthPixels": 160, "visualHeightPixels": 128,
         "rootAnchor": "bottom-center", "footprint": {"width": 5, "height": 2},
         "blockedCellOffsets": house_offsets, "blocksMovement": True,
         "excludeGrass": True, "allowedMapTypes": ["outdoor"], "editorSelectable": True},
        {"definitionId": "bed_basic", "displayName": "ベッド", "category": "world-object",
         "previewSpritePath": "Assets/Content/World/house_bed.png",
         "visualWidthPixels": 64, "visualHeightPixels": 96,
         "rootAnchor": "bottom-left", "footprint": {"width": 2, "height": 3},
         "blockedCellOffsets": [cell(x, y) for y in range(3) for x in range(2)],
         "blocksMovement": True, "excludeGrass": True,
         "allowedMapTypes": ["interior"], "editorSelectable": True},
    ]
    write(field_path, field)
    write(house_path, house)
    write(catalog_path, catalog)
    print(f"field: {len(before_surfaces)} original surfaces, {len(before_objects)} original objects + house")
    print(f"house: {len(walls)} walls, 1 bed")


if __name__ == "__main__":
    main()
