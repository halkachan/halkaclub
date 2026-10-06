"""Migrate v2 authoring maps to typed entity spawns, preserving other data."""
import json
from pathlib import Path
import sys


def migrate(path: Path) -> None:
    data = json.loads(path.read_text(encoding="utf-8-sig"))
    if data["formatVersion"] == 3:
        return
    if data["formatVersion"] != 2 or "entitySpawns" in data:
        raise ValueError(f"unsupported map: {path}")
    mapping = {
        "player_start": ("ent_player_main_start", "player_main", "down"),
        "crow_spawn": ("ent_crow_main_first", "crow_main", "right"),
    }
    spawns = []
    markers = []
    for marker in data["markers"]:
        if marker["id"] in mapping:
            instance_id, definition_id, facing = mapping[marker["id"]]
            spawns.append({"instanceId": instance_id, "definitionId": definition_id,
                           "cell": marker["cell"], "facing": facing})
        else:
            markers.append(marker)
    data["markers"] = markers
    data["entitySpawns"] = sorted(spawns, key=lambda item: (
        item["definitionId"], item["cell"]["y"], item["cell"]["x"], item["instanceId"]))
    data["formatVersion"] = 3
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    for name in sys.argv[1:]:
        migrate(Path(name))
