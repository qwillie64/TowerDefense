using System.Collections.Generic;
using CyberWizard.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace CyberWizard.Presentation
{
    /// <summary>
    /// 貼地的攻擊範圍指示：沿射程圓周平均取樣，每個取樣點從上方往下 Raycast 投影到地面，
    /// 再把投影點連成一條環帶網格。範圍內沒有敵人時顯示待機色（白），有敵人時顯示警示色（紅）；
    /// 是否有敵人由擁有者（塔、玩家）透過 <see cref="SetAlert"/> 告知。
    /// 擁有者移動、旋轉或射程改變時才重新投影；靜止的塔只在顯示或升級時計算。
    /// Raycast 會略過觸發器、擁有者自己的碰撞體、可受傷的角色（敵人、玩家、塔、閘門），
    /// 以及標記 <see cref="RangeIndicatorIgnored"/> 的物件（房屋）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class GroundRangeIndicator : MonoBehaviour
    {
        /// <summary>預設圓周取樣數（暫定值）。</summary>
        public const int DefaultSampleCount = 50;

        /// <summary>所有射程圈統一的環帶寬度。</summary>
        public const float DefaultWidth = 0.05f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField, Min(3), Tooltip("圓周上的取樣點數（每點一條 Raycast）。")]
        private int sampleCount = DefaultSampleCount;

        [SerializeField, Min(0.01f), Tooltip("環帶寬度，由射程邊緣往內延伸。")]
        private float width = DefaultWidth;

        [SerializeField, Min(0f), Tooltip("環帶高於投影點的距離，避免被路面遮住或閃爍。需高於防區填色（平台頂面 +0.2），否則會被染成藍色。")]
        private float lift = 0.3f;

        [SerializeField, Min(0.1f), Tooltip("Raycast 起點高於擁有者位置的距離。")]
        private float rayStartHeight = 20f;

        [SerializeField, Min(0.1f), Tooltip("Raycast 往下的最大距離。")]
        private float rayLength = 60f;

        [SerializeField, Tooltip("視為地面的圖層。")]
        private LayerMask groundMask = ~0;

        [SerializeField] private Color idleColor = Color.white;
        [SerializeField] private Color alertColor = new Color(1f, 0.12f, 0.08f, 1f);

        private readonly List<Vector3> _groundPoints = new List<Vector3>();
        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Vector3> _normals = new List<Vector3>();
        private readonly List<int> _triangles = new List<int>();
        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private Mesh _mesh;
        private MeshRenderer _renderer;
        private MaterialPropertyBlock _propertyBlock;
        private float _radius = 1f;
        private bool _alert;
        private bool _dirty = true;
        private Vector3 _builtPosition;
        private Quaternion _builtRotation;

        public float Radius => _radius;
        public float Width => width;
        public int SampleCount => sampleCount;
        public bool IsAlert => _alert;
        public Color CurrentColor => _alert ? alertColor : idleColor;

        /// <summary>最近一次投影得到的地面點（世界座標，未加上 lift）。</summary>
        public IReadOnlyList<Vector3> GroundPoints => _groundPoints;

        /// <summary>在 owner 底下建立指示物件，環帶寬度使用 <see cref="DefaultWidth"/>。</summary>
        public static GroundRangeIndicator Create(Transform owner, string objectName, float radius)
        {
            var indicatorObject = new GameObject(objectName);
            indicatorObject.transform.SetParent(owner, false);
            var indicator = indicatorObject.AddComponent<GroundRangeIndicator>();
            indicator.SetRadius(radius);
            return indicator;
        }

        public void SetRadius(float radius)
        {
            var safeRadius = Mathf.Max(0.05f, radius);
            if (!Mathf.Approximately(safeRadius, _radius))
            {
                _radius = safeRadius;
                _dirty = true;
            }
        }

        public void SetAlert(bool alert)
        {
            if (_alert == alert)
            {
                return;
            }

            _alert = alert;
            ApplyColor();
        }

        /// <summary>立即重新投影並重建網格。</summary>
        public void Rebuild()
        {
            EnsureInitialized();
            var center = transform.position;
            SampleGround(center, _radius, sampleCount, rayStartHeight, rayLength, groundMask,
                transform.parent != null ? transform.parent : transform, _hits, _groundPoints);
            BuildRing(_groundPoints, center, width, lift, _vertices, _triangles);

            for (var index = 0; index < _vertices.Count; index++)
            {
                _vertices[index] = transform.InverseTransformPoint(_vertices[index]);
            }

            _normals.Clear();
            for (var index = 0; index < _vertices.Count; index++)
            {
                _normals.Add(transform.InverseTransformDirection(Vector3.up));
            }

            _mesh.Clear();
            _mesh.SetVertices(_vertices);
            _mesh.SetNormals(_normals);
            _mesh.SetTriangles(_triangles, 0);
            _mesh.RecalculateBounds();

            _builtPosition = transform.position;
            _builtRotation = transform.rotation;
            _dirty = false;
        }

        /// <summary>
        /// 沿水平圓周取樣並往下 Raycast 投影到地面，結果依序寫入 points（世界座標）。
        /// 沒有打到地面的取樣點使用圓心高度。
        /// </summary>
        public static void SampleGround(
            Vector3 center,
            float radius,
            int samples,
            float startHeight,
            float length,
            LayerMask mask,
            Transform ignoreRoot,
            RaycastHit[] buffer,
            List<Vector3> points)
        {
            points.Clear();
            samples = Mathf.Max(3, samples);
            for (var index = 0; index < samples; index++)
            {
                var angle = index * Mathf.PI * 2f / samples;
                var point = new Vector3(
                    center.x + Mathf.Cos(angle) * radius,
                    center.y,
                    center.z + Mathf.Sin(angle) * radius);
                points.Add(TryProjectToGround(point, startHeight, length, mask, ignoreRoot, buffer, out var ground)
                    ? ground
                    : point);
            }
        }

        /// <summary>從 point 上方 startHeight 往下找最近的地面點。</summary>
        public static bool TryProjectToGround(
            Vector3 point,
            float startHeight,
            float length,
            LayerMask mask,
            Transform ignoreRoot,
            RaycastHit[] buffer,
            out Vector3 ground)
        {
            var origin = new Vector3(point.x, point.y + startHeight, point.z);
            var hitCount = Physics.RaycastNonAlloc(origin, Vector3.down, buffer, length, mask, QueryTriggerInteraction.Ignore);
            var bestDistance = float.MaxValue;
            ground = point;
            for (var index = 0; index < hitCount; index++)
            {
                var hit = buffer[index];
                if (hit.distance >= bestDistance || IsIgnored(hit.collider, ignoreRoot))
                {
                    continue;
                }

                bestDistance = hit.distance;
                ground = hit.point;
            }

            return bestDistance < float.MaxValue;
        }

        /// <summary>
        /// 由地面點（世界座標）建立環帶：外緣為地面點加上 lift，內緣沿水平方向往圓心縮 width。
        /// 相鄰取樣點以兩個三角形連接，最後一點接回第一點；正面朝上。
        /// </summary>
        public static void BuildRing(
            IReadOnlyList<Vector3> groundPoints,
            Vector3 center,
            float ringWidth,
            float liftHeight,
            List<Vector3> vertices,
            List<int> triangles)
        {
            vertices.Clear();
            triangles.Clear();
            var count = groundPoints != null ? groundPoints.Count : 0;
            if (count < 3)
            {
                return;
            }

            for (var index = 0; index < count; index++)
            {
                var outer = groundPoints[index] + Vector3.up * liftHeight;
                var inward = new Vector3(center.x - outer.x, 0f, center.z - outer.z);
                var distance = inward.magnitude;
                var inset = distance > 0.0001f ? inward / distance * Mathf.Min(ringWidth, distance) : Vector3.zero;
                vertices.Add(outer);
                vertices.Add(outer + inset);
            }

            for (var index = 0; index < count; index++)
            {
                var outerA = index * 2;
                var innerA = outerA + 1;
                var outerB = (index + 1) % count * 2;
                var innerB = outerB + 1;
                triangles.Add(outerA);
                triangles.Add(innerA);
                triangles.Add(outerB);
                triangles.Add(outerB);
                triangles.Add(innerA);
                triangles.Add(innerB);
            }
        }

        private static bool IsIgnored(Collider collider, Transform ignoreRoot)
        {
            if (collider == null)
            {
                return true;
            }

            if (ignoreRoot != null && collider.transform.IsChildOf(ignoreRoot))
            {
                return true;
            }

            // 角色、可破壞物件與標記為略過的物件（房屋）不是地面。
            return collider.GetComponentInParent<IDamageable>() != null
                || collider.GetComponentInParent<RangeIndicatorIgnored>() != null;
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            _dirty = true;
        }

        private void LateUpdate()
        {
            if (_dirty || transform.position != _builtPosition || transform.rotation != _builtRotation)
            {
                Rebuild();
            }
        }

        private void EnsureInitialized()
        {
            if (_mesh != null)
            {
                return;
            }

            _mesh = new Mesh { name = $"{name} Mesh" };
            _mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = GetComponent<MeshRenderer>();
            _renderer.sharedMaterial = PrototypeMaterialLibrary.Get(PrototypeMaterialRole.RangeIndicator);
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _propertyBlock = new MaterialPropertyBlock();
            ApplyColor();
        }

        private void ApplyColor()
        {
            if (_renderer == null)
            {
                return;
            }

            _renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(BaseColorId, CurrentColor);
            _renderer.SetPropertyBlock(_propertyBlock);
        }

        private void OnDestroy()
        {
            if (_mesh == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(_mesh);
            }
            else
            {
                DestroyImmediate(_mesh);
            }
        }
    }
}
