using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Halka.Game.Player;

namespace Halka.Poc2D3D.Save
{
    public enum HarukaSaveReadStatus
    {
        Valid,
        Missing,
        Corrupt,       // not JSON, a field missing or out of range, or the checksum differs
        NewerSchema,   // written by a newer game: never loaded, never overwritten
    }

    // はるかわーるど save data (Phase 6). Schema 1 holds the Life Log, the morning/night anchor,
    // the settings (AUTO, 2D/3D) and the player's logical place (map ID, Grid cell, facing).
    // Anything temporary (a step in progress, the crow's flight, the camera, messages, the menu,
    // a seated pose) is never saved. "extensions" is reserved for later data (items, furniture,
    // ...): a later schema adds fields and a migration step reads every older schema.
    // The checksum (CRC-32 over the values) only detects accidental damage; it is not a signature.
    // ver3.4: extensions.crowCompanion holds the crow's companion mode ("free" / "follow") with its
    // own checksum (over the mode and the main checksum, so it belongs to this file only). Still
    // Schema 1: the main values and checksum are unchanged, so ver3.0-3.3 read a ver3.4 file as
    // before (they ignore the extension; the crow is then simply free). A file without it (every
    // older save, and a ver3.4 save with a free crow) means "free"; a damaged entry makes the file
    // Corrupt.
    // ver3.5: extensions.fastTravel holds the ids of the places 「そらをとぶ」 can go to that she has
    // found ({ "discovered": [ids], "checksum" }, the checksum over the ids as written and the main
    // checksum). Written only when she has found more than the start places, so a ver3.4-like game
    // still writes exactly a ver3.4 file. Ids are kept as they are (an id this version does not know
    // belongs to a later version and is written back unchanged); a duplicate is read once. A wrong
    // type, a malformed id, more than 64 ids or a wrong checksum makes the file Corrupt (the snapshot
    // store then falls back to an older good snapshot). Both extensions can be present together.
    public sealed class HarukaSaveData
    {
        public const string Format = "harukaworld-save";
        public const int SchemaVersion = 1;
        public const int MaxFileBytes = 64 * 1024;
        public const string View2D = "2D";
        public const string View3D = "3D";
        public const string CrowFree = "free";
        public const string CrowFollow = "follow";
        public const string CrowExtension = "crowCompanion";
        public const string FastTravelExtension = "fastTravel";
        public const int MaxFastTravelIds = 64;

        public int Schema = SchemaVersion;
        public string GameVersion = "";
        public string SavedAtUtc = "";
        public long Revision;
        public long TotalSteps;
        public long TotalPlaySeconds;
        public long TimeAnchorUnixMs;
        public bool AutoEnabled = true;
        public string ViewMode = View2D;
        public string MapId = "";
        public int CellX;
        public int CellY;
        public string Facing = "down";
        public string CrowMode = CrowFree;
        // ver3.5: the found places beyond the start (null or empty: none, nothing written).
        public List<string> FastTravel;
        public string Checksum = "";

        public HarukaSaveData Copy()
        {
            var copy = (HarukaSaveData)MemberwiseClone();
            copy.FastTravel = FastTravel != null ? new List<string>(FastTravel) : null;
            return copy;
        }

        public bool HasFastTravel => FastTravel != null && FastTravel.Count > 0;

        public static bool ValidPlaceId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length > 64) return false;
            foreach (var c in id)
                if (!(c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '_' || c == '-')) return false;
            return true;
        }

        public static string FacingName(FacingDirection facing) => facing switch
        {
            FacingDirection.Up => "up",
            FacingDirection.Left => "left",
            FacingDirection.Right => "right",
            _ => "down",
        };

        public static bool TryParseFacing(string name, out FacingDirection facing)
        {
            facing = FacingDirection.Down;
            switch (name)
            {
                case "up": facing = FacingDirection.Up; return true;
                case "down": facing = FacingDirection.Down; return true;
                case "left": facing = FacingDirection.Left; return true;
                case "right": facing = FacingDirection.Right; return true;
                default: return false;
            }
        }

        public static string UtcStamp(DateTimeOffset time) =>
            time.ToUniversalTime().ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'", CultureInfo.InvariantCulture);

        public string CanonicalValues()
        {
            var inv = CultureInfo.InvariantCulture;
            return string.Join("|", new[]
            {
                Format, Schema.ToString(inv), GameVersion, SavedAtUtc, Revision.ToString(inv),
                TotalSteps.ToString(inv), TotalPlaySeconds.ToString(inv), TimeAnchorUnixMs.ToString(inv),
                AutoEnabled ? "1" : "0", ViewMode, MapId, CellX.ToString(inv), CellY.ToString(inv), Facing,
            });
        }

        public string ComputeChecksum() => "crc32:" + Crc32(Encoding.UTF8.GetBytes(CanonicalValues())).ToString("x8", CultureInfo.InvariantCulture);

        public void Seal() => Checksum = ComputeChecksum();

        public string ComputeCrowChecksum() => "crc32:" +
            Crc32(Encoding.UTF8.GetBytes(CrowExtension + "|" + CrowMode + "|" + Checksum)).ToString("x8", CultureInfo.InvariantCulture);

        public string ComputeFastTravelChecksum() => ComputeFastTravelChecksum(FastTravel);

        private string ComputeFastTravelChecksum(IList<string> ids) => "crc32:" +
            Crc32(Encoding.UTF8.GetBytes(FastTravelExtension + "|" + string.Join(",", ids ?? new List<string>()) + "|" + Checksum))
                .ToString("x8", CultureInfo.InvariantCulture);

        // UTF-8 JSON, readable (exported files are meant to be kept by people).
        public string ToJson()
        {
            var inv = CultureInfo.InvariantCulture;
            var b = new StringBuilder(512);
            b.Append("{\n");
            b.Append("  \"format\": ").Append(HarukaSaveJson.Quote(Format)).Append(",\n");
            b.Append("  \"schemaVersion\": ").Append(Schema.ToString(inv)).Append(",\n");
            b.Append("  \"gameVersion\": ").Append(HarukaSaveJson.Quote(GameVersion)).Append(",\n");
            b.Append("  \"savedAtUtc\": ").Append(HarukaSaveJson.Quote(SavedAtUtc)).Append(",\n");
            b.Append("  \"revision\": ").Append(Revision.ToString(inv)).Append(",\n");
            b.Append("  \"lifeLog\": { \"totalSteps\": ").Append(TotalSteps.ToString(inv))
                .Append(", \"totalPlaySeconds\": ").Append(TotalPlaySeconds.ToString(inv)).Append(" },\n");
            b.Append("  \"worldTime\": { \"anchorUnixMs\": ").Append(TimeAnchorUnixMs.ToString(inv)).Append(" },\n");
            b.Append("  \"settings\": { \"autoEnabled\": ").Append(AutoEnabled ? "true" : "false")
                .Append(", \"viewMode\": ").Append(HarukaSaveJson.Quote(ViewMode)).Append(" },\n");
            b.Append("  \"player\": { \"mapId\": ").Append(HarukaSaveJson.Quote(MapId))
                .Append(", \"cell\": { \"x\": ").Append(CellX.ToString(inv)).Append(", \"y\": ").Append(CellY.ToString(inv))
                .Append(" }, \"facing\": ").Append(HarukaSaveJson.Quote(Facing)).Append(" },\n");
            // A free crow (the default) and only the start places write nothing: the file is then
            // exactly a ver3.3 file.
            var entries = new List<string>();
            if (CrowMode != CrowFree)
                entries.Add("\"" + CrowExtension + "\": { \"mode\": " + HarukaSaveJson.Quote(CrowMode) +
                    ", \"checksum\": " + HarukaSaveJson.Quote(ComputeCrowChecksum()) + " }");
            if (HasFastTravel)
            {
                var ids = new StringBuilder();
                foreach (var id in FastTravel) ids.Append(ids.Length > 0 ? ", " : "").Append(HarukaSaveJson.Quote(id));
                entries.Add("\"" + FastTravelExtension + "\": { \"discovered\": [" + ids + "], \"checksum\": " +
                    HarukaSaveJson.Quote(ComputeFastTravelChecksum()) + " }");
            }
            if (entries.Count == 0) b.Append("  \"extensions\": {},\n");
            else b.Append("  \"extensions\": { ").Append(string.Join(", ", entries)).Append(" },\n");
            b.Append("  \"checksum\": ").Append(HarukaSaveJson.Quote(Checksum)).Append('\n');
            b.Append("}\n");
            return b.ToString();
        }

        // Reads and checks a save text. Unknown fields of a known schema are ignored (a later
        // build may add data a migration has not yet been written for; nothing of it is lost
        // because the newer build keeps its own copy). A newer schema is reported, never read.
        public static HarukaSaveReadStatus TryRead(string text, out HarukaSaveData data, out string problem)
        {
            data = null;
            problem = null;
            if (string.IsNullOrEmpty(text)) { problem = "empty"; return HarukaSaveReadStatus.Missing; }
            if (Encoding.UTF8.GetByteCount(text) > MaxFileBytes) { problem = "too large"; return HarukaSaveReadStatus.Corrupt; }
            if (!HarukaSaveJson.TryParse(text, out var root, out var jsonError)) { problem = "json: " + jsonError; return HarukaSaveReadStatus.Corrupt; }
            if (!(root is Dictionary<string, object> o)) { problem = "not an object"; return HarukaSaveReadStatus.Corrupt; }
            if (!Str(o, "format", out var format) || format != Format) { problem = "format"; return HarukaSaveReadStatus.Corrupt; }
            if (!Int(o, "schemaVersion", out var schema) || schema < 1) { problem = "schemaVersion"; return HarukaSaveReadStatus.Corrupt; }
            if (schema > SchemaVersion) { problem = "newer schema " + schema; return HarukaSaveReadStatus.NewerSchema; }
            // Schema 1 is the first; a later version adds "case 1: MigrateFrom1(o)" steps here.
            var d = new HarukaSaveData { Schema = (int)schema };
            if (!Str(o, "gameVersion", out d.GameVersion) || d.GameVersion.Length > 64) return Bad("gameVersion", out problem);
            if (!Str(o, "savedAtUtc", out d.SavedAtUtc) || !DateTimeOffset.TryParseExact(d.SavedAtUtc,
                    "yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _))
                return Bad("savedAtUtc", out problem);
            if (!Int(o, "revision", out d.Revision) || d.Revision < 0) return Bad("revision", out problem);
            if (!Obj(o, "lifeLog", out var life) || !Int(life, "totalSteps", out d.TotalSteps) || d.TotalSteps < 0 ||
                !Int(life, "totalPlaySeconds", out d.TotalPlaySeconds) || d.TotalPlaySeconds < 0)
                return Bad("lifeLog", out problem);
            if (!Obj(o, "worldTime", out var time) || !Int(time, "anchorUnixMs", out d.TimeAnchorUnixMs) ||
                d.TimeAnchorUnixMs <= 0 || d.TimeAnchorUnixMs > 253402300799999L)
                return Bad("worldTime", out problem);
            if (!Obj(o, "settings", out var settings) || !Bool(settings, "autoEnabled", out d.AutoEnabled) ||
                !Str(settings, "viewMode", out d.ViewMode) || d.ViewMode != View2D && d.ViewMode != View3D)
                return Bad("settings", out problem);
            if (!Obj(o, "player", out var player) || !Str(player, "mapId", out d.MapId) || !ValidMapId(d.MapId) ||
                !Obj(player, "cell", out var cell) || !Int(cell, "x", out var x) || !Int(cell, "y", out var y) ||
                Math.Abs(x) > 10000 || Math.Abs(y) > 10000 ||
                !Str(player, "facing", out d.Facing) || !TryParseFacing(d.Facing, out _))
                return Bad("player", out problem);
            d.CellX = (int)x;
            d.CellY = (int)y;
            if (o.TryGetValue("extensions", out var extensions) && extensions != null && !(extensions is Dictionary<string, object>))
                return Bad("extensions", out problem);
            if (!Str(o, "checksum", out d.Checksum) || d.Checksum != d.ComputeChecksum()) return Bad("checksum", out problem);
            if (extensions is Dictionary<string, object> ext && ext.TryGetValue(CrowExtension, out var crowValue))
            {
                if (!(crowValue is Dictionary<string, object> crow) || !Str(crow, "mode", out d.CrowMode) ||
                    d.CrowMode != CrowFree && d.CrowMode != CrowFollow ||
                    !Str(crow, "checksum", out var crowChecksum) || crowChecksum != d.ComputeCrowChecksum())
                    return Bad("extensions." + CrowExtension, out problem);
            }
            if (extensions is Dictionary<string, object> ext2 && ext2.TryGetValue(FastTravelExtension, out var travelValue))
            {
                if (!(travelValue is Dictionary<string, object> travel) || !travel.TryGetValue("discovered", out var listValue) ||
                    !(listValue is List<object> list) || list.Count > MaxFastTravelIds || !Str(travel, "checksum", out var travelChecksum))
                    return Bad("extensions." + FastTravelExtension, out problem);
                var ids = new List<string>();
                foreach (var item in list)
                {
                    if (!(item is string id) || !ValidPlaceId(id)) return Bad("extensions." + FastTravelExtension, out problem);
                    ids.Add(id);
                }
                if (travelChecksum != d.ComputeFastTravelChecksum(ids)) return Bad("extensions." + FastTravelExtension, out problem);
                var unique = new List<string>();
                foreach (var id in ids)
                    if (!unique.Contains(id)) unique.Add(id);
                d.FastTravel = unique.Count > 0 ? unique : null;
            }
            data = d;
            return HarukaSaveReadStatus.Valid;
        }

        public static bool ValidMapId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length > 64) return false;
            foreach (var c in id)
                if (!(c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '_' || c == '-')) return false;
            return true;
        }

        private static HarukaSaveReadStatus Bad(string field, out string problem)
        {
            problem = field;
            return HarukaSaveReadStatus.Corrupt;
        }

        private static bool Str(Dictionary<string, object> o, string key, out string value)
        {
            value = null;
            if (!o.TryGetValue(key, out var v) || !(v is string s)) return false;
            value = s;
            return true;
        }

        private static bool Int(Dictionary<string, object> o, string key, out long value)
        {
            value = 0;
            if (!o.TryGetValue(key, out var v) || !(v is long l)) return false;
            value = l;
            return true;
        }

        private static bool Bool(Dictionary<string, object> o, string key, out bool value)
        {
            value = false;
            if (!o.TryGetValue(key, out var v) || !(v is bool b)) return false;
            value = b;
            return true;
        }

        private static bool Obj(Dictionary<string, object> o, string key, out Dictionary<string, object> value)
        {
            value = null;
            if (!o.TryGetValue(key, out var v) || !(v is Dictionary<string, object> d)) return false;
            value = d;
            return true;
        }

        private static uint[] crcTable;

        public static uint Crc32(byte[] bytes)
        {
            if (crcTable == null)
            {
                var table = new uint[256];
                for (uint n = 0; n < 256; n++)
                {
                    var c = n;
                    for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                    table[n] = c;
                }
                crcTable = table;
            }
            var crc = 0xFFFFFFFFu;
            foreach (var b in bytes) crc = crcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
            return crc ^ 0xFFFFFFFFu;
        }
    }
}
