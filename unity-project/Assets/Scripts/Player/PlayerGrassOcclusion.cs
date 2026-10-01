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
            SetMasked(grassField.HasGrass(mover.Cell));
        }

        private void OnEnable()
        {
            if (mover == null) return;
            mover.StepStarted += OnStepStarted;
            mover.StepCompleted += OnStepCompleted;
        }

        private void OnDisable()
        {
            if (mover != null)
            {
                mover.StepStarted -= OnStepStarted;
                mover.StepCompleted -= OnStepCompleted;
            }
            if (playerRenderer != null) playerRenderer.maskInteraction = SpriteMaskInteraction.None;
            if (spriteMask != null) spriteMask.enabled = false;
        }

        private void OnStepStarted(Vector2Int destinationCell)
        {
            SetMasked(grassField.HasGrass(mover.Cell) || grassField.HasGrass(destinationCell));
        }

        private void OnStepCompleted(Vector2Int cell)
        {
            SetMasked(grassField.HasGrass(cell));
        }

        private void SetMasked(bool masked)
        {
            spriteMask.enabled = masked;
            playerRenderer.maskInteraction = masked
                ? SpriteMaskInteraction.VisibleInsideMask : SpriteMaskInteraction.None;
        }
    }
}
