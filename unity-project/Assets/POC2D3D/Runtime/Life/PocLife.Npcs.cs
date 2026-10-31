using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using Halka.Game.Player;
using UnityEngine;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

namespace Halka.Poc2D3D.Life
{
    // ver4.0: the passers-by, fans and antis of ﾊﾙｶﾁｬﾝ's channel (指示書 10).
    //   - Whether anyone is about is decided at a re-evaluation, never each frame: when the game
    //     starts on a new game day, at 00:00, after a post, and (from 1,000,000 subscribers) when the
    //     last one leaves. The chance that at least one is about is p = min(1, sqrt(subscribers /
    //     1,000,000)) (NpcTuning); from 1,000,000 it is 1 and the world always holds one at least.
    //   - At most 3 at a time. Kinds: fans 75 %, antis 25 %, men and women 50 % each.
    //   - Each lives in the logical world (kind, region, cell, when it leaves) whether or not its
    //     chunk is drawn; only a drawn one has a model, walks about and answers her; a far one is not
    //     updated at all (its leaving time is only compared with the clock).
    //   - Where: a walkable cell of one of the nine regions that she can reach on foot, never a door,
    //     a furniture / sign stand cell or a cell taken by someone, at least 12 cells from her and,
    //     when possible, in a chunk not drawn now (nobody appears in front of her eyes).
    public sealed partial class PocLife
    {
        public static readonly string[] NpcKinds = { "fan_male", "fan_female", "anti_male", "anti_female" };

        public sealed class NpcTuning
        {
            public double FullAtSubscribers = 1000000d;
            public int MaxPresent = 3;
            public double FanShare = 0.75;
            public double MaleShare = 0.5;
            public double SecondChance = 0.6;  // a second one: roll < p * this
            public double ThirdChance = 0.3;
            public long StayRealMsMin = 10L * 60L * 1000L;   // half a game day
            public long StayRealMsMax = 20L * 60L * 1000L;   // a game day
            public int MinSpawnDistance = 12;
            public static readonly NpcTuning Default = new NpcTuning();
        }

        public static double NpcChance(BigInteger subscribers) => NpcChance(subscribers, NpcTuning.Default);

        public static double NpcChance(BigInteger subscribers, NpcTuning tuning)
        {
            if (subscribers.Sign <= 0) return 0d;
            if (subscribers >= new BigInteger(tuning.FullAtSubscribers)) return 1d; // exactly 100 % from 1,000,000
            var ratioLog = LifeNumbers.Log10OnePlus(subscribers - BigInteger.One) - Math.Log10(tuning.FullAtSubscribers);
            if (ratioLog >= 0d) return 1d;
            var p = Math.Sqrt(Math.Pow(10d, ratioLog));
            return double.IsNaN(p) ? 0d : Math.Max(0d, Math.Min(1d, p));
        }

        public bool GuaranteeOn => NpcChance(State.Channel.Subscribers) >= 1d;

        private sealed class Npc
        {
            public LifeSaveBlock.NpcRecord Record;
            public PocWalker Walker;
            public Vector2Int Home;
            public float NextMoveAt;
            public bool Leaving;
            public int Talks;
            public readonly List<Vector2Int> Path = new List<Vector2Int>();
        }

        private readonly List<Npc> npcs = new List<Npc>();
        public int NpcEvaluations { get; private set; }
        public int NpcSpawns { get; private set; }
        public int NpcDepartures { get; private set; }
        public int NpcGuaranteeSpawns { get; private set; }
        public string LastNpcEvaluation { get; private set; } = "";
        public int NpcCount => npcs.Count;

        public IEnumerable<PocWalker> NpcWalkers()
        {
            foreach (var n in npcs) yield return n.Walker;
        }

        // The saved passers-by come back as they were (an unknown region or a cell that is no longer
        // free is repaired: the NPC goes, or moves to the nearest free reachable cell).
        private void RestoreNpcs()
        {
            ClearNpcWalkers();
            var world = session.OutdoorWorld;
            if (world == null) return;
            foreach (var record in State.Npcs)
            {
                if (npcs.Count >= NpcTuning.Default.MaxPresent) break;
                var cell = new Vector2Int(record.X, record.Y);
                if (!Reachable.Contains(cell) || !CharacterCanEnter(cell, null))
                {
                    if (!TryNearestFree(cell, 6, out cell)) { Repairs++; continue; }
                    Repairs++;
                }
                var region = session.OutdoorIndex.RegionAt(cell);
                record.Region = region != null ? region.MapId : record.Region;
                record.X = cell.x;
                record.Y = cell.y;
                npcs.Add(MakeNpc(record, cell));
            }
            State.Npcs.Clear();
        }

        private void ClearNpcWalkers()
        {
            foreach (var n in npcs)
                if (n.Walker != null) Destroy(n.Walker.gameObject);
            npcs.Clear();
        }

        private void ClearNpcs()
        {
            ClearNpcWalkers();
            State.Npcs.Clear();
            Changed();
        }

        private Npc MakeNpc(LifeSaveBlock.NpcRecord record, Vector2Int cell)
        {
            var look = Look(record.Kind);
            var go = new GameObject("NPC " + record.Kind + " " + record.Id);
            go.transform.SetParent(transform, false);
            var walker = go.AddComponent<PocWalker>();
            walker.Setup("npc_" + record.Id.ToString(CultureInfo.InvariantCulture), record.Kind, look?.model, look?.material,
                look != null ? look.scale : 1f, look?.sprites, look != null ? look.outlineId : PocOutlineTag.CharacterId);
            if (look != null) walker.SpriteOffsetNorth = look.spriteOffsetNorth;
            walker.StepSeconds = 0.32f;
            walker.Place(cell, FacingDirection.Down);
            return new Npc { Record = record, Walker = walker, Home = cell, NextMoveAt = lifeTime + 1f + (float)Unit(record.Id, 3) * 3f };
        }

        private void CaptureNpcs(LifeSaveBlock block)
        {
            block.Npcs.Clear();
            foreach (var n in npcs)
            {
                var r = n.Record.Copy();
                var cell = n.Walker.Cell;
                r.X = cell.x;
                r.Y = cell.y;
                var region = session.OutdoorIndex != null ? session.OutdoorIndex.RegionAt(cell) : null;
                if (region != null) r.Region = region.MapId;
                block.Npcs.Add(r);
            }
        }

        // The start of a game: re-evaluated only on a game day that has not had one yet, so closing
        // and opening the page never rolls again (and the saved passers-by simply come back).
        private void EvaluateNpcsAtStart()
        {
            if (State.NpcEvalDay != AbsoluteDay) EvaluateNpcs("start");
            else EnsureGuarantee("start");
        }

        // One re-evaluation: one roll (from the channel's seed and the evaluation's number, so it is
        // the same on every device and is not rolled again by a reload).
        public void EvaluateNpcs(string reason)
        {
            if (!Started || session.OutdoorWorld == null) return;
            var tuning = NpcTuning.Default;
            var p = NpcChance(State.Channel.Subscribers, tuning);
            State.NpcEvalSerial++;
            State.NpcEvalDay = AbsoluteDay;
            NpcEvaluations++;
            var seed = State.Channel.Seed;
            var roll = Unit(seed, State.NpcEvalSerial * 4 + 1);
            var target = 0;
            if (roll < p)
            {
                target = 1;
                if (Unit(seed, State.NpcEvalSerial * 4 + 2) < p * tuning.SecondChance) target++;
                if (Unit(seed, State.NpcEvalSerial * 4 + 3) < p * tuning.ThirdChance) target++;
            }
            target = Math.Min(target, tuning.MaxPresent);
            var before = npcs.Count;
            while (npcs.Count < target && SpawnNpc(false)) { }
            LastNpcEvaluation = reason + ": p " + p.ToString("0.###", CultureInfo.InvariantCulture) + " roll " +
                roll.ToString("0.###", CultureInfo.InvariantCulture) + " target " + target + " present " + before + " -> " + npcs.Count;
            Changed();
        }

        // From 1,000,000 subscribers the outdoor world never holds nobody.
        private void EnsureGuarantee(string reason)
        {
            if (!GuaranteeOn || npcs.Count > 0) return;
            if (!SpawnNpc(true)) return;
            NpcGuaranteeSpawns++;
            LastNpcEvaluation = reason + ": guarantee";
            Changed();
        }

        private bool SpawnNpc(bool guarantee)
        {
            var tuning = NpcTuning.Default;
            if (npcs.Count >= tuning.MaxPresent) return false;
            var serial = ++State.NpcSerial;
            var seed = State.Channel.Seed;
            var fan = Unit(seed, serial * 8 + 1) < tuning.FanShare;
            var male = Unit(seed, serial * 8 + 2) < tuning.MaleShare;
            var kind = (fan ? "fan_" : "anti_") + (male ? "male" : "female");
            if (!TryNpcCell(serial, out var cell)) return false;
            var region = session.OutdoorIndex.RegionAt(cell);
            var stay = tuning.StayRealMsMin + (long)(Unit(seed, serial * 8 + 3) * (tuning.StayRealMsMax - tuning.StayRealMsMin));
            var record = new LifeSaveBlock.NpcRecord
            {
                Id = serial, Kind = kind, Region = region != null ? region.MapId : "", X = cell.x, Y = cell.y, LeaveAtUnixMs = NowMs + stay,
            };
            npcs.Add(MakeNpc(record, cell));
            NpcSpawns++;
            return true;
        }

        // A cell for a newcomer (see the class comment), chosen from the seed and its number.
        private bool TryNpcCell(long serial, out Vector2Int cell)
        {
            cell = default;
            var index = session.OutdoorIndex;
            var regions = index.Regions;
            var player = session.World == session.OutdoorWorld ? session.Cell :
                entryCell ?? Vector2Int.zero;
            var seed = State.Channel.Seed;
            for (var pass = 0; pass < 2; pass++)
            {
                for (var attempt = 0; attempt < 80; attempt++)
                {
                    var region = regions[(int)(Unit(seed, serial * 1000 + attempt * 3 + 1) * regions.Count) % regions.Count];
                    var rect = region.Rect;
                    var x = rect.Min.x + (int)(Unit(seed, serial * 1000 + attempt * 3 + 2) * (rect.Max.x - rect.Min.x + 1));
                    var y = rect.Min.y + (int)(Unit(seed, serial * 1000 + attempt * 3 + 3) * (rect.Max.y - rect.Min.y + 1));
                    var c = new Vector2Int(x, y);
                    if (!Reachable.Contains(c) || ActionCells.Contains(c) || !CharacterCanEnter(c, null)) continue;
                    if (Mathf.Abs(c.x - player.x) + Mathf.Abs(c.y - player.y) < NpcTuning.Default.MinSpawnDistance) continue;
                    if (pass == 0 && session.CellReady != null && session.CellReady(c)) continue; // not where it is drawn now
                    cell = c;
                    return true;
                }
            }
            return false;
        }

        private bool TryNearestFree(Vector2Int from, int radius, out Vector2Int cell)
        {
            for (var r = 0; r <= radius; r++)
                for (var dx = -r; dx <= r; dx++)
                    for (var dy = -r; dy <= r; dy++)
                    {
                        if (Mathf.Abs(dx) + Mathf.Abs(dy) != r) continue;
                        var c = from + new Vector2Int(dx, dy);
                        if (Reachable.Contains(c) && !ActionCells.Contains(c) && CharacterCanEnter(c, null)) { cell = c; return true; }
                    }
            cell = from;
            return false;
        }

        // Drawn ones walk about near where they appeared, look round, turn to her when she comes
        // close; their time over, they walk away and go when nobody sees them (chunk not drawn).
        private void TickNpcs(float dt, long nowMs)
        {
            var outdoors = session.World == session.OutdoorWorld;
            if (npcs.Count == 0 && GuaranteeOn) EnsureGuarantee("check");
            for (var i = npcs.Count - 1; i >= 0; i--)
            {
                var n = npcs[i];
                var w = n.Walker;
                if (!n.Leaving && nowMs >= n.Record.LeaveAtUnixMs) n.Leaving = true;
                if (!w.Drawn)
                {
                    if (n.Leaving) Depart(i);
                    continue; // far away: not updated (its place is kept)
                }
                w.Advance(dt);
                if (w.IsMoving) continue;
                if (n.Path.Count > 0)
                {
                    var next = n.Path[0];
                    if (CharacterCanEnter(next, w) && w.TryStep(next - w.Cell, c => CharacterCanEnter(c, w))) n.Path.RemoveAt(0);
                    else n.Path.Clear();
                    continue;
                }
                var toHer = outdoors ? Mathf.Abs(session.Cell.x - w.Cell.x) + Mathf.Abs(session.Cell.y - w.Cell.y) : 99;
                if (!n.Leaving && toHer <= 3)
                {
                    var d = session.Cell - w.Cell;
                    w.Face(Mathf.Abs(d.x) >= Mathf.Abs(d.y) ? (d.x > 0 ? FacingDirection.Right : FacingDirection.Left)
                        : (d.y > 0 ? FacingDirection.Up : FacingDirection.Down));
                    n.NextMoveAt = Mathf.Max(n.NextMoveAt, lifeTime + 0.8f);
                    continue;
                }
                if (lifeTime < n.NextMoveAt) continue;
                var roll = Unit(n.Record.Id, w.StepsTaken * 7 + (long)(lifeTime * 10f));
                n.NextMoveAt = lifeTime + 1.2f + (float)roll * 3.5f;
                if (n.Leaving)
                {
                    // away from her, a few cells at a time
                    var away = w.Cell + ((Vector2)(w.Cell - session.Cell)).normalized.ToVector2IntRounded() * 6;
                    if (!TryPath(w, away, 6, n.Path)) n.Path.Clear();
                    if (n.Path.Count > 8) n.Path.RemoveRange(8, n.Path.Count - 8);
                    n.NextMoveAt = lifeTime + 0.2f;
                    continue;
                }
                if (roll < 0.3)
                {
                    w.Face((FacingDirection)((int)(roll * 1000) % 4)); // look round
                    continue;
                }
                var dirs = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
                var dir = dirs[(int)(roll * 997) % 4];
                var to = w.Cell + dir;
                if (Mathf.Abs(to.x - n.Home.x) + Mathf.Abs(to.y - n.Home.y) <= 5 && !ActionCells.Contains(to))
                    w.TryStep(dir, c => CharacterCanEnter(c, w));
            }
        }

        private void Depart(int index)
        {
            var n = npcs[index];
            if (n.Walker != null) Destroy(n.Walker.gameObject);
            npcs.RemoveAt(index);
            NpcDepartures++;
            Changed();
            EnsureGuarantee("left");
        }

        // A fan or an anti in front of her: a line of its own (it turns to her first).
        public static readonly string[] FanLines = { "いつもみてるよ！", "おうえんしてる！", "あたらしいどうが たのしみ！" };
        public static readonly string[] AntiLines = { "そんなに にんきなの？", "もっと れんしゅうしたら？", "ふーん……。" };
        public string LastNpcLine { get; private set; } = "";
        public int NpcTalks { get; private set; }

        private bool TalkToNpc(PocWalker walker)
        {
            foreach (var n in npcs)
            {
                if (n.Walker != walker) continue;
                var fan = n.Record.Kind.StartsWith("fan", StringComparison.Ordinal);
                var lines = fan ? FanLines : AntiLines;
                LastNpcLine = lines[n.Talks % lines.Length];
                n.Talks++;
                NpcTalks++;
                n.NextMoveAt = lifeTime + 2.5f;
                session.ShowMessage((fan ? "ファン「" : "アンチ「") + LastNpcLine + "」");
                return true;
            }
            return false;
        }

        private void AppendNpcJson(StringBuilder b)
        {
            var inv = CultureInfo.InvariantCulture;
            b.Append(",\"npcChance\":").Append(NpcChance(State.Channel.Subscribers).ToString("0.####", inv));
            b.Append(",\"npcEval\":").Append(NpcEvaluations.ToString(inv)).Append(",\"npcSpawns\":").Append(NpcSpawns.ToString(inv))
                .Append(",\"npcDepartures\":").Append(NpcDepartures.ToString(inv)).Append(",\"npcGuarantee\":").Append(NpcGuaranteeSpawns.ToString(inv))
                .Append(",\"npcTalks\":").Append(NpcTalks.ToString(inv)).Append(",\"npcLine\":\"").Append(LastNpcLine).Append('"')
                .Append(",\"npcLastEval\":\"").Append(LastNpcEvaluation).Append('"');
            b.Append(",\"npcs\":[");
            for (var i = 0; i < npcs.Count; i++)
            {
                var n = npcs[i];
                var region = session.OutdoorIndex != null ? session.OutdoorIndex.RegionAt(n.Walker.Cell) : null;
                b.Append(i > 0 ? "," : "").Append("{\"id\":").Append(n.Record.Id.ToString(inv)).Append(",\"kind\":\"").Append(n.Record.Kind)
                    .Append("\",\"x\":").Append(n.Walker.Cell.x.ToString(inv)).Append(",\"y\":").Append(n.Walker.Cell.y.ToString(inv))
                    .Append(",\"region\":\"").Append(region != null ? region.MapId : "").Append("\",\"drawn\":").Append(n.Walker.Drawn ? 1 : 0)
                    .Append(",\"model\":").Append(n.Walker.ModelInstance != null ? 1 : 0)
                    .Append(",\"leaving\":").Append(n.Leaving ? 1 : 0).Append(",\"steps\":").Append(n.Walker.StepsTaken.ToString(inv));
                AppendScreen(b, n.Walker);
                b.Append('}');
            }
            b.Append(']');
        }

        // Where a character is drawn on screen (pixels from the bottom left), for a click by the checks.
        private void AppendScreen(StringBuilder b, PocWalker walker)
        {
            var inv = CultureInfo.InvariantCulture;
            var x = -1f;
            var y = -1f;
            if (walker.Drawn && views != null)
            {
                if (views.Mode == PocViewMode.PixelView2D && views.PixelView != null && views.PixelView.WalkerRects.TryGetValue(walker, out var rect))
                {
                    x = rect.center.x;
                    y = rect.center.y;
                }
                else if (views.Mode == PocViewMode.ThirdPerson3D && views.ThirdPerson != null && views.ThirdPerson.ViewCamera != null)
                {
                    var p = views.ThirdPerson.ViewCamera.WorldToScreenPoint(walker.transform.position + Vector3.up * 0.3f);
                    if (p.z > 0f) { x = p.x; y = p.y; }
                }
            }
            b.Append(",\"sx\":").Append(x.ToString("0.#", inv)).Append(",\"sy\":").Append(y.ToString("0.#", inv));
        }
    }

    internal static class LifeVectorExtensions
    {
        public static Vector2Int ToVector2IntRounded(this Vector2 v) => new Vector2Int(Mathf.RoundToInt(v.x), Mathf.RoundToInt(v.y));
    }
}
