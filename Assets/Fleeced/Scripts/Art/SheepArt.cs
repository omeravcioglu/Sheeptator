using System.Collections.Generic;
using UnityEngine;

namespace Fleeced
{
    public enum Expr { Normal, Derp, Panic, Sleepy, Happy, Tongue, Dead, Sad }

    public struct SheepLook
    {
        public int woolStage;   // 0 shaved .. 4 mega fluff
        public int woolCol, faceCol;
        public Expr expr;
        public Variant variant;
        public int frame;
        public bool bandage, mud, burned, blood, ghost, clone, udder;
        public int strain; // DNA strain (Genetics.*)

        public string Key()
        {
            return woolStage + "_" + woolCol + "_" + faceCol + "_" + (int)expr + "_" + (int)variant + "_" + frame + "_" +
                   (bandage ? 1 : 0) + (mud ? 1 : 0) + (burned ? 1 : 0) + (blood ? 1 : 0) + (ghost ? 1 : 0) + (clone ? 1 : 0) + (udder ? 1 : 0) + "_" + strain;
        }
    }

    public static class SheepArt
    {
        public const int W = 20, H = 20;
        public static readonly string[] WoolHex = { "f2eee2", "eadcbe", "c4bfb6", "dccfae", "9a7458" };
        public static readonly string[] FaceHex = { "2c2626", "5c4232", "e2d2c2", "6c6262" };
        /// fleece colour of each DNA strain (index = Genetics id): silk pearl, cream, jumbo, gold, glow
        static readonly string[] StrainWool = { "", "f6dcef", "fbf4e4", "e6d4b4", "f0c040", "a8f070", "f4f0ec", "fff2e6", "f8b8e0", "8a5a3a", "8c8c96", "f8e060", "c8b4f0", "f07838" };
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        public static Sprite Get(SheepLook l)
        {
            string k = l.Key();
            Sprite s;
            if (cache.TryGetValue(k, out s)) return s;
            s = Build(l).ToSprite(new Vector2(0.5f, 0f));
            cache[k] = s;
            return s;
        }

        public static PixelCanvas Build(SheepLook l)
        {
            var c = new PixelCanvas(W, H, l.Key().GetHashCode());
            int st = Mathf.Clamp(l.woolStage, 0, 4);
            float[] rxs = { 4.2f, 5.2f, 6.0f, 7.0f, 8.0f };
            float[] rys = { 2.9f, 3.5f, 4.1f, 4.7f, 5.5f };
            float rx = rxs[st], ry = rys[st];
            float cx = 8.5f, cy = 16.6f - ry;

            Color32 wool = Pal.H(WoolHex[Mathf.Clamp(l.woolCol, 0, WoolHex.Length - 1)]);
            Color32 face = Pal.H(FaceHex[Mathf.Clamp(l.faceCol, 0, FaceHex.Length - 1)]);
            if (l.variant == Variant.Golden) wool = Pal.H("eec450");
            if (l.variant == Variant.Black) { wool = Pal.H("3c383c"); }
            if (l.strain > 0 && l.strain < StrainWool.Length) wool = Pal.H(StrainWool[l.strain]);
            Color32 skin = Pal.H("e8a49a");
            Color32 leg = Pal.H("2e2622");
            if (l.faceCol == 2) leg = Pal.H("8a7a6a");

            // legs
            int lx0 = Mathf.RoundToInt(cx - rx * 0.55f), lx2 = Mathf.RoundToInt(cx + rx * 0.3f);
            int[] legs = { lx0, lx0 + 2, lx2, lx2 + 2 };
            for (int i = 0; i < 4; i++)
            {
                int lx = legs[i];
                int shift = (l.frame == 1 && (i == 0 || i == 2)) ? -1 : (l.frame == 1 ? 1 : 0);
                c.Set(lx, 17, leg); c.Set(lx, 18, leg); c.Set(lx + (l.frame == 1 ? shift : 0), 19, leg);
            }

            // body
            Color32 body = st == 0 ? skin : (st == 1 ? Pal.Lerp(skin, wool, 0.55f) : wool);
            c.Ellipse(cx, cy, rx, ry, body);
            if (st >= 2)
            {
                // fluffy bumps along top and back
                Color32 hi = Pal.Lerp(wool, new Color32(255, 255, 255, 255), 0.35f);
                if (l.variant == Variant.Black) hi = Pal.H("5a5458");
                int bumps = st + 2;
                for (int i = 0; i < bumps; i++)
                {
                    float bx = cx - rx + 1.5f + i * (2 * rx - 3) / Mathf.Max(1, bumps - 1);
                    float by = cy - ry + 1.2f + (i % 2) * 0.6f;
                    c.Ellipse(bx, by, 1.8f, 1.5f, body);
                    c.Set(Mathf.FloorToInt(bx), Mathf.FloorToInt(by - 1), hi);
                }
                c.Ellipse(cx - rx + 0.8f, cy + 0.5f, 1.6f, 1.6f, body);
                // underside shading
                for (int y = Mathf.FloorToInt(cy + ry * 0.45f); y < 17; y++)
                    for (int x = 0; x < W; x++)
                        if (c.Opaque(x, y) && c.Get(x, y).Equals(body)) c.Set(x, y, Pal.Shade(body, 0.82f));
                c.SpeckleOn(Mathf.FloorToInt(cx - rx), Mathf.FloorToInt(cy - ry), Mathf.CeilToInt(rx * 2), Mathf.CeilToInt(ry * 2), Pal.Shade(body, 0.9f), 0.12f);
            }
            if (l.variant == Variant.Rainbow && st >= 1)
            {
                string[] rb = { "e86060", "f0a040", "f0e060", "70d070", "60a0f0", "a070e0" };
                for (int y = 0; y < 17; y++)
                    for (int x = 0; x < W; x++)
                        if (c.Opaque(x, y)) c.Set(x, y, Pal.Lerp(Pal.H(rb[((x + y) / 2) % rb.Length]), c.Get(x, y), 0.35f));
            }
            if (l.variant == Variant.Golden && st >= 1)
                c.SpeckleOn(0, 0, W, 17, Pal.H("fff8c0"), 0.08f);
            // DNA strains: sheen, glints, glow and cow spots
            if (l.strain == 1 && st >= 1) c.SpeckleOn(0, 0, W, 17, Pal.H("ffffff"), 0.07f);
            if (l.strain == 4 && st >= 1) c.SpeckleOn(0, 0, W, 17, Pal.H("fff4b0"), 0.1f);
            if (l.strain == 5 && st >= 1) c.SpeckleOn(0, 0, W, 17, Pal.H("eaffc0"), 0.1f);
            if (l.strain == 6 && st >= 1)
            {
                // Turbo: a racing stripe
                int sy = Mathf.RoundToInt(cy);
                for (int x = 0; x < W; x++) if (c.Opaque(x, sy)) c.Set(x, sy, Pal.H("d83a30"));
            }
            if (l.strain == 8 && st >= 1) c.SpeckleOn(0, 0, W, 17, Pal.H("a8d8ff"), 0.18f);   // cotton candy swirls
            if (l.strain == 9 && st >= 1) c.SpeckleOn(0, 0, W, 17, Pal.H("6a4028"), 0.12f);   // chocolate
            if (l.strain == 10 && st >= 1) c.SpeckleOn(0, 0, W, 17, Pal.H("5a5a64"), 0.14f);  // wolf fur
            if (l.strain == 11 && st >= 1) c.SpeckleOn(0, 0, W, 17, Pal.H("ffffff"), 0.12f);  // static
            if (l.strain == 12 && st >= 1)
            {
                // Splitter: a seam down the middle, where it will split
                int sx = Mathf.RoundToInt(cx);
                for (int y = 0; y < 17; y++) if (c.Opaque(sx, y)) c.Set(sx, y, Pal.H("8a74c0"));
            }
            if (l.strain == 13 && st >= 1) { c.SpeckleOn(0, 0, W, 17, Pal.H("ffd060"), 0.16f); c.SpeckleOn(0, 0, W, 17, Pal.H("c83820"), 0.06f); } // embers
            if (l.strain == 2 && st >= 2)
            {
                Color32 spot = Pal.H("5a3a28");
                c.Ellipse(cx - rx * 0.35f, cy - ry * 0.15f, 1.4f, 1.1f, spot);
                c.Ellipse(cx + rx * 0.3f, cy + ry * 0.2f, 1.2f, 1.0f, spot);
                c.Set(Mathf.RoundToInt(cx + rx * 0.05f), Mathf.RoundToInt(cy - ry * 0.45f), spot);
            }
            if (st == 0)
            {
                // ribs + goosebumps: shaved sheep looks sad and naked
                c.Set(Mathf.RoundToInt(cx - 1), Mathf.RoundToInt(cy), Pal.Shade(skin, 0.85f));
                c.Set(Mathf.RoundToInt(cx + 1), Mathf.RoundToInt(cy), Pal.Shade(skin, 0.85f));
                c.Set(Mathf.RoundToInt(cx), Mathf.RoundToInt(cy + 1), Pal.Shade(skin, 0.85f));
            }

            if (l.variant == Variant.Spiky && st >= 1)
            {
                Color32 sp = Pal.H("9aa0a8");
                for (int i = 0; i < 4; i++)
                {
                    int sx = Mathf.RoundToInt(cx - rx + 2 + i * (rx * 2 - 4) / 3f);
                    int sy = Mathf.RoundToInt(cy - ry);
                    c.Set(sx, sy - 1, sp); c.Set(sx, sy - 2, sp);
                }
            }

            if (l.udder)
            {
                // a pink udder between the legs: this one is ready to be milked
                Color32 ud = Pal.H("f0a0b0");
                int ux = Mathf.RoundToInt(cx);
                c.Set(ux - 1, 16, ud); c.Set(ux, 16, ud); c.Set(ux + 1, 16, ud);
                c.Set(ux - 1, 17, ud); c.Set(ux + 1, 17, ud);
            }

            // head(s)
            float hx = Mathf.Min(cx + rx - 0.5f, 15.5f), hy = cy - ry * 0.35f;
            if (st == 0) { hx = cx + rx + 0.5f; hy = cy - 1.5f; }
            if (l.variant == Variant.TwoHeaded) DrawHead(c, hx - 2.5f, hy - 3.5f, face, wool, st, Expr.Derp, false, l.frame);
            DrawHead(c, hx, hy, face, wool, st, l.expr, l.variant == Variant.ThreeEyed, l.frame);

            // overlays
            if (l.mud)
            {
                Color32 m = Pal.H("5a3e28");
                for (int i = 0; i < 6; i++)
                {
                    int mx = Mathf.RoundToInt(cx - rx + (float)c.rng.NextDouble() * rx * 2), my = Mathf.RoundToInt(cy + (float)c.rng.NextDouble() * ry);
                    if (c.Opaque(mx, my)) c.Set(mx, my, m);
                    if (c.Opaque(mx + 1, my)) c.Set(mx + 1, my, m);
                }
                c.SpeckleOn(0, 17, W, 3, m, 0.6f);
            }
            if (l.blood) c.SpeckleOn(0, 0, W, 17, Pal.H("b02020"), 0.07f);
            if (l.bandage)
            {
                Color32 bw = Pal.H("f4f0e8");
                int y0 = Mathf.RoundToInt(cy - 1);
                for (int x = Mathf.FloorToInt(cx - rx + 1); x <= Mathf.CeilToInt(cx + rx - 2); x++)
                {
                    int yy = y0 + (x % 3 == 0 ? 0 : 1) - (int)((x - cx) * 0.3f);
                    if (c.Opaque(x, yy)) c.Set(x, yy, bw);
                    if (c.Opaque(x, yy + 1)) c.Set(x, yy + 1, bw);
                }
                c.Set(Mathf.RoundToInt(cx), y0, Pal.H("d02828"));
                // head bandage
                int hbx = Mathf.RoundToInt(hx), hby = Mathf.RoundToInt(hy - 2.6f);
                for (int x = hbx - 2; x <= hbx + 2; x++) c.Set(x, hby, bw);
                c.Set(hbx + 2, hby - 1, bw);
            }
            if (l.burned)
            {
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                        if (c.Opaque(x, y)) c.Set(x, y, Pal.Shade(c.Get(x, y), 0.45f));
                c.SpeckleOn(0, 0, W, H, Pal.H("120c0c"), 0.15f);
            }
            if (l.clone)
            {
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                        if (c.Opaque(x, y)) c.Set(x, y, Pal.Lerp(c.Get(x, y), Pal.H("8ae890"), 0.18f));
            }

            c.Outline(Pal.Ink);

            if (l.ghost)
            {
                for (int i = 0; i < c.px.Length; i++)
                {
                    var p = c.px[i];
                    if (p.a == 0) continue;
                    byte g = (byte)Mathf.Clamp((p.r + p.g + p.b) / 3 + 90, 0, 255);
                    c.px[i] = new Color32((byte)(g * 0.85f), (byte)(g * 0.95f), g, 150);
                }
            }
            return c;
        }

        static void DrawHead(PixelCanvas c, float hx, float hy, Color32 face, Color32 wool, int st, Expr e, bool third, int frame)
        {
            Color32 white = Pal.H("fbfbf6"), pupil = Pal.H("100c0c"), mouth = Pal.H("401818"), tongue = Pal.H("f07a90");
            Color32 ear = Pal.Shade(face, 0.8f);
            // ears (floppy)
            int ix = Mathf.RoundToInt(hx), iy = Mathf.RoundToInt(hy);
            c.Set(ix - 3, iy - 2, ear); c.Set(ix - 4, iy - 2 + (frame == 1 ? 1 : 0), ear);
            c.Set(ix + 3, iy - 2, ear); c.Set(ix + 4, iy - 1, ear);
            c.Ellipse(hx + 0.5f, hy, 2.7f, 3.1f, face);
            // forelock
            if (st >= 2)
            {
                c.Set(ix - 1, iy - 3, wool); c.Set(ix, iy - 3, wool); c.Set(ix + 1, iy - 3, wool); c.Set(ix, iy - 4, wool);
                if (st >= 4) { c.Set(ix - 1, iy - 4, wool); c.Set(ix + 1, iy - 4, wool); }
            }
            int ey = iy - 1;
            int L = ix - 2, R = ix + 1;
            switch (e)
            {
                case Expr.Sleepy:
                    c.Set(L, ey + 1, white); c.Set(L + 1, ey + 1, white); c.Set(R, ey + 1, white); c.Set(R + 1, ey + 1, white);
                    c.Set(L + 1, ey + 1, pupil); c.Set(R + 1, ey + 1, pupil);
                    c.Set(L, ey, Pal.Shade(face, 0.7f)); c.Set(L + 1, ey, Pal.Shade(face, 0.7f)); c.Set(R, ey, Pal.Shade(face, 0.7f)); c.Set(R + 1, ey, Pal.Shade(face, 0.7f));
                    break;
                case Expr.Happy:
                    c.Set(L, ey + 1, white); c.Set(L + 1, ey, white); c.Set(R, ey, white); c.Set(R + 1, ey + 1, white);
                    break;
                case Expr.Dead:
                    c.Set(L, ey, white); c.Set(L + 1, ey + 1, white); c.Set(L + 1, ey, pupil); c.Set(L, ey + 1, pupil);
                    c.Set(R, ey, white); c.Set(R + 1, ey + 1, white); c.Set(R + 1, ey, pupil); c.Set(R, ey + 1, pupil);
                    break;
                case Expr.Panic:
                    for (int dy = -1; dy <= 1; dy++) { c.Set(L, ey + dy, white); c.Set(L + 1, ey + dy, white); c.Set(R, ey + dy, white); c.Set(R + 1, ey + dy, white); }
                    c.Set(L + 1, ey, pupil); c.Set(R, ey, pupil);
                    break;
                default:
                    c.Set(L, ey, white); c.Set(L + 1, ey, white); c.Set(L, ey + 1, white); c.Set(L + 1, ey + 1, white);
                    c.Set(R, ey, white); c.Set(R + 1, ey, white); c.Set(R, ey + 1, white); c.Set(R + 1, ey + 1, white);
                    if (e == Expr.Derp) { c.Set(L, ey + 1, pupil); c.Set(R + 1, ey, pupil); }
                    else if (e == Expr.Sad) { c.Set(L, ey + 1, pupil); c.Set(R, ey + 1, pupil); c.Set(L, ey, Pal.Shade(face, 0.7f)); c.Set(R + 1, ey, Pal.Shade(face, 0.7f)); }
                    else { c.Set(L + 1, ey + 1, pupil); c.Set(R + 1, ey + 1, pupil); }
                    break;
            }
            // mouth
            if (e == Expr.Panic) { c.Set(ix, iy + 1, mouth); c.Set(ix + 1, iy + 1, mouth); c.Set(ix, iy + 2, mouth); c.Set(ix + 1, iy + 2, tongue); }
            else if (e == Expr.Sad) { c.Set(ix, iy + 2, mouth); c.Set(ix - 1, iy + 2, mouth); c.Set(ix + 1, iy + 2, mouth); }
            else
            {
                c.Set(ix, iy + 2, mouth); c.Set(ix + 1, iy + 2, mouth);
                if (e == Expr.Tongue || e == Expr.Derp || e == Expr.Dead) { c.Set(ix + 1, iy + 3, tongue); if (e != Expr.Derp) c.Set(ix + 1, iy + 4, tongue); }
            }
            if (third)
            {
                c.Set(ix, iy - 3, white); c.Set(ix + 1, iy - 3, white); c.Set(ix, iy - 4, white); c.Set(ix + 1, iy - 4, white);
                c.Set(ix + 1, iy - 3, pupil);
            }
        }
    }
}
