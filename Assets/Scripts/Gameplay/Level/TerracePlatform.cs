using System.Collections.Generic;
using UnityEngine;

namespace CyberWizard.Gameplay.Level
{
    /// <summary>
    /// 地形中的水平平台：依邊界頂點擠出多邊形柱體（水平頂面加四周側壁）。
    /// 用於傾斜地圖上需要保持水平的區域。平台屬於地形，不代表防區；防區標示由 DefenseZone 負責。
    /// 若同一物件上有 MeshCollider，會一併使用產生的網格作為碰撞。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class TerracePlatform : MonoBehaviour
    {
        [SerializeField, Tooltip("平台邊界頂點（本地 XZ 座標），依序相連並自動封閉；邊界不可自交。頂面位於本物件的 Y = 0。")]
        private List<Vector2> boundary = new List<Vector2>();

        [SerializeField, Min(0.01f), Tooltip("側壁從頂面往下延伸的深度；需要深到埋入周圍地面。")]
        private float depth = 5f;

        private readonly List<Vector3> vertexBuffer = new List<Vector3>();
        private readonly List<Vector3> normalBuffer = new List<Vector3>();
        private readonly List<int> triangleBuffer = new List<int>();
        private Mesh platformMesh;
        private bool rebuildPending;

        public IReadOnlyList<Vector2> Boundary => boundary;

        public float Depth => depth;

        private void OnEnable()
        {
            // 網格由邊界資料即時產生，不存進場景檔。
            platformMesh = new Mesh
            {
                name = $"{name}_Mesh",
                hideFlags = HideFlags.DontSave
            };
            GetComponent<MeshFilter>().sharedMesh = platformMesh;
            Rebuild();
        }

        private void OnDisable()
        {
            if (TryGetComponent(out MeshCollider meshCollider))
            {
                meshCollider.sharedMesh = null;
            }

            if (platformMesh == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(platformMesh);
            }
            else
            {
                DestroyImmediate(platformMesh);
            }

            platformMesh = null;
        }

        private void OnValidate()
        {
            // 碰撞網格不適合在 OnValidate 中更新，延到下一次 Update 重建。
            rebuildPending = true;
        }

        private void Update()
        {
            if (rebuildPending && platformMesh != null)
            {
                Rebuild();
            }
        }

        private void Rebuild()
        {
            rebuildPending = false;
            platformMesh.Clear();

            var built = PolygonPrism.TryBuild(boundary, depth, vertexBuffer, normalBuffer, triangleBuffer);
            if (built)
            {
                platformMesh.SetVertices(vertexBuffer);
                platformMesh.SetNormals(normalBuffer);
                platformMesh.SetTriangles(triangleBuffer, 0);
                platformMesh.RecalculateBounds();
            }
            else if (boundary.Count >= 3)
            {
                Debug.LogWarning($"平台 {name} 的邊界自交或面積為零，無法產生網格。", this);
            }

            if (TryGetComponent(out MeshCollider meshCollider))
            {
                // 重新指定才會讓 MeshCollider 依新網格重建碰撞資料。
                meshCollider.sharedMesh = null;
                meshCollider.sharedMesh = built ? platformMesh : null;
            }
        }
    }
}
