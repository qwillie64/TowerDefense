using System.Collections.Generic;
using CyberWizard.Gameplay.Level;
using NUnit.Framework;
using UnityEngine;

namespace CyberWizard.Tests
{
    public sealed class PolygonPrismTests
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector3> normals = new List<Vector3>();
        private readonly List<int> triangles = new List<int>();

        [Test]
        public void TryBuild_SquareInEitherWinding_FacesPointOutwardAndSpanDepth()
        {
            var clockwise = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(0f, 2f),
                new Vector2(2f, 2f),
                new Vector2(2f, 0f)
            };
            var counterClockwise = (Vector2[])clockwise.Clone();
            System.Array.Reverse(counterClockwise);

            foreach (var outline in new[] { clockwise, counterClockwise })
            {
                Assert.That(PolygonPrism.TryBuild(outline, 3f, vertices, normals, triangles), Is.True);

                // 頂面 4 個頂點、2 個三角形；4 面側壁各 4 個頂點、2 個三角形。
                Assert.That(vertices.Count, Is.EqualTo(4 + 4 * 4));
                Assert.That(normals.Count, Is.EqualTo(vertices.Count));
                Assert.That(triangles.Count, Is.EqualTo((2 + 4 * 2) * 3));
                AssertFacesMatchNormalsAndPointOutward(new Vector3(1f, -1.5f, 1f));

                var minY = float.MaxValue;
                var maxY = float.MinValue;
                foreach (var vertex in vertices)
                {
                    minY = Mathf.Min(minY, vertex.y);
                    maxY = Mathf.Max(maxY, vertex.y);
                }

                Assert.That(maxY, Is.EqualTo(0f));
                Assert.That(minY, Is.EqualTo(-3f));
            }
        }

        [Test]
        public void TryBuild_ZeroDepth_BuildsTopFaceOnly()
        {
            var triangle = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 0f) };

            Assert.That(PolygonPrism.TryBuild(triangle, 0f, vertices, normals, triangles), Is.True);
            Assert.That(vertices.Count, Is.EqualTo(3));
            Assert.That(triangles.Count, Is.EqualTo(3));
        }

        [Test]
        public void TryBuild_InvalidOutline_ReturnsFalseAndClearsOutput()
        {
            vertices.Add(Vector3.one);
            normals.Add(Vector3.one);
            triangles.Add(0);

            Assert.That(
                PolygonPrism.TryBuild(new[] { Vector2.zero, Vector2.one }, 1f, vertices, normals, triangles),
                Is.False);
            Assert.That(vertices, Is.Empty);
            Assert.That(normals, Is.Empty);
            Assert.That(triangles, Is.Empty);
        }

        private void AssertFacesMatchNormalsAndPointOutward(Vector3 interiorPoint)
        {
            for (var index = 0; index < triangles.Count; index += 3)
            {
                var a = vertices[triangles[index]];
                var b = vertices[triangles[index + 1]];
                var c = vertices[triangles[index + 2]];
                var declared = normals[triangles[index]];
                var face = Vector3.Cross(b - a, c - a).normalized;

                Assert.That(Vector3.Dot(face, declared), Is.GreaterThan(0.99f), "三角形正面需與宣告的法線一致。");

                var center = (a + b + c) / 3f;
                Assert.That(Vector3.Dot(center - interiorPoint, declared), Is.GreaterThan(0f), "法線需朝向柱體外側。");
            }
        }
    }
}
