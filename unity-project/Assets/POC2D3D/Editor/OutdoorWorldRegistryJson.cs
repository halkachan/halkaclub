using System;
using System.Collections.Generic;
using Halka.Game.World;
using Halka.Poc2D3D.World;
using UnityEngine;

namespace Halka.Poc2D3D.EditorTools
{
    // ver3.2: reads Assets/Content/Maps/World/*.hwworld.json (the outdoor world registry).
    public static class OutdoorWorldRegistryJson
    {
        // Reads a registry and resolves its Maps by id. The problems list gets the file's own errors
        // (format, version, unknown Map) and then the world's (OutdoorWorldDefinition.Validate);
        // the result is null when any error was found.
        public static OutdoorWorldDefinition Parse(string json, Func<string, MapDefinition> resolveMap, List<string> problems) =>
            Parse(json, resolveMap, problems, null);

        // ver3.3: themes (direction_themes.json) are attached before the world is validated.
        public static OutdoorWorldDefinition Parse(string json, Func<string, MapDefinition> resolveMap, List<string> problems,
            List<DirectionTheme> themes)
        {
            OutdoorWorldRegistryDto dto;
            try { dto = JsonUtility.FromJson<OutdoorWorldRegistryDto>(json); }
            catch (ArgumentException exception) { problems.Add("ERROR: registry is not JSON: " + exception.Message); return null; }
            if (dto == null) { problems.Add("ERROR: empty registry"); return null; }
            if (dto.format != OutdoorWorldDefinition.Format) problems.Add("ERROR: registry format '" + dto.format + "' is not " + OutdoorWorldDefinition.Format);
            if (dto.formatVersion != OutdoorWorldDefinition.FormatVersion)
                problems.Add("ERROR: registry format version " + dto.formatVersion + " is not supported (" + OutdoorWorldDefinition.FormatVersion + ")");
            var definition = new OutdoorWorldDefinition { worldId = dto.worldId, formatVersion = dto.formatVersion, chunkSize = dto.chunkSize };
            if (themes != null) definition.themes = themes;
            foreach (var region in dto.regions ?? Array.Empty<OutdoorWorldRegistryDto.RegionDto>())
            {
                if (region == null || string.IsNullOrEmpty(region.mapId)) { problems.Add("ERROR: region without a mapId"); continue; }
                var map = resolveMap(region.mapId);
                if (map == null) { problems.Add("ERROR: unknown Map " + region.mapId); continue; }
                var offset = region.offset != null ? new Vector2Int(region.offset.x, region.offset.y) : Vector2Int.zero;
                definition.regions.Add(new OutdoorRegionLink { mapId = region.mapId, map = map, offset = offset,
                    direction = string.IsNullOrEmpty(region.direction) ? OutdoorWorldDefinition.Center : region.direction });
            }
            problems.AddRange(definition.Validate());
            return OutdoorWorldDefinition.HasErrors(problems) ? null : definition;
        }

        // ver3.3: the direction colours. Every direction once, colours as #rrggbb.
        public static List<DirectionTheme> ParseThemes(string json, List<string> problems)
        {
            DirectionThemesDto dto;
            try { dto = JsonUtility.FromJson<DirectionThemesDto>(json); }
            catch (ArgumentException exception) { problems.Add("ERROR: themes are not JSON: " + exception.Message); return null; }
            if (dto == null || dto.format != DirectionThemesDto.Format || dto.formatVersion != DirectionThemesDto.FormatVersion)
            { problems.Add("ERROR: themes: format " + dto?.format + " v" + dto?.formatVersion + " is not supported"); return null; }
            var list = new List<DirectionTheme>();
            foreach (var t in dto.themes ?? Array.Empty<DirectionThemesDto.ThemeDto>())
            {
                if (t == null || Array.IndexOf(OutdoorWorldDefinition.Directions, t.direction) < 0) { problems.Add("ERROR: themes: unknown direction " + t?.direction); continue; }
                if (list.Exists(x => x.direction == t.direction)) { problems.Add("ERROR: themes: " + t.direction + " twice"); continue; }
                if (!ColorUtility.TryParseHtmlString(t.color, out var color) || !ColorUtility.TryParseHtmlString(t.shade, out var shade))
                { problems.Add("ERROR: themes: bad colour for " + t.direction); continue; }
                if (string.IsNullOrWhiteSpace(t.name)) { problems.Add("ERROR: themes: no name for " + t.direction); continue; }
                list.Add(new DirectionTheme { direction = t.direction, name = t.name, color = color, shade = shade });
            }
            foreach (var d in OutdoorWorldDefinition.Directions)
                if (!list.Exists(x => x.direction == d)) problems.Add("ERROR: themes: no theme for " + d);
            return OutdoorWorldDefinition.HasErrors(problems) ? null : list;
        }
    }
}
