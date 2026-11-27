using System;
using CyberWizard.Gameplay.Player;
using CyberWizard.Gameplay.Resources;
using CyberWizard.Infrastructure;
using UnityEngine;

namespace CyberWizard.Gameplay.Towers
{
    public enum TowerUpgradeResult
    {
        Upgraded,
        NoTower,
        MaxLevel,
        NotEnoughEnergy,
        TowerDestroyed
    }

    /// <summary>
    /// 玩家靠近固定防禦塔時，以數字鍵升級該塔的部位：
    /// 1 = 砲塔、2 = 塔身、3 = 塔底。先扣能量，再呼叫塔的 ApplyUpgrade。
    /// 目前靠近的塔透過 CurrentTower 提供給畫面（TowerUpgradePrompt）顯示。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TowerUpgradeInteractor : MonoBehaviour
    {
        [SerializeField] private PrototypePlayerController player;
        [SerializeField] private ResourceWallet wallet;
        [SerializeField] private FixedTowerCombat[] towers = Array.Empty<FixedTowerCombat>();

        [SerializeField, Min(0.5f), Tooltip("玩家與塔中心的水平距離小於此值時可升級該塔。暫定值。")]
        private float interactionRadius = 12f;

        /// <summary>每次嘗試升級（含失敗）時觸發，供畫面顯示回饋。</summary>
        public event Action<FixedTowerCombat, TowerPart, TowerUpgradeResult> UpgradeAttempted;

        public FixedTowerCombat CurrentTower { get; private set; }
        public ResourceWallet Wallet => wallet;
        public float InteractionRadius => interactionRadius;

        private void Update()
        {
            CurrentTower = FindNearbyTower();
            if (CurrentTower == null)
            {
                return;
            }

            // 升級鍵的順序對應 TowerPart 的數值：1 = 砲塔、2 = 塔身、3 = 塔底。
            for (var slot = 0; slot < GameInput.UpgradeSlotCount; slot++)
            {
                if (GameInput.UpgradeSlotPressed(slot))
                {
                    TryUpgrade((TowerPart)slot);
                    break;
                }
            }
        }

        /// <summary>升級目前靠近的塔的指定部位。</summary>
        public TowerUpgradeResult TryUpgrade(TowerPart part)
        {
            var tower = CurrentTower;
            TowerUpgradeResult result;
            if (tower == null)
            {
                result = TowerUpgradeResult.NoTower;
            }
            else if (!tower.IsAlive)
            {
                result = TowerUpgradeResult.TowerDestroyed;
            }
            else if (!tower.TryGetUpgradeCost(part, out var cost))
            {
                result = TowerUpgradeResult.MaxLevel;
            }
            else if (wallet == null || !wallet.TrySpend(cost))
            {
                result = TowerUpgradeResult.NotEnoughEnergy;
            }
            else
            {
                tower.ApplyUpgrade(part);
                result = TowerUpgradeResult.Upgraded;
            }

            UpgradeAttempted?.Invoke(tower, part, result);
            return result;
        }

        /// <summary>立即重新判斷玩家附近的塔（例如瞬間移動玩家之後）。</summary>
        public void RefreshCurrentTower()
        {
            CurrentTower = FindNearbyTower();
        }

        private FixedTowerCombat FindNearbyTower()
        {
            if (player == null || !player.IsAlive || !player.InputEnabled || towers == null)
            {
                return null;
            }

            FixedTowerCombat nearest = null;
            var bestDistance = interactionRadius * interactionRadius;
            var playerPosition = player.transform.position;
            for (var index = 0; index < towers.Length; index++)
            {
                var tower = towers[index];
                if (tower == null)
                {
                    continue;
                }

                var offset = tower.transform.position - playerPosition;
                offset.y = 0f;
                var distance = offset.sqrMagnitude;
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    nearest = tower;
                }
            }

            return nearest;
        }
    }
}
