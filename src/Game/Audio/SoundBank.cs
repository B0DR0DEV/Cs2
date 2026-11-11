using System;
using System.Collections.Generic;
using Godot;

namespace StrikeZone.Game
{
    /// <summary>
    /// Synthesises every sound effect at startup (noise bursts, filtered thumps, metallic
    /// partials) into 16-bit PCM AudioStreamWav. Original by construction and licence-free.
    /// Recorded sounds replace these in phase 9; the lookup API stays the same.
    /// </summary>
    public sealed class SoundBank
    {
        public const int SampleRate = 44100;

        private readonly Dictionary<string, AudioStreamWav> _clips = new Dictionary<string, AudioStreamWav>();
        private readonly Dictionary<string, AudioStreamWav[]> _variants = new Dictionary<string, AudioStreamWav[]>();
        private readonly Random _pick = new Random();

        private struct ShotSpec
        {
            public float Duration, NoiseDecay, NoiseCutoff, ThumpFreq, ThumpDecay, ThumpGain, CrackGain, Gain, TailDecay, TailGain;
        }

        public SoundBank()
        {
            Shot("pistol", new ShotSpec { Duration = 0.45f, NoiseDecay = 0.035f, NoiseCutoff = 5200f, ThumpFreq = 140f, ThumpDecay = 0.05f, ThumpGain = 0.55f, CrackGain = 0.5f, Gain = 0.85f, TailDecay = 0.18f, TailGain = 0.15f });
            Shot("pistol_heavy", new ShotSpec { Duration = 0.7f, NoiseDecay = 0.06f, NoiseCutoff = 3800f, ThumpFreq = 95f, ThumpDecay = 0.09f, ThumpGain = 0.9f, CrackGain = 0.7f, Gain = 1f, TailDecay = 0.3f, TailGain = 0.25f });
            Shot("pistol_silenced", new ShotSpec { Duration = 0.25f, NoiseDecay = 0.02f, NoiseCutoff = 1500f, ThumpFreq = 220f, ThumpDecay = 0.03f, ThumpGain = 0.35f, CrackGain = 0.08f, Gain = 0.45f, TailDecay = 0.05f, TailGain = 0.03f });
            Shot("smg", new ShotSpec { Duration = 0.4f, NoiseDecay = 0.03f, NoiseCutoff = 4800f, ThumpFreq = 150f, ThumpDecay = 0.04f, ThumpGain = 0.5f, CrackGain = 0.45f, Gain = 0.75f, TailDecay = 0.16f, TailGain = 0.14f });
            Shot("smg_silenced", new ShotSpec { Duration = 0.22f, NoiseDecay = 0.018f, NoiseCutoff = 1400f, ThumpFreq = 230f, ThumpDecay = 0.025f, ThumpGain = 0.3f, CrackGain = 0.06f, Gain = 0.4f, TailDecay = 0.05f, TailGain = 0.03f });
            Shot("shotgun", new ShotSpec { Duration = 0.9f, NoiseDecay = 0.08f, NoiseCutoff = 2600f, ThumpFreq = 70f, ThumpDecay = 0.12f, ThumpGain = 1f, CrackGain = 0.6f, Gain = 1f, TailDecay = 0.4f, TailGain = 0.3f });
            Shot("rifle", new ShotSpec { Duration = 0.6f, NoiseDecay = 0.045f, NoiseCutoff = 4500f, ThumpFreq = 105f, ThumpDecay = 0.06f, ThumpGain = 0.7f, CrackGain = 0.65f, Gain = 0.9f, TailDecay = 0.26f, TailGain = 0.22f });
            Shot("rifle_heavy", new ShotSpec { Duration = 0.7f, NoiseDecay = 0.055f, NoiseCutoff = 3600f, ThumpFreq = 88f, ThumpDecay = 0.075f, ThumpGain = 0.85f, CrackGain = 0.7f, Gain = 1f, TailDecay = 0.3f, TailGain = 0.25f });
            Shot("rifle_silenced", new ShotSpec { Duration = 0.3f, NoiseDecay = 0.022f, NoiseCutoff = 1700f, ThumpFreq = 190f, ThumpDecay = 0.03f, ThumpGain = 0.4f, CrackGain = 0.1f, Gain = 0.5f, TailDecay = 0.07f, TailGain = 0.05f });
            Shot("sniper_heavy", new ShotSpec { Duration = 1.3f, NoiseDecay = 0.07f, NoiseCutoff = 3200f, ThumpFreq = 62f, ThumpDecay = 0.14f, ThumpGain = 1f, CrackGain = 1f, Gain = 1f, TailDecay = 0.6f, TailGain = 0.35f });
            Shot("sniper_light", new ShotSpec { Duration = 0.9f, NoiseDecay = 0.05f, NoiseCutoff = 4200f, ThumpFreq = 90f, ThumpDecay = 0.08f, ThumpGain = 0.75f, CrackGain = 0.85f, Gain = 0.95f, TailDecay = 0.4f, TailGain = 0.25f });
            Shot("sniper_auto", new ShotSpec { Duration = 0.9f, NoiseDecay = 0.06f, NoiseCutoff = 3600f, ThumpFreq = 78f, ThumpDecay = 0.1f, ThumpGain = 0.9f, CrackGain = 0.85f, Gain = 1f, TailDecay = 0.4f, TailGain = 0.28f });
            Shot("mg", new ShotSpec { Duration = 0.55f, NoiseDecay = 0.05f, NoiseCutoff = 3900f, ThumpFreq = 95f, ThumpDecay = 0.065f, ThumpGain = 0.8f, CrackGain = 0.55f, Gain = 0.9f, TailDecay = 0.25f, TailGain = 0.2f });

            _clips["knife"] = Swish(0.28f, 7);
            _clips["dry"] = Click(0.05f, 3000f, 0.6f, 11);
            _clips["mag_out"] = MagOut();
            _clips["mag_in"] = Click(0.12f, 1800f, 0.9f, 13);
            _clips["bolt"] = Bolt();
            _clips["deploy"] = Rattle(0.25f, 17);
            _clips["scope"] = Click(0.06f, 2400f, 0.35f, 19);
            _clips["headshot"] = Headshot();
            _clips["hit_body"] = BodyHit();
            _clips["hitmarker"] = Click(0.04f, 6000f, 0.35f, 23);
            _clips["land"] = Thump(0.25f, 70f, 0.08f, 0.9f, 29);
            _clips["jump"] = Thump(0.12f, 140f, 0.04f, 0.35f, 31);
            _clips["hurt"] = Thump(0.3f, 60f, 0.12f, 1f, 37);
            _clips["ui_click"] = Click(0.035f, 4200f, 0.3f, 41);
            _clips["ui_hover"] = Click(0.025f, 7000f, 0.12f, 43);
            _clips["ui_accept"] = Chime(new[] { 660f, 990f }, 0.35f);

            _variants["impact_concrete"] = Make(3, i => Impact(3500f, 0f, 51 + i));
            _variants["impact_metal"] = Make(3, i => Impact(6000f, 1f, 61 + i));
            _variants["impact_wood"] = Make(3, i => Impact(1400f, 0f, 71 + i));
            _variants["impact_dirt"] = Make(3, i => Impact(1100f, 0f, 81 + i));
            _variants["step_concrete"] = Make(4, i => Footstep(0.11f, 2400f, 0f, 0f, 91 + i));
            _variants["step_metal"] = Make(4, i => Footstep(0.3f, 4200f, 1f, 0f, 101 + i));
            _variants["step_wood"] = Make(4, i => Footstep(0.16f, 900f, 0f, 1f, 111 + i));
            _variants["step_dirt"] = Make(4, i => Footstep(0.18f, 1600f, 0f, 0.3f, 121 + i));
            _variants["shell"] = Make(3, i => Shell(131 + i));
        }

        public AudioStreamWav Get(string name)
        {
            AudioStreamWav c;
            if (_clips.TryGetValue(name, out c)) return c;
            AudioStreamWav[] v;
            if (_variants.TryGetValue(name, out v) && v.Length > 0) return v[0];
            return null;
        }

        public AudioStreamWav Pick(string name)
        {
            AudioStreamWav[] v;
            if (_variants.TryGetValue(name, out v) && v.Length > 0) return v[_pick.Next(v.Length)];
            return Get(name);
        }

        public AudioStreamWav ShotSound(string profile, bool silenced)
        {
            AudioStreamWav c;
            if (_clips.TryGetValue("shot_" + profile, out c)) return c;
            return _clips[silenced ? "shot_rifle_silenced" : "shot_rifle"];
        }

        // ------------------------------------------------------------------ synthesis

        private static AudioStreamWav[] Make(int count, Func<int, AudioStreamWav> make)
        {
            var arr = new AudioStreamWav[count];
            for (int i = 0; i < count; i++) arr[i] = make(i);
            return arr;
        }

        private void Shot(string profile, ShotSpec spec)
        {
            int n = (int)(spec.Duration * SampleRate);
            var data = new float[n];
            var rng = new Random(profile.Length * 7919 + profile[0] * 31 + profile[profile.Length - 1]);
            float lp = 0f, lpTail = 0f;
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
                phase += freq / SampleRate * Mathf.Pi * 2f;
                float thump = Mathf.Sin(phase) * Mathf.Exp(-t / spec.ThumpDecay) * spec.ThumpGain;
                float tail = lpTail * Mathf.Exp(-t / spec.TailDecay) * spec.TailGain * 3f;
                float attack = Mathf.Clamp(t / 0.0008f, 0f, 1f);
                data[i] = (noise * 1.6f + crack + thump + tail) * attack * spec.Gain;
            }
            _clips["shot_" + profile] = Create(data, 0.95f * spec.Gain);
        }

        private static AudioStreamWav Swish(float duration, int seed)
        {
            int n = (int)(duration * SampleRate);
            var data = new float[n];
            var rng = new Random(seed);
            float bp1 = 0f, bp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                float a = OnePole(Mathf.Lerp(600f, 3500f, Mathf.Sin(t * Mathf.Pi)));
                bp1 += a * (white - bp1);
                bp2 += a * 0.3f * (bp1 - bp2);
                float env = Mathf.Sin(t * Mathf.Pi);
                data[i] = (bp1 - bp2) * env * env;
            }
            return Create(data, 0.6f);
        }

        private static AudioStreamWav Click(float duration, float cutoff, float gain, int seed)
        {
            int n = (int)(duration * SampleRate);
            var data = new float[n];
            var rng = new Random(seed);
            float lp = 0f;
            float a = OnePole(cutoff);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += a * (white - lp);
                float hp = white - lp;
                data[i] = (hp * 0.7f + Mathf.Sin(t * cutoff * 0.5f * Mathf.Pi * 2f) * 0.3f) * Mathf.Exp(-t / (duration * 0.18f));
            }
            return Create(data, gain);
        }

        private static AudioStreamWav Chime(float[] freqs, float duration)
        {
            int n = (int)(duration * SampleRate);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float s = 0f;
                for (int k = 0; k < freqs.Length; k++)
                {
                    float start = k * 0.07f;
                    if (t < start) continue;
                    float tt = t - start;
                    s += Mathf.Sin(tt * freqs[k] * Mathf.Pi * 2f) * Mathf.Exp(-tt / 0.12f) * Mathf.Clamp(tt / 0.004f, 0f, 1f);
                }
                data[i] = s;
            }
            return Create(data, 0.35f);
        }

        private static AudioStreamWav MagOut()
        {
            int n = (int)(0.3f * SampleRate);
            var data = new float[n];
            var rng = new Random(5);
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
            return Create(data, 0.8f);
        }

        private static AudioStreamWav Bolt()
        {
            int n = (int)(0.4f * SampleRate);
            var data = new float[n];
            var rng = new Random(9);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                float e1 = Mathf.Exp(-t / 0.012f);
                float e2 = t > 0.18f ? Mathf.Exp(-(t - 0.18f) / 0.015f) : 0f;
                float ring = Mathf.Sin(t * 2900f * Mathf.Pi * 2f) * 0.3f;
                data[i] = (white * 0.7f + ring) * (e1 + e2);
            }
            return Create(data, 0.85f);
        }

        private static AudioStreamWav Rattle(float duration, int seed)
        {
            int n = (int)(duration * SampleRate);
            var data = new float[n];
            var rng = new Random(seed);
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
            return Create(data, 0.5f);
        }

        private static AudioStreamWav Headshot()
        {
            // Bright metallic "tink" with inharmonic partials — it must cut through gunfire.
            int n = (int)(0.5f * SampleRate);
            var data = new float[n];
            var rng = new Random(3);
            float[] partials = { 2650f, 4130f, 5370f, 7020f };
            float[] gains = { 1f, 0.6f, 0.45f, 0.25f };
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float s = 0f;
                for (int p = 0; p < partials.Length; p++)
                    s += Mathf.Sin(t * partials[p] * Mathf.Pi * 2f) * gains[p] * Mathf.Exp(-t / (0.12f / (p + 1)));
                float click = t < 0.003f ? (float)(rng.NextDouble() * 2.0 - 1.0) : 0f;
                data[i] = s * 0.6f + click;
            }
            return Create(data, 0.9f);
        }

        private static AudioStreamWav BodyHit()
        {
            int n = (int)(0.2f * SampleRate);
            var data = new float[n];
            var rng = new Random(4);
            float lp = 0f;
            float a = OnePole(700f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += a * (white - lp);
                data[i] = (lp * 2f + Mathf.Sin(t * 95f * Mathf.Pi * 2f) * 0.6f) * Mathf.Exp(-t / 0.035f);
            }
            return Create(data, 0.85f);
        }

        private static AudioStreamWav Thump(float duration, float freq, float decay, float gain, int seed)
        {
            int n = (int)(duration * SampleRate);
            var data = new float[n];
            var rng = new Random(seed);
            float lp = 0f;
            float a = OnePole(500f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += a * (white - lp);
                data[i] = (Mathf.Sin(t * freq * Mathf.Pi * 2f) + lp * 1.5f) * Mathf.Exp(-t / decay);
            }
            return Create(data, gain);
        }

        private static AudioStreamWav Impact(float cutoff, float metallic, int seed)
        {
            int n = (int)(0.25f * SampleRate);
            var data = new float[n];
            var rng = new Random(seed);
            float lp = 0f;
            float a = OnePole(cutoff);
            float ringFreq = 2000f + (float)rng.NextDouble() * 2500f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += a * (white - lp);
                float ring = metallic * Mathf.Sin(t * ringFreq * Mathf.Pi * 2f) * Mathf.Exp(-t / 0.06f) * 0.5f;
                data[i] = lp * Mathf.Exp(-t / 0.02f) + ring;
            }
            return Create(data, 0.7f);
        }

        private static AudioStreamWav Footstep(float duration, float cutoff, float metallic, float hollow, int seed)
        {
            int n = (int)(duration * SampleRate);
            var data = new float[n];
            var rng = new Random(seed);
            float lp = 0f;
            float a = OnePole(cutoff * (0.85f + (float)rng.NextDouble() * 0.3f));
            float ringFreq = 1100f + (float)rng.NextDouble() * 800f;
            float heelToe = 0.025f + (float)rng.NextDouble() * 0.02f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += a * (white - lp);
                float env = Mathf.Exp(-t / 0.018f) + (t > heelToe ? Mathf.Exp(-(t - heelToe) / 0.022f) * 0.6f : 0f);
                float ring = metallic * Mathf.Sin(t * ringFreq * Mathf.Pi * 2f) * Mathf.Exp(-t / 0.09f) * 0.4f;
                float body = hollow * Mathf.Sin(t * 160f * Mathf.Pi * 2f) * Mathf.Exp(-t / 0.04f) * 0.7f;
                data[i] = lp * env + ring + body;
            }
            return Create(data, 0.8f);
        }

        private static AudioStreamWav Shell(int seed)
        {
            int n = (int)(0.35f * SampleRate);
            var data = new float[n];
            var rng = new Random(seed);
            float f1 = 3800f + (float)rng.NextDouble() * 1500f;
            float f2 = f1 * 1.53f;
            float bounce = 0.09f + (float)rng.NextDouble() * 0.05f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float e = Mathf.Exp(-t / 0.03f) + (t > bounce ? Mathf.Exp(-(t - bounce) / 0.025f) * 0.5f : 0f);
                data[i] = (Mathf.Sin(t * f1 * Mathf.Pi * 2f) + Mathf.Sin(t * f2 * Mathf.Pi * 2f) * 0.5f) * e;
            }
            return Create(data, 0.35f);
        }

        private static float OnePole(float cutoff)
        {
            float dt = 1f / SampleRate;
            float rc = 1f / (2f * Mathf.Pi * cutoff);
            return dt / (rc + dt);
        }

        /// <summary>Normalises to <paramref name="peak"/> and packs into 16-bit little-endian PCM.</summary>
        private static AudioStreamWav Create(float[] data, float peak)
        {
            float max = 0f;
            for (int i = 0; i < data.Length; i++) max = Math.Max(max, Math.Abs(data[i]));
            float k = max > 1e-6f ? peak / max : 0f;
            var bytes = new byte[data.Length * 2];
            for (int i = 0; i < data.Length; i++)
            {
                int s = (int)Math.Round(Math.Clamp(data[i] * k, -1f, 1f) * 32767f);
                short v = (short)s;
                bytes[i * 2] = (byte)(v & 0xFF);
                bytes[i * 2 + 1] = (byte)((v >> 8) & 0xFF);
            }
            return new AudioStreamWav
            {
                Format = AudioStreamWav.FormatEnum.Format16Bits,
                MixRate = SampleRate,
                Stereo = false,
                Data = bytes
            };
        }
    }
}
