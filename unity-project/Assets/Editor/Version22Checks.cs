using System;
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
    public static class Version22Checks
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

        [MenuItem("HALKA/Validate ver2.2 scene")]
        public static void Run()
        {
            Version21Checks.Run();
            EditorSceneManager.OpenScene("Assets/Scenes/FirstDay.unity");
            var menu = UnityEngine.Object.FindFirstObjectByType<GameMenuController>();
            var lifeLog = UnityEngine.Object.FindFirstObjectByType<LifeLogController>();
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerMover>();
            var world = UnityEngine.Object.FindFirstObjectByType<GridWorld2D>();
            var maps = UnityEngine.Object.FindFirstObjectByType<MapWorldController2D>();
            var autoMode = UnityEngine.Object.FindFirstObjectByType<AutoModeController>();
            Check((GameVersion.Value == "2.2" || GameVersion.Value == "2.3" ||
                   GameVersion.Value == "2.4" || GameVersion.Value == "2.5" ||
                   GameVersion.Value == "2.5.1" || GameVersion.Value == "2.5.2") &&
                GameVersion.Label == "ver" + GameVersion.Value, "supported version");
            Check(menu != null && lifeLog != null && player != null && autoMode != null &&
                ReferenceEquals(Field(menu, "lifeLog"), lifeLog) &&
                lifeLog.transform.parent == null && !menu.IsOpen, "persistent life log and menu binding");

            var empty = LifeLogData.Deserialize("");
            Check(empty.TotalSteps == 0 && empty.TotalPlaySeconds == 0 &&
                empty.StepsLabel == "0" && empty.TimeLabel == "0:00:00", "first launch defaults");
            var large = LifeLogData.Deserialize("v1|9007199254740993|9007199254740993");
            Check(large.TotalSteps == 9007199254740993L &&
                LifeLogData.Deserialize(large.Serialize()).TotalPlaySeconds == 9007199254740993L &&
                large.StepsLabel == "9,007,199,254,740,993" && large.TimeLabel.StartsWith("2501999792983:"),
                "64-bit save and long-duration display");
            Check(LifeLogData.Deserialize("v2|3|4").TotalSteps == 0 &&
                LifeLogData.SaveVersion == 1, "unknown save version starts safely");

            var hadOldSave = PlayerPrefs.HasKey(LifeLogController.SaveKey);
            var oldSave = PlayerPrefs.GetString(LifeLogController.SaveKey, "");
            try
            {
                PlayerPrefs.DeleteKey(LifeLogController.SaveKey);
                Method(lifeLog, "Awake");
                Method(autoMode, "Awake");
                Check(lifeLog.TotalSteps == 0 && lifeLog.TotalPlaySeconds == 0, "controller starts at zero");
                SetField(lifeLog, "lastSampleAt", 0d);
                lifeLog.AdvanceClock(10d, false);
                Check(lifeLog.TotalPlaySeconds == 0, "background time excluded");
                lifeLog.AdvanceClock(12.5d, true);
                Check(lifeLog.TotalPlaySeconds == 2, "foreground time counted");
                menu.SetOpen(true, Time.unscaledTime);
                menu.OpenLifeLog(Time.unscaledTime);
                Check(menu.IsOpen && menu.IsLifeLogOpen && menu.BlocksGameplayInput &&
                    autoMode.MenuSuspended && autoMode.AutoEnabled, "life log opens without resuming gameplay");
                lifeLog.AdvanceClock(15.5d, true);
                Check(lifeLog.TotalPlaySeconds == 5, "menu time counted");
                menu.BackFromLifeLog(Time.unscaledTime);
                Check(menu.IsOpen && !menu.IsLifeLogOpen && autoMode.MenuSuspended,
                    "back returns to menu, still blocking input");
                menu.OpenLifeLog(Time.unscaledTime);
                menu.SetOpen(false, Time.unscaledTime);
                Check(!menu.IsOpen && !menu.IsLifeLogOpen && !autoMode.MenuSuspended,
                    "menu button closes life log and resumes input");
                lifeLog.AdvanceClock(100d, false);
                lifeLog.AdvanceClock(101d, true);
                Check(lifeLog.TotalPlaySeconds == 6, "focus resumes without adding hidden interval");

                var field = maps.GetMap("first_field");
                var loader = UnityEngine.Object.FindObjectsByType<MapRuntimeLoader2D>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None).Single(item => item.Map == field);
                loader.Build();
                Method(player, "Awake");
                var stepListeners = (MulticastDelegate)Field(player, "StepCompleted");
                if (stepListeners == null || !stepListeners.GetInvocationList().Any(item =>
                    ReferenceEquals(item.Target, lifeLog))) Method(lifeLog, "OnEnable");
                player.TeleportTo(Vector2Int.zero);
                Check(lifeLog.TotalSteps == 0, "spawn is not a step");
                Check(player.TryStep(Vector2Int.down), "manual destination is passable");
                Check(lifeLog.TotalSteps == 0, "step start does not count");
                Method(player, "Tick", PlayerMover.DefaultStepSeconds, Vector2Int.zero);
                Check(lifeLog.TotalSteps == 1, "completed manual step counts once");
                Check(player.TryStep(Vector2Int.right), "AUTO path uses same player step API");
                Method(player, "Tick", PlayerMover.DefaultStepSeconds, Vector2Int.zero);
                Check(lifeLog.TotalSteps == 2, "completed AUTO-style step counts once");
                player.TeleportTo(new Vector2Int(1, 0));
                Check(lifeLog.TotalSteps == 2 && !player.TryStep(Vector2Int.up) &&
                    lifeLog.TotalSteps == 2, "teleport, blocked cell and facing do not count");
                player.TeleportTo(field.OutsideEntryCell);
                Check(lifeLog.TotalSteps == 2, "area arrival is not a step");

                lifeLog.Save();
                var reloaded = LifeLogData.Deserialize(PlayerPrefs.GetString(LifeLogController.SaveKey));
                Check(reloaded.TotalSteps == 2 && reloaded.TotalPlaySeconds == 6,
                    "saved steps and play time reload");
            }
            finally
            {
                if (hadOldSave) PlayerPrefs.SetString(LifeLogController.SaveKey, oldSave);
                else PlayerPrefs.DeleteKey(LifeLogController.SaveKey);
                PlayerPrefs.Save();
            }
            Debug.Log("HALKA ver2.2 life log, save, menu and inherited regression checks passed.");
        }

        private static object Field(object target, string name) =>
            target.GetType().GetField(name, Hidden).GetValue(target);
        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, Hidden).SetValue(target, value);
        private static void Method(object target, string name, params object[] args) =>
            target.GetType().GetMethod(name, Hidden).Invoke(target, args);
        private static void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("Version22Checks failed: " + name);
        }
    }
}
