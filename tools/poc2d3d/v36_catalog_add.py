"""はるかわーるど ver3.6 (North area): adds the North area's definitions to the Object Catalog
(object_catalog.hwcatalog.json, WORLD EDITOR v1.0's format "halka-world-catalog" v3), keeping every
existing entry and the file's own layout (2-space JSON, CRLF). Run once; a second run changes nothing.

  - Surface  water          the pond's water: blocks movement, grows no grass (drawn by the game).
  - Object   flower_blue    a blue flower (North blue), blocked like flower_yellow.
  - Object   signpost_blue  a sign with a blue board and a North arrow; its message is fixed (an
                            Action Point's interactionText): only sign_basic has a per-placement
                            signText, and "sign_north" is an old alias of sign_basic.
  - Object   water_lily     lily pads on the water at the bank: its Action Point (one cell South,
                            facing up, on the bank) is the pond's みずをのぞく.
  - Object   pond_reeds     waterside reeds (on the water's edge, decoration only).
  The lily and the reeds lie on water cells, so they block nothing themselves (no blocked cells, like
  living_rug): MapRules refuses a blocked Object cell on a blocking Surface, and the water already
  blocks the cell. Run again after a change here: this script's own entries are replaced, nothing
  else in the catalog is touched.

  python tools/poc2d3d/v36_catalog_add.py
"""
import json
import os

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
CATALOG = os.path.join(ROOT, "unity-project", "Assets", "Content", "Maps", "Authoring", "object_catalog.hwcatalog.json")


def examine(point_id, text):
    return [{"id": point_id, "playerCellOffset": {"x": 0, "y": -1}, "playerFacing": "up", "actionType": "examine",
             "poseKey": None, "interactionText": text}]


def small(definition_id, name, category, sprite, actions, blocks=True):
    return {
        "definitionId": definition_id, "displayName": name, "category": category,
        "previewSpritePath": "Assets/Content/World/" + sprite + ".png",
        "visualWidthPixels": 32, "visualHeightPixels": 32, "rootAnchor": "bottom-center",
        "footprint": {"width": 1, "height": 1}, "blocksMovement": blocks, "excludeGrass": True,
        "editorSelectable": True, "blockedCellOffsets": [{"x": 0, "y": 0}] if blocks else [], "allowedMapTypes": [],
        "actionPoints": actions,
    }


SURFACES = [{
    "definitionId": "water", "displayName": "みず", "category": "surface",
    "previewSpritePath": "Assets/Content/World/water.png", "visualWidthPixels": 32, "visualHeightPixels": 32,
    "editorSelectable": True, "blocksMovement": True, "allowedMapTypes": [],
}]
OBJECTS = [
    small("flower_blue", "あおい はな", "nature", "flower_blue", examine("examine_front", "あおい はな。")),
    small("signpost_blue", "みちしるべ（きた）", "fixture", "sign_blue", examine("read_front", "きたの みずべ。 しずかな いけが ある。")),
    small("water_lily", "すいれん（みずをのぞく）", "nature", "water_lily", examine("look_into_water", "みずが きらきらしている。"), blocks=False),
    small("pond_reeds", "みずべの あし", "nature", "pond_reeds", [], blocks=False),
]


def main():
    raw = open(CATALOG, "rb").read().decode("utf-8")
    catalog = json.loads(raw)
    added = []
    for key, entries in (("surfaces", SURFACES), ("objects", OBJECTS)):
        for entry in entries:
            at = next((i for i, e in enumerate(catalog[key]) if e["definitionId"] == entry["definitionId"]), None)
            if at is None:
                catalog[key].append(entry)
                added.append(entry["definitionId"])
            elif catalog[key][at] != entry:
                catalog[key][at] = entry
                added.append(entry["definitionId"] + " (updated)")
    text = json.dumps(catalog, indent=2, ensure_ascii=False).replace("\n", "\r\n") + "\r\n"
    open(CATALOG, "wb").write(text.encode("utf-8"))
    print("added:", ", ".join(added) if added else "nothing (already up to date)")


if __name__ == "__main__":
    main()
