using System;
using Halka.Game.Core;
using Halka.Game.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Halka.Game.Editor
{
    public static class Version253Checks
    {
        [MenuItem("HALKA/Validate ver2.5.3 scene")]
        public static void Run()
        {
            Version252Checks.Run();
            EditorSceneManager.OpenScene("Assets/Scenes/FirstDay.unity");
            Check(GameVersion.Value == "2.5.3" || GameVersion.Value == "2.5.4", "game version");

            var player = GameObject.Find("Player - HarukaChan");
            Check(player != null, "player exists");
            var artwork = player.transform.Find("Player artwork");
            Check(artwork != null && artwork.parent == player.transform &&
                  artwork.Find("Player render pixels") == null &&
                  artwork.childCount == 1, "ver2.5.1 artwork hierarchy without render snap");
            var renderer = artwork.GetComponent<SpriteRenderer>();
            var mask = artwork.Find("Player grass foot mask")?.GetComponent<SpriteMask>();
            var visual = player.GetComponent<CharacterVisual>();
            var mover = player.GetComponent<PlayerMover>();
            var seat = player.GetComponent<PlayerSeatController>();
            var grass = player.GetComponent<PlayerGrassOcclusion>();
            Check(renderer != null && mask != null && visual != null && mover != null &&
                  seat != null && grass != null, "player render, movement, seat and grass mask");
            Check(Bound(visual, "spriteRenderer", renderer) &&
                  Bound(seat, "artwork", artwork) &&
                  Bound(grass, "playerRenderer", renderer) &&
                  Bound(grass, "spriteMask", mask), "original visual references");
            Check(!HasRenderSnap(player.transform) &&
                  Type.GetType("Halka.Game.Player.PlayerRenderSnap2D, Assembly-CSharp") == null,
                  "pixel snap component removed");

            Check(Mathf.Approximately(PlayerMover.DefaultStepSeconds, 0.18f) &&
                  Mathf.Approximately(new SerializedObject(mover).FindProperty("stepSeconds").floatValue, 0.18f) &&
                  Mathf.Approximately(CharacterVisual.OriginalIdleFrameSeconds, 0.4f) &&
                  Mathf.Approximately(CharacterVisual.OriginalWalkFrameSeconds, 0.15f),
                  "ver2.5.1 movement and animation timing");
            Check(typeof(CharacterVisual).GetProperty("HeadAlignmentWorldY") == null,
                  "authored frame bob is not corrected");
            CheckFrame(visual, "frontIdle", 0, "front_idle/00.png");
            CheckFrame(visual, "frontIdle", 1, "front_idle/01.png");
            CheckFrame(visual, "frontWalk", 0, "walk_front/00.png");
            CheckFrame(visual, "backIdle", 0, "back_idle/00.png");
            CheckFrame(visual, "backWalk", 0, "walk_back/00.png");
            CheckFrame(visual, "leftIdle", 0, "left_idle/00.png");
            CheckFrame(visual, "leftWalk", 0, "walk_left/00.png");
            CheckFrame(visual, "rightIdle", 0, "right_idle/00.png");
            CheckFrame(visual, "rightWalk", 0, "walk_right/00.png");

            var motion = new GridStepMotion(Vector2Int.zero);
            Check(motion.TryBegin(Vector2Int.up, _ => true, PlayerMover.DefaultStepSeconds),
                  "one-cell movement starts");
            motion.Advance(PlayerMover.DefaultStepSeconds * 0.5f);
            Check(motion.IsMoving && Mathf.Approximately(motion.Position.y, 0.5f),
                  "movement keeps fractional interpolation");
            motion.Advance(PlayerMover.DefaultStepSeconds * 0.5f);
            Check(!motion.IsMoving && motion.Cell == Vector2Int.up,
                  "movement completes at one cell");
            Debug.Log("HALKA ver2.5.3 original player animation and movement, improved font and inherited regression checks passed.");
        }

        private static bool Bound(UnityEngine.Object owner, string field, UnityEngine.Object target)
        {
            var property = new SerializedObject(owner).FindProperty(field);
            return property != null && property.objectReferenceValue == target;
        }

        private static bool HasRenderSnap(Transform root)
        {
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (behaviour == null || behaviour.GetType().Name == "PlayerRenderSnap2D") return true;
            return false;
        }

        private static void CheckFrame(CharacterVisual visual, string field, int index, string asset)
        {
            var property = new SerializedObject(visual).FindProperty(field);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Content/Character/" + asset);
            Check(property != null && property.isArray && property.arraySize > index &&
                  sprite != null && property.GetArrayElementAtIndex(index).objectReferenceValue == sprite,
                  "authored animation frame " + asset);
        }

        private static void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException("Version253Checks failed: " + description);
        }
    }
}
