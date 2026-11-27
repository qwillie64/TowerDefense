using System.Collections.Generic;
using CyberWizard.Gameplay.Level;
using CyberWizard.Gameplay.Towers;
using UnityEngine;
using UnityEngine.Rendering;

namespace CyberWizard.Presentation
{
    /// <summary>
    /// 替目前選取的塔（TowerUpgradeInteractor.CurrentTower：玩家靠近且視角指向）顯示外框。
    /// 作法是反向外殼（inverted hull）加 Stencil 遮罩：為塔的基座、塔身、砲管各建立一個子物件，
    /// 使用寫入平滑法線（UV3）的網格副本與兩個材質——先畫遮罩（TowerOutlineMask，標記整座塔的像素），
    /// 再畫外殼（TowerOutline，只畫在未標記處），因此只顯示整座塔的外輪廓、部位交界處沒有內部線條。
    /// 先後順序由兩個 Shader 的渲染佇列（Geometry+5、Geometry+10）保證。
    /// 外框物件在塔第一次被選取時才建立，之後只切換啟用狀態，不會每幀配置記憶體。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TowerOutlineHighlighter : MonoBehaviour
    {
        private const string OutlineObjectName = "Selection Outline";
        private const int SmoothNormalChannel = 3;

        [SerializeField] private TowerUpgradeInteractor interactor;
        [SerializeField, Tooltip("使用 CyberWizard/TowerOutline Shader 的材質（顏色與寬度在材質上調整）。")]
        private Material outlineMaterial;

        [SerializeField, Tooltip("使用 CyberWizard/TowerOutlineMask Shader 的材質；Stencil 參考值需與外框材質相同。")]
        private Material outlineMaskMaterial;

        private readonly Dictionary<FixedTowerCombat, List<GameObject>> _outlines =
            new Dictionary<FixedTowerCombat, List<GameObject>>();
        private readonly List<Mesh> _createdMeshes = new List<Mesh>();
        private FixedTowerCombat _highlighted;

        public FixedTowerCombat HighlightedTower => _highlighted;

        private void Awake()
        {
            if (interactor == null)
            {
                Debug.LogError($"{name} 缺少 TowerUpgradeInteractor，塔不會顯示外框。", this);
            }

            if (outlineMaterial == null || outlineMaskMaterial == null)
            {
                Debug.LogError($"{name} 缺少外框或外框遮罩材質，塔不會顯示外框。", this);
            }
        }

        private void LateUpdate()
        {
            var tower = interactor != null ? interactor.CurrentTower : null;
            if (tower == _highlighted)
            {
                return;
            }

            SetOutlineActive(_highlighted, false);
            _highlighted = tower;
            SetOutlineActive(_highlighted, true);
        }

        private void OnDisable()
        {
            SetOutlineActive(_highlighted, false);
            _highlighted = null;
        }

        private void OnDestroy()
        {
            for (var index = 0; index < _createdMeshes.Count; index++)
            {
                if (_createdMeshes[index] != null)
                {
                    Destroy(_createdMeshes[index]);
                }
            }

            _createdMeshes.Clear();
        }

        /// <summary>指定的塔目前是否有顯示外框。</summary>
        public bool IsOutlineVisible(FixedTowerCombat tower)
        {
            if (tower == null || !_outlines.TryGetValue(tower, out var outlines))
            {
                return false;
            }

            for (var index = 0; index < outlines.Count; index++)
            {
                if (outlines[index] != null && outlines[index].activeInHierarchy)
                {
                    return true;
                }
            }

            return false;
        }

        private void SetOutlineActive(FixedTowerCombat tower, bool active)
        {
            if (tower == null || outlineMaterial == null || outlineMaskMaterial == null)
            {
                return;
            }

            if (!_outlines.TryGetValue(tower, out var outlines))
            {
                if (!active)
                {
                    return;
                }

                outlines = CreateOutlines(tower);
                _outlines.Add(tower, outlines);
            }

            for (var index = 0; index < outlines.Count; index++)
            {
                if (outlines[index] != null && outlines[index].activeSelf != active)
                {
                    outlines[index].SetActive(active);
                }
            }
        }

        private List<GameObject> CreateOutlines(FixedTowerCombat tower)
        {
            var outlines = new List<GameObject>(3);
            var fixedTower = tower.GetComponent<FixedTower>();
            if (fixedTower == null)
            {
                return outlines;
            }

            AddOutline(fixedTower.BaseVisual, outlines);
            AddOutline(fixedTower.BodyVisual, outlines);
            AddOutline(fixedTower.BarrelVisual, outlines);
            if (outlines.Count == 0)
            {
                Debug.LogWarning($"{tower.name} 沒有可用的幾何物件（MeshFilter），無法顯示外框。", tower);
            }

            return outlines;
        }

        private void AddOutline(Transform visual, List<GameObject> outlines)
        {
            if (visual == null || !visual.TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh == null)
            {
                return;
            }

            // 外框是幾何物件的子物件：跟著砲塔旋轉、沿用相同縮放；沒有碰撞體，不影響 Raycast。
            var outline = new GameObject(OutlineObjectName);
            outline.layer = visual.gameObject.layer;
            outline.transform.SetParent(visual, false);
            outline.AddComponent<MeshFilter>().sharedMesh = CreateSmoothNormalMesh(filter.sharedMesh, visual);
            var renderer = outline.AddComponent<MeshRenderer>();
            // 網格只有一個子網格時，第二個材質會再畫一次同一個子網格：遮罩與外殼共用同一個物件。
            renderer.sharedMaterials = new[] { outlineMaskMaterial, outlineMaterial };
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            outline.SetActive(false);
            outlines.Add(outline);
        }

        /// <summary>
        /// 複製網格，並在 UV3 寫入「同一位置所有頂點法線的平均」，讓硬邊（方塊角、圓柱邊緣）的外殼不會裂開。
        /// 平均在考慮物件縮放後的空間中計算，避免被壓扁的圓柱（基座）外框只往上下推。
        /// 網格不可讀取時直接沿用原網格，Shader 會改用網格法線。
        /// </summary>
        private Mesh CreateSmoothNormalMesh(Mesh source, Transform visual)
        {
            if (!source.isReadable)
            {
                Debug.LogWarning($"網格 {source.name} 不可讀取，外框改用原始法線（硬邊處可能有缺口）。", visual);
                return source;
            }

            var vertices = source.vertices;
            var normals = source.normals;
            if (normals.Length != vertices.Length)
            {
                return source;
            }

            // 法線從物件空間換到世界空間用 (M^-1)^T；換回時用 M^T，兩者相乘抵銷，所以旋轉不影響結果。
            var toWorld = visual.localToWorldMatrix;
            var normalToWorld = toWorld.inverse.transpose;
            var sums = new Dictionary<Vector3, Vector3>(vertices.Length);
            for (var index = 0; index < vertices.Length; index++)
            {
                var worldNormal = normalToWorld.MultiplyVector(normals[index]).normalized;
                sums.TryGetValue(vertices[index], out var sum);
                sums[vertices[index]] = sum + worldNormal;
            }

            var toObjectNormal = toWorld.transpose;
            var smoothNormals = new Vector3[vertices.Length];
            for (var index = 0; index < vertices.Length; index++)
            {
                var average = sums[vertices[index]];
                smoothNormals[index] = average.sqrMagnitude > 1e-8f
                    ? toObjectNormal.MultiplyVector(average.normalized)
                    : normals[index];
            }

            var mesh = Instantiate(source);
            mesh.name = source.name + " (Outline)";
            mesh.SetUVs(SmoothNormalChannel, smoothNormals);
            _createdMeshes.Add(mesh);
            return mesh;
        }
    }
}
