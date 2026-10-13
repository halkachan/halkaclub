"""Prints the WASD key sequence for a shortest 4-direction route on an official map,
using only Map JSON v4 + Catalog blocking (the same data the POC Grid uses).

  python tools/poc2d3d/route.py first_field 3,2 5,0 [--face up]
"""
import argparse
import collections
import json
import os

ROOT = os.path.join(os.path.dirname(__file__), "..", "..", "unity-project", "Assets", "Content", "Maps", "Authoring")
KEY = {(1, 0): "KeyD", (-1, 0): "KeyA", (0, 1): "KeyW", (0, -1): "KeyS"}
FACE = {"up": "KeyW", "down": "KeyS", "left": "KeyA", "right": "KeyD"}


def blocked_cells(map_id):
    with open(os.path.join(ROOT, map_id + ".hwmap.json"), encoding="utf-8") as f:
        m = json.load(f)
    with open(os.path.join(ROOT, "object_catalog.hwcatalog.json"), encoding="utf-8") as f:
        c = json.load(f)
    surfaces = {s["definitionId"]: s for s in c["surfaces"]}
    objects = {o["definitionId"]: o for o in c["objects"]}
    blocked = {(s["cell"]["x"], s["cell"]["y"]) for s in m["surfaces"] if surfaces[s["definitionId"]].get("blocksMovement")}
    for o in m["objects"]:
        d = objects[o["definitionId"]]
        if d.get("blocksMovement", True):
            for off in d["blockedCellOffsets"]:
                blocked.add((o["rootCell"]["x"] + off["x"], o["rootCell"]["y"] + off["y"]))
    return m["bounds"], blocked


def main():
    p = argparse.ArgumentParser()
    p.add_argument("map_id")
    p.add_argument("start")
    p.add_argument("goal")
    p.add_argument("--face")
    a = p.parse_args()
    bounds, blocked = blocked_cells(a.map_id)
    start = tuple(map(int, a.start.split(",")))
    goal = tuple(map(int, a.goal.split(",")))
    inside = lambda c: bounds["minX"] <= c[0] <= bounds["maxX"] and bounds["minY"] <= c[1] <= bounds["maxY"]
    parent = {start: None}
    queue = collections.deque([start])
    while queue:
        cell = queue.popleft()
        if cell == goal:
            break
        for d in ((0, 1), (1, 0), (0, -1), (-1, 0)):
            n = (cell[0] + d[0], cell[1] + d[1])
            if n not in parent and inside(n) and n not in blocked:
                parent[n] = cell
                queue.append(n)
    if goal not in parent:
        raise SystemExit("no route")
    path = []
    cell = goal
    while parent[cell]:
        path.append(cell)
        cell = parent[cell]
    path.reverse()
    keys, prev = [], start
    for cell in path:
        keys.append(KEY[(cell[0] - prev[0], cell[1] - prev[1])] + ":120")
        prev = cell
    if a.face:
        keys.append(FACE[a.face] + ":60")
    print(",".join(keys))


if __name__ == "__main__":
    main()
