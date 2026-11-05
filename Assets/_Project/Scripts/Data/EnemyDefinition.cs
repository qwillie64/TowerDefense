using UnityEngine;

namespace CyberWizard.Data
{
    [CreateAssetMenu(
        fileName = "EnemyDefinition",
        menuName = "CyberWizard/Enemy Definition")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        [SerializeField] private string enemyTypeId = "enemy_basic";
        [SerializeField] private string displayName = "Basic Enemy";
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [SerializeField, Min(0.1f)] private float moveSpeed = 2.5f;
        [SerializeField, Min(0f)] private float contactDamage = 10f;
        [SerializeField, Min(0)] private int reward = 5;
        [SerializeField, Min(0.05f)] private float attackInterval = 1f;
        [SerializeField] private PrimitiveType prototypeShape = PrimitiveType.Capsule;
        [SerializeField] private Vector3 prototypeScale = new Vector3(0.8f, 1f, 0.8f);
        [SerializeField] private bool isBoss;

        public string EnemyTypeId => enemyTypeId;
        public string DisplayName => displayName;
        public float MaxHealth => maxHealth;
        public float MoveSpeed => moveSpeed;
        public float ContactDamage => contactDamage;
        public int Reward => reward;
        public float AttackInterval => attackInterval;
        public PrimitiveType PrototypeShape => prototypeShape;
        public Vector3 PrototypeScale => prototypeScale;
        public bool IsBoss => isBoss;

        public void Configure(
            string typeId,
            string newDisplayName,
            float health,
            float speed,
            float damage,
            int newReward,
            float interval,
            PrimitiveType shape,
            Vector3 scale,
            bool boss = false)
        {
            enemyTypeId = typeId;
            displayName = newDisplayName;
            maxHealth = Mathf.Max(1f, health);
            moveSpeed = Mathf.Max(0.1f, speed);
            contactDamage = Mathf.Max(0f, damage);
            reward = Mathf.Max(0, newReward);
            attackInterval = Mathf.Max(0.05f, interval);
            prototypeShape = shape;
            prototypeScale = new Vector3(
                Mathf.Max(0.1f, scale.x),
                Mathf.Max(0.1f, scale.y),
                Mathf.Max(0.1f, scale.z));
            isBoss = boss;
        }

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            moveSpeed = Mathf.Max(0.1f, moveSpeed);
            contactDamage = Mathf.Max(0f, contactDamage);
            reward = Mathf.Max(0, reward);
            attackInterval = Mathf.Max(0.05f, attackInterval);
            prototypeScale.x = Mathf.Max(0.1f, prototypeScale.x);
            prototypeScale.y = Mathf.Max(0.1f, prototypeScale.y);
            prototypeScale.z = Mathf.Max(0.1f, prototypeScale.z);
        }
    }
}
