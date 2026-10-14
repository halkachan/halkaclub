using System;
using System.Linq;
using System.IO;
using Halka.Game.Player;
using Halka.Game.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Halka.Poc2D3D.EditorTools
{
    // はるかわーるど Phase 2.1 review images: 2D outline variants, the TEMPORARY ﾊﾙｶﾁｬﾝ model.
    // Batch: -executeMethod Halka.Poc2D3D.EditorTools.Phase21Capture.CaptureBatch
    public static class Phase21Capture
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
                Capture(Environment.GetEnvironmentVariable("HARUKA_PHASE21_CAPTURE_DIR") ??
                    Path.Combine(PocSceneBuilder.RepositoryRoot, "..", "HARUKA_WORLD_ver3.0_PHASE2_1_CAPTURES", "unity"));
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
                // Outline matrix: same map, same camera centre, only density and line width change.
                Place(new Vector2Int(5, -1), FacingDirection.Down);
                foreach (var ppc in new[] { 16, 32 })
                foreach (var px in new[] { 0, 1, 2 })
                {
                    pixel.PixelsPerCell = ppc;
                    pixel.OutlinePixels = px;
                    Shot2D($"outline_{ppc}px_cell_{(px == 0 ? "off" : px + "px_line")}", 1024, 576,
                        new[] { (GridSpace3D.ToWorld(new Vector2Int(8, -3), 0.8f), "tree", 120),
                                (GridSpace3D.ToWorld(new Vector2Int(6, -3), 0.15f), "flower", 56),
                                (GridSpace3D.ToWorld(new Vector2Int(3, -4), 0.1f), "stone", 56),
                                (GridSpace3D.ToWorld(new Vector2Int(5, -1), 0.45f), "player", 80),
                                (GridSpace3D.ToWorld(new Vector2Int(-7, -4), 0.8f), "house", 140) });
                }
                pixel.PixelsPerCell = 16;
                pixel.OutlinePixels = 1;
                var colors = new[] { ("252B39", new Color32(0x25, 0x2b, 0x39, 0xff)), ("3A2A22", new Color32(0x3a, 0x2a, 0x22, 0xff)),
                    ("000000", new Color32(0, 0, 0, 0xff)) };
                foreach (var (name, color) in colors)
                {
                    pixel.OutlineColor = color;
                    Shot2D("outline_color_" + name, 1024, 576, new[] { (GridSpace3D.ToWorld(new Vector2Int(8, -3), 0.8f), "tree", 120) });
                }
                pixel.OutlineColor = colors[0].Item2;
                pixel.InnerLines = 0f;
                Shot2D("outline_inner_off", 1024, 576, new[] { (GridSpace3D.ToWorld(new Vector2Int(8, -3), 0.8f), "tree", 120),
                    (GridSpace3D.ToWorld(new Vector2Int(6, -3), 0.15f), "flower", 56) });
                pixel.InnerLines = 0.5f;
                pixel.GrassOutline = 0.35f;
                Shot2D("outline_grass_soft", 1024, 576, new[] { (GridSpace3D.ToWorld(new Vector2Int(2, -2)), "grass", 140) });
                pixel.GrassOutline = 0f;

                // Night with the outline.
                time.Override = PocPhaseOverride.Night;
                Shot2D("night_2d_outline_1px", 1024, 576, new[] { (GridSpace3D.ToWorld(new Vector2Int(8, -3), 0.8f), "tree", 120),
                    (GridSpace3D.ToWorld(new Vector2Int(5, -1), 0.45f), "player", 80) });
                Shot3D("night_3d", 1024, 576, new Vector2Int(5, -1), FacingDirection.Up);
                time.Override = PocPhaseOverride.Morning;
                Shot3D("morning_3d", 1024, 576, new Vector2Int(5, -1), FacingDirection.Up);

                // TEMPORARY ﾊﾙｶﾁｬﾝ: four directions in 3D (fixed camera from the South) and in 2D.
                foreach (FacingDirection facing in Enum.GetValues(typeof(FacingDirection)))
                {
                    var tag = facing.ToString().ToLowerInvariant();
                    Place(new Vector2Int(1, -3), facing);
                    Fixed($"placeholder_3d_{tag}", 480, 560, GridSpace3D.ToWorld(new Vector2Int(1, -3), 0.47f), 0f, 10f, 2.2f, 30f);
                    foreach (var ppc in new[] { 16, 32 })
                    {
                        pixel.PixelsPerCell = ppc;
                        Shot2D($"placeholder_2d_{tag}_{ppc}px", 780, 1688,
                            new[] { (GridSpace3D.ToWorld(new Vector2Int(1, -3), 0.75f), "closeup", 220) }, true);
                    }
                    pixel.PixelsPerCell = 16;
                }
                Place(new Vector2Int(1, -3), FacingDirection.Down);
                Fixed("placeholder_3d_three_quarter", 480, 560, GridSpace3D.ToWorld(new Vector2Int(1, -3), 0.47f), -35f, 18f, 2.2f, 30f);
                Shot3D("placeholder_3d_game_behind", 1024, 576, new Vector2Int(1, -3), FacingDirection.Up);
                Shot3D("placeholder_3d_game_facing_right", 1024, 576, new Vector2Int(1, -3), FacingDirection.Right);

                // Walk pose (limb swing) mid-step, 3D close view.
                Place(new Vector2Int(1, -3), FacingDirection.Right);
                session.Tick(0f, Vector2Int.right);
                session.Tick(0.045f, Vector2Int.zero);
                actor.Apply(0f);
                Fixed("placeholder_3d_walk_pose", 480, 560, GridSpace3D.ToWorld(new Vector2Int(1, -3), 0.47f), 0f, 10f, 2.2f, 30f, false);
                for (var i = 0; i < 12; i++) session.Tick(0.02f, Vector2Int.zero);

                // 2D original sprite: draw order next to a tree (from the South it is covered, from
                // the North it stays in front), with and without the outline.
                var southTree = session.World.Objects.First(o => o.DefinitionId == "tree_basic" &&
                    session.World.CanEnter(o.RootCell + Vector2Int.up));
                var northTree = session.World.Objects.First(o => o.DefinitionId == "tree_basic" &&
                    session.World.CanEnter(o.RootCell + Vector2Int.down));
                foreach (var px in new[] { 1, 0 })
                {
                    pixel.OutlinePixels = px;
                    pixel.FadeEnabled = false;
                    Place(southTree.RootCell + Vector2Int.up, FacingDirection.Down);
                    Shot2D($"sprite_order_behind_tree_outline{px}", 1024, 576,
                        new[] { (GridSpace3D.ToWorld(southTree.RootCell), "player", 120) }, true);
                    // See-through tree (default): let the fade settle (0.25 s), then shoot.
                    pixel.FadeEnabled = true;
                    switcher.SetMode(PocViewMode.PixelView2D);
                    for (var i = 0; i < 8; i++) Advance(0.05f, 1024, 576);
                    Shot2D($"sprite_order_behind_tree_seethrough_outline{px}", 1024, 576,
                        new[] { (GridSpace3D.ToWorld(southTree.RootCell), "player", 120) }, true);
                    Shot2D($"sprite_order_behind_tree_seethrough_full_outline{px}", 1024, 576, null, px == 0);
                    Place(northTree.RootCell + Vector2Int.down, FacingDirection.Up);
                    Shot2D($"sprite_order_in_front_of_tree_outline{px}", 1024, 576,
                        new[] { (GridSpace3D.ToWorld(northTree.RootCell), "player", 120) }, true);
                }
                pixel.OutlinePixels = 1;

                // Mobile.
                Place(new Vector2Int(5, -1), FacingDirection.Up);
                Shot2D("mobile_2d_outline_780x1688", 780, 1688, null);
                Shot3D("mobile_3d_780x1688", 780, 1688, new Vector2Int(5, -1), FacingDirection.Up);
                Debug.Log("HARUKA Phase 2.1 captures written to " + output);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

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
            var holder = new GameObject("Phase 2.1 capture holder");
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

        // Full screen image plus nearest-neighbour crops (x4) centred on world points.
        private static void Shot2D(string name, int width, int height, (Vector3 point, string label, int size)[] crops,
            bool cropsOnly = false)
        {
            switcher.SetMode(PocViewMode.PixelView2D);
            pixel.SnapToTarget();
            Advance(0f, width, height);
            pixel.WorldCamera.Render();
            // Screen-size image as the presenter draws it: upscaled view plus the player sprite.
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
                    // The player is the original sprite in 2D: centre on where it was drawn.
                    if ((label == "player" || label == "closeup") && pixel.LastSpriteRect.width > 0)
                    {
                        cx = Mathf.RoundToInt(pixel.LastSpriteRect.center.x);
                        cy = Mathf.RoundToInt(pixel.LastSpriteRect.center.y);
                    }
                    const int zoom = 4;
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

        private static void Fixed(string name, int width, int height, Vector3 target, float yaw, float pitch,
            float distance, float fov, bool advance = true)
        {
            switcher.SetMode(PocViewMode.ThirdPerson3D);
            if (advance) Advance(0f, width, height);
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
