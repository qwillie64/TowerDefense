using System.Collections;
using System.Linq;
using CyberWizard.Core;
using CyberWizard.Gameplay.Combat;
using CyberWizard.Gameplay.Enemies;
using CyberWizard.Gameplay.GameFlow;
using CyberWizard.Gameplay.Player;
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
    public sealed class Level1GateDefensePlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/Level_1.unity";
        private const float TimeScale = 20f;
        private const float RealTimeLimit = 120f;

        [UnityTest]
        public IEnumerator FragmentCleanup_RemovesFragmentsBelowKillHeightAndShrinksTheRest()
        {
            var root = new GameObject("TestFragments");
            var fallen = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var kept = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                fallen.transform.SetParent(root.transform, false);
                fallen.transform.position = new Vector3(0f, -100f, 0f);
                fallen.AddComponent<Rigidbody>().isKinematic = true;
                kept.transform.SetParent(root.transform, false);
                kept.transform.position = new Vector3(0f, 0f, 0f);
                kept.AddComponent<Rigidbody>().isKinematic = true;

                var cleanup = root.AddComponent<ShatterFragmentCleanup>();
                cleanup.Initialize(0.5f, 0.5f, -50f);
                yield return null;
                yield return null;

                Assert.That(fallen == null, Is.True, "低於清除高度的碎片應立即移除。");
                Assert.That(kept != null, Is.True, "保留時間內的碎片不應移除。");
                Assert.That(cleanup.RemainingCount, Is.EqualTo(1));

                var deadline = Time.realtimeSinceStartup + 5f;
                var sawShrink = false;
                while (root != null && Time.realtimeSinceStartup < deadline)
                {
                    if (kept != null && kept.transform.localScale.x < 0.99f)
                    {
                        sawShrink = true;
                    }

                    yield return null;
                }

                Assert.That(sawShrink, Is.True, "移除前碎片應先縮小。");
                Assert.That(root == null, Is.True, "縮小完成後應移除碎片父物件。");
            }
            finally
            {
                if (root != null)
                {
                    Object.Destroy(root);
                }
            }
        }

        [UnityTest]
        public IEnumerator Level1_MinionsClimbToGateBreakItAndThenEverythingStops()
        {
#if UNITY_EDITOR
            // Level_1 尚未加入 Build Settings，所以用編輯器 API 依路徑載入。
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                ScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Ignore("此測試只能在編輯器中執行。");
            yield break;
#endif
            yield return null;

            // 本測試驗證「無人防守時」敵人走完路線並打破閘門，所以先停用三座塔的攻擊。
            // 塔的攻擊另由 Level1CombatPlayModeTests 驗證。
            var towers = Object.FindObjectsOfType<FixedTowerCombat>();
            Assert.That(towers.Length, Is.EqualTo(3));
            foreach (var tower in towers)
            {
                tower.enabled = false;
            }

            var flow = Object.FindObjectOfType<GateDefenseFlow>();
            var waveDirector = Object.FindObjectOfType<WaveDirector>();
            var gate = Object.FindObjectOfType<BaseCore>();
            var door = GameObject.Find("Gate_Door");
            var shatter = door.GetComponent<GateShatterOnDestroyed>();
            Assert.That(shatter, Is.Not.Null, "黃銅大門應有破碎效果元件。");
            Assert.That(door.GetComponent<Rigidbody>().isKinematic, Is.True, "破碎前大門應固定不動。");
            var doorCenter = door.transform.position;
            var upperPath = Object.FindObjectsOfType<EnemyPath>().FirstOrDefault(path => path.PathId == "path_upper");
            Assert.That(flow, Is.Not.Null);
            Assert.That(waveDirector, Is.Not.Null);
            Assert.That(gate, Is.Not.Null);
            Assert.That(upperPath, Is.Not.Null);
            Assert.That(flow.CurrentPhase, Is.EqualTo(GamePhase.Wave), flow.FailureReason);
            Assert.That(waveDirector.TotalWaveCount, Is.EqualTo(2));
            Assert.That(gate.MaxHealth, Is.EqualTo(100f));

            var rightPath = Object.FindObjectsOfType<EnemyPath>().FirstOrDefault(path => path.PathId == "path_right");
            Assert.That(rightPath, Is.Not.Null);

            var gateFront = upperPath.Waypoints[upperPath.Waypoints.Length - 1].position;
            // 兩條路線的 WP_00 都是左下角路口；path_right 的 WP_01 是往右走到第二條道路（東側斜路）的轉彎點。
            var southWestJunction = upperPath.Waypoints[0].position;
            var rightTurn = rightPath.Waypoints[1].position;
            var seenEnemies = new System.Collections.Generic.HashSet<EnemyAgent>();
            var previousTimeScale = Time.timeScale;
            var player = Object.FindObjectOfType<PrototypePlayerController>();
            var cameraFollow = Object.FindObjectOfType<PrototypeCameraFollow>();
            Time.timeScale = TimeScale;
            try
            {
                var deadline = Time.realtimeSinceStartup + RealTimeLimit;
                var maxGroundGap = 0f;
                var maxSpawnDistance = 0f;
                var lowestY = float.MaxValue;
                var reachedRightTurn = false;
                while (flow.CurrentPhase == GamePhase.Wave && Time.realtimeSinceStartup < deadline)
                {
                    foreach (var enemy in waveDirector.AliveEnemies)
                    {
                        if (enemy == null)
                        {
                            continue;
                        }

                        maxGroundGap = Mathf.Max(maxGroundGap, MeasureGroundGap(enemy));

                        var position = enemy.transform.position;
                        if (seenEnemies.Add(enemy))
                        {
                            // 第一次看到的位置：剛生成，應在左下角路口附近。
                            maxSpawnDistance = Mathf.Max(maxSpawnDistance, HorizontalDistance(position, southWestJunction));
                        }

                        lowestY = Mathf.Min(lowestY, position.y);
                        if (HorizontalDistance(position, rightTurn) < 2f)
                        {
                            reachedRightTurn = true;
                        }
                    }

                    yield return null;
                }

                Assert.That(flow.CurrentPhase, Is.EqualTo(GamePhase.Defeat), "時限內閘門沒有被打破。");
                Assert.That(gate.IsAlive, Is.False);

                // 失敗瞬間遊戲時間放慢為 0.25 倍，玩家操作與滑鼠視角維持啟用。
                Assert.That(Time.timeScale, Is.EqualTo(0.25f).Within(0.0001f), "失敗瞬間應放慢為 0.25 倍。");
                Assert.That(player.InputEnabled, Is.True, "結束後不再停用玩家操作。");
                Assert.That(cameraFollow.GameplayActive, Is.True, "結束後滑鼠視角維持啟用。");

                // 以下檢查碎片落地等物理結果，恢復加速以縮短測試時間。
                Time.timeScale = TimeScale;
                Assert.That(GameObject.Find("Gate_Door"), Is.Null, "閘門破壞後原本的門應停用。");
                Assert.That(GameObject.Find("Gate_Barrier"), Is.Null, "門前護欄仍依原設定隱藏。");

                // OpenFracture 破碎：碎片為非 Kinematic 剛體，帶凸面 MeshCollider。
                Assert.That(shatter.IsShattered, Is.True);
                Assert.That(shatter.FragmentRoot, Is.Not.Null);
                var fragments = shatter.FragmentRoot.GetComponentsInChildren<Rigidbody>();
                Assert.That(fragments.Length, Is.GreaterThanOrEqualTo(10), $"碎片數量 {fragments.Length}。");
                foreach (var fragment in fragments)
                {
                    Assert.That(fragment.isKinematic, Is.False);
                    Assert.That(fragment.GetComponent<MeshCollider>().convex, Is.True);
                    Assert.That(fragment.interpolation, Is.EqualTo(RigidbodyInterpolation.Interpolate), "慢動作中碎片應開啟插值。");
                }

                // 碎片不作為射程圈投影地面，並有清除元件。先暫停清除，檢查完落點後再恢復。
                Assert.That(shatter.FragmentRoot.GetComponent<RangeIndicatorIgnored>(), Is.Not.Null);
                var cleanup = shatter.FragmentRoot.GetComponent<ShatterFragmentCleanup>();
                Assert.That(cleanup, Is.Not.Null);
                Assert.That(cleanup.KillHeight, Is.LessThan(-15f), "清除高度應低於下層地面。");
                cleanup.enabled = false;
                Assert.That(GameObject.Find("Gate_Pillar_West"), Is.Not.Null);
                Assert.That(maxSpawnDistance, Is.LessThan(8f), "敵人應從左下角道路生成。");
                Assert.That(lowestY, Is.GreaterThan(-6f), "敵人不應進入下層。");
                Assert.That(reachedRightTurn, Is.True, "沒有敵人沿路徑二走到東側斜路的轉彎點。");
                Assert.That(maxGroundGap, Is.LessThan(0.5f), "敵人腳底與地面距離過大。");

                var aliveEnemies = waveDirector.AliveEnemies.Where(enemy => enemy != null).ToList();
                Assert.That(aliveEnemies, Is.Not.Empty);
                Assert.That(
                    aliveEnemies.Any(enemy => HorizontalDistance(enemy.transform.position, gateFront) < 2f),
                    Is.True,
                    "應有敵人停在閘門前。");

                // 失敗後：不再生成，場上敵人也不再移動。
                var spawnedAtDefeat = waveDirector.SpawnedCount;
                var positions = aliveEnemies.ToDictionary(enemy => enemy, enemy => enemy.transform.position);
                for (var frame = 0; frame < 60; frame++)
                {
                    yield return null;
                }

                Assert.That(waveDirector.SpawnedCount, Is.EqualTo(spawnedAtDefeat));
                var landed = fragments.Where(fragment => fragment != null).ToList();
                Assert.That(landed.Count, Is.EqualTo(fragments.Length), "碎片不應被銷毀。");
                Assert.That(landed.Average(fragment => fragment.worldCenterOfMass.z), Is.LessThan(doorCenter.z - 0.5f), "爆炸應把碎片往南（關內）推。");
                Assert.That(landed.Count(fragment => fragment.worldCenterOfMass.y > doorCenter.y - 20f), Is.GreaterThanOrEqualTo(fragments.Length * 0.8f), "大部分碎片應落在地圖內，而不是掉出地圖。");
                foreach (var pair in positions)
                {
                    if (pair.Key != null)
                    {
                        Assert.That(Vector3.Distance(pair.Key.transform.position, pair.Value), Is.LessThan(0.001f));
                    }
                }

                // 恢復清除：保留時間加上縮小時間後，碎片與父物件都應被移除。
                var fragmentRoot = shatter.FragmentRoot.gameObject;
                cleanup.enabled = true;
                var cleanupDeadline = Time.realtimeSinceStartup + 20f;
                while (fragmentRoot != null && Time.realtimeSinceStartup < cleanupDeadline)
                {
                    yield return null;
                }

                Assert.That(fragmentRoot == null, Is.True, "碎片應在保留時間後縮小並移除。");
            }
            finally
            {
                Time.timeScale = previousTimeScale;
            }
        }

        private static float MeasureGroundGap(EnemyAgent enemy)
        {
            var bodyRenderer = enemy.GetComponent<Renderer>();
            if (bodyRenderer == null)
            {
                return 0f;
            }

            var origin = enemy.transform.position + Vector3.up * 30f;
            var hits = Physics.RaycastAll(origin, Vector3.down, 80f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            var groundY = float.NegativeInfinity;
            var nearest = float.MaxValue;
            foreach (var hit in hits)
            {
                if (hit.collider.GetComponentInParent<EnemyAgent>() != null ||
                    hit.collider.GetComponentInParent<CombatProjectile>() != null ||
                    hit.distance >= nearest)
                {
                    continue;
                }

                nearest = hit.distance;
                groundY = hit.point.y;
            }

            return float.IsNegativeInfinity(groundY) ? 0f : Mathf.Abs(bodyRenderer.bounds.min.y - groundY);
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
