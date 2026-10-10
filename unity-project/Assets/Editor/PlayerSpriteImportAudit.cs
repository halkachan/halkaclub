using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Halka.Game.Editor
{
    // Compares the authored PNG pixels with Unity's imported texture pixels.
    // Readability is enabled only during the audit and always restored.
    public static class PlayerSpriteImportAudit
    {
        private const string Folder = "Assets/Content/Character";

        [MenuItem("HALKA/Audit Player Sprite Pixels")]
        public static void Run()
        {
            var folders = new[] { "front_idle", "back_idle", "left_idle", "right_idle",
                "walk_front", "walk_back", "walk_left", "walk_right" };
            var paths = folders.SelectMany(folder => Directory.GetFiles(Folder + "/" + folder, "*.png"))
                .Concat(new[] { Folder + "/bench_sit.png" })
                .OrderBy(path => path, StringComparer.Ordinal).ToArray();
            var opaquePixels = 0;
            foreach (var path in paths)
                opaquePixels += Compare(path.Replace('\\', '/'));
            Debug.Log($"HALKA Player import audit passed: {paths.Length} sprites, " +
                $"{opaquePixels} opaque pixels identical to their source PNGs.");
        }

        private static int Compare(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing importer: " + path);
            var wasReadable = importer.isReadable;
            Texture2D source = null;
            try
            {
                if (!wasReadable)
                {
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                }
                var imported = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (imported == null || !source.LoadImage(File.ReadAllBytes(path), false) ||
                    imported.width != source.width || imported.height != source.height)
                    throw new InvalidOperationException("Source/import dimensions differ: " + path);

                var originalPixels = source.GetPixels32();
                var importedPixels = imported.GetPixels32();
                var count = 0;
                for (var i = 0; i < originalPixels.Length; i++)
                {
                    var original = originalPixels[i];
                    var actual = importedPixels[i];
                    if (original.a != actual.a ||
                        (original.a != 0 && (original.r != actual.r || original.g != actual.g ||
                                             original.b != actual.b)))
                        throw new InvalidOperationException($"Imported pixel changed: {path} at index {i}, " +
                            $"source={original}, imported={actual}");
                    if (original.a != 0) count++;
                }
                return count;
            }
            finally
            {
                if (source != null) UnityEngine.Object.DestroyImmediate(source);
                if (!wasReadable)
                {
                    importer.isReadable = false;
                    importer.SaveAndReimport();
                }
            }
        }
    }
}
