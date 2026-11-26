using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CyberWizard.Core;
using CyberWizard.Gameplay.Combat;
using CyberWizard.Gameplay.Enemies;
using CyberWizard.Gameplay.GameFlow;
using CyberWizard.Gameplay.Player;
using CyberWizard.Gameplay.Resources;
using CyberWizard.Gameplay.Towers;
using CyberWizard.Gameplay.Waves;
using CyberWizard.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace CyberWizard.Tests
{
    /// <summary>
    /// Level_1 的塔攻擊、升級、地面路標、玩家與攝影機設定（Ref/開發規劃4.docx）。
    /// </summary>
    public sealed class Level1CombatPlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/Level_1.unity";
        private const string SpawnId = "spawn_southwest";

        private float _previousTimeScale;

        [SetUp]
        public void SetUp()
        {
            _previousTimeScale = Time.timeScale;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = _previousTimeScale;
        }

        [UnityTest]
        public IEnumerator PathArrows_ShowUpcomingWaveRoutesUntilThatWaveSpawns()
        {
            yield return LoadLevel();

            var preview = Object.FindObjectOfType<WavePathPreview>();
            var waveDirector = Object.FindObjectOfType<WaveDirector>();
            Assert.That(preview, Is.Not.Null);
            Assert.That(waveDirector, Is.Not.Null);

            // 第一波開始前：兩條路線都有箭頭。
            Assert.That(preview.ShownWaveId, Is.EqualTo("wave_01"));
            Assert.That(preview.IsRouteVisible(SpawnId, "path_upper"), Is.True);
            Assert.That(preview.IsRouteVisible(SpawnId, "path_right"), Is.True);
            Assert.That(preview.VisibleRouteCount, Is.EqualTo(2));

            var strips = preview.GetComponentsInChildren<MeshRenderer>(false);
            Assert.That(strips.Length, Is.EqualTo(2));
            foreach (var strip in strips)
            {
                var color = strip.sharedMaterial.GetColor("_BaseColor");
                Assert.That(color.a, Is.EqualTo(0.6f).Within(0.01f), "箭頭應為 alpha 0.6。");
                Assert.That(color.r, Is.GreaterThan(0.9f));
                Assert.That(strip.GetComponent<MeshFilter>().sharedMesh.vertexCount, Is.GreaterThan(100));
            }

            Time.timeScale = 20f;
            yield return WaitUntil(() => waveDirector.StartedWaveCount >= 1, 10f);
            yield return null;
            Assert.That(preview.ShownWaveId, Is.Null, "第一波生成中應隱藏箭頭。");
            Assert.That(preview.VisibleRouteCount, Is.EqualTo(0));

            // 第一波全部生成後，顯示第二波（路徑一、路徑二；兩個路徑二梯隊共用同一串箭頭）。
            yield return WaitUntil(() => waveDirector.PendingSpawnCount == 0, 10f);
            yield return null;
            Assert.That(preview.ShownWaveId, Is.EqualTo("wave_02"));
            Assert.That(preview.IsRouteVisible(SpawnId, "path_upper"), Is.True);
            Assert.That(preview.IsRouteVisible(SpawnId, "path_right"), Is.True);
            Assert.That(preview.VisibleRouteCount, Is.EqualTo(2));

            yield return WaitUntil(() => waveDirector.StartedWaveCount >= 2, 10f);
            yield return null;
            Assert.That(preview.VisibleRouteCount, Is.EqualTo(0), "最後一波開始後不應再有箭頭。");
        }

        [UnityTest]
        public IEnumerator Towers_AimFireRivetsKillEnemiesAndGrantEnergy()
        {
            yield return LoadLevel();

            var waveDirector = Object.FindObjectOfType<WaveDirector>();
            var wallet = Object.FindObjectOfType<ResourceWallet>();
            var towers = Object.FindObjectsOfType<FixedTowerCombat>();
            Assert.That(towers.Length, Is.EqualTo(3));
            Assert.That(wallet.Balance, Is.EqualTo(0));
            foreach (var tower in towers)
            {
                Assert.That(tower.MaxHealth, Is.EqualTo(200f));
                Assert.That(tower.Damage, Is.EqualTo(10f));
                Assert.That(tower.ShotsPerSecond, Is.EqualTo(1f));
                Assert.That(tower.AttackRange, Is.EqualTo(24f).Within(0.001f), "射程 8 × 倍率 3。");
                Assert.That(tower.MuzzleFlash, Is.Not.Null);
                Assert.That(tower.TurretPivot, Is.Not.Null);
            }

            // 路徑二的敵人約第 110 秒才經過 7、9 號塔，但無人防守時閘門約第 98 秒就被打破、
            // 敵人全部停住。本測試只驗證塔的攻擊，所以把閘門血量調高，讓敵人走完路線。
            Object.FindObjectOfType<BaseCore>().GetComponent<Vitality>().Initialize(100000f);

            var kills = 0;
            waveDirector.EnemyDefeated += _ => kills++;
            var acquired = new HashSet<FixedTowerCombat>();
            var aimed = new HashSet<FixedTowerCombat>();
            var flashed = new HashSet<FixedTowerCombat>();
            var sawRivet = false;

            Time.timeScale = 20f;
            var deadline = Time.realtimeSinceStartup + 90f;
            while (Time.realtimeSinceStartup < deadline && (acquired.Count < 3 || flashed.Count < 3 || kills < 3))
            {
                foreach (var tower in towers)
                {
                    var target = tower.CurrentTarget;
                    if (target != null)
                    {
                        acquired.Add(tower);
                        var toTarget = target.transform.position - tower.TurretPivot.position;
                        if (Vector3.Angle(tower.TurretPivot.forward, toTarget) < 10f)
                        {
                            aimed.Add(tower);
                        }
                    }

                    if (tower.MuzzleFlash.isPlaying || tower.MuzzleFlash.particleCount > 0)
                    {
                        flashed.Add(tower);
                    }
                }

                if (!sawRivet)
                {
                    sawRivet = Object.FindObjectsOfType<RivetProjectile>().Length > 0;
                }

                yield return null;
            }

            Assert.That(acquired.Count, Is.EqualTo(3), "三座塔都應鎖定過敵人。");
            Assert.That(aimed.Count, Is.EqualTo(3), "三座塔的砲塔都應轉向目標。");
            Assert.That(flashed.Count, Is.EqualTo(3), "三座塔發射時都應播放砲口火焰。");
            Assert.That(sawRivet, Is.True, "應看到鉚釘發射物。");
            Assert.That(kills, Is.GreaterThanOrEqualTo(3));
            Assert.That(wallet.Balance, Is.EqualTo(kills * 100), "每消滅一隻 Level 0 敵人獲得 100 能量。");
        }

        [UnityTest]
        public IEnumerator Upgrade_PlayerNearTowerUpgradesEachPartWithEnergy()
        {
            yield return LoadLevel();

            var interactor = Object.FindObjectOfType<TowerUpgradeInteractor>();
            var prompt = Object.FindObjectOfType<TowerUpgradePrompt>();
            var wallet = Object.FindObjectOfType<ResourceWallet>();
            var player = Object.FindObjectOfType<PrototypePlayerController>();
            var tower = Object.FindObjectsOfType<FixedTowerCombat>().Single(item => item.TowerNumber == 5);
            Assert.That(interactor, Is.Not.Null);
            Assert.That(prompt, Is.Not.Null);

            // 玩家在閘門前時不在任何塔的升級範圍內。
            interactor.RefreshCurrentTower();
            Assert.That(interactor.CurrentTower, Is.Null);
            yield return null;
            Assert.That(prompt.IsVisible, Is.False);

            Teleport(player, tower.transform.position + new Vector3(8f, 1.5f, 0f));
            interactor.RefreshCurrentTower();
            Assert.That(interactor.CurrentTower, Is.SameAs(tower));
            yield return null;
            Assert.That(prompt.IsVisible, Is.True);
            Assert.That(prompt.ShownTower, Is.SameAs(tower));
            Assert.That(prompt.CurrentText, Does.Contain("5號塔"));
            Assert.That(tower.transform.Find("Attack Range Circle").gameObject.activeSelf, Is.True);

            // 能量不足。
            Assert.That(interactor.TryUpgrade(TowerPart.Turret), Is.EqualTo(TowerUpgradeResult.NotEnoughEnergy));
            Assert.That(tower.GetLevel(TowerPart.Turret), Is.EqualTo(1));

            wallet.Add(1000);
            interactor.RefreshCurrentTower();

            // 砲塔：40/80/130/200，攻擊 +4/+6/+8/+12，攻速 +0.2/+0.2/+0.4/+0.8。
            Assert.That(interactor.TryUpgrade(TowerPart.Turret), Is.EqualTo(TowerUpgradeResult.Upgraded));
            Assert.That(tower.Damage, Is.EqualTo(14f).Within(0.001f));
            Assert.That(tower.ShotsPerSecond, Is.EqualTo(1.2f).Within(0.001f));
            Assert.That(wallet.Balance, Is.EqualTo(960));
            for (var index = 0; index < 3; index++)
            {
                Assert.That(interactor.TryUpgrade(TowerPart.Turret), Is.EqualTo(TowerUpgradeResult.Upgraded));
            }

            Assert.That(tower.GetLevel(TowerPart.Turret), Is.EqualTo(5));
            Assert.That(tower.Damage, Is.EqualTo(40f).Within(0.001f));
            Assert.That(tower.ShotsPerSecond, Is.EqualTo(2.6f).Within(0.001f));
            Assert.That(wallet.Balance, Is.EqualTo(550));
            Assert.That(interactor.TryUpgrade(TowerPart.Turret), Is.EqualTo(TowerUpgradeResult.MaxLevel));
            Assert.That(wallet.Balance, Is.EqualTo(550));

            // 塔身：血量 +20（上限與目前血量）。
            Assert.That(interactor.TryUpgrade(TowerPart.Body), Is.EqualTo(TowerUpgradeResult.Upgraded));
            Assert.That(tower.MaxHealth, Is.EqualTo(220f));
            Assert.That(tower.Health, Is.EqualTo(220f));
            Assert.That(wallet.Balance, Is.EqualTo(510));

            // 塔底：射程 +1（乘倍率 3）。
            Assert.That(interactor.TryUpgrade(TowerPart.Base), Is.EqualTo(TowerUpgradeResult.Upgraded));
            Assert.That(tower.AttackRange, Is.EqualTo(27f).Within(0.001f));
            Assert.That(wallet.Balance, Is.EqualTo(470));

            yield return null;
            Assert.That(prompt.CurrentText, Does.Contain("Lv5"));
            Assert.That(prompt.CurrentText, Does.Contain("已滿級"));

            // 離開後提示消失、射程圈隱藏。
            Teleport(player, tower.transform.position + new Vector3(40f, 1.5f, 0f));
            interactor.RefreshCurrentTower();
            yield return null;
            yield return null;
            Assert.That(interactor.CurrentTower, Is.Null);
            Assert.That(prompt.IsVisible, Is.False);
            Assert.That(tower.transform.Find("Attack Range Circle").gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator Player_StartsInFrontOfGateAndRespawnsThere()
        {
            yield return LoadLevel();

            var player = Object.FindObjectOfType<PrototypePlayerController>();
            var upperPath = Object.FindObjectsOfType<EnemyPath>().Single(path => path.PathId == "path_upper");
            var gateFront = upperPath.Waypoints[upperPath.Waypoints.Length - 1].position;
            Assert.That(player, Is.Not.Null);
            Assert.That(player.MaxHealth, Is.EqualTo(100f));
            var spawnPosition = player.transform.position;
            Assert.That(HorizontalDistance(spawnPosition, gateFront), Is.LessThan(15f), "玩家應從黃銅閘門前出發。");

            // 站在地面上（不會掉到下層或穿過地面）。
            Time.timeScale = 5f;
            yield return new WaitForSeconds(1f);
            Assert.That(Mathf.Abs(player.transform.position.y - spawnPosition.y), Is.LessThan(0.5f));
            spawnPosition = player.transform.position;

            Teleport(player, spawnPosition + new Vector3(0f, 0f, -20f));
            player.TakeDamage(100f);
            Assert.That(player.IsAlive, Is.False);
            yield return WaitUntil(() => player.IsAlive, 10f);
            Assert.That(HorizontalDistance(player.transform.position, spawnPosition), Is.LessThan(1f), "應在出發點重生。");
            Assert.That(player.CurrentHealth, Is.EqualTo(100f));
        }

        [UnityTest]
        public IEnumerator Scene_HasFollowCameraMinimapAndSpawnSmoke()
        {
            yield return LoadLevel();

            var mainCamera = Camera.main;
            Assert.That(mainCamera, Is.Not.Null);
            Assert.That(mainCamera.GetComponent<PrototypeCameraFollow>(), Is.Not.Null);
            Assert.That(mainCamera.orthographic, Is.False);

            var minimap = Object.FindObjectOfType<MinimapView>();
            Assert.That(minimap, Is.Not.Null);
            var minimapCamera = minimap.GetComponent<Camera>();
            Assert.That(minimapCamera, Is.Not.SameAs(mainCamera));
            Assert.That(minimapCamera.GetComponent<TopDownMapCamera>(), Is.Not.Null);
            Assert.That(minimapCamera.depth, Is.GreaterThan(mainCamera.depth));
            Assert.That(minimapCamera.rect.xMax, Is.GreaterThan(0.95f), "小地圖應在右側。");
            Assert.That(minimapCamera.rect.yMax, Is.GreaterThan(0.95f), "小地圖應在上方。");
            Assert.That(minimapCamera.rect.width, Is.LessThan(0.6f));
            Assert.That(minimapCamera.pixelRect.width, Is.EqualTo(minimapCamera.pixelRect.height).Within(1.5f), "小地圖應為正方形。");

            var smoke = GameObject.Find("Spawn_SouthWest/SpawnSmoke").GetComponent<ParticleSystem>();
            Assert.That(smoke.isPlaying, Is.True);
            Assert.That(smoke.particleCount, Is.GreaterThan(20), "煙霧應預熱，一開始就有大量粒子。");
        }

        [UnityTest]
        public IEnumerator RangeIndicators_ProjectToGroundAndTurnRedWhenEnemyInRange()
        {
            yield return LoadLevel();

            var playerAttack = Object.FindObjectOfType<PrototypePlayerAttack>();
            var player = Object.FindObjectOfType<PrototypePlayerController>();
            var playerIndicator = player.transform.Find("Player Attack Range Circle").GetComponent<GroundRangeIndicator>();
            Assert.That(playerAttack.AttackRange, Is.EqualTo(4.5f).Within(0.001f), "玩家射程 3 × 1.5。");
            Assert.That(playerIndicator.Radius, Is.EqualTo(4.5f).Within(0.001f));
            Assert.That(playerIndicator.SampleCount, Is.EqualTo(25));
            Assert.That(playerIndicator.GroundPoints.Count, Is.EqualTo(25));
            Assert.That(playerIndicator.IsAlert, Is.False, "開場沒有敵人，射程圈為白色。");
            Assert.That(playerIndicator.CurrentColor, Is.EqualTo(Color.white));

            // 玩家站在地面上：投影點應在玩家腳下附近，而不是玩家中心高度。
            var feet = player.GetComponent<CharacterController>().bounds.min.y;
            foreach (var point in playerIndicator.GroundPoints)
            {
                Assert.That(Mathf.Abs(point.y - feet), Is.LessThan(1f), $"投影點 {point} 應貼近地面（腳底 {feet:0.00}）。");
            }

            var towers = Object.FindObjectsOfType<FixedTowerCombat>();
            var indicators = new Dictionary<FixedTowerCombat, GroundRangeIndicator>();
            foreach (var tower in towers)
            {
                tower.SetRangeVisible(true);
                indicators[tower] = tower.transform.Find("Attack Range Circle").GetComponent<GroundRangeIndicator>();
            }

            yield return null;
            foreach (var pair in indicators)
            {
                Assert.That(pair.Value.Radius, Is.EqualTo(pair.Key.AttackRange).Within(0.001f));
                Assert.That(pair.Value.GroundPoints.Count, Is.EqualTo(25));
                Assert.That(pair.Value.IsAlert, Is.False);
            }

            // 等到有塔鎖定敵人：該塔的射程圈應轉為紅色。
            Object.FindObjectOfType<BaseCore>().GetComponent<Vitality>().Initialize(100000f);
            Time.timeScale = 20f;
            FixedTowerCombat engaged = null;
            yield return WaitUntil(() => (engaged = towers.FirstOrDefault(t => t.CurrentTarget != null)) != null, 60f);
            Assert.That(indicators[engaged].IsAlert, Is.True, "射程內有敵人時應為紅色。");
            Assert.That(indicators[engaged].CurrentColor, Is.Not.EqualTo(Color.white));

            // 把玩家移到敵人旁邊：玩家射程圈轉為紅色。
            Time.timeScale = 1f;
            var enemy = EnemyAgent.Active.First(agent => agent != null && agent.IsAlive);
            Teleport(player, enemy.transform.position + Vector3.right * 2f + Vector3.up);
            yield return null;
            yield return null;
            Assert.That(playerAttack.HasEnemyInRange, Is.True);
            Assert.That(playerIndicator.IsAlert, Is.True);
        }

        private static IEnumerator LoadLevel()
        {
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                ScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Ignore("此測試只能在編輯器中執行。");
            yield break;
#endif
            yield return null;
            yield return null;
        }

        private static IEnumerator WaitUntil(System.Func<bool> condition, float realSeconds)
        {
            var deadline = Time.realtimeSinceStartup + realSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(condition(), Is.True, "等待逾時。");
        }

        private static void Teleport(PrototypePlayerController player, Vector3 position)
        {
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = position;
            controller.enabled = true;
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
