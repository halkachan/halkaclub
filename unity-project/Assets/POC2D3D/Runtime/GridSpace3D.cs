using Halka.Game.Player;
using Halka.Game.World;
using UnityEngine;

namespace Halka.Poc2D3D
{
    // The single mapping between Grid cells and the 3D presentation space.
    // Grid +x is East and Grid +y is North on every map, matching the 2D screen.
    // Unity: +X East, +Z North, +Y up. A camera looking down -Y with up +Z keeps +X on the
    // right, so no axis needs to be mirrored. Gameplay never reads these positions back.
    public static class GridSpace3D
    {
        public const float CellSize = GridWorld2D.TileWorldSize;

        public static Vector3 ToWorld(Vector2 cell, float height = 0f) =>
            new Vector3(cell.x * CellSize, height, cell.y * CellSize);

        public static Vector2Int ToCell(Vector3 world) => new Vector2Int(
            Mathf.RoundToInt(world.x / CellSize), Mathf.RoundToInt(world.z / CellSize));

        public static Vector3 Direction(Vector2Int gridDirection) =>
            new Vector3(gridDirection.x, 0f, gridDirection.y);

        // Root yaw so that transform.forward equals the facing direction.
        public static float FacingYaw(FacingDirection facing) => facing switch
        {
            FacingDirection.Up => 0f,
            FacingDirection.Right => 90f,
            FacingDirection.Left => 270f,
            _ => 180f
        };

        // Blender assets face South (-Z after import); actors add this under their facing root.
        public const float ModelFrontYawOffset = 180f;

        // Converts a camera-relative cardinal input into a Grid direction using a yaw that is
        // quantized to 90 degrees, so a camera that is still turning maps keys predictably.
        public static Vector2Int CameraRelative(Vector2Int input, float cameraYawDegrees)
        {
            if (input == Vector2Int.zero) return input;
            var quarter = Mathf.RoundToInt(Mathf.Repeat(cameraYawDegrees, 360f) / 90f) % 4;
            var result = input;
            for (var i = 0; i < quarter; i++) result = new Vector2Int(result.y, -result.x);
            return result;
        }
    }
}
