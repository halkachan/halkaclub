using System;
using UnityEngine;

namespace Halka.Poc2D3D.World
{
    // ver3.2: one chunk of the outdoor world, named by its chunk coordinates (cell / size, rounded
    // toward negative infinity on both axes).
    public readonly struct ChunkCoord : IEquatable<ChunkCoord>
    {
        public readonly int X;
        public readonly int Y;

        public ChunkCoord(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(ChunkCoord other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is ChunkCoord other && Equals(other);
        public override int GetHashCode() => unchecked(X * 73856093 ^ Y * 19349663);
        public override string ToString() => "(" + X + "," + Y + ")";
        public static bool operator ==(ChunkCoord a, ChunkCoord b) => a.Equals(b);
        public static bool operator !=(ChunkCoord a, ChunkCoord b) => !a.Equals(b);
        public int ChebyshevDistance(ChunkCoord other) => Mathf.Max(Mathf.Abs(X - other.X), Mathf.Abs(Y - other.Y));
    }

    // ver3.2: integer world-cell arithmetic for the outdoor world. Grid X is East, Grid Y is North,
    // one cell is 0.5 Unity units (GridSpace3D). Cells are ints; nothing here uses Transform floats.
    //   - Chunks: floor division, so -1 belongs to chunk -1 and the chunk-local cell is always
    //     0..size-1 (C#'s "/" rounds toward zero and would put -1 into chunk 0).
    //   - Limits: logical cells stay within +-MaxAbsCell (far from int overflow even after adding a
    //     region offset and a chunk size); Unity positions are floats, cell * 0.5 keeps a 1/256 unit
    //     step only up to +-RenderSafeAbsCell, beyond which a floating origin would be needed (ver3.3+).
    public static class WorldGrid
    {
        public const int MaxAbsCell = 1 << 24;          // 16,777,216 cells: int-safe for sums of a few terms
        public const int RenderSafeAbsCell = 1 << 15;   // 32,768 cells = 16,384 units: float step <= 1/512 unit
        public const int MinChunkSize = 4;
        public const int MaxChunkSize = 256;

        public static int FloorDiv(int value, int divisor)
        {
            if (divisor <= 0) throw new ArgumentOutOfRangeException(nameof(divisor));
            var quotient = value / divisor;
            if (value % divisor != 0 && value < 0) quotient--;
            return quotient;
        }

        public static int FloorMod(int value, int divisor)
        {
            if (divisor <= 0) throw new ArgumentOutOfRangeException(nameof(divisor));
            var remainder = value % divisor;
            return remainder < 0 ? remainder + divisor : remainder;
        }

        public static bool IsValidCell(Vector2Int cell) =>
            cell.x >= -MaxAbsCell && cell.x <= MaxAbsCell && cell.y >= -MaxAbsCell && cell.y <= MaxAbsCell;

        public static bool IsRenderSafe(Vector2Int cell) =>
            Math.Abs((long)cell.x) <= RenderSafeAbsCell && Math.Abs((long)cell.y) <= RenderSafeAbsCell;

        public static void RequireChunkSize(int size)
        {
            if (size < MinChunkSize || size > MaxChunkSize)
                throw new ArgumentOutOfRangeException(nameof(size), "chunk size " + size + " outside " + MinChunkSize + ".." + MaxChunkSize);
        }

        public static ChunkCoord ChunkOf(Vector2Int cell, int size) => new ChunkCoord(FloorDiv(cell.x, size), FloorDiv(cell.y, size));

        public static Vector2Int LocalInChunk(Vector2Int cell, int size) => new Vector2Int(FloorMod(cell.x, size), FloorMod(cell.y, size));

        public static Vector2Int ChunkOrigin(ChunkCoord chunk, int size) => new Vector2Int(checked(chunk.X * size), checked(chunk.Y * size));

        public static Vector2Int ChunkToWorld(ChunkCoord chunk, Vector2Int local, int size)
        {
            if (local.x < 0 || local.y < 0 || local.x >= size || local.y >= size)
                throw new ArgumentOutOfRangeException(nameof(local), "chunk-local cell " + local + " outside 0.." + (size - 1));
            var origin = ChunkOrigin(chunk, size);
            return new Vector2Int(checked(origin.x + local.x), checked(origin.y + local.y));
        }

        // Inclusive cell rectangle of a chunk: min .. min + size - 1.
        public static CellRect ChunkRect(ChunkCoord chunk, int size)
        {
            var origin = ChunkOrigin(chunk, size);
            return new CellRect(origin, new Vector2Int(origin.x + size - 1, origin.y + size - 1));
        }

        public static Vector2Int CheckedAdd(Vector2Int a, Vector2Int b) => new Vector2Int(checked(a.x + b.x), checked(a.y + b.y));
        public static Vector2Int CheckedSubtract(Vector2Int a, Vector2Int b) => new Vector2Int(checked(a.x - b.x), checked(a.y - b.y));
    }

    // An inclusive rectangle of cells (Min and Max are both inside), the way Map bounds are written.
    public readonly struct CellRect : IEquatable<CellRect>
    {
        public readonly Vector2Int Min;
        public readonly Vector2Int Max;

        public CellRect(Vector2Int min, Vector2Int max)
        {
            Min = min;
            Max = max;
        }

        public int Width => Max.x - Min.x + 1;
        public int Height => Max.y - Min.y + 1;
        public bool IsEmpty => Max.x < Min.x || Max.y < Min.y;
        public bool Contains(Vector2Int cell) => cell.x >= Min.x && cell.x <= Max.x && cell.y >= Min.y && cell.y <= Max.y;
        public bool Overlaps(CellRect other) => !IsEmpty && !other.IsEmpty &&
            Min.x <= other.Max.x && other.Min.x <= Max.x && Min.y <= other.Max.y && other.Min.y <= Max.y;
        public CellRect Expanded(int cells) => new CellRect(Min - Vector2Int.one * cells, Max + Vector2Int.one * cells);
        public CellRect Union(CellRect other) => IsEmpty ? other : other.IsEmpty ? this :
            new CellRect(Vector2Int.Min(Min, other.Min), Vector2Int.Max(Max, other.Max));
        public int ChebyshevDistance(Vector2Int cell)
        {
            var dx = cell.x < Min.x ? Min.x - cell.x : cell.x > Max.x ? cell.x - Max.x : 0;
            var dy = cell.y < Min.y ? Min.y - cell.y : cell.y > Max.y ? cell.y - Max.y : 0;
            return Mathf.Max(dx, dy);
        }
        public bool Equals(CellRect other) => Min == other.Min && Max == other.Max;
        public override bool Equals(object obj) => obj is CellRect other && Equals(other);
        public override int GetHashCode() => Min.GetHashCode() * 31 + Max.GetHashCode();
        public override string ToString() => Min + ".." + Max;
    }
}
