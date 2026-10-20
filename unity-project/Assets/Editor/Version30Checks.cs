using System;
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
    public static class Version30Checks
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

        [MenuItem("HALKA/Validate ver3.0 Preview scene")]
        public static void Run()
        {
            Version29Checks.Run();
            Check(GameVersion.Value == "3.0" && GameVersion.IsPreview &&
                GameVersion.Label == "ver3.0 Preview", "preview version identity");
            Check(WorldTimeCycle.PhaseAt(1000000L, 1000000L) == WorldPhase.Morning &&
                WorldTimeCycle.PhaseAt(1000000L, 1599900L) == WorldPhase.Morning &&
                WorldTimeCycle.PhaseAt(1000000L, 1600000L) == WorldPhase.Night &&
                WorldTimeCycle.PhaseAt(1000000L, 2199900L) == WorldPhase.Night &&
                WorldTimeCycle.PhaseAt(1000000L, 2200000L) == WorldPhase.Morning &&
                WorldTimeCycle.PhaseAt(1000000L, 2800000L) == WorldPhase.Night &&
                WorldTimeCycle.PhaseAt(1000000L, 3400000L) == WorldPhase.Morning,
                "real 600-second phase boundaries and 20-minute cycle");
            Check(Math.Abs(WorldTimeCycle.SecondsUntilChange(1000000L, 1599900L) - 0.1) < 0.001 &&
                !WorldTimeCycle.TryReadAnchor("broken", out _) &&
                !WorldTimeCycle.TryReadAnchor("-9", out _),
                "next switch time and corrupt storage recovery");

            ProjectBuilder.PrepareScene();
            EditorSceneManager.OpenScene("Assets/Scenes/FirstDay.unity");
            var clock = UnityEngine.Object.FindFirstObjectByType<WorldTimeController>();
            var lighting = UnityEngine.Object.FindFirstObjectByType<WorldNightOverlay2D>();
            var mapController = UnityEngine.Object.FindFirstObjectByType<MapWorldController2D>();
            var loader = UnityEngine.Object.FindFirstObjectByType<MapRuntimeLoader2D>();
            var crow = UnityEngine.Object.FindFirstObjectByType<CrowWander2D>(FindObjectsInactive.Include);
            var examine = crow != null ? crow.GetComponent<ExamineInteractable>() : null;
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerMover>();
            var hud = UnityEngine.Object.FindFirstObjectByType<GameHud>();
            Check(clock != null && lighting != null && mapController != null && loader != null &&
                crow != null && examine != null && player != null && hud != null,
                "time, overlay and crow scene wiring");
            var tint = (SpriteRenderer)Get(lighting, "artwork");
            Check(tint != null && tint.sprite != null && tint.sortingOrder > 100 &&
                tint.GetComponent<Collider2D>() == null &&
                tint.transform.parent == ((Camera)Get(lighting, "worldCamera")).transform &&
                WorldNightOverlay2D.TargetAlpha(WorldPhase.Morning, "first_field") == 0f &&
                WorldNightOverlay2D.TargetAlpha(WorldPhase.Night, "first_field") >
                    WorldNightOverlay2D.TargetAlpha(WorldPhase.Night, "halka_house"),
                "world-only overlay, clear morning and brighter interior");

            var hadValue = PlayerPrefs.HasKey(WorldTimeController.SaveKey);
            var oldValue = PlayerPrefs.GetString(WorldTimeController.SaveKey, "");
            const long anchor = 1791763200000L;
            try
            {
                PlayerPrefs.SetString(WorldTimeController.SaveKey, anchor.ToString());
                clock.LoadOrStart(anchor);
                Check(clock.CurrentPhase == WorldPhase.Morning &&
                    clock.AnchorMilliseconds == anchor, "saved initial morning anchor");
                var changed = 0;
                clock.PhaseChanged += _ => changed++;
                clock.RefreshAt(anchor + 600000L);
                Check(clock.CurrentPhase == WorldPhase.Night && changed == 1,
                    "night notification at 600 seconds");
                clock.RefreshAt(anchor + 1200000L);
                Check(clock.CurrentPhase == WorldPhase.Morning && changed == 2,
                    "morning notification at 1200 seconds");
                clock.LoadOrStart(anchor + 900000L);
                Check(clock.AnchorMilliseconds == anchor && clock.CurrentPhase == WorldPhase.Night,
                    "reload and offline time retain anchor");
                Check(mapController.ActiveMapId == "first_field" && clock.AnchorMilliseconds == anchor,
                    "one clock is shared across maps");
                PlayerPrefs.SetString(WorldTimeController.SaveKey, "not a timestamp");
                clock.LoadOrStart(anchor + 5000L);
                Check(clock.CurrentPhase == WorldPhase.Morning &&
                    clock.AnchorMilliseconds == anchor + 5000L,
                    "invalid save falls back to morning");
                clock.RefreshAt(anchor - 5000L);
                Check(clock.CurrentPhase == WorldPhase.Morning,
                    "clock rollback is safe");
            }
            finally
            {
                if (hadValue) PlayerPrefs.SetString(WorldTimeController.SaveKey, oldValue);
                else PlayerPrefs.DeleteKey(WorldTimeController.SaveKey);
                PlayerPrefs.Save();
            }

            loader.Build();
            Check((string)Get(examine, "message") == "カァ。" &&
                ReferenceEquals(Get(examine, "availability"), crow) &&
                Get(examine, "interactionAudio") is InteractionAudio &&
                crow.FindPerchCandidates().Length > 0 && crow.CanInteract,
                "ground interaction keeps existing message, voice and map trees");
            var now = Time.time;
            var originalNaturalFlight = (float)Get(crow, "nextFlightAt");
            player.TeleportTo(crow.Cell + Vector2Int.down);
            player.SetFacing(FacingDirection.Up);
            var voice = (InteractionAudio)Get(examine, "interactionAudio");
            examine.Interact();
            var request = (float)Get(crow, "interactionTakeoffAt");
            Check(hud.HasActiveMessage && voice.PlayRequestCount == 1 &&
                request >= now + 0.3f && request <= now + 0.6f &&
                !crow.CanInteract && crow.Activity == CrowActivity.GroundIdle &&
                Mathf.Approximately((float)Get(crow, "nextFlightAt"), originalNaturalFlight),
                "real examine shows message, requests voice and schedules delayed flight");
            examine.Interact();
            Check(voice.PlayRequestCount == 1 &&
                Mathf.Approximately((float)Get(crow, "interactionTakeoffAt"), request),
                "repeated interaction does not double-start flight");
            crow.Tick(request + 0.001f);
            Check(crow.Activity == CrowActivity.Takeoff && !crow.CanInteract,
                "interaction enters existing takeoff state");
            crow.Tick(request + 0.5f);
            Check(crow.Activity == CrowActivity.Flying, "interaction reuses continuous flight");
            crow.Tick(request + 4.1f);
            Check(crow.Activity == CrowActivity.Perched && crow.TargetTree != null,
                "interaction flight reaches an authored tree");

            var outdoor = AssetDatabase.LoadAssetAtPath<MapDefinition>(
                "Assets/Content/Maps/first_field.asset");
            var indoor = AssetDatabase.LoadAssetAtPath<MapDefinition>(
                "Assets/Content/Maps/halka_house.asset");
            Check(outdoor != null && indoor != null && outdoor.DataVersion == 4 &&
                indoor.DataVersion == 4 && outdoor.Objects.Any(item =>
                    item.Definition.StableId == "tree_basic"),
                "formal v4 maps and tree instances remain intact");

            var directionTexts = new[] { "こっちは北。", "こっちは東。", "こっちは南。", "こっちは西。" };
            var directionalSigns = outdoor.Objects.Where(item =>
                item.InstanceId.StartsWith("obj_sign_", StringComparison.Ordinal) &&
                item.InstanceId.EndsWith("_v30_preview", StringComparison.Ordinal)).ToArray();
            Check(directionalSigns.Length == 4 &&
                directionalSigns.All(item => item.Definition.StableId == "sign_basic" &&
                    directionTexts.Contains(item.SignText)) &&
                directionalSigns.Select(item => item.SignText).Distinct().Count() == 4,
                "four independent directional sign messages are authored in the formal map");
            var roomObjects = new[] { "recording_mic_basic", "art_easel_basic", "studio_shelf_basic" };
            Check(roomObjects.All(id => indoor.Objects.Count(item =>
                    item.Definition != null && item.Definition.StableId == id) == 1) &&
                MapPlacementRules.Validate(indoor).Count == 0,
                "music, singing and painting furniture remain editable and keep the room valid");
            foreach (var art in new[] { ("desk", 64, 32), ("house_floor", 32, 32),
                ("recording_mic", 32, 64), ("art_easel", 64, 64), ("studio_shelf", 64, 32) })
            {
                var path = "Assets/Content/World/" + art.Item1 + ".png";
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Check(sprite != null && importer != null &&
                    sprite.rect.width == art.Item2 && sprite.rect.height == art.Item3 &&
                    importer.filterMode == FilterMode.Point && !importer.mipmapEnabled &&
                    importer.textureCompression == TextureImporterCompression.Uncompressed &&
                    Mathf.Approximately(importer.spritePixelsPerUnit, 64f),
                    art.Item1 + " keeps the approved pixel-art import contract");
            }
            var houseArea = UnityEngine.Object.FindFirstObjectByType<HouseArea2D>();
            var camera = houseArea != null ? (Camera)Get(houseArea, "worldCamera") : null;
            Check(houseArea != null && camera != null, "house camera is available");
            var outdoorCameraSize = camera.orthographicSize;
            typeof(HouseArea2D).GetMethod("Awake", Hidden).Invoke(houseArea, null);
            houseArea.Enter();
            Check(mapController.ActiveMapId == "halka_house" &&
                Mathf.Approximately(camera.orthographicSize, outdoorCameraSize),
                "interior keeps the outdoor camera scale");
            houseArea.Exit();
            Check(mapController.ActiveMapId == "first_field" &&
                Mathf.Approximately(camera.orthographicSize, outdoorCameraSize),
                "outdoor camera scale is restored after leaving");
            Debug.Log("HALKA ver3.0 Preview time, lighting, crow trigger and inherited checks passed.");
        }

        private static object Get(object target, string name) =>
            target.GetType().GetField(name, Hidden).GetValue(target);
        private static void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException("Version30Checks failed: " + description);
        }
    }
}
