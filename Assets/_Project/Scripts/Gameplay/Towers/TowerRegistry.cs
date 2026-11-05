using System;
using System.Collections.Generic;
using CyberWizard.Core;
using UnityEngine;

namespace CyberWizard.Gameplay.Towers
{
    public sealed class TowerRegistry : MonoBehaviour
    {
        [SerializeField, Min(1)] private int towerLimit = 4;
        private readonly List<TowerController> _towers = new List<TowerController>();

        public event Action Changed;

        public IReadOnlyList<TowerController> Towers => _towers;
        public int Count => _towers.Count;
        public int TowerLimit => towerLimit;
        public bool CanAdd => _towers.Count < towerLimit;
        public bool RangesVisible { get; private set; }

        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.T))
            {
                return;
            }

            RangesVisible = !RangesVisible;
            for (var index = 0; index < _towers.Count; index++)
            {
                if (_towers[index] != null)
                {
                    _towers[index].SetRangeVisible(RangesVisible);
                }
            }

            Changed?.Invoke();
        }

        public void Configure(int limit)
        {
            towerLimit = Mathf.Max(1, limit);
        }

        public void Register(TowerController tower)
        {
            if (tower == null || _towers.Contains(tower))
            {
                return;
            }

            _towers.Add(tower);
            tower.SetRangeVisible(RangesVisible);
            Changed?.Invoke();
        }

        public void Unregister(TowerController tower)
        {
            if (_towers.Remove(tower))
            {
                Changed?.Invoke();
            }
        }

        public IDamageable FindClosestActive(Vector3 position, float maxDistance)
        {
            TowerController result = null;
            var bestDistance = maxDistance * maxDistance;
            for (var index = _towers.Count - 1; index >= 0; index--)
            {
                var tower = _towers[index];
                if (tower == null)
                {
                    _towers.RemoveAt(index);
                    continue;
                }

                if (!tower.IsAlive || !tower.IsOperational)
                {
                    continue;
                }

                var distance = (tower.transform.position - position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    result = tower;
                }
            }

            return result;
        }

        public bool IsPositionFree(Vector3 position, float minimumDistance)
        {
            var threshold = minimumDistance * minimumDistance;
            for (var index = 0; index < _towers.Count; index++)
            {
                var tower = _towers[index];
                if (tower != null && (tower.transform.position - position).sqrMagnitude < threshold)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
