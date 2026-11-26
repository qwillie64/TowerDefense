using System.Collections.Generic;
using UnityEngine;

namespace CyberWizard.Gameplay.Level
{
    /// <summary>
    /// 地圖上的防區標示：依邊界頂點產生半透明填色平面與外框線。
    /// 防區只是區域標示，不含碰撞，也不代表建築物或可通行範圍。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(LineRenderer))]
    public sealed class DefenseZone : MonoBehaviour
    {
        [SerializeField, Tooltip("防區編號，例如 5、7、9。")]
        private int zoneNumber;

        [SerializeField, Tooltip("防區邊界頂點（本地 XZ 座標），依序相連並自動封閉；邊界不可自交。")]
        private List<Vector2> boundary = new List<Vector2>();

        [SerializeField, Min(0.01f), Tooltip("外框線寬度（世界單位）。")]
        private float outlineWidth = 0.4f;

        [SerializeField, Min(0f), Tooltip("外框線高於填色平面的距離，避免兩者重疊閃爍。")]
        private float outlineLift = 0.05f;

        private readonly List<Vector3> vertexBuffer = new List<Vector3>();
        private readonly List<Vector3> normalBuffer = new List<Vector3>();
        private readonly List<int> triangleBuffer = new List<int>();
        private Mesh fillMesh;

        public int ZoneNumber => zoneNumber;

        public IReadOnlyList<Vector2> Boundary => boundary;

        private void OnEnable()
        {
            // 填色網格由邊界資料即時產生，不存進場景檔。
            fillMesh = new Mesh
            {
                name = $"DefenseZone_{zoneNumber:00}_Fill",
                hideFlags = HideFlags.DontSave
            };
            GetComponent<MeshFilter>().sharedMesh = fillMesh;
            Rebuild();
        }

        private void OnDisable()
        {
            if (fillMesh == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(fillMesh);
            }
            else
            {
                DestroyImmediate(fillMesh);
            }

            fillMesh = null;
        }

        private void OnValidate()
        {
            // 載入時 OnValidate 早於 OnEnable；此時尚無網格，交由 OnEnable 建立。
            if (fillMesh != null)
            {
                Rebuild();
            }
        }

        private void Rebuild()
        {
            RebuildOutline();
            RebuildFill();
        }

        private void RebuildOutline()
        {
            var outline = GetComponent<LineRenderer>();
            outline.useWorldSpace = false;
            outline.loop = true;
            outline.widthMultiplier = outlineWidth;
            outline.positionCount = boundary.Count;
            for (var index = 0; index < boundary.Count; index++)
            {
                outline.SetPosition(index, new Vector3(boundary[index].x, outlineLift, boundary[index].y));
            }
        }

        private void RebuildFill()
        {
            fillMesh.Clear();
            if (!PolygonTriangulator.TryTriangulate(boundary, triangleBuffer))
            {
                if (boundary.Count >= 3)
                {
                    Debug.LogWarning($"防區 {zoneNumber} 的邊界自交或面積為零，無法產生填色平面。", this);
                }

                return;
            }

            vertexBuffer.Clear();
            normalBuffer.Clear();
            for (var index = 0; index < boundary.Count; index++)
            {
                vertexBuffer.Add(new Vector3(boundary[index].x, 0f, boundary[index].y));
                normalBuffer.Add(Vector3.up);
            }

            fillMesh.SetVertices(vertexBuffer);
            fillMesh.SetNormals(normalBuffer);
            fillMesh.SetTriangles(triangleBuffer, 0);
            fillMesh.RecalculateBounds();
        }
    }
}
