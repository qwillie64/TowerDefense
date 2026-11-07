using UnityEngine;

namespace CyberWizard.Gameplay.Enemies
{
    public enum EnemyIntent
    {
        AttackBase,
        CaptureNeutral,
        AttackTower
    }

    public static class EnemyIntentSelector
    {
        public static EnemyIntent Select(
            float roll,
            float captureChance,
            float attackTowerChance,
            bool canCaptureNeutral,
            bool hasTowerTarget)
        {
            var normalizedRoll = Mathf.Clamp01(roll);
            var normalizedCaptureChance = Mathf.Clamp01(captureChance);
            var normalizedTowerChance = Mathf.Clamp(
                attackTowerChance,
                0f,
                1f - normalizedCaptureChance);

            if (normalizedRoll < normalizedCaptureChance)
            {
                return canCaptureNeutral
                    ? EnemyIntent.CaptureNeutral
                    : EnemyIntent.AttackBase;
            }

            if (normalizedRoll < normalizedCaptureChance + normalizedTowerChance)
            {
                return hasTowerTarget
                    ? EnemyIntent.AttackTower
                    : EnemyIntent.AttackBase;
            }

            return EnemyIntent.AttackBase;
        }
    }
}
