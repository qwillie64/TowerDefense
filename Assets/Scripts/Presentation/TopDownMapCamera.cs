using UnityEngine;

namespace CyberWizard.Presentation
{
    /// <summary>
    /// 正交俯視攝影機：依地圖範圍與目前畫面長寬比自動調整位置與 Orthographic Size，
    /// 確保整張地圖都在畫面內。畫面上方為北（世界 +Z），右方為東（世界 +X）。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class TopDownMapCamera : MonoBehaviour
    {
        [SerializeField, Tooltip("地圖中心（世界 XZ 座標）。")]
        private Vector2 mapCenter;

        [SerializeField, Tooltip("地圖範圍（X 為東西向寬度，Y 為南北向深度）。")]
        private Vector2 mapSize = new Vector2(100f, 60f);

        [SerializeField, Min(0f), Tooltip("地圖邊緣外額外保留的距離。")]
        private float padding = 1f;

        [SerializeField, Min(1f), Tooltip("攝影機高度；需高於場景中最高的物件。")]
        private float cameraHeight = 40f;

        private Camera targetCamera;
        private float fittedAspect;

        private void OnEnable()
        {
            Fit();
        }

        private void OnValidate()
        {
            Fit();
        }

        private void Update()
        {
            // 視窗比例改變時才重新取景，平常只做一次比較。
            if (targetCamera != null && !Mathf.Approximately(targetCamera.aspect, fittedAspect))
            {
                Fit();
            }
        }

        /// <summary>
        /// 立即依目前參數與畫面比例重新取景。
        /// </summary>
        public void Fit()
        {
            if (targetCamera == null && !TryGetComponent(out targetCamera))
            {
                return;
            }

            var halfWidth = mapSize.x * 0.5f + padding;
            var halfDepth = mapSize.y * 0.5f + padding;
            fittedAspect = targetCamera.aspect;

            targetCamera.orthographic = true;
            targetCamera.orthographicSize = Mathf.Max(halfDepth, halfWidth / Mathf.Max(fittedAspect, 0.01f));
            transform.SetPositionAndRotation(
                new Vector3(mapCenter.x, cameraHeight, mapCenter.y),
                Quaternion.Euler(90f, 0f, 0f));
        }
    }
}
