using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CyberWizard.Gameplay.Building;
using CyberWizard.Gameplay.Capture;
using CyberWizard.Gameplay.Combo;
using CyberWizard.Gameplay.Enemies;
using CyberWizard.Gameplay.GameFlow;
using CyberWizard.Gameplay.Neutral;
using CyberWizard.Gameplay.Player;
using CyberWizard.Gameplay.Resources;
using CyberWizard.Gameplay.Towers;
using CyberWizard.Gameplay.Waves;
using CyberWizard.Presentation;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace CyberWizard.EditorTools
{
    public static class VerticalSliceSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/VerticalSlice.unity";
        private const string PanelSettingsPath = "Assets/_Project/UI/PrototypePanelSettings.asset";
        private const string HudUxmlPath = "Assets/_Project/UI/PrototypeHud.uxml";
        private const string PaletteFolder = "Assets/_Project/Resources/PrototypePalette";

        [MenuItem("CyberWizard/Build Vertical Slice Scene")]
        public static void Build()
        {
            EnsureAssetFolders();
            CreatePrototypeMaterials();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "VerticalSlice";

            var camera = CreateCamera();
            CreateDirectionalLight();
            CreateEnvironment(out var spawnPoint, out var destination, out var baseCore);
            var player = CreatePlayer();
            var cameraController = camera.gameObject.AddComponent<PrototypeCameraFollow>();
            cameraController.Configure(
                player.transform,
                new Vector3(0f, 22f, -20f));
            var territory = CreateTerritory(player);

            var systems = new GameObject("Prototype Systems");
            var wallet = systems.AddComponent<ResourceWallet>();
            wallet.Configure(100);

            var towerRegistry = systems.AddComponent<TowerRegistry>();
            towerRegistry.Configure(4);

            var enemySpawner = systems.AddComponent<EnemySpawner>();

            var waveDirector = systems.AddComponent<WaveDirector>();
            waveDirector.Configure(enemySpawner);

            enemySpawner.Configure(
                spawnPoint,
                destination,
                towerRegistry,
                player,
                baseCore);

            var buildController = systems.AddComponent<PrototypeBuildController>();
            buildController.Configure(
                player,
                camera,
                territory,
                wallet,
                towerRegistry,
                waveDirector);

            var comboSystem = systems.AddComponent<PrototypeComboSystem>();
            comboSystem.Configure(camera, towerRegistry, waveDirector);

            var neutralModule = CreateNeutralOutpostModule(player, waveDirector);
            enemySpawner.SetNeutralModule(neutralModule);

            var gameFlow = systems.AddComponent<PrototypeGameFlowController>();
            gameFlow.Configure(
                waveDirector,
                "Levels/level_01.json",
                baseCore,
                player);

            var smokeDriver = systems.AddComponent<PrototypeMvpSmokeDriver>();
            smokeDriver.Configure(
                player,
                territory,
                buildController,
                comboSystem,
                neutralModule,
                waveDirector);

            CreateHud(
                gameFlow,
                waveDirector,
                player,
                baseCore,
                wallet,
                territory,
                towerRegistry,
                buildController,
                comboSystem,
                neutralModule,
                cameraController);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException($"無法儲存場景：{ScenePath}");
            }

            PutSceneFirstInBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            ValidateScene();
            Debug.Log($"CyberWizard 垂直切片場景已建立：{ScenePath}");
        }

        public static void BuildFromCommandLine()
        {
            try
            {
                Build();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void BuildSmokePlayerFromCommandLine()
        {
            try
            {
                Build();

                const string outputDirectory = "Builds/Smoke";
                const string outputPath = outputDirectory + "/CyberWizardSmoke.exe";
                Directory.CreateDirectory(outputDirectory);

                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = outputPath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development
                };

                var report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != BuildResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Smoke Player 建置失敗：{report.summary.result}");
                }

                Debug.Log($"Smoke Player 已建立：{outputPath}");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void RunPlayModeSmokeTestFromCommandLine()
        {
            try
            {
                Build();
                EditorApplication.isPlaying = true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void EnsureAssetFolders()
        {
            EnsureFolder("Assets/_Project");
            EnsureFolder("Assets/_Project/Scenes");
            EnsureFolder("Assets/_Project/UI");
            EnsureFolder("Assets/_Project/Resources");
            EnsureFolder(PaletteFolder);
        }

        private static void CreatePrototypeMaterials()
        {
            CreateOrUpdateMaterial(
                $"{PaletteFolder}/Map.mat",
                new Color32(43, 47, 53, 255));
            CreateOrUpdateMaterial(
                $"{PaletteFolder}/Neutral.mat",
                new Color32(198, 203, 209, 255));
            CreateOrUpdateMaterial(
                $"{PaletteFolder}/Player.mat",
                new Color32(47, 128, 237, 255));
            CreateOrUpdateMaterial(
                $"{PaletteFolder}/Enemy.mat",
                new Color32(224, 72, 72, 255));
        }

        private static void CreateOrUpdateMaterial(string path, Color color)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (shader == null)
                {
                    throw new InvalidOperationException("找不到可用的 Lit Shader。");
                }

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            EditorUtility.SetDirty(material);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var slashIndex = path.LastIndexOf('/');
            var parent = path.Substring(0, slashIndex);
            var name = path.Substring(slashIndex + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static Camera CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.position = new Vector3(22f, 28f, -32f);
            cameraObject.transform.LookAt(new Vector3(0f, 0f, 2f));
            camera.clearFlags = CameraClearFlags.Skybox;
            return camera;
        }

        private static void CreateDirectionalLight()
        {
            var lightObject = new GameObject("Directional Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            lightObject.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
        }

        private static void CreateEnvironment(
            out Transform spawnPoint,
            out Transform destination,
            out BaseCore baseCore)
        {
            var environment = new GameObject("Prototype Environment");

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(environment.transform);
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(4f, 1f, 6f);
            PrototypeMaterialLibrary.Apply(ground, PrototypeMaterialRole.Map);

            var spawnMarker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            spawnMarker.name = "Spawn Point A";
            spawnMarker.transform.SetParent(environment.transform);
            spawnMarker.transform.position = new Vector3(0f, 0.15f, -22f);
            spawnMarker.transform.localScale = new Vector3(1.2f, 0.15f, 1.2f);
            PrototypeMaterialLibrary.Apply(spawnMarker, PrototypeMaterialRole.Enemy);
            var spawnOrigin = new GameObject("Enemy Spawn Origin");
            spawnOrigin.transform.SetParent(spawnMarker.transform);
            spawnOrigin.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            spawnPoint = spawnOrigin.transform;

            var goalMarker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            goalMarker.name = "Goal";
            goalMarker.transform.SetParent(environment.transform);
            goalMarker.transform.position = new Vector3(0f, 0.75f, 22f);
            goalMarker.transform.localScale = new Vector3(3f, 1.5f, 1f);
            PrototypeMaterialLibrary.Apply(goalMarker, PrototypeMaterialRole.Player);
            destination = goalMarker.transform;
            baseCore = goalMarker.AddComponent<BaseCore>();

            CreateBarrier(environment.transform, "Left Boundary", new Vector3(-19f, 0.5f, 0f));
            CreateBarrier(environment.transform, "Right Boundary", new Vector3(19f, 0.5f, 0f));
        }

        private static PrototypePlayerController CreatePlayer()
        {
            var playerObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            playerObject.name = "Player";
            playerObject.transform.position = new Vector3(10f, 1f, -12f);
            PrototypeMaterialLibrary.Apply(playerObject, PrototypeMaterialRole.Player);
            var primitiveCollider = playerObject.GetComponent<Collider>();
            if (primitiveCollider != null)
            {
                UnityEngine.Object.DestroyImmediate(primitiveCollider);
            }

            var characterController = playerObject.AddComponent<CharacterController>();
            characterController.height = 2f;
            characterController.radius = 0.5f;
            characterController.center = Vector3.zero;
            return playerObject.AddComponent<PrototypePlayerController>();
        }

        private static TerritoryController CreateTerritory(PrototypePlayerController player)
        {
            var territoryObject = new GameObject("Player Territory");
            territoryObject.transform.position = new Vector3(0f, 0f, -4f);

            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "Territory Capture Point";
            marker.transform.SetParent(territoryObject.transform);
            marker.transform.localPosition = new Vector3(8f, 0.1f, -4f);
            marker.transform.localScale = new Vector3(5f, 0.1f, 5f);
            PrototypeMaterialLibrary.Apply(marker, PrototypeMaterialRole.Neutral);
            var zone = marker.AddComponent<CaptureZone>();
            zone.Configure(player, 5f, 2.5f);

            var territory = territoryObject.AddComponent<TerritoryController>();
            territory.Configure(zone, new Vector2(34f, 32f));
            return territory;
        }

        private static NeutralOutpostModule CreateNeutralOutpostModule(
            PrototypePlayerController player,
            WaveDirector waveDirector)
        {
            var moduleObject = new GameObject("Neutral Outpost Module");
            var outpostObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            outpostObject.name = "Neutral Outpost";
            outpostObject.transform.SetParent(moduleObject.transform);
            outpostObject.transform.position = new Vector3(-12f, 0.6f, -6f);
            outpostObject.transform.localScale = new Vector3(1.6f, 0.6f, 1.6f);
            PrototypeMaterialLibrary.Apply(outpostObject, PrototypeMaterialRole.Neutral);

            var captureZone = outpostObject.AddComponent<CaptureZone>();
            captureZone.Configure(player, 2.8f, 3f);

            var spawnPoint = new GameObject("Soldier Spawn Point");
            spawnPoint.transform.SetParent(outpostObject.transform);
            spawnPoint.transform.localPosition = new Vector3(0f, 1.4f, 1.5f);

            var outpost = outpostObject.AddComponent<NeutralOutpost>();
            outpost.Configure(captureZone, spawnPoint.transform, waveDirector);

            var module = moduleObject.AddComponent<NeutralOutpostModule>();
            module.Configure(true, outpost);
            return module;
        }

        private static void CreateBarrier(Transform parent, string name, Vector3 position)
        {
            var barrier = GameObject.CreatePrimitive(PrimitiveType.Cube);
            barrier.name = name;
            barrier.transform.SetParent(parent);
            barrier.transform.position = position;
            barrier.transform.localScale = new Vector3(0.5f, 1f, 56f);
            PrototypeMaterialLibrary.Apply(barrier, PrototypeMaterialRole.Map);
        }

        private static void CreateHud(
            PrototypeGameFlowController gameFlow,
            WaveDirector waveDirector,
            PrototypePlayerController player,
            BaseCore baseCore,
            ResourceWallet wallet,
            TerritoryController territory,
            TowerRegistry towerRegistry,
            PrototypeBuildController buildController,
            PrototypeComboSystem comboSystem,
            NeutralOutpostModule neutralModule,
            PrototypeCameraFollow cameraController)
        {
            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (panelSettings == null)
            {
                panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                panelSettings.name = "Prototype Panel Settings";
                panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                panelSettings.referenceResolution = new Vector2Int(1920, 1080);
                panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                panelSettings.match = 0.5f;
                AssetDatabase.CreateAsset(panelSettings, PanelSettingsPath);
            }

            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(HudUxmlPath);
            if (visualTree == null)
            {
                throw new InvalidOperationException($"找不到 HUD UXML：{HudUxmlPath}");
            }

            var hudObject = new GameObject("Prototype HUD");
            var document = hudObject.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.visualTreeAsset = visualTree;
            document.sortingOrder = 10;

            var hud = hudObject.AddComponent<PrototypeHudController>();
            hud.Configure(
                gameFlow,
                waveDirector,
                player,
                baseCore,
                wallet,
                territory,
                towerRegistry,
                buildController,
                comboSystem,
                neutralModule,
                cameraController);
        }

        private static void PutSceneFirstInBuildSettings()
        {
            var existing = EditorBuildSettings.scenes
                .Where(scene => scene.path != ScenePath)
                .ToList();

            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };
            scenes.AddRange(existing);
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void ValidateScene()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.path != ScenePath)
            {
                throw new InvalidOperationException("建立後的作用中場景不正確。 ");
            }

            if (UnityEngine.Object.FindObjectOfType<PrototypeGameFlowController>() == null ||
                UnityEngine.Object.FindObjectOfType<WaveDirector>() == null ||
                UnityEngine.Object.FindObjectOfType<EnemySpawner>() == null ||
                UnityEngine.Object.FindObjectOfType<PrototypeHudController>() == null ||
                UnityEngine.Object.FindObjectOfType<PrototypePlayerController>() == null ||
                UnityEngine.Object.FindObjectOfType<TerritoryController>() == null ||
                UnityEngine.Object.FindObjectOfType<PrototypeBuildController>() == null ||
                UnityEngine.Object.FindObjectOfType<PrototypeComboSystem>() == null ||
                UnityEngine.Object.FindObjectOfType<NeutralOutpostModule>() == null)
            {
                throw new InvalidOperationException("垂直切片場景缺少必要元件。 ");
            }
        }
    }
}
