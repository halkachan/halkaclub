using System;
using Halka.Game.Input;
using Halka.Game.Player;
using Halka.Game.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Halka.Game.Editor
{
    public static class Version11Checks
    {
        [MenuItem("HALKA/Validate ver1.1")]
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/FirstDay.unity");
            var world = UnityEngine.Object.FindFirstObjectByType<GridWorld2D>();
            Check(world != null && Mathf.Approximately(world.CellSize, 1f), "one-unit cells");
            Check(UnityEngine.Object.FindObjectsByType<GridObstacle>(FindObjectsSortMode.None).Length == 1,
                "exactly one obstacle");
            Check(!world.CanEnter(new Vector2Int(1, 1)), "stone blocks its cell");
            Check(world.CanEnter(new Vector2Int(2, 1)), "adjacent cell is open");
            Check(!world.CanEnter(new Vector2Int(6, 0)) && !world.CanEnter(new Vector2Int(0, -4)),
                "field bounds block movement");

            var motion = new GridStepMotion(Vector2Int.zero);
            Check(!motion.TryBegin(new Vector2Int(1, 1), world.CanEnter,
                PlayerMover.DefaultStepSeconds), "diagonal rejected");
            Check(motion.TryBegin(Vector2Int.right, world.CanEnter,
                PlayerMover.DefaultStepSeconds), "right step begins");
            Check(!motion.TryBegin(Vector2Int.up, world.CanEnter,
                PlayerMover.DefaultStepSeconds), "cannot turn during step");
            motion.Advance(PlayerMover.DefaultStepSeconds * 0.5f);
            Check(motion.Cell == Vector2Int.zero && motion.IsMoving &&
                Mathf.Approximately(motion.Position.x, 0.5f), "logical cell stays exact during interpolation");
            motion.Advance(PlayerMover.DefaultStepSeconds * 0.5f);
            Check(!motion.IsMoving && motion.Cell == Vector2Int.right &&
                motion.Position == Vector2.right, "step ends exactly on adjacent cell");
            Check(!motion.TryBegin(Vector2Int.up, world.CanEnter,
                PlayerMover.DefaultStepSeconds), "stone stops entry");
            Check(motion.TryBegin(Vector2Int.right, world.CanEnter,
                PlayerMover.DefaultStepSeconds), "another step begins after arrival");
            motion.Advance(PlayerMover.DefaultStepSeconds);
            Check(motion.Cell == new Vector2Int(2, 0), "second step reaches next cell");
            Check(motion.TryBegin(Vector2Int.up, world.CanEnter,
                PlayerMover.DefaultStepSeconds), "direction changes after arrival");

            Check(CardinalInput.Choose(true, false, false, true, Vector2Int.zero) == Vector2Int.up,
                "simultaneous directions are cardinal");
            Check(CardinalInput.Choose(false, false, true, false, Vector2Int.zero) == Vector2Int.left,
                "keyboard left");
            Check(CardinalInput.Choose(false, true, false, false, Vector2Int.zero) == Vector2Int.down,
                "keyboard down");
            Check(CardinalInput.Choose(false, false, false, true, Vector2Int.zero) == Vector2Int.right,
                "keyboard right");
            Check(CardinalInput.Choose(false, false, false, false, Vector2Int.up) == Vector2Int.up,
                "touch direction");

            var tap = new PointerTap();
            Check(!tap.TryBegin(1, Vector2.zero, true, 0f), "D-pad press blocks world tap");
            Check(tap.TryBegin(2, Vector2.zero, false, 0f), "world tap starts");
            Check(!tap.TryBegin(3, Vector2.zero, false, 0f), "second finger cannot replace tap");
            Check(!tap.End(3, Vector2.zero, false, 0.1f, 24f, 0.4f), "wrong finger ignored");
            Check(tap.End(2, Vector2.zero, false, 0.1f, 24f, 0.4f), "short tap interacts");
            Check(tap.TryBegin(4, Vector2.zero, false, 1f), "next tap starts");
            tap.Move(4, new Vector2(50f, 0f), 24f);
            Check(!tap.End(4, Vector2.zero, false, 1.1f, 24f, 0.4f),
                "drag never becomes a tap even after returning");
            Check(tap.TryBegin(5, Vector2.zero, false, 2f), "cancel test starts");
            tap.Cancel(5);
            Check(tap.ActiveFingerId == -1, "cancel releases finger");
            Debug.Log("HALKA ver1.1 checks passed (grid, stone, cardinal input, pointer tap).");
        }

        private static void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException("ver1.1 check failed: " + label);
        }
    }
}
