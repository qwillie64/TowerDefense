using System;
using UnityEngine;

namespace CyberWizard.Core
{
    public sealed class Vitality : MonoBehaviour
    {
        public event Action<float, float> Changed;
        public event Action Depleted;

        public float Current { get; private set; }
        public float Maximum { get; private set; } = 1f;
        public bool IsAlive => Current > 0f;

        public void Initialize(float maximum, bool refill = true)
        {
            Maximum = Mathf.Max(1f, maximum);
            Current = refill ? Maximum : Mathf.Clamp(Current, 0f, Maximum);
            Changed?.Invoke(Current, Maximum);
        }

        public bool TakeDamage(float amount)
        {
            if (!IsAlive || amount <= 0f)
            {
                return false;
            }

            var previous = Current;
            Current = Mathf.Max(0f, Current - amount);
            if (Mathf.Approximately(previous, Current))
            {
                return false;
            }

            Changed?.Invoke(Current, Maximum);
            if (Current <= 0f)
            {
                Depleted?.Invoke();
            }

            return true;
        }

        public bool Restore(float amount)
        {
            if (amount <= 0f || Current >= Maximum)
            {
                return false;
            }

            Current = Mathf.Min(Maximum, Current + amount);
            Changed?.Invoke(Current, Maximum);
            return true;
        }

        public bool IncreaseMaximum(float amount, bool restoreAddedAmount = true)
        {
            if (amount <= 0f)
            {
                return false;
            }

            Maximum += amount;
            if (restoreAddedAmount)
            {
                Current = Mathf.Min(Maximum, Current + amount);
            }

            Changed?.Invoke(Current, Maximum);
            return true;
        }
    }
}
