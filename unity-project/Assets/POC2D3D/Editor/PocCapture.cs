using System;
using System.IO;
using Halka.Game.Player;
using Halka.Game.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Halka.Poc2D3D.EditorTools
{
    // Renders review images with the POC components in Edit Mode (Unity batchmode with a GPU).
    // The 2D path is the same RenderTexture + integer nearest-neighbour upscale as the build.
    public static class PocCapture
    {
        private static string output;
        private static GameObject root;
        private static SharedWorldSession session;
        private static PocWorldPresenter presenter;
        private static PocViewSwitcher switcher;
        private static PocPixelView2D pixel;
        private static PocThirdPersonCamera third;
        private static PocActorView actor;
        private static PocTimeOfDay time;
        private static float fakeNow = 1000f;

        public static void CaptureBatch()
        {
            try
            {
                CaptureAll(Environment.GetEnvironmentVariable("HALKA_POC_CAPTURE_DIR") ??
                    Path.Combine(PocSceneBuilder.RepositoryRoot, "..", "HALKA_WORLD_ver3.0_PHASE1_CAPTURES", "unity"));
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void CaptureAll(string folder)
        {
            output = Path.GetFullPath(folder);
            Directory.CreateDirectory(output);
            Setup();
            try
            {
                Place(new Vector2Int(0, 0), FacingDirection.Down);
                Shot2D("2d_spawn_1024x576", 1024, 576);
                Shot3D("3d_spawn_1024x576", 1024, 576);

                // Same logical position, both views (house, tree).
                Place(new Vector2Int(-7, -6), FacingDirection.Up);
                Shot2D("same_house_2d", 1024, 576);
                Shot3D("same_house_3d", 1024, 576);
                Place(new Vector2Int(5, -1), FacingDirection.Up);
                Shot2D("same_tree_2d", 1024, 576);
                Shot3D("same_tree_3d", 1024, 576);
                Place(new Vector2Int(-2, 1), FacingDirection.Up);
                Shot2D("same_bench_2d", 1024, 576);
                Shot3D("same_bench_3d", 1024, 576);

                // Player in four directions.
                foreach (FacingDirection facing in Enum.GetValues(typeof(FacingDirection)))
                {
                    Place(new Vector2Int(1, -3), facing);
                    Shot2D("player_2d_" + facing.ToString().ToLowerInvariant(), 1024, 576, true);
                    ShotFixed3D("player_3d_" + facing.ToString().ToLowerInvariant(), 480, 480,
                        GridSpace3D.ToWorld(new Vector2Int(1, -3), 0.45f), 0f, 18f, 1.9f);
                }
                ShotFixed3D("tree_3d_closeup", 640, 640, GridSpace3D.ToWorld(new Vector2Int(5, 1), 0.8f), 0f, 20f, 3.4f);
                ShotFixed3D("house_3d_closeup", 800, 600, GridSpace3D.ToWorld(new Vector2Int(-7, -4), 0.8f), 0f, 22f, 4.6f);

                // 3D follow while walking east then turning north.
                Place(new Vector2Int(-1, -1), FacingDirection.Right);
                switcher.SetMode(PocViewMode.ThirdPerson3D);
                third.SnapToTarget();
                var frame = 0;
                for (var step = 0; step < 6; step++)
                {
                    var direction = step < 3 ? Vector2Int.right : Vector2Int.up;
                    session.Tick(0f, direction);
                    for (var f = 0; f < 11; f++)
                    {
                        session.Tick(1f / 60f, Vector2Int.zero);
                        Advance(1f / 60f, 1024, 576);
                        if (f % 5 == 0) Save3D($"follow_3d_{frame++:00}", 1024, 576, false);
                    }
                }
                for (var f = 0; f < 30; f++) Advance(1f / 60f, 1024, 576);
                Save3D($"follow_3d_{frame:00}", 1024, 576, false);

                // Mobile portrait (390x844 CSS at devicePixelRatio 2).
                Place(new Vector2Int(0, 0), FacingDirection.Down);
                Shot2D("mobile_2d_780x1688", 780, 1688);
                Shot3D("mobile_3d_780x1688", 780, 1688);

                // 2D options.
                Place(new Vector2Int(4, -1), FacingDirection.Up);
                foreach (var ppc in new[] { 16, 24, 32 })
                {
                    pixel.PixelsPerCell = ppc;
                    Shot2D($"option_2d_density_{ppc}px", 1024, 576);
                }
                pixel.PixelsPerCell = 16;
                foreach (var pitch in new[] { 40f, 50f, 60f, 90f })
                {
                    pixel.PitchDegrees = pitch;
                    Shot2D($"option_2d_pitch_{pitch:0}", 1024, 576);
                }
                pixel.PitchDegrees = 50f;
                pixel.SquareGround = false;
                Shot2D("option_2d_pitch_50_square_off", 1024, 576);
                pixel.SquareGround = true;

                // Grid / blocked overlay in both views.
                presenter.GridVisible = true;
                Place(new Vector2Int(-5, -3), FacingDirection.Up);
                Shot2D("grid_2d", 1024, 576);
                Shot3D("grid_3d", 1024, 576);
                presenter.GridVisible = false;

                // Night.
                time.Override = PocPhaseOverride.Night;
                Place(new Vector2Int(5, -1), FacingDirection.Up);
                Shot2D("night_2d", 1024, 576);
                Shot3D("night_3d", 1024, 576);
                time.Override = PocPhaseOverride.Morning;

                // Interior (via the real door step).
                Place(new Vector2Int(-7, -5), FacingDirection.Up);
                session.Tick(0f, Vector2Int.up);
                for (var i = 0; i < 12; i++) session.Tick(0.02f, Vector2Int.zero);
                presenter.Rebuild(session.World);
                Place(new Vector2Int(2, -3), FacingDirection.Up);
                Shot2D("indoor_2d", 1024, 576);
                third.HideOccluders = false;
                Shot3D("indoor_3d_no_hiding", 1024, 576);
                third.HideOccluders = true;
                Shot3D("indoor_3d_hide_occluders", 1024, 576);
                third.IndoorMode = PocIndoorCamera.CloserOffsetAndClamp;
                Shot3D("indoor_3d_hide_and_clamp", 1024, 576);
                third.IndoorMode = PocIndoorCamera.CloserOffset;
                Place(new Vector2Int(-3, 1), FacingDirection.Left);
                Shot3D("indoor_3d_furniture_west", 1024, 576);
                Place(new Vector2Int(0, -3), FacingDirection.Down);
                Shot3D("indoor_3d_facing_exit", 1024, 576);

                // MAP EDITOR fixture: moved tree.
                var fixturePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "POC2D3DFixtures",
                    "first_field_tree_moved.hwmap.json"));
                if (File.Exists(fixturePath))
                {
                    var fixture = PocMapJson.ToDefinition(PocMapJson.Load(fixturePath));
                    session.UseOutdoorMapForTest(fixture);
                    presenter.Rebuild(session.World);
                    presenter.GridVisible = true;
                    Place(new Vector2Int(5, -2), FacingDirection.Up);
                    Shot2D("fixture_tree_moved_2d", 1024, 576);
                    Shot3D("fixture_tree_moved_3d", 1024, 576);
                    presenter.GridVisible = false;
                    UnityEngine.Object.DestroyImmediate(fixture);
                }
                Debug.Log("HALKA POC captures written to " + output);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void Setup()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var catalog = AssetDatabase.LoadAssetAtPath<PocPresentationCatalog>(PocSceneBuilder.CatalogPath);
            var screen = AssetDatabase.LoadAssetAtPath<Material>(PocSceneBuilder.ScreenMaterialPath);
            var content = PocSceneBuilder.BuildSceneContent(catalog, screen);
            session = content.GetComponent<SharedWorldSession>();
            presenter = content.GetComponent<PocWorldPresenter>();
            switcher = content.GetComponent<PocViewSwitcher>();
            time = content.GetComponent<PocTimeOfDay>();
            pixel = UnityEngine.Object.FindAnyObjectByType<PocPixelView2D>(FindObjectsInactive.Include);
            third = UnityEngine.Object.FindAnyObjectByType<PocThirdPersonCamera>(FindObjectsInactive.Include);
            actor = UnityEngine.Object.FindAnyObjectByType<PocActorView>(FindObjectsInactive.Include);
            session.Initialize();
            presenter.Rebuild(session.World);
            time.Override = PocPhaseOverride.Morning;
            root = new GameObject("POC capture holder");
            foreach (var item in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (item != root) item.transform.SetParent(root.transform, true);
        }

        private static void Place(Vector2Int cell, FacingDirection facing) => session.PlaceForCheck(cell, facing);

        private static void Advance(float dt, int width, int height)
        {
            // A fake clock past the 2.5 s fade so forced phases render fully.
            fakeNow += 10f;
            time.Apply(fakeNow);
            time.Apply(fakeNow + WorldNightOverlay2D.FadeSeconds + 0.1f);
            actor.Apply(dt);
            if (switcher.Mode == PocViewMode.PixelView2D) pixel.UpdateView(dt, width, height);
            else
            {
                third.ViewCamera.aspect = width / (float)height;
                third.UpdateView(dt);
            }
            actor.Apply(dt);
        }

        private static void Shot2D(string name, int width, int height, bool playerCrop = false)
        {
            switcher.SetMode(PocViewMode.PixelView2D);
            pixel.SnapToTarget();
            Advance(0f, width, height);
            pixel.WorldCamera.Render();
            var rt = pixel.ComposeOutline();
            var small = Read(rt);
            var scale = pixel.Scale;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            var src = small.GetPixels32();
            var dst = new Color32[width * height];
            var alpha = time.PixelOverlayAlpha;
            var night = (Color32)PocTimeOfDay.NightColor;
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var sx = (x + pixel.Offset.x) / scale;
                var sy = (y + pixel.Offset.y) / scale;
                var c = src[sy * rt.width + sx];
                if (alpha > 0f) c = Color32.Lerp(c, night, alpha);
                dst[y * width + x] = c;
            }
            image.SetPixels32(dst);
            Write(name, image);
            if (playerCrop) WritePlayerCrop(name + "_closeup", dst, width, height, scale);
            UnityEngine.Object.DestroyImmediate(small);
            UnityEngine.Object.DestroyImmediate(image);
        }

        // 64x64 screen pixels around the player's feet, enlarged x4 with nearest neighbour.
        private static void WritePlayerCrop(string name, Color32[] screen, int width, int height, int scale)
        {
            var rtPoint = pixel.WorldCamera.WorldToScreenPoint(actor.transform.position + Vector3.up * 0.35f);
            var cx = Mathf.RoundToInt(rtPoint.x * scale - pixel.Offset.x);
            var cy = Mathf.RoundToInt(rtPoint.y * scale - pixel.Offset.y);
            const int size = 64, zoom = 4;
            var crop = new Texture2D(size * zoom, size * zoom, TextureFormat.RGB24, false);
            var pixels = new Color32[size * zoom * size * zoom];
            for (var y = 0; y < size * zoom; y++)
            for (var x = 0; x < size * zoom; x++)
            {
                var sx = Mathf.Clamp(cx - size / 2 + x / zoom, 0, width - 1);
                var sy = Mathf.Clamp(cy - size / 2 + y / zoom, 0, height - 1);
                pixels[y * size * zoom + x] = screen[sy * width + sx];
            }
            crop.SetPixels32(pixels);
            Write(name, crop);
            UnityEngine.Object.DestroyImmediate(crop);
        }

        private static void Shot3D(string name, int width, int height)
        {
            switcher.SetMode(PocViewMode.ThirdPerson3D);
            third.SnapToTarget();
            Advance(0f, width, height);
            for (var i = 0; i < 3; i++) Advance(1f / 60f, width, height);
            Save3D(name, width, height, true);
        }

        private static void Save3D(string name, int width, int height, bool resetAspect)
        {
            var camera = third.ViewCamera;
            var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = rt;
            camera.aspect = width / (float)height;
            camera.Render();
            camera.targetTexture = null;
            var image = Read(rt);
            RenderTexture.ReleaseTemporary(rt);
            if (resetAspect) camera.ResetAspect();
            Write(name, image);
            UnityEngine.Object.DestroyImmediate(image);
        }

        // A fixed camera that looks North at a target, for turnarounds and close-ups.
        private static void ShotFixed3D(string name, int width, int height, Vector3 target, float yaw, float pitch, float distance)
        {
            switcher.SetMode(PocViewMode.ThirdPerson3D);
            Advance(0f, width, height);
            var camera = third.ViewCamera;
            var rotation = Quaternion.Euler(pitch, yaw, 0f);
            camera.transform.SetPositionAndRotation(target - rotation * Vector3.forward * distance, rotation);
            camera.fieldOfView = 30f;
            foreach (var view in presenter.Views) view.SetHidden(false);
            Save3D(name, width, height, true);
        }

        private static Texture2D Read(RenderTexture rt)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            return texture;
        }

        private static void Write(string name, Texture2D texture) =>
            File.WriteAllBytes(Path.Combine(output, name + ".png"), texture.EncodeToPNG());
    }
}
