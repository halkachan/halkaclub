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
        private PlayerSeatController seat;

        public void Configure(WorldObjectDefinition objectDefinition, PlayerMover actor,
            GridWorld2D grid, GameHud messageHud, string signText)
        {
            definition = objectDefinition;
            player = actor;
            world = grid;
            hud = messageHud;
            instanceText = signText;
            seat = actor != null ? actor.GetComponent<PlayerSeatController>() : null;
        }

        public bool CanInteractAt(Vector2Int playerCell, FacingDirection facing)
        {
            if (definition == null || world == null) return false;
            foreach (var point in definition.ActionPoints)
                if (playerCell == CellAt(point) && facing == ToFacing(point.PlayerFacing)) return true;
            return false;
        }

        public void Interact()
        {
            if (definition == null || player == null || world == null || hud == null || player.IsMoving) return;
            if (seat != null && seat.IsSeated)
            {
                if (seat.ActiveSeat == transform) seat.Stand();
                return;
            }
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
                else if (point.ActionType == WorldActionType.Sit && seat != null)
                    seat.TrySit(transform, point.PoseKey);
                return;
            }
        }

        private void OnDisable()
        {
            if (seat != null) seat.StandIfActive(transform);
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
