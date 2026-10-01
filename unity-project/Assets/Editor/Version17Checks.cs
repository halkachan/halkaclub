using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Halka.Game.Core;
using Halka.Game.Input;
using Halka.Game.Interaction;
using Halka.Game.Player;
using Halka.Game.UI;
using Halka.Game.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Halka.Game.Editor
{
    public static class Version17Checks
    {
        [MenuItem("HALKA/Validate ver1.7")]
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/FirstDay.unity");
            var world = UnityEngine.Object.FindFirstObjectByType<GridWorld2D>();
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerMover>();
            var visual = UnityEngine.Object.FindFirstObjectByType<CharacterVisual>();
            var hud = UnityEngine.Object.FindFirstObjectByType<GameHud>();
            var router = UnityEngine.Object.FindFirstObjectByType<InteractionRouter>();
            var worldCamera = UnityEngine.Object.FindFirstObjectByType<Camera>();
            var stone = GameObject.Find("Stone - first world object").GetComponent<GridObstacle>();
            var flower = GameObject.Find("Flower - first bloom").GetComponent<GridObstacle>();
            var examine = stone.GetComponent<ExamineInteractable>();
            var flowerExamine = flower.GetComponent<ExamineInteractable>();
            var artwork = player.GetComponentInChildren<SpriteRenderer>().transform;
            Check(GameVersion.Value == "1.7", "version source");
            Check(GridWorld2D.TilePixels == 32 && Mathf.Approximately(GridWorld2D.TileWorldSize, 0.5f) &&
                Mathf.Approximately(world.CellSize, GridWorld2D.TileWorldSize), "32-pixel grid scale");
            Check(world.MinCell == new Vector2Int(-10, -6) && world.MaxCell == new Vector2Int(10, 6),
                "physical field size preserved");
            Check(world.CellToWorld(Vector2Int.right) == new Vector3(0.5f, 0f, 0f) &&
                world.WorldToCell(new Vector3(0.5f, 0f, 0f)) == Vector2Int.right,
                "integer grid world conversion");
            Check(world.CellToWorld(Vector2Int.zero) == world.transform.position &&
                world.CellToWorld(Vector2Int.up) - world.CellToWorld(Vector2Int.zero) ==
                    Vector3.up * GridWorld2D.TileWorldSize,
                "CellToWorld returns each tile center");
            Check(artwork.localPosition == new Vector3(0f, ProjectBuilder.ArtworkFootOffset, 0f) &&
                Mathf.Approximately(ProjectBuilder.ArtworkFootOffset, 0.25f) &&
                player.transform.position == world.CellToWorld(player.Cell) &&
                artwork.position == player.transform.position + Vector3.up * 0.25f &&
                artwork.GetComponents<Component>().Length == 2,
                "two-cell artwork center without runtime offset component");
            var playerSprite = artwork.GetComponent<SpriteRenderer>().sprite;
            var playerHalfHeight = playerSprite.rect.height / playerSprite.pixelsPerUnit / 2f;
            Check(playerSprite.rect.width == 64f && playerSprite.rect.height == 64f &&
                playerSprite.pivot == new Vector2(32f, 32f) &&
                Mathf.Approximately(artwork.position.x, player.transform.position.x) &&
                Mathf.Approximately(artwork.position.y - playerHalfHeight,
                    player.transform.position.y - GridWorld2D.TileWorldSize / 2f) &&
                Mathf.Approximately(artwork.position.y + playerHalfHeight,
                    player.transform.position.y + GridWorld2D.TileWorldSize * 1.5f),
                "64-pixel art exactly spans foot tile and tile above");
            Check(UnityEngine.Object.FindObjectsByType<GridObstacle>(FindObjectsSortMode.None).Length == 2 &&
                UnityEngine.Object.FindObjectsByType<ExamineInteractable>(FindObjectsSortMode.None).Length == 2,
                "exactly one stone and one flower use shared world components");
            var stoneSprite = stone.GetComponent<SpriteRenderer>().sprite;
            var collider = stone.GetComponent<BoxCollider2D>();
            var stoneImporter = (TextureImporter)AssetImporter.GetAtPath("Assets/Content/World/stone.png");
            var stoneSettings = new TextureImporterSettings();
            stoneImporter.ReadTextureSettings(stoneSettings);
            Check(stoneSprite.texture.width == 32 && stoneSprite.texture.height == 32 &&
                stoneSprite.pivot == new Vector2(16f, 16f) &&
                stoneSettings.spriteAlignment == (int)SpriteAlignment.Center &&
                stoneSettings.spritePivot == new Vector2(0.5f, 0.5f) &&
                Mathf.Approximately(stoneSprite.pixelsPerUnit, 64f) && stone.transform.localScale == Vector3.one,
                "stone sprite uses centered 32-pixel canvas");
            Check(collider.size == new Vector2(28f / 64f, 20f / 64f) &&
                stone.transform.position == world.CellToWorld(new Vector2Int(1, 1)) &&
                !world.CanEnter(new Vector2Int(1, 1)), "stone collider and blocking cell");
            Check(Physics2D.OverlapPoint(stone.transform.position) == collider &&
                router.IsInteractableAt(worldCamera.WorldToScreenPoint(stone.transform.position)),
                "stone world pointer routes to shared interaction");
            var flowerSprite = flower.GetComponent<SpriteRenderer>().sprite;
            var flowerCollider = flower.GetComponent<BoxCollider2D>();
            var flowerImporter = (TextureImporter)AssetImporter.GetAtPath("Assets/Content/World/flower.png");
            var flowerSettings = new TextureImporterSettings();
            flowerImporter.ReadTextureSettings(flowerSettings);
            Check(flowerImporter.textureType == TextureImporterType.Sprite &&
                flowerImporter.spriteImportMode == SpriteImportMode.Single &&
                flowerImporter.filterMode == FilterMode.Point && !flowerImporter.mipmapEnabled &&
                flowerImporter.textureCompression == TextureImporterCompression.Uncompressed &&
                flowerImporter.npotScale == TextureImporterNPOTScale.None &&
                Mathf.Approximately(flowerImporter.spritePixelsPerUnit, 64f) &&
                flowerSettings.spriteAlignment == (int)SpriteAlignment.Center &&
                flowerSettings.spritePivot == new Vector2(0.5f, 0.5f) &&
                flowerSprite.texture.width == 32 && flowerSprite.texture.height == 32 &&
                flowerSprite.pivot == new Vector2(16f, 16f),
                "flower is a sharp centered 32-pixel RGBA sprite");
            Check(ProjectBuilder.FlowerCell == new Vector2Int(-3, 1) &&
                flower.transform.position == world.CellToWorld(ProjectBuilder.FlowerCell) &&
                flower.transform.localScale == Vector3.one &&
                flowerCollider != null && flowerCollider.size == Vector2.one * GridWorld2D.TileWorldSize &&
                flowerExamine is IInteractable && world.HasObstacle(ProjectBuilder.FlowerCell) &&
                !world.CanEnter(ProjectBuilder.FlowerCell) &&
                router.IsInteractableAt(worldCamera.WorldToScreenPoint(flower.transform.position)),
                "flower occupies one exact blocking and clickable cell");
            Check(new SerializedObject(flowerExamine).FindProperty("message").stringValue == "はな。",
                "flower uses the generic examine text");
            Check(world.CanEnter(new Vector2Int(0, 1)) && !world.CanEnter(new Vector2Int(11, 0)) &&
                !world.CanEnter(new Vector2Int(0, -7)), "adjacent and field bounds");
            var grid = GameObject.Find("FirstDay - 32px cell boundaries").GetComponent<SpriteRenderer>();
            Check(grid.sprite.texture.width == 21 * 32 && grid.sprite.texture.height == 13 * 32 &&
                Mathf.Approximately(grid.sprite.bounds.size.x, 10.5f) &&
                Mathf.Approximately(grid.sprite.bounds.size.y, 6.5f) &&
                grid.transform.position == Vector3.zero && grid.sprite.texture.filterMode == FilterMode.Point,
                "world-aligned sharp 32-pixel grid overlay");
            var grassGroup = GameObject.Find("Grass decorations");
            Check(grassGroup != null && grassGroup.transform.childCount == 271,
                "all 271 walkable cells have grass prefab instances");
            var grassField = grassGroup.GetComponent<GrassField2D>();
            Check(grassField != null && grassGroup.GetComponents<MonoBehaviour>().Length == 1,
                "one shared grass animation manager");
            var grassImporter = (TextureImporter)AssetImporter.GetAtPath("Assets/Content/World/grass.png");
            var grassSettings = new TextureImporterSettings();
            grassImporter.ReadTextureSettings(grassSettings);
            Check(grassImporter != null && grassImporter.textureType == TextureImporterType.Sprite &&
                grassImporter.spriteImportMode == SpriteImportMode.Single &&
                grassImporter.filterMode == FilterMode.Point && !grassImporter.mipmapEnabled &&
                grassImporter.textureCompression == TextureImporterCompression.Uncompressed &&
                grassImporter.npotScale == TextureImporterNPOTScale.None &&
                grassSettings.spriteAlignment == (int)SpriteAlignment.Center &&
                grassSettings.spritePivot == new Vector2(0.5f, 0.5f) &&
                Mathf.Approximately(grassImporter.spritePixelsPerUnit, 64f), "grass pixel import");
            var actualGrassCells = new System.Collections.Generic.HashSet<Vector2Int>();
            foreach (Transform grass in grassGroup.transform)
            {
                var renderer = grass.GetComponentInChildren<SpriteRenderer>();
                var cell = world.WorldToCell(grass.position);
                Check(PrefabUtility.IsPartOfPrefabInstance(grass.gameObject) && renderer != null &&
                    renderer.sprite != null && renderer.sprite.texture.width == 32 &&
                    renderer.sprite.texture.height == 32 &&
                    Mathf.Approximately(renderer.sprite.pixelsPerUnit, 64f) &&
                    renderer.sortingOrder == 0 && grass.localScale == Vector3.one &&
                    renderer.transform.localPosition == Vector3.zero &&
                    renderer.transform.position == world.CellToWorld(cell) &&
                    renderer.sprite.pivot == new Vector2(16f, 16f) &&
                    grass.position == world.CellToWorld(cell), "grass image center equals tile center");
                Check(grass.GetComponent<Collider2D>() == null &&
                    grass.GetComponent<GridObstacle>() == null &&
                    grass.GetComponent<ExamineInteractable>() == null &&
                    grass.GetComponents<MonoBehaviour>().Length == 0 && world.CanEnter(cell),
                    "grass remains walkable and non-interactive");
                Check(actualGrassCells.Add(cell), "no overlapping grass cells");
            }
            for (var y = world.MinCell.y; y <= world.MaxCell.y; y++)
            for (var x = world.MinCell.x; x <= world.MaxCell.x; x++)
            {
                var cell = new Vector2Int(x, y);
                Check(actualGrassCells.Contains(cell) == world.CanEnter(cell),
                    "grass exactly covers every walkable cell");
            }
            Check(actualGrassCells.Contains(Vector2Int.zero) &&
                !actualGrassCells.Contains(new Vector2Int(1, 1)) &&
                !actualGrassCells.Contains(ProjectBuilder.FlowerCell) &&
                actualGrassCells.Count == 21 * 13 - 2,
                "start cell covered; stone and flower cells excluded");
            Check(GameObject.Find("Grass Front Overlay") == null &&
                UnityEngine.Object.FindObjectsByType<GrassField2D>(FindObjectsSortMode.None).Length == 1 &&
                UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)
                    .All(renderer => renderer.sortingOrder != 20),
                "foreground grass overlay removed");
            Check(GameObject.Find("FirstDay - small ground").GetComponent<SpriteRenderer>().sortingOrder == -10 &&
                grid.sortingOrder == -9 && stone.GetComponent<SpriteRenderer>().sortingOrder == 2 &&
                flower.GetComponent<SpriteRenderer>().sortingOrder == 2 &&
                artwork.GetComponent<SpriteRenderer>().sortingOrder == 10,
                "ground grid grass world objects player render order");
            var occlusion = player.GetComponent<PlayerGrassOcclusion>();
            var playerMask = player.GetComponentInChildren<SpriteMask>();
            Check(occlusion != null && playerMask != null &&
                playerMask.transform.parent == artwork && playerMask.transform.localPosition == Vector3.zero &&
                playerMask.sprite.rect.size == new Vector2(64f, 64f) &&
                playerMask.sprite.pivot == new Vector2(32f, 32f) &&
                PlayerGrassOcclusion.GrassPlayerOcclusionPixels == 10 &&
                artwork.GetComponent<SpriteRenderer>().maskInteraction == SpriteMaskInteraction.None,
                "single player-only ten-pixel foot mask without artwork movement");
            var maskPixels = new Texture2D(2, 2);
            maskPixels.LoadImage(File.ReadAllBytes("Assets/Content/World/player_grass_mask.png"));
            Check(maskPixels.width == 64 && maskPixels.height == 64 &&
                maskPixels.GetPixel(32, 0).a == 0f && maskPixels.GetPixel(32, 9).a == 0f &&
                maskPixels.GetPixel(32, 10).a == 1f && maskPixels.GetPixel(32, 63).a == 1f,
                "mask hides exactly bottom ten canvas pixels");
            UnityEngine.Object.DestroyImmediate(maskPixels);
            var rustle = new SerializedObject(grassField);
            var rustleFrames = rustle.FindProperty("rustleFrames");
            var frameTimes = rustle.FindProperty("frameSeconds");
            Check(rustleFrames.arraySize == 5 &&
                frameTimes.arraySize == 5 &&
                rustle.FindProperty("idleSprite").objectReferenceValue ==
                    AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Content/World/grass.png"),
                "five grass rustle frames without front overlay state");
            var expectedMilliseconds = new[] { 90, 90, 90, 90, 420 };
            for (var i = 0; i < rustleFrames.arraySize; i++)
            {
                var sprite = (Sprite)rustleFrames.GetArrayElementAtIndex(i).objectReferenceValue;
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sprite));
                var backSettings = new TextureImporterSettings();
                importer.ReadTextureSettings(backSettings);
                Check(sprite.texture.width == 32 && sprite.texture.height == 32 &&
                    Mathf.Approximately(sprite.pixelsPerUnit, 64f) &&
                    sprite.pivot == new Vector2(16f, 16f) &&
                    backSettings.spriteAlignment == (int)SpriteAlignment.Center &&
                    importer.textureType == TextureImporterType.Sprite &&
                    importer.spriteImportMode == SpriteImportMode.Single &&
                    importer.filterMode == FilterMode.Point && !importer.mipmapEnabled &&
                    importer.textureCompression == TextureImporterCompression.Uncompressed &&
                    importer.npotScale == TextureImporterNPOTScale.None &&
                    Mathf.Approximately(frameTimes.GetArrayElementAtIndex(i).floatValue,
                        expectedMilliseconds[i] / 1000f),
                    "rustle frame import and GIF timing");
            }
            foreach (var clip in new[] { "frontIdle", "backIdle", "leftIdle", "rightIdle",
                "frontWalk", "backWalk", "leftWalk", "rightWalk" })
            {
                var property = new SerializedObject(visual).FindProperty(clip);
                Check(property.arraySize > 0 &&
                    (!clip.EndsWith("Walk", StringComparison.Ordinal) || property.arraySize == 4),
                    clip + " available frames");
                for (var i = 0; i < property.arraySize; i++)
                {
                    var sprite = (Sprite)property.GetArrayElementAtIndex(i).objectReferenceValue;
                    Check(sprite != null, clip + " missing frame");
                    var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sprite));
                    var settings = new TextureImporterSettings();
                    importer.ReadTextureSettings(settings);
                    Check(sprite != null && sprite.rect.width == 64f && sprite.rect.height == 64f &&
                        sprite.pivot == new Vector2(32f, 32f) &&
                        sprite.texture.filterMode == FilterMode.Point &&
                        settings.spriteAlignment == (int)SpriteAlignment.Center &&
                        Mathf.Approximately(sprite.pixelsPerUnit, 64f) &&
                        artwork.localPosition == new Vector3(0f, 0.25f, 0f),
                        clip + " centered sprite and shared artwork anchor");
                }
            }
            var select = typeof(CharacterVisual).GetMethod("SelectFrames", BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var direction in new[] { FacingDirection.Up, FacingDirection.Down,
                FacingDirection.Left, FacingDirection.Right })
            {
                var walk = (Sprite[])select.Invoke(visual, new object[] { direction, true });
                var idle = (Sprite[])select.Invoke(visual, new object[] { direction, false });
                Check(walk.Length == 4 && idle.Length > 0 && walk != idle,
                    direction + " walk/idle selection");
            }
            Check(Mathf.Approximately(CharacterVisual.OriginalWalkFrameSeconds, 0.15f) &&
                Mathf.Approximately(PlayerMover.DefaultStepSeconds, 0.18f), "original GIF timing and step duration");
            Check(new SerializedObject(examine).FindProperty("message").stringValue == "いし。" &&
                new SerializedObject(hud).FindProperty("messageFont").objectReferenceValue ==
                    AssetDatabase.LoadAssetAtPath<Font>("Assets/Content/Fonts/k8x12L.ttf"),
                "generic stone examine uses k8x12L");
            var target = new Vector2Int(1, 1);
            foreach (var side in new[] { Vector2Int.up, Vector2Int.down,
                Vector2Int.left, Vector2Int.right })
            {
                var neighbor = target + side;
                Check(world.CanEnter(neighbor) && !world.CanEnter(target) &&
                    artwork.localPosition == new Vector3(0f, ProjectBuilder.ArtworkFootOffset, 0f),
                    "stone approach from all four sides keeps fixed artwork offset");
            }
            foreach (var facing in new[] { FacingDirection.Up, FacingDirection.Down,
                FacingDirection.Left, FacingDirection.Right })
            {
                var adjacent = target - facing.ToVector();
                Check(ExamineInteractable.IsInRange(adjacent, false, facing, target),
                    facing + " adjacent facing succeeds");
                foreach (var other in new[] { FacingDirection.Up, FacingDirection.Down,
                    FacingDirection.Left, FacingDirection.Right })
                    if (other != facing)
                        Check(!ExamineInteractable.IsInRange(adjacent, false, other, target),
                            "wrong facing rejected");
                Check(!ExamineInteractable.IsInRange(adjacent, true, facing, target),
                    "moving interaction rejected");
            }
            Check(!ExamineInteractable.IsInRange(Vector2Int.zero, false, FacingDirection.Right, target) &&
                !ExamineInteractable.IsInRange(new Vector2Int(-1, 1), false, FacingDirection.Right, target),
                "diagonal and distant interaction rejected");
            typeof(PlayerMover).GetField("motion", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(player, new GridStepMotion(new Vector2Int(0, 1)));
            var messageField = typeof(GameHud).GetField("message", BindingFlags.NonPublic | BindingFlags.Instance);
            hud.ShowMessage(string.Empty);
            examine.Interact();
            Check(string.IsNullOrEmpty(((TimedMessage)messageField.GetValue(hud)).TextAt(Time.unscaledTime)),
                "adjacent but wrong facing cannot examine");
            player.ApplyDirection(Vector2Int.right);
            Check(player.Facing == FacingDirection.Right && !player.IsMoving &&
                player.Cell == new Vector2Int(0, 1), "blocked stone input turns without entering");
            hud.ShowMessage(string.Empty);
            router.TryInteractAhead();
            var activeMessage = (TimedMessage)messageField.GetValue(hud);
            Check(activeMessage.TextAt(Time.unscaledTime) == "いし。",
                "front-cell action reaches shared stone examine");
            hud.ShowMessage(string.Empty);
            typeof(PlayerMover).GetField("<Facing>k__BackingField",
                BindingFlags.NonPublic | BindingFlags.Instance).SetValue(player, FacingDirection.Up);
            router.TryInteractAhead();
            Check(string.IsNullOrEmpty(activeMessage.TextAt(Time.unscaledTime)),
                "action does not auto-turn toward adjacent stone");
            typeof(PlayerMover).GetField("motion", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(player, new GridStepMotion(ProjectBuilder.FlowerCell + Vector2Int.left));
            hud.ShowMessage(string.Empty);
            router.TryInteract(worldCamera.WorldToScreenPoint(flower.transform.position));
            Check(string.IsNullOrEmpty(activeMessage.TextAt(Time.unscaledTime)),
                "clicking flower does not auto-turn");
            player.ApplyDirection(Vector2Int.right);
            Check(player.Facing == FacingDirection.Right && !player.IsMoving &&
                player.Cell == ProjectBuilder.FlowerCell + Vector2Int.left,
                "blocked flower input turns without entering");
            router.TryInteractAhead();
            Check(activeMessage.TextAt(Time.unscaledTime) == "はな。",
                "front-cell action reaches generic flower examine");
            hud.ShowMessage(string.Empty);
            router.TryInteract(worldCamera.WorldToScreenPoint(flower.transform.position));
            Check(activeMessage.TextAt(Time.unscaledTime) == "はな。",
                "desktop pointer reaches generic flower examine");
            hud.ShowMessage(string.Empty);
            player.ApplyDirection(Vector2Int.left);
            router.TryInteractAhead();
            Check(player.IsMoving && string.IsNullOrEmpty(activeMessage.TextAt(Time.unscaledTime)),
                "flower action ignored during a grid step");
            typeof(PlayerMover).GetField("motion", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(player, new GridStepMotion(new Vector2Int(0, 1)));
            typeof(PlayerMover).GetField("<Facing>k__BackingField",
                BindingFlags.NonPublic | BindingFlags.Instance).SetValue(player, FacingDirection.Up);
            player.ApplyDirection(Vector2Int.left);
            hud.ShowMessage(string.Empty);
            router.TryInteractAhead();
            Check(player.IsMoving && string.IsNullOrEmpty(activeMessage.TextAt(Time.unscaledTime)),
                "action is ignored during a grid step");
            typeof(PlayerMover).GetField("motion", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(player, new GridStepMotion(Vector2Int.zero));
            typeof(PlayerMover).GetField("<Facing>k__BackingField",
                BindingFlags.NonPublic | BindingFlags.Instance).SetValue(player, FacingDirection.Up);
            router.TryInteractAhead();
            Check(string.IsNullOrEmpty(activeMessage.TextAt(Time.unscaledTime)),
                "grass ahead has no action interaction");
            typeof(PlayerMover).GetField("motion", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(player, new GridStepMotion(new Vector2Int(-10, 1)));
            player.ApplyDirection(Vector2Int.left);
            Check(player.Facing == FacingDirection.Left && !player.IsMoving,
                "field wall input changes facing without movement");
            typeof(PlayerMover).GetField("motion", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(player, new GridStepMotion(new Vector2Int(0, 1)));
            hud.ShowMessage(string.Empty);
            examine.Interact();
            Check(string.IsNullOrEmpty(((TimedMessage)messageField.GetValue(hud)).TextAt(Time.unscaledTime)),
                "clicking stone does not auto-face or display message");
            var timed = new TimedMessage();
            timed.Show("いし。", 1f, 2f);
            Check(timed.TextAt(2.9f) == "いし。" && timed.TextAt(3f) == null,
                "two-second message lifetime");
            timed.Show("いし。", 2f, 2f);
            Check(timed.TextAt(3.5f) == "いし。" && timed.TextAt(4f) == null,
                "re-examine resets one message");
            var motion = new GridStepMotion(Vector2Int.zero);
            player.ApplyDirection(new Vector2Int(1, 1));
            Check(player.Facing == FacingDirection.Left && !player.IsMoving,
                "diagonal player input rejected without state change");
            Check(!motion.TryBegin(new Vector2Int(1, 1), world.CanEnter,
                PlayerMover.DefaultStepSeconds), "diagonal step rejected");
            Check(motion.TryBegin(Vector2Int.right, world.CanEnter,
                PlayerMover.DefaultStepSeconds), "cardinal step begins");
            Check(!motion.TryBegin(Vector2Int.up, world.CanEnter,
                PlayerMover.DefaultStepSeconds), "cannot turn during step");
            motion.Advance(PlayerMover.DefaultStepSeconds * 0.5f);
            Check(motion.Cell == Vector2Int.zero && motion.IsMoving &&
                world.CellToWorld(motion.Position) == new Vector3(0.25f, 0f, 0f),
                "only root interpolates half a 32-pixel step");
            motion.Advance(PlayerMover.DefaultStepSeconds * 0.5f);
            Check(!motion.IsMoving && motion.Cell == Vector2Int.right &&
                world.CellToWorld(motion.Position) == new Vector3(0.5f, 0f, 0f),
                "step ends at exact neighboring cell");
            Check(CardinalInput.Choose(true, false, false, true, Vector2Int.zero) == Vector2Int.up &&
                CardinalInput.Choose(false, false, true, false, Vector2Int.zero) == Vector2Int.left &&
                CardinalInput.Choose(false, false, false, true, Vector2Int.zero) == Vector2Int.right &&
                CardinalInput.Choose(false, true, false, false, Vector2Int.zero) == Vector2Int.down &&
                CardinalInput.Choose(false, false, false, false, Vector2Int.up) == Vector2Int.up,
                "keyboard and touch choose four cardinal directions");
            var dpad = UnityEngine.Object.FindFirstObjectByType<TouchDpad>();
            var actionButton = UnityEngine.Object.FindFirstObjectByType<TouchActionButton>();
            var gameInput = UnityEngine.Object.FindFirstObjectByType<GameInput>();
            Check(dpad != null && actionButton != null && gameInput != null &&
                new SerializedObject(actionButton).FindProperty("dpad").objectReferenceValue == dpad &&
                new SerializedObject(gameInput).FindProperty("actionButton").objectReferenceValue == actionButton &&
                new SerializedObject(hud).FindProperty("actionButton").objectReferenceValue == actionButton,
                "shared mobile D-pad and action button wiring");
            Check(new SerializedObject(router).FindProperty("player").objectReferenceValue == player &&
                new SerializedObject(router).FindProperty("world").objectReferenceValue == world,
                "front cell interaction uses shared router and grid");
            var footstep = player.GetComponent<PlayerFootstepAudio>();
            var audioSource = player.GetComponent<AudioSource>();
            var footstepClip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                "Assets/Content/Audio/footstep_one_step.wav");
            var originalMp3 = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Content/Audio/footstep.mp3");
            var footstepImporter = (AudioImporter)AssetImporter.GetAtPath(
                "Assets/Content/Audio/footstep_one_step.wav");
            Check(footstep != null && audioSource != null && footstepClip != null &&
                originalMp3 != null &&
                new SerializedObject(footstep).FindProperty("mover").objectReferenceValue == player &&
                new SerializedObject(footstep).FindProperty("source").objectReferenceValue == audioSource &&
                new SerializedObject(footstep).FindProperty("footstep").objectReferenceValue == footstepClip &&
                !audioSource.playOnAwake && audioSource.spatialBlend == 0f &&
                Mathf.Approximately(audioSource.volume, 0.45f) &&
                footstepClip.channels == 1 && footstepClip.frequency == 44100 &&
                footstepClip.length >= 0.09f && footstepClip.length <= 0.11f &&
                footstepImporter.defaultSampleSettings.compressionFormat == AudioCompressionFormat.PCM &&
                footstepImporter.defaultSampleSettings.sampleRateSetting ==
                    AudioSampleRateSetting.PreserveSampleRate &&
                new SerializedObject(footstepImporter).FindProperty("m_Normalize").boolValue == false &&
                !File.Exists("Assets/Content/Audio/footstep.wav"),
                "one-burst excerpt is wired; original MP3 is preserved and obsolete WAV removed");
            var startedCells = new System.Collections.Generic.List<Vector2Int>();
            var enteredCells = new System.Collections.Generic.List<Vector2Int>();
            Action<Vector2Int> startListener = cell => startedCells.Add(cell);
            Action<Vector2Int> completeListener = cell => enteredCells.Add(cell);
            player.StepStarted += startListener;
            player.StepCompleted += completeListener;
            typeof(PlayerMover).GetField("motion", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(player, new GridStepMotion(new Vector2Int(0, 1)));
            player.ApplyDirection(Vector2Int.right);
            Check(startedCells.Count == 0 && enteredCells.Count == 0 && !player.IsMoving,
                "blocked stone step does not announce entry");
            typeof(PlayerMover).GetField("motion", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(player, new GridStepMotion(ProjectBuilder.FlowerCell + Vector2Int.left));
            player.ApplyDirection(Vector2Int.right);
            Check(startedCells.Count == 0 && enteredCells.Count == 0 && !player.IsMoving &&
                !grassField.HasGrass(ProjectBuilder.FlowerCell),
                "blocked flower step triggers neither footstep event nor grass rustle");
            typeof(PlayerMover).GetField("motion", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(player, new GridStepMotion(new Vector2Int(-10, 1)));
            player.ApplyDirection(Vector2Int.left);
            Check(startedCells.Count == 0 && !player.IsMoving,
                "field boundary does not announce a step");
            typeof(PlayerMover).GetField("motion", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(player, new GridStepMotion(Vector2Int.zero));
            var tick = typeof(PlayerMover).GetMethod("Tick", BindingFlags.NonPublic | BindingFlags.Instance);
            tick.Invoke(player, new object[] { 0f, Vector2Int.left });
            Check(startedCells.Count == 1 && startedCells[0] == Vector2Int.left &&
                enteredCells.Count == 0 && player.IsMoving && player.Cell == Vector2Int.zero,
                "step start announces target without completed entry");
            tick.Invoke(player, new object[] { PlayerMover.DefaultStepSeconds * 0.5f, Vector2Int.zero });
            Check(player.IsMoving && enteredCells.Count == 0 &&
                player.transform.position == world.CellToWorld(new Vector2(-0.5f, 0f)),
                "half step has no completed entry");
            tick.Invoke(player, new object[] { PlayerMover.DefaultStepSeconds * 0.5f, Vector2Int.zero });
            Check(!player.IsMoving && enteredCells.Count == 1 &&
                enteredCells[0] == Vector2Int.left && player.Cell == Vector2Int.left &&
                player.transform.position == world.CellToWorld(Vector2Int.left),
                "cell entry announces after root reaches destination");
            var fiveSteps = new[] { Vector2Int.up, Vector2Int.left, Vector2Int.down,
                Vector2Int.right, Vector2Int.right };
            foreach (var direction in fiveSteps)
            {
                tick.Invoke(player, new object[] { 0f, direction });
                tick.Invoke(player, new object[] { PlayerMover.DefaultStepSeconds, Vector2Int.zero });
            }
            Check(startedCells.Count == 6 && enteredCells.Count == 6 &&
                player.Cell == Vector2Int.zero,
                "five successful steps announce exactly five additional footstep events");
            player.StepStarted -= startListener;
            player.StepCompleted -= completeListener;
            player.transform.position = world.CellToWorld(Vector2Int.zero);
            typeof(PlayerMover).GetField("motion", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(player, new GridStepMotion(Vector2Int.zero));

            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            typeof(GrassField2D).GetMethod("Initialize", flags).Invoke(grassField, null);
            var startRustle = typeof(GrassField2D).GetMethod("OnStepStarted", flags);
            var advanceRustle = typeof(GrassField2D).GetMethod("AdvanceAnimations", flags);
            var idleGrass = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Content/World/grass.png");
            var firstGrass = FindGrass(grassGroup.transform, world, Vector2Int.zero);
            var secondGrass = FindGrass(grassGroup.transform, world, Vector2Int.up);
            Check(grassField.HasGrass(Vector2Int.zero) && grassField.HasGrass(Vector2Int.up) &&
                !grassField.HasGrass(new Vector2Int(1, 1)) && firstGrass.sprite == idleGrass,
                "occupancy query distinguishes grass from stone without names");
            typeof(PlayerGrassOcclusion).GetMethod("Awake", flags).Invoke(occlusion, null);
            var maskStepStart = typeof(PlayerGrassOcclusion).GetMethod("OnStepStarted", flags);
            var maskStepComplete = typeof(PlayerGrassOcclusion).GetMethod("OnStepCompleted", flags);
            Check(occlusion.IsMasked && playerMask.enabled &&
                UnityEngine.Object.FindObjectsByType<SpriteMask>(FindObjectsSortMode.None).Length == 1 &&
                playerMask.isCustomRangeActive &&
                playerMask.frontSortingOrder == 11 && playerMask.backSortingOrder == 9 &&
                playerMask.frontSortingLayerID == artwork.GetComponent<SpriteRenderer>().sortingLayerID &&
                stone.GetComponent<SpriteRenderer>().maskInteraction == SpriteMaskInteraction.None &&
                firstGrass.maskInteraction == SpriteMaskInteraction.None,
                "only player is masked on initial grass cell");
            var maskedRenderer = artwork.GetComponent<SpriteRenderer>();
            var originalPlayerFrame = maskedRenderer.sprite;
            foreach (var clip in new[] { "frontIdle", "backIdle", "leftIdle", "rightIdle",
                "frontWalk", "backWalk", "leftWalk", "rightWalk" })
            {
                var frames = new SerializedObject(visual).FindProperty(clip);
                for (var i = 0; i < frames.arraySize; i++)
                {
                    maskedRenderer.sprite = (Sprite)frames.GetArrayElementAtIndex(i).objectReferenceValue;
                    Check(occlusion.IsMasked && playerMask.enabled &&
                        artwork.localPosition == new Vector3(0f, ProjectBuilder.ArtworkFootOffset, 0f),
                        clip + " mask survives every idle and walk frame");
                }
            }
            maskedRenderer.sprite = originalPlayerFrame;
            startRustle.Invoke(grassField, new object[] { Vector2Int.up });
            maskStepStart.Invoke(occlusion, new object[] { Vector2Int.up });
            Check(occlusion.IsMasked && secondGrass.sprite ==
                rustleFrames.GetArrayElementAtIndex(0).objectReferenceValue &&
                firstGrass.sprite == idleGrass,
                "grass-to-grass step retains foot mask and starts only target rustle");
            advanceRustle.Invoke(grassField, new object[] { 0.09f });
            Check(occlusion.IsMasked && secondGrass.sprite ==
                rustleFrames.GetArrayElementAtIndex(1).objectReferenceValue,
                "foot mask remains on during rustle frame changes");
            maskStepComplete.Invoke(occlusion, new object[] { Vector2Int.up });
            Check(occlusion.IsMasked, "grass arrival remains masked");
            startRustle.Invoke(grassField, new object[] { Vector2Int.zero });
            maskStepStart.Invoke(occlusion, new object[] { Vector2Int.zero });
            Check(occlusion.IsMasked && firstGrass.sprite ==
                rustleFrames.GetArrayElementAtIndex(0).objectReferenceValue &&
                secondGrass.sprite == rustleFrames.GetArrayElementAtIndex(1).objectReferenceValue,
                "consecutive grass step stays masked while prior back continues");
            maskStepComplete.Invoke(occlusion, new object[] { Vector2Int.zero });
            advanceRustle.Invoke(grassField, new object[] { 1f });
            Check(firstGrass.sprite == idleGrass && secondGrass.sprite == idleGrass && occlusion.IsMasked,
                "one-shot rustle returns to idle while foot mask stays on");
            startRustle.Invoke(grassField, new object[] { Vector2Int.zero });
            Check(occlusion.IsMasked && firstGrass.sprite ==
                rustleFrames.GetArrayElementAtIndex(0).objectReferenceValue,
                "re-entering grass restarts one-shot back with foot mask");
            advanceRustle.Invoke(grassField, new object[] { 1f });
            var playerPosition = player.transform.position;
            foreach (var direction in new[] { Vector2Int.up, Vector2Int.down,
                Vector2Int.left, Vector2Int.right })
            {
                startRustle.Invoke(grassField, new object[] { direction });
                maskStepStart.Invoke(occlusion, new object[] { direction });
                Check(occlusion.IsMasked && player.transform.position == playerPosition &&
                    artwork.localPosition == new Vector3(0f, ProjectBuilder.ArtworkFootOffset, 0f),
                    "four-direction rustle keeps mask on and player transform fixed");
                maskStepComplete.Invoke(occlusion, new object[] { direction });
                Check(occlusion.IsMasked, "four-direction grass arrival remains masked");
            }
            foreach (var cell in new[] { new Vector2Int(-1, 0), new Vector2Int(-2, 0),
                new Vector2Int(-3, 0), new Vector2Int(-4, 0), new Vector2Int(-5, 0) })
            {
                startRustle.Invoke(grassField, new object[] { cell });
                maskStepStart.Invoke(occlusion, new object[] { cell });
                Check(occlusion.IsMasked, "five consecutive grass steps never unmask");
                maskStepComplete.Invoke(occlusion, new object[] { cell });
                Check(occlusion.IsMasked, "five consecutive grass arrivals stay masked");
            }
            maskStepStart.Invoke(occlusion, new object[] { new Vector2Int(1, 1) });
            Check(occlusion.IsMasked, "grass-to-non-grass step stays masked until arrival");
            maskStepComplete.Invoke(occlusion, new object[] { new Vector2Int(1, 1) });
            Check(!occlusion.IsMasked && !playerMask.enabled,
                "non-grass destination restores complete player sprite");
            maskStepStart.Invoke(occlusion, new object[] { Vector2Int.zero });
            Check(occlusion.IsMasked, "non-grass-to-grass step masks from departure");
            maskStepComplete.Invoke(occlusion, new object[] { Vector2Int.zero });
            Check(occlusion.IsMasked, "grass destination remains masked");
            advanceRustle.Invoke(grassField, new object[] { 1f });
            Debug.Log("HALKA ver1.7 checks passed.");
        }

        private static SpriteRenderer FindGrass(Transform root, GridWorld2D world, Vector2Int cell)
        {
            foreach (Transform child in root)
                if (world.WorldToCell(child.position) == cell)
                    return child.GetComponentInChildren<SpriteRenderer>();
            throw new InvalidOperationException($"Missing grass cell: {cell}");
        }

        private static void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException("ver1.7 check failed: " + label);
        }
    }
}
