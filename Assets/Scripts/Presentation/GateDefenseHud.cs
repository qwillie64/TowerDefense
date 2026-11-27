using CyberWizard.Core;
using CyberWizard.Gameplay.GameFlow;
using CyberWizard.Gameplay.Player;
using CyberWizard.Gameplay.Resources;
using CyberWizard.Gameplay.Towers;
using CyberWizard.Gameplay.Waves;
using UnityEngine;

namespace CyberWizard.Presentation
{
    /// <summary>
    /// 閘門防守關卡的簡易 HUD（IMGUI，原型用）：準星、能量、玩家與閘門血量、波次倒數、
    /// 操作說明，以及關卡無法開始時的錯誤訊息（勝敗結果由 LevelResultPanel 顯示）。
    /// 只讀取其他系統的狀態，不修改任何遊戲資料。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GateDefenseHud : MonoBehaviour
    {
        [SerializeField] private GateDefenseFlow flow;
        [SerializeField] private WaveDirector waveDirector;
        [SerializeField] private ResourceWallet wallet;
        [SerializeField] private PrototypePlayerController player;
        [SerializeField] private BaseCore gate;
        [SerializeField, Tooltip("可選：顯示目前可升級的塔。")] private TowerUpgradeInteractor upgradeInteractor;

        [SerializeField, Range(0.01f, 0.05f), Tooltip("字體大小占畫面高度的比例。")]
        private float fontRatio = 0.022f;

        private GUIStyle _panelStyle;
        private GUIStyle _bannerStyle;
        private int _styledHeight;

        private void OnGUI()
        {
            EnsureStyles();
            DrawCrosshair();
            DrawStatusPanel();
            DrawBanner();
        }

        private void EnsureStyles()
        {
            if (_panelStyle != null && _styledHeight == Screen.height)
            {
                return;
            }

            _styledHeight = Screen.height;
            var fontSize = Mathf.Max(12, Mathf.RoundToInt(Screen.height * fontRatio));
            _panelStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = fontSize,
                richText = true,
                wordWrap = false,
                padding = new RectOffset(10, 10, 8, 8)
            };
            _panelStyle.normal.textColor = Color.white;
            _bannerStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = fontSize * 3,
                fontStyle = FontStyle.Bold,
                richText = true
            };
        }

        private void DrawCrosshair()
        {
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                return;
            }

            const float length = 9f;
            const float gap = 3f;
            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            GUI.color = new Color(1f, 1f, 1f, 0.9f);
            var texture = Texture2D.whiteTexture;
            GUI.DrawTexture(new Rect(center.x - gap - length, center.y - 1f, length, 2f), texture);
            GUI.DrawTexture(new Rect(center.x + gap, center.y - 1f, length, 2f), texture);
            GUI.DrawTexture(new Rect(center.x - 1f, center.y - gap - length, 2f, length), texture);
            GUI.DrawTexture(new Rect(center.x - 1f, center.y + gap, 2f, length), texture);
            GUI.color = Color.white;
        }

        private void DrawStatusPanel()
        {
            var energy = wallet != null ? wallet.Balance : 0;
            var playerLine = player == null
                ? "玩家：—"
                : player.IsAlive
                    ? $"玩家血量：{Mathf.CeilToInt(player.CurrentHealth)}/{Mathf.CeilToInt(player.MaxHealth)}"
                    : "玩家血量：0（重生中）";
            var gateLine = gate != null
                ? $"閘門血量：{Mathf.CeilToInt(gate.CurrentHealth)}/{Mathf.CeilToInt(gate.MaxHealth)}"
                : "閘門：—";
            var waveLine = "波次：—";
            if (waveDirector != null && waveDirector.TotalWaveCount > 0)
            {
                waveLine = $"波次：{waveDirector.StartedWaveCount}/{waveDirector.TotalWaveCount}　場上敵人：{waveDirector.AliveCount}";
                if (waveDirector.StartedWaveCount < waveDirector.TotalWaveCount && flow != null &&
                    (flow.CurrentPhase == GamePhase.Wave || flow.CurrentPhase == GamePhase.Paused))
                {
                    waveLine += $"　下一波：{waveDirector.TimeUntilNextWave:0} 秒";
                }
            }

            var nearbyTower = upgradeInteractor != null ? upgradeInteractor.CurrentTower : null;
            var towerLine = nearbyTower != null
                ? $"\n<color=#7fd7ff>可升級：{nearbyTower.TowerNumber}號塔（1 砲塔／2 塔身／3 塔底）</color>"
                : string.Empty;
            var text =
                $"<color=#ffd75e>能量：{energy}</color>\n{playerLine}\n{gateLine}\n{waveLine}{towerLine}\n" +
                "<size=" + Mathf.RoundToInt(_panelStyle.fontSize * 0.8f) + "><color=#c8ccd0>" +
                "WASD 移動｜空白鍵 跳躍｜滑鼠左鍵 攻擊（準星對準敵人）\n靠近塔：1 砲塔、2 塔身、3 塔底 升級</color></size>";
            var content = new GUIContent(text);
            var size = _panelStyle.CalcSize(content);
            GUI.Box(new Rect(12f, 12f, size.x, size.y), content, _panelStyle);
        }

        private void DrawBanner()
        {
            if (flow == null)
            {
                return;
            }

            // 勝利與失敗改由 LevelResultPanel 顯示；這裡只保留關卡無法開始的錯誤訊息。
            if (flow.CurrentPhase != GamePhase.Failed)
            {
                return;
            }

            var message = $"<color=#ff6b6b>關卡無法開始：{flow.FailureReason}</color>";
            GUI.Label(new Rect(0f, Screen.height * 0.3f, Screen.width, Screen.height * 0.2f), message, _bannerStyle);
        }
    }
}
