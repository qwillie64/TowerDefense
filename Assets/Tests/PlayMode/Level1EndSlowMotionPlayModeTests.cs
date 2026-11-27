using System.Collections;
using System.Linq;
using CyberWizard.Core;
using CyberWizard.Gameplay.Enemies;
using CyberWizard.Gameplay.GameFlow;
using CyberWizard.Gameplay.Player;
using CyberWizard.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace CyberWizard.Tests
{
    /// <summary>
    /// 關卡結束（勝利）的瞬間遊戲時間放慢為 0.25 倍，玩家操作與滑鼠視角維持啟用。
    /// 失敗的情況由 Level1GateDefensePlayModeTests 驗證。
    /// </summary>
    public sealed class Level1EndSlowMotionPlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/Level_1.unity";
        private const float EndTimeScale = 0.25f;

        private float _previousTimeScale;
        private float _previousFixedDeltaTime;

        [SetUp]
        public void SetUp()
        {
            _previousTimeScale = Time.timeScale;
            _previousFixedDeltaTime = Time.fixedDeltaTime;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = _previousTimeScale;
            Time.fixedDeltaTime = _previousFixedDeltaTime;
        }

        [UnityTest]
        public IEnumerator Victory_SlowsGameTimeAndKeepsPlayerControl()
        {
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                ScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Ignore("此測試只能在編輯器中執行。");
            yield break;
#endif
            yield return null;

            var flow = Object.FindObjectOfType<GateDefenseFlow>();
            var player = Object.FindObjectOfType<PrototypePlayerController>();
            var cameraFollow = Object.FindObjectOfType<PrototypeCameraFollow>();
            Assert.That(flow.CurrentPhase, Is.EqualTo(GamePhase.Wave), flow.FailureReason);
            var defaultFixedDeltaTime = Time.fixedDeltaTime;

            // 加速並直接擊殺每個生成的敵人，讓兩波很快結束。
            Time.timeScale = 20f;
            var deadline = Time.realtimeSinceStartup + 60f;
            while (flow.CurrentPhase == GamePhase.Wave && Time.realtimeSinceStartup < deadline)
            {
                foreach (var enemy in EnemyAgent.Active.Where(agent => agent != null && agent.IsAlive).ToList())
                {
                    enemy.TakeDamage(1000000f);
                }

                yield return null;
            }

            Assert.That(flow.CurrentPhase, Is.EqualTo(GamePhase.Ended), "時限內沒有勝利。");
            Assert.That(Time.timeScale, Is.EqualTo(EndTimeScale).Within(0.0001f), "勝利瞬間應放慢為 0.25 倍。");
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(defaultFixedDeltaTime).Within(0.00001f), "物理步長不跟著縮小。");
            Assert.That(player.InputEnabled, Is.True, "結束後不再停用玩家操作。");
            Assert.That(cameraFollow.GameplayActive, Is.True, "結束後滑鼠視角維持啟用。");

            // 慢動作持續：之後的幀仍是 0.25 倍，遊戲時間前進速度約為真實時間的 1/4。
            var realStart = Time.realtimeSinceStartup;
            var gameStart = Time.time;
            for (var frame = 0; frame < 30; frame++)
            {
                yield return null;
            }

            var realElapsed = Time.realtimeSinceStartup - realStart;
            var gameElapsed = Time.time - gameStart;
            Assert.That(Time.timeScale, Is.EqualTo(EndTimeScale).Within(0.0001f));
            Assert.That(gameElapsed, Is.LessThan(realElapsed * 0.3f + 0.05f), $"遊戲時間 {gameElapsed:0.000} 秒、真實時間 {realElapsed:0.000} 秒。");
        }
    }
}
