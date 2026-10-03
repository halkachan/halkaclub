using Halka.Game.CameraControl;
using Halka.Game.Player;
using UnityEngine;

namespace Halka.Game.World
{
    // The room and the field share one scene and one grid coordinate system.
    public sealed class HouseArea2D : MonoBehaviour
    {
        public static readonly Vector2Int HouseDoorCell = new Vector2Int(-7, -4);
        public static readonly Vector2Int OutsideEntryCell = new Vector2Int(-7, -5);
        public static readonly Vector2Int InsideEntryCell = new Vector2Int(0, -3);
        public static readonly Vector2Int InsideExitCell = new Vector2Int(0, -4);
        public static readonly Vector2Int InsideMinCell = new Vector2Int(-6, -4);
        public static readonly Vector2Int InsideMaxCell = new Vector2Int(6, 4);

        [SerializeField] private GridWorld2D world;
        [SerializeField] private PlayerMover player;
        [SerializeField] private PlayerGrassOcclusion grassOcclusion;
        [SerializeField] private GameObject exteriorRoot;
        [SerializeField] private GameObject interiorRoot;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private CameraFollow2D cameraFollow;

        private Vector2Int outsideMin;
        private Vector2Int outsideMax;
        private float outsideCameraSize;
        private Vector2 outsideCameraMin;
        private Vector2 outsideCameraMax;

        public bool IsInside { get; private set; }

        private void Awake()
        {
            outsideMin = world.MinCell;
            outsideMax = world.MaxCell;
            outsideCameraSize = worldCamera.orthographicSize;
            outsideCameraMin = cameraFollow.WorldMin;
            outsideCameraMax = cameraFollow.WorldMax;
            interiorRoot.SetActive(false);
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
            else if (IsInside && cell == InsideExitCell) Exit();
        }

        public void Enter()
        {
            if (IsInside) return;
            player.CancelStep();
            grassOcclusion.enabled = false;
            exteriorRoot.SetActive(false);
            interiorRoot.SetActive(true);
            world.SetBounds(InsideMinCell, InsideMaxCell);
            player.TeleportTo(InsideEntryCell);
            player.SetFacing(FacingDirection.Up);
            worldCamera.orthographicSize = 3.4f;
            cameraFollow.SetBounds(new Vector2(-3.8f, -2.8f), new Vector2(3.8f, 2.8f));
            cameraFollow.SnapToTarget();
            IsInside = true;
        }

        public void Exit()
        {
            if (!IsInside) return;
            player.CancelStep();
            interiorRoot.SetActive(false);
            exteriorRoot.SetActive(true);
            world.SetBounds(outsideMin, outsideMax);
            player.TeleportTo(OutsideEntryCell);
            player.SetFacing(FacingDirection.Down);
            grassOcclusion.enabled = true;
            worldCamera.orthographicSize = outsideCameraSize;
            cameraFollow.SetBounds(outsideCameraMin, outsideCameraMax);
            cameraFollow.SnapToTarget();
            IsInside = false;
        }
    }
}
