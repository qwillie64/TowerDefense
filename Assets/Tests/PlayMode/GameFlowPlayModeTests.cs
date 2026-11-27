using System.Collections;
using System.Linq;
using CyberWizard.Core;
using CyberWizard.Gameplay.Enemies;
using CyberWizard.Gameplay.GameFlow;
using CyberWizard.Gameplay.Player;
using CyberWizard.Gameplay.Waves;
using CyberWizard.Presentation;
using CyberWizard.Presentation.Menus;
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
    /// 遊戲流程：初始畫面 ⇄ 選關 → Level_1 → 暫停面板（繼續／重新開始／中離）與結算面板（再次挑戰／確認回選關）。
    /// 按鈕以 onClick.Invoke() 觸發，不模擬滑鼠點擊。
    /// </summary>
    public sealed class GameFlowPlayModeTests
    {
        private const string MainMenuPath = "Assets/Scenes/MainMenu.unity";
        private const string LevelSelectPath = "Assets/Scenes/LevelSelect.unity";
        private const string Level1Path = "Assets/Scenes/Level_1.unity";
        private const float PanelWaitLimit = 10f;

        private float _previousTimeScale;

        [SetUp]
        public void SetUp()
        {
            _previousTimeScale = Time.timeScale;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = _previousTimeScale;
        }

        [UnityTest]
        public IEnumerator MainMenu_StartButtonLoadsLevelSelect()
        {
            yield return LoadScene(MainMenuPath);

            var menu = Object.FindObjectOfType<MainMenuController>();
            Assert.That(menu, Is.Not.Null);
            Assert.That(menu.StartButton, Is.Not.Null);
            Assert.That(menu.QuitButton, Is.Not.Null);
            Assert.That(menu.StartButton.transform.position.y, Is.GreaterThan(menu.QuitButton.transform.position.y),
                "「開始」應排在「離開」上方。");

            menu.StartButton.onClick.Invoke();
            yield return WaitForActiveScene("LevelSelect");
        }

        [UnityTest]
        public IEnumerator LevelSelect_FirstSlotLoadsLevel1AndOtherSlotsAreLocked()
        {
            yield return LoadScene(LevelSelectPath);

            var select = Object.FindObjectOfType<LevelSelectController>();
            Assert.That(select, Is.Not.Null);
            Assert.That(select.LevelCount, Is.EqualTo(3));
            Assert.That(select.GetButton(0).interactable, Is.True);
            Assert.That(select.GetButton(1).interactable, Is.False, "第 2 關尚未開放。");
            Assert.That(select.GetButton(2).interactable, Is.False, "第 3 關尚未開放。");

            select.GetButton(0).onClick.Invoke();
            yield return WaitForActiveScene("Level_1");
            yield return null;

            var flow = Object.FindObjectOfType<GateDefenseFlow>();
            Assert.That(flow, Is.Not.Null);
            Assert.That(flow.CurrentPhase, Is.EqualTo(GamePhase.Wave), flow.FailureReason);
        }

        [UnityTest]
        public IEnumerator Level1_VictoryShowsResultPanelAndConfirmReturnsToLevelSelect()
        {
            yield return LoadScene(Level1Path);

            var flow = Object.FindObjectOfType<GateDefenseFlow>();
            var panel = Object.FindObjectOfType<LevelResultPanel>();
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.IsShowing, Is.False, "遊玩中不應顯示結算面板。");
            Assert.That(flow.CurrentPhase, Is.EqualTo(GamePhase.Wave), flow.FailureReason);

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
            Assert.That(panel.IsShowing, Is.False, "面板應在慢動作延遲之後才出現。");

            yield return WaitForPanel(panel);
            Assert.That(panel.TitleText.text, Is.EqualTo("勝利"));
            AssertControlReleased();

            panel.ConfirmButton.onClick.Invoke();
            yield return WaitForActiveScene("LevelSelect");
            Assert.That(Time.timeScale, Is.EqualTo(1f), "離開關卡後時間倍率應還原。");
        }

        [UnityTest]
        public IEnumerator Level1_DefeatShowsResultPanelAndRetryReloadsLevel()
        {
            yield return LoadScene(Level1Path);

            var flow = Object.FindObjectOfType<GateDefenseFlow>();
            var panel = Object.FindObjectOfType<LevelResultPanel>();
            var gate = Object.FindObjectOfType<BaseCore>();
            Assert.That(panel, Is.Not.Null);
            Assert.That(flow.CurrentPhase, Is.EqualTo(GamePhase.Wave), flow.FailureReason);

            gate.TakeDamage(gate.MaxHealth * 10f);
            Assert.That(flow.CurrentPhase, Is.EqualTo(GamePhase.Defeat));

            var pause = Object.FindObjectOfType<LevelPausePanel>();
            pause.TogglePause();
            Assert.That(flow.CurrentPhase, Is.EqualTo(GamePhase.Defeat), "分出勝負後不能暫停。");
            Assert.That(pause.IsShowing, Is.False);

            yield return WaitForPanel(panel);
            Assert.That(panel.TitleText.text, Is.EqualTo("失敗"));
            AssertControlReleased();

            panel.RetryButton.onClick.Invoke();
            yield return null;
            yield return null;

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Level_1"));
            Assert.That(flow == null, Is.True, "再次挑戰應重新載入場景，舊的流程物件應已銷毀。");
            var newFlow = Object.FindObjectOfType<GateDefenseFlow>();
            var newPanel = Object.FindObjectOfType<LevelResultPanel>();
            Assert.That(newFlow.CurrentPhase, Is.EqualTo(GamePhase.Wave), newFlow.FailureReason);
            Assert.That(newPanel.IsShowing, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f), "重新挑戰後時間倍率應還原。");
        }

        [UnityTest]
        public IEnumerator LevelSelect_BackButtonAtTopLeftReturnsToMainMenu()
        {
            yield return LoadScene(LevelSelectPath);

            var select = Object.FindObjectOfType<LevelSelectController>();
            Assert.That(select.BackButton, Is.Not.Null);
            var rect = (RectTransform)select.BackButton.transform;
            Assert.That(rect.anchorMin, Is.EqualTo(new Vector2(0f, 1f)), "返回按鈕應錨定在左上角。");
            Assert.That(rect.anchorMax, Is.EqualTo(new Vector2(0f, 1f)));

            select.BackButton.onClick.Invoke();
            yield return WaitForActiveScene("MainMenu");
        }

        [UnityTest]
        public IEnumerator Level1_PauseFreezesTimeAndResumeRestoresControl()
        {
            yield return LoadScene(Level1Path);

            var flow = Object.FindObjectOfType<GateDefenseFlow>();
            var waveDirector = Object.FindObjectOfType<WaveDirector>();
            var pause = Object.FindObjectOfType<LevelPausePanel>();
            var cameraFollow = Object.FindObjectOfType<PrototypeCameraFollow>();
            var player = Object.FindObjectOfType<PrototypePlayerController>();
            Assert.That(pause, Is.Not.Null);
            Assert.That(pause.IsShowing, Is.False, "遊玩中預設不顯示暫停面板。");
            Assert.That(flow.CurrentPhase, Is.EqualTo(GamePhase.Wave), flow.FailureReason);

            pause.TogglePause();
            Assert.That(flow.CurrentPhase, Is.EqualTo(GamePhase.Paused));
            Assert.That(Time.timeScale, Is.EqualTo(0f));
            Assert.That(pause.IsShowing, Is.True);
            AssertControlReleased();

            var elapsed = waveDirector.ElapsedTime;
            for (var frame = 0; frame < 10; frame++)
            {
                yield return null;
            }

            Assert.That(waveDirector.ElapsedTime, Is.EqualTo(elapsed), "暫停中波次時間不應前進。");

            pause.ResumeButton.onClick.Invoke();
            Assert.That(flow.CurrentPhase, Is.EqualTo(GamePhase.Wave));
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(pause.IsShowing, Is.False);
            Assert.That(cameraFollow.GameplayActive, Is.True, "繼續後應恢復滑鼠視角。");
            // 批次模式沒有取得焦點的視窗，Cursor.lockState 無法設為 Locked，所以只檢查 GameplayActive。
            Assert.That(player.InputEnabled, Is.True, "繼續後應恢復玩家操作。");

            // 再按一次暫停鍵（Esc）也能切換。
            pause.TogglePause();
            Assert.That(flow.CurrentPhase, Is.EqualTo(GamePhase.Paused));
            pause.TogglePause();
            Assert.That(flow.CurrentPhase, Is.EqualTo(GamePhase.Wave));
            Assert.That(pause.IsShowing, Is.False);
        }

        [UnityTest]
        public IEnumerator Level1_PauseRestartReloadsLevelAtNormalSpeed()
        {
            yield return LoadScene(Level1Path);

            var flow = Object.FindObjectOfType<GateDefenseFlow>();
            var pause = Object.FindObjectOfType<LevelPausePanel>();
            pause.TogglePause();
            Assert.That(flow.CurrentPhase, Is.EqualTo(GamePhase.Paused));

            pause.RestartButton.onClick.Invoke();
            yield return null;
            yield return null;

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Level_1"));
            Assert.That(flow == null, Is.True, "重新開始應重新載入場景。");
            var newFlow = Object.FindObjectOfType<GateDefenseFlow>();
            Assert.That(newFlow.CurrentPhase, Is.EqualTo(GamePhase.Wave), newFlow.FailureReason);
            Assert.That(Object.FindObjectOfType<LevelPausePanel>().IsShowing, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f), "從暫停重新開始後時間倍率應還原。");
        }

        [UnityTest]
        public IEnumerator Level1_PauseQuitReturnsToLevelSelectAtNormalSpeed()
        {
            yield return LoadScene(Level1Path);

            var pause = Object.FindObjectOfType<LevelPausePanel>();
            pause.TogglePause();
            Assert.That(Time.timeScale, Is.EqualTo(0f));

            pause.QuitButton.onClick.Invoke();
            yield return WaitForActiveScene("LevelSelect");
            Assert.That(Time.timeScale, Is.EqualTo(1f), "從暫停中離後時間倍率應還原。");
        }

        private static IEnumerator LoadScene(string path)
        {
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
#else
            Assert.Ignore("此測試只能在編輯器中執行。");
            yield break;
#endif
        }

        private static IEnumerator WaitForActiveScene(string sceneName)
        {
            var deadline = Time.realtimeSinceStartup + 10f;
            while (SceneManager.GetActiveScene().name != sceneName && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(sceneName));
        }

        private static IEnumerator WaitForPanel(LevelResultPanel panel)
        {
            var deadline = Time.realtimeSinceStartup + PanelWaitLimit;
            while (!panel.IsShowing && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(panel.IsShowing, Is.True, "時限內沒有顯示結算面板。");
        }

        private static void AssertControlReleased()
        {
            var cameraFollow = Object.FindObjectOfType<PrototypeCameraFollow>();
            var player = Object.FindObjectOfType<PrototypePlayerController>();
            Assert.That(cameraFollow.GameplayActive, Is.False, "面板出現後應關閉滑鼠視角。");
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None), "面板出現後應釋放游標。");
            Assert.That(player.InputEnabled, Is.False, "面板出現後應停用玩家操作。");
        }
    }
}
