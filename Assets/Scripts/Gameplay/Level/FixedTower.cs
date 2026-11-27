using UnityEngine;

namespace CyberWizard.Gameplay.Level
{
    /// <summary>
    /// 地圖原有的固定防禦塔（灰盒）。塔的位置與朝向由本物件的 Transform 決定；
    /// 本元件只負責把 Inspector 上的尺寸參數套用到基座、塔身與砲管的幾何物件。
    /// 攻擊、血量與升級由同物件上的 FixedTowerCombat 負責。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FixedTower : MonoBehaviour
    {
        [SerializeField, Tooltip("塔號，例如 5、7、9。")]
        private int towerNumber;

        [Header("幾何物件")]
        [SerializeField, Tooltip("基座（Cylinder）。")]
        private Transform baseVisual;

        [SerializeField, Tooltip("塔身（Cylinder）。")]
        private Transform bodyVisual;

        [SerializeField, Tooltip("砲管（Cube），沿本物件（或砲塔樞紐）的 +Z 方向伸出。")]
        private Transform barrelVisual;

        [SerializeField, Tooltip("可選：砲塔旋轉樞紐。設定後砲管與砲口必須是它的子物件，執行時由 FixedTowerCombat 轉向目標。")]
        private Transform turretPivot;

        [SerializeField, Tooltip("可選：砲口（發射點），放在砲管前端。需為砲塔樞紐的子物件。")]
        private Transform muzzle;

        [Header("尺寸（世界單位，暫定值）")]
        [SerializeField, Min(0.1f)]
        private float baseRadius = 4f;

        [SerializeField, Min(0.1f)]
        private float baseHeight = 1f;

        [SerializeField, Min(0.1f)]
        private float bodyRadius = 3f;

        [SerializeField, Min(0.1f)]
        private float bodyHeight = 7f;

        [SerializeField, Min(0.1f), Tooltip("砲管伸出塔身外的長度。")]
        private float barrelLength = 3.5f;

        [SerializeField, Min(0.05f)]
        private float barrelThickness = 0.9f;

        public int TowerNumber => towerNumber;
        public float BaseRadius => baseRadius;
        public float TotalHeight => baseHeight + bodyHeight;
        public Transform BaseVisual => baseVisual;
        public Transform BodyVisual => bodyVisual;
        public Transform BarrelVisual => barrelVisual;

        private void OnValidate()
        {
            ApplyDimensions();
        }

        /// <summary>
        /// 依目前的尺寸參數重新擺放基座、塔身與砲管。
        /// </summary>
        [ContextMenu("套用尺寸")]
        public void ApplyDimensions()
        {
            // Unity 內建 Cylinder 的半徑為 0.5、高度為 2。
            if (baseVisual != null)
            {
                baseVisual.localPosition = new Vector3(0f, baseHeight * 0.5f, 0f);
                baseVisual.localScale = new Vector3(baseRadius * 2f, baseHeight * 0.5f, baseRadius * 2f);
            }

            if (bodyVisual != null)
            {
                bodyVisual.localPosition = new Vector3(0f, baseHeight + bodyHeight * 0.5f, 0f);
                bodyVisual.localScale = new Vector3(bodyRadius * 2f, bodyHeight * 0.5f, bodyRadius * 2f);
            }

            // 砲管從塔身中心伸出，露出塔身外的部分即為 barrelLength。
            var totalLength = bodyRadius + barrelLength;
            var barrelHeight = baseHeight + bodyHeight * 0.75f;
            if (turretPivot != null)
            {
                // 有樞紐時：樞紐在砲管高度的塔身中心，砲管與砲口使用樞紐的本地座標。
                turretPivot.localPosition = new Vector3(0f, barrelHeight, 0f);
                if (barrelVisual != null)
                {
                    barrelVisual.localPosition = new Vector3(0f, 0f, totalLength * 0.5f);
                    barrelVisual.localScale = new Vector3(barrelThickness, barrelThickness, totalLength);
                }

                if (muzzle != null)
                {
                    muzzle.localPosition = new Vector3(0f, 0f, totalLength + 0.3f);
                }
            }
            else if (barrelVisual != null)
            {
                barrelVisual.localPosition = new Vector3(0f, barrelHeight, totalLength * 0.5f);
                barrelVisual.localScale = new Vector3(barrelThickness, barrelThickness, totalLength);
            }
        }
    }
}
