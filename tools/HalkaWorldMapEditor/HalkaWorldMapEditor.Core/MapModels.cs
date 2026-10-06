using System.Text.Json.Serialization;

namespace HalkaWorldMapEditor.Core;

public readonly record struct GridCell(int X, int Y)
{
    public override string ToString() => $"({X},{Y})";
}

public sealed class MapBounds
{
    public int MinX { get; set; }
    public int MaxX { get; set; }
    public int MinY { get; set; }
    public int MaxY { get; set; }
    public bool Contains(GridCell cell) => cell.X >= MinX && cell.X <= MaxX &&
        cell.Y >= MinY && cell.Y <= MaxY;
}

public sealed class GridSize
{
    public int Width { get; set; } = 1;
    public int Height { get; set; } = 1;
}

public sealed class SurfacePlacement
{
    public string DefinitionId { get; set; } = "";
    public GridCell Cell { get; set; }
}

public sealed class ObjectPlacement
{
    public string InstanceId { get; set; } = "";
    public string DefinitionId { get; set; } = "";
    public GridCell RootCell { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SignText { get; set; }
}

public sealed class RoadEnds
{
    public GridCell North { get; set; }
    public GridCell East { get; set; }
    public GridCell South { get; set; }
    public GridCell West { get; set; }
}

public sealed class MapMarker
{
    public string Id { get; set; } = "";
    public GridCell Cell { get; set; }
}

public sealed class MapMarkers
{
    public GridCell PlayerSpawn { get; set; }
    public GridCell CrowSpawn { get; set; }
    public GridCell HouseDoor { get; set; }
    public GridCell OutsideEntry { get; set; }
    public GridSize HouseFootprint { get; set; } = new() { Width = 5, Height = 2 };
    public RoadEnds RoadEnds { get; set; } = new();
}

public sealed class MapDocument
{
    public string Format { get; set; } = "halka-world-map";
    public int FormatVersion { get; set; } = 2;
    public string MapId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string MapType { get; set; } = "outdoor";
    public string BaseSurfaceDefinitionId { get; set; } = "base_ground";
    public string GrassMode { get; set; } = "auto";
    public string BackdropColor { get; set; } = "#101014";
    public MapBounds Bounds { get; set; } = new();
    public List<SurfacePlacement> Surfaces { get; set; } = [];
    public List<ObjectPlacement> Objects { get; set; } = [];
    public List<MapMarker> Markers { get; set; } = [];

    public GridCell? Marker(string id) => Markers.FirstOrDefault(item => item.Id == id)?.Cell;
}

public sealed class CatalogSurface
{
    public string DefinitionId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Category { get; set; } = "surface";
    public string PreviewSpritePath { get; set; } = "";
    public int VisualWidthPixels { get; set; } = 32;
    public int VisualHeightPixels { get; set; } = 32;
    public bool BlocksMovement { get; set; }
    public bool GrowsGrass { get; set; }
    public List<string> AllowedMapTypes { get; set; } = [];
    public bool EditorSelectable { get; set; } = true;
}

public sealed class CatalogObject
{
    public string DefinitionId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Category { get; set; } = "object";
    public string PreviewSpritePath { get; set; } = "";
    public int VisualWidthPixels { get; set; } = 32;
    public int VisualHeightPixels { get; set; } = 32;
    public string RootAnchor { get; set; } = "bottom-center";
    public GridSize Footprint { get; set; } = new();
    public List<GridCell> BlockedCellOffsets { get; set; } = [];
    public List<string> AllowedMapTypes { get; set; } = [];
    public bool BlocksMovement { get; set; } = true;
    public bool ExcludeGrass { get; set; } = true;
    public bool EditorSelectable { get; set; } = true;
    public List<ActionPoint> ActionPoints { get; set; } = [];
}

public sealed class ActionPoint
{
    public string Id { get; set; } = "";
    public GridCell PlayerCellOffset { get; set; }
    public string PlayerFacing { get; set; } = "up";
    public string ActionType { get; set; } = "none";
    public string? PoseKey { get; set; }
    public string? InteractionText { get; set; }
}

public sealed class CatalogVisuals
{
    public string GrassSpritePath { get; set; } = "Assets/Content/World/grass.png";
    public string HouseSpritePath { get; set; } = "Assets/Content/World/house_exterior.png";
    public string CrowSpritePath { get; set; } = "Assets/Content/World/crow_idle_right.png";
    public string PlayerSpritePath { get; set; } = "Assets/Content/Character/front_idle/00.png";
}

public sealed class CatalogDocument
{
    public string Format { get; set; } = "halka-world-catalog";
    public int FormatVersion { get; set; } = 3;
    public List<CatalogSurface> Surfaces { get; set; } = [];
    public List<CatalogObject> Objects { get; set; } = [];
    public CatalogVisuals Visuals { get; set; } = new();

    [JsonIgnore] public Dictionary<string, CatalogSurface> SurfaceById =>
        Surfaces.ToDictionary(item => item.DefinitionId, StringComparer.Ordinal);
    [JsonIgnore] public Dictionary<string, CatalogObject> ObjectById =>
        Objects.ToDictionary(item => item.DefinitionId, StringComparer.Ordinal);
}

public sealed record MapIssue(string Code, string Message, GridCell? Cell = null, bool IsWarning = false);
