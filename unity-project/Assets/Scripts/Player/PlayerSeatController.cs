using Halka.Game.World;
using UnityEngine;

namespace Halka.Game.Player
{
    // A seat changes only the artwork. The player keeps the action-point cell.
    public sealed class PlayerSeatController : MonoBehaviour
    {
        public const string BenchPoseKey = "bench_sit";
        public const float ArtworkAboveSeatCenter = 10f / 64f;

        [SerializeField] private PlayerMover mover;
        [SerializeField] private GridWorld2D world;
        [SerializeField] private Transform artwork;

        private Transform seat;
        private bool seated;
        private Vector3 standingArtworkPosition;

        public bool IsSeated => seated;
        public Transform ActiveSeat => seat;

        public bool TrySit(Transform target, string poseKey)
        {
            if (target == null || poseKey != BenchPoseKey || IsSeated || mover == null ||
                world == null || artwork == null || mover.IsMoving) return false;
            standingArtworkPosition = artwork.localPosition;
            seat = target;
            seated = true;
            mover.SetMovementLocked(true);
            artwork.position = world.CellToWorld(mover.Cell + mover.Facing.ToVector()) +
                Vector3.up * ArtworkAboveSeatCenter;
            return true;
        }

        public bool Stand()
        {
            if (!IsSeated) return false;
            seated = false;
            seat = null;
            if (artwork != null) artwork.localPosition = standingArtworkPosition;
            if (mover != null) mover.SetMovementLocked(false);
            return true;
        }

        public void StandIfActive(Transform target)
        {
            if (IsSeated && seat == target) Stand();
        }

        private void OnDisable() => Stand();
    }
}
