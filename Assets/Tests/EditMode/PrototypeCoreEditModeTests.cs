using CyberWizard.Core;
using CyberWizard.Data;
using CyberWizard.Gameplay.Enemies;
using CyberWizard.Gameplay.Resources;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

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
                // EditMode 不會呼叫 Awake，餘額從 0 開始。
                wallet.Add(20);

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
        public void Vitality_DamageAndDepletionAreConsistent()
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
                "{\"enemyTypeId\":\"enemy_level0\",\"count\":1,\"spawnPointId\":\"spawn_a\",\"spawnInterval\":0}]}," +
                "{\"waveId\":\"b\",\"startTime\":2,\"enemies\":[" +
                "{\"enemyTypeId\":\"enemy_level1\",\"count\":2,\"spawnPointId\":\"spawn_b\",\"spawnInterval\":0.5}]}]}";

            Assert.That(LevelScheduleParser.TryParse(validJson, out var schedule, out var error), Is.True, error);
            Assert.That(schedule.waves.Length, Is.EqualTo(2));

            var duplicateJson = validJson.Replace("\"waveId\":\"b\"", "\"waveId\":\"a\"");
            Assert.That(LevelScheduleParser.TryParse(duplicateJson, out _, out error), Is.False);
            StringAssert.Contains("重複", error);
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
    }
}
