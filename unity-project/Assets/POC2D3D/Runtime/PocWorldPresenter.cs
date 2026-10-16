using System.Collections.Generic;
using Halka.Game.World;
using UnityEngine;

namespace Halka.Poc2D3D
{
    // One instantiated view of an Object Instance. Both cameras render this same GameObject.
    public sealed class PocObjectView : MonoBehaviour
    {
        public string InstanceId;
        public string DefinitionId;
        public Vector2Int RootCell;
        public bool IsFallback;
        public bool CameraOccluder;
        public bool CastShadows = true;
        public bool FadeInFront2D;
        public string ModelName;
        public bool Placeholder;
        public Transform PerchAnchor;
        public Renderer[] Renderers;
        // Phase 3: interior wall sides (cut away on the camera side) and part-wise occlusion.
        public bool IsWall;
        public Vector2 WallOutward;
        public Renderer[] CutawayRenderers;
        public bool PartialOcclusion;
        public float MinPickSize;
        public bool CutAway { get; private set; }

        private readonly Dictionary<Renderer, Vector3[]> triangles = new Dictionary<Renderer, Vector3[]>();

        // Bounds test (fast, generous): occluders and clicks. Renderers switched off by a cutaway
        // or the 2D see-through layer never block. forPick enlarges tiny things to MinPickSize.
        public bool TryRaycast(Ray ray, out float distance, bool forPick = false)
        {
            distance = float.MaxValue;
            var hit = false;
            foreach (var item in Renderers)
            {
                if (item == null || !item.enabled || item.forceRenderingOff) continue;
                var bounds = item.bounds;
                if (forPick && MinPickSize > 0f)
                    bounds.size = Vector3.Max(bounds.size, Vector3.one * MinPickSize);
                if (bounds.IntersectRay(ray, out var d) && d < distance)
                {
                    distance = d;
                    hit = true;
                }
            }
            return hit;
        }

        // Clicks: exact triangles for part-wise objects (the house, whose box also holds the space
        // in front of it); otherwise the (enlarged) bounds, ignoring a ray that starts inside them.
        public bool TryPick(Ray ray, out float distance)
        {
            distance = float.MaxValue;
            var hit = false;
            if (PartialOcclusion)
            {
                foreach (var part in Renderers)
                    if (!HiddenForCamera(part) && RaycastPart(part, ray, out var d) && d < distance)
                    {
                        distance = d;
                        hit = true;
                    }
                return hit;
            }
            foreach (var item in Renderers)
            {
                if (item == null || !item.enabled || item.forceRenderingOff || HiddenForCamera(item)) continue;
                var bounds = item.bounds;
                if (MinPickSize > 0f) bounds.size = Vector3.Max(bounds.size, Vector3.one * MinPickSize);
                if (bounds.Contains(ray.origin)) continue;
                if (bounds.IntersectRay(ray, out var d) && d < distance)
                {
                    distance = d;
                    hit = true;
                }
            }
            return hit;
        }

        // A part the 3D camera has hidden (only its shadow is drawn) does not stop a click.
        private static bool HiddenForCamera(Renderer item) =>
            item.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;

        // Exact triangle test for one part (the house roof, walls, door). The world-space
        // triangles are cached when the view is built, before static batching merges meshes.
        public void CacheTriangles()
        {
            triangles.Clear();
            foreach (var item in Renderers)
            {
                var filter = item != null ? item.GetComponent<MeshFilter>() : null;
                if (filter == null || filter.sharedMesh == null || !filter.sharedMesh.isReadable) continue;
                var mesh = filter.sharedMesh;
                var local = mesh.vertices;
                var indices = mesh.triangles;
                var world = new Vector3[indices.Length];
                var matrix = item.transform.localToWorldMatrix;
                for (var i = 0; i < indices.Length; i++) world[i] = matrix.MultiplyPoint3x4(local[indices[i]]);
                triangles[item] = world;
            }
        }

        public bool RaycastPart(Renderer part, Ray ray, out float distance)
        {
            distance = float.MaxValue;
            if (part == null || part.forceRenderingOff) return false;
            if (!triangles.TryGetValue(part, out var world)) return part.bounds.IntersectRay(ray, out distance);
            var hit = false;
            for (var i = 0; i + 2 < world.Length; i += 3)
                if (PocMeshRay.Intersect(ray, world[i], world[i + 1], world[i + 2], out var d) && d < distance)
                {
                    distance = d;
                    hit = true;
                }
            return hit;
        }

        public void SetCutaway(bool cut)
        {
            CutAway = cut && CutawayRenderers != null && CutawayRenderers.Length > 0;
            if (CutawayRenderers == null) return;
            foreach (var item in CutawayRenderers)
                if (item != null) item.forceRenderingOff = cut;
        }

        public void SetRendererHidden(Renderer item, bool hidden)
        {
            if (item == null) return;
            item.shadowCastingMode = hidden
                ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly
                : CastShadows ? UnityEngine.Rendering.ShadowCastingMode.On
                : UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        public void SetHidden(bool hidden)
        {
            foreach (var item in Renderers)
                if (item != null)
                    item.shadowCastingMode = hidden
                        ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly
                        : CastShadows ? UnityEngine.Rendering.ShadowCastingMode.On
                        : UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    // Moller-Trumbore ray / triangle test (both faces), for exact part-wise occlusion without
    // a physics module (the project ships with 2D physics only).
    public static class PocMeshRay
    {
        public static bool Intersect(Ray ray, Vector3 a, Vector3 b, Vector3 c, out float distance)
        {
            distance = 0f;
            var e1 = b - a;
            var e2 = c - a;
            var p = Vector3.Cross(ray.direction, e2);
            var det = Vector3.Dot(e1, p);
            if (Mathf.Abs(det) < 1e-8f) return false;
            var inv = 1f / det;
            var t = ray.origin - a;
            var u = Vector3.Dot(t, p) * inv;
            if (u < 0f || u > 1f) return false;
            var q = Vector3.Cross(t, e1);
            var v = Vector3.Dot(ray.direction, q) * inv;
            if (v < 0f || u + v > 1f) return false;
            distance = Vector3.Dot(e2, q) * inv;
            return distance > 0f;
        }
    }

    // Builds the 3D presentation of the active map from shared data. Nothing here decides
    // movement; a rebuilt presentation is a pure function of the SharedWorldModel.
    public sealed class PocWorldPresenter : MonoBehaviour
    {
        public const float WallHeight = 1.1f;
        private const int OutdoorMargin = 8;

        [SerializeField] private SharedWorldSession session;
        [SerializeField] private PocPresentationCatalog catalog;

        private readonly List<PocObjectView> views = new List<PocObjectView>();
        private Transform mapRoot;
        private GameObject gridOverlay;
        private bool gridVisible;

        public IReadOnlyList<PocObjectView> Views => views;
        public int BuiltTriangles { get; private set; }
        public string BuiltMapId { get; private set; }
        public PocEnvironmentStats Environment { get; private set; }
        public PocInteriorStats Interior { get; private set; }
        public GameObject GroundObject { get; private set; }
        public GameObject TuftObject { get; private set; }

        public bool GridVisible
        {
            get => gridVisible;
            set { gridVisible = value; if (gridOverlay != null) gridOverlay.SetActive(value); }
        }

        public void Configure(SharedWorldSession worldSession, PocPresentationCatalog presentation)
        {
            session = worldSession;
            catalog = presentation;
        }

        private void OnEnable()
        {
            if (session != null) session.MapChanged += Rebuild;
        }

        private void OnDisable()
        {
            if (session != null) session.MapChanged -= Rebuild;
        }

        private void Start()
        {
            session.Initialize();
            Rebuild(session.World);
        }

        public void Rebuild(SharedWorldModel world)
        {
            if (mapRoot != null)
            {
                if (Application.isPlaying) Destroy(mapRoot.gameObject);
                else DestroyImmediate(mapRoot.gameObject);
            }
            views.Clear();
            BuiltTriangles = 0;
            mapRoot = new GameObject("Map 3D - " + world.MapId).transform;
            mapRoot.SetParent(transform, false);
            BuildGround(world);
            foreach (var instance in world.Objects) views.Add(BuildObject(instance));
            gridOverlay = BuildGridOverlay(world);
            gridOverlay.SetActive(gridVisible);
            BuiltMapId = world.MapId;
            // Runtime static batching of the freshly built objects (one shared material), redone on
            // every rebuild so Map edits still move things. Edit Mode checks keep the meshes as is.
            if (Application.isPlaying) StaticBatchingUtility.Combine(mapRoot.gameObject);
        }

        public PocObjectView FindView(string instanceId)
        {
            foreach (var view in views)
                if (view.InstanceId == instanceId) return view;
            return null;
        }

        private void BuildGround(SharedWorldModel world)
        {
            Environment = null;
            Interior = null;
            GroundObject = TuftObject = null;
            var dressing = catalog.Environment;
            if (!world.IsInterior && dressing != null && dressing.IsComplete)
            {
                Environment = PocEnvironmentBuilder.Build(world, dressing, catalog.VertexColorMaterial, mapRoot,
                    out var dressedGround, out var tufts);
                GroundObject = dressedGround;
                TuftObject = tufts;
                // Grass gets the global grass outline setting (marker strength), never a fixed one.
                PocOutlineTag.ApplyInPlace(tufts.GetComponent<MeshFilter>().sharedMesh,
                    PocOutlineTag.DecorationId, PocOutlineTag.MarkerStrength);
                BuiltTriangles += Environment.GroundTriangles + Environment.TuftTriangles;
                BuildWalls(world);
                return;
            }
            var interior = catalog.Interior;
            if (world.IsInterior && interior != null && interior.IsComplete)
            {
                Interior = PocInteriorBuilder.Build(world, interior, catalog.VertexColorMaterial, mapRoot, views);
                BuiltTriangles += Interior.Triangles;
                return;
            }
            // Fallback: the Phase 1 generated floor and wall boxes.
            var mesh = new PocMeshBuilder();
            var map = world.Map;
            var margin = world.IsInterior ? 0 : OutdoorMargin;
            for (var y = map.MinCell.y - margin; y <= map.MaxCell.y + margin; y++)
            for (var x = map.MinCell.x - margin; x <= map.MaxCell.x + margin; x++)
            {
                var cell = new Vector2Int(x, y);
                var inside = map.Contains(cell);
                var surface = inside ? map.SurfaceAt(cell) : null;
                var surfaceId = surface != null ? surface.StableId :
                    map.BaseSurface != null ? map.BaseSurface.StableId : "base_ground";
                var color = SurfaceColor(surfaceId, cell);
                if (!inside) color = Color.Lerp(color, new Color(0.36f, 0.5f, 0.3f), 0.45f);
                var center = GridSpace3D.ToWorld(cell);
                var half = GridSpace3D.CellSize * 0.5f;
                if (surface != null && surface.BlocksMovement) continue;
                mesh.Quad(center + new Vector3(-half, 0f, -half), center + new Vector3(half, 0f, half), color);
            }
            BuiltTriangles += mesh.TriangleCount;
            var ground = mesh.CreateObject("Ground " + world.MapId, mapRoot, catalog.VertexColorMaterial);
            ground.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            BuildWalls(world);
        }

        // Blocking surfaces (the house walls) become one box per cell, so the 3D camera can
        // hide only the wall cells that stand between it and the player.
        private void BuildWalls(SharedWorldModel world)
        {
            var map = world.Map;
            for (var y = map.MinCell.y; y <= map.MaxCell.y; y++)
            for (var x = map.MinCell.x; x <= map.MaxCell.x; x++)
            {
                var cell = new Vector2Int(x, y);
                var surface = map.SurfaceAt(cell);
                if (surface == null || !surface.BlocksMovement) continue;
                var mesh = new PocMeshBuilder();
                var center = GridSpace3D.ToWorld(cell);
                var half = GridSpace3D.CellSize * 0.5f;
                var color = SurfaceColor(surface.StableId, cell);
                mesh.Box(center + new Vector3(-half, 0f, -half), center + new Vector3(half, WallHeight, half), color,
                    Color.Lerp(color, Color.white, 0.18f));
                var wall = mesh.CreateObject($"Wall {x},{y}", mapRoot, catalog.VertexColorMaterial);
                var view = wall.AddComponent<PocObjectView>();
                view.InstanceId = $"wall_{x}_{y}";
                view.DefinitionId = surface.StableId;
                view.RootCell = cell;
                view.CameraOccluder = true;
                view.Renderers = wall.GetComponentsInChildren<Renderer>();
                views.Add(view);
                BuiltTriangles += mesh.TriangleCount;
            }
        }

        private PocObjectView BuildObject(SharedObjectInstance instance)
        {
            var binding = catalog.Find(instance.DefinitionId);
            GameObject root;
            if (binding != null)
            {
                root = new GameObject(instance.DefinitionId + " " + instance.InstanceId);
                root.transform.SetParent(mapRoot, false);
                var source = binding.Pick(instance.InstanceId);
                var model = Instantiate(source, root.transform, false);
                model.name = source.name;
                model.transform.localPosition = binding.meshOffset;
                model.transform.localRotation = Quaternion.Euler(0f, binding.yawDegrees, 0f);
                model.transform.localScale = Vector3.one * binding.uniformScale;
                foreach (var filter in model.GetComponentsInChildren<MeshFilter>())
                    BuiltTriangles += filter.sharedMesh.triangles.Length / 3;
            }
            else
            {
                var mesh = new PocMeshBuilder();
                var cells = new List<Vector2Int>(instance.BlockedCells());
                if (cells.Count == 0) cells.Add(instance.RootCell);
                var color = FallbackColor(instance.DefinitionId);
                var height = FallbackHeight(instance.Definition);
                var inset = 0.06f;
                foreach (var cell in cells)
                {
                    var c = GridSpace3D.ToWorld(cell) - GridSpace3D.ToWorld(instance.RootCell);
                    var half = GridSpace3D.CellSize * 0.5f - inset;
                    mesh.Box(c + new Vector3(-half, 0f, -half), c + new Vector3(half, height, half), color,
                        Color.Lerp(color, Color.white, 0.25f));
                }
                BuiltTriangles += mesh.TriangleCount;
                root = mesh.CreateObject(instance.DefinitionId + " " + instance.InstanceId + " (fallback)",
                    mapRoot, catalog.VertexColorMaterial);
            }
            root.transform.position = GridSpace3D.ToWorld(instance.RootCell);
            // Object IDs 1..249 for the 2D outline; the order follows the Map's object list.
            PocOutlineTag.Apply(root, views.Count % 249 + 1, binding != null ? binding.outlineStrength : 1f);
            var view = root.AddComponent<PocObjectView>();
            view.InstanceId = instance.InstanceId;
            view.DefinitionId = instance.DefinitionId;
            view.RootCell = instance.RootCell;
            view.IsFallback = binding == null;
            view.CameraOccluder = binding == null || binding.cameraOccluder;
            view.CastShadows = binding == null || binding.castShadows;
            view.FadeInFront2D = binding != null && binding.fadeInFront2D;
            view.PartialOcclusion = binding != null && binding.partialOcclusion;
            view.MinPickSize = binding != null ? binding.minPickSize : 0f;
            view.Placeholder = binding == null || binding.placeholder;
            view.ModelName = binding != null ? binding.Pick(instance.InstanceId).name : null;
            view.Renderers = root.GetComponentsInChildren<Renderer>();
            view.SetHidden(false);
            if (view.PartialOcclusion) view.CacheTriangles();
            if (binding != null && !string.IsNullOrEmpty(binding.perchAnchorName))
                view.PerchAnchor = FindChild(root.transform, binding.perchAnchorName);
            return view;
        }

        private static Transform FindChild(Transform parent, string name)
        {
            foreach (var child in parent.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child;
            return null;
        }

        private GameObject BuildGridOverlay(SharedWorldModel world)
        {
            var map = world.Map;
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var lines = new List<int>();
            var lineColor = new Color(1f, 1f, 1f, 0.55f);
            var half = GridSpace3D.CellSize * 0.5f;
            const float lift = 0.012f;
            for (var x = map.MinCell.x; x <= map.MaxCell.x + 1; x++)
                AddLine(new Vector3(x * GridSpace3D.CellSize - half, lift, map.MinCell.y * GridSpace3D.CellSize - half),
                    new Vector3(x * GridSpace3D.CellSize - half, lift, map.MaxCell.y * GridSpace3D.CellSize + half), lineColor);
            for (var y = map.MinCell.y; y <= map.MaxCell.y + 1; y++)
                AddLine(new Vector3(map.MinCell.x * GridSpace3D.CellSize - half, lift, y * GridSpace3D.CellSize - half),
                    new Vector3(map.MaxCell.x * GridSpace3D.CellSize + half, lift, y * GridSpace3D.CellSize - half), lineColor);
            var blockedColor = new Color(1f, 0.15f, 0.1f, 1f);
            foreach (var cell in world.BlockedCells)
            {
                var c = GridSpace3D.ToWorld(cell, lift);
                var d = half * 0.7f;
                AddLine(c + new Vector3(-d, 0f, -d), c + new Vector3(d, 0f, d), blockedColor);
                AddLine(c + new Vector3(-d, 0f, d), c + new Vector3(d, 0f, -d), blockedColor);
            }
            var mesh = new Mesh { name = "Grid overlay" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetIndices(lines, MeshTopology.Lines, 0);
            var overlay = new GameObject("Grid overlay (debug)");
            overlay.transform.SetParent(mapRoot, false);
            overlay.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = overlay.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = catalog.OverlayMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return overlay;

            void AddLine(Vector3 a, Vector3 b, Color color)
            {
                lines.Add(vertices.Count);
                vertices.Add(a);
                colors.Add(color);
                lines.Add(vertices.Count);
                vertices.Add(b);
                colors.Add(color);
            }
        }

        public static Color SurfaceColor(string surfaceId, Vector2Int cell)
        {
            Color baseColor = surfaceId switch
            {
                "dirt" => new Color32(0xc9, 0xa8, 0x74, 0xff),
                "house_floor" => new Color32(0xd3, 0x8b, 0x40, 0xff),
                "house_wall" => new Color32(0x8a, 0x76, 0x62, 0xff),
                _ => new Color32(0x8f, 0xb8, 0x5c, 0xff)
            };
            // A tiny per-cell variation keeps the grid readable after pixelation.
            var hash = (cell.x * 73856093) ^ (cell.y * 19349663);
            var shade = ((hash & 3) - 1.5f) * 0.012f;
            return new Color(baseColor.r + shade, baseColor.g + shade, baseColor.b + shade, 1f);
        }

        private static Color FallbackColor(string definitionId) => definitionId switch
        {
            "stone_basic" => new Color32(0x9a, 0x9c, 0xa2, 0xff),
            "flower_basic" => new Color32(0xf0, 0x8a, 0xb4, 0xff),
            "sign_basic" => new Color32(0x9a, 0x6b, 0x3e, 0xff),
            "well_basic" => new Color32(0x7c, 0x84, 0x90, 0xff),
            "bench_basic" => new Color32(0x8c, 0x5e, 0x36, 0xff),
            "bed_basic" => new Color32(0x6f, 0x8f, 0xd8, 0xff),
            "cushion_basic" => new Color32(0xe8, 0x9a, 0x6a, 0xff),
            "desk_basic" => new Color32(0x5c, 0x40, 0x2c, 0xff),
            "recording_mic_basic" => new Color32(0x40, 0x40, 0x48, 0xff),
            "art_easel_basic" => new Color32(0xb0, 0x84, 0x52, 0xff),
            "studio_shelf_basic" => new Color32(0x74, 0x52, 0x38, 0xff),
            _ => new Color32(0xc0, 0x60, 0xc0, 0xff)
        };

        private static float FallbackHeight(WorldObjectDefinition definition) => definition.StableId switch
        {
            "flower_basic" => 0.16f,
            "stone_basic" => 0.22f,
            "cushion_basic" => 0.12f,
            "bench_basic" => 0.24f,
            "bed_basic" => 0.3f,
            "sign_basic" => 0.48f,
            "well_basic" => 0.42f,
            "recording_mic_basic" => 0.8f,
            "art_easel_basic" => 0.75f,
            "studio_shelf_basic" => 0.85f,
            _ => 0.4f
        };
    }
}
