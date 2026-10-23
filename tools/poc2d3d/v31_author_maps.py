"""はるかわーるど ver3.1: the authoring changes of the studio update, as one reproducible script.

  python tools/poc2d3d/v31_author_maps.py

Object Catalog (object_catalog.hwcatalog.json): adds
  surfaces  booth_floor, booth_wall
  objects   soundbooth_exterior (the booth's door, like house_main's), dtm_desk_large, illust_desk_tablet
  Nothing is removed: art_easel_basic and recording_mic_basic keep their Definitions.
halka_house.hwmap.json: removes the easel and the mic placements, adds the booth and the
  illustration desk (existing furniture, its cells and Action Point cells are unchanged).
halka_soundbooth.hwmap.json: the new interior map (15 x 11).

Re-running is safe: every step checks the current state first. Files keep CRLF and the
json.dumps(indent=2, ensure_ascii=False) layout of the existing files.
"""
import json
import os

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
AUTHORING = os.path.join(ROOT, "unity-project", "Assets", "Content", "Maps", "Authoring")
CATALOG = os.path.join(AUTHORING, "object_catalog.hwcatalog.json")
HOUSE = os.path.join(AUTHORING, "halka_house.hwmap.json")
BOOTH = os.path.join(AUTHORING, "halka_soundbooth.hwmap.json")

BOOTH_MAP_ID = "halka_soundbooth"
DTM_TEXT = "おんがくを つくる つくえ。"
TABLET_TEXT = "えきタブ。 イラストを かく。"


def load(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def save(path, data):
    text = json.dumps(data, indent=2, ensure_ascii=False) + "\n"
    with open(path, "w", encoding="utf-8", newline="\r\n") as f:
        f.write(text)


def cell(x, y):
    return {"x": x, "y": y}


def examine(point_id, dx, dy, text):
    return {"id": point_id, "playerCellOffset": cell(dx, dy), "playerFacing": "up", "actionType": "examine",
            "poseKey": None, "interactionText": text}


SURFACES = [
    {"definitionId": "booth_floor", "displayName": "防音室の床", "category": "base",
     "previewSpritePath": "Assets/Content/World/booth_floor.png", "visualWidthPixels": 32, "visualHeightPixels": 32,
     "blocksMovement": False, "allowedMapTypes": [], "editorSelectable": True},
    {"definitionId": "booth_wall", "displayName": "防音室の壁", "category": "surface",
     "previewSpritePath": "Assets/Content/World/booth_wall.png", "visualWidthPixels": 32, "visualHeightPixels": 32,
     "blocksMovement": True, "allowedMapTypes": [], "editorSelectable": True},
]

OBJECTS = [
    # The booth as seen in the house: 2 x 2, the root (bottom-left) cell is its walkable door,
    # like house_main's root cell outdoors. The importer gives it the HouseTransition behavior.
    {"definitionId": "soundbooth_exterior", "displayName": "防音室", "category": "fixture",
     "previewSpritePath": "Assets/Content/World/soundbooth_exterior.png", "visualWidthPixels": 64,
     "visualHeightPixels": 128, "rootAnchor": "bottom-left", "footprint": {"width": 2, "height": 2},
     "blockedCellOffsets": [cell(1, 0), cell(0, 1), cell(1, 1)], "blocksMovement": True, "excludeGrass": True,
     "editorSelectable": True, "allowedMapTypes": [], "actionPoints": []},
    # The large DTM desk: desk_basic's depth and height, twice its width.
    {"definitionId": "dtm_desk_large", "displayName": "DTMデスク", "category": "furniture",
     "previewSpritePath": "Assets/Content/World/dtm_desk_large.png", "visualWidthPixels": 128,
     "visualHeightPixels": 80, "rootAnchor": "bottom-left", "footprint": {"width": 4, "height": 1},
     "blockedCellOffsets": [cell(0, 0), cell(1, 0), cell(2, 0), cell(3, 0)], "blocksMovement": True,
     "excludeGrass": True, "editorSelectable": True, "allowedMapTypes": [],
     "actionPoints": [examine("examine_left", 1, -1, DTM_TEXT), examine("examine_right", 2, -1, DTM_TEXT)]},
    {"definitionId": "illust_desk_tablet", "displayName": "イラスト机", "category": "furniture",
     "previewSpritePath": "Assets/Content/World/illust_desk_tablet.png", "visualWidthPixels": 64,
     "visualHeightPixels": 64, "rootAnchor": "bottom-left", "footprint": {"width": 2, "height": 1},
     "blockedCellOffsets": [cell(0, 0), cell(1, 0)], "blocksMovement": True, "excludeGrass": True,
     "editorSelectable": True, "allowedMapTypes": [],
     "actionPoints": [examine("examine_front", 0, -1, TABLET_TEXT)]},
]

HOUSE_REMOVED = ["obj_recording_mic_v30_preview", "obj_art_easel_v30_preview"]
HOUSE_ADDED = [
    {"instanceId": "obj_soundbooth_v31_halka_house", "definitionId": "soundbooth_exterior", "rootCell": cell(-3, 1)},
    {"instanceId": "obj_illust_desk_v31_halka_house", "definitionId": "illust_desk_tablet", "rootCell": cell(-3, -2)},
]


def booth_map():
    min_x, max_x, min_y, max_y = -7, 7, -5, 5
    exit_cell = (0, min_y)
    surfaces = []
    for y in range(max_y, min_y - 1, -1):
        for x in range(min_x, max_x + 1):
            wall = y >= max_y - 1 or y == min_y or x in (min_x, max_x)
            if wall and (x, y) != exit_cell:
                surfaces.append({"definitionId": "booth_wall", "cell": cell(x, y)})
    surfaces.sort(key=lambda s: (s["cell"]["y"], s["cell"]["x"]))
    return {
        "format": "halka-world-map",
        "formatVersion": 4,
        "mapId": BOOTH_MAP_ID,
        "displayName": "防音室",
        "mapType": "interior",
        "baseSurfaceDefinitionId": "booth_floor",
        "grassMode": "none",
        "backdropColor": "#0c0c10",
        "bounds": {"minX": min_x, "maxX": max_x, "minY": min_y, "maxY": max_y},
        "surfaces": surfaces,
        "objects": [
            {"instanceId": "obj_dtm_desk_v31_soundbooth", "definitionId": "dtm_desk_large", "rootCell": cell(-2, 3)},
            {"instanceId": "obj_recording_mic_v31_soundbooth", "definitionId": "recording_mic_basic",
             "rootCell": cell(4, 1)},
        ],
        "markers": [{"id": "interior_entry", "cell": cell(0, min_y + 1)}, {"id": "interior_exit", "cell": cell(*exit_cell)}],
        "entitySpawns": [],
        "areaTransitions": [],
    }


def main():
    catalog = load(CATALOG)
    for surface in SURFACES:
        if not any(s["definitionId"] == surface["definitionId"] for s in catalog["surfaces"]):
            catalog["surfaces"].append(surface)
    for obj in OBJECTS:
        if not any(o["definitionId"] == obj["definitionId"] for o in catalog["objects"]):
            catalog["objects"].append(obj)
    for kept in ("art_easel_basic", "recording_mic_basic", "desk_basic"):
        assert any(o["definitionId"] == kept for o in catalog["objects"]), kept
    save(CATALOG, catalog)

    house = load(HOUSE)
    house["objects"] = [o for o in house["objects"] if o["instanceId"] not in HOUSE_REMOVED]
    for obj in HOUSE_ADDED:
        if not any(o["instanceId"] == obj["instanceId"] for o in house["objects"]):
            house["objects"].append(obj)
    save(HOUSE, house)

    save(BOOTH, booth_map())
    print("catalog surfaces", [s["definitionId"] for s in catalog["surfaces"]])
    print("catalog objects", [o["definitionId"] for o in catalog["objects"]])
    print("house objects", [(o["definitionId"], o["rootCell"]) for o in house["objects"]])


if __name__ == "__main__":
    main()
