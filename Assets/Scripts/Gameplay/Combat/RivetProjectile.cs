using System.Collections.Generic;
using CyberWizard.Core;
using CyberWizard.Presentation;
using UnityEngine;

namespace CyberWizard.Gameplay.Combat
{
    /// <summary>
    /// 防禦塔的「鉚釘」發射物：圓錐外形、尖端朝飛行方向，追蹤目標直到命中。
    /// 目標在飛行途中消失時，沿最後方向繼續飛行到壽命結束後回收。
    /// 使用物件池，避免頻繁建立與銷毀。
    /// </summary>
    public sealed class RivetProjectile : MonoBehaviour
    {
        private const float HitDistance = 0.35f;
        private const float MaxLifetime = 4f;
        private static readonly Stack<RivetProjectile> Pool = new Stack<RivetProjectile>();

        private IDamageable _target;
        private MonoBehaviour _targetBehaviour;
        private Vector3 _direction;
        private float _damage;
        private float _speed;
        private float _age;
        private bool _isPooled;
        private MeshRenderer _renderer;

        public static RivetProjectile Spawn(
            Vector3 position,
            Vector3 initialDirection,
            IDamageable target,
            float damage,
            float speed,
            Material material,
            Vector3 scale)
        {
            RivetProjectile projectile = null;
            while (Pool.Count > 0 && projectile == null)
            {
                projectile = Pool.Pop();
            }

            if (projectile == null)
            {
                var projectileObject = new GameObject("Rivet Projectile");
                projectileObject.AddComponent<MeshFilter>().sharedMesh = ConeMesh.Shared;
                projectile = projectileObject.AddComponent<RivetProjectile>();
                projectile._renderer = projectileObject.AddComponent<MeshRenderer>();
                projectile._renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            projectile._renderer.sharedMaterial = material;
            projectile.transform.localScale = scale;
            projectile._target = target;
            projectile._targetBehaviour = target as MonoBehaviour;
            projectile._direction = initialDirection.sqrMagnitude > 0.0001f
                ? initialDirection.normalized
                : Vector3.forward;
            projectile._damage = Mathf.Max(0f, damage);
            projectile._speed = Mathf.Max(0.1f, speed);
            projectile._age = 0f;
            projectile._isPooled = false;
            projectile.transform.SetPositionAndRotation(position, Quaternion.LookRotation(projectile._direction));
            projectile.gameObject.SetActive(true);
            return projectile;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            var step = _speed * Time.deltaTime;
            var hasTarget = _target != null && _targetBehaviour != null && _target.IsAlive;

            if (hasTarget)
            {
                var targetPosition = _target.TargetTransform.position;
                var offset = targetPosition - transform.position;
                if (offset.magnitude <= Mathf.Max(HitDistance, step))
                {
                    transform.position = targetPosition;
                    _target.TakeDamage(_damage);
                    PrototypePulseEffect.Spawn(
                        targetPosition,
                        PrototypeMaterialRole.Enemy,
                        Vector3.one * 0.6f,
                        0.15f);
                    Release();
                    return;
                }

                _direction = offset.normalized;
            }
            else
            {
                _target = null;
                _targetBehaviour = null;
            }

            transform.position += _direction * step;
            transform.rotation = Quaternion.LookRotation(_direction);
            if (_age >= MaxLifetime)
            {
                Release();
            }
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
