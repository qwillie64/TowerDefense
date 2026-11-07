using CyberWizard.Gameplay.Enemies;
using CyberWizard.Presentation;
using CyberWizard.Core;
using UnityEngine;
using System.Collections.Generic;

namespace CyberWizard.Gameplay.Towers
{
    public sealed class TowerProjectile : MonoBehaviour
    {
        private static readonly Stack<TowerProjectile> Pool = new Stack<TowerProjectile>();

        private IDamageable _target;
        private MonoBehaviour _targetBehaviour;
        private float _damage;
        private float _speed;
        private bool _isPooled;

        public static int CreatedCount { get; private set; }
        public static int ReusedCount { get; private set; }

        public static void Spawn(
            Vector3 position,
            ElementType element,
            IDamageable target,
            float damage,
            float speed)
        {
            TowerProjectile projectile = null;
            while (Pool.Count > 0 && projectile == null)
            {
                projectile = Pool.Pop();
            }

            if (projectile == null)
            {
                var projectileObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                projectileObject.transform.localScale = Vector3.one * 0.28f;
                var collider = projectileObject.GetComponent<Collider>();
                if (collider != null)
                {
                    Destroy(collider);
                }

                PrototypeMaterialLibrary.Apply(projectileObject, PrototypeMaterialRole.Player);
                projectile = projectileObject.AddComponent<TowerProjectile>();
                CreatedCount++;
            }
            else
            {
                ReusedCount++;
            }

            projectile.name = $"{element} Tower Projectile";
            projectile.transform.position = position;
            projectile._isPooled = false;
            projectile.Initialize(target, damage, speed);
            projectile.gameObject.SetActive(true);
        }

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
                Release();
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
            Release();
        }

        private void Release()
        {
            if (_isPooled)
            {
                return;
            }

            _isPooled = true;
            _target = null;
            _targetBehaviour = null;
            gameObject.SetActive(false);
            Pool.Push(this);
        }
    }
}
