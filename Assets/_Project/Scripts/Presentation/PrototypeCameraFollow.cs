using System;
using UnityEngine;

namespace CyberWizard.Presentation
{
    public enum PrototypeCameraMode
    {
        Overview,
        ThirdPerson
    }

    public sealed class PrototypeCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 overviewOffset = new Vector3(0f, 22f, -20f);
        [SerializeField, Min(0.1f)] private float followSpeed = 10f;
        [SerializeField, Min(1f)] private float thirdPersonDistance = 5.5f;
        [SerializeField, Min(0.1f)] private float mouseSensitivity = 2.5f;

        private float _yaw;
        private float _pitch = 24f;

        public event Action<PrototypeCameraMode> ModeChanged;

        public PrototypeCameraMode CurrentMode { get; private set; } = PrototypeCameraMode.Overview;

        public void Configure(Transform newTarget)
        {
            target = newTarget;
        }

        public void Configure(Transform newTarget, Vector3 newOffset)
        {
            target = newTarget;
            overviewOffset = newOffset;
        }

        private void Start()
        {
            if (target != null)
            {
                _yaw = target.eulerAngles.y;
            }

            ApplyCursorState();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                SetMode(CurrentMode == PrototypeCameraMode.Overview
                    ? PrototypeCameraMode.ThirdPerson
                    : PrototypeCameraMode.Overview);
            }

            if (CurrentMode == PrototypeCameraMode.ThirdPerson && Cursor.lockState == CursorLockMode.Locked)
            {
                _yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
                _pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
                _pitch = Mathf.Clamp(_pitch, 10f, 65f);
            }
        }

        public void SetMode(PrototypeCameraMode mode)
        {
            if (CurrentMode == mode)
            {
                ApplyCursorState();
                return;
            }

            CurrentMode = mode;
            ApplyCursorState();
            ModeChanged?.Invoke(CurrentMode);
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            if (CurrentMode == PrototypeCameraMode.Overview)
            {
                var desiredPosition = target.position + overviewOffset;
                transform.position = SmoothPosition(desiredPosition);
                transform.LookAt(target.position + Vector3.up * 0.5f);
                return;
            }

            var anchor = target.position + Vector3.up * 1.55f;
            var lookRotation = Quaternion.Euler(_pitch, _yaw, 0f);
            var shoulderOffset = lookRotation * new Vector3(0.65f, 0.25f, -thirdPersonDistance);
            transform.position = SmoothPosition(anchor + shoulderOffset);
            transform.rotation = lookRotation;
        }

        private Vector3 SmoothPosition(Vector3 desiredPosition)
        {
            return Vector3.Lerp(
                transform.position,
                desiredPosition,
                1f - Mathf.Exp(-followSpeed * Time.deltaTime));
        }

        private void ApplyCursorState()
        {
            var locked = CurrentMode == PrototypeCameraMode.ThirdPerson;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
            {
                ApplyCursorState();
            }
        }

        private void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
