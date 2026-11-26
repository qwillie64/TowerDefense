using UnityEngine;

namespace CyberWizard.Core
{
    public interface IDamageable
    {
        bool IsAlive { get; }
        Faction Faction { get; }
        Transform TargetTransform { get; }
        void TakeDamage(float amount);
    }
}
