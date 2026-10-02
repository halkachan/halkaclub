using Halka.Game.Player;
using UnityEngine;

namespace Halka.Game.World
{
    [DefaultExecutionOrder(70)]
    public sealed class RootedWorldObjectDepth2D : MonoBehaviour
    {
        [SerializeField] private GridWorld2D world;
        [SerializeField] private PlayerMover player;
        [SerializeField] private SpriteRenderer playerRenderer;
        [SerializeField] private SpriteRenderer objectRenderer;

        private void LateUpdate() => UpdateSorting();

        public void UpdateSorting()
        {
            // During a step, the rendered foot crosses into the next cell halfway through.
            var footCellY = world.WorldToCell(player.transform.position).y;
            var rootCellY = world.WorldToCell(transform.position).y;
            var order = footCellY > rootCellY
                ? playerRenderer.sortingOrder + 1 : playerRenderer.sortingOrder - 1;
            if (objectRenderer.sortingOrder != order)
                objectRenderer.sortingOrder = order;
        }
    }
}
