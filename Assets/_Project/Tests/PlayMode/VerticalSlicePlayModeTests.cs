using System.Collections;
using CyberWizard.Core;
using CyberWizard.Gameplay.GameFlow;
using CyberWizard.Gameplay.Waves;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace CyberWizard.Tests
{
    public sealed class VerticalSlicePlayModeTests
    {
        [UnityTest]
        public IEnumerator ReadyScreen_CanSwitchToLevel02AndStartSchedule()
        {
            yield return SceneManager.LoadSceneAsync(
                "Assets/_Project/Scenes/VerticalSlice.unity",
                LoadSceneMode.Single);
            yield return null;

            var gameFlow = Object.FindObjectOfType<PrototypeGameFlowController>();
            var waveDirector = Object.FindObjectOfType<WaveDirector>();
            var document = Object.FindObjectOfType<UIDocument>();

            Assert.That(gameFlow, Is.Not.Null);
            Assert.That(waveDirector, Is.Not.Null);
            Assert.That(document, Is.Not.Null);
            Assert.That(gameFlow.CurrentPhase, Is.EqualTo(GamePhase.Ready));

            if (gameFlow.SelectedLevelId != "level_01")
            {
                gameFlow.SelectNextLevel();
            }

            Assert.That(gameFlow.SelectedLevelId, Is.EqualTo("level_01"));
            gameFlow.SelectNextLevel();
            yield return null;

            Assert.That(gameFlow.SelectedLevelId, Is.EqualTo("level_02"));
            Assert.That(gameFlow.SelectedLevelName, Is.EqualTo("赤潮壓境"));
            Assert.That(waveDirector.TotalWaveCount, Is.EqualTo(4));

            var levelButton = document.rootVisualElement.Q<Button>("level-button");
            var summaryLabel = document.rootVisualElement.Q<Label>("level-summary-label");
            Assert.That(levelButton, Is.Not.Null);
            Assert.That(summaryLabel, Is.Not.Null);
            StringAssert.Contains("赤潮壓境", levelButton.text);
            StringAssert.Contains("進階", summaryLabel.text);

            gameFlow.StartGame();
            yield return null;

            Assert.That(gameFlow.CurrentPhase, Is.EqualTo(GamePhase.Wave));
            Assert.That(waveDirector.TotalWaveCount, Is.EqualTo(4));
            Time.timeScale = 1f;
        }
    }
}
