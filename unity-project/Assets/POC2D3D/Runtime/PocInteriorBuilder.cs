using System.Collections.Generic;
using Halka.Game.World;
using UnityEngine;

namespace Halka.Poc2D3D
{
    public sealed class PocInteriorStats
    {
        public string MapId;
        public int FloorTiles;
        public int WallCells;
        public bool HasDoorway;
        public Vector2Int DoorwayCell;
        public string DoorwaySide;
        public int Triangles;
        public readonly Dictionary<string, int> WallCellsBySide = new Dictionary<string, int>();
        public readonly HashSet<Vector2Int> FloorCells = new HashSet<Vector2Int>();
        public readonly HashSet<Vector2Int> WallCellSet = new HashSet<Vector2Int>();
    }

    // Builds an interior map from Blender pieces, entirely from Map Data (Phase 3):
    //   - a floor tile on every cell inside the bounds that is not a wall Surface (also under
    //     furniture, so nothing floats or hangs over a hole);
    //   - the blocking Surfaces (walls) as one always-visible low base mesh, plus one upper mesh
    //     per room side, so the 3D camera and the 2D view can cut away the side facing them;
    //   - the doorway on the interior_exit marker, turned to face its side.
    // A wall cell belongs to the map edge it is nearest to (ties: North/South first), so any
    // rectangular room made in MAP EDITOR is split the same way. Nothing changes collision.
    public static class PocInteriorBuilder
    {
        public static readonly string[] SideNames = { "north", "south", "east", "west" };

        public static Vector2Int SideNormal(string side) => side switch
        {
            "north" => Vector2Int.up,
            "south" => Vector2Int.down,
            "east" => Vector2Int.right,
            _ => Vector2Int.left
        };

        public static string SideOf(MapDefinition map, Vector2Int cell)
        {
            var distances = new[]
            {
                map.MaxCell.y - cell.y, cell.y - map.MinCell.y, map.MaxCell.x - cell.x, cell.x - map.MinCell.x
            };
            var best = 0;
            for (var i = 1; i < distances.Length; i++)
                if (distances[i] < distances[best]) best = i;
            return SideNames[best];
        }

        public static PocInteriorStats Build(SharedWorldModel world, PocInteriorPresentation dressing,
            Material material, Transform parent, List<PocObjectView> views)
        {
            var map = world.Map;
            var stats = new PocInteriorStats { MapId = map.MapId };
            var hasExit = world.TryGetMarker("interior_exit", out var exit);
            var floor = new PocEnvironmentBuilder.MeshAppender();
            var bases = new PocEnvironmentBuilder.MeshAppender();
            var uppers = new Dictionary<string, PocEnvironmentBuilder.MeshAppender>();
            foreach (var side in SideNames)
            {
                uppers[side] = new PocEnvironmentBuilder.MeshAppender();
                stats.WallCellsBySide[side] = 0;
            }
            var white = Color.white;
            for (var y = map.MinCell.y; y <= map.MaxCell.y; y++)
            for (var x = map.MinCell.x; x <= map.MaxCell.x; x++)
            {
                var cell = new Vector2Int(x, y);
                var at = Matrix4x4.Translate(GridSpace3D.ToWorld(cell));
                var surface = map.SurfaceAt(cell);
                if (hasExit && cell == exit)
                {
                    var side = SideOf(map, cell);
                    var turn = Matrix4x4.TRS(GridSpace3D.ToWorld(cell), Quaternion.Euler(0f, DoorwayYaw(side), 0f),
                        Vector3.one);
                    floor.Append(PocEnvironmentBuilder.Mesh(dressing.doorwayBase), turn, white);
                    uppers[side].Append(PocEnvironmentBuilder.Mesh(dressing.doorwayUpper), turn, white);
                    stats.HasDoorway = true;
                    stats.DoorwayCell = cell;
                    stats.DoorwaySide = side;
                    stats.FloorCells.Add(cell);
                }
                else if (surface != null && surface.BlocksMovement)
                {
                    var side = SideOf(map, cell);
                    bases.Append(PocEnvironmentBuilder.Mesh(dressing.wallBase), at, white);
                    uppers[side].Append(PocEnvironmentBuilder.Mesh(dressing.wallUpper), at, white);
                    stats.WallCells++;
                    stats.WallCellsBySide[side]++;
                    stats.WallCellSet.Add(cell);
                }
                else
                {
                    var tile = dressing.floorTiles[(int)(PocStableHash.Of(map.MapId, cell, "floor") %
                        (uint)dressing.floorTiles.Count)];
                    floor.Append(PocEnvironmentBuilder.Mesh(tile), at, white);
                    stats.FloorTiles++;
                    stats.FloorCells.Add(cell);
                }
            }
            stats.Triangles = floor.TriangleCount + bases.TriangleCount;
            var floorObject = floor.CreateObject("Interior floor (Blender tiles from Map Data)", parent, material);
            floorObject.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var baseObject = bases.CreateObject("Interior wall base (always shown)", parent, material);
            baseObject.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            foreach (var side in SideNames)
            {
                var mesh = uppers[side];
                if (mesh.TriangleCount == 0) continue;
                stats.Triangles += mesh.TriangleCount;
                var upper = mesh.CreateObject("Interior wall " + side + " (upper, cut away facing the camera)", parent,
                    material);
                var renderer = upper.GetComponent<MeshRenderer>();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var view = upper.AddComponent<PocObjectView>();
                view.InstanceId = "wall_" + side;
                view.DefinitionId = "house_wall";
                view.IsWall = true;
                view.WallOutward = SideNormal(side);
                view.CameraOccluder = true;
                view.CastShadows = false;
                view.Renderers = new Renderer[] { renderer };
                view.CutawayRenderers = new Renderer[] { renderer };
                views.Add(view);
            }
            return stats;
        }

        // The doorway model faces South (door on its South edge); turn it for other sides.
        public static float DoorwayYaw(string side) => side switch
        {
            "north" => 180f,
            "east" => -90f,
            "west" => 90f,
            _ => 0f
        };
    }
}
