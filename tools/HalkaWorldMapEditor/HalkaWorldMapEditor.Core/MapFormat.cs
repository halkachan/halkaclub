using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace HalkaWorldMapEditor.Core;

public static class MapFormat
{
    private sealed class LegacyMapDocument
    {
        public string Format { get; set; } = "";
        public int FormatVersion { get; set; }
        public string MapId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public MapBounds Bounds { get; set; } = new();
        public List<SurfacePlacement> Surfaces { get; set; } = [];
        public List<ObjectPlacement> Objects { get; set; } = [];
        public MapMarkers Markers { get; set; } = new();
    }

    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static MapDocument ParseMap(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            Require(root, "format", "formatVersion", "mapId", "displayName", "bounds", "surfaces", "objects", "markers");
            if (root.GetProperty("formatVersion").GetInt32() == 1)
            {
                var legacy = JsonSerializer.Deserialize<LegacyMapDocument>(json, Options) ??
                    throw new InvalidDataException("Legacy Map JSON is empty.");
                if (legacy.Format != "halka-world-map" || legacy.Markers == null ||
                    legacy.Markers.HouseFootprint.Width != 5 || legacy.Markers.HouseFootprint.Height != 2)
                    throw new InvalidDataException("Unsupported legacy Map format.");
                var migrated = new MapDocument
                {
                    MapId = legacy.MapId, DisplayName = legacy.DisplayName,
                    Bounds = legacy.Bounds, Surfaces = legacy.Surfaces, Objects = legacy.Objects,
                    Markers = [
                        new() { Id = "player_start", Cell = legacy.Markers.PlayerSpawn },
                        new() { Id = "crow_spawn", Cell = legacy.Markers.CrowSpawn },
                        new() { Id = "road_north", Cell = legacy.Markers.RoadEnds.North },
                        new() { Id = "road_east", Cell = legacy.Markers.RoadEnds.East },
                        new() { Id = "road_south", Cell = legacy.Markers.RoadEnds.South },
                        new() { Id = "road_west", Cell = legacy.Markers.RoadEnds.West }
                    ]
                };
                if (!migrated.Objects.Any(item => item.DefinitionId == "house_main"))
                    migrated.Objects.Add(new ObjectPlacement
                    {
                        InstanceId = "obj_house_main_v03_migration",
                        DefinitionId = "house_main", RootCell = legacy.Markers.HouseDoor
                    });
                return migrated;
            }
            Require(root.GetProperty("bounds"), "minX", "maxX", "minY", "maxY");
            Require(root, "mapType", "baseSurfaceDefinitionId", "grassMode", "backdropColor");
            foreach (var marker in root.GetProperty("markers").EnumerateArray())
            { Require(marker, "id", "cell"); Require(marker.GetProperty("cell"), "x", "y"); }
            foreach (var surface in root.GetProperty("surfaces").EnumerateArray())
            { Require(surface, "definitionId", "cell"); Require(surface.GetProperty("cell"), "x", "y"); }
            foreach (var placement in root.GetProperty("objects").EnumerateArray())
            { Require(placement, "instanceId", "definitionId", "rootCell"); Require(placement.GetProperty("rootCell"), "x", "y"); }
            var map = JsonSerializer.Deserialize<MapDocument>(json, Options) ??
                throw new InvalidDataException("Map JSON is empty.");
            if (map.Format != "halka-world-map" || map.FormatVersion != 2)
                throw new InvalidDataException($"Unsupported map format {map.Format} v{map.FormatVersion}.");
            if (map.Bounds == null || map.Surfaces == null || map.Objects == null ||
                map.Markers == null)
                throw new InvalidDataException("Map JSON is missing a required section.");
            return map;
        }
        catch (InvalidOperationException error)
        { throw new InvalidDataException("Map JSON structure error: " + error.Message, error); }
        catch (KeyNotFoundException error)
        { throw new InvalidDataException("Map JSON missing required property: " + error.Message, error); }
        catch (JsonException error)
        {
            throw new InvalidDataException($"Map JSON parse error: line {error.LineNumber + 1}, byte {error.BytePositionInLine + 1}: {error.Message}", error);
        }
    }

    public static CatalogDocument ParseCatalog(string json)
    {
        try
        {
            var catalog = JsonSerializer.Deserialize<CatalogDocument>(json, Options) ??
                throw new InvalidDataException("Catalog JSON is empty.");
            if (catalog.Format != "halka-world-catalog" || catalog.FormatVersion != 2 ||
                catalog.Surfaces == null || catalog.Objects == null || catalog.Visuals == null)
                throw new InvalidDataException("Unsupported or incomplete catalog format.");
            if (catalog.Surfaces.Any(item => string.IsNullOrWhiteSpace(item.DefinitionId)) ||
                catalog.Objects.Any(item => string.IsNullOrWhiteSpace(item.DefinitionId)) ||
                catalog.Surfaces.Select(item => item.DefinitionId).Distinct(StringComparer.Ordinal).Count() != catalog.Surfaces.Count ||
                catalog.Objects.Select(item => item.DefinitionId).Distinct(StringComparer.Ordinal).Count() != catalog.Objects.Count)
                throw new InvalidDataException("Catalog has an empty or duplicate definitionId.");
            return catalog;
        }
        catch (JsonException error)
        {
            throw new InvalidDataException($"Catalog JSON parse error: line {error.LineNumber + 1}: {error.Message}", error);
        }
    }

    public static MapDocument LoadMap(string path) => ParseMap(File.ReadAllText(path, Encoding.UTF8));
    public static CatalogDocument LoadCatalog(string path) => ParseCatalog(File.ReadAllText(path, Encoding.UTF8));

    private static void Require(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Expected a JSON object.");
        foreach (var name in names)
            if (!element.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
                throw new InvalidDataException("Missing required property: " + name);
    }

    public static string SerializeMap(MapDocument map)
    {
        map.Surfaces = map.Surfaces.OrderBy(item => item.Cell.Y).ThenBy(item => item.Cell.X)
            .ThenBy(item => item.DefinitionId, StringComparer.Ordinal).ToList();
        map.Objects = map.Objects.OrderBy(item => item.DefinitionId, StringComparer.Ordinal)
            .ThenBy(item => item.RootCell.Y).ThenBy(item => item.RootCell.X)
            .ThenBy(item => item.InstanceId, StringComparer.Ordinal).ToList();
        map.Markers = map.Markers.OrderBy(item => item.Id, StringComparer.Ordinal).ToList();
        return JsonSerializer.Serialize(map, Options) + "\n";
    }

    public static MapDocument Clone(MapDocument map) => ParseMap(SerializeMap(map));

    public static void SaveAtomic(MapDocument map, string path)
    {
        var text = SerializeMap(map);
        ParseMap(text); // Verify serialized data before touching the original.
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        if (File.Exists(path) && File.ReadAllText(path, Encoding.UTF8) == text) return;
        var temporary = path + ".tmp";
        var backup = path + ".bak";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write,
                       FileShare.None, 4096, FileOptions.WriteThrough))
            {
                var bytes = new UTF8Encoding(false).GetBytes(text);
                stream.Write(bytes);
                stream.Flush(true);
            }
            if (File.Exists(path))
            {
                try { File.Replace(temporary, path, backup, true); }
                catch (PlatformNotSupportedException)
                {
                    File.Copy(path, backup, true);
                    File.Move(temporary, path, true);
                }
            }
            else File.Move(temporary, path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
