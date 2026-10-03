using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Halka.Game.Core;
using Halka.Game.CameraControl;
using Halka.Game.Interaction;
using Halka.Game.Player;
using Halka.Game.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Halka.Game.Editor
{
    public static class Version20Checks
    {
        private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
        private const string HouseSourceHash =
            "BBC4B8CBD5677B8DE6BF7C19BC2E0624887F9AFEAB70473743D73B7530BC41FD";
        private const string CrowSourceHash =
            "331E55F621A20063C701CF0350FD4A9C83EEA8F4E2991D8CF51C7AA80B1DECBB";
        private const string CrowVoiceHash =
            "A94B18E8F33EAA48194E1115128B348CBAD4CE4CF8378A00BCE02CA146BC50E7";

        [MenuItem("HALKA/Validate ver2.0")]
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/FirstDay.unity");
            var world = UnityEngine.Object.FindFirstObjectByType<GridWorld2D>();
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerMover>();
            var area = UnityEngine.Object.FindFirstObjectByType<HouseArea2D>();
            var autoMode = UnityEngine.Object.FindFirstObjectByType<AutoModeController>();
            var cameraFollow = UnityEngine.Object.FindFirstObjectByType<CameraFollow2D>();
            var occupancy = UnityEngine.Object.FindFirstObjectByType<DynamicGridOccupancy2D>();
            var crow = UnityEngine.Object.FindFirstObjectByType<CrowWander2D>();
            var grass = UnityEngine.Object.FindFirstObjectByType<GrassField2D>();
            var surface = UnityEngine.Object.FindFirstObjectByType<GroundSurfaceField2D>();
            var house = GameObject.Find("House - HarukaChan home");
            var interior = (GameObject)Field(area, "interiorRoot");
            var exterior = (GameObject)Field(area, "exteriorRoot");
            Check(GameVersion.Value == "2.0" && GameVersion.Label == "ver2.0",
                "ver2.0 remains the one game version source");
            Check(GridWorld2D.TilePixels == 32 &&
                Mathf.Approximately(GridWorld2D.TileWorldSize, 0.5f) &&
                player.transform.Find("Player artwork").localPosition == new Vector3(0f, 0.25f),
                "grid and fixed player artwork stay unchanged");
            Check(SourceHash("house_exterior_user_source.png") == HouseSourceHash &&
                SourceHash("crow_user_reference.png") == CrowSourceHash,
                "user-approved house and crow source bytes are preserved");
            CheckSprite("house_exterior.png", 160, 128);
            CheckSprite("house_bed.png", 64, 96);
            CheckSprite("house_wall.png", 32, 32);
            Check(MaxChannel("house_wall.png") <= 80 &&
                AverageBrightness("house_wall.png") < 0.20f &&
                AverageBrightness("house_floor.png") >
                    AverageBrightness("house_wall.png") * 2f,
                "the actual room wall is dark while the original wood floor remains bright");
            Check(File.Exists(Path.Combine(Application.dataPath, "..", "SourceGeneratedArt",
                "v20", "house_bed_simple_source.png")),
                "simplified bed retains its GPT Image source");
            foreach (var direction in new[] { "down", "up", "left", "right" })
            {
                CheckSprite($"crow_idle_{direction}.png", 32, 32);
                CheckSprite($"crow_walk_{direction}_0.png", 32, 32);
                CheckSprite($"crow_walk_{direction}_1.png", 32, 32);
            }
            CheckSprite("crow_grass_mask.png", 32, 32);

            var houseArt = house.transform.Find("House exterior artwork");
            Check(house.transform.position == world.CellToWorld(HouseArea2D.HouseDoorCell) &&
                houseArt.localPosition == Vector3.up * 0.75f &&
                HouseArea2D.HouseDoorCell == new Vector2Int(-7, -4) &&
                HouseArea2D.OutsideEntryCell == new Vector2Int(-7, -5),
                "user house remains centered on the original door cell");
            var houseObstacles = house.GetComponentsInChildren<GridObstacle>();
            Check(houseObstacles.Length == 9 && world.CanEnter(HouseArea2D.HouseDoorCell) &&
                !house.GetComponents<MonoBehaviour>().Any(component => component is IInteractable),
                "only nine footprint cells block; entrance has no interaction");
            foreach (var cell in ProjectBuilder.HouseVisualCells)
                Check(surface.HasGroundOverride(cell) == (cell == HouseArea2D.HouseDoorCell),
                    $"house art has plain ground behind it except the entry at {cell}");
            CheckRoad(world, surface, ProjectBuilder.HouseRoadCells,
                new Vector2Int(0, -1), "existing house road");
            CheckRoad(world, surface, ProjectBuilder.NorthRoadCells,
                new Vector2Int(0, world.MaxCell.y), "north road");
            CheckRoad(world, surface, ProjectBuilder.EastRoadCells,
                new Vector2Int(world.MaxCell.x, -1), "east road");
            CheckRoad(world, surface, ProjectBuilder.SouthRoadCells,
                new Vector2Int(2, world.MinCell.y), "south road");
            CheckRoad(world, surface, ProjectBuilder.WestRoadCells,
                new Vector2Int(world.MinCell.x, 0), "west road");
            Check(ProjectBuilder.RoadCells.Length ==
                    ProjectBuilder.RoadCells.Distinct().Count() &&
                ProjectBuilder.NorthRoadCells[0] == new Vector2Int(0, -1) &&
                ProjectBuilder.EastRoadCells[0] == new Vector2Int(0, -1) &&
                ProjectBuilder.SouthRoadCells[0] == new Vector2Int(0, -1) &&
                ProjectBuilder.WestRoadCells[0] == new Vector2Int(0, -1),
                "all four road branches connect to the central junction");
            Check(surface.Count == ProjectBuilder.DirtCells.Length &&
                ProjectBuilder.DirtCells.Length == ProjectBuilder.RoadCells.Length + 1,
                "only the expanded road and single entry have dirt");

            typeof(GrassField2D).GetMethod("Initialize", Hidden).Invoke(grass, null);
            foreach (var cell in ProjectBuilder.DirtCells)
                Check(!grass.HasGrass(cell), $"dirt excludes grass at {cell}");
            foreach (var cell in ProjectBuilder.HouseVisualCells)
                Check(grass.HasGrass(cell) ==
                    (world.CanEnter(cell) && !surface.HasGroundOverride(cell)),
                    $"visible former underlay returns to ordinary grass at {cell}");
            var obstacles = UnityEngine.Object.FindObjectsByType<GridObstacle>(
                FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(item => item.gameObject.activeInHierarchy).ToArray();
            foreach (var obstacle in obstacles)
                Check(!grass.HasGrass(world.WorldToCell(obstacle.transform.position)),
                    "outdoor obstacle excludes grass");
            Check(ProjectBuilder.StoneCells.Length == 4 &&
                ProjectBuilder.FlowerCells.Length == 4 &&
                ProjectBuilder.TreeCells.Length == 3,
                "four stones, four flowers and three trees are fixed");
            foreach (var cell in ProjectBuilder.StoneCells.Concat(ProjectBuilder.FlowerCells)
                .Concat(ProjectBuilder.TreeCells))
                Check(!world.CanEnter(cell) && !grass.HasGrass(cell) &&
                    !surface.HasGroundOverride(cell),
                    $"world object at {cell} blocks and excludes grass");
            var expectedGrass = 0;
            for (var y = world.MinCell.y; y <= world.MaxCell.y; y++)
            for (var x = world.MinCell.x; x <= world.MaxCell.x; x++)
            {
                var cell = new Vector2Int(x, y);
                if (!world.CanEnter(cell) || surface.HasGroundOverride(cell)) continue;
                expectedGrass++;
                Check(grass.HasGrass(cell), $"eligible field cell has grass at {cell}");
            }
            Check(grass.transform.childCount == expectedGrass,
                "grass count is derived from walkable cells minus surface overrides");
            foreach (var cell in ProjectBuilder.RoadCells)
                Check(!grass.HasGrass(cell), $"expanded road has no grass at {cell}");

            Check(HouseArea2D.InsideMinCell == new Vector2Int(-6, -4) &&
                HouseArea2D.InsideMaxCell == new Vector2Int(6, 4) &&
                HouseArea2D.InsideEntryCell == new Vector2Int(0, -3) &&
                HouseArea2D.InsideExitCell == new Vector2Int(0, -4),
                "interior is thirteen by nine with a bottom passage");
            var backdrop = interior.transform.Find("Interior dark backdrop")
                .GetComponent<SpriteRenderer>();
            var bed = interior.transform.Find("Bed - simple one");
            Check(backdrop.color.r < 0.1f && backdrop.sortingOrder < -9 &&
                interior.transform.Find("House exit passage") != null &&
                interior.transform.Find("Room wall 0,-4") == null &&
                interior.transform.Find("Room floor 0,-4") != null &&
                bed.GetComponentsInChildren<GridObstacle>().Length == 6 &&
                bed.GetComponent<SpriteRenderer>().sprite.rect.size == new Vector2(64f, 96f),
                "dark backdrop, open passage, two by three bed");

            Check(crow.Cell == CrowWander2D.InitialCell &&
                crow.GetComponent<GridObstacle>() == null &&
                crow.GetComponent<ExamineInteractable>() is IInteractable &&
                crow.GetComponent<CrowGrassOcclusion>() != null &&
                CrowGrassOcclusion.HiddenPixels == 4 &&
                Mathf.Approximately(CrowWander2D.StepSeconds, 0.27f),
                "crow retains interaction, gains walking and four-pixel mask");
            var crowRenderer = crow.GetComponent<SpriteRenderer>();
            var crowExamine = crow.GetComponent<ExamineInteractable>();
            var crowAudio = crow.GetComponent<InteractionAudio>();
            var crowClip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                "Assets/Content/Audio/crow_voice.mp3");
            var crowVoicePath = Path.Combine(Application.dataPath, "Content", "Audio",
                "crow_voice.mp3");
            using (var sha = SHA256.Create())
                Check(File.Exists(crowVoicePath) &&
                    BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(crowVoicePath)))
                        .Replace("-", "") == CrowVoiceHash,
                    "crow voice asset is byte identical to the supplied MP3");
            Check(crowClip != null && crowAudio != null &&
                Field(crowAudio, "clip") == crowClip &&
                Field(crowExamine, "interactionAudio") == crowAudio &&
                Field(crowExamine, "availability") == crow &&
                crow.GetComponent<AudioSource>().playOnAwake == false &&
                crow.GetComponent<AudioSource>().spatialBlend == 0f &&
                crowClip.channels == 2 && crowClip.frequency == 44100,
                "original crow MP3 is wired to successful examine only");
            foreach (var direction in new[] { "Down", "Up", "Left", "Right" })
            {
                Check(Field(crow, "idle" + direction) is Sprite,
                    $"crow idle {direction} assigned");
                Check(((Sprite[])Field(crow, "walk" + direction)).Length == 2,
                    $"crow walk {direction} has two frames");
            }
            var right = File.ReadAllBytes(WorldPath("crow_idle_right.png"));
            var left = File.ReadAllBytes(WorldPath("crow_idle_left.png"));
            Check(right.Length > 0 && left.Length > 0,
                "right user source and left mirror assets exist");
            Check(crowRenderer.sprite != null &&
                (string)Field(crowExamine, "message") == "カァ。",
                "crow keeps common examine message");

            typeof(PlayerMover).GetMethod("Awake", Hidden).Invoke(player, null);
            typeof(CrowWander2D).GetMethod("Awake", Hidden).Invoke(crow, null);
            player.TeleportTo(CrowWander2D.InitialCell + Vector2Int.left);
            Check(!player.TryStep(Vector2Int.right) &&
                !occupancy.CanPlayerEnter(crow.Cell) &&
                !occupancy.CanCrowEnter(player.Cell),
                "player and crow reserve each other's occupied cells");
            var playRequests = crowAudio.PlayRequestCount;
            crowExamine.Interact();
            Check(crowAudio.PlayRequestCount == playRequests + 1,
                "one successful crow examine requests one voice playback");
            player.SetFacing(FacingDirection.Up);
            crowExamine.Interact();
            Check(crowAudio.PlayRequestCount == playRequests + 1,
                "wrong facing does not request crow voice");
            player.TeleportTo(Vector2Int.zero);
            crowExamine.Interact();
            Check(crowAudio.PlayRequestCount == playRequests + 1,
                "distant examine does not request crow voice");
            player.TeleportTo(CrowWander2D.InitialCell + Vector2Int.left);
            Check(player.TryStep(Vector2Int.left), "player can start a step beside crow");
            crowExamine.Interact();
            Check(crowAudio.PlayRequestCount == playRequests + 1,
                "moving player does not request crow voice");
            player.CancelStep();
            player.TeleportTo(Vector2Int.zero);
            typeof(GrassField2D).GetMethod("Initialize", Hidden).Invoke(grass, null);
            UnityEngine.Random.InitState(2020);
            var baseTime = Time.time;
            var observedStep = false;
            var activeRustles = (IDictionary)Field(grass, "active");
            for (var i = 1; i <= 20 && !observedStep; i++)
            {
                var start = baseTime + 5f * i;
                crow.Tick(start);
                if (!crow.IsMoving) continue;
                observedStep = true;
                Check(crow.StepToCell != crow.StepFromCell &&
                    !crow.GetComponent<BoxCollider2D>().enabled &&
                    !occupancy.CanPlayerEnter(crow.StepFromCell) &&
                    !occupancy.CanPlayerEnter(crow.StepToCell),
                    "crow begins a reserved walking step with pointer disabled");
                var crowDestinationGrass = grass.HasGrass(crow.StepToCell);
                Check(activeRustles.Contains(crow.StepToCell) == crowDestinationGrass &&
                    activeRustles.Count == (crowDestinationGrass ? 1 : 0),
                    "crow successful step rustles its destination only when grass exists");
                player.TeleportTo(crow.StepFromCell + Vector2Int.left);
                player.SetFacing(FacingDirection.Right);
                crowExamine.Interact();
                Check(crowAudio.PlayRequestCount == playRequests + 1,
                    "moving crow cannot be examined or voiced");
                player.TeleportTo(Vector2Int.zero);
                crow.Tick(start + CrowWander2D.StepSeconds * 0.5f);
                Check(crow.IsMoving && crow.StepProgressNormalized > 0f &&
                    crow.StepProgressNormalized < 1f &&
                    crow.transform.position != world.CellToWorld(crow.StepFromCell) &&
                    crow.transform.position != world.CellToWorld(crow.StepToCell),
                    "crow interpolates through the cell instead of teleporting");
                crow.Tick(start + CrowWander2D.StepSeconds + 0.01f);
                Check(!crow.IsMoving && crow.GetComponent<BoxCollider2D>().enabled &&
                    crowRenderer.sprite == Field(crow, "idle" + crow.Facing) as Sprite,
                    "crow ends at an idle pose and re-enables pointer interaction");
            }
            Check(observedStep, "crow eventually walks after waiting");
            Check(crow.Cell.x >= CrowWander2D.RangeMin.x &&
                crow.Cell.x <= CrowWander2D.RangeMax.x &&
                crow.Cell.y >= CrowWander2D.RangeMin.y &&
                crow.Cell.y <= CrowWander2D.RangeMax.y,
                "crow remains near the tree");
            var crowMask = crow.GetComponent<CrowGrassOcclusion>();
            crowMask.RefreshMask();
            Check(crowMask.IsMasked == grass.HasGrass(crow.Cell),
                "crow grass mask follows its visual cell");
            var grassCell = new Vector2Int(2, 0);
            var dirtCell = new Vector2Int(2, -1);
            Check(grass.HasGrass(grassCell) && !grass.HasGrass(dirtCell) &&
                grass.HasGrass(CrowGrassOcclusion.VisualCell(grassCell, dirtCell, true, 0.49f)) &&
                !grass.HasGrass(CrowGrassOcclusion.VisualCell(grassCell, dirtCell, true, 0.5f)) &&
                !grass.HasGrass(CrowGrassOcclusion.VisualCell(grassCell, dirtCell, true, 0.51f)) &&
                !grass.HasGrass(CrowGrassOcclusion.VisualCell(dirtCell, grassCell, true, 0.49f)) &&
                grass.HasGrass(CrowGrassOcclusion.VisualCell(dirtCell, grassCell, true, 0.5f)) &&
                grass.HasGrass(CrowGrassOcclusion.VisualCell(dirtCell, grassCell, true, 0.51f)),
                "crow mask uses source before halfway and destination from halfway");
            var rustlesBeforeDirt = activeRustles.Count;
            Check(!grass.RustleAt(dirtCell) && activeRustles.Count == rustlesBeforeDirt &&
                grass.RustleAt(grassCell) && activeRustles.Contains(grassCell),
                "grass rustles on destination only, never on dirt or failed step");

            typeof(HouseArea2D).GetMethod("Awake", Hidden).Invoke(area, null);
            typeof(HouseArea2D).GetMethod("OnEnable", Hidden).Invoke(area, null);
            player.TeleportTo(HouseArea2D.OutsideEntryCell);
            Check(player.TryStep(Vector2Int.up), "player may walk into the house entrance");
            typeof(CameraFollow2D).GetField("velocity", Hidden)
                .SetValue(cameraFollow, new Vector3(1f, 1f));
            StepPlayer(player, PlayerMover.DefaultStepSeconds, Vector2Int.zero);
            Check(area.IsInside && interior.activeSelf && !exterior.activeSelf &&
                player.Cell == HouseArea2D.InsideEntryCell,
                "step completion enters expanded room");
            Check(cameraFollow.transform.position == cameraFollow.TargetPosition &&
                (Vector3)Field(cameraFollow, "velocity") == Vector3.zero,
                "entry cuts immediately to indoor camera without old smoothing velocity");
            Check(player.TryStep(Vector2Int.down), "inside passage is walkable");
            typeof(CameraFollow2D).GetField("velocity", Hidden)
                .SetValue(cameraFollow, new Vector3(-1f, -1f));
            StepPlayer(player, PlayerMover.DefaultStepSeconds, Vector2Int.zero);
            Check(!area.IsInside && exterior.activeSelf && !interior.activeSelf &&
                player.Cell == HouseArea2D.OutsideEntryCell &&
                player.Facing == FacingDirection.Down,
                "step completion exits without an interaction");
            Check(cameraFollow.transform.position == cameraFollow.TargetPosition &&
                (Vector3)Field(cameraFollow, "velocity") == Vector3.zero,
                "exit cuts immediately to outdoor camera without old smoothing velocity");
            var playerMask = player.GetComponent<PlayerGrassOcclusion>();
            typeof(PlayerGrassOcclusion).GetMethod("Awake", Hidden).Invoke(playerMask, null);
            CheckPlayerMaskStep(world, player, playerMask, grass, grassCell, dirtCell);
            CheckPlayerMaskStep(world, player, playerMask, grass, dirtCell, grassCell);
            player.TeleportTo(HouseArea2D.OutsideEntryCell);
            Check(autoMode.CanAutoEnter(HouseArea2D.HouseDoorCell) == false &&
                autoMode.CanAutoEnter(crow.Cell) == false &&
                AutoModeController.IdleSeconds == 60f,
                "AUTO avoids house transition and dynamic crow occupancy");
            Check(player.GetComponent<PlayerGrassOcclusion>() != null &&
                player.GetComponent<PlayerFootstepAudio>() != null &&
                GameObject.Find("Stone - first world object")
                    .GetComponent<ExamineInteractable>() != null &&
                GameObject.Find("Flower - first bloom")
                    .GetComponent<ExamineInteractable>() != null &&
                GameObject.Find("Tree - first tree")
                    .GetComponent<RootedWorldObjectDepth2D>() != null,
                "player mask, footstep and original interactions stay present");
            Debug.Log("HALKA ver2.0 finish checks passed.");
        }

        private static void StepPlayer(PlayerMover player, float seconds, Vector2Int direction) =>
            typeof(PlayerMover).GetMethod("Tick", Hidden)
                .Invoke(player, new object[] { seconds, direction });

        private static void CheckPlayerMaskStep(GridWorld2D world, PlayerMover player,
            PlayerGrassOcclusion mask, GrassField2D grass, Vector2Int source,
            Vector2Int destination)
        {
            var refresh = typeof(PlayerGrassOcclusion).GetMethod("RefreshMask", Hidden);
            var motion = new GridStepMotion(source);
            typeof(PlayerMover).GetField("motion", Hidden).SetValue(player, motion);
            player.transform.position = world.CellToWorld(source);
            refresh.Invoke(mask, null);
            Check(mask.IsMasked == grass.HasGrass(source),
                "stationary Player mask follows the current surface");
            Check(motion.TryBegin(destination - source, world.CanEnter,
                PlayerMover.DefaultStepSeconds), "Player mask road boundary step starts");
            motion.Advance(PlayerMover.DefaultStepSeconds * 0.49f);
            refresh.Invoke(mask, null);
            Check(mask.IsMasked == grass.HasGrass(source),
                "Player mask retains the source surface at progress 0.49");
            motion.Advance(PlayerMover.DefaultStepSeconds * 0.02f);
            refresh.Invoke(mask, null);
            Check(mask.IsMasked == grass.HasGrass(destination),
                "Player mask uses the destination surface at progress 0.51");
        }

        private static void CheckRoad(GridWorld2D world, GroundSurfaceField2D surface,
            Vector2Int[] cells, Vector2Int endpoint, string label)
        {
            Check(cells.Length > 1 && cells[cells.Length - 1] == endpoint,
                label + " reaches its field endpoint");
            for (var i = 0; i < cells.Length; i++)
            {
                Check(world.CanEnter(cells[i]) && surface.HasGroundOverride(cells[i]),
                    label + " is walkable dirt at " + cells[i]);
                if (i == 0) continue;
                var delta = cells[i] - cells[i - 1];
                Check(Mathf.Abs(delta.x) + Mathf.Abs(delta.y) == 1,
                    label + " stays connected by one-cell steps");
            }
        }

        private static Color32[] ImagePixels(string fileName)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Check(ImageConversion.LoadImage(texture, File.ReadAllBytes(WorldPath(fileName))),
                    "PNG decoded: " + fileName);
                return texture.GetPixels32();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static int MaxChannel(string fileName) => ImagePixels(fileName)
            .Max(pixel => Math.Max(pixel.r, Math.Max(pixel.g, pixel.b)));

        private static float AverageBrightness(string fileName)
        {
            var pixels = ImagePixels(fileName);
            return pixels.Average(pixel => (pixel.r + pixel.g + pixel.b) / (3f * 255f));
        }

        private static object Field(object target, string name) =>
            target.GetType().GetField(name, Hidden).GetValue(target);

        private static string WorldPath(string fileName) =>
            Path.Combine(Application.dataPath, "Content", "World", fileName);

        private static string SourceHash(string fileName)
        {
            var path = Path.Combine(Application.dataPath, "..", "SourceGeneratedArt",
                "v20", fileName);
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "");
        }

        private static void CheckSprite(string fileName, int width, int height)
        {
            var path = "Assets/Content/World/" + fileName;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            Check(sprite != null && sprite.rect.size == new Vector2(width, height) &&
                sprite.texture.width == width && sprite.texture.height == height &&
                sprite.pivot == new Vector2(width * 0.5f, height * 0.5f) &&
                importer.textureType == TextureImporterType.Sprite &&
                importer.spriteImportMode == SpriteImportMode.Single &&
                importer.filterMode == FilterMode.Point && !importer.mipmapEnabled &&
                importer.textureCompression == TextureImporterCompression.Uncompressed &&
                importer.npotScale == TextureImporterNPOTScale.None &&
                Mathf.Approximately(importer.spritePixelsPerUnit, 64f) &&
                settings.spriteAlignment == (int)SpriteAlignment.Center,
                "sharp centered sprite: " + fileName);
        }

        private static void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException("ver2.0 check failed: " + label);
        }
    }
}
