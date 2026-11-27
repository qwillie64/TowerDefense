using System;
using System.Collections;
using System.IO;
using CyberWizard.Data;
using UnityEngine;
using UnityEngine.Networking;

namespace CyberWizard.Infrastructure
{
    /// <summary>
    /// 從 StreamingAssets 讀取關卡波次 JSON。
    /// WebGL 輸出時 StreamingAssets 是伺服器上的網址，無法用 System.IO 讀取，改用 UnityWebRequest；
    /// 本地執行檔與編輯器直接讀檔（同步完成）。
    /// </summary>
    public static class LevelScheduleLoader
    {
        /// <summary>
        /// 依平台選擇讀取方式。呼叫端以 MoveNext() 手動走訪或交給 StartCoroutine：
        /// 本地讀檔不會 yield，第一次 MoveNext() 就完成並呼叫 onCompleted；
        /// WebGL 會 yield 網路請求，請求結束後才呼叫 onCompleted。
        /// onCompleted 的參數：成功時為 (schedule, 空字串)，失敗時為 (null, 錯誤訊息)。
        /// </summary>
        public static IEnumerator Load(string relativePath, Action<LevelWaveScheduleDto, string> onCompleted)
        {
            if (onCompleted == null)
            {
                throw new ArgumentNullException(nameof(onCompleted));
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            return LoadWithWebRequest(BuildUri(relativePath), relativePath, onCompleted);
#else
            return LoadFromFile(relativePath, onCompleted);
#endif
        }

        /// <summary>本地檔案系統同步讀取（編輯器與本地執行檔）。WebGL 執行檔無法使用。</summary>
        public static bool TryLoadFromStreamingAssets(
            string relativePath,
            out LevelWaveScheduleDto schedule,
            out string error)
        {
            schedule = null;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(relativePath))
            {
                error = "關卡 JSON 路徑不可為空。";
                return false;
            }

            var fullPath = Path.Combine(Application.streamingAssetsPath, relativePath);
            if (!File.Exists(fullPath))
            {
                error = $"找不到關卡 JSON：{fullPath}";
                return false;
            }

            try
            {
                var json = File.ReadAllText(fullPath);
                return LevelScheduleParser.TryParse(json, out schedule, out error);
            }
            catch (Exception exception)
            {
                error = $"讀取關卡 JSON 失敗：{exception.Message}";
                return false;
            }
        }

        /// <summary>
        /// 以 UnityWebRequest 讀取指定網址的關卡 JSON。WebGL 執行檔由 Load() 走這條路徑；
        /// 其他平台也能編譯與呼叫（例如以 file:// 網址測試）。
        /// </summary>
        public static IEnumerator LoadWithWebRequest(
            string uri,
            string relativePath,
            Action<LevelWaveScheduleDto, string> onCompleted)
        {
            if (onCompleted == null)
            {
                throw new ArgumentNullException(nameof(onCompleted));
            }

            if (string.IsNullOrWhiteSpace(relativePath) || string.IsNullOrWhiteSpace(uri))
            {
                onCompleted(null, "關卡 JSON 路徑不可為空。");
                yield break;
            }

            using (var request = UnityWebRequest.Get(uri))
            {
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    onCompleted(null, $"讀取關卡 JSON 失敗（{uri}）：{request.error}");
                    yield break;
                }

                var parsed = LevelScheduleParser.TryParse(request.downloadHandler.text, out var schedule, out var error);
                onCompleted(parsed ? schedule : null, parsed ? string.Empty : error);
            }
        }

        /// <summary>組合 StreamingAssets 底下檔案的網址；路徑分隔一律用 '/'。</summary>
        public static string BuildUri(string relativePath)
        {
            var root = Application.streamingAssetsPath.TrimEnd('/', '\\');
            var relative = (relativePath ?? string.Empty).Replace('\\', '/').TrimStart('/');
            return $"{root}/{relative}";
        }

        private static IEnumerator LoadFromFile(string relativePath, Action<LevelWaveScheduleDto, string> onCompleted)
        {
            var loaded = TryLoadFromStreamingAssets(relativePath, out var schedule, out var error);
            onCompleted(loaded ? schedule : null, loaded ? string.Empty : error);
            yield break;
        }
    }
}
