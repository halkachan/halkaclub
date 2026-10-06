using Halka.Game.CameraControl;
using Halka.Game.Player;
using UnityEngine;

namespace Halka.Game.World
{
    // House entrance and exit are normal completed steps between registered maps.
    public sealed class HouseArea2D : MonoBehaviour
    {
        [SerializeField] private GridWorld2D world;
        [SerializeField] private PlayerMover player;
        [SerializeField] private PlayerGrassOcclusion grassOcclusion;
        [SerializeField] private GrassField2D exteriorGrass;
        [SerializeField] private GrassField2D interiorGrass;
        [SerializeField] private MapWorldController2D maps;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private CameraFollow2D cameraFollow;

        private float outsideCameraSize;
        private Vector2 outsideCameraMin;
        private Vector2 outsideCameraMax;

        public bool IsInside { get; private set; }
        public Vector2Int HouseDoorCell => maps.GetMap("first_field").HouseDoorCell;
        public Vector2Int OutsideEntryCell => maps.GetMap("first_field").OutsideEntryCell;

        private void Awake()
        {
            outsideCameraSize = worldCamera.orthographicSize;
            outsideCameraMin = cameraFollow.WorldMin;
            outsideCameraMax = cameraFollow.WorldMax;
        }

        private void OnEnable()
        {
            if (player != null) player.StepCompleted += OnStepCompleted;
        }

        private void OnDisable()
        {
            if (player != null) player.StepCompleted -= OnStepCompleted;
        }

        private void OnStepCompleted(Vector2Int cell)
        {
            if (!IsInside && cell == HouseDoorCell) Enter();
            else if (IsInside && maps.GetMap("halka_house").TryGetMarker("interior_exit", out var exit) &&
                cell == exit) Exit();
        }

        public void Enter()
        {
            if (IsInside) return;
            player.CancelStep();
            maps.LoadMap("halka_house", "interior_entry");
            grassOcclusion.UseGrassField(interiorGrass);
            player.SetFacing(FacingDirection.Up);
            var room = maps.GetMap("halka_house");
            worldCamera.orthographicSize = 3.4f;
            cameraFollow.SetBounds(
                new Vector2(room.MinCell.x * GridWorld2D.TileWorldSize - 0.8f,
                    room.MinCell.y * GridWorld2D.TileWorldSize - 0.8f),
                new Vector2(room.MaxCell.x * GridWorld2D.TileWorldSize + 0.8f,
                    room.MaxCell.y * GridWorld2D.TileWorldSize + 0.8f));
            cameraFollow.SnapToTarget();
            IsInside = true;
        }

        public void Exit()
        {
            if (!IsInside) return;
            player.CancelStep();
            maps.ActivateMap("first_field");
            player.TeleportTo(OutsideEntryCell);
            player.SetFacing(FacingDirection.Down);
            grassOcclusion.UseGrassField(exteriorGrass);
            worldCamera.orthographicSize = outsideCameraSize;
            cameraFollow.SetBounds(outsideCameraMin, outsideCameraMax);
            cameraFollow.SnapToTarget();
            IsInside = false;
        }
    }
}
