using UnityEngine;

namespace CyberWizard.Gameplay.Enemies
{
    public sealed class EnemyStatusController : MonoBehaviour
    {
        private EnemyAgent _owner;
        private float _slowMultiplier = 1f;
        private float _slowRemaining;
        private float _stunRemaining;
        private float _burnDamagePerSecond;
        private float _burnRemaining;

        public float SpeedMultiplier => _slowRemaining > 0f ? _slowMultiplier : 1f;
        public bool CanAct => _stunRemaining <= 0f;

        public void Configure(EnemyAgent owner)
        {
            _owner = owner;
        }

        public void ApplySlow(float multiplier, float duration)
        {
            if (duration <= 0f)
            {
                return;
            }

            _slowMultiplier = Mathf.Min(_slowMultiplier, Mathf.Clamp(multiplier, 0.1f, 1f));
            _slowRemaining = Mathf.Max(_slowRemaining, duration);
            Debug.Log($"[EnemyStatus] {name} Slow x{_slowMultiplier:0.00} duration={_slowRemaining:0.0}", this);
        }

        public void ApplyStun(float duration)
        {
            if (duration <= 0f)
            {
                return;
            }

            _stunRemaining = Mathf.Max(_stunRemaining, duration);
            Debug.Log($"[EnemyStatus] {name} Stun duration={_stunRemaining:0.0}", this);
        }

        public void ApplyBurn(float damagePerSecond, float duration)
        {
            if (damagePerSecond <= 0f || duration <= 0f)
            {
                return;
            }

            _burnDamagePerSecond = Mathf.Max(_burnDamagePerSecond, damagePerSecond);
            _burnRemaining = Mathf.Max(_burnRemaining, duration);
            Debug.Log($"[EnemyStatus] {name} Burn dps={_burnDamagePerSecond:0.0} duration={_burnRemaining:0.0}", this);
        }

        private void Update()
        {
            var deltaTime = Time.deltaTime;
            if (_slowRemaining > 0f)
            {
                _slowRemaining = Mathf.Max(0f, _slowRemaining - deltaTime);
                if (_slowRemaining <= 0f)
                {
                    _slowMultiplier = 1f;
                }
            }

            if (_stunRemaining > 0f)
            {
                _stunRemaining = Mathf.Max(0f, _stunRemaining - deltaTime);
            }

            if (_burnRemaining > 0f && _owner != null && _owner.IsAlive)
            {
                var appliedTime = Mathf.Min(deltaTime, _burnRemaining);
                _burnRemaining = Mathf.Max(0f, _burnRemaining - deltaTime);
                _owner.TakeDamage(_burnDamagePerSecond * appliedTime);
                if (_burnRemaining <= 0f)
                {
                    _burnDamagePerSecond = 0f;
                }
            }
        }
    }
}
