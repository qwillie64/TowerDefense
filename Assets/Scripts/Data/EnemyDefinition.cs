using UnityEngine;

namespace CyberWizard.Data
{
    [CreateAssetMenu(
        fileName = "EnemyDefinition",
        menuName = "CyberWizard/Enemy Definition")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        [SerializeField] private string enemyTypeId = "enemy_level0";
        [SerializeField] private string displayName = "Level 0 敵人";
        [SerializeField, Min(1f)] private float maxHealth = 50f;
        [SerializeField, Min(0.1f)] private float moveSpeed = 3f;
        [SerializeField, Min(0f)] private float contactDamage = 3f;
        [SerializeField, Min(0)] private int reward = 150;
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
