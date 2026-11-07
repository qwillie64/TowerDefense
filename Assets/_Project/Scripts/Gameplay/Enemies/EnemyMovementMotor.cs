using UnityEngine;

namespace CyberWizard.Gameplay.Enemies
{
    public sealed class EnemyMovementMotor
    {
        private readonly Transform _owner;
        private readonly float _moveSpeed;

        public EnemyMovementMotor(Transform owner, float moveSpeed)
        {
            _owner = owner;
            _moveSpeed = Mathf.Max(0f, moveSpeed);
        }

        public bool MoveTowards(
            Transform target,
            float stopDistance,
            float speedMultiplier,
            float deltaTime)
        {
            if (_owner == null || target == null)
            {
                return true;
            }

            var targetPosition = target.position;
            targetPosition.y = _owner.position.y;
            var direction = targetPosition - _owner.position;
            var clampedStopDistance = Mathf.Max(0f, stopDistance);
            if (direction.sqrMagnitude <= clampedStopDistance * clampedStopDistance)
            {
                return true;
            }

            _owner.position = Vector3.MoveTowards(
                _owner.position,
                targetPosition,
                _moveSpeed * Mathf.Max(0f, speedMultiplier) * Mathf.Max(0f, deltaTime));
            if (direction.sqrMagnitude > 0.001f)
            {
                _owner.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }

            return false;
        }
    }
}
