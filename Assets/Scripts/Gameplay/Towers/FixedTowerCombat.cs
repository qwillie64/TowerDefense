using CyberWizard.Core;
using CyberWizard.Gameplay.Combat;
using CyberWizard.Gameplay.Enemies;
using CyberWizard.Gameplay.Level;
using CyberWizard.Gameplay.Waves;
using CyberWizard.Presentation;
using UnityEngine;

namespace CyberWizard.Gameplay.Towers
{
    /// <summary>
    /// 地圖固定防禦塔的戰鬥與升級狀態：
    /// 自動選取射程內最近的敵人、轉動砲塔瞄準、發射鉚釘並播放砲口火焰；
    /// 三個部位（砲塔、塔身、塔底）各自保存等級，數值由 FixedTowerProfile 計算。
    /// 能量的扣除由呼叫端（TowerUpgradeInteractor）負責，本元件只套用升級。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FixedTower))]
    public sealed class FixedTowerCombat : MonoBehaviour, IDamageable
    {
        [SerializeField] private FixedTowerProfile profile;
        [SerializeField, Tooltip("提供場上存活敵人清單。")] private WaveDirector waveDirector;

        [Header("外觀與特效")]
        [SerializeField, Tooltip("砲塔旋轉樞紐（含砲管）。")] private Transform turretPivot;
        [SerializeField, Tooltip("發射點。")] private Transform muzzle;
        [SerializeField, Tooltip("砲口火焰，發射時播放。")] private ParticleSystem muzzleFlash;
        [SerializeField, Tooltip("鉚釘使用的金屬材質。")] private Material rivetMaterial;
        [SerializeField, Tooltip("鉚釘大小（圓錐：X、Y 為直徑，Z 為長度）。暫定值。")]
        private Vector3 rivetScale = new Vector3(0.45f, 0.45f, 1.4f);

        [Header("瞄準（暫定值）")]
        [SerializeField, Min(1f), Tooltip("砲塔每秒最大轉動角度。")]
        private float turnSpeed = 240f;

        [SerializeField, Range(1f, 45f), Tooltip("砲管與目標方向的夾角小於此值才發射。")]
        private float fireAngleTolerance = 8f;

        [SerializeField, Range(0f, 60f), Tooltip("砲管上下俯仰的最大角度。")]
        private float maxPitch = 35f;

        [SerializeField, Tooltip("血條相對於塔底的位置。")]
        private Vector3 healthBarOffset = new Vector3(0f, 9.6f, 0f);

        private readonly int[] _levels = { 1, 1, 1 };
        private Vitality _vitality;
        private WorldHealthBar _healthBar;
        private GroundRangeIndicator _rangeIndicator;
        private FixedTower _fixedTower;
        private EnemyAgent _target;
        private float _cooldown;

        public bool IsAlive => _vitality != null && _vitality.IsAlive;
        public Faction Faction => Faction.Player;
        public Transform TargetTransform => transform;
        public FixedTowerProfile Profile => profile;
        public int TowerNumber => _fixedTower != null ? _fixedTower.TowerNumber : 0;
        public float FootprintRadius => _fixedTower != null ? _fixedTower.BaseRadius : 4f;
        public float Health => _vitality != null ? _vitality.Current : 0f;
        public float MaxHealth => _vitality != null ? _vitality.Maximum : 0f;
        public float AttackRange => profile != null ? profile.GetAttackRange(GetLevel(TowerPart.Base)) : 0f;
        public float Damage => profile != null ? profile.GetDamage(GetLevel(TowerPart.Turret)) : 0f;
        public float ShotsPerSecond => profile != null ? profile.GetShotsPerSecond(GetLevel(TowerPart.Turret)) : 0f;
        public EnemyAgent CurrentTarget => _target;
        public Transform TurretPivot => turretPivot;
        public ParticleSystem MuzzleFlash => muzzleFlash;

        private void Awake()
        {
            _fixedTower = GetComponent<FixedTower>();
            if (profile == null)
            {
                Debug.LogError($"{name} 缺少 FixedTowerProfile，塔不會攻擊。", this);
            }

            if (waveDirector == null)
            {
                Debug.LogError($"{name} 缺少 WaveDirector，塔找不到敵人。", this);
            }

            _vitality = gameObject.AddComponent<Vitality>();
            _vitality.Changed += HandleVitalityChanged;
            _vitality.Depleted += HandleDepleted;
            _vitality.Initialize(profile != null ? profile.GetMaxHealth(1) : 1f);

            _healthBar = WorldHealthBar.Create(transform, healthBarOffset, PrototypeMaterialRole.Player, 6f);
            _healthBar.SetValue(Health, MaxHealth);

            // 射程圈持續顯示；塔被摧毀時才隱藏。
            _rangeIndicator = GroundRangeIndicator.Create(transform, "Attack Range Circle", AttackRange, 0.4f);
        }

        public int GetLevel(TowerPart part) => _levels[(int)part];

        public int GetMaxLevel(TowerPart part) => profile != null ? profile.GetMaxLevel(part) : 1;

        /// <summary>下一級的花費；已滿級或沒有設定資料時回傳 false。</summary>
        public bool TryGetUpgradeCost(TowerPart part, out int cost)
        {
            cost = 0;
            return profile != null && profile.TryGetUpgradeCost(part, GetLevel(part), out cost);
        }

        /// <summary>
        /// 把指定部位升一級並套用數值。不檢查能量，呼叫端需先扣除花費。
        /// 已滿級或塔已被摧毀時回傳 false。
        /// </summary>
        public bool ApplyUpgrade(TowerPart part)
        {
            if (!IsAlive || !TryGetUpgradeCost(part, out _))
            {
                return false;
            }

            var index = (int)part;
            _levels[index]++;
            switch (part)
            {
                case TowerPart.Body:
                    // 血量上限增加，增加的量同時補到目前血量。
                    var newMaximum = profile.GetMaxHealth(_levels[index]);
                    _vitality.IncreaseMaximum(newMaximum - _vitality.Maximum);
                    break;
                case TowerPart.Base:
                    _rangeIndicator.SetRadius(AttackRange);
                    break;
            }

            PrototypePulseEffect.Spawn(
                transform.position + Vector3.up * (_fixedTower != null ? _fixedTower.TotalHeight : 8f),
                PrototypeMaterialRole.Player,
                Vector3.one * 3f,
                0.4f);
            return true;
        }

        public void SetRangeVisible(bool visible)
        {
            if (_rangeIndicator != null && _rangeIndicator.gameObject.activeSelf != visible)
            {
                _rangeIndicator.gameObject.SetActive(visible);
            }
        }

        public void TakeDamage(float amount)
        {
            _vitality?.TakeDamage(amount);
        }

        private void Update()
        {
            if (!IsAlive || profile == null || waveDirector == null)
            {
                return;
            }

            _cooldown = Mathf.Max(0f, _cooldown - Time.deltaTime);
            if (!IsTargetValid(_target))
            {
                _target = FindClosestTarget();
            }

            // 目標無效時已重新搜尋整個射程，所以「有目標」等同於「射程內有敵人」。
            _rangeIndicator.SetAlert(_target != null);
            if (_target == null)
            {
                return;
            }

            var aimError = AimAt(_target.TargetTransform.position);
            if (_cooldown <= 0f && aimError <= fireAngleTolerance)
            {
                Fire();
                _cooldown = 1f / Mathf.Max(0.05f, ShotsPerSecond);
            }
        }

        private bool IsTargetValid(EnemyAgent enemy)
        {
            return enemy != null && enemy.IsAlive && IsInRange(enemy.transform.position);
        }

        private bool IsInRange(Vector3 position)
        {
            // 射程以水平距離計算，與地面上的射程圈一致。
            var offset = position - transform.position;
            offset.y = 0f;
            var range = AttackRange;
            return offset.sqrMagnitude <= range * range;
        }

        private EnemyAgent FindClosestTarget()
        {
            EnemyAgent closest = null;
            var bestDistance = float.MaxValue;
            foreach (var enemy in waveDirector.AliveEnemies)
            {
                if (enemy == null || !enemy.IsAlive || !IsInRange(enemy.transform.position))
                {
                    continue;
                }

                var offset = enemy.transform.position - transform.position;
                offset.y = 0f;
                var distance = offset.sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    closest = enemy;
                }
            }

            return closest;
        }

        /// <summary>轉動砲塔朝向目標，回傳砲管與目標方向的剩餘夾角（度）。</summary>
        private float AimAt(Vector3 targetPosition)
        {
            if (turretPivot == null)
            {
                return 0f;
            }

            var origin = turretPivot.position;
            var direction = targetPosition - origin;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return 0f;
            }

            var flat = new Vector3(direction.x, 0f, direction.z);
            var pitch = -Mathf.Atan2(direction.y, Mathf.Max(0.0001f, flat.magnitude)) * Mathf.Rad2Deg;
            pitch = Mathf.Clamp(pitch, -maxPitch, maxPitch);
            var yaw = flat.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(flat).eulerAngles.y
                : turretPivot.eulerAngles.y;
            var desired = Quaternion.Euler(pitch, yaw, 0f);
            turretPivot.rotation = Quaternion.RotateTowards(
                turretPivot.rotation,
                desired,
                turnSpeed * Time.deltaTime);
            return Vector3.Angle(turretPivot.forward, direction);
        }

        private void Fire()
        {
            var origin = muzzle != null ? muzzle.position : transform.position + Vector3.up * 6f;
            var forward = turretPivot != null ? turretPivot.forward : transform.forward;
            RivetProjectile.Spawn(
                origin,
                forward,
                _target,
                Damage,
                profile.ProjectileSpeed,
                rivetMaterial,
                rivetScale);
            if (muzzleFlash != null)
            {
                muzzleFlash.Play(true);
            }
        }

        private void HandleVitalityChanged(float current, float maximum)
        {
            if (_healthBar != null)
            {
                _healthBar.SetValue(current, maximum);
            }
        }

        private void HandleDepleted()
        {
            // 目前沒有敵人會攻擊塔；保留被摧毀時停止攻擊的處理。
            _target = null;
            _rangeIndicator.SetAlert(false);
            SetRangeVisible(false);
            Debug.Log($"[FixedTowerCombat] {name} 已被摧毀，停止攻擊。", this);
        }

        private void OnDestroy()
        {
            if (_vitality != null)
            {
                _vitality.Changed -= HandleVitalityChanged;
                _vitality.Depleted -= HandleDepleted;
            }
        }
    }
}
