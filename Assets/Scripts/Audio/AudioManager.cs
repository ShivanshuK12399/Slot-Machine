using UnityEngine;
using SlotMachine.Core;
using SlotMachine.Reel;

namespace SlotMachine.Audio
{
    /// <summary>
    /// Master audio manager for the slot machine.
    /// Manages sound effects for button clicks, lever pulls, reel spinning loops,
    /// reel stop clunks, win fanfares, and coin tally count-up ticks.
    /// Includes built-in procedural sound synthesis as fallback if no audio clips are assigned.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Audio Sources")]
        [SerializeField] private AudioSource _sfxSource;
        [SerializeField] private AudioSource _loopSource;

        [Header("Audio Clips (Optional - Uses procedural if unassigned)")]
        [Tooltip("Sound played on button presses (Bet Up/Down, Max Bet).")]
        [SerializeField] private AudioClip _buttonClickClip;

        [Tooltip("Sound played when pulling the lever.")]
        [SerializeField] private AudioClip _leverPullClip;

        [Tooltip("Looping sound played while reels are actively spinning.")]
        [SerializeField] private AudioClip _reelSpinLoopClip;

        [Tooltip("Mechanical clunk/pawl sound played when an individual reel stops.")]
        [SerializeField] private AudioClip _reelStopClip;

        [Tooltip("Celebration sound played on a winning outcome.")]
        [SerializeField] private AudioClip _winCelebrationClip;

        [Tooltip("Short tick played during the rolling balance count-up.")]
        [SerializeField] private AudioClip _coinTickClip;

        [Header("Volume Controls")]
        [Range(0f, 1f)] [SerializeField] private float _masterVolume = 1f;
        [Range(0f, 1f)] [SerializeField] private float _sfxVolume = 1f;
        [Range(0f, 1f)] [SerializeField] private float _spinLoopVolume = 0.5f;
        [Tooltip("Volume multiplier for mechanical ratchet ticks.")]
        [Range(0f, 1f)] [SerializeField] private float _ratchetVolume = 0.7f;

        // Dedicated audio source pool for rapid ratchet clicks
        private AudioSource[] _ratchetSources;
        private int _ratchetSourceIndex;

        // Procedural fallback clips
        private AudioClip _procClick;
        private AudioClip _procLever;
        private AudioClip _procReelStop;
        private AudioClip _procWin;
        private AudioClip _procCoin;
        private AudioClip _procSpinLoop;
        private AudioClip _procRatchetClick;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            EnsureAudioSources();
            GenerateProceduralFallbacks();
        }

        private void EnsureAudioSources()
        {
            if (_sfxSource == null)
            {
                _sfxSource = gameObject.AddComponent<AudioSource>();
                _sfxSource.playOnAwake = false;
                _sfxSource.loop = false;
            }

            if (_loopSource == null)
            {
                _loopSource = gameObject.AddComponent<AudioSource>();
                _loopSource.playOnAwake = false;
                _loopSource.loop = true;
            }

            if (_ratchetSources == null || _ratchetSources.Length == 0)
            {
                _ratchetSources = new AudioSource[4];
                for (int i = 0; i < 4; i++)
                {
                    var src = gameObject.AddComponent<AudioSource>();
                    src.playOnAwake = false;
                    src.loop = false;
                    _ratchetSources[i] = src;
                }
            }
        }

        public void PlayButtonClick()
        {
            PlayClip(_buttonClickClip ?? _procClick, _sfxVolume);
        }

        public void PlayLeverPull()
        {
            PlayClip(_leverPullClip ?? _procLever, _sfxVolume);
        }

        public void StartSpinLoop()
        {
            if (_loopSource == null) return;
            _loopSource.clip = _reelSpinLoopClip ?? _procSpinLoop;
            _loopSource.volume = _spinLoopVolume * _masterVolume;
            if (_loopSource.clip != null && !_loopSource.isPlaying)
            {
                _loopSource.Play();
            }
        }

        public void StopSpinLoop()
        {
            if (_loopSource != null && _loopSource.isPlaying)
            {
                _loopSource.Stop();
            }
        }

        public void PlayReelStop(int reelIndex = 0)
        {
            // Subtle pitch variation for each reel index gives authentic mechanical feel
            float pitch = 1.0f - (reelIndex * 0.05f);
            PlayClipWithPitch(_reelStopClip ?? _procReelStop, _sfxVolume, pitch);
        }

        public void PlayWin()
        {
            PlayClip(_winCelebrationClip ?? _procWin, _sfxVolume * 1.1f);
        }

        public void PlayCoinTick()
        {
            PlayClip(_coinTickClip ?? _procCoin, _sfxVolume * 0.6f);
        }

        /// <summary>
        /// Plays a mathematically synthesized mechanical ratchet tick.
        /// Pitch is kept strictly constant (1.0f) for an authentic physical star-wheel tooth sound,
        /// while the temporal repetition rate naturally speeds up and slows down with reel motion.
        /// </summary>
        public void PlayRatchetTick(float speedRatio)
        {
            if (_procRatchetClick == null || _ratchetSources == null || _ratchetSources.Length == 0) return;

            speedRatio = Mathf.Clamp01(speedRatio);
            float volume = Mathf.Lerp(0.5f, 1.0f, speedRatio) * _ratchetVolume * _sfxVolume * _masterVolume;

            var src = _ratchetSources[_ratchetSourceIndex];
            _ratchetSourceIndex = (_ratchetSourceIndex + 1) % _ratchetSources.Length;

            src.pitch = 1.0f;
            src.PlayOneShot(_procRatchetClick, volume);
        }

        private void PlayClip(AudioClip clip, float volume)
        {
            if (clip == null || _sfxSource == null) return;
            _sfxSource.pitch = 1f;
            _sfxSource.PlayOneShot(clip, volume * _masterVolume);
        }

        private void PlayClipWithPitch(AudioClip clip, float volume, float pitch)
        {
            if (clip == null || _sfxSource == null) return;
            _sfxSource.pitch = pitch;
            _sfxSource.PlayOneShot(clip, volume * _masterVolume);
        }

        #region Procedural Sound Synthesis (Fallback)

        private void GenerateProceduralFallbacks()
        {
            _procClick = CreateClickClip("Proc_Click", 1200f, 0.03f);
            _procLever = CreateClickClip("Proc_Lever", 450f, 0.08f);
            _procReelStop = CreateThudClip("Proc_ReelStop", 160f, 0.10f);
            _procWin = CreateArpeggioClip("Proc_Win", new float[] { 523.25f, 659.25f, 783.99f, 1046.50f }, 0.45f);
            _procCoin = CreateBellClip("Proc_Coin", 1800f, 0.06f);
            _procSpinLoop = CreateHumClip("Proc_SpinLoop", 220f, 0.5f);
            _procRatchetClick = CreateRatchetClickClip("Proc_RatchetClick");
        }

        private AudioClip CreateRatchetClickClip(string name)
        {
            int sampleRate = 44100;
            float duration = 0.016f; // 16ms crisp mechanical click
            int samples = Mathf.CeilToInt(sampleRate * duration);
            float[] data = new float[samples];

            float freq = 1450f;
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Exp(-t * 240f);
                float body = Mathf.Sin(2f * Mathf.PI * freq * t) * envelope;
                float noise = (Random.value * 2f - 1f) * 0.45f * Mathf.Exp(-t * 320f);
                data[i] = Mathf.Clamp(body + noise, -1f, 1f);
            }

            var clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateClickClip(string name, float freq, float duration)
        {
            int sampleRate = 44100;
            int samples = Mathf.CeilToInt(sampleRate * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Exp(-t * 80f);
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * envelope;
            }

            var clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateThudClip(string name, float freq, float duration)
        {
            int sampleRate = 44100;
            int samples = Mathf.CeilToInt(sampleRate * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Exp(-t * 25f);
                float wave = Mathf.Sin(2f * Mathf.PI * freq * (1f - t * 2f) * t);
                float noise = (Random.value * 2f - 1f) * 0.15f * envelope;
                data[i] = Mathf.Clamp(wave * envelope + noise, -1f, 1f);
            }

            var clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateBellClip(string name, float freq, float duration)
        {
            int sampleRate = 44100;
            int samples = Mathf.CeilToInt(sampleRate * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Exp(-t * 30f);
                data[i] = (Mathf.Sin(2f * Mathf.PI * freq * t) + 0.3f * Mathf.Sin(4f * Mathf.PI * freq * t)) * envelope;
            }

            var clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateArpeggioClip(string name, float[] notes, float duration)
        {
            int sampleRate = 44100;
            int samples = Mathf.CeilToInt(sampleRate * duration);
            float[] data = new float[samples];
            float noteDuration = duration / notes.Length;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                int noteIndex = Mathf.Clamp(Mathf.FloorToInt(t / noteDuration), 0, notes.Length - 1);
                float freq = notes[noteIndex];
                float noteT = t - (noteIndex * noteDuration);
                float envelope = Mathf.Exp(-noteT * 12f);
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * noteT) * envelope * 0.7f;
            }

            var clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateHumClip(string name, float freq, float duration)
        {
            int sampleRate = 44100;
            int samples = Mathf.CeilToInt(sampleRate * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                data[i] = (Mathf.Sin(2f * Mathf.PI * freq * t) * 0.4f + (Random.value * 2f - 1f) * 0.1f);
            }

            var clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        #endregion
    }
}

