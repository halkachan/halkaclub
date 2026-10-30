"""はるかわーるど ver3.7 (West area): adds the West forest's definitions to the Object Catalog
(object_catalog.hwcatalog.json, WORLD EDITOR v1.0's format "halka-world-catalog" v3), keeping every
existing entry and the file's own layout (2-space JSON, CRLF). Run once; a second run changes nothing.

  - Object   tree_stump_hollow  the clearing's big stump: blocks its cell; its Action Point (one cell
                                South, facing up) is きりかぶにふれる (the game adds the leaves).
  - Object   forest_bush        a low round bush: blocks its cell (the forest's edges).
  - Object   forest_fern        ferns: decoration only, walkable (no blocked cells, like pond_reeds); grass
                                grows round them as on any walkable cell (excludeGrass false: the game's
                                grass rule only clears cells an Object blocks).
  - Object   fallen_leaves      a few fallen leaves flat on the ground: decoration only, walkable (as ferns).
  - Object   signpost_green     a sign with a West-green board and a West arrow; its message is fixed
                                (an Action Point's interactionText): "sign_west" is an old alias of
                                sign_basic, so the new sign has its own id (as signpost_blue).
  Run again after a change here: this script's own entries are replaced, nothing else in the catalog
  is touched.

  python tools/poc2d3d/v37_catalog_add.py
"""
import json
import os

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
CATALOG = os.path.join(ROOT, "unity-project", "Assets", "Content", "Maps", "Authoring", "object_catalog.hwcatalog.json")


def examine(point_id, text):
    return [{"id": point_id, "playerCellOffset": {"x": 0, "y": -1}, "playerFacing": "up", "actionType": "examine",
             "poseKey": None, "interactionText": text}]


def small(definition_id, name, category, sprite, actions, blocks=True, exclude_grass=True):
    return {
        "definitionId": definition_id, "displayName": name, "category": category,
        "previewSpritePath": "Assets/Content/World/" + sprite + ".png",
        "visualWidthPixels": 32, "visualHeightPixels": 32, "rootAnchor": "bottom-center",
        "footprint": {"width": 1, "height": 1}, "blocksMovement": blocks, "excludeGrass": exclude_grass,
        "editorSelectable": True, "blockedCellOffsets": [{"x": 0, "y": 0}] if blocks else [], "allowedMapTypes": [],
        "actionPoints": actions,
    }


OBJECTS = [
    small("tree_stump_hollow", "おおきな きりかぶ（きりかぶにふれる）", "nature", "stump_hollow",
          examine("touch_stump", "はっぱが くるくる まいあがった。")),
    small("forest_bush", "もりの しげみ", "nature", "forest_bush", []),
    small("forest_fern", "しだ", "nature", "forest_fern", [], blocks=False, exclude_grass=False),
    small("fallen_leaves", "おちば", "nature", "fallen_leaves", [], blocks=False, exclude_grass=False),
    small("signpost_green", "みちしるべ（にし）", "fixture", "sign_green",
          examine("read_front", "にしの もり。 こもれびの こみちが つづく。")),
]


def main():
    raw = open(CATALOG, "rb").read().decode("utf-8")
    catalog = json.loads(raw)
    added = []
    for entry in OBJECTS:
        at = next((i for i, e in enumerate(catalog["objects"]) if e["definitionId"] == entry["definitionId"]), None)
        if at is None:
            catalog["objects"].append(entry)
            added.append(entry["definitionId"])
        elif catalog["objects"][at] != entry:
            catalog["objects"][at] = entry
            added.append(entry["definitionId"] + " (updated)")
    text = json.dumps(catalog, indent=2, ensure_ascii=False).replace("\n", "\r\n") + "\r\n"
    open(CATALOG, "wb").write(text.encode("utf-8"))
    print("added:", ", ".join(added) if added else "nothing (already up to date)")


if __name__ == "__main__":
    main()
