using UnityEngine;

namespace CyberWizard.Presentation
{
    public sealed class PrototypePulseEffect : MonoBehaviour
    {
        private Vector3 _targetScale;
        private float _duration;
        private float _elapsed;

        public static void Spawn(
            Vector3 position,
            PrototypeMaterialRole role,
            Vector3 targetScale,
            float duration = 0.35f)
        {
            var effectObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            effectObject.name = $"{role} Pulse Effect";
            effectObject.transform.position = position;
            effectObject.transform.localScale = Vector3.one * 0.1f;
            var collider = effectObject.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            PrototypeMaterialLibrary.Apply(effectObject, role);
            var effect = effectObject.AddComponent<PrototypePulseEffect>();
            effect._targetScale = targetScale;
            effect._duration = Mathf.Max(0.05f, duration);
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            var normalized = Mathf.Clamp01(_elapsed / _duration);
            transform.localScale = Vector3.Lerp(Vector3.one * 0.1f, _targetScale, normalized);
            transform.Rotate(Vector3.up, 180f * Time.deltaTime, Space.World);
            if (_elapsed >= _duration)
            {
                Destroy(gameObject);
            }
        }
    }
}
