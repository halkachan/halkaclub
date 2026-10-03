namespace HalkaWorldMapEditor.Core;

public static class MapRules
{
    public static IEnumerable<GridCell> HouseBlockedCells(MapDocument map)
    {
        var door = map.Markers.HouseDoor;
        var size = map.Markers.HouseFootprint;
        var left = door.X - size.Width / 2;
        for (var y = 0; y < size.Height; y++)
        for (var x = 0; x < size.Width; x++)
        {
            var cell = new GridCell(left + x, door.Y + y);
            if (cell != door) yield return cell;
        }
    }

    public static bool IsProtected(MapDocument map, GridCell cell) =>
        HouseBlockedCells(map).Contains(cell) || cell == map.Markers.HouseDoor ||
        cell == map.Markers.OutsideEntry || cell == map.Markers.PlayerSpawn ||
        cell == map.Markers.CrowSpawn || RoadEndCells(map).Contains(cell);

    public static IEnumerable<GridCell> RoadEndCells(MapDocument map)
    {
        yield return map.Markers.RoadEnds.North;
        yield return map.Markers.RoadEnds.East;
        yield return map.Markers.RoadEnds.South;
        yield return map.Markers.RoadEnds.West;
    }

    public static IEnumerable<GridCell> FootprintCells(ObjectPlacement placement, CatalogObject definition)
    {
        for (var y = 0; y < definition.Footprint.Height; y++)
        for (var x = 0; x < definition.Footprint.Width; x++)
            yield return new GridCell(placement.RootCell.X + x, placement.RootCell.Y + y);
    }

    public static bool BlocksMovement(MapDocument map, CatalogDocument catalog, GridCell cell)
    {
        if (HouseBlockedCells(map).Contains(cell)) return true;
        var definitions = catalog.ObjectById;
        return map.Objects.Any(item => definitions.TryGetValue(item.DefinitionId, out var definition) &&
            definition.BlocksMovement && FootprintCells(item, definition).Contains(cell));
    }

    public static bool HasGrass(MapDocument map, CatalogDocument catalog, GridCell cell)
    {
        if (!map.Bounds.Contains(cell) || map.Surfaces.Any(item => item.Cell == cell) ||
            BlocksMovement(map, catalog, cell)) return false;
        var definitions = catalog.ObjectById;
        return !map.Objects.Any(item => item.RootCell == cell &&
            definitions.TryGetValue(item.DefinitionId, out var definition) && definition.ExcludeGrass);
    }

    public static bool CanPlace(MapDocument map, CatalogDocument catalog, string definitionId,
        GridCell root, out string reason, string? ignoreInstanceId = null)
    {
        if (!catalog.ObjectById.TryGetValue(definitionId, out var definition) ||
            !definition.EditorSelectable)
        { reason = "このオブジェクトはPaletteにありません。"; return false; }
        if (definition.Footprint.Width < 1 || definition.Footprint.Height < 1)
        { reason = "Footprintが不正です。"; return false; }
        var candidate = new ObjectPlacement { DefinitionId = definitionId, RootCell = root };
        foreach (var cell in FootprintCells(candidate, definition))
        {
            if (!map.Bounds.Contains(cell))
            { reason = "マップ範囲外です。"; return false; }
            if (IsProtected(map, cell))
            { reason = "家・Spawn・将来のエリア接続用セルは保護されています。"; return false; }
            if (map.Surfaces.Any(item => item.Cell == cell))
            { reason = "地面SurfaceとObjectのFootprintは重ねられません。"; return false; }
            foreach (var existing in map.Objects)
            {
                if (existing.InstanceId == ignoreInstanceId) continue;
                if (!catalog.ObjectById.TryGetValue(existing.DefinitionId, out var other)) continue;
                if (FootprintCells(existing, other).Contains(cell))
                { reason = "既存ObjectのFootprintと重なります。"; return false; }
            }
        }
        reason = "";
        return true;
    }

    public static IReadOnlyList<MapIssue> Validate(MapDocument map, CatalogDocument catalog)
    {
        var issues = new List<MapIssue>();
        if (map.Format != "halka-world-map" || map.FormatVersion != 1)
            issues.Add(new("format", "Map format/versionが未対応です。"));
        if (string.IsNullOrWhiteSpace(map.MapId) ||
            !System.Text.RegularExpressions.Regex.IsMatch(map.MapId, "^[a-z0-9_-]+$"))
            issues.Add(new("mapId", "Map IDは小文字英数字、_、-のみです。"));
        if (map.Bounds.MinX > map.Bounds.MaxX || map.Bounds.MinY > map.Bounds.MaxY)
            issues.Add(new("bounds", "Boundsが逆転しています。"));
        if (map.Markers.HouseFootprint.Width != 5 || map.Markers.HouseFootprint.Height != 2)
            issues.Add(new("houseFootprint", "現在のRuntimeでは家のFootprintは5×2固定です。"));
        var surfaceIds = catalog.SurfaceById;
        var objectIds = catalog.ObjectById;
        var seenSurface = new HashSet<GridCell>();
        foreach (var placement in map.Surfaces)
        {
            if (!map.Bounds.Contains(placement.Cell))
                issues.Add(new("surfaceBounds", "地面が範囲外です。", placement.Cell));
            if (!surfaceIds.ContainsKey(placement.DefinitionId))
                issues.Add(new("surfaceDefinition", "不明な地面ID: " + placement.DefinitionId, placement.Cell));
            if (!seenSurface.Add(placement.Cell))
                issues.Add(new("duplicateSurface", "地面が重複しています。", placement.Cell));
        }
        var seenRoot = new HashSet<GridCell>();
        var seenInstance = new HashSet<string>(StringComparer.Ordinal);
        var occupied = new HashSet<GridCell>();
        foreach (var placement in map.Objects)
        {
            if (!seenRoot.Add(placement.RootCell))
                issues.Add(new("duplicateRoot", "Object Rootが重複しています。", placement.RootCell));
            if (string.IsNullOrWhiteSpace(placement.InstanceId) || !seenInstance.Add(placement.InstanceId))
                issues.Add(new("duplicateInstanceId", "instanceIdが空または重複しています。", placement.RootCell));
            if (!objectIds.TryGetValue(placement.DefinitionId, out var definition))
            { issues.Add(new("objectDefinition", "不明なObject ID: " + placement.DefinitionId, placement.RootCell)); continue; }
            if (definition.Footprint.Width < 1 || definition.Footprint.Height < 1)
            { issues.Add(new("footprint", "Footprintが不正です。", placement.RootCell)); continue; }
            foreach (var cell in FootprintCells(placement, definition))
            {
                if (!map.Bounds.Contains(cell)) issues.Add(new("objectBounds", "Objectが範囲外です。", cell));
                if (IsProtected(map, cell)) issues.Add(new("protected", "保護セルにObjectがあります。", cell));
                if (seenSurface.Contains(cell)) issues.Add(new("surfaceOverlap", "Objectと地面Surfaceが重なります。", cell));
                if (!occupied.Add(cell)) issues.Add(new("overlap", "ObjectのFootprintが重なります。", cell));
            }
        }
        foreach (var marker in new[] { map.Markers.HouseDoor, map.Markers.OutsideEntry,
                     map.Markers.PlayerSpawn, map.Markers.CrowSpawn }.Concat(RoadEndCells(map)))
        {
            if (!map.Bounds.Contains(marker)) issues.Add(new("markerBounds", "Markerが範囲外です。", marker));
            if (BlocksMovement(map, catalog, marker))
                issues.Add(new("markerBlocked", "入口・Spawn・Road Endが閉塞しています。", marker));
        }
        return issues;
    }
}
