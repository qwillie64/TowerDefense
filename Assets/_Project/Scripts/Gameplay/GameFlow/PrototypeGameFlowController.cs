using System;
using System.Collections;
using CyberWizard.Core;
using CyberWizard.Gameplay.Waves;
using CyberWizard.Gameplay.Player;
using CyberWizard.Gameplay.Capture;
using CyberWizard.Infrastructure;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CyberWizard.Gameplay.GameFlow
{
    public sealed class PrototypeGameFlowController : MonoBehaviour
    {
        [SerializeField] private WaveDirector waveDirector;
        [SerializeField] private string[] scheduleRelativePaths =
        {
            "Levels/level_01.json",
            "Levels/level_02.json"
        };
        [SerializeField] private BaseCore baseCore;
        [SerializeField] private PrototypePlayerController player;
        private CaptureZone[] _captureZones = Array.Empty<CaptureZone>();
        private static int _retainedLevelIndex;

        public event Action<GamePhase> PhaseChanged;
        public event Action LevelChanged;

        public GamePhase CurrentPhase { get; private set; } = GamePhase.Starting;
        public string FailureReason { get; private set; } = string.Empty;
        public string DefeatReason { get; private set; } = string.Empty;
        public int SelectedLevelIndex { get; private set; }
        public int LevelCount => scheduleRelativePaths?.Length ?? 0;
        public string SelectedLevelId { get; private set; } = string.Empty;
        public string SelectedLevelName { get; private set; } = string.Empty;
        public string SelectedLevelDifficulty { get; private set; } = string.Empty;
        public string SelectedLevelDescription { get; private set; } = string.Empty;

        public void Configure(
            WaveDirector newWaveDirector,
            string[] relativePaths,
            BaseCore newBaseCore = null,
            PrototypePlayerController newPlayer = null)
        {
            waveDirector = newWaveDirector;
            scheduleRelativePaths = relativePaths;
            baseCore = newBaseCore;
            player = newPlayer;
        }

        private void Start()
        {
            BeginFlow();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape) &&
                (CurrentPhase == GamePhase.Wave || CurrentPhase == GamePhase.Paused))
            {
                TogglePause();
            }
        }

        private void BeginFlow()
        {
            ChangePhase(GamePhase.Starting);

            if (waveDirector == null)
            {
                Fail("PrototypeGameFlowController 缺少 WaveDirector。");
                return;
            }

            if (scheduleRelativePaths == null || scheduleRelativePaths.Length == 0)
            {
                Fail("PrototypeGameFlowController 未配置任何關卡資料。");
                return;
            }

            var initialIndex = HasCommandLineArgument("-level02Smoke")
                ? Mathf.Min(1, scheduleRelativePaths.Length - 1)
                : Mathf.Clamp(_retainedLevelIndex, 0, scheduleRelativePaths.Length - 1);
            if (!TrySelectLevel(initialIndex, out var selectionError))
            {
                Fail(selectionError);
                return;
            }

            waveDirector.AllWavesCompleted += HandleAllWavesCompleted;
            if (baseCore != null) baseCore.Destroyed += HandleBaseDestroyed;
            if (player != null) player.Died += HandlePlayerDied;

            _captureZones = FindObjectsOfType<CaptureZone>();
            SetGameplayEnabled(false);
            ChangePhase(GamePhase.Ready);

            if (HasCommandLineArgument("-smokeTest"))
            {
                StartGame();
            }
        }

        public void StartGame()
        {
            if (CurrentPhase != GamePhase.Ready)
            {
                return;
            }

            SetGameplayEnabled(true);
            ChangePhase(GamePhase.Wave);
            waveDirector.StartSchedule();
        }

        public void SelectNextLevel()
        {
            if (CurrentPhase != GamePhase.Ready || LevelCount < 2)
            {
                return;
            }

            var nextIndex = (SelectedLevelIndex + 1) % LevelCount;
            if (!TrySelectLevel(nextIndex, out var selectionError))
            {
                Fail(selectionError);
            }
        }

        public void Replay()
        {
            if (CurrentPhase != GamePhase.Ended && CurrentPhase != GamePhase.Defeat)
            {
                return;
            }

            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            var activeScene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(activeScene.buildIndex);
        }

        public void TogglePause()
        {
            if (CurrentPhase == GamePhase.Wave)
            {
                SetGameplayEnabled(false);
                ChangePhase(GamePhase.Paused);
                Time.timeScale = 0f;
                return;
            }

            if (CurrentPhase == GamePhase.Paused)
            {
                Time.timeScale = 1f;
                SetGameplayEnabled(true);
                ChangePhase(GamePhase.Wave);
            }
        }

        private void HandleAllWavesCompleted()
        {
            if (CurrentPhase != GamePhase.Wave)
            {
                return;
            }

            ChangePhase(GamePhase.Ended);
            SetGameplayEnabled(false);
            Time.timeScale = 0f;

            if (HasCommandLineArgument("-smokeTest"))
            {
                StartCoroutine(QuitSmokeTestAfterDelay());
            }
        }

        private void HandleBaseDestroyed()
        {
            DefeatReason = "主堡已被摧毀";
            HandleDefeat();
        }

        private void HandlePlayerDied()
        {
            DefeatReason = "玩家已被擊倒";
            HandleDefeat();
        }

        private void HandleDefeat()
        {
            if (CurrentPhase != GamePhase.Wave)
            {
                return;
            }

            ChangePhase(GamePhase.Defeat);
            SetGameplayEnabled(false);
            Time.timeScale = 0f;

            if (HasCommandLineArgument("-smokeTest"))
            {
                StartCoroutine(QuitSmokeTestAfterDelay());
            }
        }

        private void Fail(string reason)
        {
            FailureReason = reason;
            Debug.LogError(reason, this);
            ChangePhase(GamePhase.Failed);
        }

        private bool TrySelectLevel(int index, out string error)
        {
            if (index < 0 || index >= LevelCount)
            {
                error = $"關卡索引超出範圍：{index}。";
                return false;
            }

            var relativePath = scheduleRelativePaths[index];
            if (!LevelScheduleLoader.TryLoadFromStreamingAssets(
                    relativePath,
                    out var schedule,
                    out var loadError))
            {
                error = loadError;
                return false;
            }

            if (!waveDirector.Initialize(schedule, out var initializeError))
            {
                error = initializeError;
                return false;
            }

            SelectedLevelIndex = index;
            SelectedLevelId = schedule.levelId;
            SelectedLevelName = string.IsNullOrWhiteSpace(schedule.displayName)
                ? schedule.levelId
                : schedule.displayName;
            SelectedLevelDifficulty = string.IsNullOrWhiteSpace(schedule.difficulty)
                ? "未標示"
                : schedule.difficulty;
            SelectedLevelDescription = schedule.description ?? string.Empty;
            _retainedLevelIndex = index;
            error = string.Empty;
            Debug.Log($"[PrototypeFlow] LevelSelected id={SelectedLevelId} path={relativePath}", this);
            LevelChanged?.Invoke();
            return true;
        }

        private void SetGameplayEnabled(bool enabled)
        {
            if (player != null)
            {
                player.InputEnabled = enabled;
            }

            for (var index = 0; index < _captureZones.Length; index++)
            {
                if (_captureZones[index] != null && !_captureZones[index].IsCaptured)
                {
                    _captureZones[index].CaptureEnabled = enabled;
                }
            }
        }

        private void ChangePhase(GamePhase phase)
        {
            CurrentPhase = phase;
            Debug.Log($"[PrototypeFlow] Phase={phase}", this);
            PhaseChanged?.Invoke(phase);
        }

        private static bool HasCommandLineArgument(string expected)
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length; index++)
            {
                if (string.Equals(arguments[index], expected, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static IEnumerator QuitSmokeTestAfterDelay()
        {
            yield return new WaitForSecondsRealtime(0.25f);
            Debug.Log("[PrototypeFlow] SmokeTestComplete");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#else
            Application.Quit(0);
#endif
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;
            if (waveDirector != null)
            {
                waveDirector.AllWavesCompleted -= HandleAllWavesCompleted;
            }

            if (baseCore != null) baseCore.Destroyed -= HandleBaseDestroyed;
            if (player != null) player.Died -= HandlePlayerDied;
        }
    }
}
