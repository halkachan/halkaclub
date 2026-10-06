using UnityEngine;

namespace Halka.Game.World
{
    // Catalog-backed definition for a dynamic map inhabitant.
    public sealed class EntityDefinition : ScriptableObject
    {
        [SerializeField] private string stableId;
        [SerializeField] private string displayName;
        [SerializeField] private Sprite previewSprite;
        [SerializeField] private Vector2Int visualCells = Vector2Int.one;
        [SerializeField] private string defaultFacing = "down";
        [SerializeField] private string runtimeBehavior;
        [SerializeField] private string spawnMode;
        [SerializeField] private bool blocksMovement = true;
        [SerializeField] private int maxInstances = 1;
        [SerializeField] private Vector2Int wanderMinimum;
        [SerializeField] private Vector2Int wanderMaximum;

        public string StableId => stableId;
        public string DisplayName => displayName;
        public Sprite PreviewSprite => previewSprite;
        public Vector2Int VisualCells => visualCells;
        public string DefaultFacing => defaultFacing;
        public string RuntimeBehavior => runtimeBehavior;
        public string SpawnMode => spawnMode;
        public bool BlocksMovement => blocksMovement;
        public int MaxInstances => maxInstances;
        public Vector2Int WanderMinimum => wanderMinimum;
        public Vector2Int WanderMaximum => wanderMaximum;
    }
}
