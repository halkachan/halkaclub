using System;
using System.Collections.Generic;
using System.IO;
using Halka.Game.World;
using Halka.Poc2D3D.Save;
using Halka.Poc2D3D.World;
using UnityEditor;
using UnityEngine;
using C6 = Halka.Poc2D3D.EditorTools.Phase6SaveChecks;

namespace Halka.Poc2D3D.EditorTools
{
    // はるかわーるど ver3.3: overview images of the whole outdoor world, before and after the East
    // area, rendered by the game itself (Phase 6 harness, every chunk drawn). "Before" is the same
    // game with first_field alone in the registry (ver3.2's world: first_field is unchanged).
    // Presentation cameras only (not the game's cameras): one at the 2D view's angle (orthographic,
    // 3D models and tufts; the 2D game's grass sprites are drawn by the 2D view, not here) and one
    // high 3D view from the South.
    //   -executeMethod Halka.Poc2D3D.EditorTools.WorldVer33Capture.CaptureBatch
    // Output: ../HARUKA_WORLD_V33_CAPTURES/unity/
    public static class WorldVer33Capture
    {
        private const int W = 1920, H = 1080;
        private static string output;
        private static readonly List<string> notes = new List<string>();

        public static void CaptureBatch()
        {
            var ok = false;
            try { Capture(); ok = true; }
            catch (Exception exception) { Debug.LogException(exception); }
            finally { C6.DisposeHarness(); }
            EditorApplication.Exit(ok ? 0 : 1);
        }

        public static void Capture()
        {
            output = Path.GetFullPath(Path.Combine(PocSceneBuilder.RepositoryRoot, "..", "HARUKA_WORLD_V33_CAPTURES", "unity"));
            Directory.CreateDirectory(output);
            notes.Clear();
            // The same framing for both (the ver3.3 world's bounds), so the pictures line up.
            var frameMin = new Vector2Int(-12, -8);
            var frameMax = new Vector2Int(38, 8);
            foreach (var (tag, eastToo) in new[] { ("before_ver32", false), ("after_ver33", true) })
            {
                var c = C6.Start(new HarukaMemoryStorage(), null, s =>
                {
                    if (eastToo) return;
                    var field = AssetDatabase.LoadAssetAtPath<MapDefinition>(PocSceneBuilder.OutdoorMapPath);
                    s.UseOutdoorWorldForTest(OutdoorWorldDefinition.Single(field), field, false);
                });
                c.Presenter.ReachCells3D = 64;   // every chunk of the frame drawn for the picture
                c.Presenter.UpdateStreaming(true);
                c.Time.Override = PocPhaseOverride.Morning;
                for (var i = 0; i < 5; i++) C6.Frame(c, 1f / 30f, Vector2Int.zero);
                Overview2D(c, tag + "_overview_2d_angle", frameMin, frameMax);
                Overview3D(c, tag + "_overview_3d", frameMin, frameMax);
            }
            File.WriteAllLines(Path.Combine(output, "captures.txt"), notes);
        }

        private static Camera NewCamera(C6.Ctx c)
        {
            var game = c.Third.ViewCamera;
            var camera = new GameObject("ver3.3 capture camera").AddComponent<Camera>();
            camera.CopyFrom(game);
            camera.farClipPlane = 200f;
            camera.gameObject.AddComponent<PocOutline3D>().Configure(Shader.Find(PocSceneBuilder.OutlineMaskShader),
                AssetDatabase.LoadAssetAtPath<Material>(PocSceneBuilder.Outline3DMaterialPath));
            return camera;
        }

        private static void Overview2D(C6.Ctx c, string name, Vector2Int min, Vector2Int max)
        {
            var camera = NewCamera(c);
            var a = GridSpace3D.ToWorld(min);
            var b = GridSpace3D.ToWorld(max);
            var centre = (a + b) * 0.5f;
            var pitch = 50f;
            camera.orthographic = true;
            camera.transform.rotation = Quaternion.Euler(pitch, 0f, 0f);
            camera.transform.position = centre - camera.transform.forward * 40f;
            var width = (b.x - a.x) + 1f;
            var depth = ((b.z - a.z) + 1f) * Mathf.Sin(pitch * Mathf.Deg2Rad) + 1.2f;
            camera.orthographicSize = Mathf.Max(depth * 0.5f, width * 0.5f * H / W);
            Render(name, camera, $"orthographic at the 2D view's angle (50°), cells {min}..{max} (presentation camera, 3D models)");
            UnityEngine.Object.DestroyImmediate(camera.gameObject);
        }

        private static void Overview3D(C6.Ctx c, string name, Vector2Int min, Vector2Int max)
        {
            var camera = NewCamera(c);
            var a = GridSpace3D.ToWorld(min);
            var b = GridSpace3D.ToWorld(max);
            var centre = (a + b) * 0.5f;
            camera.fieldOfView = 34f;
            camera.transform.position = centre + new Vector3(0f, 15f, -17f);
            camera.transform.rotation = Quaternion.LookRotation(centre + new Vector3(0f, 0f, 1.2f) - camera.transform.position, Vector3.up);
            Render(name, camera, "a high 3D view from the South over the whole outdoor world (presentation camera)");
            UnityEngine.Object.DestroyImmediate(camera.gameObject);
        }

        private static void Render(string name, Camera camera, string note)
        {
            var rt = RenderTexture.GetTemporary(W, H, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = rt;
            camera.aspect = W / (float)H;
            camera.Render();
            camera.targetTexture = null;
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var texture = new Texture2D(W, H, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            File.WriteAllBytes(Path.Combine(output, name + ".png"), texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            notes.Add(name + ".png: " + note);
        }
    }
}
