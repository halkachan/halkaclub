using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Halka.Game.Player;
using Halka.Game.UI;
using UnityEngine;

namespace Halka.Poc2D3D.Life
{
    // ver4.0: Rin and Len (指示書 11-12), each with its own mode:
    //   Free   (あそんでおいで): they potter about the field around the house (never far, so she can
    //          always find them); from far away they first walk home;
    //   Follow (ついてきて):    they keep close behind her anywhere outdoors, Rin and Len each on their
    //          own, together, and with the crow (nobody shares a cell; in a narrow place they wait);
    //   Wait:  while she is indoors, a follower waits beside the door (not drawn) and follows again
    //          when she comes out (the crow's rule since ver3.4);
    //   hide-and-seek (PocLife.HideSeek.cs) holds them still where they hide.
    // そらをとぶ: a follower lands beside her; a free one stays where it is. Saved: each one's mode
    // (Follow while waiting) and its cell.
    public sealed partial class PocLife
    {
        public const string RinId = "rin";
        public const string LenId = "len";
        public const string RinName = "リン";
        public const string LenName = "レン";
        public const string ChoiceFollow = "ついてきて";
        public const string ChoicePlay = "あそんでおいで";
        public const string ChoiceHide = "かくれんぼ";

        private PocWalker rin;
        private PocWalker len;
        private Vector2Int? entryCell;
        private Vector2Int homeCenter;
        private readonly List<Vector2Int> homeCells = new List<Vector2Int>();
        private readonly Dictionary<PocWalker, List<Vector2Int>> paths = new Dictionary<PocWalker, List<Vector2Int>>();
        private readonly Dictionary<PocWalker, float> nextMove = new Dictionary<PocWalker, float>();
        public PocWalker Rin => rin;
        public PocWalker Len => len;
        public int CompanionTeleports { get; private set; }
        public int CompanionLandings { get; private set; }

        public LifeSaveBlock.CompanionRecord RecordOf(PocWalker w) => w == rin ? State.Rin : State.Len;
        public CompanionMode ModeOf(PocWalker w) => RecordOf(w).Mode;
        public bool Waiting(PocWalker w) => ModeOf(w) == CompanionMode.Follow && session.World != session.OutdoorWorld;
        public string NameOf(PocWalker w) => w == rin ? RinName : LenName;

        private void BuildCompanions()
        {
            var world = session.OutdoorWorld;
            if (world == null) return;
            entryCell = world.TryGetHouseDoor(out var door) ? door + Vector2Int.down : (Vector2Int?)null;
            // Home: the field's open ground just North-East of where she starts (the crossroads by the
            // house), inside first_field (the region the game starts in).
            var field = session.OutdoorIndex.PrimaryRegion;
            var start = field.Map.TryGetEntitySpawn("player_main", out var spawn) ? field.ToWorld(spawn.Cell) : Vector2Int.zero;
            homeCenter = start + new Vector2Int(4, 2);
            if (!Reachable.Contains(homeCenter) || session.OutdoorIndex.RegionAt(homeCenter) != field)
                foreach (var c in Reachable)
                    if (session.OutdoorIndex.RegionAt(c) == field && (Mathf.Abs(c.x - start.x - 4) + Mathf.Abs(c.y - start.y - 2)) <
                        (Mathf.Abs(homeCenter.x - start.x - 4) + Mathf.Abs(homeCenter.y - start.y - 2) + (Reachable.Contains(homeCenter) ? 0 : 99)))
                        homeCenter = c;
            homeCells.Clear();
            foreach (var c in Reachable)
                if (Mathf.Abs(c.x - homeCenter.x) <= 6 && Mathf.Abs(c.y - homeCenter.y) <= 5 &&
                    session.OutdoorIndex.RegionAt(c) == field && !ActionCells.Contains(c) && !IsDoorOrTransition(c))
                    homeCells.Add(c);
            homeCells.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            rin = MakeCompanion(RinId);
            len = MakeCompanion(LenId);
            RestoreCompanions();
        }

        private PocWalker MakeCompanion(string id)
        {
            var look = Look(id);
            var go = new GameObject(id == RinId ? "Rin" : "Len");
            go.transform.SetParent(transform, false);
            var walker = go.AddComponent<PocWalker>();
            walker.Setup(id, id, look?.model, look?.material, look != null ? look.scale : 1f, look?.sprites,
                look != null ? look.outlineId : PocOutlineTag.CharacterId);
            if (look != null) walker.SpriteOffsetNorth = look.spriteOffsetNorth;
            walker.StepSeconds = 0.22f;
            paths[walker] = new List<Vector2Int>();
            nextMove[walker] = 0f;
            return walker;
        }

        // Each to its saved cell when that is still a free reachable cell, else a free cell at home.
        private void RestoreCompanions()
        {
            if (rin == null) return;
            foreach (var w in new[] { rin, len })
            {
                var record = RecordOf(w);
                w.Remove();
                var cell = new Vector2Int(record.X, record.Y);
                var ok = record.HasPlace && Reachable.Contains(cell) && CharacterCanEnter(cell, w);
                if (!ok)
                {
                    if (record.HasPlace) Repairs++;
                    cell = HomeSpot(w == rin ? 0 : 1);
                }
                w.Place(cell, FacingDirection.Down);
                paths[w].Clear();
            }
            CompanionsMapChanged(session.World);
        }

        private Vector2Int HomeSpot(int which)
        {
            var preferred = homeCenter + (which == 0 ? new Vector2Int(-1, 0) : new Vector2Int(1, 0));
            if (TryNearestFree(preferred, 6, out var cell)) return cell;
            foreach (var c in homeCells)
                if (CharacterCanEnter(c, null)) return c;
            return preferred;
        }

        private void CaptureCompanions(LifeSaveBlock block)
        {
            if (rin == null) return;
            foreach (var w in new[] { rin, len })
            {
                var target = w == rin ? block.Rin : block.Len;
                var mode = HideSeekActive && hideModes.TryGetValue(w, out var before) ? before : ModeOf(w);
                target.Mode = mode == CompanionMode.Wait ? CompanionMode.Follow : mode;
                target.HasPlace = true;
                target.X = w.Cell.x;
                target.Y = w.Cell.y;
            }
        }

        public void SetMode(PocWalker w, CompanionMode mode)
        {
            RecordOf(w).Mode = mode;
            paths[w].Clear();
            nextMove[w] = lifeTime + 0.3f;
            Changed();
        }

        // Indoors: followers wait beside the door (hidden); back outdoors they show again there.
        private void CompanionsMapChanged(Halka.Poc2D3D.SharedWorldModel world)
        {
            if (rin == null) return;
            var outdoors = world == session.OutdoorWorld;
            foreach (var w in new[] { rin, len })
            {
                w.HiddenByGame = !outdoors;
                if (!outdoors && ModeOf(w) == CompanionMode.Follow && entryCell.HasValue)
                {
                    var side = w == rin ? Vector2Int.left : Vector2Int.right;
                    var spot = entryCell.Value + side;
                    if (!CharacterCanEnter(spot, w) && !TryNearestFree(entryCell.Value + side * 2, 3, out spot)) spot = w.Cell;
                    if (spot != entryCell.Value) w.Place(spot, side == Vector2Int.left ? FacingDirection.Right : FacingDirection.Left);
                    paths[w].Clear();
                }
            }
        }

        // そらをとぶ: followers land beside her (behind first), free ones stay where they are.
        private void CompanionsAfterTravel()
        {
            if (rin == null || session.World != session.OutdoorWorld) return;
            foreach (var w in new[] { rin, len })
            {
                if (ModeOf(w) != CompanionMode.Follow) continue;
                if (TryFollowSpot(w, out var spot))
                {
                    w.Place(spot, session.Facing);
                    paths[w].Clear();
                    CompanionLandings++;
                }
            }
            Changed();
        }

        // A free cell 1-3 steps from her, behind her first, on her side of a wall (reachable).
        private bool TryFollowSpot(PocWalker w, out Vector2Int spot)
        {
            foreach (var c in FollowSpots(w)) { spot = c; return true; }
            return TryNearestFree(her: session.Cell, w, out spot);
        }

        // The places behind her in this one's order (free, reachable cells only).
        private IEnumerable<Vector2Int> FollowSpots(PocWalker w)
        {
            var her = session.Cell;
            var back = -session.Facing.ToVector();
            var side = new Vector2Int(back.y, back.x);
            var order = w == rin
                ? new[] { back, back + side, side, back * 2, back * 2 + side, side * 2, -side, back - side }
                : new[] { back - side, -side, back * 2, back, back * 2 - side, -side * 2, side, back + side };
            foreach (var d in order)
            {
                var c = her + d;
                if (Reachable.Contains(c) && CharacterCanEnter(c, w)) yield return c;
            }
        }

        private bool TryNearestFree(Vector2Int her, PocWalker w, out Vector2Int spot) =>
            TryNearestFree(her - session.Facing.ToVector(), 3, out spot) && spot != her;

        // A path to the first place behind her that can be walked to: a place boxed in by the others
        // (a tree, her, the crow and the other one round it) is passed over, never waited for.
        private bool FollowPath(PocWalker w, List<Vector2Int> path)
        {
            foreach (var spot in FollowSpots(w))
            {
                if (spot == w.Cell) return false;
                if (TryPath(w, spot, 6, path) || TryPath(w, spot, 24, path)) return true;
            }
            return TryNearestFree(session.Cell, w, out var free) && free != w.Cell && (TryPath(w, free, 6, path) || TryPath(w, free, 24, path));
        }

        private void TickCompanions(float dt)
        {
            if (rin == null) return;
            var outdoors = session.World == session.OutdoorWorld;
            foreach (var w in new[] { rin, len })
            {
                w.Advance(dt);
                if (w.IsMoving) continue;
                if (HideSeekActive && hideModes.ContainsKey(w)) { TickHider(w); continue; }
                var mode = ModeOf(w);
                if (mode == CompanionMode.Follow)
                {
                    if (outdoors) TickFollow(w);
                }
                else TickFree(w);
            }
        }

        // She keeps walking into Rin, Len or a passer-by (as long as the crow waits, 0.3 s): it steps
        // aside (or on), so a character standing in a narrow way (a door, a path) never shuts her in.
        // A hider stays where it is (she finds it with A).
        public int WalkerYields { get; private set; }

        private void YieldToPush()
        {
            if (session.World != session.OutdoorWorld || session.PushSeconds < PocCrow.PushYieldSeconds) return;
            foreach (var w in PocWalker.Active)
            {
                if (w == null || !w.Present || w.HiddenByGame || w.IsMoving || w.Cell != session.PushedCell) continue;
                if (HideSeekActive && hideModes.ContainsKey(w)) return;
                var push = session.PushedCell - session.Cell;
                var side = new Vector2Int(push.y, -push.x);
                foreach (var d in new[] { side, -side, push })
                {
                    if (!w.TryStep(d, c => c != session.Cell && CharacterCanEnter(c, w))) continue;
                    WalkerYields++;
                    if (paths.TryGetValue(w, out var path)) path.Clear();
                    if (nextMove.ContainsKey(w)) nextMove[w] = lifeTime + 1f;
                    return;
                }
                return;
            }
        }

        private void TickFollow(PocWalker w)
        {
            var her = session.Cell;
            var distance = Mathf.Abs(her.x - w.Cell.x) + Mathf.Abs(her.y - w.Cell.y);
            var path = paths[w];
            // Far away (a flight, a long run): it catches up out of sight (never before her eyes).
            if (distance > 14 && !w.Drawn && TryFollowSpot(w, out var catchUp))
            {
                w.Place(catchUp, session.Facing);
                path.Clear();
                CompanionTeleports++;
                return;
            }
            // Close enough: it waits where it is (also while she takes a step toward it, so she can
            // walk up to it and talk); it moves on only once she is further away.
            var close = w == rin ? 2 : 3;
            if (distance <= close)
            {
                path.Clear();
                var d = her - w.Cell;
                w.Face(Mathf.Abs(d.x) >= Mathf.Abs(d.y) ? (d.x > 0 ? FacingDirection.Right : FacingDirection.Left)
                    : (d.y > 0 ? FacingDirection.Up : FacingDirection.Down));
                return;
            }
            if (distance <= 1) return;
            if (path.Count == 0 || lifeTime >= nextMove[w])
            {
                nextMove[w] = lifeTime + 0.5f;
                // the first place behind her it can walk to (a small search box first; around a wood or a
                // pond the way may go farther round)
                FollowPath(w, path);
            }
            StepAlong(w, distance > 5 ? 0.15f : 0.2f);
        }

        private void TickFree(PocWalker w)
        {
            var path = paths[w];
            // She is next to it (to talk): it stops and looks at her.
            if (session.World == session.OutdoorWorld && path.Count == 0)
            {
                var d = session.Cell - w.Cell;
                if (Mathf.Abs(d.x) + Mathf.Abs(d.y) <= 2)
                {
                    w.Face(Mathf.Abs(d.x) >= Mathf.Abs(d.y) ? (d.x > 0 ? FacingDirection.Right : FacingDirection.Left)
                        : (d.y > 0 ? FacingDirection.Up : FacingDirection.Down));
                    nextMove[w] = Mathf.Max(nextMove[w], lifeTime + 1.5f);
                    return;
                }
            }
            var dist = Mathf.Abs(w.Cell.x - homeCenter.x) + Mathf.Abs(w.Cell.y - homeCenter.y);
            if (dist > 9)
            {
                // home first (out of sight a long way is skipped)
                if (!w.Drawn && dist > 16)
                {
                    w.Place(HomeSpot(w == rin ? 0 : 1), FacingDirection.Down);
                    path.Clear();
                    return;
                }
                if (path.Count == 0 && lifeTime >= nextMove[w])
                {
                    nextMove[w] = lifeTime + 1f;
                    var goal = HomeSpot(w == rin ? 0 : 1);
                    TryPath(w, goal, 8, path);
                    if (path.Count > 12) path.RemoveRange(12, path.Count - 12);
                }
                StepAlong(w, 0.22f);
                return;
            }
            if (path.Count > 0) { StepAlong(w, 0.26f); return; }
            if (lifeTime < nextMove[w]) return;
            var roll = Unit(w.StepsTaken + (w == rin ? 17 : 31), (long)(lifeTime * 7f));
            nextMove[w] = lifeTime + 1.5f + (float)roll * 3.5f;
            if (roll < 0.25) { w.Face((FacingDirection)((int)(roll * 1000) % 4)); return; }
            var dirs = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            var dir = dirs[(int)(roll * 997) % 4];
            var to = w.Cell + dir;
            if (homeCells.Contains(to)) w.TryStep(dir, c => CharacterCanEnter(c, w), 0.26f);
        }

        private void StepAlong(PocWalker w, float seconds)
        {
            var path = paths[w];
            if (path.Count == 0) return;
            var next = path[0];
            if (Mathf.Abs(next.x - w.Cell.x) + Mathf.Abs(next.y - w.Cell.y) != 1) { path.Clear(); return; }
            if (w.TryStep(next - w.Cell, c => CharacterCanEnter(c, w), seconds)) path.RemoveAt(0);
            else
            {
                // someone in the way: wait a moment, then look for another way
                path.Clear();
                nextMove[w] = lifeTime + 0.4f;
            }
        }

        // ---------------------------------------------------------------- talking to them

        private CompanionPage companionPage;

        private bool OpenCompanionChoices(PocWalker w)
        {
            if (menu == null) return false;
            companionPage ??= new CompanionPage(this);
            return companionPage.Open(w, Time.unscaledTime);
        }

        private sealed class CompanionPage : IGameMenuPage
        {
            private readonly PocLife owner;
            private readonly List<string> lines = new List<string>();
            private readonly List<string> buttons = new List<string>();
            private PocWalker who;
            private bool picking;   // かくれんぼ: who hides

            public CompanionPage(PocLife life) => owner = life;
            public string Title => who != null ? owner.NameOf(who) : "";
            public IReadOnlyList<string> Lines => lines;
            public IReadOnlyList<string> Buttons => buttons;
            public int Opens { get; private set; }

            public bool Open(PocWalker walker, float now)
            {
                who = walker;
                picking = false;
                lines.Clear();
                buttons.Clear();
                lines.Add(owner.NameOf(walker) + "「" + (walker == owner.rin ? "なあに？" : "なに？") + "」");
                buttons.Add(owner.ModeOf(walker) == CompanionMode.Follow ? ChoicePlay : ChoiceFollow);
                buttons.Add(ChoiceHide);
                buttons.Add(CloseLabel);
                if (owner.hud != null) owner.hud.ClearMessage();
                if (owner.menu.IsOpen) owner.menu.SetOpen(false, now);
                owner.menu.OpenPage(this, now);
                if (owner.menu.Page == this) Opens++;
                return owner.menu.Page == this;
            }

            public void Press(int index, float now)
            {
                var label = index >= 0 && index < buttons.Count ? buttons[index] : CloseLabel;
                if (!picking)
                {
                    if (label == ChoiceFollow || label == ChoicePlay)
                    {
                        owner.SetMode(who, label == ChoiceFollow ? CompanionMode.Follow : CompanionMode.Free);
                        owner.menu.SetOpen(false, now);
                        owner.session.ShowMessage(owner.NameOf(who) + "「" + (label == ChoiceFollow ? "いっしょに いく！" : "あそんでくる！") + "」");
                        return;
                    }
                    if (label == ChoiceHide)
                    {
                        if (!owner.CanStartHideSeek(out var why))
                        {
                            owner.menu.SetOpen(false, now);
                            owner.session.ShowMessage(why);
                            return;
                        }
                        picking = true;
                        lines.Clear();
                        lines.Add("だれを さがす？");
                        buttons.Clear();
                        buttons.Add(RinName);
                        buttons.Add(LenName);
                        buttons.Add(RinName + "と" + LenName);
                        buttons.Add(CloseLabel);
                        return;
                    }
                    owner.menu.SetOpen(false, now);
                    return;
                }
                owner.menu.SetOpen(false, now);
                if (label == RinName) owner.StartHideSeek(HideSeekMode.Rin);
                else if (label == LenName) owner.StartHideSeek(HideSeekMode.Len);
                else if (label == RinName + "と" + LenName) owner.StartHideSeek(HideSeekMode.Both);
            }
        }

        private void AppendCompanionJson(StringBuilder b)
        {
            var inv = CultureInfo.InvariantCulture;
            foreach (var w in new[] { rin, len })
            {
                if (w == null) continue;
                var key = w == rin ? "rin" : "len";
                b.Append(",\"").Append(key).Append("\":{\"x\":").Append(w.Cell.x.ToString(inv)).Append(",\"y\":").Append(w.Cell.y.ToString(inv))
                    .Append(",\"mode\":\"").Append(LifeSaveBlock.ModeName(ModeOf(w))).Append("\",\"waiting\":").Append(Waiting(w) ? 1 : 0)
                    .Append(",\"drawn\":").Append(w.Drawn ? 1 : 0).Append(",\"model\":").Append(w.ModelInstance != null ? 1 : 0)
                    .Append(",\"moving\":").Append(w.IsMoving ? 1 : 0).Append(",\"steps\":").Append(w.StepsTaken.ToString(inv))
                    .Append(",\"facing\":\"").Append(w.Facing.ToString()).Append('"')
                    .Append(",\"region\":\"").Append(session.OutdoorIndex != null && session.OutdoorIndex.RegionAt(w.Cell) != null ? session.OutdoorIndex.RegionAt(w.Cell).MapId : "").Append('"');
                AppendScreen(b, w);
                b.Append('}');
            }
            b.Append(",\"companionTeleports\":").Append(CompanionTeleports.ToString(inv)).Append(",\"companionLandings\":").Append(CompanionLandings.ToString(inv));
            b.Append(",\"companionPageOpens\":").Append(companionPage != null ? companionPage.Opens.ToString(inv) : "0");
            AppendHideSeekJson(b);
        }
    }
}
