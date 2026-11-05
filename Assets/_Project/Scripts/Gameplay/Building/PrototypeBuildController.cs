using System;
using CyberWizard.Gameplay.Capture;
using CyberWizard.Gameplay.Player;
using CyberWizard.Gameplay.Resources;
using CyberWizard.Gameplay.Towers;
using CyberWizard.Gameplay.Waves;
using UnityEngine;
using CyberWizard.Presentation;

namespace CyberWizard.Gameplay.Building
{
    public sealed class PrototypeBuildController : MonoBehaviour
    {
        private const int TowerCost = 25;
        private const int RepairCost = 5;

        [SerializeField] private PrototypePlayerController player;
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private TerritoryController territory;
        [SerializeField] private ResourceWallet wallet;
        [SerializeField] private TowerRegistry towerRegistry;
        [SerializeField] private WaveDirector waveDirector;

        private GameObject _preview;
        private bool _hasGroundPoint;
        private Vector3 _groundPoint;

        public event Action Changed;

        public bool IsBuildMode { get; private set; }
        public ElementType SelectedElement { get; private set; } = ElementType.Fire;
        public string StatusMessage { get; private set; } = "先前往據點完成佔領";

        public void Configure(
            PrototypePlayerController newPlayer,
            Camera newCamera,
            TerritoryController newTerritory,
            ResourceWallet newWallet,
            TowerRegistry newTowerRegistry,
            WaveDirector newWaveDirector)
        {
            player = newPlayer;
            gameplayCamera = newCamera;
            territory = newTerritory;
            wallet = newWallet;
            towerRegistry = newTowerRegistry;
            waveDirector = newWaveDirector;
        }

        private void Update()
        {
            if (player == null || !player.InputEnabled)
            {
                return;
            }

            HandleElementSelection();

            if (Input.GetKeyDown(KeyCode.B))
            {
                SetBuildMode(!IsBuildMode);
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                TryRepairNearestTower();
            }

            if (!IsBuildMode)
            {
                return;
            }

            UpdateGroundPoint();
            UpdatePreview();

            if (Input.GetMouseButtonDown(1))
            {
                SetBuildMode(false);
            }
            else if (Input.GetMouseButtonDown(0))
            {
                TryBuildTower();
            }
        }

        public bool TryBuildAt(Vector3 position, ElementType element)
        {
            _groundPoint = position;
            _groundPoint.y = 1f;
            _hasGroundPoint = true;
            SelectedElement = element;
            return TryBuildTower();
        }

        private void HandleElementSelection()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) SetElement(ElementType.Fire);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SetElement(ElementType.Water);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SetElement(ElementType.Wind);
            if (Input.GetKeyDown(KeyCode.Alpha4)) SetElement(ElementType.Earth);
        }

        private void SetElement(ElementType element)
        {
            SelectedElement = element;
            StatusMessage = $"已選擇 {GetElementName(element)}塔";
            Changed?.Invoke();
        }

        private void SetBuildMode(bool enabled)
        {
            IsBuildMode = enabled;
            if (enabled && _preview == null)
            {
                _preview = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                _preview.name = "Tower Build Preview";
                _preview.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
                var collider = _preview.GetComponent<Collider>();
                if (collider != null) collider.enabled = false;
            }

            if (_preview != null)
            {
                _preview.SetActive(enabled);
            }

            StatusMessage = enabled ? "左鍵建塔，右鍵取消" : "按 B 進入建造模式";
            Changed?.Invoke();
        }

        private void UpdateGroundPoint()
        {
            _hasGroundPoint = false;
            if (gameplayCamera == null)
            {
                return;
            }

            var pointerPosition = Cursor.lockState == CursorLockMode.Locked
                ? new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f)
                : Input.mousePosition;
            var ray = gameplayCamera.ScreenPointToRay(pointerPosition);
            var hits = Physics.RaycastAll(ray, 100f);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            for (var index = 0; index < hits.Length; index++)
            {
                if (hits[index].collider.gameObject.name == "Ground")
                {
                    _groundPoint = hits[index].point;
                    _groundPoint.y = 1f;
                    _hasGroundPoint = true;
                    break;
                }
            }
        }

        private void UpdatePreview()
        {
            if (_preview == null)
            {
                return;
            }

            _preview.SetActive(_hasGroundPoint);
            if (_hasGroundPoint)
            {
                _preview.transform.position = _groundPoint;
            }
        }

        private bool TryBuildTower()
        {
            var failure = ValidatePlacement();
            if (!string.IsNullOrEmpty(failure))
            {
                StatusMessage = failure;
                Changed?.Invoke();
                return false;
            }

            if (!wallet.TrySpend(TowerCost))
            {
                StatusMessage = "資源不足";
                Changed?.Invoke();
                return false;
            }

            var towerObject = new GameObject($"Tower_{SelectedElement}_{towerRegistry.Count + 1}");
            towerObject.transform.position = _groundPoint;

            var basePart = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            basePart.name = "Base";
            basePart.transform.SetParent(towerObject.transform, false);
            basePart.transform.localScale = new Vector3(1f, 0.5f, 1f);

            var topPart = GameObject.CreatePrimitive(GetTopPrimitive(SelectedElement));
            topPart.name = "Top";
            topPart.transform.SetParent(towerObject.transform, false);
            topPart.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            topPart.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f);

            var tower = towerObject.AddComponent<TowerController>();
            tower.Initialize(SelectedElement, waveDirector, towerRegistry);
            PrototypeMaterialLibrary.Apply(towerObject, PrototypeMaterialRole.Player);
            StatusMessage = $"已建造 {GetElementName(SelectedElement)}塔";
            Changed?.Invoke();
            return true;
        }

        private string ValidatePlacement()
        {
            if (!_hasGroundPoint) return "沒有有效的地面位置";
            if (territory == null || !territory.IsCaptured) return "必須先佔領據點";
            if (!territory.ContainsBuildPosition(_groundPoint)) return "超出已佔領區域";
            if (Mathf.Abs(_groundPoint.x) < 1.6f) return "不能建在敵人道路上";
            if (!towerRegistry.CanAdd) return "已達塔數上限";
            if (!towerRegistry.IsPositionFree(_groundPoint, 2f)) return "距離其他塔太近";
            if (!wallet.CanAfford(TowerCost)) return "資源不足";
            return string.Empty;
        }

        private void TryRepairNearestTower()
        {
            TowerController nearest = null;
            var bestDistance = 3.2f * 3.2f;
            foreach (var tower in towerRegistry.Towers)
            {
                if (tower == null || tower.Durability >= tower.MaxDurability)
                {
                    continue;
                }

                var distance = (tower.transform.position - player.transform.position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    nearest = tower;
                }
            }

            if (nearest == null)
            {
                StatusMessage = "附近沒有需要修理的塔";
            }
            else if (!wallet.TrySpend(RepairCost))
            {
                StatusMessage = "修理資源不足";
            }
            else
            {
                nearest.TryRepair(35f);
                StatusMessage = "已修理防禦塔";
            }

            Changed?.Invoke();
        }

        private static string GetElementName(ElementType element)
        {
            switch (element)
            {
                case ElementType.Fire: return "火";
                case ElementType.Water: return "水";
                case ElementType.Wind: return "風";
                case ElementType.Earth: return "地";
                default: return element.ToString();
            }
        }

        private static PrimitiveType GetTopPrimitive(ElementType element)
        {
            switch (element)
            {
                case ElementType.Fire: return PrimitiveType.Sphere;
                case ElementType.Water: return PrimitiveType.Cube;
                case ElementType.Wind: return PrimitiveType.Capsule;
                case ElementType.Earth: return PrimitiveType.Cylinder;
                default: return PrimitiveType.Cube;
            }
        }
    }
}
