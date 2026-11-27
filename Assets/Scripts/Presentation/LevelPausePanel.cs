using CyberWizard.Core;
using CyberWizard.Gameplay.GameFlow;
using CyberWizard.Gameplay.Player;
using CyberWizard.Infrastructure;
using UnityEngine;
using UnityEngine.UI;

namespace CyberWizard.Presentation
{
    /// <summary>
    /// 暫停面板：波次進行中按 Esc 透過 GateDefenseFlow 暫停並顯示面板，再按一次 Esc 或「繼續」恢復。
    /// 「重新開始」重新載入目前關卡，「中離」離開關卡（預設回到選關）。
    /// 面板顯示期間解除滑鼠鎖定並停用玩家操作，避免點按鈕時同時攻擊。
    /// 時間倍率與狀態由 GateDefenseFlow 管理；本元件只處理輸入與畫面。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelPausePanel : MonoBehaviour
    {
        [SerializeField] private GateDefenseFlow flow;

        [SerializeField, Tooltip("面板根物件；場景中預設隱藏，暫停時才啟用。")]
        private GameObject panelRoot;

        [SerializeField] private Button resumeButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button quitButton;

        [SerializeField, Tooltip("可選：暫停時關閉滑鼠視角並釋放游標，恢復時重新啟用。")]
        private PrototypeCameraFollow cameraFollow;

        [SerializeField, Tooltip("可選：暫停時停用玩家移動與攻擊，恢復時重新啟用。")]
        private PrototypePlayerController player;

        [SerializeField, Tooltip("切換暫停的按鍵。")]
        private KeyCode toggleKey = KeyCode.Escape;

        [SerializeField, Tooltip("按下「中離」後載入的場景名稱（需加入 Build Settings）。")]
        private string quitSceneName = "LevelSelect";

        public bool IsShowing => panelRoot != null && panelRoot.activeSelf;
        public Button ResumeButton => resumeButton;
        public Button RestartButton => restartButton;
        public Button QuitButton => quitButton;

        private void Awake()
        {
            if (flow == null || panelRoot == null || resumeButton == null || restartButton == null || quitButton == null)
            {
                Debug.LogError("[LevelPausePanel] 缺少 GateDefenseFlow、面板或按鈕的參考，暫停功能無法運作。", this);
                enabled = false;
                return;
            }

            panelRoot.SetActive(false);
        }

        private void OnEnable()
        {
            if (flow == null)
            {
                return;
            }

            resumeButton.onClick.AddListener(HandleResume);
            restartButton.onClick.AddListener(HandleRestart);
            quitButton.onClick.AddListener(HandleQuit);
        }

        private void OnDisable()
        {
            if (flow == null)
            {
                return;
            }

            resumeButton.onClick.RemoveListener(HandleResume);
            restartButton.onClick.RemoveListener(HandleRestart);
            quitButton.onClick.RemoveListener(HandleQuit);
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                TogglePause();
            }
        }

        /// <summary>波次進行中則暫停、暫停中則恢復；其他狀態（例如已分出勝負）不做任何事。</summary>
        public void TogglePause()
        {
            if (flow.CurrentPhase == GamePhase.Paused)
            {
                HandleResume();
            }
            else if (flow.TryPause())
            {
                SetGameplayControl(false);
                panelRoot.SetActive(true);
            }
        }

        private void HandleResume()
        {
            if (!flow.Resume())
            {
                return;
            }

            panelRoot.SetActive(false);
            SetGameplayControl(true);
        }

        private void HandleRestart()
        {
            SceneLoading.TryReloadActive(this);
        }

        private void HandleQuit()
        {
            SceneLoading.TryLoad(quitSceneName, this);
        }

        private void SetGameplayControl(bool active)
        {
            if (cameraFollow != null)
            {
                cameraFollow.SetGameplayActive(active);
            }

            if (player != null)
            {
                player.InputEnabled = active;
            }
        }
    }
}
