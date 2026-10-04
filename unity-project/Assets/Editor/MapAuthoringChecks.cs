using System;
using System.IO;
using System.Linq;
using Halka.Game.World;
using UnityEditor;
using UnityEngine;

namespace Halka.Game.Editor
{
    public static class MapAuthoringChecks
    {
        private const string OfficialPath = "Assets/Content/Maps/first_field.asset";
        private const string FixtureAssetPath = "Assets/Content/Maps/standalone_contract_fixture.asset";

        [MenuItem("HALKA WORLD/Validate Standalone Map Contract v0.4")]
        public static void Run()
        {
            MapAuthoringImporter.SyncAll();
            var official = AssetDatabase.LoadAssetAtPath<MapDefinition>(OfficialPath);
            var interior = AssetDatabase.LoadAssetAtPath<MapDefinition>(
                "Assets/Content/Maps/halka_house.asset");
            Check(official != null, "official generated cache exists");
            Check(interior != null && interior.MapId == "halka_house" &&
                interior.MapType == "interior" && interior.GrassMode == "none" &&
                interior.BaseSurface.StableId == "house_floor", "indoor map is imported");
            var bed = interior.Objects.Single(item => item.Definition.StableId == "bed_basic").Definition;
            Check(bed.ActionPoints.Count == 1 && bed.ActionPoints[0].Id == "sleep_main" &&
                bed.ActionPoints[0].PlayerCellOffset == new Vector2Int(0, 1) &&
                bed.ActionPoints[0].PlayerFacing == WorldFacing.Up &&
                bed.ActionPoints[0].ActionType == WorldActionType.Sleep &&
                bed.ActionPoints[0].PoseKey == "bed_sleep" &&
                bed.Behavior == WorldObjectBehavior.None, "bed sleep metadata is imported without execution");
            foreach (var idAndText in new[] { ("stone_basic", "いし。"),
                         ("flower_basic", "はな。"), ("tree_basic", "き。") })
            {
                var definition = official.Objects.First(item => item.Definition.StableId == idAndText.Item1).Definition;
                Check(definition.ActionPoints.Count == 1 &&
                    definition.ActionPoints[0].ActionType == WorldActionType.Examine &&
                    definition.ActionPoints[0].InteractionText == idAndText.Item2 &&
                    definition.ExamineMessage == idAndText.Item2 &&
                    definition.Behavior == WorldObjectBehavior.Examine,
                    idAndText.Item1 + " examine metadata matches runtime text");
            }
            var sign = AssetDatabase.LoadAssetAtPath<WorldObjectDefinition>("Assets/Content/Maps/sign_north.asset");
            Check(sign != null && sign.PreviewSprite == null && sign.ActionPoints.Count == 1 &&
                sign.ActionPoints[0].InteractionText == "きた" &&
                sign.ActionPoints[0].PlayerFacing == WorldFacing.Up &&
                sign.Behavior == WorldObjectBehavior.None,
                "unplaced sign metadata imported without runtime sprite or action");
            var before = JsonUtility.ToJson(official);
            var authoringPath = Path.GetFullPath(Path.Combine(Application.dataPath,
                "Content/Maps/Authoring/first_field.hwmap.json"));
            var source = File.ReadAllText(authoringPath);
            var fixture = source.Replace("\"mapId\": \"first_field\"",
                "\"mapId\": \"standalone_contract_fixture\"");
            Check(fixture != source, "fixture mapId replaced");
            var temporaryFolder = Path.Combine(Application.dataPath, "../Temp/StandaloneMapContract");
            Directory.CreateDirectory(temporaryFolder);
            var fixturePath = Path.Combine(temporaryFolder, "standalone_contract_fixture.hwmap.json");
            var invalidPath = Path.Combine(temporaryFolder, "first_field.hwmap.json");
            Check(AssetDatabase.LoadAssetAtPath<MapDefinition>(FixtureAssetPath) == null,
                "old fixture cache is absent");
            try
            {
                File.WriteAllText(fixturePath, fixture);
                var imported = MapAuthoringImporter.SyncMapFromFile(fixturePath);
                Check(imported != null && imported.MapId == "standalone_contract_fixture" &&
                    imported.MinCell == new Vector2Int(-10, -6) &&
                    imported.MaxCell == new Vector2Int(10, 6), "fixture bounds and mapId");
                Check(imported.Surfaces.Count == official.Surfaces.Count &&
                    imported.Objects.Count == official.Objects.Count,
                    "fixture placement counts");
                Check(imported.Objects.All(item => !string.IsNullOrWhiteSpace(item.InstanceId)) &&
                    imported.Objects.Select(item => item.InstanceId).Distinct().Count() == official.Objects.Count,
                    "fixture instance IDs are preserved and unique");
                Check(imported.PlayerSpawnCell == new Vector2Int(0, 0) &&
                    imported.HouseDoorCell == new Vector2Int(-7, -4) &&
                    imported.NorthRoadEnd == new Vector2Int(0, 6), "fixture markers");
                Check(MapPlacementRules.Validate(imported).Count == 0, "fixture validates");
                Check(interior.MinCell == new Vector2Int(-6, -4) &&
                    interior.MaxCell == new Vector2Int(6, 4) &&
                    interior.Surfaces.Count == 50 && interior.Objects.Count == 1 &&
                    interior.TryGetMarker("interior_entry", out var entry) && entry == new Vector2Int(0, -3) &&
                    interior.TryGetMarker("interior_exit", out var exit) && exit == new Vector2Int(0, -4) &&
                    MapPlacementRules.Validate(interior).Count == 0,
                    "indoor bounds, walls, bed and passage match the former room");
                File.WriteAllText(invalidPath, "{ broken json");
                var rejected = false;
                try { MapAuthoringImporter.SyncMapFromFile(invalidPath); }
                catch (InvalidDataException) { rejected = true; }
                Check(rejected, "invalid JSON rejected");
                Check(JsonUtility.ToJson(official) == before,
                    "invalid JSON leaves official generated cache unchanged");
                Debug.Log("HALKA WORLD Standalone Map Contract v0.4: passed.");
            }
            finally
            {
                AssetDatabase.DeleteAsset(FixtureAssetPath);
                if (File.Exists(fixturePath)) File.Delete(fixturePath);
                if (File.Exists(invalidPath)) File.Delete(invalidPath);
                if (Directory.Exists(temporaryFolder)) Directory.Delete(temporaryFolder);
                AssetDatabase.SaveAssets();
            }
        }

        private static void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("Standalone Map Contract failed: " + name);
        }
    }
}
