using System;
using System.Collections.Generic;
using CyberWizard.Core;
using CyberWizard.Gameplay.GameFlow;
using CyberWizard.Gameplay.Player;
using CyberWizard.Presentation;
using CyberWizard.Data;
using CyberWizard.Gameplay.Movement;
using UnityEngine;

namespace CyberWizard.Gameplay.Enemies
{
    public sealed class EnemyAgent : MonoBehaviour, IDamageable
    {
        private const float ArrivalDistance = 0.08f;

        private static readonly List<EnemyAgent> ActiveAgents = new List<EnemyAgent>();

        /// <summary>目前啟用中的敵人（含已死亡但尚未銷毀者，使用前請檢查 IsAlive）。</summary>
        public static IReadOnlyList<EnemyAgent> Active => ActiveAgents;

        /// <summary>走到路徑終點後停住，持續攻擊目標（例如閘門），不會自行離場。</summary>
        public const string SiegeObjective = "siege";

        private Transform _destination;
        private bool _reportedExit;
        private PrototypePlayerController _player;
        private BaseCore _baseCore;
        private WorldHealthBar _healthBar;
        private Vitality _vitality;
        private EnemyStatusController _status;
        private EnemyBehaviorSettings _behaviorSettings;
        private EnemyMovementMotor _movementMotor;
        private EnemyMeleeAttack _meleeAttack;
        private Transform[] _path;
        private int _pathIndex;
        private bool _holdAtDestination;
        private bool _halted;

        public event Action<EnemyAgent> Exited;
        public event Action<EnemyAgent> Killed;

        public string WaveId { get; private set; }
        public string EnemyTypeId { get; private set; }
        public bool IsBoss { get; private set; }
        public int Reward { get; private set; }
        public bool IsAlive => _vitality != null && _vitality.IsAlive;
        public Faction Faction => Faction.Enemy;
        public Transform TargetTransform => transform;
        public float CurrentHealth => _vitality != null ? _vitality.Current : 0f;
        public float MaxHealth => _vitality != null ? _vitality.Maximum : 100f;

        public void Initialize(
            EnemyDefinition definition,
            string waveId,
            Transform destination,
            PrototypePlayerController player,
            BaseCore baseCore,
            EnemyBehaviorSettings behaviorSettings,
            Transform[] path = null,
            string objective = null,
            bool followPathHeight = false)
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
            if (followPathHeight)
            {
                // 路徑點放在地表上；物件中心要再高出腳底到中心的距離。
                var bodyRenderer = GetComponent<Renderer>();
                var footOffset = bodyRenderer != null ? transform.position.y - bodyRenderer.bounds.min.y : 0f;
                transform.position += Vector3.up * footOffset;
                _movementMotor = new EnemyMovementMotor(transform, definition.MoveSpeed, true, footOffset);
            }
            else
            {
                _movementMotor = new EnemyMovementMotor(transform, definition.MoveSpeed);
            }

            _meleeAttack = new EnemyMeleeAttack(
                definition.ContactDamage,
                definition.AttackInterval);
            _player = player;
            _baseCore = baseCore;
            _behaviorSettings = behaviorSettings;
            _path = path;
            _holdAtDestination = string.Equals(objective, SiegeObjective, StringComparison.OrdinalIgnoreCase);

            // 跟隨路徑高度時由路徑點決定高度，不再套用重力。
            if (!followPathHeight && GetComponent<GroundGravity>() == null)
            {
                gameObject.AddComponent<GroundGravity>();
            }
            _vitality = gameObject.AddComponent<Vitality>();
            _vitality.Changed += HandleVitalityChanged;
            _vitality.Depleted += HandleDepleted;
            _vitality.Initialize(definition.MaxHealth);
            _status = gameObject.AddComponent<EnemyStatusController>();
            _status.Configure(this);
            if (IsBoss)
            {
                var bossAbility = gameObject.AddComponent<BossAbilityController>();
                bossAbility.Configure(this, player);
            }

            _healthBar = WorldHealthBar.Create(
                transform,
                new Vector3(0f, IsBoss ? 1.65f : 1.45f, 0f),
                PrototypeMaterialRole.Enemy,
                IsBoss ? 4f : 1.5f);
            _healthBar.SetValue(CurrentHealth, MaxHealth);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActiveAgents() => ActiveAgents.Clear();

        private void OnEnable() => ActiveAgents.Add(this);

        private void OnDisable() => ActiveAgents.Remove(this);

        /// <summary>停止所有行動（移動與攻擊），例如關卡已判定失敗時。</summary>
        public void Halt()
        {
            _halted = true;
        }

        private void Update()
        {
            if (!IsAlive || _halted)
            {
                return;
            }

            _meleeAttack?.Tick(Time.deltaTime);
            if (_status != null && !_status.CanAct)
            {
                return;
            }

            UpdateAttackBase();
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

        private void UpdateAttackBase()
        {
            if (_player != null && _player.IsAlive &&
                Vector3.Distance(transform.position, _player.transform.position) <= GetAttackDistance())
            {
                _meleeAttack?.TryAttack(_player, transform.position + Vector3.up * 0.7f);
                return;
            }

            if (_destination == null)
            {
                ReportExit();
                Destroy(gameObject);
                return;
            }

            if (MoveAlongPath(ArrivalDistance) || (_path == null && MoveTowards(_destination, ArrivalDistance)))
            {
                // 圍攻：停在終點持續攻擊；沒有目標或目標已毀壞時仍留在原地，不會離場。
                if (_holdAtDestination)
                {
                    if (_baseCore != null)
                    {
                        _meleeAttack?.TryAttack(_baseCore, transform.position + Vector3.up * 0.7f);
                    }

                    return;
                }

                if (_baseCore != null)
                {
                    if (IsBoss)
                    {
                        _meleeAttack?.TryAttack(_baseCore, transform.position + Vector3.up * 0.7f);
                        return;
                    }

                    _meleeAttack?.AttackImmediately(_baseCore, transform.position + Vector3.up * 0.7f);
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

        private bool MoveAlongPath(float stopDistance)
        {
            if (_path == null || _path.Length == 0)
            {
                return false;
            }

            while (_pathIndex < _path.Length && _path[_pathIndex] == null)
            {
                _pathIndex++;
            }

            if (_pathIndex >= _path.Length)
            {
                return true;
            }

            if (MoveTowards(_path[_pathIndex], stopDistance))
            {
                _pathIndex++;
            }

            return _pathIndex >= _path.Length;
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
