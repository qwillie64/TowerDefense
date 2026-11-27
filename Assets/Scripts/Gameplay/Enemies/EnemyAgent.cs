using System;
using System.Collections.Generic;
using CyberWizard.Core;
using CyberWizard.Data;
using CyberWizard.Gameplay.GameFlow;
using CyberWizard.Gameplay.Player;
using CyberWizard.Presentation;
using UnityEngine;
using UnityEngine.AI;

namespace CyberWizard.Gameplay.Enemies
{
    /// <summary>
    /// 敵人的生命週期與行為：以 NavMeshAgent 依序走向路線上的每個路徑點，
    /// 途中玩家進入近戰距離時停下攻擊玩家，走完路線後攻擊目標（閘門）。
    /// 需要場景中已有 NavMesh（由 LevelNavMesh 建立）。
    /// </summary>
    public sealed class EnemyAgent : MonoBehaviour, IDamageable
    {
        /// <summary>走到路徑終點後停住，持續攻擊目標（例如閘門），不會自行離場。</summary>
        public const string SiegeObjective = "siege";

        private const float NavMeshSampleRadius = 4f;

        private static readonly List<EnemyAgent> ActiveAgents = new List<EnemyAgent>();
        private static EnemyBehaviorSettings _defaultSettings;

        private Transform[] _route = Array.Empty<Transform>();
        private int _routeIndex;
        private bool _holdAtRouteEnd;
        private bool _reportedExit;
        private bool _halted;
        private PrototypePlayerController _player;
        private BaseCore _baseCore;
        private EnemyBehaviorSettings _settings;
        private NavMeshAgent _navAgent;
        private EnemyMeleeAttack _meleeAttack;
        private Vitality _vitality;
        private WorldHealthBar _healthBar;

        /// <summary>目前啟用中的敵人（含已死亡但尚未銷毀者，使用前請檢查 IsAlive）。</summary>
        public static IReadOnlyList<EnemyAgent> Active => ActiveAgents;

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

        private Vector3 AttackOrigin => transform.position + Vector3.up * 0.7f;

        /// <param name="route">依序要走過的路徑點；最後一點是攻擊目標前的停留位置。</param>
        /// <param name="holdAtRouteEnd">true：走完路線後停住並持續攻擊目標（圍攻）；false：攻擊一次後離場。</param>
        public void Initialize(
            EnemyDefinition definition,
            string waveId,
            Transform[] route,
            PrototypePlayerController player,
            BaseCore baseCore,
            EnemyBehaviorSettings settings,
            bool holdAtRouteEnd)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            EnemyTypeId = definition.EnemyTypeId;
            IsBoss = definition.IsBoss;
            Reward = definition.Reward;
            WaveId = waveId;
            _route = route ?? Array.Empty<Transform>();
            _holdAtRouteEnd = holdAtRouteEnd;
            _player = player;
            _baseCore = baseCore;
            _settings = settings != null ? settings : DefaultSettings;
            _meleeAttack = new EnemyMeleeAttack(definition.ContactDamage, definition.AttackInterval);

            _vitality = gameObject.AddComponent<Vitality>();
            _vitality.Changed += HandleVitalityChanged;
            _vitality.Depleted += HandleDepleted;
            _vitality.Initialize(definition.MaxHealth);
            if (IsBoss)
            {
                gameObject.AddComponent<BossAbilityController>().Configure(this, player);
            }

            _healthBar = WorldHealthBar.Create(
                transform,
                new Vector3(0f, IsBoss ? 1.65f : 1.45f, 0f),
                PrototypeMaterialRole.Enemy,
                IsBoss ? 4f : 1.5f);
            _healthBar.SetValue(CurrentHealth, MaxHealth);

            if (CreateNavAgent(definition.MoveSpeed))
            {
                SetNextDestination();
            }
        }

        private static EnemyBehaviorSettings DefaultSettings
        {
            get
            {
                if (_defaultSettings == null)
                {
                    _defaultSettings = ScriptableObject.CreateInstance<EnemyBehaviorSettings>();
                    _defaultSettings.hideFlags = HideFlags.HideAndDontSave;
                }

                return _defaultSettings;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActiveAgents() => ActiveAgents.Clear();

        private void OnEnable() => ActiveAgents.Add(this);

        private void OnDisable() => ActiveAgents.Remove(this);

        /// <summary>停止所有行動（移動與攻擊），例如關卡已判定失敗時。</summary>
        public void Halt()
        {
            _halted = true;
            SetMoving(false);
        }

        public void TakeDamage(float amount)
        {
            _vitality?.TakeDamage(amount);
        }

        private void Update()
        {
            if (!IsAlive || _halted || _navAgent == null)
            {
                return;
            }

            _meleeAttack.Tick(Time.deltaTime);
            if (_player != null && _player.IsAlive &&
                Vector3.Distance(transform.position, _player.transform.position) <= _settings.AttackDistance)
            {
                SetMoving(false);
                _meleeAttack.TryAttack(_player, AttackOrigin);
                return;
            }

            if (_routeIndex < _route.Length)
            {
                SetMoving(true);
                if (!_navAgent.pathPending &&
                    _navAgent.remainingDistance <= _settings.WaypointArrivalDistance)
                {
                    _routeIndex++;
                    SetNextDestination();
                }

                return;
            }

            SetMoving(false);
            AttackAtRouteEnd();
        }

        /// <summary>
        /// 路線走完後的行為。圍攻與 Boss：停在原地持續攻擊（目標已毀壞時仍留在原地）；
        /// 其他敵人：立即攻擊一次後離場。
        /// </summary>
        private void AttackAtRouteEnd()
        {
            if (_holdAtRouteEnd || IsBoss)
            {
                if (_baseCore != null)
                {
                    _meleeAttack.TryAttack(_baseCore, AttackOrigin);
                }

                return;
            }

            if (_baseCore != null)
            {
                _meleeAttack.AttackImmediately(_baseCore, AttackOrigin);
            }

            ReportExit();
            Destroy(gameObject);
        }

        private bool CreateNavAgent(float moveSpeed)
        {
            if (!NavMesh.SamplePosition(transform.position, out var hit, NavMeshSampleRadius, NavMesh.AllAreas))
            {
                Debug.LogError($"{name} 的出生點附近沒有 NavMesh，敵人無法移動。請確認場景中有 LevelNavMesh。", this);
                return false;
            }

            // NavMeshAgent 的位置是腳底；baseOffset 讓模型中心高於地面（以本地座標計算）。
            var bodyRenderer = GetComponent<Renderer>();
            var footOffset = bodyRenderer != null ? transform.position.y - bodyRenderer.bounds.min.y : 0f;

            // NavMeshAgent 啟用時會立刻把物件貼到 NavMesh 上；先停用物件，設定好 baseOffset 再啟用，
            // 避免出生當幀以 baseOffset = 0 貼地而半埋進地面。
            gameObject.SetActive(false);
            _navAgent = gameObject.AddComponent<NavMeshAgent>();
            _navAgent.baseOffset = footOffset / Mathf.Max(0.0001f, transform.lossyScale.y);
            _navAgent.speed = moveSpeed;
            _navAgent.acceleration = _settings.Acceleration;
            _navAgent.angularSpeed = _settings.AngularSpeed;
            _navAgent.stoppingDistance = 0f;
            _navAgent.obstacleAvoidanceType = _settings.AvoidOtherEnemies
                ? ObstacleAvoidanceType.LowQualityObstacleAvoidance
                : ObstacleAvoidanceType.NoObstacleAvoidance;
            transform.position = hit.position + Vector3.up * footOffset;
            gameObject.SetActive(true);
            return _navAgent.isOnNavMesh;
        }

        /// <summary>把目的地設為下一個有效路徑點；略過遺失或無法規劃路徑的點。</summary>
        private void SetNextDestination()
        {
            for (; _routeIndex < _route.Length; _routeIndex++)
            {
                var waypoint = _route[_routeIndex];
                if (waypoint == null)
                {
                    continue;
                }

                // 中途點不減速，直接轉向下一點；最後一點才煞車停在終點。
                _navAgent.autoBraking = _routeIndex == _route.Length - 1;
                if (_navAgent.SetDestination(waypoint.position))
                {
                    return;
                }

                Debug.LogWarning($"{name} 無法規劃到路徑點 {waypoint.name} 的路線，略過此點。", this);
            }
        }

        private void SetMoving(bool moving)
        {
            if (_navAgent == null || !_navAgent.isOnNavMesh || _navAgent.isStopped != moving)
            {
                return;
            }

            _navAgent.isStopped = !moving;
            if (!moving)
            {
                _navAgent.velocity = Vector3.zero;
            }
        }

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
