using System.Linq;
using CyberWizard.Gameplay.Enemies;
using CyberWizard.Infrastructure;
using NUnit.Framework;
using UnityEngine;

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
        public void EnemyMovementMotor_FollowTargetHeight_ClimbsToWaypointAndStaysUpright()
        {
            var owner = new GameObject("Climber").transform;
            var target = new GameObject("Waypoint").transform;
            try
            {
                target.position = new Vector3(6f, 3f, 0f);
                var motor = new EnemyMovementMotor(owner, 2f, true, 1f);

                var arrived = false;
                for (var step = 0; step < 100 && !arrived; step++)
                {
                    arrived = motor.MoveTowards(target, 0.01f, 1f, 0.1f);
                }

                Assert.That(arrived, Is.True);
                Assert.That(Vector3.Distance(owner.position, new Vector3(6f, 4f, 0f)), Is.LessThan(0.02f));
                Assert.That(Vector3.Angle(owner.up, Vector3.up), Is.LessThan(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(owner.gameObject);
                Object.DestroyImmediate(target.gameObject);
            }
        }
    }
}
