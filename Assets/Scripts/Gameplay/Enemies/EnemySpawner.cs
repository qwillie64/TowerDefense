using System.Collections.Generic;
using UnityEngine;
using CyberWizard.Gameplay.GameFlow;
using CyberWizard.Gameplay.Player;
using CyberWizard.Presentation;
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
        [SerializeField] private EnemyPath[] paths;
        [SerializeField] private PrototypePlayerController player;
        [SerializeField] private BaseCore baseCore;
        [SerializeField] private EnemyDefinition[] enemyDefinitions;
        [SerializeField] private EnemyBehaviorSettings behaviorSettings;

        [SerializeField, Tooltip("開啟時敵人沿路徑點的高度移動（可爬坡、走斜坡），不套用重力；路徑點需放在地表上。")]
        private bool followWaypointHeight;

        private int _spawnSequence;

        public void Configure(
            Transform newSpawnPoint,
            Transform newDestination,
            PrototypePlayerController newPlayer = null,
            BaseCore newBaseCore = null,
            EnemyDefinition[] newEnemyDefinitions = null,
            EnemyBehaviorSettings newBehaviorSettings = null,
            Transform newSecondarySpawnPoint = null,
            EnemyPath[] newPaths = null)
        {
            spawnPoint = newSpawnPoint;
            destination = newDestination;
            player = newPlayer;
            baseCore = newBaseCore;
            enemyDefinitions = newEnemyDefinitions;
            behaviorSettings = newBehaviorSettings;
            secondarySpawnPoint = newSecondarySpawnPoint;
            paths = newPaths;
        }

        public bool HasDefinition(string enemyTypeId) => FindDefinition(enemyTypeId) != null;

        public bool HasSpawnPoint(string requestedSpawnPointId)
        {
            return string.IsNullOrWhiteSpace(requestedSpawnPointId) ||
                   requestedSpawnPointId == spawnPointId ||
                   (secondarySpawnPoint != null && requestedSpawnPointId == secondarySpawnPointId);
        }

        public bool HasPath(string requestedPathId)
        {
            if (string.IsNullOrWhiteSpace(requestedPathId))
            {
                return true;
            }

            if (paths == null)
            {
                return false;
            }

            for (var index = 0; index < paths.Length; index++)
            {
                if (paths[index] != null && paths[index].PathId == requestedPathId)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 取得敵人實際會走的路線：出生點位置加上路線的所有路徑點，依序加入 points。
        /// 出生點或路線不存在時回傳 false。供路線預覽等畫面使用。
        /// </summary>
        public bool TryGetRoutePoints(string requestedSpawnPointId, string requestedPathId, List<Vector3> points)
        {
            points.Clear();
            if (spawnPoint == null || !HasSpawnPoint(requestedSpawnPointId) || !HasPath(requestedPathId))
            {
                return false;
            }

            points.Add(ResolveSpawnPoint(requestedSpawnPointId).position);
            var waypoints = ResolvePath(requestedPathId);
            if (waypoints != null)
            {
                for (var index = 0; index < waypoints.Length; index++)
                {
                    if (waypoints[index] != null)
                    {
                        points.Add(waypoints[index].position);
                    }
                }
            }
            else if (destination != null)
            {
                points.Add(destination.position);
            }

            return points.Count >= 2;
        }

        public EnemyAgent Spawn(
            string enemyTypeId,
            string waveId,
            string requestedSpawnPointId,
            string requestedPathId = null,
            string objective = null)
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

            if (!HasPath(requestedPathId))
            {
                Debug.LogError($"EnemySpawner 找不到道路：{requestedPathId}", this);
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
                player,
                baseCore,
                behaviorSettings,
                ResolvePath(requestedPathId),
                objective,
                followWaypointHeight);
            Debug.Log(
                $"[EnemySpawner] Spawn type={enemyTypeId} wave={waveId} point={requestedSpawnPointId}",
                this);
            return agent;
        }

        private Transform[] ResolvePath(string requestedPathId)
        {
            if (string.IsNullOrWhiteSpace(requestedPathId) || paths == null)
            {
                return null;
            }

            for (var index = 0; index < paths.Length; index++)
            {
                if (paths[index] != null && paths[index].PathId == requestedPathId)
                {
                    return paths[index].Waypoints;
                }
            }

            return null;
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
