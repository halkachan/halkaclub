using UnityEngine;

namespace Halka.Game.World
{
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
        public bool BlocksMovement => blocksMovement;
        public bool ExcludesGrass => excludesGrass;
        public int SortingOrder => sortingOrder;
    }
}
