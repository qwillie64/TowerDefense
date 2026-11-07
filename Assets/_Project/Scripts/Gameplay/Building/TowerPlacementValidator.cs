using CyberWizard.Gameplay.Capture;
using CyberWizard.Gameplay.Resources;
using CyberWizard.Gameplay.Towers;
using UnityEngine;

namespace CyberWizard.Gameplay.Building
{
    public enum TowerPlacementFailure
    {
        None,
        NoGroundPoint,
        TerritoryLocked,
        OutsideTerritory,
        OnEnemyRoad,
        TowerLimitReached,
        TooCloseToTower,
        MissingDefinition,
        InsufficientResources
    }

    public readonly struct TowerPlacementResult
    {
        public TowerPlacementResult(TowerPlacementFailure failure, string message)
        {
            Failure = failure;
            Message = message;
        }

        public TowerPlacementFailure Failure { get; }
        public string Message { get; }
        public bool IsValid => Failure == TowerPlacementFailure.None;
    }

    public sealed class TowerPlacementValidator
    {
        private readonly TerritoryController _territory;
        private readonly TowerRegistry _towerRegistry;
        private readonly ResourceWallet _wallet;
        private readonly float _roadHalfWidth;
        private readonly float _minimumTowerSpacing;

        public TowerPlacementValidator(
            TerritoryController territory,
            TowerRegistry towerRegistry,
            ResourceWallet wallet,
            float roadHalfWidth = 1.6f,
            float minimumTowerSpacing = 2f)
        {
            _territory = territory;
            _towerRegistry = towerRegistry;
            _wallet = wallet;
            _roadHalfWidth = Mathf.Max(0f, roadHalfWidth);
            _minimumTowerSpacing = Mathf.Max(0f, minimumTowerSpacing);
        }

        public TowerPlacementResult Validate(
            Vector3 position,
            bool hasGroundPoint,
            TowerDefinition definition)
        {
            var territoryReady = _territory != null && _territory.IsCaptured;
            var insideTerritory = _territory != null && _territory.ContainsBuildPosition(position);
            var isOnRoad = Mathf.Abs(position.x) < _roadHalfWidth;
            var registryCanAdd = _towerRegistry != null && _towerRegistry.CanAdd;
            var positionIsFree = _towerRegistry != null &&
                                 _towerRegistry.IsPositionFree(position, _minimumTowerSpacing);
            var hasDefinition = definition != null;
            var canAfford = hasDefinition && _wallet != null &&
                            _wallet.CanAfford(definition.RuntimeStats.BuildCost);

            return Evaluate(
                hasGroundPoint,
                territoryReady,
                insideTerritory,
                isOnRoad,
                registryCanAdd,
                positionIsFree,
                hasDefinition,
                canAfford);
        }

        public static TowerPlacementResult Evaluate(
            bool hasGroundPoint,
            bool territoryReady,
            bool insideTerritory,
            bool isOnRoad,
            bool registryCanAdd,
            bool positionIsFree,
            bool hasDefinition,
            bool canAfford)
        {
            if (!hasGroundPoint)
                return Failure(TowerPlacementFailure.NoGroundPoint, "沒有有效的地面位置");
            if (!territoryReady)
                return Failure(TowerPlacementFailure.TerritoryLocked, "必須先佔領據點");
            if (!insideTerritory)
                return Failure(TowerPlacementFailure.OutsideTerritory, "超出已佔領區域");
            if (isOnRoad)
                return Failure(TowerPlacementFailure.OnEnemyRoad, "不能建在敵人道路上");
            if (!registryCanAdd)
                return Failure(TowerPlacementFailure.TowerLimitReached, "已達塔數上限");
            if (!positionIsFree)
                return Failure(TowerPlacementFailure.TooCloseToTower, "距離其他塔太近");
            if (!hasDefinition)
                return Failure(TowerPlacementFailure.MissingDefinition, "找不到所選元素塔的資料");
            if (!canAfford)
                return Failure(TowerPlacementFailure.InsufficientResources, "資源不足");

            return new TowerPlacementResult(TowerPlacementFailure.None, string.Empty);
        }

        private static TowerPlacementResult Failure(
            TowerPlacementFailure failure,
            string message)
        {
            return new TowerPlacementResult(failure, message);
        }
    }
}
