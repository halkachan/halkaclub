using System;
using System.IO;
using System.Reflection;
using Halka.Game.Player;
using Halka.Game.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Halka.Poc2D3D.EditorTools
{
    // はるかわーるど Phase 5 review images: the 2D grass and feet (original grass sprites vs the
    // Phase 4 3D tufts), seats, crow perches, and RC views. Same harness as Phase4Capture.
    // Batch: -executeMethod Halka.Poc2D3D.EditorTools.Phase5Capture.CaptureBatch
    public static class Phase5Capture
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private static string output;
        private static SharedWorldSession session;
        private static PocWorldPresenter presenter;
        private static PocViewSwitcher switcher;
        private static PocPixelView2D pixel;
        private static PocThirdPersonCamera third;
        private static PocActorView actor;
        private static PocTimeOfDay time;
        private static PocGameplay gameplay;
        private static PocCrow crow;
        private static PocCrowView3D crowView;
        private static PocInput input;
        private static float fakeNow = 1000f;

        public static void CaptureBatch()
        {
            try
            {
                Capture(Environment.GetEnvironmentVariable("HARUKA_PHASE5_CAPTURE_DIR") ??
                    Path.Combine(PocSceneBuilder.RepositoryRoot, "..", "HARUKA_WORLD_ver3.0_PHASE5_CAPTURES", "unity"));
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        // After RC1: the line boil, a few steps of it in 2D and 3D, outdoors and indoors.
        // Batch: -executeMethod Halka.Poc2D3D.EditorTools.Phase5Capture.CaptureLineBoilBatch
        public static void CaptureLineBoilBatch()
        {
            try
            {
                output = Path.GetFullPath(Environment.GetEnvironmentVariable("HARUKA_PHASE5_CAPTURE_DIR") ??
                    Path.Combine(PocSceneBuilder.RepositoryRoot, "..", "HARUKA_WORLD_ver3.0_PHASE5_CAPTURES", "unity", "line_boil"));
                Directory.CreateDirectory(output);
                var randomState = UnityEngine.Random.state;
                UnityEngine.Random.InitState(4);
                var root = Setup();
                try
                {
                    time.Override = PocPhaseOverride.Morning;
                    const int w = 1280, h = 720;
                    foreach (var interior in new[] { false, true })
                    {
                        var cell = interior ? new Vector2Int(2, 0) : new Vector2Int(-1, 0);
                        if (interior) ToInterior(cell);
                        else ToOutdoor(cell, FacingDirection.Up);
                        var place = interior ? "in" : "out";
                        foreach (var view in new[] { PocViewMode.PixelView2D, PocViewMode.ThirdPerson3D })
                        {
                            var tag = (view == PocViewMode.PixelView2D ? "2d_" : "3d_") + place;
                            if (view == PocViewMode.ThirdPerson3D)
                            {
                                switcher.SetMode(PocViewMode.ThirdPerson3D);
                                third.SnapToTarget();
                                Settle3D(w, h, 0.5f);
                            }
                            BoilShot(tag + "_still", view, PocLineBoilMode.Off, 0, w, h);
                            for (var step = 1; step <= 8; step++)
                                BoilShot($"{tag}_s{step}", view, PocLineBoilMode.Lines, step, w, h);
                            BoilShot(tag + "_strong_s1", view, PocLineBoilMode.LinesStrong, 1, w, h);
                            BoilShot(tag + "_strong_s2", view, PocLineBoilMode.LinesStrong, 2, w, h);
                            BoilShot(tag + "_picture_s1", view, PocLineBoilMode.LinesAndPicture, 1, w, h);
                            BoilShot(tag + "_picture_s2", view, PocLineBoilMode.LinesAndPicture, 2, w, h);
                        }
                    }
                    Debug.Log("HARUKA line boil captures written to " + output);
                }
                finally
                {
                    PocLineBoil.Apply(PocLineBoilMode.Off, 0);
                    UnityEngine.Random.state = randomState;
                    UnityEngine.Object.DestroyImmediate(root);
                }
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void BoilShot(string name, PocViewMode view, PocLineBoilMode mode, int step, int width, int height)
        {
            PocLineBoil.Apply(mode, step);
            if (view == PocViewMode.PixelView2D)
            {
                Shot2D(name, width, height);
                return;
            }
            var camera = third.ViewCamera;
            var src = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            var dst = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            camera.targetTexture = src;
            camera.aspect = width / (float)height;
            camera.Render();
            camera.targetTexture = null;
            third.Outline.Render(src, dst);
            var image = Read(dst);
            RenderTexture.ReleaseTemporary(src);
            RenderTexture.ReleaseTemporary(dst);
            camera.ResetAspect();
            File.WriteAllBytes(Path.Combine(output, name + ".png"), image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
        }

        public static void Capture(string folder)
        {
            output = Path.GetFullPath(folder);
            Directory.CreateDirectory(output);
            var randomState = UnityEngine.Random.state;
            UnityEngine.Random.InitState(4);
            var root = Setup();
            try
            {
                foreach (var night in new[] { false, true })
                {
                    time.Override = night ? PocPhaseOverride.Night : PocPhaseOverride.Morning;
                    var suffix = night ? "_night" : "";
                    Grass2D(suffix);
                    Seats(suffix);
                    CrowPerch(suffix);
                }
                time.Override = PocPhaseOverride.Morning;
                Debug.Log("HARUKA Phase 5 captures written to " + output);
            }
            finally
            {
                UnityEngine.Random.state = randomState;
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        // ---------------------------------------------------------------- 2D grass and feet

        private static void Grass2D(string suffix)
        {
            ToOutdoor(new Vector2Int(0, 0), FacingDirection.Down);
            var feet = FindGrassPair();
            Debug.Log($"HARUKA Phase 5 feet capture: player {feet} crow {feet + Vector2Int.right}");
            foreach (var original in new[] { false, true })
            {
                pixel.OriginalGrass2D = original;
                var tag = original ? "orig2d_option" : "rc_default";
                PlaceCrow(new Vector2Int(3, -1));
                session.PlaceForCheck(new Vector2Int(0, 0), FacingDirection.Down);
                Shot2D($"grass2d_{tag}_start{suffix}", 1280, 720);
                // Feet: on a grass cell (feet hidden) next to the crow on grass.
                PlaceCrow(feet + Vector2Int.right);
                session.PlaceForCheck(feet, FacingDirection.Right);
                Shot2D($"grass2d_{tag}_feet_on_grass{suffix}", 1280, 720);
                Crop($"grass2d_{tag}_feet_on_grass{suffix}", 640 - 40, 360 - 120, 260, 180);
                session.PlaceForCheck(new Vector2Int(0, 0), FacingDirection.Down);
            }
            pixel.OriginalGrass2D = false; // RC default (Phase 4 look)
        }

        // The nearest walkable grass cell (to the start) whose right neighbour is walkable grass too.
        private static Vector2Int FindGrassPair()
        {
            var best = new Vector2Int(2, -1);
            var bestDistance = int.MaxValue;
            for (var y = -6; y <= 6; y++)
            for (var x = -6; x <= 6; x++)
            {
                var cell = new Vector2Int(x, y);
                var right = cell + Vector2Int.right;
                if (cell == Vector2Int.zero || !pixel.HasGrass(cell) || !pixel.HasGrass(right)) continue;
                if (!session.World.CanEnter(cell) || !session.World.CanEnter(right)) continue;
                var distance = Mathf.Abs(x) + Mathf.Abs(y);
                if (distance < bestDistance) { bestDistance = distance; best = cell; }
            }
            return best;
        }

        private static void PlaceCrow(Vector2Int cell)
        {
            crow.gameObject.SetActive(true);
            var now = 3000f;
            typeof(CrowBehaviourCore).GetField("nextFlightAt", Hidden).SetValue(crow, now + 1000f);
            typeof(CrowBehaviourCore).GetField("nextActionAt", Hidden).SetValue(crow, now + 1000f);
            typeof(CrowBehaviourCore).GetField("motion", Hidden).SetValue(crow, new GridStepMotion(cell));
            crow.transform.position = GridSpace3D.ToWorld(cell);
            crow.Tick(now);
        }

        // ---------------------------------------------------------------- seats

        private static void Seats(string suffix)
        {
            ToOutdoor(new Vector2Int(-2, 1), FacingDirection.Up);
            foreach (var mode in new[] { PocViewMode.PixelView2D, PocViewMode.ThirdPerson3D })
            foreach (var left in new[] { true, false })
            {
                var cell = new Vector2Int(left ? -2 : -1, 1);
                switcher.SetMode(mode);
                session.PlaceForCheck(cell, FacingDirection.Up);
                session.InteractAhead();
                var name = $"bench_{(left ? "left" : "right")}_{(mode == PocViewMode.PixelView2D ? "2d" : "3d")}{suffix}";
                if (mode == PocViewMode.PixelView2D) Shot2D(name, 1280, 720);
                else
                {
                    third.SnapToTarget();
                    Settle3D(1280, 720, 0.5f);
                    Save3D(name, 1280, 720);
                    SeatClose(name + "_close");
                }
                session.Stand();
            }
            // RC1 feedback: the sofa (with the cushion on it) at (4,2), sat on from (4,1).
            ToInterior(new Vector2Int(4, 1));
            foreach (var mode in new[] { PocViewMode.PixelView2D, PocViewMode.ThirdPerson3D })
            {
                switcher.SetMode(mode);
                session.PlaceForCheck(new Vector2Int(4, 1), FacingDirection.Up);
                session.InteractAhead();
                var name = $"sofa_{(mode == PocViewMode.PixelView2D ? "2d" : "3d")}{suffix}";
                if (mode == PocViewMode.PixelView2D) Shot2D(name, 1280, 720);
                else
                {
                    third.SnapToTarget();
                    Settle3D(1280, 720, 0.5f);
                    Save3D(name, 1280, 720);
                    SeatClose(name + "_close");
                }
                session.Stand();
            }
        }

        // A close side view of the seated model (seat anchor check), not the game camera.
        private static void SeatClose(string name)
        {
            SeatProbe(name);
            var camera = third.ViewCamera;
            var target = actor.transform.position + Vector3.up * 0.35f;
            var rotation = Quaternion.Euler(12f, -60f, 0f);
            camera.transform.SetPositionAndRotation(target - rotation * Vector3.forward * 2.2f, rotation);
            camera.fieldOfView = 35f;
            Save3D(name, 800, 600);
        }

        // Logs the seat furniture parts and the seated model parts (world bounds) for the seat review.
        private static void SeatProbe(string name)
        {
            var log = new System.Text.StringBuilder($"HARUKA seat probe {name}: seat point {actor.SeatPoint:F3} actor {actor.transform.position:F3} hip {actor.HipHeight:F3}\n");
            var view = presenter.FindView(session.SeatObject.InstanceId);
            foreach (var t in view.GetComponentsInChildren<Transform>(true))
            {
                var r = t.GetComponent<Renderer>();
                log.Append($"  furniture {t.name} pos {t.position:F3}");
                if (r != null) log.Append($" bounds min {r.bounds.min:F3} max {r.bounds.max:F3}");
                var filter = t.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null && filter.sharedMesh.isReadable)
                {
                    // Seat surface: the highest vertex under 0.35 m near the seat point; backrest
                    // front: the smallest z of vertices above 0.3 m near the seat point's x.
                    var top = float.MinValue;
                    var back = float.MaxValue;
                    foreach (var v in filter.sharedMesh.vertices)
                    {
                        var w = t.TransformPoint(v);
                        if (Mathf.Abs(w.x - actor.SeatPoint.x) > 0.2f) continue;
                        if (w.y < 0.35f && Mathf.Abs(w.z - actor.SeatPoint.z) < 0.08f) top = Mathf.Max(top, w.y);
                        if (w.y > 0.3f) back = Mathf.Min(back, w.z);
                    }
                    log.Append($" seatTop {top:F3} backFrontZ {back:F3} readable");
                }
                else if (filter != null) log.Append(" mesh not readable");
                log.Append("\n");
            }
            foreach (var r in actor.GetComponentsInChildren<Renderer>())
                log.Append($"  model {r.name} min {r.bounds.min:F3} max {r.bounds.max:F3}\n");
            Debug.Log(log.ToString());
        }

        // ---------------------------------------------------------------- crow perch

        private static void CrowPerch(string suffix)
        {
            ToOutdoor(new Vector2Int(4, -3), FacingDirection.Up);
            var now = 4000f;
            typeof(CrowBehaviourCore).GetField("nextFlightAt", Hidden).SetValue(crow, now);
            typeof(CrowBehaviourCore).GetField("nextActionAt", Hidden).SetValue(crow, now + 1000f);
            crow.Tick(now);
            for (var i = 0; i < 120 && crow.Activity != CrowActivity.Perched; i++) crow.Tick(now += 0.1f);
            typeof(CrowBehaviourCore).GetField("perchedUntil", Hidden).SetValue(crow, now + 1000f);
            Shot2D("crow_perch_2d" + suffix, 1280, 720);
            switcher.SetMode(PocViewMode.ThirdPerson3D);
            Advance(0f, 1280, 720);
            crowView.Apply(1f / 60f, fakeNow);
            var camera = third.ViewCamera;
            var target = crow.transform.position;
            var rotation = Quaternion.Euler(20f, 0f, 0f);
            camera.transform.SetPositionAndRotation(target - rotation * Vector3.forward * 3.2f, rotation);
            camera.fieldOfView = 40f;
            Save3D("crow_perch_3d" + suffix, 1280, 720);
        }

        private static void Crop(string name, int x, int yFromTop, int w, int h)
        {
            var path = Path.Combine(output, name + ".png");
            var image = new Texture2D(2, 2);
            image.LoadImage(File.ReadAllBytes(path));
            var y = image.height - yFromTop - h;
            var part = new Texture2D(w * 3, h * 3, TextureFormat.RGB24, false);
            for (var py = 0; py < h * 3; py++)
            for (var px = 0; px < w * 3; px++)
                part.SetPixel(px, py, image.GetPixel(x + px / 3, y + py / 3));
            part.Apply();
            File.WriteAllBytes(Path.Combine(output, name + "_zoom.png"), part.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(part);
        }

        // ---------------------------------------------------------------- harness

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
            gameplay = content.GetComponent<PocGameplay>();
            input = content.GetComponent<PocInput>();
            pixel = UnityEngine.Object.FindAnyObjectByType<PocPixelView2D>(FindObjectsInactive.Include);
            third = UnityEngine.Object.FindAnyObjectByType<PocThirdPersonCamera>(FindObjectsInactive.Include);
            actor = UnityEngine.Object.FindAnyObjectByType<PocActorView>(FindObjectsInactive.Include);
            crow = UnityEngine.Object.FindAnyObjectByType<PocCrow>(FindObjectsInactive.Include);
            crowView = UnityEngine.Object.FindAnyObjectByType<PocCrowView3D>(FindObjectsInactive.Include);
            session.Initialize();
            presenter.Rebuild(session.World);
            // Edit mode runs no Awake: start the game's own components as Play Mode would.
            typeof(LifeLogController).GetMethod("Awake", Hidden).Invoke(gameplay.LifeLog, null);
            typeof(AutoModeController).GetMethod("Awake", Hidden).Invoke(gameplay.AutoMode, null);
            gameplay.Connect();
            // Edit mode has no OnEnable for these; subscribe the map-change reactions by hand.
            typeof(PocThirdPersonCamera).GetMethod("OnEnable", Hidden)?.Invoke(third, null);
            var holder = new GameObject("Phase 5 capture holder");
            foreach (var item in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (item != holder) item.transform.SetParent(holder.transform, true);
            return holder;
        }

        private static void ToOutdoor(Vector2Int cell, FacingDirection facing)
        {
            if (session.World != session.OutdoorWorld)
            {
                session.LoadMapForCheck(false, cell, facing);
                presenter.Rebuild(session.World);
            }
            session.PlaceForCheck(cell, facing);
        }

        private static void ToInterior(Vector2Int cell)
        {
            if (session.World != session.InteriorWorld)
            {
                session.LoadMapForCheck(true, cell, FacingDirection.Up);
                presenter.Rebuild(session.World);
            }
            session.PlaceForCheck(cell, FacingDirection.Up);
        }

        // Steps with the shared session (as the game does each frame) until the condition holds;
        // the presentation follows every frame. Map changes rebuild the presenter.
        // The key that walks this world direction with the current camera (3D keys are camera-relative).
        private static Vector2Int RawFor(Vector2Int worldDirection)
        {
            if (switcher.Mode != PocViewMode.ThirdPerson3D) return worldDirection;
            foreach (var raw in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                if (GridSpace3D.CameraRelative(raw, GridSpace3D.FacingYaw(switcher.CameraHeading)) == worldDirection) return raw;
            return worldDirection;
        }

        private static void StepUntil(Vector2Int worldDirection, Func<bool> done, int width, int height, Vector2Int? hold = null)
        {
            // A fresh key press (a view switch asks for a release first).
            input.SetRaw(Vector2Int.zero, fakeNow);
            input.Resolve(Vector2Int.zero, false, FacingDirection.Up);
            var raw = RawFor(worldDirection);
            for (var i = 0; i < 240 && !done(); i++)
            {
                var builtFor = session.World;
                input.SetRaw(raw, fakeNow);
                session.Tick(1f / 60f, input.Resolve(raw, switcher.Mode == PocViewMode.ThirdPerson3D, switcher.CameraHeading));
                if (session.World != builtFor) presenter.Rebuild(session.World);
                // The key is let go as soon as the goal is reached (a tap into the doorway).
                if (done()) input.SetRaw(Vector2Int.zero, fakeNow);
                Advance(1f / 60f, width, height);
            }
            input.SetRaw(Vector2Int.zero, fakeNow);
            input.Resolve(Vector2Int.zero, false, FacingDirection.Up);
        }

        private static void Settle3D(int width, int height, float seconds, Vector2Int? raw = null)
        {
            for (var t = 0f; t < seconds; t += 1f / 60f)
            {
                input.SetRaw(raw ?? Vector2Int.zero, fakeNow);
                Advance(1f / 60f, width, height);
            }
        }

        private static void Advance(float dt, int width, int height)
        {
            fakeNow += dt <= 0f ? 10f : dt;
            time.Apply(fakeNow);
            time.Apply(fakeNow + WorldNightOverlay2D.FadeSeconds + 0.1f);
            actor.Apply(dt);
            if (switcher.Mode == PocViewMode.PixelView2D) pixel.UpdateView(dt, width, height);
            else
            {
                third.ViewCamera.aspect = width / (float)height;
                third.UpdateView(dt, fakeNow);
            }
            actor.Apply(dt);
            crowView.Apply(dt, fakeNow);
        }

        private static void Shot2D(string name, int width, int height)
        {
            switcher.SetMode(PocViewMode.PixelView2D);
            pixel.SnapToTarget();
            Advance(0f, width, height);
            for (var i = 0; i < 8; i++) Advance(0.05f, width, height);
            pixel.WorldCamera.Render();
            var small = Read(pixel.ComposeScreen(width, height));
            var dst = small.GetPixels32();
            var alpha = time.PixelOverlayAlpha;
            var night = (Color32)PocTimeOfDay.NightColor;
            if (alpha > 0f)
                for (var i = 0; i < dst.Length; i++) dst[i] = Color32.Lerp(dst[i], night, alpha);
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.SetPixels32(dst);
            File.WriteAllBytes(Path.Combine(output, name + ".png"), image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(small);
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

        private static void Set(object target, string name, object value) =>
            typeof(CrowBehaviourCore).GetField(name, Hidden).SetValue(target, value);
    }
}
