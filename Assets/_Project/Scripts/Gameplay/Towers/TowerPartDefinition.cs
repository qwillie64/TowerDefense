using UnityEngine;

namespace CyberWizard.Gameplay.Towers
{
    public enum TowerPartSlot
    {
        Base,
        Body,
        Top
    }

    [CreateAssetMenu(
        fileName = "TowerPartDefinition",
        menuName = "CyberWizard/Tower Part Definition")]
    public sealed class TowerPartDefinition : ScriptableObject
    {
        [SerializeField] private string partId = "part_base_balanced";
        [SerializeField] private string displayName = "均衡部件";
        [SerializeField] private TowerPartSlot slot;
        [SerializeField] private PrimitiveType prototypeShape = PrimitiveType.Cylinder;
        [SerializeField] private Vector3 prototypeScale = Vector3.one;
        [SerializeField, Min(0.1f)] private float durabilityMultiplier = 1f;
        [SerializeField, Min(0f)] private float damageMultiplier = 1f;
        [SerializeField] private float rangeAdditive;
        [SerializeField, Min(0.1f)] private float attackIntervalMultiplier = 1f;
        [SerializeField, Min(0.1f)] private float projectileSpeedMultiplier = 1f;
        [SerializeField, Min(0f)] private float shotCostMultiplier = 1f;
        [SerializeField, Min(0)] private int buildCostAdditive;

        public string PartId => partId;
        public string DisplayName => displayName;
        public TowerPartSlot Slot => slot;
        public PrimitiveType PrototypeShape => prototypeShape;
        public Vector3 PrototypeScale => prototypeScale;
        public float DurabilityMultiplier => durabilityMultiplier;
        public float DamageMultiplier => damageMultiplier;
        public float RangeAdditive => rangeAdditive;
        public float AttackIntervalMultiplier => attackIntervalMultiplier;
        public float ProjectileSpeedMultiplier => projectileSpeedMultiplier;
        public float ShotCostMultiplier => shotCostMultiplier;
        public int BuildCostAdditive => buildCostAdditive;

        public void Configure(
            string newPartId,
            string newDisplayName,
            TowerPartSlot newSlot,
            PrimitiveType shape,
            Vector3 scale,
            float durability,
            float damage,
            float range,
            float interval,
            float projectileSpeed,
            float shotCost,
            int buildCost)
        {
            partId = newPartId;
            displayName = newDisplayName;
            slot = newSlot;
            prototypeShape = shape;
            prototypeScale = new Vector3(
                Mathf.Max(0.1f, scale.x),
                Mathf.Max(0.1f, scale.y),
                Mathf.Max(0.1f, scale.z));
            durabilityMultiplier = Mathf.Max(0.1f, durability);
            damageMultiplier = Mathf.Max(0f, damage);
            rangeAdditive = range;
            attackIntervalMultiplier = Mathf.Max(0.1f, interval);
            projectileSpeedMultiplier = Mathf.Max(0.1f, projectileSpeed);
            shotCostMultiplier = Mathf.Max(0f, shotCost);
            buildCostAdditive = Mathf.Max(0, buildCost);
        }

        private void OnValidate()
        {
            prototypeScale = new Vector3(
                Mathf.Max(0.1f, prototypeScale.x),
                Mathf.Max(0.1f, prototypeScale.y),
                Mathf.Max(0.1f, prototypeScale.z));
            durabilityMultiplier = Mathf.Max(0.1f, durabilityMultiplier);
            damageMultiplier = Mathf.Max(0f, damageMultiplier);
            attackIntervalMultiplier = Mathf.Max(0.1f, attackIntervalMultiplier);
            projectileSpeedMultiplier = Mathf.Max(0.1f, projectileSpeedMultiplier);
            shotCostMultiplier = Mathf.Max(0f, shotCostMultiplier);
            buildCostAdditive = Mathf.Max(0, buildCostAdditive);
        }
    }
}
