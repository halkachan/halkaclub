using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Halka.Game.Player;
using UnityEngine;

namespace Halka.Poc2D3D.World
{
    // ver3.5: one place 「そらをとぶ」 can fly to. The definition is the game's (positions, names);
    // what the player has found is the save's (only the stable id). Cells are Map-local cells of the
    // region's Map, so moving a region in the world registry never moves a point off its place.
    [Serializable]
    public sealed class FastTravelPoint
    {
        public string id;
        public string name;                 // shown in the destination list (bitmap font glyphs only)
        public string worldId = OutdoorWorldDefinition.DefaultWorldId;
        public string mapId;                // the outdoor region (Map) the point lies in
        public Vector2Int discoverCell;     // walking within discoverRadius of it finds the point
        public int discoverRadius = 2;      // Manhattan distance
        public Vector2Int arrivalCell;      // where she lands (the nearest safe cell around it)
        public string arrivalFacing = "down";
        public int landingSearchRadius = 4; // how far from arrivalCell a safe cell is looked for
        public bool unlockedAtStart;
        public int order;

        public FacingDirection ArrivalFacing =>
            Save.HarukaSaveData.TryParseFacing(arrivalFacing, out var facing) ? facing : FacingDirection.Down;
    }

    // ver3.5: every fast travel point of the game (Assets/Content/FastTravel/fast_travel_points.json,
    // "format": "harukaworld-fast-travel", "formatVersion": 1, read by the scene builder). A new place
    // (a north, south or west region later) is one more entry here; nothing in the code names a place.
    [Serializable]
    public sealed class FastTravelRegistry
    {
        public const string Format = "harukaworld-fast-travel";
        public const int FormatVersion = 1;
        public const int MaxPoints = 64;
        public const int MaxLandingSearchRadius = 8;
        public const int MaxDiscoverRadius = 4;
        public static readonly Regex IdPattern = new Regex("^[a-z0-9_-]{1,64}$");

        public List<FastTravelPoint> points = new List<FastTravelPoint>();

        public static bool ValidId(string id) => !string.IsNullOrEmpty(id) && IdPattern.IsMatch(id);

        public FastTravelPoint Find(string id)
        {
            if (points != null)
                foreach (var point in points)
                    if (point != null && point.id == id) return point;
            return null;
        }

        // The points in the order the list shows them (order, then the registry's own order).
        public List<FastTravelPoint> Ordered()
        {
            var list = new List<FastTravelPoint>();
            if (points != null)
                foreach (var point in points)
                    if (point != null) list.Add(point);
            var position = new Dictionary<FastTravelPoint, int>();
            for (var i = 0; i < list.Count; i++) position[list[i]] = i;
            list.Sort((a, b) => a.order != b.order ? a.order.CompareTo(b.order) : position[a].CompareTo(position[b]));
            return list;
        }

        // Everything a point must satisfy against the outdoor world (the logical world: Collision and
        // doors, no chunk drawn). Errors stop the build; a blocked arrival cell is only a warning (the
        // game lands on the nearest safe cell around it).
        public List<string> Validate(OutdoorWorldIndex world, Func<Vector2Int, bool> isTransitionCell)
        {
            var problems = new List<string>();
            if (points == null || points.Count == 0) { problems.Add("ERROR: no fast travel point"); return problems; }
            if (points.Count > MaxPoints) problems.Add("ERROR: more than " + MaxPoints + " points");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var start = 0;
            foreach (var point in points)
            {
                if (point == null) { problems.Add("ERROR: empty point"); continue; }
                var label = point.id ?? "(no id)";
                if (!ValidId(point.id)) problems.Add("ERROR: invalid point id '" + point.id + "'");
                else if (!ids.Add(point.id)) problems.Add("ERROR: point " + point.id + " twice");
                if (string.IsNullOrWhiteSpace(point.name)) problems.Add("ERROR: " + label + " has no name");
                if (point.unlockedAtStart) start++;
                if (!Save.HarukaSaveData.TryParseFacing(point.arrivalFacing, out _))
                    problems.Add("ERROR: " + label + " has an unknown arrival facing '" + point.arrivalFacing + "'");
                if (point.discoverRadius < 0 || point.discoverRadius > MaxDiscoverRadius)
                    problems.Add("ERROR: " + label + " discover radius " + point.discoverRadius + " outside 0.." + MaxDiscoverRadius);
                if (point.landingSearchRadius < 0 || point.landingSearchRadius > MaxLandingSearchRadius)
                    problems.Add("ERROR: " + label + " landing search radius " + point.landingSearchRadius + " outside 0.." + MaxLandingSearchRadius);
                if (world == null) continue;
                if (point.worldId != world.WorldId) { problems.Add("ERROR: " + label + " is in the unknown world '" + point.worldId + "'"); continue; }
                var region = world.FindRegion(point.mapId);
                if (region == null) { problems.Add("ERROR: " + label + " is in the unknown region '" + point.mapId + "'"); continue; }
                var discover = region.ToWorld(point.discoverCell);
                var arrival = region.ToWorld(point.arrivalCell);
                if (!region.ContainsWorld(discover)) problems.Add("ERROR: " + label + " discover cell " + point.discoverCell + " outside " + point.mapId);
                if (!region.ContainsWorld(arrival)) { problems.Add("ERROR: " + label + " arrival cell " + point.arrivalCell + " outside " + point.mapId); continue; }
                if (!world.CanEnter(arrival) || isTransitionCell != null && isTransitionCell(arrival))
                    problems.Add("WARNING: " + label + " arrival cell " + point.arrivalCell + " is not a standing cell (the nearest safe cell is used)");
            }
            if (start == 0) problems.Add("ERROR: no point is unlocked at the start (the house)");
            return problems;
        }

        public static bool HasErrors(List<string> problems)
        {
            foreach (var problem in problems)
                if (problem.StartsWith("ERROR", StringComparison.Ordinal)) return true;
            return false;
        }
    }

    // fast_travel_points.json's shape (JsonUtility), read in the Editor.
    [Serializable]
    public sealed class FastTravelRegistryDto
    {
        public string format;
        public int formatVersion;
        public PointDto[] points;

        [Serializable]
        public sealed class PointDto
        {
            public string id;
            public string name;
            public string worldId;
            public string mapId;
            public OutdoorWorldRegistryDto.CellDto discoverCell;
            public int discoverRadius = 2;
            public OutdoorWorldRegistryDto.CellDto arrivalCell;
            public string arrivalFacing;
            public int landingSearchRadius = 4;
            public bool unlockedAtStart;
            public int order;
        }
    }
}
