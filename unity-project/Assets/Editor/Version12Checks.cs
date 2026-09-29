using System;
using System.Reflection;
using Halka.Game.Input;
using Halka.Game.Interaction;
using Halka.Game.Player;
using Halka.Game.UI;
using Halka.Game.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Halka.Game.Editor
{
    public static class Version12Checks
    {
        [MenuItem("HALKA/Validate ver1.2")]
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/FirstDay.unity");
            var world = UnityEngine.Object.FindFirstObjectByType<GridWorld2D>();
            Check(world != null && GridWorld2D.TilePixels == 64 &&
                Mathf.Approximately(world.CellSize, GridWorld2D.TileWorldSize), "64-pixel tile units");
            Check(UnityEngine.Object.FindObjectsByType<GridObstacle>(FindObjectsSortMode.None).Length == 1,
                "exactly one obstacle");
            var stone = UnityEngine.Object.FindFirstObjectByType<GridObstacle>();
            var stoneSprite = stone.GetComponent<SpriteRenderer>().sprite;
            var stoneCollider = stone.GetComponent<BoxCollider2D>();
            Check(stoneSprite.texture.width == 32 && stoneSprite.texture.height == 32 &&
                Mathf.Approximately(stoneSprite.pixelsPerUnit, GridWorld2D.TilePixels) &&
                stone.transform.localScale == Vector3.one, "supplied 32-pixel stone at tile scale");
            Check(stoneCollider != null &&
                stoneCollider.bounds.size.x < stone.GetComponent<SpriteRenderer>().bounds.size.x &&
                stoneCollider.bounds.size.y < stone.GetComponent<SpriteRenderer>().bounds.size.y,
                "stone collider excludes transparent border");
            Check(stoneCollider.OverlapPoint(stone.transform.position) &&
                !stoneCollider.OverlapPoint(stone.transform.position + Vector3.up * 0.23f),
                "stone art can be targeted without hitting the transparent top margin");
            Check(!world.CanEnter(new Vector2Int(1, 1)), "stone blocks its cell");
            Check(world.CanEnter(new Vector2Int(2, 1)), "adjacent cell is open");
            Check(!world.CanEnter(new Vector2Int(6, 0)) && !world.CanEnter(new Vector2Int(0, -4)),
                "field bounds block movement");
            Check(world.HasObstacle(new Vector2Int(1, 1)) &&
                !world.HasObstacle(new Vector2Int(0, 1)), "obstacle remains in one tile");
            Check(UnityEngine.Object.FindFirstObjectByType<PlayerVisualAnchor2D>() != null &&
                UnityEngine.Object.FindFirstObjectByType<PlayerMover>().GetComponentInChildren<SpriteRenderer>() != null,
                "player artwork is separate from logical grid root");

            var examine = stone.GetComponent<ExamineInteractable>();
            Check(examine != null && new SerializedObject(examine).FindProperty("message").stringValue == "いし。",
                "one generic examine component with exact message");
            var hud = UnityEngine.Object.FindFirstObjectByType<GameHud>();
            Check(new SerializedObject(hud).FindProperty("messageFont").objectReferenceValue != null,
                "Japanese message font is assigned");
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerMover>();
            typeof(PlayerMover).GetField("motion", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(player, new GridStepMotion(new Vector2Int(0, 1)));
            examine.Interact();
            var activeMessage = (TimedMessage)typeof(GameHud)
                .GetField("message", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(hud);
            Check(activeMessage.TextAt(Time.unscaledTime) == "いし。",
                "adjacent stone invokes shared message HUD");
            foreach (var direction in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                Check(ExamineInteractable.IsInRange(new Vector2Int(1, 1) + direction,
                    false, new Vector2Int(1, 1)), "cardinal adjacent interaction");
            Check(!ExamineInteractable.IsInRange(new Vector2Int(2, 2), false, new Vector2Int(1, 1)) &&
                !ExamineInteractable.IsInRange(new Vector2Int(3, 1), false, new Vector2Int(1, 1)) &&
                !ExamineInteractable.IsInRange(new Vector2Int(1, 0), true, new Vector2Int(1, 1)),
                "diagonal, far, and moving interactions are rejected");

            var message = new TimedMessage();
            message.Show("いし。", 1f, 2f);
            Check(message.TextAt(1.1f) == "いし。" && message.TextAt(3f) == null,
                "message lasts two seconds");
            message.Show("いし。", 2f, 2f);
            Check(message.TextAt(3.5f) == "いし。" && message.TextAt(4f) == null,
                "re-examine resets one message timer");

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
            Debug.Log("HALKA ver1.2 checks passed (tile grid, stone, examine range, message, input).");
        }

        private static void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException("ver1.2 check failed: " + label);
        }
    }
}
