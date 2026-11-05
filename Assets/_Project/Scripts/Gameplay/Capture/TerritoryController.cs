using System;
using UnityEngine;
using CyberWizard.Presentation;

namespace CyberWizard.Gameplay.Capture
{
    public sealed class TerritoryController : MonoBehaviour
    {
        [SerializeField] private CaptureZone captureZone;
        [SerializeField] private Vector2 buildAreaSize = new Vector2(17f, 12f);

        public event Action Captured;

        public bool IsCaptured { get; private set; }
        public CaptureZone CaptureZone => captureZone;
        private WorldHealthBar _captureBar;

        public void Configure(CaptureZone zone, Vector2 areaSize)
        {
            captureZone = zone;
            buildAreaSize = areaSize;
        }

        private void OnEnable()
        {
            if (captureZone != null)
            {
                captureZone.Captured += HandleCaptured;
                captureZone.ProgressChanged += HandleProgressChanged;
            }
        }

        private void Start()
        {
            if (captureZone != null)
            {
                _captureBar = WorldHealthBar.Create(
                    captureZone.transform,
                    new Vector3(0f, 1.1f, 0f),
                    PrototypeMaterialRole.Neutral,
                    2.5f);
                _captureBar.SetValue(captureZone.NormalizedProgress, 1f);
            }
        }

        private void OnDisable()
        {
            if (captureZone != null)
            {
                captureZone.Captured -= HandleCaptured;
                captureZone.ProgressChanged -= HandleProgressChanged;
            }
        }

        public bool ContainsBuildPosition(Vector3 position)
        {
            var local = transform.InverseTransformPoint(position);
            return Mathf.Abs(local.x) <= buildAreaSize.x * 0.5f &&
                   Mathf.Abs(local.z) <= buildAreaSize.y * 0.5f;
        }

        private void HandleCaptured()
        {
            IsCaptured = true;
            PrototypeMaterialLibrary.Apply(gameObject, PrototypeMaterialRole.Player);
            if (_captureBar != null)
            {
                _captureBar.SetFillRole(PrototypeMaterialRole.Player);
                _captureBar.SetValue(1f, 1f);
            }
            PrototypePulseEffect.Spawn(
                captureZone.transform.position + Vector3.up * 0.4f,
                PrototypeMaterialRole.Player,
                new Vector3(4f, 0.3f, 4f),
                0.5f);
            Captured?.Invoke();
        }

        private void HandleProgressChanged(float normalizedProgress)
        {
            if (_captureBar != null)
            {
                _captureBar.SetValue(normalizedProgress, 1f);
            }
        }
    }
}
