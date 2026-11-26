using System;
using System.Collections;
using System.Collections.Generic;
using CyberWizard.Data;
using CyberWizard.Gameplay.Enemies;
using UnityEngine;

namespace CyberWizard.Gameplay.Waves
{
    public sealed class WaveDirector : MonoBehaviour
    {
        private sealed class WaveRuntime
        {
            public readonly WaveDto Data;
            public bool Started;
            public bool Completed;
            public int PendingSpawnCount;

            public WaveRuntime(WaveDto data)
            {
                Data = data;
            }
        }

        [SerializeField] private EnemySpawner enemySpawner;

        private readonly HashSet<EnemyAgent> _aliveEnemies = new HashSet<EnemyAgent>();
        private WaveRuntime[] _waves = Array.Empty<WaveRuntime>();
        private bool _running;
        private bool _stopped;
        private bool _completionReported;
        private int _startedWaveCount;
        private int _pendingSpawnCount;
        private int _spawnedCount;
        private float _elapsedTime;

        public event Action StatsChanged;
        public event Action AllWavesCompleted;
        public event Action<string, int> WaveBegan;
        public event Action<string, int> WaveCleared;
        public event Action<EnemyAgent> EnemyDefeated;

        public float ElapsedTime => _elapsedTime;
        public int TotalWaveCount => _waves.Length;
        public int StartedWaveCount => _startedWaveCount;
        public int PendingSpawnCount => _pendingSpawnCount;
        public int SpawnedCount => _spawnedCount;
        public int AliveCount => _aliveEnemies.Count;
        public IEnumerable<EnemyAgent> AliveEnemies => _aliveEnemies;
        public EnemyAgent ActiveBoss { get; private set; }

        public float TimeUntilNextWave
        {
            get
            {
                for (var index = 0; index < _waves.Length; index++)
                {
                    if (!_waves[index].Started)
                    {
                        return Mathf.Max(0f, _waves[index].Data.startTime - _elapsedTime);
                    }
                }

                return 0f;
            }
        }

        /// <summary>
        /// 取得下一個尚未開始的波次（依開始時間排序）。排程被 StopSchedule 中止、
        /// 尚未初始化或所有波次都已開始時回傳 false。
        /// </summary>
        public bool TryGetUpcomingWave(out WaveDto wave)
        {
            wave = null;
            if (_stopped)
            {
                return false;
            }

            for (var index = 0; index < _waves.Length; index++)
            {
                if (!_waves[index].Started)
                {
                    wave = _waves[index].Data;
                    return true;
                }
            }

            return false;
        }

        public void Configure(EnemySpawner newEnemySpawner)
        {
            enemySpawner = newEnemySpawner;
        }

        public bool Initialize(LevelWaveScheduleDto schedule, out string error)
        {
            if (enemySpawner == null)
            {
                error = "WaveDirector 缺少 EnemySpawner。";
                return false;
            }

            if (schedule == null || schedule.waves == null || schedule.waves.Length == 0)
            {
                error = "WaveDirector 收到空的波次資料。";
                return false;
            }

            for (var waveIndex = 0; waveIndex < schedule.waves.Length; waveIndex++)
            {
                var entries = schedule.waves[waveIndex].enemies;
                for (var entryIndex = 0; entryIndex < entries.Length; entryIndex++)
                {
                    var entry = entries[entryIndex];
                    if (!enemySpawner.HasDefinition(entry.enemyTypeId))
                    {
                        error = $"波次 {schedule.waves[waveIndex].waveId} 使用未知敵人：{entry.enemyTypeId}。";
                        return false;
                    }

                    if (!enemySpawner.HasSpawnPoint(entry.spawnPointId))
                    {
                        error = $"波次 {schedule.waves[waveIndex].waveId} 使用未知出生點：{entry.spawnPointId}。";
                        return false;
                    }

                    if (!enemySpawner.HasPath(entry.pathId))
                    {
                        error = $"波次 {schedule.waves[waveIndex].waveId} 使用未知道路：{entry.pathId}。";
                        return false;
                    }
                }
            }

            _waves = new WaveRuntime[schedule.waves.Length];
            for (var index = 0; index < schedule.waves.Length; index++)
            {
                _waves[index] = new WaveRuntime(schedule.waves[index]);
            }

            Array.Sort(_waves, (left, right) => left.Data.startTime.CompareTo(right.Data.startTime));
            _running = false;
            _stopped = false;
            _completionReported = false;
            _startedWaveCount = 0;
            _pendingSpawnCount = 0;
            _spawnedCount = 0;
            _elapsedTime = 0f;
            ActiveBoss = null;
            StatsChanged?.Invoke();
            error = string.Empty;
            return true;
        }

        public void StartSchedule()
        {
            _elapsedTime = 0f;
            _startedWaveCount = 0;
            _pendingSpawnCount = 0;
            _spawnedCount = 0;
            _completionReported = false;
            _running = true;
            _stopped = false;
            StatsChanged?.Invoke();
        }

        /// <summary>
        /// 立即停止排程：不再開始新波次，也中止尚未生成完的梯隊。已生成的敵人不受影響。
        /// </summary>
        public void StopSchedule()
        {
            _running = false;
            _stopped = true;
            StopAllCoroutines();
            Debug.Log($"[WaveDirector] ScheduleStopped time={_elapsedTime:0.00}", this);
            StatsChanged?.Invoke();
        }

        private void Update()
        {
            if (!_running)
            {
                return;
            }

            _elapsedTime += Time.deltaTime;

            for (var index = 0; index < _waves.Length; index++)
            {
                var wave = _waves[index];
                if (!wave.Started && _elapsedTime >= wave.Data.startTime)
                {
                    BeginWave(wave);
                }
            }

            TryCompleteSchedule();
            StatsChanged?.Invoke();
        }

        private void BeginWave(WaveRuntime wave)
        {
            wave.Started = true;
            _startedWaveCount++;
            var entries = wave.Data.enemies;
            for (var index = 0; index < entries.Length; index++)
            {
                wave.PendingSpawnCount += entries[index].count;
            }
            Debug.Log($"[WaveDirector] WaveStarted id={wave.Data.waveId} time={_elapsedTime:0.00}", this);
            WaveBegan?.Invoke(wave.Data.waveId, _startedWaveCount);

            for (var index = 0; index < entries.Length; index++)
            {
                _pendingSpawnCount += entries[index].count;
                StartCoroutine(SpawnEntry(wave, entries[index]));
            }
        }

        private IEnumerator SpawnEntry(WaveRuntime wave, EnemySpawnDto entry)
        {
            for (var index = 0; index < entry.count; index++)
            {
                var enemy = enemySpawner.Spawn(
                    entry.enemyTypeId,
                    wave.Data.waveId,
                    entry.spawnPointId,
                    entry.pathId,
                    entry.objective);
                _pendingSpawnCount--;
                wave.PendingSpawnCount--;

                if (enemy != null)
                {
                    _spawnedCount++;
                    _aliveEnemies.Add(enemy);
                    enemy.Exited += HandleEnemyExited;
                    enemy.Killed += HandleEnemyKilled;
                    if (enemy.IsBoss)
                    {
                        ActiveBoss = enemy;
                        Debug.Log($"[WaveDirector] BossSpawned type={enemy.EnemyTypeId}", this);
                    }
                }

                StatsChanged?.Invoke();
                TryCompleteWaves();

                if (index < entry.count - 1 && entry.spawnInterval > 0f)
                {
                    yield return new WaitForSeconds(entry.spawnInterval);
                }
            }

            TryCompleteSchedule();
        }

        private void HandleEnemyExited(EnemyAgent enemy)
        {
            if (enemy != null)
            {
                enemy.Exited -= HandleEnemyExited;
                enemy.Killed -= HandleEnemyKilled;
                _aliveEnemies.Remove(enemy);
                if (enemy == ActiveBoss)
                {
                    ActiveBoss = null;
                }
            }

            StatsChanged?.Invoke();
            TryCompleteWaves();
            TryCompleteSchedule();
        }

        private void HandleEnemyKilled(EnemyAgent enemy)
        {
            if (enemy != null)
            {
                EnemyDefeated?.Invoke(enemy);
            }
        }

        private void TryCompleteWaves()
        {
            for (var waveIndex = 0; waveIndex < _waves.Length; waveIndex++)
            {
                var wave = _waves[waveIndex];
                if (!wave.Started || wave.Completed || wave.PendingSpawnCount > 0)
                {
                    continue;
                }

                var hasLivingEnemy = false;
                foreach (var enemy in _aliveEnemies)
                {
                    if (enemy != null && enemy.IsAlive && enemy.WaveId == wave.Data.waveId)
                    {
                        hasLivingEnemy = true;
                        break;
                    }
                }

                if (hasLivingEnemy)
                {
                    continue;
                }

                wave.Completed = true;
                Debug.Log($"[WaveDirector] WaveCompleted id={wave.Data.waveId} time={_elapsedTime:0.00}", this);
                WaveCleared?.Invoke(wave.Data.waveId, waveIndex + 1);
            }
        }

        private void TryCompleteSchedule()
        {
            if (!_running || _completionReported)
            {
                return;
            }

            TryCompleteWaves();
            if (_startedWaveCount < _waves.Length || _pendingSpawnCount > 0 || _aliveEnemies.Count > 0)
            {
                return;
            }

            _completionReported = true;
            _running = false;
            Debug.Log($"[WaveDirector] AllWavesCompleted spawned={_spawnedCount} time={_elapsedTime:0.00}", this);
            StatsChanged?.Invoke();
            AllWavesCompleted?.Invoke();
        }

        private void OnDestroy()
        {
            foreach (var enemy in _aliveEnemies)
            {
                if (enemy != null)
                {
                    enemy.Exited -= HandleEnemyExited;
                    enemy.Killed -= HandleEnemyKilled;
                }
            }
        }
    }
}
