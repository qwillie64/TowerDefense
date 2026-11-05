using CyberWizard.Core;
using CyberWizard.Gameplay.Capture;
using CyberWizard.Gameplay.GameFlow;
using CyberWizard.Gameplay.Enemies;
using CyberWizard.Gameplay.Waves;
using UnityEngine;

namespace CyberWizard.Presentation
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class PrototypeAudioService : MonoBehaviour
    {
        [SerializeField] private PrototypeGameFlowController gameFlow;
        [SerializeField] private WaveDirector waveDirector;
        [SerializeField] private TerritoryController territory;
        [SerializeField, Range(0f, 1f)] private float volume = 0.22f;

        private AudioSource _source;
        private AudioClip _waveClip;
        private AudioClip _bossClip;
        private AudioClip _defeatEnemyClip;
        private AudioClip _captureClip;
        private AudioClip _victoryClip;
        private AudioClip _defeatClip;

        public void Configure(
            PrototypeGameFlowController newGameFlow,
            WaveDirector newWaveDirector,
            TerritoryController newTerritory)
        {
            gameFlow = newGameFlow;
            waveDirector = newWaveDirector;
            territory = newTerritory;
        }

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            _waveClip = CreateTone("Wave Start", 520f, 0.16f);
            _bossClip = CreateTone("Boss Warning", 150f, 0.42f);
            _defeatEnemyClip = CreateTone("Enemy Defeated", 760f, 0.07f);
            _captureClip = CreateTone("Capture", 620f, 0.28f);
            _victoryClip = CreateSequence("Victory", new[] { 440f, 554f, 659f }, 0.13f);
            _defeatClip = CreateSequence("Defeat", new[] { 330f, 247f, 196f }, 0.16f);
        }

        private void Start()
        {
            if (gameFlow != null) gameFlow.PhaseChanged += HandlePhaseChanged;
            if (waveDirector != null)
            {
                waveDirector.WaveBegan += HandleWaveBegan;
                waveDirector.EnemyDefeated += HandleEnemyDefeated;
            }

            if (territory != null) territory.Captured += HandleCaptured;
        }

        private void HandlePhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.Ended) Play(_victoryClip);
            else if (phase == GamePhase.Defeat || phase == GamePhase.Failed) Play(_defeatClip);
        }

        private void HandleWaveBegan(string waveId, int waveNumber)
        {
            Play(waveId == "wave_boss" ? _bossClip : _waveClip);
        }

        private void HandleEnemyDefeated(EnemyAgent enemy) => Play(_defeatEnemyClip);
        private void HandleCaptured() => Play(_captureClip);

        private void Play(AudioClip clip)
        {
            if (_source != null && clip != null)
            {
                _source.PlayOneShot(clip, volume);
            }
        }

        private static AudioClip CreateTone(string clipName, float frequency, float duration)
        {
            const int sampleRate = 44100;
            var sampleCount = Mathf.CeilToInt(sampleRate * duration);
            var samples = new float[sampleCount];
            for (var index = 0; index < sampleCount; index++)
            {
                var time = index / (float)sampleRate;
                var envelope = 1f - index / (float)sampleCount;
                samples[index] = Mathf.Sin(2f * Mathf.PI * frequency * time) * envelope * 0.45f;
            }

            var clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateSequence(
            string clipName,
            float[] frequencies,
            float noteDuration)
        {
            const int sampleRate = 44100;
            var noteSamples = Mathf.CeilToInt(sampleRate * noteDuration);
            var samples = new float[noteSamples * frequencies.Length];
            for (var note = 0; note < frequencies.Length; note++)
            {
                for (var index = 0; index < noteSamples; index++)
                {
                    var time = index / (float)sampleRate;
                    var envelope = 1f - index / (float)noteSamples;
                    samples[note * noteSamples + index] =
                        Mathf.Sin(2f * Mathf.PI * frequencies[note] * time) * envelope * 0.4f;
                }
            }

            var clip = AudioClip.Create(clipName, samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void OnDestroy()
        {
            if (gameFlow != null) gameFlow.PhaseChanged -= HandlePhaseChanged;
            if (waveDirector != null)
            {
                waveDirector.WaveBegan -= HandleWaveBegan;
                waveDirector.EnemyDefeated -= HandleEnemyDefeated;
            }

            if (territory != null) territory.Captured -= HandleCaptured;
        }
    }
}
