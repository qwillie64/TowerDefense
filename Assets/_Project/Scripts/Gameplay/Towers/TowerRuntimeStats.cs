using UnityEngine;

namespace CyberWizard.Gameplay.Towers
{
    public readonly struct TowerRuntimeStats
    {
        public TowerRuntimeStats(
            float maxDurability,
            float attackDamage,
            float attackRange,
            float attackInterval,
            float projectileSpeed,
            float durabilityCostPerShot,
            int buildCost)
        {
            MaxDurability = maxDurability;
            AttackDamage = attackDamage;
            AttackRange = attackRange;
            AttackInterval = attackInterval;
            ProjectileSpeed = projectileSpeed;
            DurabilityCostPerShot = durabilityCostPerShot;
            BuildCost = buildCost;
        }

        public float MaxDurability { get; }
        public float AttackDamage { get; }
        public float AttackRange { get; }
        public float AttackInterval { get; }
        public float ProjectileSpeed { get; }
        public float DurabilityCostPerShot { get; }
        public int BuildCost { get; }
    }

    public static class TowerAssembler
    {
        public static TowerRuntimeStats Assemble(TowerDefinition definition)
        {
            if (definition == null)
            {
                return new TowerRuntimeStats(100f, 20f, 6.5f, 1f, 15f, 2f, 25);
            }

            var durability = definition.MaxDurability;
            var damage = definition.AttackDamage;
            var range = definition.AttackRange;
            var interval = definition.AttackInterval;
            var projectileSpeed = definition.ProjectileSpeed;
            var shotCost = definition.DurabilityCostPerShot;
            var buildCost = definition.BuildCost;

            Apply(definition.BasePart, ref durability, ref damage, ref range,
                ref interval, ref projectileSpeed, ref shotCost, ref buildCost);
            Apply(definition.BodyPart, ref durability, ref damage, ref range,
                ref interval, ref projectileSpeed, ref shotCost, ref buildCost);
            Apply(definition.TopPart, ref durability, ref damage, ref range,
                ref interval, ref projectileSpeed, ref shotCost, ref buildCost);

            return new TowerRuntimeStats(
                Mathf.Max(1f, durability),
                Mathf.Max(0f, damage),
                Mathf.Max(0.1f, range),
                Mathf.Max(0.05f, interval),
                Mathf.Max(0.1f, projectileSpeed),
                Mathf.Max(0f, shotCost),
                Mathf.Max(0, buildCost));
        }

        private static void Apply(
            TowerPartDefinition part,
            ref float durability,
            ref float damage,
            ref float range,
            ref float interval,
            ref float projectileSpeed,
            ref float shotCost,
            ref int buildCost)
        {
            if (part == null)
            {
                return;
            }

            durability *= part.DurabilityMultiplier;
            damage *= part.DamageMultiplier;
            range += part.RangeAdditive;
            interval *= part.AttackIntervalMultiplier;
            projectileSpeed *= part.ProjectileSpeedMultiplier;
            shotCost *= part.ShotCostMultiplier;
            buildCost += part.BuildCostAdditive;
        }
    }
}
