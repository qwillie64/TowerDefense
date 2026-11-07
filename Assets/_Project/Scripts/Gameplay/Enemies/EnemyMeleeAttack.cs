using CyberWizard.Core;
using UnityEngine;

namespace CyberWizard.Gameplay.Enemies
{
    public sealed class EnemyMeleeAttack
    {
        private readonly float _damage;
        private readonly float _interval;
        private float _cooldown;

        public EnemyMeleeAttack(float damage, float interval)
        {
            _damage = Mathf.Max(0f, damage);
            _interval = Mathf.Max(0.05f, interval);
        }

        public float CooldownRemaining => _cooldown;

        public void Tick(float deltaTime)
        {
            _cooldown = Mathf.Max(0f, _cooldown - Mathf.Max(0f, deltaTime));
        }

        public bool TryAttack(IDamageable target)
        {
            if (target == null || !target.IsAlive || _cooldown > 0f)
            {
                return false;
            }

            target.TakeDamage(_damage);
            _cooldown = _interval;
            return true;
        }

        public bool AttackImmediately(IDamageable target)
        {
            if (target == null || !target.IsAlive)
            {
                return false;
            }

            target.TakeDamage(_damage);
            return true;
        }
    }
}
