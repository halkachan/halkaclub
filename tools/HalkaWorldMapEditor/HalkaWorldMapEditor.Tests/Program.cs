using HalkaWorldMapEditor.Core;
using System.Text.Json.Nodes;

var project = ProjectPaths.FindNear(AppContext.BaseDirectory) ?? throw new Exception("Unity project not found");
var mapPath = Path.Combine(ProjectPaths.AuthoringFolder(project), "first_field.hwmap.json");
var catalogPath = ProjectPaths.CatalogPath(project);
var source = File.ReadAllText(mapPath);
var map = MapFormat.LoadMap(mapPath);
var catalog = MapFormat.LoadCatalog(catalogPath);
var passes = 0;
void Check(string name, bool condition)
{
    if (!condition) throw new Exception("FAIL " + name);
    Console.WriteLine("PASS " + name);
    passes++;
}

Check("format and bounds", map.MapId == "first_field" && map.Bounds.MinX == -10 && map.Bounds.MaxX == 10 &&
    map.Bounds.MinY == -6 && map.Bounds.MaxY == 6);
Check("editable authoring placements", map.Surfaces.Count > 0 && map.Objects.Count > 0 &&
    map.Surfaces.All(s => catalog.SurfaceById.ContainsKey(s.DefinitionId)) &&
    map.Objects.All(o => catalog.ObjectById.ContainsKey(o.DefinitionId)));
Check("golden entity coordinates", map.EntitySpawns.Single(e => e.DefinitionId == "player_main").Cell == new GridCell(0, 0) &&
    map.EntitySpawns.Single(e => e.DefinitionId == "crow_main").Cell == new GridCell(7, 2) && MapRules.HouseRoot(map) == new GridCell(-7, -4) &&
    MapRules.OutsideEntry(map) == new GridCell(-7, -5) &&
    map.Marker("road_north") == new GridCell(0, 6) &&
    map.Marker("road_east") == new GridCell(10, -1) &&
    map.Marker("road_south") == new GridCell(2, -6) &&
    map.Marker("road_west") == new GridCell(-10, 0));
var officialIssues = MapRules.Validate(map, catalog);
foreach (var issue in officialIssues) Console.WriteLine("OFFICIAL MAP ISSUE " + issue);
Check("valid official map", officialIssues.All(issue => issue.IsWarning));
var grass = 0;
for (var y = map.Bounds.MinY; y <= map.Bounds.MaxY; y++)
for (var x = map.Bounds.MinX; x <= map.Bounds.MaxX; x++)
    if (MapRules.HasGrass(map, catalog, new GridCell(x, y))) grass++;
Check("derived grass exists", grass > 0);
Check("no grass on road", map.Surfaces.All(p => !MapRules.HasGrass(map, catalog, p.Cell)));
Check("IDs stable unique", map.Objects.All(o => !string.IsNullOrEmpty(o.InstanceId)) &&
    map.Objects.Select(o => o.InstanceId).Distinct().Count() == map.Objects.Count);
Check("stable ordered roundtrip", MapFormat.SerializeMap(MapFormat.ParseMap(source)) == MapFormat.SerializeMap(map));
Check("locked cells", MapRules.IsProtected(map, MapRules.HouseRoot(map)!.Value) &&
    MapRules.IsProtected(map, map.EntitySpawns.Single(e => e.DefinitionId == "player_main").Cell) &&
    MapRules.IsProtected(map, map.Marker("road_north")!.Value));
Check("reject protected and outside", !MapRules.CanPlace(map, catalog, "stone_basic", MapRules.HouseRoot(map)!.Value, out _) &&
    !MapRules.CanPlace(map, catalog, "stone_basic", new GridCell(100, 100), out _));
Check("sprite path safety", ProjectPaths.SpritePath(project, "Assets/Content/World/stone.png").EndsWith("stone.png") &&
    Throws(() => ProjectPaths.SpritePath(project, "../other.png")));
Check("malformed JSON", Throws(() => MapFormat.ParseMap("{not json}")));
Check("catalog v3 and action point serialization", catalog.FormatVersion == 3 &&
    catalog.ObjectById["bed_basic"].ActionPoints.Single().PoseKey == "bed_sleep" &&
    catalog.ObjectById["bed_basic"].ActionPoints.Single().PlayerCellOffset == new GridCell(0, 1));
Check("bed sleep world cell", MapRules.ActionCell(new ObjectPlacement { RootCell = new GridCell(-5, 0) },
    catalog.ObjectById["bed_basic"].ActionPoints.Single()) == new GridCell(-5, 1));
Check("palette has no map type limits", catalog.Objects.All(o => o.AllowedMapTypes.Count == 0) &&
    catalog.Surfaces.All(s => s.AllowedMapTypes.Count == 0 && s.EditorSelectable));
Check("passable cushion metadata", !catalog.ObjectById["cushion_basic"].BlocksMovement &&
    !catalog.ObjectById["cushion_basic"].BlockedCellOffsets.Any() &&
    catalog.ObjectById["cushion_basic"].ActionPoints.Single().ActionType == "sit");
Check("bench multiple seats", catalog.ObjectById["bench_basic"].ActionPoints.Count == 2 &&
    catalog.ObjectById["bench_basic"].ActionPoints.All(p => p.PoseKey == "bench_sit"));
Check("one sign definition", catalog.ObjectById.ContainsKey("sign_basic") &&
    catalog.Objects.Count(o => o.DefinitionId.StartsWith("sign_", StringComparison.Ordinal)) == 1 &&
    catalog.ObjectById["sign_basic"].ActionPoints.Single().Id == "read_front" &&
    catalog.ObjectById["sign_basic"].ActionPoints.Single().InteractionText == "かんばん。");
var newObjects = new[] { "cushion_basic", "desk_basic", "bench_basic", "sign_basic", "well_basic" };
Check("formal sprite paths and sizes", newObjects.All(id => {
    var definition = catalog.ObjectById[id];
    var spritePath = ProjectPaths.SpritePath(project, definition.PreviewSpritePath);
    return File.Exists(spritePath) && MapRules.HasSprite(definition) &&
        (id == "desk_basic" || id == "bench_basic" ?
            definition.VisualWidthPixels == 64 && definition.VisualHeightPixels == 32 :
                definition.VisualWidthPixels == 32 && definition.VisualHeightPixels == 32);
}));
Check("one tile blank sign art", catalog.ObjectById["sign_basic"].PreviewSpritePath ==
    "Assets/Content/World/sign.png" && catalog.ObjectById["sign_basic"].Footprint.Width == 1 &&
    catalog.ObjectById["sign_basic"].Footprint.Height == 1);
Check("formal blocked footprint metadata", !catalog.ObjectById["cushion_basic"].BlockedCellOffsets.Any() &&
    new[] { "desk_basic", "bench_basic" }.All(id =>
        catalog.ObjectById[id].RootAnchor == "bottom-left" &&
        catalog.ObjectById[id].BlockedCellOffsets.SequenceEqual(new[] { new GridCell(0, 0), new GridCell(1, 0) })) &&
    new[] { "sign_basic", "well_basic" }.All(id =>
        catalog.ObjectById[id].BlockedCellOffsets.SequenceEqual(new[] { new GridCell(0, 0) })));
Check("formal action point metadata", catalog.ObjectById["desk_basic"].ActionPoints.Single().InteractionText == "つくえ。" &&
    catalog.ObjectById["bench_basic"].ActionPoints.Select(p => p.PlayerCellOffset)
        .SequenceEqual(new[] { new GridCell(0, -1), new GridCell(1, -1) }) &&
    catalog.ObjectById["well_basic"].ActionPoints.Single().InteractionText == "いど。");
var legacyCatalog = JsonNode.Parse(File.ReadAllText(catalogPath))!;
legacyCatalog["formatVersion"] = 2;
foreach (var entry in legacyCatalog["objects"]!.AsArray()) entry!.AsObject().Remove("actionPoints");
var migratedCatalog = MapFormat.ParseCatalog(legacyCatalog.ToJsonString());
Check("catalog v2 migrates in memory", migratedCatalog.FormatVersion == 3 &&
    migratedCatalog.ObjectById["bed_basic"].ActionPoints.Count == 0);
var invalidCatalog = JsonNode.Parse(File.ReadAllText(catalogPath))!;
var bedPoints = invalidCatalog["objects"]!.AsArray().Single(entry =>
    (string?)entry!["definitionId"] == "bed_basic")!["actionPoints"]!.AsArray();
bedPoints.Add(bedPoints[0]!.DeepClone());
Check("duplicate action point rejected", Throws(() => MapFormat.ParseCatalog(invalidCatalog.ToJsonString())));
bedPoints.RemoveAt(1);
bedPoints[0]!["playerFacing"] = "diagonal";
Check("invalid facing rejected", Throws(() => MapFormat.ParseCatalog(invalidCatalog.ToJsonString())));
bedPoints[0]!["playerFacing"] = "up";
bedPoints[0]!["actionType"] = "warp";
Check("invalid action type rejected", Throws(() => MapFormat.ParseCatalog(invalidCatalog.ToJsonString())));

var houseMapPath = Path.Combine(ProjectPaths.AuthoringFolder(project), "halka_house.hwmap.json");
var houseSource = File.ReadAllText(houseMapPath);
var houseMap = MapFormat.LoadMap(houseMapPath);
Check("multiple map IDs", map.MapId == "first_field" && houseMap.MapId == "halka_house" &&
    map.MapType == "outdoor" && houseMap.MapType == "interior" && map.FormatVersion == 3 && houseMap.FormatVersion == 3);
Check("base surfaces and grass policies", map.BaseSurfaceDefinitionId == "base_ground" &&
    map.GrassMode == "auto" && houseMap.BaseSurfaceDefinitionId == "house_floor" &&
    houseMap.GrassMode == "none" && !MapRules.HasGrass(houseMap, catalog, new GridCell(0, 0)));
Check("interior migration contract", houseMap.Bounds.MinX == -6 && houseMap.Bounds.MaxX == 6 &&
    houseMap.Bounds.MinY == -4 && houseMap.Bounds.MaxY == 4 &&
    houseMap.Surfaces.Count == 50 && houseMap.Surfaces.All(s => s.DefinitionId == "house_wall") &&
    houseMap.Marker("interior_entry") == new GridCell(0, -3) &&
    houseMap.Marker("interior_exit") == new GridCell(0, -4) &&
    houseMap.Objects.Single(o => o.DefinitionId == "bed_basic").RootCell == new GridCell(-5, 0) &&
    MapRules.Validate(houseMap, catalog).Count == 0);
Check("interior stable roundtrip", MapFormat.SerializeMap(MapFormat.ParseMap(houseSource)) ==
    MapFormat.SerializeMap(houseMap));
var legacySignFixture = MapFormat.Clone(houseMap);
legacySignFixture.Objects.Add(new ObjectPlacement { InstanceId = "legacy_sign", DefinitionId = "sign_west",
    RootCell = new GridCell(0, 1) });
var migratedSign = MapFormat.ParseMap(MapFormat.SerializeMap(legacySignFixture)).Objects
    .Single(o => o.InstanceId == "legacy_sign");
Check("old sign migrates in memory", migratedSign.DefinitionId == "sign_basic" &&
    migratedSign.SignText == "にし" && !houseMap.Objects.Any(o => o.InstanceId == "legacy_sign"));
var house = map.Objects.Single(o => o.DefinitionId == "house_main");
var houseDefinition = catalog.ObjectById["house_main"];
var houseBlocked = MapRules.FootprintCells(house, houseDefinition).ToArray();
Check("house irregular footprint", houseBlocked.Length == 9 && houseBlocked.Distinct().Count() == 9 &&
    !houseBlocked.Contains(house.RootCell) &&
    MapRules.OutsideEntry(map) == new GridCell(house.RootCell.X, house.RootCell.Y - 1));
Check("house visual footprint", MapRules.VisualBounds(house, houseDefinition) ==
    (new GridCell(-9, -4), new GridCell(-5, -1)));
Check("house movement collision rules", !MapRules.CanPlace(map, catalog, "stone_basic", houseBlocked[0], out _) &&
    !MapRules.CanPlace(map, catalog, "house_main", house.RootCell, out _) &&
    !MapRules.CanPlace(houseMap, catalog, "bed_basic", new GridCell(-6, 0), out _));
var bed = houseMap.Objects.Single(o => o.DefinitionId == "bed_basic");
Check("bed two by three", MapRules.FootprintCells(bed, catalog.ObjectById["bed_basic"]).Count() == 6 &&
    MapRules.BlocksMovement(houseMap, catalog, bed.RootCell));
var actionOutside = MapFormat.Clone(houseMap);
actionOutside.Objects.Single().RootCell = new GridCell(-6, 4);
Check("action point outside map validation", MapRules.Validate(actionOutside, catalog).Any(i => i.Code == "actionBounds"));
var oldJson = """
    {"format":"halka-world-map","formatVersion":1,"mapId":"legacy_field","displayName":"Legacy","bounds":{"minX":-10,"maxX":10,"minY":-6,"maxY":6},"surfaces":[],"objects":[],"markers":{"playerSpawn":{"x":0,"y":0},"crowSpawn":{"x":7,"y":2},"houseDoor":{"x":-7,"y":-4},"outsideEntry":{"x":-7,"y":-5},"houseFootprint":{"width":5,"height":2},"roadEnds":{"north":{"x":0,"y":6},"east":{"x":10,"y":-1},"south":{"x":2,"y":-6},"west":{"x":-10,"y":0}}}}
    """;
var migrated = MapFormat.ParseMap(oldJson);
Check("v1 to v3 migration", migrated.FormatVersion == 3 && migrated.MapId == "legacy_field" &&
    migrated.Objects.Single(o => o.DefinitionId == "house_main").RootCell == new GridCell(-7, -4) &&
    migrated.EntitySpawns.Single(e => e.DefinitionId == "player_main").Cell == new GridCell(0, 0) &&
    migrated.Marker("road_north") == new GridCell(0, 6));

var tempDir = Path.Combine(Path.GetTempPath(), "HalkaMapEditorTests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(tempDir);
try
{
    var tempMap = Path.Combine(tempDir, "first_field.hwmap.json");
    File.WriteAllText(tempMap, source);
    var edit = MapSession.Open(tempMap, catalog);
    var originalSurfaceCount = edit.Map.Surfaces.Count;
    var blank = new GridCell(-4, 4);
    Check("surface paint", edit.PaintSurface("dirt", blank) && edit.Map.Surfaces.Count == originalSurfaceCount + 1 && edit.IsDirty);
    edit.Undo();
    Check("surface undo", edit.Map.Surfaces.Count == originalSurfaceCount && !edit.IsDirty);
    edit.Redo();
    Check("surface redo", edit.Map.Surfaces.Count == originalSurfaceCount + 1);
    Check("surface erase", edit.EraseSurface(blank));
    var dirtCell = new GridCell(0, -1);
    var grassSession = new MapSession(MapFormat.Clone(map), catalog, Path.Combine(tempDir, "grass-test.hwmap.json"));
    var dirtBefore = grassSession.Map.Surfaces.Count;
    Check("grass tool removes dirt", grassSession.RestoreGrass(dirtCell) &&
        grassSession.Map.Surfaces.Count == dirtBefore - 1 &&
        MapRules.HasGrass(grassSession.Map, catalog, dirtCell));
    grassSession.Undo();
    Check("grass tool undo restores dirt", grassSession.Map.Surfaces.Any(s => s.Cell == dirtCell) &&
        !MapRules.HasGrass(grassSession.Map, catalog, dirtCell));
    grassSession.Redo();
    Check("grass tool redo", !grassSession.Map.Surfaces.Any(s => s.Cell == dirtCell) &&
        MapRules.HasGrass(grassSession.Map, catalog, dirtCell));
    var alreadyGrass = new GridCell(-4, 4);
    var unchanged = MapFormat.SerializeMap(grassSession.Map);
    Check("grass tool on grass is no-op", !grassSession.RestoreGrass(alreadyGrass) &&
        MapFormat.SerializeMap(grassSession.Map) == unchanged);
    var drag = new MapSession(MapFormat.Clone(map), catalog, Path.Combine(tempDir, "grass-drag.hwmap.json"));
    var dragCells = new[] { new GridCell(0, -1), new GridCell(0, -2) };
    drag.BeginStroke();
    Check("grass drag removes multiple dirt", dragCells.All(drag.RestoreGrass) &&
        drag.Map.Surfaces.Count == map.Surfaces.Count - dragCells.Length);
    drag.EndStroke();
    drag.Undo();
    Check("grass drag one undo restores all dirt", dragCells.All(c => drag.Map.Surfaces.Any(s => s.Cell == c)) &&
        !drag.CanUndo);
    drag.Redo();
    Check("grass drag redo removes all dirt", dragCells.All(c => !drag.Map.Surfaces.Any(s => s.Cell == c)));
    var stoneMap = MapFormat.Clone(map);
    var stone = stoneMap.Objects.First(o => o.DefinitionId == "stone_basic");
    stoneMap.Surfaces.RemoveAll(s => s.Cell == stone.RootCell);
    stoneMap.Surfaces.Add(new SurfacePlacement { DefinitionId = "dirt", Cell = stone.RootCell });
    var stoneSession = new MapSession(stoneMap, catalog, Path.Combine(tempDir, "stone-grass.hwmap.json"));
    Check("grass tool removes dirt but preserves stone", stoneSession.RestoreGrass(stone.RootCell) &&
        stoneSession.Map.Objects.Any(o => o.InstanceId == stone.InstanceId) &&
        !MapRules.HasGrass(stoneSession.Map, catalog, stone.RootCell));
    File.WriteAllText(grassSession.FilePath, source);
    grassSession.Save();
    var savedGrassMap = MapFormat.LoadMap(grassSession.FilePath);
    Check("grass tool save writes only surface removal", savedGrassMap.Surfaces.Count == dirtBefore - 1 &&
        savedGrassMap.Surfaces.All(s => s.DefinitionId != "grass" && s.DefinitionId != "grass_basic"));
    edit.BeginStroke();
    edit.PaintSurface("dirt", new GridCell(-4, 4));
    edit.PaintSurface("dirt", new GridCell(-4, 5));
    edit.EndStroke();
    edit.Undo();
    Check("drag stroke one undo", !edit.Map.Surfaces.Any(s => s.Cell == new GridCell(-4, 4) || s.Cell == new GridCell(-4, 5)));
    var objectCell = new GridCell(-1, 5);
    Check("object add", edit.PlaceObject("stone_basic", objectCell, out _));
    var placed = edit.Map.Objects.Single(o => o.RootCell == objectCell);
    Check("object ID format", placed.InstanceId.StartsWith("obj_") && placed.InstanceId.Length > 30);
    Check("object overlap rejected", !edit.PlaceObject("flower_basic", objectCell, out _));
    Check("object move", edit.MoveObject(placed.InstanceId, new GridCell(-1, 4), out _) &&
        edit.Map.Objects.Single(o => o.InstanceId == placed.InstanceId).RootCell == new GridCell(-1, 4));
    Check("action point follows moved root", MapRules.ActionCell(edit.Map.Objects.Single(o => o.InstanceId == placed.InstanceId),
        catalog.ObjectById["stone_basic"].ActionPoints.Single()) == new GridCell(-1, 3));
    Check("object copy new identity", edit.CopyObject(placed.InstanceId, objectCell, out _) &&
        edit.Map.Objects.Single(o => o.RootCell == objectCell).InstanceId != placed.InstanceId);
    Check("copied action point derives from new root", MapRules.ActionCell(edit.Map.Objects.Single(o => o.RootCell == objectCell),
        catalog.ObjectById["stone_basic"].ActionPoints.Single()) == new GridCell(-1, 4));
    Check("object delete", edit.DeleteObject(placed.InstanceId));
    edit.Undo();
    Check("object delete undo", edit.Map.Objects.Any(o => o.InstanceId == placed.InstanceId));
    edit.Save();
    Check("atomic save and backup", File.Exists(tempMap) && File.Exists(tempMap + ".bak") && !edit.IsDirty);
    Check("reloaded edited map valid", MapRules.Validate(MapFormat.LoadMap(tempMap), catalog).All(i => i.IsWarning));
    var invalid = MapFormat.Clone(map);
    invalid.Objects[1].InstanceId = invalid.Objects[0].InstanceId;
    Check("duplicate ID validation", MapRules.Validate(invalid, catalog).Any(i => i.Code == "duplicateInstanceId"));
    invalid = MapFormat.Clone(map);
    invalid.Surfaces.Add(new SurfacePlacement { DefinitionId = "dirt", Cell = invalid.Surfaces[0].Cell });
    Check("duplicate surface validation", MapRules.Validate(invalid, catalog).Any(i => i.Code == "duplicateSurface"));
    var houseEdit = new MapSession(MapFormat.Clone(map), catalog, Path.Combine(tempDir, "house-move.hwmap.json"));
    var houseId = houseEdit.Map.Objects.Single(o => o.DefinitionId == "house_main").InstanceId;
    var houseTarget = FindPlacement(houseEdit.Map, catalog, "house_main", houseId,
        houseEdit.Map.Objects.Single(o => o.InstanceId == houseId).RootCell);
    Check("house move keeps ID and derives entry", houseEdit.MoveObject(houseId, houseTarget, out _) &&
        houseEdit.Map.Objects.Single(o => o.InstanceId == houseId).RootCell == houseTarget &&
        MapRules.OutsideEntry(houseEdit.Map) == new GridCell(houseTarget.X, houseTarget.Y - 1));
    houseEdit.Undo();
    Check("house move undo", MapRules.HouseRoot(houseEdit.Map) == new GridCell(-7, -4));
    houseEdit.Redo();
    Check("house move redo", MapRules.HouseRoot(houseEdit.Map) == houseTarget);
    var roomEdit = new MapSession(MapFormat.Clone(houseMap), catalog, Path.Combine(tempDir, "house-room.hwmap.json"));
    var wall = new GridCell(0, 0);
    Check("wall paint blocks movement", roomEdit.PaintSurface("house_wall", wall) &&
        MapRules.BlocksMovement(roomEdit.Map, catalog, wall));
    Check("floor restore removes wall", roomEdit.RestoreBaseSurface(wall) &&
        !MapRules.BlocksMovement(roomEdit.Map, catalog, wall));
    roomEdit.Undo();
    Check("floor restore undo", MapRules.BlocksMovement(roomEdit.Map, catalog, wall));
    roomEdit.Redo();
    Check("floor restore redo", !MapRules.BlocksMovement(roomEdit.Map, catalog, wall));
    roomEdit.Save();
    Check("interior save and reload", MapRules.Validate(MapFormat.LoadMap(roomEdit.FilePath), catalog).Count == 0);
    var surfaceFreedom = new MapSession(MapFormat.Clone(houseMap), catalog,
        Path.Combine(tempDir, "indoor-grass.hwmap.json"));
    var interiorCell = new GridCell(0, 0);
    Check("grass paints indoors", surfaceFreedom.RestoreGrass(interiorCell) &&
        surfaceFreedom.Map.Surfaces.Any(s => s.Cell == interiorCell && s.DefinitionId == "base_ground") &&
        MapRules.HasGrass(surfaceFreedom.Map, catalog, interiorCell));
    Check("floor repaints indoors", surfaceFreedom.PaintSurface("house_floor", interiorCell) &&
        !MapRules.HasGrass(surfaceFreedom.Map, catalog, interiorCell) &&
        !surfaceFreedom.Map.Surfaces.Any(s => s.Cell == interiorCell));
    Check("dirt paints indoors", surfaceFreedom.PaintSurface("dirt", interiorCell) &&
        !MapRules.HasGrass(surfaceFreedom.Map, catalog, interiorCell));
    var indoorTreeRoot = FindPlacement(surfaceFreedom.Map, catalog, "tree_basic");
    Check("tree can be placed indoors", surfaceFreedom.PlaceObject("tree_basic", indoorTreeRoot, out _) &&
        MapRules.Validate(surfaceFreedom.Map, catalog).All(issue => issue.IsWarning));
    surfaceFreedom.Save();
    Check("indoor unrestricted map reload", MapFormat.LoadMap(surfaceFreedom.FilePath).Objects
        .Any(o => o.DefinitionId == "tree_basic"));
    var outdoorBed = new MapSession(MapFormat.Clone(map), catalog,
        Path.Combine(tempDir, "outdoor-bed.hwmap.json"));
    var outdoorBedRoot = FindPlacement(outdoorBed.Map, catalog, "bed_basic");
    Check("bed can be placed outdoors", outdoorBed.PlaceObject("bed_basic", outdoorBedRoot, out _) &&
        MapRules.Validate(outdoorBed.Map, catalog).All(issue => issue.IsWarning));
    foreach (var id in new[] { "cushion_basic", "desk_basic", "bench_basic", "sign_basic", "well_basic" })
    {
        var original = id is "cushion_basic" or "desk_basic" ? houseMap : map;
        var session = new MapSession(MapFormat.Clone(original), catalog,
            Path.Combine(tempDir, id + ".hwmap.json"));
        var root = FindPlacement(session.Map, catalog, id);
        Check(id + " test map placement", session.PlaceObject(id, root, out var placementReason));
        var placement = session.Map.Objects.Single(o => o.DefinitionId == id);
        var definition = catalog.ObjectById[id];
        var footprint = MapRules.FootprintCells(placement, definition).ToArray();
        Check(id + " collision", (id == "cushion_basic" && footprint.Length == 0 &&
            !MapRules.BlocksMovement(session.Map, catalog, root)) ||
            (id is "desk_basic" or "bench_basic" && footprint.Length == 2 &&
             footprint.All(c => MapRules.BlocksMovement(session.Map, catalog, c))) ||
            (id != "cushion_basic" && id is not ("desk_basic" or "bench_basic") &&
             footprint.Length == 1 && MapRules.BlocksMovement(session.Map, catalog, root)));
        Check(id + " action overlay", definition.ActionPoints.All(point =>
            MapRules.ActionCell(placement, point) ==
            new GridCell(root.X + point.PlayerCellOffset.X, root.Y + point.PlayerCellOffset.Y)));
        var visual = MapRules.VisualBounds(placement, definition);
        Check(id + " visual size", visual.Maximum.X - visual.Minimum.X + 1 ==
            definition.VisualWidthPixels / 32 && visual.Maximum.Y - visual.Minimum.Y + 1 ==
            definition.VisualHeightPixels / 32);
        session.Save();
        Check(id + " test map reload", MapFormat.LoadMap(session.FilePath).Objects.Any(o =>
            o.InstanceId == placement.InstanceId));
        var idOfPlacement = placement.InstanceId;
        var moved = FindPlacement(session.Map, catalog, id, idOfPlacement, root);
        Check(id + " generic move", session.MoveObject(idOfPlacement, moved, out _));
        session.Undo();
        Check(id + " generic undo", session.Map.Objects.Single(o => o.InstanceId == idOfPlacement).RootCell == root);
        session.Redo();
        Check(id + " generic redo", session.Map.Objects.Single(o => o.InstanceId == idOfPlacement).RootCell == moved);
        Check(id + " generic copy", session.CopyObject(idOfPlacement, root, out _));
        Check(id + " generic delete", session.DeleteObject(idOfPlacement));
    }
    var signSession = new MapSession(MapFormat.Clone(houseMap), catalog,
        Path.Combine(tempDir, "sign-instance.hwmap.json"));
    var signRoot = FindPlacement(signSession.Map, catalog, "sign_basic");
    Check("new sign default text", signSession.PlaceObject("sign_basic", signRoot, out _) &&
        signSession.Map.Objects.Single(o => o.DefinitionId == "sign_basic").SignText == "かんばん。");
    var signId = signSession.Map.Objects.Single(o => o.DefinitionId == "sign_basic").InstanceId;
    Check("inspector sign text edit", signSession.SetSignText(signId, "ここからさき") &&
        signSession.Map.Objects.Single(o => o.InstanceId == signId).SignText == "ここからさき");
    signSession.Undo();
    Check("sign text undo", signSession.Map.Objects.Single(o => o.InstanceId == signId).SignText == "かんばん。");
    signSession.Redo();
    var signMove = FindPlacement(signSession.Map, catalog, "sign_basic", signId, signRoot);
    Check("sign move preserves text", signSession.MoveObject(signId, signMove, out _) &&
        signSession.Map.Objects.Single(o => o.InstanceId == signId).SignText == "ここからさき");
    Check("sign copy preserves text", signSession.CopyObject(signId, signRoot, out _) &&
        signSession.Map.Objects.Single(o => o.RootCell == signRoot).SignText == "ここからさき" &&
        signSession.Map.Objects.Single(o => o.RootCell == signRoot).InstanceId != signId);
    signSession.Save();
    Check("sign instance text reload", MapFormat.LoadMap(signSession.FilePath).Objects
        .Count(o => o.DefinitionId == "sign_basic" && o.SignText == "ここからさき") == 2);
Check("non-sign JSON stays sparse", !MapFormat.SerializeMap(map).Contains("\"signText\"", StringComparison.Ordinal));

// v0.5: Entity / Spawn authoring, including the v2 marker migration.
var entityCatalog = MapFormat.LoadEntityCatalog(ProjectPaths.EntityCatalogPath(project));
Check("entity catalog two definitions", entityCatalog.Entities.Count == 2 &&
    entityCatalog.ById.ContainsKey("player_main") && entityCatalog.ById.ContainsKey("crow_main"));
Check("player entity metadata", entityCatalog.ById["player_main"].SpawnMode == "game-start" &&
    entityCatalog.ById["player_main"].MaxInstances == 1);
var region = entityCatalog.ById["crow_main"].WanderRegion!;
Check("crow relative wander metadata", region != null &&
    region.MinX == -3 && region.MaxX == 1 && region.MinY == -2 && region.MaxY == 2);
Check("official entity spawns", map.FormatVersion == 3 && map.EntitySpawns.Count == 2 &&
    houseMap.EntitySpawns.Count == 0 && !map.Markers.Any(m => m.Id is "player_start" or "crow_spawn"));
Check("entity stable IDs", map.EntitySpawns.Select(e => e.InstanceId).Distinct().Count() == 2 &&
    map.EntitySpawns.All(e => !string.IsNullOrWhiteSpace(e.InstanceId)));
Check("entity facing persisted", map.EntitySpawns.Single(e => e.DefinitionId == "player_main").Facing == "down" &&
    map.EntitySpawns.Single(e => e.DefinitionId == "crow_main").Facing == "right");
var legacyV2 = JsonNode.Parse(source)!.AsObject();
legacyV2["formatVersion"] = 2;
legacyV2.Remove("entitySpawns");
legacyV2["markers"]!.AsArray().Insert(0, JsonNode.Parse("{\"id\":\"player_start\",\"cell\":{\"x\":0,\"y\":0}}"));
legacyV2["markers"]!.AsArray().Insert(0, JsonNode.Parse("{\"id\":\"crow_spawn\",\"cell\":{\"x\":7,\"y\":2}}"));
var migratedV2 = MapFormat.ParseMap(legacyV2.ToJsonString());
Check("v2 marker migration", migratedV2.EntitySpawns.Count == 2 && migratedV2.Markers.Count == map.Markers.Count);
Check("v2 semantic placements preserved", MapFormat.SerializeMap(migratedV2) == MapFormat.SerializeMap(map));
var entitySession = new MapSession(MapFormat.Clone(map), catalog, Path.Combine(tempDir, "entity-edit.hwmap.json"));
var playerId = entitySession.Map.EntitySpawns.Single(e => e.DefinitionId == "player_main").InstanceId;
var crowId = entitySession.Map.EntitySpawns.Single(e => e.DefinitionId == "crow_main").InstanceId;
var movedCrow = new GridCell(0, 3);
var movedPlayer = new GridCell(0, -3);
Check("crow moved fixture", entitySession.MoveEntity(crowId, movedCrow, out _) &&
    entitySession.Map.EntitySpawns.Single(e => e.InstanceId == crowId).Cell == movedCrow);
Check("crow range follows spawn", movedCrow.X + region.MinX == -3 && movedCrow.X + region.MaxX == 1 &&
    movedCrow.Y + region.MinY == 1 && movedCrow.Y + region.MaxY == 5);
Check("player moved fixture", entitySession.MoveEntity(playerId, movedPlayer, out _) &&
    entitySession.Map.EntitySpawns.Single(e => e.InstanceId == playerId).Cell == movedPlayer);
Check("entity facing edit", entitySession.SetEntityFacing(crowId, "up") &&
    entitySession.Map.EntitySpawns.Single(e => e.InstanceId == crowId).Facing == "up");
entitySession.Undo();
Check("entity facing undo", entitySession.Map.EntitySpawns.Single(e => e.InstanceId == crowId).Facing == "right");
entitySession.Redo();
Check("entity facing redo", entitySession.Map.EntitySpawns.Single(e => e.InstanceId == crowId).Facing == "up");
Check("reject second player", !entitySession.PlaceEntity("player_main", new GridCell(2, 0), out _));
Check("player copy forbidden", !entitySession.CopyEntity(playerId, new GridCell(2, 0), out _));
Check("reject blocked entity", !entitySession.PlaceEntity("crow_main", MapRules.HouseRoot(map)!.Value, out _));
Check("reject second crow", !entitySession.PlaceEntity("crow_main", new GridCell(2, 0), out _));
entitySession.Save();
var entityReload = MapFormat.LoadMap(entitySession.FilePath);
Check("entity save reload", entityReload.EntitySpawns.Single(e => e.InstanceId == crowId).Cell == movedCrow &&
    entityReload.EntitySpawns.Single(e => e.InstanceId == crowId).Facing == "up" &&
    entityReload.EntitySpawns.Single(e => e.InstanceId == playerId).Cell == movedPlayer);
Check("entity stable serialization", MapFormat.SerializeMap(entityReload) == MapFormat.SerializeMap(entitySession.Map));
Check("house permits crow placement", MapRules.CanPlaceEntity(houseMap, catalog, "crow_main", new GridCell(0, 0), out _));
}
finally { Directory.Delete(tempDir, true); }
Check("formal map never written", File.ReadAllText(mapPath) == source);
Check("formal interior never written", File.ReadAllText(houseMapPath) == houseSource);
Console.WriteLine($"All {passes} checks passed.");

static bool Throws(Action action)
{
    try { action(); return false; }
    catch { return true; }
}

static GridCell FindPlacement(MapDocument map, CatalogDocument catalog, string id,
    string? ignoreInstanceId = null, GridCell? excluded = null)
{
    for (var y = map.Bounds.MinY; y <= map.Bounds.MaxY; y++)
    for (var x = map.Bounds.MinX; x <= map.Bounds.MaxX; x++)
    {
        var cell = new GridCell(x, y);
        if (cell != excluded &&
            !(excluded is { } previous && id is ("desk_basic" or "bench_basic") &&
              cell.Y == previous.Y && Math.Abs(cell.X - previous.X) < 2) &&
            (id != "cushion_basic" || !MapRules.BlocksMovement(map, catalog, cell)) &&
            MapRules.CanPlace(map, catalog, id, cell, out _, ignoreInstanceId))
            return cell;
    }
    throw new Exception("No test placement available: " + id);
}
