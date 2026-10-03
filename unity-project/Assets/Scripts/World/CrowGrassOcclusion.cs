using System;
using UnityEngine;

namespace Halka.Game.World
{
    [DefaultExecutionOrder(60)]
    public sealed class CrowGrassOcclusion : MonoBehaviour
    {
        public const int HiddenPixels = 4;

        [SerializeField] private CrowWander2D crow;
        [SerializeField] private GrassField2D grassField;
        [SerializeField] private SpriteRenderer artwork;
        [SerializeField] private SpriteMask spriteMask;

        public bool IsMasked => artwork.maskInteraction == SpriteMaskInteraction.VisibleInsideMask;

        private void Awake()
        {
            if (crow == null || grassField == null || artwork == null ||
                spriteMask == null || spriteMask.sprite == null)
                throw new InvalidOperationException("Crow grass mask references are incomplete");
            spriteMask.isCustomRangeActive = true;
            spriteMask.frontSortingLayerID = artwork.sortingLayerID;
            spriteMask.backSortingLayerID = artwork.sortingLayerID;
            spriteMask.frontSortingOrder = artwork.sortingOrder + 1;
            spriteMask.backSortingOrder = artwork.sortingOrder - 1;
            RefreshMask();
        }

        private void LateUpdate() => RefreshMask();

        private void OnDisable()
        {
            if (artwork != null) artwork.maskInteraction = SpriteMaskInteraction.None;
            if (spriteMask != null) spriteMask.enabled = false;
        }

        public void RefreshMask()
        {
            var visualCell = crow.IsMoving && crow.StepProgressNormalized >= 0.5f
                ? crow.StepToCell : crow.Cell;
            var masked = grassField.HasGrass(visualCell);
            if (spriteMask.enabled == masked && IsMasked == masked) return;
            spriteMask.enabled = masked;
            artwork.maskInteraction = masked
                ? SpriteMaskInteraction.VisibleInsideMask : SpriteMaskInteraction.None;
        }
    }
}
