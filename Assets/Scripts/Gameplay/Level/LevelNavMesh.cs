using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace CyberWizard.Gameplay.Level
{
    /// <summary>
    /// 在執行時由關卡幾何建立 NavMesh，供敵人的 NavMeshAgent 使用。
    /// 只收集 geometryRoot 底下的碰撞體（非 Trigger），因此玩家、敵人與發射物不會被烘焙進去。
    /// 使用 Unity 內建的 AI 模組（UnityEngine.AI.NavMeshBuilder），不需要 AI Navigation 套件，也不產生資產。
    /// 地形修改後不需要手動重新烘焙；每次進入場景時（Start）會自動重建。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1000)]
    public sealed class LevelNavMesh : MonoBehaviour
    {
        [SerializeField, Tooltip("要烘焙的關卡幾何根物件；未設定時使用本物件。")]
        private Transform geometryRoot;

        [SerializeField, Tooltip("參與烘焙的圖層。")]
        private LayerMask includedLayers = ~0;

        [Header("代理尺寸（暫定值，應與敵人體型相符）")]
        [SerializeField, Min(0.05f), Tooltip("敵人與牆面、障礙物保持的最小距離。")]
        private float agentRadius = 0.5f;

        [SerializeField, Min(0.1f)]
        private float agentHeight = 2f;

        [SerializeField, Range(0f, 60f), Tooltip("可行走的最大坡度（度）。")]
        private float maxSlope = 45f;

        [SerializeField, Min(0f), Tooltip("可直接跨上的最大高低差。")]
        private float stepHeight = 0.75f;

        [SerializeField, Min(0.05f), Tooltip("體素大小；越小越精細，烘焙時間越長。")]
        private float voxelSize = 0.25f;

        [SerializeField, Range(16, 1024), Tooltip("NavMesh 圖塊邊長（體素數）。圖塊交界在斜坡上可能讓路線繞經圖塊角落，因此預設用最大值，讓整張地圖盡量落在同一圖塊內。")]
        private int tileSize = 1024;

        private readonly List<NavMeshBuildSource> _sources = new List<NavMeshBuildSource>();
        private NavMeshDataInstance _instance;

        public bool IsBuilt => _instance.valid;

        // 地面與平台的碰撞網格在各自的 OnEnable 才產生，所以等到 Start（所有 Awake／OnEnable 之後）才烘焙。
        // 執行順序設為最前，讓 NavMesh 在其他元件的 Start 之前就緒。
        private void Start()
        {
            Build();
        }

        private void OnDestroy()
        {
            Clear();
        }

        /// <summary>重新收集幾何並建立 NavMesh；已有舊資料時先移除。成功時回傳 true。</summary>
        public bool Build()
        {
            Clear();
            // 碰撞體的位置以物理場景為準；先同步，避免剛移動過的幾何以舊位置烘焙。
            Physics.SyncTransforms();
            var root = geometryRoot != null ? geometryRoot : transform;
            NavMeshBuilder.CollectSources(
                root,
                includedLayers,
                NavMeshCollectGeometry.PhysicsColliders,
                0,
                new List<NavMeshBuildMarkup>(),
                _sources);
            _sources.RemoveAll(source => source.component is Collider collider && collider.isTrigger);
            if (_sources.Count == 0 || !TryGetGeometryBounds(root, out var bounds))
            {
                Debug.LogError($"{name}：{root.name} 底下沒有可烘焙的碰撞體，敵人將無法移動。", this);
                return false;
            }

            var settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = agentRadius;
            settings.agentHeight = agentHeight;
            settings.agentSlope = maxSlope;
            settings.agentClimb = Mathf.Min(stepHeight, agentHeight);
            settings.overrideVoxelSize = true;
            settings.voxelSize = voxelSize;
            settings.overrideTileSize = true;
            settings.tileSize = tileSize;
            // 高度網格讓代理的高度貼合實際地面，而不是 NavMesh 多邊形的近似高度（大圖塊在斜坡上誤差約 0.4）。
            settings.buildHeightMesh = true;

            var data = NavMeshBuilder.BuildNavMeshData(
                settings,
                _sources,
                bounds,
                Vector3.zero,
                Quaternion.identity);
            _sources.Clear();
            if (data == null)
            {
                Debug.LogError($"{name}：NavMesh 建立失敗。", this);
                return false;
            }

            data.name = $"{root.name} NavMesh";
            _instance = NavMesh.AddNavMeshData(data);
            return _instance.valid;
        }

        /// <summary>移除本元件建立的 NavMesh。</summary>
        public void Clear()
        {
            if (_instance.valid)
            {
                _instance.Remove();
            }

            _instance = default;
        }

        private bool TryGetGeometryBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            var found = false;
            var colliders = root.GetComponentsInChildren<Collider>();
            for (var index = 0; index < colliders.Length; index++)
            {
                var collider = colliders[index];
                if (collider.isTrigger || (includedLayers.value & (1 << collider.gameObject.layer)) == 0)
                {
                    continue;
                }

                if (found)
                {
                    bounds.Encapsulate(collider.bounds);
                }
                else
                {
                    bounds = collider.bounds;
                    found = true;
                }
            }

            // 頂部預留代理高度，讓最高的地面上方也能容納敵人。
            bounds.Expand(new Vector3(1f, agentHeight * 2f, 1f));
            return found;
        }
    }
}
