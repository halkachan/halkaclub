using System;
using System.Collections.Generic;
using Halka.Game.World;
using UnityEngine;

namespace Halka.Poc2D3D.World
{
    // ver3.2: one Map of the outdoor world, with its cells pre-computed once (the same rules as
    // MapPlacementRules.BlocksMovement and .HasGrass, which test every Object for every cell: fine for
    // 21 x 13 cells, far too slow for 256 x 256). Flags live in a flat array over the Map bounds.
    public sealed class OutdoorRegion
    {
        [Flags]
        public enum CellFlags : byte
        {
            None = 0,
            Blocked = 1,
            Grass = 2,
            Road = 4,          // a Dirt Surface cell (the road the environment dresses)
            BlockingSurface = 8 // a blocking Surface (walls)
        }

        public readonly MapDefinition Map;
        public readonly Vector2Int Offset;
        public readonly CellRect Rect;      // world cells
        public readonly int Order;          // position in the registry
        public readonly string Direction;   // ver3.3: center / north / east / south / west
        private readonly CellFlags[] flags;
        private readonly Dictionary<Vector2Int, SurfaceDefinition> surfaces = new Dictionary<Vector2Int, SurfaceDefinition>();

        public string MapId => Map.MapId;

        public OutdoorRegion(OutdoorRegionLink link, int order)
        {
            Map = link.map;
            Offset = link.offset;
            Order = order;
            Direction = string.IsNullOrEmpty(link.direction) ? OutdoorWorldDefinition.Center : link.direction;
            Rect = new CellRect(Map.MinCell + Offset, Map.MaxCell + Offset);
            flags = new CellFlags[Rect.Width * Rect.Height];
            foreach (var surface in Map.Surfaces)
                if (surface.Definition != null) surfaces[surface.Cell] = surface.Definition;
            var noGrass = new bool[flags.Length];
            for (var y = Map.MinCell.y; y <= Map.MaxCell.y; y++)
            for (var x = Map.MinCell.x; x <= Map.MaxCell.x; x++)
            {
                var local = new Vector2Int(x, y);
                surfaces.TryGetValue(local, out var surface);
                var f = CellFlags.None;
                if (surface != null && surface.BlocksMovement) f |= CellFlags.Blocked | CellFlags.BlockingSurface;
                if (surface != null && surface.StableId == PocEnvironmentBuilder.RoadSurfaceId) f |= CellFlags.Road;
                flags[LocalIndex(local)] = f;
            }
            foreach (var placement in Map.Objects)
            {
                var definition = placement.Definition;
                if (definition == null) continue;
                foreach (var offset in definition.EffectiveBlockedOffsets())
                {
                    var local = placement.RootCell + offset;
                    if (!Map.Contains(local)) continue;
                    if (definition.BlocksMovement) flags[LocalIndex(local)] |= CellFlags.Blocked;
                    if (definition.ExcludesGrass) noGrass[LocalIndex(local)] = true;
                }
            }
            var baseGrows = Map.GrassMode == "auto" && Map.BaseSurface != null && Map.BaseSurface.GrowsGrass;
            for (var y = Map.MinCell.y; y <= Map.MaxCell.y; y++)
            for (var x = Map.MinCell.x; x <= Map.MaxCell.x; x++)
            {
                var local = new Vector2Int(x, y);
                var i = LocalIndex(local);
                if ((flags[i] & CellFlags.Blocked) != 0 || noGrass[i]) continue;
                surfaces.TryGetValue(local, out var surface);
                if (surface != null ? surface.GrowsGrass : baseGrows) flags[i] |= CellFlags.Grass;
            }
        }

        private int LocalIndex(Vector2Int local) => (local.x - Map.MinCell.x) + (local.y - Map.MinCell.y) * Rect.Width;

        public Vector2Int ToWorld(Vector2Int local) => local + Offset;
        public Vector2Int ToLocal(Vector2Int world) => world - Offset;
        public bool ContainsWorld(Vector2Int world) => Rect.Contains(world);

        public CellFlags FlagsAtWorld(Vector2Int world) =>
            Rect.Contains(world) ? flags[LocalIndex(world - Offset)] : CellFlags.Blocked;

        public IEnumerable<Vector2Int> BlockedWorldCells()
        {
            for (var y = Map.MinCell.y; y <= Map.MaxCell.y; y++)
            for (var x = Map.MinCell.x; x <= Map.MaxCell.x; x++)
            {
                var local = new Vector2Int(x, y);
                if ((flags[LocalIndex(local)] & CellFlags.Blocked) != 0) yield return ToWorld(local);
            }
        }

        public SurfaceDefinition SurfaceAtWorld(Vector2Int world) =>
            Rect.Contains(world) && surfaces.TryGetValue(world - Offset, out var surface) ? surface : null;
    }

    // ver3.2: what one chunk holds before anything is drawn: the Objects whose root cell lies in it
    // (each Object belongs to exactly one chunk, so it is never built twice) and the chunks of
    // Objects that stand elsewhere but reach into it (a wide house), which must be loaded with it.
    public sealed class ChunkStatic
    {
        public readonly ChunkCoord Coord;
        public readonly CellRect Rect;
        public readonly List<SharedObjectInstance> OwnedObjects = new List<SharedObjectInstance>();
        public readonly HashSet<ChunkCoord> ReachingOwners = new HashSet<ChunkCoord>();
        // The Objects of other chunks whose picture reaches into this one: their owner chunk and the
        // cells the picture covers (the streamer loads the owner only when the view meets those cells).
        public readonly List<(ChunkCoord owner, CellRect extent)> Reaching = new List<(ChunkCoord, CellRect)>();
        public int RegionCells;
        public bool HasDecoration;

        public ChunkStatic(ChunkCoord coord, CellRect rect)
        {
            Coord = coord;
            Rect = rect;
        }
    }

    // ver3.2: the logical outdoor world in world cells: regions, collision, grass, roads, Objects and
    // the static chunk table. Built once when the session starts and kept (it is the world's state,
    // not a chunk's): chunks only stream the presentation. Cells outside every region are blocked.
    public sealed class OutdoorWorldIndex
    {
        private readonly List<OutdoorRegion> regions = new List<OutdoorRegion>();
        private readonly List<SharedObjectInstance> objects = new List<SharedObjectInstance>();
        private readonly Dictionary<string, SharedObjectInstance> objectById = new Dictionary<string, SharedObjectInstance>();
        private readonly Dictionary<ChunkCoord, ChunkStatic> chunks = new Dictionary<ChunkCoord, ChunkStatic>();

        public OutdoorWorldDefinition Definition { get; }
        public string WorldId => Definition.worldId;
        public int ChunkSize { get; }
        public int DecorationMargin { get; }
        public IReadOnlyList<OutdoorRegion> Regions => regions;
        public OutdoorRegion PrimaryRegion => regions[0];
        public IReadOnlyList<SharedObjectInstance> Objects => objects;
        public IReadOnlyDictionary<ChunkCoord, ChunkStatic> Chunks => chunks;
        public CellRect Bounds { get; }
        public float BuildMilliseconds { get; }

        // decorationMargin: how far outside the regions the ground is still dressed (the ver3.1
        // "outside margin": tinted grass and roads running off the map). Presentation only.
        public OutdoorWorldIndex(OutdoorWorldDefinition definition, int decorationMargin = 8)
        {
            var started = Time.realtimeSinceStartupAsDouble;
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            var problems = definition.Validate();
            if (OutdoorWorldDefinition.HasErrors(problems))
                throw new InvalidOperationException("Invalid outdoor world " + definition.worldId + ": " + string.Join("; ", problems));
            ChunkSize = definition.chunkSize;
            DecorationMargin = Mathf.Max(0, decorationMargin);
            var bounds = new CellRect(Vector2Int.one, Vector2Int.zero);
            for (var i = 0; i < definition.regions.Count; i++)
            {
                var region = new OutdoorRegion(definition.regions[i], i);
                regions.Add(region);
                bounds = bounds.Union(region.Rect);
            }
            Bounds = bounds;
            foreach (var region in regions)
            {
                var placements = region.Map.Objects;
                for (var i = 0; i < placements.Count; i++)
                {
                    var placement = placements[i];
                    if (placement.Definition == null) continue;
                    var instance = new SharedObjectInstance
                    {
                        InstanceId = placement.InstanceId,
                        DefinitionId = placement.Definition.StableId,
                        Definition = placement.Definition,
                        RootCell = region.ToWorld(placement.RootCell),
                        SignText = placement.SignText,
                        RegionMapId = region.MapId,
                        LocalRootCell = placement.RootCell,
                        OrderInMap = i
                    };
                    objects.Add(instance);
                    if (!objectById.ContainsKey(instance.InstanceId)) objectById[instance.InstanceId] = instance;
                }
            }
            BuildChunkTable();
            BuildMilliseconds = (float)((Time.realtimeSinceStartupAsDouble - started) * 1000.0);
        }

        private void BuildChunkTable()
        {
            foreach (var region in regions)
            {
                var decorated = region.Rect.Expanded(DecorationMargin);
                var c0 = WorldGrid.ChunkOf(decorated.Min, ChunkSize);
                var c1 = WorldGrid.ChunkOf(decorated.Max, ChunkSize);
                for (var cy = c0.Y; cy <= c1.Y; cy++)
                for (var cx = c0.X; cx <= c1.X; cx++)
                {
                    var chunk = Ensure(new ChunkCoord(cx, cy));
                    chunk.HasDecoration = true;
                    var overlap = Intersect(chunk.Rect, region.Rect);
                    if (!overlap.IsEmpty) chunk.RegionCells += overlap.Width * overlap.Height;
                }
            }
            // ver3.8: the corners of the world's rectangle between the regions (East of the South area,
            // and so on) are dressed too, so the 2D view never shows an undrawn (black) gap there.
            var world = Bounds.Expanded(DecorationMargin);
            var w0 = WorldGrid.ChunkOf(world.Min, ChunkSize);
            var w1 = WorldGrid.ChunkOf(world.Max, ChunkSize);
            for (var cy = w0.Y; cy <= w1.Y; cy++)
            for (var cx = w0.X; cx <= w1.X; cx++)
                Ensure(new ChunkCoord(cx, cy)).HasDecoration = true;
            foreach (var instance in objects)
            {
                var owner = WorldGrid.ChunkOf(instance.RootCell, ChunkSize);
                Ensure(owner).OwnedObjects.Add(instance);
                var extent = PresentationExtent(instance);
                var c0 = WorldGrid.ChunkOf(extent.Min, ChunkSize);
                var c1 = WorldGrid.ChunkOf(extent.Max, ChunkSize);
                for (var cy = c0.Y; cy <= c1.Y; cy++)
                for (var cx = c0.X; cx <= c1.X; cx++)
                {
                    var reached = new ChunkCoord(cx, cy);
                    if (reached == owner) continue;
                    var chunk = Ensure(reached);
                    chunk.ReachingOwners.Add(owner);
                    chunk.Reaching.Add((owner, extent));
                }
            }
        }

        private ChunkStatic Ensure(ChunkCoord coord)
        {
            if (!chunks.TryGetValue(coord, out var chunk))
            {
                chunk = new ChunkStatic(coord, WorldGrid.ChunkRect(coord, ChunkSize));
                chunks[coord] = chunk;
            }
            return chunk;
        }

        private static CellRect Intersect(CellRect a, CellRect b) =>
            new CellRect(Vector2Int.Max(a.Min, b.Min), Vector2Int.Min(a.Max, b.Max));

        // The cells an Object's picture covers: its authored footprint (from the root anchor) and its
        // blocked cells, one cell around for the parts that hang over (canopies, eaves).
        public static CellRect PresentationExtent(SharedObjectInstance instance)
        {
            var definition = instance.Definition;
            var footprint = definition.Footprint;
            var minX = definition.RootAnchor == "bottom-center" ? -(footprint.x - 1) / 2 : 0;
            var min = new Vector2Int(minX, 0);
            var max = new Vector2Int(minX + Mathf.Max(1, footprint.x) - 1, Mathf.Max(1, footprint.y) - 1);
            foreach (var offset in definition.EffectiveBlockedOffsets())
            {
                min = Vector2Int.Min(min, offset);
                max = Vector2Int.Max(max, offset);
            }
            return new CellRect(instance.RootCell + min - Vector2Int.one, instance.RootCell + max + Vector2Int.one);
        }

        public OutdoorRegion RegionAt(Vector2Int world)
        {
            foreach (var region in regions)
                if (region.Rect.Contains(world)) return region;
            return null;
        }

        public OutdoorRegion FindRegion(string mapId)
        {
            foreach (var region in regions)
                if (region.MapId == mapId) return region;
            return null;
        }

        // The region whose dressing a cell outside every region borrows (the nearest within the margin;
        // ver3.8: also the nearest for any cell of the world's rectangle and its margin).
        public OutdoorRegion DecorationRegionAt(Vector2Int world)
        {
            OutdoorRegion best = null;
            var bestDistance = int.MaxValue;
            foreach (var region in regions)
            {
                var d = region.Rect.ChebyshevDistance(world);
                if (d < bestDistance) { best = region; bestDistance = d; }
            }
            return bestDistance <= DecorationMargin || Bounds.Expanded(DecorationMargin).Contains(world) ? best : null;
        }

        public bool Contains(Vector2Int world) => RegionAt(world) != null;
        public bool IsBlocked(Vector2Int world)
        {
            var region = RegionAt(world);
            return region == null || (region.FlagsAtWorld(world) & OutdoorRegion.CellFlags.Blocked) != 0;
        }
        public bool CanEnter(Vector2Int world) => !IsBlocked(world);
        public bool HasGrass(Vector2Int world)
        {
            var region = RegionAt(world);
            return region != null && (region.FlagsAtWorld(world) & OutdoorRegion.CellFlags.Grass) != 0;
        }
        public SurfaceDefinition SurfaceAt(Vector2Int world) => RegionAt(world)?.SurfaceAtWorld(world);

        public SharedObjectInstance FindInstance(string instanceId) =>
            objectById.TryGetValue(instanceId, out var instance) ? instance : null;

        public bool TryGetHouseDoor(out Vector2Int world)
        {
            foreach (var region in regions)
                if (region.Map.TryGetHouseRoot(out var local)) { world = region.ToWorld(local); return true; }
            world = default;
            return false;
        }

        public bool TryGetMarker(string id, out Vector2Int world)
        {
            foreach (var region in regions)
                if (region.Map.TryGetMarker(id, out var local)) { world = region.ToWorld(local); return true; }
            world = default;
            return false;
        }

        public ChunkStatic ChunkAt(ChunkCoord coord) => chunks.TryGetValue(coord, out var chunk) ? chunk : null;
        public ChunkCoord ChunkOfCell(Vector2Int world) => WorldGrid.ChunkOf(world, ChunkSize);

        // Objects that may answer from a cell: the ones owned by the chunks around it (an Object's
        // Action Points lie within a few cells of its root).
        public IEnumerable<SharedObjectInstance> ObjectsNear(Vector2Int world)
        {
            var center = ChunkOfCell(world);
            for (var cy = center.Y - 1; cy <= center.Y + 1; cy++)
            for (var cx = center.X - 1; cx <= center.X + 1; cx++)
                if (chunks.TryGetValue(new ChunkCoord(cx, cy), out var chunk))
                    foreach (var instance in chunk.OwnedObjects) yield return instance;
        }
    }
}
