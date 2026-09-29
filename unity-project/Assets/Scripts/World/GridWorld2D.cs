using UnityEngine;

namespace Halka.Game.World
{
    public sealed class GridWorld2D : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float cellSize = 1f;
        [SerializeField] private Vector2Int minCell = new Vector2Int(-5, -3);
        [SerializeField] private Vector2Int maxCell = new Vector2Int(5, 3);

        public float CellSize => cellSize;

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

            Physics2D.SyncTransforms();
            foreach (var hit in Physics2D.OverlapPointAll(CellToWorld(cell)))
            {
                if (hit.GetComponentInParent<GridObstacle>() != null) return false;
            }
            return true;
        }
    }
}
