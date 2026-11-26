using System.Collections.Generic;
using CyberWizard.Core;
using CyberWizard.Presentation;
using UnityEngine;

namespace CyberWizard.Gameplay.Combat
{
    public sealed class CombatProjectile : MonoBehaviour
    {
        private static readonly Stack<CombatProjectile> Pool = new Stack<CombatProjectile>();

        private IDamageable _target;
        private MonoBehaviour _targetBehaviour;
        private float _damage;
        private float _speed;
        private bool _isPooled;

        public static void Spawn(
            Vector3 position,
            IDamageable target,
            float damage,
            PrototypeMaterialRole role,
            float speed = 20f)
        {
            CombatProjectile projectile = null;
            while (Pool.Count > 0 && projectile == null)
            {
                projectile = Pool.Pop();
            }

            if (projectile == null)
            {
                var projectileObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                projectileObject.name = "Combat Projectile";
                projectileObject.transform.localScale = Vector3.one * 0.22f;
                var collider = projectileObject.GetComponent<Collider>();
                if (collider != null)
                {
                    Destroy(collider);
                }

                projectile = projectileObject.AddComponent<CombatProjectile>();
            }

            PrototypeMaterialLibrary.Apply(projectile.gameObject, role, false);
            projectile.transform.position = position;
            projectile._target = target;
            projectile._targetBehaviour = target as MonoBehaviour;
            projectile._damage = Mathf.Max(0f, damage);
            projectile._speed = Mathf.Max(0.1f, speed);
            projectile._isPooled = false;
            projectile.gameObject.SetActive(true);
        }

        private void Update()
        {
            if (_target == null || _targetBehaviour == null || !_target.IsAlive)
            {
                Release();
                return;
            }

            var targetPosition = _target.TargetTransform.position + Vector3.up * 0.6f;
            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                _speed * Time.deltaTime);
            if ((transform.position - targetPosition).sqrMagnitude > 0.01f)
            {
                return;
            }

            _target.TakeDamage(_damage);
            PrototypePulseEffect.Spawn(
                targetPosition,
                _target.Faction == Faction.Enemy
                    ? PrototypeMaterialRole.Enemy
                    : PrototypeMaterialRole.Player,
                Vector3.one * 0.45f,
                0.15f);
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
