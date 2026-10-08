using UnityEngine;

namespace Halka.Game.Player
{
    [DefaultExecutionOrder(50)]
    public sealed class CharacterVisual : MonoBehaviour
    {
        public const float OriginalIdleFrameSeconds = 0.4f;
        public const float OriginalWalkFrameSeconds = 0.15f;
        [SerializeField] private PlayerMover mover;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private PlayerSeatController seat;
        [SerializeField] private Sprite benchSit;
        [SerializeField] private Sprite[] frontIdle;
        [SerializeField] private Sprite[] backIdle;
        [SerializeField] private Sprite[] leftIdle;
        [SerializeField] private Sprite[] rightIdle;
        [SerializeField] private Sprite[] frontWalk;
        [SerializeField] private Sprite[] backWalk;
        [SerializeField] private Sprite[] leftWalk;
        [SerializeField] private Sprite[] rightWalk;

        private Sprite[] currentFrames;
        private float frameTimer;
        private int frameIndex;

        private void Update()
        {
            if (seat != null && seat.IsSeated)
            {
                spriteRenderer.sprite = benchSit;
                return;
            }
            var selected = SelectFrames(mover.Facing, mover.IsMoving);
            if (selected == null || selected.Length == 0) return;

            if (currentFrames != selected)
            {
                currentFrames = selected;
                frameIndex = 0;
                frameTimer = 0f;
            }
            var frameSeconds = mover.IsMoving ? OriginalWalkFrameSeconds : OriginalIdleFrameSeconds;
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

        private Sprite[] SelectFrames(FacingDirection facing, bool walking)
        {
            switch (facing)
            {
                case FacingDirection.Up: return walking ? backWalk : backIdle;
                case FacingDirection.Left: return walking ? leftWalk : leftIdle;
                case FacingDirection.Right: return walking ? rightWalk : rightIdle;
                default: return walking ? frontWalk : frontIdle;
            }
        }
    }
}
