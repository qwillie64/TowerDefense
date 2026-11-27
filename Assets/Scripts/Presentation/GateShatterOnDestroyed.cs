using System;
using CyberWizard.Gameplay.GameFlow;
using UnityEngine;
using UnityEngine.Events;

namespace CyberWizard.Presentation
{
    /// <summary>
    /// 閘門被擊垮時的破碎效果：監聽 <see cref="BaseCore.Destroyed"/>，呼叫同物件上 OpenFracture 的
    /// <see cref="Fracture.CauseFracture"/> 把網格切成碎片，再對碎片施加一次爆炸速度。
    /// 碎片數量、內部切面材質等破碎參數在同物件的 Fracture 元件上調整；
    /// 平時物件上的 Rigidbody 需設為 Kinematic，讓門固定不動。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Fracture))]
    public sealed class GateShatterOnDestroyed : MonoBehaviour
    {
        [SerializeField, Tooltip("血量歸零時觸發破碎的閘門核心。")]
        private BaseCore gate;

        [Header("爆炸（暫定值）")]
        [SerializeField, Tooltip("爆炸中心相對於本物件中心的世界座標位移。預設在門的北側下方，碎片往南（關內、玩家所在側）與上方噴散；閘門北側是地圖邊界，往北的碎片會掉出地圖。")]
        private Vector3 explosionOffset = new Vector3(0f, -2f, 3f);

        [SerializeField, Min(0f), Tooltip("爆炸給碎片的速度變化量（與質量無關）。")]
        private float explosionStrength = 7f;

        [SerializeField, Min(0.1f), Tooltip("爆炸影響半徑；需涵蓋整扇門。")]
        private float explosionRadius = 25f;

        [SerializeField, Min(0f), Tooltip("把爆炸點往下移的量，數值越大碎片越往上噴。")]
        private float upwardsModifier = 1.5f;

        private Fracture _fracture;
        private Vector3 _explosionCenter;

        /// <summary>破碎完成、碎片已施力後觸發。</summary>
        public event Action<GateShatterOnDestroyed> Shattered;

        public bool IsShattered { get; private set; }

        /// <summary>OpenFracture 建立的碎片父物件（破碎前為 null）。</summary>
        public Transform FragmentRoot { get; private set; }

        private void Awake()
        {
            _fracture = GetComponent<Fracture>();
            if (gate == null)
            {
                Debug.LogError($"{name} 的 GateShatterOnDestroyed 沒有設定閘門，不會產生破碎效果。", this);
            }

            var body = GetComponent<Rigidbody>();
            if (body != null && !body.isKinematic)
            {
                Debug.LogWarning($"{name} 的 Rigidbody 不是 Kinematic，門在破碎前就會受物理影響。", this);
            }
        }

        private void OnEnable()
        {
            if (gate != null)
            {
                gate.Destroyed += HandleGateDestroyed;
            }
        }

        private void OnDisable()
        {
            if (gate != null)
            {
                gate.Destroyed -= HandleGateDestroyed;
            }
        }

        private void HandleGateDestroyed()
        {
            if (IsShattered || _fracture == null)
            {
                return;
            }

            IsShattered = true;
            _explosionCenter = transform.position + explosionOffset;

            // OpenFracture 在破碎完成後（同步或非同步）呼叫 onCompleted。
            var callbacks = _fracture.callbackOptions ?? (_fracture.callbackOptions = new CallbackOptions());
            if (callbacks.onCompleted == null)
            {
                callbacks.onCompleted = new UnityEvent();
            }

            callbacks.onCompleted.AddListener(HandleFractureCompleted);
            _fracture.CauseFracture();
        }

        private void HandleFractureCompleted()
        {
            _fracture.callbackOptions.onCompleted.RemoveListener(HandleFractureCompleted);

            // 碎片父物件由 OpenFracture 建立在本物件的父物件底下，名稱為「物件名稱 + Fragments」。
            FragmentRoot = transform.parent != null
                ? transform.parent.Find($"{name}Fragments")
                : GameObject.Find($"{name}Fragments")?.transform;
            if (FragmentRoot == null)
            {
                Debug.LogWarning($"{name} 破碎後找不到碎片物件。", this);
            }
            else
            {
                foreach (var body in FragmentRoot.GetComponentsInChildren<Rigidbody>())
                {
                    // 關卡結束會放慢遊戲時間但不縮小物理步長，開啟插值讓碎片在慢動作中仍平滑移動。
                    body.interpolation = RigidbodyInterpolation.Interpolate;
                    body.AddExplosionForce(
                        explosionStrength,
                        _explosionCenter,
                        explosionRadius,
                        upwardsModifier,
                        ForceMode.VelocityChange);
                }
            }

            Shattered?.Invoke(this);
        }
    }
}
