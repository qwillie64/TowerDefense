using System.Collections.Generic;
using CyberWizard.Gameplay.Level;
using NUnit.Framework;
using UnityEngine;

namespace CyberWizard.Tests
{
    public sealed class RoadMeshBuilderTests
    {
        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Vector3> _normals = new List<Vector3>();
        private readonly List<Vector4> _tangents = new List<Vector4>();
        private readonly List<Vector2> _uvs = new List<Vector2>();
        private readonly List<int> _triangles = new List<int>();

        [SetUp]
        public void SetUp()
        {
            _vertices.Clear();
            _normals.Clear();
            _tangents.Clear();
            _uvs.Clear();
            _triangles.Clear();
        }

        [Test]
        public void StraightRoad_IsOneUpwardQuadWithSquareEnds()
        {
            var built = Append(new[] { new Vector2(0f, 0f), new Vector2(0f, 20f) }, 10f, 0.1f, 4f, 16);

            Assert.That(built, Is.True);
            Assert.That(_vertices.Count, Is.EqualTo(4), "兩端是平頭，不加圓盤。");
            Assert.That(_triangles.Count, Is.EqualTo(6));
            Assert.That(_vertices, Has.All.Matches<Vector3>(v => Mathf.Approximately(v.y, 0.1f)));
            Assert.That(_vertices, Has.All.Matches<Vector3>(v => Mathf.Approximately(Mathf.Abs(v.x), 5f)));
            AssertAllTrianglesFaceUp();
        }

        [Test]
        public void CornerVertex_GetsRoundJoinThatCoversOuterCorner()
        {
            const int segments = 24;
            var points = new[] { new Vector2(0f, 0f), new Vector2(0f, 20f), new Vector2(20f, 20f) };
            Append(points, 10f, 0f, 4f, segments);

            Assert.That(_vertices.Count, Is.EqualTo(4 + 4 + 1 + segments));
            Assert.That(_triangles.Count, Is.EqualTo(6 + 6 + segments * 3));
            AssertAllTrianglesFaceUp();

            // 轉角外側、距離中線轉折點 4.9（小於半寬）的點在兩個矩形之外，必須由圓盤覆蓋。
            var outer = new Vector2(-1f, 1f).normalized * 4.9f + new Vector2(0f, 20f);
            Assert.That(IsCovered(outer), Is.True, "轉角外側出現缺口。");
            Assert.That(IsCovered(new Vector2(-6f, 26f)), Is.False, "超出半寬的位置不應被覆蓋。");
        }

        [Test]
        public void Uv_IsPlanarProjectionSoOverlappingPiecesMatch()
        {
            const float tile = 4f;
            Append(new[] { new Vector2(3f, -7f), new Vector2(15f, 9f), new Vector2(30f, 9f) }, 8f, 0f, tile, 12);

            for (var index = 0; index < _vertices.Count; index++)
            {
                var expected = new Vector2(_vertices[index].x, _vertices[index].z) / tile;
                Assert.That((_uvs[index] - expected).magnitude, Is.LessThan(1e-5f));
                Assert.That(_normals[index], Is.EqualTo(Vector3.up));
                Assert.That(_tangents[index], Is.EqualTo(new Vector4(1f, 0f, 0f, -1f)));
            }
        }

        [Test]
        public void InvalidInput_AppendsNothing()
        {
            Assert.That(Append(new[] { Vector2.zero }, 10f, 0f, 4f, 16), Is.False);
            Assert.That(Append(new[] { Vector2.zero, Vector2.one }, 0f, 0f, 4f, 16), Is.False);
            Assert.That(Append(new[] { Vector2.zero, Vector2.one }, 10f, 0f, 0f, 16), Is.False);
            Assert.That(Append(null, 10f, 0f, 4f, 16), Is.False);
            Assert.That(_vertices, Is.Empty);
            Assert.That(_triangles, Is.Empty);
        }

        private bool Append(IReadOnlyList<Vector2> points, float width, float height, float tile, int segments)
        {
            return RoadMeshBuilder.AppendPolyline(points, width, height, tile, segments,
                _vertices, _normals, _tangents, _uvs, _triangles);
        }

        private void AssertAllTrianglesFaceUp()
        {
            for (var index = 0; index < _triangles.Count; index += 3)
            {
                var a = _vertices[_triangles[index]];
                var b = _vertices[_triangles[index + 1]];
                var c = _vertices[_triangles[index + 2]];
                Assert.That(Vector3.Cross(b - a, c - a).y, Is.GreaterThan(0f), $"第 {index / 3} 個三角形朝下。");
            }
        }

        private bool IsCovered(Vector2 point)
        {
            for (var index = 0; index < _triangles.Count; index += 3)
            {
                if (Contains(Flat(_vertices[_triangles[index]]), Flat(_vertices[_triangles[index + 1]]),
                        Flat(_vertices[_triangles[index + 2]]), point))
                {
                    return true;
                }
            }

            return false;
        }

        private static Vector2 Flat(Vector3 value) => new Vector2(value.x, value.z);

        private static bool Contains(Vector2 a, Vector2 b, Vector2 c, Vector2 p)
        {
            var d1 = Cross(b - a, p - a);
            var d2 = Cross(c - b, p - b);
            var d3 = Cross(a - c, p - c);
            var hasNegative = d1 < 0f || d2 < 0f || d3 < 0f;
            var hasPositive = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNegative && hasPositive);
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    }
}
