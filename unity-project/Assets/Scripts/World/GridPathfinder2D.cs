using System.Collections.Generic;
using UnityEngine;

namespace Halka.Game.World
{
    public static class GridPathfinder2D
    {
        private static readonly Vector2Int[] Directions =
            { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

        public static bool TryFind(GridWorld2D world, Vector2Int start, Vector2Int goal,
            List<Vector2Int> path)
        {
            path.Clear();
            if (start == goal || !world.CanEnter(goal)) return false;
            var queue = new Queue<Vector2Int>();
            var parents = new Dictionary<Vector2Int, Vector2Int> { [start] = start };
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                if (cell == goal) break;
                foreach (var direction in Directions)
                {
                    var next = cell + direction;
                    if (parents.ContainsKey(next) || !world.CanEnter(next)) continue;
                    parents.Add(next, cell);
                    queue.Enqueue(next);
                }
            }
            if (!parents.ContainsKey(goal)) return false;
            for (var cell = goal; cell != start; cell = parents[cell]) path.Add(cell);
            path.Reverse();
            return true;
        }
    }
}
