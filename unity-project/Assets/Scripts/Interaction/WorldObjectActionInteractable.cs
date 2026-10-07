using Halka.Game.Player;
using Halka.Game.UI;
using Halka.Game.World;
using UnityEngine;

namespace Halka.Game.Interaction
{
    // Runs authored Action Points for multi-cell furniture and future actions.
    public sealed class WorldObjectActionInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private WorldObjectDefinition definition;
        [SerializeField] private PlayerMover player;
        [SerializeField] private GridWorld2D world;
        [SerializeField] private GameHud hud;
        [SerializeField] private string instanceText;

        public void Configure(WorldObjectDefinition objectDefinition, PlayerMover actor,
            GridWorld2D grid, GameHud messageHud, string signText)
        {
            definition = objectDefinition;
            player = actor;
            world = grid;
            hud = messageHud;
            instanceText = signText;
        }

        public void Interact()
        {
            if (definition == null || player == null || world == null || hud == null || player.IsMoving) return;
            foreach (var point in definition.ActionPoints)
            {
                if (player.Cell != CellAt(point)) continue;
                if (player.Facing != ToFacing(point.PlayerFacing)) continue;
                if (point.ActionType == WorldActionType.Examine)
                {
                    var text = !string.IsNullOrWhiteSpace(instanceText) ? instanceText :
                        !string.IsNullOrWhiteSpace(point.InteractionText) ? point.InteractionText :
                        definition.ExamineMessage;
                    if (!string.IsNullOrEmpty(text)) hud.ShowMessage(text);
                }
                else if (point.ActionType == WorldActionType.Sleep)
                {
                    // A real sleep pose and time change belong to the future time system.
                    hud.ShowMessage("ベッド。");
                }
                // Sit and None remain authored metadata without runtime behavior.
                return;
            }
        }

        private Vector2Int CellAt(WorldObjectActionPoint point) =>
            world.WorldToCell(transform.position) + point.PlayerCellOffset;

        private static FacingDirection ToFacing(WorldFacing facing)
        {
            switch (facing)
            {
                case WorldFacing.Up: return FacingDirection.Up;
                case WorldFacing.Down: return FacingDirection.Down;
                case WorldFacing.Left: return FacingDirection.Left;
                default: return FacingDirection.Right;
            }
        }
    }
}
