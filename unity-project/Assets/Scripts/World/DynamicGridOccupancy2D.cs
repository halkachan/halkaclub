using Halka.Game.Player;
using UnityEngine;

namespace Halka.Game.World
{
    // Moving actors reserve both ends of their current one-cell step.
    // Static obstacles remain the responsibility of GridWorld2D.
    public sealed class DynamicGridOccupancy2D : MonoBehaviour
    {
        [SerializeField] private PlayerMover player;
        [SerializeField] private CrowWander2D crow;

        public bool CanPlayerEnter(Vector2Int cell) =>
            crow == null || !crow.isActiveAndEnabled || !IsReservedByCrow(cell);

        public bool CanCrowEnter(Vector2Int cell) =>
            player == null || !player.isActiveAndEnabled || !IsReservedByPlayer(cell);

        public bool IsReservedByCrow(Vector2Int cell) =>
            crow != null && crow.isActiveAndEnabled &&
            (crow.Cell == cell || crow.IsMoving &&
                (crow.StepFromCell == cell || crow.StepToCell == cell));

        public bool IsReservedByPlayer(Vector2Int cell) =>
            player != null && player.isActiveAndEnabled &&
            (player.Cell == cell || player.IsMoving &&
                (player.StepFromCell == cell || player.StepToCell == cell));
    }
}
