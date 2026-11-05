using CyberWizard.Gameplay.Enemies;
using CyberWizard.Presentation;
using CyberWizard.Core;
using UnityEngine;

namespace CyberWizard.Gameplay.Towers
{
    public sealed class TowerProjectile : MonoBehaviour
    {
        private IDamageable _target;
        private MonoBehaviour _targetBehaviour;
        private float _damage;
        private float _speed;

        public void Initialize(IDamageable target, float damage, float speed)
        {
            _target = target;
            _targetBehaviour = target as MonoBehaviour;
            _damage = Mathf.Max(0f, damage);
            _speed = Mathf.Max(0.1f, speed);
        }

        private void Update()
        {
            if (_targetBehaviour == null || _target == null || !_target.IsAlive)
            {
                Destroy(gameObject);
                return;
            }

            var targetPosition = _target.TargetTransform.position + Vector3.up * 0.6f;
            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                _speed * Time.deltaTime);
            if ((transform.position - targetPosition).sqrMagnitude > 0.08f * 0.08f)
            {
                return;
            }

            _target.TakeDamage(_damage);
            PrototypePulseEffect.Spawn(
                targetPosition,
                PrototypeMaterialRole.Player,
                Vector3.one * 0.55f,
                0.18f);
            Destroy(gameObject);
        }
    }
}
