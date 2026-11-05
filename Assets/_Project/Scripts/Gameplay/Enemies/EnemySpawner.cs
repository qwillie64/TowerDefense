using UnityEngine;
using CyberWizard.Gameplay.GameFlow;
using CyberWizard.Gameplay.Player;
using CyberWizard.Gameplay.Towers;
using CyberWizard.Presentation;
using CyberWizard.Gameplay.Neutral;

namespace CyberWizard.Gameplay.Enemies
{
    public sealed class EnemySpawner : MonoBehaviour
    {
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private Transform destination;
        [SerializeField, Min(0.1f)] private float enemyMoveSpeed = 2.5f;
        [SerializeField] private TowerRegistry towerRegistry;
        [SerializeField] private PrototypePlayerController player;
        [SerializeField] private BaseCore baseCore;
        [SerializeField] private NeutralOutpostModule neutralModule;

        private int _spawnSequence;

        public void Configure(
            Transform newSpawnPoint,
            Transform newDestination,
            TowerRegistry newTowerRegistry = null,
            PrototypePlayerController newPlayer = null,
            BaseCore newBaseCore = null)
        {
            spawnPoint = newSpawnPoint;
            destination = newDestination;
            towerRegistry = newTowerRegistry;
            player = newPlayer;
            baseCore = newBaseCore;
        }

        public void SetNeutralModule(NeutralOutpostModule module)
        {
            neutralModule = module;
        }

        public EnemyAgent Spawn(string enemyTypeId, string waveId)
        {
            if (spawnPoint == null || destination == null)
            {
                Debug.LogError("EnemySpawner 缺少出生點或終點。", this);
                return null;
            }

            var enemyObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            enemyObject.name = $"Enemy_{enemyTypeId}_{++_spawnSequence:00}";
            enemyObject.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
            enemyObject.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
            PrototypeMaterialLibrary.Apply(enemyObject, PrototypeMaterialRole.Enemy);

            var agent = enemyObject.AddComponent<EnemyAgent>();
            agent.Initialize(
                enemyTypeId,
                waveId,
                destination,
                enemyMoveSpeed,
                towerRegistry,
                player,
                baseCore,
                neutralModule);
            return agent;
        }

        private void OnValidate()
        {
            enemyMoveSpeed = Mathf.Max(0.1f, enemyMoveSpeed);
        }
    }
}
