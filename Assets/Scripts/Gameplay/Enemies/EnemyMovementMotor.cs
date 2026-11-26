using UnityEngine;

namespace CyberWizard.Gameplay.Enemies
{
    public sealed class EnemyMovementMotor
    {
        private readonly Transform _owner;
        private readonly float _moveSpeed;
        private readonly bool _followTargetHeight;
        private readonly float _heightOffset;

        /// <param name="followTargetHeight">
        /// false：只在水平面移動，高度交給重力處理（原有行為）。
        /// true：連同高度一起移向目標，用於沿著放在地表上的路徑點爬坡。
        /// </param>
        /// <param name="heightOffset">跟隨目標高度時，物件中心要高於目標點的距離（通常是腳底到中心的高度）。</param>
        public EnemyMovementMotor(
            Transform owner,
            float moveSpeed,
            bool followTargetHeight = false,
            float heightOffset = 0f)
        {
            _owner = owner;
            _moveSpeed = Mathf.Max(0f, moveSpeed);
            _followTargetHeight = followTargetHeight;
            _heightOffset = heightOffset;
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
            if (_followTargetHeight)
            {
                targetPosition.y += _heightOffset;
            }
            else
            {
                targetPosition.y = _owner.position.y;
            }

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

            // 只依水平方向轉向，爬坡時不讓身體前傾。
            var facing = new Vector3(direction.x, 0f, direction.z);
            if (facing.sqrMagnitude > 0.001f)
            {
                _owner.rotation = Quaternion.LookRotation(facing.normalized, Vector3.up);
            }

            return false;
        }
    }
}
