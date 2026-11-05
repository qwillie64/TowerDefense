using System;
using UnityEngine;

namespace CyberWizard.Gameplay.Neutral
{
    public sealed class NeutralOutpostModule : MonoBehaviour
    {
        [SerializeField] private bool featureEnabled = true;
        [SerializeField] private NeutralOutpost outpost;

        public event Action Changed;

        public bool FeatureEnabled => featureEnabled;
        public NeutralOutpost Outpost => outpost;

        public void Configure(bool enabled, NeutralOutpost neutralOutpost)
        {
            featureEnabled = enabled;
            outpost = neutralOutpost;
        }

        private void Start()
        {
            ApplyFeatureState();
        }

        public void SetFeatureEnabled(bool enabled)
        {
            featureEnabled = enabled;
            ApplyFeatureState();
            Changed?.Invoke();
        }

        private void ApplyFeatureState()
        {
            if (outpost != null)
            {
                outpost.gameObject.SetActive(featureEnabled);
            }
        }
    }
}
