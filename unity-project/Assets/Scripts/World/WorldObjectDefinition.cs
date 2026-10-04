using System.Collections.Generic;
using UnityEngine;

namespace Halka.Game.World
{
    public enum WorldObjectBehavior { Examine, HouseTransition, None }

    [CreateAssetMenu(menuName = "HALKA WORLD/World Object Definition")]
    public sealed class WorldObjectDefinition : ScriptableObject
    {
        [SerializeField] private string stableId;
        [SerializeField] private string displayName;
        [SerializeField] private Sprite previewSprite;
        [SerializeField] private string examineMessage;
        [SerializeField] private bool rootedArtwork;
        [SerializeField] private Vector2 clickColliderSize = Vector2.one * GridWorld2D.TileWorldSize;
        [SerializeField] private Vector2Int footprint = Vector2Int.one;
        [SerializeField] private List<Vector2Int> blockedCellOffsets = new List<Vector2Int>();
        [SerializeField] private string rootAnchor = "bottom-center";
        [SerializeField] private WorldObjectBehavior behavior = WorldObjectBehavior.Examine;
        [SerializeField] private bool blocksMovement = true;
        [SerializeField] private bool excludesGrass = true;
        [SerializeField] private int sortingOrder = 2;

        public string StableId => stableId;
        public string DisplayName => displayName;
        public Sprite PreviewSprite => previewSprite;
        public string ExamineMessage => examineMessage;
        public bool RootedArtwork => rootedArtwork;
        public Vector2 ClickColliderSize => clickColliderSize;
        public Vector2Int Footprint => footprint;
        public string RootAnchor => rootAnchor;
        public WorldObjectBehavior Behavior => behavior;
        public IReadOnlyList<Vector2Int> BlockedCellOffsets => blockedCellOffsets;
        public IEnumerable<Vector2Int> EffectiveBlockedOffsets()
        {
            if (blockedCellOffsets.Count > 0)
            {
                foreach (var offset in blockedCellOffsets) yield return offset;
                yield break;
            }
            for (var y = 0; y < footprint.y; y++)
            for (var x = 0; x < footprint.x; x++) yield return new Vector2Int(x, y);
        }
        public bool BlocksMovement => blocksMovement;
        public bool ExcludesGrass => excludesGrass;
        public int SortingOrder => sortingOrder;
    }
}
