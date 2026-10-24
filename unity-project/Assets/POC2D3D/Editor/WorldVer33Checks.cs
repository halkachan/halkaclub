using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Halka.Game.Player;
using Halka.Game.UI;
using Halka.Game.World;
using Halka.Poc2D3D.Save;
using Halka.Poc2D3D.World;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using C6 = Halka.Poc2D3D.EditorTools.Phase6SaveChecks;
using V32 = Halka.Poc2D3D.EditorTools.WorldVer32Checks;

namespace Halka.Poc2D3D.EditorTools
{
    // はるかわーるど ver3.3「シームレスエリア拡張・東エリア追加」: the checks of the first East region
    // (east_area_01, world offset (11,-6), right East of first_field) and of the direction themes. The
    // game is started with the Phase 6 harness (memory storage only). Every check prints PASS or FAIL.
    //   Prepare: -executeMethod Halka.Poc2D3D.EditorTools.WorldVer32Checks.PrepareBatch
    //   Checks:  -executeMethod Halka.Poc2D3D.EditorTools.WorldVer33Checks.RunBatch
    public static class WorldVer33Checks
    {
        public const string EastId = "east_area_01";
        public static readonly Vector2Int EastOffset = new Vector2Int(11, -6);
        public static readonly Vector2Int FieldRoadEnd = new Vector2Int(10, -1);     // first_field's road_east
        public static readonly Vector2Int EastRoadStart = new Vector2Int(11, -1);    // east_area_01 local (0,5)
        public static readonly Vector2Int PlazaFront = new Vector2Int(27, -2);       // landmark marker, local (16,4)
        public static readonly Vector2Int Landmark = new Vector2Int(27, -1);         // sunflowers, local (16,5)
        private static readonly List<string> failures = new List<string>();
        private static readonly SortedDictionary<string, int> perCategory = new SortedDictionary<string, int>();
        private static readonly List<string> lines = new List<string>();
        private static int passes;
        private static string category = "";

        public static void RunBatch()
        {
            var ok = false;
            try { ok = Run(); }
            catch (Exception exception) { Debug.LogException(exception); }
            EditorApplication.Exit(ok ? 0 : 1);
        }

        public static bool Run()
        {
            passes = 0;
            failures.Clear();
            perCategory.Clear();
            lines.Clear();
            var keys = new[] { LifeLogController.SaveKey, WorldTimeController.SaveKey };
            var saved = keys.ToDictionary(k => k, k => PlayerPrefs.HasKey(k) ? PlayerPrefs.GetString(k) : null);
            var randomState = UnityEngine.Random.state;
            UnityEngine.Random.InitState(3333);
            try
            {
                Run("registry", RegistryChecks);
                Run("themes", ThemeChecks);
                Run("content", ContentChecks);
                Run("seamless", SeamlessChecks);
                Run("camera", CameraChecks);
                Run("chunks", ChunkChecks);
                Run("field-look", FieldLookChecks);
                Run("display", DisplayChecks);
                Run("save", SaveChecks);
                Run("crow-auto", CrowAutoChecks);
                Run("rooms", RoomChecks);
                Run("time-lifelog", TimeChecks);
            }
            finally
            {
                C6.DisposeHarness();
                UnityEngine.Random.state = randomState;
                foreach (var pair in saved)
                {
                    if (pair.Value == null) PlayerPrefs.DeleteKey(pair.Key);
                    else PlayerPrefs.SetString(pair.Key, pair.Value);
                }
                PlayerPrefs.Save();
            }
            foreach (var failure in failures) Debug.LogError("VER33 FAIL " + failure);
            Debug.Log("HARUKA ver3.3 categories: " + string.Join(", ", perCategory.Select(p => p.Key + " " + p.Value)));
            Debug.Log($"HARUKA ver3.3 checks: {passes} passed, {failures.Count} failed");
            var report = Path.Combine(PocSceneBuilder.RepositoryRoot, "..", "HARUKA_WORLD_V33_LOGS", "ver33_checks.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(report));
            File.WriteAllLines(report, lines.Concat(new[] { $"TOTAL {passes} passed, {failures.Count} failed" }));
            return failures.Count == 0;
        }

        private static void Run(string name, Action checks)
        {
            category = name;
            try { checks(); }
            catch (Exception exception) { Check("no exception in " + name, false, exception.GetType().Name + ": " + exception.Message); Debug.LogException(exception); }
        }

        private static void Check(string name, bool condition, string detail = "")
        {
            detail = detail ?? "";
            perCategory[category] = perCategory.TryGetValue(category, out var n) ? n + 1 : 1;
            var line = (condition ? "PASS" : "FAIL") + " [" + category + "] " + name + (detail.Length > 0 ? " | " + detail : "");
            lines.Add(line);
            if (condition) { passes++; Debug.Log("VER33 " + line); }
            else failures.Add("[" + category + "] " + name + (detail.Length > 0 ? " | " + detail : ""));
        }

        private static MapDefinition Map(string id) => V32.Content<MapDefinition>(id);

        private static (OutdoorWorldDefinition world, List<string> problems) LoadRegistry(string registryJson = null, string themesJson = null)
        {
            var problems = new List<string>();
            var themes = OutdoorWorldRegistryJson.ParseThemes(themesJson ?? AssetDatabase.LoadAssetAtPath<TextAsset>(PocSceneBuilder.DirectionThemesPath).text, problems);
            var world = OutdoorWorldRegistryJson.Parse(registryJson ?? AssetDatabase.LoadAssetAtPath<TextAsset>(PocSceneBuilder.OutdoorWorldRegistryPath).text,
                id => Map(id), problems, themes);
            return (world, problems);
        }

        // ---------------------------------------------------------------- 16.1 region registry

        private static void RegistryChecks()
        {
            var (world, problems) = LoadRegistry();
            Check("outdoor_main: two regions, first_field at (0,0) (center), east_area_01 at (11,-6) (east), no problem",
                world != null && problems.Count == 0 && world.regions.Count == 2 &&
                world.regions[0].mapId == "first_field" && world.regions[0].offset == Vector2Int.zero && world.regions[0].direction == "center" &&
                world.regions[1].mapId == EastId && world.regions[1].offset == EastOffset && world.regions[1].direction == "east",
                string.Join("; ", problems));
            var index = new OutdoorWorldIndex(world);
            var field = index.FindRegion("first_field");
            var east = index.FindRegion(EastId);
            Check("first_field did not move: world (-10,-6)..(10,6), every world cell = its first_field cell",
                field.Rect.Equals(new CellRect(new Vector2Int(-10, -6), new Vector2Int(10, 6))) && field.ToWorld(new Vector2Int(-7, -4)) == new Vector2Int(-7, -4));
            Check("east_area_01: local (0,0)..(25,12) = world (11,-6)..(36,6): 26 x 13 cells, right East of first_field",
                east.Map.MinCell == Vector2Int.zero && east.Map.MaxCell == new Vector2Int(25, 12) &&
                east.Rect.Equals(new CellRect(new Vector2Int(11, -6), new Vector2Int(36, 6))) && east.Rect.Min.x == field.Rect.Max.x + 1);
            Check("local <-> world: local (0,5) is world (11,-1) (the East road goes on), local (25,12) is world (36,6), round trip for every cell",
                east.ToWorld(new Vector2Int(0, 5)) == EastRoadStart && east.ToWorld(new Vector2Int(25, 12)) == new Vector2Int(36, 6) &&
                Enumerable.Range(0, 26).SelectMany(x => Enumerable.Range(0, 13).Select(y => new Vector2Int(x, y))).All(l => east.ToLocal(east.ToWorld(l)) == l));
            Check("RegionAt: (10,-1) first_field, (11,-1) east_area_01; (37,-1) and (11,7) belong to no region (undefined: blocked, no grass)",
                index.RegionAt(FieldRoadEnd) == field && index.RegionAt(EastRoadStart) == east && index.RegionAt(new Vector2Int(37, -1)) == null &&
                index.IsBlocked(new Vector2Int(37, -1)) && !index.HasGrass(new Vector2Int(37, -1)) && index.IsBlocked(new Vector2Int(11, 7)));
            Check("the world's bounds: (-10,-6)..(36,6) (the field's height kept: the 2D camera frames first_field as before)",
                index.Bounds.Equals(new CellRect(new Vector2Int(-10, -6), new Vector2Int(36, 6))));

            string Registry(string regions) => "{\"format\":\"halka-world-outdoor\",\"formatVersion\":1,\"worldId\":\"outdoor_main\",\"chunkSize\":16,\"regions\":[" + regions + "]}";
            const string field0 = "{\"mapId\":\"first_field\",\"offset\":{\"x\":0,\"y\":0},\"direction\":\"center\"}";
            var bad = new (string label, string json, string expect)[]
            {
                ("the East region on the field's cells (offset (10,-6): one column overlaps)", Registry(field0 + ",{\"mapId\":\"east_area_01\",\"offset\":{\"x\":10,\"y\":-6},\"direction\":\"east\"}"), "overlaps"),
                ("the same region twice", Registry(field0 + ",{\"mapId\":\"east_area_01\",\"offset\":{\"x\":11,\"y\":-6},\"direction\":\"east\"},{\"mapId\":\"east_area_01\",\"offset\":{\"x\":60,\"y\":0},\"direction\":\"east\"}"), "placed twice"),
                ("an unknown direction", Registry(field0 + ",{\"mapId\":\"east_area_01\",\"offset\":{\"x\":11,\"y\":-6},\"direction\":\"up\"}"), "unknown direction"),
                ("an unknown region id", Registry(field0 + ",{\"mapId\":\"east_area_99\",\"offset\":{\"x\":11,\"y\":-6},\"direction\":\"east\"}"), "unknown Map"),
                ("an interior Map as a region", Registry(field0 + ",{\"mapId\":\"halka_house\",\"offset\":{\"x\":11,\"y\":-6},\"direction\":\"east\"}"), "not an outdoor Map"),
                ("an offset beyond ±2^24", Registry(field0 + ",{\"mapId\":\"east_area_01\",\"offset\":{\"x\":2147483000,\"y\":0},\"direction\":\"east\"}"), "ERROR"),
            };
            foreach (var (label, json, expect) in bad)
            {
                var (w, p) = LoadRegistry(json);
                Check("registry with " + label + " is refused", w == null && p.Any(x => x.Contains(expect)), string.Join("; ", p));
            }
            var (old, oldProblems) = LoadRegistry(Registry("{\"mapId\":\"first_field\",\"offset\":{\"x\":0,\"y\":0}}"));
            Check("a ver3.2 registry (no direction field) still loads: first_field is center", old != null && old.regions[0].direction == "center",
                string.Join("; ", oldProblems));
        }

        // ---------------------------------------------------------------- 3 / 14 direction themes

        private static void ThemeChecks()
        {
            var (world, _) = LoadRegistry();
            var expected = new (string direction, string name, string hex)[]
                { ("north", "きた", "5B84B1"), ("east", "ひがし", "F2C443"), ("south", "みなみ", "C8564B"), ("west", "にし", "6E9F57") };
            foreach (var (direction, name, hex) in expected)
            {
                var theme = world.ThemeOf(direction);
                Check($"theme {direction}: {name}, #{hex}", theme != null && theme.name == name && ColorUtility.ToHtmlStringRGB(theme.color) == hex,
                    theme != null ? theme.name + " #" + ColorUtility.ToHtmlStringRGB(theme.color) : "missing");
            }
            var hues = expected.Select(e => { Color.RGBToHSV(world.ThemeOf(e.direction).color, out var h, out _, out _); return h * 360f; }).ToArray();
            Check("North blue (200-230°), East yellow (40-55°), South red (0-15°), West green (90-120°)",
                hues[0] > 200 && hues[0] < 230 && hues[1] > 40 && hues[1] < 55 && (hues[2] < 15 || hues[2] > 350) && hues[3] > 90 && hues[3] < 120,
                string.Join(" ", hues.Select(h => h.ToString("0"))));
            Check("the direction names are drawn by the game's font (きた ひがし みなみ にし)", expected.All(e => GameBitmapFont.HasAllGlyphs(e.name)));
            var bad = new List<string>();
            OutdoorWorldRegistryJson.ParseThemes("{\"format\":\"halka-direction-themes\",\"formatVersion\":1,\"themes\":[{\"direction\":\"east\",\"name\":\"ひがし\",\"color\":\"yellow?\",\"shade\":\"#000000\"}]}", bad);
            Check("themes with a bad colour or missing directions are refused", bad.Any(p => p.Contains("bad colour")) && bad.Any(p => p.Contains("no theme for north")),
                string.Join("; ", bad));
            // The East models take their yellow from the theme file (blender/exports/v33_assets_manifest.json).
            var manifest = File.ReadAllText(Path.Combine(PocSceneBuilder.RepositoryRoot, "blender", "exports", "v33_assets_manifest.json"));
            Check("the East models were built with the theme's yellow (#f2c443), read from direction_themes.json",
                manifest.Contains("\"#f2c443\"") && manifest.Contains("direction_themes.json"));
        }

        // ---------------------------------------------------------------- 4 / 5 the East area's content

        private static void ContentChecks()
        {
            var east = Map(EastId);
            Check("east_area_01: an outdoor Map (Map JSON v4 import), 26 x 13, base grass (auto)", east != null && east.MapType == "outdoor" &&
                east.MaxCell - east.MinCell + Vector2Int.one == new Vector2Int(26, 13) && east.GrassMode == "auto");
            Check("map rules (MapPlacementRules.Validate) report nothing", MapPlacementRules.Validate(east).Count == 0, string.Join("; ", MapPlacementRules.Validate(east)));
            Check("no Player Start, no crow, no transition in the East area (the one player_main / crow_main stay in first_field)",
                !east.EntitySpawns.Any() && !east.AreaTransitions.Any());
            Check("the road: local y=5 from x=0 to 25 (world (11,-1)..(36,-1)), markers road_west (0,5) and road_east (25,5)",
                Enumerable.Range(0, 26).All(x => east.SurfaceAt(new Vector2Int(x, 5))?.StableId == "dirt" || east.Objects.Any(o => o.RootCell == new Vector2Int(x, 5))) &&
                east.TryGetMarker("road_west", out var rw) && rw == new Vector2Int(0, 5) && east.TryGetMarker("road_east", out var re) && re == new Vector2Int(25, 5));
            Check("the landmark: sunflowers at the plaza's centre (local (16,5)), marker landmark_east_flower_plaza on its front cell (16,4)",
                east.Objects.Count(o => o.Definition.StableId == "sunflower_cluster") == 1 &&
                east.Objects.Single(o => o.Definition.StableId == "sunflower_cluster").RootCell == new Vector2Int(16, 5) &&
                east.TryGetMarker("landmark_east_flower_plaza", out var lm) && lm == new Vector2Int(16, 4));
            var yellow = east.Objects.Count(o => o.Definition.StableId == "flower_yellow");
            Check("yellow accents: yellow flowers (8 by the plaza + more around), a yellow sign at the entrance", yellow >= 15 &&
                east.Objects.Count(o => o.Definition.StableId == "sign_yellow") == 1, "yellow flowers " + yellow);
            Check("the existing look is reused: trees, stones, white flowers, a bench from the field's catalog",
                new[] { "tree_basic", "stone_basic", "flower_basic", "bench_basic" }.All(id => east.Objects.Any(o => o.Definition.StableId == id)));
            Check("first_field unchanged: the same as published ver3.1 / ver3.2 (Git)",
                V32.GitSameAsVer31("unity-project/Assets/Content/Maps/Authoring/first_field.hwmap.json") &&
                V32.GitSameAsVer31("unity-project/Assets/Content/Maps/first_field.asset"));
            var sign = east.Objects.Single(o => o.Definition.StableId == "sign_yellow");
            foreach (var text in new[] { sign.SignText, "きいろい はな。", "ひまわり。 みんな おひさまの ほうを むいている。" })
                Check($"font: every character of 「{text}」", GameBitmapFont.HasAllGlyphs(text),
                    new string(text.Where(ch => !GameBitmapFont.HasAllGlyphs(ch.ToString())).ToArray()));
            var model = new SharedWorldModel(east);
            var reach = V32.ReachableCells(model, new Vector2Int(0, 5), _ => true);
            var actions = east.Objects.SelectMany(o => o.Definition.ActionPoints.Select(a => o.RootCell + a.PlayerCellOffset)).Distinct().ToList();
            Check("every Action Point (sign, bench seats, landmark, flowers...) can be reached from the road's West end",
                actions.All(reach.Contains), string.Join(" ", actions.Where(a => !reach.Contains(a))));
        }

        // ---------------------------------------------------------------- 6 seamless crossing

        private static void SeamlessChecks()
        {
            C6.SetLegacy(null, null);
            foreach (var mode in new[] { PocViewMode.PixelView2D, PocViewMode.ThirdPerson3D })
            {
                var c = C6.Start(new HarukaMemoryStorage());
                c.Switcher.SetMode(mode);
                var tag = mode == PocViewMode.PixelView2D ? "2D: " : "3D: ";
                Check(tag + "walked from the start to the East road's end (10,-1)", V32.WalkTo(c, FieldRoadEnd), c.Session.Cell.ToString());
                var mapChanged = 0;
                Action<SharedWorldModel> onMap = _ => mapChanged++;
                c.Session.MapChanged += onMap;
                var before = (c.Session.MapLoads, c.Session.CompletedSteps, c.LifeLog.TotalSteps, c.Session.RegionChanges, c.Presenter.BuiltMapId,
                    c.Presenter.Streamer.Stats.TotalUnloads, V32.Footsteps().PlayCount, c.Session.LastTransition.Kind);
                var loadsBefore = c.Presenter.Streamer.Stats.TotalLoads;
                V32.Step(c, Vector2Int.right, 0.3f);
                Check(tag + "one step East from (10,-1) arrives on (11,-1) in east_area_01", c.Session.Cell == EastRoadStart && c.Session.CurrentRegionMapId == EastId,
                    c.Session.Cell + " " + c.Session.CurrentRegionMapId);
                Check(tag + "no map load, no MapChanged, no transition, the same outdoor world (no scene change, no fade)",
                    c.Session.MapLoads == before.MapLoads && mapChanged == 0 && c.Session.LastTransition.Kind == before.Kind &&
                    c.Session.World == c.Session.OutdoorWorld && c.Presenter.BuiltMapId == before.BuiltMapId);
                Check(tag + "exactly one step (session, Life Log, footstep), facing Right, one region change",
                    c.Session.CompletedSteps == before.CompletedSteps + 1 && c.LifeLog.TotalSteps == before.TotalSteps + 1 &&
                    V32.Footsteps().PlayCount == before.Item7 + 1 && c.Session.Facing == FacingDirection.Right && c.Session.RegionChanges == before.RegionChanges + 1);
                Check(tag + "no chunk released or rebuilt by the crossing (region border ≠ chunk border)",
                    c.Presenter.Streamer.Stats.TotalUnloads == before.TotalUnloads && c.Presenter.Streamer.Stats.TotalLoads == loadsBefore, V32.ChunkSummary(c));
                var steps = c.Session.CompletedSteps;
                Check(tag + "walk on to the plaza's front (27,-2)", V32.WalkTo(c, PlazaFront), c.Session.Cell.ToString());
                Check(tag + "on the way: chunk border x 15|16 crossed inside the East region (the region did not change)",
                    c.Session.RegionChanges == before.RegionChanges + 1 && c.Session.CurrentRegionMapId == EastId);
                var found = c.Session.World.FindInteractionAhead(c.Session.Cell, FacingDirection.Up, out var message);
                Check(tag + "the landmark answers from its front cell", found?.DefinitionId == "sunflower_cluster", message);
                Check(tag + "back West to first_field's road (8,-1)", V32.WalkTo(c, new Vector2Int(8, -1)) && c.Session.CurrentRegionMapId == "first_field");
                Check(tag + "the way back: no map load either, region changed once more, steps = cells walked",
                    c.Session.MapLoads == before.MapLoads && mapChanged == 0 && c.Session.RegionChanges == before.RegionChanges + 2 &&
                    c.LifeLog.TotalSteps - before.TotalSteps == c.Session.CompletedSteps - before.CompletedSteps);
                c.Session.MapChanged -= onMap;
                // Walking across the border on every row of the field's East edge (not only the road).
                var crossable = Enumerable.Range(-6, 13).Count(y => c.Session.OutdoorWorld.CanEnter(new Vector2Int(10, y)) && c.Session.OutdoorWorld.CanEnter(new Vector2Int(11, y)));
                Check(tag + "the border is no wall: on every row where both sides are free, the step East is allowed", crossable >= 8, "rows " + crossable);
            }
            // The step that crosses: half-way the session already counts the East cell as its target.
            var g = C6.Start(new HarukaMemoryStorage());
            V32.WalkTo(g, FieldRoadEnd);
            C6.Release(g);
            for (var i = 0; i < 4; i++) V32.Frame(g, 1f / 60f, Vector2Int.right);
            C6.Release(g);
            var midway = g.Session.IsMoving && g.Session.StepFromCell == FieldRoadEnd && g.Session.StepToCell == EastRoadStart;
            V32.Settle(g, 0.3f);
            Check("half-way across the region border the step finishes on (11,-1) (never back on (10,-1))", midway && g.Session.Cell == EastRoadStart);
        }

        // ---------------------------------------------------------------- 7.3 camera continuity

        private static void CameraChecks()
        {
            // first_field framed as in ver3.2: the 2D camera at the start and on the field's West
            // side stands where the one-region world (ver3.2) had it.
            Vector3 CameraAt(bool eastToo, Vector2Int cell)
            {
                var g = C6.Start(new HarukaMemoryStorage(), null, s =>
                {
                    if (eastToo) return;
                    var f = Map("first_field");
                    s.UseOutdoorWorldForTest(OutdoorWorldDefinition.Single(f), f, false);
                });
                g.Session.PlaceForCheck(cell, FacingDirection.Down);
                g.Pixel.SnapToTarget();
                V32.Settle(g, 1.5f);
                return g.Pixel.WorldCamera.transform.position;
            }
            foreach (var cell in new[] { Vector2Int.zero, new Vector2Int(-9, 2) })
            {
                var before = CameraAt(false, cell);
                var after = CameraAt(true, cell);
                Check($"2D: with her on {cell} the camera frames first_field as ver3.2 did (the East area does not shift the field's picture)",
                    Vector3.Distance(before, after) < 0.01f, $"ver3.2-like {before} / ver3.3 {after}");
            }

            foreach (var mode in new[] { PocViewMode.PixelView2D, PocViewMode.ThirdPerson3D })
            {
                var c = C6.Start(new HarukaMemoryStorage());
                c.Switcher.SetMode(mode);
                var camera = mode == PocViewMode.PixelView2D ? c.Pixel.WorldCamera : c.Third.ViewCamera;
                var tag = mode == PocViewMode.PixelView2D ? "2D: " : "3D: ";
                V32.WalkTo(c, new Vector2Int(6, -1));
                V32.Settle(c, 1.5f);
                float Largest(int steps)
                {
                    var largest = 0f;
                    var last = camera.transform.position;
                    for (var s = 0; s < steps; s++)
                    {
                        C6.Release(c);
                        var raw = V32.Raw(c, Vector2Int.right);
                        for (var i = 0; i < 4; i++) { V32.Frame(c, 1f / 60f, raw); largest = Mathf.Max(largest, Vector3.Distance(last, camera.transform.position)); last = camera.transform.position; }
                        C6.Release(c);
                        for (var i = 0; i < 14; i++) { V32.Frame(c, 1f / 60f, Vector2Int.zero); largest = Mathf.Max(largest, Vector3.Distance(last, camera.transform.position)); last = camera.transform.position; }
                    }
                    return largest;
                }
                var inside = Largest(3);   // (6,-1) -> (9,-1): steps inside first_field
                V32.WalkTo(c, new Vector2Int(9, -1));
                var across = Largest(4);   // (9,-1) -> (13,-1): over the region border
                // A snap would move the camera by at least a cell (0.5) in one frame; following a step at
                // walking speed moves it a few hundredths per frame.
                Check(tag + "over the region border the camera only glides (largest move per frame < 0.1 unit: no snap, no jump)",
                    across < 0.1f, $"largest per frame: over the border {across:0.000}, inside the field {inside:0.000}");
                var player = GridSpace3D.ToWorld(c.Session.Cell);
                var ray = new Ray(camera.transform.position, camera.transform.forward);
                var aim = ray.origin + ray.direction * (-ray.origin.y / ray.direction.y);
                V32.Settle(c, 1.5f);
                Check(tag + "and keeps following her in the East area", new Vector2(aim.x - player.x, aim.z - player.z).magnitude < 3f, $"aim {aim} player {player}");
            }
        }

        // ---------------------------------------------------------------- 8 chunks across regions

        private static void ChunkChecks()
        {
            var c = C6.Start(new HarukaMemoryStorage());
            var index = c.Session.OutdoorIndex;
            Check("one chunk system for both regions (16 x 16): the East region's chunks are in the same table",
                index.ChunkSize == 16 && new[] { new ChunkCoord(0, -1), new ChunkCoord(1, -1), new ChunkCoord(2, -1), new ChunkCoord(2, 0) }.All(k => index.ChunkAt(k) != null));
            Check("chunk (0,-1) holds cells of both regions (the region border x 10|11 runs inside it)",
                index.RegionAt(new Vector2Int(10, -1)).MapId == "first_field" && index.RegionAt(new Vector2Int(11, -1)).MapId == EastId &&
                index.ChunkOfCell(new Vector2Int(10, -1)) == index.ChunkOfCell(new Vector2Int(11, -1)));
            Check("at the start the streamer already draws the East area's near chunks (3D reach 40 cells), nothing waits on the step",
                c.Presenter.IsChunkDrawn(index.ChunkOfCell(EastRoadStart)) && c.Presenter.Streamer.Stats.SyncLoads == 0, V32.ChunkSummary(c));
            V32.WalkTo(c, new Vector2Int(36, -1), null, 0.14f);
            V32.Settle(c, 0.5f);
            Check("walked to the East area's East end (36,-1); every chunk under her was ready before (no step-gate build)",
                c.Session.Cell == new Vector2Int(36, -1) && c.Presenter.Streamer.Stats.SyncLoads == 0, V32.ChunkSummary(c));
            Check("beyond (37,-1) is no region: not walkable, no grass (nothing generated)", !c.Session.World.CanEnter(new Vector2Int(37, -1)) &&
                !c.Session.World.HasGrass(new Vector2Int(37, -1)));
            var views = V32.DuplicateViews(c);
            Check("no Object drawn twice (both regions' Objects, chunks shared)", views.Count == 0, string.Join(" ", views));
            V32.WalkTo(c, new Vector2Int(-9, 0), null, 0.12f);
            V32.Settle(c, 0.5f);
            var released = c.Presenter.Streamer.Stats.TotalUnloads;
            V32.WalkTo(c, PlazaFront, null, 0.12f);
            V32.Settle(c, 0.5f);
            var sunflower = c.Presenter.Views.Where(v => v.DefinitionId == "sunflower_cluster").ToList();
            Check("West end -> East plaza and back: chunks released and built again, the landmark drawn once at its cell",
                sunflower.Count == 1 && Vector3.Distance(sunflower[0].transform.position, GridSpace3D.ToWorld(Landmark)) < 1e-4f && V32.DuplicateViews(c).Count == 0,
                $"unloads {released} -> {c.Presenter.Streamer.Stats.TotalUnloads} {V32.ChunkSummary(c)}");
            var wrong = 0;
            var east = index.FindRegion(EastId);
            for (var y = east.Map.MinCell.y; y <= east.Map.MaxCell.y; y++)
            for (var x = east.Map.MinCell.x; x <= east.Map.MaxCell.x; x++)
            {
                var local = new Vector2Int(x, y);
                if (index.IsBlocked(east.ToWorld(local)) != MapPlacementRules.BlocksMovement(east.Map, local) ||
                    index.HasGrass(east.ToWorld(local)) != MapPlacementRules.HasGrass(east.Map, local)) wrong++;
            }
            Check("collision and grass of every East cell = its Map's rules at its local cell (no offset error)", wrong == 0, "wrong " + wrong);
            Check("the region border cells answer as their own Map: (10,y) by first_field's rules, (11,y) by east_area_01's",
                Enumerable.Range(-6, 13).All(y => index.IsBlocked(new Vector2Int(10, y)) == MapPlacementRules.BlocksMovement(Map("first_field"), new Vector2Int(10, y)) &&
                    index.IsBlocked(new Vector2Int(11, y)) == MapPlacementRules.BlocksMovement(Map(EastId), new Vector2Int(0, y + 6))));
            // first_field's sign on its East edge (10,0): its picture reaches the East region's cells.
            var sign = index.FindInstance("obj_sign_east_v30_preview");
            var extent = OutdoorWorldIndex.PresentationExtent(sign);
            var signChunk = index.ChunkOfCell(sign.RootCell);
            Check("an Object at the region border (first_field's sign at (10,0)): its picture reaches East cells, its blocked cell stays first_field's, drawn once",
                index.RegionAt(new Vector2Int(extent.Max.x, sign.RootCell.y))?.MapId == EastId && index.RegionAt(sign.RootCell).MapId == "first_field" &&
                index.IsBlocked(sign.RootCell) && c.Presenter.Views.Count(v => v.InstanceId == sign.InstanceId) == 1, extent + " chunk " + signChunk);
            // The road across the border: one road for the world (the ground builder joins (10,-1) and (11,-1)).
            var road = PocEnvironmentBuilder.CollectWorldRoad(index, 8, out _, out _);
            Check("one continuous road from first_field into the East area: (3..36,-1) and (8..10,-1) are road cells of the same road",
                Enumerable.Range(8, 29).All(x => road.Contains(new Vector2Int(x, -1))));
        }

        // ---------------------------------------------------------------- first_field's own look

        private static void FieldLookChecks()
        {
            var c = C6.Start(new HarukaMemoryStorage());
            var map = Map("first_field");
            var holder = new GameObject("ver3.2 reference build (first_field alone)");
            try
            {
                var catalog = AssetDatabase.LoadAssetAtPath<PocPresentationCatalog>(PocSceneBuilder.CatalogPath);
                var legacy = PocEnvironmentBuilder.Build(new SharedWorldModel(map), catalog.Environment, catalog.VertexColorMaterial, holder.transform, out _, out _);
                var rect = new CellRect(map.MinCell, map.MaxCell);
                Vector3 Round(Vector3 p) => new Vector3(Mathf.Round(p.x * 1000f), Mathf.Round(p.y * 1000f), Mathf.Round(p.z * 1000f));
                var now = new HashSet<Vector3>(c.Presenter.Environment.TuftPositions.Where(p => rect.Contains(GridSpace3D.ToCell(p))).Select(Round));
                var before = new HashSet<Vector3>(legacy.TuftPositions.Where(p => rect.Contains(GridSpace3D.ToCell(p))).Select(Round));
                Check("first_field's own cells: every grass tuft where ver3.2 had it (the East area changes nothing inside the field)",
                    now.SetEquals(before), $"{now.Count} / {before.Count}");
            }
            finally { UnityEngine.Object.DestroyImmediate(holder); }
            var fieldViews = c.Presenter.Views.Where(v => c.Session.OutdoorIndex.FindInstance(v.InstanceId)?.RegionMapId == "first_field").ToList();
            Check("every first_field Object drawn once at its cell (29), Outline ids in its Map's order as before",
                fieldViews.Count == map.Objects.Count && map.Objects.All(o => fieldViews.Count(v => v.InstanceId == o.InstanceId && v.RootCell == o.RootCell) == 1) &&
                fieldViews.All(v => (int)v.GetComponentInChildren<MeshFilter>().sharedMesh.uv2[0].x == map.Objects.ToList().FindIndex(o => o.InstanceId == v.InstanceId) % 249 + 1),
                $"{fieldViews.Count} / {map.Objects.Count}");
            var wrong = 0;
            for (var y = map.MinCell.y; y <= map.MaxCell.y; y++)
            for (var x = map.MinCell.x; x <= map.MaxCell.x; x++)
            {
                var cell = new Vector2Int(x, y);
                if (c.Session.OutdoorWorld.IsBlocked(cell) != MapPlacementRules.BlocksMovement(map, cell) ||
                    c.Session.OutdoorWorld.HasGrass(cell) != MapPlacementRules.HasGrass(map, cell)) wrong++;
            }
            Check("first_field's collision and grass on all its 273 cells as before", wrong == 0, "wrong " + wrong);
        }

        // ---------------------------------------------------------------- 7 display

        private static void DisplayChecks()
        {
            var c = C6.Start(new HarukaMemoryStorage());
            V32.WalkTo(c, new Vector2Int(20, -1), null, 0.14f);
            V32.Settle(c, 0.3f);
            var eastViews = c.Presenter.Views.Where(v => c.Session.OutdoorIndex.FindInstance(v.InstanceId)?.RegionMapId == EastId).ToList();
            Check("the East area's Objects are drawn from the Blender models (no fallback box), at their world cells",
                eastViews.Count >= 30 && eastViews.All(v => !v.IsFallback) &&
                eastViews.All(v => Vector3.Distance(v.transform.position, GridSpace3D.ToWorld(v.RootCell)) < 1e-4f), "views " + eastViews.Count);
            Check("Outline: every East Object carries its outline id", eastViews.All(v => v.GetComponentsInChildren<MeshFilter>().All(f => f.sharedMesh.uv2 != null && f.sharedMesh.uv2.Length > 0)));
            Check("shadows: trees and sunflowers cast shadows", eastViews.Where(v => v.DefinitionId == "tree_basic" || v.DefinitionId == "sunflower_cluster")
                .All(v => v.GetComponentsInChildren<Renderer>().Any(r => r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off)));
            var binding = AssetDatabase.LoadAssetAtPath<PocPresentationCatalog>(PocSceneBuilder.CatalogPath).Find("sunflower_cluster");
            Check("Tree Fade: the sunflowers fade in front of her in 2D and 3D, as the trees", binding != null && binding.fadeInFront2D && binding.fadeInFront3D &&
                eastViews.Where(v => v.DefinitionId == "sunflower_cluster").All(v => v.FadeInFront2D && v.FadeInFront3D));
            var grass = (List<Vector2Int>)typeof(PocPixelView2D).GetField("grassCells", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(c.Pixel);
            c.Pixel.HasGrass(Vector2Int.zero);
            Check("2D grass: the East area's grass cells are listed with the field's (one list, North rows first)",
                grass.Any(g => g.x >= 11) && grass.Any(g => g.x <= 10) && grass.Zip(grass.Skip(1), (a, b) => a.y > b.y || a.y == b.y && a.x < b.x).All(x => x));
            var env = c.Presenter.Environment;
            var index = c.Session.OutdoorIndex;
            float TuftsPerGrassCell(string mapId)
            {
                var rect = index.FindRegion(mapId).Rect;
                var grassCells = 0;
                for (var y = rect.Min.y; y <= rect.Max.y; y++)
                for (var x = rect.Min.x; x <= rect.Max.x; x++) if (index.HasGrass(new Vector2Int(x, y))) grassCells++;
                var tufts = env.TuftPositions.Count(p => rect.Contains(GridSpace3D.ToCell(p)));
                return grassCells > 0 ? tufts / (float)grassCells : 0f;
            }
            var fieldDensity = TuftsPerGrassCell("first_field");
            var eastDensity = TuftsPerGrassCell(EastId);
            Check("grass tufts as dense in the East area as in the field (one ground builder, the same per-cell rules)",
                fieldDensity > 0f && Mathf.Abs(eastDensity - fieldDensity) / fieldDensity < 0.3f, $"tufts per grass cell: field {fieldDensity:0.00} east {eastDensity:0.00}");
            foreach (var mode in new[] { PocViewMode.PixelView2D, PocViewMode.ThirdPerson3D })
            {
                c.Switcher.SetMode(mode);
                V32.Settle(c, 0.3f);
                var player = GridSpace3D.ToWorld(c.Session.Cell);
                Check((mode == PocViewMode.PixelView2D ? "2D" : "3D") + ": she is drawn at her world cell in the East area",
                    Vector3.Distance(new Vector3(c.Actor.transform.position.x, 0f, c.Actor.transform.position.z), new Vector3(player.x, 0f, player.z)) < 0.05f);
            }
            // The top-right button at the region border.
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            V32.WalkTo(c, EastRoadStart);
            var before = (c.Session.Cell, c.Session.Facing, c.Session.CompletedSteps, c.Crow.Cell, c.Session.CurrentRegionMapId);
            var switches = c.Switcher.SwitchCount;
            for (var i = 0; i < 6; i++) { c.Menu.ActivateQuickItem(C6.Clock); V32.Settle(c, 0.6f); }
            var after = (c.Session.Cell, c.Session.Facing, c.Session.CompletedSteps, c.Crow.Cell, c.Session.CurrentRegionMapId);
            Check("the top-right 3Dへ / 2Dへ button 6 times at the region border: 6 switches; cell, facing, steps, region unchanged",
                c.Switcher.SwitchCount == switches + 6 && before.Item1 == after.Item1 && before.Item2 == after.Item2 && before.Item3 == after.Item3 &&
                before.Item5 == after.Item5, $"switches +{c.Switcher.SwitchCount - switches} {before} -> {after}");
            c.Presenter.RegionBordersVisible = true;
            var shown = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Count(r => r.name.StartsWith("Region border", StringComparison.Ordinal));
            c.Presenter.RegionBordersVisible = false;
            var hidden = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Count(r => r.name.StartsWith("Region border", StringComparison.Ordinal));
            Check("debug region borders (direction colours): shown only when switched on, hidden by default", shown > 0 && hidden == 0, $"shown {shown} hidden {hidden}");
        }

        // ---------------------------------------------------------------- 9 save

        private static void SaveChecks()
        {
            C6.SetLegacy(null, null);
            Check("Save Schema stays 1", HarukaSaveData.SchemaVersion == 1);
            foreach (var mode in new[] { PocViewMode.PixelView2D, PocViewMode.ThirdPerson3D })
            {
                var storage = new HarukaMemoryStorage();
                var c = C6.Start(storage);
                V32.Settle(c, 1.2f);
                c.Switcher.SetMode(mode);
                V32.WalkTo(c, new Vector2Int(20, 0), null, 0.14f);
                var steps = c.LifeLog.TotalSteps;
                c.Save.OnPageHidden("");
                var latest = C6.Latest(c);
                var tag = mode == PocViewMode.PixelView2D ? "2D" : "3D";
                Check($"saved in the East area ({tag}): Map ID east_area_01 and its local cell (9,6) (world (20,0)), Schema 1, no chunk or world fields",
                    latest.MapId == EastId && latest.CellX == 9 && latest.CellY == 6 && latest.Schema == 1 &&
                    C6.Sealed(latest).IndexOf("chunk", StringComparison.OrdinalIgnoreCase) < 0, $"{latest.MapId} ({latest.CellX},{latest.CellY})");
                var r = C6.Start(storage);
                Check($"restart: back in the East area on (20,0) in {tag}, its chunk drawn, steps kept, no step added",
                    r.Session.CurrentRegionMapId == EastId && r.Session.Cell == new Vector2Int(20, 0) && (r.Switcher.Mode == mode) &&
                    r.Presenter.IsChunkDrawn(r.Session.OutdoorIndex.ChunkOfCell(r.Session.Cell)) && r.LifeLog.TotalSteps == steps && r.Session.CompletedSteps == 0,
                    $"{r.Session.CurrentRegionMapId} {r.Session.Cell} {r.Switcher.Mode} steps {r.LifeLog.TotalSteps}/{steps}");
                var camera = mode == PocViewMode.PixelView2D ? r.Pixel.WorldCamera : r.Third.ViewCamera;
                V32.Frame(r, 1f / 60f, Vector2Int.zero);
                var first = camera.transform.position;
                V32.Settle(r, 1f);
                Check($"restart in the East area ({tag}): the camera starts on her (no slide from the field's start)", Vector3.Distance(first, camera.transform.position) < 0.06f,
                    first + " / " + camera.transform.position);
                if (mode == PocViewMode.PixelView2D)
                {
                    Check("restart there and walk home: West along the road into first_field", V32.WalkTo(r, new Vector2Int(5, -1), null, 0.12f) &&
                        r.Session.CurrentRegionMapId == "first_field");
                    Check("the save menu names the place ひがし (the East direction's name)", r.Save.PlaceName(EastId) == "ひがし" && r.Save.PlaceName("first_field") == "そと");
                }
            }
            // Export / Import of an East save; refusal of bad data.
            var s1 = new HarukaMemoryStorage();
            var g = C6.Start(s1);
            V32.Settle(g, 1.2f);
            V32.WalkTo(g, PlazaFront, null, 0.12f);
            var exported = g.Save.ExportText();
            var fresh = C6.Start(new HarukaMemoryStorage());
            var check = fresh.Save.CheckImport(exported, out var imported);
            var applied = check == HarukaSaveSystem.ImportCheck.Ok && fresh.Save.ApplyImport(imported, C6.Clock, out _);
            Check("Export in the East area -> Import: accepted, she is at the plaza's front (27,-2)", applied && fresh.Session.Cell == PlazaFront &&
                fresh.Session.CurrentRegionMapId == EastId, check.ToString());
            var badRegion = V32.Old("ver3.3", "east_area_99", new Vector2Int(3, 3));
            var badCell = V32.Old("ver3.3", EastId, new Vector2Int(40, 3));
            Check("Import of an unknown region id is refused (UnknownMap), nothing replaced",
                fresh.Save.CheckImport(C6.Sealed(badRegion), out _) == HarukaSaveSystem.ImportCheck.UnknownMap && fresh.Session.Cell == PlazaFront);
            Check("Import of a cell outside the East Map is refused (OutOfMap)", fresh.Save.CheckImport(C6.Sealed(badCell), out _) == HarukaSaveSystem.ImportCheck.OutOfMap);
            // A snapshot naming a blocked East cell: resumes on the nearest free cell of the East area.
            var s2 = new HarukaMemoryStorage();
            new HarukaSnapshotStore(s2).Write(V32.Old("ver3.3", EastId, new Vector2Int(16, 5)));   // the sunflowers' cell
            var r2 = C6.Start(s2);
            Check("a save on a blocked East cell (the sunflowers) resumes on a free cell next to it, in the East area",
                r2.Session.CurrentRegionMapId == EastId && r2.Session.World.CanEnter(r2.Session.Cell) && (r2.Session.Cell - Landmark).sqrMagnitude <= 2, r2.Session.Cell.ToString());
            // A first_field save on a cell outside every region comes back inside first_field.
            var s4 = new HarukaMemoryStorage();
            new HarukaSnapshotStore(s4).Write(V32.Old("ver3.2", "first_field", new Vector2Int(40, 40)));
            var r4 = C6.Start(s4);
            Check("a first_field save on a cell outside every region: safe return inside first_field (its own region first), steps kept",
                r4.Session.CurrentRegionMapId == "first_field" && r4.Session.World.CanEnter(r4.Session.Cell) && r4.LifeLog.TotalSteps == 4321,
                r4.Session.CurrentRegionMapId + " " + r4.Session.Cell);
            // Old saves.
            foreach (var (version, map, cell) in new[] { ("ver3.0", "first_field", new Vector2Int(-1, 0)), ("ver3.1", "halka_house", new Vector2Int(-2, 0)),
                ("ver3.1", "halka_soundbooth", new Vector2Int(3, -1)), ("ver3.2", "first_field", new Vector2Int(10, -1)), ("ver3.2", "halka_house", new Vector2Int(4, -2)) })
            {
                var st = new HarukaMemoryStorage();
                new HarukaSnapshotStore(st).Write(V32.Old(version, map, cell, HarukaSaveData.View3D, true));
                var o = C6.Start(st);
                Check($"{version} save ({map} {cell}, 3D, AUTO ON): same place, view, AUTO, steps, time", o.Session.World.MapId == map && o.Session.Cell == cell &&
                    o.Switcher.Mode == PocViewMode.ThirdPerson3D && o.Auto.AutoEnabled && o.LifeLog.TotalSteps == 4321 && o.Clock.AnchorMilliseconds == 1760000000000L,
                    $"{o.Session.World.MapId} {o.Session.Cell}");
            }
            // Rollback: a ver3.2 build (no East region in its registry) reading a save made in the East area.
            var s3 = new HarukaMemoryStorage();
            new HarukaSnapshotStore(s3).Write(V32.Old("ver3.3", EastId, new Vector2Int(9, 6)));
            MapDefinition field = null;
            var old = C6.Start(s3, null, s =>
            {
                field = Map("first_field");   // loaded after the harness's new scene (which unloads unused assets)
                s.UseOutdoorWorldForTest(OutdoorWorldDefinition.Single(field), field, false);
            });
            Check("rollback (a ver3.2-like build without the East region): an East save starts at the Player Start, steps / time / settings kept (never reset)",
                old.Session.World.MapId == "first_field" && old.Session.Cell == field.PlayerSpawnCell && old.LifeLog.TotalSteps == 4321 &&
                old.Clock.AnchorMilliseconds == 1760000000000L && old.Save.LoadSource == "snapshot",
                $"{old.Save.LoadSource} {old.Session.World.MapId} {old.Session.Cell} start {old.Session.StartMap?.MapId} spawns {old.Session.StartMap?.EntitySpawns.Count} steps {old.LifeLog.TotalSteps}");
        }

        // ---------------------------------------------------------------- 10 / 11 crow, AUTO

        private static void CrowAutoChecks()
        {
            var c = C6.Start(new HarukaMemoryStorage());
            var crows = UnityEngine.Object.FindObjectsByType<PocCrow>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Check("one crow; it lives in first_field (its region, as before)", crows.Length == 1 &&
                c.Session.OutdoorIndex.RegionAt(c.Crow.Cell)?.MapId == "first_field", c.Crow.Cell.ToString());
            var activities = new HashSet<CrowActivity>();
            V32.WalkTo(c, new Vector2Int(30, -1), d => activities.Add(c.Crow.Activity), 0.12f);
            for (var t = 0f; t < 40f; t += 0.05f) { V32.Frame(c, 0.05f, Vector2Int.zero); activities.Add(c.Crow.Activity); }
            V32.WalkTo(c, new Vector2Int(2, -1), d => activities.Add(c.Crow.Activity), 0.12f);
            for (var t = 0f; t < 30f; t += 0.05f) { V32.Frame(c, 0.05f, Vector2Int.zero); activities.Add(c.Crow.Activity); }
            Check("while she went East and back: still one crow, it kept living in first_field",
                UnityEngine.Object.FindObjectsByType<PocCrow>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1 &&
                c.Session.OutdoorIndex.RegionAt(c.Crow.Cell)?.MapId == "first_field" && activities.Count >= 2, string.Join(",", activities));
            // AUTO in first_field next to the border never walks into the East area by itself.
            c.Session.PlaceForCheck(FieldRoadEnd, FacingDirection.Right);
            V32.Settle(c, 0.2f);
            Check("AUTO refuses (11,-1) from (10,-1) (another region); a player step there is allowed",
                !c.Auto.CanAutoEnter(EastRoadStart) && c.Auto.CanAutoEnter(new Vector2Int(9, -1)) && c.Session.World.CanEnter(EastRoadStart));
            c.Auto.SetAutoEnabled(true, C6.Clock);
            var visited = new HashSet<Vector2Int>();
            for (var t = 0f; t < 300f; t += 0.05f) { V32.Frame(c, 0.05f, Vector2Int.zero, true); visited.Add(c.Session.Cell); }
            Check("AUTO started on first_field's East edge for 5 minutes: walks, never enters the East area (found by walking there herself)",
                visited.Count > 5 && visited.All(v => v.x <= 10), "cells " + visited.Count + " max x " + visited.Max(v => v.x));
            // In the East area AUTO walks within it.
            c.Session.PlaceForCheck(new Vector2Int(20, -1), FacingDirection.Down);
            V32.Settle(c, 0.2f);
            visited.Clear();
            var loads = c.Session.MapLoads;
            for (var t = 0f; t < 300f; t += 0.05f) { V32.Frame(c, 0.05f, Vector2Int.zero, true); visited.Add(c.Session.Cell); }
            Check("AUTO in the East area for 5 minutes: walks there, stays in the East area, no map change",
                visited.Count > 5 && visited.All(v => v.x >= 11) && c.Session.MapLoads == loads, "cells " + visited.Count + " min x " + visited.Min(v => v.x));
            c.Auto.SetAutoEnabled(false, C6.Clock);
        }

        // ---------------------------------------------------------------- 12 house and booth

        private static void RoomChecks()
        {
            var c = C6.Start(new HarukaMemoryStorage());
            V32.WalkTo(c, new Vector2Int(20, -1), null, 0.12f);
            c.Session.OutdoorWorld.TryGetHouseDoor(out var door);
            Check("East area -> first_field -> the house door's front, walking", V32.WalkTo(c, door + Vector2Int.down, null, 0.12f));
            V32.Step(c, Vector2Int.up, 0.4f);
            Check("-> halka_house (the room change as before)", c.Session.World.MapId == "halka_house" && c.Session.Cell == new Vector2Int(0, -3));
            var house = Map("halka_house");
            Check("the house keeps its ver3.2 furniture (kitchen, rug, low table, sofa, bed, shelf, booth)",
                new[] { "kitchen_counter", "living_rug", "low_table_living", "sofa_basic", "bed_basic", "studio_shelf_basic", "soundbooth_exterior" }
                    .All(id => house.Objects.Any(o => o.Definition.StableId == id)));
            V32.WalkTo(c, WorldVer31Checks.BoothApproach, null, 0.14f);
            V32.Step(c, Vector2Int.up, 0.4f);
            var booth = Map("halka_soundbooth");
            Check("-> halka_soundbooth, with the 5-cell DTM desk, the 3-cell pen-tablet desk, the guitar and the mic",
                c.Session.World.MapId == "halka_soundbooth" &&
                booth.Objects.Single(o => o.Definition.StableId == "dtm_desk_large").Definition.Footprint.x == 5 &&
                booth.Objects.Single(o => o.Definition.StableId == "illust_desk_tablet").Definition.Footprint.x == 3 &&
                booth.Objects.Any(o => o.Definition.StableId == "guitar_stand") && booth.Objects.Any(o => o.Definition.StableId == "recording_mic_basic"));
            V32.Step(c, Vector2Int.down, 0.4f);
            V32.WalkTo(c, new Vector2Int(0, -3), null, 0.14f);
            V32.Step(c, Vector2Int.down, 0.4f);
            Check("booth -> house -> first_field (in front of the door)", c.Session.World == c.Session.OutdoorWorld && c.Session.Cell == door + Vector2Int.down);
            Check("-> back East into the East area, walking", V32.WalkTo(c, new Vector2Int(20, -1), null, 0.12f) && c.Session.CurrentRegionMapId == EastId);
        }

        // ---------------------------------------------------------------- 13 time, Life Log

        private static void TimeChecks()
        {
            C6.SetLegacy(null, null);
            var c = C6.Start(new HarukaMemoryStorage());
            var anchor = c.Clock.AnchorMilliseconds;
            V32.WalkTo(c, new Vector2Int(9, -1), null, 0.14f);
            var steps = c.LifeLog.TotalSteps;
            for (var i = 0; i < 6; i++) V32.Step(c, i % 2 == 0 ? Vector2Int.right : Vector2Int.left, 0.22f);
            Check("6 steps back and forth over the region border: 6 steps counted, the World Time anchor unchanged",
                c.LifeLog.TotalSteps == steps + 6 && c.Clock.AnchorMilliseconds == anchor, $"+{c.LifeLog.TotalSteps - steps}");
            c.Time.Override = PocPhaseOverride.Night;
            V32.WalkTo(c, new Vector2Int(20, -1), null, 0.12f);
            var materials = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
                .Where(r => r.name.StartsWith("Ground", StringComparison.Ordinal)).Select(r => r.sharedMaterial).Distinct().ToList();
            Check("at night the East area's ground uses the same lit material as the field (one World Time)", materials.Count == 1);
            c.Session.PlaceForCheck(new Vector2Int(36, -1), FacingDirection.Right);
            V32.Settle(c, 0.2f);
            var stuck = c.LifeLog.TotalSteps;
            V32.Step(c, Vector2Int.right, 0.2f);
            Check("a step refused at the world's East end (no region beyond) adds no step", c.LifeLog.TotalSteps == stuck && c.Session.Cell == new Vector2Int(36, -1));
        }
    }
}
