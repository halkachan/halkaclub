"""はるかわーるど ver3.8 (South area): adds the South hill's definitions to the Object Catalog
(object_catalog.hwcatalog.json, WORLD EDITOR v1.0's format "halka-world-catalog" v3), keeping every
existing entry and the file's own layout (2-space JSON, CRLF). Run once; a second run changes nothing.

  - Object   south_pinwheel_red   the hilltop's red pinwheel: blocks its cell; its Action Point (one cell
                                  South, facing up) is かざぐるまをまわす (the game turns the blades).
  - Object   south_rock_red       a red-brown boulder: blocks its cell (the rocky ground).
  - Object   south_rock_ledge     a low step of red-brown rock with a turf lip: blocks its cell (the
                                  hill's edge; pieces side by side).
  - Object   south_rock_small     a few small stones: decoration only, walkable (grass grows round them).
  - Object   south_grass_short    a clump of short, sun-dried grass: decoration only, walkable (grass grows
                                  round it as on any walkable cell: excludeGrass false, as ver3.7's ferns;
                                  the game's grass rule only clears cells an Object blocks).
  - Object   south_ground_accent  a patch of reddish soil on the ground: decoration only, walkable (the
                                  same grass rule: the field's tufts may stand on its edge).
  - Object   south_signpost_red   a sign with a South-red board and a South arrow; its message is fixed
                                  (an Action Point's interactionText): "sign_south" is an old alias of
                                  sign_basic, so the new sign has its own id (as signpost_green).
  Run again after a change here: this script's own entries are replaced, nothing else in the catalog
  is touched.

  python tools/poc2d3d/v38_catalog_add.py
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
    dict(small("south_pinwheel_red", "あかねの かざぐるま（かざぐるまをまわす）", "fixture", "pinwheel_red",
               examine("turn_pinwheel", "かざぐるまが くるくる まわった！")), visualHeightPixels=96),
    small("south_rock_red", "あかちゃいろの いわ", "nature", "rock_red", []),
    small("south_rock_ledge", "いわの だん（おかの ふち）", "nature", "rock_ledge", []),
    small("south_rock_small", "ちいさな いし", "nature", "rock_small", [], blocks=False, exclude_grass=False),
    small("south_grass_short", "みじかい くさ", "nature", "grass_short", [], blocks=False, exclude_grass=False),
    small("south_ground_accent", "あかい つち", "nature", "ground_accent", [], blocks=False, exclude_grass=False),
    small("south_signpost_red", "みちしるべ（みなみ）", "fixture", "sign_red",
          examine("read_front", "みなみの かぜのおか。 こみちの さきに かざぐるま。")),
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
