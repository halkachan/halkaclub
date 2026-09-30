using System;
using System.Reflection;
using Halka.Game.Core;
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
    public static class Version13Checks
    {
        [MenuItem("HALKA/Validate ver1.3")]
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/FirstDay.unity");
            var world = UnityEngine.Object.FindFirstObjectByType<GridWorld2D>();
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerMover>();
            var visual = UnityEngine.Object.FindFirstObjectByType<CharacterVisual>();
            var hud = UnityEngine.Object.FindFirstObjectByType<GameHud>();
            var stone = UnityEngine.Object.FindFirstObjectByType<GridObstacle>();
            var examine = stone.GetComponent<ExamineInteractable>();
            var artwork = player.GetComponentInChildren<SpriteRenderer>().transform;
            Check(GameVersion.Value == "1.3", "version source");
            Check(GridWorld2D.TilePixels == 32 && Mathf.Approximately(GridWorld2D.TileWorldSize, 0.5f) &&
                Mathf.Approximately(world.CellSize, GridWorld2D.TileWorldSize), "32-pixel grid scale");
            Check(world.MinCell == new Vector2Int(-10, -6) && world.MaxCell == new Vector2Int(10, 6),
                "physical field size preserved");
            Check(world.CellToWorld(Vector2Int.right) == new Vector3(0.5f, 0f, 0f) &&
                world.WorldToCell(new Vector3(0.5f, 0f, 0f)) == Vector2Int.right,
                "integer grid world conversion");
            Check(artwork.localPosition == new Vector3(0f, 0.5f, 0f) &&
                artwork.GetComponents<Component>().Length == 2,
                "fixed artwork foot anchor without runtime offset component");
            Check(UnityEngine.Object.FindObjectsByType<GridObstacle>(FindObjectsSortMode.None).Length == 1,
                "one world obstacle");
            var stoneSprite = stone.GetComponent<SpriteRenderer>().sprite;
            var collider = stone.GetComponent<BoxCollider2D>();
            Check(stoneSprite.texture.width == 32 && stoneSprite.texture.height == 32 &&
                Mathf.Approximately(stoneSprite.pixelsPerUnit, 64f) && stone.transform.localScale == Vector3.one,
                "supplied stone occupies one 32-pixel cell");
            Check(collider.size == new Vector2(28f / 64f, 20f / 64f) &&
                stone.transform.position == world.CellToWorld(new Vector2Int(1, 1)) &&
                !world.CanEnter(new Vector2Int(1, 1)), "stone collider and blocking cell");
            Check(world.CanEnter(new Vector2Int(0, 1)) && !world.CanEnter(new Vector2Int(11, 0)) &&
                !world.CanEnter(new Vector2Int(0, -7)), "adjacent and field bounds");
            var grid = GameObject.Find("FirstDay - 32px cell boundaries").GetComponent<SpriteRenderer>();
            Check(grid.sprite.texture.width == 21 * 32 && grid.sprite.texture.height == 13 * 32 &&
                Mathf.Approximately(grid.sprite.bounds.size.x, 10.5f) &&
                Mathf.Approximately(grid.sprite.bounds.size.y, 6.5f) &&
                grid.transform.position == Vector3.zero && grid.sprite.texture.filterMode == FilterMode.Point,
                "world-aligned sharp 32-pixel grid overlay");
            foreach (var clip in new[] { "frontWalk", "backWalk", "leftWalk", "rightWalk" })
            {
                var property = new SerializedObject(visual).FindProperty(clip);
                Check(property.arraySize == 4, clip + " four frames");
                for (var i = 0; i < property.arraySize; i++)
                {
                    var sprite = (Sprite)property.GetArrayElementAtIndex(i).objectReferenceValue;
                    Check(sprite != null && sprite.texture.width == 64 && sprite.texture.height == 64 &&
                        sprite.texture.filterMode == FilterMode.Point &&
                        Mathf.Approximately(sprite.pixelsPerUnit, 64f), clip + " sprite import");
                }
            }
            var select = typeof(CharacterVisual).GetMethod("SelectFrames", BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var direction in new[] { FacingDirection.Up, FacingDirection.Down,
                FacingDirection.Left, FacingDirection.Right })
            {
                var walk = (Sprite[])select.Invoke(visual, new object[] { direction, true });
                var idle = (Sprite[])select.Invoke(visual, new object[] { direction, false });
                Check(walk.Length == 4 && idle.Length > 0 && walk != idle,
                    direction + " walk/idle selection");
            }
            Check(Mathf.Approximately(CharacterVisual.OriginalWalkFrameSeconds, 0.15f) &&
                Mathf.Approximately(PlayerMover.DefaultStepSeconds, 0.18f), "original GIF timing and step duration");
            Check(new SerializedObject(examine).FindProperty("message").stringValue == "いし。" &&
                new SerializedObject(hud).FindProperty("messageFont").objectReferenceValue != null,
                "generic stone examine and Japanese font");
            var target = new Vector2Int(1, 1);
            foreach (var side in new[] { Vector2Int.up, Vector2Int.down,
                Vector2Int.left, Vector2Int.right })
            {
                var neighbor = target + side;
                Check(world.CanEnter(neighbor) && !world.CanEnter(target) &&
                    artwork.localPosition == new Vector3(0f, 0.5f, 0f),
                    "stone approach from all four sides keeps fixed artwork offset");
            }
            foreach (var facing in new[] { FacingDirection.Up, FacingDirection.Down,
                FacingDirection.Left, FacingDirection.Right })
            {
                var adjacent = target - facing.ToVector();
                Check(ExamineInteractable.IsInRange(adjacent, false, facing, target),
                    facing + " adjacent facing succeeds");
                foreach (var other in new[] { FacingDirection.Up, FacingDirection.Down,
                    FacingDirection.Left, FacingDirection.Right })
                    if (other != facing)
                        Check(!ExamineInteractable.IsInRange(adjacent, false, other, target),
                            "wrong facing rejected");
                Check(!ExamineInteractable.IsInRange(adjacent, true, facing, target),
                    "moving interaction rejected");
            }
            Check(!ExamineInteractable.IsInRange(Vector2Int.zero, false, FacingDirection.Right, target) &&
                !ExamineInteractable.IsInRange(new Vector2Int(-1, 1), false, FacingDirection.Right, target),
                "diagonal and distant interaction rejected");
            typeof(PlayerMover).GetField("motion", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(player, new GridStepMotion(new Vector2Int(0, 1)));
            var messageField = typeof(GameHud).GetField("message", BindingFlags.NonPublic | BindingFlags.Instance);
            hud.ShowMessage(string.Empty);
            examine.Interact();
            Check(string.IsNullOrEmpty(((TimedMessage)messageField.GetValue(hud)).TextAt(Time.unscaledTime)),
                "adjacent but wrong facing cannot examine");
            player.ApplyDirection(Vector2Int.right);
            Check(player.Facing == FacingDirection.Right && !player.IsMoving &&
                player.Cell == new Vector2Int(0, 1), "blocked stone input turns without entering");
            hud.ShowMessage(string.Empty);
            examine.Interact();
            var activeMessage = (TimedMessage)messageField.GetValue(hud);
            Check(activeMessage.TextAt(Time.unscaledTime) == "いし。", "facing stone displays exact message");
            typeof(PlayerMover).GetField("motion", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(player, new GridStepMotion(new Vector2Int(-10, 1)));
            player.ApplyDirection(Vector2Int.left);
            Check(player.Facing == FacingDirection.Left && !player.IsMoving,
                "field wall input changes facing without movement");
            typeof(PlayerMover).GetField("motion", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(player, new GridStepMotion(new Vector2Int(0, 1)));
            hud.ShowMessage(string.Empty);
            examine.Interact();
            Check(string.IsNullOrEmpty(((TimedMessage)messageField.GetValue(hud)).TextAt(Time.unscaledTime)),
                "clicking stone does not auto-face or display message");
            var timed = new TimedMessage();
            timed.Show("いし。", 1f, 2f);
            Check(timed.TextAt(2.9f) == "いし。" && timed.TextAt(3f) == null,
                "two-second message lifetime");
            timed.Show("いし。", 2f, 2f);
            Check(timed.TextAt(3.5f) == "いし。" && timed.TextAt(4f) == null,
                "re-examine resets one message");
            var motion = new GridStepMotion(Vector2Int.zero);
            player.ApplyDirection(new Vector2Int(1, 1));
            Check(player.Facing == FacingDirection.Left && !player.IsMoving,
                "diagonal player input rejected without state change");
            Check(!motion.TryBegin(new Vector2Int(1, 1), world.CanEnter,
                PlayerMover.DefaultStepSeconds), "diagonal step rejected");
            Check(motion.TryBegin(Vector2Int.right, world.CanEnter,
                PlayerMover.DefaultStepSeconds), "cardinal step begins");
            Check(!motion.TryBegin(Vector2Int.up, world.CanEnter,
                PlayerMover.DefaultStepSeconds), "cannot turn during step");
            motion.Advance(PlayerMover.DefaultStepSeconds * 0.5f);
            Check(motion.Cell == Vector2Int.zero && motion.IsMoving &&
                world.CellToWorld(motion.Position) == new Vector3(0.25f, 0f, 0f),
                "only root interpolates half a 32-pixel step");
            motion.Advance(PlayerMover.DefaultStepSeconds * 0.5f);
            Check(!motion.IsMoving && motion.Cell == Vector2Int.right &&
                world.CellToWorld(motion.Position) == new Vector3(0.5f, 0f, 0f),
                "step ends at exact neighboring cell");
            Check(CardinalInput.Choose(true, false, false, true, Vector2Int.zero) == Vector2Int.up &&
                CardinalInput.Choose(false, false, true, false, Vector2Int.zero) == Vector2Int.left &&
                CardinalInput.Choose(false, false, false, true, Vector2Int.zero) == Vector2Int.right &&
                CardinalInput.Choose(false, true, false, false, Vector2Int.zero) == Vector2Int.down &&
                CardinalInput.Choose(false, false, false, false, Vector2Int.up) == Vector2Int.up,
                "keyboard and touch choose four cardinal directions");
            var tap = new PointerTap();
            Check(!tap.TryBegin(1, Vector2.zero, true, 0f), "D-pad blocks world tap");
            Check(tap.TryBegin(2, Vector2.zero, false, 0f) &&
                !tap.TryBegin(3, Vector2.zero, false, 0f) &&
                !tap.End(3, Vector2.zero, false, 0.1f, 24f, 0.4f) &&
                tap.End(2, Vector2.zero, false, 0.1f, 24f, 0.4f),
                "pointer ID prevents second finger interference");
            Debug.Log("HALKA ver1.3 checks passed.");
        }

        private static void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException("ver1.3 check failed: " + label);
        }
    }
}
