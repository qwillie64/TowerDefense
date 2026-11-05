using CyberWizard.Core;
using CyberWizard.Gameplay.Enemies;
using CyberWizard.Gameplay.Waves;
using UnityEngine;
using CyberWizard.Presentation;

namespace CyberWizard.Gameplay.Neutral
{
    public sealed class SoldierAgent : MonoBehaviour, IDamageable
    {
        private WaveDirector _waveDirector;
        private float _health = 50f;
        private float _attackCooldown;

        public bool IsAlive => _health > 0f;
        public Faction Faction => Faction.Player;
        public Transform TargetTransform => transform;

        public void Initialize(WaveDirector waveDirector)
        {
            _waveDirector = waveDirector;
            PrototypeMaterialLibrary.Apply(gameObject, PrototypeMaterialRole.Player);
        }

        private void Update()
        {
            if (!IsAlive || _waveDirector == null)
            {
                return;
            }

            _attackCooldown -= Time.deltaTime;
            var target = FindClosestEnemy();
            if (target == null)
            {
                return;
            }

            var targetPosition = target.transform.position;
            targetPosition.y = transform.position.y;
            var distance = Vector3.Distance(transform.position, targetPosition);
            if (distance > 1.7f)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    targetPosition,
                    3f * Time.deltaTime);
                transform.LookAt(targetPosition);
            }
            else if (_attackCooldown <= 0f)
            {
                target.TakeDamage(12f);
                _attackCooldown = 0.8f;
            }
        }

        public void TakeDamage(float amount)
        {
            if (!IsAlive || amount <= 0f)
            {
                return;
            }

            _health = Mathf.Max(0f, _health - amount);
            if (_health <= 0f)
            {
                Destroy(gameObject);
            }
        }

        private EnemyAgent FindClosestEnemy()
        {
            EnemyAgent result = null;
            var bestDistance = float.MaxValue;
            foreach (var enemy in _waveDirector.AliveEnemies)
            {
                if (enemy == null || !enemy.IsAlive)
                {
                    continue;
                }

                var distance = (enemy.transform.position - transform.position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    result = enemy;
                }
            }

            return result;
        }
    }
}
