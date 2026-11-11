using System;
using System.Collections.Generic;
using UnityEngine;

namespace StrikeZone.Game
{
    /// <summary>
    /// Synthesises every sound effect at startup (noise bursts, filtered thumps, metallic
    /// partials). Original by construction and licence-free; good enough for tuning
    /// gameplay audio cues in phase 1. Recorded/Foley sounds replace these in phase 9 —
    /// the AudioManager API stays the same.
    /// </summary>
    public sealed class ProceduralAudio
    {
        public const int SampleRate = 44100;

        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        private readonly Dictionary<string, AudioClip[]> _variants = new Dictionary<string, AudioClip[]>();

        private struct ShotSpec
        {
            public float Duration;
            public float NoiseDecay;
            public float NoiseCutoff;
            public float ThumpFreq;
            public float ThumpDecay;
            public float ThumpGain;
            public float CrackGain;
            public float Gain;
            public float TailDecay;
            public float TailGain;
        }

        public ProceduralAudio()
        {
            BuildShot("pistol", new ShotSpec { Duration = 0.45f, NoiseDecay = 0.035f, NoiseCutoff = 5200f, ThumpFreq = 140f, ThumpDecay = 0.05f, ThumpGain = 0.55f, CrackGain = 0.5f, Gain = 0.85f, TailDecay = 0.18f, TailGain = 0.15f });
            BuildShot("pistol_heavy", new ShotSpec { Duration = 0.7f, NoiseDecay = 0.06f, NoiseCutoff = 3800f, ThumpFreq = 95f, ThumpDecay = 0.09f, ThumpGain = 0.9f, CrackGain = 0.7f, Gain = 1f, TailDecay = 0.3f, TailGain = 0.25f });
            BuildShot("pistol_silenced", new ShotSpec { Duration = 0.25f, NoiseDecay = 0.02f, NoiseCutoff = 1500f, ThumpFreq = 220f, ThumpDecay = 0.03f, ThumpGain = 0.35f, CrackGain = 0.08f, Gain = 0.45f, TailDecay = 0.05f, TailGain = 0.03f });
            BuildShot("smg", new ShotSpec { Duration = 0.4f, NoiseDecay = 0.03f, NoiseCutoff = 4800f, ThumpFreq = 150f, ThumpDecay = 0.04f, ThumpGain = 0.5f, CrackGain = 0.45f, Gain = 0.75f, TailDecay = 0.16f, TailGain = 0.14f });
            BuildShot("smg_silenced", new ShotSpec { Duration = 0.22f, NoiseDecay = 0.018f, NoiseCutoff = 1400f, ThumpFreq = 230f, ThumpDecay = 0.025f, ThumpGain = 0.3f, CrackGain = 0.06f, Gain = 0.4f, TailDecay = 0.05f, TailGain = 0.03f });
            BuildShot("shotgun", new ShotSpec { Duration = 0.9f, NoiseDecay = 0.08f, NoiseCutoff = 2600f, ThumpFreq = 70f, ThumpDecay = 0.12f, ThumpGain = 1f, CrackGain = 0.6f, Gain = 1f, TailDecay = 0.4f, TailGain = 0.3f });
            BuildShot("rifle", new ShotSpec { Duration = 0.6f, NoiseDecay = 0.045f, NoiseCutoff = 4500f, ThumpFreq = 105f, ThumpDecay = 0.06f, ThumpGain = 0.7f, CrackGain = 0.65f, Gain = 0.9f, TailDecay = 0.26f, TailGain = 0.22f });
            BuildShot("rifle_heavy", new ShotSpec { Duration = 0.7f, NoiseDecay = 0.055f, NoiseCutoff = 3600f, ThumpFreq = 88f, ThumpDecay = 0.075f, ThumpGain = 0.85f, CrackGain = 0.7f, Gain = 1f, TailDecay = 0.3f, TailGain = 0.25f });
            BuildShot("rifle_silenced", new ShotSpec { Duration = 0.3f, NoiseDecay = 0.022f, NoiseCutoff = 1700f, ThumpFreq = 190f, ThumpDecay = 0.03f, ThumpGain = 0.4f, CrackGain = 0.1f, Gain = 0.5f, TailDecay = 0.07f, TailGain = 0.05f });
            BuildShot("sniper_heavy", new ShotSpec { Duration = 1.3f, NoiseDecay = 0.07f, NoiseCutoff = 3200f, ThumpFreq = 62f, ThumpDecay = 0.14f, ThumpGain = 1f, CrackGain = 1f, Gain = 1f, TailDecay = 0.6f, TailGain = 0.35f });
            BuildShot("sniper_light", new ShotSpec { Duration = 0.9f, NoiseDecay = 0.05f, NoiseCutoff = 4200f, ThumpFreq = 90f, ThumpDecay = 0.08f, ThumpGain = 0.75f, CrackGain = 0.85f, Gain = 0.95f, TailDecay = 0.4f, TailGain = 0.25f });
            BuildShot("sniper_auto", new ShotSpec { Duration = 0.9f, NoiseDecay = 0.06f, NoiseCutoff = 3600f, ThumpFreq = 78f, ThumpDecay = 0.1f, ThumpGain = 0.9f, CrackGain = 0.85f, Gain = 1f, TailDecay = 0.4f, TailGain = 0.28f });
            BuildShot("mg", new ShotSpec { Duration = 0.55f, NoiseDecay = 0.05f, NoiseCutoff = 3900f, ThumpFreq = 95f, ThumpDecay = 0.065f, ThumpGain = 0.8f, CrackGain = 0.55f, Gain = 0.9f, TailDecay = 0.25f, TailGain = 0.2f });

            _clips["knife"] = Swish("knife", 0.28f, 7);
            _clips["dry"] = Click("dry", 0.05f, 3000f, 0.6f, 11);
            _clips["mag_out"] = MagOut();
            _clips["mag_in"] = Click("mag_in", 0.12f, 1800f, 0.9f, 13);
            _clips["bolt"] = Bolt();
            _clips["deploy"] = Rattle("deploy", 0.25f, 17);
            _clips["scope"] = Click("scope", 0.06f, 2400f, 0.35f, 19);
            _clips["headshot"] = Headshot();
            _clips["hit_body"] = BodyHit();
            _clips["hitmarker"] = Click("hitmarker", 0.04f, 6000f, 0.35f, 23);
            _clips["land"] = Thump("land", 0.25f, 70f, 0.08f, 0.9f, 29);
            _clips["jump"] = Thump("jump", 0.12f, 140f, 0.04f, 0.35f, 31);
            _clips["hurt"] = Thump("hurt", 0.3f, 60f, 0.12f, 1f, 37);

            _variants["impact_concrete"] = Variants("impact_concrete", 3, i => Impact("impact_concrete" + i, 3500f, 0f, 41 + i));
            _variants["impact_metal"] = Variants("impact_metal", 3, i => Impact("impact_metal" + i, 6000f, 1f, 51 + i));
            _variants["impact_wood"] = Variants("impact_wood", 3, i => Impact("impact_wood" + i, 1400f, 0f, 61 + i));
            _variants["impact_dirt"] = Variants("impact_dirt", 3, i => Impact("impact_dirt" + i, 1100f, 0f, 71 + i));

            _variants["step_concrete"] = Variants("step_concrete", 4, i => Footstep("step_concrete" + i, 0.11f, 2400f, 0f, 0f, 81 + i));
            _variants["step_metal"] = Variants("step_metal", 4, i => Footstep("step_metal" + i, 0.3f, 4200f, 1f, 0f, 91 + i));
            _variants["step_wood"] = Variants("step_wood", 4, i => Footstep("step_wood" + i, 0.16f, 900f, 0f, 1f, 101 + i));
            _variants["step_dirt"] = Variants("step_dirt", 4, i => Footstep("step_dirt" + i, 0.18f, 1600f, 0f, 0.3f, 111 + i));
            _variants["shell"] = Variants("shell", 3, i => Shell("shell" + i, 121 + i));
        }

        public AudioClip Get(string name)
        {
            AudioClip c;
            if (_clips.TryGetValue(name, out c)) return c;
            AudioClip[] v;
            if (_variants.TryGetValue(name, out v) && v.Length > 0) return v[0];
            return null;
        }

        public AudioClip GetVariant(string name)
        {
            AudioClip[] v;
            if (_variants.TryGetValue(name, out v) && v.Length > 0) return v[UnityEngine.Random.Range(0, v.Length)];
            return Get(name);
        }

        public AudioClip Shot(string profile, bool silenced)
        {
            AudioClip c;
            if (_clips.TryGetValue("shot_" + profile, out c)) return c;
            return _clips[silenced ? "shot_rifle_silenced" : "shot_rifle"];
        }

        // ------------------------------------------------------------------ synthesis

        private static AudioClip[] Variants(string name, int count, Func<int, AudioClip> make)
        {
            var arr = new AudioClip[count];
            for (int i = 0; i < count; i++) arr[i] = make(i);
            return arr;
        }

        private void BuildShot(string profile, ShotSpec spec)
        {
            int n = (int)(spec.Duration * SampleRate);
            var data = new float[n];
            var rng = new System.Random(profile.GetHashCode() & 0x7fffffff);
            float lp = 0f;
            float lpTail = 0f;
            float alpha = OnePole(spec.NoiseCutoff);
            float alphaTail = OnePole(900f);
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += alpha * (white - lp);
                lpTail += alphaTail * (white - lpTail);
                float noise = lp * Mathf.Exp(-t / spec.NoiseDecay);
                float crack = t < 0.004f ? white * spec.CrackGain * (1f - t / 0.004f) : 0f;
                float freq = spec.ThumpFreq * (1f + 1.5f * Mathf.Exp(-t / 0.01f));
                phase += freq / SampleRate * Mathf.PI * 2f;
                float thump = Mathf.Sin(phase) * Mathf.Exp(-t / spec.ThumpDecay) * spec.ThumpGain;
                // Room tail: low-passed noise with slow decay, gives distance and weight.
                float tail = lpTail * Mathf.Exp(-t / spec.TailDecay) * spec.TailGain * 3f;
                float attack = Mathf.Clamp01(t / 0.0008f);
                data[i] = (noise * 1.6f + crack + thump + tail) * attack * spec.Gain;
            }
            Normalize(data, 0.95f * spec.Gain);
            _clips["shot_" + profile] = Create("shot_" + profile, data);
        }

        private static AudioClip Swish(string name, float duration, int seed)
        {
            int n = (int)(duration * SampleRate);
            var data = new float[n];
            var rng = new System.Random(seed);
            float bp1 = 0f, bp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                float cutoff = Mathf.Lerp(600f, 3500f, Mathf.Sin(t * Mathf.PI));
                float a = OnePole(cutoff);
                bp1 += a * (white - bp1);
                bp2 += a * 0.3f * (bp1 - bp2);
                float env = Mathf.Sin(t * Mathf.PI);
                data[i] = (bp1 - bp2) * env * env;
            }
            Normalize(data, 0.6f);
            return Create(name, data);
        }

        private static AudioClip Click(string name, float duration, float cutoff, float gain, int seed)
        {
            int n = (int)(duration * SampleRate);
            var data = new float[n];
            var rng = new System.Random(seed);
            float lp = 0f;
            float a = OnePole(cutoff);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += a * (white - lp);
                float hp = white - lp;
                data[i] = (hp * 0.7f + Mathf.Sin(t * cutoff * 0.5f * Mathf.PI * 2f) * 0.3f) * Mathf.Exp(-t / (duration * 0.18f));
            }
            Normalize(data, gain);
            return Create(name, data);
        }

        private static AudioClip MagOut()
        {
            int n = (int)(0.3f * SampleRate);
            var data = new float[n];
            var rng = new System.Random(5);
            float lp = 0f;
            float a = OnePole(2500f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += a * (white - lp);
                float click = Mathf.Exp(-t / 0.008f);
                float slide = t > 0.04f ? Mathf.Exp(-(t - 0.04f) / 0.08f) * 0.45f : 0f;
                data[i] = lp * (click + slide);
            }
            Normalize(data, 0.8f);
            return Create("mag_out", data);
        }

        private static AudioClip Bolt()
        {
            int n = (int)(0.4f * SampleRate);
            var data = new float[n];
            var rng = new System.Random(9);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                float e1 = Mathf.Exp(-t / 0.012f);
                float e2 = t > 0.18f ? Mathf.Exp(-(t - 0.18f) / 0.015f) : 0f;
                float ring = Mathf.Sin(t * 2900f * Mathf.PI * 2f) * 0.3f;
                data[i] = (white * 0.7f + ring) * (e1 + e2);
            }
            Normalize(data, 0.85f);
            return Create("bolt", data);
        }

        private static AudioClip Rattle(string name, float duration, int seed)
        {
            int n = (int)(duration * SampleRate);
            var data = new float[n];
            var rng = new System.Random(seed);
            float lp = 0f;
            float a = OnePole(3000f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += a * (white - lp);
                float grains = Mathf.Abs(Mathf.Sin(t * 70f)) > 0.92f ? 1f : 0.15f;
                data[i] = lp * grains * Mathf.Exp(-t / (duration * 0.5f));
            }
            Normalize(data, 0.5f);
            return Create(name, data);
        }

        private static AudioClip Headshot()
        {
            // Bright metallic "tink" with inharmonic partials — must cut through gunfire.
            int n = (int)(0.5f * SampleRate);
            var data = new float[n];
            var rng = new System.Random(3);
            float[] partials = { 2650f, 4130f, 5370f, 7020f };
            float[] gains = { 1f, 0.6f, 0.45f, 0.25f };
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float s = 0f;
                for (int p = 0; p < partials.Length; p++)
                    s += Mathf.Sin(t * partials[p] * Mathf.PI * 2f) * gains[p] * Mathf.Exp(-t / (0.12f / (p + 1)));
                float click = t < 0.003f ? (float)(rng.NextDouble() * 2.0 - 1.0) : 0f;
                data[i] = s * 0.6f + click;
            }
            Normalize(data, 0.9f);
            return Create("headshot", data);
        }

        private static AudioClip BodyHit()
        {
            int n = (int)(0.2f * SampleRate);
            var data = new float[n];
            var rng = new System.Random(4);
            float lp = 0f;
            float a = OnePole(700f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += a * (white - lp);
                data[i] = (lp * 2f + Mathf.Sin(t * 95f * Mathf.PI * 2f) * 0.6f) * Mathf.Exp(-t / 0.035f);
            }
            Normalize(data, 0.85f);
            return Create("hit_body", data);
        }

        private static AudioClip Thump(string name, float duration, float freq, float decay, float gain, int seed)
        {
            int n = (int)(duration * SampleRate);
            var data = new float[n];
            var rng = new System.Random(seed);
            float lp = 0f;
            float a = OnePole(500f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += a * (white - lp);
                data[i] = (Mathf.Sin(t * freq * Mathf.PI * 2f) + lp * 1.5f) * Mathf.Exp(-t / decay);
            }
            Normalize(data, gain);
            return Create(name, data);
        }

        private static AudioClip Impact(string name, float cutoff, float metallic, int seed)
        {
            int n = (int)(0.25f * SampleRate);
            var data = new float[n];
            var rng = new System.Random(seed);
            float lp = 0f;
            float a = OnePole(cutoff);
            float ringFreq = 2000f + (float)rng.NextDouble() * 2500f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += a * (white - lp);
                float ring = metallic * Mathf.Sin(t * ringFreq * Mathf.PI * 2f) * Mathf.Exp(-t / 0.06f) * 0.5f;
                data[i] = lp * Mathf.Exp(-t / 0.02f) + ring;
            }
            Normalize(data, 0.7f);
            return Create(name, data);
        }

        private static AudioClip Footstep(string name, float duration, float cutoff, float metallic, float hollow, int seed)
        {
            int n = (int)(duration * SampleRate);
            var data = new float[n];
            var rng = new System.Random(seed);
            float lp = 0f;
            float a = OnePole(cutoff * (0.85f + (float)rng.NextDouble() * 0.3f));
            float ringFreq = 1100f + (float)rng.NextDouble() * 800f;
            float heelToe = 0.025f + (float)rng.NextDouble() * 0.02f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += a * (white - lp);
                // Two contacts: heel then toe.
                float env = Mathf.Exp(-t / 0.018f) + (t > heelToe ? Mathf.Exp(-(t - heelToe) / 0.022f) * 0.6f : 0f);
                float ring = metallic * Mathf.Sin(t * ringFreq * Mathf.PI * 2f) * Mathf.Exp(-t / 0.09f) * 0.4f;
                float body = hollow * Mathf.Sin(t * 160f * Mathf.PI * 2f) * Mathf.Exp(-t / 0.04f) * 0.7f;
                data[i] = lp * env + ring + body;
            }
            Normalize(data, 0.8f);
            return Create(name, data);
        }

        private static AudioClip Shell(string name, int seed)
        {
            int n = (int)(0.35f * SampleRate);
            var data = new float[n];
            var rng = new System.Random(seed);
            float f1 = 3800f + (float)rng.NextDouble() * 1500f;
            float f2 = f1 * 1.53f;
            float bounce = 0.09f + (float)rng.NextDouble() * 0.05f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float e = Mathf.Exp(-t / 0.03f) + (t > bounce ? Mathf.Exp(-(t - bounce) / 0.025f) * 0.5f : 0f);
                data[i] = (Mathf.Sin(t * f1 * Mathf.PI * 2f) + Mathf.Sin(t * f2 * Mathf.PI * 2f) * 0.5f) * e;
            }
            Normalize(data, 0.35f);
            return Create(name, data);
        }

        private static float OnePole(float cutoff)
        {
            float dt = 1f / SampleRate;
            float rc = 1f / (2f * Mathf.PI * cutoff);
            return dt / (rc + dt);
        }

        private static void Normalize(float[] data, float peak)
        {
            float max = 0f;
            for (int i = 0; i < data.Length; i++) max = Mathf.Max(max, Mathf.Abs(data[i]));
            if (max < 1e-6f) return;
            float k = peak / max;
            for (int i = 0; i < data.Length; i++) data[i] *= k;
        }

        private static AudioClip Create(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
