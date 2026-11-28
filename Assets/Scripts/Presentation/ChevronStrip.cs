using System.Collections.Generic;
using UnityEngine;

namespace CyberWizard.Presentation
{
    /// <summary>
    /// 沿折線產生一串「&gt;」形箭頭的網格（朝上），用於地面路線提示。
    /// 每個箭頭由兩個平行四邊形組成，尖端指向前進方向；高度沿折線線性內插。
    /// 可選擇沿坡度傾斜（<see cref="Settings.FollowSlope"/>）與頭尾縮放淡入淡出（<see cref="Settings.FadeDistance"/>）。
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

            /// <summary>
            /// 頭尾淡入淡出距離：箭頭中心離折線起點或終點小於此距離時，依比例縮小（0 表示不縮放）。
            /// 搭配流動動畫，新箭頭會從起點逐漸放大出現，到終點逐漸縮小消失。
            /// </summary>
            public float FadeDistance;

            /// <summary>
            /// true 時箭頭沿線段的 3D 方向傾斜（跟著上下坡）；false 時箭頭水平平放。
            /// 左右方向維持水平。
            /// </summary>
            public bool FollowSlope;

            public static Settings Default => new Settings
            {
                Spacing = 3.5f,
                Width = 3.2f,
                Length = 1.6f,
                Thickness = 0.7f,
                Lift = 0.12f,
                Offset = 0f,
                FadeDistance = 0f,
                FollowSlope = false
            };
        }

        /// <summary>折線的水平總長度。</summary>
        public static float HorizontalLength(IReadOnlyList<Vector3> points)
        {
            var total = 0f;
            if (points == null)
            {
                return total;
            }

            for (var index = 1; index < points.Count; index++)
            {
                total += new Vector2(points[index].x - points[index - 1].x, points[index].z - points[index - 1].z).magnitude;
            }

            return total;
        }

        /// <summary>
        /// 箭頭在 distance（沿折線的水平距離）處的大小比例：0～1。
        /// fadeDistance ≤ 0 時永遠為 1。
        /// </summary>
        public static float FadeScale(float distance, float totalLength, float fadeDistance)
        {
            if (fadeDistance <= 0f)
            {
                return 1f;
            }

            var edgeDistance = Mathf.Min(distance, totalLength - distance);
            return Mathf.Clamp01(edgeDistance / fadeDistance);
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
            var totalLength = settings.FadeDistance > 0f ? HorizontalLength(points) : 0f;

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

                var flatForward = flat / segmentLength;
                var right = new Vector3(flatForward.z, 0f, -flatForward.x);
                // 沿坡度時使用 3D 方向；箭頭的長度與粗細沿傾斜方向量測。
                var forward = settings.FollowSlope ? (end - start).normalized : flatForward;
                while (nextDistance <= travelled + segmentLength)
                {
                    var t = (nextDistance - travelled) / segmentLength;
                    var scale = FadeScale(nextDistance, totalLength, settings.FadeDistance);
                    if (scale > 0.001f)
                    {
                        var center = Vector3.Lerp(start, end, t) + Vector3.up * settings.Lift;
                        AddChevron(
                            center,
                            forward,
                            right,
                            halfWidth * scale,
                            halfLength * scale,
                            thickness * scale,
                            vertices,
                            triangles);
                        count++;
                    }

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
