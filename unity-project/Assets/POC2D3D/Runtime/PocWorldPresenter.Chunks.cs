using System;
using System.Collections.Generic;
using Halka.Game.World;
using Halka.Poc2D3D.World;
using UnityEngine;

namespace Halka.Poc2D3D
{
    // ver3.2: the outdoor world drawn chunk by chunk. The logical world (OutdoorWorldIndex: collision,
    // Objects, grass) is kept whole by the session; only what is drawn streams:
    //   - a chunk holds its ground, road, tufts, blocking-Surface walls and the Objects whose root
    //     cell lies in it (an Object that reaches into other chunks loads with them: ChunkStreamer);
    //   - a released chunk destroys its GameObjects and the Meshes it made (ground, tufts, overlays,
    //     the static-batch mesh); the catalog's models and materials are shared and kept;
    //   - the step gate (session.PrepareCell) builds a chunk at once if the prefetch was late, and a
    //     chunk that fails to build is drawn as plain ground, so a load can never shut her in.
    public sealed partial class PocWorldPresenter
    {
        private sealed class ChunkPayload
        {
            public ChunkCoord Coord;
            public Transform Root;
            public readonly List<PocObjectView> Views = new List<PocObjectView>();
            public readonly List<Mesh> Meshes = new List<Mesh>();
            public GameObject Ground;
            public GameObject Tufts;
            public GameObject Grid;
            public GameObject Border;
            public PocEnvironmentStats Stats;
            public int Triangles;
            public bool Fallback;
        }

        private bool chunkMode;
        private OutdoorWorldIndex chunkIndex;
        private ChunkStreamer streamer;
        private HashSet<Vector2Int> worldRoad;
        private int roadInside;
        private int roadOutside;
        private readonly Dictionary<ChunkCoord, ChunkPayload> payloads = new Dictionary<ChunkCoord, ChunkPayload>();
        private bool tuftsHidden;
        private bool chunkBordersVisible;
        private PocEnvironmentStats aggregated;
        private int aggregatedVersion = -1;
        private readonly Dictionary<string, (bool ok, Vector3 offset)> perchOffsets = new Dictionary<string, (bool, Vector3)>();

        public bool ChunkMode => chunkMode;
        public ChunkStreamer Streamer => streamer;
        public OutdoorWorldIndex ChunkIndex => chunkIndex;
        public int ChunkVersion => streamer != null ? streamer.Version : 0;
        public int LoadedChunkObjects { get; private set; }
        // The cells the 2D view shows around her (PocPixelView2D sets it from its target size) and
        // how far the 3D camera may see: the streamer loads both, so a view switch never waits.
        public Vector2Int ViewHalfCells2D { get; set; } = new Vector2Int(20, 11);
        public int ReachCells3D { get; set; } = 40;
        public float ChunkBudgetMs { get; set; } = 4f;
        public event Action ChunksChanged;

        public bool ChunkBordersVisible
        {
            get => chunkBordersVisible;
            set { chunkBordersVisible = value; ApplyOverlayVisibility(); }
        }

        public IEnumerable<ChunkCoord> ReadyChunks()
        {
            foreach (var coord in payloads.Keys) yield return coord;
        }

        public bool IsChunkDrawn(ChunkCoord coord) => payloads.ContainsKey(coord);

        // The 3D tufts belong to the 3D view; the 2D view hides them (it draws the 2D game's grass).
        public void SetTuftsHidden(bool hidden)
        {
            tuftsHidden = hidden;
            foreach (var payload in payloads.Values)
            {
                var renderer = payload.Tufts != null ? payload.Tufts.GetComponent<Renderer>() : null;
                if (renderer != null) renderer.enabled = !hidden;
            }
        }

        private void BuildChunkedWorld(SharedWorldModel world)
        {
            chunkMode = true;
            chunkIndex = world.OutdoorIndex;
            worldRoad = PocEnvironmentBuilder.CollectWorldRoad(chunkIndex, catalog.Environment.outsideMargin, out roadInside, out roadOutside);
            streamer = new ChunkStreamer(chunkIndex, new Builder(this));
            gridOverlay = null;
            BuiltMapId = world.MapId;
            if (session != null)
            {
                session.PrepareCell = cell => streamer == null || streamer.EnsureReady(chunkIndex.ChunkOfCell(cell), Now());
                session.CellReady = cell => streamer == null || streamer.IsCellReady(cell);
            }
            UpdateStreaming(true);
        }

        private static double Now() => Time.realtimeSinceStartupAsDouble;

        private void Update()
        {
            if (chunkMode && Application.isPlaying) UpdateStreaming(false);
        }

        // Where she is and where she is going decide what is drawn. everythingNow builds every
        // required chunk at once (a map change, the checks); otherwise within ChunkBudgetMs.
        public void UpdateStreaming(bool everythingNow)
        {
            if (!chunkMode || streamer == null || session == null || session.World == null || !session.World.IsOutdoorWorld) return;
            var focus = session.Cell;
            var moving = session.IsMoving ? session.StepToCell - session.StepFromCell : Vector2Int.zero;
            var half = new Vector2Int(Mathf.Max(ViewHalfCells2D.x, ReachCells3D), Mathf.Max(ViewHalfCells2D.y, ReachCells3D));
            streamer.SetFocus(focus, new CellRect(focus - half, focus + half), moving);
            if (everythingNow) streamer.LoadAllRequiredNow(Now());
            else streamer.Update(ChunkBudgetMs, Now());
        }

        private void ReleaseChunks()
        {
            if (streamer != null) streamer.ReleaseAll();
            foreach (var payload in new List<ChunkPayload>(payloads.Values)) FreePayload(payload);
            payloads.Clear();
            streamer = null;
            chunkMode = false;
            chunkIndex = null;
            aggregatedVersion = -1;
        }

        private GameObject ChunkPart(bool ground)
        {
            ChunkPayload payload = null;
            if (session != null && chunkIndex != null) payloads.TryGetValue(chunkIndex.ChunkOfCell(session.Cell), out payload);
            if (payload == null) foreach (var item in payloads.Values) { payload = item; break; }
            return payload == null ? null : ground ? payload.Ground : payload.Tufts;
        }

        // The ground statistics of every drawn chunk added up (the ver3.1 numbers of the whole map
        // when every chunk of the field is drawn); road totals are the world's.
        private PocEnvironmentStats ChunkEnvironment()
        {
            if (aggregated != null && aggregatedVersion == ChunkVersion) return aggregated;
            var total = new PocEnvironmentStats { MapId = chunkIndex != null ? chunkIndex.PrimaryRegion.MapId : null };
            foreach (var payload in payloads.Values)
            {
                var s = payload.Stats;
                if (s == null) continue;
                total.GrassTiles += s.GrassTiles;
                total.RoadFull += s.RoadFull;
                total.RoadEdge += s.RoadEdge;
                total.RoadCorner += s.RoadCorner;
                total.Tufts += s.Tufts;
                total.RoadsideTufts += s.RoadsideTufts;
                total.GroundTriangles += s.GroundTriangles;
                total.TuftTriangles += s.TuftTriangles;
                foreach (var cell in s.RoadCellSet) total.RoadCellSet.Add(cell);
                total.TuftPositions.AddRange(s.TuftPositions);
            }
            total.RoadCells = roadInside;
            total.RoadCellsOutside = roadOutside;
            aggregated = total;
            aggregatedVersion = ChunkVersion;
            return total;
        }

        private ChunkPayload BuildChunkPayload(ChunkCoord coord, bool fallback)
        {
            var chunk = chunkIndex.ChunkAt(coord);
            var root = new GameObject("Chunk " + coord + (fallback ? " (fallback)" : "")).transform;
            root.SetParent(mapRoot, false);
            var payload = new ChunkPayload { Coord = coord, Root = root, Fallback = fallback };
            try
            {
                if (fallback) BuildFallbackGround(chunk, payload);
                else
                {
                    payload.Stats = PocEnvironmentBuilder.BuildChunk(chunkIndex, chunk, worldRoad, catalog.Environment,
                        catalog.VertexColorMaterial, root, out var ground, out var tufts);
                    payload.Ground = ground;
                    payload.Tufts = tufts;
                    payload.Meshes.Add(ground.GetComponent<MeshFilter>().sharedMesh);
                    payload.Meshes.Add(tufts.GetComponent<MeshFilter>().sharedMesh);
                    PocOutlineTag.ApplyInPlace(tufts.GetComponent<MeshFilter>().sharedMesh, PocOutlineTag.DecorationId, PocOutlineTag.MarkerStrength);
                    tufts.GetComponent<Renderer>().enabled = !tuftsHidden;
                    payload.Triangles += payload.Stats.GroundTriangles + payload.Stats.TuftTriangles;
                }
                BuildChunkWalls(chunk, payload);
                foreach (var instance in chunk.OwnedObjects)
                {
                    try
                    {
                        var view = BuildObject(instance, root, instance.OrderInMap, out var triangles);
                        payload.Views.Add(view);
                        payload.Triangles += triangles;
                    }
                    catch (Exception exception) when (fallback)
                    {
                        Debug.LogWarning("HARUKA fallback chunk " + coord + ": " + instance.InstanceId + " not drawn: " + exception.Message);
                    }
                }
                BuildChunkOverlays(chunk, payload);
                if (Application.isPlaying && !fallback)
                {
                    StaticBatchingUtility.Combine(root.gameObject);
                    foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                        if (filter.sharedMesh != null && filter.sharedMesh.name.StartsWith("Combined Mesh", StringComparison.Ordinal) &&
                            !payload.Meshes.Contains(filter.sharedMesh)) payload.Meshes.Add(filter.sharedMesh);
                }
            }
            catch
            {
                FreePayload(payload);
                throw;
            }
            views.AddRange(payload.Views);
            payloads[coord] = payload;
            BuiltTriangles += payload.Triangles;
            LoadedChunkObjects += payload.Views.Count;
            ChunksChanged?.Invoke();
            return payload;
        }

        private void ReleaseChunkPayload(object item)
        {
            if (!(item is ChunkPayload payload)) return;
            foreach (var view in payload.Views) views.Remove(view);
            payloads.Remove(payload.Coord);
            BuiltTriangles -= payload.Triangles;
            LoadedChunkObjects -= payload.Views.Count;
            FreePayload(payload);
            ChunksChanged?.Invoke();
        }

        private static void FreePayload(ChunkPayload payload)
        {
            if (payload.Root != null)
            {
                if (Application.isPlaying) Destroy(payload.Root.gameObject);
                else DestroyImmediate(payload.Root.gameObject);
            }
            foreach (var mesh in payload.Meshes)
                if (mesh != null)
                {
                    if (Application.isPlaying) Destroy(mesh);
                    else DestroyImmediate(mesh);
                }
            payload.Meshes.Clear();
        }

        // A chunk that failed to build: flat ground in the Surface colours (no Blender pieces), so the
        // place stays visible and walkable as the logical world says.
        private void BuildFallbackGround(ChunkStatic chunk, ChunkPayload payload)
        {
            var mesh = new PocMeshBuilder();
            var half = GridSpace3D.CellSize * 0.5f;
            for (var y = chunk.Rect.Min.y; y <= chunk.Rect.Max.y; y++)
            for (var x = chunk.Rect.Min.x; x <= chunk.Rect.Max.x; x++)
            {
                var cell = new Vector2Int(x, y);
                var region = chunkIndex.RegionAt(cell);
                if (region == null && chunkIndex.DecorationRegionAt(cell) == null) continue;
                var surface = chunkIndex.SurfaceAt(cell);
                var color = SurfaceColor(surface != null ? surface.StableId : "base_ground", cell);
                if (region == null) color = Color.Lerp(color, new Color(0.36f, 0.5f, 0.3f), 0.45f);
                var center = GridSpace3D.ToWorld(cell);
                mesh.Quad(center + new Vector3(-half, 0f, -half), center + new Vector3(half, 0f, half), color);
            }
            payload.Ground = mesh.CreateObject("Ground " + chunk.Coord + " (fallback)", payload.Root, catalog.VertexColorMaterial);
            payload.Ground.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            payload.Meshes.Add(payload.Ground.GetComponent<MeshFilter>().sharedMesh);
            payload.Stats = new PocEnvironmentStats();
            payload.Triangles += mesh.TriangleCount;
        }

        // Blocking Surfaces (walls) outdoors: one box per cell, as BuildWalls does for a whole map.
        private void BuildChunkWalls(ChunkStatic chunk, ChunkPayload payload)
        {
            if (chunk.RegionCells == 0) return;
            for (var y = chunk.Rect.Min.y; y <= chunk.Rect.Max.y; y++)
            for (var x = chunk.Rect.Min.x; x <= chunk.Rect.Max.x; x++)
            {
                var cell = new Vector2Int(x, y);
                var region = chunkIndex.RegionAt(cell);
                if (region == null || (region.FlagsAtWorld(cell) & OutdoorRegion.CellFlags.BlockingSurface) == 0) continue;
                var surface = region.SurfaceAtWorld(cell);
                var mesh = new PocMeshBuilder();
                var center = GridSpace3D.ToWorld(cell);
                var half = GridSpace3D.CellSize * 0.5f;
                var color = SurfaceColor(surface.StableId, cell);
                mesh.Box(center + new Vector3(-half, 0f, -half), center + new Vector3(half, WallHeight, half), color,
                    Color.Lerp(color, Color.white, 0.18f));
                var wall = mesh.CreateObject($"Wall {x},{y}", payload.Root, catalog.VertexColorMaterial);
                payload.Meshes.Add(wall.GetComponent<MeshFilter>().sharedMesh);
                var view = wall.AddComponent<PocObjectView>();
                view.InstanceId = $"wall_{x}_{y}";
                view.DefinitionId = surface.StableId;
                view.RootCell = cell;
                view.CameraOccluder = true;
                view.Renderers = wall.GetComponentsInChildren<Renderer>();
                payload.Views.Add(view);
                payload.Triangles += mesh.TriangleCount;
            }
        }

        // Debug only (GridVisible, ChunkBordersVisible): the cell grid with blocked cells crossed,
        // and the chunk's border with its coordinates' colour. Hidden unless switched on.
        private void BuildChunkOverlays(ChunkStatic chunk, ChunkPayload payload)
        {
            var half = GridSpace3D.CellSize * 0.5f;
            const float lift = 0.012f;
            var r = chunk.Rect;
            var lineColor = new Color(1f, 1f, 1f, 0.55f);
            var blockedColor = new Color(1f, 0.15f, 0.1f, 1f);
            payload.Grid = LineObject("Grid overlay " + chunk.Coord + " (debug)", payload, (add) =>
            {
                for (var x = r.Min.x; x <= r.Max.x + 1; x++)
                    add(new Vector3(x * GridSpace3D.CellSize - half, lift, r.Min.y * GridSpace3D.CellSize - half),
                        new Vector3(x * GridSpace3D.CellSize - half, lift, r.Max.y * GridSpace3D.CellSize + half), lineColor);
                for (var y = r.Min.y; y <= r.Max.y + 1; y++)
                    add(new Vector3(r.Min.x * GridSpace3D.CellSize - half, lift, y * GridSpace3D.CellSize - half),
                        new Vector3(r.Max.x * GridSpace3D.CellSize + half, lift, y * GridSpace3D.CellSize - half), lineColor);
                for (var y = r.Min.y; y <= r.Max.y; y++)
                for (var x = r.Min.x; x <= r.Max.x; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (!chunkIndex.Contains(cell) || !chunkIndex.IsBlocked(cell)) continue;
                    var c = GridSpace3D.ToWorld(cell, lift);
                    var d = half * 0.7f;
                    add(c + new Vector3(-d, 0f, -d), c + new Vector3(d, 0f, d), blockedColor);
                    add(c + new Vector3(-d, 0f, d), c + new Vector3(d, 0f, -d), blockedColor);
                }
            });
            var hue = Mathf.Repeat((chunk.Coord.X * 0.37f + chunk.Coord.Y * 0.61f), 1f);
            var borderColor = Color.HSVToRGB(hue, 0.85f, 1f);
            payload.Border = LineObject("Chunk border " + chunk.Coord + " (debug)", payload, (add) =>
            {
                var x0 = r.Min.x * GridSpace3D.CellSize - half + 0.02f;
                var x1 = r.Max.x * GridSpace3D.CellSize + half - 0.02f;
                var z0 = r.Min.y * GridSpace3D.CellSize - half + 0.02f;
                var z1 = r.Max.y * GridSpace3D.CellSize + half - 0.02f;
                for (var k = 0; k < 3; k++)
                {
                    var h = lift + 0.01f + k * 0.01f;
                    add(new Vector3(x0, h, z0), new Vector3(x1, h, z0), borderColor);
                    add(new Vector3(x1, h, z0), new Vector3(x1, h, z1), borderColor);
                    add(new Vector3(x1, h, z1), new Vector3(x0, h, z1), borderColor);
                    add(new Vector3(x0, h, z1), new Vector3(x0, h, z0), borderColor);
                }
            });
            ApplyOverlayVisibility(payload);
        }

        private GameObject LineObject(string name, ChunkPayload payload, Action<Action<Vector3, Vector3, Color>> fill)
        {
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var lines = new List<int>();
            fill((a, b, color) =>
            {
                lines.Add(vertices.Count); vertices.Add(a); colors.Add(color);
                lines.Add(vertices.Count); vertices.Add(b); colors.Add(color);
            });
            var mesh = new Mesh { name = name };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetIndices(lines, MeshTopology.Lines, 0);
            payload.Meshes.Add(mesh);
            var go = new GameObject(name);
            go.transform.SetParent(payload.Root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = catalog.OverlayMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }

        private void ApplyOverlayVisibility()
        {
            foreach (var payload in payloads.Values) ApplyOverlayVisibility(payload);
        }

        private void ApplyOverlayVisibility(ChunkPayload payload)
        {
            if (payload.Grid != null) payload.Grid.SetActive(gridVisible);
            if (payload.Border != null) payload.Border.SetActive(chunkBordersVisible);
        }

        // ver3.2: where a crow perches on a tree, from the Object alone (the drawn tree's PerchAnchor
        // when its chunk is drawn, else the same point computed from the model): a tree in a released
        // chunk is still a valid flight target, and the crow never loses its state with a chunk.
        public bool TryGetPerchPosition(SharedObjectInstance instance, out Vector3 position)
        {
            position = default;
            if (instance == null) return false;
            var view = FindView(instance.InstanceId);
            if (view != null && view.PerchAnchor != null && view.gameObject.activeInHierarchy)
            {
                position = view.PerchAnchor.position;
                return true;
            }
            if (!perchOffsets.TryGetValue(instance.DefinitionId, out var cached))
            {
                cached = (false, Vector3.zero);
                var binding = catalog != null ? catalog.Find(instance.DefinitionId) : null;
                if (binding != null && !string.IsNullOrEmpty(binding.perchAnchorName))
                {
                    var model = binding.Pick(instance.InstanceId);
                    var anchor = FindChild(model.transform, binding.perchAnchorName);
                    if (anchor != null)
                    {
                        var local = model.transform.InverseTransformPoint(anchor.position);
                        cached = (true, binding.meshOffset + Quaternion.Euler(0f, binding.yawDegrees, 0f) * (local * binding.uniformScale));
                    }
                }
                perchOffsets[instance.DefinitionId] = cached;
            }
            if (!cached.ok) return false;
            position = GridSpace3D.ToWorld(instance.RootCell) + cached.offset;
            return true;
        }

        private sealed class Builder : IChunkBuilder
        {
            private readonly PocWorldPresenter owner;
            public Builder(PocWorldPresenter presenter) => owner = presenter;
            public object Build(ChunkCoord coord) => owner.BuildChunkPayload(coord, false);
            public object BuildFallback(ChunkCoord coord) => owner.BuildChunkPayload(coord, true);
            public void Release(ChunkCoord coord, object payload) => owner.ReleaseChunkPayload(payload);
        }
    }
}
