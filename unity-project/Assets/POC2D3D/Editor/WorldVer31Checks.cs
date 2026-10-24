using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Halka.Game.Player;
using Halka.Game.UI;
using Halka.Game.World;
using Halka.Poc2D3D.Save;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using C6 = Halka.Poc2D3D.EditorTools.Phase6SaveChecks;

namespace Halka.Poc2D3D.EditorTools
{
    // はるかわーるど ver3.1「制作拠点アップデート」: the checks of the studio update. The game is started
    // with the Phase 6 harness (the game's own start order, memory storage only: no browser save,
    // no PlayerPrefs left changed). Every check prints PASS or FAIL; nothing is skipped silently.
    //   Prepare: -executeMethod Halka.Poc2D3D.EditorTools.WorldVer31Checks.PrepareBatch
    //            (imports the Authoring JSON, builds the POC and the ver3.1 scenes)
    //   Checks:  -executeMethod Halka.Poc2D3D.EditorTools.WorldVer31Checks.RunBatch
    public static class WorldVer31Checks
    {
        public const string BoothId = "halka_soundbooth";
        public const string HouseId = "halka_house";
        public const string DtmText = "おんがくを つくる つくえ。";
        public const string TabletText = "えきタブ。 イラストを かく。";
        // User feedback 2026-10-24: the booth (3 x 3) at the right end of the house, its door in the middle.
        public static readonly Vector2Int BoothDoor = new Vector2Int(4, 0);
        public static readonly Vector2Int BoothApproach = new Vector2Int(4, -1);
        public static readonly Vector2Int TabletDesk = new Vector2Int(-4, 3);
        public static readonly Vector2Int TabletFront = new Vector2Int(-4, 2);
        public static readonly Vector2Int SofaSeat = new Vector2Int(1, 1);
        public static readonly Vector2Int BoothEntry = new Vector2Int(0, -4);
        public static readonly Vector2Int BoothExit = new Vector2Int(0, -5);
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private static readonly List<string> failures = new List<string>();
        private static readonly SortedDictionary<string, int> perCategory = new SortedDictionary<string, int>();
        private static readonly List<string> lines = new List<string>();
        private static int passes;
        private static string category = "";

        public static void PrepareBatch()
        {
            try
            {
                Halka.Game.Editor.MapAuthoringImporter.SyncAll();
                PocSceneBuilder.Build();
                PocSceneBuilder.Build(PocSceneBuilder.RcScenePath, true);
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

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
            UnityEngine.Random.InitState(3131);
            try
            {
                Run("content", ContentChecks);
                Run("models", ModelChecks);
                Run("transitions", TransitionChecks);
                Run("auto", AutoChecks);
                Run("save", SaveChecks);
                Run("toggle", ToggleChecks);
                Run("toggle-state", ToggleStateChecks);
                Run("mixed-route", MixedRouteChecks);
                Run("auto-takeover", AutoTakeoverChecks);
                Run("old-saves", OldSaveChecks);
                Run("kept-sprites", KeptSpriteChecks);
                Run("room-2d-3d", RoomViewChecks);
                Run("interaction", InteractionChecks);
                Run("night", NightChecks);
                Run("performance", PerformanceChecks);
                Run("font", FontChecks);
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
            foreach (var failure in failures) Debug.LogError("VER31 FAIL " + failure);
            Debug.Log("HARUKA ver3.1 categories: " + string.Join(", ", perCategory.Select(p => p.Key + " " + p.Value)));
            Debug.Log($"HARUKA ver3.1 checks: {passes} passed, {failures.Count} failed");
            var report = Path.Combine(PocSceneBuilder.RepositoryRoot, "..", "HARUKA_WORLD_V31_LOGS", "ver31_checks.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(report));
            File.WriteAllLines(report, lines.Concat(new[] { $"TOTAL {passes} passed, {failures.Count} failed" }));
            return failures.Count == 0;
        }

        // A category that throws counts as one failure (and the run goes on with the next one).
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
            if (condition) { passes++; Debug.Log("VER31 " + line); }
            else failures.Add("[" + category + "] " + name + (detail.Length > 0 ? " | " + detail : ""));
        }

        // ---------------------------------------------------------------- content

        private static MapDefinition Map(string id) => AssetDatabase.LoadAssetAtPath<MapDefinition>("Assets/Content/Maps/" + id + ".asset");
        private static WorldObjectDefinition Definition(string id) =>
            AssetDatabase.FindAssets("t:WorldObjectDefinition").Select(g => AssetDatabase.LoadAssetAtPath<WorldObjectDefinition>(AssetDatabase.GUIDToAssetPath(g)))
                .FirstOrDefault(d => d != null && d.StableId == id);

        private static IEnumerable<WorldObjectPlacement> Placements(MapDefinition map, string definitionId) =>
            map.Objects.Where(p => p.Definition != null && p.Definition.StableId == definitionId);

        private static void ContentChecks()
        {
            var house = Map(HouseId);
            var booth = Map(BoothId);
            Check("halka_soundbooth: its own Map (interior), separate from halka_house", booth != null && booth.MapType == "interior" && house != null && booth != house);
            if (booth == null || house == null) return;
            var size = booth.MaxCell - booth.MinCell + Vector2Int.one;
            Check("halka_soundbooth: 15 x 11 cells (not limited to the house's 13 x 9)", size == new Vector2Int(15, 11), size.ToString());
            Check("halka_soundbooth: entry and exit markers", booth.TryGetMarker("interior_entry", out var entry) && entry == BoothEntry &&
                booth.TryGetMarker("interior_exit", out var exit) && exit == BoothExit);
            Check("halka_soundbooth: booth floor and acoustic walls (booth_floor base, booth_wall Surfaces)",
                booth.BaseSurface != null && booth.BaseSurface.StableId == "booth_floor" &&
                booth.Surfaces.Count(s => s.Definition.StableId == "booth_wall") > 0 && booth.Surfaces.All(s => s.Definition.StableId == "booth_wall"));
            Check("halka_soundbooth: one large DTM desk and the mic stand", Placements(booth, "dtm_desk_large").Count() == 1 &&
                Placements(booth, "recording_mic_basic").Count() == 1);
            var dtmRoot = Placements(booth, "dtm_desk_large").First().RootCell;
            Check("halka_soundbooth: the pen-tablet desk stands just left of the DTM desk, against the same wall",
                Placements(booth, "illust_desk_tablet").Count() == 1 && Placements(booth, "illust_desk_tablet").First().RootCell == TabletDesk &&
                TabletDesk + new Vector2Int(2, 0) == dtmRoot, TabletDesk + " / DTM " + dtmRoot);
            Check("halka_soundbooth: no Player Start (the one player_main stays in first_field)", !booth.TryGetEntitySpawn("player_main", out _));
            Check("halka_soundbooth: map rules (MapPlacementRules.Validate) report nothing", MapPlacementRules.Validate(booth).Count == 0,
                string.Join("; ", MapPlacementRules.Validate(booth)));

            Check("halka_house: the easel is no longer placed", !Placements(house, "art_easel_basic").Any());
            Check("halka_house: the mic stand is no longer placed (it is in the booth: no copy left in the house)", !Placements(house, "recording_mic_basic").Any());
            Check("halka_house: one sound booth, its door (root) on (4, 0)", Placements(house, "soundbooth_exterior").Count() == 1 &&
                Placements(house, "soundbooth_exterior").First().RootCell == BoothDoor);
            var boothCells = Placements(house, "soundbooth_exterior").SelectMany(p => p.Definition.EffectiveBlockedOffsets().Select(o => p.RootCell + o)).Append(BoothDoor).ToList();
            Check("halka_house: the booth fills the right end: its East column touches the East wall, its top row the North wall",
                boothCells.Max(c => c.x) == house.MaxCell.x - 1 && boothCells.Max(c => c.y) == house.MaxCell.y - 2, string.Join(" ", boothCells));
            Check("halka_house: no illustration desk and no PC desk (desk_basic) in the house",
                !Placements(house, "illust_desk_tablet").Any() && !Placements(house, "desk_basic").Any());
            var kept = new Dictionary<string, Vector2Int>
            {
                ["bed_basic"] = new Vector2Int(-5, 0), ["studio_shelf_basic"] = new Vector2Int(-1, 2), ["sofa_basic"] = new Vector2Int(1, 2)
            };
            Check("halka_house: bed and shelf stay; the sofa moved West along the North wall (1, 2) to make room",
                kept.All(k => Placements(house, k.Key).Count() == 1 && Placements(house, k.Key).First().RootCell == k.Value));
            Check("halka_house: still 13 x 9 (the house was not enlarged)", house.MaxCell - house.MinCell + Vector2Int.one == new Vector2Int(13, 9));
            Check("halka_house: map rules report nothing", MapPlacementRules.Validate(house).Count == 0, string.Join("; ", MapPlacementRules.Validate(house)));
            var model = new SharedWorldModel(house);
            var reach = Reachable(model, new Vector2Int(0, -3), c => !HarukaSaveSystem.IsTransitionCell(model, c));
            var actionCells = house.Objects.SelectMany(p => p.Definition.ActionPoints.Select(a => p.RootCell + a.PlayerCellOffset)).ToList();
            Check("halka_house: every Action Point cell (bed, sofa seats) can be reached from the entry",
                actionCells.All(reach.Contains), string.Join(" ", actionCells.Where(c => !reach.Contains(c))));
            Check("halka_house: the booth door can be walked onto from (4, -1)", reach.Contains(BoothApproach) && model.CanEnter(BoothDoor));

            foreach (var id in new[] { "art_easel_basic", "recording_mic_basic", "desk_basic" })
                Check("Definition kept: " + id, Definition(id) != null);
            Check("easel assets kept (model, sprite, Blender source)",
                File.Exists(Path.Combine(PocSceneBuilder.RepositoryRoot, "unity-project", "Assets", "POC2D3D", "Models", "Phase3", "art_easel_basic.fbx")) &&
                File.Exists(Path.Combine(PocSceneBuilder.RepositoryRoot, "unity-project", "Assets", "Content", "World", "art_easel.png")) &&
                File.Exists(Path.Combine(PocSceneBuilder.RepositoryRoot, "blender", "scripts", "build_phase3_buildings.py")));
            var boothDef = Definition("soundbooth_exterior");
            Check("soundbooth_exterior: a door like house_main's (HouseTransition, its root cell walkable)",
                boothDef != null && boothDef.Behavior == WorldObjectBehavior.HouseTransition &&
                !boothDef.EffectiveBlockedOffsets().Contains(Vector2Int.zero) && boothDef.EffectiveBlockedOffsets().Count() == 8);
            var offsets = boothDef != null ? boothDef.EffectiveBlockedOffsets().Append(Vector2Int.zero).ToList() : new List<Vector2Int>();
            Check("soundbooth_exterior: square (3 x 3) with the door in the middle of the front row",
                boothDef != null && boothDef.Footprint == new Vector2Int(3, 3) && offsets.Min(o => o.x) == -1 && offsets.Max(o => o.x) == 1 &&
                offsets.Min(o => o.y) == 0 && offsets.Max(o => o.y) == 2 && offsets.Distinct().Count() == 9);
            var dtm = Definition("dtm_desk_large");
            var desk = Definition("desk_basic");
            Check("dtm_desk_large: twice desk_basic's width, the same depth", dtm != null && desk != null &&
                dtm.Footprint.x == desk.Footprint.x * 2 && dtm.Footprint.y == desk.Footprint.y);
            Check("dtm_desk_large: examined from the two middle cells in front, facing up",
                dtm != null && dtm.ActionPoints.Count == 2 && dtm.ActionPoints.All(a => a.PlayerFacing == WorldFacing.Up && a.InteractionText == DtmText));
            var tablet = Definition("illust_desk_tablet");
            Check("illust_desk_tablet: examine text", tablet != null && tablet.ExamineMessage == TabletText);
            foreach (var sprite in new[] { ("soundbooth_exterior", 96, 160), ("dtm_desk_large", 128, 80), ("illust_desk_tablet", 64, 64), ("booth_floor", 32, 32), ("booth_wall", 32, 32) })
            {
                var path = "Assets/Content/World/" + sprite.Item1 + ".png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Check("editor sprite " + sprite.Item1 + ": " + sprite.Item2 + "x" + sprite.Item3 + ", point filter, no mipmaps, uncompressed, 64 PPU",
                    importer != null && texture != null && texture.width == sprite.Item2 && texture.height == sprite.Item3 &&
                    importer.filterMode == FilterMode.Point && !importer.mipmapEnabled && importer.textureCompression == TextureImporterCompression.Uncompressed &&
                    Mathf.Approximately(importer.spritePixelsPerUnit, 64f));
            }
        }

        private static HashSet<Vector2Int> Reachable(SharedWorldModel model, Vector2Int origin, Func<Vector2Int, bool> allowed)
        {
            var seen = new HashSet<Vector2Int> { origin };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(origin);
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                foreach (var d in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                {
                    var next = cell + d;
                    if (seen.Contains(next) || !model.CanEnter(next)) continue;
                    seen.Add(next);
                    if (allowed(next)) queue.Enqueue(next);
                }
            }
            return seen;
        }

        // ---------------------------------------------------------------- models

        private static void ModelChecks()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<PocPresentationCatalog>(PocSceneBuilder.CatalogPath);
            var missing = PocBindingAudit.Missing(catalog, PocSceneBuilder.V31Definitions).ToList();
            Check("every ver3.1 Definition has a Blender model (no generated box, no 2D-only asset)", missing.Count == 0, string.Join(",", missing));
            foreach (var name in PocSceneBuilder.V31ModelNames)
            {
                var path = PocSceneBuilder.V31Models + name + ".fbx";
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var renderers = model != null ? model.GetComponentsInChildren<MeshRenderer>(true) : new MeshRenderer[0];
                Check("model " + name + ": imported, the shared toon material only",
                    model != null && renderers.Length > 0 && renderers.All(r => r.sharedMaterials.All(m => m != null && m.name == "POC_ToonVertexColor")),
                    string.Join(",", renderers.SelectMany(r => r.sharedMaterials).Select(m => m == null ? "null" : m.name).Distinct()));
                var source = Path.Combine(PocSceneBuilder.RepositoryRoot, "blender", "sources", "v31");
                Check("model " + name + ": its .blend source exists", Directory.Exists(source) &&
                    Directory.GetFiles(source, "*.blend").Length == 4);
            }
            var dtm = AssetDatabase.LoadAssetAtPath<GameObject>(PocSceneBuilder.V31Models + "dtm_desk_large.fbx");
            var desk = AssetDatabase.LoadAssetAtPath<GameObject>(PocSceneBuilder.Phase3Models + "desk_basic.fbx");
            if (dtm != null && desk != null)
            {
                var a = Bounds(dtm, "dtm_desk_large");
                var b = Bounds(desk, "desk_basic");
                // The desk top is 0.41 in both Blender scripts; a uniform 2x scale would double the height.
                Check("DTM desk: about twice desk_basic's width, not twice its height (not a uniform 2x scale)",
                    a.size.x > b.size.x * 1.8f && a.size.x < b.size.x * 2.3f && a.size.y < b.size.y * 1.25f && Mathf.Abs(a.size.z - b.size.z) < 0.12f,
                    $"width {a.size.x:0.00} vs {b.size.x:0.00}, height {a.size.y:0.00} vs {b.size.y:0.00}, depth {a.size.z:0.00} vs {b.size.z:0.00}");
                var children = dtm.GetComponentsInChildren<Transform>(true).Select(t => t.name).ToList();
                Check("DTM desk: left and right monitor speakers are separate parts", children.Contains("monitor_speaker_L") && children.Contains("monitor_speaker_R"));
                var left = dtm.GetComponentsInChildren<Transform>(true).First(t => t.name == "monitor_speaker_L");
                var right = dtm.GetComponentsInChildren<Transform>(true).First(t => t.name == "monitor_speaker_R");
                var centre = (left.position + right.position) * 0.5f;
                Check("DTM desk: the speakers stand mirrored left and right of the monitor",
                    Mathf.Abs(left.position.z - right.position.z) < 0.01f && left.position.x < centre.x && right.position.x > centre.x &&
                    Mathf.Abs(Mathf.DeltaAngle(left.eulerAngles.y, -right.eulerAngles.y)) < 1f,
                    $"L {left.position} {left.eulerAngles.y:0}, R {right.position} {right.eulerAngles.y:0}");
            }
            var manifest = Path.Combine(PocSceneBuilder.RepositoryRoot, "blender", "exports", "v31_assets_manifest.json");
            Check("Blender manifest: written, marked as not approved by the user yet", File.Exists(manifest) &&
                File.ReadAllText(manifest).Contains("\"approval\": \"pending"));
            Check("original models were not overwritten (Phase 3 easel, mic and desk FBX unchanged since ver3.0)",
                GitUnchanged("unity-project/Assets/POC2D3D/Models/Phase3"), "git diff b2b799d -- Models/Phase3");
        }

        private static bool GitUnchanged(string path)
        {
            try
            {
                var info = new System.Diagnostics.ProcessStartInfo("git", "diff --quiet b2b799d -- " + path)
                    { WorkingDirectory = PocSceneBuilder.RepositoryRoot, UseShellExecute = false, CreateNoWindow = true };
                using (var process = System.Diagnostics.Process.Start(info))
                {
                    process.WaitForExit(30000);
                    return process.ExitCode == 0;
                }
            }
            catch (Exception) { return false; }
        }

        private static Bounds Bounds(GameObject model, string label)
        {
            var renderers = model.GetComponentsInChildren<MeshFilter>(true);
            var found = false;
            var bounds = new Bounds();
            foreach (var filter in renderers)
            {
                if (filter.sharedMesh == null) continue;
                var local = filter.sharedMesh.bounds;
                var matrix = filter.transform.localToWorldMatrix;
                foreach (var corner in new[] { new Vector3(-1, -1, -1), new Vector3(1, -1, -1), new Vector3(-1, 1, -1), new Vector3(1, 1, -1),
                    new Vector3(-1, -1, 1), new Vector3(1, -1, 1), new Vector3(-1, 1, 1), new Vector3(1, 1, 1) })
                {
                    var p = matrix.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents, corner));
                    if (!found) { bounds = new Bounds(p, Vector3.zero); found = true; }
                    else bounds.Encapsulate(p);
                }
            }
            return bounds;
        }

        // ---------------------------------------------------------------- transitions

        private static void ToHouse(C6.Ctx c, Vector2Int cell, FacingDirection facing)
        {
            if (c.Session.World.MapId != HouseId) c.Session.LoadMapForCheck(HouseId, cell, facing);
            c.Session.PlaceForCheck(cell, facing);
            C6.Settle(c, 0.2f);
        }

        // One step in a world direction that ends on a door: stops on the first frame after the map
        // changed (so the camera check sees the very first frames of the new map).
        private static void StepThrough(C6.Ctx c, Vector2Int world)
        {
            var loads = c.Session.MapLoads;
            C6.Release(c);
            var raw = world;
            if (c.Switcher.Mode == PocViewMode.ThirdPerson3D)
                foreach (var candidate in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                    if (GridSpace3D.CameraRelative(candidate, GridSpace3D.FacingYaw(c.Switcher.CameraHeading)) == world) raw = candidate;
            for (var i = 0; i < 4; i++) C6.Frame(c, 1f / 60f, raw);
            C6.Release(c);
            for (var i = 0; i < 60 && c.Session.MapLoads == loads; i++) C6.Frame(c, 1f / 60f, Vector2Int.zero);
        }

        // The camera after one frame equals the camera after many (snapped, not sliding in).
        private static bool Snapped(C6.Ctx c, out string detail)
        {
            var camera = c.Switcher.Mode == PocViewMode.PixelView2D ? c.Pixel.WorldCamera : c.Third.ViewCamera;
            C6.Frame(c, 1f / 60f, Vector2Int.zero);
            var first = camera.transform.position;
            C6.Settle(c, 1.5f);
            var later = camera.transform.position;
            detail = first + " / " + later;
            return Vector3.Distance(first, later) < 0.06f;
        }

        private static void TransitionChecks()
        {
            C6.SetLegacy("v1|10|10", "1750000000000");
            foreach (var mode in new[] { PocViewMode.PixelView2D, PocViewMode.ThirdPerson3D })
            {
                var c = C6.Start(new HarukaMemoryStorage());
                c.Switcher.SetMode(mode);
                var tag = mode == PocViewMode.PixelView2D ? "2D: " : "3D: ";
                Check(tag + "the session plays three maps (first_field, halka_house, halka_soundbooth)",
                    c.Session.Maps.Select(m => m.MapId).SequenceEqual(new[] { "first_field", HouseId, BoothId }));
                c.Session.OutdoorWorld.TryGetHouseDoor(out var houseDoor);
                C6.ToOutdoor(c, houseDoor + Vector2Int.down);
                var steps = c.Session.CompletedSteps;
                C6.Step(c, Vector2Int.up, 0.4f);
                Check(tag + "first_field -> halka_house by walking in (one step, no extra step)",
                    c.Session.World.MapId == HouseId && c.Session.Cell == new Vector2Int(0, -3) && c.Session.CompletedSteps == steps + 1);
                ToHouse(c, BoothApproach, FacingDirection.Up);
                steps = c.Session.CompletedSteps;
                var loads = c.Session.MapLoads;
                StepThrough(c, Vector2Int.up);
                Check(tag + "halka_house -> halka_soundbooth by walking onto the booth door",
                    c.Session.World.MapId == BoothId && c.Session.Cell == BoothEntry && c.Session.Facing == FacingDirection.Up &&
                    c.Session.LastTransition.Kind == SharedTransitionKind.EnteredBuilding && c.Session.LastTransition.DoorCell == BoothDoor,
                    c.Session.World.MapId + " " + c.Session.Cell);
                Check(tag + "entering the booth: one step counted, one map load", c.Session.CompletedSteps == steps + 1 && c.Session.MapLoads == loads + 1);
                Check(tag + "the booth's interior is built from its own pieces (booth walls, doorway on the exit)",
                    c.Presenter.Interior != null && c.Presenter.Interior.MapId == BoothId && c.Presenter.Interior.HasDoorway &&
                    c.Presenter.Interior.DoorwayCell == BoothExit &&
                    c.Presenter.Views.Where(v => v.IsWall).All(v => v.DefinitionId == "booth_wall"));
                Check(tag + "camera snapped on entering (no slide from the house)", Snapped(c, out var d1), d1);
                steps = c.Session.CompletedSteps;
                StepThrough(c, Vector2Int.down);
                Check(tag + "halka_soundbooth -> halka_house through the exit, in front of the booth facing down",
                    c.Session.World.MapId == HouseId && c.Session.Cell == BoothApproach && c.Session.Facing == FacingDirection.Down &&
                    c.Session.LastTransition.Kind == SharedTransitionKind.ExitedBuilding && c.Session.CompletedSteps == steps + 1,
                    c.Session.World.MapId + " " + c.Session.Cell);
                Check(tag + "camera snapped on leaving the booth", Snapped(c, out var d2), d2);
                Check(tag + "the house's interior pieces again (house walls)", c.Presenter.Interior != null && c.Presenter.Interior.MapId == HouseId &&
                    c.Presenter.Views.Where(v => v.IsWall).All(v => v.DefinitionId == "house_wall"));
                ToHouse(c, new Vector2Int(0, -3), FacingDirection.Down);
                C6.Step(c, Vector2Int.down, 0.4f);
                Check(tag + "halka_house -> first_field still works", c.Session.World.MapId == "first_field" && c.Session.Cell == houseDoor + Vector2Int.down);
                if (mode == PocViewMode.ThirdPerson3D)
                {
                    ToHouse(c, BoothApproach, FacingDirection.Up);
                    StepThrough(c, Vector2Int.up);
                    C6.Settle(c, 0.3f);
                    StepThrough(c, Vector2Int.down);
                    C6.Settle(c, 0.1f);
                    Check("3D: leaving the booth frames the booth (the same exit composition as the house)",
                        c.Third.CompositionsShown > 0, "compositions " + c.Third.CompositionsShown);
                }
            }
        }

        // ---------------------------------------------------------------- AUTO

        private static void AutoChecks()
        {
            C6.SetLegacy("v1|10|10", "1750000000000");
            var c = C6.Start(new HarukaMemoryStorage());
            ToHouse(c, BoothApproach, FacingDirection.Down);
            Check("AUTO in the house never steps onto the booth door or the house exit",
                !c.Auto.CanAutoEnter(BoothDoor) && !c.Auto.CanAutoEnter(new Vector2Int(0, -4)) && c.Auto.CanAutoEnter(new Vector2Int(-2, 0)));
            c.Auto.SetAutoEnabled(true, C6.Clock);
            var loads = c.Session.MapLoads;
            var visited = new HashSet<Vector2Int>();
            for (var t = 0f; t < 420f; t += 0.05f)
            {
                C6.Frame(c, 0.05f, Vector2Int.zero, true);
                visited.Add(c.Session.Cell);
            }
            Check("AUTO in the house for 7 minutes: walks, but never changes the map", c.Session.MapLoads == loads &&
                c.Session.World.MapId == HouseId && visited.Count > 3 && !visited.Contains(BoothDoor), "cells visited " + visited.Count);
            c.Session.LoadMapForCheck(BoothId, BoothEntry, FacingDirection.Up);
            C6.Settle(c, 0.2f);
            Check("AUTO in the booth never steps onto its exit", !c.Auto.CanAutoEnter(BoothExit) && c.Auto.CanAutoEnter(new Vector2Int(1, -4)));
            loads = c.Session.MapLoads;
            visited.Clear();
            for (var t = 0f; t < 420f; t += 0.05f)
            {
                C6.Frame(c, 0.05f, Vector2Int.zero, true);
                visited.Add(c.Session.Cell);
            }
            Check("AUTO in the booth for 7 minutes: walks, never leaves", c.Session.MapLoads == loads && c.Session.World.MapId == BoothId &&
                visited.Count > 3 && !visited.Contains(BoothExit), "cells visited " + visited.Count);
            Check("AUTO stays ON", c.Auto.AutoEnabled);
        }

        // ---------------------------------------------------------------- save

        private static HarukaSaveData OldSave(string map, Vector2Int cell, string facing = "up") => new HarukaSaveData
        {
            GameVersion = "ver3.0", SavedAtUtc = "2026-10-20T03:04:05Z", Revision = 0, TotalSteps = 4321, TotalPlaySeconds = 765,
            TimeAnchorUnixMs = 1760000000000L, AutoEnabled = false, ViewMode = HarukaSaveData.View2D, MapId = map,
            CellX = cell.x, CellY = cell.y, Facing = facing,
        };

        private static void SaveChecks()
        {
            C6.SetLegacy(null, null);
            Check("save schema unchanged (1)", HarukaSaveData.SchemaVersion == 1);
            var storage = new HarukaMemoryStorage();
            var c = C6.Start(storage);
            C6.Settle(c, 1.2f);
            c.Session.LoadMapForCheck(BoothId, new Vector2Int(3, -1), FacingDirection.Left);
            C6.Settle(c, 0.3f);
            c.Save.OnPageHidden("");
            c = C6.Start(storage);
            Check("saved in the booth: resumes in the booth, same cell and facing",
                c.Session.World.MapId == BoothId && c.Session.Cell == new Vector2Int(3, -1) && c.Session.Facing == FacingDirection.Left,
                c.Session.World.MapId + " " + c.Session.Cell + " " + c.Session.Facing);
            Check("resumed in the booth: its interior is shown", c.Presenter.Interior != null && c.Presenter.Interior.MapId == BoothId);

            var exported = c.Save.ExportText();
            var fresh = C6.Start(new HarukaMemoryStorage());
            var check = fresh.Save.CheckImport(exported, out var imported);
            Check("export in the booth -> import: accepted", check == HarukaSaveSystem.ImportCheck.Ok, check.ToString());
            if (check == HarukaSaveSystem.ImportCheck.Ok)
            {
                var applied = fresh.Save.ApplyImport(imported, C6.Clock, out var problem);
                Check("import of a booth save: the player is in the booth", applied && fresh.Session.World.MapId == BoothId &&
                    fresh.Session.Cell == new Vector2Int(3, -1), problem);
            }

            var cases = new[]
            {
                (new Vector2Int(0, 0), true, "a cell still free"),
                (new Vector2Int(3, 0), false, "a cell the booth now takes"),
                (BoothDoor, false, "the booth door (a door is never a resume place)"),
                (new Vector2Int(5, 1), false, "ver3.0's sofa seat, now inside the booth"),
                (new Vector2Int(2, 0), true, "in front of where the PC desk stood (ver3.0's Phase 6 test place)"),
                (new Vector2Int(-3, -1), true, "where the easel stood (free now)"),
            };
            foreach (var (cell, kept, label) in cases)
            {
                var old = new HarukaMemoryStorage();
                new HarukaSnapshotStore(old).Write(OldSave(HouseId, cell));
                var r = C6.Start(old);
                var model = r.Session.World;
                var ok = r.Session.World.MapId == HouseId && (kept ? r.Session.Cell == cell :
                    r.Session.Cell != cell && model.CanEnter(r.Session.Cell) && !r.Session.IsTransitionCell(model, r.Session.Cell));
                Check("ver3.0 save in the house, " + label + ": " + (kept ? "kept" : "moved to the nearest free cell"), ok,
                    r.Session.World.MapId + " " + r.Session.Cell);
                Check("ver3.0 save, " + label + ": steps and play time unchanged, no step added",
                    r.LifeLog.TotalSteps == 4321 && r.Session.CompletedSteps == 0, "steps " + r.LifeLog.TotalSteps);
            }
            var outdoor = new HarukaMemoryStorage();
            new HarukaSnapshotStore(outdoor).Write(OldSave("first_field", new Vector2Int(-1, 0), "right"));
            var o = C6.Start(outdoor);
            Check("ver3.0 save outdoors: unchanged", o.Session.World.MapId == "first_field" && o.Session.Cell == new Vector2Int(-1, 0) &&
                o.Session.Facing == FacingDirection.Right);
            var unknown = new HarukaMemoryStorage();
            new HarukaSnapshotStore(unknown).Write(OldSave("not_a_map", new Vector2Int(0, 0)));
            var u = C6.Start(unknown);
            Check("a save naming an unknown map still starts at the Player Start (as in ver3.0)", u.Session.World.MapId == "first_field");
            Check("place names: 家 / そと / ぼうおんしつ", PlaceName(HouseId) == "家" && PlaceName("first_field") == "そと" && PlaceName(BoothId) == "ぼうおんしつ");
        }

        private static string PlaceName(string mapId)
        {
            var type = typeof(HarukaSaveSystem).GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public)
                .FirstOrDefault(t => t.GetMethod("PlaceName", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public) != null);
            return type?.GetMethod("PlaceName", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)?.Invoke(null, new object[] { mapId }) as string;
        }

        // ---------------------------------------------------------------- the 2D/3D button

        private static Rect MenuButton(GameMenuController menu) =>
            (Rect)typeof(GameMenuController).GetMethod("ButtonRect", Hidden).Invoke(menu, null);

        private static HashSet<Vector2Int> CrowCells(C6.Ctx c)
        {
            var set = new HashSet<Vector2Int>();
            var w = c.Session.OutdoorWorld;
            for (var y = w.MinCell.y; y <= w.MaxCell.y; y++)
            for (var x = w.MinCell.x; x <= w.MaxCell.x; x++)
                if (c.Crow.ReservesGround(new Vector2Int(x, y))) set.Add(new Vector2Int(x, y));
            return set;
        }

        private static void ToggleChecks()
        {
            C6.SetLegacy("v1|10|10", "1750000000000");
            var c = C6.Start(new HarukaMemoryStorage());
            var menu = c.Menu;
            Check("the button runs the menu's own 「ひょうじ」 row (no second view switch)",
                menu.QuickItem != null && menu.ExtraItems.Count == 1 && ReferenceEquals(menu.QuickItem, menu.ExtraItems[0]) &&
                menu.ExtraItems[0].Label == "ひょうじ");
            var safeFull = Screen.safeArea.width >= Screen.width - 0.5f;
            Check("top right, mirroring the menu button (same top margin and height, same side margin)",
                menu.TryGetQuickButtonRect(out var quick) && Mathf.Approximately(quick.y, MenuButton(menu).y) &&
                Mathf.Approximately(quick.height, MenuButton(menu).height) &&
                (!safeFull || Mathf.Abs((Screen.width - quick.xMax) - MenuButton(menu).x) < 0.5f),
                quick + " vs " + MenuButton(menu) + " screen " + Screen.width + "x" + Screen.height);
            var centre = new Vector2(quick.center.x, Screen.height - quick.center.y);
            Check("a click or tap on the button never reaches the world (IsOverControls)", menu.IsOverControls(centre));
            menu.TryGetQuickTouchRect(out var touch);
            var side = Mathf.Min(Screen.width, Screen.height) * GameMenuController.QuickTouchFraction;
            Check("phones: the finger's hit area is at least 11% of the short side around the button (drawn size unchanged), on screen, clear of the menu button",
                touch.Contains(quick.min) && touch.Contains(quick.max - Vector2.one * 0.01f) && touch.width >= side - 0.01f && touch.height >= side - 0.01f &&
                touch.xMax <= Screen.width + 0.01f && touch.yMin >= -0.01f && !touch.Overlaps(MenuButton(menu)) &&
                menu.IsOverControls(new Vector2(touch.xMin + 1f, Screen.height - (touch.yMax - 1f))), touch + " drawn " + quick);

            foreach (var place in new[] { "first_field", HouseId, BoothId })
            {
                if (place == "first_field") C6.ToOutdoor(c, new Vector2Int(-1, 0));
                else if (place == HouseId) ToHouse(c, new Vector2Int(1, 0), FacingDirection.Right);
                else { c.Session.LoadMapForCheck(BoothId, new Vector2Int(2, -1), FacingDirection.Left); C6.Settle(c, 0.3f); }
                c.Switcher.SetMode(PocViewMode.PixelView2D);
                C6.Settle(c, 0.2f);
                c.Auto.SetAutoEnabled(true, C6.Clock);
                var before = (c.Session.World.MapId, c.Session.Cell, c.Session.Facing, c.Session.CompletedSteps, c.LifeLog.TotalSteps,
                    c.Clock.AnchorMilliseconds, c.Auto.AutoEnabled, c.Session.MapLoads);
                var crow = place == "first_field" ? CrowCells(c) : null;
                Check(place + ": in 2D the button says 3Dへ", menu.QuickLabel == "3Dへ", menu.QuickLabel);
                menu.ActivateQuickItem(C6.Clock);
                C6.Settle(c, 0.3f);
                Check(place + ": pressed in 2D -> 3D", c.Switcher.Mode == PocViewMode.ThirdPerson3D && menu.QuickLabel == "2Dへ", menu.QuickLabel);
                var after = (c.Session.World.MapId, c.Session.Cell, c.Session.Facing, c.Session.CompletedSteps, c.LifeLog.TotalSteps,
                    c.Clock.AnchorMilliseconds, c.Auto.AutoEnabled, c.Session.MapLoads);
                Check(place + ": map, cell, facing, steps, time, AUTO unchanged and no map load", before.Equals(after), before + " -> " + after);
                if (crow != null) Check(place + ": the crow keeps its cells", crow.SetEquals(CrowCells(c)));
                Check(place + ": world input is blocked right after the press (the press is not a world click)", menu.BlocksGameplayInput);
                menu.ActivateQuickItem(C6.Clock + 0.5f);
                C6.Settle(c, 0.3f);
                Check(place + ": pressed again -> 2D", c.Switcher.Mode == PocViewMode.PixelView2D && menu.QuickLabel == "3Dへ");
                c.Auto.SetAutoEnabled(false, C6.Clock);
            }

            // Seated: the view changes, she stays seated.
            ToHouse(c, SofaSeat, FacingDirection.Up);
            c.Session.InteractAhead();
            var seated = c.Session.IsSeated;
            menu.ActivateQuickItem(C6.Clock);
            C6.Settle(c, 0.3f);
            Check("seated on the sofa: the button switches the view and she stays seated", seated && c.Session.IsSeated &&
                c.Switcher.Mode == PocViewMode.ThirdPerson3D);
            c.Session.Stand();
            menu.ActivateQuickItem(C6.Clock);
            C6.Settle(c, 0.3f);

            menu.SetOpen(true, C6.Clock);
            Check("hidden while the menu is open (the menu has the same row)", !menu.QuickButtonShown);
            var mode = c.Switcher.Mode;
            menu.ActivateQuickItem(C6.Clock);
            Check("…and cannot be pressed then", c.Switcher.Mode == mode);
            menu.SetOpen(false, C6.Clock);
            menu.enabled = false;
            Check("hidden while the phone rotation guide disables the menu", !menu.QuickButtonShown);
            menu.enabled = true;
            Check("the same switch count as the menu row would give", c.Switcher.SwitchCount >= 7, "switches " + c.Switcher.SwitchCount);
        }

        // ---------------------------------------------------------------- spec 8.4: moving, messages

        private static void ToggleStateChecks()
        {
            C6.SetLegacy("v1|10|10", "1750000000000");
            var c = C6.Start(new HarukaMemoryStorage());
            foreach (var place in new[] { HouseId, BoothId })
            {
                var start = place == HouseId ? new Vector2Int(1, 0) : new Vector2Int(2, -1);
                if (place == HouseId) ToHouse(c, start, FacingDirection.Right);
                else { c.Session.LoadMapForCheck(BoothId, start, FacingDirection.Right); c.Session.PlaceForCheck(start, FacingDirection.Right); C6.Settle(c, 0.3f); }
                c.Switcher.SetMode(PocViewMode.PixelView2D);
                C6.Settle(c, 0.2f);
                var steps = c.Session.CompletedSteps;
                var life = c.LifeLog.TotalSteps;
                C6.Release(c);
                C6.Frame(c, 1f / 60f, Vector2Int.right);
                C6.Frame(c, 1f / 60f, Vector2Int.right);
                var moving = c.Session.IsMoving;
                var target = c.Session.StepToCell;
                c.Menu.ActivateQuickItem(C6.Clock);
                C6.Release(c);
                C6.Settle(c, 0.4f);
                Check(place + ": pressed while walking: the step in progress ends where it was going, counted once",
                    moving && c.Session.Cell == target && target == start + Vector2Int.right && c.Session.CompletedSteps == steps + 1 &&
                    c.LifeLog.TotalSteps == life + 1 && c.Switcher.Mode == PocViewMode.ThirdPerson3D,
                    $"moving {moving}, cell {c.Session.Cell}, target {target}, steps +{c.Session.CompletedSteps - steps}");
                c.Menu.ActivateQuickItem(C6.Clock + 0.5f);
                C6.Settle(c, 0.3f);
            }
            c.Session.LoadMapForCheck(BoothId, TabletFront, FacingDirection.Up);
            c.Session.PlaceForCheck(TabletFront, FacingDirection.Up);
            C6.Settle(c, 0.2f);
            c.Session.InteractAhead();
            var shown = c.Hud.HasActiveMessage;
            var text = c.Session.LastMessage;
            c.Menu.ActivateQuickItem(C6.Clock);
            C6.Settle(c, 0.1f);
            Check("a message on screen (the pen tablet): still shown after the switch, same text",
                shown && c.Hud.HasActiveMessage && c.Session.LastMessage == text && text == TabletText);
            c.Menu.ActivateQuickItem(C6.Clock + 0.5f);
        }

        // ---------------------------------------------------------------- spec 13.4: the mixed route

        private static void MixedRouteChecks()
        {
            C6.SetLegacy("v1|10|10", "1750000000000");
            var c = C6.Start(new HarukaMemoryStorage());
            c.Switcher.SetMode(PocViewMode.PixelView2D);
            c.Session.OutdoorWorld.TryGetHouseDoor(out var houseDoor);
            C6.ToOutdoor(c, houseDoor + Vector2Int.down);
            var route = new List<string>();
            string Here() => c.Session.World.MapId + (c.Switcher.Mode == PocViewMode.PixelView2D ? " 2D" : " 3D");
            route.Add(Here());
            StepThrough(c, Vector2Int.up); C6.Settle(c, 0.2f); route.Add(Here());
            // In the house, walk (no jump) from the entry (0,-3) to the booth: four cells East, two North.
            foreach (var d in new[] { Vector2Int.right, Vector2Int.right, Vector2Int.right, Vector2Int.right, Vector2Int.up, Vector2Int.up })
                C6.Step(c, d, 0.25f);
            var before = c.Session.Cell;
            StepThrough(c, Vector2Int.up); C6.Settle(c, 0.2f); route.Add(Here());
            c.Menu.ActivateQuickItem(C6.Clock); C6.Settle(c, 0.3f); route.Add(Here());
            StepThrough(c, Vector2Int.down); C6.Settle(c, 0.3f); route.Add(Here());
            var houseCell = c.Session.Cell;
            // Back to the house exit in 3D: (4,-1) -> (4,-3) -> (0,-3) -> the exit.
            foreach (var d in new[] { Vector2Int.down, Vector2Int.down, Vector2Int.left, Vector2Int.left, Vector2Int.left, Vector2Int.left })
                C6.Step(c, d, 0.25f);
            StepThrough(c, Vector2Int.down); C6.Settle(c, 0.3f); route.Add(Here());
            var expected = new[] { "first_field 2D", HouseId + " 2D", BoothId + " 2D", BoothId + " 3D", HouseId + " 3D", "first_field 3D" };
            Check("outdoor 2D -> house 2D -> booth 2D -> booth 3D -> house 3D -> outdoor 3D, walking all the way",
                route.SequenceEqual(expected) && before == BoothApproach && houseCell == BoothApproach && c.Session.Cell == houseDoor + Vector2Int.down,
                string.Join(" > ", route) + $" (before booth {before}, back in house {houseCell}, end {c.Session.Cell})");
        }

        // ---------------------------------------------------------------- spec 13.5: manual input takes AUTO's walk back

        private static void AutoTakeoverChecks()
        {
            C6.SetLegacy("v1|10|10", "1750000000000");
            var c = C6.Start(new HarukaMemoryStorage());
            c.Session.LoadMapForCheck(BoothId, new Vector2Int(-3, -1), FacingDirection.Up);
            C6.Settle(c, 0.2f);
            c.Auto.SetAutoEnabled(true, C6.Clock);
            for (var t = 0f; t < 70f && !c.Auto.Active; t += 0.05f) C6.Frame(c, 0.05f, Vector2Int.zero, true);
            Check("booth: AUTO starts after about 60 s without input", c.Auto.Active);
            for (var t = 0f; t < 3f; t += 0.05f) C6.Frame(c, 0.05f, Vector2Int.zero, true);
            var direction = C6.FreeDirection(c);
            // A held key: PocInput.Update reports it to AUTO (RecordUserAction) every frame it is held;
            // the harness feeds the raw direction below Update, so it makes that same call.
            var cellBefore = c.Session.Cell;
            for (var i = 0; i < 20; i++) { c.Auto.RecordUserAction(C6.Clock); C6.Frame(c, 1f / 60f, direction, true); }
            Check("booth: a manual key takes control back (AUTO's walk stops, AUTO stays ON)", !c.Auto.Active && c.Auto.AutoEnabled);
            C6.Release(c);
            C6.Settle(c, 0.4f, true);
            Check("booth: the key walked her (the manual step happened)", c.Session.Cell != cellBefore, $"{cellBefore} -> {c.Session.Cell}");
            Check("booth: still in the booth", c.Session.World.MapId == BoothId);
        }

        // ---------------------------------------------------------------- spec 13.6: old saves

        private static void OldSaveChecks()
        {
            C6.SetLegacy(null, null);
            foreach (var (view, auto) in new[] { (HarukaSaveData.View2D, false), (HarukaSaveData.View3D, false), (HarukaSaveData.View2D, true), (HarukaSaveData.View3D, true) })
            foreach (var (map, cell) in new[] { ("first_field", new Vector2Int(-1, 0)), (HouseId, new Vector2Int(0, 0)) })
            {
                var storage = new HarukaMemoryStorage();
                var data = OldSave(map, cell, "left");
                data.ViewMode = view;
                data.AutoEnabled = auto;
                new HarukaSnapshotStore(storage).Write(data);
                var c = C6.Start(storage);
                Check($"ver3.0 save ({map}, {view}, AUTO {(auto ? "ON" : "OFF")}): the same place, view and AUTO",
                    c.Session.World.MapId == map && c.Session.Cell == cell && c.Session.Facing == FacingDirection.Left &&
                    (c.Switcher.Mode == PocViewMode.ThirdPerson3D) == (view == HarukaSaveData.View3D) && c.Auto.AutoEnabled == auto &&
                    c.LifeLog.TotalSteps == 4321 && c.Clock.AnchorMilliseconds == 1760000000000L,
                    $"{c.Session.World.MapId} {c.Session.Cell} {c.Switcher.Mode} auto {c.Auto.AutoEnabled} steps {c.LifeLog.TotalSteps}");
            }
            var fixture = Path.Combine(PocSceneBuilder.RepositoryRoot, "tools", "poc2d3d", "v31_fixtures", "ver30_export_house_3d_auto.json");
            var text = File.Exists(fixture) ? File.ReadAllText(fixture) : null;
            var game = C6.Start(new HarukaMemoryStorage());
            var check = game.Save.CheckImport(text, out var imported);
            Check("a file exported by the ver3.0 build (release test, house, 3D, AUTO ON): accepted by ver3.1", check == HarukaSaveSystem.ImportCheck.Ok, check.ToString());
            if (check == HarukaSaveSystem.ImportCheck.Ok)
            {
                var applied = game.Save.ApplyImport(imported, C6.Clock, out var problem);
                Check("…imported: house (1, -2) facing right, 3D, AUTO ON, 20 steps",
                    applied && game.Session.World.MapId == HouseId && game.Session.Cell == new Vector2Int(1, -2) &&
                    game.Session.Facing == FacingDirection.Right && game.Switcher.Mode == PocViewMode.ThirdPerson3D && game.Auto.AutoEnabled &&
                    game.LifeLog.TotalSteps == 20, problem);
            }
            // Damaged newest snapshot: the existing recovery loads the previous generation (in the booth).
            var damaged = new HarukaMemoryStorage();
            var store = new HarukaSnapshotStore(damaged);
            var older = OldSave(BoothId, new Vector2Int(3, -1)); older.TotalSteps = 100;
            store.Write(older);
            var newer = OldSave(BoothId, new Vector2Int(-3, -1)); newer.TotalSteps = 101;
            store.Write(newer);
            var newest = store.Scan().Slots.First(slot => slot.Data != null && slot.Data.TotalSteps == 101).Name;
            damaged.Files[newest] = damaged.Files[newest].Substring(0, damaged.Files[newest].Length / 2);
            var r = C6.Start(damaged);
            Check("newest snapshot damaged: the previous one loads (booth, (3, -1), 100 steps); nothing is reset",
                r.Session.World.MapId == BoothId && r.Session.Cell == new Vector2Int(3, -1) && r.LifeLog.TotalSteps == 100,
                $"{r.Session.World.MapId} {r.Session.Cell} steps {r.LifeLog.TotalSteps}");
        }

        // ---------------------------------------------------------------- the sprites Version30Checks pins in its last part

        private static void KeptSpriteChecks()
        {
            foreach (var art in new[] { ("desk", 64, 32), ("house_floor", 32, 32), ("recording_mic", 32, 64), ("art_easel", 64, 64), ("studio_shelf", 64, 32) })
            {
                var path = "Assets/Content/World/" + art.Item1 + ".png";
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Check(art.Item1 + " keeps the approved pixel-art import contract (and its file)",
                    sprite != null && importer != null && sprite.rect.width == art.Item2 && sprite.rect.height == art.Item3 &&
                    importer.filterMode == FilterMode.Point && !importer.mipmapEnabled &&
                    importer.textureCompression == TextureImporterCompression.Uncompressed && Mathf.Approximately(importer.spritePixelsPerUnit, 64f) &&
                    GitUnchanged("unity-project/Assets/Content/World/" + art.Item1 + ".png"));
            }
        }

        // ---------------------------------------------------------------- Phase 5's room checks, for both rooms
        // (Phase 5's room section asserts ver3.0's easel and stops there, so its wall and mic checks
        // that follow are repeated here for the house and the booth with the same expectations).

        private static void RoomViewChecks()
        {
            C6.SetLegacy("v1|10|10", "1750000000000");
            var c = C6.Start(new HarukaMemoryStorage());
            var cases = new (string map, Vector2Int cell, bool crop)[]
            {
                (HouseId, new Vector2Int(2, -3), true), (HouseId, new Vector2Int(-3, -3), true),
                (HouseId, new Vector2Int(0, -3), false), (HouseId, new Vector2Int(2, -2), false),
                (BoothId, new Vector2Int(2, -4), true), (BoothId, new Vector2Int(-3, -4), true),
                (BoothId, new Vector2Int(0, -4), false), (BoothId, new Vector2Int(2, -3), false),
            };
            foreach (var (map, cell, expectCrop) in cases)
            {
                if (c.Session.World.MapId != map) c.Session.LoadMapForCheck(map, cell, FacingDirection.Down);
                c.Session.PlaceForCheck(cell, FacingDirection.Down);
                c.Switcher.SetMode(PocViewMode.PixelView2D);
                C6.Settle(c, 0.1f);
                c.Pixel.UpdateView(0f, 1280, 720);
                c.Pixel.WorldCamera.Render();
                c.Pixel.ComposeScreen(1280, 720);
                var cropped = c.Pixel.LastWallCropPixels;
                Check($"2D {map} {cell}: " + (expectCrop ? "the feet stop at the South wall's top edge" : "nothing hidden (doorway / not the bottom row)"),
                    expectCrop ? cropped > 0 && cropped <= 20 && c.Pixel.LastPlayerCropPixels == cropped : cropped == 0, $"crop {cropped} px");
            }
            // The mic in the booth: only its stand's cell blocks; the cell North of it is reached from three sides.
            var mic = new Vector2Int(4, 1);
            var north = mic + Vector2Int.up;
            var booth = c.Session.FindWorld(BoothId).Map;
            Check("booth mic: only the stand's cell blocks; the cell North of it is free",
                MapPlacementRules.BlocksMovement(booth, mic) && !MapPlacementRules.BlocksMovement(booth, north));
            foreach (var (from, key) in new[] { (north + Vector2Int.left, Vector2Int.right), (north + Vector2Int.up, Vector2Int.down), (north + Vector2Int.right, Vector2Int.left) })
            {
                if (c.Session.World.MapId != BoothId) c.Session.LoadMapForCheck(BoothId, from, FacingDirection.Up);
                c.Session.PlaceForCheck(from, FacingDirection.Up);
                c.Switcher.SetMode(PocViewMode.PixelView2D);
                C6.Step(c, key, 0.4f);
                Check($"booth mic: from {from} she walks onto {north} above the mic", c.Session.Cell == north, c.Session.Cell.ToString());
            }
            c.Session.PlaceForCheck(north, FacingDirection.Down);
            c.Switcher.SetMode(PocViewMode.ThirdPerson3D);
            C6.Settle(c, 0.6f);
            var view = c.Presenter.Views.First(v => v.DefinitionId == "recording_mic_basic");
            Check("booth mic: standing above it in 3D it is not made see-through, never hidden as an occluder",
                !view.CameraOccluder && c.Third.TreeFade.AlphaOf(view) > 0.99f && !c.Third.TreeFade.IsSwapped(view) &&
                view.Renderers.All(r => r.enabled && r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly));
            foreach (var id in new[] { "dtm_desk_large", "soundbooth_exterior", "illust_desk_tablet" })
            {
                var map = id == "soundbooth_exterior" ? HouseId : BoothId;
                if (c.Session.World.MapId != map) c.Session.LoadMapForCheck(map, map == BoothId ? BoothEntry : new Vector2Int(0, -3), FacingDirection.Up);
                C6.Settle(c, 0.2f);
                var v = c.Presenter.Views.FirstOrDefault(x => x.DefinitionId == id);
                Check(id + ": drawn in 3D with its Blender model, no see-through treatment",
                    v != null && !v.CameraOccluder && !v.FadeInFront3D && v.Renderers.Length > 0 && v.Renderers.All(r => r != null && r.enabled));
            }
        }

        // ---------------------------------------------------------------- interaction

        // The message the game shows for A from this cell and facing (null: nothing happens).
        private static string Use(C6.Ctx c, Vector2Int cell, FacingDirection facing)
        {
            c.Session.PlaceForCheck(cell, facing);
            string shown = null;
            void Seen(string message) => shown = message;
            c.Session.MessageShown += Seen;
            try { c.Session.InteractAhead(); }
            finally { c.Session.MessageShown -= Seen; }
            return shown;
        }

        private static void InteractionChecks()
        {
            C6.SetLegacy("v1|10|10", "1750000000000");
            var c = C6.Start(new HarukaMemoryStorage());
            ToHouse(c, new Vector2Int(0, -1), FacingDirection.Up);
            Check("house: where the PC desk was, nothing answers now", Use(c, new Vector2Int(2, 0), FacingDirection.Up) == null);
            Check("bed: still answers from (-5, -1)", Use(c, new Vector2Int(-5, -1), FacingDirection.Up) == "ベッド。");
            foreach (var seat in new[] { SofaSeat, SofaSeat + Vector2Int.right })
            {
                c.Session.PlaceForCheck(seat, FacingDirection.Up);
                Check($"sofa (moved): the seat {seat} still seats her", c.Session.InteractAhead() && c.Session.IsSeated);
                c.Session.Stand();
            }
            Check("the booth itself answers nothing (it is a door)", Use(c, new Vector2Int(3, -1), FacingDirection.Up) == null);
            c.Session.LoadMapForCheck(BoothId, BoothEntry, FacingDirection.Up);
            C6.Settle(c, 0.2f);
            Check("pen-tablet desk (booth, left of the DTM desk): examined from the front", Use(c, TabletFront, FacingDirection.Up) == TabletText);
            Check("pen-tablet desk: nothing from the side", Use(c, TabletDesk + Vector2Int.left, FacingDirection.Right) == null);
            Check("DTM desk: examined from the left middle cell", Use(c, new Vector2Int(-1, 2), FacingDirection.Up) == DtmText);
            Check("DTM desk: examined from the right middle cell", Use(c, new Vector2Int(0, 2), FacingDirection.Up) == DtmText);
            Check("DTM desk: blocks all four cells", new[] { -2, -1, 0, 1 }.All(x => !c.Session.World.CanEnter(new Vector2Int(x, 3))));
            Check("mic stand in the booth: blocks its cell, as in the house before", !c.Session.World.CanEnter(new Vector2Int(4, 1)));
        }

        // ---------------------------------------------------------------- night

        private static void NightChecks()
        {
            Check("2D night overlay: booth 0.08, lighter than the house (0.16) and outdoors (0.33)",
                Mathf.Approximately(WorldNightOverlay2D.TargetAlpha(WorldPhase.Night, BoothId), 0.08f) &&
                Mathf.Approximately(WorldNightOverlay2D.TargetAlpha(WorldPhase.Night, HouseId), 0.16f) &&
                Mathf.Approximately(WorldNightOverlay2D.TargetAlpha(WorldPhase.Night, "first_field"), 0.33f) &&
                WorldNightOverlay2D.TargetAlpha(WorldPhase.Morning, BoothId) == 0f);
            C6.SetLegacy("v1|10|10", "1750000000000");
            var c = C6.Start(new HarukaMemoryStorage());
            var sun = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(l => l.type == LightType.Directional);
            float Light(string map, PocViewMode mode)
            {
                if (map == "first_field") C6.ToOutdoor(c, new Vector2Int(-1, 0));
                else c.Session.LoadMapForCheck(map, map == BoothId ? BoothEntry : new Vector2Int(0, -3), FacingDirection.Up);
                c.Switcher.SetMode(mode);
                c.Time.Override = PocPhaseOverride.Night;
                c.Time.Apply(1000f);
                c.Time.Apply(1010f);
                return mode == PocViewMode.PixelView2D ? c.Time.PixelOverlayAlpha : sun.intensity + RenderSettings.ambientLight.grayscale;
            }
            var boothLight = Light(BoothId, PocViewMode.ThirdPerson3D);
            var backdrop = c.Third.ViewCamera.backgroundColor;
            Check("3D night in the booth: the background is the booth's backdrop (never the night sky)",
                backdrop == c.Session.World.Map.BackdropColor && c.Session.World.MapId == BoothId, backdrop.ToString());
            var houseLight = Light(HouseId, PocViewMode.ThirdPerson3D);
            var outdoorLight = Light("first_field", PocViewMode.ThirdPerson3D);
            Check("3D night: the booth keeps more light than the house and outdoors (its lamps)", boothLight > houseLight && houseLight > outdoorLight,
                $"booth {boothLight:0.000}, house {houseLight:0.000}, outdoors {outdoorLight:0.000}");
            var boothOverlay = Light(BoothId, PocViewMode.PixelView2D);
            Check("2D night in the booth: the overlay is 0.08", Mathf.Abs(boothOverlay - 0.08f) < 0.001f, boothOverlay.ToString("0.000"));
            c.Time.Override = PocPhaseOverride.Morning;
        }

        // ---------------------------------------------------------------- performance

        private static void PerformanceChecks()
        {
            C6.SetLegacy("v1|10|10", "1750000000000");
            var c = C6.Start(new HarukaMemoryStorage());
            var counts = new Dictionary<string, int>();
            foreach (var map in new[] { HouseId, BoothId })
            {
                c.Session.LoadMapForCheck(map, map == BoothId ? BoothEntry : new Vector2Int(0, -3), FacingDirection.Up);
                C6.Settle(c, 0.1f);
                var objects = c.Presenter.Views.Where(v => !v.IsWall).Sum(v => v.Renderers.Where(r => r != null)
                    .Select(r => r.GetComponent<MeshFilter>()).Where(f => f != null && f.sharedMesh != null).Sum(f => f.sharedMesh.triangles.Length / 3));
                counts[map] = c.Presenter.BuiltTriangles + objects;
            }
            C6.ToOutdoor(c, new Vector2Int(-1, 0));
            var outdoor = c.Presenter.BuiltTriangles;
            Check("triangles: the booth (room + furniture) stays under the outdoor ground's budget",
                counts[BoothId] < Math.Max(outdoor, 20000), $"booth {counts[BoothId]}, house {counts[HouseId]}, outdoor ground {outdoor}");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (var i = 0; i < 10; i++)
            {
                c.Session.LoadMapForCheck(i % 2 == 0 ? BoothId : HouseId, i % 2 == 0 ? BoothEntry : new Vector2Int(0, -3), FacingDirection.Up);
                C6.Frame(c, 1f / 60f, Vector2Int.zero);
            }
            Check("map change house <-> booth: rebuild is fast in the editor (< 150 ms each)", sw.ElapsedMilliseconds / 10f < 150f,
                (sw.ElapsedMilliseconds / 10f).ToString("0.0") + " ms (editor, not WebGL)");
            // Managed allocations per frame while standing still (editor Mono, not the WebGL build's IL2CPP:
            // a comparison between places, not the shipped numbers). Heap growth after a full collection
            // (GetAllocatedBytesForCurrentThread reads 0 in Unity's Mono), so the numbers are approximate.
            long Heap() { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); return GC.GetTotalMemory(false); }
            // Per-frame allocations are not measured: the heap grows in blocks, so a few hundred bytes a
            // frame do not show (it read 0 B/frame everywhere). Reported as NOT RUN, not as a pass.
            lines.Add("INFO [performance] per-frame GC Alloc: NOT RUN (no reliable method in the batch editor; WebGL release builds have no profiler)");
            var before = Heap();
            c.Menu.ActivateQuickItem(C6.Clock);
            C6.Frame(c, 1f / 60f, Vector2Int.zero);
            var toggleBytes = GC.GetTotalMemory(false) - before;
            before = Heap();
            c.Session.LoadMapForCheck(HouseId, new Vector2Int(0, -3), FacingDirection.Up);
            C6.Frame(c, 1f / 60f, Vector2Int.zero);
            var mapBytes = GC.GetTotalMemory(false) - before;
            Check("GC: one view switch and one map change, measured (editor Mono, approximate: heap growth after a full collection)", mapBytes > 0,
                $"view switch {toggleBytes / 1024f:0.0} KB, booth -> house rebuild {mapBytes / 1024f:0.0} KB");
        }

        // ---------------------------------------------------------------- font

        private static void FontChecks()
        {
            foreach (var text in new[] { "3Dへ", "2Dへ", DtmText, TabletText, "ぼうおんしつ", "ver3.1" })
                Check("bitmap font has every character of 「" + text + "」", GameBitmapFont.HasAllGlyphs(text),
                    new string(text.Where(ch => !GameBitmapFont.HasAllGlyphs(ch.ToString())).ToArray()));
        }
    }
}
