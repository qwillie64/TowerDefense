using System;
using UnityEngine;

namespace CyberWizard.Gameplay.Resources
{
    public sealed class ResourceWallet : MonoBehaviour
    {
        [SerializeField, Min(0)] private int startingBalance = 100;

        public event Action<int> Changed;

        public int Balance { get; private set; }

        public void Configure(int initialBalance)
        {
            startingBalance = Mathf.Max(0, initialBalance);
            Balance = startingBalance;
        }

        private void Awake()
        {
            Balance = startingBalance;
        }

        public bool CanAfford(int amount)
        {
            return amount >= 0 && Balance >= amount;
        }

        public bool TrySpend(int amount)
        {
            if (!CanAfford(amount))
            {
                return false;
            }

            Balance -= amount;
            Changed?.Invoke(Balance);
            return true;
        }

        public void Add(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            Balance += amount;
            Changed?.Invoke(Balance);
        }
    }
}
