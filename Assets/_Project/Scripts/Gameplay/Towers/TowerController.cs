using System;
using CyberWizard.Core;
using CyberWizard.Gameplay.Enemies;
using CyberWizard.Gameplay.Waves;
using UnityEngine;
using CyberWizard.Presentation;

namespace CyberWizard.Gameplay.Towers
{
    public sealed class TowerController : MonoBehaviour, IDamageable
    {
        private WaveDirector _waveDirector;
        private TowerRegistry _registry;
        private float _attackCooldown;
        private float _durability;
        private WorldHealthBar _healthBar;

        public event Action<TowerController> StateChanged;

        public bool IsAlive => _durability > 0f;
        public bool IsOperational => _durability > 0f;
        public Faction Faction => Faction.Player;
        public Transform TargetTransform => transform;
        public ElementType Element { get; private set; }
        public float Durability => _durability;
        public float MaxDurability { get; private set; } = 100f;
        public float AttackRange { get; private set; } = 6.5f;

        public void Initialize(
            ElementType element,
            WaveDirector waveDirector,
            TowerRegistry registry)
        {
            Element = element;
            _waveDirector = waveDirector;
            _registry = registry;
            _durability = MaxDurability;
            _registry.Register(this);
            _healthBar = WorldHealthBar.Create(
                transform,
                new Vector3(0f, 2.15f, 0f),
                PrototypeMaterialRole.Player,
                1.8f);
            _healthBar.SetValue(_durability, MaxDurability);
        }

        private void Update()
        {
            if (!IsOperational || _waveDirector == null)
            {
                return;
            }

            _attackCooldown -= Time.deltaTime;
            if (_attackCooldown > 0f)
            {
                return;
            }

            var target = FindTarget();
            if (target == null)
            {
                return;
            }

            target.TakeDamage(20f);
            _durability = Mathf.Max(0f, _durability - 2f);
            if (_healthBar != null) _healthBar.SetValue(_durability, MaxDurability);
            _attackCooldown = 1f;
            StateChanged?.Invoke(this);
        }

        public void TakeDamage(float amount)
        {
            if (!IsAlive || amount <= 0f)
            {
                return;
            }

            _durability = Mathf.Max(0f, _durability - amount);
            if (_healthBar != null) _healthBar.SetValue(_durability, MaxDurability);
            StateChanged?.Invoke(this);
        }

        public bool TryRepair(float amount)
        {
            if (amount <= 0f || _durability >= MaxDurability)
            {
                return false;
            }

            _durability = Mathf.Min(MaxDurability, _durability + amount);
            if (_healthBar != null) _healthBar.SetValue(_durability, MaxDurability);
            StateChanged?.Invoke(this);
            return true;
        }

        private EnemyAgent FindTarget()
        {
            EnemyAgent result = null;
            var bestDistance = AttackRange * AttackRange;
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

        private void OnDestroy()
        {
            if (_registry != null)
            {
                _registry.Unregister(this);
            }
        }
    }
}
