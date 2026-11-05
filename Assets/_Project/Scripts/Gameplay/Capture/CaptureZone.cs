using System;
using CyberWizard.Gameplay.Player;
using UnityEngine;

namespace CyberWizard.Gameplay.Capture
{
    public sealed class CaptureZone : MonoBehaviour
    {
        [SerializeField] private PrototypePlayerController player;
        [SerializeField, Min(0.5f)] private float captureRadius = 2.5f;
        [SerializeField, Min(0.1f)] private float captureDuration = 2.5f;

        public event Action<float> ProgressChanged;
        public event Action Captured;

        public bool IsCaptured { get; private set; }
        public bool CaptureEnabled { get; set; } = true;
        public float Progress { get; private set; }
        public float NormalizedProgress => captureDuration <= 0f ? 1f : Progress / captureDuration;

        public void Configure(PrototypePlayerController newPlayer, float radius, float duration)
        {
            player = newPlayer;
            captureRadius = Mathf.Max(0.5f, radius);
            captureDuration = Mathf.Max(0.1f, duration);
        }

        private void Update()
        {
            if (!CaptureEnabled || IsCaptured || player == null || !player.IsAlive)
            {
                return;
            }

            var playerPosition = player.transform.position;
            var zonePosition = transform.position;
            playerPosition.y = 0f;
            zonePosition.y = 0f;
            var inside = (playerPosition - zonePosition).sqrMagnitude <= captureRadius * captureRadius;

            var previous = Progress;
            Progress = inside
                ? Mathf.Min(captureDuration, Progress + Time.deltaTime)
                : Mathf.Max(0f, Progress - Time.deltaTime * 0.5f);

            if (!Mathf.Approximately(previous, Progress))
            {
                ProgressChanged?.Invoke(NormalizedProgress);
            }

            if (Progress >= captureDuration)
            {
                IsCaptured = true;
                ProgressChanged?.Invoke(1f);
                Captured?.Invoke();
            }
        }
    }
}
