using System;
using System.Collections.Generic;
using Halka.Game.World;
using UnityEngine;

namespace Halka.Poc2D3D.World
{
    // ver3.2, test only: a square outdoor world of size x size cells centred on (0,0) (so half of it
    // has negative coordinates), built in memory from the official definitions:
    //   - size <= 64: one region, offset (0,0);
    //   - size >= 128: four regions (2 x 2) split at x = 0 and y = 0; the North-East one has offset
    //     (0,0) and holds the Player Start, the crow and a house; the other three are authored from
    //     (0,0) and placed with a non-zero offset (their Map cells differ from their world cells);
    //   - roads every 24 cells, trees / stones / flowers at the density of first_field, and wide
    //     Objects across chunk borders (the house across x = 0 or x = 16, benches across x = 16k).
    // The same size and seed always give the same world. Nothing here is saved to disk.
    public static class PocTestWorldGenerator
    {
        public static readonly int[] Sizes = { 32, 64, 128, 256 };
        public static readonly Vector2Int PlayerStart = new Vector2Int(3, 2);
        public static readonly Vector2Int CrowStart = new Vector2Int(6, 3);
        public const int RoadSpacing = 24;

        public static Vector2Int HouseRoot(int size) => size <= 64 ? new Vector2Int(0, 6) : new Vector2Int(16, 6);

        public static OutdoorWorldDefinition Build(PocTestWorldFixture fixture, int size, int chunkSize, int seed, out MapDefinition primary)
        {
            if (fixture == null || !fixture.IsComplete) throw new ArgumentException("incomplete test world fixture");
            if (size < 16 || size % 2 != 0) throw new ArgumentOutOfRangeException(nameof(size));
            var half = size / 2;
            var world = new OutdoorWorldDefinition { worldId = "chunktest_" + size, chunkSize = chunkSize };
            var reserved = Reserved(size);
            primary = null;
            if (size <= 64)
            {
                primary = Region(fixture, "chunktest_" + size + "_main", new CellRect(new Vector2Int(-half, -half), new Vector2Int(half - 1, half - 1)),
                    Vector2Int.zero, true, size, seed, reserved);
                world.regions.Add(new OutdoorRegionLink { mapId = primary.MapId, map = primary, offset = Vector2Int.zero });
                return world;
            }
            var parts = new (string name, CellRect rect, bool primary)[]
            {
                ("ne", new CellRect(new Vector2Int(0, 0), new Vector2Int(half - 1, half - 1)), true),
                ("nw", new CellRect(new Vector2Int(-half, 0), new Vector2Int(-1, half - 1)), false),
                ("sw", new CellRect(new Vector2Int(-half, -half), new Vector2Int(-1, -1)), false),
                ("se", new CellRect(new Vector2Int(0, -half), new Vector2Int(half - 1, -1)), false),
            };
            foreach (var (name, rect, isPrimary) in parts)
            {
                var offset = isPrimary ? Vector2Int.zero : rect.Min;
                var map = Region(fixture, "chunktest_" + size + "_" + name, rect, offset, isPrimary, size, seed, reserved);
                world.regions.Add(new OutdoorRegionLink { mapId = map.MapId, map = map, offset = offset });
                if (isPrimary) primary = map;
            }
            return world;
        }

        // Cells kept free of random Objects: the starts, the house and its doorstep, the benches.
        private static HashSet<Vector2Int> Reserved(int size)
        {
            var reserved = new HashSet<Vector2Int>();
            for (var y = -1; y <= 5; y++)
            for (var x = 0; x <= 8; x++) reserved.Add(new Vector2Int(x, y));
            var house = HouseRoot(size);
            for (var y = house.y - 2; y <= house.y + 2; y++)
            for (var x = house.x - 3; x <= house.x + 3; x++) reserved.Add(new Vector2Int(x, y));
            foreach (var bench in Benches(size))
                for (var x = bench.x - 1; x <= bench.x + 2; x++)
                for (var y = bench.y - 1; y <= bench.y + 1; y++) reserved.Add(new Vector2Int(x, y));
            return reserved;
        }

        // Benches (2 cells, bottom-left root) whose two cells lie on both sides of a 16-cell border.
        public static IEnumerable<Vector2Int> Benches(int size)
        {
            var half = size / 2;
            for (var x = 15; x + 1 < half; x += 16)
                yield return new Vector2Int(x, 12);
        }

        private static bool IsRoad(Vector2Int world) =>
            WorldGrid.FloorMod(world.x, RoadSpacing) == RoadSpacing / 2 || WorldGrid.FloorMod(world.y, RoadSpacing) == RoadSpacing / 2;

        private static uint Hash(int seed, Vector2Int cell, int salt)
        {
            unchecked
            {
                var h = (uint)seed * 2654435761u ^ (uint)cell.x * 73856093u ^ (uint)cell.y * 19349663u ^ (uint)salt * 83492791u;
                h ^= h >> 16; h *= 0x7feb352du; h ^= h >> 15; h *= 0x846ca68bu; h ^= h >> 16;
                return h;
            }
        }

        private static MapDefinition Region(PocTestWorldFixture f, string mapId, CellRect worldRect, Vector2Int offset, bool primary,
            int size, int seed, HashSet<Vector2Int> reserved)
        {
            var surfaces = new List<SurfacePlacement>();
            var objects = new List<WorldObjectPlacement>();
            var spawns = new List<EntitySpawnPlacement>();
            var taken = new HashSet<Vector2Int>();
            void Place(WorldObjectDefinition definition, Vector2Int worldRoot, string id)
            {
                objects.Add(new WorldObjectPlacement { InstanceId = mapId + "_" + id, RootCell = worldRoot - offset, Definition = definition });
                foreach (var o in definition.EffectiveBlockedOffsets()) taken.Add(worldRoot + o);
                taken.Add(worldRoot);
            }
            for (var y = worldRect.Min.y; y <= worldRect.Max.y; y++)
            for (var x = worldRect.Min.x; x <= worldRect.Max.x; x++)
            {
                var cell = new Vector2Int(x, y);
                if (IsRoad(cell) && !reserved.Contains(cell)) surfaces.Add(new SurfacePlacement { Cell = cell - offset, Definition = f.dirt });
            }
            if (primary)
            {
                var house = HouseRoot(size);
                if (worldRect.Contains(house + new Vector2Int(-2, 0)) && worldRect.Contains(house + new Vector2Int(2, 1)))
                    Place(f.house, house, "house");
                var n = 0;
                foreach (var bench in Benches(size))
                    if (worldRect.Contains(bench) && worldRect.Contains(bench + Vector2Int.right)) Place(f.bench, bench, "bench_" + n++);
                Place(f.well, new Vector2Int(8, 8), "well");
                spawns.Add(new EntitySpawnPlacement { InstanceId = "player_main_" + mapId, Definition = f.player, Cell = PlayerStart - offset, Facing = "down" });
                spawns.Add(new EntitySpawnPlacement { InstanceId = "crow_main_" + mapId, Definition = f.crow, Cell = CrowStart - offset, Facing = "left" });
            }
            for (var y = worldRect.Min.y; y <= worldRect.Max.y; y++)
            for (var x = worldRect.Min.x; x <= worldRect.Max.x; x++)
            {
                var cell = new Vector2Int(x, y);
                if (reserved.Contains(cell) || taken.Contains(cell) || IsRoad(cell)) continue;
                // Keep one free cell around the region edge rows next to roads so every road stays walkable.
                var r = Hash(seed, cell, 1) % 1000;
                if (r < 40) Place(f.tree, cell, "tree_" + x + "_" + y);
                else if (r < 60) Place(f.stone, cell, "stone_" + x + "_" + y);
                else if (r < 90) Place(f.flower, cell, "flower_" + x + "_" + y);
            }
            var map = ScriptableObject.CreateInstance<MapDefinition>();
            map.name = mapId;
            map.ReplaceFromAuthoring(mapId, mapId, "outdoor", f.grass, "auto", new Color(0.06f, 0.06f, 0.08f),
                worldRect.Min - offset, worldRect.Max - offset, surfaces, objects, new List<LockedMapMarker>(), spawns);
            return map;
        }
    }
}
