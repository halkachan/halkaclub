using System;
using System.Linq;
using System.Reflection;
using Halka.Game.Core;
using Halka.Game.Interaction;
using Halka.Game.Player;
using Halka.Game.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Halka.Game.Editor
{
    public static class Version24Checks
    {
        [MenuItem("HALKA/Validate ver2.4 scene")]
        public static void Run()
        {
            Version23Checks.Run();
            EditorSceneManager.OpenScene("Assets/Scenes/FirstDay.unity");
            Check(GameVersion.Value == "2.4" || GameVersion.Value == "2.5" ||
                GameVersion.Value == "2.5.1" || GameVersion.Value == "2.5.2" ||
                GameVersion.Value == "2.5.3", "supported game version");
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Content/Character/bench_sit.png");
            Check(sprite != null && sprite.rect.width == 64 && sprite.rect.height == 64 &&
                Mathf.Approximately(sprite.pixelsPerUnit, 64f), "approved seat sprite dimensions and PPU");
            var importer = AssetImporter.GetAtPath("Assets/Content/Character/bench_sit.png") as TextureImporter;
            Check(importer != null && importer.filterMode == FilterMode.Point &&
                !importer.mipmapEnabled &&
                importer.textureCompression == TextureImporterCompression.Uncompressed,
                "seat sprite import settings");
            var sceneSeat = UnityEngine.Object.FindFirstObjectByType<PlayerSeatController>();
            var sceneVisual = UnityEngine.Object.FindFirstObjectByType<CharacterVisual>();
            var sceneAuto = UnityEngine.Object.FindFirstObjectByType<AutoModeController>();
            Check(sceneSeat != null && sceneVisual != null && sceneAuto != null &&
                new SerializedObject(sceneVisual).FindProperty("benchSit").objectReferenceValue == sprite,
                "scene seat controller and approved sprite binding");
            Check(AssetDatabase.LoadAssetAtPath<WorldObjectDefinition>(
                "Assets/Content/Maps/cushion_basic.asset").ActionPoints[0].PoseKey == "cushion_sit",
                "cushion pose remains future metadata");
            CheckBenchFixture(sprite);
            Debug.Log("HALKA ver2.4 bench seat and inherited regression checks passed.");
        }

        private static void CheckBenchFixture(Sprite seatedSprite)
        {
            var bench = AssetDatabase.LoadAssetAtPath<WorldObjectDefinition>(
                "Assets/Content/Maps/bench_basic.asset");
            Check(bench != null && bench.Footprint == new Vector2Int(2, 1) &&
                bench.BlockedCellOffsets.Count == 2 && bench.ActionPoints.Count == 2 &&
                bench.ActionPoints.Select(point => point.Id).SequenceEqual(new[] { "seat_left", "seat_right" }) &&
                bench.ActionPoints.All(point => point.ActionType == WorldActionType.Sit &&
                    point.PoseKey == PlayerSeatController.BenchPoseKey &&
                    point.PlayerFacing == WorldFacing.Up), "bench authored action points");
            var root = new GameObject("ver2.4 bench fixture");
            root.transform.position = new Vector3(100f, 100f);
            try
            {
                var world = root.AddComponent<GridWorld2D>();
                var playerObject = new GameObject("fixture player");
                playerObject.transform.SetParent(root.transform, false);
                playerObject.transform.position = world.CellToWorld(new Vector2Int(0, -1));
                var player = playerObject.AddComponent<PlayerMover>();
                Set(player, "world", world);
                Call(player, "Awake");
                var artwork = new GameObject("artwork").transform;
                artwork.SetParent(playerObject.transform, false);
                artwork.localPosition = Vector3.up * 0.25f;
                var artworkRenderer = artwork.gameObject.AddComponent<SpriteRenderer>();
                var seat = playerObject.AddComponent<PlayerSeatController>();
                Set(seat, "mover", player);
                Set(seat, "world", world);
                Set(seat, "artwork", artwork);
                var visual = playerObject.AddComponent<CharacterVisual>();
                Set(visual, "mover", player);
                Set(visual, "spriteRenderer", artworkRenderer);
                Set(visual, "seat", seat);
                Set(visual, "benchSit", seatedSprite);
                var standingSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                    "Assets/Content/Character/front_idle/00.png");
                typeof(CharacterVisual).GetField("frontIdle", BindingFlags.NonPublic |
                    BindingFlags.Instance).SetValue(visual, new[] { standingSprite });
                var standingBack = AssetDatabase.LoadAssetAtPath<Sprite>(
                    "Assets/Content/Character/back_idle/00.png");
                typeof(CharacterVisual).GetField("backIdle", BindingFlags.NonPublic |
                    BindingFlags.Instance).SetValue(visual, new[] { standingBack });
                var hud = root.AddComponent<Halka.Game.UI.GameHud>();
                var benchObject = new GameObject("fixture bench");
                benchObject.transform.SetParent(root.transform, false);
                benchObject.transform.position = world.CellToWorld(Vector2Int.zero);
                var action = benchObject.AddComponent<WorldObjectActionInteractable>();
                action.Configure(bench, player, world, hud, null);
                var benchArtwork = new GameObject("bench click target");
                benchArtwork.transform.SetParent(benchObject.transform, false);
                benchArtwork.transform.localPosition = Vector3.right * 0.25f;
                benchArtwork.AddComponent<BoxCollider2D>().size = new Vector2(1f, 0.5f);
                var router = root.AddComponent<InteractionRouter>();
                Set(router, "player", player);
                Set(router, "world", world);
                var house = root.AddComponent<HouseArea2D>();
                var auto = root.AddComponent<AutoModeController>();
                Set(auto, "player", player);
                Set(auto, "world", world);
                Set(auto, "house", house);
                Call(auto, "Awake");

                foreach (var x in new[] { 0, 1 })
                {
                    var standingCell = new Vector2Int(x, -1);
                    player.TeleportTo(standingCell);
                    player.SetFacing(FacingDirection.Up);
                    Check(action.CanInteractAt(standingCell, FacingDirection.Up), "both seats reachable");
                    router.TryInteractAhead();
                    Call(visual, "Update");
                    Check(seat.IsSeated && seat.ActiveSeat == benchObject.transform &&
                        player.Cell == standingCell && player.MovementLocked &&
                        artworkRenderer.sprite == seatedSprite &&
                        artwork.position == world.CellToWorld(new Vector2Int(x, 0)) +
                            Vector3.up * PlayerSeatController.ArtworkAboveSeatCenter &&
                        !player.TryStep(Vector2Int.left), "seat locks movement without teleport");
                    auto.Tick(Time.unscaledTime + AutoModeController.IdleSeconds + 1f);
                    Check(auto.AutoEnabled && !auto.Active,
                        "seated AUTO stays enabled but does not walk");
                    Check(!seat.TrySit(benchObject.transform, "cushion_sit"),
                        "unimplemented cushion pose cannot start bench state");
                    action.Interact();
                    auto.RecordUserAction(Time.unscaledTime);
                    Call(visual, "Update");
                    Check(!seat.IsSeated && !player.MovementLocked &&
                        player.Cell == standingCell && artwork.localPosition == Vector3.up * 0.25f &&
                        artworkRenderer.sprite == standingBack,
                        "second action stands at original cell");
                }
                player.TeleportTo(new Vector2Int(0, -1));
                player.SetFacing(FacingDirection.Up);
                action.Interact();
                benchObject.SetActive(false);
                // Edit-mode fixtures do not receive the play-mode OnDisable callback.
                Call(action, "OnDisable");
                Check(!seat.IsSeated && !player.MovementLocked,
                    "map unload clears seat and movement lock");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var data = new SerializedObject(target);
            data.FindProperty(field).objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Call(object target, string method) =>
            target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(target, null);

        private static void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("Version24Checks failed: " + name);
        }
    }
}
