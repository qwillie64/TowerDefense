using UnityEngine;

namespace CyberWizard.Gameplay.Enemies
{
    [CreateAssetMenu(
        fileName = "EnemyBehaviorSettings",
        menuName = "CyberWizard/Enemy Behavior Settings")]
    public sealed class EnemyBehaviorSettings : ScriptableObject
    {
        [SerializeField, Min(0.1f), Tooltip("敵人攻擊玩家的近戰距離。")]
        private float attackDistance = 2.6f;

        public float AttackDistance => attackDistance;

        private void OnValidate()
        {
            attackDistance = Mathf.Max(0.1f, attackDistance);
        }
    }
}
