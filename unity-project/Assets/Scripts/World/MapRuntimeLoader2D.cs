using System;
using System.Collections.Generic;
using Halka.Game.Player;
using Halka.Game.UI;
using Halka.Game.Interaction;
using UnityEngine;

namespace Halka.Game.World
{
    // Builds editable field content from MapDefinition before movement and grass Awake.
    [DefaultExecutionOrder(-10000)]
    public sealed class MapRuntimeLoader2D : MonoBehaviour
    {
        [SerializeField] private MapDefinition map;
        [SerializeField] private GridWorld2D world;
        [SerializeField] private GroundSurfaceField2D surfaceField;
        [SerializeField] private GrassField2D grassField;
        [SerializeField] private Transform surfaceRoot;
        [SerializeField] private Transform objectRoot;
        [SerializeField] private GameObject grassPrefab;
        [SerializeField] private PlayerMover player;
        [SerializeField] private SpriteRenderer playerRenderer;
        [SerializeField] private GameHud hud;

        private bool built;
        public MapDefinition Map => map;
        public bool IsBuilt => built;

        public void SetMap(MapDefinition definition) => map = definition;

        private void Awake() => Build();

        public void Build()
        {
            if (built) return;
            if (map == null || world == null || surfaceField == null || grassField == null ||
                surfaceRoot == null || objectRoot == null || grassPrefab == null ||
                player == null || playerRenderer == null || hud == null)
                throw new InvalidOperationException("MapRuntimeLoader2D has missing references");
            var errors = MapPlacementRules.Validate(map);
            if (errors.Count != 0) throw new InvalidOperationException(
                "Invalid map " + map.MapId + ": " + string.Join("; ", errors));

            world.SetBounds(map.MinCell, map.MaxCell);
            foreach (var surface in map.Surfaces)
            {
                var sprite = surface.Definition.Sprite;
                if (sprite == null) throw new InvalidOperationException("Surface sprite missing at " + surface.Cell);
                surfaceField.AddSurface(surface.Cell, sprite);
                var tile = new GameObject($"Ground tile {surface.Cell.x},{surface.Cell.y}");
                tile.transform.SetParent(surfaceRoot, false);
                tile.transform.position = world.CellToWorld(surface.Cell);
                var renderer = tile.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = -9;
            }

            var serial = new Dictionary<WorldObjectDefinition, int>();
            foreach (var placement in map.Objects)
            {
                var definition = placement.Definition;
                if (definition.PreviewSprite == null)
                    throw new InvalidOperationException("Object sprite missing: " + definition.StableId);
                serial.TryGetValue(definition, out var number);
                serial[definition] = ++number;
                CreateObject(placement.RootCell, definition, number);
            }

            for (var y = map.MinCell.y; y <= map.MaxCell.y; y++)
            for (var x = map.MinCell.x; x <= map.MaxCell.x; x++)
            {
                var cell = new Vector2Int(x, y);
                if (!MapPlacementRules.HasGrass(map, cell)) continue;
                var grass = Instantiate(grassPrefab, grassField.transform);
                grass.transform.position = world.CellToWorld(cell);
            }
            built = true;
        }

        private void CreateObject(Vector2Int cell, WorldObjectDefinition definition, int number)
        {
            var label = definition.DisplayName;
            var name = number == 1 ? label + " - first world object" : label + " " + number;
            if (definition.StableId == "flower_basic" && number == 1) name = "Flower - first bloom";
            if (definition.StableId == "tree_basic" && number == 1) name = "Tree - first tree";
            var root = new GameObject(name);
            root.transform.SetParent(objectRoot, false);
            root.transform.position = world.CellToWorld(cell);
            var examine = root.AddComponent<ExamineInteractable>();
            examine.Configure(player, world, hud, definition.ExamineMessage);

            if (!definition.RootedArtwork)
            {
                var renderer = root.AddComponent<SpriteRenderer>();
                renderer.sprite = definition.PreviewSprite;
                renderer.sortingOrder = definition.SortingOrder;
                root.AddComponent<BoxCollider2D>().size = definition.ClickColliderSize;
                if (definition.BlocksMovement && definition.Footprint == Vector2Int.one)
                    root.AddComponent<GridObstacle>();
                else if (definition.BlocksMovement)
                    AddObstacleCells(root, definition);
                return;
            }

            if (definition.BlocksMovement) AddObstacleCells(root, definition);
            var artwork = new GameObject(label + " artwork");
            artwork.transform.SetParent(root.transform, false);
            artwork.transform.localPosition = RootedSpriteLayout2D.OffsetFromBottomCenter(
                definition.PreviewSprite);
            var artRenderer = artwork.AddComponent<SpriteRenderer>();
            artRenderer.sprite = definition.PreviewSprite;
            artRenderer.sortingOrder = playerRenderer.sortingOrder - 1;
            artwork.AddComponent<BoxCollider2D>().size = definition.PreviewSprite.bounds.size;
            var depth = root.AddComponent<RootedWorldObjectDepth2D>();
            depth.Configure(world, player, playerRenderer, artRenderer);
        }

        private static void AddObstacleCells(GameObject root, WorldObjectDefinition definition)
        {
            for (var y = 0; y < definition.Footprint.y; y++)
            for (var x = 0; x < definition.Footprint.x; x++)
            {
                var obstacle = new GameObject(definition.DisplayName + " root obstacle");
                obstacle.transform.SetParent(root.transform, false);
                obstacle.transform.localPosition = new Vector3(x * GridWorld2D.TileWorldSize,
                    y * GridWorld2D.TileWorldSize);
                obstacle.AddComponent<BoxCollider2D>().size =
                    Vector2.one * GridWorld2D.TileWorldSize;
                obstacle.AddComponent<GridObstacle>();
            }
        }
    }
}
