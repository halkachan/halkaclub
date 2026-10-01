using System;
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
    public static class Version15Checks
    {
        [MenuItem("HALKA/Validate ver1.5")]
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/FirstDay.unity");
            var world = UnityEngine.Object.FindFirstObjectByType<GridWorld2D>();
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerMover>();
            var visual = UnityEngine.Object.FindFirstObjectByType<CharacterVisual>();
            var hud = UnityEngine.Object.FindFirstObjectByType<GameHud>();
            var router = UnityEngine.Object.FindFirstObjectByType<InteractionRouter>();
            var worldCamera = UnityEngine.Object.FindFirstObjectByType<Camera>();
            var stone = UnityEngine.Object.FindFirstObjectByType<GridObstacle>();
            var examine = stone.GetComponent<ExamineInteractable>();
            var artwork = player.GetComponentInChildren<SpriteRenderer>().transform;
            Check(GameVersion.Value == "1.5", "version source");
            Check(GridWorld2D.TilePixels == 32 && Mathf.Approximately(GridWorld2D.TileWorldSize, 0.5f) &&
                Mathf.Approximately(world.CellSize, GridWorld2D.TileWorldSize), "32-pixel grid scale");
            Check(world.MinCell == new Vector2Int(-10, -6) && world.MaxCell == new Vector2Int(10, 6),
                "physical field size preserved");
            Check(world.CellToWorld(Vector2Int.right) == new Vector3(0.5f, 0f, 0f) &&
                world.WorldToCell(new Vector3(0.5f, 0f, 0f)) == Vector2Int.right,
                "integer grid world conversion");
            Check(artwork.localPosition == new Vector3(0f, ProjectBuilder.ArtworkFootOffset, 0f) &&
                Mathf.Approximately(ProjectBuilder.PlayerVisualOffsetPixelsY, -3f) &&
                Mathf.Approximately(ProjectBuilder.GrassVisualOffsetPixelsY, -3f) &&
                artwork.GetComponents<Component>().Length == 2,
                "fixed artwork foot anchor without runtime offset component");
            Check(UnityEngine.Object.FindObjectsByType<GridObstacle>(FindObjectsSortMode.None).Length == 1,
                "one world obstacle");
            var stoneSprite = stone.GetComponent<SpriteRenderer>().sprite;
            var collider = stone.GetComponent<BoxCollider2D>();
            Check(stoneSprite.texture.width == 32 && stoneSprite.texture.height == 32 &&
                Mathf.Approximately(stoneSprite.pixelsPerUnit, 64f) && stone.transform.localScale == Vector3.one,
                "supplied stone occupies one 32-pixel cell");
            Check(collider.size == new Vector2(28f / 64f, 20f / 64f) &&
                stone.transform.position == world.CellToWorld(new Vector2Int(1, 1)) &&
                !world.CanEnter(new Vector2Int(1, 1)), "stone collider and blocking cell");
            Check(Physics2D.OverlapPoint(stone.transform.position) == collider &&
                router.IsInteractableAt(worldCamera.WorldToScreenPoint(stone.transform.position)),
                "stone world pointer routes to shared interaction");
            Check(world.CanEnter(new Vector2Int(0, 1)) && !world.CanEnter(new Vector2Int(11, 0)) &&
                !world.CanEnter(new Vector2Int(0, -7)), "adjacent and field bounds");
            var grid = GameObject.Find("FirstDay - 32px cell boundaries").GetComponent<SpriteRenderer>();
            Check(grid.sprite.texture.width == 21 * 32 && grid.sprite.texture.height == 13 * 32 &&
                Mathf.Approximately(grid.sprite.bounds.size.x, 10.5f) &&
                Mathf.Approximately(grid.sprite.bounds.size.y, 6.5f) &&
                grid.transform.position == Vector3.zero && grid.sprite.texture.filterMode == FilterMode.Point,
                "world-aligned sharp 32-pixel grid overlay");
            var grassGroup = GameObject.Find("Grass decorations");
            Check(grassGroup != null && grassGroup.transform.childCount == 272,
                "all 272 walkable cells have grass prefab instances");
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
                grassSettings.spriteAlignment == (int)SpriteAlignment.Custom &&
                grassSettings.spritePivot.y < 0.5f &&
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
                    renderer.transform.localPosition == new Vector3(0f,
                        ProjectBuilder.GrassVisualOffsetPixelsY / 64f, 0f) &&
                    grass.position == world.CellToWorld(cell), "grass prefab, sprite, order and cell alignment");
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
                actualGrassCells.Count == 21 * 13 - 1,
                "start cell covered and stone cell excluded");
            var frontRoot = GameObject.Find("Grass Front Overlay").transform;
            var frontOverlay = frontRoot.GetComponentInChildren<SpriteRenderer>();
            Check(frontOverlay != null && frontOverlay.sortingOrder == 20 &&
                frontRoot.parent == null &&
                frontOverlay.transform.parent == frontRoot &&
                frontOverlay.transform.localPosition == new Vector3(0f,
                    ProjectBuilder.GrassVisualOffsetPixelsY / 64f, 0f) &&
                frontRoot.position == world.CellToWorld(Vector2Int.zero) &&
                frontOverlay.GetComponent<Collider2D>() == null &&
                UnityEngine.Object.FindObjectsByType<GrassField2D>(FindObjectsSortMode.None).Length == 1 &&
                UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)
                    .Count(renderer => renderer.sortingOrder == 20) == 1,
                "one world-space foreground overlay without collider");
            Check(GameObject.Find("FirstDay - small ground").GetComponent<SpriteRenderer>().sortingOrder == -10 &&
                grid.sortingOrder == -9 && stone.GetComponent<SpriteRenderer>().sortingOrder == 2 &&
                artwork.GetComponent<SpriteRenderer>().sortingOrder == 10,
                "ground grid grass back stone player grass front render order");
            var rustle = new SerializedObject(grassField);
            var rustleFrames = rustle.FindProperty("rustleFrames");
            var rustleFrontFrames = rustle.FindProperty("rustleFrontFrames");
            var frameTimes = rustle.FindProperty("frameSeconds");
            var idleFront = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Content/World/grass_front.png");
            Check(rustleFrames.arraySize == 5 && rustleFrontFrames.arraySize == 5 &&
                frameTimes.arraySize == 5 &&
                rustle.FindProperty("idleSprite").objectReferenceValue ==
                    AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Content/World/grass.png") &&
                rustle.FindProperty("frontOverlay").objectReferenceValue == frontOverlay &&
                rustle.FindProperty("idleFrontSprite").objectReferenceValue == idleFront &&
                idleFront.texture.width == 32 && idleFront.texture.height == 32 &&
                Mathf.Approximately(idleFront.pixelsPerUnit, 64f) &&
                idleFront.pivot == AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Content/World/grass.png").pivot,
                "five paired rustle frames and aligned idle grass sprites");
            var frontImporter = (TextureImporter)AssetImporter.GetAtPath("Assets/Content/World/grass_front.png");
            var frontSettings = new TextureImporterSettings();
            frontImporter.ReadTextureSettings(frontSettings);
            Check(frontImporter.textureType == TextureImporterType.Sprite &&
                frontImporter.spriteImportMode == SpriteImportMode.Single &&
                frontImporter.filterMode == FilterMode.Point && !frontImporter.mipmapEnabled &&
                frontImporter.textureCompression == TextureImporterCompression.Uncompressed &&
                frontImporter.npotScale == TextureImporterNPOTScale.None &&
                frontSettings.spriteAlignment == grassSettings.spriteAlignment &&
                frontSettings.spritePivot == grassSettings.spritePivot,
                "front sprite pixel import");
            var expectedMilliseconds = new[] { 90, 90, 90, 90, 420 };
            for (var i = 0; i < rustleFrames.arraySize; i++)
            {
                var sprite = (Sprite)rustleFrames.GetArrayElementAtIndex(i).objectReferenceValue;
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sprite));
                var frontSprite = (Sprite)rustleFrontFrames.GetArrayElementAtIndex(i).objectReferenceValue;
                var pairedImporter = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(frontSprite));
                Check(sprite.texture.width == 32 && sprite.texture.height == 32 &&
                    Mathf.Approximately(sprite.pixelsPerUnit, 64f) &&
                    importer.textureType == TextureImporterType.Sprite &&
                    importer.spriteImportMode == SpriteImportMode.Single &&
                    importer.filterMode == FilterMode.Point && !importer.mipmapEnabled &&
                    importer.textureCompression == TextureImporterCompression.Uncompressed &&
                    importer.npotScale == TextureImporterNPOTScale.None &&
                    Mathf.Approximately(frameTimes.GetArrayElementAtIndex(i).floatValue,
                        expectedMilliseconds[i] / 1000f) &&
                    frontSprite.texture.width == 32 && frontSprite.texture.height == 32 &&
                    Mathf.Approximately(frontSprite.pixelsPerUnit, sprite.pixelsPerUnit) &&
                    sprite.pivot == idleFront.pivot && frontSprite.pivot == sprite.pivot &&
                    pairedImporter.textureType == TextureImporterType.Sprite &&
                    pairedImporter.spriteImportMode == SpriteImportMode.Single &&
                    pairedImporter.filterMode == FilterMode.Point && !pairedImporter.mipmapEnabled &&
                    pairedImporter.textureCompression == TextureImporterCompression.Uncompressed &&
                    pairedImporter.npotScale == TextureImporterNPOTScale.None,
                    "paired rustle frame import and GIF timing");
            }
            foreach (var clip in new[] { "frontWalk", "backWalk", "leftWalk", "rightWalk" })
            {
                var property = new SerializedObject(visual).FindProperty(clip);
                Check(property.arraySize == 4, clip + " four frames");
                for (var i = 0; i < property.arraySize; i++)
                {
                    var sprite = (Sprite)property.GetArrayElementAtIndex(i).objectReferenceValue;
                    Check(sprite != null && sprite.texture.width == 64 && sprite.texture.height == 64 &&
                        sprite.texture.filterMode == FilterMode.Point &&
                        Mathf.Approximately(sprite.pixelsPerUnit, 64f), clip + " sprite import");
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
                new SerializedObject(hud).FindProperty("messageFont").objectReferenceValue != null,
                "generic stone examine and Japanese font");
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
            examine.Interact();
            var activeMessage = (TimedMessage)messageField.GetValue(hud);
            Check(activeMessage.TextAt(Time.unscaledTime) == "いし。", "facing stone displays exact message");
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
            var tap = new PointerTap();
            Check(!tap.TryBegin(1, Vector2.zero, true, 0f), "D-pad blocks world tap");
            Check(tap.TryBegin(2, Vector2.zero, false, 0f) &&
                !tap.TryBegin(3, Vector2.zero, false, 0f) &&
                !tap.End(3, Vector2.zero, false, 0.1f, 24f, 0.4f) &&
                tap.End(2, Vector2.zero, false, 0.1f, 24f, 0.4f),
                "pointer ID prevents second finger interference");
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
            player.StepStarted -= startListener;
            player.StepCompleted -= completeListener;
            player.transform.position = world.CellToWorld(Vector2Int.zero);
            typeof(PlayerMover).GetField("motion", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(player, new GridStepMotion(Vector2Int.zero));

            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            typeof(GrassField2D).GetMethod("Initialize", flags).Invoke(grassField, null);
            var startRustle = typeof(GrassField2D).GetMethod("OnStepStarted", flags);
            var completeStep = typeof(GrassField2D).GetMethod("OnStepCompleted", flags);
            var advanceRustle = typeof(GrassField2D).GetMethod("AdvanceAnimations", flags);
            var idleGrass = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Content/World/grass.png");
            var firstGrass = FindGrass(grassGroup.transform, world, Vector2Int.zero);
            var secondGrass = FindGrass(grassGroup.transform, world, Vector2Int.up);
            Check(frontOverlay.enabled && frontRoot.position == world.CellToWorld(Vector2Int.zero) &&
                frontOverlay.sprite == idleFront && firstGrass.sprite == idleGrass,
                "initial grass is behind player with only current cell foot overlay");
            startRustle.Invoke(grassField, new object[] { Vector2Int.up });
            Check(!frontOverlay.enabled && secondGrass.sprite ==
                rustleFrames.GetArrayElementAtIndex(0).objectReferenceValue &&
                firstGrass.sprite == idleGrass,
                "departure hides front while only target back begins rustling");
            advanceRustle.Invoke(grassField, new object[] { 0.09f });
            Check(!frontOverlay.enabled && secondGrass.sprite ==
                rustleFrames.GetArrayElementAtIndex(1).objectReferenceValue,
                "front remains hidden during step even as back animates");
            completeStep.Invoke(grassField, new object[] { Vector2Int.up });
            Check(frontOverlay.enabled && frontRoot.position == world.CellToWorld(Vector2Int.up) &&
                frontOverlay.sprite == rustleFrontFrames.GetArrayElementAtIndex(1).objectReferenceValue,
                "arrival shows matching current back frame at destination");
            startRustle.Invoke(grassField, new object[] { Vector2Int.zero });
            Check(!frontOverlay.enabled && firstGrass.sprite ==
                rustleFrames.GetArrayElementAtIndex(0).objectReferenceValue &&
                secondGrass.sprite == rustleFrames.GetArrayElementAtIndex(1).objectReferenceValue,
                "next departure clears previous front while old back continues");
            completeStep.Invoke(grassField, new object[] { Vector2Int.zero });
            Check(frontOverlay.enabled && frontRoot.position == world.CellToWorld(Vector2Int.zero) &&
                frontOverlay.sprite == rustleFrontFrames.GetArrayElementAtIndex(0).objectReferenceValue,
                "next arrival shows only destination front");
            advanceRustle.Invoke(grassField, new object[] { 1f });
            Check(firstGrass.sprite == idleGrass && secondGrass.sprite == idleGrass &&
                frontOverlay.enabled && frontOverlay.sprite == idleFront,
                "one-shot animations return to original grass while occupied front remains");
            startRustle.Invoke(grassField, new object[] { Vector2Int.zero });
            Check(!frontOverlay.enabled && firstGrass.sprite ==
                rustleFrames.GetArrayElementAtIndex(0).objectReferenceValue,
                "re-entering grass restarts one-shot back without early front");
            completeStep.Invoke(grassField, new object[] { Vector2Int.zero });
            Check(frontOverlay.enabled &&
                frontOverlay.sprite == rustleFrontFrames.GetArrayElementAtIndex(0).objectReferenceValue,
                "re-entering grass shows matched front after arrival");
            advanceRustle.Invoke(grassField, new object[] { 1f });
            var playerPosition = player.transform.position;
            foreach (var direction in new[] { Vector2Int.up, Vector2Int.down,
                Vector2Int.left, Vector2Int.right })
            {
                startRustle.Invoke(grassField, new object[] { direction });
                Check(!frontOverlay.enabled, "four-direction entry has no in-flight foreground");
                completeStep.Invoke(grassField, new object[] { direction });
                Check(frontOverlay.enabled && frontRoot.position == world.CellToWorld(direction) &&
                    frontOverlay.sprite == rustleFrontFrames.GetArrayElementAtIndex(0).objectReferenceValue &&
                    player.transform.position == playerPosition &&
                    artwork.localPosition == new Vector3(0f, ProjectBuilder.ArtworkFootOffset, 0f),
                    "four-direction entry keeps overlay cell-aligned and player transform fixed");
            }
            foreach (var cell in new[] { new Vector2Int(-1, 0), new Vector2Int(-2, 0),
                new Vector2Int(-3, 0), new Vector2Int(-4, 0), new Vector2Int(-5, 0) })
            {
                startRustle.Invoke(grassField, new object[] { cell });
                Check(!frontOverlay.enabled, "consecutive departure removes last front");
                completeStep.Invoke(grassField, new object[] { cell });
                Check(frontOverlay.enabled && frontRoot.position == world.CellToWorld(cell) &&
                    UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)
                        .Count(renderer => renderer.sortingOrder == 20) == 1,
                    "five consecutive grass cells keep one current front overlay");
            }
            startRustle.Invoke(grassField, new object[] { new Vector2Int(1, 1) });
            completeStep.Invoke(grassField, new object[] { new Vector2Int(1, 1) });
            Check(!frontOverlay.enabled, "stone cell has no grass front");
            advanceRustle.Invoke(grassField, new object[] { 1f });
            Debug.Log("HALKA ver1.5 checks passed.");
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
            if (!condition) throw new InvalidOperationException("ver1.5 check failed: " + label);
        }
    }
}
