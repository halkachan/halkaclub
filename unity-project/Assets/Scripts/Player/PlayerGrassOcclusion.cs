using System;
using Halka.Game.World;
using UnityEngine;

namespace Halka.Game.Player
{
    [DefaultExecutionOrder(60)]
    public sealed class PlayerGrassOcclusion : MonoBehaviour
    {
        public const int GrassPlayerOcclusionPixels = 10;

        [SerializeField] private PlayerMover mover;
        [SerializeField] private GrassField2D grassField;
        [SerializeField] private SpriteRenderer playerRenderer;
        [SerializeField] private SpriteMask spriteMask;

        public bool IsMasked => playerRenderer.maskInteraction == SpriteMaskInteraction.VisibleInsideMask;

        public void UseGrassField(GrassField2D field)
        {
            if (field == null) throw new ArgumentNullException(nameof(field));
            grassField = field;
            RefreshMask();
        }

        private void Awake()
        {
            if (mover == null || grassField == null || playerRenderer == null || spriteMask == null ||
                spriteMask.sprite == null)
                throw new InvalidOperationException("Player grass mask references are incomplete");

            spriteMask.isCustomRangeActive = true;
            spriteMask.frontSortingLayerID = playerRenderer.sortingLayerID;
            spriteMask.backSortingLayerID = playerRenderer.sortingLayerID;
            spriteMask.frontSortingOrder = playerRenderer.sortingOrder + 1;
            spriteMask.backSortingOrder = playerRenderer.sortingOrder - 1;
            RefreshMask();
        }

        private void LateUpdate()
        {
            RefreshMask();
        }

        private void OnDisable()
        {
            if (playerRenderer != null) playerRenderer.maskInteraction = SpriteMaskInteraction.None;
            if (spriteMask != null) spriteMask.enabled = false;
        }

        private void RefreshMask()
        {
            var visualCell = mover.IsMoving && mover.StepProgressNormalized >= 0.5f
                ? mover.StepToCell : mover.Cell;
            SetMasked(grassField.HasGrass(visualCell));
        }

        private void SetMasked(bool masked)
        {
            if (spriteMask.enabled == masked && IsMasked == masked) return;
            spriteMask.enabled = masked;
            playerRenderer.maskInteraction = masked
                ? SpriteMaskInteraction.VisibleInsideMask : SpriteMaskInteraction.None;
        }
    }
}
