using System;
using System.Collections.Generic;
using UnityEngine;

namespace CyberWizard.Gameplay.Level
{
    /// <summary>
    /// 關卡的道路路面：保存多條道路的中線與路寬，合併產生一個平面網格（見 RoadMeshBuilder）。
    /// 路面位於本物件的 Y = 0，UV 以本地 XZ 平面投影，因此各段與路口的貼圖連續、沒有接縫。
    /// 只負責外觀，沒有碰撞，也不代表敵人路徑（敵人路徑由 EnemyPath 的路徑點決定）。
    /// 網格標記為 DontSave，由資料即時產生，不寫入場景檔。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class RoadNetwork : MonoBehaviour
    {
        [Serializable]
        public sealed class RoadPath
        {
            [Tooltip("辨識用名稱，不影響網格。")]
            public string name = "Road";

            [Min(0.1f), Tooltip("路寬。")]
            public float width = 10f;

            [Tooltip("道路中線頂點（本地 XZ 座標），依序相連；中間的轉折點會補圓角，兩端為平頭。要接到另一條路時，把端點放在那條路的中線上。")]
            public List<Vector2> points = new List<Vector2>();
        }

        [SerializeField, Tooltip("所有道路。每條至少兩個點。")]
        private List<RoadPath> roads = new List<RoadPath>();

        [SerializeField, Min(0.1f), Tooltip("貼圖重複一次對應的距離（世界單位）。數值越大，石塊越大。")]
        private float textureTileSize = 4f;

        [SerializeField, Range(6, 64), Tooltip("路口與轉角圓盤的邊數。")]
        private int joinSegments = 24;

        private readonly List<Vector3> vertexBuffer = new List<Vector3>();
        private readonly List<Vector3> normalBuffer = new List<Vector3>();
        private readonly List<Vector4> tangentBuffer = new List<Vector4>();
        private readonly List<Vector2> uvBuffer = new List<Vector2>();
        private readonly List<int> triangleBuffer = new List<int>();
        private Mesh roadMesh;

        public IReadOnlyList<RoadPath> Roads => roads;

        public float TextureTileSize => textureTileSize;

        private void OnEnable()
        {
            roadMesh = new Mesh
            {
                name = $"{name}_Mesh",
                hideFlags = HideFlags.DontSave
            };
            GetComponent<MeshFilter>().sharedMesh = roadMesh;
            Rebuild();
        }

        private void OnDisable()
        {
            if (roadMesh == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(roadMesh);
            }
            else
            {
                DestroyImmediate(roadMesh);
            }

            roadMesh = null;
        }

        private void OnValidate()
        {
            // 沒有碰撞需要同步，可以直接在 OnValidate 中重建；OnEnable 之前 roadMesh 為 null，略過。
            if (roadMesh != null)
            {
                Rebuild();
            }
        }

        /// <summary>依目前的道路資料重建網格。</summary>
        public void Rebuild()
        {
            if (roadMesh == null)
            {
                return;
            }

            vertexBuffer.Clear();
            normalBuffer.Clear();
            tangentBuffer.Clear();
            uvBuffer.Clear();
            triangleBuffer.Clear();

            foreach (var road in roads)
            {
                if (road == null)
                {
                    continue;
                }

                if (!RoadMeshBuilder.AppendPolyline(
                        road.points, road.width, 0f, textureTileSize, joinSegments,
                        vertexBuffer, normalBuffer, tangentBuffer, uvBuffer, triangleBuffer))
                {
                    Debug.LogWarning($"道路 {name}/{road.name} 少於兩個點或路寬無效，已略過。", this);
                }
            }

            roadMesh.Clear();
            roadMesh.indexFormat = vertexBuffer.Count > 65535
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            roadMesh.SetVertices(vertexBuffer);
            roadMesh.SetNormals(normalBuffer);
            roadMesh.SetTangents(tangentBuffer);
            roadMesh.SetUVs(0, uvBuffer);
            roadMesh.SetTriangles(triangleBuffer, 0);
            roadMesh.RecalculateBounds();
        }
    }
}
