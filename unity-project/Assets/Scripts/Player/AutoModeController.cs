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
        // ver3.0 shared 2D/3D world: the same decisions on another world (see IAutoModeWorld).
        private IAutoModeWorld sharedWorld;
        private IAutoModeWorld defaultWorld;

        public bool AutoEnabled { get; private set; }
        // ver3.0 Phase 6: the setting a new save starts with (the scene's default).
        public bool EnabledByDefault => enabledByDefault;
        public bool Active { get; private set; }
        public bool MenuSuspended => menuSuspended;
        public float LastUserAt => lastUserAt;
        private IAutoModeWorld World => sharedWorld ?? (defaultWorld ??= new SceneWorld(this));

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int HalkaLifeLogTabVisible();
#endif

        // Runs AUTO on another world (null returns to the 2D game's components).
        public void UseWorld(IAutoModeWorld autoWorld)
        {
            sharedWorld = autoWorld;
            observedMapId = World.HasMapRegistry ? World.ActiveMapId : null;
        }

        private void Awake()
        {
            AutoEnabled = enabledByDefault;
            lastUserAt = Time.unscaledTime;
            observedMapId = World.HasMapRegistry ? World.ActiveMapId : null;
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
            return foreground && !World.Suspended;
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

            var world = World;
            var mapId = world.ActiveMapId;
            if (observedMapId != mapId)
            {
                observedMapId = mapId;
                previousIndoorGoal = null;
                RecordUserAction(now);
                return;
            }
            if (!AutoEnabled || menuSuspended || !world.HasPlayer || world.PlayerMovementLocked ||
                world.HasActiveMessage ||
                !IsOutdoorMap(world, mapId) && !IsIndoorMap(world, mapId))
            {
                if (world.HasActiveMessage) RecordUserAction(now);
                else if (Active) StopAuto();
                return;
            }
            if (!Active)
            {
                if (now - lastUserAt < IdleSeconds || world.PlayerIsMoving) return;
                Active = true;
                nextActionAt = now + 1f;
                route.Clear();
                routeIndex = 0;
            }
            if (world.PlayerIsMoving || now < nextActionAt) return;
            if (routeIndex >= route.Count)
            {
                if (IsIndoorMap(world, mapId)) PlanIndoorRoute(mapId);
                else PlanOutdoorRoute();
                routeIndex = 0;
                if (route.Count == 0)
                {
                    nextActionAt = now + 2f;
                    return;
                }
            }
            var direction = route[routeIndex] - world.PlayerCell;
            if (world.TryStep(direction)) routeIndex++;
            else
            {
                route.Clear();
                routeIndex = 0;
                nextActionAt = now + 2f;
                return;
            }
            if (routeIndex >= route.Count)
                nextActionAt = now + (IsIndoorMap(world, mapId)
                    ? Random.Range(1f, 4f) : Random.Range(1.5f, 3.5f));
        }

        private void PlanOutdoorRoute()
        {
            route.Clear();
            var world = World;
            // Keep the established outdoor interests and route selection unchanged.
            var crowNearby = world.TryGetCrowCell(out var crowCell)
                ? crowCell + Vector2Int.down : world.PlayerCell;
            var interests = new[] { world.OutsideEntryCell, new Vector2Int(-3, 0),
                new Vector2Int(5, 0), crowNearby, new Vector2Int(0, -1) };
            for (var attempt = 0; attempt < 12; attempt++)
            {
                var goal = attempt < 5 ? interests[Random.Range(0, interests.Length)] :
                    world.PlayerCell + new Vector2Int(Random.Range(-4, 5), Random.Range(-4, 5));
                if (GridPathfinder2D.TryFind(world.PlayerCell, goal, route,
                    CanAutoEnter)) return;
            }
        }

        // ver3.1: AUTO walks in every interior map of the registry (the house, the sound booth)
        // with the same planner; it never steps onto a door or an exit (CanAutoEnter).
        // ver3.2: first_field and any other outdoor Map of the registry (a ver3.3 region).
        private static bool IsOutdoorMap(IAutoModeWorld world, string mapId)
        {
            if (mapId == "first_field") return true;
            if (!world.HasMapRegistry || string.IsNullOrEmpty(mapId)) return false;
            try { return world.GetMap(mapId) is MapDefinition map && map.MapType == "outdoor"; }
            catch (System.ArgumentException) { return false; }
        }

        private static bool IsIndoorMap(IAutoModeWorld world, string mapId)
        {
            if (mapId == "halka_house") return world.HasMapRegistry;
            if (!world.HasMapRegistry || string.IsNullOrEmpty(mapId) || mapId == "first_field") return false;
            try { return world.GetMap(mapId) is MapDefinition map && map.MapType == "interior"; }
            catch (System.ArgumentException) { return false; }
        }

        private void PlanIndoorRoute(string mapId)
        {
            route.Clear();
            var world = World;
            var room = world.GetMap(mapId);
            var destinations = IndoorAutoRoutePlanner2D.CollectDestinations(
                room, world.PlayerCell, previousIndoorGoal, CanAutoEnter);
            while (destinations.Count > 0)
            {
                var index = Random.Range(0, destinations.Count);
                var goal = destinations[index];
                if (GridPathfinder2D.TryFind(world.PlayerCell, goal, route, CanAutoEnter))
                {
                    previousIndoorGoal = goal;
                    return;
                }
                destinations.RemoveAt(index);
            }
        }

        public bool CanAutoEnter(Vector2Int cell)
        {
            var world = World;
            var mapId = world.HasMapRegistry ? world.ActiveMapId : "first_field";
            var outdoors = IsOutdoorMap(world, mapId);
            if (outdoors && world.HasHouse && cell == world.HouseDoorCell)
                return false;
            if (IsIndoorMap(world, mapId))
            {
                // ver3.1: a room's own door Object (the sound booth in the house) is also a door.
                var room = world.GetMap(mapId);
                if (!room.Contains(cell) ||
                    room.TryGetMarker("interior_exit", out var exit) && cell == exit ||
                    room.TryGetHouseRoot(out var innerDoor) && cell == innerDoor)
                    return false;
            }
            else if (!outdoors) return false;
            return world.CanEnter(cell) && world.CanPlayerEnter(cell);
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
            if (Active && World.HasPlayer) World.CancelStep();
            Active = false;
            route.Clear();
            routeIndex = 0;
        }

        // The 2D game's own components, read at call time (scene wiring and checks may set them).
        private sealed class SceneWorld : IAutoModeWorld
        {
            private readonly AutoModeController owner;

            public SceneWorld(AutoModeController controller) => owner = controller;

            public string ActiveMapId => owner.maps != null ? owner.maps.ActiveMapId :
                owner.house != null && owner.house.IsInside ? "halka_house" : "first_field";
            public bool HasMapRegistry => owner.maps != null;
            public MapDefinition GetMap(string mapId) => owner.maps.GetMap(mapId);
            public bool HasPlayer => owner.player != null;
            public Vector2Int PlayerCell => owner.player.Cell;
            public bool PlayerIsMoving => owner.player.IsMoving;
            public bool PlayerMovementLocked => owner.player.MovementLocked;
            public bool TryStep(Vector2Int direction) => owner.player.TryStep(direction);
            public void CancelStep() => owner.player.CancelStep();
            public bool HasActiveMessage => owner.hud != null && owner.hud.HasActiveMessage;
            public bool HasHouse => owner.house != null;
            public Vector2Int HouseDoorCell => owner.house.HouseDoorCell;
            public Vector2Int OutsideEntryCell => owner.house.OutsideEntryCell;
            public bool CanEnter(Vector2Int cell) => owner.world != null && owner.world.CanEnter(cell);
            public bool CanPlayerEnter(Vector2Int cell) =>
                owner.occupancy == null || owner.occupancy.CanPlayerEnter(cell);

            public bool TryGetCrowCell(out Vector2Int cell)
            {
                var active = owner.crow != null && owner.crow.isActiveAndEnabled;
                cell = active ? owner.crow.Cell : default;
                return active;
            }

            public bool Suspended => false;
        }
    }
}
