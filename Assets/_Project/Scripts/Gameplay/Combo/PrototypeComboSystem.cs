using System;
using System.Collections.Generic;
using CyberWizard.Gameplay.Enemies;
using CyberWizard.Gameplay.Towers;
using CyberWizard.Gameplay.Waves;
using UnityEngine;
using CyberWizard.Presentation;
using CyberWizard.Gameplay.Neutral;
using CyberWizard.Core;
using CyberWizard.Gameplay.Player;

namespace CyberWizard.Gameplay.Combo
{
    public sealed class PrototypeComboSystem : MonoBehaviour
    {
        private enum ComboEffect
        {
            Burn,
            Stun,
            Slow
        }

        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private TowerRegistry towerRegistry;
        [SerializeField] private WaveDirector waveDirector;
        [SerializeField, Min(0f)] private float cooldown = 4f;
        [SerializeField] private SoldierRegistry soldierRegistry;
        [SerializeField] private PrototypePlayerController player;

        private float _cooldownRemaining;

        public event Action Changed;

        public string StatusMessage { get; private set; } = "在兩座元素塔的重疊射程按 Q 施放連攜";
        public float CooldownRemaining => _cooldownRemaining;

        public void Configure(
            Camera camera,
            TowerRegistry registry,
            WaveDirector director,
            SoldierRegistry newSoldierRegistry = null,
            PrototypePlayerController newPlayer = null)
        {
            gameplayCamera = camera;
            towerRegistry = registry;
            waveDirector = director;
            soldierRegistry = newSoldierRegistry;
            player = newPlayer;
        }

        private void Update()
        {
            if (_cooldownRemaining > 0f)
            {
                _cooldownRemaining = Mathf.Max(0f, _cooldownRemaining - Time.deltaTime);
            }

            if (player != null && player.InputEnabled && Input.GetKeyDown(KeyCode.Q))
            {
                TryCastAtCursor();
            }
        }

        private void TryCastAtCursor()
        {
            if (_cooldownRemaining > 0f)
            {
                StatusMessage = $"連攜冷卻 {_cooldownRemaining:0.0} 秒";
                Changed?.Invoke();
                return;
            }

            if (!TryGetGroundPoint(out var point))
            {
                StatusMessage = "瞄準位置無效";
                Changed?.Invoke();
                return;
            }

            TryCastAtPoint(point);
        }

        public bool TryCastAtPoint(Vector3 point)
        {
            if (_cooldownRemaining > 0f)
            {
                StatusMessage = $"連攜冷卻 {_cooldownRemaining:0.0} 秒";
                Changed?.Invoke();
                return false;
            }

            var coveringTowers = new List<TowerController>();
            foreach (var tower in towerRegistry.Towers)
            {
                if (tower != null && tower.IsOperational &&
                    Vector3.Distance(tower.transform.position, point) <= tower.AttackRange)
                {
                    coveringTowers.Add(tower);
                }
            }

            for (var left = 0; left < coveringTowers.Count; left++)
            {
                for (var right = left + 1; right < coveringTowers.Count; right++)
                {
                    if (TryGetRecipe(
                            coveringTowers[left].Element,
                            coveringTowers[right].Element,
                            out var name,
                            out var damage,
                            out var radius,
                            out var effect,
                            out var effectMagnitude,
                            out var effectDuration))
                    {
                        ApplyAreaEffect(
                            point,
                            damage,
                            radius,
                            effect,
                            effectMagnitude,
                            effectDuration);
                        PrototypePulseEffect.Spawn(
                            point + Vector3.up * 0.5f,
                            PrototypeMaterialRole.Player,
                            new Vector3(radius * 2f, 0.35f, radius * 2f),
                            0.45f);
                        _cooldownRemaining = cooldown;
                        StatusMessage = $"施放 {name}";
                        Changed?.Invoke();
                        return true;
                    }
                }
            }

            StatusMessage = "此處沒有合法的元素塔射程組合";
            Changed?.Invoke();
            return false;
        }

        private bool TryGetGroundPoint(out Vector3 point)
        {
            point = default;
            if (gameplayCamera == null)
            {
                return false;
            }

            var pointerPosition = Cursor.lockState == CursorLockMode.Locked
                ? new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f)
                : Input.mousePosition;
            var ray = gameplayCamera.ScreenPointToRay(pointerPosition);
            var hits = Physics.RaycastAll(ray, 100f);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            for (var index = 0; index < hits.Length; index++)
            {
                if (hits[index].collider.gameObject.name == "Ground")
                {
                    point = hits[index].point;
                    return true;
                }
            }

            return false;
        }

        private void ApplyAreaEffect(
            Vector3 point,
            float damage,
            float radius,
            ComboEffect effect,
            float effectMagnitude,
            float effectDuration)
        {
            var targets = new List<EnemyAgent>();
            foreach (var enemy in waveDirector.AliveEnemies)
            {
                if (enemy != null && enemy.IsAlive &&
                    Vector3.Distance(enemy.transform.position, point) <= radius)
                {
                    targets.Add(enemy);
                }
            }

            for (var index = 0; index < targets.Count; index++)
            {
                var target = targets[index];
                target.TakeDamage(damage);
                if (!target.IsAlive)
                {
                    continue;
                }

                switch (effect)
                {
                    case ComboEffect.Burn:
                        target.ApplyBurn(effectMagnitude, effectDuration);
                        break;
                    case ComboEffect.Stun:
                        target.ApplyStun(effectDuration);
                        break;
                    case ComboEffect.Slow:
                        target.ApplySlow(effectMagnitude, effectDuration);
                        break;
                }
            }

            if (soldierRegistry != null)
            {
                var enemySoldiers = new List<SoldierAgent>();
                soldierRegistry.CollectInRadius(
                    Faction.Enemy,
                    point,
                    radius,
                    enemySoldiers);
                for (var index = 0; index < enemySoldiers.Count; index++)
                {
                    enemySoldiers[index].TakeDamage(damage);
                }
            }
        }

        private static bool TryGetRecipe(
            ElementType first,
            ElementType second,
            out string name,
            out float damage,
            out float radius,
            out ComboEffect effect,
            out float effectMagnitude,
            out float effectDuration)
        {
            if ((first == ElementType.Fire && second == ElementType.Wind) ||
                (first == ElementType.Wind && second == ElementType.Fire))
            {
                name = "火風爆流";
                damage = 45f;
                radius = 4f;
                effect = ComboEffect.Burn;
                effectMagnitude = 8f;
                effectDuration = 3f;
                return true;
            }

            if ((first == ElementType.Fire && second == ElementType.Earth) ||
                (first == ElementType.Earth && second == ElementType.Fire))
            {
                name = "熔岩震擊";
                damage = 60f;
                radius = 3f;
                effect = ComboEffect.Stun;
                effectMagnitude = 0f;
                effectDuration = 1.5f;
                return true;
            }

            if ((first == ElementType.Water && second == ElementType.Wind) ||
                (first == ElementType.Wind && second == ElementType.Water))
            {
                name = "霜暴";
                damage = 35f;
                radius = 5f;
                effect = ComboEffect.Slow;
                effectMagnitude = 0.45f;
                effectDuration = 4f;
                return true;
            }

            name = string.Empty;
            damage = 0f;
            radius = 0f;
            effect = ComboEffect.Burn;
            effectMagnitude = 0f;
            effectDuration = 0f;
            return false;
        }
    }
}
