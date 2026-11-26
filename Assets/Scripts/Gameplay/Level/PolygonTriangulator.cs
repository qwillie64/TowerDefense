using System.Collections.Generic;
using UnityEngine;

namespace CyberWizard.Gameplay.Level
{
    /// <summary>
    /// 以 Ear Clipping 將不自交的簡單多邊形切成三角形，支援凹多邊形。
    /// </summary>
    public static class PolygonTriangulator
    {
        private const float Epsilon = 1e-5f;

        /// <summary>
        /// Shoelace 公式的有向面積；正值代表頂點為逆時針排列。
        /// </summary>
        public static float SignedArea(IReadOnlyList<Vector2> points)
        {
            var doubledArea = 0f;
            for (var index = 0; index < points.Count; index++)
            {
                var current = points[index];
                var next = points[(index + 1) % points.Count];
                doubledArea += current.x * next.y - next.x * current.y;
            }

            return doubledArea * 0.5f;
        }

        /// <summary>
        /// 將多邊形切成三角形索引（每三個一組）。
        /// 輸出的三角形在 (x, y) 平面上為順時針；把 y 對應到世界 Z 軸時，網格法線朝上。
        /// 頂點不足三個、面積為零或邊界自交而無法切分時回傳 false，且 triangles 會被清空。
        /// </summary>
        public static bool TryTriangulate(IReadOnlyList<Vector2> points, List<int> triangles)
        {
            triangles.Clear();
            if (points == null || points.Count < 3)
            {
                return false;
            }

            var area = SignedArea(points);
            if (Mathf.Abs(area) < Epsilon)
            {
                return false;
            }

            // 一律以逆時針順序處理，凸角判斷才能使用同一個符號。
            var remaining = new List<int>(points.Count);
            for (var index = 0; index < points.Count; index++)
            {
                remaining.Add(area > 0f ? index : points.Count - 1 - index);
            }

            while (remaining.Count > 3)
            {
                if (!TryClipOneVertex(points, remaining, triangles))
                {
                    triangles.Clear();
                    return false;
                }
            }

            triangles.Add(remaining[0]);
            triangles.Add(remaining[2]);
            triangles.Add(remaining[1]);
            return true;
        }

        private static bool TryClipOneVertex(
            IReadOnlyList<Vector2> points,
            List<int> remaining,
            List<int> triangles)
        {
            for (var index = 0; index < remaining.Count; index++)
            {
                var previous = remaining[(index - 1 + remaining.Count) % remaining.Count];
                var current = remaining[index];
                var next = remaining[(index + 1) % remaining.Count];
                var turn = Cross(points[current] - points[previous], points[next] - points[current]);

                if (Mathf.Abs(turn) <= Epsilon)
                {
                    // 三點共線：移除多餘頂點，不輸出面積為零的三角形。
                    remaining.RemoveAt(index);
                    return true;
                }

                if (turn < 0f || ContainsOtherVertex(points, remaining, previous, current, next))
                {
                    continue;
                }

                triangles.Add(previous);
                triangles.Add(next);
                triangles.Add(current);
                remaining.RemoveAt(index);
                return true;
            }

            return false;
        }

        private static bool ContainsOtherVertex(
            IReadOnlyList<Vector2> points,
            List<int> remaining,
            int previous,
            int current,
            int next)
        {
            for (var index = 0; index < remaining.Count; index++)
            {
                var candidate = remaining[index];
                if (candidate == previous || candidate == current || candidate == next)
                {
                    continue;
                }

                if (IsInsideTriangle(points[candidate], points[previous], points[current], points[next]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsInsideTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
        {
            // a、b、c 為逆時針；點在三條邊的左側或邊上即視為在三角形內。
            return Cross(b - a, point - a) >= -Epsilon
                && Cross(c - b, point - b) >= -Epsilon
                && Cross(a - c, point - c) >= -Epsilon;
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }
    }
}
