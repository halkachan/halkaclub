using System;
using System.Collections.Generic;
using Halka.Game.Player;
using UnityEngine;

namespace Halka.Game.World
{
    // Explicit runtime handlers for the two authored entity types. No static Map Object is created for Player.
    public static class EntityRuntimeFactory2D
    {
        public static int FindPlayerStartMap(IReadOnlyList<MapDefinition> maps)
        {
            var startIndex = -1;
            for (var i = 0; i < maps.Count; i++)
            {
                if (maps[i] == null || !maps[i].TryGetEntitySpawn("player_main", out _)) continue;
                if (startIndex >= 0) throw new InvalidOperationException("World has multiple Player Starts");
                startIndex = i;
            }
            if (startIndex < 0) throw new InvalidOperationException("World has no Player Start");
            return startIndex;
        }

        public static EntitySpawnPlacement Required(MapDefinition map, string definitionId)
        {
            if (!map.TryGetEntitySpawn(definitionId, out var spawn) || spawn.Definition == null)
                throw new InvalidOperationException(map.MapId + " has no " + definitionId + " Entity Spawn");
            return spawn;
        }

        // Bind authored spawns to the two existing runtime actors explicitly.
        public static bool TryApplyPlayerStart(MapDefinition map, GridWorld2D world,
            PlayerMover player)
        {
            if (!map.TryGetEntitySpawn("player_main", out var spawn)) return false;
            player.transform.position = world.CellToWorld(spawn.Cell);
            player.SetInitialFacing(ParseFacing(spawn.Facing));
            return true;
        }

        public static bool TryActivateCrow(MapDefinition map, GridWorld2D world,
            GrassField2D grassField, Transform entityRoot, CrowWander2D crow)
        {
            if (!map.TryGetEntitySpawn("crow_main", out var spawn))
            {
                if (crow != null) crow.gameObject.SetActive(false);
                return false;
            }
            if (crow == null || entityRoot == null)
                throw new InvalidOperationException("Crow Entity Spawn needs a Crow runtime actor and root");
            crow.transform.SetParent(entityRoot, true);
            crow.SetGrassField(grassField);
            var occlusion = crow.GetComponent<CrowGrassOcclusion>();
            if (occlusion != null) occlusion.SetGrassField(grassField);
            crow.ApplySpawn(spawn, world);
            crow.gameObject.SetActive(true);
            return true;
        }

        public static FacingDirection ParseFacing(string facing)
        {
            switch (facing)
            {
                case "up": return FacingDirection.Up;
                case "left": return FacingDirection.Left;
                case "right": return FacingDirection.Right;
                case "down": return FacingDirection.Down;
                default: throw new InvalidOperationException("Invalid Entity facing: " + facing);
            }
        }
    }
}
