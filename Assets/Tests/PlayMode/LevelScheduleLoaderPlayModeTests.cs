using System;
using System.Collections;
using System.IO;
using CyberWizard.Data;
using CyberWizard.Infrastructure;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CyberWizard.Tests
{
    /// <summary>
    /// WebGL 執行檔讀取 StreamingAssets 時走 UnityWebRequest。編輯器無法直接跑 WebGL，
    /// 這裡以 file:// 網址呼叫同一段程式，確認請求、解析與錯誤回報流程。
    /// </summary>
    public sealed class LevelScheduleLoaderPlayModeTests
    {
        private const string SchedulePath = "Levels/level1_rust_alley.json";

        [UnityTest]
        public IEnumerator LoadWithWebRequest_ReadsAndParsesSchedule()
        {
            var uri = new Uri(Path.Combine(Application.streamingAssetsPath, SchedulePath)).AbsoluteUri;
            LevelWaveScheduleDto schedule = null;
            string error = null;
            var completed = false;

            yield return LevelScheduleLoader.LoadWithWebRequest(uri, SchedulePath, (loaded, loadError) =>
            {
                schedule = loaded;
                error = loadError;
                completed = true;
            });

            Assert.That(completed, Is.True);
            Assert.That(error, Is.Empty);
            Assert.That(schedule, Is.Not.Null);
            Assert.That(schedule.levelId, Is.EqualTo("level1_rust_alley"));
            Assert.That(schedule.waves.Length, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator LoadWithWebRequest_MissingFile_ReportsError()
        {
            var uri = new Uri(Path.Combine(Application.streamingAssetsPath, "Levels/does_not_exist.json")).AbsoluteUri;
            LevelWaveScheduleDto schedule = null;
            string error = null;
            var completed = false;

            yield return LevelScheduleLoader.LoadWithWebRequest(uri, "Levels/does_not_exist.json", (loaded, loadError) =>
            {
                schedule = loaded;
                error = loadError;
                completed = true;
            });

            Assert.That(completed, Is.True);
            Assert.That(schedule, Is.Null);
            Assert.That(error, Does.Contain("does_not_exist.json"));
        }
    }
}
