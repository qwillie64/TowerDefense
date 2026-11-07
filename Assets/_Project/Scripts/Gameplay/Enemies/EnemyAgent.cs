using System;
using CyberWizard.Core;
using CyberWizard.Gameplay.GameFlow;
using CyberWizard.Gameplay.Neutral;
using CyberWizard.Gameplay.Player;
using CyberWizard.Gameplay.Towers;
using CyberWizard.Presentation;
using CyberWizard.Data;
using UnityEngine;

namespace CyberWizard.Gameplay.Enemies
{
    public sealed class EnemyAgent : MonoBehaviour, IDamageable
    {
        private const float ArrivalDistance = 0.08f;

        private Transform _destination;
        private bool _reportedExit;
        private float _neutralCaptureProgress;
        private IDamageable _attackTarget;
        private TowerRegistry _towerRegistry;
        private PrototypePlayerController _player;
        private BaseCore _baseCore;
        private NeutralOutpostModule _neutralModule;
        private WorldHealthBar _healthBar;
        private Vitality _vitality;
        private EnemyStatusController _status;
        private EnemyBehaviorSettings _behaviorSettings;
        private EnemyMovementMotor _movementMotor;
        private EnemyMeleeAttack _meleeAttack;

        public event Action<EnemyAgent> Exited;
        public event Action<EnemyAgent> Killed;

        public string WaveId { get; private set; }
        public string EnemyTypeId { get; private set; }
        public bool IsBoss { get; private set; }
        public int Reward { get; private set; }
        public EnemyIntent Intent { get; private set; }
        public bool IsAlive => _vitality != null && _vitality.IsAlive;
        public Faction Faction => Faction.Enemy;
        public Transform TargetTransform => transform;
        public float CurrentHealth => _vitality != null ? _vitality.Current : 0f;
        public float MaxHealth => _vitality != null ? _vitality.Maximum : 100f;

        public void Initialize(
            EnemyDefinition definition,
            string waveId,
            Transform destination,
            TowerRegistry towerRegistry,
            PrototypePlayerController player,
            BaseCore baseCore,
            NeutralOutpostModule neutralModule,
            EnemyBehaviorSettings behaviorSettings)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            EnemyTypeId = definition.EnemyTypeId;
            IsBoss = definition.IsBoss;
            Reward = definition.Reward;
            WaveId = waveId;
            _destination = destination;
            _movementMotor = new EnemyMovementMotor(transform, definition.MoveSpeed);
            _meleeAttack = new EnemyMeleeAttack(
                definition.ContactDamage,
                definition.AttackInterval);
            _towerRegistry = towerRegistry;
            _player = player;
            _baseCore = baseCore;
            _neutralModule = neutralModule;
            _behaviorSettings = behaviorSettings;
            _vitality = gameObject.AddComponent<Vitality>();
            _vitality.Changed += HandleVitalityChanged;
            _vitality.Depleted += HandleDepleted;
            _vitality.Initialize(definition.MaxHealth);
            _status = gameObject.AddComponent<EnemyStatusController>();
            _status.Configure(this);
            if (IsBoss)
            {
                var bossAbility = gameObject.AddComponent<BossAbilityController>();
                bossAbility.Configure(this, player, towerRegistry);
            }
            SelectIntent();

            _healthBar = WorldHealthBar.Create(
                transform,
                new Vector3(0f, IsBoss ? 1.65f : 1.45f, 0f),
                PrototypeMaterialRole.Enemy,
                IsBoss ? 4f : 1.5f);
            _healthBar.SetValue(CurrentHealth, MaxHealth);
        }

        private void Update()
        {
            if (!IsAlive)
            {
                return;
            }

            _meleeAttack?.Tick(Time.deltaTime);
            if (_status != null && !_status.CanAct)
            {
                return;
            }

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
            _vitality?.TakeDamage(amount);
        }

        public void ApplySlow(float multiplier, float duration) => _status?.ApplySlow(multiplier, duration);
        public void ApplyStun(float duration) => _status?.ApplyStun(duration);
        public void ApplyBurn(float damagePerSecond, float duration) => _status?.ApplyBurn(damagePerSecond, duration);

        private void HandleVitalityChanged(float current, float maximum)
        {
            if (_healthBar != null) _healthBar.SetValue(current, maximum);
        }

        private void HandleDepleted()
        {
            Killed?.Invoke(this);
            ReportExit();
            Destroy(gameObject);
        }

        private void SelectIntent()
        {
            var outpost = _neutralModule != null ? _neutralModule.Outpost : null;
            var captureChance = _behaviorSettings != null
                ? _behaviorSettings.CaptureNeutralChance
                : 0.15f;
            var towerChance = _behaviorSettings != null
                ? _behaviorSettings.AttackTowerChance
                : 0.15f;
            var canCaptureNeutral = _neutralModule != null && _neutralModule.FeatureEnabled &&
                                    outpost != null && !outpost.IsAllied &&
                                    !outpost.IsEnemyControlled;
            var hasTowerTarget = _towerRegistry != null && _towerRegistry.Count > 0;

            Intent = EnemyIntentSelector.Select(
                UnityEngine.Random.value,
                captureChance,
                towerChance,
                canCaptureNeutral,
                hasTowerTarget);

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
                var captureDuration = _behaviorSettings != null
                    ? _behaviorSettings.NeutralCaptureDuration
                    : 3f;
                outpost.SetEnemyCaptureProgress(_neutralCaptureProgress / captureDuration);
                if (_neutralCaptureProgress >= captureDuration)
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

            if (MoveTowards(_attackTarget.TargetTransform, GetAttackDistance()))
            {
                _meleeAttack?.TryAttack(_attackTarget);
            }
        }

        private void UpdateAttackBase()
        {
            if (_player != null && _player.IsAlive &&
                Vector3.Distance(transform.position, _player.transform.position) <= GetAttackDistance())
            {
                _meleeAttack?.TryAttack(_player);
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
                    if (IsBoss)
                    {
                        _meleeAttack?.TryAttack(_baseCore);
                        return;
                    }

                    _meleeAttack?.AttackImmediately(_baseCore);
                }

                ReportExit();
                Destroy(gameObject);
            }
        }

        private bool MoveTowards(Transform target, float stopDistance)
        {
            return _movementMotor == null || _movementMotor.MoveTowards(
                target,
                stopDistance,
                _status != null ? _status.SpeedMultiplier : 1f,
                Time.deltaTime);
        }

        private float GetAttackDistance()
        {
            return _behaviorSettings != null ? _behaviorSettings.AttackDistance : 2.6f;
        }

        private void OnDestroy()
        {
            if (_vitality != null)
            {
                _vitality.Changed -= HandleVitalityChanged;
                _vitality.Depleted -= HandleDepleted;
            }

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
