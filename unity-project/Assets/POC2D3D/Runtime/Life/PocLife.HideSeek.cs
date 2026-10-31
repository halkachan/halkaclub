using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Halka.Game.Player;
using Halka.Game.World;
using UnityEngine;

namespace Halka.Poc2D3D.Life
{
    public enum HideSeekMode { Rin, Len, Both }

    // ver4.0: hide-and-seek with Rin, Len or both, in the field around the house (指示書 13).
    //   1. ﾊﾙｶﾁｬﾝ turns her back and counts (her input and AUTO wait; World Time runs on);
    //   2. the hiders run to their places (fast steps; at most 10 s, then they are simply there);
    //   3. 「もういいよ！」: she looks for them; A (or a click) in front of one finds it;
    //   4. a hint after 40 s, then every 30 s: 「リンは きたのほうに いるみたい！」 (no penalty);
    //   5. everyone found: the time, the records; each goes back to the mode it had.
    // A hiding place: a free cell she can reach in the field, next to a tree, bush, rock, the house
    // or other furniture (North of it first, where it hides them a little), never a door or a stand
    // cell, at least 6 cells from her, the two hiders apart. Nobody hides inside anything.
    // Stopped from the menu (かくれんぼをやめる) or by going indoors; a save during the game keeps
    // their modes from before it and their cells (a load never resumes the game: it starts normal).
    public sealed partial class PocLife
    {
        public const float HideCountMaxSeconds = 10f;
        public const float FirstHintSeconds = 40f;
        public const float NextHintSeconds = 30f;

        private readonly Dictionary<PocWalker, CompanionMode> hideModes = new Dictionary<PocWalker, CompanionMode>();
        private readonly Dictionary<PocWalker, Vector2Int> hideSpots = new Dictionary<PocWalker, Vector2Int>();
        private readonly HashSet<PocWalker> hideFound = new HashSet<PocWalker>();
        private HideSeekMode hideMode;
        private float hideCountStarted;
        private float hideSeekStarted;
        private float hideSeekHintAt;
        private int hideCountShown;
        public bool HideSeekActive => hideModes.Count > 0;
        public bool HideSeekCounting { get; private set; }
        public int HideSeekGames { get; private set; }
        public int HideSeekCleared { get; private set; }
        public int HideSeekStopped { get; private set; }
        public int HideSeekHints { get; private set; }
        public string LastHint { get; private set; } = "";
        public float LastClearSeconds { get; private set; } = -1f;

        public bool CanStartHideSeek(out string why)
        {
            why = null;
            if (HideSeekActive) { why = "いま かくれんぼ ちゅう！"; return false; }
            var world = session.OutdoorWorld;
            var here = session.World == world ? session.OutdoorIndex.RegionAt(session.Cell) : null;
            var field = session.OutdoorIndex != null ? session.OutdoorIndex.PrimaryRegion : null;
            if (here == null || here != field) { why = "かくれんぼは いえのまえの くさはらで しよう！"; return false; }
            return true;
        }

        public bool StartHideSeek(HideSeekMode mode)
        {
            if (!CanStartHideSeek(out var why)) { session.ShowMessage(why); return false; }
            var hiders = mode == HideSeekMode.Rin ? new[] { rin } : mode == HideSeekMode.Len ? new[] { len } : new[] { rin, len };
            var spots = PickHideSpots(hiders.Length);
            if (spots.Count < hiders.Length) { session.ShowMessage("いい かくればしょが ないみたい……。"); return false; }
            hideMode = mode;
            hideModes.Clear();
            hideSpots.Clear();
            hideFound.Clear();
            for (var i = 0; i < hiders.Length; i++)
            {
                var w = hiders[i];
                hideModes[w] = ModeOf(w);
                hideSpots[w] = spots[i];
                paths[w].Clear();
                TryPath(w, spots[i], 10, paths[w]);
            }
            HideSeekGames++;
            HideSeekCounting = true;
            hideCountStarted = lifeTime;
            hideCountShown = 0;
            if (gameplay != null) gameplay.ActivityOpen = true;
            session.SetFacing(FacingDirection.Up);   // her back to them
            session.ShowMessage("かくれんぼ スタート！ うしろを むいて かぞえるね。");
            Changed();
            return true;
        }

        private List<Vector2Int> PickHideSpots(int count)
        {
            var field = session.OutdoorIndex.PrimaryRegion;
            var world = session.OutdoorWorld;
            var her = session.Cell;
            var scored = new List<(int score, Vector2Int cell)>();
            foreach (var c in Reachable)
            {
                if (session.OutdoorIndex.RegionAt(c) != field || ActionCells.Contains(c) || IsDoorOrTransition(c)) continue;
                var distance = Mathf.Abs(c.x - her.x) + Mathf.Abs(c.y - her.y);
                if (distance < 6 || distance > 18 || !CharacterCanEnter(c, null)) continue;
                var score = 0;
                if (world.IsBlocked(c + Vector2Int.down)) score += 3;           // just North of something
                foreach (var d in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, new Vector2Int(1, -1), new Vector2Int(-1, -1) })
                    if (world.IsBlocked(c + d)) score += 1;
                if (score == 0) continue;
                scored.Add((score, c));
            }
            scored.Sort((a, b) => a.score != b.score ? b.score.CompareTo(a.score) : a.cell.x != b.cell.x ? a.cell.x.CompareTo(b.cell.x) : a.cell.y.CompareTo(b.cell.y));
            var top = scored.GetRange(0, Mathf.Min(24, scored.Count));
            var result = new List<Vector2Int>();
            var seed = State.Channel.Seed + HideSeekGames * 131 + (long)lifeTime;
            for (var attempt = 0; attempt < 60 && result.Count < count && top.Count > 0; attempt++)
            {
                var pick = top[(int)(Unit(seed, attempt) * top.Count) % top.Count].cell;
                var apart = true;
                foreach (var r in result)
                    if (Mathf.Abs(r.x - pick.x) + Mathf.Abs(r.y - pick.y) < 4) apart = false;
                if (apart && !result.Contains(pick)) result.Add(pick);
            }
            return result;
        }

        private void TickHideSeek(float dt)
        {
            if (!HideSeekActive) return;
            if (HideSeekCounting)
            {
                var elapsed = lifeTime - hideCountStarted;
                var count = Mathf.FloorToInt(elapsed / 1.5f) + 1;
                if (count > hideCountShown && count <= 6)
                {
                    hideCountShown = count;
                    session.ShowMessage(new[] { "いーち", "にーい", "さーん", "しーい", "ごーお", "もういいかい？" }[count - 1]);
                }
                var allThere = true;
                foreach (var pair in hideSpots)
                    if (pair.Key.Cell != pair.Value || pair.Key.IsMoving) allThere = false;
                if ((allThere && elapsed >= 7.5f) || elapsed >= HideCountMaxSeconds)
                {
                    foreach (var pair in hideSpots)
                        if (pair.Key.Cell != pair.Value && !pair.Key.IsMoving && CharacterCanEnter(pair.Value, pair.Key))
                            pair.Key.Place(pair.Value, FacingDirection.Down);
                    HideSeekCounting = false;
                    if (gameplay != null) gameplay.ActivityOpen = false;
                    hideSeekStarted = lifeTime;
                    hideSeekHintAt = lifeTime + FirstHintSeconds;
                    session.ShowMessage("もういいよ！");
                }
                return;
            }
            if (lifeTime >= hideSeekHintAt)
            {
                hideSeekHintAt = lifeTime + NextHintSeconds;
                ShowHint();
            }
        }

        private void TickHider(PocWalker w)
        {
            if (HideSeekCounting) { StepAlong(w, 0.12f); return; }
            if (hideFound.Contains(w)) return;
            w.Face(FacingDirection.Down);
        }

        private void ShowHint()
        {
            foreach (var pair in hideSpots)
            {
                if (hideFound.Contains(pair.Key)) continue;
                var d = pair.Key.Cell - session.Cell;
                var name = NameOf(pair.Key);
                string where;
                if (Mathf.Abs(d.x) + Mathf.Abs(d.y) <= 3) where = "すぐ ちかくに";
                else
                {
                    var ns = d.y > Mathf.Abs(d.x) / 2 ? "きた" : -d.y > Mathf.Abs(d.x) / 2 ? "みなみ" : "";
                    var ew = d.x > Mathf.Abs(d.y) / 2 ? "ひがし" : -d.x > Mathf.Abs(d.y) / 2 ? "にし" : "";
                    where = ns + ew + "のほうに";
                }
                LastHint = name + "は " + where + " いるみたい！";
                HideSeekHints++;
                session.ShowMessage(LastHint);
                return;
            }
        }

        // A in front of a hider: found.
        private bool TryFind(PocWalker w)
        {
            if (HideSeekCounting || !hideModes.ContainsKey(w) || hideFound.Contains(w)) return false;
            hideFound.Add(w);
            if (w == rin) State.HideSeek.RinFound++;
            else State.HideSeek.LenFound++;
            session.ShowMessage(NameOf(w) + " みつけた！");
            Changed();
            if (hideFound.Count < hideModes.Count) return true;
            var seconds = Mathf.Max(0f, lifeTime - hideSeekStarted);
            LastClearSeconds = seconds;
            var whole = (long)Mathf.Round(seconds);
            var h = State.HideSeek;
            if (hideMode == HideSeekMode.Both)
            {
                h.BothCleared++;
                if (h.BestBothSeconds < 0 || whole < h.BestBothSeconds) h.BestBothSeconds = whole;
            }
            else if (hideMode == HideSeekMode.Rin) { if (h.BestRinSeconds < 0 || whole < h.BestRinSeconds) h.BestRinSeconds = whole; }
            else if (h.BestLenSeconds < 0 || whole < h.BestLenSeconds) h.BestLenSeconds = whole;
            HideSeekCleared++;
            EndHideSeek();
            session.ShowMessage("ぜんいん みつけた！ " + whole.ToString(CultureInfo.InvariantCulture) + "びょう");
            if (saveSystem != null) saveSystem.Write("hide and seek", Time.unscaledTime);
            return true;
        }

        public void StopHideSeek(bool byMenu)
        {
            if (!HideSeekActive) return;
            HideSeekStopped++;
            EndHideSeek();
            if (byMenu)
            {
                if (menu != null) menu.SetOpen(false, Time.unscaledTime);
                session.ShowMessage("かくれんぼを やめた。");
            }
        }

        private void EndHideSeek()
        {
            foreach (var pair in hideModes)
            {
                RecordOf(pair.Key).Mode = pair.Value;
                paths[pair.Key].Clear();
                nextMove[pair.Key] = lifeTime + 1f;
            }
            hideModes.Clear();
            hideSpots.Clear();
            hideFound.Clear();
            if (HideSeekCounting && gameplay != null) gameplay.ActivityOpen = false;
            HideSeekCounting = false;
            Changed();
        }

        private void AppendHideSeekJson(StringBuilder b)
        {
            var inv = CultureInfo.InvariantCulture;
            var h = State.HideSeek;
            b.Append(",\"hide\":{\"active\":").Append(HideSeekActive ? 1 : 0).Append(",\"counting\":").Append(HideSeekCounting ? 1 : 0)
                .Append(",\"mode\":\"").Append(HideSeekActive ? hideMode.ToString() : "").Append("\",\"found\":").Append(hideFound.Count.ToString(inv))
                .Append(",\"hiders\":").Append(hideModes.Count.ToString(inv)).Append(",\"games\":").Append(HideSeekGames.ToString(inv))
                .Append(",\"cleared\":").Append(HideSeekCleared.ToString(inv)).Append(",\"stopped\":").Append(HideSeekStopped.ToString(inv))
                .Append(",\"hints\":").Append(HideSeekHints.ToString(inv)).Append(",\"hint\":\"").Append(LastHint).Append('"')
                .Append(",\"lastSeconds\":").Append(LastClearSeconds.ToString("0.#", inv))
                .Append(",\"rinFound\":").Append(h.RinFound.ToString(inv)).Append(",\"lenFound\":").Append(h.LenFound.ToString(inv))
                .Append(",\"bothCleared\":").Append(h.BothCleared.ToString(inv)).Append(",\"bestRin\":").Append(h.BestRinSeconds.ToString(inv))
                .Append(",\"bestLen\":").Append(h.BestLenSeconds.ToString(inv)).Append(",\"bestBoth\":").Append(h.BestBothSeconds.ToString(inv));
            b.Append(",\"spots\":[");
            var first = true;
            foreach (var pair in hideSpots)
            {
                b.Append(first ? "" : ",").Append("{\"who\":\"").Append(pair.Key == rin ? "rin" : "len").Append("\",\"x\":")
                    .Append(pair.Value.x.ToString(inv)).Append(",\"y\":").Append(pair.Value.y.ToString(inv)).Append(",\"found\":")
                    .Append(hideFound.Contains(pair.Key) ? 1 : 0).Append('}');
                first = false;
            }
            b.Append("]}");
        }
    }
}
