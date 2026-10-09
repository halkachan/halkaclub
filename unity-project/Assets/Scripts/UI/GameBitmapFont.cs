using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Halka.Game.UI
{
    // Limited raster artwork made from 遊びメモ書き. Raw TrueType is never a game asset.
    public static class GameBitmapFont
    {
        [Serializable]
        private sealed class GlyphMap
        {
            public string characters;
            public float[] advances;
            public int cellSize;
            public int columns;
            public int rows;
            public int sourceSize;
        }

        private static Texture2D atlas;
        private static GlyphMap map;
        private static Dictionary<char, int> indices;
        public static bool IsAvailable { get { EnsureLoaded(); return atlas != null && map != null; } }

        public static bool HasAllGlyphs(string text)
        {
            EnsureLoaded();
            if (!IsAvailable || text == null) return false;
            foreach (var character in text)
                if (character != '\n' && !indices.ContainsKey(character)) return false;
            return true;
        }

        public static void Draw(Rect rect, string text, int fontSize, Color color,
            TextAnchor alignment, Font fallbackFont)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (!HasAllGlyphs(text))
            {
                var safeText = new StringBuilder(text.Length);
                foreach (var character in text)
                    safeText.Append(character == '\n' || fallbackFont == null ||
                        fallbackFont.HasCharacter(character) ? character : '?');
                var fallback = new GUIStyle(GUI.skin.label) {
                    font = fallbackFont, fontSize = fontSize, alignment = alignment,
                    wordWrap = true, normal = { textColor = color } };
                GUI.Label(rect, safeText.ToString(), fallback);
                return;
            }
            var lines = text.Split('\n');
            var scale = fontSize / (float)map.sourceSize;
            var widest = 0f;
            foreach (var line in lines) widest = Mathf.Max(widest, Width(line) * scale);
            if (widest > rect.width) scale *= rect.width / widest;
            var lineHeight = map.cellSize * scale;
            var blockHeight = lineHeight * lines.Length;
            var y = alignment == TextAnchor.UpperLeft || alignment == TextAnchor.UpperCenter ||
                alignment == TextAnchor.UpperRight ? rect.y :
                alignment == TextAnchor.LowerLeft || alignment == TextAnchor.LowerCenter ||
                alignment == TextAnchor.LowerRight ? rect.yMax - blockHeight :
                rect.center.y - blockHeight * 0.5f;
            var previous = GUI.color;
            GUI.color = color;
            foreach (var line in lines)
            {
                var lineWidth = Width(line) * scale;
                var x = alignment == TextAnchor.UpperRight || alignment == TextAnchor.MiddleRight ||
                    alignment == TextAnchor.LowerRight ? rect.xMax - lineWidth :
                    alignment == TextAnchor.UpperCenter || alignment == TextAnchor.MiddleCenter ||
                    alignment == TextAnchor.LowerCenter ? rect.center.x - lineWidth * 0.5f : rect.x;
                foreach (var character in line)
                {
                    var index = indices[character];
                    var uv = new Rect(index % map.columns / (float)map.columns,
                        1f - (index / map.columns + 1f) / map.rows,
                        1f / map.columns, 1f / map.rows);
                    // Keep glyph edges aligned with the final UI pixel grid.
                    var drawnSize = Mathf.Max(1f, Mathf.Round(lineHeight));
                    GUI.DrawTextureWithTexCoords(new Rect(Mathf.Round(x), Mathf.Round(y),
                        drawnSize, drawnSize), atlas, uv);
                    x += map.advances[index] * scale;
                }
                y += lineHeight;
            }
            GUI.color = previous;
        }

        private static float Width(string line)
        {
            var width = 0f;
            foreach (var character in line) width += map.advances[indices[character]];
            return width;
        }

        private static void EnsureLoaded()
        {
            if (indices != null) return;
            atlas = Resources.Load<Texture2D>("asobi_ui_atlas");
            var data = Resources.Load<TextAsset>("asobi_ui_map");
            if (atlas == null || data == null) return;
            var lines = data.text.Split('\n');
            if (lines.Length < 3) return;
            var advanceParts = lines[1].Split(',');
            var advances = new float[advanceParts.Length];
            for (var i = 0; i < advances.Length; i++)
                if (!float.TryParse(advanceParts[i], NumberStyles.Float,
                    CultureInfo.InvariantCulture, out advances[i])) return;
            if (!int.TryParse(lines[2], NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var rows)) return;
            map = new GlyphMap { characters = lines[0].TrimEnd('\r'), advances = advances,
                cellSize = 64, columns = 32, rows = rows, sourceSize = 50 };
            if (map == null || map.characters == null || map.advances == null ||
                map.characters.Length != map.advances.Length) { map = null; return; }
            indices = new Dictionary<char, int>();
            for (var i = 0; i < map.characters.Length; i++) indices[map.characters[i]] = i;
        }
    }
}
