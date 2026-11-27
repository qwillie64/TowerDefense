using System;
using CyberWizard.Gameplay.Enemies;
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
    /// 玩家靠近固定防禦塔、且視角（畫面中央準心）指向該塔時，以數字鍵升級該塔的部位：
    /// 1 = 砲塔、2 = 塔身、3 = 塔底。先扣能量，再呼叫塔的 ApplyUpgrade。
    /// 指向判斷：從攝影機沿準心方向 Raycast，略過觸發器、玩家自己與敵人，
    /// 第一個擊中的碰撞體必須屬於清單中的塔（被牆或建築擋住則不算）。
    /// 目前選取的塔透過 CurrentTower 提供給畫面（TowerUpgradePrompt、TowerOutlineHighlighter）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TowerUpgradeInteractor : MonoBehaviour
    {
        [SerializeField] private PrototypePlayerController player;
        [SerializeField] private ResourceWallet wallet;
        [SerializeField] private FixedTowerCombat[] towers = Array.Empty<FixedTowerCombat>();

        [SerializeField, Min(0.5f), Tooltip("玩家與塔中心的水平距離小於此值時可升級該塔。暫定值。")]
        private float interactionRadius = 12f;

        [SerializeField, Tooltip("視角 Raycast 的來源攝影機；未設定時使用 Camera.main。")]
        private Camera viewCamera;

        [SerializeField, Min(1f), Tooltip("視角 Raycast 的最大距離（從攝影機算起）。暫定值。")]
        private float aimRayDistance = 60f;

        [SerializeField, Tooltip("視角 Raycast 會檢查的圖層。")]
        private LayerMask aimLayers = Physics.DefaultRaycastLayers;

        private readonly RaycastHit[] _aimHits = new RaycastHit[16];

        /// <summary>每次嘗試升級（含失敗）時觸發，供畫面顯示回饋。</summary>
        public event Action<FixedTowerCombat, TowerPart, TowerUpgradeResult> UpgradeAttempted;

        public FixedTowerCombat CurrentTower { get; private set; }
        public ResourceWallet Wallet => wallet;
        public float InteractionRadius => interactionRadius;

        private void Update()
        {
            CurrentTower = FindTargetTower();
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

        /// <summary>立即重新判斷目前選取的塔（例如瞬間移動玩家或轉動攝影機之後）。</summary>
        public void RefreshCurrentTower()
        {
            CurrentTower = FindTargetTower();
        }

        /// <summary>同時滿足「玩家在升級範圍內」與「視角指向該塔」的塔；沒有則回傳 null。</summary>
        private FixedTowerCombat FindTargetTower()
        {
            if (player == null || !player.IsAlive || !player.InputEnabled || towers == null)
            {
                return null;
            }

            var aimed = FindAimedTower();
            return aimed != null && IsWithinInteractionRadius(aimed) ? aimed : null;
        }

        private bool IsWithinInteractionRadius(FixedTowerCombat tower)
        {
            var offset = tower.transform.position - player.transform.position;
            offset.y = 0f;
            return offset.sqrMagnitude <= interactionRadius * interactionRadius;
        }

        private FixedTowerCombat FindAimedTower()
        {
            if (viewCamera == null)
            {
                viewCamera = Camera.main;
                if (viewCamera == null)
                {
                    return null;
                }
            }

            // 視角方向＝畫面中央（準心位置），與滑鼠游標位置無關。
            var ray = viewCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            var hitCount = Physics.RaycastNonAlloc(
                ray,
                _aimHits,
                aimRayDistance,
                aimLayers,
                QueryTriggerInteraction.Ignore);

            // RaycastNonAlloc 不保證順序，找出最近的有效擊中點。
            Collider closest = null;
            var closestDistance = float.MaxValue;
            var playerTransform = player.transform;
            for (var index = 0; index < hitCount; index++)
            {
                var hit = _aimHits[index];
                if (hit.distance >= closestDistance)
                {
                    continue;
                }

                // 玩家自己與移動中的敵人不阻擋視線，避免敵人經過時提示閃爍。
                var hitTransform = hit.collider.transform;
                if (hitTransform.IsChildOf(playerTransform) || hit.collider.GetComponentInParent<EnemyAgent>() != null)
                {
                    continue;
                }

                closest = hit.collider;
                closestDistance = hit.distance;
            }

            if (closest == null)
            {
                return null;
            }

            var tower = closest.GetComponentInParent<FixedTowerCombat>();
            return tower != null && Array.IndexOf(towers, tower) >= 0 ? tower : null;
        }
    }
}
