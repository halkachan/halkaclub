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
    // はるかわーるど Phase 4 review images: the 3D exit camera (before / held / after the first
    // move), the 3D see-through tree, the crow in 2D and 3D (ground, flight, perch), the seats in
    // 2D and 3D, AUTO, landscape phone sizes, and night.
    // Batch: -executeMethod Halka.Poc2D3D.EditorTools.Phase4Capture.CaptureBatch
    public static class Phase4Capture
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
                Capture(Environment.GetEnvironmentVariable("HARUKA_PHASE4_CAPTURE_DIR") ??
                    Path.Combine(PocSceneBuilder.RepositoryRoot, "..", "HARUKA_WORLD_ver3.0_PHASE4_CAPTURES", "unity"));
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
            var randomState = UnityEngine.Random.state;
            UnityEngine.Random.InitState(4);
            var root = Setup();
            try
            {
                time.Override = PocPhaseOverride.Morning;
                ExitCamera("");
                TreeFade("");
                CrowShots("");
                Seats("");
                Landscape();
                time.Override = PocPhaseOverride.Night;
                ExitCamera("_night");
                TreeFade("_night");
                CrowShots("_night");
                Seats("_night");
                time.Override = PocPhaseOverride.Morning;
                Debug.Log("HARUKA Phase 4 captures written to " + output);
            }
            finally
            {
                UnityEngine.Random.state = randomState;
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        // ---------------------------------------------------------------- exit camera

        private static void ExitCamera(string suffix)
        {
            // Phase 3 behaviour (camera behind the player = beyond the house) and Phase 4.
            foreach (var composition in new[] { false, true })
            {
                third.ExitComposition = composition;
                ToInterior(new Vector2Int(0, -3));
                switcher.SetMode(PocViewMode.ThirdPerson3D);
                session.PlaceForCheck(new Vector2Int(0, -3), FacingDirection.Down);
                Settle3D(1280, 720, 0.4f);
                // Walk out through the doorway (a real step onto interior_exit).
                StepUntil(Vector2Int.down, () => session.World == session.OutdoorWorld, 1280, 720);
                Settle3D(1280, 720, 0.1f, Vector2Int.zero);
                Save3D("exit_camera_" + (composition ? "new_held" : "old_phase3") + suffix, 1280, 720);
                if (!composition) continue;
                // The first move (S: toward the camera, South) releases it; the camera blends back.
                StepUntil(Vector2Int.down, () => !session.IsMoving && session.Cell.y < session.OutdoorWorld.Map.OutsideEntryCell.y,
                    1280, 720, Vector2Int.down);
                Settle3D(1280, 720, 0.9f, Vector2Int.zero);
                Save3D("exit_camera_new_after_first_move" + suffix, 1280, 720);
            }
            third.ExitComposition = true;
        }

        // ---------------------------------------------------------------- tree fade

        private static void TreeFade(string suffix)
        {
            // North of the tree (5,1), facing North: the camera behind her looks through the tree.
            ToOutdoor(new Vector2Int(5, 2), FacingDirection.Up);
            foreach (var enabled in new[] { false, true })
            {
                third.TreeFade.Enabled = enabled;
                switcher.SetMode(PocViewMode.ThirdPerson3D);
                session.PlaceForCheck(new Vector2Int(5, 2), FacingDirection.Up);
                third.SnapToTarget();
                Settle3D(1280, 720, 0.6f);
                Save3D("tree_3d_fade_" + (enabled ? "on" : "off") + suffix, 1280, 720);
            }
            third.TreeFade.Enabled = true;
            switcher.SetMode(PocViewMode.PixelView2D);
            session.PlaceForCheck(new Vector2Int(5, 2), FacingDirection.Down);
            Shot2D("tree_2d_fade" + suffix, 1280, 720);
        }

        // ---------------------------------------------------------------- crow

        private static void CrowShots(string suffix)
        {
            ToOutdoor(new Vector2Int(4, 0), FacingDirection.Right);
            var now = 2000f;
            Set(crow, "nextFlightAt", now + 1000f);
            Set(crow, "nextActionAt", now + 1000f);
            crow.Tick(now);
            CrowFixed("crow_3d_ground_idle" + suffix, 0.25f);
            Shot2D("crow_2d_ground" + suffix, 1280, 720);
            // A wander step, caught half way.
            for (var i = 0; i < 40 && !crow.IsMoving; i++)
            {
                Set(crow, "nextActionAt", now);
                crow.Tick(now += 0.01f);
            }
            crow.Tick(now += CrowBehaviourCore.StepSeconds * 0.5f);
            CrowFixed("crow_3d_ground_walk" + suffix, 0.25f);
            crow.Tick(now += 1f);
            // Take off toward a tree, fly, perch.
            Set(crow, "nextActionAt", now + 1000f);
            Set(crow, "nextFlightAt", now);
            crow.Tick(now += 0.01f);
            crow.Tick(now += 0.25f);
            CrowFixed("crow_3d_takeoff" + suffix, 0.6f);
            crow.Tick(now += 0.3f);
            crow.Tick(now += 0.4f);
            CrowFixed("crow_3d_flying" + suffix, 1.2f);
            switcher.SetMode(PocViewMode.PixelView2D);
            Shot2D("crow_2d_flying" + suffix, 1280, 720);
            for (var i = 0; i < 80 && crow.Activity != CrowActivity.Perched; i++) crow.Tick(now += 0.1f);
            CrowFixed("crow_3d_perched" + suffix, 1.6f);
            Shot2D("crow_2d_perched" + suffix, 1280, 720);
            // Leave the perch and come back to the ground.
            for (var i = 0; i < 200 && crow.Activity != CrowActivity.GroundIdle; i++) crow.Tick(now += 0.1f);
            CrowFixed("crow_3d_landed" + suffix, 0.25f);
        }

        private static void CrowFixed(string name, float height)
        {
            switcher.SetMode(PocViewMode.ThirdPerson3D);
            Advance(0f, 1000, 750);
            crowView.Apply(1f / 60f, fakeNow);
            var target = crow.transform.position + Vector3.up * 0.12f;
            var camera = third.ViewCamera;
            var rotation = Quaternion.Euler(18f, -30f, 0f);
            camera.transform.SetPositionAndRotation(target - rotation * Vector3.forward * (1.4f + height), rotation);
            camera.fieldOfView = 40f;
            Save3D(name, 1000, 750);
        }

        // ---------------------------------------------------------------- seats

        private static void Seats(string suffix)
        {
            // Bench (outdoor): the left seat's Action Point (-2,1), facing Up.
            ToOutdoor(new Vector2Int(-2, 1), FacingDirection.Up);
            foreach (var mode in new[] { PocViewMode.PixelView2D, PocViewMode.ThirdPerson3D })
            {
                switcher.SetMode(mode);
                session.PlaceForCheck(new Vector2Int(-2, 1), FacingDirection.Up);
                session.InteractAhead();
                if (mode == PocViewMode.PixelView2D) Shot2D("bench_sit_2d" + suffix, 1280, 720);
                else
                {
                    third.SnapToTarget();
                    Settle3D(1280, 720, 0.5f);
                    Save3D("bench_sit_3d" + suffix, 1280, 720);
                }
                session.Stand();
            }
            // Cushion (indoor): its own cell (0,0), facing Up.
            ToInterior(new Vector2Int(0, 0));
            foreach (var mode in new[] { PocViewMode.PixelView2D, PocViewMode.ThirdPerson3D })
            {
                switcher.SetMode(mode);
                session.PlaceForCheck(new Vector2Int(0, 0), FacingDirection.Up);
                session.InteractAhead();
                if (mode == PocViewMode.PixelView2D) Shot2D("cushion_sit_2d" + suffix, 1280, 720);
                else
                {
                    third.SnapToTarget();
                    Settle3D(1280, 720, 0.5f);
                    Save3D("cushion_sit_3d" + suffix, 1280, 720);
                }
                session.Stand();
            }
        }

        // ---------------------------------------------------------------- landscape phone sizes

        private static void Landscape()
        {
            foreach (var (w, h, label) in new[] { (1688, 780, "844x390_dpr2"), (1280, 720, "pc_1280x720"),
                         (1024, 576, "pc_1024x576") })
            {
                ToOutdoor(new Vector2Int(-7, -6), FacingDirection.Up);
                Shot2D("landscape_2d_" + label, w, h);
                switcher.SetMode(PocViewMode.ThirdPerson3D);
                session.PlaceForCheck(new Vector2Int(-7, -6), FacingDirection.Up);
                third.SnapToTarget();
                Settle3D(w, h, 0.4f);
                Save3D("landscape_3d_" + label, w, h);
            }
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
            var holder = new GameObject("Phase 4 capture holder");
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
