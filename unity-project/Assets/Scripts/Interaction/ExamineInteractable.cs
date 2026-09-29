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

        public static bool IsInRange(Vector2Int playerCell, bool isMoving, Vector2Int targetCell)
        {
            if (isMoving) return false;
            var delta = targetCell - playerCell;
            return Mathf.Abs(delta.x) + Mathf.Abs(delta.y) == 1;
        }

        public void Interact()
        {
            if (!IsInRange(player.Cell, player.IsMoving, world.WorldToCell(transform.position))) return;
            hud.ShowMessage(message);
        }
    }
}
