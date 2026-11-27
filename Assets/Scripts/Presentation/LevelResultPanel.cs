using System.Collections;
using CyberWizard.Core;
using CyberWizard.Gameplay.GameFlow;
using CyberWizard.Gameplay.Player;
using CyberWizard.Infrastructure;
using UnityEngine;
using UnityEngine.UI;

namespace CyberWizard.Presentation
{
    /// <summary>
    /// 關卡結算面板：監聽 GateDefenseFlow 的狀態，勝利（Ended）或失敗（Defeat）後等待一段真實時間
    /// （讓玩家看到結束慢動作），再顯示結果與「再次挑戰」「確認」兩個按鈕。
    /// 面板出現時解除滑鼠鎖定並停用玩家操作，避免點按鈕時同時攻擊或轉動視角。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelResultPanel : MonoBehaviour
    {
        [SerializeField] private GateDefenseFlow flow;

        [SerializeField, Tooltip("面板根物件；場景中預設隱藏，結算時才啟用。")]
        private GameObject panelRoot;

        [SerializeField] private Text titleText;
        [SerializeField] private Text detailText;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button confirmButton;

        [SerializeField, Tooltip("可選：面板出現時關閉滑鼠視角並釋放游標。")]
        private PrototypeCameraFollow cameraFollow;

        [SerializeField, Tooltip("可選：面板出現時停用玩家移動與攻擊。")]
        private PrototypePlayerController player;

        [SerializeField, Min(0f), Tooltip("勝敗判定後到顯示面板的等待時間（真實秒數，不受慢動作影響）。暫定值。")]
        private float showDelaySeconds = 2f;

        [SerializeField, Tooltip("按下「確認」後載入的場景名稱（需加入 Build Settings）。")]
        private string levelSelectSceneName = "LevelSelect";

        [SerializeField] private string victoryTitle = "勝利";
        [SerializeField] private string defeatTitle = "失敗";
        [SerializeField] private string victoryDetail = "所有波次已清除";
        [SerializeField] private Color victoryColor = new Color(0.49f, 1f, 0.63f);
        [SerializeField] private Color defeatColor = new Color(1f, 0.42f, 0.42f);

        private Coroutine _showRoutine;

        public bool IsShowing => panelRoot != null && panelRoot.activeSelf;
        public Text TitleText => titleText;
        public Button RetryButton => retryButton;
        public Button ConfirmButton => confirmButton;

        private void Awake()
        {
            if (flow == null || panelRoot == null || titleText == null || retryButton == null || confirmButton == null)
            {
                Debug.LogError("[LevelResultPanel] 缺少 GateDefenseFlow、面板或按鈕的參考，結算面板無法運作。", this);
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

            flow.PhaseChanged += HandlePhaseChanged;
            retryButton.onClick.AddListener(HandleRetry);
            confirmButton.onClick.AddListener(HandleConfirm);
        }

        private void OnDisable()
        {
            if (flow == null)
            {
                return;
            }

            flow.PhaseChanged -= HandlePhaseChanged;
            retryButton.onClick.RemoveListener(HandleRetry);
            confirmButton.onClick.RemoveListener(HandleConfirm);
        }

        private void HandlePhaseChanged(GamePhase phase)
        {
            if ((phase != GamePhase.Ended && phase != GamePhase.Defeat) || _showRoutine != null)
            {
                return;
            }

            _showRoutine = StartCoroutine(ShowAfterDelay(phase == GamePhase.Ended));
        }

        private IEnumerator ShowAfterDelay(bool victory)
        {
            if (showDelaySeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(showDelaySeconds);
            }

            Show(victory);
        }

        private void Show(bool victory)
        {
            titleText.text = victory ? victoryTitle : defeatTitle;
            titleText.color = victory ? victoryColor : defeatColor;
            if (detailText != null)
            {
                detailText.text = victory ? victoryDetail : flow.FailureReason;
            }

            if (cameraFollow != null)
            {
                cameraFollow.SetGameplayActive(false);
            }

            if (player != null)
            {
                player.InputEnabled = false;
            }

            panelRoot.SetActive(true);
        }

        private void HandleRetry()
        {
            SceneLoading.TryReloadActive(this);
        }

        private void HandleConfirm()
        {
            SceneLoading.TryLoad(levelSelectSceneName, this);
        }
    }
}
