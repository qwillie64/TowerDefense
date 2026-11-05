using System;
using System.Collections.Generic;
using UnityEngine;

namespace CyberWizard.Data
{
    [Serializable]
    public sealed class LevelWaveScheduleDto
    {
        public int version;
        public string levelId;
        public WaveDto[] waves;
    }

    [Serializable]
    public sealed class WaveDto
    {
        public string waveId;
        public float startTime;
        public EnemySpawnDto[] enemies;
    }

    [Serializable]
    public sealed class EnemySpawnDto
    {
        public string enemyTypeId;
        public int count;
        public string spawnPointId;
        public float spawnInterval;
    }

    public static class LevelScheduleParser
    {
        public const int SupportedVersion = 1;

        public static bool TryParse(string json, out LevelWaveScheduleDto schedule, out string error)
        {
            schedule = null;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "關卡 JSON 是空的。";
                return false;
            }

            try
            {
                schedule = JsonUtility.FromJson<LevelWaveScheduleDto>(json);
            }
            catch (Exception exception)
            {
                error = $"關卡 JSON 無法解析：{exception.Message}";
                return false;
            }

            return Validate(schedule, out error);
        }

        private static bool Validate(LevelWaveScheduleDto schedule, out string error)
        {
            if (schedule == null)
            {
                error = "關卡 JSON 沒有產生有效資料。";
                return false;
            }

            if (schedule.version != SupportedVersion)
            {
                error = $"不支援關卡 JSON 版本 {schedule.version}，目前版本為 {SupportedVersion}。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(schedule.levelId))
            {
                error = "levelId 不可為空。";
                return false;
            }

            if (schedule.waves == null || schedule.waves.Length == 0)
            {
                error = $"關卡 {schedule.levelId} 沒有設定波次。";
                return false;
            }

            var waveIds = new HashSet<string>(StringComparer.Ordinal);
            for (var waveIndex = 0; waveIndex < schedule.waves.Length; waveIndex++)
            {
                var wave = schedule.waves[waveIndex];
                if (wave == null)
                {
                    error = $"波次索引 {waveIndex} 是空資料。";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(wave.waveId) || !waveIds.Add(wave.waveId))
                {
                    error = $"波次索引 {waveIndex} 的 waveId 為空或重複。";
                    return false;
                }

                if (wave.startTime < 0f)
                {
                    error = $"波次 {wave.waveId} 的 startTime 不可小於零。";
                    return false;
                }

                if (wave.enemies == null || wave.enemies.Length == 0)
                {
                    error = $"波次 {wave.waveId} 沒有敵人設定。";
                    return false;
                }

                for (var entryIndex = 0; entryIndex < wave.enemies.Length; entryIndex++)
                {
                    var entry = wave.enemies[entryIndex];
                    if (entry == null || string.IsNullOrWhiteSpace(entry.enemyTypeId))
                    {
                        error = $"波次 {wave.waveId} 的敵人設定 {entryIndex} 缺少 enemyTypeId。";
                        return false;
                    }

                    if (entry.count <= 0)
                    {
                        error = $"波次 {wave.waveId} 的敵人數量必須大於零。";
                        return false;
                    }

                    if (entry.spawnInterval < 0f)
                    {
                        error = $"波次 {wave.waveId} 的 spawnInterval 不可小於零。";
                        return false;
                    }
                }
            }

            error = string.Empty;
            return true;
        }
    }
}
