using CyberWizard.Core;
using CyberWizard.Gameplay.Enemies;
using CyberWizard.Gameplay.Waves;
using UnityEngine;
using CyberWizard.Presentation;
using CyberWizard.Gameplay.Player;
using CyberWizard.Gameplay.Towers;
using CyberWizard.Gameplay.GameFlow;

namespace CyberWizard.Gameplay.Neutral
{
    public sealed class SoldierAgent : MonoBehaviour, IDamageable
    {
        private WaveDirector _waveDirector;
        private float _attackCooldown;
        private Vitality _vitality;
        private WorldHealthBar _healthBar;
        private Faction _faction = Faction.Player;
        private PrototypePlayerController _player;
        private TowerRegistry _towerRegistry;
        private BaseCore _baseCore;
        private SoldierRegistry _registry;

        public bool IsAlive => _vitality != null && _vitality.IsAlive;
        public Faction Faction => _faction;
        public Transform TargetTransform => transform;

        public void Initialize(
            WaveDirector waveDirector,
            Faction faction,
            PrototypePlayerController player = null,
            TowerRegistry towerRegistry = null,
            BaseCore baseCore = null,
            SoldierRegistry registry = null)
        {
            _waveDirector = waveDirector;
            _faction = faction;
            _player = player;
            _towerRegistry = towerRegistry;
            _baseCore = baseCore;
            _registry = registry;
            var role = faction == Faction.Enemy
                ? PrototypeMaterialRole.Enemy
                : PrototypeMaterialRole.Player;
            PrototypeMaterialLibrary.Apply(gameObject, role);
            _vitality = gameObject.AddComponent<Vitality>();
            _vitality.Changed += HandleVitalityChanged;
            _vitality.Depleted += HandleDepleted;
            _vitality.Initialize(50f);
            _healthBar = WorldHealthBar.Create(
                transform,
                new Vector3(0f, 1.35f, 0f),
                role,
                1.2f);
            _healthBar.SetValue(_vitality.Current, _vitality.Maximum);
            _registry?.Register(this);
        }

        private void Update()
        {
            if (!IsAlive || _waveDirector == null)
            {
                return;
            }

            _attackCooldown -= Time.deltaTime;
            var target = _faction == Faction.Enemy
                ? FindClosestPlayerTarget()
                : FindClosestEnemy();
            if (target == null)
            {
                return;
            }

            var targetPosition = target.TargetTransform.position;
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
            _vitality?.TakeDamage(amount);
        }

        private void HandleVitalityChanged(float current, float maximum)
        {
            if (_healthBar != null) _healthBar.SetValue(current, maximum);
        }

        private void HandleDepleted() => Destroy(gameObject);

        private IDamageable FindClosestEnemy()
        {
            IDamageable result = null;
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

            var enemySoldier = _registry != null
                ? _registry.FindClosest(Faction.Enemy, transform.position, 1000f)
                : null;
            ConsiderTarget(enemySoldier, ref result, ref bestDistance);

            return result;
        }

        private IDamageable FindClosestPlayerTarget()
        {
            IDamageable result = null;
            var bestDistance = float.MaxValue;

            var tower = _towerRegistry != null
                ? _towerRegistry.FindClosestActive(transform.position, 1000f)
                : null;
            ConsiderTarget(tower, ref result, ref bestDistance);
            ConsiderTarget(_player, ref result, ref bestDistance);
            ConsiderTarget(_baseCore, ref result, ref bestDistance);
            return result;
        }

        private void ConsiderTarget(
            IDamageable candidate,
            ref IDamageable result,
            ref float bestDistance)
        {
            if (candidate == null || !candidate.IsAlive)
            {
                return;
            }

            var distance = (candidate.TargetTransform.position - transform.position).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                result = candidate;
            }
        }

        private void OnDestroy()
        {
            _registry?.Unregister(this);
            if (_vitality == null) return;
            _vitality.Changed -= HandleVitalityChanged;
            _vitality.Depleted -= HandleDepleted;
        }
    }
}
