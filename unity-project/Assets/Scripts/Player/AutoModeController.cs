using System.Collections.Generic;
using System.Runtime.InteropServices;
using Halka.Game.Input;
using Halka.Game.UI;
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
        [SerializeField] private MapWorldController2D maps;
        [SerializeField] private DynamicGridOccupancy2D occupancy;
        [SerializeField] private CrowWander2D crow;
        [SerializeField] private GameHud hud;
        [SerializeField] private bool enabledByDefault = true;

        private readonly List<Vector2Int> route = new List<Vector2Int>();
        private int routeIndex;
        private float lastUserAt;
        private float nextActionAt;
        private bool menuSuspended;
        private bool hasFocus = true;
        private bool paused;
        private string observedMapId;
        private Vector2Int? previousIndoorGoal;

        public bool AutoEnabled { get; private set; }
        public bool Active { get; private set; }
        public bool MenuSuspended => menuSuspended;
        public float LastUserAt => lastUserAt;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int HalkaLifeLogTabVisible();
#endif

        private void Awake()
        {
            AutoEnabled = enabledByDefault;
            lastUserAt = Time.unscaledTime;
            observedMapId = maps != null ? maps.ActiveMapId : null;
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

        private void Update() => Tick(Time.unscaledTime, IsForeground());

        private bool IsForeground()
        {
            var foreground = hasFocus && !paused && Application.isFocused;
#if UNITY_WEBGL && !UNITY_EDITOR
            foreground = foreground && HalkaLifeLogTabVisible() != 0;
#endif
            return foreground;
        }

        // The overload lets regression checks advance the existing idle clock without waiting a minute.
        public void Tick(float now) => Tick(now, true);

        public void Tick(float now, bool foreground)
        {
            if (!foreground)
            {
                RecordUserAction(now);
                return;
            }

            var mapId = maps != null ? maps.ActiveMapId :
                house != null && house.IsInside ? "halka_house" : "first_field";
            if (observedMapId != mapId)
            {
                observedMapId = mapId;
                previousIndoorGoal = null;
                RecordUserAction(now);
                return;
            }
            if (!AutoEnabled || menuSuspended || player == null || player.MovementLocked ||
                hud != null && hud.HasActiveMessage ||
                mapId != "first_field" && mapId != "halka_house" ||
                mapId == "halka_house" && maps == null)
            {
                if (hud != null && hud.HasActiveMessage) RecordUserAction(now);
                else if (Active) StopAuto();
                return;
            }
            if (!Active)
            {
                if (now - lastUserAt < IdleSeconds || player.IsMoving) return;
                Active = true;
                nextActionAt = now + 1f;
                route.Clear();
                routeIndex = 0;
            }
            if (player.IsMoving || now < nextActionAt) return;
            if (routeIndex >= route.Count)
            {
                if (mapId == "halka_house") PlanIndoorRoute();
                else PlanOutdoorRoute();
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
                nextActionAt = now + 2f;
                return;
            }
            if (routeIndex >= route.Count)
                nextActionAt = now + (mapId == "halka_house"
                    ? Random.Range(1f, 4f) : Random.Range(1.5f, 3.5f));
        }

        private void PlanOutdoorRoute()
        {
            route.Clear();
            // Keep the established outdoor interests and route selection unchanged.
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

        private void PlanIndoorRoute()
        {
            route.Clear();
            var room = maps.GetMap("halka_house");
            var destinations = IndoorAutoRoutePlanner2D.CollectDestinations(
                room, player.Cell, previousIndoorGoal, CanAutoEnter);
            while (destinations.Count > 0)
            {
                var index = Random.Range(0, destinations.Count);
                var goal = destinations[index];
                if (GridPathfinder2D.TryFind(world, player.Cell, goal, route, CanAutoEnter))
                {
                    previousIndoorGoal = goal;
                    return;
                }
                destinations.RemoveAt(index);
            }
        }

        public bool CanAutoEnter(Vector2Int cell)
        {
            var mapId = maps != null ? maps.ActiveMapId : "first_field";
            if (mapId == "first_field" && house != null && cell == house.HouseDoorCell)
                return false;
            if (mapId == "halka_house")
            {
                var room = maps.GetMap("halka_house");
                if (!room.Contains(cell) ||
                    room.TryGetMarker("interior_exit", out var exit) && cell == exit)
                    return false;
            }
            else if (mapId != "first_field") return false;
            return world != null && world.CanEnter(cell) &&
                (occupancy == null || occupancy.CanPlayerEnter(cell));
        }

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

        private void OnApplicationFocus(bool focused)
        {
            hasFocus = focused;
            RecordUserAction(Time.unscaledTime);
        }

        private void OnApplicationPause(bool isPaused)
        {
            paused = isPaused;
            RecordUserAction(Time.unscaledTime);
        }

        private void StopAuto()
        {
            if (Active && player != null) player.CancelStep();
            Active = false;
            route.Clear();
            routeIndex = 0;
        }
    }
}
