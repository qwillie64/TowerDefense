using UnityEngine;

namespace CyberWizard.Gameplay.Towers
{
    /// <summary>固定防禦塔可升級的三個部位。</summary>
    public enum TowerPart
    {
        /// <summary>砲塔：攻擊與攻速。</summary>
        Turret = 0,

        /// <summary>塔身：血量。</summary>
        Body = 1,

        /// <summary>塔底：射程。</summary>
        Base = 2
    }

    /// <summary>
    /// 地圖固定防禦塔的數值與升級表（來源：Ref/開發規劃4.docx）。
    /// 三個部位各自從 1 級升到 maxLevel 級；第 n 次升級使用陣列中的第 n 個加成與花費。
    /// 只保存資料與計算，不含任何場景邏輯。
    /// </summary>
    [CreateAssetMenu(
        fileName = "FixedTowerProfile",
        menuName = "CyberWizard/Fixed Tower Profile")]
    public sealed class FixedTowerProfile : ScriptableObject
    {
        [Header("初始數值")]
        [SerializeField, Min(1f)] private float maxHealth = 200f;

        [SerializeField, Min(0.1f), Tooltip("企劃射程（未乘倍率）。")]
        private float attackRange = 8f;

        [SerializeField, Min(0f)] private float attackDamage = 10f;

        [SerializeField, Min(0.05f), Tooltip("每秒發射次數。")]
        private float shotsPerSecond = 1f;

        [SerializeField, Range(1, 10), Tooltip("各部位的最高等級。")]
        private int maxLevel = 5;

        [Header("地圖比例調整（暫定）")]
        [SerializeField, Min(0.1f), Tooltip("射程倍率：企劃射程與射程加成都乘上此值。暫定 3，配合 Level_1 的地圖比例。")]
        private float rangeMultiplier = 3f;

        [Header("發射物（暫定）")]
        [SerializeField, Min(1f)] private float projectileSpeed = 30f;

        [Header("塔底：射程（未乘倍率）")]
        [SerializeField] private float[] baseRangeBonuses = { 1f, 1.1f, 1.3f, 1.6f };
        [SerializeField] private int[] baseCosts = { 40, 80, 130, 200 };

        [Header("塔身：血量")]
        [SerializeField] private float[] bodyHealthBonuses = { 20f, 30f, 60f, 90f };
        [SerializeField] private int[] bodyCosts = { 40, 80, 130, 200 };

        [Header("砲塔：攻擊與攻速")]
        [SerializeField] private float[] turretDamageBonuses = { 4f, 6f, 8f, 12f };
        [SerializeField] private float[] turretRateBonuses = { 0.2f, 0.2f, 0.4f, 0.8f };
        [SerializeField] private int[] turretCosts = { 40, 80, 130, 200 };

        public float ProjectileSpeed => projectileSpeed;
        public float RangeMultiplier => rangeMultiplier;

        /// <summary>
        /// 部位實際可達到的最高等級：受 maxLevel 與升級表長度限制，避免表格不足時越界。
        /// </summary>
        public int GetMaxLevel(TowerPart part)
        {
            int upgradeCount;
            switch (part)
            {
                case TowerPart.Turret:
                    upgradeCount = Mathf.Min(
                        Length(turretCosts),
                        Mathf.Min(Length(turretDamageBonuses), Length(turretRateBonuses)));
                    break;
                case TowerPart.Body:
                    upgradeCount = Mathf.Min(Length(bodyCosts), Length(bodyHealthBonuses));
                    break;
                default:
                    upgradeCount = Mathf.Min(Length(baseCosts), Length(baseRangeBonuses));
                    break;
            }

            return Mathf.Clamp(upgradeCount + 1, 1, maxLevel);
        }

        /// <summary>塔底等級對應的實際射程（已乘倍率）。</summary>
        public float GetAttackRange(int baseLevel)
        {
            return (attackRange + SumBonuses(baseRangeBonuses, baseLevel, TowerPart.Base)) * rangeMultiplier;
        }

        /// <summary>塔身等級對應的血量上限。</summary>
        public float GetMaxHealth(int bodyLevel)
        {
            return maxHealth + SumBonuses(bodyHealthBonuses, bodyLevel, TowerPart.Body);
        }

        /// <summary>砲塔等級對應的每發傷害。</summary>
        public float GetDamage(int turretLevel)
        {
            return attackDamage + SumBonuses(turretDamageBonuses, turretLevel, TowerPart.Turret);
        }

        /// <summary>砲塔等級對應的每秒發射次數。</summary>
        public float GetShotsPerSecond(int turretLevel)
        {
            return shotsPerSecond + SumBonuses(turretRateBonuses, turretLevel, TowerPart.Turret);
        }

        /// <summary>從 currentLevel 升到下一級的花費；已達最高等級時回傳 false。</summary>
        public bool TryGetUpgradeCost(TowerPart part, int currentLevel, out int cost)
        {
            cost = 0;
            if (currentLevel < 1 || currentLevel >= GetMaxLevel(part))
            {
                return false;
            }

            var costs = part == TowerPart.Turret ? turretCosts : part == TowerPart.Body ? bodyCosts : baseCosts;
            cost = Mathf.Max(0, costs[currentLevel - 1]);
            return true;
        }

        private float SumBonuses(float[] bonuses, int level, TowerPart part)
        {
            var upgrades = Mathf.Clamp(level, 1, GetMaxLevel(part)) - 1;
            var total = 0f;
            for (var index = 0; index < upgrades && bonuses != null && index < bonuses.Length; index++)
            {
                total += bonuses[index];
            }

            return total;
        }

        private static int Length<T>(T[] values) => values != null ? values.Length : 0;

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            attackRange = Mathf.Max(0.1f, attackRange);
            attackDamage = Mathf.Max(0f, attackDamage);
            shotsPerSecond = Mathf.Max(0.05f, shotsPerSecond);
            rangeMultiplier = Mathf.Max(0.1f, rangeMultiplier);
            projectileSpeed = Mathf.Max(1f, projectileSpeed);
        }
    }
}
