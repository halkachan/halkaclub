using System;
using System.Linq;
using System.Reflection;
using Halka.Game.Core;
using Halka.Game.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Halka.Game.Editor
{
    public static class Version29Checks
    {
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

        [MenuItem("HALKA/Validate ver2.9 scene")]
        public static void Run()
        {
            Version28Checks.Run();
            Check((GameVersion.Value == "2.9" || GameVersion.Value == "3.0"), "game version");
            ProjectBuilder.PrepareScene();
            EditorSceneManager.OpenScene("Assets/Scenes/FirstDay.unity");
            var map = AssetDatabase.LoadAssetAtPath<MapDefinition>(
                "Assets/Content/Maps/first_field.asset");
            var loader = UnityEngine.Object.FindFirstObjectByType<MapRuntimeLoader2D>();
            var crow = UnityEngine.Object.FindFirstObjectByType<CrowWander2D>(FindObjectsInactive.Include);
            var occupancy = UnityEngine.Object.FindFirstObjectByType<DynamicGridOccupancy2D>();
            Check(map != null && loader != null && crow != null && occupancy != null &&
                map.DataVersion == 4 && map.MapId == "first_field", "formal outdoor map and runtime");
            var treeCount = map.Objects.Count(item => item.Definition.StableId == "tree_basic");
            Check(treeCount >= 2, "formal map has several authored trees");

            foreach (var suffix in new[] { "right_up", "right_down", "down_up", "down_down" })
            {
                var path = "Assets/Content/World/crow_flight_" + suffix + ".png";
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Check(sprite != null && sprite.rect.size == new Vector2(32, 32) &&
                    Mathf.Approximately(sprite.pixelsPerUnit, 64f) && importer != null &&
                    importer.filterMode == FilterMode.Point && !importer.mipmapEnabled &&
                    importer.textureCompression == TextureImporterCompression.Uncompressed,
                    "flight sprite import: " + suffix);
            }

            var randomState = UnityEngine.Random.state;
            UnityEngine.Random.InitState(29);
            try
            {
                loader.Build();
                var trees = crow.FindPerchCandidates();
                Check(trees.Length == treeCount && trees.All(tree => tree.IsUsable),
                    "runtime tree search uses every authored tree");
                var tree = trees[0];
                var before = tree.PerchPosition;
                tree.transform.position += Vector3.right * GridWorld2D.TileWorldSize;
                Check(Vector3.Distance(tree.PerchPosition,
                    before + Vector3.right * GridWorld2D.TileWorldSize) < 0.001f,
                    "perch follows moved tree visual");
                tree.transform.position -= Vector3.right * GridWorld2D.TileWorldSize;

                var now = Time.time;
                var groundCell = crow.Cell;
                Check(crow.Activity == CrowActivity.GroundIdle &&
                    occupancy.IsReservedByCrow(groundCell) && crow.CanInteract,
                    "ground occupancy and interaction");
                var allTreesRoot = (Transform)Get(crow, "treeRoot");
                Set(crow, "treeRoot", null);
                Check(crow.FindPerchCandidates().Length == 0,
                    "map without trees has no perch candidates");
                Set(crow, "nextFlightAt", now);
                Set(crow, "nextActionAt", now + 100f);
                crow.Tick(now + 0.001f);
                Check(crow.Activity == CrowActivity.GroundIdle &&
                    occupancy.IsReservedByCrow(groundCell),
                    "crow stays safely on ground without trees");
                Set(crow, "treeRoot", trees[0].transform);
                Check(crow.FindPerchCandidates().Length == 1,
                    "single tree is a valid perch candidate");
                Set(crow, "treeRoot", allTreesRoot);
                Set(crow, "nextFlightAt", now);
                Set(crow, "nextActionAt", now + 100f);
                crow.Tick(now + 0.01f);
                Check(crow.Activity == CrowActivity.Takeoff &&
                    occupancy.IsReservedByCrow(groundCell) && !crow.CanInteract,
                    "takeoff retains ground reservation until airborne");
                crow.Tick(now + 0.25f);
                Check(crow.Activity == CrowActivity.Takeoff &&
                    Vector3.Distance(crow.transform.position,
                        ((GridWorld2D)Get(crow, "world")).CellToWorld(groundCell)) > 0.01f,
                    "takeoff moves continuously");
                crow.Tick(now + 0.5f);
                Check(crow.Activity == CrowActivity.Flying &&
                    !occupancy.IsReservedByCrow(groundCell) && !crow.CanInteract,
                    "flight releases ground occupancy");
                crow.Tick(now + 4.1f);
                Check(crow.Activity == CrowActivity.Perched &&
                    !crow.CanInteract && !occupancy.IsReservedByCrow(groundCell) &&
                    crow.TargetTree != null &&
                    Vector3.Distance(crow.transform.position, crow.TargetTree.PerchPosition) < 0.001f,
                    "tree perch is anchored and has no ground occupancy");
                crow.Tick(now + 13f);
                Check(crow.Activity == CrowActivity.LeavingPerch,
                    "perch rest finishes with a separate takeoff");
                crow.Tick(now + 13.4f);
                Check(crow.Activity == CrowActivity.Flying,
                    "crow flies back toward a ground landing");
                crow.Tick(now + 17f);
                Check(crow.Activity == CrowActivity.LandingGround,
                    "ground approach precedes touchdown");
                crow.Tick(now + 17.5f);
                Check(crow.Activity == CrowActivity.GroundIdle &&
                    occupancy.IsReservedByCrow(crow.Cell) && crow.CanInteract &&
                    crow.Cell.x >= crow.RangeMin.x && crow.Cell.x <= crow.RangeMax.x &&
                    crow.Cell.y >= crow.RangeMin.y && crow.Cell.y <= crow.RangeMax.y,
                    "safe landing restores ground wander and interaction");

                Set(crow, "nextFlightAt", now + 17.5f);
                Set(crow, "nextActionAt", now + 100f);
                crow.Tick(now + 17.51f);
                Check(crow.Activity == CrowActivity.Takeoff,
                    "second flight cycle can start from landed ground");
                Debug.Log("HALKA ver2.9 crow flight, perch, occupancy and inherited checks passed.");
            }
            finally { UnityEngine.Random.state = randomState; }
        }

        private static object Get(object target, string name) =>
            target.GetType().GetField(name, Hidden).GetValue(target);
        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, Hidden).SetValue(target, value);
        private static void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException("Version29Checks failed: " + description);
        }
    }
}
