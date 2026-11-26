using System.Collections.Generic;
using CyberWizard.Gameplay.Level;
using NUnit.Framework;
using UnityEngine;

namespace CyberWizard.Tests
{
    public sealed class PolygonTriangulatorTests
    {
        [Test]
        public void TryTriangulate_Square_CoversWholeAreaWithUpFacingTriangles()
        {
            var square = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(2f, 0f),
                new Vector2(2f, 2f),
                new Vector2(0f, 2f)
            };
            var triangles = new List<int>();

            Assert.That(PolygonTriangulator.TryTriangulate(square, triangles), Is.True);
            Assert.That(triangles.Count, Is.EqualTo(6));
            AssertTrianglesCoverPolygon(square, triangles);
        }

        [Test]
        public void TryTriangulate_ConcavePolygonInEitherWinding_CoversWholeArea()
        {
            // L 形凹多邊形，順時針排列。
            var clockwise = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(0f, 4f),
                new Vector2(2f, 4f),
                new Vector2(2f, 2f),
                new Vector2(4f, 2f),
                new Vector2(4f, 0f)
            };
            var counterClockwise = (Vector2[])clockwise.Clone();
            System.Array.Reverse(counterClockwise);
            var triangles = new List<int>();

            Assert.That(PolygonTriangulator.TryTriangulate(clockwise, triangles), Is.True);
            Assert.That(triangles.Count, Is.EqualTo(12));
            AssertTrianglesCoverPolygon(clockwise, triangles);

            Assert.That(PolygonTriangulator.TryTriangulate(counterClockwise, triangles), Is.True);
            Assert.That(triangles.Count, Is.EqualTo(12));
            AssertTrianglesCoverPolygon(counterClockwise, triangles);
        }

        [Test]
        public void TryTriangulate_CollinearVertexOnEdge_IsSkippedWithoutLosingArea()
        {
            var squareWithMidpoint = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(2f, 0f),
                new Vector2(2f, 2f),
                new Vector2(0f, 2f)
            };
            var triangles = new List<int>();

            Assert.That(PolygonTriangulator.TryTriangulate(squareWithMidpoint, triangles), Is.True);
            AssertTrianglesCoverPolygon(squareWithMidpoint, triangles);
        }

        [Test]
        public void TryTriangulate_TooFewOrDegeneratePoints_ReturnsFalseAndClearsOutput()
        {
            var triangles = new List<int> { 7, 8, 9 };

            Assert.That(
                PolygonTriangulator.TryTriangulate(new[] { Vector2.zero, Vector2.one }, triangles),
                Is.False);
            Assert.That(triangles, Is.Empty);

            triangles.Add(1);
            var collinear = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(2f, 0f) };
            Assert.That(PolygonTriangulator.TryTriangulate(collinear, triangles), Is.False);
            Assert.That(triangles, Is.Empty);
        }

        private static void AssertTrianglesCoverPolygon(Vector2[] polygon, List<int> triangles)
        {
            var coveredArea = 0f;
            for (var index = 0; index < triangles.Count; index += 3)
            {
                var triangle = new[]
                {
                    polygon[triangles[index]],
                    polygon[triangles[index + 1]],
                    polygon[triangles[index + 2]]
                };
                var signedArea = PolygonTriangulator.SignedArea(triangle);

                // 順時針（負面積）對應到 XZ 平面時法線朝上。
                Assert.That(signedArea, Is.LessThan(0f), "三角形應為順時針排列。");
                coveredArea += -signedArea;
            }

            Assert.That(
                coveredArea,
                Is.EqualTo(Mathf.Abs(PolygonTriangulator.SignedArea(polygon))).Within(1e-4f));
        }
    }
}
