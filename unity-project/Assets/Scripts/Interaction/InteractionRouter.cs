using Halka.Game.Player;
using Halka.Game.World;
using UnityEngine;

namespace Halka.Game.Interaction
{
    public sealed class InteractionRouter : MonoBehaviour
    {
        [SerializeField] private Camera worldCamera;
        [SerializeField] private PlayerMover player;
        [SerializeField] private GridWorld2D world;

        public bool IsInteractableAt(Vector2 screenPosition) => FindAt(screenPosition) != null;

        public void TryInteract(Vector2 screenPosition)
        {
            FindAt(screenPosition)?.Interact();
        }

        public void TryInteractAhead()
        {
            if (player.IsMoving) return;
            var targetCell = player.Cell + player.Facing.ToVector();
            Physics2D.SyncTransforms();
            foreach (var hit in Physics2D.OverlapBoxAll(world.CellToWorld(targetCell),
                Vector2.one * (GridWorld2D.TileWorldSize * 0.9f), 0f))
            {
                foreach (var component in hit.GetComponentsInParent<MonoBehaviour>())
                {
                    if (component is IInteractable interactable && component.isActiveAndEnabled &&
                        world.WorldToCell(component.transform.position) == targetCell)
                    {
                        interactable.Interact();
                        return;
                    }
                }
            }
        }

        private IInteractable FindAt(Vector2 screenPosition)
        {
            var world = worldCamera.ScreenToWorldPoint(screenPosition);
            var hit = Physics2D.OverlapPoint(world);
            if (hit == null) return null;
            foreach (var component in hit.GetComponentsInParent<MonoBehaviour>())
            {
                if (component is IInteractable interactable && component.isActiveAndEnabled)
                    return interactable;
            }
            return null;
        }
    }
}
