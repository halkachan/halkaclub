using System.Collections.Generic;
using Halka.Game.Input;
using Halka.Game.World;
using UnityEngine;

namespace Halka.Game.Player
{
    [DefaultExecutionOrder(-40)]
    public sealed class AutoModeController : MonoBehaviour
    {
        public const float IdleSeconds = 60f;

        [SerializeField] private GameInput input;
        [SerializeField] private PlayerMover player;
        [SerializeField] private GridWorld2D world;
        [SerializeField] private HouseArea2D house;
        [SerializeField] private DynamicGridOccupancy2D occupancy;
        [SerializeField] private bool enabledByDefault = true;

        private readonly List<Vector2Int> route = new List<Vector2Int>();
        private int routeIndex;
        private float lastUserAt;
        private float nextActionAt;
        private GUIStyle buttonStyle;

        public bool AutoEnabled { get; private set; }
        public bool Active { get; private set; }
        public float LastUserAt => lastUserAt;

        private void Awake()
        {
            AutoEnabled = enabledByDefault;
            lastUserAt = Time.unscaledTime;
        }

        private void OnEnable()
        {
            if (input != null) input.UserActed += OnUserActed;
        }

        private void OnDisable()
        {
            if (input != null) input.UserActed -= OnUserActed;
            StopAuto();
        }

        private void Update() => Tick(Time.unscaledTime);

        public void Tick(float now)
        {
            if (!AutoEnabled || house.IsInside)
            {
                if (Active) StopAuto();
                return;
            }
            if (!Active)
            {
                if (now - lastUserAt < IdleSeconds || player.IsMoving) return;
                Active = true;
                nextActionAt = now + 1f;
                route.Clear();
            }
            if (player.IsMoving || now < nextActionAt) return;
            if (routeIndex >= route.Count)
            {
                PlanRoute();
                routeIndex = 0;
                if (route.Count == 0)
                {
                    nextActionAt = now + 2f;
                    return;
                }
            }
            var direction = route[routeIndex] - player.Cell;
            if (player.TryStep(direction)) routeIndex++;
            else
            {
                route.Clear();
                routeIndex = 0;
            }
            if (routeIndex >= route.Count) nextActionAt = now + Random.Range(1.5f, 3.5f);
        }

        private void PlanRoute()
        {
            route.Clear();
            // Nearby interests remain destinations on the existing outdoor grid.
            var interests = new[] { new Vector2Int(-7, -5), new Vector2Int(-3, 0),
                new Vector2Int(5, 0), new Vector2Int(7, 2), new Vector2Int(0, -1) };
            for (var attempt = 0; attempt < 12; attempt++)
            {
                var goal = attempt < 5 ? interests[Random.Range(0, interests.Length)] :
                    player.Cell + new Vector2Int(Random.Range(-4, 5), Random.Range(-4, 5));
                if (GridPathfinder2D.TryFind(world, player.Cell, goal, route,
                    CanAutoEnter)) return;
            }
        }

        public bool CanAutoEnter(Vector2Int cell) =>
            cell != HouseArea2D.HouseDoorCell && world.CanEnter(cell) &&
            (occupancy == null || occupancy.CanPlayerEnter(cell));

        private void OnUserActed() => RecordUserAction(Time.unscaledTime);

        public void RecordUserAction(float now)
        {
            lastUserAt = now;
            if (Active) StopAuto();
        }

        public void SetAutoEnabled(bool enabled, float now)
        {
            AutoEnabled = enabled;
            RecordUserAction(now);
        }

        private void StopAuto()
        {
            if (Active) player.CancelStep();
            Active = false;
            route.Clear();
            routeIndex = 0;
        }

        public bool IsOverControls(Vector2 screenPosition) =>
            ButtonRect().Contains(new Vector2(screenPosition.x, Screen.height - screenPosition.y));

        private Rect ButtonRect()
        {
            var safe = Screen.safeArea;
            const float width = 114f;
            const float height = 34f;
            var x = Mathf.Clamp(safe.xMax - width - 12f, safe.xMin, safe.xMax - width);
            return new Rect(x, Screen.height - safe.yMax + 44f, width, height);
        }

        private void OnGUI()
        {
            if (buttonStyle == null)
            {
                buttonStyle = new GUIStyle(GUI.skin.button)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 15
                };
            }
            var label = Active ? "AUTO: WALK" : AutoEnabled ? "AUTO: ON" : "AUTO: OFF";
            if (GUI.Button(ButtonRect(), label, buttonStyle))
                SetAutoEnabled(!AutoEnabled, Time.unscaledTime);
        }
    }
}
