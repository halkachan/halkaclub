using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Halka.Game.Player;
using Halka.Game.World;
using Halka.Poc2D3D.Save;
using UnityEditor;
using UnityEngine;
using C6 = Halka.Poc2D3D.EditorTools.Phase6SaveChecks;

namespace Halka.Poc2D3D.EditorTools
{
    // はるかわーるど ver3.1: the design images rendered by the game itself (the shared world, the 2D
    // Pixel View and the 3D third-person camera with its outline), started with the Phase 6 harness
    // (memory storage). "Before" is ver3.0's house: an in-memory copy of halka_house with the
    // easel and the mic back where ver3.0 had them (the official asset is not touched).
    //   -executeMethod Halka.Poc2D3D.EditorTools.WorldVer31Capture.CaptureBatch
    // Output: ../HARUKA_WORLD_V31_CAPTURES/unity/
    public static class WorldVer31Capture
    {
        private const int W = 1280, H = 720;
        private static string output;
        private static C6.Ctx c;
        private static float fakeNow = 1000f;
        private static readonly List<string> notes = new List<string>();

        public static void CaptureBatch()
        {
            var ok = false;
            try { Capture(); ok = true; }
            catch (Exception exception) { Debug.LogException(exception); }
            finally { C6.DisposeHarness(); }
            EditorApplication.Exit(ok ? 0 : 1);
        }

        private static MapDefinition Ver30House(MapDefinition house)
        {
            WorldObjectDefinition Def(string id) => AssetDatabase.FindAssets("t:WorldObjectDefinition")
                .Select(g => AssetDatabase.LoadAssetAtPath<WorldObjectDefinition>(AssetDatabase.GUIDToAssetPath(g)))
                .First(d => d != null && d.StableId == id);
            var objects = house.Objects.Where(o => o.Definition.StableId != "soundbooth_exterior" && o.Definition.StableId != "illust_desk_tablet").ToList();
            objects.Insert(2, new WorldObjectPlacement { InstanceId = "obj_recording_mic_v30_preview", RootCell = new Vector2Int(4, -1), Definition = Def("recording_mic_basic") });
            objects.Insert(3, new WorldObjectPlacement { InstanceId = "obj_art_easel_v30_preview", RootCell = new Vector2Int(-3, -2), Definition = Def("art_easel_basic") });
            var copy = ScriptableObject.CreateInstance<MapDefinition>();
            copy.ReplaceFromAuthoring(house.MapId, house.DisplayName, house.MapType, house.BaseSurface, "none", house.BackdropColor,
                house.MinCell, house.MaxCell, house.Surfaces.ToList(), objects, house.Markers.ToList());
            return copy;
        }

        public static void Capture()
        {
            output = Path.GetFullPath(Path.Combine(PocSceneBuilder.RepositoryRoot, "..", "HARUKA_WORLD_V31_CAPTURES", "unity"));
            Directory.CreateDirectory(output);
            C6.SetLegacy("v1|10|10", "1750000000000");
            c = C6.Start(new HarukaMemoryStorage());
            var house = c.Session.InteriorWorld.Map;

            // 1-4: the house before (ver3.0) and after (ver3.1), the same place and cameras.
            var before = Ver30House(house);
            c.Session.UseInteriorMapForTest(before);
            Shoot("01_house_before_2d", "02_house_before_3d", c.Session.InteriorWorld.MapId, new Vector2Int(0, -1), FacingDirection.Up);
            Overview("02b_house_before_3d_overview", house);
            c.Session.UseInteriorMapForTest(house);
            Shoot("03_house_after_2d", "04_house_after_3d", "halka_house", new Vector2Int(0, -1), FacingDirection.Up);
            Overview("04b_house_after_3d_overview", house);

            // 5-6: the black booth in the house, from where you walk in.
            Shoot("05_booth_exterior_2d", "06_booth_exterior_3d", "halka_house", new Vector2Int(-3, -1), FacingDirection.Up);
            // 7-8: inside the booth.
            Shoot("07_booth_interior_2d", "08_booth_interior_3d", "halka_soundbooth", new Vector2Int(0, -3), FacingDirection.Up);
            Overview("08b_booth_interior_3d_overview", c.Session.FindWorld("halka_soundbooth").Map);
            // 9-12: the DTM desk from the front and at an angle, the speakers, the mic. Still cameras
            // with her standing aside (desk root (-2,3): world x -1.0 .. 0.75, z 1.5; mic (4,1): x 2, z 0.5).
            Shoot("09a_dtm_desk_2d", null, "halka_soundbooth", new Vector2Int(-1, 2), FacingDirection.Up);
            Place("halka_soundbooth", new Vector2Int(5, -3), FacingDirection.Up);
            Settle3D();
            Closeup("09_dtm_desk_front_3d", new Vector3(-0.25f, 0.5f, 1.5f), new Vector3(-0.25f, 1.15f, -0.65f), 42f);
            Closeup("10_dtm_desk_oblique_3d", new Vector3(-0.25f, 0.5f, 1.5f), new Vector3(1.35f, 1.25f, 0.0f), 42f);
            Closeup("11_monitor_speakers_3d", new Vector3(-0.25f, 0.62f, 1.55f), new Vector3(-0.25f, 0.85f, 0.55f), 50f);
            Shoot("12_mic_stand_2d", null, "halka_soundbooth", new Vector2Int(4, 0), FacingDirection.Up);
            Place("halka_soundbooth", new Vector2Int(-5, -3), FacingDirection.Up);
            Settle3D();
            Closeup("12_mic_stand_3d", new Vector3(2.0f, 0.55f, 0.6f), new Vector3(3.1f, 1.25f, -1.25f), 40f);
            // 13-14: the illustration desk with the pen tablet (root (-3,-2): world x -1.5 .. -0.5, z -1).
            Shoot("13_illust_desk_2d", "14_illust_desk_3d", "halka_house", new Vector2Int(-3, -3), FacingDirection.Up, 0.6f);
            Place("halka_house", new Vector2Int(3, -3), FacingDirection.Up);
            Settle3D();
            Closeup("14b_illust_desk_closeup_3d", new Vector3(-1.25f, 0.45f, -1.0f), new Vector3(-0.75f, 1.05f, -2.0f), 40f);
            Closeup("06b_booth_door_closeup_3d", new Vector3(-1.5f, 0.45f, 0.5f), new Vector3(-1.1f, 0.95f, -0.9f), 44f);

            // Night: the booth keeps its light; the house and outdoors for comparison.
            c.Time.Override = PocPhaseOverride.Night;
            Shoot("night_booth_2d", "night_booth_3d", "halka_soundbooth", new Vector2Int(0, -3), FacingDirection.Up);
            Shoot("night_house_2d", "night_house_3d", "halka_house", new Vector2Int(0, -1), FacingDirection.Up);
            c.Time.Override = PocPhaseOverride.Morning;
            File.WriteAllLines(Path.Combine(output, "captures.txt"), notes);
            Debug.Log("HARUKA ver3.1 captures: " + output);
        }

        private static void Place(string map, Vector2Int cell, FacingDirection facing)
        {
            if (c.Session.World.MapId != map) c.Session.LoadMapForCheck(map, cell, facing);
            c.Session.PlaceForCheck(cell, facing);
        }

        private static void Settle3D()
        {
            c.Switcher.SetMode(PocViewMode.ThirdPerson3D);
            c.Third.SnapToTarget();
            for (var i = 0; i < 20; i++) Advance(1f / 30f);
        }

        private static void Advance(float dt)
        {
            fakeNow += dt <= 0f ? 10f : dt;
            c.Time.Apply(fakeNow);
            c.Time.Apply(fakeNow + WorldNightOverlay2D.FadeSeconds + 0.1f);
            c.Actor.Apply(dt);
            if (c.Switcher.Mode == PocViewMode.PixelView2D) c.Pixel.UpdateView(dt, W, H);
            else { c.Third.ViewCamera.aspect = W / (float)H; c.Third.UpdateView(dt, fakeNow); }
            c.Actor.Apply(dt);
            c.CrowView.Apply(dt, fakeNow);
        }

        private static void Shoot(string name2D, string name3D, string map, Vector2Int cell, FacingDirection facing, float distanceScale = 1f)
        {
            Place(map, cell, facing);
            if (name2D != null)
            {
                c.Switcher.SetMode(PocViewMode.PixelView2D);
                c.Pixel.SnapToTarget();
                Advance(0f);
                for (var i = 0; i < 8; i++) Advance(0.05f);
                c.Pixel.WorldCamera.Render();
                var small = Read(c.Pixel.ComposeScreen(W, H));
                var pixels = small.GetPixels32();
                var alpha = c.Time.PixelOverlayAlpha;
                var night = (Color32)PocTimeOfDay.NightColor;
                if (alpha > 0f) for (var i = 0; i < pixels.Length; i++) pixels[i] = Color32.Lerp(pixels[i], night, alpha);
                small.SetPixels32(pixels);
                Write(name2D, small, $"{map} {cell} {facing} 2D overlay {alpha:0.00}");
            }
            if (name3D != null)
            {
                c.Switcher.SetMode(PocViewMode.ThirdPerson3D);
                var outdoorDistance = c.Third.OutdoorDistance;
                var indoorDistance = c.Third.IndoorDistance;
                c.Third.OutdoorDistance = outdoorDistance * distanceScale;
                c.Third.IndoorDistance = indoorDistance * distanceScale;
                c.Third.SnapToTarget();
                Advance(0f);
                for (var i = 0; i < 30; i++) Advance(1f / 30f);
                Render3D(name3D, c.Third.ViewCamera, $"{map} {cell} {facing} 3D game camera" + (distanceScale != 1f ? $" (distance x{distanceScale})" : ""));
                c.Third.OutdoorDistance = outdoorDistance;
                c.Third.IndoorDistance = indoorDistance;
            }
        }

        // A still camera at a chosen place, rendered with the game's 3D outline.
        private static void Closeup(string name, Vector3 target, Vector3 from, float fov)
        {
            var camera = NewCamera(fov);
            camera.transform.position = from;
            camera.transform.rotation = Quaternion.LookRotation(target - from, Vector3.up);
            Render3D(name, camera, $"still camera {from} -> {target}, fov {fov}");
            UnityEngine.Object.DestroyImmediate(camera.gameObject);
        }

        // The whole room from the South, high and wide (presentation only: the game camera never
        // stands here), so the layout can be compared in one picture.
        private static void Overview(string name, MapDefinition map)
        {
            Place(map.MapId, map.MapId == "halka_soundbooth" ? new Vector2Int(0, -3) : new Vector2Int(0, -1), FacingDirection.Up);
            c.Switcher.SetMode(PocViewMode.ThirdPerson3D);
            c.Third.SnapToTarget();
            for (var i = 0; i < 10; i++) Advance(1f / 30f);
            var min = GridSpace3D.ToWorld(map.MinCell);
            var max = GridSpace3D.ToWorld(map.MaxCell);
            var centre = (min + max) * 0.5f;
            var camera = NewCamera(32f);
            camera.transform.position = centre + new Vector3(0f, (max.z - min.z) * 1.35f + 2.4f, -(max.z - min.z) * 1.05f - 2.2f);
            camera.transform.rotation = Quaternion.LookRotation(centre + new Vector3(0f, 0.2f, 0.35f) - camera.transform.position, Vector3.up);
            // The South walls stay cut away as the game camera has them.
            Render3D(name, camera, $"overview of {map.MapId} (not a game camera)");
            UnityEngine.Object.DestroyImmediate(camera.gameObject);
        }

        private static Camera NewCamera(float fov)
        {
            var game = c.Third.ViewCamera;
            var camera = new GameObject("ver3.1 capture camera").AddComponent<Camera>();
            camera.CopyFrom(game);
            camera.fieldOfView = fov;
            var outline = game.GetComponent<PocOutline3D>();
            if (outline != null)
                camera.gameObject.AddComponent<PocOutline3D>().Configure(Shader.Find(PocSceneBuilder.OutlineMaskShader),
                    AssetDatabase.LoadAssetAtPath<Material>(PocSceneBuilder.Outline3DMaterialPath));
            return camera;
        }

        private static void Render3D(string name, Camera camera, string note)
        {
            var rt = RenderTexture.GetTemporary(W, H, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = rt;
            camera.aspect = W / (float)H;
            camera.Render();
            camera.targetTexture = null;
            var image = Read(rt);
            RenderTexture.ReleaseTemporary(rt);
            camera.ResetAspect();
            Write(name, image, note);
        }

        private static void Write(string name, Texture2D image, string note)
        {
            File.WriteAllBytes(Path.Combine(output, name + ".png"), image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
            notes.Add(name + ".png: " + note);
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
    }
}
