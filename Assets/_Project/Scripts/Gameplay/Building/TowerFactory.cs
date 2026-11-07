using System;
using CyberWizard.Gameplay.Neutral;
using CyberWizard.Gameplay.Towers;
using CyberWizard.Gameplay.Waves;
using CyberWizard.Presentation;
using UnityEngine;

namespace CyberWizard.Gameplay.Building
{
    public sealed class TowerFactory
    {
        private readonly WaveDirector _waveDirector;
        private readonly TowerRegistry _towerRegistry;
        private readonly SoldierRegistry _soldierRegistry;

        public TowerFactory(
            WaveDirector waveDirector,
            TowerRegistry towerRegistry,
            SoldierRegistry soldierRegistry)
        {
            _waveDirector = waveDirector;
            _towerRegistry = towerRegistry;
            _soldierRegistry = soldierRegistry;
        }

        public bool TryCreate(
            TowerDefinition definition,
            Vector3 position,
            out TowerController tower,
            out string error)
        {
            tower = null;
            error = string.Empty;
            if (definition == null || _waveDirector == null || _towerRegistry == null)
            {
                error = "塔工廠缺少定義或必要服務";
                return false;
            }

            GameObject towerObject = null;
            try
            {
                towerObject = new GameObject(
                    $"Tower_{definition.Element}_{_towerRegistry.Count + 1}");
                towerObject.transform.position = position;

                CreatePart(
                    towerObject.transform,
                    "Base",
                    definition.BasePart,
                    PrimitiveType.Cylinder,
                    Vector3.zero,
                    new Vector3(1f, 0.5f, 1f));
                CreatePart(
                    towerObject.transform,
                    "Body",
                    definition.BodyPart,
                    PrimitiveType.Cube,
                    new Vector3(0f, 0.85f, 0f),
                    new Vector3(0.65f, 0.8f, 0.65f));
                CreatePart(
                    towerObject.transform,
                    "Top",
                    definition.TopPart,
                    definition.PrototypeTopShape,
                    new Vector3(0f, 1.65f, 0f),
                    new Vector3(0.7f, 0.7f, 0.7f));

                PrototypeMaterialLibrary.Apply(towerObject, PrototypeMaterialRole.Player);
                tower = towerObject.AddComponent<TowerController>();
                tower.Initialize(
                    definition,
                    _waveDirector,
                    _towerRegistry,
                    _soldierRegistry);
                return true;
            }
            catch (Exception exception)
            {
                error = $"建立防禦塔失敗：{exception.Message}";
                Debug.LogException(exception);
                if (towerObject != null)
                {
                    UnityEngine.Object.Destroy(towerObject);
                }
                tower = null;
                return false;
            }
        }

        private static void CreatePart(
            Transform parent,
            string objectName,
            TowerPartDefinition part,
            PrimitiveType fallbackShape,
            Vector3 localPosition,
            Vector3 fallbackScale)
        {
            var partObject = GameObject.CreatePrimitive(
                part != null ? part.PrototypeShape : fallbackShape);
            partObject.name = objectName;
            partObject.transform.SetParent(parent, false);
            partObject.transform.localPosition = localPosition;
            partObject.transform.localScale = part != null
                ? part.PrototypeScale
                : fallbackScale;
        }
    }
}
