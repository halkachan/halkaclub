using System;
using System.Collections.Generic;
using UnityEngine;

namespace Halka.Game.World
{
    // A ground override changes artwork and grass coverage, never collision or interaction.
    public sealed class GroundSurfaceField2D : MonoBehaviour
    {
        [Serializable]
        private struct SurfaceCell
        {
            public Vector2Int Cell;
            public Sprite Sprite;
        }

        [SerializeField] private List<SurfaceCell> cells = new List<SurfaceCell>();

        public int Count => cells.Count;

        public bool HasGroundOverride(Vector2Int cell)
        {
            foreach (var surface in cells)
                if (surface.Cell == cell) return true;
            return false;
        }

        public Sprite GetSurface(Vector2Int cell)
        {
            foreach (var surface in cells)
                if (surface.Cell == cell) return surface.Sprite;
            return null;
        }

        public void AddSurface(Vector2Int cell, Sprite sprite)
        {
            if (sprite == null || HasGroundOverride(cell))
                throw new ArgumentException($"Invalid or duplicate ground surface at {cell}");
            cells.Add(new SurfaceCell { Cell = cell, Sprite = sprite });
        }

        public void Clear() => cells.Clear();
    }
}
