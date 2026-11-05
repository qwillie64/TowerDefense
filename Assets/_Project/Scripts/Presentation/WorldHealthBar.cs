using UnityEngine;

namespace CyberWizard.Presentation
{
    public sealed class WorldHealthBar : MonoBehaviour
    {
        private const float DefaultWidth = 1.8f;
        private Transform _fill;
        private float _width;
        private Camera _camera;

        public static WorldHealthBar Create(
            Transform owner,
            Vector3 localOffset,
            PrototypeMaterialRole fillRole,
            float width = DefaultWidth)
        {
            var root = new GameObject("World Health Bar");
            root.transform.SetParent(owner, false);
            root.transform.localPosition = localOffset;

            var bar = root.AddComponent<WorldHealthBar>();
            bar._width = Mathf.Max(0.5f, width);
            bar.Build(fillRole);
            return bar;
        }

        private void Build(PrototypeMaterialRole fillRole)
        {
            _camera = Camera.main;

            var background = GameObject.CreatePrimitive(PrimitiveType.Cube);
            background.name = "Background";
            background.transform.SetParent(transform, false);
            background.transform.localScale = new Vector3(_width, 0.16f, 0.08f);
            RemoveCollider(background);
            PrototypeMaterialLibrary.Apply(background, PrototypeMaterialRole.Map);

            var fill = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fill.name = "Fill";
            fill.transform.SetParent(transform, false);
            fill.transform.localPosition = new Vector3(0f, 0f, -0.06f);
            fill.transform.localScale = new Vector3(_width, 0.11f, 0.06f);
            RemoveCollider(fill);
            _fill = fill.transform;
            PrototypeMaterialLibrary.Apply(fill, fillRole);
        }

        public void SetValue(float current, float maximum)
        {
            if (_fill == null)
            {
                return;
            }

            var ratio = maximum <= 0f ? 0f : Mathf.Clamp01(current / maximum);
            var fillWidth = _width * ratio;
            _fill.localScale = new Vector3(fillWidth, 0.11f, 0.06f);
            _fill.localPosition = new Vector3(-(_width - fillWidth) * 0.5f, 0f, -0.06f);
        }

        public void SetFillRole(PrototypeMaterialRole role)
        {
            if (_fill != null)
            {
                PrototypeMaterialLibrary.Apply(_fill.gameObject, role);
            }
        }

        private void LateUpdate()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
            }

            if (_camera != null)
            {
                transform.rotation = _camera.transform.rotation;
            }
        }

        private static void RemoveCollider(GameObject target)
        {
            var collider = target.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }
        }
    }
}
