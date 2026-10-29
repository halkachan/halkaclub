using System.Collections.Generic;
using Halka.Game.Player;
using Halka.Poc2D3D.World;
using UnityEngine;

namespace Halka.Poc2D3D
{
    // ver3.6: water (the pond of the North area). A water cell is a blocking Surface ("water") of its
    // Map, so Collision, AUTO, the crow and a save treat it as any blocked cell; only the drawing is its
    // own. Per chunk: one flat low mesh just above the ground (deeper blue in the middle, lighter where
    // the bank is, a thin earthen rim on the bank side), and two small sets of glints that take turns
    // (a quiet shimmer, a renderer switched every GlintSeconds: no shader, no texture, no reflection).
    // The 2D view draws the same meshes from above, so both views show the same pond.
    // StartRipple: one ripple (two thin rings growing and fading into the water's colour), e.g. when
    // she looks into the water (PocGameplay); RippleSeconds long.
    public sealed partial class PocWorldPresenter
    {
        public const string WaterSurfaceId = "water";
        public const float WaterHeight = 0.012f;    // grass 0, road 0.004; the lily pads float at 0.014
        public const float WaterRimWidth = 0.035f;
        public const float GlintSeconds = 0.7f;
        public const float RippleSeconds = 1.4f;
        public const float RippleMaxRadius = 0.45f;
        private const int RippleSegments = 20;

        public static readonly Color WaterDeep = new Color32(0x3f, 0x6c, 0xa0, 0xff);
        public static readonly Color WaterShallow = new Color32(0x5c, 0x8d, 0xc2, 0xff);
        public static readonly Color WaterRim = new Color32(0x7c, 0x6c, 0x4c, 0xff);
        public static readonly Color WaterGlint = new Color32(0xd2, 0xe8, 0xf8, 0xff);

        private sealed class Ripple
        {
            public GameObject Object;
            public Mesh Mesh;
            public Vector3 Center;
            public float StartedAt;
        }

        private readonly List<Ripple> ripples = new List<Ripple>();
        private readonly List<Vector3> rippleVertices = new List<Vector3>();
        private readonly List<Color> rippleColors = new List<Color>();
        private readonly List<int> rippleTriangles = new List<int>();
        private readonly List<Vector3> rippleNormals = new List<Vector3>();
        private bool glintPhase;
        private float glintSwitchAt;

        public int WaterCellsDrawn { get; private set; }
        // The clock ripples run on (the game: Time.time; the checks run their own, as TickWater's).
        public System.Func<float> WaterClock { get; set; } = () => Time.time;
        public int RipplesStarted { get; private set; }
        public int ActiveRipples => ripples.Count;
        public float LastRippleRadius { get; private set; }

        private bool IsWater(Vector2Int cell)
        {
            var region = chunkIndex != null ? chunkIndex.RegionAt(cell) : null;
            var surface = region != null ? region.SurfaceAtWorld(cell) : null;
            return surface != null && surface.StableId == WaterSurfaceId;
        }

        private void BuildChunkWater(ChunkPayload payload, List<Vector2Int> cells)
        {
            if (cells.Count == 0) return;
            var water = new PocMeshBuilder();
            var glintsA = new PocMeshBuilder();
            var glintsB = new PocMeshBuilder();
            var half = GridSpace3D.CellSize * 0.5f;
            var sides = new[] { Vector2Int.left, Vector2Int.right, Vector2Int.down, Vector2Int.up };
            foreach (var cell in cells)
            {
                var c = GridSpace3D.ToWorld(cell);
                var bank = new bool[4];
                var nearBank = false;
                for (var i = 0; i < 4; i++) { bank[i] = !IsWater(cell + sides[i]); nearBank |= bank[i]; }
                var hash = (cell.x * 73856093) ^ (cell.y * 19349663);
                var shade = ((hash & 3) - 1.5f) * 0.01f;
                var color = nearBank ? WaterShallow : WaterDeep;
                color = new Color(color.r + shade, color.g + shade, color.b + shade, 1f);
                // The water's surface, drawn in from each bank side; the rim fills that strip.
                var min = new Vector3(c.x - half + (bank[0] ? WaterRimWidth : 0f), WaterHeight, c.z - half + (bank[2] ? WaterRimWidth : 0f));
                var max = new Vector3(c.x + half - (bank[1] ? WaterRimWidth : 0f), WaterHeight, c.z + half - (bank[3] ? WaterRimWidth : 0f));
                water.Quad(min, max, color);
                var rim = WaterHeight - 0.002f;
                if (bank[0]) water.Quad(new Vector3(c.x - half, rim, c.z - half), new Vector3(c.x - half + WaterRimWidth, rim, c.z + half), WaterRim);
                if (bank[1]) water.Quad(new Vector3(c.x + half - WaterRimWidth, rim, c.z - half), new Vector3(c.x + half, rim, c.z + half), WaterRim);
                if (bank[2]) water.Quad(new Vector3(c.x - half, rim, c.z - half), new Vector3(c.x + half, rim, c.z - half + WaterRimWidth), WaterRim);
                if (bank[3]) water.Quad(new Vector3(c.x - half, rim, c.z + half - WaterRimWidth), new Vector3(c.x + half, rim, c.z + half), WaterRim);
                // A short glint in most cells, in one of the two sets (they take turns).
                if ((hash & 7) < 6)
                {
                    var gx = c.x + (((hash >> 3) & 7) - 3.5f) * 0.035f;
                    var gz = c.z + (((hash >> 6) & 7) - 3.5f) * 0.03f;
                    var target = (hash & 1) == 0 ? glintsA : glintsB;
                    target.Quad(new Vector3(gx - 0.05f, WaterHeight + 0.001f, gz - 0.008f), new Vector3(gx + 0.05f, WaterHeight + 0.001f, gz + 0.008f), WaterGlint);
                }
            }
            AddWaterObject(payload, water, "Water " + payload.Coord);
            payload.GlintsA = AddWaterObject(payload, glintsA, "Water glints A " + payload.Coord);
            payload.GlintsB = AddWaterObject(payload, glintsB, "Water glints B " + payload.Coord);
            if (payload.GlintsB != null) payload.GlintsB.enabled = false;
            payload.WaterCells = cells.Count;
            WaterCellsDrawn += cells.Count;
        }

        private Renderer AddWaterObject(ChunkPayload payload, PocMeshBuilder mesh, string name)
        {
            if (mesh.TriangleCount == 0) return null;
            var go = mesh.CreateObject(name, payload.Root, catalog.VertexColorMaterial);
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            payload.Meshes.Add(go.GetComponent<MeshFilter>().sharedMesh);
            payload.Triangles += mesh.TriangleCount;
            return renderer;
        }

        // The glints take turns; ripples grow and fade. Every frame in play (the checks call it).
        public void TickWater(float now)
        {
            if (now >= glintSwitchAt)
            {
                glintSwitchAt = now + GlintSeconds;
                glintPhase = !glintPhase;
                foreach (var payload in payloads.Values)
                {
                    if (payload.GlintsA != null) payload.GlintsA.enabled = !glintPhase;
                    if (payload.GlintsB != null) payload.GlintsB.enabled = glintPhase;
                }
            }
            for (var i = ripples.Count - 1; i >= 0; i--)
            {
                var ripple = ripples[i];
                var t = now - ripple.StartedAt;
                if (t >= RippleSeconds || ripple.Object == null)
                {
                    FreeRipple(ripple);
                    ripples.RemoveAt(i);
                    continue;
                }
                BuildRippleMesh(ripple, t);
            }
        }

        // She looks into the water (the Action Point of the lily pads, みずをのぞく): a ripple there.
        public const string WaterLookDefinitionId = "water_lily";

        private void OnInteracted(SharedObjectInstance target, string message)
        {
            if (target == null || target.DefinitionId != WaterLookDefinitionId || session == null || session.World != session.OutdoorWorld) return;
            // One cell further out on the water when there is water there: the cell right in front of her
            // is hidden behind her figure in both views.
            var ahead = target.RootCell + session.Facing.ToVector();
            StartRipple(IsWater(ahead) ? ahead : target.RootCell, WaterClock());
        }

        // One ripple centred on this world cell (a water cell), drawn on the water.
        public bool StartRipple(Vector2Int cell, float now)
        {
            if (!IsWater(cell)) return false;
            var go = new GameObject("Ripple " + cell);
            go.transform.SetParent(mapRoot != null ? mapRoot : transform, false);
            var ripple = new Ripple { Object = go, Mesh = new Mesh { name = "Ripple" }, StartedAt = now,
                Center = GridSpace3D.ToWorld(cell) + new Vector3(0f, WaterHeight + 0.003f, 0f) };
            ripple.Mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = ripple.Mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = catalog.VertexColorMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ripples.Add(ripple);
            RipplesStarted++;
            BuildRippleMesh(ripple, 0f);
            return true;
        }

        // Two thin rings (the second 0.35 s later), growing and turning into the water's colour.
        private void BuildRippleMesh(Ripple ripple, float t)
        {
            rippleVertices.Clear();
            rippleColors.Clear();
            rippleTriangles.Clear();
            for (var ring = 0; ring < 2; ring++)
            {
                var k = Mathf.Clamp01((t - ring * 0.35f) / (RippleSeconds - 0.35f));
                if (k <= 0f || k >= 1f) continue;
                var eased = 1f - (1f - k) * (1f - k);
                var radius = Mathf.Lerp(0.04f, RippleMaxRadius, eased);
                if (ring == 0) LastRippleRadius = radius;
                var width = Mathf.Lerp(0.03f, 0.012f, k);
                var color = Color.Lerp(WaterGlint, WaterShallow, k);
                var start = rippleVertices.Count;
                for (var s = 0; s <= RippleSegments; s++)
                {
                    var a = s * Mathf.PI * 2f / RippleSegments;
                    var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a) * 0.85f);
                    rippleVertices.Add(ripple.Center + dir * (radius + width * 0.5f));
                    rippleVertices.Add(ripple.Center + dir * (radius - width * 0.5f));
                    rippleColors.Add(color);
                    rippleColors.Add(color);
                }
                for (var s = 0; s < RippleSegments; s++)
                {
                    var i = start + s * 2;
                    // Clockwise seen from above (PocMeshBuilder's front faces): outer, inner, next outer.
                    rippleTriangles.Add(i); rippleTriangles.Add(i + 1); rippleTriangles.Add(i + 2);
                    rippleTriangles.Add(i + 1); rippleTriangles.Add(i + 3); rippleTriangles.Add(i + 2);
                }
            }
            ripple.Mesh.Clear();
            ripple.Mesh.SetVertices(rippleVertices);
            ripple.Mesh.SetColors(rippleColors);
            rippleNormals.Clear();
            for (var i = 0; i < rippleVertices.Count; i++) rippleNormals.Add(Vector3.up);
            ripple.Mesh.SetNormals(rippleNormals);
            ripple.Mesh.SetTriangles(rippleTriangles, 0);
            ripple.Mesh.RecalculateBounds();
        }

        private static void FreeRipple(Ripple ripple)
        {
            if (ripple.Object != null)
            {
                if (Application.isPlaying) Destroy(ripple.Object);
                else DestroyImmediate(ripple.Object);
            }
            if (ripple.Mesh != null)
            {
                if (Application.isPlaying) Destroy(ripple.Mesh);
                else DestroyImmediate(ripple.Mesh);
            }
        }
    }
}
