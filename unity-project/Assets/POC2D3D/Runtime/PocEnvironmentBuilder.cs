using System.Collections.Generic;
using Halka.Game.World;
using UnityEngine;

namespace Halka.Poc2D3D
{
    public sealed class PocEnvironmentStats
    {
        public string MapId;
        public int GrassTiles;
        public int RoadCells;
        public int RoadCellsOutside;
        public int RoadFull;
        public int RoadEdge;
        public int RoadCorner;
        public int Tufts;
        public int RoadsideTufts;
        public int GroundTriangles;
        public int TuftTriangles;
        public readonly HashSet<Vector2Int> RoadCellSet = new HashSet<Vector2Int>();
        public readonly List<Vector3> TuftPositions = new List<Vector3>();
    }

    // Dresses an outdoor map with Blender pieces, entirely from Map Data:
    // grass tiles on every cell (plus an outside margin), road pieces on every Dirt Surface
    // cell chosen from its neighbours, and grass tufts where MapPlacementRules.HasGrass says
    // the 2D game grows grass. Positions come from a stable hash of map ID and cell, so the
    // same Map JSON always rebuilds the same scenery and a moved object updates it.
    public static class PocEnvironmentBuilder
    {
        public const string RoadSurfaceId = "dirt";
        private static readonly Vector2Int[] Sides =
            { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

        public static PocEnvironmentStats Build(SharedWorldModel world, PocEnvironmentPresentation dressing,
            Material material, Transform parent, out GameObject ground, out GameObject tufts)
        {
            var map = world.Map;
            var stats = new PocEnvironmentStats { MapId = map.MapId };
            var margin = dressing.outsideMargin;
            CollectRoad(map, margin, stats);

            var groundMesh = new MeshAppender();
            var whiteTint = Color.white;
            for (var y = map.MinCell.y - margin; y <= map.MaxCell.y + margin; y++)
            for (var x = map.MinCell.x - margin; x <= map.MaxCell.x + margin; x++)
            {
                var cell = new Vector2Int(x, y);
                var tile = dressing.grassTiles[(int)(PocStableHash.Of(map.MapId, cell, "tile") %
                    (uint)dressing.grassTiles.Count)];
                var tint = map.Contains(cell) ? whiteTint : dressing.outsideTint;
                groundMesh.Append(Mesh(tile), Matrix4x4.Translate(GridSpace3D.ToWorld(cell)), tint);
                stats.GrassTiles++;
            }
            foreach (var cell in stats.RoadCellSet)
            {
                var tint = map.Contains(cell) ? whiteTint : dressing.outsideTint;
                for (var hx = -1; hx <= 1; hx += 2)
                for (var hz = -1; hz <= 1; hz += 2)
                {
                    var horizontal = stats.RoadCellSet.Contains(cell + new Vector2Int(hx, 0));
                    var vertical = stats.RoadCellSet.Contains(cell + new Vector2Int(0, hz));
                    GameObject piece;
                    var swap = false;
                    if (horizontal && vertical) { piece = dressing.dirtFull; stats.RoadFull++; }
                    else if (!horizontal && vertical) { piece = dressing.dirtEdge; stats.RoadEdge++; }
                    else if (horizontal) { piece = dressing.dirtEdge; swap = true; stats.RoadEdge++; }
                    else { piece = dressing.dirtCorner; stats.RoadCorner++; }
                    groundMesh.Append(Mesh(piece), QuadrantMatrix(cell, hx, hz, swap), tint);
                }
            }
            stats.GroundTriangles = groundMesh.TriangleCount;
            ground = groundMesh.CreateObject("Ground (Blender tiles + road from Map Data)", parent, material);
            var groundRenderer = ground.GetComponent<MeshRenderer>();
            groundRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var tuftMesh = new MeshAppender();
            for (var y = map.MinCell.y; y <= map.MaxCell.y; y++)
            for (var x = map.MinCell.x; x <= map.MaxCell.x; x++)
            {
                var cell = new Vector2Int(x, y);
                if (!MapPlacementRules.HasGrass(map, cell)) continue;
                var center = GridSpace3D.ToWorld(cell);
                var hash = PocStableHash.Of(map.MapId, cell, "tuft");
                if (PocStableHash.Unit(hash) < dressing.tuftChance)
                {
                    var jitter = new Vector3(Jitter(map.MapId, cell, "jx", 0.11f), 0f, Jitter(map.MapId, cell, "jz", 0.11f));
                    AddTuft(tuftMesh, dressing, map.MapId, cell, "main", center + jitter,
                        0.85f + 0.3f * PocStableHash.Unit(PocStableHash.Of(map.MapId, cell, "scale")), stats);
                }
                foreach (var side in Sides)
                {
                    if (!stats.RoadCellSet.Contains(cell + side)) continue;
                    var salt = "road" + side.x + side.y;
                    if (PocStableHash.Unit(PocStableHash.Of(map.MapId, cell, salt)) >= dressing.roadsideTuftChance) continue;
                    var along = new Vector3(side.y, 0f, side.x) * Jitter(map.MapId, cell, salt + "a", 0.12f);
                    var position = center + GridSpace3D.Direction(side) * 0.17f + along;
                    AddTuft(tuftMesh, dressing, map.MapId, cell, salt, position, 0.7f, stats);
                    stats.RoadsideTufts++;
                }
            }
            stats.TuftTriangles = tuftMesh.TriangleCount;
            tufts = tuftMesh.CreateObject("Grass tufts (decoration only, no collider)", parent, material);
            var tuftRenderer = tufts.GetComponent<MeshRenderer>();
            tuftRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return stats;
        }

        // Dirt Surfaces inside the bounds, plus roads continued straight out of the map from any
        // road-end Marker that sits on the map edge (the 2D game draws them running off-screen).
        public static void CollectRoad(MapDefinition map, int margin, PocEnvironmentStats stats)
        {
            foreach (var surface in map.Surfaces)
                if (surface.Definition != null && surface.Definition.StableId == RoadSurfaceId)
                    stats.RoadCellSet.Add(surface.Cell);
            stats.RoadCells = stats.RoadCellSet.Count;
            foreach (var marker in map.Markers)
            {
                if (!stats.RoadCellSet.Contains(marker.Cell)) continue;
                var outward = marker.Cell.y == map.MaxCell.y ? Vector2Int.up :
                    marker.Cell.y == map.MinCell.y ? Vector2Int.down :
                    marker.Cell.x == map.MaxCell.x ? Vector2Int.right :
                    marker.Cell.x == map.MinCell.x ? Vector2Int.left : Vector2Int.zero;
                if (outward == Vector2Int.zero) continue;
                for (var i = 1; i <= margin; i++)
                    if (stats.RoadCellSet.Add(marker.Cell + outward * i)) stats.RoadCellsOutside++;
            }
        }

        // The Blender piece covers the north-east quarter. Swapping X/Z moves its wavy edge from
        // the East side to the North side; the signs mirror it into the other three quarters.
        public static Matrix4x4 QuadrantMatrix(Vector2Int cell, int hx, int hz, bool swap)
        {
            var m = Matrix4x4.identity;
            m.m00 = swap ? 0f : hx; m.m02 = swap ? hx : 0f;
            m.m20 = swap ? hz : 0f; m.m22 = swap ? 0f : hz;
            var origin = GridSpace3D.ToWorld(cell);
            m.m03 = origin.x; m.m13 = origin.y; m.m23 = origin.z;
            return m;
        }

        private static void AddTuft(MeshAppender target, PocEnvironmentPresentation dressing, string mapId,
            Vector2Int cell, string salt, Vector3 position, float scale, PocEnvironmentStats stats)
        {
            var variant = dressing.grassTufts[(int)(PocStableHash.Of(mapId, cell, salt + "v") % (uint)dressing.grassTufts.Count)];
            var yaw = PocStableHash.Unit(PocStableHash.Of(mapId, cell, salt + "yaw")) * 360f;
            target.Append(Mesh(variant), Matrix4x4.TRS(position, Quaternion.Euler(0f, yaw, 0f), Vector3.one * scale), Color.white);
            stats.Tufts++;
            stats.TuftPositions.Add(position);
        }

        private static float Jitter(string mapId, Vector2Int cell, string salt, float range) =>
            (PocStableHash.Unit(PocStableHash.Of(mapId, cell, salt)) * 2f - 1f) * range;

        internal static Mesh Mesh(GameObject model)
        {
            var filter = model.GetComponentInChildren<MeshFilter>();
            return filter != null ? filter.sharedMesh : null;
        }

        // Copies transformed vertices into one mesh, so the whole ground is one draw call.
        internal sealed class MeshAppender
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector3> normals = new List<Vector3>();
            private readonly List<Color32> colors = new List<Color32>();
            private readonly List<int> triangles = new List<int>();
            private readonly Dictionary<Mesh, (Vector3[] v, Vector3[] n, Color32[] c, int[] t)> cache =
                new Dictionary<Mesh, (Vector3[], Vector3[], Color32[], int[])>();

            public int TriangleCount => triangles.Count / 3;

            public void Append(Mesh mesh, Matrix4x4 matrix, Color tint)
            {
                if (mesh == null) return;
                if (!cache.TryGetValue(mesh, out var data))
                {
                    data = (mesh.vertices, mesh.normals, mesh.colors32, mesh.triangles);
                    cache[mesh] = data;
                }
                var start = vertices.Count;
                var flip = matrix.determinant < 0f;
                for (var i = 0; i < data.v.Length; i++)
                {
                    vertices.Add(matrix.MultiplyPoint3x4(data.v[i]));
                    normals.Add(matrix.MultiplyVector(data.n[i]).normalized);
                    var c = data.c.Length > i ? (Color)data.c[i] : Color.white;
                    colors.Add(new Color(c.r * tint.r, c.g * tint.g, c.b * tint.b, 1f));
                }
                for (var i = 0; i < data.t.Length; i += 3)
                {
                    triangles.Add(start + data.t[i]);
                    triangles.Add(start + (flip ? data.t[i + 2] : data.t[i + 1]));
                    triangles.Add(start + (flip ? data.t[i + 1] : data.t[i + 2]));
                }
            }

            public GameObject CreateObject(string name, Transform parent, Material material)
            {
                var mesh = new Mesh { name = name };
                if (vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetColors(colors);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = material;
                return go;
            }
        }
    }
}
