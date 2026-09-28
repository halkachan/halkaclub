using Halka.Game.UI;
using UnityEngine;

namespace Halka.Game.Interaction
{
    public sealed class PlaceholderInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private GameHud hud;

        public void Interact()
        {
            hud.ShowMessage("Touched.");
        }
    }
}
