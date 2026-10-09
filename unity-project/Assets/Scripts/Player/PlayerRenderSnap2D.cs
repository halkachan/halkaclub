using UnityEngine;

namespace Halka.Game.Player
{
    // Aligns only the artwork to the final render pixel grid. The mover and seat
    // keep their continuous world positions and authored action-point cells.
    [DefaultExecutionOrder(200)]
    public sealed class PlayerRenderSnap2D : MonoBehaviour
    {
        [SerializeField] private Camera worldCamera;
        [SerializeField] private CharacterVisual characterVisual;

        public bool Enabled { get; set; } = true;

        private void LateUpdate()
        {
            if (worldCamera == null || transform.parent == null) return;
            var basePosition = transform.parent.position;
            if (!Enabled)
            {
                transform.localPosition = Vector3.zero;
                return;
            }
            var screen = worldCamera.WorldToScreenPoint(basePosition);
            screen.x = Mathf.Round(screen.x);
            screen.y = Mathf.Round(screen.y);
            // Apply the authored frame correction after base-position snapping.
            // At 720p one source pixel is 1.25 screen pixels: rounding that
            // correction to one pixel would change the face's sampled shape.
            if (characterVisual != null)
                screen.y += characterVisual.HeadAlignmentWorldY *
                    Screen.height / (2f * worldCamera.orthographicSize);
            var aligned = worldCamera.ScreenToWorldPoint(screen);
            transform.position = new Vector3(aligned.x, aligned.y, basePosition.z);
        }
    }
}
