using CyberWizard.Infrastructure;
using UnityEngine;

namespace CyberWizard.Presentation
{
    public sealed class PrototypeCameraFollow : MonoBehaviour
    {
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
            var look = GameInput.Look;
            _yaw += look.x * sensitivity;
            _pitch -= look.y * sensitivity;
            _pitch = Mathf.Clamp(_pitch, 10f, 65f);
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

            var anchor = target.position + Vector3.up * 1.55f;
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
