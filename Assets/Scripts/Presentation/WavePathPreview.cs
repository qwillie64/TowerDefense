using System.Collections.Generic;
using CyberWizard.Gameplay.Enemies;
using CyberWizard.Gameplay.Waves;
using UnityEngine;
using UnityEngine.Rendering;

namespace CyberWizard.Presentation
{
    /// <summary>
    /// 地面路標：在下一波的梯隊生成前，沿每個梯隊預計的路線（出生點 → 各路徑點）
    /// 貼地顯示一串發光箭頭，箭頭會沿前進方向循環流動。
    /// 顯示時機：下一波尚未開始，且目前沒有梯隊正在生成（第一波在關卡開始前就顯示；
    /// 之後的波次在上一波全部生成完畢後顯示）；該波開始生成時隱藏。排程中止（失敗）後全部隱藏。
    /// 同一條路線只建立一個網格，多個梯隊走同一條路線時共用；只有顯示中的路線每幀更新網格。
    /// 發光效果來自 HDR 箭頭材質加上場景中的 Bloom 後製（Volume）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WavePathPreview : MonoBehaviour
    {
        private sealed class RouteStrip
        {
            public GameObject GameObject;
            public Mesh Mesh;
            public readonly List<Vector3> LocalPoints = new List<Vector3>();
        }

        [SerializeField] private WaveDirector waveDirector;
        [SerializeField] private EnemySpawner enemySpawner;
        [SerializeField, Tooltip("箭頭材質（半透明 HDR 紅色；亮度大於 1 的部分會被 Bloom 擴散成光暈）。")]
        private Material arrowMaterial;

        [Header("箭頭外形（暫定值）")]
        [SerializeField, Min(0.5f)] private float spacing = 3.5f;
        [SerializeField, Min(0.1f)] private float width = 3.2f;
        [SerializeField, Min(0.1f)] private float length = 1.6f;
        [SerializeField, Min(0.05f)] private float thickness = 0.7f;
        [SerializeField, Min(0f), Tooltip("箭頭高於路徑點（路面）的距離。")] private float lift = 0.12f;

        [Header("流動動畫（暫定值）")]
        [SerializeField, Min(0f), Tooltip("箭頭沿路線前進的速度（單位／秒）；0 表示箭頭靜止。")]
        private float flowSpeed = 3f;

        private readonly Dictionary<string, RouteStrip> _strips = new Dictionary<string, RouteStrip>();
        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Vector3> _normals = new List<Vector3>();
        private readonly List<int> _triangles = new List<int>();
        private string _shownWaveId;
        private bool _initialized;

        /// <summary>目前顯示的波次 ID；沒有顯示時為 null。</summary>
        public string ShownWaveId => _shownWaveId;

        /// <summary>目前的流動偏移量（單位），以箭頭間距為週期循環。</summary>
        public float FlowOffset => flowSpeed > 0f ? Mathf.Repeat(Time.time * flowSpeed, spacing) : 0f;

        /// <summary>指定路線（出生點 + 路線 ID）的箭頭目前是否顯示。</summary>
        public bool IsRouteVisible(string spawnPointId, string pathId)
        {
            return _strips.TryGetValue(MakeKey(spawnPointId, pathId), out var strip) &&
                   strip != null && strip.GameObject.activeSelf;
        }

        /// <summary>目前顯示中的路線數量。</summary>
        public int VisibleRouteCount
        {
            get
            {
                var count = 0;
                foreach (var strip in _strips.Values)
                {
                    if (strip != null && strip.GameObject.activeSelf)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        private void Start()
        {
            if (waveDirector == null || enemySpawner == null)
            {
                Debug.LogError("WavePathPreview 缺少 WaveDirector 或 EnemySpawner，不會顯示路線箭頭。", this);
                enabled = false;
                return;
            }

            if (arrowMaterial == null)
            {
                Debug.LogWarning("WavePathPreview 沒有設定箭頭材質。", this);
            }

            _initialized = true;
            Refresh();
        }

        private void LateUpdate()
        {
            if (!_initialized)
            {
                return;
            }

            Refresh();
            if (flowSpeed > 0f)
            {
                var offset = FlowOffset;
                foreach (var strip in _strips.Values)
                {
                    if (strip != null && strip.GameObject.activeSelf)
                    {
                        RebuildMesh(strip, offset);
                    }
                }
            }
        }

        private void Refresh()
        {
            var show = waveDirector.TryGetUpcomingWave(out var wave) && waveDirector.PendingSpawnCount == 0;
            var waveId = show ? wave.waveId : null;
            if (waveId == _shownWaveId)
            {
                return;
            }

            _shownWaveId = waveId;
            foreach (var strip in _strips.Values)
            {
                if (strip != null)
                {
                    strip.GameObject.SetActive(false);
                }
            }

            if (!show || wave.enemies == null)
            {
                return;
            }

            for (var index = 0; index < wave.enemies.Length; index++)
            {
                var entry = wave.enemies[index];
                var strip = GetOrBuildStrip(entry.spawnPointId, entry.pathId);
                if (strip != null)
                {
                    strip.GameObject.SetActive(true);
                }
            }
        }

        private RouteStrip GetOrBuildStrip(string spawnPointId, string pathId)
        {
            var key = MakeKey(spawnPointId, pathId);
            if (_strips.TryGetValue(key, out var existing))
            {
                return existing;
            }

            RouteStrip strip = null;
            if (enemySpawner.TryGetRoutePoints(spawnPointId, pathId, _vertices))
            {
                strip = new RouteStrip
                {
                    GameObject = new GameObject($"Path Arrows ({key})"),
                    Mesh = new Mesh { name = $"PathArrows_{key}" }
                };
                strip.GameObject.transform.SetParent(transform, false);
                strip.Mesh.MarkDynamic();

                // 網格頂點使用本物件的本地座標。
                for (var index = 0; index < _vertices.Count; index++)
                {
                    strip.LocalPoints.Add(strip.GameObject.transform.InverseTransformPoint(_vertices[index]));
                }

                RebuildMesh(strip, FlowOffset);

                strip.GameObject.AddComponent<MeshFilter>().sharedMesh = strip.Mesh;
                var meshRenderer = strip.GameObject.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = arrowMaterial;
                meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;
                strip.GameObject.SetActive(false);
            }
            else
            {
                Debug.LogWarning($"WavePathPreview 找不到路線：出生點 {spawnPointId}、路線 {pathId}。", this);
            }

            _strips[key] = strip;
            return strip;
        }

        private void RebuildMesh(RouteStrip strip, float offset)
        {
            var settings = new ChevronStrip.Settings
            {
                Spacing = spacing,
                Width = width,
                Length = length,
                Thickness = thickness,
                Lift = lift,
                Offset = offset
            };
            ChevronStrip.Build(strip.LocalPoints, settings, _vertices, _triangles);

            _normals.Clear();
            for (var index = 0; index < _vertices.Count; index++)
            {
                _normals.Add(Vector3.up);
            }

            // 偏移量改變時箭頭數量可能增減一個，先清空避免三角形索引超出頂點範圍。
            strip.Mesh.Clear();
            strip.Mesh.SetVertices(_vertices);
            strip.Mesh.SetNormals(_normals);
            strip.Mesh.SetTriangles(_triangles, 0);
            strip.Mesh.RecalculateBounds();
        }

        private static string MakeKey(string spawnPointId, string pathId) => $"{spawnPointId}|{pathId}";

        private void OnDestroy()
        {
            foreach (var strip in _strips.Values)
            {
                if (strip != null && strip.Mesh != null)
                {
                    Destroy(strip.Mesh);
                }
            }
        }
    }
}
