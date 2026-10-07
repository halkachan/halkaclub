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
        if (!Map.Bounds.Contains(cell) || !Catalog.SurfaceById.TryGetValue(definitionId, out var surface) ||
            (surface.BlocksMovement && (MapRules.IsProtected(Map, cell) ||
                Map.Objects.Any(item => Catalog.ObjectById.TryGetValue(item.DefinitionId, out var definition) &&
                    MapRules.FootprintCells(item, definition).Contains(cell))))) return false;
        if (definitionId == Map.BaseSurfaceDefinitionId &&
            (definitionId != "base_ground" || Map.GrassMode == "auto"))
            return EraseSurface(cell);
        if (Map.Surfaces.Any(item => item.Cell == cell && item.DefinitionId == definitionId)) return false;
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

    // Paints explicit grass ground indoors; on an outdoor grass base this erases the override.
    public bool RestoreGrass(GridCell cell) => PaintSurface("base_ground", cell);
    public bool RestoreBaseSurface(GridCell cell) => EraseSurface(cell);

    public bool PlaceObject(string definitionId, GridCell cell, out string reason, string? signText = null)
    {
        if (!MapRules.CanPlace(Map, Catalog, definitionId, cell, out reason)) return false;
        Change(() => Map.Objects.Add(new ObjectPlacement
        {
            InstanceId = "obj_" + Guid.NewGuid().ToString("N"),
            DefinitionId = definitionId,
            RootCell = cell,
            SignText = definitionId == "sign_basic" ? signText ?? "かんばん。" : null
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
        return PlaceObject(source.DefinitionId, target, out reason, source.SignText);
    }

    public bool SetSignText(string instanceId, string text)
    {
        var placement = Map.Objects.FirstOrDefault(item => item.InstanceId == instanceId &&
            item.DefinitionId == "sign_basic");
        var normalized = text.Trim();
        if (placement == null || normalized.Length is < 1 or > 80 || placement.SignText == normalized)
            return false;
        Change(() => placement.SignText = normalized);
        return true;
    }

    public bool PlaceEntity(string definitionId, GridCell cell, out string reason)
    {
        if (!MapRules.CanPlaceEntity(Map, Catalog, definitionId, cell, out reason)) return false;
        if (Catalog.EntityCatalog.ById[definitionId].SpawnMode == "game-start" &&
            OtherMaps().Any(map => map.EntitySpawns.Any(item => item.DefinitionId == definitionId)))
        { reason = "ﾊﾙｶﾁｬﾝ開始位置は別Mapに既に存在します。"; return false; }
        Change(() => Map.EntitySpawns.Add(new EntitySpawn {
            InstanceId = "ent_" + Guid.NewGuid().ToString("N"), DefinitionId = definitionId,
            Cell = cell, Facing = Catalog.EntityCatalog.ById[definitionId].DefaultFacing }));
        return true;
    }

    public bool MoveEntity(string instanceId, GridCell target, out string reason)
    {
        var spawn = Map.EntitySpawns.FirstOrDefault(item => item.InstanceId == instanceId);
        if (spawn == null) { reason = "Entityが見つかりません。"; return false; }
        if (!MapRules.CanPlaceEntity(Map, Catalog, spawn.DefinitionId, target, out reason, instanceId)) return false;
        Change(() => spawn.Cell = target);
        return true;
    }

    public bool SetEntityFacing(string instanceId, string facing)
    {
        var spawn = Map.EntitySpawns.FirstOrDefault(item => item.InstanceId == instanceId);
        if (spawn == null || facing is not ("up" or "down" or "left" or "right") || spawn.Facing == facing) return false;
        Change(() => spawn.Facing = facing);
        return true;
    }

    public bool DeleteEntity(string instanceId)
    {
        if (!Map.EntitySpawns.Any(item => item.InstanceId == instanceId)) return false;
        Change(() => Map.EntitySpawns.RemoveAll(item => item.InstanceId == instanceId));
        return true;
    }

    public bool CopyEntity(string instanceId, GridCell target, out string reason)
    {
        var spawn = Map.EntitySpawns.FirstOrDefault(item => item.InstanceId == instanceId);
        if (spawn == null) { reason = "Entityが見つかりません。"; return false; }
        if (Catalog.EntityCatalog.ById[spawn.DefinitionId].SpawnMode == "game-start")
        { reason = "ﾊﾙｶﾁｬﾝ開始位置はコピーできません。"; return false; }
        if (!PlaceEntity(spawn.DefinitionId, target, out reason)) return false;
        var placed = Map.EntitySpawns.Last(item => item.Cell == target && item.DefinitionId == spawn.DefinitionId);
        SetEntityFacing(placed.InstanceId, spawn.Facing);
        return true;
    }

    public IReadOnlyList<MapDocument> WorldMaps() => OtherMaps().Append(Map).ToArray();

    public bool AddTransition(GridCell source, out string reason)
    {
        if (!Map.Bounds.Contains(source) || MapRules.BlocksMovement(Map, Catalog, source))
        { reason = "出口は通行可能なMap内のCellに配置してください。"; return false; }
        var direction = source.Y == Map.Bounds.MaxY ? "up" :
            source.Y == Map.Bounds.MinY ? "down" :
            source.X == Map.Bounds.MaxX ? "right" :
            source.X == Map.Bounds.MinX ? "left" : "";
        if (direction.Length == 0)
        { reason = "Area TransitionはMap端へ配置してください。"; return false; }
        if (Map.AreaTransitions.Any(item => item.SourceCell == source && item.ExitDirection == direction))
        { reason = "この出口には既にTransitionがあります。"; return false; }
        var destination = WorldMaps().FirstOrDefault(item => item.MapId != Map.MapId) ?? Map;
        var arrival = destination.EntitySpawns.FirstOrDefault(item => item.DefinitionId == "player_main")?.Cell ??
            destination.Markers.FirstOrDefault(item => item.Id == "interior_entry")?.Cell ??
            new GridCell(destination.Bounds.MinX, destination.Bounds.MinY);
        if (MapRules.BlocksMovement(destination, Catalog, arrival))
        {
            var found = false;
            for (var y = destination.Bounds.MinY; y <= destination.Bounds.MaxY && !found; y++)
            for (var x = destination.Bounds.MinX; x <= destination.Bounds.MaxX; x++)
                if (!MapRules.BlocksMovement(destination, Catalog, new GridCell(x, y)))
                { arrival = new GridCell(x, y); found = true; break; }
            if (!found) { reason = "行き先Mapに通行可能なCellがありません。"; return false; }
        }
        var serial = 1;
        string id;
        do { id = $"exit_{direction}_{serial++}"; }
        while (Map.AreaTransitions.Any(item => item.TransitionId == id));
        Change(() => Map.AreaTransitions.Add(new AreaTransition {
            InstanceId = "transition_" + Guid.NewGuid().ToString("N"), TransitionId = id,
            SourceCell = source, ExitDirection = direction, DestinationMapId = destination.MapId,
            DestinationCell = arrival, ArrivalFacing = direction }));
        reason = "";
        return true;
    }

    public bool UpdateTransition(string instanceId, string transitionId, GridCell source, string exitDirection,
        string destinationMapId, GridCell destination, string arrivalFacing, out string reason)
    {
        var item = Map.AreaTransitions.FirstOrDefault(value => value.InstanceId == instanceId);
        if (item == null) { reason = "Transitionが見つかりません。"; return false; }
        var candidate = new AreaTransition { InstanceId = instanceId, TransitionId = transitionId.Trim(),
            SourceCell = source, ExitDirection = exitDirection, DestinationMapId = destinationMapId,
            DestinationCell = destination, ArrivalFacing = arrivalFacing };
        var probe = MapFormat.Clone(Map);
        var index = probe.AreaTransitions.FindIndex(value => value.InstanceId == instanceId);
        probe.AreaTransitions[index] = candidate;
        var errors = MapRules.Validate(probe, Catalog, OtherMaps().Append(probe))
            .Where(issue => !issue.IsWarning && issue.Cell == source &&
                issue.Code.StartsWith("transition", StringComparison.Ordinal)).ToArray();
        if (errors.Length > 0) { reason = errors[0].Message; return false; }
        Change(() => { item.TransitionId = candidate.TransitionId; item.SourceCell = source;
            item.ExitDirection = exitDirection; item.DestinationMapId = destinationMapId;
            item.DestinationCell = destination; item.ArrivalFacing = arrivalFacing; });
        reason = "";
        return true;
    }

    public bool DeleteTransition(string instanceId)
    {
        if (!Map.AreaTransitions.Any(item => item.InstanceId == instanceId)) return false;
        Change(() => Map.AreaTransitions.RemoveAll(item => item.InstanceId == instanceId));
        return true;
    }

    private IEnumerable<MapDocument> OtherMaps()
    {
        var folder = Path.GetDirectoryName(FilePath);
        if (folder == null || !Directory.Exists(folder)) yield break;
        foreach (var path in Directory.GetFiles(folder, "*.hwmap.json"))
            if (!string.Equals(Path.GetFullPath(path), Path.GetFullPath(FilePath), StringComparison.OrdinalIgnoreCase))
                yield return MapFormat.LoadMap(path);
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
        var issues = MapRules.Validate(Map, Catalog, WorldMaps());
        var errors = issues.Where(item => !item.IsWarning).ToArray();
        if (errors.Length > 0) throw new InvalidDataException(
            "Map validation failed: " + string.Join("; ", errors.Take(5).Select(item => item.Message)));
        if (Path.GetFileName(Path.GetDirectoryName(FilePath)) == "Authoring")
        {
            var allMaps = OtherMaps().Append(Map).ToArray();
            if (allMaps.Sum(item => item.EntitySpawns.Count(spawn => spawn.DefinitionId == "player_main")) != 1)
                throw new InvalidDataException("World全体でﾊﾙｶﾁｬﾝ開始位置は1個必要です。");
            if (allMaps.SelectMany(item => item.EntitySpawns).GroupBy(item => item.InstanceId)
                .Any(group => group.Count() > 1))
                throw new InvalidDataException("Entity instanceIdがWorld内で重複しています。");
        }
        MapFormat.SaveAtomic(Map, FilePath);
        savedSnapshot = MapFormat.SerializeMap(Map);
    }
}
