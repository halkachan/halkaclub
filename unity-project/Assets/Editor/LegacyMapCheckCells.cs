using System;
using System.Linq;
using Halka.Game.World;
using UnityEditor;
using UnityEngine;

namespace Halka.Game.Editor
{
    // Compatibility for historical ver1.8/1.9 checks. Never stores coordinates.
    internal static class LegacyMapCheckCells
    {
        private static MapDefinition Map => AssetDatabase.LoadAssetAtPath<MapDefinition>(
            "Assets/Content/Maps/first_field.asset");

        internal static Vector2Int FlowerCell => First("flower_basic");
        internal static Vector2Int TreeRootCell => First("tree_basic");
        internal static Vector2Int[] DirtCells => Map.Surfaces
            .Select(item => item.Cell).ToArray();

        private static Vector2Int First(string id)
        {
            foreach (var placement in Map.Objects)
                if (placement.Definition != null && placement.Definition.StableId == id)
                    return placement.RootCell;
            throw new InvalidOperationException("Missing legacy object definition " + id);
        }
    }
}
