using CyberWizard.Gameplay.Player;
using CyberWizard.Gameplay.Towers;
using CyberWizard.Presentation;
using UnityEngine;

namespace CyberWizard.Gameplay.Enemies
{
    public sealed class BossAbilityController : MonoBehaviour
    {
        private EnemyAgent _owner;
        private PrototypePlayerController _player;
        private TowerRegistry _towerRegistry;
        private float _cooldownRemaining;
        private float _interval = 5f;
        private float _radius = 5f;
        private float _damage = 8f;

        public void Configure(
            EnemyAgent owner,
            PrototypePlayerController player,
            TowerRegistry towerRegistry,
            float interval = 5f,
            float radius = 5f,
            float damage = 8f)
        {
            _owner = owner;
            _player = player;
            _towerRegistry = towerRegistry;
            _interval = Mathf.Max(0.5f, interval);
            _radius = Mathf.Max(0.5f, radius);
            _damage = Mathf.Max(0f, damage);
            _cooldownRemaining = _interval;
        }

        private void Update()
        {
            if (_owner == null || !_owner.IsAlive)
            {
                return;
            }

            _cooldownRemaining -= Time.deltaTime;
            if (_cooldownRemaining > 0f)
            {
                return;
            }

            _cooldownRemaining = _interval;
            CastPulse();
        }

        private void CastPulse()
        {
            var position = transform.position;
            if (_player != null && _player.IsAlive &&
                Vector3.Distance(position, _player.transform.position) <= _radius)
            {
                _player.TakeDamage(_damage);
            }

            if (_towerRegistry != null)
            {
                foreach (var tower in _towerRegistry.Towers)
                {
                    if (tower != null && tower.IsAlive &&
                        Vector3.Distance(position, tower.transform.position) <= _radius)
                    {
                        tower.TakeDamage(_damage);
                    }
                }
            }

            PrototypePulseEffect.Spawn(
                position + Vector3.up * 0.6f,
                PrototypeMaterialRole.Enemy,
                new Vector3(_radius * 2f, 0.4f, _radius * 2f),
                0.55f);
            Debug.Log($"[BossAbility] Pulse radius={_radius:0.0} damage={_damage:0.0}", this);
        }
    }
}
