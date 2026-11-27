using System.Linq;
using CyberWizard.Gameplay.Enemies;
using CyberWizard.Gameplay.Level;
using CyberWizard.Infrastructure;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;

namespace CyberWizard.Tests
{
    public sealed class GateDefenseEditModeTests
    {
        [Test]
        public void Level1Schedule_HasTwoWavesWithFourMinionsPerGroup()
        {
            Assert.That(
                LevelScheduleLoader.TryLoadFromStreamingAssets("Levels/level1_rust_alley.json", out var schedule, out var error),
                Is.True,
                error);

            Assert.That(schedule.waves.Length, Is.EqualTo(2));
            Assert.That(
                schedule.waves[0].enemies.Select(entry => entry.pathId),
                Is.EqualTo(new[] { "path_upper", "path_right" }));
            Assert.That(
                schedule.waves[1].enemies.Select(entry => entry.pathId),
                Is.EqualTo(new[] { "path_upper", "path_right", "path_right" }));

            foreach (var entry in schedule.waves.SelectMany(wave => wave.enemies))
            {
                Assert.That(entry.spawnPointId, Is.EqualTo("spawn_southwest"));
                Assert.That(entry.count, Is.EqualTo(4));
                Assert.That(entry.enemyTypeId, Is.EqualTo("enemy_level0"));
                Assert.That(entry.objective, Is.EqualTo(EnemyAgent.SiegeObjective));
            }
        }

        [Test]
        public void LevelNavMesh_BuildsWalkableSlopeAndRoutesAroundObstacles()
        {
            var root = new GameObject("NavMesh Test Level");
            var navMesh = root.AddComponent<LevelNavMesh>();
            try
            {
                // 斜坡地面（繞 X 軸傾斜 5 度，北高南低）與一道擋在中間的牆。
                var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.transform.SetParent(root.transform);
                ground.transform.localScale = new Vector3(40f, 1f, 40f);
                ground.transform.rotation = Quaternion.Euler(-5f, 0f, 0f);
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.SetParent(root.transform);
                wall.transform.position = new Vector3(0f, 2f, 0f);
                wall.transform.localScale = new Vector3(20f, 4f, 1f);
                wall.transform.rotation = ground.transform.rotation;

                Assert.That(navMesh.Build(), Is.True);
                Assert.That(navMesh.IsBuilt, Is.True);

                var south = new Vector3(0f, 0f, -12f);
                var north = new Vector3(0f, 0f, 12f);
                Assert.That(NavMesh.SamplePosition(south, out var start, 3f, NavMesh.AllAreas), Is.True);
                Assert.That(NavMesh.SamplePosition(north, out var end, 3f, NavMesh.AllAreas), Is.True);
                Assert.That(end.position.y, Is.GreaterThan(start.position.y + 1f), "北側應位於斜坡較高處。");

                var path = new NavMeshPath();
                Assert.That(NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path), Is.True);
                Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete));
                Assert.That(path.corners.Length, Is.GreaterThan(2), "路線應繞過牆，而不是直線穿過。");
                Assert.That(path.corners.Max(corner => Mathf.Abs(corner.x)), Is.GreaterThan(10f));
            }
            finally
            {
                navMesh.Clear();
                Object.DestroyImmediate(root);
            }
        }
    }
}
