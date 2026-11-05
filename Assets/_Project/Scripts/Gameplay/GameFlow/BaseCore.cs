using System;
using CyberWizard.Core;
using UnityEngine;
using CyberWizard.Presentation;

namespace CyberWizard.Gameplay.GameFlow
{
    public sealed class BaseCore : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(1f)] private float maxHealth = 100f;

        public event Action<float, float> HealthChanged;
        public event Action Destroyed;

        public bool IsAlive => CurrentHealth > 0f;
        public Faction Faction => Faction.Player;
        public Transform TargetTransform => transform;
        public float CurrentHealth { get; private set; }
        public float MaxHealth => maxHealth;
        private WorldHealthBar _healthBar;

        private void Awake()
        {
            CurrentHealth = maxHealth;
            _healthBar = WorldHealthBar.Create(
                transform,
                new Vector3(0f, 1.4f, 0f),
                PrototypeMaterialRole.Player,
                3f);
            _healthBar.SetValue(CurrentHealth, maxHealth);
        }

        public void TakeDamage(float amount)
        {
            if (!IsAlive || amount <= 0f)
            {
                return;
            }

            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            if (_healthBar != null) _healthBar.SetValue(CurrentHealth, maxHealth);
            HealthChanged?.Invoke(CurrentHealth, maxHealth);
            if (CurrentHealth <= 0f)
            {
                Destroyed?.Invoke();
            }
        }
    }
}
