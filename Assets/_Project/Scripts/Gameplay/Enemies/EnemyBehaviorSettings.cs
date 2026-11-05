using UnityEngine;

namespace CyberWizard.Gameplay.Enemies
{
    [CreateAssetMenu(
        fileName = "EnemyBehaviorSettings",
        menuName = "CyberWizard/Enemy Behavior Settings")]
    public sealed class EnemyBehaviorSettings : ScriptableObject
    {
        [SerializeField, Range(0f, 1f)] private float captureNeutralChance = 0.15f;
        [SerializeField, Range(0f, 1f)] private float attackTowerChance = 0.15f;
        [SerializeField, Min(0.1f)] private float neutralCaptureDuration = 3f;
        [SerializeField, Min(0.1f)] private float attackDistance = 2.6f;

        public float CaptureNeutralChance => captureNeutralChance;
        public float AttackTowerChance => attackTowerChance;
        public float NeutralCaptureDuration => neutralCaptureDuration;
        public float AttackDistance => attackDistance;

        public void Configure(
            float captureChance,
            float towerChance,
            float captureDuration,
            float newAttackDistance)
        {
            captureNeutralChance = Mathf.Clamp01(captureChance);
            attackTowerChance = Mathf.Clamp(towerChance, 0f, 1f - captureNeutralChance);
            neutralCaptureDuration = Mathf.Max(0.1f, captureDuration);
            attackDistance = Mathf.Max(0.1f, newAttackDistance);
        }

        private void OnValidate()
        {
            captureNeutralChance = Mathf.Clamp01(captureNeutralChance);
            attackTowerChance = Mathf.Clamp(attackTowerChance, 0f, 1f - captureNeutralChance);
            neutralCaptureDuration = Mathf.Max(0.1f, neutralCaptureDuration);
            attackDistance = Mathf.Max(0.1f, attackDistance);
        }
    }
}
