using UnityEngine;

namespace CyberWizard.Presentation
{
    /// <summary>
    /// 產生圓錐網格（Unity 沒有內建圓錐 Primitive）。尖端朝本地 +Z，
    /// 底面半徑 0.5、總長 1，中心在原點（底面 z = -0.5，尖端 z = +0.5）。
    /// 側面每個三角形使用獨立頂點與圓錐法線，讓金屬材質有平順的反光。
    /// </summary>
    public static class ConeMesh
    {
        private static Mesh shared;

        /// <summary>整個執行期間共用的圓錐網格（16 段）。</summary>
        public static Mesh Shared
        {
            get
            {
                if (shared == null)
                {
                    shared = Create(16);
                    shared.hideFlags = HideFlags.DontSave;
                }

                return shared;
            }
        }

        public static Mesh Create(int segments)
        {
            segments = Mathf.Max(3, segments);
            const float radius = 0.5f;
            const float baseZ = -0.5f;
            const float apexZ = 0.5f;

            // 側面：每段 3 個頂點（兩個底邊點 + 尖端）；底面：中心 + 每段 2 個點。
            var vertices = new Vector3[segments * 3 + 1 + segments * 2];
            var normals = new Vector3[vertices.Length];
            var triangles = new int[segments * 3 * 2];

            // 圓錐側面法線的 Z 分量：斜邊與軸的夾角決定。
            var slope = radius / (apexZ - baseZ);
            var t = 0;
            for (var index = 0; index < segments; index++)
            {
                var angle0 = index * Mathf.PI * 2f / segments;
                var angle1 = (index + 1) * Mathf.PI * 2f / segments;
                var angleMid = (angle0 + angle1) * 0.5f;
                var v = index * 3;
                vertices[v] = new Vector3(Mathf.Cos(angle0) * radius, Mathf.Sin(angle0) * radius, baseZ);
                vertices[v + 1] = new Vector3(0f, 0f, apexZ);
                vertices[v + 2] = new Vector3(Mathf.Cos(angle1) * radius, Mathf.Sin(angle1) * radius, baseZ);
                normals[v] = SideNormal(angle0, slope);
                normals[v + 1] = SideNormal(angleMid, slope);
                normals[v + 2] = SideNormal(angle1, slope);

                // 從外側看為順時針（Unity 正面），法線朝外。
                triangles[t++] = v;
                triangles[t++] = v + 2;
                triangles[t++] = v + 1;
            }

            var center = segments * 3;
            vertices[center] = new Vector3(0f, 0f, baseZ);
            normals[center] = Vector3.back;
            for (var index = 0; index < segments; index++)
            {
                var angle0 = index * Mathf.PI * 2f / segments;
                var angle1 = (index + 1) * Mathf.PI * 2f / segments;
                var v = center + 1 + index * 2;
                vertices[v] = new Vector3(Mathf.Cos(angle0) * radius, Mathf.Sin(angle0) * radius, baseZ);
                vertices[v + 1] = new Vector3(Mathf.Cos(angle1) * radius, Mathf.Sin(angle1) * radius, baseZ);
                normals[v] = Vector3.back;
                normals[v + 1] = Vector3.back;

                // 底面朝 -Z。
                triangles[t++] = center;
                triangles[t++] = v + 1;
                triangles[t++] = v;
            }

            var mesh = new Mesh { name = "Cone" };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3 SideNormal(float angle, float slope)
        {
            return new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), slope).normalized;
        }
    }
}
