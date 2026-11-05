using CyberWizard.Core;
using CyberWizard.Gameplay.Building;
using CyberWizard.Gameplay.Capture;
using CyberWizard.Gameplay.Combo;
using CyberWizard.Gameplay.GameFlow;
using CyberWizard.Gameplay.Neutral;
using CyberWizard.Gameplay.Player;
using CyberWizard.Gameplay.Resources;
using CyberWizard.Gameplay.Towers;
using CyberWizard.Gameplay.Waves;
using UnityEngine;
using UnityEngine.UIElements;

namespace CyberWizard.Presentation
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class PrototypeHudController : MonoBehaviour
    {
        [SerializeField] private PrototypeGameFlowController gameFlow;
        [SerializeField] private WaveDirector waveDirector;
        [SerializeField] private PrototypePlayerController player;
        [SerializeField] private BaseCore baseCore;
        [SerializeField] private ResourceWallet wallet;
        [SerializeField] private TerritoryController territory;
        [SerializeField] private TowerRegistry towerRegistry;
        [SerializeField] private PrototypeBuildController buildController;
        [SerializeField] private PrototypeComboSystem comboSystem;
        [SerializeField] private NeutralOutpostModule neutralModule;
        [SerializeField] private PrototypeCameraFollow cameraController;

        private Label _phaseLabel;
        private Label _timerLabel;
        private Label _statsLabel;
        private Label _playerLabel;
        private Label _baseLabel;
        private Label _resourceLabel;
        private Label _captureLabel;
        private Label _buildLabel;
        private Label _comboLabel;
        private Label _neutralLabel;
        private Label _messageLabel;
        private Label _cameraLabel;
        private Button _startButton;
        private Button _replayButton;

        public void Configure(
            PrototypeGameFlowController newGameFlow,
            WaveDirector newWaveDirector,
            PrototypePlayerController newPlayer = null,
            BaseCore newBaseCore = null,
            ResourceWallet newWallet = null,
            TerritoryController newTerritory = null,
            TowerRegistry newTowerRegistry = null,
            PrototypeBuildController newBuildController = null,
            PrototypeComboSystem newComboSystem = null,
            NeutralOutpostModule newNeutralModule = null,
            PrototypeCameraFollow newCameraController = null)
        {
            gameFlow = newGameFlow;
            waveDirector = newWaveDirector;
            player = newPlayer;
            baseCore = newBaseCore;
            wallet = newWallet;
            territory = newTerritory;
            towerRegistry = newTowerRegistry;
            buildController = newBuildController;
            comboSystem = newComboSystem;
            neutralModule = newNeutralModule;
            cameraController = newCameraController;
        }

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            _phaseLabel = root.Q<Label>("phase-label");
            _timerLabel = root.Q<Label>("timer-label");
            _statsLabel = root.Q<Label>("stats-label");
            _playerLabel = root.Q<Label>("player-label");
            _baseLabel = root.Q<Label>("base-label");
            _resourceLabel = root.Q<Label>("resource-label");
            _captureLabel = root.Q<Label>("capture-label");
            _buildLabel = root.Q<Label>("build-label");
            _comboLabel = root.Q<Label>("combo-label");
            _neutralLabel = root.Q<Label>("neutral-label");
            _messageLabel = root.Q<Label>("message-label");
            _cameraLabel = root.Q<Label>("camera-label");
            _startButton = root.Q<Button>("start-button");
            _replayButton = root.Q<Button>("replay-button");
        }

        private void Start()
        {
            if (gameFlow != null) gameFlow.PhaseChanged += HandlePhaseChanged;
            if (waveDirector != null) waveDirector.StatsChanged += Refresh;
            if (player != null) player.HealthChanged += HandleHealthChanged;
            if (baseCore != null) baseCore.HealthChanged += HandleHealthChanged;
            if (wallet != null) wallet.Changed += HandleIntegerChanged;
            if (territory != null && territory.CaptureZone != null)
                territory.CaptureZone.ProgressChanged += HandleFloatChanged;
            if (towerRegistry != null) towerRegistry.Changed += Refresh;
            if (buildController != null) buildController.Changed += Refresh;
            if (comboSystem != null) comboSystem.Changed += Refresh;
            if (neutralModule != null) neutralModule.Changed += Refresh;
            if (neutralModule != null && neutralModule.Outpost != null)
                neutralModule.Outpost.Changed += Refresh;
            if (cameraController != null) cameraController.ModeChanged += HandleCameraModeChanged;
            if (_startButton != null) _startButton.clicked += HandleStartClicked;
            if (_replayButton != null) _replayButton.clicked += HandleReplayClicked;

            Refresh();
        }

        private void Update()
        {
            RefreshTimer();
            RefreshDynamicGameplayStatus();
        }

        private void HandlePhaseChanged(GamePhase phase)
        {
            if ((phase == GamePhase.Ended || phase == GamePhase.Defeat || phase == GamePhase.Failed) &&
                cameraController != null)
            {
                cameraController.SetMode(PrototypeCameraMode.Overview);
            }

            Refresh();
        }
        private void HandleHealthChanged(float current, float maximum) => Refresh();
        private void HandleIntegerChanged(int value) => Refresh();
        private void HandleFloatChanged(float value) => Refresh();
        private void HandleCameraModeChanged(PrototypeCameraMode mode) => Refresh();
        private void HandleStartClicked() => gameFlow?.StartGame();
        private void HandleReplayClicked() => gameFlow?.Replay();

        private void Refresh()
        {
            if (gameFlow == null || waveDirector == null)
                return;

            if (_phaseLabel != null)
                _phaseLabel.text = $"流程：{GetPhaseText(gameFlow.CurrentPhase)}";
            if (_statsLabel != null)
                _statsLabel.text =
                    $"波次 {waveDirector.StartedWaveCount}/{waveDirector.TotalWaveCount}　" +
                    $"已生成 {waveDirector.SpawnedCount}　存活 {waveDirector.AliveCount}";
            if (_playerLabel != null && player != null)
                _playerLabel.text = $"玩家生命：{player.CurrentHealth:0}/{player.MaxHealth:0}";
            if (_baseLabel != null && baseCore != null)
                _baseLabel.text = $"主堡生命：{baseCore.CurrentHealth:0}/{baseCore.MaxHealth:0}";
            if (_resourceLabel != null && wallet != null && towerRegistry != null)
                _resourceLabel.text =
                    $"資源：{wallet.Balance}　防禦塔：{towerRegistry.Count}/{towerRegistry.TowerLimit}";
            if (_cameraLabel != null && cameraController != null)
                _cameraLabel.text = cameraController.CurrentMode == PrototypeCameraMode.Overview
                    ? "視角：戰術俯視（Tab 切換）"
                    : "視角：第三人稱肩後（滑鼠控制）";

            if (_startButton != null)
                _startButton.style.display = gameFlow.CurrentPhase == GamePhase.Ready
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            if (_replayButton != null)
                _replayButton.style.display =
                    gameFlow.CurrentPhase == GamePhase.Ended || gameFlow.CurrentPhase == GamePhase.Defeat
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;

            RefreshDynamicGameplayStatus();

            if (_messageLabel != null)
            {
                switch (gameFlow.CurrentPhase)
                {
                    case GamePhase.Starting:
                        _messageLabel.text = "正在載入關卡資料";
                        break;
                    case GamePhase.Ready:
                        _messageLabel.text = "按下開始遊戲進入關卡";
                        break;
                    case GamePhase.Wave:
                        _messageLabel.text = waveDirector.StartedWaveCount == 0
                            ? "準備防線並等待第一波敵人"
                            : "防守主堡，擊退所有敵人";
                        break;
                    case GamePhase.Ended:
                        _messageLabel.text = "勝利：所有波次已清除";
                        break;
                    case GamePhase.Defeat:
                        _messageLabel.text = "失敗：玩家或主堡已被擊破";
                        break;
                    case GamePhase.Failed:
                        _messageLabel.text = gameFlow.FailureReason;
                        break;
                }
            }

            RefreshTimer();
        }

        private void RefreshDynamicGameplayStatus()
        {
            if (_captureLabel != null && territory != null)
                _captureLabel.text = territory.IsCaptured
                    ? "建造區域：已佔領"
                    : $"建造區域佔領：{territory.CaptureZone.NormalizedProgress * 100f:0}%";
            if (_buildLabel != null && buildController != null)
                _buildLabel.text = $"建造：{buildController.StatusMessage}";
            if (_comboLabel != null && comboSystem != null)
                _comboLabel.text = $"連攜：{comboSystem.StatusMessage}";
            if (_neutralLabel != null && neutralModule != null)
            {
                if (!neutralModule.FeatureEnabled)
                    _neutralLabel.text = "中立據點模組：關閉";
                else if (neutralModule.Outpost == null)
                    _neutralLabel.text = "中立據點：未配置";
                else if (neutralModule.Outpost.IsAllied)
                    _neutralLabel.text = $"中立據點：已投靠　士兵 {neutralModule.Outpost.SoldierCount}";
                else if (neutralModule.Outpost.IsEnemyControlled)
                    _neutralLabel.text = "中立據點：已被敵方佔領";
                else
                    _neutralLabel.text =
                        $"中立據點佔領：{neutralModule.Outpost.CaptureZone.NormalizedProgress * 100f:0}%";
            }
        }

        private void RefreshTimer()
        {
            if (_timerLabel == null || waveDirector == null || gameFlow == null)
                return;

            _timerLabel.text = gameFlow.CurrentPhase == GamePhase.Wave && waveDirector.StartedWaveCount == 0
                ? $"第一波倒數：{waveDirector.TimeUntilNextWave:0.0} 秒"
                : $"關卡時間：{waveDirector.ElapsedTime:0.0} 秒";
        }

        private static string GetPhaseText(GamePhase phase)
        {
            switch (phase)
            {
                case GamePhase.Starting: return "開始";
                case GamePhase.Ready: return "等待開始";
                case GamePhase.Wave: return "防守中";
                case GamePhase.Ended: return "勝利";
                case GamePhase.Defeat: return "失敗";
                case GamePhase.Failed: return "載入失敗";
                default: return phase.ToString();
            }
        }

        private void OnDisable()
        {
            if (gameFlow != null) gameFlow.PhaseChanged -= HandlePhaseChanged;
            if (waveDirector != null) waveDirector.StatsChanged -= Refresh;
            if (player != null) player.HealthChanged -= HandleHealthChanged;
            if (baseCore != null) baseCore.HealthChanged -= HandleHealthChanged;
            if (wallet != null) wallet.Changed -= HandleIntegerChanged;
            if (territory != null && territory.CaptureZone != null)
                territory.CaptureZone.ProgressChanged -= HandleFloatChanged;
            if (towerRegistry != null) towerRegistry.Changed -= Refresh;
            if (buildController != null) buildController.Changed -= Refresh;
            if (comboSystem != null) comboSystem.Changed -= Refresh;
            if (neutralModule != null) neutralModule.Changed -= Refresh;
            if (neutralModule != null && neutralModule.Outpost != null)
                neutralModule.Outpost.Changed -= Refresh;
            if (cameraController != null) cameraController.ModeChanged -= HandleCameraModeChanged;
            if (_startButton != null) _startButton.clicked -= HandleStartClicked;
            if (_replayButton != null) _replayButton.clicked -= HandleReplayClicked;
        }
    }
}
