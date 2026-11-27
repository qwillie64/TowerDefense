using System;
using CyberWizard.Infrastructure;
using UnityEngine;
using UnityEngine.UI;

namespace CyberWizard.Presentation.Menus
{
    /// <summary>
    /// 選關畫面：每個關卡格子對應一個場景名稱。場景名稱空白的格子視為尚未開放，
    /// 按鈕設為不可互動，按下也不會有任何作用。左上角的返回按鈕回到初始畫面。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelSelectController : MonoBehaviour
    {
        [Serializable]
        public sealed class LevelEntry
        {
            [Tooltip("關卡格子的按鈕。")]
            public Button button;

            [Tooltip("按下後載入的場景名稱（需加入 Build Settings）；留空表示尚未開放。")]
            public string sceneName;
        }

        [SerializeField] private LevelEntry[] levels = Array.Empty<LevelEntry>();

        [SerializeField, Tooltip("返回初始畫面的按鈕。")]
        private Button backButton;

        [SerializeField, Tooltip("按下返回按鈕後載入的場景名稱（需加入 Build Settings）。")]
        private string mainMenuSceneName = "MainMenu";

        private UnityEngine.Events.UnityAction[] _handlers = Array.Empty<UnityEngine.Events.UnityAction>();

        public int LevelCount => levels.Length;

        public Button GetButton(int index) => levels[index].button;

        public Button BackButton => backButton;

        private void OnEnable()
        {
            if (backButton != null)
            {
                backButton.onClick.AddListener(HandleBack);
            }
            else
            {
                Debug.LogError("[LevelSelectController] 缺少返回按鈕的參考。", this);
            }

            _handlers = new UnityEngine.Events.UnityAction[levels.Length];
            for (var i = 0; i < levels.Length; i++)
            {
                var entry = levels[i];
                if (entry == null || entry.button == null)
                {
                    Debug.LogError($"[LevelSelectController] 第 {i + 1} 個關卡格子缺少按鈕參考。", this);
                    continue;
                }

                var isOpen = !string.IsNullOrWhiteSpace(entry.sceneName);
                entry.button.interactable = isOpen;
                if (!isOpen)
                {
                    continue;
                }

                var sceneName = entry.sceneName;
                _handlers[i] = () => SceneLoading.TryLoad(sceneName, this);
                entry.button.onClick.AddListener(_handlers[i]);
            }
        }

        private void OnDisable()
        {
            if (backButton != null)
            {
                backButton.onClick.RemoveListener(HandleBack);
            }

            for (var i = 0; i < levels.Length && i < _handlers.Length; i++)
            {
                if (_handlers[i] != null && levels[i]?.button != null)
                {
                    levels[i].button.onClick.RemoveListener(_handlers[i]);
                }
            }

            _handlers = Array.Empty<UnityEngine.Events.UnityAction>();
        }

        private void HandleBack()
        {
            SceneLoading.TryLoad(mainMenuSceneName, this);
        }
    }
}
