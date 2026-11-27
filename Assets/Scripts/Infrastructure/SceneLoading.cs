using UnityEngine;
using UnityEngine.SceneManagement;

namespace CyberWizard.Infrastructure
{
    /// <summary>
    /// 依場景名稱切換場景，並在名稱空白或場景未加入 Build Settings 時回報錯誤，
    /// 讓選單與結算面板共用同一套檢查。
    /// </summary>
    public static class SceneLoading
    {
        /// <summary>載入指定場景（取代目前場景）；無法載入時回傳 false 並記錄錯誤。</summary>
        public static bool TryLoad(string sceneName, Object context = null)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogError("[SceneLoading] 未設定要載入的場景名稱。", context);
                return false;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[SceneLoading] 場景「{sceneName}」不存在或未加入 Build Settings。", context);
                return false;
            }

            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            return true;
        }

        /// <summary>
        /// 重新載入目前的使用中場景；場景未加入 Build Settings 時回傳 false 並記錄錯誤。
        /// 以名稱而非 buildIndex 載入：以編輯器 API 依路徑開啟的場景 buildIndex 會是 -1。
        /// </summary>
        public static bool TryReloadActive(Object context = null)
        {
            return TryLoad(SceneManager.GetActiveScene().name, context);
        }
    }
}
