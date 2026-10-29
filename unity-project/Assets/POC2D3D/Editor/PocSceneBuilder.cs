using System;
using System.Collections.Generic;
using System.IO;
using Halka.Game.Input;
using Halka.Game.Interaction;
using Halka.Game.Player;
using Halka.Game.UI;
using Halka.Game.World;
using Halka.Poc2D3D.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Halka.Poc2D3D.EditorTools
{
    // Generates the Phase 1 POC scene and its assets. It only writes under Assets/POC2D3D and
    // only reads the official Map caches, Font and Catalog definitions.
    public static class PocSceneBuilder
    {
        public const string Root = "Assets/POC2D3D";
        public const string ScenePath = Root + "/Scenes/HALKA_WORLD_2D3D_POC.unity";
        // Phase 5: the Release Candidate scene, built from the same content as the POC scene (the
        // POC scene stays the technical verification scene with its options shown).
        // ver3.1: the game scene is HARUKA_WORLD_ver3.1 (the ver3.0 RC3 scene file is kept as the
        // published version's scene and no longer rebuilt).
        public const string RcScenePath = Root + "/Scenes/HARUKA_WORLD_ver3.8.unity";
        // ver3.2: the chunk test scene (the release scene + a generated test world; test builds only).
        public const string ChunkTestScenePath = Root + "/Scenes/HARUKA_WORLD_ver3.2_CHUNKTEST.unity";
        public const string ChunkTestFixturePath = Root + "/Fixtures/ChunkTest/PocTestWorldFixture.asset";
        public const string Ver30Rc3ScenePath = Root + "/Scenes/HARUKA_WORLD_ver3.0_RC3.unity";
        public const string CatalogPath = Root + "/Data/POC_PresentationCatalog.asset";
        public const string ToonMaterialPath = Root + "/Materials/POC_ToonVertexColor.mat";
        public const string GridMaterialPath = Root + "/Materials/POC_GridOverlay.mat";
        public const string ScreenMaterialPath = Root + "/Materials/POC_ScreenOverlay.mat";
        public const string PlayerModelPath = Root + "/Models/poc_player.fbx";
        public const string TreeModelPath = Root + "/Models/poc_tree.fbx";
        public const string HouseModelPath = Root + "/Models/poc_house.fbx";
        public const string Phase2Models = Root + "/Models/Phase2/";
        public const string OutlineMaterialPath = Root + "/Materials/POC_OutlineComposite.mat";
        public const string OutlineMaskShader = "HALKA/POC/OutlineMask";
        // Feedback after RC1: the 3D view's outline (same mask, composite at screen size).
        public const string Outline3DMaterialPath = Root + "/Materials/POC_Outline3D.mat";
        // Phase 7: the official 3D ﾊﾙｶﾁｬﾝ (adopted by the user). Made in Phase 2.1 as a stand-in:
        // Minecraft-like blocks painted after the 2D sprites. The file keeps its first name so no
        // reference breaks (blender/scripts/build_haruka_blocky.py, HARUKA_WORLD_PLAYER_MODEL_SPEC.md).
        // The first, rounded haruka_placeholder.fbx stays for comparison only; the 2D view keeps the
        // original sprites.
        public const string OfficialPlayerModelPath = Root + "/Models/Phase2_1/haruka_blocky_placeholder.fbx";
        // The Phase 2.1 name of the same path (older checks and tools use it).
        public const string PlaceholderPlayerPath = OfficialPlayerModelPath;
        // 2D Pixel View: the existing ﾊﾙｶﾁｬﾝ sprites, referenced read-only (same files as the
        // FirstDay CharacterVisual).
        public const string SpriteMaterialPath = Root + "/Materials/POC_PixelSprite.mat";
        public const string FadeMaterialPath = Root + "/Materials/POC_FadeLayer.mat";
        public const string CharacterSprites = "Assets/Content/Character/";
        public static readonly string[] Phase2ModelNames =
        {
            "ground_grass_a", "ground_grass_b", "ground_grass_c", "ground_dirt_full", "ground_dirt_edge",
            "ground_dirt_corner", "grass_tuft_a", "grass_tuft_b", "grass_tuft_c", "tree_basic", "flower_basic",
            "stone_basic_a", "stone_basic_b", "stone_basic_c"
        };
        // Phase 3: the house, outdoor fixtures, interior pieces and furniture made in Blender
        // (blender/scripts/build_phase3_buildings.py).
        public const string Phase3Models = Root + "/Models/Phase3/";
        public static readonly string[] Phase3ModelNames =
        {
            "house_main", "bench_basic", "sign_basic", "well_basic", "bed_basic", "desk_basic", "cushion_basic",
            "recording_mic_basic", "art_easel_basic", "studio_shelf_basic", "interior_floor_a", "interior_floor_b",
            "interior_wall_base", "interior_wall_upper", "interior_doorway_base", "interior_doorway_upper"
        };
        // Definitions that Phase 3 turns from generated boxes / the Phase 1 house into Blender models.
        public static readonly string[] Phase3Definitions =
        {
            "house_main", "bench_basic", "sign_basic", "well_basic", "bed_basic", "desk_basic", "cushion_basic",
            "recording_mic_basic", "art_easel_basic", "studio_shelf_basic"
        };
        // Phase 4: the crow (blender/scripts/build_phase4_crow.py), its 2D sprites and voice (the
        // 2D game's files, read only), the seated artwork, and the 3D see-through tree material.
        public const string Phase4Models = Root + "/Models/Phase4/";
        public const string CrowModelPath = Phase4Models + "crow_basic.fbx";
        public const string CrowSprites = "Assets/Content/World/crow_";
        public const string CrowVoicePath = "Assets/Content/Audio/crow_voice.mp3";
        // Phase 5: the 2D game's footstep and grass (same files, same settings).
        public const string FootstepPath = "Assets/Content/Audio/footstep_one_step.wav";
        public const string GrassPath = "Assets/Content/World/grass.png";
        public const string GrassRustlePath = "Assets/Content/World/grass_rustle/";
        public const string BenchSitPath = "Assets/Content/Character/bench_sit.png";
        // Feedback after RC1: the sofa (blender/scripts/build_phase5_sofa.py) with the cushion on it.
        public const string Phase5Models = Root + "/Models/Phase5/";
        public const string SofaModelPath = Phase5Models + "sofa_basic.fbx";
        public const string TreeFadeMaterialPath = Root + "/Materials/POC_ToonVertexColorFade.mat";
        public const string OutdoorMapPath = "Assets/Content/Maps/first_field.asset";
        public const string InteriorMapPath = "Assets/Content/Maps/halka_house.asset";
        // ver3.2: the outdoor world registry (which outdoor Maps, at which world offsets).
        public const string OutdoorWorldRegistryPath = "Assets/Content/Maps/World/outdoor_main.hwworld.json";
        // ver3.3: the direction colours (North blue, East yellow, South red, West green).
        public const string DirectionThemesPath = "Assets/Content/Maps/World/direction_themes.json";
        // ver3.5: the places 「そらをとぶ」 can fly to.
        public const string FastTravelPointsPath = "Assets/Content/FastTravel/fast_travel_points.json";
        // ver3.1: the sound booth (its own map, entered from the booth in the house) and its
        // Blender models (blender/scripts/build_v31_studio.py).
        public const string SoundBoothMapPath = "Assets/Content/Maps/halka_soundbooth.asset";
        public const string SoundBoothMapId = "halka_soundbooth";
        public const string V31Models = Root + "/Models/V31/";
        public static readonly string[] V31ModelNames =
        {
            "soundbooth_exterior", "dtm_desk_large", "illust_desk_tablet", "booth_floor_a", "booth_floor_b",
            "booth_wall_base", "booth_wall_upper", "booth_doorway_base", "booth_doorway_upper"
        };
        public static readonly string[] V31Definitions = { "soundbooth_exterior", "dtm_desk_large", "illust_desk_tablet" };
        // ver3.2 interior: the two booth desks one cell wider (a middle cell to stand in front of),
        // the house's kitchen, rug and low table, the booth's guitar (blender/scripts/build_v32_interior.py).
        public const string V32Models = Root + "/Models/V32/";
        // ver3.3: the East area's yellow accents (blender/scripts/build_v33_east.py).
        public const string V33Models = Root + "/Models/V33/";
        public static readonly string[] V33ModelNames = { "flower_yellow", "sunflower_cluster", "sign_yellow" };
        // ver3.6, the North area (blender/scripts/build_v36_north.py).
        public const string V36Models = Root + "/Models/V36/";
        public static readonly string[] V36ModelNames = { "flower_blue", "sign_blue", "water_lily", "pond_reeds" };
        // ver3.7, the West forest (blender/scripts/build_v37_west.py).
        public const string V37Models = Root + "/Models/V37/";
        public static readonly string[] V37ModelNames = { "stump_hollow", "forest_bush", "forest_fern", "fallen_leaves", "sign_green" };
        // ver3.8, the South hill (blender/scripts/build_v38_south.py).
        public const string V38Models = Root + "/Models/V38/";
        public static readonly string[] V38ModelNames =
            { "pinwheel_red", "rock_red", "rock_ledge", "rock_small", "grass_short", "ground_accent", "sign_red" };
        public static readonly string[] V32ModelNames =
        {
            "dtm_desk_large", "illust_desk_tablet", "kitchen_counter", "living_rug", "low_table_living", "guitar_stand"
        };
        public const string MessageFontPath = "Assets/Content/Fonts/HalkaMessageSubset.ttf";
        public const string BlenderMaterialName = "HALKA_VertexColor";

        [MenuItem("HALKA/POC 2D3D/Build POC Scene")]
        public static void BuildFromMenu() => Build();

        public static void BuildBatch()
        {
            try
            {
                Build();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void BuildReleaseCandidateBatch()
        {
            try
            {
                Build(RcScenePath, true);
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void Build() => Build(ScenePath, false);

        public static void Build(string scenePath, bool releaseCandidate)
        {
            foreach (var folder in new[] { "Scenes", "Data", "Materials" })
                if (!AssetDatabase.IsValidFolder(Root + "/" + folder)) AssetDatabase.CreateFolder(Root, folder);
            var toon = EnsureMaterial(ToonMaterialPath, "HALKA/POC/ToonVertexColor", null);
            var grid = EnsureMaterial(GridMaterialPath, "HALKA/POC/UnlitVertexColor", (float)CompareFunction.LessEqual);
            var screen = EnsureMaterial(ScreenMaterialPath, "HALKA/POC/UnlitVertexColor", (float)CompareFunction.Always);
            foreach (var path in new[] { PlayerModelPath, TreeModelPath, HouseModelPath }) RemapModelMaterial(path, toon);
            foreach (var name in Phase2ModelNames) RemapModelMaterial(Phase2Models + name + ".fbx", toon);
            RemapModelMaterial(PlaceholderPlayerPath, toon);
            foreach (var name in Phase3ModelNames) RemapModelMaterial(Phase3Models + name + ".fbx", toon);
            RemapModelMaterial(CrowModelPath, toon);
            RemapModelMaterial(SofaModelPath, toon);
            foreach (var name in V31ModelNames) RemapModelMaterial(V31Models + name + ".fbx", toon);
            foreach (var name in V32ModelNames) RemapModelMaterial(V32Models + name + ".fbx", toon);
            foreach (var name in V33ModelNames) RemapModelMaterial(V33Models + name + ".fbx", toon);
            foreach (var name in V36ModelNames) RemapModelMaterial(V36Models + name + ".fbx", toon);
            foreach (var name in V37ModelNames) RemapModelMaterial(V37Models + name + ".fbx", toon);
            foreach (var name in V38ModelNames) RemapModelMaterial(V38Models + name + ".fbx", toon);
            EnsureMaterial(TreeFadeMaterialPath, "HALKA/POC/ToonVertexColorFade", null);
            EnsureMaterial(OutlineMaterialPath, "HALKA/POC/OutlineComposite", null);
            EnsureMaterial(Outline3DMaterialPath, "HALKA/POC/Outline3D", null);
            EnsureMaterial(SpriteMaterialPath, "HALKA/POC/PixelSprite", null);
            EnsureMaterial(FadeMaterialPath, "HALKA/POC/FadeLayer", null);

            var catalog = AssetDatabase.LoadAssetAtPath<PocPresentationCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<PocPresentationCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            // Phase 2: nature objects and ground are the Blender models made for はるかわーるど;
            // the player and the house remain Phase 1 placeholders.
            catalog.Configure(LoadModel(PlayerModelPath), toon, grid, new List<PocPresentationBinding>
            {
                // Phase 4: a tree that hides the player fades (40%, 0.25 s) in 3D as in 2D,
                // instead of being hidden as a camera occluder.
                new PocPresentationBinding { definitionId = "tree_basic", model = Phase2("tree_basic"),
                    perchAnchorName = "PerchAnchor", fadeInFront2D = true, fadeInFront3D = true,
                    cameraOccluder = false, fadeOpacity = 0.4f, fadeSeconds = 0.25f },
                new PocPresentationBinding { definitionId = "flower_basic", model = Phase2("flower_basic"),
                    cameraOccluder = false, minPickSize = SmallPickSize },
                new PocPresentationBinding { definitionId = "stone_basic", model = Phase2("stone_basic_a"),
                    cameraOccluder = false, minPickSize = SmallPickSize, variants = new List<GameObject>
                    { Phase2("stone_basic_a"), Phase2("stone_basic_b"), Phase2("stone_basic_c") } },
                // Phase 3 (Blender). The house hides only its blocking parts (roof / walls / door);
                // low furniture never hides itself for the camera, tall thin pieces may.
                new PocPresentationBinding { definitionId = "house_main", model = Phase3("house_main"),
                    partialOcclusion = true },
                new PocPresentationBinding { definitionId = "bench_basic", model = Phase3("bench_basic"), cameraOccluder = false },
                new PocPresentationBinding { definitionId = "sign_basic", model = Phase3("sign_basic"), cameraOccluder = false },
                new PocPresentationBinding { definitionId = "well_basic", model = Phase3("well_basic"), cameraOccluder = false },
                new PocPresentationBinding { definitionId = "bed_basic", model = Phase3("bed_basic"), cameraOccluder = false },
                new PocPresentationBinding { definitionId = "desk_basic", model = Phase3("desk_basic"), cameraOccluder = false },
                new PocPresentationBinding { definitionId = "cushion_basic", model = Phase3("cushion_basic"),
                    cameraOccluder = false, minPickSize = SmallPickSize },
                // Feedback after RC1: the easel and the mic stay drawn (never hidden as camera
                // occluders); the easel fades like the trees in front of her, the thin mic stand
                // does not fade either (second feedback).
                new PocPresentationBinding { definitionId = "recording_mic_basic", model = Phase3("recording_mic_basic"),
                    cameraOccluder = false },
                new PocPresentationBinding { definitionId = "art_easel_basic", model = Phase3("art_easel_basic"),
                    cameraOccluder = false, fadeInFront3D = true, fadeOpacity = 0.4f, fadeSeconds = 0.25f },
                new PocPresentationBinding { definitionId = "sofa_basic", model = LoadModel(SofaModelPath), cameraOccluder = false },
                new PocPresentationBinding { definitionId = "studio_shelf_basic", model = Phase3("studio_shelf_basic"),
                    cameraOccluder = false },
                // ver3.1: the booth stands against the house's North wall (the camera never looks
                // through it at the player); the desks are low furniture.
                new PocPresentationBinding { definitionId = "soundbooth_exterior", model = V31("soundbooth_exterior"),
                    cameraOccluder = false },
                // ver3.2 interior: the wider desks (V32), the kitchen (tall, against the East wall: it
                // never stands between the camera and her), the rug (flat: no shadow, a soft outline),
                // the low table and the guitar (small: never hidden for the camera).
                new PocPresentationBinding { definitionId = "dtm_desk_large", model = V32("dtm_desk_large"),
                    cameraOccluder = false },
                new PocPresentationBinding { definitionId = "illust_desk_tablet", model = V32("illust_desk_tablet"),
                    cameraOccluder = false },
                new PocPresentationBinding { definitionId = "kitchen_counter", model = V32("kitchen_counter"),
                    cameraOccluder = false },
                new PocPresentationBinding { definitionId = "living_rug", model = V32("living_rug"),
                    cameraOccluder = false, castShadows = false, outlineStrength = 0.5f },
                new PocPresentationBinding { definitionId = "low_table_living", model = V32("low_table_living"),
                    cameraOccluder = false },
                new PocPresentationBinding { definitionId = "guitar_stand", model = V32("guitar_stand"),
                    cameraOccluder = false, minPickSize = SmallPickSize },
                // ver3.3, the East area: a yellow flower (as flower_basic), the plaza's sunflowers (tall:
                // they fade in front of her like the trees), the yellow signpost (its model: sign_yellow).
                new PocPresentationBinding { definitionId = "flower_yellow", model = V33("flower_yellow"),
                    cameraOccluder = false, minPickSize = SmallPickSize },
                new PocPresentationBinding { definitionId = "sunflower_cluster", model = V33("sunflower_cluster"),
                    cameraOccluder = false, fadeInFront2D = true, fadeInFront3D = true, fadeOpacity = 0.4f, fadeSeconds = 0.25f },
                new PocPresentationBinding { definitionId = "signpost_yellow", model = V33("sign_yellow"), cameraOccluder = false },
                // ver3.6, the North area: a blue flower, the blue signpost (its model: sign_blue), and at the
                // pond the lily pads (flat on the water: no shadow, a soft outline) and the reeds.
                new PocPresentationBinding { definitionId = "flower_blue", model = V36("flower_blue"),
                    cameraOccluder = false, minPickSize = SmallPickSize },
                new PocPresentationBinding { definitionId = "signpost_blue", model = V36("sign_blue"), cameraOccluder = false },
                new PocPresentationBinding { definitionId = "water_lily", model = V36("water_lily"),
                    cameraOccluder = false, castShadows = false, outlineStrength = 0.5f, minPickSize = SmallPickSize },
                new PocPresentationBinding { definitionId = "pond_reeds", model = V36("pond_reeds"),
                    cameraOccluder = false, minPickSize = SmallPickSize },
                // ver3.7, the West forest: the clearing's stump (low: never hidden for the camera), the low bush,
                // ferns and fallen leaves (walkable decoration: flat leaves cast no shadow, soft outline), the
                // green signpost (its model: sign_green).
                new PocPresentationBinding { definitionId = "tree_stump_hollow", model = V37("stump_hollow"), cameraOccluder = false },
                new PocPresentationBinding { definitionId = "forest_bush", model = V37("forest_bush"), cameraOccluder = false },
                new PocPresentationBinding { definitionId = "forest_fern", model = V37("forest_fern"),
                    cameraOccluder = false, minPickSize = SmallPickSize },
                new PocPresentationBinding { definitionId = "fallen_leaves", model = V37("fallen_leaves"),
                    cameraOccluder = false, castShadows = false, outlineStrength = 0.5f },
                new PocPresentationBinding { definitionId = "signpost_green", model = V37("sign_green"), cameraOccluder = false },
                // ver3.8, the South hill: the red pinwheel (its blades, the child pinwheel_blades, are turned by
                // PocWorldPresenter.Pinwheel.cs; slim: never hidden for the camera), the red-brown boulder and the
                // hill's rock ledge (low), small stones and short grass (walkable decoration), the reddish soil
                // (flat on the ground: no shadow, a soft outline), the red signpost (its model: sign_red).
                new PocPresentationBinding { definitionId = "south_pinwheel_red", model = V38("pinwheel_red"), cameraOccluder = false },
                new PocPresentationBinding { definitionId = "south_rock_red", model = V38("rock_red"), cameraOccluder = false },
                new PocPresentationBinding { definitionId = "south_rock_ledge", model = V38("rock_ledge"), cameraOccluder = false },
                new PocPresentationBinding { definitionId = "south_rock_small", model = V38("rock_small"),
                    cameraOccluder = false, minPickSize = SmallPickSize },
                new PocPresentationBinding { definitionId = "south_grass_short", model = V38("grass_short"),
                    cameraOccluder = false, minPickSize = SmallPickSize },
                new PocPresentationBinding { definitionId = "south_ground_accent", model = V38("ground_accent"),
                    cameraOccluder = false, castShadows = false, outlineStrength = 0.5f },
                new PocPresentationBinding { definitionId = "south_signpost_red", model = V38("sign_red"), cameraOccluder = false }
            }, new PocEnvironmentPresentation
            {
                grassTiles = new List<GameObject> { Phase2("ground_grass_a"), Phase2("ground_grass_b"), Phase2("ground_grass_c") },
                dirtFull = Phase2("ground_dirt_full"),
                dirtEdge = Phase2("ground_dirt_edge"),
                dirtCorner = Phase2("ground_dirt_corner"),
                grassTufts = new List<GameObject> { Phase2("grass_tuft_a"), Phase2("grass_tuft_b"), Phase2("grass_tuft_c") }
            });
            var sprites = LoadCharacterSprites();
            sprites.benchSit = AssetDatabase.LoadAssetAtPath<Sprite>(BenchSitPath);
            if (sprites.benchSit == null) throw new InvalidOperationException("Sprite missing: " + BenchSitPath);
            catalog.ConfigurePlayer(new PocActorPresentation
            {
                model = LoadModel(OfficialPlayerModelPath),
                placeholder = false, // Phase 7: the official model
                sprites = sprites
            });
            catalog.ConfigureInterior(new PocInteriorPresentation
            {
                floorTiles = new List<GameObject> { Phase3("interior_floor_a"), Phase3("interior_floor_b") },
                wallBase = Phase3("interior_wall_base"),
                wallUpper = Phase3("interior_wall_upper"),
                doorwayBase = Phase3("interior_doorway_base"),
                doorwayUpper = Phase3("interior_doorway_upper")
            });
            catalog.ConfigureRoomInteriors(new List<PocInteriorPresentation>
            {
                new PocInteriorPresentation
                {
                    mapId = SoundBoothMapId,
                    wallDefinitionId = "booth_wall",
                    floorTiles = new List<GameObject> { V31("booth_floor_a"), V31("booth_floor_b") },
                    wallBase = V31("booth_wall_base"),
                    wallUpper = V31("booth_wall_upper"),
                    doorwayBase = V31("booth_doorway_base"),
                    doorwayUpper = V31("booth_doorway_upper")
                }
            });
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = BuildSceneContent(catalog, screen);
            if (releaseCandidate) ConfigureReleaseCandidate(root);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = PocTimeOfDay.DayAmbient;
            RenderSettings.skybox = null;
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("HALKA POC scene built: " + scenePath + (releaseCandidate ? " (Release Candidate)" : ""));
        }

        // ver3.2: the release scene with a generated test world (PocChunkTestBoot) for the chunk
        // checks in WebGL. The fixture asset is referenced only by this scene.
        public static void BuildChunkTestScene()
        {
            Build(ChunkTestScenePath, true);
            var fixture = AssetDatabase.LoadAssetAtPath<Halka.Poc2D3D.World.PocTestWorldFixture>(ChunkTestFixturePath);
            if (fixture == null || !fixture.IsComplete) throw new InvalidOperationException("chunk test fixture missing: " + ChunkTestFixturePath);
            var scene = EditorSceneManager.OpenScene(ChunkTestScenePath, OpenSceneMode.Single);
            var root = scene.GetRootGameObjects()[0];
            var boot = root.AddComponent<Halka.Poc2D3D.Dev.PocChunkTestBoot>();
            boot.Configure(fixture, root.GetComponent<SharedWorldSession>(), root.GetComponentInChildren<Halka.Poc2D3D.Save.HarukaSaveSystem>(true));
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("HALKA chunk test scene built: " + ChunkTestScenePath);
        }

        // Phase 5: the same world and game; only the launch differs. 2D at start, the debug panel
        // off unless ?debug=1, the HUD reads "ver3.0 RC1".
        private static void ConfigureReleaseCandidate(GameObject root)
        {
            root.name = "HARUKA WORLD ver3.8";
            var panel = root.GetComponent<PocDebugPanel>();
            var hud = root.GetComponentInChildren<GameHud>(true);
            var switcher = root.GetComponent<PocViewSwitcher>();
            if (panel == null || hud == null || switcher == null) throw new InvalidOperationException("RC scene parts are missing");
            panel.ConfigureReleaseCandidate(true);
            hud.SetVersionLabel(PocDebugPanel.ReleaseCandidateVersionLabel);
            // RC1 feedback: the version is shown in the menu, phones are played upright again.
            hud.SetVersionLabelShown(false);
            var menu = root.GetComponentInChildren<GameMenuController>(true);
            var guard = root.GetComponentInChildren<PocOrientationGuard>(true);
            if (menu == null || guard == null) throw new InvalidOperationException("RC scene menu / guard missing");
            menu.SetVersionText(PocDebugPanel.ReleaseCandidateVersionLabel);
            guard.LandscapeOnly = false;
            var save = root.GetComponentInChildren<Halka.Poc2D3D.Save.HarukaSaveSystem>(true);
            if (save == null) throw new InvalidOperationException("RC scene save system missing");
            var saveSettings = new SerializedObject(save);
            saveSettings.FindProperty("gameVersion").stringValue = PocDebugPanel.ReleaseCandidateVersionLabel;
            saveSettings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(panel);
            EditorUtility.SetDirty(hud);
            EditorUtility.SetDirty(menu);
            EditorUtility.SetDirty(guard);
        }

        // ver3.5: the fast travel points, checked against the outdoor world (any error stops the build).
        public static FastTravelRegistry LoadFastTravel(SharedWorldSession session)
        {
            var file = AssetDatabase.LoadAssetAtPath<TextAsset>(FastTravelPointsPath);
            if (file == null) throw new InvalidOperationException("Fast travel points are missing: " + FastTravelPointsPath);
            var problems = new List<string>();
            var registry = OutdoorWorldRegistryJson.ParseFastTravel(file.text, problems);
            if (registry != null)
            {
                // The logical world on its own (the session itself is initialized at play).
                var index = new OutdoorWorldIndex(session.EffectiveOutdoorWorld);
                problems.AddRange(registry.Validate(index, cell => index.TryGetHouseDoor(out var door) && door == cell));
            }
            foreach (var problem in problems) Debug.LogWarning("[ver3.5 fast travel] " + problem);
            if (registry == null || FastTravelRegistry.HasErrors(problems))
                throw new InvalidOperationException("Fast travel points are invalid: " + string.Join("; ", problems));
            return registry;
        }

        // ver3.2: the registry decides the outdoor world; its Player Start Map must be the one the
        // session starts on (first_field), and any error stops the build.
        public static OutdoorWorldDefinition LoadOutdoorWorld(MapDefinition startMap)
        {
            var file = AssetDatabase.LoadAssetAtPath<TextAsset>(OutdoorWorldRegistryPath);
            if (file == null) throw new InvalidOperationException("Outdoor world registry is missing: " + OutdoorWorldRegistryPath);
            var problems = new List<string>();
            var themesFile = AssetDatabase.LoadAssetAtPath<TextAsset>(DirectionThemesPath);
            if (themesFile == null) throw new InvalidOperationException("Direction themes are missing: " + DirectionThemesPath);
            var themes = OutdoorWorldRegistryJson.ParseThemes(themesFile.text, problems);
            var world = themes == null ? null : OutdoorWorldRegistryJson.Parse(file.text,
                id => AssetDatabase.LoadAssetAtPath<MapDefinition>("Assets/Content/Maps/" + id + ".asset"), problems, themes);
            foreach (var problem in problems) Debug.LogWarning("[ver3.2 world registry] " + problem);
            if (world == null) throw new InvalidOperationException("Outdoor world registry is invalid: " + string.Join("; ", problems));
            if (!world.regions.Exists(link => link.map == startMap))
                throw new InvalidOperationException("Outdoor world registry does not contain " + startMap.MapId);
            return world;
        }

        public static GameObject BuildSceneContent(PocPresentationCatalog catalog, Material screenOverlay)
        {
            var outdoor = AssetDatabase.LoadAssetAtPath<MapDefinition>(OutdoorMapPath);
            var interior = AssetDatabase.LoadAssetAtPath<MapDefinition>(InteriorMapPath);
            var soundBooth = AssetDatabase.LoadAssetAtPath<MapDefinition>(SoundBoothMapPath);
            var font = AssetDatabase.LoadAssetAtPath<Font>(MessageFontPath);
            if (outdoor == null || interior == null || soundBooth == null)
                throw new InvalidOperationException("Official map caches are missing");
            if (font == null) throw new InvalidOperationException("Message font is missing");

            var root = new GameObject("HALKA WORLD 2D3D POC");
            root.AddComponent<AudioListener>();
            var worldTime = root.AddComponent<WorldTimeController>();
            var session = root.AddComponent<SharedWorldSession>();
            var input = root.AddComponent<PocInput>();
            var presenter = root.AddComponent<PocWorldPresenter>();
            var switcher = root.AddComponent<PocViewSwitcher>();
            var time = root.AddComponent<PocTimeOfDay>();
            var panel = root.AddComponent<PocDebugPanel>();
            panel.ConfigureLineBoil(root.AddComponent<PocLineBoil>());

            var controls = new GameObject("Existing touch controls (D-pad, A)");
            controls.transform.SetParent(root.transform, false);
            var dpad = controls.AddComponent<TouchDpad>();
            var action = controls.AddComponent<TouchActionButton>();
            var actionObject = new SerializedObject(action);
            actionObject.FindProperty("dpad").objectReferenceValue = dpad;
            actionObject.FindProperty("fallbackFont").objectReferenceValue = font;
            actionObject.ApplyModifiedPropertiesWithoutUndo();

            var player = new GameObject("POC Player (shared state view)");
            var actor = player.AddComponent<PocActorView>();
            var yawRoot = new GameObject("Model yaw (+180: Blender front faces South)");
            yawRoot.transform.SetParent(player.transform, false);
            yawRoot.transform.localRotation = Quaternion.Euler(0f, GridSpace3D.ModelFrontYawOffset, 0f);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(52f, 32f, 0f);
            sun.shadows = LightShadows.Hard;
            sun.shadowStrength = 1f;
            sun.shadowBias = 0.02f;
            sun.shadowNormalBias = 0.25f;
            sun.intensity = PocTimeOfDay.DaySunIntensity;

            var backdrop = new Color(0.06f, 0.06f, 0.08f);
            var pixelCamera = CreateCamera("2D Pixel Camera (renders into low-res texture)", 0, backdrop);
            pixelCamera.orthographic = true;
            var pixelView = pixelCamera.gameObject.AddComponent<PocPixelView2D>();
            var presenterCamera = CreateCamera("2D Presenter Camera (integer upscale)", 1, Color.black);
            presenterCamera.cullingMask = 0;
            var pixelPresenter = presenterCamera.gameObject.AddComponent<PocPixelPresenter>();
            var thirdCamera = CreateCamera("3D Third-Person Camera", 0, PocTimeOfDay.DaySky);
            thirdCamera.nearClipPlane = 0.05f;
            thirdCamera.farClipPlane = 60f;
            var thirdPerson = thirdCamera.gameObject.AddComponent<PocThirdPersonCamera>();
            thirdCamera.gameObject.AddComponent<PocOutline3D>().Configure(Shader.Find(OutlineMaskShader),
                AssetDatabase.LoadAssetAtPath<Material>(Outline3DMaterialPath));
            thirdCamera.enabled = false;
            thirdPerson.enabled = false;

            session.Configure(outdoor, interior, input);
            session.ConfigureOutdoorWorld(LoadOutdoorWorld(outdoor), catalog.Environment.outsideMargin);
            session.ConfigureInnerRoom(interior.MapId, soundBooth);
            input.Configure(dpad, action, switcher, panel, session);
            presenter.Configure(session, catalog);
            actor.Configure(session, yawRoot.transform, catalog);
            actor.BuildModel();
            pixelView.Configure(pixelCamera, session, actor);
            pixelView.ConfigureOutline(Shader.Find(OutlineMaskShader),
                AssetDatabase.LoadAssetAtPath<Material>(OutlineMaterialPath));
            pixelView.ConfigureSprite(AssetDatabase.LoadAssetAtPath<Material>(SpriteMaterialPath));
            pixelView.ConfigureFade(presenter, AssetDatabase.LoadAssetAtPath<Material>(FadeMaterialPath));
            pixelPresenter.Configure(pixelView, time, screenOverlay);
            thirdPerson.Configure(thirdCamera, session, presenter, input);
            switcher.Configure(pixelView, presenterCamera, thirdPerson, actor, presenter, session, input);
            time.Configure(worldTime, session, switcher, sun, thirdCamera);
            panel.Configure(session, switcher, input, time, presenter, dpad, action, font);
            // ver3.2: development numbers for the browser checks (off unless ?debug=1 / ?devstats=1).
            root.AddComponent<Halka.Poc2D3D.Dev.PocDevStats>().Configure(session, presenter, switcher);
            BuildPhase4Content(root, session, input, presenter, switcher, pixelView, thirdPerson, actor, panel,
                dpad, action, font);
            return root;
        }

        // Phase 4: the game's own systems on the shared world.
        private static void BuildPhase4Content(GameObject root, SharedWorldSession session, PocInput input,
            PocWorldPresenter presenter, PocViewSwitcher switcher, PocPixelView2D pixelView,
            PocThirdPersonCamera thirdPerson, PocActorView actor, PocDebugPanel panel, TouchDpad dpad,
            TouchActionButton action, Font font)
        {
            var hud = root.AddComponent<GameHud>();
            SetReference(hud, "dpad", dpad);
            SetReference(hud, "actionButton", action);
            SetReference(hud, "messageFont", font);
            var lifeLog = root.AddComponent<LifeLogController>();
            var autoMode = root.AddComponent<AutoModeController>();
            var menu = root.AddComponent<GameMenuController>();
            SetReference(menu, "autoMode", autoMode);
            SetReference(menu, "lifeLog", lifeLog);
            SetReference(menu, "hud", hud);
            SetReference(menu, "font", font);

            // The crow: shared state (CrowBehaviourCore) with the 2D game's sprites and voice.
            var crowObject = new GameObject("Crow (shared state: CrowBehaviourCore)");
            crowObject.transform.SetParent(root.transform, false);
            var crow = crowObject.AddComponent<PocCrow>();
            var source = crowObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0.55f; // as the 2D game's crow
            var voice = crowObject.AddComponent<InteractionAudio>();
            SetReference(voice, "audioSource", source);
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(CrowVoicePath);
            if (clip == null) throw new InvalidOperationException("Crow voice missing: " + CrowVoicePath);
            SetReference(voice, "clip", clip);
            crow.Configure(session, presenter, voice);
            crow.ConfigureSprites(CrowSprite("idle_down"), CrowSprite("idle_up"), CrowSprite("idle_left"),
                CrowSprite("idle_right"), CrowWalk("down"), CrowWalk("up"), CrowWalk("left"), CrowWalk("right"),
                new[] { CrowSprite("flight_right_up"), CrowSprite("flight_right_down") },
                new[] { CrowSprite("flight_down_up"), CrowSprite("flight_down_down") });
            var crowViewObject = new GameObject("Crow 3D view (Blender crow_basic)");
            crowViewObject.transform.SetParent(root.transform, false);
            var crowView = crowViewObject.AddComponent<PocCrowView3D>();
            crowView.Configure(crow, LoadModel(CrowModelPath));
            crowView.BuildModel();

            var gameplay = root.AddComponent<PocGameplay>();
            gameplay.Configure(session, switcher, autoMode, lifeLog, hud, menu, crow);
            var guard = root.AddComponent<PocOrientationGuard>();
            guard.Configure(gameplay, dpad, action, menu);
            input.ConfigureGame(menu, autoMode, gameplay);
            switcher.ConfigureCrow(crow, crowView);
            pixelView.ConfigureCrow(crow);
            actor.ConfigureSeats(presenter);
            thirdPerson.ConfigureTreeFade(AssetDatabase.LoadAssetAtPath<Material>(TreeFadeMaterialPath));
            panel.ConfigureGame(gameplay, guard);
            // ver3.5: そらをとぶ (the destination list, the flight, what she has found).
            var fastTravel = root.AddComponent<PocFastTravel>();
            fastTravel.Configure(LoadFastTravel(session), session, switcher, presenter, actor, gameplay, root.GetComponent<PocTimeOfDay>());
            gameplay.ConfigureFastTravel(fastTravel);
            BuildPhase5Content(root, gameplay, pixelView);
            BuildPhase6Content(root, session, switcher, autoMode, lifeLog, menu, hud);
        }

        // Phase 6: the save system (automatic save and load, backups, export / import). Its
        // object's name is what the page calls when it is hidden (HarukaSave.jslib).
        private static void BuildPhase6Content(GameObject root, SharedWorldSession session, PocViewSwitcher switcher,
            AutoModeController autoMode, LifeLogController lifeLog, GameMenuController menu, GameHud hud)
        {
            var saveObject = new GameObject(Halka.Poc2D3D.Save.HarukaSaveSystem.ObjectName);
            saveObject.transform.SetParent(root.transform, false);
            var save = saveObject.AddComponent<Halka.Poc2D3D.Save.HarukaSaveSystem>();
            var worldTime = root.GetComponent<WorldTimeController>();
            if (worldTime == null) throw new InvalidOperationException("World time missing");
            save.Configure(session, switcher, autoMode, lifeLog, worldTime, menu, hud, "ver3.0 POC");
            // ver3.4: the crow's companion mode is saved with the rest.
            var gameplay = root.GetComponentInChildren<PocGameplay>(true);
            save.ConfigureCrow(gameplay != null ? gameplay.Crow : null, gameplay);
            // ver3.5: and the places found for そらをとぶ.
            save.ConfigureFastTravel(gameplay != null ? gameplay.FastTravel : null);
        }

        // Phase 5: footsteps (PlayerFootstepAudio driven by SharedWorldSession.StepStarted, the 2D
        // game's AudioSource settings) and the 2D game's grass sprites for the 2D view.
        private static void BuildPhase5Content(GameObject root, PocGameplay gameplay, PocPixelView2D pixelView)
        {
            var footstepObject = new GameObject("Footsteps (PlayerFootstepAudio)");
            footstepObject.transform.SetParent(root.transform, false);
            var source = footstepObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0.45f; // ProjectBuilder's value for the 2D game
            var footsteps = footstepObject.AddComponent<Halka.Game.Player.PlayerFootstepAudio>();
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(FootstepPath);
            if (clip == null) throw new InvalidOperationException("Footstep missing: " + FootstepPath);
            SetReference(footsteps, "source", source);
            SetReference(footsteps, "footstep", clip);
            var serialized = new SerializedObject(footsteps);
            serialized.FindProperty("externallyDriven").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            gameplay.ConfigureFootsteps(footsteps);

            var idle = AssetDatabase.LoadAssetAtPath<Sprite>(GrassPath);
            if (idle == null) throw new InvalidOperationException("Grass missing: " + GrassPath);
            var rustle = new Sprite[5];
            for (var i = 0; i < rustle.Length; i++)
            {
                var path = GrassRustlePath + i.ToString("00") + ".png";
                rustle[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (rustle[i] == null) throw new InvalidOperationException("Grass rustle missing: " + path);
            }
            pixelView.ConfigureGrass(idle, rustle, new[] { 0.09f, 0.09f, 0.09f, 0.09f, 0.42f });
        }

        private static Sprite CrowSprite(string suffix)
        {
            var path = CrowSprites + suffix + ".png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new InvalidOperationException("Sprite missing: " + path);
            return sprite;
        }

        private static Sprite[] CrowWalk(string direction) =>
            new[] { CrowSprite("walk_" + direction + "_0"), CrowSprite("walk_" + direction + "_1") };

        private static void SetReference(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            if (property == null) throw new InvalidOperationException(target.GetType().Name + " has no field " + field);
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Camera CreateCamera(string name, int depth, Color background)
        {
            var camera = new GameObject(name).AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            camera.depth = depth;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            return camera;
        }

        public static PocSpriteSet LoadCharacterSprites() => new PocSpriteSet
        {
            frontIdle = Frames("front_idle", 2),
            backIdle = Frames("back_idle", 2),
            leftIdle = Frames("left_idle", 2),
            rightIdle = Frames("right_idle", 2),
            frontWalk = Frames("walk_front", 4),
            backWalk = Frames("walk_back", 4),
            leftWalk = Frames("walk_left", 4),
            rightWalk = Frames("walk_right", 4)
        };

        private static Sprite[] Frames(string folder, int count)
        {
            var frames = new Sprite[count];
            for (var i = 0; i < count; i++)
            {
                var path = $"{CharacterSprites}{folder}/{i:00}.png";
                frames[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (frames[i] == null) throw new InvalidOperationException("Sprite missing: " + path);
            }
            return frames;
        }

        private static Material EnsureMaterial(string path, string shaderName, float? zTest)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException("Shader missing: " + shaderName);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            if (zTest.HasValue) material.SetFloat("_ZTest", zTest.Value);
            EditorUtility.SetDirty(material);
            return material;
        }

        // The Blender material slot is a name only; Unity maps it to the shared POC material.
        private static void RemapModelMaterial(string path, Material material)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Model not imported: " + path);
            var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), BlenderMaterialName);
            var map = importer.GetExternalObjectMap();
            if (map.TryGetValue(id, out var current) && current == material) return;
            importer.AddRemap(id, material);
            importer.SaveAndReimport();
        }

        public static GameObject Phase2(string name) => LoadModel(Phase2Models + name + ".fbx");
        public static GameObject Phase3(string name) => LoadModel(Phase3Models + name + ".fbx");
        public static GameObject V31(string name) => LoadModel(V31Models + name + ".fbx");
        public static GameObject V32(string name) => LoadModel(V32Models + name + ".fbx");
        public static GameObject V33(string name) => LoadModel(V33Models + name + ".fbx");
        public static GameObject V36(string name) => LoadModel(V36Models + name + ".fbx");
        public static GameObject V37(string name) => LoadModel(V37Models + name + ".fbx");
        public static GameObject V38(string name) => LoadModel(V38Models + name + ".fbx");
        // Clicks on small things (flower, stone, cushion) count within at least this cube.
        public const float SmallPickSize = 0.3f;

        private static GameObject LoadModel(string path)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null) throw new InvalidOperationException("Model missing: " + path);
            return model;
        }

        public static string RepositoryRoot =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
    }
}
