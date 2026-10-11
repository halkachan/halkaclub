using System;
using System.Collections.Generic;
using Halka.Game.World;
using UnityEngine;

namespace Halka.Game.Player
{
    // Only destination selection is new. Movement still uses GridPathfinder2D and PlayerMover.
    public static class IndoorAutoRoutePlanner2D
    {
        private static readonly Vector2Int[] Directions =
            { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

        public static List<Vector2Int> CollectDestinations(MapDefinition map, Vector2Int start,
            Vector2Int? previousGoal, Func<Vector2Int, bool> canEnter)
        {
            var destinations = new List<Vector2Int>();
            if (map == null || canEnter == null || !map.Contains(start)) return destinations;
            var hasExit = map.TryGetMarker("interior_exit", out var exit);
            var distances = new Dictionary<Vector2Int, int> { [start] = 0 };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                foreach (var direction in Directions)
                {
                    var next = cell + direction;
                    if (distances.ContainsKey(next) || !map.Contains(next) ||
                        hasExit && next == exit || !canEnter(next)) continue;
                    distances[next] = distances[cell] + 1;
                    queue.Enqueue(next);
                }
            }

            // Prefer a real stroll; relax the distance only in a small edited room.
            for (var minimum = 3; minimum >= 1; minimum--)
            {
                destinations.Clear();
                foreach (var pair in distances)
                    if (pair.Value >= minimum && pair.Key != previousGoal)
                        destinations.Add(pair.Key);
                if (destinations.Count > 0) return destinations;
            }
            foreach (var pair in distances)
                if (pair.Value > 0) destinations.Add(pair.Key);
            return destinations;
        }
    }
}
