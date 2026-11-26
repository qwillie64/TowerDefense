using UnityEngine;
using System.Collections.Generic;

namespace CyberWizard.Presentation
{
    public sealed class PrototypePulseEffect : MonoBehaviour
    {
        private static readonly Stack<PrototypePulseEffect> Pool = new Stack<PrototypePulseEffect>();

        private Vector3 _targetScale;
        private float _duration;
        private float _elapsed;
        private bool _isPooled;

        public static int CreatedCount { get; private set; }
        public static int ReusedCount { get; private set; }

        public static void Spawn(
            Vector3 position,
            PrototypeMaterialRole role,
            Vector3 targetScale,
            float duration = 0.35f)
        {
            PrototypePulseEffect effect = null;
            while (Pool.Count > 0 && effect == null)
            {
                effect = Pool.Pop();
            }

            if (effect == null)
            {
                var effectObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                var collider = effectObject.GetComponent<Collider>();
                if (collider != null)
                {
                    Destroy(collider);
                }

                effect = effectObject.AddComponent<PrototypePulseEffect>();
                CreatedCount++;
            }
            else
            {
                ReusedCount++;
            }

            effect.name = $"{role} Pulse Effect";
            effect.transform.position = position;
            effect.transform.localScale = Vector3.one * 0.1f;
            effect._elapsed = 0f;
            effect._isPooled = false;
            effect._targetScale = targetScale;
            effect._duration = Mathf.Max(0.05f, duration);
            PrototypeMaterialLibrary.Apply(effect.gameObject, role);
            effect.gameObject.SetActive(true);
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            var normalized = Mathf.Clamp01(_elapsed / _duration);
            transform.localScale = Vector3.Lerp(Vector3.one * 0.1f, _targetScale, normalized);
            transform.Rotate(Vector3.up, 180f * Time.deltaTime, Space.World);
            if (_elapsed >= _duration)
            {
                Release();
            }
        }

        private void Release()
        {
            if (_isPooled)
            {
                return;
            }

            _isPooled = true;
            gameObject.SetActive(false);
            Pool.Push(this);
        }
    }
}
