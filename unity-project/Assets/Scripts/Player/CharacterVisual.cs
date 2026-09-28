using UnityEngine;

namespace Halka.Game.Player
{
    public sealed class CharacterVisual : MonoBehaviour
    {
        public const float OriginalIdleFrameSeconds = 0.4f;
        [SerializeField] private PlayerMover mover;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Sprite[] frontIdle;
        [SerializeField] private Sprite[] backIdle;
        [SerializeField] private Sprite[] leftIdle;
        [SerializeField] private Sprite[] rightIdle;
        [SerializeField, Min(0.02f)] private float frameSeconds = OriginalIdleFrameSeconds;

        private Sprite[] currentFrames;
        private float frameTimer;
        private int frameIndex;

        private void Update()
        {
            var facing = mover.Facing;
            var selected = Mathf.Abs(facing.x) > Mathf.Abs(facing.y)
                ? (facing.x < 0 ? leftIdle : rightIdle)
                : (facing.y > 0 ? backIdle : frontIdle);

            // Unsupplied directional art keeps the existing character visible until real sprites arrive.
            if (selected == null || selected.Length == 0) selected = frontIdle;
            if (selected == null || selected.Length == 0) return;

            if (currentFrames != selected)
            {
                currentFrames = selected;
                frameIndex = 0;
                frameTimer = 0f;
            }
            if (currentFrames.Length > 1)
            {
                frameTimer += Time.deltaTime;
                while (frameTimer >= frameSeconds)
                {
                    frameTimer -= frameSeconds;
                    frameIndex = (frameIndex + 1) % currentFrames.Length;
                }
            }
            spriteRenderer.sprite = currentFrames[frameIndex];
        }
    }
}
