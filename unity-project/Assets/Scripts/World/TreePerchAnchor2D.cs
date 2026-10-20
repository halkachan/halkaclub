using UnityEngine;

namespace Halka.Game.World
{
    // Built from each authored tree instance; no map coordinates are stored here.
    public sealed class TreePerchAnchor2D : MonoBehaviour, ICrowPerch
    {
        [SerializeField] private SpriteRenderer artwork;
        [SerializeField, Range(-0.35f, 0.35f)] private float horizontalFraction = -0.12f;
        [SerializeField, Range(0.05f, 0.45f)] private float insetFromTop = 0.28f;

        public void Configure(SpriteRenderer renderer) => artwork = renderer;
        public bool IsUsable => artwork != null && artwork.enabled && artwork.sprite != null && gameObject.activeInHierarchy;
        public Bounds VisualBounds => artwork.bounds;
        public Vector3 PerchPosition
        {
            get
            {
                var bounds = VisualBounds;
                return new Vector3(bounds.center.x + bounds.size.x * horizontalFraction,
                    bounds.max.y - bounds.size.y * insetFromTop, transform.position.z);
            }
        }
    }
}
