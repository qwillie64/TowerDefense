using CyberWizard.Infrastructure;
using UnityEngine;
using UnityEngine.UI;

namespace CyberWizard.Presentation.Menus
{
    /// <summary>
    /// 遊戲初始畫面：「開始」切換到選關場景，「離開」結束程式（編輯器中則停止 Play Mode）。
    /// 只負責按鈕與場景切換，畫面配置在 MainMenu 場景中編輯。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] private Button startButton;
        [SerializeField] private Button quitButton;

        [SerializeField, Tooltip("按下「開始」後載入的場景名稱（需加入 Build Settings）。")]
        private string levelSelectSceneName = "LevelSelect";

        public Button StartButton => startButton;
        public Button QuitButton => quitButton;

        private void Awake()
        {
            if (startButton == null || quitButton == null)
            {
                Debug.LogError("[MainMenuController] 缺少「開始」或「離開」按鈕的參考。", this);
            }
        }

        private void OnEnable()
        {
            if (startButton != null)
            {
                startButton.onClick.AddListener(HandleStart);
            }

            if (quitButton != null)
            {
                quitButton.onClick.AddListener(HandleQuit);
            }
        }

        private void OnDisable()
        {
            if (startButton != null)
            {
                startButton.onClick.RemoveListener(HandleStart);
            }

            if (quitButton != null)
            {
                quitButton.onClick.RemoveListener(HandleQuit);
            }
        }

        private void HandleStart()
        {
            SceneLoading.TryLoad(levelSelectSceneName, this);
        }

        private void HandleQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
