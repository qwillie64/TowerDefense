using CyberWizard.Gameplay.Enemies;
using CyberWizard.Gameplay.Waves;
using UnityEngine;

namespace CyberWizard.Gameplay.Resources
{
    public sealed class RewardService : MonoBehaviour
    {
        [SerializeField] private ResourceWallet wallet;
        [SerializeField] private WaveDirector waveDirector;

        private void Start()
        {
            if (waveDirector != null)
            {
                waveDirector.EnemyDefeated += HandleEnemyDefeated;
            }
        }

        private void HandleEnemyDefeated(EnemyAgent enemy)
        {
            if (enemy == null || wallet == null || enemy.Reward <= 0)
            {
                return;
            }

            wallet.Add(enemy.Reward);
        }

        private void OnDestroy()
        {
            if (waveDirector != null)
            {
                waveDirector.EnemyDefeated -= HandleEnemyDefeated;
            }
        }
    }
}
