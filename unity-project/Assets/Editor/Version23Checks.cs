using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Halka.Game.Core;
using Halka.Game.Interaction;
using Halka.Game.Player;
using Halka.Game.UI;
using Halka.Game.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Halka.Game.Editor
{
    public static class Version23Checks
    {
        [MenuItem("HALKA/Validate ver2.3 scene")]
        public static void Run()
        {
            Version22Checks.Run();
            EditorSceneManager.OpenScene("Assets/Scenes/FirstDay.unity");
            Check(GameVersion.Value == "2.3" || GameVersion.Value == "2.4" ||
                GameVersion.Value == "2.5" || GameVersion.Value == "2.5.1" ||
                GameVersion.Value == "2.5.2",
                "supported game version");
            Check(GameBitmapFont.IsAvailable &&
                GameBitmapFont.HasAllGlyphs("メニュー AUTO ON OFF せいかつきろく あるいたかず プレイじかん もどる") &&
                GameBitmapFont.HasAllGlyphs("いし。 はな。 き。 つくえ。 ベッド。 かんばん。 123 ABC") &&
                GameBitmapFont.HasAllGlyphs("きた ひがし みなみ にし もり いえ") &&
                !GameBitmapFont.HasAllGlyphs("𠮷"), "rasterized primary and missing glyph fallback");
            var menu = UnityEngine.Object.FindFirstObjectByType<GameMenuController>();
            var hud = UnityEngine.Object.FindFirstObjectByType<GameHud>();
            var actionButton = UnityEngine.Object.FindFirstObjectByType<Halka.Game.Input.TouchActionButton>();
            Check(menu != null && hud != null && actionButton != null &&
                new SerializedObject(menu).FindProperty("font").objectReferenceValue != null &&
                new SerializedObject(hud).FindProperty("messageFont").objectReferenceValue != null &&
                new SerializedObject(actionButton).FindProperty("fallbackFont").objectReferenceValue != null,
                "menu, message and touch fallback references");
            Check(!System.IO.Directory.GetFiles(Application.dataPath, "*Asobi*", SearchOption.AllDirectories)
                    .Any(path => path.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) ||
                                 path.EndsWith(".otf", StringComparison.OrdinalIgnoreCase)),
                "no raw Asobi font in Unity Assets");
            CheckFurnitureFixture();
            Debug.Log("HALKA ver2.3 furniture, raster font and inherited regression checks passed.");
        }

        private static void CheckFurnitureFixture()
        {
            var bed = Definition("bed_basic");
            var cushion = Definition("cushion_basic");
            var desk = Definition("desk_basic");
            Check(bed != null && bed.ActionPoints.Count == 1 &&
                bed.ActionPoints[0].Id == "sleep_main" &&
                bed.ActionPoints[0].ActionType == WorldActionType.Sleep &&
                bed.ActionPoints[0].PlayerCellOffset == Vector2Int.down &&
                bed.ActionPoints[0].PoseKey == "bed_sleep", "reachable bed action point");
            Check(cushion != null && !cushion.BlocksMovement &&
                cushion.ActionPoints[0].ActionType == WorldActionType.Sit &&
                desk != null && desk.BlockedCellOffsets.Count == 2 &&
                desk.ActionPoints[0].InteractionText == "つくえ。", "cushion and desk definitions");

            var fixture = ScriptableObject.CreateInstance<MapDefinition>();
            var root = new GameObject("ver2.3 furniture fixture");
            root.transform.position = new Vector3(100f, 100f);
            try
            {
                fixture.ReplaceFromAuthoring("v23_furniture_fixture", "Furniture fixture", "interior",
                    null, "none", Color.black, new Vector2Int(-6, -4), new Vector2Int(6, 4),
                    new List<SurfacePlacement>(), new List<WorldObjectPlacement> {
                        new WorldObjectPlacement { InstanceId = "test_bed", RootCell = new Vector2Int(-5, 0), Definition = bed },
                        new WorldObjectPlacement { InstanceId = "test_cushion", RootCell = new Vector2Int(0, 0), Definition = cushion },
                        new WorldObjectPlacement { InstanceId = "test_desk", RootCell = new Vector2Int(3, 0), Definition = desk }
                    }, new List<LockedMapMarker>());
                Check(MapPlacementRules.Validate(fixture).Count == 0 &&
                    !MapPlacementRules.BlocksMovement(fixture, Vector2Int.zero) &&
                    MapPlacementRules.BlocksMovement(fixture, new Vector2Int(3, 0)) &&
                    MapPlacementRules.BlocksMovement(fixture, new Vector2Int(4, 0)),
                    "test map passability and two-cell desk collision");
                var grid = root.AddComponent<GridWorld2D>();
                var surfaces = root.AddComponent<GroundSurfaceField2D>();
                var player = root.AddComponent<PlayerMover>();
                var renderer = root.AddComponent<SpriteRenderer>();
                renderer.sortingOrder = 10;
                var hud = root.AddComponent<GameHud>();
                var surfaceRoot = new GameObject("Surfaces").transform;
                var objectRoot = new GameObject("Objects").transform;
                surfaceRoot.SetParent(root.transform, false);
                objectRoot.SetParent(root.transform, false);
                var loader = root.AddComponent<MapRuntimeLoader2D>();
                var serialized = new SerializedObject(loader);
                serialized.FindProperty("map").objectReferenceValue = fixture;
                serialized.FindProperty("world").objectReferenceValue = grid;
                serialized.FindProperty("surfaceField").objectReferenceValue = surfaces;
                serialized.FindProperty("surfaceRoot").objectReferenceValue = surfaceRoot;
                serialized.FindProperty("objectRoot").objectReferenceValue = objectRoot;
                serialized.FindProperty("player").objectReferenceValue = player;
                serialized.FindProperty("playerRenderer").objectReferenceValue = renderer;
                serialized.FindProperty("hud").objectReferenceValue = hud;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var playerData = new SerializedObject(player);
                playerData.FindProperty("world").objectReferenceValue = grid;
                playerData.ApplyModifiedPropertiesWithoutUndo();
                loader.Build();
                Check(objectRoot.childCount == 3 &&
                    objectRoot.GetChild(0).GetComponent<WorldObjectActionInteractable>() != null &&
                    objectRoot.GetChild(1).GetComponent<WorldObjectActionInteractable>() != null &&
                    objectRoot.GetChild(2).GetComponent<WorldObjectActionInteractable>() != null,
                    "definition-driven furniture runtime on a copied test map");
                Call(player, "Awake");
                player.TeleportTo(Vector2Int.down);
                var steps = 0;
                player.StepCompleted += _ => steps++;
                Check(player.TryStep(Vector2Int.up), "cushion can be entered");
                Call(player, "Tick", PlayerMover.DefaultStepSeconds, Vector2Int.zero);
                Check(steps == 1 && player.Cell == Vector2Int.zero, "cushion completed step counts once");
                Check(player.TryStep(Vector2Int.down), "cushion can be exited");
                Call(player, "Tick", PlayerMover.DefaultStepSeconds, Vector2Int.zero);
                Check(steps == 2, "leaving cushion is a normal step");
                player.TeleportTo(new Vector2Int(3, -1));
                Check(!player.TryStep(Vector2Int.up) && steps == 2, "desk blocks step");
                var deskAction = objectRoot.GetChild(2).GetComponent<WorldObjectActionInteractable>();
                deskAction.Interact();
                Check(Message(hud) == "つくえ。" && steps == 2, "desk action uses existing HUD without step");
                player.TeleportTo(new Vector2Int(-5, -1));
                player.SetFacing(FacingDirection.Up);
                var bedAction = objectRoot.GetChild(0).GetComponent<WorldObjectActionInteractable>();
                bedAction.Interact();
                Check(Message(hud) == "ベッド。" && steps == 2, "bed action does not move time or steps");
                player.TeleportTo(Vector2Int.zero);
                objectRoot.GetChild(1).GetComponent<WorldObjectActionInteractable>().Interact();
                Check(steps == 2, "unimplemented sit action is safe");
                Check(objectRoot.GetChild(1).GetComponentInChildren<SpriteRenderer>().sortingOrder <
                    renderer.sortingOrder, "cushion renders beneath Player");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(fixture);
            }
        }

        private static string Message(GameHud hud)
        {
            var field = typeof(GameHud).GetField("message", BindingFlags.Instance | BindingFlags.NonPublic);
            return ((TimedMessage)field.GetValue(hud)).TextAt(Time.unscaledTime);
        }

        private static void Call(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);

        private static WorldObjectDefinition Definition(string id) =>
            AssetDatabase.LoadAssetAtPath<WorldObjectDefinition>("Assets/Content/Maps/" + id + ".asset");

        private static void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("Version23Checks failed: " + name);
        }
    }
}
