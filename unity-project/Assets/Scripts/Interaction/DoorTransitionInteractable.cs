using Halka.Game.Player;
using Halka.Game.World;
using UnityEngine;

namespace Halka.Game.Interaction
{
    public sealed class DoorTransitionInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private PlayerMover player;
        [SerializeField] private GridWorld2D world;
        [SerializeField] private HouseArea2D house;
        [SerializeField] private bool entersHouse;

        public void Interact()
        {
            if (house.IsInside == entersHouse ||
                !ExamineInteractable.IsInRange(player.Cell, player.IsMoving, player.Facing,
                    world.WorldToCell(transform.position))) return;
            if (entersHouse) house.Enter();
            else house.Exit();
        }
    }
}
