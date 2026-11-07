using System.IO;
using CyberWizard.Core;
using CyberWizard.Data;
using CyberWizard.Gameplay.Enemies;
using CyberWizard.Gameplay.Building;
using CyberWizard.Gameplay.Resources;
using CyberWizard.Gameplay.Towers;
using NUnit.Framework;
using UnityEngine;

namespace CyberWizard.Tests
{
    public sealed class PrototypeCoreEditModeTests
    {
        private sealed class DamageableStub : IDamageable
        {
            public bool IsAlive => Health > 0f;
            public Faction Faction => Faction.Player;
            public Transform TargetTransform => null;
            public float Health { get; private set; } = 100f;
            public void TakeDamage(float amount) => Health = Mathf.Max(0f, Health - amount);
        }

        [Test]
        public void ResourceWallet_FailedSpendDoesNotChangeBalance()
        {
            var gameObject = new GameObject("Wallet Test");
            try
            {
                var wallet = gameObject.AddComponent<ResourceWallet>();
                wallet.Configure(20);

                Assert.That(wallet.TrySpend(25), Is.False);
                Assert.That(wallet.Balance, Is.EqualTo(20));
                Assert.That(wallet.TrySpend(15), Is.True);
                Assert.That(wallet.Balance, Is.EqualTo(5));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void Vitality_DamageRestoreAndDepletionAreConsistent()
        {
            var gameObject = new GameObject("Vitality Test");
            try
            {
                var vitality = gameObject.AddComponent<Vitality>();
                var depletedCount = 0;
                vitality.Depleted += () => depletedCount++;
                vitality.Initialize(100f);

                Assert.That(vitality.TakeDamage(35f), Is.True);
                Assert.That(vitality.Current, Is.EqualTo(65f));
                Assert.That(vitality.Restore(20f), Is.True);
                Assert.That(vitality.Current, Is.EqualTo(85f));
                Assert.That(vitality.TakeDamage(200f), Is.True);
                Assert.That(vitality.Current, Is.Zero);
                Assert.That(vitality.IsAlive, Is.False);
                Assert.That(depletedCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void LevelScheduleParser_AcceptsMultipleWavesAndRejectsDuplicateIds()
        {
            const string validJson =
                "{\"version\":1,\"levelId\":\"test\",\"waves\":[" +
                "{\"waveId\":\"a\",\"startTime\":0,\"enemies\":[" +
                "{\"enemyTypeId\":\"enemy_basic\",\"count\":1,\"spawnPointId\":\"spawn_a\",\"spawnInterval\":0}]}," +
                "{\"waveId\":\"b\",\"startTime\":2,\"enemies\":[" +
                "{\"enemyTypeId\":\"enemy_fast\",\"count\":2,\"spawnPointId\":\"spawn_b\",\"spawnInterval\":0.5}]}]}";

            Assert.That(LevelScheduleParser.TryParse(validJson, out var schedule, out var error), Is.True, error);
            Assert.That(schedule.waves.Length, Is.EqualTo(2));

            var duplicateJson = validJson.Replace("\"waveId\":\"b\"", "\"waveId\":\"a\"");
            Assert.That(LevelScheduleParser.TryParse(duplicateJson, out _, out error), Is.False);
            StringAssert.Contains("重複", error);
        }

        [Test]
        public void Level02_ParsesAndContainsTankEnemy()
        {
            var path = Path.Combine(Application.streamingAssetsPath, "Levels", "level_02.json");
            var json = File.ReadAllText(path);

            Assert.That(LevelScheduleParser.TryParse(json, out var schedule, out var error), Is.True, error);
            Assert.That(schedule.levelId, Is.EqualTo("level_02"));
            Assert.That(schedule.displayName, Is.EqualTo("赤潮壓境"));
            Assert.That(schedule.difficulty, Is.EqualTo("進階"));

            var containsTank = false;
            for (var waveIndex = 0; waveIndex < schedule.waves.Length; waveIndex++)
            {
                var entries = schedule.waves[waveIndex].enemies;
                for (var entryIndex = 0; entryIndex < entries.Length; entryIndex++)
                {
                    containsTank |= entries[entryIndex].enemyTypeId == "enemy_tank";
                }
            }

            Assert.That(containsTank, Is.True);
        }

        [Test]
        public void EnemyBehaviorSettings_ClampsCombinedIntentChance()
        {
            var settings = ScriptableObject.CreateInstance<EnemyBehaviorSettings>();
            try
            {
                settings.Configure(0.8f, 0.8f, -2f, 0f);

                Assert.That(settings.CaptureNeutralChance, Is.EqualTo(0.8f).Within(0.0001f));
                Assert.That(settings.AttackTowerChance, Is.EqualTo(0.2f).Within(0.0001f));
                Assert.That(settings.NeutralCaptureDuration, Is.GreaterThanOrEqualTo(0.1f));
                Assert.That(settings.AttackDistance, Is.GreaterThanOrEqualTo(0.1f));
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void EnemyIntentSelector_UsesAvailabilityAndConfiguredThresholds()
        {
            Assert.That(
                EnemyIntentSelector.Select(0.1f, 0.15f, 0.15f, true, true),
                Is.EqualTo(EnemyIntent.CaptureNeutral));
            Assert.That(
                EnemyIntentSelector.Select(0.1f, 0.15f, 0.15f, false, true),
                Is.EqualTo(EnemyIntent.AttackBase));
            Assert.That(
                EnemyIntentSelector.Select(0.2f, 0.15f, 0.15f, true, true),
                Is.EqualTo(EnemyIntent.AttackTower));
            Assert.That(
                EnemyIntentSelector.Select(0.2f, 0.15f, 0.15f, true, false),
                Is.EqualTo(EnemyIntent.AttackBase));
            Assert.That(
                EnemyIntentSelector.Select(0.9f, 0.15f, 0.15f, true, true),
                Is.EqualTo(EnemyIntent.AttackBase));
        }

        [Test]
        public void EnemyMeleeAttack_RespectsCooldownAndTicksDeterministically()
        {
            var target = new DamageableStub();
            var attack = new EnemyMeleeAttack(10f, 1f);

            Assert.That(attack.TryAttack(target), Is.True);
            Assert.That(target.Health, Is.EqualTo(90f));
            Assert.That(attack.TryAttack(target), Is.False);
            attack.Tick(0.5f);
            Assert.That(attack.TryAttack(target), Is.False);
            attack.Tick(0.5f);
            Assert.That(attack.TryAttack(target), Is.True);
            Assert.That(target.Health, Is.EqualTo(80f));
        }

        [Test]
        public void EnemyMovementMotor_MovesWithoutOvershootAndReportsArrival()
        {
            var owner = new GameObject("Movement Owner");
            var target = new GameObject("Movement Target");
            try
            {
                owner.transform.position = Vector3.zero;
                target.transform.position = new Vector3(2f, 3f, 0f);
                var motor = new EnemyMovementMotor(owner.transform, 2f);

                Assert.That(motor.MoveTowards(target.transform, 0.1f, 1f, 0.5f), Is.False);
                Assert.That(owner.transform.position, Is.EqualTo(new Vector3(1f, 0f, 0f)));
                Assert.That(motor.MoveTowards(target.transform, 0.1f, 1f, 0.5f), Is.False);
                Assert.That(owner.transform.position, Is.EqualTo(new Vector3(2f, 0f, 0f)));
                Assert.That(motor.MoveTowards(target.transform, 0.1f, 1f, 0.5f), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void TowerDefinition_ClampsInvalidDesignValues()
        {
            var definition = ScriptableObject.CreateInstance<TowerDefinition>();
            try
            {
                definition.Configure(
                    "tower_test",
                    ElementType.Earth,
                    -10f,
                    -5f,
                    0f,
                    0f,
                    0f,
                    -1f,
                    -10,
                    PrimitiveType.Cube,
                    0,
                    -1,
                    -1f,
                    -1f,
                    -1f);

                Assert.That(definition.MaxDurability, Is.GreaterThanOrEqualTo(1f));
                Assert.That(definition.AttackDamage, Is.Zero);
                Assert.That(definition.AttackRange, Is.GreaterThanOrEqualTo(0.1f));
                Assert.That(definition.AttackInterval, Is.GreaterThanOrEqualTo(0.05f));
                Assert.That(definition.BuildCost, Is.Zero);
                Assert.That(definition.MaxLevel, Is.EqualTo(1));
                Assert.That(definition.UpgradeBaseCost, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void TowerAssembler_CombinesThreePartModifiersWithoutMutatingDefinition()
        {
            var definition = ScriptableObject.CreateInstance<TowerDefinition>();
            var basePart = ScriptableObject.CreateInstance<TowerPartDefinition>();
            var bodyPart = ScriptableObject.CreateInstance<TowerPartDefinition>();
            var topPart = ScriptableObject.CreateInstance<TowerPartDefinition>();
            try
            {
                definition.Configure(
                    "tower_test",
                    ElementType.Fire,
                    100f,
                    20f,
                    6f,
                    1f,
                    10f,
                    2f,
                    25,
                    PrimitiveType.Sphere);
                basePart.Configure(
                    "base_test", "測試塔基", TowerPartSlot.Base,
                    PrimitiveType.Cylinder, Vector3.one,
                    1.2f, 1f, 0f, 1f, 1f, 1f, 5);
                bodyPart.Configure(
                    "body_test", "測試塔身", TowerPartSlot.Body,
                    PrimitiveType.Cube, Vector3.one,
                    1f, 1.5f, 0.5f, 0.8f, 1f, 1f, 0);
                topPart.Configure(
                    "top_test", "測試塔頂", TowerPartSlot.Top,
                    PrimitiveType.Sphere, Vector3.one,
                    1f, 1f, 0f, 1f, 2f, 0.5f, 2);
                definition.SetParts(basePart, bodyPart, topPart);

                var stats = TowerAssembler.Assemble(definition);

                Assert.That(stats.MaxDurability, Is.EqualTo(120f).Within(0.001f));
                Assert.That(stats.AttackDamage, Is.EqualTo(30f).Within(0.001f));
                Assert.That(stats.AttackRange, Is.EqualTo(6.5f).Within(0.001f));
                Assert.That(stats.AttackInterval, Is.EqualTo(0.8f).Within(0.001f));
                Assert.That(stats.ProjectileSpeed, Is.EqualTo(20f).Within(0.001f));
                Assert.That(stats.DurabilityCostPerShot, Is.EqualTo(1f).Within(0.001f));
                Assert.That(stats.BuildCost, Is.EqualTo(32));
                Assert.That(definition.AttackDamage, Is.EqualTo(20f));
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(basePart);
                Object.DestroyImmediate(bodyPart);
                Object.DestroyImmediate(topPart);
            }
        }

        [Test]
        public void TowerPlacementRules_ReturnFirstActionableFailure()
        {
            var noGround = TowerPlacementValidator.Evaluate(
                false, false, false, true, false, false, false, false);
            Assert.That(noGround.Failure, Is.EqualTo(TowerPlacementFailure.NoGroundPoint));

            var locked = TowerPlacementValidator.Evaluate(
                true, false, false, false, true, true, true, true);
            Assert.That(locked.Failure, Is.EqualTo(TowerPlacementFailure.TerritoryLocked));

            var missingDefinition = TowerPlacementValidator.Evaluate(
                true, true, true, false, true, true, false, false);
            Assert.That(
                missingDefinition.Failure,
                Is.EqualTo(TowerPlacementFailure.MissingDefinition));

            var valid = TowerPlacementValidator.Evaluate(
                true, true, true, false, true, true, true, true);
            Assert.That(valid.IsValid, Is.True);
            Assert.That(valid.Message, Is.Empty);
        }
    }
}
