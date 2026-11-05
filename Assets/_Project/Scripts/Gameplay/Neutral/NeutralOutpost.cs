using System;
using System.Collections;
using System.Collections.Generic;
using CyberWizard.Gameplay.Capture;
using CyberWizard.Gameplay.Waves;
using UnityEngine;
using CyberWizard.Presentation;
using CyberWizard.Gameplay.Player;
using CyberWizard.Gameplay.Towers;
using CyberWizard.Gameplay.GameFlow;
using CyberWizard.Core;

namespace CyberWizard.Gameplay.Neutral
{
    public sealed class NeutralOutpost : MonoBehaviour
    {
        [SerializeField] private CaptureZone captureZone;
        [SerializeField] private Transform soldierSpawnPoint;
        [SerializeField] private WaveDirector waveDirector;
        [SerializeField, Min(1f)] private float productionInterval = 5f;
        [SerializeField, Min(1)] private int soldierLimit = 3;
        [SerializeField] private PrototypePlayerController player;
        [SerializeField] private TowerRegistry towerRegistry;
        [SerializeField] private BaseCore baseCore;
        [SerializeField] private SoldierRegistry soldierRegistry;

        private readonly List<SoldierAgent> _soldiers = new List<SoldierAgent>();
        private Coroutine _productionRoutine;
        private WorldHealthBar _captureBar;

        public event Action Changed;

        public bool IsAllied { get; private set; }
        public bool IsEnemyControlled { get; private set; }
        public int SoldierCount
        {
            get
            {
                _soldiers.RemoveAll(soldier => soldier == null);
                return _soldiers.Count;
            }
        }
        public CaptureZone CaptureZone => captureZone;

        public void Configure(
            CaptureZone zone,
            Transform spawnPoint,
            WaveDirector director,
            PrototypePlayerController newPlayer = null,
            TowerRegistry newTowerRegistry = null,
            BaseCore newBaseCore = null,
            SoldierRegistry newSoldierRegistry = null)
        {
            captureZone = zone;
            soldierSpawnPoint = spawnPoint;
            waveDirector = director;
            player = newPlayer;
            towerRegistry = newTowerRegistry;
            baseCore = newBaseCore;
            soldierRegistry = newSoldierRegistry;
        }

        private void OnEnable()
        {
            if (captureZone != null)
            {
                captureZone.Captured += HandleCaptured;
                captureZone.ProgressChanged += HandleProgressChanged;
            }
        }

        private void Start()
        {
            _captureBar = WorldHealthBar.Create(
                transform,
                new Vector3(0f, 1.8f, 0f),
                PrototypeMaterialRole.Neutral,
                2.2f);
            _captureBar.SetValue(captureZone != null ? captureZone.NormalizedProgress : 0f, 1f);
        }

        private void OnDisable()
        {
            if (captureZone != null)
            {
                captureZone.Captured -= HandleCaptured;
                captureZone.ProgressChanged -= HandleProgressChanged;
            }

            if (_productionRoutine != null)
            {
                StopCoroutine(_productionRoutine);
                _productionRoutine = null;
            }
        }

        private void HandleCaptured()
        {
            if (IsAllied)
            {
                return;
            }

            IsAllied = true;
            PrototypeMaterialLibrary.Apply(gameObject, PrototypeMaterialRole.Player);
            if (_captureBar != null)
            {
                _captureBar.SetFillRole(PrototypeMaterialRole.Player);
                _captureBar.SetValue(1f, 1f);
            }
            PrototypePulseEffect.Spawn(
                transform.position + Vector3.up * 0.5f,
                PrototypeMaterialRole.Player,
                new Vector3(3f, 0.35f, 3f),
                0.5f);
            Changed?.Invoke();
            _productionRoutine = StartCoroutine(ProductionLoop());
        }

        public void CaptureByEnemy()
        {
            if (IsAllied || IsEnemyControlled)
            {
                return;
            }

            IsEnemyControlled = true;
            if (captureZone != null) captureZone.enabled = false;
            PrototypeMaterialLibrary.Apply(gameObject, PrototypeMaterialRole.Enemy);
            if (_captureBar != null)
            {
                _captureBar.SetFillRole(PrototypeMaterialRole.Enemy);
                _captureBar.SetValue(1f, 1f);
            }
            PrototypePulseEffect.Spawn(
                transform.position + Vector3.up * 0.5f,
                PrototypeMaterialRole.Enemy,
                new Vector3(3f, 0.35f, 3f),
                0.5f);
            Changed?.Invoke();
            _productionRoutine = StartCoroutine(ProductionLoop());
        }

        public void SetEnemyCaptureProgress(float normalizedProgress)
        {
            if (IsAllied || IsEnemyControlled || _captureBar == null)
            {
                return;
            }

            _captureBar.SetFillRole(PrototypeMaterialRole.Enemy);
            _captureBar.SetValue(Mathf.Clamp01(normalizedProgress), 1f);
        }

        private void HandleProgressChanged(float normalizedProgress)
        {
            if (_captureBar != null && !IsEnemyControlled)
            {
                _captureBar.SetFillRole(PrototypeMaterialRole.Neutral);
                _captureBar.SetValue(normalizedProgress, 1f);
            }
        }

        private IEnumerator ProductionLoop()
        {
            while (enabled)
            {
                if (SoldierCount < soldierLimit)
                {
                    SpawnSoldier();
                }

                yield return new WaitForSeconds(productionInterval);
            }
        }

        private void SpawnSoldier()
        {
            if (soldierSpawnPoint == null || waveDirector == null)
            {
                return;
            }

            var soldierObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var faction = IsEnemyControlled ? Faction.Enemy : Faction.Player;
            soldierObject.name = faction == Faction.Enemy
                ? $"Enemy Outpost Soldier {_soldiers.Count + 1}"
                : $"Allied Soldier {_soldiers.Count + 1}";
            soldierObject.transform.position = soldierSpawnPoint.position;
            soldierObject.transform.localScale = new Vector3(0.7f, 1.4f, 0.7f);
            var soldier = soldierObject.AddComponent<SoldierAgent>();
            soldier.Initialize(
                waveDirector,
                faction,
                player,
                towerRegistry,
                baseCore,
                soldierRegistry);
            _soldiers.Add(soldier);
            Changed?.Invoke();
        }
    }
}
