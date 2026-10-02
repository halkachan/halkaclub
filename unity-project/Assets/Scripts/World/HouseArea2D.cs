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
        public static readonly Vector2Int InsideEntryCell = new Vector2Int(0, -2);
        public static readonly Vector2Int InsideDoorCell = new Vector2Int(0, -3);
        public static readonly Vector2Int InsideMinCell = new Vector2Int(-4, -3);
        public static readonly Vector2Int InsideMaxCell = new Vector2Int(4, 3);

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

        public void Enter()
        {
            if (IsInside) return;
            player.CancelStep();
            grassOcclusion.enabled = false;
            exteriorRoot.SetActive(false);
            interiorRoot.SetActive(true);
            world.SetBounds(InsideMinCell, InsideMaxCell);
            player.TeleportTo(InsideEntryCell);
            worldCamera.orthographicSize = 2.4f;
            cameraFollow.SetBounds(new Vector2(-2.5f, -2f), new Vector2(2.5f, 2f));
            worldCamera.transform.position = new Vector3(0f, 0f, -10f);
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
            grassOcclusion.enabled = true;
            worldCamera.orthographicSize = outsideCameraSize;
            cameraFollow.SetBounds(outsideCameraMin, outsideCameraMax);
            worldCamera.transform.position = new Vector3(player.transform.position.x,
                player.transform.position.y, -10f);
            IsInside = false;
        }
    }
}
