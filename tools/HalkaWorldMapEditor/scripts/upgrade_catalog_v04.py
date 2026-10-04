"""One-time, deterministic v0.3 catalog migration. Map placement JSON is untouched."""
import json
from pathlib import Path

catalog_path = Path(__file__).resolve().parents[3] / "unity-project/Assets/Content/Maps/Authoring/object_catalog.hwcatalog.json"
catalog = json.loads(catalog_path.read_text(encoding="utf-8"))
if catalog["formatVersion"] != 2:
    raise SystemExit("Expected catalog v2; refusing to overwrite another version")

def point(id, x, y, facing, kind, pose=None, text=None):
    return {"id": id, "playerCellOffset": {"x": x, "y": y},
            "playerFacing": facing, "actionType": kind,
            "poseKey": pose, "interactionText": text}

existing = {
    "stone_basic": [point("examine_front", 0, -1, "up", "examine", text="いし。")],
    "flower_basic": [point("examine_front", 0, -1, "up", "examine", text="はな。")],
    "tree_basic": [point("examine_front", 0, -1, "up", "examine", text="き。")],
    "house_main": [],
    "bed_basic": [point("sleep_main", 0, 1, "up", "sleep", pose="bed_sleep")],
}
for entry in catalog["objects"]:
    entry["actionPoints"] = existing[entry["definitionId"]]

def planned(id, name, category, map_type, blocks, points):
    return {"definitionId": id, "displayName": name, "category": category,
            "previewSpritePath": "", "visualWidthPixels": 0, "visualHeightPixels": 0,
            "rootAnchor": "bottom-center", "footprint": {"width": 0, "height": 0},
            "blocksMovement": blocks, "excludeGrass": blocks, "editorSelectable": True,
            "blockedCellOffsets": [], "allowedMapTypes": [map_type],
            "actionPoints": points}

# Offsets for artless definitions are authoring proposals. Confirm against final art before placement.
catalog["objects"] += [
    planned("cushion_basic", "クッション", "furniture", "interior", False,
            [point("sit_main", 0, 0, "up", "sit", pose="cushion_sit")]),
    planned("desk_basic", "机", "furniture", "interior", True,
            [point("examine_front", 0, -1, "up", "examine", text="つくえ。")]),
    planned("bench_basic", "ベンチ", "furniture", "outdoor", True,
            [point("seat_left", -1, -1, "up", "sit", pose="bench_sit"),
             point("seat_right", 1, -1, "up", "sit", pose="bench_sit")]),
    *[planned("sign_" + direction, "看板：" + label, "fixture", "outdoor", True,
              [point("read_front", 0, -1, "up", "examine", text=label)])
      for direction, label in (("north", "きた"), ("east", "ひがし"),
                               ("south", "みなみ"), ("west", "にし"))],
    planned("well_basic", "井戸", "fixture", "outdoor", True,
            [point("examine_front", 0, -1, "up", "examine", text="いど。")]),
]
catalog["formatVersion"] = 3
catalog_path.write_text(json.dumps(catalog, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
