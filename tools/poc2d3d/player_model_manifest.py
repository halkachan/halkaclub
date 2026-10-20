"""Writes blender/exports/haruka_player_official_manifest.json: the official 3D ﾊﾙｶﾁｬﾝ (Phase 7).

  python tools/poc2d3d/player_model_manifest.py

Records which files make the official player model and their SHA-256, so a later change to the
model (or to the 2D sprites it is shown with) is noticed: Phase7ReleaseChecks compares the files
with these hashes. It reads the files only; it never rebuilds the model.
"""
import hashlib
import json
import os
import re

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "blender", "exports", "haruka_player_official_manifest.json")
FILES = {
    "blenderSource": "blender/sources/phase2_1/haruka_blocky_placeholder.blend",
    "generator": "blender/scripts/build_haruka_blocky.py",
    "generatorHelpers": "blender/scripts/halka_lowpoly.py",
    "fbx": "unity-project/Assets/POC2D3D/Models/Phase2_1/haruka_blocky_placeholder.fbx",
    "fbxImportSettings": "unity-project/Assets/POC2D3D/Models/Phase2_1/haruka_blocky_placeholder.fbx.meta",
    "material": "unity-project/Assets/POC2D3D/Materials/POC_ToonVertexColor.mat",
    "materialShader": "unity-project/Assets/POC2D3D/Shaders/PocToonVertexColor.shader",
}
PREVIEWS = ["south_front", "east", "north_back", "west", "three_quarter", "top", "game_2d_angle"]


TEXT = (".py", ".meta", ".mat", ".shader")


def sha256(path):
    """Binary files as they are; text files with LF line ends (Git and Unity may write either)."""
    with open(os.path.join(ROOT, path), "rb") as f:
        data = f.read()
    if path.endswith(TEXT):
        data = data.replace(b"\r\n", b"\n")
    return hashlib.sha256(data).hexdigest()


def guid(meta_path):
    with open(os.path.join(ROOT, meta_path), encoding="utf-8") as f:
        return re.search(r"^guid: ([0-9a-f]{32})", f.read(), re.M).group(1)


def main():
    for key, path in FILES.items():
        assert os.path.exists(os.path.join(ROOT, path)), path
    built = json.load(open(os.path.join(ROOT, "blender/exports/phase2_1_haruka_blocky_placeholder_manifest.json"), encoding="utf-8"))
    sprites = [line.split(None, 1) for line in open(os.path.join(ROOT, "tools/poc2d3d/player_sprite_sha256.txt"), encoding="utf-8")
               if len(line.strip()) > 66]
    manifest = {
        "model": "ﾊﾙｶﾁｬﾝ 3D (blocky, Minecraft-like low poly)",
        "status": "official",
        "adopted": "Phase 7 (user decision): the model used in the 3D view since Phase 2.1 is the official 3D player model; it is not redesigned",
        "internalName": "haruka_blocky_placeholder (the Phase 2.1 file name, kept so no reference breaks)",
        "files": {key: {"path": path, "sha256": sha256(path)} for key, path in FILES.items()},
        "hashNote": "SHA-256; .py, .meta, .mat and .shader are hashed with LF line ends",
        "previews": {v: f"blender/exports/previews/phase2_1/haruka_blocky_placeholder_{v}.png" for v in PREVIEWS},
        "unity": {
            "fbxGuid": guid(FILES["fbxImportSettings"]),
            "materialGuid": guid(FILES["material"] + ".meta"),
            "catalog": "Assets/POC2D3D/Data/POC_PresentationCatalog.asset, field player (PocActorPresentation)",
            "catalogLegacyField": "playerModel = poc_player.fbx (Phase 1 technical stand-in, not drawn in the game)",
            "prefab": "the FBX's imported model prefab (no separate .prefab asset; PocActorView instantiates it)",
            "importSettings": "scale 1 (file scale), axis conversion baked, no animation, no blend shapes, materials remapped to POC_ToonVertexColor, readable",
        },
        "geometry": {
            "triangles": built["triangles"],
            "parts": {name: {"triangles": part["triangles"], "pivot": part["pivot"]} for name, part in built["parts"].items()},
            "boundsMin": built["boundsMin"],
            "boundsMax": built["boundsMax"],
            "heightMetres": built["boundsMax"][2],
            "pixelMetres": built["pixelMetres"],
            "front": built["front"],
        },
        "presentation": {
            "pivot": "ground centre of the model at the logical cell centre",
            "uniformScale": 1,
            "yawOffsetDegrees": 180,
            "idle": "rest pose (limbs at 0)",
            "walk": "procedural swing of ArmL / ArmR / LegL / LegR, 26 degrees, during the 0.18 s Grid step",
            "sit": "procedural: legs 90 degrees, arms 28 degrees; bench hips +0.03 and 0.08 toward the middle, sofa +0.07 and 0.06, cushion legs 62 degrees",
            "view2D": "the original sprites (Assets/Content/Character), never this model",
        },
        "sprites2D": {path.strip(): digest for digest, path in sprites},
    }
    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        json.dump(manifest, f, indent=2, ensure_ascii=False)
        f.write("\n")
    print("written", OUT, len(manifest["sprites2D"]), "sprites")


if __name__ == "__main__":
    main()
