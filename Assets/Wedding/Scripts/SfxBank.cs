using UnityEngine;

namespace WeddingRun
{
    /// <summary>Small synthesized sound effects (no extra audio files needed).</summary>
    public static class SfxBank
    {
        const int Rate = 44100;

        public static AudioClip Bump, Slide, Join, Coin, Chime, Bad, Shield, ShieldBreak, Click, Hover, Beep, Go, Whoosh, Fanfare;

        public static void Build()
        {
            Coin = Notes("coin", new[] { 1318.5f, 1975.5f }, 0.07f, 0.35f, 0.5f, Wave.Bell);
            Chime = Notes("chime", new[] { 1046.5f, 1318.5f, 1568f, 2093f }, 0.07f, 0.45f, 0.45f, Wave.Bell);
            Bad = Notes("bad", new[] { 392f, 311f, 233f }, 0.09f, 0.2f, 0.35f, Wave.Square);
            Shield = Notes("shield", new[] { 784f, 988f, 1175f, 1568f }, 0.06f, 0.5f, 0.4f, Wave.Bell);
            ShieldBreak = Notes("shieldbreak", new[] { 1568f, 784f, 523f }, 0.05f, 0.3f, 0.4f, Wave.Triangle);
            Click = Notes("click", new[] { 1200f }, 0.0f, 0.06f, 0.3f, Wave.Triangle);
            Hover = Notes("hover", new[] { 1800f }, 0.0f, 0.04f, 0.12f, Wave.Sine);
            Beep = Notes("beep", new[] { 880f }, 0.0f, 0.18f, 0.35f, Wave.Triangle);
            Go = Notes("go", new[] { 1318.5f, 1760f }, 0.06f, 0.35f, 0.4f, Wave.Triangle);
            Whoosh = Noise("whoosh", 0.25f, 0.18f);
            Bump = Sweep("bump", 520f, 180f, 0.35f, 0.45f);
            Slide = Noise("slide", 0.3f, 0.14f);
            Join = Notes("join", new[] { 784f, 988f, 1175f, 1568f, 1976f }, 0.08f, 0.6f, 0.4f, Wave.Bell);
            Fanfare = Notes("fanfare", new[] { 523.3f, 659.3f, 784f, 1046.5f, 784f, 1046.5f }, 0.11f, 0.6f, 0.4f, Wave.Triangle);
        }

        enum Wave { Sine, Square, Triangle, Bell }

        static float Osc(Wave w, float phase)
        {
            float s = Mathf.Sin(phase);
            switch (w)
            {
                case Wave.Square: return s >= 0 ? 0.5f : -0.5f;
                case Wave.Triangle: return Mathf.Asin(s) * 0.6366f;
                case Wave.Bell: return s * 0.7f + Mathf.Sin(phase * 2.01f) * 0.2f + Mathf.Sin(phase * 3.98f) * 0.1f;
                default: return s;
            }
        }

        /// <summary>Plays notes one after another (step seconds apart), each ringing for decay seconds.</summary>
        static AudioClip Notes(string name, float[] freqs, float step, float decay, float volume, Wave wave)
        {
            float length = step * (freqs.Length - 1) + decay + 0.02f;
            int n = Mathf.CeilToInt(length * Rate);
            var data = new float[n];
            for (int k = 0; k < freqs.Length; k++)
            {
                int start = Mathf.FloorToInt(k * step * Rate);
                int len = Mathf.FloorToInt(decay * Rate);
                for (int i = 0; i < len && start + i < n; i++)
                {
                    float t = i / (float)Rate;
                    float env = Mathf.Min(1, t * 400f) * Mathf.Exp(-t * 5f / decay);
                    data[start + i] += Osc(wave, 2 * Mathf.PI * freqs[k] * t) * env * volume;
                }
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Cartoon "boing": a pitch slide with a wobble.</summary>
        static AudioClip Sweep(string name, float f0, float f1, float length, float volume)
        {
            int n = Mathf.CeilToInt(length * Rate);
            var data = new float[n];
            float ph = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float f = Mathf.Lerp(f0, f1, t) * (1 + Mathf.Sin(t * 60f) * 0.06f);
                ph += 2 * Mathf.PI * f / Rate;
                data[i] = Osc(Wave.Triangle, ph) * Mathf.Min(1, t * 60f) * (1 - t) * volume;
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip Noise(string name, float length, float volume)
        {
            int n = Mathf.CeilToInt(length * Rate);
            var data = new float[n];
            var rnd = new System.Random(3);
            float lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float env = Mathf.Sin(t * Mathf.PI);
                float cut = Mathf.Lerp(0.05f, 0.35f, t);
                lp += ((float)rnd.NextDouble() * 2 - 1 - lp) * cut;
                data[i] = lp * env * volume;
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
