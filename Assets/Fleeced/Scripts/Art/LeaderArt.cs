using System.Collections.Generic;
using UnityEngine;

namespace Fleeced
{
    public enum LExpr { Stern, Shout, Smug }

    /// The Supreme Leader: a big ram (fleece, curly horns, goat-eyes) in an officer's cap, sash and medals.
    public static class LeaderArt
    {
        public const int PW = 48, PH = 48;
        static readonly Dictionary<string, Texture2D> portraits = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();

        static Color32 C(string h) { return Pal.H(h); }
        static readonly Color32 Crown = Pal.H("2f3a22"), CrownHi = Pal.H("44523a"), Band = Pal.H("8c2f2f"), BandHi = Pal.H("b04545"),
            Gold = Pal.H("ffdc5a"), GoldLo = Pal.H("c9a227"), Visor = Pal.H("141210"), VisorHi = Pal.H("4a4440"),
            Face = Pal.H("2c2626"), FaceHi = Pal.H("403838"), Wool = Pal.H("f2eee2"), WoolLo = Pal.H("d4ccbc"),
            Lens = Pal.H("101014"), LensHi = Pal.H("8aa0b8"), Nose = Pal.H("e8a0a8"), Mouth = Pal.H("4a1010"), MouthIn = Pal.H("8c2020"),
            Teeth = Pal.H("f4efe6"), Tongue = Pal.H("e86a80"), Jacket = Pal.H("3a4628"), JacketLo = Pal.H("2a3420"), Sash = Pal.H("a02828"),
            SashHi = Pal.H("c84040"), EarIn = Pal.H("c98f84"),
            Horn = Pal.H("c8a070"), HornLo = Pal.H("8a6a44"), HornHi = Pal.H("ecd2a0"), Muzzle = Pal.H("3a3232"),
            Eye = Pal.H("f2e2a8"), Pupil = Pal.H("101010"), Brow = Pal.H("b8aca0"), NoseLo = Pal.H("b87078");

        /// 48x48 portrait for dialogue boxes: a big ram with a fleece, curly horns, an officer's cap and too many medals.
        public static Texture2D Portrait(LExpr e, bool mouthOpen)
        {
            string key = e + "_" + mouthOpen;
            Texture2D t;
            if (portraits.TryGetValue(key, out t)) return t;
            var c = new PixelCanvas(PW, PH, 1337);
            // ---- uniform over a woolly body ----
            for (int y = 38; y < PH; y++)
            {
                int half = Mathf.Min(24, 14 + (y - 38) * 2);
                for (int x = 24 - half; x < 24 + half; x++) c.Set(x, y, (x + y) % 7 == 0 ? JacketLo : Jacket);
            }
            // the chest is a sheep: wool between the lapels
            for (int y = 38; y < PH; y++)
            {
                float w = Mathf.Max(0, 5.5f - (y - 38) * 0.55f);
                for (int x = Mathf.RoundToInt(24 - w); x <= Mathf.RoundToInt(23 + w); x++) c.Set(x, y, (x + y) % 3 == 0 ? WoolLo : Wool);
            }
            // woolly shoulders bulging out of the jacket, epaulettes on top
            c.Ellipse(8, 39, 7, 3.6f, Wool); c.Ellipse(40, 39, 7, 3.6f, Wool);
            c.SpeckleOn(1, 37, 14, 5, WoolLo, 0.25f); c.SpeckleOn(33, 37, 14, 5, WoolLo, 0.25f);
            c.Rect(3, 40, 10, 3, Gold); c.HLine(3, 12, 40, Pal.H("fff3b0"));
            c.Rect(35, 40, 10, 3, Gold); c.HLine(35, 44, 40, Pal.H("fff3b0"));
            for (int x = 3; x <= 12; x += 2) c.Set(x, 43, GoldLo);
            for (int x = 35; x <= 44; x += 2) c.Set(x, 43, GoldLo);
            // red sash
            for (int k = 0; k < 10; k++) { int sy = Mathf.Min(46, 41 + (int)(k * 0.7f)); c.Rect(12 + k, sy, 3, 2, Sash); c.Set(12 + k, sy, SashHi); }
            // medals pinned straight into the fleece
            string[] rib = { "3a64a8", "c83a30", "f0c840" };
            for (int i = 0; i < 3; i++) { c.Rect(30 + i * 3, 42, 2, 3, C(rib[i])); c.Set(30 + i * 3, 45, Gold); }
            c.Set(35, 46, Gold); c.Set(34, 47, Gold); c.Set(36, 47, Gold); c.Set(35, 47, Pal.H("fff3b0"));
            // ---- curly ram horns ----
            CurlHorn(c, 8.5f, 22f, false);
            CurlHorn(c, 39.5f, 22f, true);
            // ---- floppy ears ----
            c.Ellipse(13.5f, 29.5f, 4.4f, 1.9f, Face); c.Ellipse(13.5f, 29.5f, 2.6f, 0.9f, EarIn);
            c.Ellipse(34.5f, 29.5f, 4.4f, 1.9f, Face); c.Ellipse(34.5f, 29.5f, 2.6f, 0.9f, EarIn);
            // ---- the fleece on top of the head ----
            c.Ellipse(24, 14, 11, 7, Wool);
            c.Ellipse(15.5f, 13, 5, 4.6f, Wool); c.Ellipse(32.5f, 13, 5, 4.6f, Wool);
            c.Ellipse(18.5f, 9, 4.6f, 3.6f, Wool); c.Ellipse(29.5f, 9, 4.6f, 3.6f, Wool); c.Ellipse(24, 8, 5, 3.6f, Wool);
            c.SpeckleOn(11, 14, 26, 6, WoolLo, 0.22f);
            c.Set(17, 10, Pal.H("ffffff")); c.Set(29, 9, Pal.H("ffffff")); c.Set(14, 13, Pal.H("ffffff"));
            // ---- long black sheep face ----
            c.Ellipse(24, 27.5f, 7.6f, 10, Face);
            c.Ellipse(24, 32.5f, 5.6f, 4.6f, Muzzle);
            c.Ellipse(21, 24, 2, 3, FaceHi);
            // forehead fringe
            c.Ellipse(19.5f, 18.5f, 3.3f, 2.3f, Wool); c.Ellipse(24, 18, 3.6f, 2.5f, Wool); c.Ellipse(28.5f, 18.5f, 3.3f, 2.3f, Wool);
            c.Set(22, 19, WoolLo); c.Set(26, 20, WoolLo);
            // ---- peaked officer cap perched on the wool ----
            c.Ellipse(24, 4.6f, 9.5f, 3.6f, Crown);
            c.HLine(18, 30, 2, CrownHi); c.HLine(16, 32, 3, CrownHi);
            c.Rect(15, 7, 19, 2, Band); c.HLine(15, 33, 7, BandHi);
            c.Ellipse(24, 4.8f, 2.6f, 2.6f, Gold);
            c.Line(23, 3, 25, 6, Pal.H("6b4f35")); c.Line(25, 3, 23, 6, Pal.H("6b4f35"));
            c.Ellipse(24, 9.6f, 10, 1.5f, Visor);
            c.HLine(16, 32, 9, VisorHi);
            // ---- goat-eyes with horizontal pupils (this is a sheep) ----
            float er = e == LExpr.Shout ? 2.9f : 2.5f, ery = e == LExpr.Shout ? 2.1f : 1.7f;
            c.Ellipse(20.5f, 23.5f, er, ery, Eye); c.Ellipse(28.5f, 23.5f, er, ery, Eye);
            if (e == LExpr.Shout) { c.Set(20, 23, Pupil); c.Set(28, 23, Pupil); c.Set(18, 24, Pal.H("e05040")); c.Set(30, 24, Pal.H("e05040")); }
            else if (e == LExpr.Smug)
            {
                c.HLine(18, 23, 22, Face); c.HLine(26, 31, 22, Face);
                c.HLine(18, 23, 23, FaceHi); c.HLine(26, 31, 23, FaceHi);
                c.HLine(19, 21, 24, Pupil); c.HLine(27, 29, 24, Pupil);
            }
            else { c.HLine(19, 21, 23, Pupil); c.HLine(27, 29, 23, Pupil); }
            // brows
            if (e == LExpr.Shout) { c.Line(17, 19, 22, 21, Brow); c.Line(26, 21, 31, 19, Brow); c.Line(17, 20, 22, 22, Brow); c.Line(26, 22, 31, 20, Brow); }
            else if (e == LExpr.Smug) { c.HLine(17, 22, 21, Brow); c.Line(26, 20, 31, 18, Brow); }
            else { c.Line(17, 20, 22, 21, Brow); c.Line(26, 21, 31, 20, Brow); }
            // ---- nose ----
            c.Set(22, 30, Nose); c.Set(23, 31, Nose); c.Set(26, 30, Nose); c.Set(25, 31, Nose);
            c.Set(24, 32, NoseLo); c.Set(24, 33, NoseLo);
            // ---- mouth ----
            if (e == LExpr.Shout || mouthOpen)
            {
                float rx = e == LExpr.Shout ? 3.8f : 2.6f, ry = e == LExpr.Shout ? 2.4f : 1.5f;
                c.Ellipse(24, 35.6f, rx, ry, MouthIn);
                if (e == LExpr.Shout) { c.HLine(22, 26, 34, Teeth); c.Set(23, 37, Tongue); c.Set(24, 37, Tongue); c.Set(25, 37, Tongue); }
                else c.HLine(23, 25, 35, Teeth);
            }
            else if (e == LExpr.Smug)
            {
                c.Set(21, 35, Mouth); c.Set(22, 35, Mouth); c.Set(23, 35, Mouth); c.Set(24, 35, Mouth);
                c.Set(25, 34, Mouth); c.Set(26, 34, Mouth); c.Set(27, 33, Mouth);
            }
            else { c.HLine(21, 27, 35, Mouth); c.Set(20, 36, Mouth); c.Set(28, 36, Mouth); }
            c.Outline(Pal.Ink);
            t = c.ToTexture();
            portraits[key] = t;
            return t;
        }

        /// A ram horn: a thick tapering spiral with ridges, curling back from the top of the head.
        static void CurlHorn(PixelCanvas c, float cx, float cy, bool mirror)
        {
            const int N = 70;
            float th0 = -Mathf.PI / 3f, turn = 2.05f * Mathf.PI;
            // body
            for (int i = 0; i <= N; i++)
            {
                float t = i / (float)N;
                float th = th0 - t * turn, r = 6.3f - 4.2f * t, w = 2.9f - 1.9f * t;
                float x = Mathf.Cos(th) * r, y = Mathf.Sin(th) * r;
                if (mirror) x = -x;
                c.Ellipse(cx + x, cy + y, w, w, Horn);
            }
            // inner shading, ridges and a highlight on the outside of the curl
            for (int i = 0; i <= N; i++)
            {
                float t = i / (float)N;
                float th = th0 - t * turn, r = 6.3f - 4.2f * t, w = 2.9f - 1.9f * t;
                float dx = Mathf.Cos(th), dy = Mathf.Sin(th);
                if (mirror) dx = -dx;
                c.Set(Mathf.RoundToInt(cx + dx * (r - w * 0.8f)), Mathf.RoundToInt(cy + dy * (r - w * 0.8f)), HornLo);
                if (i % 7 == 3 && w > 1.3f)
                    for (float k = -w * 0.7f; k <= w * 0.7f; k += 0.5f) c.Set(Mathf.RoundToInt(cx + dx * (r + k)), Mathf.RoundToInt(cy + dy * (r + k)), HornLo);
                if (i % 3 == 0 && t < 0.55f) c.Set(Mathf.RoundToInt(cx + dx * (r + w * 0.6f)), Mathf.RoundToInt(cy + dy * (r + w * 0.6f)), HornHi);
            }
        }

        /// Small bust that stands on the palace balcony (behind its own railing). frame 1 = saluting.
        public static Sprite Balcony(int frame)
        {
            string key = "balcony" + frame;
            Sprite s;
            if (sprites.TryGetValue(key, out s)) return s;
            var c = new PixelCanvas(24, 26, 9);
            int cx = 12;
            // body: jacket over wool, epaulettes
            c.Rect(cx - 7, 17, 14, 4, Jacket);
            c.Ellipse(cx, 16.5f, 7, 2, Wool);
            c.Rect(cx - 9, 17, 3, 2, Gold); c.Rect(cx + 6, 17, 3, 2, Gold);
            c.Line(cx - 4, 18, cx + 2, 20, Sash);
            // horns
            c.Ring(cx - 7, 11, 2.7f, 1.5f, Horn); c.Ring(cx + 7, 11, 2.7f, 1.5f, Horn);
            c.Set(cx - 7, 11, HornLo); c.Set(cx + 7, 11, HornLo);
            // fleece
            c.Ellipse(cx, 8, 6.2f, 4.4f, Wool);
            c.Ellipse(cx - 4.5f, 9, 2.6f, 2.6f, Wool); c.Ellipse(cx + 4.5f, 9, 2.6f, 2.6f, Wool);
            // ears
            c.Ellipse(cx - 5.5f, 13, 1.9f, 0.9f, Face); c.Ellipse(cx + 5.5f, 13, 1.9f, 0.9f, Face);
            // face
            c.Ellipse(cx, 12, 3.6f, 4.6f, Face);
            c.Ellipse(cx - 1.5f, 8, 1.8f, 1.3f, Wool); c.Ellipse(cx + 1.5f, 8, 1.8f, 1.3f, Wool);
            c.Set(cx - 2, 11, Eye); c.Set(cx - 1, 11, Pupil); c.Set(cx + 1, 11, Pupil); c.Set(cx + 2, 11, Eye);
            c.Set(cx, 14, Nose);
            // cap
            c.Ellipse(cx, 3.5f, 5, 1.9f, Crown);
            c.HLine(cx - 5, cx + 5, 5, Band);
            c.HLine(cx - 5, cx + 5, 6, Visor);
            c.Set(cx, 3, Gold); c.Set(cx - 1, 3, Gold);
            if (frame == 1)
            {
                // raised hoof: salute
                c.Rect(cx + 7, 7, 2, 11, Jacket); c.Rect(cx + 6, 5, 3, 2, Face);
            }
            // balcony railing (stone balustrade)
            Color32 st = Pal.H("d8d0c0"), stLo = Pal.H("a89c88");
            c.Rect(0, 20, 24, 2, st); c.HLine(0, 23, 20, Pal.H("f0e8d8"));
            for (int x = 1; x < 24; x += 3) c.Rect(x, 22, 2, 4, stLo);
            c.Outline(Pal.Ink);
            s = c.ToSprite(new Vector2(0.5f, 0f));
            sprites[key] = s;
            return s;
        }

        /// The Supreme Palace (3x3 footprint). Drawn into the building art cache via BuildingArt.
        public static void PaintPalace(PixelCanvas c, BuildingSprite s)
        {
            int W = c.w, base_ = c.h - 2;
            Color32 stone = Pal.H("cfc6b4"), stoneLo = Pal.H("a89c88"), col = Pal.H("ece4d4"), colLo = Pal.H("bdb2a0"), roof = Pal.H("7a6e60");
            int wallTop = base_ - 36;
            // steps
            for (int k = 0; k < 3; k++) c.Rect(6 - k * 2, base_ - 2 - k * 2, W - 12 + k * 4, 2, k % 2 == 0 ? stoneLo : stone);
            // walls
            BuildingArt.Bricks(c, 3, wallTop, W - 6, base_ - wallTop - 4, stone);
            // pediment roof with a gold emblem
            for (int j = 0; j < 12; j++)
            {
                int inset = (11 - j) * 2;
                c.HLine(1 + inset, W - 2 - inset, wallTop - 12 + j, j == 11 ? Pal.Shade(roof, 0.7f) : roof);
            }
            c.Ellipse(W / 2f, wallTop - 4, 3.2f, 3, Gold); c.Set(W / 2 - 1, wallTop - 5, Pal.H("fff3b0"));
            c.HLine(0, W - 1, wallTop, Pal.H("e8e0d0"));
            // columns
            int[] cols = { 6, 14, 31, 39 };
            foreach (int x in cols)
            {
                c.Rect(x, wallTop + 2, 4, base_ - wallTop - 8, col);
                c.VLine(x + 3, wallTop + 2, base_ - 7, colLo);
                c.Rect(x - 1, wallTop + 1, 6, 2, colLo); c.Rect(x - 1, base_ - 7, 6, 2, colLo);
            }
            // red banners with the shears emblem
            int[] bx = { 10, 34 };
            foreach (int x in bx)
            {
                c.Rect(x, wallTop + 3, 4, 16, Band);
                c.VLine(x, wallTop + 3, wallTop + 18, BandHi);
                c.Set(x, wallTop + 19, Band); c.Set(x + 3, wallTop + 19, Band);
                c.Ellipse(x + 1.5f, wallTop + 9, 1.4f, 1.4f, Gold);
            }
            // grand door
            c.Rect(19, base_ - 18, 10, 14, Pal.H("3a2418"));
            c.Ellipse(24, base_ - 18, 5, 3, Pal.H("3a2418"));
            c.VLine(24, base_ - 18, base_ - 5, Pal.H("2a180e"));
            c.Set(22, base_ - 11, Gold); c.Set(26, base_ - 11, Gold);
            // flags on the roof corners
            for (int k = 0; k < 2; k++)
            {
                int fx = k == 0 ? 3 : W - 4;
                c.VLine(fx, Mathf.Max(0, wallTop - 20), wallTop, Pal.H("5a5e64"));
                int dir = k == 0 ? 1 : -1;
                for (int j = 0; j < 4; j++) c.HLine(Mathf.Min(fx + dir, fx + dir * 6), Mathf.Max(fx + dir, fx + dir * 6), Mathf.Max(0, wallTop - 20) + j, Band);
                c.Set(fx + dir * 3, Mathf.Max(0, wallTop - 19), Gold);
            }
            // grime: it is still a field
            c.SpeckleOn(0, base_ - 10, W, 10, Pal.H("7a6a50"), 0.08f);
        }

        /// where the balcony bust stands, relative to the palace footprint origin (world units)
        public static readonly Vector2 BalconyOffset = new Vector2(1.5f, 1.45f);

        /// Trough with a visible food level (0 empty .. 3 heaped).
        public static Sprite Trough(int fill)
        {
            string key = "trough" + fill;
            Sprite s;
            if (sprites.TryGetValue(key, out s)) return s;
            var c = new PixelCanvas(32, 16, 4);
            Color32 wood = Pal.H("7a5638"), woodLo = Pal.H("5a3e28"), hay = Pal.H("c8a050"), grass = Pal.H("6a9a3a");
            c.Rect(1, 5, 30, 8, wood);
            c.HLine(1, 30, 5, Pal.H("9a7448"));
            for (int x = 4; x < 30; x += 6) c.VLine(x, 6, 12, woodLo);
            c.Rect(3, 6, 26, 3, Pal.H("2a1c12"));
            if (fill >= 1) c.Rect(3, 8 - fill, 26, fill + 1, hay);
            if (fill >= 2) c.SpeckleOn(3, 4, 26, 6, grass, 0.35f);
            if (fill >= 3) { for (int x = 5; x < 28; x += 4) c.Ellipse(x, 3.5f, 2, 1.5f, hay); c.SpeckleOn(2, 1, 28, 6, Pal.H("e0c070"), 0.25f); c.Set(10, 3, Pal.H("e07a28")); c.Set(20, 2, Pal.H("e07a28")); }
            c.VLine(3, 13, 15, woodLo); c.VLine(28, 13, 15, woodLo);
            c.Outline(Pal.Ink);
            s = c.ToSprite(new Vector2(0f, 0f));
            sprites[key] = s;
            return s;
        }
    }
}
