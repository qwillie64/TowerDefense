using UnityEngine;

namespace CyberWizard.Gameplay.Movement
{
    [DisallowMultipleComponent]
    public sealed class GroundGravity : MonoBehaviour
    {
        [SerializeField] private float gravity = -25f;
        [SerializeField, Min(1f)] private float maximumFallSpeed = 50f;
        [SerializeField, Min(0.01f)] private float groundProbeDistance = 0.15f;

        private Collider _bodyCollider;
        private float _verticalVelocity;

        public float VerticalVelocity => _verticalVelocity;

        private void Awake()
        {
            _bodyCollider = GetComponent<Collider>();
        }

        private void LateUpdate()
        {
            var deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
            {
                return;
            }

            _verticalVelocity = Mathf.Max(
                -maximumFallSpeed,
                _verticalVelocity + gravity * deltaTime);

            var fallDistance = Mathf.Max(0f, -_verticalVelocity * deltaTime);
            if (_bodyCollider == null)
            {
                transform.position += Vector3.up * (_verticalVelocity * deltaTime);
                return;
            }

            var bounds = _bodyCollider.bounds;
            var rayDistance = bounds.extents.y + fallDistance + groundProbeDistance;
            var hits = Physics.RaycastAll(
                bounds.center,
                Vector3.down,
                rayDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            var foundGround = false;
            var nearestDistance = float.MaxValue;
            var groundY = 0f;
            for (var index = 0; index < hits.Length; index++)
            {
                var hit = hits[index];
                if (hit.collider == null || hit.collider.transform == transform ||
                    hit.collider.transform.IsChildOf(transform) ||
                    hit.collider.GetComponentInParent<GroundGravity>() != null ||
                    hit.collider.GetComponentInParent<CharacterController>() != null ||
                    hit.distance >= nearestDistance)
                {
                    continue;
                }

                nearestDistance = hit.distance;
                groundY = hit.point.y;
                foundGround = true;
            }

            var currentFootY = bounds.min.y;
            if (_verticalVelocity <= 0f && foundGround &&
                currentFootY - fallDistance <= groundY + groundProbeDistance)
            {
                var position = transform.position;
                // Grounding must never move a falling object upward. This avoids
                // the visible bounce caused by another collider being detected.
                position.y += Mathf.Min(0f, groundY - currentFootY);
                transform.position = position;
                _verticalVelocity = -2f;
                return;
            }

            transform.position += Vector3.down * fallDistance;
        }
    }
}
