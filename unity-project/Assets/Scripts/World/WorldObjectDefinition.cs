using System.Collections.Generic;
using UnityEngine;

namespace Halka.Game.World
{
    public enum WorldObjectBehavior { Examine, HouseTransition, None }

    public enum WorldActionType { None, Examine, Sit, Sleep }
    public enum WorldFacing { Up, Down, Left, Right }

    [System.Serializable]
    public sealed class WorldObjectActionPoint
    {
        [SerializeField] private string id;
        [SerializeField] private Vector2Int playerCellOffset;
        [SerializeField] private WorldFacing playerFacing;
        [SerializeField] private WorldActionType actionType;
        [SerializeField] private string poseKey;
        [SerializeField] private string interactionText;

        public string Id => id;
        public Vector2Int PlayerCellOffset => playerCellOffset;
        public WorldFacing PlayerFacing => playerFacing;
        public WorldActionType ActionType => actionType;
        public string PoseKey => poseKey;
        public string InteractionText => interactionText;
    }

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
        [SerializeField] private List<WorldObjectActionPoint> actionPoints = new List<WorldObjectActionPoint>();
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
        public IReadOnlyList<WorldObjectActionPoint> ActionPoints => actionPoints;
        public IEnumerable<Vector2Int> EffectiveBlockedOffsets()
        {
            if (!blocksMovement) yield break;
            foreach (var offset in blockedCellOffsets) yield return offset;
        }
        public bool BlocksMovement => blocksMovement;
        public bool ExcludesGrass => excludesGrass;
        public int SortingOrder => sortingOrder;
    }
}
