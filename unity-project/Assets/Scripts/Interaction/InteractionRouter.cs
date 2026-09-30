using UnityEngine;

namespace Halka.Game.Interaction
{
    public sealed class InteractionRouter : MonoBehaviour
    {
        [SerializeField] private Camera worldCamera;

        public bool IsInteractableAt(Vector2 screenPosition) => FindAt(screenPosition) != null;

        public void TryInteract(Vector2 screenPosition)
        {
            FindAt(screenPosition)?.Interact();
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
