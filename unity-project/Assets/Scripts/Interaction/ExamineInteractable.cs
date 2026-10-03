using Halka.Game.Player;
using Halka.Game.UI;
using Halka.Game.World;
using UnityEngine;

namespace Halka.Game.Interaction
{
    public sealed class ExamineInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private string message;
        [SerializeField] private PlayerMover player;
        [SerializeField] private GridWorld2D world;
        [SerializeField] private GameHud hud;
        [SerializeField] private MonoBehaviour availability;
        [SerializeField] private InteractionAudio interactionAudio;

        public void Configure(PlayerMover actor, GridWorld2D grid, GameHud messageHud,
            string text)
        {
            player = actor;
            world = grid;
            hud = messageHud;
            message = text;
        }

        public static bool IsInRange(Vector2Int playerCell, bool isMoving,
            FacingDirection facing, Vector2Int targetCell)
        {
            if (isMoving) return false;
            var delta = targetCell - playerCell;
            return Mathf.Abs(delta.x) + Mathf.Abs(delta.y) == 1 &&
                delta == facing.ToVector();
        }

        public void Interact()
        {
            if (!IsInRange(player.Cell, player.IsMoving, player.Facing,
                world.WorldToCell(transform.position))) return;
            if (availability is IInteractionAvailability state && !state.CanInteract) return;
            hud.ShowMessage(message);
            if (interactionAudio != null) interactionAudio.Play();
        }
    }
}
