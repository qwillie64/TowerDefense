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
    public enum EnemyIntent
    {
        AttackBase,
        CaptureNeutral,
        AttackTower
    }

    public sealed class EnemyAgent : MonoBehaviour, IDamageable
    {
        private const float ArrivalDistance = 0.08f;

        private Transform _destination;
        private float _moveSpeed;
        private bool _reportedExit;
        private float _attackCooldown;
        private float _contactDamage;
        private float _attackInterval;
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
            _moveSpeed = definition.MoveSpeed;
            _contactDamage = definition.ContactDamage;
            _attackInterval = definition.AttackInterval;
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

            _attackCooldown -= Time.deltaTime;
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
            var roll = UnityEngine.Random.value;
            var outpost = _neutralModule != null ? _neutralModule.Outpost : null;

            var captureChance = _behaviorSettings != null
                ? _behaviorSettings.CaptureNeutralChance
                : 0.15f;
            var towerThreshold = captureChance + (_behaviorSettings != null
                ? _behaviorSettings.AttackTowerChance
                : 0.15f);

            if (roll < captureChance)
            {
                Intent = _neutralModule != null && _neutralModule.FeatureEnabled &&
                         outpost != null && !outpost.IsAllied && !outpost.IsEnemyControlled
                    ? EnemyIntent.CaptureNeutral
                    : EnemyIntent.AttackBase;
            }
            else if (roll < towerThreshold)
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

            if (MoveTowards(_attackTarget.TargetTransform, GetAttackDistance()) && _attackCooldown <= 0f)
            {
                _attackTarget.TakeDamage(_contactDamage);
                _attackCooldown = _attackInterval;
            }
        }

        private void UpdateAttackBase()
        {
            if (_player != null && _player.IsAlive &&
                Vector3.Distance(transform.position, _player.transform.position) <= GetAttackDistance())
            {
                if (_attackCooldown <= 0f)
                {
                    _player.TakeDamage(_contactDamage);
                    _attackCooldown = _attackInterval;
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
                    if (IsBoss)
                    {
                        if (_attackCooldown <= 0f)
                        {
                            _baseCore.TakeDamage(_contactDamage);
                            _attackCooldown = _attackInterval;
                        }
                        return;
                    }

                    _baseCore.TakeDamage(_contactDamage);
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
                _moveSpeed * (_status != null ? _status.SpeedMultiplier : 1f) * Time.deltaTime);
            if (direction.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }

            return false;
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
