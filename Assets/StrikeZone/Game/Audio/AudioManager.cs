using UnityEngine;

namespace StrikeZone.Game
{
    /// <summary>
    /// Pooled one-shot audio. 3D sounds get a cheap occlusion pass: if world geometry
    /// blocks the line from the listener, the source is muffled with a low-pass filter
    /// and attenuated — enough to tell "behind the wall" from "in the open" by ear.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        private const int PoolSize = 28;

        private AudioSource[] _sources;
        private AudioLowPassFilter[] _filters;
        private float[] _startTimes;
        private Transform _listener;

        public ProceduralAudio Clips { get; private set; }
        public float MasterVolume = 1f;

        public void Initialize(ProceduralAudio clips, Transform listener)
        {
            Clips = clips;
            _listener = listener;
            _sources = new AudioSource[PoolSize];
            _filters = new AudioLowPassFilter[PoolSize];
            _startTimes = new float[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject("Voice" + i);
                go.transform.SetParent(transform, false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.dopplerLevel = 0f;
                src.rolloffMode = AudioRolloffMode.Logarithmic;
                src.minDistance = 2f;
                src.maxDistance = 80f;
                src.spread = 0f;
                var filter = go.AddComponent<AudioLowPassFilter>();
                filter.cutoffFrequency = 22000f;
                filter.enabled = false;
                _sources[i] = src;
                _filters[i] = filter;
            }
        }

        public void SetListener(Transform listener)
        {
            _listener = listener;
        }

        /// <summary>Non-positional sound (own weapon, UI).</summary>
        public void Play2D(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (clip == null || _sources == null) return;
            int i = Acquire();
            var src = _sources[i];
            src.transform.localPosition = Vector3.zero;
            src.spatialBlend = 0f;
            _filters[i].enabled = false;
            BeginVoice(i, clip, volume, pitch);
        }

        /// <summary>Positional sound with occlusion. minDistance controls how far it carries.</summary>
        public void PlayAt(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f, float minDistance = 2f)
        {
            if (clip == null || _sources == null) return;
            int i = Acquire();
            var src = _sources[i];
            src.transform.position = position;
            src.spatialBlend = 1f;
            src.minDistance = minDistance;
            src.maxDistance = Mathf.Max(minDistance * 40f, 60f);

            bool occluded = false;
            if (_listener != null)
            {
                Vector3 from = _listener.position;
                Vector3 to = position;
                Vector3 delta = to - from;
                float dist = delta.magnitude;
                if (dist > 0.5f)
                    occluded = Physics.Raycast(from, delta / dist, dist - 0.3f, 1 << Layers.World, QueryTriggerInteraction.Ignore);
            }
            _filters[i].enabled = occluded;
            _filters[i].cutoffFrequency = occluded ? 1200f : 22000f;
            BeginVoice(i, clip, occluded ? volume * 0.55f : volume, pitch);
        }

        private void BeginVoice(int i, AudioClip clip, float volume, float pitch)
        {
            var src = _sources[i];
            src.clip = clip;
            src.volume = Mathf.Clamp01(volume * MasterVolume);
            src.pitch = pitch;
            src.Play();
            _startTimes[i] = Time.unscaledTime;
        }

        private int Acquire()
        {
            int oldest = 0;
            float oldestTime = float.MaxValue;
            for (int i = 0; i < _sources.Length; i++)
            {
                if (!_sources[i].isPlaying) return i;
                if (_startTimes[i] < oldestTime)
                {
                    oldestTime = _startTimes[i];
                    oldest = i;
                }
            }
            _sources[oldest].Stop();
            return oldest;
        }
    }
}
