using UnityEngine;

namespace Halka.Game.World
{
    public sealed class GridWorld2D : MonoBehaviour
    {
        public const int TilePixels = 32;
        public const float TileWorldSize = 0.5f;

        [SerializeField, Min(0.1f)] private float cellSize = TileWorldSize;
        [SerializeField] private Vector2Int minCell = new Vector2Int(-10, -6);
        [SerializeField] private Vector2Int maxCell = new Vector2Int(10, 6);

        public float CellSize => cellSize;
        public Vector2Int MinCell => minCell;
        public Vector2Int MaxCell => maxCell;

        public void SetBounds(Vector2Int minimum, Vector2Int maximum)
        {
            if (minimum.x > maximum.x || minimum.y > maximum.y)
                throw new System.ArgumentException("Grid bounds are reversed");
            minCell = minimum;
            maxCell = maximum;
        }

        public Vector2Int WorldToCell(Vector3 position) => new Vector2Int(
            Mathf.RoundToInt((position.x - transform.position.x) / cellSize),
            Mathf.RoundToInt((position.y - transform.position.y) / cellSize));

        public Vector3 CellToWorld(Vector2 cell) => new Vector3(
            transform.position.x + cell.x * cellSize,
            transform.position.y + cell.y * cellSize,
            0f);

        public bool CanEnter(Vector2Int cell)
        {
            if (cell.x < minCell.x || cell.x > maxCell.x ||
                cell.y < minCell.y || cell.y > maxCell.y) return false;

            return !HasObstacle(cell);
        }

        public bool HasObstacle(Vector2Int cell)
        {
            Physics2D.SyncTransforms();
            foreach (var hit in Physics2D.OverlapPointAll(CellToWorld(cell)))
            {
                if (hit.GetComponentInParent<GridObstacle>() != null) return true;
            }
            return false;
        }
    }
}
