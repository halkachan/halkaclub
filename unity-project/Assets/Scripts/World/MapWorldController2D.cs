using System;
using Halka.Game.Player;
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

        public string ActiveMapId { get; private set; } = "first_field";

        private void Awake()
        {
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
