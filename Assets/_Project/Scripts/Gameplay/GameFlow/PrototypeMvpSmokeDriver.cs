using System;
using System.Collections;
using CyberWizard.Gameplay.Building;
using CyberWizard.Gameplay.Capture;
using CyberWizard.Gameplay.Combo;
using CyberWizard.Gameplay.Neutral;
using CyberWizard.Gameplay.Player;
using CyberWizard.Gameplay.Towers;
using CyberWizard.Gameplay.Waves;
using UnityEngine;

namespace CyberWizard.Gameplay.GameFlow
{
    public sealed class PrototypeMvpSmokeDriver : MonoBehaviour
    {
        [SerializeField] private PrototypePlayerController player;
        [SerializeField] private TerritoryController territory;
        [SerializeField] private PrototypeBuildController buildController;
        [SerializeField] private PrototypeComboSystem comboSystem;
        [SerializeField] private NeutralOutpostModule neutralModule;
        [SerializeField] private WaveDirector waveDirector;
        [SerializeField] private PrototypeGameFlowController gameFlow;

        public void Configure(
            PrototypePlayerController newPlayer,
            TerritoryController newTerritory,
            PrototypeBuildController newBuildController,
            PrototypeComboSystem newComboSystem,
            NeutralOutpostModule newNeutralModule,
            WaveDirector newWaveDirector,
            PrototypeGameFlowController newGameFlow)
        {
            player = newPlayer;
            territory = newTerritory;
            buildController = newBuildController;
            comboSystem = newComboSystem;
            neutralModule = newNeutralModule;
            waveDirector = newWaveDirector;
            gameFlow = newGameFlow;
        }

        private void Start()
        {
            if (HasCommandLineArgument("-smokeTest"))
            {
                StartCoroutine(HasCommandLineArgument("-enemyOutpostSmoke")
                    ? RunEnemyOutpostScenario()
                    : RunSmokeScenario());
            }
        }

        private IEnumerator RunEnemyOutpostScenario()
        {
            yield return null;
            if (neutralModule == null || !neutralModule.FeatureEnabled || neutralModule.Outpost == null)
            {
                Debug.LogError("[MvpSmoke] EnemyOutpostTestUnavailable");
                yield break;
            }

            neutralModule.Outpost.CaptureByEnemy();
            yield return new WaitUntil(() => neutralModule.Outpost.SoldierCount > 0);
            Debug.Log($"[MvpSmoke] EnemyOutpostSoldierSpawned count={neutralModule.Outpost.SoldierCount}");
        }

        private IEnumerator RunSmokeScenario()
        {
            yield return new WaitUntil(() => territory != null && territory.IsCaptured);
            Debug.Log("[MvpSmoke] TerritoryCaptured");

            if (gameFlow != null)
            {
                var elapsedBeforePause = waveDirector.ElapsedTime;
                gameFlow.TogglePause();
                yield return new WaitForSecondsRealtime(0.15f);
                var pausePassed = gameFlow.CurrentPhase == CyberWizard.Core.GamePhase.Paused &&
                                  Mathf.Approximately(elapsedBeforePause, waveDirector.ElapsedTime);
                gameFlow.TogglePause();
                Debug.Log($"[MvpSmoke] PausePassed={pausePassed}");
            }

            var fireBuilt = buildController.TryBuildAt(
                new Vector3(2.2f, 1f, -1f),
                ElementType.Fire);
            var windBuilt = buildController.TryBuildAt(
                new Vector3(4.4f, 1f, -1f),
                ElementType.Wind);
            Debug.Log($"[MvpSmoke] TowersBuilt fire={fireBuilt} wind={windBuilt}");

            TeleportPlayer(new Vector3(2.2f, 1f, -1f));
            var towerUpgraded = buildController.TryUpgradeNearestTower();
            Debug.Log($"[MvpSmoke] TowerUpgraded={towerUpgraded}");

            if (neutralModule != null && neutralModule.FeatureEnabled && neutralModule.Outpost != null)
            {
                TeleportPlayer(neutralModule.Outpost.transform.position + Vector3.up);
                yield return new WaitUntil(() => neutralModule.Outpost.IsAllied);
                Debug.Log("[MvpSmoke] NeutralOutpostAllied");
                TeleportPlayer(new Vector3(6f, 1f, -6f));
            }

            yield return new WaitUntil(() => waveDirector != null && waveDirector.AliveCount > 0);
            var comboCast = false;
            var deadline = Time.time + 8f;
            while (!comboCast && Time.time < deadline && waveDirector.AliveCount > 0)
            {
                CyberWizard.Gameplay.Enemies.EnemyAgent candidate = null;
                foreach (var enemy in waveDirector.AliveEnemies)
                {
                    if (enemy != null &&
                        Vector3.Distance(enemy.transform.position, new Vector3(2.2f, 1f, -1f)) <= 6.5f &&
                        Vector3.Distance(enemy.transform.position, new Vector3(4.4f, 1f, -1f)) <= 6.5f)
                    {
                        candidate = enemy;
                        break;
                    }
                }

                if (candidate != null)
                    comboCast = comboSystem.TryCastAtPoint(candidate.transform.position);

                if (!comboCast) yield return null;
            }

            Debug.Log($"[MvpSmoke] ComboCast={comboCast}");
        }

        private void TeleportPlayer(Vector3 position)
        {
            if (player == null)
            {
                return;
            }

            var characterController = player.GetComponent<CharacterController>();
            if (characterController != null) characterController.enabled = false;
            player.transform.position = position;
            if (characterController != null) characterController.enabled = true;
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
    }
}
