using System.Collections.Generic;
using UnityEngine;

namespace CyberWizard.Presentation
{
    /// <summary>
    /// 沿折線產生一串「&gt;」形箭頭的網格（平放、朝上），用於地面路線提示。
    /// 每個箭頭由兩個平行四邊形組成，尖端指向前進方向；高度沿折線線性內插。
    /// 純計算工具，不依賴場景。
    /// </summary>
    public static class ChevronStrip
    {
        public struct Settings
        {
            /// <summary>相鄰箭頭中心的距離。</summary>
            public float Spacing;

            /// <summary>箭頭寬度（垂直於前進方向）。</summary>
            public float Width;

            /// <summary>箭頭長度（尖端到兩翼末端，沿前進方向）。</summary>
            public float Length;

            /// <summary>箭頭線條粗細（沿前進方向）。</summary>
            public float Thickness;

            /// <summary>網格高於路線點的距離，避免與地面重疊閃爍。</summary>
            public float Lift;

            /// <summary>
            /// 所有箭頭沿前進方向額外推進的距離，以一個間距為週期循環；用於流動動畫。0 表示第一個箭頭在半個間距處。
            /// </summary>
            public float Offset;

            public static Settings Default => new Settings
            {
                Spacing = 3.5f,
                Width = 3.2f,
                Length = 1.6f,
                Thickness = 0.7f,
                Lift = 0.12f,
                Offset = 0f
            };
        }

        /// <summary>
        /// 依折線點（世界或本地座標皆可，輸出使用同一座標系）產生網格資料。
        /// 回傳箭頭數量；點少於 2 個或總長度不足一個間距時回傳 0。
        /// </summary>
        public static int Build(
            IReadOnlyList<Vector3> points,
            Settings settings,
            List<Vector3> vertices,
            List<int> triangles)
        {
            vertices.Clear();
            triangles.Clear();
            if (points == null || points.Count < 2 || settings.Spacing <= 0f)
            {
                return 0;
            }

            var halfWidth = settings.Width * 0.5f;
            var halfLength = settings.Length * 0.5f;
            var thickness = Mathf.Clamp(settings.Thickness, 0.01f, settings.Length + halfWidth);
            var count = 0;

            // 第一個箭頭放在半個間距（加上偏移量，以間距為週期）處，之後每隔一個間距放一個，跨越轉折點時延續距離。
            var nextDistance = Mathf.Repeat(settings.Spacing * 0.5f + settings.Offset, settings.Spacing);
            var travelled = 0f;
            for (var index = 1; index < points.Count; index++)
            {
                var start = points[index - 1];
                var end = points[index];
                var flat = new Vector3(end.x - start.x, 0f, end.z - start.z);
                var segmentLength = flat.magnitude;
                if (segmentLength < 0.0001f)
                {
                    continue;
                }

                var forward = flat / segmentLength;
                var right = new Vector3(forward.z, 0f, -forward.x);
                while (nextDistance <= travelled + segmentLength)
                {
                    var t = (nextDistance - travelled) / segmentLength;
                    var center = Vector3.Lerp(start, end, t) + Vector3.up * settings.Lift;
                    AddChevron(center, forward, right, halfWidth, halfLength, thickness, vertices, triangles);
                    count++;
                    nextDistance += settings.Spacing;
                }

                travelled += segmentLength;
            }

            return count;
        }

        private static void AddChevron(
            Vector3 center,
            Vector3 forward,
            Vector3 right,
            float halfWidth,
            float halfLength,
            float thickness,
            List<Vector3> vertices,
            List<int> triangles)
        {
            var first = vertices.Count;

            // 外側尖端、右翼前緣、右翼後緣、內側尖端、左翼後緣、左翼前緣。
            vertices.Add(center + forward * halfLength);
            vertices.Add(center + right * halfWidth - forward * halfLength);
            vertices.Add(center + right * halfWidth - forward * (halfLength + thickness));
            vertices.Add(center + forward * (halfLength - thickness));
            vertices.Add(center - right * halfWidth - forward * (halfLength + thickness));
            vertices.Add(center - right * halfWidth - forward * halfLength);

            // 從上方看為順時針，法線朝上。
            triangles.Add(first);
            triangles.Add(first + 1);
            triangles.Add(first + 2);
            triangles.Add(first);
            triangles.Add(first + 2);
            triangles.Add(first + 3);
            triangles.Add(first);
            triangles.Add(first + 3);
            triangles.Add(first + 4);
            triangles.Add(first);
            triangles.Add(first + 4);
            triangles.Add(first + 5);
        }
    }
}
