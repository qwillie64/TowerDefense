using UnityEngine;

namespace CyberWizard.Gameplay.Towers
{
    [CreateAssetMenu(
        fileName = "TowerDefinition",
        menuName = "CyberWizard/Tower Definition")]
    public sealed class TowerDefinition : ScriptableObject
    {
        [SerializeField] private string towerTypeId = "tower_fire";
        [SerializeField] private ElementType element = ElementType.Fire;
        [SerializeField, Min(1f)] private float maxDurability = 100f;
        [SerializeField, Min(0f)] private float attackDamage = 20f;
        [SerializeField, Min(0.1f)] private float attackRange = 6.5f;
        [SerializeField, Min(0.05f)] private float attackInterval = 1f;
        [SerializeField, Min(0.1f)] private float projectileSpeed = 15f;
        [SerializeField, Min(0f)] private float durabilityCostPerShot = 2f;
        [SerializeField, Min(0)] private int buildCost = 25;
        [SerializeField] private PrimitiveType prototypeTopShape = PrimitiveType.Sphere;
        [SerializeField, Min(1)] private int maxLevel = 3;
        [SerializeField, Min(0)] private int upgradeBaseCost = 20;
        [SerializeField, Min(0f)] private float damagePerLevel = 0.3f;
        [SerializeField, Min(0f)] private float rangePerLevel = 0.4f;
        [SerializeField, Min(0f)] private float durabilityPerLevel = 20f;

        public string TowerTypeId => towerTypeId;
        public ElementType Element => element;
        public float MaxDurability => maxDurability;
        public float AttackDamage => attackDamage;
        public float AttackRange => attackRange;
        public float AttackInterval => attackInterval;
        public float ProjectileSpeed => projectileSpeed;
        public float DurabilityCostPerShot => durabilityCostPerShot;
        public int BuildCost => buildCost;
        public PrimitiveType PrototypeTopShape => prototypeTopShape;
        public int MaxLevel => maxLevel;
        public int UpgradeBaseCost => upgradeBaseCost;
        public float DamagePerLevel => damagePerLevel;
        public float RangePerLevel => rangePerLevel;
        public float DurabilityPerLevel => durabilityPerLevel;

        public void Configure(
            string typeId,
            ElementType newElement,
            float durability,
            float damage,
            float range,
            float interval,
            float speed,
            float shotCost,
            int cost,
            PrimitiveType topShape,
            int newMaxLevel = 3,
            int newUpgradeBaseCost = 20,
            float newDamagePerLevel = 0.3f,
            float newRangePerLevel = 0.4f,
            float newDurabilityPerLevel = 20f)
        {
            towerTypeId = typeId;
            element = newElement;
            maxDurability = Mathf.Max(1f, durability);
            attackDamage = Mathf.Max(0f, damage);
            attackRange = Mathf.Max(0.1f, range);
            attackInterval = Mathf.Max(0.05f, interval);
            projectileSpeed = Mathf.Max(0.1f, speed);
            durabilityCostPerShot = Mathf.Max(0f, shotCost);
            buildCost = Mathf.Max(0, cost);
            prototypeTopShape = topShape;
            maxLevel = Mathf.Max(1, newMaxLevel);
            upgradeBaseCost = Mathf.Max(0, newUpgradeBaseCost);
            damagePerLevel = Mathf.Max(0f, newDamagePerLevel);
            rangePerLevel = Mathf.Max(0f, newRangePerLevel);
            durabilityPerLevel = Mathf.Max(0f, newDurabilityPerLevel);
        }

        private void OnValidate()
        {
            maxDurability = Mathf.Max(1f, maxDurability);
            attackDamage = Mathf.Max(0f, attackDamage);
            attackRange = Mathf.Max(0.1f, attackRange);
            attackInterval = Mathf.Max(0.05f, attackInterval);
            projectileSpeed = Mathf.Max(0.1f, projectileSpeed);
            durabilityCostPerShot = Mathf.Max(0f, durabilityCostPerShot);
            buildCost = Mathf.Max(0, buildCost);
            maxLevel = Mathf.Max(1, maxLevel);
            upgradeBaseCost = Mathf.Max(0, upgradeBaseCost);
            damagePerLevel = Mathf.Max(0f, damagePerLevel);
            rangePerLevel = Mathf.Max(0f, rangePerLevel);
            durabilityPerLevel = Mathf.Max(0f, durabilityPerLevel);
        }
    }
}
