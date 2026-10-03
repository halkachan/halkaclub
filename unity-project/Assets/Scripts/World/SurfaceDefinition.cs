using UnityEngine;

namespace Halka.Game.World
{
    [CreateAssetMenu(menuName = "HALKA WORLD/Surface Definition")]
    public sealed class SurfaceDefinition : ScriptableObject
    {
        [SerializeField] private string stableId;
        [SerializeField] private string displayName;
        [SerializeField] private Sprite sprite;

        public string StableId => stableId;
        public string DisplayName => displayName;
        public Sprite Sprite => sprite;
    }
}
