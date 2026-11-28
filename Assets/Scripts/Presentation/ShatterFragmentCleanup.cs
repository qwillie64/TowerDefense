using System.Collections.Generic;
using UnityEngine;

namespace CyberWizard.Presentation
{
    /// <summary>
    /// 破碎碎片的清除：掛在碎片父物件上，由 <see cref="GateShatterOnDestroyed"/> 在破碎完成後建立。
    /// - 碎片重心低於 killHeight（掉出地圖）時立即移除。
    /// - 經過 lifetime 遊戲秒數後，所有碎片在 shrinkDuration 內縮小到 0，最後連同父物件一起移除。
    /// 時間使用 Time.deltaTime，會跟著結束慢動作一起放慢；停用本元件會暫停計時與檢查。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShatterFragmentCleanup : MonoBehaviour
    {
        private readonly List<Rigidbody> _fragments = new List<Rigidbody>();
        private readonly List<Vector3> _initialScales = new List<Vector3>();
        private float _lifetime;
        private float _shrinkDuration;
        private float _killHeight;
        private float _elapsed;
        private bool _initialized;

        /// <summary>目前仍存在的碎片數量。</summary>
        public int RemainingCount
        {
            get
            {
                var count = 0;
                foreach (var fragment in _fragments)
                {
                    if (fragment != null)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public float Lifetime => _lifetime;
        public float ShrinkDuration => _shrinkDuration;
        public float KillHeight => _killHeight;

        /// <summary>收集子物件的碎片剛體並開始計時。</summary>
        public void Initialize(float lifetime, float shrinkDuration, float killHeight)
        {
            _lifetime = Mathf.Max(0f, lifetime);
            _shrinkDuration = Mathf.Max(0.01f, shrinkDuration);
            _killHeight = killHeight;
            _elapsed = 0f;
            _fragments.Clear();
            _initialScales.Clear();
            GetComponentsInChildren(_fragments);
            foreach (var fragment in _fragments)
            {
                _initialScales.Add(fragment.transform.localScale);
            }

            _initialized = true;
        }

        private void Update()
        {
            if (!_initialized)
            {
                return;
            }

            _elapsed += Time.deltaTime;
            var shrinkProgress = _elapsed <= _lifetime ? 0f : (_elapsed - _lifetime) / _shrinkDuration;
            if (shrinkProgress >= 1f)
            {
                Destroy(gameObject);
                _initialized = false;
                return;
            }

            for (var index = 0; index < _fragments.Count; index++)
            {
                var fragment = _fragments[index];
                if (fragment == null)
                {
                    continue;
                }

                if (fragment.worldCenterOfMass.y < _killHeight)
                {
                    Destroy(fragment.gameObject);
                    _fragments[index] = null;
                    continue;
                }

                if (shrinkProgress > 0f)
                {
                    fragment.transform.localScale = _initialScales[index] * (1f - shrinkProgress);
                }
            }
        }
    }
}
