using System;
using CyberWizard.Core;
using CyberWizard.Gameplay.Combat;
using CyberWizard.Gameplay.Enemies;
using CyberWizard.Infrastructure;
using CyberWizard.Presentation;
using UnityEngine;

namespace CyberWizard.Gameplay.Player
{
    public sealed class PrototypePlayerAttack : MonoBehaviour
    {
        [SerializeField] private PrototypePlayerController player;
        [SerializeField] private Camera gameplayCamera;
        [SerializeField, Min(0f)] private float damage = 10f;
        [SerializeField, Min(0.05f)] private float attackInterval = 0.5f;
        [SerializeField, Min(0.1f), Tooltip("攻擊範圍（水平距離）。2026-11-26 由 3 放大 1.5 倍為 4.5，再提升 1 倍為 9。")]
        private float attackRange = 9f;

        private float _cooldownRemaining;
        private GroundRangeIndicator _rangeIndicator;
        private IDamageable _aimedTarget;

        private bool HasValidTarget => _aimedTarget != null && _aimedTarget.IsAlive;

        /// <summary>攻擊範圍內是否有存活的敵人（射程圈顯示紅色）。</summary>
        public bool HasEnemyInRange { get; private set; }
        public float AttackRange => attackRange;

        private void Start()
        {
            CreateRangeIndicator();
        }

        private void Update()
        {
            _cooldownRemaining = Mathf.Max(0f, _cooldownRemaining - Time.deltaTime);
            _aimedTarget = FindAimedTarget();

            HasEnemyInRange = player != null && player.IsAlive && AnyEnemyInRange(player.transform.position);
            if (_rangeIndicator != null)
            {
                _rangeIndicator.SetAlert(HasEnemyInRange);
            }

            if (player == null || !player.InputEnabled || !player.IsAlive ||
                !GameInput.AttackPressed || _cooldownRemaining > 0f ||
                !HasValidTarget)
            {
                return;
            }

            var origin = player.transform.position + Vector3.up * 0.9f;
            CombatProjectile.Spawn(
                origin,
                _aimedTarget,
                damage,
                PrototypeMaterialRole.Player);
            _cooldownRemaining = attackInterval;
        }

        private IDamageable FindAimedTarget()
        {
            if (player == null || gameplayCamera == null || !player.IsAlive)
            {
                return null;
            }

            var pointerPosition = Cursor.lockState == CursorLockMode.Locked
                ? new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f)
                : GameInput.PointerPosition;
            var ray = gameplayCamera.ScreenPointToRay(pointerPosition);
            var hits = Physics.RaycastAll(ray, 100f);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            for (var index = 0; index < hits.Length; index++)
            {
                IDamageable candidate = hits[index].collider.GetComponentInParent<EnemyAgent>();
                if (candidate == null || !candidate.IsAlive)
                {
                    continue;
                }

                var offset = candidate.TargetTransform.position - player.transform.position;
                offset.y = 0f;
                return offset.sqrMagnitude <= attackRange * attackRange ? candidate : null;
            }

            return null;
        }

        private bool AnyEnemyInRange(Vector3 position)
        {
            var enemies = EnemyAgent.Active;
            for (var index = 0; index < enemies.Count; index++)
            {
                var enemy = enemies[index];
                if (enemy == null || !enemy.IsAlive)
                {
                    continue;
                }

                var offset = enemy.transform.position - position;
                offset.y = 0f;
                if (offset.sqrMagnitude <= attackRange * attackRange)
                {
                    return true;
                }
            }

            return false;
        }

        private void CreateRangeIndicator()
        {
            if (player == null || _rangeIndicator != null)
            {
                return;
            }

            _rangeIndicator = GroundRangeIndicator.Create(
                player.transform,
                "Player Attack Range Circle",
                attackRange);
        }
    }
}
