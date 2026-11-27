using CyberWizard.Data;
using CyberWizard.Infrastructure;
using NUnit.Framework;
using UnityEngine;

namespace CyberWizard.Tests
{
    public sealed class LevelScheduleLoaderEditModeTests
    {
        private const string SchedulePath = "Levels/level1_rust_alley.json";

        [Test]
        public void Load_InEditor_ReadsFileSynchronouslyOnFirstMoveNext()
        {
            LevelWaveScheduleDto schedule = null;
            string error = null;
            var loading = LevelScheduleLoader.Load(SchedulePath, (loaded, loadError) =>
            {
                schedule = loaded;
                error = loadError;
            });

            // 編輯器與本地執行檔走檔案讀取：不需要等待，第一次 MoveNext() 就結束。
            Assert.That(loading.MoveNext(), Is.False);
            Assert.That(error, Is.Empty);
            Assert.That(schedule, Is.Not.Null);
            Assert.That(schedule.waves.Length, Is.EqualTo(2));
        }

        [Test]
        public void Load_MissingFile_ReportsError()
        {
            LevelWaveScheduleDto schedule = null;
            string error = null;
            var loading = LevelScheduleLoader.Load("Levels/does_not_exist.json", (loaded, loadError) =>
            {
                schedule = loaded;
                error = loadError;
            });

            Assert.That(loading.MoveNext(), Is.False);
            Assert.That(schedule, Is.Null);
            Assert.That(error, Does.Contain("does_not_exist.json"));
        }

        [Test]
        public void BuildUri_JoinsStreamingAssetsPathWithForwardSlashes()
        {
            var uri = LevelScheduleLoader.BuildUri("\\Levels\\level1_rust_alley.json");

            Assert.That(uri, Does.StartWith(Application.streamingAssetsPath.TrimEnd('/', '\\') + "/"));
            Assert.That(uri, Does.EndWith("/Levels/level1_rust_alley.json"));
            Assert.That(uri, Does.Not.Contain("//Levels"));
        }
    }
}
