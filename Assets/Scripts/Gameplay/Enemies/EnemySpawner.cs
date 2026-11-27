using System;
using System.Collections.Generic;
using CyberWizard.Data;
using CyberWizard.Gameplay.GameFlow;
using CyberWizard.Gameplay.Player;
using CyberWizard.Presentation;
using UnityEngine;

namespace CyberWizard.Gameplay.Enemies
{
    public sealed class EnemySpawner : MonoBehaviour
    {
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private string spawnPointId = "spawn_a";
        [SerializeField] private Transform secondarySpawnPoint;
        [SerializeField] private string secondarySpawnPointId = "spawn_b";
        [SerializeField, Tooltip("波次資料沒有指定道路時，敵人直接走向此點。")]
        private Transform destination;
        [SerializeField] private EnemyPath[] paths;
        [SerializeField] private PrototypePlayerController player;
        [SerializeField] private BaseCore baseCore;
        [SerializeField] private EnemyDefinition[] enemyDefinitions;
        [SerializeField] private EnemyBehaviorSettings behaviorSettings;

        private int _spawnSequence;

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
            var route = ResolveRoute(requestedPathId);
            for (var index = 0; index < route.Length; index++)
            {
                if (route[index] != null)
                {
                    points.Add(route[index].position);
                }
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
                ResolveRoute(requestedPathId),
                player,
                baseCore,
                behaviorSettings,
                string.Equals(objective, EnemyAgent.SiegeObjective, StringComparison.OrdinalIgnoreCase));
            return agent;
        }

        /// <summary>敵人要依序走過的點：指定道路的路徑點；沒有指定道路時只有終點。</summary>
        private Transform[] ResolveRoute(string requestedPathId)
        {
            if (!string.IsNullOrWhiteSpace(requestedPathId) && paths != null)
            {
                for (var index = 0; index < paths.Length; index++)
                {
                    if (paths[index] != null && paths[index].PathId == requestedPathId)
                    {
                        return paths[index].Waypoints ?? Array.Empty<Transform>();
                    }
                }
            }

            return destination != null ? new[] { destination } : Array.Empty<Transform>();
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
