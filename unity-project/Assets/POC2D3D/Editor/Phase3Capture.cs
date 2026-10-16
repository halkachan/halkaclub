using System;
using System.IO;
using System.Linq;
using Halka.Game.Player;
using Halka.Game.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Halka.Poc2D3D.EditorTools
{
    // はるかわーるど Phase 3 review images: the Blender house, outdoor fixtures, interior and
    // furniture in the 2D Pixel View and the 3D view, the interior camera cutaway, night.
    // Batch: -executeMethod Halka.Poc2D3D.EditorTools.Phase3Capture.CaptureBatch
    public static class Phase3Capture
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

        // (label, cell centre of the drawn object, height of its middle, 2D crop size, 3D distance)
        private static readonly (string label, Vector2 cell, float height, int crop, float distance)[] Outdoor =
        {
            ("house", new Vector2(-7f, -3.5f), 0.8f, 200, 4.2f),
            ("bench", new Vector2(-1.5f, 2f), 0.25f, 96, 1.9f),
            ("sign", new Vector2(3f, 1f), 0.3f, 72, 1.5f),
            ("well", new Vector2(-2f, -2f), 0.12f, 72, 1.4f)
        };

        private static readonly (string label, Vector2 cell, float height, int crop, float distance)[] Furniture =
        {
            ("bed", new Vector2(-4.5f, 1f), 0.3f, 120, 2.6f),
            ("desk", new Vector2(2.5f, 1f), 0.35f, 96, 1.9f),
            ("cushion", new Vector2(0f, 0f), 0.06f, 64, 1.2f),
            ("recording_mic", new Vector2(4f, -1f), 0.55f, 96, 2.0f),
            ("art_easel", new Vector2(-2.5f, -1.5f), 0.55f, 104, 2.2f),
            ("studio_shelf", new Vector2(-0.5f, 2f), 0.3f, 96, 1.9f)
        };

        public static void CaptureBatch()
        {
            try
            {
                Capture(Environment.GetEnvironmentVariable("HARUKA_PHASE3_CAPTURE_DIR") ??
                    Path.Combine(PocSceneBuilder.RepositoryRoot, "..", "HARUKA_WORLD_ver3.0_PHASE3_CAPTURES", "unity"));
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
                time.Override = PocPhaseOverride.Morning;
                pixel.PixelsPerCell = 16;
                pixel.OutlinePixels = 1;

                // ---- Outdoor: full views and every fixture in 2D and 3D.
                Place(new Vector2Int(-3, -1), FacingDirection.Down);
                Shot2D("outdoor_2d_full", 1280, 720, Outdoor.Select(o => (Point(o.cell, o.height), o.label, o.crop)).ToArray());
                Fixed("outdoor_3d_overview", 1280, 720, GridSpace3D.ToWorld(new Vector2(-3.5f, -1f)), 0f, 42f, 8.5f, 45f);
                foreach (var o in Outdoor)
                    Fixed("outdoor_3d_" + o.label, 800, 600, Point(o.cell, o.height), -25f, 24f, o.distance, 40f);
                Fixed("house_3d_front", 1000, 750, Point(new Vector2(-7f, -3.6f), 0.75f), 0f, 12f, 4.4f, 40f);
                Fixed("house_3d_side", 1000, 750, Point(new Vector2(-7f, -3.5f), 0.75f), 90f, 15f, 4.4f, 40f);
                Fixed("house_3d_back", 1000, 750, Point(new Vector2(-7f, -3.5f), 0.75f), 180f, 15f, 4.4f, 40f);
                Fixed("house_3d_top", 1000, 750, Point(new Vector2(-7f, -3.5f), 0.4f), 0f, 85f, 4.4f, 40f);

                // ---- The entrance in the game camera: approaching, in the alcove, behind the house.
                Shot3D("house_3d_game_approach", 1280, 720, new Vector2Int(-7, -6), FacingDirection.Up);
                Shot3D("house_3d_game_in_alcove", 1280, 720, new Vector2Int(-7, -4), FacingDirection.Up);
                Shot3D("house_3d_game_behind_house", 1280, 720, new Vector2Int(-7, -2), FacingDirection.Up);
                Place(new Vector2Int(-7, -4), FacingDirection.Up);
                Shot2D("house_2d_in_alcove", 1280, 720, new[] { (Point(new Vector2(-7f, -3.5f), 0.8f), "house", 200) },
                    true);

                // ---- Interior.
                session.LoadMapForCheck(true, new Vector2Int(0, -3), FacingDirection.Up);
                presenter.Rebuild(session.World);
                Shot2D("indoor_2d_full", 1280, 720, Furniture.Select(o => (Point(o.cell, o.height), o.label, o.crop)).ToArray());
                Shot3D("indoor_3d_entry", 1280, 720, new Vector2Int(0, -3), FacingDirection.Up);
                foreach (var (name, cell, facing) in new[]
                         {
                             ("indoor_3d_walk_north", new Vector2Int(0, 1), FacingDirection.Up),
                             ("indoor_3d_walk_west", new Vector2Int(-2, 0), FacingDirection.Left),
                             ("indoor_3d_walk_east", new Vector2Int(3, -2), FacingDirection.Right),
                             ("indoor_3d_walk_south", new Vector2Int(0, -2), FacingDirection.Down),
                             ("indoor_3d_by_bed", new Vector2Int(-5, -1), FacingDirection.Up),
                             ("indoor_3d_by_desk", new Vector2Int(2, 0), FacingDirection.Up)
                         })
                    Shot3D(name, 1280, 720, cell, facing);
                // Occlusion comparison at the entry: no hiding, rays only (Phase 1/2), cutaway (Phase 3).
                foreach (var (mode, hide, cutaway) in new[] { ("none", false, false), ("rays", true, false), ("cutaway", true, true) })
                {
                    third.HideOccluders = hide;
                    third.WallCutaway = cutaway;
                    Shot3D("indoor_3d_occlusion_" + mode, 1280, 720, new Vector2Int(0, -3), FacingDirection.Up);
                }
                third.HideOccluders = true;
                third.WallCutaway = true;
                foreach (var o in Furniture)
                    Fixed("indoor_3d_" + o.label, 800, 600, Point(o.cell, o.height), -25f, 26f, o.distance, 40f, true);
                Fixed("indoor_3d_overview", 1280, 720, GridSpace3D.ToWorld(new Vector2(0f, -0.5f)), 0f, 55f, 7.5f, 45f, true);

                // ---- Night.
                time.Override = PocPhaseOverride.Night;
                Place(new Vector2Int(0, -1), FacingDirection.Up);
                Shot2D("indoor_2d_night", 1280, 720, null);
                Shot3D("indoor_3d_night", 1280, 720, new Vector2Int(0, -1), FacingDirection.Up);
                time.Override = PocPhaseOverride.Morning;
                Shot3D("indoor_3d_morning", 1280, 720, new Vector2Int(0, -1), FacingDirection.Up);

                // ---- Mobile (portrait, phone-sized).
                Place(new Vector2Int(0, -2), FacingDirection.Up);
                Shot2D("mobile_indoor_2d_780x1688", 780, 1688, null);
                Shot3D("mobile_indoor_3d_780x1688", 780, 1688, new Vector2Int(0, -2), FacingDirection.Up);
                session.LoadMapForCheck(false, new Vector2Int(-7, -5), FacingDirection.Up);
                presenter.Rebuild(session.World);
                Shot2D("mobile_outdoor_2d_780x1688", 780, 1688, null);
                Shot3D("mobile_outdoor_3d_780x1688", 780, 1688, new Vector2Int(-7, -5), FacingDirection.Up);
                time.Override = PocPhaseOverride.Night;
                Shot2D("outdoor_2d_night", 1280, 720, null);
                Shot3D("outdoor_3d_night_house", 1280, 720, new Vector2Int(-7, -6), FacingDirection.Up);
                time.Override = PocPhaseOverride.Morning;
                Debug.Log("HARUKA Phase 3 captures written to " + output);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Vector3 Point(Vector2 cell, float height) => GridSpace3D.ToWorld(cell, height);

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
            var holder = new GameObject("Phase 3 capture holder");
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
                third.UpdateView(dt, 1e6f);
            }
            actor.Apply(dt);
        }

        private static void Shot2D(string name, int width, int height, (Vector3 point, string label, int size)[] crops,
            bool cropsOnly = false)
        {
            switcher.SetMode(PocViewMode.PixelView2D);
            pixel.SnapToTarget();
            Advance(0f, width, height);
            for (var i = 0; i < 8; i++) Advance(0.05f, width, height); // let a tree see-through fade settle
            pixel.WorldCamera.Render();
            var small = Read(pixel.ComposeScreen(width, height));
            var dst = small.GetPixels32();
            var alpha = time.PixelOverlayAlpha;
            var night = (Color32)PocTimeOfDay.NightColor;
            if (alpha > 0f)
                for (var i = 0; i < dst.Length; i++) dst[i] = Color32.Lerp(dst[i], night, alpha);
            if (!cropsOnly) WritePixels(name, dst, width, height);
            if (crops != null)
                foreach (var (point, label, size) in crops)
                {
                    var p = pixel.WorldCamera.WorldToScreenPoint(point);
                    var cx = Mathf.RoundToInt(p.x * pixel.Scale - pixel.Offset.x);
                    var cy = Mathf.RoundToInt(p.y * pixel.Scale - pixel.Offset.y);
                    const int zoom = 3;
                    var crop = new Color32[size * zoom * size * zoom];
                    for (var y = 0; y < size * zoom; y++)
                    for (var x = 0; x < size * zoom; x++)
                    {
                        var sx = Mathf.Clamp(cx - size / 2 + x / zoom, 0, width - 1);
                        var sy = Mathf.Clamp(cy - size / 2 + y / zoom, 0, height - 1);
                        crop[y * size * zoom + x] = dst[sy * width + sx];
                    }
                    WritePixels(name + "_crop_" + label, crop, size * zoom, size * zoom);
                }
            UnityEngine.Object.DestroyImmediate(small);
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

        // A fixed showcase camera. Interior walls facing it are cut like in the game.
        private static void Fixed(string name, int width, int height, Vector3 target, float yaw, float pitch,
            float distance, float fov, bool cutaway = false)
        {
            switcher.SetMode(PocViewMode.ThirdPerson3D);
            Advance(0f, width, height);
            var camera = third.ViewCamera;
            var rotation = Quaternion.Euler(pitch, yaw, 0f);
            camera.transform.SetPositionAndRotation(target - rotation * Vector3.forward * distance, rotation);
            camera.fieldOfView = fov;
            var forward = camera.transform.forward;
            var toward = -new Vector2(forward.x, forward.z).normalized;
            foreach (var view in presenter.Views)
            {
                view.SetHidden(false);
                view.SetCutaway(cutaway && view.IsWall && Vector2.Dot(view.WallOutward, toward) > PocThirdPersonCamera.CutawayFacing);
            }
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
            File.WriteAllBytes(Path.Combine(output, name + ".png"), image.EncodeToPNG());
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

        private static void WritePixels(string name, Color32[] pixels, int width, int height)
        {
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.SetPixels32(pixels);
            File.WriteAllBytes(Path.Combine(output, name + ".png"), image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
        }
    }
}
