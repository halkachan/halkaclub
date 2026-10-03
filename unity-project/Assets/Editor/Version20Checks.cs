using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Halka.Game.Core;
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

        [MenuItem("HALKA/Validate ver2.0")]
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/FirstDay.unity");
            var world = UnityEngine.Object.FindFirstObjectByType<GridWorld2D>();
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerMover>();
            var area = UnityEngine.Object.FindFirstObjectByType<HouseArea2D>();
            var autoMode = UnityEngine.Object.FindFirstObjectByType<AutoModeController>();
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
            for (var y = -4; y <= -1; y++)
            for (var x = -9; x <= -5; x++)
            {
                var cell = new Vector2Int(x, y);
                Check(surface.HasGroundOverride(cell),
                    $"house underlay dirt at {cell}");
            }
            foreach (var cell in ProjectBuilder.RoadCells)
                Check(surface.HasGroundOverride(cell) && world.CanEnter(cell),
                    $"road is walkable dirt at {cell}");
            Check(surface.Count == ProjectBuilder.DirtCells.Length &&
                ProjectBuilder.DirtCells.Length == 33,
                "twenty underlay cells and thirteen road cells use one surface field");

            typeof(GrassField2D).GetMethod("Initialize", Hidden).Invoke(grass, null);
            foreach (var cell in ProjectBuilder.DirtCells)
                Check(!grass.HasGrass(cell), $"dirt excludes grass at {cell}");
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
                Check(!world.CanEnter(cell) && !grass.HasGrass(cell),
                    $"world object at {cell} blocks and excludes grass");
            Check(grass.transform.childCount == 229,
                "grass count matches obstacles and surface overrides");

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
                (string)Field(crow.GetComponent<ExamineInteractable>(), "message") == "カァ。",
                "crow keeps common examine message");

            typeof(PlayerMover).GetMethod("Awake", Hidden).Invoke(player, null);
            typeof(CrowWander2D).GetMethod("Awake", Hidden).Invoke(crow, null);
            player.TeleportTo(CrowWander2D.InitialCell + Vector2Int.left);
            Check(!player.TryStep(Vector2Int.right) &&
                !occupancy.CanPlayerEnter(crow.Cell) &&
                !occupancy.CanCrowEnter(player.Cell),
                "player and crow reserve each other's occupied cells");
            player.TeleportTo(Vector2Int.zero);
            UnityEngine.Random.InitState(2020);
            var baseTime = Time.time;
            var observedStep = false;
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

            typeof(HouseArea2D).GetMethod("Awake", Hidden).Invoke(area, null);
            typeof(HouseArea2D).GetMethod("OnEnable", Hidden).Invoke(area, null);
            player.TeleportTo(HouseArea2D.OutsideEntryCell);
            Check(player.TryStep(Vector2Int.up), "player may walk into the house entrance");
            StepPlayer(player, PlayerMover.DefaultStepSeconds, Vector2Int.zero);
            Check(area.IsInside && interior.activeSelf && !exterior.activeSelf &&
                player.Cell == HouseArea2D.InsideEntryCell,
                "step completion enters expanded room");
            Check(player.TryStep(Vector2Int.down), "inside passage is walkable");
            StepPlayer(player, PlayerMover.DefaultStepSeconds, Vector2Int.zero);
            Check(!area.IsInside && exterior.activeSelf && !interior.activeSelf &&
                player.Cell == HouseArea2D.OutsideEntryCell &&
                player.Facing == FacingDirection.Down,
                "step completion exits without an interaction");
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
