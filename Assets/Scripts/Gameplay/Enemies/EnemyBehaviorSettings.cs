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

        [Header("NavMesh 移動（暫定值）")]
        [SerializeField, Min(0.05f), Tooltip("與路徑點的剩餘路程小於此值時視為抵達，改往下一個路徑點。")]
        private float waypointArrivalDistance = 1f;

        [SerializeField, Min(0.1f), Tooltip("NavMeshAgent 加速度；數值高時起步與停止接近瞬間完成。")]
        private float acceleration = 60f;

        [SerializeField, Min(1f), Tooltip("NavMeshAgent 每秒最大轉向角度。")]
        private float angularSpeed = 720f;

        [SerializeField, Tooltip("開啟時敵人彼此避讓（RVO）。關閉時與改用 NavMesh 前相同，敵人可以重疊。")]
        private bool avoidOtherEnemies;

        public float AttackDistance => attackDistance;
        public float WaypointArrivalDistance => waypointArrivalDistance;
        public float Acceleration => acceleration;
        public float AngularSpeed => angularSpeed;
        public bool AvoidOtherEnemies => avoidOtherEnemies;

        private void OnValidate()
        {
            attackDistance = Mathf.Max(0.1f, attackDistance);
            waypointArrivalDistance = Mathf.Max(0.05f, waypointArrivalDistance);
            acceleration = Mathf.Max(0.1f, acceleration);
            angularSpeed = Mathf.Max(1f, angularSpeed);
        }
    }
}
