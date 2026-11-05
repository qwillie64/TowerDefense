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
        private Label _bossLabel;
        private Button _startButton;
        private Button _replayButton;
        private Button _pauseButton;
        private Button _neutralToggleButton;
        private string _waveNotice = string.Empty;
        private float _waveNoticeUntil;

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
            _bossLabel = root.Q<Label>("boss-label");
            _startButton = root.Q<Button>("start-button");
            _replayButton = root.Q<Button>("replay-button");
            _pauseButton = root.Q<Button>("pause-button");
            _neutralToggleButton = root.Q<Button>("neutral-toggle-button");
        }

        private void Start()
        {
            if (gameFlow != null) gameFlow.PhaseChanged += HandlePhaseChanged;
            if (waveDirector != null) waveDirector.StatsChanged += Refresh;
            if (waveDirector != null) waveDirector.WaveBegan += HandleWaveBegan;
            if (waveDirector != null) waveDirector.WaveCleared += HandleWaveCleared;
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
            if (_pauseButton != null) _pauseButton.clicked += HandlePauseClicked;
            if (_neutralToggleButton != null) _neutralToggleButton.clicked += HandleNeutralToggleClicked;

            Refresh();
        }

        private void Update()
        {
            RefreshTimer();
            RefreshDynamicGameplayStatus();
        }

        private void HandlePhaseChanged(GamePhase phase)
        {
            if ((phase == GamePhase.Paused || phase == GamePhase.Ended ||
                 phase == GamePhase.Defeat || phase == GamePhase.Failed) &&
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
        private void HandlePauseClicked() => gameFlow?.TogglePause();
        private void HandleNeutralToggleClicked()
        {
            if (neutralModule == null || gameFlow.CurrentPhase != GamePhase.Ready)
            {
                return;
            }

            neutralModule.SetFeatureEnabled(!neutralModule.FeatureEnabled);
            Refresh();
        }

        private void HandleWaveBegan(string waveId, int waveNumber)
        {
            _waveNotice = waveId == "wave_boss"
                ? "警告：關卡王已出現"
                : $"第 {waveNumber} 波開始";
            _waveNoticeUntil = Time.unscaledTime + 2.5f;
            Refresh();
        }

        private void HandleWaveCleared(string waveId, int waveNumber)
        {
            if (waveNumber >= waveDirector.TotalWaveCount)
            {
                return;
            }

            _waveNotice = $"第 {waveNumber} 波完成，進入整備時間";
            _waveNoticeUntil = Time.unscaledTime + 3f;
            Refresh();
        }

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
            if (_bossLabel != null)
            {
                var boss = waveDirector.ActiveBoss;
                _bossLabel.style.display = boss != null && boss.IsAlive
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
                if (boss != null)
                    _bossLabel.text = $"關卡王：{boss.CurrentHealth:0}/{boss.MaxHealth:0}";
            }

            if (_startButton != null)
                _startButton.style.display = gameFlow.CurrentPhase == GamePhase.Ready
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            if (_replayButton != null)
                _replayButton.style.display =
                    gameFlow.CurrentPhase == GamePhase.Ended || gameFlow.CurrentPhase == GamePhase.Defeat
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
            if (_pauseButton != null)
            {
                var canPause = gameFlow.CurrentPhase == GamePhase.Wave ||
                               gameFlow.CurrentPhase == GamePhase.Paused;
                _pauseButton.style.display = canPause ? DisplayStyle.Flex : DisplayStyle.None;
                _pauseButton.text = gameFlow.CurrentPhase == GamePhase.Paused ? "繼續遊戲" : "暫停遊戲";
            }
            if (_neutralToggleButton != null)
            {
                _neutralToggleButton.style.display = gameFlow.CurrentPhase == GamePhase.Ready
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
                _neutralToggleButton.text = neutralModule != null && neutralModule.FeatureEnabled
                    ? "中立據點模組：開啟"
                    : "中立據點模組：關閉";
            }

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
                        if (Time.unscaledTime < _waveNoticeUntil)
                            _messageLabel.text = _waveNotice;
                        else
                            _messageLabel.text = waveDirector.StartedWaveCount == 0
                                ? "準備防線並等待第一波敵人"
                                : "防守主堡，擊退所有敵人";
                        break;
                    case GamePhase.Paused:
                        _messageLabel.text = "遊戲已暫停，按 Esc 或繼續遊戲恢復";
                        break;
                    case GamePhase.Ended:
                        _messageLabel.text =
                            $"勝利：{waveDirector.TotalWaveCount} 波已清除｜" +
                            $"時間 {waveDirector.ElapsedTime:0.0} 秒｜" +
                            $"主堡 {baseCore.CurrentHealth:0}/{baseCore.MaxHealth:0}";
                        break;
                    case GamePhase.Defeat:
                        _messageLabel.text = $"失敗：{gameFlow.DefeatReason}";
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
                    _neutralLabel.text = $"中立據點：敵方控制　紅軍 {neutralModule.Outpost.SoldierCount}";
                else
                    _neutralLabel.text =
                        $"中立據點佔領：{neutralModule.Outpost.CaptureZone.NormalizedProgress * 100f:0}%";
            }
        }

        private void RefreshTimer()
        {
            if (_timerLabel == null || waveDirector == null || gameFlow == null)
                return;

            if (gameFlow.CurrentPhase == GamePhase.Paused)
                _timerLabel.text = "關卡已暫停";
            else if (gameFlow.CurrentPhase == GamePhase.Wave &&
                     waveDirector.StartedWaveCount < waveDirector.TotalWaveCount)
                _timerLabel.text = $"下一波倒數：{waveDirector.TimeUntilNextWave:0.0} 秒";
            else
                _timerLabel.text = $"關卡時間：{waveDirector.ElapsedTime:0.0} 秒";
        }

        private static string GetPhaseText(GamePhase phase)
        {
            switch (phase)
            {
                case GamePhase.Starting: return "開始";
                case GamePhase.Ready: return "等待開始";
                case GamePhase.Wave: return "防守中";
                case GamePhase.Paused: return "暫停";
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
            if (waveDirector != null) waveDirector.WaveBegan -= HandleWaveBegan;
            if (waveDirector != null) waveDirector.WaveCleared -= HandleWaveCleared;
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
            if (_pauseButton != null) _pauseButton.clicked -= HandlePauseClicked;
            if (_neutralToggleButton != null) _neutralToggleButton.clicked -= HandleNeutralToggleClicked;
        }
    }
}
