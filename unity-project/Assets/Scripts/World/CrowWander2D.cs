using System.Collections.Generic;
using UnityEngine;

namespace Halka.Game.World
{
    // A small walker: one interpolated grid step, then a few seconds of rest.
    // ver3.0: the behaviour lives in CrowBehaviourCore (shared with the 2D/3D world); this is the
    // 2D game's host: GridWorld2D, the dynamic occupancy, the trees' sprite perches, the grass
    // rustle, and the existing SpriteRenderer, sorting and click collider.
    public sealed class CrowWander2D : CrowBehaviourCore
    {
        [SerializeField] private GridWorld2D world;
        [SerializeField] private DynamicGridOccupancy2D occupancy;
        [SerializeField] private GrassField2D grassField;
        [SerializeField] private SpriteRenderer artwork;
        [SerializeField] private BoxCollider2D clickCollider;

        private Transform treeRoot;
        private int groundSortingOrder;

        public TreePerchAnchor2D TargetTree => targetTree as TreePerchAnchor2D;

        public void SetGrassField(GrassField2D field) => grassField = field;
        public void ConfigureFlightEnvironment(MapDefinition map, Transform objects)
        {
            activeMap = map;
            treeRoot = objects;
        }

        public void ApplySpawn(EntitySpawnPlacement spawn, GridWorld2D grid)
        {
            RequireCrowSpawn(spawn);
            world = grid;
            if (groundSortingOrder == 0 && artwork != null)
                groundSortingOrder = artwork.sortingOrder;
            ApplySpawnState(spawn);
            if (clickCollider != null) clickCollider.enabled = true;
            if (artwork != null)
            {
                artwork.flipX = false;
                artwork.sortingOrder = groundSortingOrder;
                RefreshVisual();
            }
        }

        public TreePerchAnchor2D[] FindPerchCandidates()
        {
            if (treeRoot == null || activeMap == null || world == null)
                return System.Array.Empty<TreePerchAnchor2D>();
            var found = new List<TreePerchAnchor2D>();
            foreach (var tree in treeRoot.GetComponentsInChildren<TreePerchAnchor2D>())
            {
                if (!tree.IsUsable) continue;
                if (!activeMap.Contains(world.WorldToCell(tree.PerchPosition))) continue;
                found.Add(tree);
            }
            return found.ToArray();
        }

        protected override IReadOnlyList<ICrowPerch> PerchCandidates() => FindPerchCandidates();
        protected override bool CanEnterCell(Vector2Int cell) => world != null && world.CanEnter(cell);
        protected override bool CanCrowEnterCell(Vector2Int cell) =>
            occupancy == null || occupancy.CanCrowEnter(cell);
        protected override Vector3 CellToPosition(Vector2 cell) => world.CellToWorld(cell);
        protected override Vector2Int PositionToCell(Vector3 position) => world.WorldToCell(position);
        protected override void RustleAt(Vector2Int cell) => grassField.RustleAt(cell);

        protected override bool HasVisual => artwork != null;

        protected override void ShowFrame(Sprite sprite, bool flipX)
        {
            artwork.flipX = flipX;
            artwork.sprite = sprite;
        }

        protected override void SetPointerInteraction(bool enabled)
        {
            if (clickCollider != null) clickCollider.enabled = enabled;
        }

        protected override void OnTakeoffVisual()
        {
            artwork.sortingOrder = Mathf.Max(groundSortingOrder + 1, 12);
            artwork.maskInteraction = SpriteMaskInteraction.None;
        }

        protected override void OnLandedVisual()
        {
            artwork.sortingOrder = groundSortingOrder;
            artwork.flipX = false;
        }

        protected override void Awake()
        {
            groundSortingOrder = artwork != null ? artwork.sortingOrder : 3;
            base.Awake();
        }
    }
}
