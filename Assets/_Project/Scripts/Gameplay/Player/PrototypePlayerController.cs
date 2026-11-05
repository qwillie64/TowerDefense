using System;
using CyberWizard.Core;
using UnityEngine;

namespace CyberWizard.Gameplay.Player
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PrototypePlayerController : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(0.1f)] private float moveSpeed = 6f;
        [SerializeField, Min(1f)] private float maxHealth = 100f;

        private CharacterController _characterController;
        private Camera _camera;

        public event Action<float, float> HealthChanged;
        public event Action Died;

        public bool IsAlive => CurrentHealth > 0f;
        public Faction Faction => Faction.Player;
        public Transform TargetTransform => transform;
        public float CurrentHealth { get; private set; }
        public float MaxHealth => maxHealth;
        public bool InputEnabled { get; set; } = true;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _camera = Camera.main;
            CurrentHealth = maxHealth;
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
            if (!IsAlive || amount <= 0f)
            {
                return;
            }

            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            HealthChanged?.Invoke(CurrentHealth, maxHealth);
            if (CurrentHealth <= 0f)
            {
                InputEnabled = false;
                Died?.Invoke();
            }
        }
    }
}
