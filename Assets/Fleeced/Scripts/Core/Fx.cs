using System.Collections.Generic;
using UnityEngine;

namespace Fleeced
{
    public partial class Game
    {
        class Particle
        {
            public Transform t; public SpriteRenderer sr; public Vector2 pos, vel; public float life, max, size, grav, z; public Color col; public bool ground; public bool active; public bool smoke;
        }
        class FText { public Vector2 pos; public string text; public Color col; public float life, max; public bool speech; }

        readonly List<Particle> particles = new List<Particle>();
        readonly List<FText> ftexts = new List<FText>();
        GameObject fxRoot;

        Particle GetParticle()
        {
            foreach (var p in particles) if (!p.active) return p;
            if (particles.Count > 600) return null;
            if (fxRoot == null) { fxRoot = new GameObject("FX"); fxRoot.transform.SetParent(worldRoot.transform); }
            var go = new GameObject("p");
            go.transform.SetParent(fxRoot.transform);
            var np = new Particle { t = go.transform, sr = go.AddComponent<SpriteRenderer>() };
            np.sr.sprite = EnvArt.Pixel();
            particles.Add(np);
            return np;
        }

        public void SpawnParticles(Vector2 at, Color col, int n, float power)
        {
            for (int i = 0; i < n; i++)
            {
                var p = GetParticle();
                if (p == null) return;
                p.active = true; p.t.gameObject.SetActive(true);
                p.pos = at; p.z = 0;
                float a = Random.Range(0f, Mathf.PI * 2);
                p.vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.5f + 0.9f) * Random.Range(0.4f, 1f) * power;
                p.grav = 9f; p.life = p.max = Random.Range(0.5f, 1.1f);
                p.size = Random.Range(0.8f, 1.6f);
                p.col = col; p.ground = true; p.smoke = false;
            }
        }

        public void SpawnSmoke(Vector2 at, float size)
        {
            var p = GetParticle();
            if (p == null) return;
            p.active = true; p.t.gameObject.SetActive(true);
            p.pos = at + new Vector2(Random.Range(-0.1f, 0.1f), 0);
            p.vel = new Vector2(Random.Range(0.1f, 0.5f), Random.Range(0.6f, 1.1f));
            p.grav = -0.2f; p.life = p.max = Random.Range(1.5f, 2.8f);
            p.size = size * Random.Range(1.5f, 2.6f);
            float g = Random.Range(0.25f, 0.45f);
            p.col = new Color(g, g, g * 0.95f, 0.7f); p.ground = false; p.smoke = true;
        }

        public void SpawnSteam(Vector2 at)
        {
            var p = GetParticle();
            if (p == null) return;
            p.active = true; p.t.gameObject.SetActive(true);
            p.pos = at;
            p.vel = new Vector2(Random.Range(-0.2f, 0.2f), Random.Range(0.4f, 0.8f));
            p.grav = -0.1f; p.life = p.max = Random.Range(0.8f, 1.4f);
            p.size = Random.Range(1.2f, 2f);
            p.col = new Color(0.92f, 0.94f, 0.96f, 0.55f); p.ground = false; p.smoke = true;
        }

        public void FloatText(Vector2 at, string text, Color col)
        {
            if (ftexts.Count > 40) ftexts.RemoveAt(0);
            ftexts.Add(new FText { pos = at, text = text, col = col, life = 1.8f, max = 1.8f });
        }
        public void SpeechText(Vector2 at, string text)
        {
            if (!IsVisible(at) || pixelScale < 2) return;
            ftexts.Add(new FText { pos = at, text = text, col = new Color(1, 1, 1, 0.95f), life = 1.6f, max = 1.6f, speech = true });
        }

        void UpdateFx(float rdt, float dt)
        {
            float fdt = rdt; // particles run in real time so they don't freeze on pause abruptly
            if (dt <= 0 && launchSeq <= 0) fdt = rdt * 0.25f;
            foreach (var p in particles)
            {
                if (!p.active) continue;
                p.life -= fdt;
                if (p.life <= 0) { p.active = false; p.t.gameObject.SetActive(false); continue; }
                if (p.ground)
                {
                    p.z += p.vel.y * fdt; p.vel.y -= p.grav * fdt;
                    p.pos.x += p.vel.x * fdt;
                    if (p.z < 0) { p.z = 0; p.vel *= 0.4f; p.vel.y = Mathf.Abs(p.vel.y) * 0.3f; }
                }
                else { p.pos += p.vel * fdt; p.vel.y -= p.grav * fdt; }
                float k = p.life / p.max;
                p.t.position = new Vector3(p.pos.x, p.pos.y + p.z, 0);
                float s = p.smoke ? p.size * (1.6f - k * 0.6f) : p.size;
                p.t.localScale = new Vector3(s, s, 1);
                var c = p.col; c.a *= p.smoke ? k : Mathf.Clamp01(k * 2);
                p.sr.color = c;
                p.sr.sortingOrder = p.smoke ? 28000 : SortY(p.pos.y) + 5;
            }
            for (int i = ftexts.Count - 1; i >= 0; i--)
            {
                var f = ftexts[i];
                f.life -= rdt;
                f.pos.y += rdt * (f.speech ? 0.15f : 0.6f);
                if (f.life <= 0) ftexts.RemoveAt(i);
            }
            if (deathBannerT > 0) deathBannerT -= rdt;
            if (toastT > 0) toastT -= rdt;
            if (autosaveNoteT > 0) autosaveNoteT -= rdt;
            if (tierShownT > 0) tierShownT -= rdt;
        }

        public string deathBanner, deathCauseText; public float deathBannerT;

        void DrawWorldTexts(float scale)
        {
            foreach (var f in ftexts)
            {
                Vector3 sp = cam.WorldToScreenPoint(f.pos);
                if (sp.z < 0) continue;
                float gx = sp.x / scale, gy = (Screen.height - sp.y) / scale;
                float a = Mathf.Clamp01(f.life / f.max * 2.5f);
                var c = f.col; c.a *= a;
                float w = PixelFont.Width(f.text);
                if (f.speech)
                {
                    var r = new Rect(gx - w / 2 - 3, gy - 6, w + 6, 11);
                    UI.Fill(r, new Color(1, 1, 1, 0.85f * a));
                    UI.Fill(new Rect(r.x, r.yMax, 3, 2), new Color(1, 1, 1, 0.85f * a));
                    PixelFont.Draw(r.x + 3, r.y + 2, f.text, new Color(0.1f, 0.08f, 0.08f, a), 1f, false);
                }
                else PixelFont.DrawOutlined(Mathf.Round(gx - w / 2), Mathf.Round(gy - 4), f.text, c);
            }
        }
    }
}
