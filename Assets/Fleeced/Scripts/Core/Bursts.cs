using System.Collections.Generic;
using UnityEngine;

namespace Fleeced
{
    /// What the player sees when sheep appear at once (M12): gate releases, chamber litters, herd births, stampedes and stage
    /// openings. Each source adds up its lambs over a short window, then shows ONE number ("+12.3k LAMBS"), bigger from a thousand
    /// and again from a million, plays ONE sound and, for the player's own press only, shakes the camera a little (log10 of the
    /// count). Off screen, nothing shows, plays or shakes. Bursts keep their own list, so SOLD, WOOL and DOOR texts keep theirs.
    /// Visual only: nothing here is read by the simulation, and it never touches the simulation's rng.
    public partial class Game
    {
        public enum BurstKind { Gate, Stampede, Stage, Litter, Births }

        class BurstText { public Vector2 pos; public string text; public Color col; public float life; public int scale; }
        readonly List<BurstText> bursts = new List<BurstText>();
        const float BurstLife = 1.4f;
        float birthsSoundT; // births baa at most once every 3 s, farm-wide

        /// one burst of `n` lambs at `at` (`label` replaces the number). `held`: the player's own press, the only kind that shakes.
        void ShowBurst(Vector2 at, double n, BurstKind kind, string label = null, bool held = false)
        {
            if (!IsVisible(at)) return;
            if (label == null && !(n >= 1)) return;
            string text = label ?? "+" + UI.Fmt(n) + (n >= 2 ? " LAMBS" : " LAMB");
            if (kind == BurstKind.Stampede) text = "STAMPEDE! " + text;
            int scale = kind == BurstKind.Stage ? 3 : n >= 1e6 ? 3 : n >= 1e3 ? 2 : 1;
            if (kind == BurstKind.Stampede) scale = Mathf.Min(3, scale + 1);
            Color col = kind == BurstKind.Gate ? new Color(1f, 0.92f, 0.7f) : kind == BurstKind.Stampede || kind == BurstKind.Stage ? (Color)UI.Gold : new Color(1f, 0.6f, 0.75f);
            if (bursts.Count >= 12) bursts.RemoveAt(0); // the oldest goes
            bursts.Add(new BurstText { pos = at, text = text, col = col, life = BurstLife, scale = scale });
#if FLEECED_DEV
            DevHooks.BurstsShown++;
            if (label == null) DevHooks.BurstLambsShown += n;
#endif
            // one sound a burst
            double lg = System.Math.Log10(System.Math.Max(1, n));
            bool played = true;
            switch (kind)
            {
                case BurstKind.Gate:
                    if (n < 1000) Sfx.Play("pop", Mathf.Min(0.8f, 0.5f + 0.03f * (float)lg), Mathf.Max(0.7f, 1f - 0.04f * (float)lg));
                    else Sfx.Play("flock", 0.7f);
                    break;
                case BurstKind.Stampede: Sfx.Play("thud", 0.5f); Sfx.Play("flock", 0.8f); break;
                case BurstKind.Stage: Sfx.Play("thud", 0.7f); break;
                case BurstKind.Litter: Sfx.Play("pop", 0.4f, 1.3f); break;
                case BurstKind.Births:
                    if (n >= 100 && birthsSoundT <= 0) { Sfx.Baa(0.25f); birthsSoundT = 3f; }
                    else played = false;
                    break;
            }
#if FLEECED_DEV
            if (played) DevHooks.BurstSounds++;
#endif
            // a slight shake, only for the player's own press at the gate
            if (held && (kind == BurstKind.Gate || kind == BurstKind.Stampede))
            {
                float v = (float)ScaleMath.ShakeFor(n);
                if (kind == BurstKind.Stampede) v = Mathf.Min(0.45f, v + 0.15f);
                if (v > 0) shake = Mathf.Max(shake, v);
#if FLEECED_DEV
                DevHooks.LastShakeSet = v;
#endif
            }
        }

        /// every frame, from UpdateFx (real time)
        void UpdateBursts(float rdt)
        {
            if (birthsSoundT > 0) birthsSoundT -= rdt;
            for (int i = bursts.Count - 1; i >= 0; i--)
            {
                var b = bursts[i];
                b.life -= rdt;
                b.pos.y += rdt * 0.7f;
                if (b.life <= 0) bursts.RemoveAt(i);
            }
        }

        /// with the world texts (DrawWorldTexts): each burst at its integer text scale, fading over its last 0.4 s
        void DrawBursts(float scale)
        {
            foreach (var b in bursts)
            {
                Vector3 sp = cam.WorldToScreenPoint(b.pos);
                if (sp.z < 0) continue;
                float gx = sp.x / scale, gy = (Screen.height - sp.y) / scale;
                var c = b.col; c.a *= Mathf.Clamp01(b.life / 0.4f);
                float w = PixelFont.Width(b.text, b.scale);
                PixelFont.DrawOutlined(Mathf.Round(gx - w / 2), Mathf.Round(gy - 4 * b.scale), b.text, c, b.scale);
            }
        }

#if FLEECED_DEV
        /// bursts on screen now (tests)
        public int DevBurstsOnScreen { get { return bursts.Count; } }
#endif
    }
}
