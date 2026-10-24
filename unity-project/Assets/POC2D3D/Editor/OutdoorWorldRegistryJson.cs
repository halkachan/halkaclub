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
        public static OutdoorWorldDefinition Parse(string json, Func<string, MapDefinition> resolveMap, List<string> problems)
        {
            OutdoorWorldRegistryDto dto;
            try { dto = JsonUtility.FromJson<OutdoorWorldRegistryDto>(json); }
            catch (ArgumentException exception) { problems.Add("ERROR: registry is not JSON: " + exception.Message); return null; }
            if (dto == null) { problems.Add("ERROR: empty registry"); return null; }
            if (dto.format != OutdoorWorldDefinition.Format) problems.Add("ERROR: registry format '" + dto.format + "' is not " + OutdoorWorldDefinition.Format);
            if (dto.formatVersion != OutdoorWorldDefinition.FormatVersion)
                problems.Add("ERROR: registry format version " + dto.formatVersion + " is not supported (" + OutdoorWorldDefinition.FormatVersion + ")");
            var definition = new OutdoorWorldDefinition { worldId = dto.worldId, formatVersion = dto.formatVersion, chunkSize = dto.chunkSize };
            foreach (var region in dto.regions ?? Array.Empty<OutdoorWorldRegistryDto.RegionDto>())
            {
                if (region == null || string.IsNullOrEmpty(region.mapId)) { problems.Add("ERROR: region without a mapId"); continue; }
                var map = resolveMap(region.mapId);
                if (map == null) { problems.Add("ERROR: unknown Map " + region.mapId); continue; }
                var offset = region.offset != null ? new Vector2Int(region.offset.x, region.offset.y) : Vector2Int.zero;
                definition.regions.Add(new OutdoorRegionLink { mapId = region.mapId, map = map, offset = offset });
            }
            problems.AddRange(definition.Validate());
            return OutdoorWorldDefinition.HasErrors(problems) ? null : definition;
        }
    }
}
