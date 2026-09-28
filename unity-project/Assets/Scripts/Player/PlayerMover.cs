using Halka.Game.Input;
using UnityEngine;

namespace Halka.Game.Player
{
    public sealed class PlayerMover : MonoBehaviour
    {
        [SerializeField] private GameInput input;
        [SerializeField, Min(0.1f)] private float speed = 2.6f;
        [SerializeField] private Vector2 minPosition = new Vector2(-5.3f, -3.3f);
        [SerializeField] private Vector2 maxPosition = new Vector2(5.3f, 3.3f);

        public Vector2 Facing { get; private set; } = Vector2.down;
        public bool IsMoving { get; private set; }

        private void Update()
        {
            var direction = input.Move;
            IsMoving = direction.sqrMagnitude > 0.001f;
            if (IsMoving) Facing = direction;

            var next = (Vector2)transform.position + direction * (speed * Time.deltaTime);
            transform.position = new Vector3(
                Mathf.Clamp(next.x, minPosition.x, maxPosition.x),
                Mathf.Clamp(next.y, minPosition.y, maxPosition.y),
                transform.position.z);
        }
    }
}
