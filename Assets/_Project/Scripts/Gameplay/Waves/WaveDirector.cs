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

            public WaveRuntime(WaveDto data)
            {
                Data = data;
            }
        }

        [SerializeField] private EnemySpawner enemySpawner;

        private readonly HashSet<EnemyAgent> _aliveEnemies = new HashSet<EnemyAgent>();
        private WaveRuntime[] _waves = Array.Empty<WaveRuntime>();
        private bool _running;
        private bool _completionReported;
        private int _startedWaveCount;
        private int _pendingSpawnCount;
        private int _spawnedCount;
        private float _elapsedTime;

        public event Action StatsChanged;
        public event Action AllWavesCompleted;

        public float ElapsedTime => _elapsedTime;
        public int TotalWaveCount => _waves.Length;
        public int StartedWaveCount => _startedWaveCount;
        public int PendingSpawnCount => _pendingSpawnCount;
        public int SpawnedCount => _spawnedCount;
        public int AliveCount => _aliveEnemies.Count;
        public IEnumerable<EnemyAgent> AliveEnemies => _aliveEnemies;

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

            _waves = new WaveRuntime[schedule.waves.Length];
            for (var index = 0; index < schedule.waves.Length; index++)
            {
                _waves[index] = new WaveRuntime(schedule.waves[index]);
            }

            Array.Sort(_waves, (left, right) => left.Data.startTime.CompareTo(right.Data.startTime));
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
            Debug.Log($"[WaveDirector] WaveStarted id={wave.Data.waveId} time={_elapsedTime:0.00}", this);

            var entries = wave.Data.enemies;
            for (var index = 0; index < entries.Length; index++)
            {
                _pendingSpawnCount += entries[index].count;
                StartCoroutine(SpawnEntry(wave.Data.waveId, entries[index]));
            }
        }

        private IEnumerator SpawnEntry(string waveId, EnemySpawnDto entry)
        {
            for (var index = 0; index < entry.count; index++)
            {
                var enemy = enemySpawner.Spawn(entry.enemyTypeId, waveId);
                _pendingSpawnCount--;

                if (enemy != null)
                {
                    _spawnedCount++;
                    _aliveEnemies.Add(enemy);
                    enemy.Exited += HandleEnemyExited;
                }

                StatsChanged?.Invoke();

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
                _aliveEnemies.Remove(enemy);
            }

            StatsChanged?.Invoke();
            TryCompleteSchedule();
        }

        private void TryCompleteSchedule()
        {
            if (!_running || _completionReported)
            {
                return;
            }

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
                }
            }
        }
    }
}
