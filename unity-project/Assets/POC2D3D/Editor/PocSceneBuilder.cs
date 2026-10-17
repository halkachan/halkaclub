using System;
using System.Collections.Generic;
using System.IO;
using Halka.Game.Input;
using Halka.Game.Interaction;
using Halka.Game.Player;
using Halka.Game.UI;
using Halka.Game.World;
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
        public const string RcScenePath = Root + "/Scenes/HARUKA_WORLD_ver3.0_RC1.unity";
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
        // Phase 2.1: TEMPORARY ﾊﾙｶﾁｬﾝ stand-in for the 3D view until the user's official model
        // arrives: Minecraft-like blocks painted after the 2D sprites (the first, rounded
        // low-poly stand-in haruka_placeholder.fbx is kept for comparison only).
        public const string PlaceholderPlayerPath = Root + "/Models/Phase2_1/haruka_blocky_placeholder.fbx";
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
        public const string CushionSitPath = "Assets/POC2D3D/Provisional/cushion_sit_provisional.png";
        public const string TreeFadeMaterialPath = Root + "/Materials/POC_ToonVertexColorFade.mat";
        public const string OutdoorMapPath = "Assets/Content/Maps/first_field.asset";
        public const string InteriorMapPath = "Assets/Content/Maps/halka_house.asset";
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
            EnsureMaterial(TreeFadeMaterialPath, "HALKA/POC/ToonVertexColorFade", null);
            EnsureMaterial(OutlineMaterialPath, "HALKA/POC/OutlineComposite", null);
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
                new PocPresentationBinding { definitionId = "recording_mic_basic", model = Phase3("recording_mic_basic") },
                new PocPresentationBinding { definitionId = "art_easel_basic", model = Phase3("art_easel_basic") },
                new PocPresentationBinding { definitionId = "studio_shelf_basic", model = Phase3("studio_shelf_basic"),
                    cameraOccluder = false }
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
            sprites.cushionSit = AssetDatabase.LoadAssetAtPath<Sprite>(CushionSitPath);
            if (sprites.cushionSit == null) throw new InvalidOperationException("Sprite missing: " + CushionSitPath);
            catalog.ConfigurePlayer(new PocActorPresentation
            {
                model = LoadModel(PlaceholderPlayerPath),
                placeholder = true,
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

        // Phase 5: the same world and game; only the launch differs. 2D at start, the debug panel
        // off unless ?debug=1, the HUD reads "ver3.0 RC1".
        private static void ConfigureReleaseCandidate(GameObject root)
        {
            root.name = "HARUKA WORLD ver3.0 RC1";
            var panel = root.GetComponent<PocDebugPanel>();
            var hud = root.GetComponentInChildren<GameHud>(true);
            var switcher = root.GetComponent<PocViewSwitcher>();
            if (panel == null || hud == null || switcher == null) throw new InvalidOperationException("RC scene parts are missing");
            panel.ConfigureReleaseCandidate(true);
            hud.SetVersionLabel(PocDebugPanel.ReleaseCandidateVersionLabel);
            EditorUtility.SetDirty(panel);
            EditorUtility.SetDirty(hud);
        }

        public static GameObject BuildSceneContent(PocPresentationCatalog catalog, Material screenOverlay)
        {
            var outdoor = AssetDatabase.LoadAssetAtPath<MapDefinition>(OutdoorMapPath);
            var interior = AssetDatabase.LoadAssetAtPath<MapDefinition>(InteriorMapPath);
            var font = AssetDatabase.LoadAssetAtPath<Font>(MessageFontPath);
            if (outdoor == null || interior == null) throw new InvalidOperationException("Official map caches are missing");
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
            thirdCamera.enabled = false;
            thirdPerson.enabled = false;

            session.Configure(outdoor, interior, input);
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
            BuildPhase5Content(root, gameplay, pixelView);
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
