using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using Halka.Poc2D3D.Save;

namespace Halka.Poc2D3D.Life
{
    public enum CompanionMode { Free, Follow, Wait }

    // ver4.0: everything ver4.0 adds to the save, in ONE extension ("life40") with its own checksum
    // (over its values and the main checksum, like the crow's and the places' extensions): the day
    // count's origin, the three abilities and the mini games' records, the ﾆｺﾁｭｰﾌﾞ channel, the
    // passers-by, Rin's and Len's modes and places, and the hide-and-seek records.
    //   - The file stays Schema 1: ver3.0-3.9 read a ver4.0 file and ignore the extension.
    //   - A file without it (every older save) starts ver4.0's data from its defaults, once
    //     (LifeState.StartFresh); after that the extension is always written.
    //   - A damaged extension (not an object, a missing field, a wrong checksum) makes the file
    //     Corrupt, so the snapshot store falls back to the last good snapshot (指示書 9.8 / 15.5:
    //     the existing recovery, never a reset of the whole save). Values that are well-formed but
    //     odd (a huge count, a post time in the future, an unknown region, Rin standing on a
    //     rock) are repaired when the game applies them (LifeState.Sanitize), not refused.
    public sealed class LifeSaveBlock
    {
        public const string Extension = "life40";
        public const int Version = 1;
        public const int MaxNpcs = 8;

        public long DayOrigin;
        public long StartedUnixMs;
        public CreatorState Creator = new CreatorState();
        public NicoTubeChannel Channel = new NicoTubeChannel();
        public long NpcSerial;
        public long NpcEvalSerial;
        public long NpcEvalDay = -1;
        public readonly List<NpcRecord> Npcs = new List<NpcRecord>();
        public readonly CompanionRecord Rin = new CompanionRecord();
        public readonly CompanionRecord Len = new CompanionRecord();
        public readonly HideSeekRecords HideSeek = new HideSeekRecords();
        // Sleep (added after the first look): the game minute (World Time, absolute) she last woke up
        // or the life began, and how often she slept. -1: not known yet (an older ver4.0 block: the
        // "sleep" object is then simply absent, and its checksum stays as it was).
        public long WakeMinute = -1;
        public long Sleeps;
        public bool HasSleep => WakeMinute >= 0 || Sleeps > 0;

        public sealed class NpcRecord
        {
            public long Id;
            public string Kind = "";
            public string Region = "";
            public int X;
            public int Y;
            public long LeaveAtUnixMs;
            public NpcRecord Copy() => (NpcRecord)MemberwiseClone();
        }

        public sealed class CompanionRecord
        {
            public CompanionMode Mode = CompanionMode.Free;
            public bool HasPlace;
            public int X;
            public int Y;
            public CompanionRecord Copy() => (CompanionRecord)MemberwiseClone();
            public void From(CompanionRecord o) { Mode = o.Mode; HasPlace = o.HasPlace; X = o.X; Y = o.Y; }
        }

        public sealed class HideSeekRecords
        {
            public long RinFound;
            public long LenFound;
            public long BothCleared;
            public long BestRinSeconds = -1;
            public long BestLenSeconds = -1;
            public long BestBothSeconds = -1;
            public HideSeekRecords Copy() => (HideSeekRecords)MemberwiseClone();
            public void From(HideSeekRecords o)
            {
                RinFound = o.RinFound; LenFound = o.LenFound; BothCleared = o.BothCleared;
                BestRinSeconds = o.BestRinSeconds; BestLenSeconds = o.BestLenSeconds; BestBothSeconds = o.BestBothSeconds;
            }
        }

        public LifeSaveBlock Copy()
        {
            var c = new LifeSaveBlock
            {
                DayOrigin = DayOrigin, StartedUnixMs = StartedUnixMs, Creator = Creator.Copy(), Channel = Channel.Copy(),
                NpcSerial = NpcSerial, NpcEvalSerial = NpcEvalSerial, NpcEvalDay = NpcEvalDay,
                WakeMinute = WakeMinute, Sleeps = Sleeps,
            };
            foreach (var n in Npcs) c.Npcs.Add(n.Copy());
            c.Rin.From(Rin);
            c.Len.From(Len);
            c.HideSeek.From(HideSeek);
            return c;
        }

        public static string ModeName(CompanionMode mode) => mode == CompanionMode.Follow ? "follow" : mode == CompanionMode.Wait ? "wait" : "free";

        public static bool TryMode(string name, out CompanionMode mode)
        {
            mode = CompanionMode.Free;
            switch (name)
            {
                case "free": return true;
                case "follow": mode = CompanionMode.Follow; return true;
                case "wait": mode = CompanionMode.Wait; return true;
                default: return false;
            }
        }

        // Every value in a fixed order (the checksum's input; also the checks' "same data" test).
        public string Canonical()
        {
            var inv = CultureInfo.InvariantCulture;
            var b = new StringBuilder(512);
            b.Append(Extension).Append('|').Append(Version.ToString(inv)).Append('|').Append(DayOrigin.ToString(inv)).Append('|')
                .Append(StartedUnixMs.ToString(inv));
            for (var i = 0; i < CreatorState.SkillCount; i++)
                b.Append('|').Append(LifeNumbers.ToSave(Creator.Skills[i])).Append(',').Append(Creator.Plays[i].ToString(inv))
                    .Append(',').Append(Creator.Best[i].ToString(inv)).Append(',').Append(Creator.Last[i].ToString(inv));
            var ch = Channel;
            b.Append('|').Append(LifeNumbers.ToSave(ch.Subscribers)).Append(',').Append(LifeNumbers.ToSave(ch.TotalViews)).Append(',')
                .Append(LifeNumbers.ToSave(ch.Money)).Append(',').Append(ch.Posts.ToString(inv)).Append(',').Append(ch.LastPostUnixMs.ToString(inv))
                .Append(',').Append(ch.BasisUnixMs.ToString(inv)).Append(',').Append(ch.Seed.ToString(inv));
            b.Append('|').Append(NpcSerial.ToString(inv)).Append(',').Append(NpcEvalSerial.ToString(inv)).Append(',').Append(NpcEvalDay.ToString(inv));
            foreach (var n in Npcs)
                b.Append('|').Append(n.Id.ToString(inv)).Append(',').Append(n.Kind).Append(',').Append(n.Region).Append(',')
                    .Append(n.X.ToString(inv)).Append(',').Append(n.Y.ToString(inv)).Append(',').Append(n.LeaveAtUnixMs.ToString(inv));
            foreach (var c in new[] { Rin, Len })
                b.Append('|').Append(ModeName(c.Mode)).Append(',').Append(c.HasPlace ? "1" : "0").Append(',')
                    .Append(c.X.ToString(inv)).Append(',').Append(c.Y.ToString(inv));
            var h = HideSeek;
            b.Append('|').Append(h.RinFound.ToString(inv)).Append(',').Append(h.LenFound.ToString(inv)).Append(',').Append(h.BothCleared.ToString(inv))
                .Append(',').Append(h.BestRinSeconds.ToString(inv)).Append(',').Append(h.BestLenSeconds.ToString(inv)).Append(',')
                .Append(h.BestBothSeconds.ToString(inv));
            if (HasSleep) b.Append("|sleep,").Append(WakeMinute.ToString(inv)).Append(',').Append(Sleeps.ToString(inv));
            return b.ToString();
        }

        public string ComputeChecksum(string mainChecksum) => "crc32:" +
            HarukaSaveData.Crc32(Encoding.UTF8.GetBytes(Canonical() + "|" + mainChecksum)).ToString("x8", CultureInfo.InvariantCulture);

        public bool SameAs(LifeSaveBlock other) => other != null && Canonical() == other.Canonical();

        public string ToJson(string mainChecksum)
        {
            var inv = CultureInfo.InvariantCulture;
            string Q(string s) => HarukaSaveJson.Quote(s);
            var b = new StringBuilder(1024);
            b.Append("{ \"v\": ").Append(Version.ToString(inv));
            b.Append(", \"dayOrigin\": ").Append(DayOrigin.ToString(inv));
            b.Append(", \"startedUnixMs\": ").Append(StartedUnixMs.ToString(inv));
            b.Append(", \"skills\": {");
            for (var i = 0; i < CreatorState.SkillCount; i++)
                b.Append(i > 0 ? ", " : " ").Append(Q(CreatorState.SkillIds[i])).Append(": { \"value\": ").Append(Q(LifeNumbers.ToSave(Creator.Skills[i])))
                    .Append(", \"plays\": ").Append(Creator.Plays[i].ToString(inv)).Append(", \"best\": ").Append(Creator.Best[i].ToString(inv))
                    .Append(", \"last\": ").Append(Creator.Last[i].ToString(inv)).Append(" }");
            b.Append(" }");
            var ch = Channel;
            b.Append(", \"nicotube\": { \"subscribers\": ").Append(Q(LifeNumbers.ToSave(ch.Subscribers)))
                .Append(", \"views\": ").Append(Q(LifeNumbers.ToSave(ch.TotalViews)))
                .Append(", \"money\": ").Append(Q(LifeNumbers.ToSave(ch.Money)))
                .Append(", \"posts\": ").Append(ch.Posts.ToString(inv))
                .Append(", \"lastPostUnixMs\": ").Append(ch.LastPostUnixMs.ToString(inv))
                .Append(", \"basisUnixMs\": ").Append(ch.BasisUnixMs.ToString(inv))
                .Append(", \"seed\": ").Append(ch.Seed.ToString(inv)).Append(" }");
            b.Append(", \"npcs\": { \"serial\": ").Append(NpcSerial.ToString(inv)).Append(", \"evalSerial\": ").Append(NpcEvalSerial.ToString(inv))
                .Append(", \"evalDay\": ").Append(NpcEvalDay.ToString(inv)).Append(", \"list\": [");
            for (var i = 0; i < Npcs.Count; i++)
            {
                var n = Npcs[i];
                b.Append(i > 0 ? ", " : "").Append("{ \"id\": ").Append(n.Id.ToString(inv)).Append(", \"kind\": ").Append(Q(n.Kind))
                    .Append(", \"region\": ").Append(Q(n.Region)).Append(", \"x\": ").Append(n.X.ToString(inv)).Append(", \"y\": ").Append(n.Y.ToString(inv))
                    .Append(", \"leaveAtUnixMs\": ").Append(n.LeaveAtUnixMs.ToString(inv)).Append(" }");
            }
            b.Append("] }");
            b.Append(", \"companions\": {");
            var first = true;
            foreach (var (key, c) in new[] { ("rin", Rin), ("len", Len) })
            {
                b.Append(first ? " " : ", ").Append(Q(key)).Append(": { \"mode\": ").Append(Q(ModeName(c.Mode)))
                    .Append(", \"hasPlace\": ").Append(c.HasPlace ? "true" : "false")
                    .Append(", \"x\": ").Append(c.X.ToString(inv)).Append(", \"y\": ").Append(c.Y.ToString(inv)).Append(" }");
                first = false;
            }
            b.Append(" }");
            var h = HideSeek;
            b.Append(", \"hideAndSeek\": { \"rinFound\": ").Append(h.RinFound.ToString(inv)).Append(", \"lenFound\": ").Append(h.LenFound.ToString(inv))
                .Append(", \"bothCleared\": ").Append(h.BothCleared.ToString(inv)).Append(", \"bestRinSeconds\": ").Append(h.BestRinSeconds.ToString(inv))
                .Append(", \"bestLenSeconds\": ").Append(h.BestLenSeconds.ToString(inv)).Append(", \"bestBothSeconds\": ").Append(h.BestBothSeconds.ToString(inv))
                .Append(" }");
            if (HasSleep)
                b.Append(", \"sleep\": { \"wakeMinute\": ").Append(WakeMinute.ToString(inv)).Append(", \"sleeps\": ").Append(Sleeps.ToString(inv)).Append(" }");
            b.Append(", \"checksum\": ").Append(Q(ComputeChecksum(mainChecksum))).Append(" }");
            return b.ToString();
        }

        // Structure and checksum only (a well-formed but odd value is kept for Sanitize).
        public static bool TryRead(object value, string mainChecksum, out LifeSaveBlock block, out string problem)
        {
            block = null;
            problem = null;
            if (!(value is Dictionary<string, object> o)) return Fail("not an object", out problem);
            var b = new LifeSaveBlock();
            if (!Long(o, "v", out var v) || v < 1) return Fail("v", out problem);
            if (!Long(o, "dayOrigin", out b.DayOrigin) || !Long(o, "startedUnixMs", out b.StartedUnixMs)) return Fail("day", out problem);
            if (!Obj(o, "skills", out var skills)) return Fail("skills", out problem);
            for (var i = 0; i < CreatorState.SkillCount; i++)
            {
                if (!Obj(skills, CreatorState.SkillIds[i], out var s) || !Str(s, "value", out var text) || !LifeNumbers.TryParse(text, out var skill) ||
                    !Long(s, "plays", out var plays) || plays < 0 || !Long(s, "best", out var best) || !Long(s, "last", out var last) ||
                    best < -1 || best > 100 || last < -1 || last > 100)
                    return Fail("skills." + CreatorState.SkillIds[i], out problem);
                b.Creator.Skills[i] = skill;
                b.Creator.Plays[i] = plays;
                b.Creator.Best[i] = (int)best;
                b.Creator.Last[i] = (int)last;
            }
            if (!Obj(o, "nicotube", out var n) || !Num(n, "subscribers", out b.Channel.Subscribers) || !Num(n, "views", out b.Channel.TotalViews) ||
                !Num(n, "money", out b.Channel.Money) || !Long(n, "posts", out b.Channel.Posts) || b.Channel.Posts < 0 ||
                !Long(n, "lastPostUnixMs", out b.Channel.LastPostUnixMs) || !Long(n, "basisUnixMs", out b.Channel.BasisUnixMs) ||
                !Long(n, "seed", out b.Channel.Seed))
                return Fail("nicotube", out problem);
            if (!Obj(o, "npcs", out var npcs) || !Long(npcs, "serial", out b.NpcSerial) || !Long(npcs, "evalSerial", out b.NpcEvalSerial) ||
                !Long(npcs, "evalDay", out b.NpcEvalDay) || !npcs.TryGetValue("list", out var listValue) || !(listValue is List<object> list) ||
                list.Count > MaxNpcs)
                return Fail("npcs", out problem);
            foreach (var item in list)
            {
                if (!(item is Dictionary<string, object> e)) return Fail("npcs.list", out problem);
                var r = new NpcRecord();
                if (!Long(e, "id", out r.Id) || !Str(e, "kind", out r.Kind) || r.Kind.Length > 32 || !Str(e, "region", out r.Region) ||
                    r.Region.Length > 64 || !Long(e, "x", out var x) || !Long(e, "y", out var y) || Math.Abs(x) > 10000 || Math.Abs(y) > 10000 ||
                    !Long(e, "leaveAtUnixMs", out r.LeaveAtUnixMs))
                    return Fail("npcs.list", out problem);
                r.X = (int)x;
                r.Y = (int)y;
                b.Npcs.Add(r);
            }
            if (!Obj(o, "companions", out var companions)) return Fail("companions", out problem);
            foreach (var (key, c) in new[] { ("rin", b.Rin), ("len", b.Len) })
            {
                if (!Obj(companions, key, out var e) || !Str(e, "mode", out var mode) || !TryMode(mode, out c.Mode) ||
                    !Bool(e, "hasPlace", out c.HasPlace) || !Long(e, "x", out var x) || !Long(e, "y", out var y) ||
                    Math.Abs(x) > 10000 || Math.Abs(y) > 10000)
                    return Fail("companions." + key, out problem);
                c.X = (int)x;
                c.Y = (int)y;
            }
            if (!Obj(o, "hideAndSeek", out var h) || !Long(h, "rinFound", out b.HideSeek.RinFound) || !Long(h, "lenFound", out b.HideSeek.LenFound) ||
                !Long(h, "bothCleared", out b.HideSeek.BothCleared) || !Long(h, "bestRinSeconds", out b.HideSeek.BestRinSeconds) ||
                !Long(h, "bestLenSeconds", out b.HideSeek.BestLenSeconds) || !Long(h, "bestBothSeconds", out b.HideSeek.BestBothSeconds) ||
                b.HideSeek.RinFound < 0 || b.HideSeek.LenFound < 0 || b.HideSeek.BothCleared < 0)
                return Fail("hideAndSeek", out problem);
            if (o.ContainsKey("sleep") && (!Obj(o, "sleep", out var sleep) || !Long(sleep, "wakeMinute", out b.WakeMinute) ||
                !Long(sleep, "sleeps", out b.Sleeps) || b.Sleeps < 0))
                return Fail("sleep", out problem);
            if (!Str(o, "checksum", out var checksum) || checksum != b.ComputeChecksum(mainChecksum)) return Fail("checksum", out problem);
            block = b;
            return true;
        }

        private static bool Fail(string what, out string problem)
        {
            problem = what;
            return false;
        }

        private static bool Long(Dictionary<string, object> o, string key, out long value)
        {
            value = 0;
            if (!o.TryGetValue(key, out var v) || !(v is long l)) return false;
            value = l;
            return true;
        }

        private static bool Str(Dictionary<string, object> o, string key, out string value)
        {
            value = null;
            if (!o.TryGetValue(key, out var v) || !(v is string s)) return false;
            value = s;
            return true;
        }

        private static bool Bool(Dictionary<string, object> o, string key, out bool value)
        {
            value = false;
            if (!o.TryGetValue(key, out var v) || !(v is bool b)) return false;
            value = b;
            return true;
        }

        private static bool Num(Dictionary<string, object> o, string key, out BigInteger value)
        {
            value = BigInteger.Zero;
            return Str(o, key, out var text) && LifeNumbers.TryParse(text, out value);
        }

        private static bool Obj(Dictionary<string, object> o, string key, out Dictionary<string, object> value)
        {
            value = null;
            if (!o.TryGetValue(key, out var v) || !(v is Dictionary<string, object> d)) return false;
            value = d;
            return true;
        }
    }
}
