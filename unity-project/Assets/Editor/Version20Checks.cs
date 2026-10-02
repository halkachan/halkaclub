using System;
using System.Collections.Generic;
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
    public static class Version20Checks
    {
        private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;

        [MenuItem("HALKA/Validate ver2.0")]
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/FirstDay.unity");
            var world = UnityEngine.Object.FindFirstObjectByType<GridWorld2D>();
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerMover>();
            var input = UnityEngine.Object.FindFirstObjectByType<GameInput>();
            var router = UnityEngine.Object.FindFirstObjectByType<InteractionRouter>();
            var hud = UnityEngine.Object.FindFirstObjectByType<GameHud>();
            var area = UnityEngine.Object.FindFirstObjectByType<HouseArea2D>();
            var auto = UnityEngine.Object.FindFirstObjectByType<AutoModeController>();
            var grass = UnityEngine.Object.FindFirstObjectByType<GrassField2D>();
            var mask = player.GetComponent<PlayerGrassOcclusion>();
            var camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
            var house = GameObject.Find("House - HarukaChan home");
            var crow = GameObject.Find("Crow - first neighbor");
            var crowWander = crow.GetComponent<CrowWander2D>();
            var exteriorRoot = (GameObject)Field(area, "exteriorRoot");
            var interiorRoot = (GameObject)Field(area, "interiorRoot");
            var insideDoor = interiorRoot.transform.Find("House exit door");
            Check(GameVersion.Value == "2.0" &&
                GameVersion.Label == "ver2.0", "one version source and HUD label");
            Check(GridWorld2D.TilePixels == 32 &&
                Mathf.Approximately(GridWorld2D.TileWorldSize, 0.5f) &&
                player.transform.Find("Player artwork").localPosition == new Vector3(0f, 0.25f, 0f),
                "existing grid and fixed player artwork placement");
            Check(world.MinCell == new Vector2Int(-10, -6) &&
                world.MaxCell == new Vector2Int(10, 6) &&
                world.CellToWorld(new Vector2Int(0, -1)) == new Vector3(0f, -0.5f),
                "outdoor grid and dirt cell preserved");

            CheckSprite("house_exterior.png", 160, 128);
            CheckSprite("house_floor.png", 32, 32);
            CheckSprite("house_wall.png", 32, 32);
            CheckSprite("house_door_inside.png", 32, 32);
            CheckSprite("house_bed.png", 64, 64);
            CheckSprite("crow_idle.png", 32, 32);
            CheckSprite("crow_hop_1.png", 32, 32);
            CheckSprite("crow_hop_2.png", 32, 32);

            var houseArt = house.transform.Find("House exterior artwork");
            var houseSprite = houseArt.GetComponent<SpriteRenderer>().sprite;
            Check(HouseArea2D.HouseDoorCell == new Vector2Int(-7, -4) &&
                HouseArea2D.OutsideEntryCell == new Vector2Int(-7, -5) &&
                house.transform.position == world.CellToWorld(HouseArea2D.HouseDoorCell) &&
                houseArt.localPosition == Vector3.up * 0.75f &&
                houseArt.position == world.CellToWorld(HouseArea2D.HouseDoorCell) +
                    Vector3.up * 0.75f &&
                Mathf.Approximately(houseSprite.bounds.size.x, 2.5f) &&
                Mathf.Approximately(houseSprite.bounds.size.y, 2f),
                "house occupies exactly five by four visual cells above the door");
            var footprints = house.GetComponentsInChildren<GridObstacle>();
            Check(footprints.Length == 10 &&
                house.GetComponent<GridObstacle>() == null &&
                houseArt.GetComponent<Collider2D>() == null,
                "only the house lower two rows block movement");
            for (var y = -4; y <= -3; y++)
            for (var x = -9; x <= -5; x++)
            {
                var cell = new Vector2Int(x, y);
                Check(footprints.Count(obstacle =>
                    world.WorldToCell(obstacle.transform.position) == cell) == 1 &&
                    !world.CanEnter(cell) && !grass.HasGrass(cell),
                    $"house footprint blocks and excludes grass at {cell}");
            }
            Check(world.CanEnter(HouseArea2D.OutsideEntryCell),
                "outside entry remains walkable");
            Check(house.GetComponent<DoorTransitionInteractable>() is IInteractable &&
                router.IsInteractableAt(camera.WorldToScreenPoint(house.transform.position)),
                "PC click on house door resolves shared interaction router");

            var grassObjects = GameObject.Find("Grass decorations");
            typeof(GrassField2D).GetMethod("Initialize", Hidden).Invoke(grass, null);
            Check(grassObjects.transform.childCount == 259 &&
                grass.HasGrass(Vector2Int.zero) &&
                !grass.HasGrass(new Vector2Int(0, -1)) &&
                !grass.HasGrass(new Vector2Int(1, 1)) &&
                !grass.HasGrass(new Vector2Int(-3, 1)) &&
                !grass.HasGrass(new Vector2Int(5, 1)),
                "grass excludes ten new house cells and retains prior exclusions");
            Check(interiorRoot.transform.Find("Bed - simple one") != null &&
                insideDoor != null &&
                interiorRoot.transform.Find("Bed - simple one")
                    .GetComponentsInChildren<GridObstacle>().Length == 4,
                "one room contains floor, walls, exit and a two by two blocked bed");

            Check(crow.transform.position == world.CellToWorld(CrowWander2D.InitialCell) &&
                CrowWander2D.InitialCell == new Vector2Int(7, 2) &&
                crow.GetComponent<GridObstacle>() == null &&
                crow.GetComponent<BoxCollider2D>() != null &&
                crow.GetComponent<ExamineInteractable>() is IInteractable &&
                world.CanEnter(CrowWander2D.InitialCell) &&
                (string)Field(crow.GetComponent<ExamineInteractable>(), "message") == "カァ。",
                "crow begins by the tree, remains walkable, and uses generic examine");
            Check(CrowWander2D.RangeMin == new Vector2Int(4, 0) &&
                CrowWander2D.RangeMax == new Vector2Int(8, 4),
                "crow stays in the limited tree neighborhood");
            typeof(CrowWander2D).GetMethod("Awake", Hidden).Invoke(crowWander, null);
            UnityEngine.Random.InitState(2020);
            var moved = false;
            for (var i = 1; i <= 30; i++)
            {
                crowWander.Tick(i * 5f);
                moved |= crowWander.Cell != CrowWander2D.InitialCell;
                Check(crowWander.Cell.x >= CrowWander2D.RangeMin.x &&
                    crowWander.Cell.x <= CrowWander2D.RangeMax.x &&
                    crowWander.Cell.y >= CrowWander2D.RangeMin.y &&
                    crowWander.Cell.y <= CrowWander2D.RangeMax.y &&
                    world.CanEnter(crowWander.Cell),
                    "crow wait/hop remains within walkable nearby cells");
            }
            Check(moved, "crow hops to another cell over time");

            typeof(PlayerMover).GetMethod("Awake", Hidden).Invoke(player, null);
            typeof(HouseArea2D).GetMethod("Awake", Hidden).Invoke(area, null);
            player.TeleportTo(HouseArea2D.OutsideEntryCell);
            Check(!player.TryStep(Vector2Int.up) && player.Facing == FacingDirection.Up,
                "blocked house door turns player without starting a step");
            router.TryInteractAhead();
            Check(area.IsInside && interiorRoot.activeSelf && !exteriorRoot.activeSelf &&
                player.Cell == HouseArea2D.InsideEntryCell &&
                world.MinCell == HouseArea2D.InsideMinCell &&
                world.MaxCell == HouseArea2D.InsideMaxCell && !mask.enabled,
                "facing the outside door enters the room without grass masking");
            Check(!player.TryStep(Vector2Int.down) && player.Facing == FacingDirection.Down,
                "room exit cell is a blocking interactable door");
            router.TryInteractAhead();
            Check(!area.IsInside && !interiorRoot.activeSelf && exteriorRoot.activeSelf &&
                player.Cell == HouseArea2D.OutsideEntryCell && mask.enabled &&
                world.MinCell == new Vector2Int(-10, -6) &&
                world.MaxCell == new Vector2Int(10, 6),
                "interior exit returns to the same outdoor entry");

            var crowTarget = crowWander.Cell;
            var crowApproach = crowTarget + Vector2Int.left;
            player.TeleportTo(crowApproach + Vector2Int.left);
            Check(player.TryStep(Vector2Int.right), "player approaches crow using one grid step");
            player.TeleportTo(crowApproach);
            router.TryInteractAhead();
            var message = (TimedMessage)Field(hud, "message");
            Check(message.TextAt(Time.unscaledTime) == "カァ。",
                "crow displays its message through the existing HUD");

            var autoInput = (AutoModeController)Field(input, "autoMode");
            Check(auto == autoInput && Field(auto, "input") == input &&
                Field(auto, "world") == world && Field(auto, "player") == player &&
                auto.IsOverControls(new Vector2(Screen.width - 30f, Screen.height - 55f)),
                "PC and touch share a safe-area AUTO toggle outside D-pad and A button");
            typeof(AutoModeController).GetMethod("Awake", Hidden).Invoke(auto, null);
            Check(auto.AutoEnabled && AutoModeController.IdleSeconds == 60f,
                "AUTO defaults on after exactly one minute of inactivity");
            player.TeleportTo(Vector2Int.zero);
            auto.RecordUserAction(0f);
            auto.Tick(59.99f);
            Check(!auto.Active, "AUTO stays idle before sixty seconds");
            auto.Tick(60f);
            Check(auto.Active, "AUTO starts at the sixty-second threshold");
            var autoSteps = 0;
            player.StepStarted += _ => autoSteps++;
            UnityEngine.Random.InitState(2000);
            auto.Tick(61f);
            Check(autoSteps == 1 && player.IsMoving,
                "AUTO uses PlayerMover's successful one-cell step event");
            auto.RecordUserAction(61.01f);
            Check(!auto.Active && !player.IsMoving,
                "user input immediately cancels AUTO movement");
            auto.SetAutoEnabled(false, 62f);
            auto.Tick(200f);
            Check(!auto.Active, "AUTO OFF prevents idle activation");
            auto.SetAutoEnabled(true, 201f);
            player.TeleportTo(HouseArea2D.OutsideEntryCell);
            player.TryStep(Vector2Int.up);
            router.TryInteractAhead();
            auto.Tick(300f);
            Check(area.IsInside && !auto.Active,
                "AUTO never starts or enters the house on its own");
            player.TryStep(Vector2Int.down);
            router.TryInteractAhead();

            var stone = GameObject.Find("Stone - first world object");
            var flower = GameObject.Find("Flower - first bloom");
            var tree = GameObject.Find("Tree - first tree");
            Check((string)Field(stone.GetComponent<ExamineInteractable>(), "message") == "いし。" &&
                (string)Field(flower.GetComponent<ExamineInteractable>(), "message") == "はな。" &&
                (string)Field(tree.GetComponent<ExamineInteractable>(), "message") == "き。" &&
                tree.GetComponent<RootedWorldObjectDepth2D>() != null &&
                world.CanEnter(new Vector2Int(0, -1)) &&
                player.GetComponent<PlayerFootstepAudio>() != null &&
                player.GetComponent<PlayerGrassOcclusion>() != null &&
                UnityEngine.Object.FindFirstObjectByType<TouchDpad>() != null &&
                UnityEngine.Object.FindFirstObjectByType<TouchActionButton>() != null,
                "stone, flower, tree, dirt, depth, footsteps, mask and touch controls regressions");
            Debug.Log("HALKA ver2.0 checks passed.");
        }

        private static object Field(object target, string name) =>
            target.GetType().GetField(name, Hidden).GetValue(target);

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
                "sharp centered world sprite: " + fileName);
        }

        private static void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException("ver2.0 check failed: " + label);
        }
    }
}
