using System;
using System.IO;
using CyberWizard.Data;
using UnityEngine;

namespace CyberWizard.Infrastructure
{
    public static class LevelScheduleLoader
    {
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
    }
}
