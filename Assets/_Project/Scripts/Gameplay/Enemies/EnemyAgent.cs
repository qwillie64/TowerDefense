using System;
using CyberWizard.Core;
using CyberWizard.Gameplay.GameFlow;
using CyberWizard.Gameplay.Neutral;
using CyberWizard.Gameplay.Player;
using CyberWizard.Gameplay.Towers;
using CyberWizard.Presentation;
using UnityEngine;

namespace CyberWizard.Gameplay.Enemies
{
    public enum EnemyIntent
    {
        AttackBase,
        CaptureNeutral,
        AttackTower
    }

    public sealed class EnemyAgent : MonoBehaviour, IDamageable
    {
        private const float ArrivalDistance = 0.08f;
        private const float AttackDistance = 2.6f;
        private const float NeutralCaptureDuration = 3f;

        private Transform _destination;
        private float _moveSpeed;
        private bool _reportedExit;
        private float _currentHealth;
        private float _attackCooldown;
        private float _neutralCaptureProgress;
        private IDamageable _attackTarget;
        private TowerRegistry _towerRegistry;
        private PrototypePlayerController _player;
        private BaseCore _baseCore;
        private NeutralOutpostModule _neutralModule;
        private WorldHealthBar _healthBar;

        public event Action<EnemyAgent> Exited;
        public event Action<EnemyAgent> Killed;

        public string WaveId { get; private set; }
        public string EnemyTypeId { get; private set; }
        public EnemyIntent Intent { get; private set; }
        public bool IsAlive => _currentHealth > 0f;
        public Faction Faction => Faction.Enemy;
        public Transform TargetTransform => transform;
        public float CurrentHealth => _currentHealth;
        public float MaxHealth { get; private set; } = 100f;

        public void Initialize(
            string enemyTypeId,
            string waveId,
            Transform destination,
            float moveSpeed,
            TowerRegistry towerRegistry,
            PrototypePlayerController player,
            BaseCore baseCore,
            NeutralOutpostModule neutralModule)
        {
            EnemyTypeId = enemyTypeId;
            WaveId = waveId;
            _destination = destination;
            _moveSpeed = Mathf.Max(0.1f, moveSpeed);
            _towerRegistry = towerRegistry;
            _player = player;
            _baseCore = baseCore;
            _neutralModule = neutralModule;
            _currentHealth = MaxHealth;
            SelectIntent();

            _healthBar = WorldHealthBar.Create(
                transform,
                new Vector3(0f, 1.45f, 0f),
                PrototypeMaterialRole.Enemy,
                1.5f);
            _healthBar.SetValue(_currentHealth, MaxHealth);
        }

        private void Update()
        {
            if (!IsAlive)
            {
                return;
            }

            _attackCooldown -= Time.deltaTime;

            switch (Intent)
            {
                case EnemyIntent.CaptureNeutral:
                    UpdateCaptureNeutral();
                    break;
                case EnemyIntent.AttackTower:
                    UpdateAttackTower();
                    break;
                default:
                    UpdateAttackBase();
                    break;
            }
        }

        public void TakeDamage(float amount)
        {
            if (!IsAlive || amount <= 0f)
            {
                return;
            }

            _currentHealth = Mathf.Max(0f, _currentHealth - amount);
            if (_healthBar != null) _healthBar.SetValue(_currentHealth, MaxHealth);
            if (_currentHealth <= 0f)
            {
                Killed?.Invoke(this);
                ReportExit();
                Destroy(gameObject);
            }
        }

        private void SelectIntent()
        {
            var roll = UnityEngine.Random.value;
            var outpost = _neutralModule != null ? _neutralModule.Outpost : null;

            if (roll < 0.15f)
            {
                Intent = _neutralModule != null && _neutralModule.FeatureEnabled &&
                         outpost != null && !outpost.IsAllied && !outpost.IsEnemyControlled
                    ? EnemyIntent.CaptureNeutral
                    : EnemyIntent.AttackBase;
            }
            else if (roll < 0.30f)
            {
                Intent = _towerRegistry != null && _towerRegistry.Count > 0
                    ? EnemyIntent.AttackTower
                    : EnemyIntent.AttackBase;
            }
            else
            {
                Intent = EnemyIntent.AttackBase;
            }

            Debug.Log($"[EnemyAgent] {name} Intent={Intent}", this);
        }

        private void UpdateCaptureNeutral()
        {
            var outpost = _neutralModule != null ? _neutralModule.Outpost : null;
            if (outpost == null || outpost.IsAllied || outpost.IsEnemyControlled)
            {
                Intent = EnemyIntent.AttackBase;
                return;
            }

            if (MoveTowards(outpost.transform, 2.2f))
            {
                _neutralCaptureProgress += Time.deltaTime;
                outpost.SetEnemyCaptureProgress(_neutralCaptureProgress / NeutralCaptureDuration);
                if (_neutralCaptureProgress >= NeutralCaptureDuration)
                {
                    outpost.CaptureByEnemy();
                    Intent = EnemyIntent.AttackBase;
                }
            }
        }

        private void UpdateAttackTower()
        {
            if (_attackTarget == null || !_attackTarget.IsAlive)
            {
                _attackTarget = _towerRegistry != null
                    ? _towerRegistry.FindClosestActive(transform.position, 1000f)
                    : null;
            }

            if (_attackTarget == null || !_attackTarget.IsAlive)
            {
                Intent = EnemyIntent.AttackBase;
                return;
            }

            if (MoveTowards(_attackTarget.TargetTransform, AttackDistance) && _attackCooldown <= 0f)
            {
                _attackTarget.TakeDamage(10f);
                _attackCooldown = 1f;
            }
        }

        private void UpdateAttackBase()
        {
            if (_player != null && _player.IsAlive &&
                Vector3.Distance(transform.position, _player.transform.position) <= AttackDistance)
            {
                if (_attackCooldown <= 0f)
                {
                    _player.TakeDamage(10f);
                    _attackCooldown = 1f;
                }
                return;
            }

            if (_destination == null)
            {
                ReportExit();
                Destroy(gameObject);
                return;
            }

            if (MoveTowards(_destination, ArrivalDistance))
            {
                if (_baseCore != null)
                {
                    _baseCore.TakeDamage(10f);
                }

                ReportExit();
                Destroy(gameObject);
            }
        }

        private bool MoveTowards(Transform target, float stopDistance)
        {
            if (target == null)
            {
                return true;
            }

            var targetPosition = target.position;
            targetPosition.y = transform.position.y;
            var direction = targetPosition - transform.position;
            if (direction.sqrMagnitude <= stopDistance * stopDistance)
            {
                return true;
            }

            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                _moveSpeed * Time.deltaTime);
            if (direction.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }

            return false;
        }

        private void OnDestroy()
        {
            ReportExit();
        }

        private void ReportExit()
        {
            if (_reportedExit)
            {
                return;
            }

            _reportedExit = true;
            Exited?.Invoke(this);
        }
    }
}
