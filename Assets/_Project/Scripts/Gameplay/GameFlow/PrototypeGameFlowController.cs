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
        [SerializeField] private string scheduleRelativePath = "Levels/level_01.json";
        [SerializeField] private BaseCore baseCore;
        [SerializeField] private PrototypePlayerController player;
        private CaptureZone[] _captureZones = Array.Empty<CaptureZone>();

        public event Action<GamePhase> PhaseChanged;

        public GamePhase CurrentPhase { get; private set; } = GamePhase.Starting;
        public string FailureReason { get; private set; } = string.Empty;

        public void Configure(
            WaveDirector newWaveDirector,
            string relativePath,
            BaseCore newBaseCore = null,
            PrototypePlayerController newPlayer = null)
        {
            waveDirector = newWaveDirector;
            scheduleRelativePath = relativePath;
            baseCore = newBaseCore;
            player = newPlayer;
        }

        private void Start()
        {
            BeginFlow();
        }

        private void BeginFlow()
        {
            ChangePhase(GamePhase.Starting);

            if (waveDirector == null)
            {
                Fail("PrototypeGameFlowController 缺少 WaveDirector。");
                return;
            }

            if (!LevelScheduleLoader.TryLoadFromStreamingAssets(
                    scheduleRelativePath,
                    out var schedule,
                    out var loadError))
            {
                Fail(loadError);
                return;
            }

            if (!waveDirector.Initialize(schedule, out var initializeError))
            {
                Fail(initializeError);
                return;
            }

            waveDirector.AllWavesCompleted += HandleAllWavesCompleted;
            if (baseCore != null) baseCore.Destroyed += HandleDefeat;
            if (player != null) player.Died += HandleDefeat;

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

        public void Replay()
        {
            if (CurrentPhase != GamePhase.Ended && CurrentPhase != GamePhase.Defeat)
            {
                return;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            var activeScene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(activeScene.buildIndex);
        }

        private void HandleAllWavesCompleted()
        {
            if (CurrentPhase != GamePhase.Wave)
            {
                return;
            }

            ChangePhase(GamePhase.Ended);
            SetGameplayEnabled(false);

            if (HasCommandLineArgument("-smokeTest"))
            {
                StartCoroutine(QuitSmokeTestAfterDelay());
            }
        }

        private void HandleDefeat()
        {
            if (CurrentPhase != GamePhase.Wave)
            {
                return;
            }

            ChangePhase(GamePhase.Defeat);
            SetGameplayEnabled(false);

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
            yield return new WaitForSeconds(0.25f);
            Debug.Log("[PrototypeFlow] SmokeTestComplete");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#else
            Application.Quit(0);
#endif
        }

        private void OnDestroy()
        {
            if (waveDirector != null)
            {
                waveDirector.AllWavesCompleted -= HandleAllWavesCompleted;
            }

            if (baseCore != null) baseCore.Destroyed -= HandleDefeat;
            if (player != null) player.Died -= HandleDefeat;
        }
    }
}
