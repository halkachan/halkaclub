using System;
using System.IO;
using Halka.Game.Player;
using Halka.Poc2D3D.Save;
using UnityEditor;
using UnityEngine;
using C6 = Halka.Poc2D3D.EditorTools.Phase6SaveChecks;

namespace Halka.Poc2D3D.EditorTools
{
    // ver4.0 (the worn world, in the models themselves): the game's own 2D screen and 3D camera at a few
    // strengths of PocWorn, rendered by Unity (no WebGL build), for the user to compare.
    //   -executeMethod Halka.Poc2D3D.EditorTools.Ver40WornCapture.CaptureBatch
    // Output: ../HARUKA_WORLD_V40_LOGS/worn_preview/
    public static class Ver40WornCapture
    {
        private const int W = 1280, H = 720;
        public static readonly float[] Strengths = { 0f, 0.6f, 1f };

        public static void CaptureBatch()
        {
            var ok = false;
            try { Capture(); ok = true; }
            catch (Exception exception) { Debug.LogException(exception); }
            finally
            {
                C6.DisposeHarness();
                PocWorn.Apply();
            }
            EditorApplication.Exit(ok ? 0 : 1);
        }

        public static void Capture()
        {
            var output = Path.GetFullPath(Path.Combine(PocSceneBuilder.RepositoryRoot, "..", "HARUKA_WORLD_V40_LOGS", "worn_preview"));
            Directory.CreateDirectory(output);
            var c = C6.Start(new HarukaMemoryStorage());
            c.Time.Override = PocPhaseOverride.Morning;
            c.Presenter.ReachCells3D = 40;
            var places = new (string name, Vector2Int cell, FacingDirection facing)[]
            {
                ("field", new Vector2Int(-4, -7), FacingDirection.Up),
                ("east", new Vector2Int(22, 0), FacingDirection.Right),
                ("forest", new Vector2Int(-30, 2), FacingDirection.Left),
            };
            foreach (var (name, cell, facing) in places)
            {
                c.Session.PlaceForCheck(cell, facing);
                for (var i = 0; i < 20; i++) C6.Frame(c, 1f / 30f, Vector2Int.zero);
                foreach (var k in Strengths)
                {
                    Shot2D(c, k, Path.Combine(output, $"{name}_2d_worn{k:0.0}.png"));
                    Shot3D(c, k, Path.Combine(output, $"{name}_3d_worn{k:0.0}.png"));
                }
            }
            // indoors
            c.Session.LoadMapForCheck("halka_house", new Vector2Int(0, -2), FacingDirection.Up);
            for (var i = 0; i < 20; i++) C6.Frame(c, 1f / 30f, Vector2Int.zero);
            foreach (var k in Strengths)
            {
                Shot2D(c, k, Path.Combine(output, $"room_2d_worn{k:0.0}.png"));
                Shot3D(c, k, Path.Combine(output, $"room_3d_worn{k:0.0}.png"));
            }
        }

        private static void Shot2D(C6.Ctx c, float k, string path)
        {
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            for (var i = 0; i < 3; i++) C6.Frame(c, 1f / 30f, Vector2Int.zero);
            PocWorn.Apply(k);
            c.Pixel.SnapToTarget();
            c.Pixel.WorldCamera.Render();
            Save(c.Pixel.ComposeScreen(W, H), path);
        }

        private static void Shot3D(C6.Ctx c, float k, string path)
        {
            c.Switcher.SetMode(PocViewMode.ThirdPerson3D);
            for (var i = 0; i < 20; i++) C6.Frame(c, 1f / 30f, Vector2Int.zero);
            PocWorn.Apply(k);
            var camera = c.Third.ViewCamera;
            var rt = RenderTexture.GetTemporary(W, H, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = rt;
            camera.aspect = W / (float)H;
            camera.Render();
            camera.targetTexture = null;
            Save(rt, path);
            RenderTexture.ReleaseTemporary(rt);
        }

        private static void Save(RenderTexture rt, string path)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
