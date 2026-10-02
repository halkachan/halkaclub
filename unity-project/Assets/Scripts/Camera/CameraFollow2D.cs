using UnityEngine;

namespace Halka.Game.CameraControl
{
    public sealed class CameraFollow2D : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Camera cameraComponent;
        [SerializeField, Min(0.1f)] private float smoothTime = 0.2f;
        [SerializeField] private Vector2 worldMin = new Vector2(-6f, -4f);
        [SerializeField] private Vector2 worldMax = new Vector2(6f, 4f);

        private Vector3 velocity;

        public Vector2 WorldMin => worldMin;
        public Vector2 WorldMax => worldMax;

        public void SetBounds(Vector2 minimum, Vector2 maximum)
        {
            worldMin = minimum;
            worldMax = maximum;
            velocity = Vector3.zero;
        }

        private void LateUpdate()
        {
            var halfHeight = cameraComponent.orthographicSize;
            var halfWidth = halfHeight * cameraComponent.aspect;
            var centerX = (worldMin.x + worldMax.x) * 0.5f;
            var centerY = (worldMin.y + worldMax.y) * 0.5f;
            var rangeX = Mathf.Max(0f, (worldMax.x - worldMin.x) * 0.5f - halfWidth);
            var rangeY = Mathf.Max(0f, (worldMax.y - worldMin.y) * 0.5f - halfHeight);
            var desired = new Vector3(
                Mathf.Clamp(target.position.x, centerX - rangeX, centerX + rangeX),
                Mathf.Clamp(target.position.y, centerY - rangeY, centerY + rangeY),
                transform.position.z);
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
        }
    }
}
