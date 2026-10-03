namespace HalkaWorldMapEditor.Core;

public sealed class MapSession
{
    private readonly Stack<string> undo = new();
    private readonly Stack<string> redo = new();
    private string savedSnapshot;
    private string? strokeStart;

    public MapDocument Map { get; private set; }
    public CatalogDocument Catalog { get; }
    public string FilePath { get; }
    public bool IsDirty => MapFormat.SerializeMap(Map) != savedSnapshot;
    public bool CanUndo => undo.Count > 0;
    public bool CanRedo => redo.Count > 0;

    public MapSession(MapDocument map, CatalogDocument catalog, string filePath)
    {
        Map = map;
        Catalog = catalog;
        FilePath = filePath;
        savedSnapshot = MapFormat.SerializeMap(map);
    }

    public static MapSession Open(string path, CatalogDocument catalog) =>
        new(MapFormat.LoadMap(path), catalog, path);

    private void Change(Action action)
    {
        var before = MapFormat.SerializeMap(Map);
        action();
        if (MapFormat.SerializeMap(Map) == before) return;
        if (strokeStart == null) undo.Push(before);
        redo.Clear();
    }

    public void BeginStroke() => strokeStart ??= MapFormat.SerializeMap(Map);
    public void EndStroke()
    {
        if (strokeStart == null) return;
        if (MapFormat.SerializeMap(Map) != strokeStart) undo.Push(strokeStart);
        strokeStart = null;
    }

    public bool PaintSurface(string definitionId, GridCell cell)
    {
        if (!Map.Bounds.Contains(cell) || !Catalog.SurfaceById.ContainsKey(definitionId) ||
            Map.Objects.Any(item => Catalog.ObjectById.TryGetValue(item.DefinitionId, out var definition) &&
                MapRules.FootprintCells(item, definition).Contains(cell)) ||
            Map.Surfaces.Any(item => item.Cell == cell && item.DefinitionId == definitionId)) return false;
        Change(() =>
        {
            Map.Surfaces.RemoveAll(item => item.Cell == cell);
            Map.Surfaces.Add(new SurfacePlacement { DefinitionId = definitionId, Cell = cell });
        });
        return true;
    }

    public bool EraseSurface(GridCell cell)
    {
        if (!Map.Surfaces.Any(item => item.Cell == cell)) return false;
        Change(() => Map.Surfaces.RemoveAll(item => item.Cell == cell));
        return true;
    }

    public bool PlaceObject(string definitionId, GridCell cell, out string reason)
    {
        if (!MapRules.CanPlace(Map, Catalog, definitionId, cell, out reason)) return false;
        Change(() => Map.Objects.Add(new ObjectPlacement
        {
            InstanceId = "obj_" + Guid.NewGuid().ToString("N"),
            DefinitionId = definitionId,
            RootCell = cell
        }));
        return true;
    }

    public bool MoveObject(string instanceId, GridCell target, out string reason)
    {
        var placement = Map.Objects.FirstOrDefault(item => item.InstanceId == instanceId);
        if (placement == null) { reason = "Objectが見つかりません。"; return false; }
        if (placement.RootCell == target) { reason = ""; return true; }
        if (!MapRules.CanPlace(Map, Catalog, placement.DefinitionId, target, out reason, instanceId)) return false;
        Change(() => placement.RootCell = target);
        return true;
    }

    public bool DeleteObject(string instanceId)
    {
        if (!Map.Objects.Any(item => item.InstanceId == instanceId)) return false;
        Change(() => Map.Objects.RemoveAll(item => item.InstanceId == instanceId));
        return true;
    }

    public bool CopyObject(string instanceId, GridCell target, out string reason)
    {
        var source = Map.Objects.FirstOrDefault(item => item.InstanceId == instanceId);
        if (source == null) { reason = "Objectが見つかりません。"; return false; }
        return PlaceObject(source.DefinitionId, target, out reason);
    }

    public void Undo()
    {
        EndStroke();
        if (undo.Count == 0) return;
        redo.Push(MapFormat.SerializeMap(Map));
        Map = MapFormat.ParseMap(undo.Pop());
    }

    public void Redo()
    {
        EndStroke();
        if (redo.Count == 0) return;
        undo.Push(MapFormat.SerializeMap(Map));
        Map = MapFormat.ParseMap(redo.Pop());
    }

    public void Save()
    {
        EndStroke();
        var issues = MapRules.Validate(Map, Catalog);
        if (issues.Count > 0) throw new InvalidDataException(
            "Map validation failed: " + string.Join("; ", issues.Take(5).Select(item => item.Message)));
        MapFormat.SaveAtomic(Map, FilePath);
        savedSnapshot = MapFormat.SerializeMap(Map);
    }
}
