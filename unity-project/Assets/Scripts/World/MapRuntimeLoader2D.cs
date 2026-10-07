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
        [SerializeField] private Transform entityRoot;
        [SerializeField] private GameObject grassPrefab;
        [SerializeField] private PlayerMover player;
        [SerializeField] private SpriteRenderer playerRenderer;
        [SerializeField] private GameHud hud;
        [SerializeField] private CrowWander2D crow;

        private bool built;
        private bool initialStartApplied;
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
            var needsGrass = map.GrassMode == "auto" ||
                System.Linq.Enumerable.Any(map.Surfaces, placement =>
                    placement.Definition != null && placement.Definition.GrowsGrass);
            if (needsGrass && (grassField == null || grassPrefab == null))
                throw new InvalidOperationException("Grass surface needs a field and prefab");
            var errors = MapPlacementRules.Validate(map);
            if (errors.Count != 0) throw new InvalidOperationException(
                "Invalid map " + map.MapId + ": " + string.Join("; ", errors));

            world.SetBounds(map.MinCell, map.MaxCell);
            if (!initialStartApplied)
                initialStartApplied = EntityRuntimeFactory2D.TryApplyPlayerStart(map, world, player);
            if (map.BaseSurface != null && map.MapType == "interior")
            {
                for (var y = map.MinCell.y; y <= map.MaxCell.y; y++)
                for (var x = map.MinCell.x; x <= map.MaxCell.x; x++)
                    CreateSurfaceTile(new Vector2Int(x, y), map.BaseSurface.Sprite, -10, false);
            }
            foreach (var surface in map.Surfaces)
            {
                var sprite = surface.Definition.Sprite;
                if (sprite == null) throw new InvalidOperationException("Surface sprite missing at " + surface.Cell);
                surfaceField.AddSurface(surface.Cell, sprite);
                CreateSurfaceTile(surface.Cell, sprite, surface.Definition.BlocksMovement ? -7 : -9,
                    surface.Definition.BlocksMovement, surface.Definition.GrowsGrass);
            }

            var serial = new Dictionary<WorldObjectDefinition, int>();
            foreach (var placement in map.Objects)
            {
                var definition = placement.Definition;
                if (definition.PreviewSprite == null)
                    throw new InvalidOperationException("Object sprite missing: " + definition.StableId);
                serial.TryGetValue(definition, out var number);
                serial[definition] = ++number;
                CreateObject(placement, number);
            }

            if (needsGrass)
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
            EntityRuntimeFactory2D.TryActivateCrow(map, world, grassField, entityRoot, crow);
            built = true;
        }

        private void CreateSurfaceTile(Vector2Int cell, Sprite sprite, int order, bool blocks,
            bool grassGround = false)
        {
            var tile = new GameObject($"Surface {cell.x},{cell.y}");
            tile.transform.SetParent(surfaceRoot, false);
            tile.transform.position = world.CellToWorld(cell);
            var renderer = tile.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            if (grassGround && sprite.rect.width == 1 && sprite.rect.height == 1)
            {
                tile.transform.localScale = new Vector3(
                    GridWorld2D.TileWorldSize / sprite.bounds.size.x,
                    GridWorld2D.TileWorldSize / sprite.bounds.size.y, 1f);
                renderer.color = new Color(0.85f, 0.91f, 0.72f);
            }

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

        private void CreateObject(WorldObjectPlacement placement, int number)
        {
            var cell = placement.RootCell;
            var definition = placement.Definition;
            var label = definition.DisplayName;
            var name = number == 1 ? label + " - first world object" : label + " " + number;
            if (definition.StableId == "flower_basic" && number == 1) name = "Flower - first bloom";
            if (definition.StableId == "tree_basic" && number == 1) name = "Tree - first tree";
            var root = new GameObject(name);
            root.transform.SetParent(objectRoot, false);
            root.transform.position = world.CellToWorld(cell);
            var authoredAction = definition.ActionPoints.Count > 0 &&
                (definition.Footprint.x > 1 || definition.Footprint.y > 1 ||
                 definition.Behavior == WorldObjectBehavior.None);
            if (authoredAction)
            {
                var action = root.AddComponent<WorldObjectActionInteractable>();
                action.Configure(definition, player, world, hud, placement.SignText);
            }
            else if (definition.Behavior == WorldObjectBehavior.Examine)
            {
                var examine = root.AddComponent<ExamineInteractable>();
                var message = definition.StableId == "sign_basic" &&
                    !string.IsNullOrWhiteSpace(placement.SignText)
                    ? placement.SignText : definition.ExamineMessage;
                examine.Configure(player, world, hud, message);
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
            if (authoredAction || definition.Behavior == WorldObjectBehavior.Examine)
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
