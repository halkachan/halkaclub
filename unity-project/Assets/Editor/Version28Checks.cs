using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Halka.Game.Core;
using Halka.Game.Player;
using Halka.Game.UI;
using Halka.Game.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Halka.Game.Editor
{
    public static class Version28Checks
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

        [MenuItem("HALKA/Validate ver2.8 scene")]
        public static void Run()
        {
            Version27Checks.Run();
            Check(GameVersion.Value == "2.8" || (GameVersion.Value == "2.9" || GameVersion.Value == "3.0"), "game version");
            ProjectBuilder.PrepareScene();
            EditorSceneManager.OpenScene("Assets/Scenes/FirstDay.unity");
            var field = AssetDatabase.LoadAssetAtPath<MapDefinition>(
                "Assets/Content/Maps/first_field.asset");
            var room = AssetDatabase.LoadAssetAtPath<MapDefinition>(
                "Assets/Content/Maps/halka_house.asset");
            var auto = UnityEngine.Object.FindFirstObjectByType<AutoModeController>();
            var maps = UnityEngine.Object.FindFirstObjectByType<MapWorldController2D>();
            var area = UnityEngine.Object.FindFirstObjectByType<HouseArea2D>();
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerMover>();
            var world = UnityEngine.Object.FindFirstObjectByType<GridWorld2D>();
            var hud = UnityEngine.Object.FindFirstObjectByType<GameHud>();
            Check(field != null && room != null && auto != null && maps != null &&
                area != null && player != null && world != null && hud != null &&
                ReferenceEquals(Field(auto, "maps"), maps) &&
                ReferenceEquals(Field(auto, "hud"), hud),
                "persistent AUTO uses active map and existing HUD");
            Check(field.DataVersion == 4 && room.DataVersion == 4 &&
                field.MapId == "first_field" && room.MapId == "halka_house" &&
                room.MapType == "interior" && AutoModeController.IdleSeconds == 60f,
                "Map v4, both map IDs and idle duration");
            var entry = Vector2Int.zero;
            var exit = Vector2Int.zero;
            Check(room.TryGetMarker("interior_entry", out entry) &&
                room.TryGetMarker("interior_exit", out exit), "indoor entry and exit markers");

            var canEnter = new Func<Vector2Int, bool>(cell => room.Contains(cell) &&
                !MapPlacementRules.BlocksMovement(room, cell) && cell != exit);
            var firstChoices = IndoorAutoRoutePlanner2D.CollectDestinations(
                room, entry, null, canEnter);
            var bed = room.Objects.Single(item => item.Definition.StableId == "bed_basic");
            var desk = room.Objects.Single(item => item.Definition.StableId == "desk_basic");
            // ver3.0 RC1 feedback: the cushion moved onto a sofa; its seat cell replaces the cushion cell.
            var sofa = room.Objects.Single(item => item.Definition.StableId == "sofa_basic");
            var sofaSeat = sofa.RootCell + sofa.Definition.ActionPoints[0].PlayerCellOffset;
            Check(firstChoices.Count > 1 && firstChoices.All(cell =>
                canEnter(cell) && cell != entry && cell != exit) &&
                !firstChoices.Contains(bed.RootCell) && !firstChoices.Contains(desk.RootCell) &&
                !firstChoices.Contains(sofa.RootCell) && firstChoices.Contains(sofaSeat),
                "indoor destinations are reachable, avoid blocked furniture and include the sofa seat");
            var lastGoal = firstChoices[0];
            var nextChoices = IndoorAutoRoutePlanner2D.CollectDestinations(
                room, entry, lastGoal, canEnter);
            Check(nextChoices.Count > 0 && !nextChoices.Contains(lastGoal),
                "destination selection avoids immediate repetition");
            CheckFixtureWithMovedDesk(room, entry, exit, desk);

            typeof(HouseArea2D).GetMethod("Awake", Hidden).Invoke(area, null);
            typeof(AutoModeController).GetMethod("Awake", Hidden).Invoke(auto, null);
            var now = Time.unscaledTime;
            auto.RecordUserAction(now);
            auto.Tick(now + 59.9f);
            Check(auto.AutoEnabled && !auto.Active && maps.ActiveMapId == "first_field",
                "outdoor AUTO waits for the full 60 seconds");
            auto.Tick(now + 60f);
            Check(auto.Active && !auto.CanAutoEnter(field.HouseDoorCell),
                "outdoor AUTO starts but cannot enter the house");
            auto.SetAutoEnabled(false, now + 61f);
            auto.Tick(now + 122f);
            Check(!auto.AutoEnabled && !auto.Active, "AUTO OFF stops outdoor movement");
            auto.SetAutoEnabled(true, now + 122f);

            player.TeleportTo(field.OutsideEntryCell);
            area.Enter();
            Check(maps.ActiveMapId == "halka_house" && player.Cell == entry &&
                world.MinCell == room.MinCell && world.MaxCell == room.MaxCell,
                "manual entry activates the formal house map");
            auto.Tick(now + 123f);
            Check(!auto.Active && Mathf.Approximately(auto.LastUserAt, now + 123f),
                "map switch restarts the idle timer");
            auto.Tick(now + 182.9f);
            Check(!auto.Active, "indoor AUTO does not start at 59.9 seconds");
            auto.Tick(now + 183f);
            Check(auto.Active && !auto.CanAutoEnter(exit) &&
                !auto.CanAutoEnter(bed.RootCell) && !auto.CanAutoEnter(desk.RootCell) &&
                !auto.CanAutoEnter(sofa.RootCell) && auto.CanAutoEnter(sofaSeat),
                "indoor AUTO starts at 60 seconds and respects the formal furniture and exit");

            var completed = 0;
            var indoorGoals = new HashSet<Vector2Int>();
            player.StepCompleted += CountStep;
            try
            {
                for (var i = 0; i < 120; i++)
                {
                    auto.Tick(now + 184.1f + i * 0.25f);
                    if ((Vector2Int?)Field(auto, "previousIndoorGoal") is { } goal)
                        indoorGoals.Add(goal);
                    typeof(PlayerMover).GetMethod("Tick", Hidden)
                        .Invoke(player, new object[] { 0.25f, Vector2Int.zero });
                    Check(maps.ActiveMapId == "halka_house" && player.Cell != exit &&
                        !MapPlacementRules.BlocksMovement(room, player.Cell),
                        "AUTO steps remain inside walkable house cells");
                }
            }
            finally { player.StepCompleted -= CountStep; }
            Check(completed > 3 && indoorGoals.Count >= 2 && auto.Active,
                "indoor AUTO visits several goals with ordinary grid steps for Life Log");
            auto.RecordUserAction(now + 215f);
            Check(!auto.Active && Mathf.Approximately(auto.LastUserAt, now + 215f),
                "manual input interrupts AUTO and resets the timer");
            auto.Tick(now + 274.9f);
            Check(!auto.Active, "manual input gets a fresh 60-second interval");
            auto.Tick(now + 275f);
            Check(auto.Active, "AUTO resumes indoors after another idle interval");
            auto.SetMenuSuspended(true, now + 276f);
            Check(auto.AutoEnabled && !auto.Active && auto.MenuSuspended,
                "menu suspends movement without changing AUTO ON");
            auto.SetMenuSuspended(false, now + 277f);
            auto.Tick(now + 337f);
            Check(auto.Active, "closing menu restarts the idle timer");
            auto.Tick(now + 338f, false);
            Check(!auto.Active && Mathf.Approximately(auto.LastUserAt, now + 338f),
                "background suspends AUTO and resets idle time");
            hud.ShowMessage("テスト");
            auto.Tick(now + 399f);
            Check(!auto.Active && Mathf.Approximately(auto.LastUserAt, now + 399f),
                "message prevents AUTO from starting");
            auto.SetAutoEnabled(false, now + 400f);
            auto.Tick(now + 461f);
            Check(!auto.Active && !auto.AutoEnabled, "AUTO OFF stops indoor movement");

            // The authored exit remains a manual step, while AUTO never routes onto it.
            player.TeleportTo(entry);
            typeof(HouseArea2D).GetMethod("OnEnable", Hidden).Invoke(area, null);
            Check(world.CanEnter(exit) && player.TryStep(Vector2Int.down),
                "manual input can still step onto the exit");
            typeof(PlayerMover).GetMethod("Tick", Hidden)
                .Invoke(player, new object[] { 0.25f, Vector2Int.zero });
            Check(maps.ActiveMapId == "first_field" && !area.IsInside,
                "manual exit still returns to the outdoor map");
            typeof(HouseArea2D).GetMethod("OnDisable", Hidden).Invoke(area, null);
            Debug.Log("HALKA ver2.8 indoor AUTO and inherited regression checks passed.");

            void CountStep(Vector2Int _) => completed++;
        }

        private static void CheckFixtureWithMovedDesk(MapDefinition room, Vector2Int entry,
            Vector2Int exit, WorldObjectPlacement desk)
        {
            var fixture = UnityEngine.Object.Instantiate(room);
            try
            {
                var moved = Vector2Int.zero;
                var found = false;
                for (var y = room.MinCell.y; y <= room.MaxCell.y && !found; y++)
                for (var x = room.MinCell.x; x <= room.MaxCell.x && !found; x++)
                {
                    var cell = new Vector2Int(x, y);
                    found = cell != desk.RootCell &&
                        MapPlacementRules.CanPlaceObject(room, cell, desk.Definition,
                            out _, desk.RootCell) && cell != entry && cell != exit;
                    if (found) moved = cell;
                }
                Check(found, "fixture has another legal desk position");
                var objects = room.Objects.Select(item => item.InstanceId == desk.InstanceId
                    ? new WorldObjectPlacement { InstanceId = item.InstanceId,
                        Definition = item.Definition, RootCell = moved } : item).ToList();
                fixture.ReplaceFromAuthoring(room.MapId, room.DisplayName, room.MapType,
                    room.BaseSurface, room.GrassMode, room.BackdropColor, room.MinCell,
                    room.MaxCell, room.Surfaces.ToList(), objects, room.Markers.ToList(),
                    room.EntitySpawns.ToList(), room.AreaTransitions.ToList());
                Check(MapPlacementRules.Validate(fixture).Count == 0 &&
                    MapPlacementRules.BlocksMovement(fixture, moved),
                    "moved desk fixture remains valid and blocks its new cells");
                var candidates = IndoorAutoRoutePlanner2D.CollectDestinations(fixture,
                    entry, null, cell => fixture.Contains(cell) && cell != exit &&
                        !MapPlacementRules.BlocksMovement(fixture, cell));
                Check(candidates.Count > 0 && candidates.All(cell =>
                    !MapPlacementRules.BlocksMovement(fixture, cell) && cell != exit),
                    "AUTO recomputes reachable cells after authored furniture moves");
            }
            finally { UnityEngine.Object.DestroyImmediate(fixture); }
        }

        private static object Field(object target, string name) =>
            target.GetType().GetField(name, Hidden).GetValue(target);

        private static void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException(
                "Version28Checks failed: " + description);
        }
    }
}
