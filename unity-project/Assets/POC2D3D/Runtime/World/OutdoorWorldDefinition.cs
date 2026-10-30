using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Halka.Game.World;
using UnityEngine;

namespace Halka.Poc2D3D.World
{
    // ver3.2: one Map placed in the outdoor world. World cell = Map-local cell + offset.
    [Serializable]
    public sealed class OutdoorRegionLink
    {
        public string mapId;
        public MapDefinition map;
        public Vector2Int offset;
        // ver3.3: which direction of the world the region belongs to (center, north, east, south,
        // west): its accents use that direction's theme colour (North blue, East yellow, South red,
        // West green). The direction is the region's; the ground keeps the field's own colours.
        // ver3.9: or one of the four diagonals between them (northwest ...), for the transition regions.
        public string direction = OutdoorWorldDefinition.Center;
    }

    // ver3.3: one direction's colour (Assets/Content/Maps/World/direction_themes.json).
    [Serializable]
    public sealed class DirectionTheme
    {
        public string direction;
        public string name;     // the place name the game shows (ひがし ...)
        public Color color;
        public Color shade;
    }

    // ver3.2: the outdoor world (game side only; WORLD EDITOR v1.0 and the Map JSON are untouched).
    // Its source is the versioned registry Assets/Content/Maps/World/outdoor_main.hwworld.json
    // ("format": "halka-world-outdoor", "formatVersion": 1), read by the scene builder.
    // Interiors (halka_house, halka_soundbooth) are never part of it: they keep Map ID + Map cell.
    [Serializable]
    public sealed class OutdoorWorldDefinition
    {
        public const string Format = "halka-world-outdoor";
        public const int FormatVersion = 1;
        public const string DefaultWorldId = "outdoor_main";
        public const int DefaultChunkSize = 16;

        public string worldId = DefaultWorldId;
        public int formatVersion = FormatVersion;
        public int chunkSize = DefaultChunkSize;
        public List<OutdoorRegionLink> regions = new List<OutdoorRegionLink>();
        public List<DirectionTheme> themes = new List<DirectionTheme>();

        public const string Center = "center";
        // ver3.9: the diagonals, for the transition regions between two directions (their theme's name is the
        // place name the save menu shows: きたにし ..., never one of the two neighbours' names).
        public static readonly string[] Directions = { Center, "north", "east", "south", "west", "northwest", "northeast", "southwest", "southeast" };

        public DirectionTheme ThemeOf(string direction)
        {
            if (themes != null)
                foreach (var theme in themes)
                    if (theme != null && theme.direction == direction) return theme;
            return null;
        }

        // The ver3.1 world as ver3.2 sees it: the one Map at offset (0,0), so every world cell equals
        // its first_field cell. Used when no registry is configured (old tests, fixtures).
        public static OutdoorWorldDefinition Single(MapDefinition map, int chunkSize = DefaultChunkSize) => new OutdoorWorldDefinition
        {
            chunkSize = chunkSize,
            regions = new List<OutdoorRegionLink>
            {
                new OutdoorRegionLink { mapId = map != null ? map.MapId : null, map = map, offset = Vector2Int.zero }
            }
        };

        public static CellRect WorldRectOf(OutdoorRegionLink link) =>
            new CellRect(link.map.MinCell + link.offset, link.map.MaxCell + link.offset);

        // Everything a world must satisfy before the game uses it. Errors stop it; nothing is fixed.
        public List<string> Validate()
        {
            var problems = new List<string>();
            if (string.IsNullOrWhiteSpace(worldId) || !Regex.IsMatch(worldId, "^[a-z0-9_-]{1,64}$"))
                problems.Add("ERROR: invalid world id '" + worldId + "'");
            if (formatVersion != FormatVersion) problems.Add("ERROR: unsupported world format version " + formatVersion);
            if (chunkSize < WorldGrid.MinChunkSize || chunkSize > WorldGrid.MaxChunkSize)
                problems.Add("ERROR: chunk size " + chunkSize + " outside " + WorldGrid.MinChunkSize + ".." + WorldGrid.MaxChunkSize);
            if (regions == null || regions.Count == 0) { problems.Add("ERROR: the world has no region"); return problems; }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var rects = new List<(string id, CellRect rect)>();
            foreach (var link in regions)
            {
                if (link == null || link.map == null) { problems.Add("ERROR: region without a Map (" + link?.mapId + ")"); continue; }
                if (link.mapId != link.map.MapId) problems.Add("ERROR: region id " + link.mapId + " is not its Map's id " + link.map.MapId);
                if (!ids.Add(link.map.MapId)) problems.Add("ERROR: Map " + link.map.MapId + " placed twice");
                if (link.map.MapType != "outdoor") problems.Add("ERROR: " + link.map.MapId + " is not an outdoor Map (" + link.map.MapType + ")");
                if (Array.IndexOf(Directions, string.IsNullOrEmpty(link.direction) ? Center : link.direction) < 0)
                    problems.Add("ERROR: " + link.map.MapId + " has an unknown direction '" + link.direction + "'");
                else if (themes != null && themes.Count > 0 && ThemeOf(string.IsNullOrEmpty(link.direction) ? Center : link.direction) == null)
                    problems.Add("ERROR: no theme for the direction '" + link.direction + "' of " + link.map.MapId);
                if (!WorldGrid.IsValidCell(link.offset)) { problems.Add("ERROR: offset out of range for " + link.map.MapId); continue; }
                CellRect rect;
                try { rect = new CellRect(WorldGrid.CheckedAdd(link.map.MinCell, link.offset), WorldGrid.CheckedAdd(link.map.MaxCell, link.offset)); }
                catch (OverflowException) { problems.Add("ERROR: offset overflows for " + link.map.MapId); continue; }
                if (!WorldGrid.IsValidCell(rect.Min) || !WorldGrid.IsValidCell(rect.Max))
                    problems.Add("ERROR: " + link.map.MapId + " lies outside +-" + WorldGrid.MaxAbsCell + " cells");
                else if (!WorldGrid.IsRenderSafe(rect.Min) || !WorldGrid.IsRenderSafe(rect.Max))
                    problems.Add("WARNING: " + link.map.MapId + " lies beyond +-" + WorldGrid.RenderSafeAbsCell + " cells (float precision)");
                foreach (var other in rects)
                    if (other.rect.Overlaps(rect)) problems.Add("ERROR: " + link.map.MapId + " overlaps " + other.id);
                rects.Add((link.map.MapId, rect));
            }
            return problems;
        }

        public static bool HasErrors(List<string> problems)
        {
            foreach (var problem in problems)
                if (problem.StartsWith("ERROR", StringComparison.Ordinal)) return true;
            return false;
        }
    }

    // The registry file's shape (JsonUtility), read in the Editor by OutdoorWorldRegistryJson
    // (the scene builder and the checks); Maps are resolved by id.
    [Serializable]
    public sealed class OutdoorWorldRegistryDto
    {
        public string format;
        public int formatVersion;
        public string worldId;
        public int chunkSize;
        public RegionDto[] regions;

        [Serializable]
        public sealed class RegionDto
        {
            public string mapId;
            public CellDto offset;
            public string direction;   // ver3.3 (optional: center)
        }

        [Serializable]
        public sealed class CellDto
        {
            public int x;
            public int y;
        }
    }

    // ver3.3: direction_themes.json's shape.
    [Serializable]
    public sealed class DirectionThemesDto
    {
        public const string Format = "halka-direction-themes";
        public const int FormatVersion = 1;
        public string format;
        public int formatVersion;
        public ThemeDto[] themes;

        [Serializable]
        public sealed class ThemeDto
        {
            public string direction;
            public string name;
            public string color;
            public string shade;
        }

    }
}
