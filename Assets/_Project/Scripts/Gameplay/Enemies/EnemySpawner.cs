using UnityEngine;
using CyberWizard.Gameplay.GameFlow;
using CyberWizard.Gameplay.Player;
using CyberWizard.Gameplay.Towers;
using CyberWizard.Presentation;
using CyberWizard.Gameplay.Neutral;
using CyberWizard.Data;

namespace CyberWizard.Gameplay.Enemies
{
    public sealed class EnemySpawner : MonoBehaviour
    {
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private string spawnPointId = "spawn_a";
        [SerializeField] private Transform secondarySpawnPoint;
        [SerializeField] private string secondarySpawnPointId = "spawn_b";
        [SerializeField] private Transform destination;
        [SerializeField] private TowerRegistry towerRegistry;
        [SerializeField] private PrototypePlayerController player;
        [SerializeField] private BaseCore baseCore;
        [SerializeField] private NeutralOutpostModule neutralModule;
        [SerializeField] private EnemyDefinition[] enemyDefinitions;
        [SerializeField] private EnemyBehaviorSettings behaviorSettings;

        private int _spawnSequence;

        public void Configure(
            Transform newSpawnPoint,
            Transform newDestination,
            TowerRegistry newTowerRegistry = null,
            PrototypePlayerController newPlayer = null,
            BaseCore newBaseCore = null,
            EnemyDefinition[] newEnemyDefinitions = null,
            EnemyBehaviorSettings newBehaviorSettings = null,
            Transform newSecondarySpawnPoint = null)
        {
            spawnPoint = newSpawnPoint;
            destination = newDestination;
            towerRegistry = newTowerRegistry;
            player = newPlayer;
            baseCore = newBaseCore;
            enemyDefinitions = newEnemyDefinitions;
            behaviorSettings = newBehaviorSettings;
            secondarySpawnPoint = newSecondarySpawnPoint;
        }

        public void SetNeutralModule(NeutralOutpostModule module)
        {
            neutralModule = module;
        }

        public bool HasDefinition(string enemyTypeId) => FindDefinition(enemyTypeId) != null;

        public bool HasSpawnPoint(string requestedSpawnPointId)
        {
            return string.IsNullOrWhiteSpace(requestedSpawnPointId) ||
                   requestedSpawnPointId == spawnPointId ||
                   (secondarySpawnPoint != null && requestedSpawnPointId == secondarySpawnPointId);
        }

        public EnemyAgent Spawn(string enemyTypeId, string waveId, string requestedSpawnPointId)
        {
            if (spawnPoint == null || destination == null)
            {
                Debug.LogError("EnemySpawner 缺少出生點或終點。", this);
                return null;
            }

            if (!HasSpawnPoint(requestedSpawnPointId))
            {
                Debug.LogError($"EnemySpawner 找不到出生點：{requestedSpawnPointId}", this);
                return null;
            }

            var definition = FindDefinition(enemyTypeId);
            if (definition == null)
            {
                Debug.LogError($"EnemySpawner 找不到敵人定義：{enemyTypeId}", this);
                return null;
            }

            var enemyObject = GameObject.CreatePrimitive(definition.PrototypeShape);
            enemyObject.name = $"Enemy_{enemyTypeId}_{++_spawnSequence:00}";
            var selectedSpawnPoint = ResolveSpawnPoint(requestedSpawnPointId);
            enemyObject.transform.SetPositionAndRotation(
                selectedSpawnPoint.position,
                selectedSpawnPoint.rotation);
            enemyObject.transform.localScale = definition.PrototypeScale;
            PrototypeMaterialLibrary.Apply(enemyObject, PrototypeMaterialRole.Enemy);

            var agent = enemyObject.AddComponent<EnemyAgent>();
            agent.Initialize(
                definition,
                waveId,
                destination,
                towerRegistry,
                player,
                baseCore,
                neutralModule,
                behaviorSettings);
            Debug.Log(
                $"[EnemySpawner] Spawn type={enemyTypeId} wave={waveId} point={requestedSpawnPointId}",
                this);
            return agent;
        }

        private Transform ResolveSpawnPoint(string requestedSpawnPointId)
        {
            if (secondarySpawnPoint != null && requestedSpawnPointId == secondarySpawnPointId)
            {
                return secondarySpawnPoint;
            }

            return spawnPoint;
        }

        private EnemyDefinition FindDefinition(string enemyTypeId)
        {
            if (enemyDefinitions == null)
            {
                return null;
            }

            for (var index = 0; index < enemyDefinitions.Length; index++)
            {
                var definition = enemyDefinitions[index];
                if (definition != null && definition.EnemyTypeId == enemyTypeId)
                {
                    return definition;
                }
            }

            return null;
        }
    }
}
