using System;
using System.IO;
using Halka.Game.Player;
using Halka.Game.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Halka.Poc2D3D.EditorTools
{
    // はるかわーるど Phase 2 review images rendered with the POC components in Edit Mode.
    // Batch: -executeMethod Halka.Poc2D3D.EditorTools.Phase2Capture.CaptureBatch
    public static class Phase2Capture
    {
        private static string output;
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
                Capture(Environment.GetEnvironmentVariable("HARUKA_PHASE2_CAPTURE_DIR") ??
                    Path.Combine(PocSceneBuilder.RepositoryRoot, "..", "HARUKA_WORLD_ver3.0_PHASE2_CAPTURES", "unity"));
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void Capture(string folder)
        {
            output = Path.GetFullPath(folder);
            Directory.CreateDirectory(output);
            var root = Setup();
            try
            {
                foreach (var phase in new[] { PocPhaseOverride.Morning, PocPhaseOverride.Night })
                {
                    time.Override = phase;
                    var tag = phase == PocPhaseOverride.Morning ? "morning" : "night";
                    Place(new Vector2Int(0, 0), FacingDirection.Down);
                    foreach (var ppc in new[] { 16, 32 })
                    {
                        pixel.PixelsPerCell = ppc;
                        Shot2D($"map_2d_{ppc}px_{tag}", 1024, 576);
                    }
                    pixel.PixelsPerCell = 16;
                    Fixed($"map_3d_overview_{tag}", 1280, 720, GridSpace3D.ToWorld(new Vector2Int(0, -1)), 0f, 52f, 11.5f, 40f);
                    foreach (var (name, cell, facing) in Spots)
                        Shot3D($"third_person_{name}_{tag}", 1024, 576, cell, facing);
                }
                time.Override = PocPhaseOverride.Morning;
                Fixed("map_3d_top_roads", 1280, 720, GridSpace3D.ToWorld(new Vector2Int(0, 0)), 0f, 89f, 15f, 40f);
                Fixed("closeup_tree_3d", 720, 720, GridSpace3D.ToWorld(new Vector2Int(8, -3), 0.85f), 0f, 14f, 4.2f, 30f);
                Fixed("closeup_flower_3d", 720, 720, GridSpace3D.ToWorld(new Vector2Int(6, -3), 0.2f), 0f, 26f, 1.5f, 30f);
                Fixed("closeup_stone_3d", 720, 720, GridSpace3D.ToWorld(new Vector2Int(3, -4), 0.12f), 0f, 24f, 1.4f, 30f);
                Fixed("closeup_road_crossing_3d", 1024, 576, GridSpace3D.ToWorld(new Vector2Int(0, -1)), 20f, 48f, 3.6f, 40f);
                Fixed("closeup_grass_road_edge_3d", 1024, 576, GridSpace3D.ToWorld(new Vector2Int(-4, -5)), 10f, 42f, 2.2f, 40f);
                Fixed("closeup_grass_field_3d", 1024, 576, GridSpace3D.ToWorld(new Vector2Int(-8, 3)), 10f, 30f, 2.6f, 40f);

                Place(new Vector2Int(0, 0), FacingDirection.Down);
                Shot2D("mobile_2d_780x1688", 780, 1688);
                pixel.PixelsPerCell = 32;
                Shot2D("mobile_2d_32px_780x1688", 780, 1688);
                pixel.PixelsPerCell = 16;
                Shot3D("mobile_3d_780x1688", 780, 1688, new Vector2Int(5, -1), FacingDirection.Up);

                presenter.GridVisible = true;
                Place(new Vector2Int(5, -1), FacingDirection.Up);
                Shot2D("grid_2d_nature", 1024, 576);
                Shot3D("grid_3d_nature", 1024, 576, new Vector2Int(5, -1), FacingDirection.Up);
                presenter.GridVisible = false;

                var fixturePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "POC2D3DFixtures",
                    "first_field_nature_moved.hwmap.json"));
                var fixture = PocMapJson.ToDefinition(PocMapJson.Load(fixturePath));
                session.UseOutdoorMapForTest(fixture);
                presenter.Rebuild(session.World);
                presenter.GridVisible = true;
                Place(new Vector2Int(0, 0), FacingDirection.Down);
                Shot2D("fixture_nature_moved_2d", 1024, 576);
                Fixed("fixture_nature_moved_3d", 1280, 720, GridSpace3D.ToWorld(new Vector2Int(7, 1)), 0f, 55f, 5.5f, 40f);
                presenter.GridVisible = false;
                UnityEngine.Object.DestroyImmediate(fixture);
                Debug.Log("HARUKA Phase 2 captures written to " + output);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static readonly (string, Vector2Int, FacingDirection)[] Spots =
        {
            ("tree", new Vector2Int(8, -4), FacingDirection.Up),
            ("flower", new Vector2Int(6, -4), FacingDirection.Up),
            ("stone", new Vector2Int(3, -5), FacingDirection.Up),
            ("crossing", new Vector2Int(0, -3), FacingDirection.Up),
            ("spawn", new Vector2Int(0, 0), FacingDirection.Down)
        };

        private static GameObject Setup()
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
            var holder = new GameObject("Phase 2 capture holder");
            foreach (var item in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (item != holder) item.transform.SetParent(holder.transform, true);
            return holder;
        }

        private static void Place(Vector2Int cell, FacingDirection facing) => session.PlaceForCheck(cell, facing);

        private static void Advance(float dt, int width, int height)
        {
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

        private static void Shot2D(string name, int width, int height)
        {
            switcher.SetMode(PocViewMode.PixelView2D);
            pixel.SnapToTarget();
            Advance(0f, width, height);
            pixel.WorldCamera.Render();
            var rt = pixel.Target;
            var small = Read(rt);
            var src = small.GetPixels32();
            var dst = new Color32[width * height];
            var alpha = time.PixelOverlayAlpha;
            var night = (Color32)PocTimeOfDay.NightColor;
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var c = src[(y + pixel.Offset.y) / pixel.Scale * rt.width + (x + pixel.Offset.x) / pixel.Scale];
                dst[y * width + x] = alpha > 0f ? Color32.Lerp(c, night, alpha) : c;
            }
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.SetPixels32(dst);
            Write(name, image);
            UnityEngine.Object.DestroyImmediate(small);
            UnityEngine.Object.DestroyImmediate(image);
        }

        private static void Shot3D(string name, int width, int height, Vector2Int cell, FacingDirection facing)
        {
            Place(cell, facing);
            switcher.SetMode(PocViewMode.ThirdPerson3D);
            third.SnapToTarget();
            Advance(0f, width, height);
            for (var i = 0; i < 3; i++) Advance(1f / 60f, width, height);
            Save3D(name, width, height);
        }

        private static void Fixed(string name, int width, int height, Vector3 target, float yaw, float pitch,
            float distance, float fov)
        {
            switcher.SetMode(PocViewMode.ThirdPerson3D);
            Advance(0f, width, height);
            foreach (var view in presenter.Views) view.SetHidden(false);
            var camera = third.ViewCamera;
            var rotation = Quaternion.Euler(pitch, yaw, 0f);
            camera.transform.SetPositionAndRotation(target - rotation * Vector3.forward * distance, rotation);
            camera.fieldOfView = fov;
            Save3D(name, width, height);
        }

        private static void Save3D(string name, int width, int height)
        {
            var camera = third.ViewCamera;
            var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = rt;
            camera.aspect = width / (float)height;
            camera.Render();
            camera.targetTexture = null;
            var image = Read(rt);
            RenderTexture.ReleaseTemporary(rt);
            camera.ResetAspect();
            Write(name, image);
            UnityEngine.Object.DestroyImmediate(image);
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
