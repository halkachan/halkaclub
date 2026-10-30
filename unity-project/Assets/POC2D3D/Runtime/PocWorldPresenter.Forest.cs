using System.Collections.Generic;
using Halka.Poc2D3D.World;
using UnityEngine;

namespace Halka.Poc2D3D
{
    // ver3.7: the West forest (「にしのこもれびのもり」).
    // - 木漏れ日: where a Map has komorebi_ Markers, the sunlight coming through the leaves is drawn round
    //   each one as small blocky dapples of light on the ground (a few cells, thinning out; never on a
    //   blocked cell). Per chunk, built and freed with it: one mesh that is always on by day and two
    //   small sets that take turns (the leaves moving overhead). No light, no shader, no texture: the
    //   ground's vertex-colour material, so the 2D view (the same meshes from above, 1/8 cell = 2 px)
    //   shows the same light as the 3D view. By night the light goes (it would be brighter than the
    //   moonlit ground); it comes back with the morning.
    // - きりかぶにふれる: when she touches the clearing's stump (its Action Point), a handful of small
    //   leaves fly up from it, spinning, spread out and drift down again (LeafSeconds). One burst at a
    //   time: touching it again while the leaves are in the air starts nothing new (the message is the
    //   Action Point's, shown by the session as before). The burst's object and mesh are made once and
    //   reused. Nothing waits: she can walk on at once.
    public sealed partial class PocWorldPresenter
    {
        public const string KomorebiMarkerPrefix = "komorebi_";
        public const float KomorebiHeight = 0.006f;        // above the road (0.004), under the water (0.012)
        public const float KomorebiFlickerSeconds = 1.1f;
        public const float KomorebiNightLimit = 0.5f;       // night blend at which the light is gone
        public const string StumpDefinitionId = "tree_stump_hollow";
        public const float LeafSeconds = 1.8f;
        public const int LeafCount = 12;
        public const float StumpTop = 0.26f;               // the cut of the stump model (build_v37_west.py)

        private static readonly Color KomorebiGrass = new Color32(0xd6, 0xe8, 0x8c, 0xff);
        private static readonly Color KomorebiGrassSoft = new Color32(0xbd, 0xd8, 0x7a, 0xff);
        private static readonly Color KomorebiRoad = new Color32(0xf0, 0xdc, 0x9c, 0xff);
        private static readonly Color KomorebiRoadSoft = new Color32(0xe0, 0xc8, 0x8a, 0xff);
        private static readonly Color[] LeafColors =
        {
            new Color32(0x9c, 0xc5, 0x52, 0xff), new Color32(0x74, 0xb0, 0x4a, 0xff), new Color32(0xe9, 0xc9, 0x4e, 0xff),
            new Color32(0xe0, 0xa0, 0x3c, 0xff)
        };

        private sealed class LeafBurst
        {
            public GameObject Object;
            public Mesh Mesh;
            public Vector3 Origin;
            public float StartedAt;
            public bool Active;
            public readonly Vector3[] Direction = new Vector3[LeafCount];
            public readonly float[] Rise = new float[LeafCount];
            public readonly float[] Reach = new float[LeafCount];
            public readonly float[] Spin = new float[LeafCount];
            public readonly float[] Phase = new float[LeafCount];
        }

        private LeafBurst leaves;
        private readonly List<Vector3> leafVertices = new List<Vector3>();
        private readonly List<Color> leafColors = new List<Color>();
        private readonly List<int> leafTriangles = new List<int>();
        private readonly List<Vector3> leafNormals = new List<Vector3>();
        private PocTimeOfDay timeOfDay;
        private bool timeOfDaySearched;
        private bool komorebiFlicker;
        private float komorebiSwitchAt;

        public int KomorebiCellsDrawn { get; private set; }
        public int KomorebiChunks { get; private set; }
        public bool KomorebiVisible { get; private set; } = true;
        public int LeafBurstsStarted { get; private set; }
        public int LeafRetriggersIgnored { get; private set; }
        public bool LeavesActive => leaves != null && leaves.Active;
        public float LeafHighest { get; private set; }
        public float LeafWidest { get; private set; }
        // Checks / dev only: the light by night or day regardless of the clock (null: the clock decides).
        public bool? KomorebiNightForTest { get; set; }

        // The light round each komorebi_ Marker of every region with cells in this chunk.
        private void BuildChunkKomorebi(ChunkStatic chunk, ChunkPayload payload)
        {
            if (chunk.RegionCells == 0) return;
            var main = new PocMeshBuilder();
            var flickerA = new PocMeshBuilder();
            var flickerB = new PocMeshBuilder();
            var cells = 0;
            foreach (var region in chunkIndex.Regions)
            {
                if (!region.Rect.Overlaps(chunk.Rect)) continue;
                foreach (var marker in region.Map.Markers)
                {
                    if (marker.StableId == null || !marker.StableId.StartsWith(KomorebiMarkerPrefix, System.StringComparison.Ordinal)) continue;
                    var centre = region.ToWorld(marker.Cell);
                    for (var dy = -2; dy <= 2; dy++)
                    for (var dx = -2; dx <= 2; dx++)
                    {
                        var cell = centre + new Vector2Int(dx, dy);
                        if (!chunk.Rect.Contains(cell) || !region.ContainsWorld(cell)) continue;
                        if ((region.FlagsAtWorld(cell) & OutdoorRegion.CellFlags.Blocked) != 0) continue;
                        var ring = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
                        var hash = CellHash(cell, 0x5bd1);
                        // the centre and its ring are lit; the outer ring only here and there
                        if (ring == 2 && (hash & 3) != 0) continue;
                        if (AddDapples(cell, ring, hash, (region.FlagsAtWorld(cell) & OutdoorRegion.CellFlags.Road) != 0,
                            main, flickerA, flickerB)) cells++;
                    }
                }
            }
            if (cells == 0) return;
            payload.Komorebi = AddKomorebiObject(payload, main, "Komorebi " + payload.Coord);
            payload.KomorebiA = AddKomorebiObject(payload, flickerA, "Komorebi flicker A " + payload.Coord);
            payload.KomorebiB = AddKomorebiObject(payload, flickerB, "Komorebi flicker B " + payload.Coord);
            payload.KomorebiCells = cells;
            KomorebiCellsDrawn += cells;
            KomorebiChunks++;
            ApplyKomorebi(payload);
        }

        // A cell's dapples: 2..4 blocky spots on a 1/8-cell grid (2 px in the 2D view), smaller and
        // softer away from the Marker, warmer on the road; one more spot in one of the flicker sets.
        private static bool AddDapples(Vector2Int cell, int ring, int hash, bool road, PocMeshBuilder main,
            PocMeshBuilder flickerA, PocMeshBuilder flickerB)
        {
            var c = GridSpace3D.ToWorld(cell);
            var half = GridSpace3D.CellSize * 0.5f;
            var px = GridSpace3D.CellSize / 8f;
            var bright = road ? KomorebiRoad : KomorebiGrass;
            var soft = road ? KomorebiRoadSoft : KomorebiGrassSoft;
            var spots = ring == 0 ? 4 : ring == 1 ? 3 : 2;
            var added = false;
            for (var i = 0; i <= spots; i++)
            {
                var h = CellHash(cell, 0x2f6b + i * 97);
                var gx = h & 7;
                var gy = (h >> 3) & 7;
                var w = 1 + ((h >> 6) & 1) + (ring == 0 ? 1 : 0);   // 1..3 grid units wide
                var d = 1 + ((h >> 7) & 1);
                gx = Mathf.Min(gx, 8 - w);
                gy = Mathf.Min(gy, 8 - d);
                var target = i < spots ? main : ((h >> 9) & 1) == 0 ? flickerA : flickerB;
                var color = ring == 0 && i == 0 ? bright : ((h >> 10) & 1) == 0 ? soft : bright;
                var min = new Vector3(c.x - half + gx * px, KomorebiHeight, c.z - half + gy * px);
                target.Quad(min, min + new Vector3(w * px, 0f, d * px), color);
                // a rounded look: one grid unit more under the middle of a wide spot
                if (w == 3) target.Quad(min + new Vector3(px, 0f, -px), min + new Vector3(2f * px, 0f, 0f), soft);
                added = true;
            }
            return added;
        }

        private static int CellHash(Vector2Int cell, int salt) =>
            ((cell.x * 73856093) ^ (cell.y * 19349663) ^ (salt * 83492791)) & 0x7fffffff;

        private Renderer AddKomorebiObject(ChunkPayload payload, PocMeshBuilder mesh, string name)
        {
            if (mesh.TriangleCount == 0) return null;
            var go = mesh.CreateObject(name, payload.Root, catalog.VertexColorMaterial);
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            payload.Meshes.Add(go.GetComponent<MeshFilter>().sharedMesh);
            payload.Triangles += mesh.TriangleCount;
            return renderer;
        }

        private void ApplyKomorebi(ChunkPayload payload)
        {
            if (payload.Komorebi != null) payload.Komorebi.enabled = KomorebiVisible;
            if (payload.KomorebiA != null) payload.KomorebiA.enabled = KomorebiVisible && !komorebiFlicker;
            if (payload.KomorebiB != null) payload.KomorebiB.enabled = KomorebiVisible && komorebiFlicker;
        }

        private float NightBlend()
        {
            if (KomorebiNightForTest.HasValue) return KomorebiNightForTest.Value ? 1f : 0f;
            if (!timeOfDaySearched)
            {
                timeOfDay = GetComponent<PocTimeOfDay>();
                timeOfDaySearched = true;
            }
            return timeOfDay != null ? timeOfDay.NightBlend : 0f;
        }

        // Every frame in play (the checks call it): the light by day only, its flicker, the leaves.
        public void TickForest(float now)
        {
            var visible = NightBlend() < KomorebiNightLimit;
            var flip = now >= komorebiSwitchAt;
            if (flip)
            {
                komorebiSwitchAt = now + KomorebiFlickerSeconds;
                komorebiFlicker = !komorebiFlicker;
            }
            if (flip || visible != KomorebiVisible)
            {
                KomorebiVisible = visible;
                foreach (var payload in payloads.Values) ApplyKomorebi(payload);
            }
            if (leaves != null && leaves.Active)
            {
                var t = now - leaves.StartedAt;
                if (t >= LeafSeconds || leaves.Object == null) StopLeaves();
                else BuildLeafMesh(t);
            }
        }

        private void OnForestInteracted(SharedObjectInstance target, string message)
        {
            if (target == null || target.DefinitionId != StumpDefinitionId || session == null || session.World != session.OutdoorWorld) return;
            StartLeaves(target.RootCell, WaterClock());
        }

        // One burst of leaves from the stump on this world cell. False (nothing new) while one is in the air.
        public bool StartLeaves(Vector2Int stumpCell, float now)
        {
            if (leaves != null && leaves.Active)
            {
                LeafRetriggersIgnored++;
                return false;
            }
            if (leaves == null)
            {
                leaves = new LeafBurst { Mesh = new Mesh { name = "Leaves" } };
                leaves.Mesh.MarkDynamic();
            }
            // Made once; made again (with the same mesh) only when a map change took the old one away.
            if (leaves.Object == null)
            {
                leaves.Object = new GameObject("Leaves (きりかぶにふれる)");
                leaves.Object.AddComponent<MeshFilter>().sharedMesh = leaves.Mesh;
                var renderer = leaves.Object.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = catalog.VertexColorMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            leaves.Object.transform.SetParent(mapRoot != null ? mapRoot : transform, false);
            leaves.Object.SetActive(true);
            leaves.Origin = GridSpace3D.ToWorld(stumpCell) + new Vector3(0f, StumpTop, 0f);
            leaves.StartedAt = now;
            leaves.Active = true;
            var seed = LeafBurstsStarted * 7 + 3;
            for (var i = 0; i < LeafCount; i++)
            {
                var h = CellHash(stumpCell + new Vector2Int(i, seed), 0x6c1f);
                var a = (i + ((h & 15) / 16f) * 0.6f) * Mathf.PI * 2f / LeafCount;
                leaves.Direction[i] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                leaves.Rise[i] = 0.5f + ((h >> 4) & 15) / 15f * 0.45f;    // 0.5 .. 0.95 above the cut
                leaves.Reach[i] = 0.22f + ((h >> 8) & 15) / 15f * 0.3f;   // 0.22 .. 0.52 out
                leaves.Spin[i] = (((h >> 12) & 1) == 0 ? 1f : -1f) * (5f + ((h >> 13) & 7));
                leaves.Phase[i] = ((h >> 16) & 31) / 31f * Mathf.PI * 2f;
            }
            LeafBurstsStarted++;
            LeafHighest = LeafWidest = 0f;
            BuildLeafMesh(0f);
            return true;
        }

        private void StopLeaves()
        {
            if (leaves == null) return;
            leaves.Active = false;
            if (leaves.Mesh != null) leaves.Mesh.Clear();
            if (leaves.Object != null) leaves.Object.SetActive(false);
        }

        // Up fast (eased), then a slow fall with a sway; each leaf a small flat quad turning about its
        // own axis and rocking, drawn from both sides.
        private void BuildLeafMesh(float t)
        {
            leafVertices.Clear();
            leafColors.Clear();
            leafTriangles.Clear();
            leafNormals.Clear();
            var k = Mathf.Clamp01(t / LeafSeconds);
            for (var i = 0; i < LeafCount; i++)
            {
                const float up = 0.3f;   // share of the time spent rising
                float height;
                if (k < up)
                {
                    var r = k / up;
                    height = leaves.Rise[i] * (1f - (1f - r) * (1f - r));
                }
                else
                {
                    var f = (k - up) / (1f - up);
                    height = Mathf.Lerp(leaves.Rise[i], -StumpTop + 0.03f, f * f * 0.85f + f * 0.15f);
                }
                var spread = leaves.Reach[i] * (1f - Mathf.Pow(1f - k, 2.2f));
                var sway = Mathf.Sin(t * 5.5f + leaves.Phase[i]) * 0.05f * k;
                var dir = leaves.Direction[i];
                var side = new Vector3(-dir.z, 0f, dir.x);
                var centre = leaves.Origin + dir * spread + side * sway + Vector3.up * height;
                LeafHighest = Mathf.Max(LeafHighest, height);
                LeafWidest = Mathf.Max(LeafWidest, spread);
                var yaw = leaves.Phase[i] + leaves.Spin[i] * t;
                var rock = Mathf.Sin(t * 7f + leaves.Phase[i]) * 0.9f;   // tilt: the leaf flutters
                var u = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw)) * 0.07f;   // a leaf about 4 x 3 px in the 2D view
                var v = Quaternion.AngleAxis(rock * Mathf.Rad2Deg, u) * new Vector3(-Mathf.Sin(yaw), 0f, Mathf.Cos(yaw)) * 0.045f;
                var color = LeafColors[i % LeafColors.Length];
                var start = leafVertices.Count;
                leafVertices.Add(centre - u - v); leafVertices.Add(centre - u + v);
                leafVertices.Add(centre + u + v); leafVertices.Add(centre + u - v);
                for (var j = 0; j < 4; j++) { leafColors.Add(color); leafNormals.Add(Vector3.up); }
                // both faces
                leafTriangles.Add(start); leafTriangles.Add(start + 1); leafTriangles.Add(start + 2);
                leafTriangles.Add(start); leafTriangles.Add(start + 2); leafTriangles.Add(start + 3);
                leafTriangles.Add(start); leafTriangles.Add(start + 2); leafTriangles.Add(start + 1);
                leafTriangles.Add(start); leafTriangles.Add(start + 3); leafTriangles.Add(start + 2);
            }
            leaves.Mesh.Clear();
            leaves.Mesh.SetVertices(leafVertices);
            leaves.Mesh.SetColors(leafColors);
            leaves.Mesh.SetNormals(leafNormals);
            leaves.Mesh.SetTriangles(leafTriangles, 0);
            leaves.Mesh.RecalculateBounds();
        }

        private void ReleaseForest()
        {
            KomorebiCellsDrawn = 0;
            KomorebiChunks = 0;
            StopLeaves();
        }

        private void ReleaseChunkKomorebi(ChunkPayload payload)
        {
            if (payload.KomorebiCells == 0) return;
            KomorebiCellsDrawn -= payload.KomorebiCells;
            KomorebiChunks--;
        }
    }
}
