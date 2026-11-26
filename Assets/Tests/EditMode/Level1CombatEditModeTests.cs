using System.Collections.Generic;
using CyberWizard.Gameplay.Towers;
using CyberWizard.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CyberWizard.Tests
{
    /// <summary>固定防禦塔數值表、路標箭頭網格與鉚釘圓錐網格（Ref/開發規劃4.docx）。</summary>
    public sealed class Level1CombatEditModeTests
    {
        [Test]
        public void FixedTowerProfile_DefaultsMatchPlanningDocument()
        {
            var profile = ScriptableObject.CreateInstance<FixedTowerProfile>();
            try
            {
                Assert.That(profile.GetMaxHealth(1), Is.EqualTo(200f));
                Assert.That(profile.GetDamage(1), Is.EqualTo(10f));
                Assert.That(profile.GetShotsPerSecond(1), Is.EqualTo(1f));
                Assert.That(profile.RangeMultiplier, Is.EqualTo(3f));
                Assert.That(profile.GetAttackRange(1), Is.EqualTo(24f).Within(0.0001f));

                foreach (TowerPart part in System.Enum.GetValues(typeof(TowerPart)))
                {
                    Assert.That(profile.GetMaxLevel(part), Is.EqualTo(5), part.ToString());
                    var costs = new List<int>();
                    for (var level = 1; level < 5; level++)
                    {
                        Assert.That(profile.TryGetUpgradeCost(part, level, out var cost), Is.True);
                        costs.Add(cost);
                    }

                    Assert.That(costs, Is.EqualTo(new[] { 40, 80, 130, 200 }), part.ToString());
                    Assert.That(profile.TryGetUpgradeCost(part, 5, out _), Is.False, "5 級已滿級。");
                }

                // 每一級的累計值：塔底 +1/+1.1/+1.3/+1.6（再乘 3）、塔身 +20/+30/+60/+90、
                // 砲塔攻擊 +4/+6/+8/+12、攻速 +0.2/+0.2/+0.4/+0.8。
                var expectedRange = new[] { 8f, 9f, 10.1f, 11.4f, 13f };
                var expectedHealth = new[] { 200f, 220f, 250f, 310f, 400f };
                var expectedDamage = new[] { 10f, 14f, 20f, 28f, 40f };
                var expectedRate = new[] { 1f, 1.2f, 1.4f, 1.8f, 2.6f };
                for (var level = 1; level <= 5; level++)
                {
                    Assert.That(profile.GetAttackRange(level), Is.EqualTo(expectedRange[level - 1] * 3f).Within(0.0001f));
                    Assert.That(profile.GetMaxHealth(level), Is.EqualTo(expectedHealth[level - 1]).Within(0.0001f));
                    Assert.That(profile.GetDamage(level), Is.EqualTo(expectedDamage[level - 1]).Within(0.0001f));
                    Assert.That(profile.GetShotsPerSecond(level), Is.EqualTo(expectedRate[level - 1]).Within(0.0001f));
                }

                // 超出範圍的等級會被夾住，不會越界。
                Assert.That(profile.GetDamage(9), Is.EqualTo(40f).Within(0.0001f));
                Assert.That(profile.GetDamage(0), Is.EqualTo(10f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void ChevronStrip_PlacesFlatUpwardArrowsAlongPolyline()
        {
            var points = new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(0f, 1f, 10f),
                new Vector3(10f, 1f, 10f)
            };
            var settings = ChevronStrip.Settings.Default;
            settings.Spacing = 2f;
            settings.Lift = 0.1f;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();

            var count = ChevronStrip.Build(points, settings, vertices, triangles);

            // 總長 20，第一個在 1，之後每 2 一個：1, 3, ..., 19，共 10 個。
            Assert.That(count, Is.EqualTo(10));
            Assert.That(vertices.Count, Is.EqualTo(count * 6));
            Assert.That(triangles.Count, Is.EqualTo(count * 12));

            for (var index = 0; index < triangles.Count; index += 3)
            {
                var a = vertices[triangles[index]];
                var b = vertices[triangles[index + 1]];
                var c = vertices[triangles[index + 2]];
                Assert.That(Vector3.Cross(b - a, c - a).y, Is.GreaterThan(0f), "箭頭正面應朝上。");
            }

            // 第一段往 +Z：尖端（頂點 0）在中心前方；高度沿線段內插再加上 Lift。
            var firstTip = vertices[0];
            Assert.That(firstTip.x, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(firstTip.z, Is.EqualTo(1f + settings.Length * 0.5f).Within(0.0001f));
            Assert.That(firstTip.y, Is.EqualTo(0.1f + 0.1f).Within(0.0001f));

            // 最後一個在第二段（往 +X）：尖端朝 +X。
            var lastTip = vertices[(count - 1) * 6];
            Assert.That(lastTip.x, Is.EqualTo(9f + settings.Length * 0.5f).Within(0.0001f));
            Assert.That(lastTip.z, Is.EqualTo(10f).Within(0.0001f));
        }

        [Test]
        public void ChevronStrip_OffsetShiftsArrowsForwardAndWrapsEverySpacing()
        {
            var points = new[] { Vector3.zero, new Vector3(0f, 0f, 10f) };
            var settings = ChevronStrip.Settings.Default;
            settings.Spacing = 2f;
            settings.Lift = 0f;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();

            // 偏移 0.5：第一個中心從 1 移到 1.5。
            settings.Offset = 0.5f;
            ChevronStrip.Build(points, settings, vertices, triangles);
            Assert.That(vertices[0].z, Is.EqualTo(1.5f + settings.Length * 0.5f).Within(0.0001f));

            // 偏移 1.5：1 + 1.5 = 2.5，以間距 2 循環後為 0.5（新箭頭從起點冒出）。
            settings.Offset = 1.5f;
            ChevronStrip.Build(points, settings, vertices, triangles);
            Assert.That(vertices[0].z, Is.EqualTo(0.5f + settings.Length * 0.5f).Within(0.0001f));

            // 偏移剛好一個間距時與偏移 0 相同。
            settings.Offset = 2f;
            var countAtSpacing = ChevronStrip.Build(points, settings, vertices, triangles);
            var firstAtSpacing = vertices[0];
            settings.Offset = 0f;
            var countAtZero = ChevronStrip.Build(points, settings, vertices, triangles);
            Assert.That(countAtSpacing, Is.EqualTo(countAtZero));
            Assert.That(firstAtSpacing.z, Is.EqualTo(vertices[0].z).Within(0.0001f));
        }

        [Test]
        public void GroundRangeIndicator_ProjectsSamplesOntoGroundAndSkipsActors()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var actor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                // 地面頂面 y = 2。
                ground.transform.position = new Vector3(0f, 1.5f, 0f);
                ground.transform.localScale = new Vector3(40f, 1f, 40f);
                // +X 方向的取樣點（10, z = 0）上有一個頂面 y = 5 的方塊。
                block.transform.position = new Vector3(10f, 4f, 0f);
                block.transform.localScale = new Vector3(2f, 2f, 2f);
                // -X 方向的取樣點上有一個可受傷的角色，應被略過。
                actor.transform.position = new Vector3(-10f, 4f, 0f);
                actor.transform.localScale = new Vector3(2f, 2f, 2f);
                actor.AddComponent<DamageableMarker>();
                Physics.SyncTransforms();

                var points = new List<Vector3>();
                GroundRangeIndicator.SampleGround(
                    Vector3.zero, 10f, 4, 20f, 60f, ~0, null, new RaycastHit[16], points);

                Assert.That(points.Count, Is.EqualTo(4));
                Assert.That(points[0].y, Is.EqualTo(5f).Within(0.001f), "+X 取樣點落在方塊頂面。");
                Assert.That(points[1].y, Is.EqualTo(2f).Within(0.001f), "+Z 取樣點落在地面。");
                Assert.That(points[2].y, Is.EqualTo(2f).Within(0.001f), "-X 取樣點略過角色，落在地面。");
                Assert.That(points[2].x, Is.EqualTo(-10f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(ground);
                Object.DestroyImmediate(block);
                Object.DestroyImmediate(actor);
            }
        }

        [Test]
        public void GroundRangeIndicator_BuildsClosedUpwardRing()
        {
            var points = new List<Vector3>();
            for (var index = 0; index < GroundRangeIndicator.DefaultSampleCount; index++)
            {
                var angle = index * Mathf.PI * 2f / GroundRangeIndicator.DefaultSampleCount;
                points.Add(new Vector3(Mathf.Cos(angle) * 6f, index % 2, Mathf.Sin(angle) * 6f));
            }

            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            GroundRangeIndicator.BuildRing(points, Vector3.zero, 0.5f, 0.2f, vertices, triangles);

            Assert.That(vertices.Count, Is.EqualTo(25 * 2));
            Assert.That(triangles.Count, Is.EqualTo(25 * 6), "25 段（含最後一點接回第一點），每段兩個三角形。");
            for (var index = 0; index < points.Count; index++)
            {
                var outer = vertices[index * 2];
                var inner = vertices[index * 2 + 1];
                Assert.That(outer.y, Is.EqualTo(points[index].y + 0.2f).Within(0.0001f), "外緣跟隨投影點高度並加上 lift。");
                Assert.That(inner.y, Is.EqualTo(outer.y).Within(0.0001f));
                Assert.That(new Vector2(inner.x, inner.z).magnitude, Is.EqualTo(5.5f).Within(0.0001f));
            }

            for (var index = 0; index < triangles.Count; index += 3)
            {
                var a = vertices[triangles[index]];
                var b = vertices[triangles[index + 1]];
                var c = vertices[triangles[index + 2]];
                Assert.That(Vector3.Cross(b - a, c - a).y, Is.GreaterThan(0f), "環帶正面應朝上。");
            }
        }

        private sealed class DamageableMarker : MonoBehaviour, CyberWizard.Core.IDamageable
        {
            public bool IsAlive => true;
            public CyberWizard.Core.Faction Faction => CyberWizard.Core.Faction.Enemy;
            public Transform TargetTransform => transform;
            public void TakeDamage(float amount) { }
        }

        [Test]
        public void ChevronStrip_ReturnsNothingForDegeneratePath()
        {
            var vertices = new List<Vector3> { Vector3.one };
            var triangles = new List<int> { 0 };
            Assert.That(ChevronStrip.Build(new[] { Vector3.zero }, ChevronStrip.Settings.Default, vertices, triangles), Is.EqualTo(0));
            Assert.That(vertices, Is.Empty);
            Assert.That(triangles, Is.Empty);
        }

        [Test]
        public void ConeMesh_PointsAlongPositiveZWithOutwardFaces()
        {
            var mesh = ConeMesh.Create(12);
            try
            {
                var bounds = mesh.bounds;
                Assert.That(bounds.min.z, Is.EqualTo(-0.5f).Within(0.0001f));
                Assert.That(bounds.max.z, Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(bounds.extents.x, Is.EqualTo(0.5f).Within(0.01f));

                var vertices = mesh.vertices;
                var normals = mesh.normals;
                var triangles = mesh.triangles;
                Assert.That(triangles.Length, Is.EqualTo(12 * 6));
                for (var index = 0; index < triangles.Length; index += 3)
                {
                    var a = vertices[triangles[index]];
                    var b = vertices[triangles[index + 1]];
                    var c = vertices[triangles[index + 2]];
                    var faceNormal = Vector3.Cross(b - a, c - a);
                    var centroid = (a + b + c) / 3f;

                    // 正面法線朝外（遠離圓錐中心軸或朝底面外），且與頂點法線同向。
                    var outward = centroid.z <= -0.4999f ? Vector3.back : new Vector3(centroid.x, centroid.y, 0f);
                    Assert.That(Vector3.Dot(faceNormal, outward), Is.GreaterThan(0f), $"三角形 {index / 3} 朝內。");
                    Assert.That(Vector3.Dot(faceNormal, normals[triangles[index]]), Is.GreaterThan(0f));
                }
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }
    }
}
