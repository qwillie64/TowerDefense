using System;
using CyberWizard.Core;
using UnityEngine;
using CyberWizard.Presentation;

namespace CyberWizard.Gameplay.Player
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PrototypePlayerController : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(0.1f)] private float moveSpeed = 6f;
        [SerializeField, Min(1f)] private float maxHealth = 100f;

        private CharacterController _characterController;
        private Camera _camera;
        private Vitality _vitality;
        private WorldHealthBar _healthBar;

        public event Action<float, float> HealthChanged;
        public event Action Died;

        public bool IsAlive => _vitality != null && _vitality.IsAlive;
        public Faction Faction => Faction.Player;
        public Transform TargetTransform => transform;
        public float CurrentHealth => _vitality != null ? _vitality.Current : 0f;
        public float MaxHealth => _vitality != null ? _vitality.Maximum : maxHealth;
        public bool InputEnabled { get; set; } = true;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _camera = Camera.main;
            _vitality = gameObject.AddComponent<Vitality>();
            _vitality.Changed += HandleVitalityChanged;
            _vitality.Depleted += HandleDepleted;
            _vitality.Initialize(maxHealth);
            _healthBar = WorldHealthBar.Create(
                transform,
                new Vector3(0f, 1.5f, 0f),
                PrototypeMaterialRole.Player,
                1.5f);
            _healthBar.SetValue(CurrentHealth, MaxHealth);
        }

        private void Update()
        {
            if (!InputEnabled || !IsAlive)
            {
                return;
            }

            var input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (input.sqrMagnitude > 1f)
            {
                input.Normalize();
            }

            var forward = _camera != null ? _camera.transform.forward : Vector3.forward;
            var right = _camera != null ? _camera.transform.right : Vector3.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            var movement = (forward * input.y + right * input.x) * moveSpeed;
            movement.y = -2f;
            _characterController.Move(movement * Time.deltaTime);

            var horizontal = new Vector3(movement.x, 0f, movement.z);
            if (horizontal.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(horizontal.normalized, Vector3.up);
            }
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

        private void HandleDepleted()
        {
            InputEnabled = false;
            Died?.Invoke();
        }

        private void OnDestroy()
        {
            if (_vitality == null) return;
            _vitality.Changed -= HandleVitalityChanged;
            _vitality.Depleted -= HandleDepleted;
        }
    }
}
