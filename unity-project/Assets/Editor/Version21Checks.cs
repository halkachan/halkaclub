using System;
using System.Reflection;
using Halka.Game.Core;
using Halka.Game.Input;
using Halka.Game.Player;
using Halka.Game.UI;
using Halka.Game.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Halka.Game.Editor
{
    public static class Version21Checks
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

        [MenuItem("HALKA/Validate ver2.1 scene")]
        public static void Run()
        {
            Version20Checks.Run();
            EditorSceneManager.OpenScene("Assets/Scenes/FirstDay.unity");
            var menu = UnityEngine.Object.FindFirstObjectByType<GameMenuController>();
            var autoMode = UnityEngine.Object.FindFirstObjectByType<AutoModeController>();
            var input = UnityEngine.Object.FindFirstObjectByType<GameInput>();
            var hud = UnityEngine.Object.FindFirstObjectByType<GameHud>();
            var dpad = UnityEngine.Object.FindFirstObjectByType<TouchDpad>();
            var action = UnityEngine.Object.FindFirstObjectByType<TouchActionButton>();
            var area = UnityEngine.Object.FindFirstObjectByType<HouseArea2D>();
            Check((GameVersion.Value == "2.1" || GameVersion.Value == "2.2") &&
                GameVersion.Label == "ver" + GameVersion.Value, "supported game version");
            Check(menu != null && autoMode != null && input != null && hud != null &&
                dpad != null && action != null && area != null, "persistent menu and controls");
            Check(menu.transform.parent == null && !menu.IsOpen &&
                GameObject.Find("UI - auto living toggle") == null, "menu starts closed, old toggle removed");
            Check(ReferenceEquals(Field(menu, "autoMode"), autoMode) &&
                ReferenceEquals(Field(input, "menu"), menu) &&
                ReferenceEquals(Field(menu, "hud"), hud), "one AUTO state source and input binding");

            typeof(AutoModeController).GetMethod("Awake", Hidden).Invoke(autoMode, null);
            var now = Time.unscaledTime;
            autoMode.RecordUserAction(now);
            Check(autoMode.AutoEnabled && !autoMode.Active, "AUTO default ON");
            menu.SetOpen(true, now + 1f);
            Check(menu.IsOpen && menu.BlocksGameplayInput && autoMode.MenuSuspended &&
                autoMode.AutoEnabled && !autoMode.Active, "open pauses AUTO without changing setting");
            Check(menu.IsOverControls(Vector2.zero), "open menu captures world pointer input");
            typeof(GameInput).GetProperty("Direction").SetValue(input, Vector2Int.up);
            typeof(GameInput).GetMethod("Update", Hidden).Invoke(input, null);
            Check(input.Direction == Vector2Int.zero, "open menu clears manual and mobile direction");
            autoMode.Tick(now + AutoModeController.IdleSeconds + 2f);
            Check(!autoMode.Active, "AUTO cannot start behind menu");
            menu.ToggleAuto(now + 3f);
            Check(!autoMode.AutoEnabled && menu.IsOpen, "one menu click turns AUTO OFF");
            menu.ToggleAuto(now + 4f);
            Check(autoMode.AutoEnabled && !autoMode.Active, "one menu click turns AUTO ON while paused");
            menu.SetOpen(false, now + 5f);
            Check(!menu.IsOpen && !autoMode.MenuSuspended && autoMode.AutoEnabled &&
                Mathf.Approximately(autoMode.LastUserAt, now + 5f), "close restores input and resets idle timer");
            autoMode.Tick(now + 5f + AutoModeController.IdleSeconds - 1f);
            Check(!autoMode.Active, "AUTO does not start immediately after close");
            menu.SetOpen(true, now + 6f);
            Check(menu.IsOpen, "same button can reopen menu");
            menu.SetOpen(false, now + 7f);
            Check(!menu.IsOpen, "same button can close menu");

            area.Enter();
            Check(menu.isActiveAndEnabled && !menu.IsOpen, "same persistent menu remains indoors");
            area.Exit();
            hud.ShowMessage("テスト");
            menu.SetOpen(true, Time.unscaledTime);
            Check(!menu.IsOpen && hud.HasActiveMessage, "message is preserved and blocks menu open");
            Debug.Log("HALKA ver2.1 menu and ver2.0 gameplay regression checks passed.");
        }

        private static object Field(object target, string name) =>
            target.GetType().GetField(name, Hidden).GetValue(target);

        private static void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("Version21Checks failed: " + name);
        }
    }
}
