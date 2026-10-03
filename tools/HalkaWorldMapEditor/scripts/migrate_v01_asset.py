"""One-time v0.1 YAML -> v0.2 authoring JSON migration; never used at runtime."""
from __future__ import annotations

import json
import re
import uuid
from pathlib import Path

PROJECT = Path(__file__).resolve().parents[3] / "unity-project"
MAPS = PROJECT / "Assets/Content/Maps"
AUTHORING = MAPS / "Authoring"
TEXT = (MAPS / "first_field.asset").read_text(encoding="utf-8")
NAMESPACE = uuid.UUID("a6d05230-421f-4221-884f-79fb22ee269c")


def cell(text: str) -> dict[str, int]:
    match = re.search(r"\{x: (-?\d+), y: (-?\d+)\}", text)
    if not match:
        raise ValueError(f"No cell in {text}")
    return {"x": int(match[1]), "y": int(match[2])}


def section(name: str, next_name: str) -> str:
    return TEXT.split(f"  {name}:\n", 1)[1].split(f"  {next_name}:", 1)[0]


def guid_for(name: str) -> str:
    data = (MAPS / f"{name}.asset.meta").read_text(encoding="utf-8")
    return re.search(r"guid: ([0-9a-f]+)", data)[1]


guids = {guid_for(name): name for name in ("stone_basic", "flower_basic", "tree_basic")}
surfaces = [
    {"definitionId": "dirt", "cell": cell(match)}
    for match in re.findall(r"Cell: \{x: -?\d+, y: -?\d+\}", section("surfaces", "objects"))
]
objects = []
for match in re.finditer(
    r"RootCell: (\{x: -?\d+, y: -?\d+\})\s+Definition: \{fileID: \d+, guid: ([0-9a-f]+)",
    section("objects", "playerSpawnCell"),
):
    root = cell(match[1])
    kind = guids[match[2]]
    stable = "obj_" + uuid.uuid5(NAMESPACE, f"{kind}:{root['x']}:{root['y']}").hex
    objects.append({"instanceId": stable, "definitionId": kind, "rootCell": root})

map_data = {
    "format": "halka-world-map",
    "formatVersion": 1,
    "mapId": "first_field",
    "displayName": "First Field",
    "bounds": {"minX": -10, "maxX": 10, "minY": -6, "maxY": 6},
    "surfaces": sorted(surfaces, key=lambda p: (p["cell"]["y"], p["cell"]["x"], p["definitionId"])),
    "objects": sorted(objects, key=lambda p: (p["definitionId"], p["rootCell"]["y"], p["rootCell"]["x"], p["instanceId"])),
    "markers": {
        "playerSpawn": cell(re.search(r"playerSpawnCell: (\{.*\})", TEXT)[1]),
        "crowSpawn": cell(re.search(r"StableId: crow\s+Cell: (\{.*\})", TEXT)[1]),
        "houseDoor": cell(re.search(r"houseDoorCell: (\{.*\})", TEXT)[1]),
        "outsideEntry": cell(re.search(r"outsideEntryCell: (\{.*\})", TEXT)[1]),
        "houseFootprint": {"width": 5, "height": 2},
        "roadEnds": {
            direction: cell(re.search(rf"{direction}RoadEnd: (\{{.*\}})", TEXT)[1])
            for direction in ("north", "east", "south", "west")
        },
    },
}
assert (len(surfaces), len(objects)) == (52, 11)

catalog = {
    "format": "halka-world-catalog",
    "formatVersion": 1,
    "surfaces": [{
        "definitionId": "dirt", "displayName": "土", "category": "surface",
        "previewSpritePath": "Assets/Content/World/dirt.png",
        "visualWidthPixels": 32, "visualHeightPixels": 32,
        "editorSelectable": True,
    }],
    "objects": [
        {
            "definitionId": name, "displayName": display, "category": "world-object",
            "previewSpritePath": f"Assets/Content/World/{sprite}.png",
            "visualWidthPixels": width, "visualHeightPixels": height,
            "rootAnchor": "bottom-center", "footprint": {"width": 1, "height": 1},
            "blocksMovement": True, "excludeGrass": True, "editorSelectable": True,
        }
        for name, display, sprite, width, height in (
            ("stone_basic", "石", "stone", 32, 32),
            ("flower_basic", "花", "flower", 32, 32),
            ("tree_basic", "木", "tree", 96, 128),
        )
    ],
    "visuals": {
        "grassSpritePath": "Assets/Content/World/grass.png",
        "houseSpritePath": "Assets/Content/World/house_exterior.png",
        "crowSpritePath": "Assets/Content/World/crow_idle_right.png",
        "playerSpritePath": "Assets/Content/Character/front_idle/00.png",
    },
}
AUTHORING.mkdir(parents=True, exist_ok=True)
for name, payload in (("first_field.hwmap.json", map_data),
                      ("object_catalog.hwcatalog.json", catalog)):
    (AUTHORING / name).write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(f"Migrated {len(surfaces)} dirt and {len(objects)} objects to {AUTHORING}")
