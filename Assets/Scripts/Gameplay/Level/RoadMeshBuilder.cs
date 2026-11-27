using System.Collections.Generic;
using UnityEngine;

namespace CyberWizard.Gameplay.Level
{
    /// <summary>
    /// 純靜態工具：把道路中線（本地 XZ 折線）展開成朝上的平面網格。
    /// 每一段產生一個矩形；中間的轉折點再補一個半徑為半寬的圓盤，讓轉角沒有缺口。
    /// 兩端是平頭（不加圓盤），避免道路接到外牆時圓頭穿出牆外；
    /// 接到另一條路（T 字或 Y 字）時，把端點放在那條路的中線上，平頭的角會落在那條路的範圍內
    /// （前提是那條路不比這條路窄）。
    /// UV 一律取本地 XZ 除以貼圖尺寸（平面投影），所以重疊的部分貼圖完全一致，看不出接縫。
    /// </summary>
    public static class RoadMeshBuilder
    {
        private const float MinSegmentLength = 0.0001f;

        // Unity 內建 Quad／Plane 的切線慣例：U 沿 +X、V 沿 +Z 時 w = -1。
        private static readonly Vector4 SurfaceTangent = new Vector4(1f, 0f, 0f, -1f);

        /// <summary>
        /// 把一條道路附加到網格緩衝區。少於兩個點、寬度或貼圖尺寸不大於 0 時不附加任何資料並回傳 false。
        /// </summary>
        /// <param name="points">道路中線（本地 XZ）。</param>
        /// <param name="width">路寬。</param>
        /// <param name="height">路面高度（本地 Y）。</param>
        /// <param name="textureTileSize">貼圖重複一次對應的距離。</param>
        /// <param name="joinSegments">圓盤的邊數，至少 3。</param>
        public static bool AppendPolyline(
            IReadOnlyList<Vector2> points,
            float width,
            float height,
            float textureTileSize,
            int joinSegments,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector4> tangents,
            List<Vector2> uvs,
            List<int> triangles)
        {
            if (points == null || points.Count < 2 || width <= 0f || textureTileSize <= 0f)
            {
                return false;
            }

            var halfWidth = width * 0.5f;
            var segments = Mathf.Max(3, joinSegments);

            for (var index = 0; index < points.Count - 1; index++)
            {
                AppendSegment(points[index], points[index + 1], halfWidth, height, textureTileSize,
                    vertices, normals, tangents, uvs, triangles);
            }

            for (var index = 1; index < points.Count - 1; index++)
            {
                AppendDisc(points[index], halfWidth, segments, height, textureTileSize,
                    vertices, normals, tangents, uvs, triangles);
            }

            return true;
        }

        private static void AppendSegment(
            Vector2 start,
            Vector2 end,
            float halfWidth,
            float height,
            float tileSize,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector4> tangents,
            List<Vector2> uvs,
            List<int> triangles)
        {
            var direction = end - start;
            var length = direction.magnitude;
            if (length < MinSegmentLength)
            {
                return;
            }

            direction /= length;
            // 俯視（X 右、Z 上）時逆時針轉 90°，指向道路左側。
            var left = new Vector2(-direction.y, direction.x) * halfWidth;
            var first = vertices.Count;
            AddVertex(start + left, height, tileSize, vertices, normals, tangents, uvs);
            AddVertex(end + left, height, tileSize, vertices, normals, tangents, uvs);
            AddVertex(end - left, height, tileSize, vertices, normals, tangents, uvs);
            AddVertex(start - left, height, tileSize, vertices, normals, tangents, uvs);

            // 從上方看為順時針，正面朝上。
            triangles.Add(first);
            triangles.Add(first + 1);
            triangles.Add(first + 2);
            triangles.Add(first);
            triangles.Add(first + 2);
            triangles.Add(first + 3);
        }

        private static void AppendDisc(
            Vector2 center,
            float radius,
            int segments,
            float height,
            float tileSize,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector4> tangents,
            List<Vector2> uvs,
            List<int> triangles)
        {
            var centerIndex = vertices.Count;
            AddVertex(center, height, tileSize, vertices, normals, tangents, uvs);

            // 外接多邊形：邊的中點正好落在半寬上，與矩形邊緣齊平。
            var ringRadius = radius / Mathf.Cos(Mathf.PI / segments);
            for (var index = 0; index < segments; index++)
            {
                var angle = index * Mathf.PI * 2f / segments;
                var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ringRadius;
                AddVertex(center + offset, height, tileSize, vertices, normals, tangents, uvs);
            }

            for (var index = 0; index < segments; index++)
            {
                var current = centerIndex + 1 + index;
                var next = centerIndex + 1 + (index + 1) % segments;
                // 角度遞增為俯視逆時針，反過來排列才是順時針（正面朝上）。
                triangles.Add(centerIndex);
                triangles.Add(next);
                triangles.Add(current);
            }
        }

        private static void AddVertex(
            Vector2 position,
            float height,
            float tileSize,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector4> tangents,
            List<Vector2> uvs)
        {
            vertices.Add(new Vector3(position.x, height, position.y));
            normals.Add(Vector3.up);
            tangents.Add(SurfaceTangent);
            uvs.Add(position / tileSize);
        }
    }
}
