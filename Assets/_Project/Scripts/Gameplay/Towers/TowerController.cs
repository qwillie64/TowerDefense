using System;
using CyberWizard.Core;
using CyberWizard.Gameplay.Enemies;
using CyberWizard.Gameplay.Waves;
using UnityEngine;
using CyberWizard.Presentation;
using CyberWizard.Gameplay.Neutral;

namespace CyberWizard.Gameplay.Towers
{
    public sealed class TowerController : MonoBehaviour, IDamageable
    {
        private WaveDirector _waveDirector;
        private TowerRegistry _registry;
        private float _attackCooldown;
        private WorldHealthBar _healthBar;
        private Vitality _vitality;
        private TowerDefinition _definition;
        private TowerRuntimeStats _runtimeStats;
        private GameObject _rangeIndicator;
        private SoldierRegistry _soldierRegistry;

        public event Action<TowerController> StateChanged;

        public bool IsAlive => _vitality != null && _vitality.IsAlive;
        public bool IsOperational => IsAlive;
        public Faction Faction => Faction.Player;
        public Transform TargetTransform => transform;
        public ElementType Element { get; private set; }
        public int Level { get; private set; } = 1;
        public float Durability => _vitality != null ? _vitality.Current : 0f;
        public float MaxDurability => _vitality != null ? _vitality.Maximum : 100f;
        public float AttackRange => _definition != null
            ? _runtimeStats.AttackRange + (Level - 1) * _definition.RangePerLevel
            : 6.5f;
        public bool CanUpgrade => _definition != null && Level < _definition.MaxLevel;
        public int UpgradeCost => _definition != null
            ? _definition.UpgradeBaseCost * Level
            : 0;

        public void Initialize(
            TowerDefinition definition,
            WaveDirector waveDirector,
            TowerRegistry registry,
            SoldierRegistry soldierRegistry = null)
        {
            _definition = definition;
            _runtimeStats = TowerAssembler.Assemble(definition);
            Element = definition != null ? definition.Element : ElementType.Fire;
            _waveDirector = waveDirector;
            _registry = registry;
            _soldierRegistry = soldierRegistry;
            _vitality = gameObject.AddComponent<Vitality>();
            _vitality.Changed += HandleVitalityChanged;
            _vitality.Initialize(_runtimeStats.MaxDurability);
            _registry.Register(this);
            _healthBar = WorldHealthBar.Create(
                transform,
                new Vector3(0f, 2.15f, 0f),
                PrototypeMaterialRole.Player,
                1.8f);
            _healthBar.SetValue(Durability, MaxDurability);
            CreateRangeIndicator();
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

            FireProjectile(target);
            _vitality.TakeDamage(
                _runtimeStats.DurabilityCostPerShot);
            _attackCooldown = _runtimeStats.AttackInterval;
        }

        public void TakeDamage(float amount)
        {
            _vitality?.TakeDamage(amount);
        }

        public bool TryRepair(float amount)
        {
            return _vitality != null && _vitality.Restore(amount);
        }

        public bool TryUpgrade()
        {
            if (!CanUpgrade)
            {
                return false;
            }

            Level++;
            _vitality.IncreaseMaximum(_definition.DurabilityPerLevel, true);
            UpdateRangeIndicatorScale();
            var top = transform.Find("Top");
            if (top != null)
            {
                top.localScale *= 1.12f;
            }

            PrototypePulseEffect.Spawn(
                transform.position + Vector3.up,
                PrototypeMaterialRole.Player,
                Vector3.one * 1.8f,
                0.4f);
            StateChanged?.Invoke(this);
            return true;
        }

        private void HandleVitalityChanged(float current, float maximum)
        {
            if (_healthBar != null) _healthBar.SetValue(current, maximum);
            StateChanged?.Invoke(this);
        }

        private IDamageable FindTarget()
        {
            IDamageable result = null;
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

            var enemySoldier = _soldierRegistry != null
                ? _soldierRegistry.FindClosest(Faction.Enemy, transform.position, AttackRange)
                : null;
            if (enemySoldier != null)
            {
                var distance = (enemySoldier.transform.position - transform.position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    result = enemySoldier;
                }
            }

            return result;
        }

        private void FireProjectile(IDamageable target)
        {
            TowerProjectile.Spawn(
                transform.position + Vector3.up * 1.8f,
                Element,
                target,
                _definition != null
                    ? _runtimeStats.AttackDamage *
                      (1f + (Level - 1) * _definition.DamagePerLevel)
                    : 20f,
                _runtimeStats.ProjectileSpeed);
        }

        public void SetRangeVisible(bool visible)
        {
            if (_rangeIndicator != null)
            {
                _rangeIndicator.SetActive(visible);
            }
        }

        private void CreateRangeIndicator()
        {
            _rangeIndicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _rangeIndicator.name = "Attack Range Indicator";
            _rangeIndicator.transform.SetParent(transform, false);
            _rangeIndicator.transform.localPosition = new Vector3(0f, -0.96f, 0f);
            _rangeIndicator.transform.localScale = new Vector3(
                AttackRange * 2f,
                0.015f,
                AttackRange * 2f);
            var collider = _rangeIndicator.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            PrototypeMaterialLibrary.Apply(_rangeIndicator, PrototypeMaterialRole.Player);
            _rangeIndicator.SetActive(_registry != null && _registry.RangesVisible);
        }

        private void UpdateRangeIndicatorScale()
        {
            if (_rangeIndicator == null)
            {
                return;
            }

            _rangeIndicator.transform.localScale = new Vector3(
                AttackRange * 2f,
                0.015f,
                AttackRange * 2f);
        }

        private void OnDestroy()
        {
            if (_vitality != null)
            {
                _vitality.Changed -= HandleVitalityChanged;
            }

            if (_registry != null)
            {
                _registry.Unregister(this);
            }
        }
    }
}
