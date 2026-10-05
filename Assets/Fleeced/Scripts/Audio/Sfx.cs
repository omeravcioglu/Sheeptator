using System.Collections.Generic;
using UnityEngine;

namespace Fleeced
{
    /// Tiny procedural sound synth. No audio files needed.
    public static class Sfx
    {
        static AudioSource src;
        static readonly Dictionary<string, AudioClip[]> clips = new Dictionary<string, AudioClip[]>();
        public static bool muted;
        const int Rate = 22050;
        static readonly System.Random rng = new System.Random(7);

        public static void Init(GameObject host)
        {
            src = host.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0;
            if (Object.FindFirstObjectByType<AudioListener>() == null) host.AddComponent<AudioListener>();
            if (clips.Count > 0) return;
            clips["baa"] = new[] { Baa(0.95f, 0), Baa(1.15f, 1), Baa(0.8f, 2), Baa(1.35f, 3), Baa(1.05f, 4) };
            clips["coin"] = new[] { Make(0.18f, (t, i) => Sq(t, t < 0.06f ? 988 : 1318) * Env(t, 0.18f) * 0.25f) };
            clips["click"] = new[] { Make(0.04f, (t, i) => Sq(t, 660) * Env(t, 0.04f) * 0.15f) };
            clips["snip"] = new[] { Make(0.12f, (t, i) => (Noise() * 0.5f + Sq(t, 2400) * 0.2f) * Env(t, 0.12f) * (t % 0.06f < 0.03f ? 1 : 0.2f) * 0.4f) };
            clips["crunch"] = new[] { Make(0.35f, (t, i) => (Noise() * 0.8f + Mathf.Sin(t * 2 * Mathf.PI * 70) * 0.5f) * Env(t, 0.35f) * 0.5f) };
            clips["boom"] = new[] { Make(0.9f, (t, i) => (Noise() * 0.9f + Mathf.Sin(t * 2 * Mathf.PI * (50 - t * 30))) * Mathf.Exp(-t * 4) * 0.6f) };
            clips["build"] = new[] { Make(0.3f, (t, i) => (Noise() * 0.4f + Mathf.Sin(t * 2 * Mathf.PI * 180)) * Mathf.Exp(-t * 18) * 0.5f + (t > 0.15f ? (Noise() * 0.3f + Mathf.Sin(t * 2 * Mathf.PI * 160)) * Mathf.Exp(-(t - 0.15f) * 18) * 0.5f : 0)) };
            clips["fanfare"] = new[] { Make(0.7f, (t, i) => { float[] n = { 523, 659, 784, 1046 }; int k = Mathf.Min(3, (int)(t / 0.12f)); return Sq(t, n[k]) * 0.18f * (k == 3 ? Mathf.Exp(-(t - 0.36f) * 4) : 1f); }) };
            clips["sad"] = new[] { Make(1.4f, (t, i) => { float[] n = { 392, 370, 349, 330 }; int k = Mathf.Min(3, (int)(t / 0.3f)); float vib = k == 3 ? Mathf.Sin(t * 30) * 8 : 0; return Saw(t, n[k] + vib) * 0.18f * (k == 3 ? Mathf.Exp(-(t - 0.9f) * 3) : 1f); }) };
            clips["event"] = new[] { Make(0.35f, (t, i) => Sq(t, t < 0.12f ? 440 : 554) * Env(t, 0.35f) * 0.18f) };
            clips["howl"] = new[] { Make(1.2f, (t, i) => Saw(t, 300 + Mathf.Sin(t * 3) * 220 + t * 100) * Env(t, 1.2f) * 0.18f) };
            clips["thud"] = new[] { Make(0.25f, (t, i) => Mathf.Sin(t * 2 * Mathf.PI * (90 - t * 120)) * Mathf.Exp(-t * 14) * 0.8f) };
            clips["boing"] = new[] { Make(0.5f, (t, i) => Mathf.Sin(t * 2 * Mathf.PI * (200 + t * 900)) * Env(t, 0.5f) * 0.3f) };
            clips["rocket"] = new[] { Make(3.0f, (t, i) => Noise() * Mathf.Min(1, t * 2) * Mathf.Exp(-Mathf.Max(0, t - 2) * 2) * 0.5f) };
            clips["blip"] = new[] { Voice(150), Voice(170), Voice(135), Voice(190) };
            clips["squirt"] = new[] { Make(0.2f, (t, i) => Noise() * Mathf.Exp(-t * 16) * 0.3f + Mathf.Sin(t * 2 * Mathf.PI * (900 - t * 2600)) * Env(t, 0.2f) * 0.16f) };
            clips["nom"] = new[] { Make(0.12f, (t, i) => Noise() * 0.45f * Env(t, 0.12f) * ((t % 0.04f) < 0.02f ? 1f : 0.25f)) };
            clips["zap"] = new[] { Make(0.2f, (t, i) => Sq(t, 1200 + Noise() * 800) * Env(t, 0.2f) * 0.15f) };
        }

        public static void Play(string name, float vol = 1f, float pitch = 1f)
        {
            if (muted || src == null) return;
            AudioClip[] arr;
            if (!clips.TryGetValue(name, out arr)) return;
            src.pitch = pitch;
            src.PlayOneShot(arr[rng.Next(arr.Length)], vol);
        }

        public static void Baa(float vol = 0.6f)
        {
            if (muted || src == null) return;
            var arr = clips["baa"];
            src.pitch = 0.9f + (float)rng.NextDouble() * 0.25f;
            src.PlayOneShot(arr[rng.Next(arr.Length)], vol);
        }

        delegate float Gen(float t, int i);

        static AudioClip Make(float dur, Gen g)
        {
            int n = (int)(dur * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(g(i / (float)Rate, i), -1f, 1f);
            var clip = AudioClip.Create("sfx", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip Baa(float pitch, int seed)
        {
            float dur = 0.55f + seed * 0.04f;
            float f0 = 260 * pitch;
            float phase = 0;
            return Make(dur, (t, i) =>
            {
                float vib = Mathf.Sin(t * 2 * Mathf.PI * (11 + seed)) * (t > 0.12f ? 0.06f : 0.01f);
                float f = f0 * (1 + vib) * (t < 0.08f ? 1.15f - t * 1.5f : 1f) * (1 - t * 0.15f);
                phase += f / Rate;
                float p = phase % 1f;
                float saw = 2f * p - 1f;
                float pulse = p < 0.3f ? 1 : -0.4f;
                float s = saw * 0.5f + pulse * 0.4f + Mathf.Sin(phase * 2 * Mathf.PI * 3) * 0.25f;
                float env = Mathf.Min(1, t * 25) * Mathf.Clamp01((dur - t) * 5f) * (0.75f + 0.25f * Mathf.Sin(t * 2 * Mathf.PI * 7));
                return s * env * 0.32f;
            });
        }

        /// a gruff little vowel: the Supreme Leader's voice, one syllable at a time
        static AudioClip Voice(float f0)
        {
            float phase = 0;
            return Make(0.075f, (t, i) =>
            {
                float f = f0 * (1f + Mathf.Sin(t * 70f) * 0.04f);
                phase += f / Rate;
                float p = phase % 1f;
                float saw = 2f * p - 1f;
                float body = saw * 0.55f + Mathf.Sin(phase * 2 * Mathf.PI * 4.2f) * 0.35f;
                return body * Mathf.Min(1, t * 120) * Mathf.Clamp01((0.075f - t) * 40f) * 0.42f;
            });
        }

        static float Sq(float t, float f) { return ((t * f) % 1f) < 0.5f ? 1 : -1; }
        static float Saw(float t, float f) { return 2f * ((t * f) % 1f) - 1f; }
        static float Noise() { return (float)rng.NextDouble() * 2 - 1; }
        static float Env(float t, float d) { return Mathf.Min(1, t * 200) * Mathf.Clamp01(1 - t / d); }
    }
}
