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

        [MenuItem("HALKA WORLD/Validate Standalone Map Contract v0.2")]
        public static void Run()
        {
            var official = AssetDatabase.LoadAssetAtPath<MapDefinition>(OfficialPath);
            Check(official != null, "official generated cache exists");
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
                Check(imported.Surfaces.Count == 52 && imported.Objects.Count == 11,
                    "fixture placement counts");
                Check(imported.Objects.All(item => !string.IsNullOrWhiteSpace(item.InstanceId)) &&
                    imported.Objects.Select(item => item.InstanceId).Distinct().Count() == 11,
                    "fixture instance IDs are preserved and unique");
                Check(imported.PlayerSpawnCell == new Vector2Int(0, 0) &&
                    imported.HouseDoorCell == new Vector2Int(-7, -4) &&
                    imported.NorthRoadEnd == new Vector2Int(0, 6), "fixture markers");
                Check(MapPlacementRules.Validate(imported).Count == 0, "fixture validates");
                File.WriteAllText(invalidPath, "{ broken json");
                var rejected = false;
                try { MapAuthoringImporter.SyncMapFromFile(invalidPath); }
                catch (InvalidDataException) { rejected = true; }
                Check(rejected, "invalid JSON rejected");
                Check(JsonUtility.ToJson(official) == before,
                    "invalid JSON leaves official generated cache unchanged");
                Debug.Log("HALKA WORLD Standalone Map Contract v0.2: passed.");
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
