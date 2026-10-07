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
        [SerializeField] private CrowWander2D crow;
        [SerializeField] private bool enabledByDefault = true;

        private readonly List<Vector2Int> route = new List<Vector2Int>();
        private int routeIndex;
        private float lastUserAt;
        private float nextActionAt;
        private bool menuSuspended;

        public bool AutoEnabled { get; private set; }
        public bool Active { get; private set; }
        public bool MenuSuspended => menuSuspended;
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
            if (!AutoEnabled || menuSuspended || house.IsInside)
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
            var crowNearby = crow != null && crow.isActiveAndEnabled
                ? crow.Cell + Vector2Int.down : player.Cell;
            var interests = new[] { house.OutsideEntryCell, new Vector2Int(-3, 0),
                new Vector2Int(5, 0), crowNearby, new Vector2Int(0, -1) };
            for (var attempt = 0; attempt < 12; attempt++)
            {
                var goal = attempt < 5 ? interests[Random.Range(0, interests.Length)] :
                    player.Cell + new Vector2Int(Random.Range(-4, 5), Random.Range(-4, 5));
                if (GridPathfinder2D.TryFind(world, player.Cell, goal, route,
                    CanAutoEnter)) return;
            }
        }

        public bool CanAutoEnter(Vector2Int cell) =>
            cell != house.HouseDoorCell && world.CanEnter(cell) &&
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

        public void SetMenuSuspended(bool suspended, float now)
        {
            menuSuspended = suspended;
            RecordUserAction(now);
        }

        private void StopAuto()
        {
            if (Active) player.CancelStep();
            Active = false;
            route.Clear();
            routeIndex = 0;
        }

    }
}
