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
    /// 「重新開始」重新載入目前關卡，「中離」先顯示確認視窗，按「確定中離」才離開關卡（預設回到選關）。
    /// 確認視窗在執行時複製暫停視窗（Window）建立，沿用相同外觀：標題改為確認訊息、移除「重新開始」，
    /// 「繼續」與「中離」改為「取消」與「確定中離」。確認視窗開啟時按 Esc 等同取消。
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

        [Header("中離確認")]
        [SerializeField, Tooltip("按下「中離」時先顯示確認視窗；關閉時直接離開。")]
        private bool confirmBeforeQuit = true;

        [SerializeField, TextArea] private string confirmMessage = "確定要中離嗎？\n目前關卡進度不會保留。";
        [SerializeField] private string confirmQuitLabel = "確定中離";
        [SerializeField] private string cancelQuitLabel = "取消";

        private GameObject _pauseWindow;
        private GameObject _confirmWindow;
        private Button _confirmQuitButton;
        private Button _cancelQuitButton;

        public bool IsShowing => panelRoot != null && panelRoot.activeSelf;
        public Button ResumeButton => resumeButton;
        public Button RestartButton => restartButton;
        public Button QuitButton => quitButton;
        public bool IsConfirmingQuit => _confirmWindow != null && _confirmWindow.activeSelf;
        public Button ConfirmQuitButton => _confirmQuitButton;
        public Button CancelQuitButton => _cancelQuitButton;

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
            if (IsConfirmingQuit)
            {
                CancelQuit();
                return;
            }

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

            CancelQuit();
            panelRoot.SetActive(false);
            SetGameplayControl(true);
        }

        private void HandleRestart()
        {
            SceneLoading.TryReloadActive(this);
        }

        private void HandleQuit()
        {
            if (!confirmBeforeQuit || !EnsureConfirmWindow())
            {
                ConfirmQuit();
                return;
            }

            _pauseWindow.SetActive(false);
            _confirmWindow.SetActive(true);
        }

        private void ConfirmQuit()
        {
            SceneLoading.TryLoad(quitSceneName, this);
        }

        private void CancelQuit()
        {
            if (_confirmWindow == null)
            {
                return;
            }

            _confirmWindow.SetActive(false);
            _pauseWindow.SetActive(true);
        }

        /// <summary>
        /// 第一次需要時建立確認視窗：找出包含「中離」按鈕、位於 panelRoot 正下方的視窗物件並複製一份。
        /// 找不到對應結構時回傳 false（直接中離，不確認）。
        /// </summary>
        private bool EnsureConfirmWindow()
        {
            if (_confirmWindow != null)
            {
                return true;
            }

            var window = FindDirectChildContaining(panelRoot.transform, quitButton.transform);
            if (window == null)
            {
                Debug.LogWarning("[LevelPausePanel] 中離按鈕不在暫停面板的視窗中，無法建立確認視窗，將直接中離。", this);
                return false;
            }

            var resumePath = RelativePath(window, resumeButton.transform);
            var restartPath = RelativePath(window, restartButton.transform);
            var quitPath = RelativePath(window, quitButton.transform);
            var clone = Instantiate(window.gameObject, window.parent, false);
            clone.name = "Quit Confirm Window";

            var cancel = clone.transform.Find(resumePath);
            var confirm = clone.transform.Find(quitPath);
            var restart = restartPath != null ? clone.transform.Find(restartPath) : null;
            if (cancel == null || confirm == null)
            {
                Destroy(clone);
                Debug.LogWarning("[LevelPausePanel] 複製的確認視窗缺少按鈕，將直接中離。", this);
                return false;
            }

            if (restart != null)
            {
                Destroy(restart.gameObject);
            }

            // 標題：視窗中不屬於任何按鈕的第一個文字。
            foreach (var text in clone.GetComponentsInChildren<Text>(true))
            {
                if (text.GetComponentInParent<Button>() == null)
                {
                    text.text = confirmMessage;
                    break;
                }
            }

            _confirmQuitButton = confirm.GetComponent<Button>();
            _cancelQuitButton = cancel.GetComponent<Button>();
            SetButtonLabel(_confirmQuitButton, confirmQuitLabel);
            SetButtonLabel(_cancelQuitButton, cancelQuitLabel);
            _confirmQuitButton.onClick.AddListener(ConfirmQuit);
            _cancelQuitButton.onClick.AddListener(CancelQuit);

            _pauseWindow = window.gameObject;
            _confirmWindow = clone;
            _confirmWindow.SetActive(false);
            return true;
        }

        private static Transform FindDirectChildContaining(Transform root, Transform descendant)
        {
            var current = descendant;
            while (current != null && current.parent != root)
            {
                current = current.parent;
            }

            return current;
        }

        private static string RelativePath(Transform root, Transform descendant)
        {
            if (descendant == null || !descendant.IsChildOf(root) || descendant == root)
            {
                return null;
            }

            var path = descendant.name;
            for (var current = descendant.parent; current != root; current = current.parent)
            {
                path = current.name + "/" + path;
            }

            return path;
        }

        private static void SetButtonLabel(Button button, string label)
        {
            var text = button.GetComponentInChildren<Text>(true);
            if (text != null)
            {
                text.text = label;
            }
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
