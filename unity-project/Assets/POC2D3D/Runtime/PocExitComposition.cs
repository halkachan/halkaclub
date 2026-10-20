using Halka.Game.Player;
using UnityEngine;

namespace Halka.Poc2D3D
{
    // Phase 4: the composition shown right after leaving a building in 3D. Nothing here is
    // specific to the house: the building is whatever object stands on the door cell of the last
    // transition, framed from the side the player came out on, the player in front of it.
    public static class PocExitComposition
    {
        public const float Pitch = 22f;
        public const float FocusTowardBuilding = 0.45f;
        public const float MinDistance = 3f;
        public const float MaxDistance = 7.5f;

        public static bool TryCompose(SharedWorldSession session, PocWorldPresenter presenter, Camera camera,
            out PocCameraPose pose)
        {
            pose = default;
            if (session == null || presenter == null || camera == null) return false;
            var transition = session.LastTransition;
            if (transition.Kind != SharedTransitionKind.ExitedBuilding) return false;
            PocObjectView building = null;
            foreach (var view in presenter.Views)
                if (view != null && !view.IsWall && view.RootCell == transition.DoorCell)
                {
                    building = view;
                    break;
                }
            if (building == null || !TryBounds(building, out var bounds)) return false;

            // Look at the building from where the player stands: heading = toward the building.
            var heading = GridSpace3D.Opposite(transition.ArrivalFacing);
            var yaw = GridSpace3D.FacingYaw(heading);
            var player = GridSpace3D.ToWorld(session.Position, 0.45f);
            var centre = bounds.center;
            centre.y = Mathf.Min(bounds.center.y, bounds.min.y + bounds.size.y * 0.42f);
            var focus = Vector3.Lerp(player, centre, FocusTowardBuilding);

            // Far enough for the building's bounding sphere (and the player in front of it) to fit
            // in the narrower of the two field-of-view angles.
            var vertical = camera.fieldOfView > 1f ? camera.fieldOfView : 42f;
            var horizontal = 2f * Mathf.Atan(Mathf.Tan(vertical * 0.5f * Mathf.Deg2Rad) * Mathf.Max(0.1f, camera.aspect)) * Mathf.Rad2Deg;
            var half = Mathf.Min(vertical, horizontal) * 0.5f * Mathf.Deg2Rad;
            var radius = Mathf.Max(bounds.extents.magnitude, Vector3.Distance(focus, player) + 0.6f);
            var distance = Mathf.Clamp(radius / Mathf.Sin(half) * 1.05f, MinDistance, MaxDistance);
            pose = new PocCameraPose { Focus = focus, Yaw = yaw, Pitch = Pitch, Distance = distance, Heading = heading };
            return true;
        }

        private static bool TryBounds(PocObjectView view, out Bounds bounds)
        {
            bounds = new Bounds();
            var found = false;
            foreach (var item in view.Renderers)
            {
                if (item == null) continue;
                if (!found) bounds = item.bounds;
                else bounds.Encapsulate(item.bounds);
                found = true;
            }
            return found;
        }
    }
}
