using System.Collections.Generic;
using CyberWizard.Core;
using UnityEngine;

namespace CyberWizard.Gameplay.Neutral
{
    public sealed class SoldierRegistry : MonoBehaviour
    {
        private readonly List<SoldierAgent> _soldiers = new List<SoldierAgent>();

        public void Register(SoldierAgent soldier)
        {
            if (soldier != null && !_soldiers.Contains(soldier))
            {
                _soldiers.Add(soldier);
            }
        }

        public void Unregister(SoldierAgent soldier)
        {
            _soldiers.Remove(soldier);
        }

        public SoldierAgent FindClosest(
            Faction faction,
            Vector3 position,
            float maxDistance)
        {
            SoldierAgent result = null;
            var bestDistance = maxDistance * maxDistance;
            for (var index = _soldiers.Count - 1; index >= 0; index--)
            {
                var soldier = _soldiers[index];
                if (soldier == null)
                {
                    _soldiers.RemoveAt(index);
                    continue;
                }

                if (!soldier.IsAlive || soldier.Faction != faction)
                {
                    continue;
                }

                var distance = (soldier.transform.position - position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    result = soldier;
                }
            }

            return result;
        }

        public void CollectInRadius(
            Faction faction,
            Vector3 position,
            float radius,
            List<SoldierAgent> results)
        {
            if (results == null)
            {
                return;
            }

            var threshold = radius * radius;
            for (var index = _soldiers.Count - 1; index >= 0; index--)
            {
                var soldier = _soldiers[index];
                if (soldier == null)
                {
                    _soldiers.RemoveAt(index);
                    continue;
                }

                if (soldier.IsAlive && soldier.Faction == faction &&
                    (soldier.transform.position - position).sqrMagnitude <= threshold)
                {
                    results.Add(soldier);
                }
            }
        }
    }
}
