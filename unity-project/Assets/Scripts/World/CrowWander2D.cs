using Halka.Game.Player;
using UnityEngine;

namespace Halka.Game.World
{
    public sealed class CrowWander2D : MonoBehaviour
    {
        public static readonly Vector2Int InitialCell = new Vector2Int(7, 2);
        public static readonly Vector2Int RangeMin = new Vector2Int(4, 0);
        public static readonly Vector2Int RangeMax = new Vector2Int(8, 4);

        [SerializeField] private GridWorld2D world;
        [SerializeField] private SpriteRenderer artwork;
        [SerializeField] private Sprite idle;
        [SerializeField] private Sprite hopOne;
        [SerializeField] private Sprite hopTwo;

        private float nextActionAt;
        private float hopStartedAt = -10f;

        public Vector2Int Cell => world.WorldToCell(transform.position);

        private void Awake()
        {
            nextActionAt = Time.time + 2f;
            artwork.sprite = idle;
        }

        private void Update() => Tick(Time.time);

        public void Tick(float now)
        {
            if (now - hopStartedAt < 0.3f)
                artwork.sprite = now - hopStartedAt < 0.15f ? hopOne : hopTwo;
            else artwork.sprite = idle;
            if (now < nextActionAt) return;
            nextActionAt = now + Random.Range(2f, 4f);
            if (Random.value < 0.3f) return;
            var directions = new[] { Vector2Int.up, Vector2Int.down,
                Vector2Int.left, Vector2Int.right };
            var start = Random.Range(0, directions.Length);
            for (var i = 0; i < directions.Length; i++)
            {
                var candidate = Cell + directions[(start + i) % directions.Length];
                if (candidate.x < RangeMin.x || candidate.x > RangeMax.x ||
                    candidate.y < RangeMin.y || candidate.y > RangeMax.y ||
                    !world.CanEnter(candidate)) continue;
                transform.position = world.CellToWorld(candidate);
                hopStartedAt = now;
                artwork.sprite = hopOne;
                break;
            }
        }
    }
}
