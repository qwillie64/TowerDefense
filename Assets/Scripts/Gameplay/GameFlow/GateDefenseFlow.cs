using System;
using System.Collections;
using System.Collections.Generic;
using CyberWizard.Core;
using CyberWizard.Data;
using CyberWizard.Gameplay.Enemies;
using CyberWizard.Gameplay.Waves;
using CyberWizard.Infrastructure;
using CyberWizard.Presentation;
using UnityEngine;

namespace CyberWizard.Gameplay.GameFlow
{
    /// <summary>
    /// 閘門防守關卡的流程：讀取波次資料並開始排程；閘門被打破時判定失敗，
    /// 停止生成、讓場上敵人停下，並隱藏閘門物件。
    /// 若有設定跟隨攝影機：波次開始時啟用滑鼠視角（鎖定游標）。
    /// 勝利或失敗的瞬間把遊戲時間放慢（Time.timeScale），玩家仍可操作；元件銷毀時把 timeScale 還原為 1。
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

        [SerializeField, Tooltip("可選：跟隨玩家的攝影機。波次開始時啟用滑鼠視角。")]
        private PrototypeCameraFollow cameraFollow;

        [SerializeField, Range(0.05f, 1f), Tooltip("勝利或失敗瞬間套用的 Time.timeScale（1 表示不放慢）。")]
        private float endTimeScale = 0.25f;

        private readonly List<EnemyAgent> haltBuffer = new List<EnemyAgent>();
        private bool subscribed;
        private bool endSlowMotionApplied;

        public event Action<GamePhase> PhaseChanged;

        public GamePhase CurrentPhase { get; private set; } = GamePhase.Starting;

        public string FailureReason { get; private set; } = string.Empty;

        private IEnumerator Start()
        {
            if (waveDirector == null || gate == null)
            {
                Fail("GateDefenseFlow 缺少 WaveDirector 或閘門設定。");
                yield break;
            }

            LevelWaveScheduleDto schedule = null;
            var error = string.Empty;
            // 手動走訪：本地讀檔在第一次 MoveNext() 就完成，不會延後一幀；WebGL 才會等待網路請求。
            var loading = LevelScheduleLoader.Load(scheduleRelativePath, (loaded, loadError) =>
            {
                schedule = loaded;
                error = loadError;
            });
            while (loading.MoveNext())
            {
                yield return loading.Current;
            }

            if (schedule == null || !waveDirector.Initialize(schedule, out error))
            {
                Fail(error);
                yield break;
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
            ApplyEndSlowMotion();
            ChangePhase(GamePhase.Defeat);
        }

        private void HandleAllWavesCompleted()
        {
            if (CurrentPhase != GamePhase.Wave)
            {
                return;
            }

            Debug.Log("[GateDefenseFlow] Victory：所有波次已清除。", this);
            ApplyEndSlowMotion();
            ChangePhase(GamePhase.Ended);
        }

        /// <summary>
        /// 關卡結束（勝或敗）時放慢遊戲時間，取代原本停用玩家操作的做法。
        /// 物理步長（fixedDeltaTime）刻意不跟著縮小：縮小步長會改變閘門碎片的物理結果；
        /// 慢動作中的平滑度由碎片剛體的插值（GateShatterOnDestroyed）處理。
        /// </summary>
        private void ApplyEndSlowMotion()
        {
            endSlowMotionApplied = true;
            Time.timeScale = endTimeScale;
            Debug.Log($"[GateDefenseFlow] 關卡結束，遊戲時間放慢為 {endTimeScale:0.##} 倍。", this);
        }

        private void RestoreTimeScale()
        {
            if (!endSlowMotionApplied)
            {
                return;
            }

            endSlowMotionApplied = false;
            Time.timeScale = 1f;
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
            // 時間設定是全域的，離開場景時必須還原，避免影響下一個場景。
            RestoreTimeScale();

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
