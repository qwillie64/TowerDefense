using System;
using System.Collections.Generic;
using CyberWizard.Core;
using CyberWizard.Gameplay.Enemies;
using CyberWizard.Gameplay.Player;
using CyberWizard.Gameplay.Waves;
using CyberWizard.Infrastructure;
using CyberWizard.Presentation;
using UnityEngine;

namespace CyberWizard.Gameplay.GameFlow
{
    /// <summary>
    /// 閘門防守關卡的流程：讀取波次資料並開始排程；閘門被打破時判定失敗，
    /// 停止生成、讓場上敵人停下，並隱藏閘門物件。
    /// 若有設定玩家與跟隨攝影機：波次進行中啟用滑鼠視角（鎖定游標），結束後停用玩家操作並釋放游標。
    /// 本元件不含 UI，結果透過 PhaseChanged 事件與 Console 紀錄提供。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GateDefenseFlow : MonoBehaviour
    {
        [SerializeField, Tooltip("StreamingAssets 底下的波次 JSON 路徑。")]
        private string scheduleRelativePath = "Levels/level1_rust_alley.json";

        [SerializeField]
        private WaveDirector waveDirector;

        [SerializeField, Tooltip("敵人要攻擊的閘門。")]
        private BaseCore gate;

        [SerializeField, Tooltip("閘門被打破時要隱藏的物件。")]
        private GameObject[] hideWhenGateDestroyed = Array.Empty<GameObject>();

        [SerializeField, Tooltip("進入 Play Mode 後自動開始波次。")]
        private bool startOnPlay = true;

        [SerializeField, Tooltip("可選：玩家。關卡結束（勝或敗）後停用操作。")]
        private PrototypePlayerController player;

        [SerializeField, Tooltip("可選：跟隨玩家的攝影機。波次進行中啟用滑鼠視角。")]
        private PrototypeCameraFollow cameraFollow;

        private readonly List<EnemyAgent> haltBuffer = new List<EnemyAgent>();
        private bool subscribed;

        public event Action<GamePhase> PhaseChanged;

        public GamePhase CurrentPhase { get; private set; } = GamePhase.Starting;

        public string FailureReason { get; private set; } = string.Empty;

        private void Start()
        {
            if (waveDirector == null || gate == null)
            {
                Fail("GateDefenseFlow 缺少 WaveDirector 或閘門設定。");
                return;
            }

            if (!LevelScheduleLoader.TryLoadFromStreamingAssets(scheduleRelativePath, out var schedule, out var error) ||
                !waveDirector.Initialize(schedule, out error))
            {
                Fail(error);
                return;
            }

            waveDirector.AllWavesCompleted += HandleAllWavesCompleted;
            gate.Destroyed += HandleGateDestroyed;
            subscribed = true;
            ChangePhase(GamePhase.Ready);

            if (startOnPlay)
            {
                StartLevel();
            }
        }

        /// <summary>開始波次排程；只有在 Ready 狀態下有效。</summary>
        public void StartLevel()
        {
            if (CurrentPhase != GamePhase.Ready)
            {
                return;
            }

            waveDirector.StartSchedule();
            if (cameraFollow != null)
            {
                cameraFollow.SetGameplayActive(true);
            }

            ChangePhase(GamePhase.Wave);
        }

        private void HandleGateDestroyed()
        {
            if (CurrentPhase != GamePhase.Wave)
            {
                return;
            }

            waveDirector.StopSchedule();

            // 先複製，避免在走訪集合時有敵人離場而修改集合。
            haltBuffer.Clear();
            haltBuffer.AddRange(waveDirector.AliveEnemies);
            foreach (var enemy in haltBuffer)
            {
                if (enemy != null)
                {
                    enemy.Halt();
                }
            }

            haltBuffer.Clear();

            foreach (var target in hideWhenGateDestroyed)
            {
                if (target != null)
                {
                    target.SetActive(false);
                }
            }

            FailureReason = "黃銅閘門已被打破";
            Debug.Log($"[GateDefenseFlow] Defeat：{FailureReason}，經過 {waveDirector.ElapsedTime:0.0} 秒。", this);
            EndPlayerControl();
            ChangePhase(GamePhase.Defeat);
        }

        private void HandleAllWavesCompleted()
        {
            if (CurrentPhase != GamePhase.Wave)
            {
                return;
            }

            Debug.Log("[GateDefenseFlow] Victory：所有波次已清除。", this);
            EndPlayerControl();
            ChangePhase(GamePhase.Ended);
        }

        private void EndPlayerControl()
        {
            if (player != null)
            {
                player.InputEnabled = false;
            }

            if (cameraFollow != null)
            {
                cameraFollow.SetGameplayActive(false);
            }
        }

        private void Fail(string error)
        {
            FailureReason = error;
            Debug.LogError($"[GateDefenseFlow] 無法開始關卡：{error}", this);
            ChangePhase(GamePhase.Failed);
        }

        private void ChangePhase(GamePhase phase)
        {
            CurrentPhase = phase;
            PhaseChanged?.Invoke(phase);
        }

        private void OnDestroy()
        {
            if (!subscribed)
            {
                return;
            }

            if (waveDirector != null)
            {
                waveDirector.AllWavesCompleted -= HandleAllWavesCompleted;
            }

            if (gate != null)
            {
                gate.Destroyed -= HandleGateDestroyed;
            }
        }
    }
}
