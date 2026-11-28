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
                // 實際顏色來自 MaterialPropertyBlock：材質顏色 × 發光強度（HDR），透明度沿用材質。
                var block = new MaterialPropertyBlock();
                strip.GetPropertyBlock(block);
                var color = block.GetColor("_BaseColor");
                var expected = preview.GlowColor;
                Assert.That(((Vector4)color - (Vector4)expected).magnitude, Is.LessThan(0.001f), $"實際 {color}，預期 {expected}。");
                Assert.That(color.r, Is.GreaterThan(1f), "箭頭紅色分量應超過 Bloom 門檻 1 才會發光。");
                Assert.That(color.a, Is.EqualTo(strip.sharedMaterial.GetColor("_BaseColor").a).Within(0.001f));
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
            // 數值以場景使用的 FixedTowerProfile 資產為準，不寫死，調整資產時測試不必跟著改。
            foreach (var tower in towers)
            {
                var profile = tower.Profile;
                Assert.That(profile, Is.Not.Null, $"{tower.name} 缺少 FixedTowerProfile。");
                Assert.That(tower.MaxHealth, Is.EqualTo(profile.GetMaxHealth(1)).Within(0.001f));
                Assert.That(tower.Damage, Is.EqualTo(profile.GetDamage(1)).Within(0.001f));
                Assert.That(tower.ShotsPerSecond, Is.EqualTo(profile.GetShotsPerSecond(1)).Within(0.001f));
                Assert.That(tower.AttackRange, Is.EqualTo(profile.GetAttackRange(1)).Within(0.001f), "企劃射程 × 倍率。");
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
            var highlighter = Object.FindObjectOfType<TowerOutlineHighlighter>();
            var cameraFollow = Object.FindObjectOfType<PrototypeCameraFollow>();
            var wallet = Object.FindObjectOfType<ResourceWallet>();
            var player = Object.FindObjectOfType<PrototypePlayerController>();
            var tower = Object.FindObjectsOfType<FixedTowerCombat>().Single(item => item.TowerNumber == 5);
            Assert.That(interactor, Is.Not.Null);
            Assert.That(prompt, Is.Not.Null);
            Assert.That(highlighter, Is.Not.Null);
            Assert.That(cameraFollow, Is.Not.Null);

            // 玩家在閘門前時不在任何塔的升級範圍內。
            interactor.RefreshCurrentTower();
            Assert.That(interactor.CurrentTower, Is.Null);
            yield return null;
            Assert.That(prompt.IsVisible, Is.False);

            // 靠近但視角沒有指向塔：不選取、不顯示提示與外框。
            Teleport(player, tower.transform.position + new Vector3(8f, 1.5f, 0f));
            cameraFollow.LookAt(player.transform.position + new Vector3(0f, 0f, 10f));
            interactor.RefreshCurrentTower();
            Assert.That(interactor.CurrentTower, Is.Null, "視角沒有指向塔時不應選取。");
            yield return null;
            Assert.That(prompt.IsVisible, Is.False);
            Assert.That(highlighter.IsOutlineVisible(tower), Is.False);

            // 靠近且視角指向塔：顯示提示與外框。
            cameraFollow.LookAt(tower.transform.position + Vector3.up * 4f);
            interactor.RefreshCurrentTower();
            Assert.That(interactor.CurrentTower, Is.SameAs(tower));
            yield return null;
            Assert.That(prompt.IsVisible, Is.True);
            Assert.That(prompt.ShownTower, Is.SameAs(tower));
            Assert.That(prompt.CurrentText, Does.Contain("5號塔"));
            Assert.That(highlighter.HighlightedTower, Is.SameAs(tower));
            Assert.That(highlighter.IsOutlineVisible(tower), Is.True, "被指向的塔應顯示外框。");
            Assert.That(tower.transform.Find("Attack Range Circle").gameObject.activeSelf, Is.True);

            // 能量不足。
            Assert.That(interactor.TryUpgrade(TowerPart.Turret), Is.EqualTo(TowerUpgradeResult.NotEnoughEnergy));
            Assert.That(tower.GetLevel(TowerPart.Turret), Is.EqualTo(1));

            wallet.Add(1000);
            interactor.RefreshCurrentTower();

            // 期望值由場景使用的 FixedTowerProfile 資產計算，不寫死數值。
            var profile = tower.Profile;
            var expectedBalance = 1000;
            var maxTurretLevel = profile.GetMaxLevel(TowerPart.Turret);
            Assert.That(maxTurretLevel, Is.GreaterThan(1), "砲塔至少要能升一級，測試才有意義。");

            // 砲塔：每次升級扣除對應花費，攻擊與攻速依升級表增加。
            for (var level = 1; level < maxTurretLevel; level++)
            {
                Assert.That(profile.TryGetUpgradeCost(TowerPart.Turret, level, out var cost), Is.True);
                Assert.That(interactor.TryUpgrade(TowerPart.Turret), Is.EqualTo(TowerUpgradeResult.Upgraded));
                expectedBalance -= cost;
                Assert.That(tower.GetLevel(TowerPart.Turret), Is.EqualTo(level + 1));
                Assert.That(tower.Damage, Is.EqualTo(profile.GetDamage(level + 1)).Within(0.001f));
                Assert.That(tower.ShotsPerSecond, Is.EqualTo(profile.GetShotsPerSecond(level + 1)).Within(0.001f));
                Assert.That(wallet.Balance, Is.EqualTo(expectedBalance));
            }

            Assert.That(interactor.TryUpgrade(TowerPart.Turret), Is.EqualTo(TowerUpgradeResult.MaxLevel));
            Assert.That(wallet.Balance, Is.EqualTo(expectedBalance));

            // 塔身：血量上限與目前血量一起增加。
            Assert.That(profile.TryGetUpgradeCost(TowerPart.Body, 1, out var bodyCost), Is.True);
            Assert.That(interactor.TryUpgrade(TowerPart.Body), Is.EqualTo(TowerUpgradeResult.Upgraded));
            expectedBalance -= bodyCost;
            Assert.That(tower.MaxHealth, Is.EqualTo(profile.GetMaxHealth(2)).Within(0.001f));
            Assert.That(tower.Health, Is.EqualTo(profile.GetMaxHealth(2)).Within(0.001f));
            Assert.That(wallet.Balance, Is.EqualTo(expectedBalance));

            // 塔底：射程增加（已乘倍率）。
            Assert.That(profile.TryGetUpgradeCost(TowerPart.Base, 1, out var baseCost), Is.True);
            Assert.That(interactor.TryUpgrade(TowerPart.Base), Is.EqualTo(TowerUpgradeResult.Upgraded));
            expectedBalance -= baseCost;
            Assert.That(tower.AttackRange, Is.EqualTo(profile.GetAttackRange(2)).Within(0.001f));
            Assert.That(wallet.Balance, Is.EqualTo(expectedBalance));

            yield return null;
            Assert.That(prompt.CurrentText, Does.Contain($"Lv{maxTurretLevel}"));
            Assert.That(prompt.CurrentText, Does.Contain("已滿級"));

            // 離開後提示消失、射程圈隱藏。
            Teleport(player, tower.transform.position + new Vector3(40f, 1.5f, 0f));
            interactor.RefreshCurrentTower();
            yield return null;
            yield return null;
            Assert.That(interactor.CurrentTower, Is.Null);
            Assert.That(prompt.IsVisible, Is.False);
            Assert.That(highlighter.IsOutlineVisible(tower), Is.False, "離開後外框應消失。");
            Assert.That(tower.transform.Find("Attack Range Circle").gameObject.activeSelf, Is.True, "射程圈持續顯示，玩家離開後仍在。");
        }

        [UnityTest]
        public IEnumerator Upgrade_RequiresAimingAtNearbyTowerAndShowsOutline()
        {
            yield return LoadLevel();

            var interactor = Object.FindObjectOfType<TowerUpgradeInteractor>();
            var prompt = Object.FindObjectOfType<TowerUpgradePrompt>();
            var highlighter = Object.FindObjectOfType<TowerOutlineHighlighter>();
            var cameraFollow = Object.FindObjectOfType<PrototypeCameraFollow>();
            var player = Object.FindObjectOfType<PrototypePlayerController>();
            var tower = Object.FindObjectsOfType<FixedTowerCombat>().Single(item => item.TowerNumber == 5);
            Assert.That(highlighter, Is.Not.Null, "場景應有 TowerOutlineHighlighter。");
            Assert.That(cameraFollow, Is.Not.Null);

            // 遠處（水平距離 30）即使視角指向塔也不選取。
            Teleport(player, tower.transform.position + new Vector3(30f, 1.5f, 0f));
            cameraFollow.LookAt(tower.transform.position + Vector3.up * 4f);
            interactor.RefreshCurrentTower();
            Assert.That(interactor.CurrentTower, Is.Null, "超出升級範圍時不應選取。");

            // 靠近但視角轉開：不選取。
            Teleport(player, tower.transform.position + new Vector3(8f, 1.5f, 0f));
            cameraFollow.LookAt(player.transform.position + new Vector3(0f, 0f, 10f));
            interactor.RefreshCurrentTower();
            Assert.That(interactor.CurrentTower, Is.Null, "視角沒有指向塔時不應選取。");
            yield return null;
            Assert.That(prompt.IsVisible, Is.False);
            Assert.That(highlighter.IsOutlineVisible(tower), Is.False);

            // 靠近且視角指向塔：提示與外框出現，外框掛在基座、塔身、砲管底下。
            cameraFollow.LookAt(tower.transform.position + Vector3.up * 4f);
            interactor.RefreshCurrentTower();
            Assert.That(interactor.CurrentTower, Is.SameAs(tower));
            yield return null;
            Assert.That(prompt.IsVisible, Is.True);
            Assert.That(highlighter.HighlightedTower, Is.SameAs(tower));
            Assert.That(highlighter.IsOutlineVisible(tower), Is.True);
            var outlines = tower.GetComponentsInChildren<MeshRenderer>()
                .Where(renderer => renderer.name == "Selection Outline")
                .ToArray();
            Assert.That(outlines.Length, Is.EqualTo(3), "基座、塔身、砲管各一個外框。");
            Assert.That(
                outlines.All(renderer => renderer.sharedMaterials.Select(material => material.shader.name)
                    .SequenceEqual(new[] { "CyberWizard/TowerOutlineMask", "CyberWizard/TowerOutline" })),
                Is.True,
                "外框物件應先畫 Stencil 遮罩、再畫外殼。");
            Assert.That(
                outlines[0].sharedMaterials[0].renderQueue,
                Is.LessThan(outlines[0].sharedMaterials[1].renderQueue),
                "遮罩的渲染佇列必須早於外殼。");
            Assert.That(outlines.All(renderer => renderer.GetComponent<Collider>() == null), Is.True, "外框不應有碰撞體。");
            var smoothNormals = new List<Vector3>();
            outlines[0].GetComponent<MeshFilter>().sharedMesh.GetUVs(3, smoothNormals);
            Assert.That(smoothNormals.Count, Is.GreaterThan(0), "外框網格應在 UV3 寫入平滑法線。");

            // 其他塔沒有外框。
            foreach (var other in Object.FindObjectsOfType<FixedTowerCombat>().Where(item => item != tower))
            {
                Assert.That(highlighter.IsOutlineVisible(other), Is.False);
            }

            // 視角再轉開：提示與外框消失。
            cameraFollow.LookAt(player.transform.position + new Vector3(0f, 0f, 10f));
            interactor.RefreshCurrentTower();
            yield return null;
            Assert.That(interactor.CurrentTower, Is.Null);
            Assert.That(prompt.IsVisible, Is.False);
            Assert.That(highlighter.IsOutlineVisible(tower), Is.False);
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
            Assert.That(smoke.particleCount, Is.GreaterThan(40), "煙霧應預熱，一開始就有大量粒子。");
            // 2026-11-26：範圍與粒子尺寸放大為 2 倍（半徑 9→18、尺寸 5～10→10～20），每秒生成數加倍（22→44）。
            Assert.That(smoke.shape.radius, Is.EqualTo(18f).Within(0.001f));
            Assert.That(smoke.main.startSize.constantMin, Is.EqualTo(10f).Within(0.001f));
            Assert.That(smoke.main.startSize.constantMax, Is.EqualTo(20f).Within(0.001f));
            Assert.That(smoke.emission.rateOverTime.constant, Is.EqualTo(44f).Within(0.001f));
            Assert.That(smoke.main.maxParticles, Is.EqualTo(600));

            // 道路改為單一 RoadNetwork 網格，舊的六段方塊道路已移除。
            var roads = GameObject.Find("Roads").transform;
            Assert.That(roads.childCount, Is.EqualTo(1));
            var network = roads.GetChild(0).GetComponent<CyberWizard.Gameplay.Level.RoadNetwork>();
            Assert.That(network, Is.Not.Null);
            Assert.That(network.Roads.Count, Is.EqualTo(4));
            Assert.That(network.GetComponent<MeshFilter>().sharedMesh.vertexCount, Is.GreaterThan(16));
            Assert.That(network.GetComponent<MeshRenderer>().sharedMaterial.name, Is.EqualTo("Env_RoadCobblestone"));
        }

        [UnityTest]
        public IEnumerator RangeIndicators_ProjectToGroundAndTurnRedWhenEnemyInRange()
        {
            yield return LoadLevel();

            var playerAttack = Object.FindObjectOfType<PrototypePlayerAttack>();
            var player = Object.FindObjectOfType<PrototypePlayerController>();
            var playerIndicator = player.transform.Find("Player Attack Range Circle").GetComponent<GroundRangeIndicator>();
            Assert.That(playerAttack.AttackRange, Is.EqualTo(9f).Within(0.001f), "玩家射程 3 × 1.5 × 2。");
            Assert.That(playerIndicator.Radius, Is.EqualTo(9f).Within(0.001f));
            Assert.That(playerIndicator.SampleCount, Is.EqualTo(25));
            Assert.That(playerIndicator.Width, Is.EqualTo(2f).Within(0.001f), "射程圈寬度統一為 2。");
            Assert.That(playerIndicator.GroundPoints.Count, Is.EqualTo(25));
            Assert.That(playerIndicator.IsAlert, Is.False, "開場沒有敵人，射程圈為白色。");
            Assert.That(playerIndicator.CurrentColor, Is.EqualTo(Color.white));

            // 玩家站在地面上：投影點應在玩家腳下附近，而不是玩家中心高度。
            var feet = player.GetComponent<CharacterController>().bounds.min.y;
            foreach (var point in playerIndicator.GroundPoints)
            {
                Assert.That(Mathf.Abs(point.y - feet), Is.LessThan(1f), $"投影點 {point} 應貼近地面（腳底 {feet:0.00}）。");
            }

            // 房屋不作為投影地面：所有房屋碰撞體都在標記 RangeIndicatorIgnored 的群組底下。
            var buildings = GameObject.Find("Buildings");
            Assert.That(buildings, Is.Not.Null);
            Assert.That(buildings.GetComponent<RangeIndicatorIgnored>(), Is.Not.Null, "Buildings 應標記為射程圈投影略過。");
            Assert.That(buildings.GetComponentsInChildren<Collider>().Length, Is.GreaterThan(0));

            // 防區查詢：每座塔都在同編號的防區內，閘門前不在任何防區內。
            var zones = Object.FindObjectsOfType<CyberWizard.Gameplay.Level.DefenseZone>();
            Assert.That(zones.Length, Is.EqualTo(3));
            foreach (var combat in Object.FindObjectsOfType<FixedTowerCombat>())
            {
                var zone = zones.Single(item => item.ZoneNumber == combat.TowerNumber);
                Assert.That(zone.ContainsPoint(combat.transform.position), Is.True, $"{combat.name} 應在防區 {zone.ZoneNumber} 內。");
            }

            var gatePosition = Object.FindObjectOfType<BaseCore>().transform.position;
            Assert.That(zones.Any(zone => zone.ContainsPoint(gatePosition)), Is.False, "閘門不在任何防區內。");

            // 塔的射程圈不需要玩家靠近，開場就持續顯示。
            var towers = Object.FindObjectsOfType<FixedTowerCombat>();
            var indicators = new Dictionary<FixedTowerCombat, GroundRangeIndicator>();
            foreach (var tower in towers)
            {
                var circle = tower.transform.Find("Attack Range Circle");
                Assert.That(circle.gameObject.activeInHierarchy, Is.True, $"{tower.name} 的射程圈應持續顯示。");
                indicators[tower] = circle.GetComponent<GroundRangeIndicator>();
            }

            yield return null;
            foreach (var pair in indicators)
            {
                Assert.That(pair.Value.Radius, Is.EqualTo(pair.Key.AttackRange).Within(0.001f));
                Assert.That(pair.Value.Width, Is.EqualTo(2f).Within(0.001f), "射程圈寬度統一為 2。");
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
            Teleport(player, enemy.transform.position + Vector3.right * 4f + Vector3.up);
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
