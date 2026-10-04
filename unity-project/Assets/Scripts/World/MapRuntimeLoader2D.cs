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
            if (map == null || world == null || surfaceField == null ||
                surfaceRoot == null || objectRoot == null ||
                player == null || playerRenderer == null || hud == null)
                throw new InvalidOperationException("MapRuntimeLoader2D has missing references");
            if (map.GrassMode == "auto" && (grassField == null || grassPrefab == null))
                throw new InvalidOperationException("Outdoor map needs grass field and prefab");
            var errors = MapPlacementRules.Validate(map);
            if (errors.Count != 0) throw new InvalidOperationException(
                "Invalid map " + map.MapId + ": " + string.Join("; ", errors));

            world.SetBounds(map.MinCell, map.MaxCell);
            if (map.BaseSurface != null && map.MapType == "interior")
            {
                for (var y = map.MinCell.y; y <= map.MaxCell.y; y++)
                for (var x = map.MinCell.x; x <= map.MaxCell.x; x++)
                    CreateSurfaceTile(new Vector2Int(x, y), map.BaseSurface.Sprite, -9, false);
            }
            foreach (var surface in map.Surfaces)
            {
                var sprite = surface.Definition.Sprite;
                if (sprite == null) throw new InvalidOperationException("Surface sprite missing at " + surface.Cell);
                surfaceField.AddSurface(surface.Cell, sprite);
                CreateSurfaceTile(surface.Cell, sprite, surface.Definition.BlocksMovement ? -7 : -9,
                    surface.Definition.BlocksMovement);
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

            if (map.GrassMode == "auto")
            {
                for (var y = map.MinCell.y; y <= map.MaxCell.y; y++)
                for (var x = map.MinCell.x; x <= map.MaxCell.x; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (!MapPlacementRules.HasGrass(map, cell)) continue;
                    var grass = Instantiate(grassPrefab, grassField.transform);
                    grass.transform.position = world.CellToWorld(cell);
                }
                grassField.Rebuild();
            }
            built = true;
        }

        private void CreateSurfaceTile(Vector2Int cell, Sprite sprite, int order, bool blocks)
        {
            var tile = new GameObject($"Surface {cell.x},{cell.y}");
            tile.transform.SetParent(surfaceRoot, false);
            tile.transform.position = world.CellToWorld(cell);
            var renderer = tile.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            if (blocks)
            {
                tile.AddComponent<BoxCollider2D>().size = Vector2.one * GridWorld2D.TileWorldSize;
                tile.AddComponent<GridObstacle>();
            }
        }

        public void Unload()
        {
            if (!built) return;
            ClearChildren(surfaceRoot);
            ClearChildren(objectRoot);
            if (grassField != null)
            {
                grassField.Clear();
                ClearChildren(grassField.transform);
            }
            surfaceField.Clear();
            built = false;
        }

        private static void ClearChildren(Transform parent)
        {
            var children = new List<GameObject>();
            foreach (Transform child in parent) children.Add(child.gameObject);
            foreach (var child in children)
            {
                child.SetActive(false);
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
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
            if (definition.Behavior == WorldObjectBehavior.Examine)
            {
                var examine = root.AddComponent<ExamineInteractable>();
                examine.Configure(player, world, hud, definition.ExamineMessage);
            }
            if (definition.BlocksMovement) AddObstacleCells(root, definition);
            var artwork = new GameObject(label + " artwork");
            artwork.transform.SetParent(root.transform, false);
            artwork.transform.localPosition = RootedSpriteLayout2D.OffsetFromBottomCenter(definition.PreviewSprite);
            if (definition.RootAnchor == "bottom-left")
                artwork.transform.localPosition += Vector3.right *
                    ((definition.PreviewSprite.bounds.size.x - GridWorld2D.TileWorldSize) * 0.5f);
            var artRenderer = artwork.AddComponent<SpriteRenderer>();
            artRenderer.sprite = definition.PreviewSprite;
            artRenderer.sortingOrder = definition.RootedArtwork ? playerRenderer.sortingOrder - 1 : definition.SortingOrder;
            if (definition.Behavior == WorldObjectBehavior.Examine)
                artwork.AddComponent<BoxCollider2D>().size = definition.PreviewSprite.bounds.size;
            if (definition.RootedArtwork)
            {
                var depth = root.AddComponent<RootedWorldObjectDepth2D>();
                depth.Configure(world, player, playerRenderer, artRenderer);
            }
        }

        private static void AddObstacleCells(GameObject root, WorldObjectDefinition definition)
        {
            foreach (var offset in definition.EffectiveBlockedOffsets())
            {
                var obstacle = new GameObject(definition.DisplayName + " root obstacle");
                obstacle.transform.SetParent(root.transform, false);
                obstacle.transform.localPosition = new Vector3(offset.x * GridWorld2D.TileWorldSize,
                    offset.y * GridWorld2D.TileWorldSize);
                obstacle.AddComponent<BoxCollider2D>().size =
                    Vector2.one * GridWorld2D.TileWorldSize;
                obstacle.AddComponent<GridObstacle>();
            }
        }
    }
}
