using System.Collections;
using CyberWizard.Core;
using CyberWizard.Infrastructure;
using CyberWizard.Presentation;
using UnityEngine;

namespace CyberWizard.Gameplay.Player
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PrototypePlayerController : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(0.1f)] private float moveSpeed = 6f;
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [SerializeField] private float gravity = -25f;
        [SerializeField] private float groundedVelocity = -2f;
        [SerializeField, Min(0.1f)] private float jumpHeight = 2f;

        private CharacterController _characterController;
        private Camera _camera;
        private Vitality _vitality;
        private WorldHealthBar _healthBar;
        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation;
        private Renderer _bodyRenderer;
        private Coroutine _respawnRoutine;
        private float _verticalVelocity;

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
            _spawnPosition = transform.position;
            _spawnRotation = transform.rotation;
            _bodyRenderer = GetComponent<Renderer>();
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

            var input = GameInput.Move;

            var forward = _camera != null ? _camera.transform.forward : Vector3.forward;
            var right = _camera != null ? _camera.transform.right : Vector3.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            var movement = (forward * input.y + right * input.x) * moveSpeed;
            if (_characterController.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = groundedVelocity;
            }

            if (_characterController.isGrounded && GameInput.JumpPressed)
            {
                _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            _verticalVelocity += gravity * Time.deltaTime;
            movement.y = _verticalVelocity;
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
        }

        private void HandleDepleted()
        {
            if (_respawnRoutine == null)
            {
                _respawnRoutine = StartCoroutine(RespawnAfterDelay());
            }
        }

        private IEnumerator RespawnAfterDelay()
        {
            if (_bodyRenderer != null)
            {
                _bodyRenderer.enabled = false;
            }

            if (_characterController != null)
            {
                _characterController.enabled = false;
            }

            yield return new WaitForSeconds(3f);
            transform.SetPositionAndRotation(_spawnPosition, _spawnRotation);
            _verticalVelocity = 0f;
            _vitality.Initialize(maxHealth);

            if (_characterController != null)
            {
                _characterController.enabled = true;
            }

            if (_bodyRenderer != null)
            {
                _bodyRenderer.enabled = true;
            }

            _respawnRoutine = null;
        }

        private void OnDestroy()
        {
            if (_vitality == null) return;
            _vitality.Changed -= HandleVitalityChanged;
            _vitality.Depleted -= HandleDepleted;
        }
    }
}
