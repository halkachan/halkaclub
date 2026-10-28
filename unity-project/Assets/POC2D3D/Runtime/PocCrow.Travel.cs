using System.Collections.Generic;
using Halka.Game.Player;
using Halka.Game.World;
using UnityEngine;

namespace Halka.Poc2D3D
{
    // ver3.5 そらをとぶ: the one crow flies her to a place she has found. During the flight the crow is
    // held: no wander, perch, landing or companion decision runs, and its logical cell does not change.
    // PocFastTravel only moves where it is drawn (ShowTravelFlight: flying, in both views, the 2D game's
    // flight frames and the Blender crow's flight pose): to her, up with her, and down at the
    // destination. After the landing it stands beside her. The companion walks on with her; the free
    // crow goes back to its own life a moment later (a tree of its home region, as after あそんでおいで).
    // The mode never changes here. A flight that is given up puts it back exactly as it was.
    public sealed partial class PocCrow
    {
        public const float FreeReturnSeconds = 1.2f;

        private float travelReturnAt = -1f;
        private Vector3 travelHeldPosition;
        private CrowActivity travelHeldActivity;
        private Vector3 travelHeldLegStart;
        private Vector3 travelHeldLegEnd;
        private bool travelShown;

        public bool TravelHeld { get; private set; }
        // Not drawn in either view (kept for a host that draws the crow itself; ver3.5 never sets it).
        public bool HiddenForTravel { get; set; }
        public int TravelLandings { get; private set; }
        public bool TravelReturnPending => travelReturnAt >= 0f;
        public Vector2Int TravelHeldCell { get; private set; }

        public void HoldForTravel()
        {
            TravelHeld = true;
            ChoiceOpen = false;
            travelReturnAt = -1f;
            travelHeldPosition = transform.position;
            travelHeldActivity = activity;
            travelHeldLegStart = legStart;
            travelHeldLegEnd = legEnd;
            TravelHeldCell = Cell;
            travelShown = false;
        }

        // Presentation only while held: drawn flying at this point, heading the way it moved.
        public void ShowTravelFlight(Vector3 position, float now)
        {
            if (!TravelHeld) return;
            var from = transform.position;
            if ((position - from).sqrMagnitude > 0.0004f) { legStart = from; legEnd = position; }
            transform.position = position;
            activity = CrowActivity.Flying;
            travelShown = true;
            SetPointerInteraction(false);
            RefreshAirVisual(now);
        }

        // The flight is over. land: she is at the destination and the crow (put beside her behind the
        // sky by LandBeside) stands there; else it is back where and as it was.
        public void ReleaseAfterTravel(bool land, float now)
        {
            TravelHeld = false;
            HiddenForTravel = false;
            lastTickAt = now;
            if (land && isActiveAndEnabled && PlayerOutdoors)
            {
                PlaceOnGround(Cell, FacingToward(Cell, session.Cell), now);
                if (!Following) travelReturnAt = now + FreeReturnSeconds;
                return;
            }
            if (!travelShown) return;
            transform.position = travelHeldPosition;
            activity = travelHeldActivity;
            legStart = travelHeldLegStart;
            legEnd = travelHeldLegEnd;
            if (activity == CrowActivity.GroundIdle || activity == CrowActivity.GroundWalk) { SetPointerInteraction(true); RefreshVisual(); }
            else RefreshAirVisual(now);
        }

        // Behind her first (her facing is where she will walk), then beside her, then the nearest
        // free cell around her; never her cell, a door, or the two cells ahead of her.
        public bool LandBeside(float now)
        {
            ResetCompanion(now);
            placed = true;
            WaitingOutside = false;
            TravelLandings++;
            var target = session.Cell;
            foreach (var c in LandingCandidates(target))
                if (c != target && CanStand(c) && !InHerWay(c, target))
                {
                    PlaceOnGround(c, FacingToward(c, target), now);
                    return true;
                }
            Relocate(now);
            return false;
        }

        // Where it stands once landed (the fast travel brings it down there).
        public Vector3 GroundPoint => CellToPosition(Cell);

        private IEnumerable<Vector2Int> LandingCandidates(Vector2Int target)
        {
            var side = new Vector2Int(heading.y, -heading.x);
            yield return target - heading;
            yield return target - heading + side;
            yield return target - heading - side;
            yield return target + side;
            yield return target - side;
            yield return target - heading * 2;
            for (var radius = 2; radius <= 6; radius++)
                foreach (var c in Ring(target, radius))
                    yield return c;
        }

        // The free crow, a moment after landing beside her: off to its own trees (FlyHome: from another
        // region it flies back to the nearest tree of its home, at home to a tree near it).
        public void WatchTravelReturn(float now)
        {
            if (travelReturnAt < 0f || now < travelReturnAt) return;
            if (Following) { travelReturnAt = -1f; return; }
            if (ChoiceOpen || activity != CrowActivity.GroundIdle || IsMoving) return;
            travelReturnAt = -1f;
            FlyHome(now);
        }
    }
}
