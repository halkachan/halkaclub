using System;
using Halka.Game.Player;
using Halka.Game.CameraControl;
using UnityEngine;

namespace Halka.Game.World
{
    // A small explicit registry for the maps used by the first field and its house.
    [DefaultExecutionOrder(-20000)]
    public sealed class MapWorldController2D : MonoBehaviour
    {
        [SerializeField] private MapDefinition[] maps;
        [SerializeField] private MapRuntimeLoader2D[] loaders;
        [SerializeField] private GameObject[] roots;
        [SerializeField] private PlayerMover player;
        [SerializeField] private PlayerGrassOcclusion grassOcclusion;
        [SerializeField] private GrassField2D[] grassFields;
        [SerializeField] private CameraFollow2D cameraFollow;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private HouseArea2D houseArea;

        private float initialCameraSize;

        public string ActiveMapId { get; private set; } = "first_field";

        private void Awake()
        {
            if (worldCamera != null) initialCameraSize = worldCamera.orthographicSize;
            if (maps == null || roots == null || maps.Length != roots.Length)
                throw new InvalidOperationException("Map registry is incomplete");
            var startIndex = EntityRuntimeFactory2D.FindPlayerStartMap(maps);
            ActiveMapId = maps[startIndex].MapId;
            for (var i = 0; i < roots.Length; i++) roots[i].SetActive(i == startIndex);
        }

        public MapDefinition GetMap(string mapId)
        {
            for (var i = 0; i < maps.Length; i++)
                if (maps[i] != null && maps[i].MapId == mapId) return maps[i];
            throw new ArgumentException("Unknown map ID: " + mapId);
        }

        public void ActivateMap(string mapId)
        {
            var target = IndexOf(mapId);
            if (ActiveMapId == mapId) return;
            var previous = IndexOf(ActiveMapId);
            loaders[previous].Unload();
            roots[previous].SetActive(false);
            roots[target].SetActive(true);
            loaders[target].Build();
            ActiveMapId = mapId;
        }

        public void LoadMap(string mapId, string spawnMarkerId)
        {
            ActivateMap(mapId);
            if (!maps[IndexOf(mapId)].TryGetMarker(spawnMarkerId, out var spawn))
                throw new InvalidOperationException(mapId + " has no spawn marker " + spawnMarkerId);
            player.TeleportTo(spawn);
        }

        // Called only by manual input attempting to leave a map edge.
        public bool TryExit(Vector2Int sourceCell, Vector2Int direction)
        {
            var sourceMap = maps[IndexOf(ActiveMapId)];
            var target = sourceCell + direction;
            if (sourceMap.Contains(target)) return false;
            var directionName = direction == Vector2Int.up ? "up" :
                direction == Vector2Int.down ? "down" :
                direction == Vector2Int.left ? "left" : "right";
            if (!sourceMap.TryGetAreaTransition(sourceCell, directionName, out var transition)) return false;
            var destination = GetMap(transition.DestinationMapId);
            if (!destination.Contains(transition.DestinationCell) ||
                MapPlacementRules.BlocksMovement(destination, transition.DestinationCell)) return false;
            if (destination.TryGetEntitySpawn("crow_main", out var crowSpawn) &&
                crowSpawn.Cell == transition.DestinationCell) return false;
            ActivateMap(transition.DestinationMapId);
            player.TeleportTo(transition.DestinationCell);
            player.SetFacing(EntityRuntimeFactory2D.ParseFacing(transition.ArrivalFacing));
            if (houseArea != null) houseArea.NotifyAreaTransition(transition.DestinationMapId);
            var index = IndexOf(transition.DestinationMapId);
            if (grassOcclusion != null && grassFields != null && index < grassFields.Length && grassFields[index] != null)
                grassOcclusion.UseGrassField(grassFields[index]);
            if (cameraFollow != null)
            {
                if (worldCamera != null)
                    worldCamera.orthographicSize = destination.MapType == "interior" ? 3.4f : initialCameraSize;
                cameraFollow.SetBounds(new Vector2(destination.MinCell.x * GridWorld2D.TileWorldSize - 0.8f,
                    destination.MinCell.y * GridWorld2D.TileWorldSize - 0.8f),
                    new Vector2(destination.MaxCell.x * GridWorld2D.TileWorldSize + 0.8f,
                    destination.MaxCell.y * GridWorld2D.TileWorldSize + 0.8f));
                cameraFollow.SnapToTarget();
            }
            return true;
        }

        private int IndexOf(string mapId)
        {
            if (maps == null || loaders == null || roots == null ||
                maps.Length != loaders.Length || maps.Length != roots.Length)
                throw new InvalidOperationException("Map registry is incomplete");
            for (var i = 0; i < maps.Length; i++)
                if (maps[i] != null && maps[i].MapId == mapId && loaders[i] != null && roots[i] != null)
                    return i;
            throw new ArgumentException("Unknown map ID: " + mapId);
        }
    }
}
