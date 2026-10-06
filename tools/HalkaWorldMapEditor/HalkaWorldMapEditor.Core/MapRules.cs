namespace HalkaWorldMapEditor.Core;

public static class MapRules
{
    public static GridCell? HouseRoot(MapDocument map) =>
        map.Objects.FirstOrDefault(item => item.DefinitionId == "house_main")?.RootCell;

    public static GridCell? OutsideEntry(MapDocument map) => HouseRoot(map) is { } root
        ? new GridCell(root.X, root.Y - 1) : null;

    public static IEnumerable<GridCell> RoadEndCells(MapDocument map) => map.Markers
        .Where(item => item.Id.StartsWith("road_", StringComparison.Ordinal)).Select(item => item.Cell);

    public static bool IsProtected(MapDocument map, GridCell cell) =>
        map.Markers.Any(item => item.Cell == cell) || OutsideEntry(map) == cell || HouseRoot(map) == cell;

    public static IEnumerable<GridCell> FootprintCells(ObjectPlacement placement, CatalogObject definition)
    {
        foreach (var offset in definition.BlockedCellOffsets)
            yield return new GridCell(placement.RootCell.X + offset.X, placement.RootCell.Y + offset.Y);
    }

    public static GridCell ActionCell(ObjectPlacement placement, ActionPoint point) =>
        new(placement.RootCell.X + point.PlayerCellOffset.X,
            placement.RootCell.Y + point.PlayerCellOffset.Y);

    public static string? ActionText(ObjectPlacement placement, ActionPoint point) =>
        placement.DefinitionId == "sign_basic" && point.ActionType == "examine"
            ? placement.SignText : point.InteractionText;

    public static bool HasSprite(CatalogObject definition) =>
        !string.IsNullOrWhiteSpace(definition.PreviewSpritePath) &&
        definition.VisualWidthPixels > 0 && definition.VisualHeightPixels > 0;

    public static (GridCell Minimum, GridCell Maximum) VisualBounds(ObjectPlacement placement, CatalogObject definition)
    {
        var width = Math.Max(1, definition.VisualWidthPixels / 32);
        var height = Math.Max(1, definition.VisualHeightPixels / 32);
        var left = definition.RootAnchor == "bottom-left" ? placement.RootCell.X :
            placement.RootCell.X - width / 2;
        return (new GridCell(left, placement.RootCell.Y),
            new GridCell(left + width - 1, placement.RootCell.Y + height - 1));
    }

    public static bool BlocksMovement(MapDocument map, CatalogDocument catalog, GridCell cell)
    {
        if (map.Surfaces.Any(item => item.Cell == cell &&
            catalog.SurfaceById.TryGetValue(item.DefinitionId, out var surface) && surface.BlocksMovement)) return true;
        return map.Objects.Any(item => catalog.ObjectById.TryGetValue(item.DefinitionId, out var definition) &&
            definition.BlocksMovement && FootprintCells(item, definition).Contains(cell));
    }

    public static bool HasGrass(MapDocument map, CatalogDocument catalog, GridCell cell)
    {
        if (!map.Bounds.Contains(cell) || BlocksMovement(map, catalog, cell)) return false;
        var overrideId = map.Surfaces.FirstOrDefault(item => item.Cell == cell)?.DefinitionId;
        var growsGrass = overrideId != null
            ? catalog.SurfaceById.TryGetValue(overrideId, out var surface) && surface.GrowsGrass
            : map.GrassMode == "auto" && catalog.SurfaceById.TryGetValue(map.BaseSurfaceDefinitionId,
                out var baseSurface) && baseSurface.GrowsGrass;
        if (!growsGrass) return false;
        return !map.Objects.Any(item => catalog.ObjectById.TryGetValue(item.DefinitionId, out var definition) &&
            definition.ExcludeGrass && FootprintCells(item, definition).Contains(cell));
    }

    public static bool CanPlace(MapDocument map, CatalogDocument catalog, string definitionId,
        GridCell root, out string reason, string? ignoreInstanceId = null)
    {
        if (!catalog.ObjectById.TryGetValue(definitionId, out var definition) ||
            !definition.EditorSelectable)
        { reason = "配置できないオブジェクトです。"; return false; }
        if (!HasSprite(definition))
        { reason = "MISSING ASSET: 正式Spriteがないため配置できません。"; return false; }
        if (definition.VisualWidthPixels < 1 || definition.VisualHeightPixels < 1 ||
            (definition.BlocksMovement && definition.BlockedCellOffsets.Count == 0))
        { reason = "VisualまたはBlocked Footprintが不正です。"; return false; }
        var candidate = new ObjectPlacement { DefinitionId = definitionId, RootCell = root };
        var visual = VisualBounds(candidate, definition);
        if (!map.Bounds.Contains(visual.Minimum) || !map.Bounds.Contains(visual.Maximum))
        { reason = "見た目がマップ範囲外です。"; return false; }
        if (definition.ActionPoints.Any(point => !map.Bounds.Contains(ActionCell(candidate, point))))
        { reason = "Action Pointがマップ範囲外です。"; return false; }
        if (definitionId == "house_main" && map.Objects.Any(item =>
            item.DefinitionId == "house_main" && item.InstanceId != ignoreInstanceId))
        { reason = "家は1つだけ配置できます。"; return false; }
        if (definitionId == "house_main" &&
            (map.Markers.Any(item => item.Cell == root) ||
             map.Objects.Any(item => item.InstanceId != ignoreInstanceId &&
                 catalog.ObjectById.TryGetValue(item.DefinitionId, out var other) &&
                 FootprintCells(item, other).Contains(root))))
        { reason = "家の入口セルが使用できません。"; return false; }
        var oldEntry = OutsideEntry(map);
        var candidateEntry = definitionId == "house_main" ? new GridCell(root.X, root.Y - 1) : (GridCell?)null;
        if (candidateEntry is { } entry && (!map.Bounds.Contains(entry) ||
            map.Markers.Any(item => item.Cell == entry) ||
            map.Objects.Any(item => item.InstanceId != ignoreInstanceId &&
                catalog.ObjectById.TryGetValue(item.DefinitionId, out var other) &&
                FootprintCells(item, other).Contains(entry))))
        { reason = "家の入口前が使用できません。"; return false; }
        foreach (var cell in FootprintCells(candidate, definition))
        {
            if (!map.Bounds.Contains(cell)) { reason = "Blocked Footprintが範囲外です。"; return false; }
            if (map.Markers.Any(item => item.Cell == cell) ||
                (definitionId != "house_main" && (oldEntry == cell || HouseRoot(map) == cell)))
            { reason = "Spawn・道路終端・入口前の保護セルです。"; return false; }
            if (map.Surfaces.Any(item => item.Cell == cell &&
                catalog.SurfaceById.TryGetValue(item.DefinitionId, out var surface) && surface.BlocksMovement))
            { reason = "通行不可Surfaceと重なります。"; return false; }
            if (map.Objects.Any(item => item.InstanceId != ignoreInstanceId &&
                catalog.ObjectById.TryGetValue(item.DefinitionId, out var other) &&
                FootprintCells(item, other).Contains(cell)))
            { reason = "既存Objectと重なります。"; return false; }
        }
        reason = "";
        return true;
    }

    public static IReadOnlyList<MapIssue> Validate(MapDocument map, CatalogDocument catalog)
    {
        var issues = new List<MapIssue>();
        if (map.Format != "halka-world-map" || map.FormatVersion != 2)
            issues.Add(new("format", "Map format/versionが未対応です。"));
        if (string.IsNullOrWhiteSpace(map.MapId) ||
            !System.Text.RegularExpressions.Regex.IsMatch(map.MapId, "^[a-z0-9_-]+$"))
            issues.Add(new("mapId", "Map IDは小文字英数字、_、-のみです。"));
        if (map.MapType != "outdoor" && map.MapType != "interior")
            issues.Add(new("mapType", "Map Typeが不正です。"));
        if (!catalog.SurfaceById.ContainsKey(map.BaseSurfaceDefinitionId) && map.BaseSurfaceDefinitionId != "base_ground")
            issues.Add(new("baseSurface", "Base Surfaceが不明です。"));
        if (map.GrassMode != "auto" && map.GrassMode != "none")
            issues.Add(new("grassMode", "Grass Modeが不正です。"));
        if (map.Bounds.MinX > map.Bounds.MaxX || map.Bounds.MinY > map.Bounds.MaxY)
            issues.Add(new("bounds", "Boundsが逆転しています。"));
        var markerIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var marker in map.Markers)
        {
            if (string.IsNullOrWhiteSpace(marker.Id) || !markerIds.Add(marker.Id))
                issues.Add(new("markerId", "Marker IDが空または重複しています。", marker.Cell));
            if (!map.Bounds.Contains(marker.Cell)) issues.Add(new("markerBounds", "Markerが範囲外です。", marker.Cell));
        }
        var surfaceCells = new HashSet<GridCell>();
        foreach (var placement in map.Surfaces)
        {
            if (!map.Bounds.Contains(placement.Cell)) issues.Add(new("surfaceBounds", "地面が範囲外です。", placement.Cell));
            if (!catalog.SurfaceById.ContainsKey(placement.DefinitionId))
                issues.Add(new("surfaceDefinition", "不明な地面ID: " + placement.DefinitionId, placement.Cell));
            if (!surfaceCells.Add(placement.Cell)) issues.Add(new("duplicateSurface", "地面が重複しています。", placement.Cell));
        }
        var seenInstance = new HashSet<string>(StringComparer.Ordinal);
        var occupied = new HashSet<GridCell>();
        foreach (var placement in map.Objects)
        {
            if (string.IsNullOrWhiteSpace(placement.InstanceId) || !seenInstance.Add(placement.InstanceId))
                issues.Add(new("duplicateInstanceId", "instanceIdが空または重複しています。", placement.RootCell));
            if (!catalog.ObjectById.TryGetValue(placement.DefinitionId, out var definition))
            { issues.Add(new("objectDefinition", "不明なObject ID: " + placement.DefinitionId, placement.RootCell)); continue; }
            if (placement.DefinitionId == "sign_basic" &&
                (string.IsNullOrWhiteSpace(placement.SignText) || placement.SignText.Length > 80))
                issues.Add(new("signText", "看板の内容は1～80文字で入力してください。", placement.RootCell));
            if ((definition.BlocksMovement && definition.BlockedCellOffsets.Count == 0) ||
                definition.BlockedCellOffsets.Distinct().Count() != definition.BlockedCellOffsets.Count)
                issues.Add(new("footprint", "Blocked Footprintが空または重複しています。", placement.RootCell));
            if (!HasSprite(definition))
                issues.Add(new("missingAsset", "MISSING ASSET: 正式Spriteがありません。", placement.RootCell));
            foreach (var point in definition.ActionPoints)
            {
                var actionCell = ActionCell(placement, point);
                if (!map.Bounds.Contains(actionCell))
                    issues.Add(new("actionBounds", "Action PointがMap範囲外です: " + point.Id, actionCell));
                else if (point.ActionType == "examine" && BlocksMovement(map, catalog, actionCell))
                    issues.Add(new("actionBlocked", "調べる立ち位置が塞がれています: " + point.Id, actionCell, true));
            }
            var visual = VisualBounds(placement, definition);
            if (!map.Bounds.Contains(visual.Minimum) || !map.Bounds.Contains(visual.Maximum))
                issues.Add(new("visualBounds", "Object Visualが範囲外です。", placement.RootCell));
            foreach (var cell in FootprintCells(placement, definition))
            {
                if (!map.Bounds.Contains(cell)) issues.Add(new("objectBounds", "Blocked Cellが範囲外です。", cell));
                if (map.Markers.Any(marker => marker.Cell == cell)) issues.Add(new("protected", "保護Markerを塞いでいます。", cell));
                if (map.Surfaces.Any(item => item.Cell == cell &&
                    catalog.SurfaceById.TryGetValue(item.DefinitionId, out var surface) && surface.BlocksMovement))
                    issues.Add(new("surfaceOverlap", "通行不可SurfaceとObjectが重なります。", cell));
                if (!occupied.Add(cell)) issues.Add(new("overlap", "ObjectのBlocked Footprintが重なります。", cell));
            }
        }
        if (map.MapType == "outdoor")
        {
            if (map.Objects.Count(item => item.DefinitionId == "house_main") != 1)
                issues.Add(new("house", "屋外Mapには家が1つ必要です。"));
            if (HouseRoot(map) is { } door &&
                (map.Markers.Any(item => item.Cell == door) ||
                 map.Objects.Any(item => item.DefinitionId != "house_main" &&
                     catalog.ObjectById.TryGetValue(item.DefinitionId, out var other) &&
                     FootprintCells(item, other).Contains(door))))
                issues.Add(new("houseDoor", "家の入口セルが閉塞しています。", door));
            if (map.Marker("player_start") is not { } start || BlocksMovement(map, catalog, start))
                issues.Add(new("playerStart", "Player Spawnが無いか閉塞しています。"));
            if (OutsideEntry(map) is not { } entry || !map.Bounds.Contains(entry) || BlocksMovement(map, catalog, entry))
                issues.Add(new("houseEntry", "家の入口前が使用できません。"));
        }
        else
        {
            foreach (var id in new[] { "interior_entry", "interior_exit" })
                if (map.Marker(id) is not { } cell || BlocksMovement(map, catalog, cell))
                    issues.Add(new("interiorMarker", id + "が無いか閉塞しています。"));
        }
        foreach (var marker in map.Markers)
            if (BlocksMovement(map, catalog, marker.Cell))
                issues.Add(new("markerBlocked", "Markerが閉塞しています。", marker.Cell));
        return issues;
    }
}
