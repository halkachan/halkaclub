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

        // ver4.0: close-ups of ﾊﾙｶﾁｬﾝ in the room (the kitchen, the bottom wall row), 2D, at the game's strength.
        //   -executeMethod Halka.Poc2D3D.EditorTools.Ver40WornCapture.CaptureRoomBatch
        public static void CaptureRoomBatch()
        {
            var ok = false;
            try
            {
                var output = Path.GetFullPath(Path.Combine(PocSceneBuilder.RepositoryRoot, "..", "HARUKA_WORLD_V40_LOGS", "room_closeups"));
                Directory.CreateDirectory(output);
                var c = C6.Start(new HarukaMemoryStorage());
                c.Time.Override = PocPhaseOverride.Morning;
                var heights = Environment.GetEnvironmentVariable("HALKA_LOW2D");
                foreach (var h in (string.IsNullOrEmpty(heights) ? "0.3" : heights).Split(','))
                foreach (var (cell, facing) in new[] { (new Vector2Int(3, -2), FacingDirection.Down), (new Vector2Int(4, -2), FacingDirection.Down),
                             (new Vector2Int(5, -2), FacingDirection.Down) })
                {
                    var hs = h.Split('/');
                    PocPixelView2D.Low2DScale = float.Parse(hs[0], System.Globalization.CultureInfo.InvariantCulture);
                    PocPixelView2D.Low2DShift = hs.Length > 1 ? float.Parse(hs[1], System.Globalization.CultureInfo.InvariantCulture) : 0f;
                    if (c.Session.World == null || c.Session.World.MapId != "halka_house") c.Session.LoadMapForCheck("halka_house", cell, facing);
                    c.Session.PlaceForCheck(cell, facing);
                    for (var i = 0; i < 20; i++) C6.Frame(c, 1f / 30f, Vector2Int.zero);
                    var path = Path.Combine(output, $"room_h{h.Replace('/', 's')}_{cell.x}_{cell.y}_{facing}.png");
                    Shot2D(c, PocWorn.Strength, path);
                    // a close-up around her sprite (the sprite rectangle counts from the bottom)
                    var full = new Texture2D(2, 2);
                    full.LoadImage(File.ReadAllBytes(path));
                    var r = c.Pixel.LastSpriteRect;
                    int x0 = Mathf.Clamp(r.x - 48, 0, full.width - 1), y0 = Mathf.Clamp(r.y - 48, 0, full.height - 1);
                    int cw = Mathf.Min(r.width + 96, full.width - x0), ch = Mathf.Min(r.height + 96, full.height - y0);
                    var crop = new Texture2D(cw, ch, TextureFormat.RGB24, false);
                    crop.SetPixels(full.GetPixels(x0, y0, cw, ch));
                    crop.Apply();
                    File.WriteAllBytes(path.Replace(".png", "_closeup.png"), crop.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(full);
                    UnityEngine.Object.DestroyImmediate(crop);
                }
                ok = true;
            }
            catch (Exception exception) { Debug.LogException(exception); }
            finally { C6.DisposeHarness(); PocWorn.Apply(); }
            EditorApplication.Exit(ok ? 0 : 1);
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
