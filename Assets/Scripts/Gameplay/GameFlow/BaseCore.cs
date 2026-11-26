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
        public float CurrentHealth => _vitality != null ? _vitality.Current : 0f;
        public float MaxHealth => _vitality != null ? _vitality.Maximum : maxHealth;
        private WorldHealthBar _healthBar;
        private Vitality _vitality;

        private void Awake()
        {
            _vitality = gameObject.AddComponent<Vitality>();
            _vitality.Changed += HandleVitalityChanged;
            _vitality.Depleted += HandleDepleted;
            _vitality.Initialize(maxHealth);
            _healthBar = WorldHealthBar.Create(
                transform,
                new Vector3(0f, 1.4f, 0f),
                PrototypeMaterialRole.Player,
                3f);
            _healthBar.SetValue(CurrentHealth, MaxHealth);
        }

        public void TakeDamage(float amount)
        {
            _vitality?.TakeDamage(amount);
        }

        private void HandleVitalityChanged(float current, float maximum)
        {
            if (_healthBar != null) _healthBar.SetValue(current, maximum);
            HealthChanged?.Invoke(current, maximum);
        }

        private void HandleDepleted() => Destroyed?.Invoke();

        private void OnDestroy()
        {
            if (_vitality == null) return;
            _vitality.Changed -= HandleVitalityChanged;
            _vitality.Depleted -= HandleDepleted;
        }
    }
}
