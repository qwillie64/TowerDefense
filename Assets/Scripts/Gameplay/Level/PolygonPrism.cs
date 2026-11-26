using System.Collections.Generic;
using UnityEngine;

namespace CyberWizard.Gameplay.Level
{
    /// <summary>
    /// 把 XZ 平面上的簡單多邊形擠出成柱體網格資料：水平頂面（Y = 0）加上往下延伸的側壁。
    /// 頂面與每面側壁各自使用獨立頂點，讓邊緣保持硬邊光影。
    /// </summary>
    public static class PolygonPrism
    {
        private const float MinEdgeLengthSquared = 1e-8f;

        /// <summary>
        /// 產生柱體的頂點、法線與三角形索引。depth 小於等於 0 時只產生頂面。
        /// 多邊形無法三角化時回傳 false，且三個輸出清單都會被清空。
        /// </summary>
        public static bool TryBuild(
            IReadOnlyList<Vector2> outline,
            float depth,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<int> triangles)
        {
            vertices.Clear();
            normals.Clear();
            triangles.Clear();

            // 三角化的索引直接對應 outline 的頂點順序，也就是接下來加入的頂面頂點。
            if (!PolygonTriangulator.TryTriangulate(outline, triangles))
            {
                return false;
            }

            for (var index = 0; index < outline.Count; index++)
            {
                vertices.Add(new Vector3(outline[index].x, 0f, outline[index].y));
                normals.Add(Vector3.up);
            }

            if (depth <= 0f)
            {
                return true;
            }

            var isCounterClockwise = PolygonTriangulator.SignedArea(outline) > 0f;
            for (var index = 0; index < outline.Count; index++)
            {
                var start = outline[index];
                var end = outline[(index + 1) % outline.Count];
                var edge = end - start;
                if (edge.sqrMagnitude < MinEdgeLengthSquared)
                {
                    continue;
                }

                // 逆時針多邊形的外側在邊的右手邊。
                var outward = isCounterClockwise ? new Vector2(edge.y, -edge.x) : new Vector2(-edge.y, edge.x);
                var normal = new Vector3(outward.x, 0f, outward.y).normalized;

                var first = vertices.Count;
                vertices.Add(new Vector3(start.x, 0f, start.y));
                vertices.Add(new Vector3(end.x, 0f, end.y));
                vertices.Add(new Vector3(end.x, -depth, end.y));
                vertices.Add(new Vector3(start.x, -depth, start.y));
                for (var corner = 0; corner < 4; corner++)
                {
                    normals.Add(normal);
                }

                AddTriangleFacing(vertices, triangles, first, first + 1, first + 2, normal);
                AddTriangleFacing(vertices, triangles, first, first + 2, first + 3, normal);
            }

            return true;
        }

        private static void AddTriangleFacing(
            List<Vector3> vertices,
            List<int> triangles,
            int a,
            int b,
            int c,
            Vector3 facing)
        {
            // Unity 以 Cross(b - a, c - a) 的方向為正面；方向相反時交換兩個頂點。
            var faceNormal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            triangles.Add(a);
            if (Vector3.Dot(faceNormal, facing) >= 0f)
            {
                triangles.Add(b);
                triangles.Add(c);
            }
            else
            {
                triangles.Add(c);
                triangles.Add(b);
            }
        }
    }
}
