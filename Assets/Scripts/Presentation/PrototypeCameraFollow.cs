using CyberWizard.Infrastructure;
using UnityEngine;

namespace CyberWizard.Presentation
{
    public sealed class PrototypeCameraFollow : MonoBehaviour
    {
        private const float AnchorHeight = 1.55f;
        private const float MinPitch = 10f;
        private const float MaxPitch = 65f;

        [SerializeField] private Transform target;
        [SerializeField, Min(0.1f)] private float followSpeed = 10f;
        [SerializeField, Min(1f)] private float distance = 5.5f;
        [SerializeField, Min(0.1f)] private float mouseSensitivity = 2.5f;
        [SerializeField] private Vector3 shoulderOffset = new Vector3(0.65f, 0.25f, 0f);

        private float _yaw;
        private float _pitch = 24f;
        private bool _gameplayActive;

        public bool GameplayActive => _gameplayActive;

        private void Start()
        {
            if (target != null)
            {
                _yaw = target.eulerAngles.y;
                ApplyCameraTransform(true);
            }

            ApplyCursorState();
        }

        private void Update()
        {
            if (!_gameplayActive || Cursor.lockState != CursorLockMode.Locked)
            {
                return;
            }

            // 滑鼠位移本身與時間無關，乘上 timeScale 讓視角轉動跟著遊戲時間變慢（例如關卡結束的慢動作）。
            var sensitivity = mouseSensitivity * Time.timeScale;
            _yaw += Input.GetAxis("Mouse X") * sensitivity;
            _pitch -= Input.GetAxis("Mouse Y") * sensitivity;
            _pitch = Mathf.Clamp(_pitch, MinPitch, MaxPitch);
        }

        /// <summary>立即把視角轉向指定的世界座標（俯仰角仍受上下限制），不經過平滑跟隨。</summary>
        public void LookAt(Vector3 worldPoint)
        {
            if (target == null)
            {
                return;
            }

            var direction = worldPoint - (target.position + Vector3.up * AnchorHeight);
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            direction.Normalize();
            _yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            _pitch = Mathf.Clamp(-Mathf.Asin(direction.y) * Mathf.Rad2Deg, MinPitch, MaxPitch);
            ApplyCameraTransform(true);
        }

        public void SetGameplayActive(bool active)
        {
            _gameplayActive = active;
            ApplyCursorState();
        }

        private void LateUpdate()
        {
            ApplyCameraTransform(false);
        }

        private void ApplyCameraTransform(bool immediate)
        {
            if (target == null)
            {
                return;
            }

            var anchor = target.position + Vector3.up * AnchorHeight;
            var lookRotation = Quaternion.Euler(_pitch, _yaw, 0f);
            var desiredPosition = anchor + lookRotation *
                new Vector3(shoulderOffset.x, shoulderOffset.y, -distance);
            transform.position = immediate
                ? desiredPosition
                : Vector3.Lerp(
                    transform.position,
                    desiredPosition,
                    1f - Mathf.Exp(-followSpeed * Time.deltaTime));
            transform.rotation = lookRotation;
        }

        private void ApplyCursorState()
        {
            Cursor.lockState = _gameplayActive ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !_gameplayActive;
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
