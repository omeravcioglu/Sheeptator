using System.Collections.Generic;
using UnityEngine;

namespace Fleeced
{
    public class BuildingSprite
    {
        public Sprite sprite;
        public List<Vector2> smoke = new List<Vector2>();   // local units from pivot (bottom-left of footprint)
        public List<Vector2> sparks = new List<Vector2>();
        public List<Vector2> steam = new List<Vector2>();
    }

    public static class BuildingArt
    {
        public const int Top = 26; // extra pixels above footprint
        static readonly Dictionary<string, BuildingSprite> cache = new Dictionary<string, BuildingSprite>();

        static Color32 C(string h) { return Pal.H(h); }
        static readonly Color32 Wood = Pal.H("7a5638"), WoodDark = Pal.H("5a3e28"), Straw = Pal.H("b48c48"), Rust = Pal.H("8e4a2a"),
            Metal = Pal.H("6a7078"), MetalDark = Pal.H("4a4e56"), Stone = Pal.H("7c7470"), Shingle = Pal.H("8a3e2e"), Tin = Pal.H("8a8e94"),
            Blood = Pal.H("9a1a1a"), Yellow = Pal.H("e8c030"), Black = Pal.H("201818"), Glass = Pal.H("2a3440"), Lit = Pal.H("f0d070");

        public static BuildingSprite Get(BKind k, int level)
        {
            string key = k + "_" + level;
            BuildingSprite s;
            if (cache.TryGetValue(key, out s)) return s;
            var d = Buildings.Get(k);
            s = new BuildingSprite();
            int extra = level >= 4 ? (level - 3) * 18 + (d.w <= 2 ? 10 : 0) : 0;
            var c = new PixelCanvas(d.w * 16, d.h * 16 + Top + extra, key.GetHashCode());
            Paint(c, d, level, s);
            c.Outline(Pal.Ink);
            Shadow(c);
            s.sprite = c.ToSprite(new Vector2(0, 0));
            cache[key] = s;
            return s;
        }

        static void Shadow(PixelCanvas c)
        {
            for (int x = 1; x < c.w - 1; x++)
            {
                if (!c.Opaque(x, c.h - 1)) c.Set(x, c.h - 1, new Color32(20, 16, 12, 70));
            }
        }

        static Vector2 L(PixelCanvas c, int px, int py) { return new Vector2(px / 16f, (c.h - py) / 16f); }

        static void Paint(PixelCanvas c, BuildingDef d, int lv, BuildingSprite s)
        {
            if (d.kind == BKind.Funnel) { Funnel(c, lv, s); return; }
            if (d.kind == BKind.Palace) { LeaderArt.PaintPalace(c, s); return; }
            if (d.kind == BKind.Trough) { TroughPreview(c); return; }
            if (d.kind == BKind.Pen || d.kind == BKind.FeedLot) { PenPreview(c, d); return; }
            if (lv >= 4)
            {
                if (d.kind == BKind.House) { HouseMega(c, lv, s); return; }
                if (d.kind == BKind.Wheel) { WheelMega(c, lv, s); return; }
                if (d.kind == BKind.Plant) { Plant(c, lv, s); return; }
                Mega(c, d, lv, s); return;
            }
            switch (d.kind)
            {
                case BKind.House: House(c, lv, s); return;
                case BKind.Farm: Farm(c, lv, s); return;
                case BKind.Market: Market(c, lv, s); return;
                case BKind.Wheel: Wheel(c, lv, s); return;
                case BKind.GuardPost: Guard(c, lv, s); return;
                case BKind.Spa: Spa(c, lv, s); return;
                case BKind.Statue: Statue(c, lv, s); return;
                case BKind.Graveyard: Graveyard(c, lv, s); return;
                case BKind.Rocket: RocketPad(c, s); return;
                case BKind.CloneVat: Vat(c, s); return;
                case BKind.Flowers: Flowers(c); return;
                case BKind.Sign: SafetySign(c); return;
                case BKind.Plant: Plant(c, lv, s); return;
                case BKind.Shed: case BKind.Dairy: case BKind.Slaughter: case BKind.GeneLab: Machine(c, d, lv, s); return;
                case BKind.Lab: if (lv == 1) { Tent(c, s); return; } break;
            }
            Generic(c, d, lv, s);
        }

        // ---------- primitives ----------
        public static void Planks(PixelCanvas c, int x, int y, int w, int h, Color32 col)
        {
            for (int i = x; i < x + w; i += 3)
            {
                float f = 0.85f + (float)c.rng.NextDouble() * 0.25f;
                Color32 pc = Pal.Shade(col, f);
                int top = y + (c.rng.NextDouble() < 0.25 ? 1 : 0);
                c.Rect(i, top, Mathf.Min(3, x + w - i), h - (top - y), pc);
                c.VLine(i, top, y + h - 1, Pal.Shade(col, 0.65f));
                if (c.rng.NextDouble() < 0.5) c.Set(i + 1, y + 1 + c.rng.Next(Mathf.Max(1, h - 2)), Pal.Shade(col, 0.55f));
            }
            c.HLine(x, x + w - 1, y + h - 1, Pal.Shade(col, 0.6f));
        }
        public static void Bricks(PixelCanvas c, int x, int y, int w, int h, Color32 col)
        {
            c.Rect(x, y, w, h, Pal.Shade(col, 0.7f));
            for (int j = 0; j < h; j += 3)
            {
                int off = (j / 3) % 2 == 0 ? 0 : 2;
                for (int i = -off; i < w; i += 5)
                {
                    float f = 0.88f + (float)c.rng.NextDouble() * 0.22f;
                    for (int yy = y + j; yy < Mathf.Min(y + j + 2, y + h); yy++)
                        for (int xx = Mathf.Max(x, x + i); xx < Mathf.Min(x + w, x + i + 4); xx++) c.Set(xx, yy, Pal.Shade(col, f));
                }
            }
        }
        public static void Corrugated(PixelCanvas c, int x, int y, int w, int h, Color32 col, int rustSpots)
        {
            for (int i = x; i < x + w; i++) c.VLine(i, y, y + h - 1, Pal.Shade(col, (i % 2 == 0) ? 1f : 0.8f));
            for (int r = 0; r < rustSpots; r++)
            {
                float rx = x + (float)c.rng.NextDouble() * w, ry = y + (float)c.rng.NextDouble() * h;
                c.EllipseBlend(rx, ry, 1.5f + (float)c.rng.NextDouble() * 3, 1 + (float)c.rng.NextDouble() * 2, Pal.A(Rust, 170));
            }
            c.HLine(x, x + w - 1, y, Pal.Shade(col, 1.2f));
        }
        public static void Roof(PixelCanvas c, int x, int y, int w, int h, Color32 col, int style)
        {
            for (int j = 0; j < h; j++)
            {
                int inset = Mathf.RoundToInt((h - 1 - j) * 0.6f);
                int jag = style == 0 ? c.rng.Next(-1, 2) : 0;
                int x0 = x + inset + (j == 0 ? jag : 0), x1 = x + w - 1 - inset;
                for (int i = x0; i <= x1; i++)
                {
                    Color32 pc = col;
                    if (style == 0) pc = Pal.Shade(col, ((i * 7 + j * 3) % 5 == 0) ? 0.78f : (j % 2 == 0 ? 1f : 0.92f));
                    else if (style == 1) pc = Pal.Shade(col, (j % 3 == 2) ? 0.7f : (((i + (j / 3) * 2) % 4 == 0) ? 0.82f : 1f));
                    else pc = Pal.Shade(col, (i % 3 == 0) ? 0.85f : 1f);
                    c.Set(i, y + j, pc);
                }
            }
            // patches
            int patches = style == 2 ? 1 : 2 + c.rng.Next(2);
            for (int p = 0; p < patches; p++)
            {
                int pw = 4 + c.rng.Next(5), ph = 2 + c.rng.Next(3);
                int px = x + 3 + c.rng.Next(Mathf.Max(1, w - pw - 6)), py = y + 1 + c.rng.Next(Mathf.Max(1, h - ph - 1));
                Color32 pc = p % 2 == 0 ? Tin : (style == 0 ? C("6e7a6a") : Rust);
                for (int j = py; j < py + ph; j++) for (int i = px; i < px + pw; i++) if (c.Opaque(i, j)) c.Set(i, j, Pal.Shade(pc, (i % 2 == 0) ? 1 : 0.85f));
            }
            c.HLine(x, x + w - 1, y + h - 1, Pal.Shade(col, 0.55f));
        }
        public static void Door(PixelCanvas c, int x, int y, int w, int h, Color32 col)
        {
            c.Rect(x, y, w, h, Black);
            c.Rect(x + 1, y + 1, w - 2, h - 1, Pal.Shade(col, 0.7f));
            for (int i = x + 1; i < x + w - 1; i += 2) c.VLine(i, y + 1, y + h - 1, Pal.Shade(col, 0.55f));
            c.Set(x + w - 2, y + h / 2, Yellow);
        }
        public static void Window(PixelCanvas c, int x, int y, int w, int h, bool lit, bool broken)
        {
            c.Rect(x, y, w, h, WoodDark);
            c.Rect(x + 1, y + 1, w - 2, h - 2, lit ? Lit : Glass);
            if (w >= 5) c.VLine(x + w / 2, y + 1, y + h - 2, WoodDark);
            if (h >= 5) c.HLine(x + 1, x + w - 2, y + h / 2, WoodDark);
            if (broken) { c.Set(x + 1, y + 1, Black); c.Set(x + 2, y + 2, Black); c.Set(x + 1, y + 2, Pal.H("dfe8f0")); }
        }
        public static void Sign(PixelCanvas c, int x, int y, string icon, bool crooked)
        {
            c.Rect(x, y, 12, 11, WoodDark);
            c.Rect(x + 1, y + 1, 10, 9, Pal.H("c8a878"));
            c.Art(x + 2, y + 2, Icons.Art.ContainsKey(icon) ? Icons.Art[icon] : Icons.Art["crate"]);
            if (crooked) { c.Set(x + 11, y, Pal.Clear); c.Set(x, y + 10, Pal.Clear); }
        }
        public static Vector2 Chimney(PixelCanvas c, int x, int y, int h, Color32 col, bool striped)
        {
            for (int j = 0; j < h; j++)
            {
                Color32 pc = striped && (j / 3) % 2 == 0 ? C("c8c0b8") : col;
                if (striped && (j / 3) % 2 == 1) pc = C("a83228");
                c.HLine(x, x + 3, y + j, Pal.Shade(pc, 0.9f));
                c.Set(x, y + j, Pal.Shade(pc, 0.7f));
            }
            c.HLine(x - 1, x + 4, y, Pal.Shade(col, 0.6f));
            return L(c, x + 2, y - 1);
        }
        public static void Pipe(PixelCanvas c, int x0, int y0, int x1, int y1)
        {
            if (y0 == y1) { for (int i = Mathf.Min(x0, x1); i <= Mathf.Max(x0, x1); i++) { c.Set(i, y0, Pal.H("8a9098")); c.Set(i, y0 + 1, Pal.H("5a6068")); if (i % 6 == 0) { c.Set(i, y0 - 1, MetalDark); c.Set(i, y0 + 2, MetalDark); } } }
            else { for (int j = Mathf.Min(y0, y1); j <= Mathf.Max(y0, y1); j++) { c.Set(x0, j, Pal.H("8a9098")); c.Set(x0 + 1, j, Pal.H("5a6068")); if (j % 6 == 0) { c.Set(x0 - 1, j, MetalDark); c.Set(x0 + 2, j, MetalDark); } } }
        }
        public static void Stripes(PixelCanvas c, int x, int y, int w, int h)
        {
            for (int j = y; j < y + h; j++) for (int i = x; i < x + w; i++) c.Set(i, j, ((i + j) / 2) % 2 == 0 ? Yellow : Black);
        }
        public static void Gear(PixelCanvas c, float cx, float cy, float r, Color32 col)
        {
            c.Ellipse(cx, cy, r, r, col);
            for (int a = 0; a < 8; a++)
            {
                float ang = a * Mathf.PI / 4;
                c.Rect(Mathf.RoundToInt(cx + Mathf.Cos(ang) * (r + 0.5f)) - 1, Mathf.RoundToInt(cy + Mathf.Sin(ang) * (r + 0.5f)) - 1, 2, 2, col);
            }
            c.Ellipse(cx, cy, r * 0.35f, r * 0.35f, MetalDark);
        }
        public static void Crate(PixelCanvas c, int x, int y, int s)
        {
            c.Rect(x, y, s, s, Pal.H("9a6a3c"));
            c.Frame(x, y, s, s, WoodDark);
            c.Line(x, y, x + s - 1, y + s - 1, WoodDark);
        }
        public static void Barrel(PixelCanvas c, int x, int y)
        {
            c.Rect(x, y, 5, 7, Pal.H("7a4a2a"));
            c.HLine(x, x + 4, y + 1, MetalDark); c.HLine(x, x + 4, y + 5, MetalDark);
            c.VLine(x + 1, y, y + 6, Pal.H("8a5a34"));
        }
        public static void Stains(PixelCanvas c, int x, int y, int w, int n)
        {
            for (int i = 0; i < n; i++)
            {
                float sx = x + (float)c.rng.NextDouble() * w, sy = y + (float)c.rng.NextDouble() * 3;
                c.Ellipse(sx, sy, 1 + (float)c.rng.NextDouble() * 2, 0.8f + (float)c.rng.NextDouble(), Blood);
                if (c.rng.NextDouble() < 0.5) c.Set(Mathf.RoundToInt(sx + 3), Mathf.RoundToInt(sy + 1), Blood);
            }
        }
        public static void WoolPile(PixelCanvas c, int x, int y)
        {
            Color32 w = Pal.H("eee8dc");
            c.Ellipse(x + 4, y + 3, 4, 2.5f, w); c.Ellipse(x + 2, y + 2, 2, 2, w); c.Ellipse(x + 6, y + 1.5f, 2, 2, w);
            c.Set(x + 3, y + 1, Pal.H("ffffff"));
        }
        public static void Text(PixelCanvas c, int x, int y, string t, Color32 col, Color32 bg)
        {
            int w = PixelFont.StampWidth(t);
            c.Rect(x - 2, y - 2, w + 4, 11, bg);
            c.Frame(x - 2, y - 2, w + 4, 11, Black);
            PixelFont.Stamp(c, x, y, t, col);
        }

        // ---------- generic building ----------
        static void Generic(PixelCanvas c, BuildingDef d, int lv, BuildingSprite s)
        {
            int W = c.w, Hc = c.h;
            int wallH = lv == 1 ? 15 : lv == 2 ? 19 : 23;
            int base_ = Hc - 2;
            int wallTop = base_ - wallH;
            int roofH = Mathf.Clamp(d.h * 16 - wallH + 2, 10, 26);
            if (lv == 3) roofH = Mathf.Min(roofH, 10);
            Color32 wallCol = Wood, roofCol = Straw; int roofStyle = 0;
            bool brick = false, metal = lv == 3, stone = false;
            switch (d.kind)
            {
                case BKind.Barn: wallCol = C("8e3428"); roofCol = lv == 1 ? Straw : C("4a3a36"); break;
                case BKind.Textile: brick = true; wallCol = C("8a4a3a"); roofCol = Metal; roofStyle = 2; break;
                case BKind.Atelier: wallCol = C("d8c8a8"); roofCol = C("6a3a8a"); roofStyle = 1; break;
                case BKind.Trading: stone = true; wallCol = C("8a8480"); roofCol = C("3a4a6a"); roofStyle = 1; break;
                case BKind.Bank: stone = true; wallCol = C("c8c0b0"); roofCol = C("8a8070"); roofStyle = 1; break;
                case BKind.Lab: stone = true; wallCol = C("7a7a80"); roofCol = C("3a5a4a"); roofStyle = 1; break;
                case BKind.Storage: wallCol = C("7a6a50"); break;
                case BKind.Dairy: wallCol = C("d4d6d0"); roofCol = C("3a64a8"); roofStyle = 1; break;
                case BKind.Creamery: wallCol = C("dcc488"); roofCol = C("b8642a"); roofStyle = 1; break;
            }
            if (lv == 2 && roofStyle == 0) { roofCol = Shingle; roofStyle = 1; }
            if (metal) { roofCol = MetalDark; roofStyle = 2; }

            // walls
            int wx = 2, ww = W - 4;
            if (metal && d.kind != BKind.Atelier && d.kind != BKind.Bank) Corrugated(c, wx, wallTop, ww, wallH, d.kind == BKind.Barn ? C("8a5a6a") : Metal, 5);
            else if (brick) Bricks(c, wx, wallTop, ww, wallH, wallCol);
            else if (stone) Bricks(c, wx, wallTop, ww, wallH, wallCol);
            else
            {
                Planks(c, wx, wallTop, ww, wallH, wallCol);
                if (lv == 2) Bricks(c, wx, base_ - 5, ww, 5, Stone);
            }
            if (d.kind == BKind.Barn && !metal)
            {
                // barn trim
                c.HLine(wx, wx + ww - 1, wallTop, C("e8e0d0"));
            }

            // roof
            int roofY = wallTop - roofH + 2;
            if (d.kind == BKind.Textile)
            {
                // sawtooth roof
                for (int i = 0; i < W; i += 10)
                    for (int j = 0; j < 8; j++)
                        for (int k = 0; k < Mathf.Min(10, W - i) - (int)(j * 1.2f); k++)
                            c.Set(i + k, wallTop - j, Pal.Shade(Metal, j == 7 ? 0.6f : (k % 3 == 0 ? 0.85f : 1f)));
                for (int i = 0; i < W; i += 10) c.VLine(i, wallTop - 7, wallTop - 1, Glass);
            }
            else Roof(c, 0, roofY, W, roofH, roofCol, roofStyle);

            // door & windows
            int doorW = lv == 3 ? 8 : 6, doorH = lv == 1 ? 9 : 11;
            int doorX = W / 2 - doorW / 2;
            if (d.kind == BKind.Storage && lv == 3) { doorW = W / 2; doorX = W / 4; doorH = 14; }
            if (d.kind == BKind.Barn && !metal)
            {
                doorW = 10; doorX = W / 2 - 5; doorH = Mathf.Min(13, wallH - 2);
                c.Rect(doorX, base_ - doorH, doorW, doorH, C("6a2420"));
                c.Frame(doorX, base_ - doorH, doorW, doorH, C("e8e0d0"));
                c.Line(doorX, base_ - doorH, doorX + doorW - 1, base_ - 1, C("e8e0d0"));
                c.Line(doorX + doorW - 1, base_ - doorH, doorX, base_ - 1, C("e8e0d0"));
            }
            else if (d.kind == BKind.Storage && lv == 3)
            {
                c.Rect(doorX, base_ - doorH, doorW, doorH, MetalDark);
                for (int j = base_ - doorH; j < base_; j += 2) c.HLine(doorX, doorX + doorW - 1, j, Pal.Shade(Metal, 0.9f));
                Stripes(c, doorX, base_ - doorH - 2, doorW, 2);
            }
            else Door(c, doorX, base_ - doorH, doorW, doorH, metal ? MetalDark : WoodDark);

            if (wallH >= 15)
            {
                bool lit = lv >= 2;
                Window(c, 5, wallTop + 3, 6, 6, lit && c.rng.NextDouble() < 0.7, lv == 1 && c.rng.NextDouble() < 0.5);
                if (W >= 40) Window(c, W - 11, wallTop + 3, 6, 6, lit, c.rng.NextDouble() < 0.3);
            }

            // sign
            string icon = Icons.ForBuilding(d.kind);
            if (lv == 1) Sign(c, doorX + doorW + 1 <= W - 13 ? doorX + doorW + 1 : 2, wallTop + 1, icon, true);
            else Sign(c, W / 2 - 6, roofY + Mathf.Max(1, roofH / 2 - 7), icon, false);

            // level extras
            if (lv >= 2)
            {
                s.smoke.Add(Chimney(c, W - 9, roofY - 6, 9, Stone, false));
                Barrel(c, 0, base_ - 7);
                if (W >= 48) Crate(c, W - 7, base_ - 6, 6);
            }
            if (lv == 3)
            {
                s.smoke.Add(Chimney(c, 4, Mathf.Max(1, roofY - 14), 16, C("8a8a8a"), true));
                Stripes(c, 2, base_ - 2, W - 4, 2);
                Pipe(c, 1, wallTop + 2, 1, base_ - 3);
                Pipe(c, 1, wallTop + 2, 8, wallTop + 2);
                Gear(c, W - 8, wallTop + 6, 3.5f, Tin);
                Stains(c, 3, base_ - 3, W - 6, 4);
                c.Art(W - 12, base_ - 12, Icons.Art["skull"]);
                s.steam.Add(L(c, 2, wallTop + 2));
            }

            // type-specific flavor
            switch (d.kind)
            {
                case BKind.Shed:
                    WoolPile(c, 1, base_ - 6);
                    if (lv == 3)
                    {
                        // giant shears on the roof
                        int sx = W / 2, sy = Mathf.Max(2, roofY - 12);
                        c.Line(sx - 8, sy, sx + 2, sy + 10, Pal.H("c8ccd0")); c.Line(sx - 7, sy, sx + 3, sy + 10, Pal.H("8a9098"));
                        c.Line(sx + 8, sy, sx - 2, sy + 10, Pal.H("c8ccd0")); c.Line(sx + 7, sy, sx - 3, sy + 10, Pal.H("8a9098"));
                        c.Ring(sx - 4, sy + 12, 2.5f, 1.2f, Pal.H("c83a30")); c.Ring(sx + 4, sy + 12, 2.5f, 1.2f, Pal.H("c83a30"));
                        Stains(c, W / 2 - 6, base_ - 4, 12, 4);
                    }
                    break;
                case BKind.Workshop:
                    if (lv == 1)
                    {
                        // wash tub
                        c.Ellipse(W - 6, base_ - 3, 5, 3, WoodDark); c.Ellipse(W - 6, base_ - 4, 4, 1.5f, Pal.H("7aa0b8"));
                        s.steam.Add(L(c, W - 6, base_ - 6));
                    }
                    else
                    {
                        c.Ring(W - 8, base_ - 8, 6, 1.5f, Wood);
                        c.Line(W - 8, base_ - 14, W - 8, base_ - 2, WoodDark); c.Line(W - 14, base_ - 8, W - 2, base_ - 8, WoodDark);
                        c.Ellipse(W - 8, base_ - 8, 1.5f, 1.5f, Pal.H("8a4ab0"));
                    }
                    break;
                case BKind.Lab:
                    if (lv == 3)
                    {
                        int tx = W - 10;
                        for (int j = 0; j < 18; j++) c.HLine(tx + j / 6, tx + 5 - j / 6, roofY - j, j % 3 == 0 ? Tin : MetalDark);
                        c.Ellipse(tx + 2.5f, roofY - 20, 3, 3, Pal.H("a0f0c0"));
                        s.sparks.Add(L(c, tx + 2, roofY - 20));
                    }
                    else
                    {
                        // antenna + bubbling flask in window
                        c.VLine(W - 6, Mathf.Max(0, roofY - 10), roofY + 2, MetalDark);
                        c.Set(W - 7, Mathf.Max(0, roofY - 10), Pal.H("e04040"));
                        s.sparks.Add(L(c, W - 6, roofY - 10));
                    }
                    break;
                case BKind.Barn:
                    c.Art(W / 2 - 4, roofY + 3, Icons.Art["heart"]);
                    if (lv >= 2) { c.Ellipse(4, base_ - 2, 4, 2, Straw); }
                    break;
                case BKind.Storage:
                    Crate(c, 1, base_ - 6, 6); Crate(c, 3, base_ - 11, 5);
                    if (lv >= 2) Barrel(c, W - 6, base_ - 7);
                    break;
                case BKind.Textile:
                    s.smoke.Add(Chimney(c, W - 7, 1, wallTop - 7, C("6a3a30"), lv == 3));
                    if (lv >= 2) { Pipe(c, W - 4, wallTop, W - 4, base_ - 2); s.steam.Add(L(c, W - 4, base_ - 2)); }
                    // fabric rolls
                    c.Rect(2, base_ - 4, 7, 3, Pal.H("3a64a8")); c.Rect(3, base_ - 7, 6, 3, Pal.H("a83a3a"));
                    break;
                case BKind.Atelier:
                    // striped awning
                    for (int i = 2; i < W - 2; i++) for (int j = 0; j < 4; j++) c.Set(i, wallTop + j, (i / 3) % 2 == 0 ? Pal.H("8a4ab0") : Pal.H("f0e8f0"));
                    c.Rect(W - 9, base_ - 12, 4, 10, Pal.H("e8d0b0")); c.Ellipse(W - 7, base_ - 13, 2, 2, Pal.H("e8d0b0")); c.Rect(W - 10, base_ - 10, 6, 4, Pal.H("c83a30"));
                    break;
                case BKind.Bank:
                    for (int i = 4; i < W - 4; i += 8) { c.Rect(i, wallTop + 2, 3, wallH - 3, Pal.H("e8e4dc")); c.VLine(i + 2, wallTop + 2, base_ - 2, Pal.H("b8b0a0")); }
                    break;
                case BKind.Trading:
                    c.Art(W - 10, wallTop + 2, Icons.Art["chart"]);
                    break;
                case BKind.Dairy:
                    // milk churns and a bucket
                    for (int k = 0; k < 2; k++)
                    {
                        int mx = 1 + k * 5;
                        c.Rect(mx, base_ - 9, 4, 8, C("c8ccd0")); c.HLine(mx, mx + 3, base_ - 9, C("8a9098")); c.Rect(mx + 1, base_ - 11, 2, 2, C("8a9098"));
                        c.VLine(mx + 3, base_ - 8, base_ - 2, C("a0a4aa"));
                    }
                    c.Rect(W - 7, base_ - 5, 5, 4, C("8a6440")); c.HLine(W - 7, W - 3, base_ - 5, C("f4f4f0"));
                    if (lv >= 3) { Pipe(c, W - 3, wallTop, W - 3, base_ - 6); s.steam.Add(L(c, W - 3, base_ - 6)); }
                    break;
                case BKind.Creamery:
                    // stacked cheese wheels
                    c.Ellipse(5, base_ - 3, 4, 2, C("f0c840")); c.Ellipse(5, base_ - 6, 3.5f, 1.8f, C("e8b830")); c.Ellipse(5, base_ - 9, 3, 1.6f, C("f0c840"));
                    c.Set(4, base_ - 3, C("c89820")); c.Set(6, base_ - 6, C("c89820"));
                    c.Ellipse(W - 6, base_ - 3, 3.5f, 2, C("f0c840")); c.Set(W - 6, base_ - 3, C("c89820"));
                    s.steam.Add(L(c, W / 2, wallTop - 2));
                    break;
            }
        }

        // ---------- special buildings ----------
        static void House(PixelCanvas c, int lv, BuildingSprite s)
        {
            int W = c.w, base_ = c.h - 2;
            var sheepFace = "kwwk|wkwk"; // tiny
            if (lv == 1)
            {
                int wallTop = base_ - 13;
                Planks(c, 4, wallTop, W - 8, 13, Wood);
                Roof(c, 1, wallTop - 14, W - 2, 16, Straw, 0);
                Door(c, 8, base_ - 9, 6, 9, WoodDark);
                c.Ellipse(22, wallTop + 5, 3, 3, WoodDark); c.Ellipse(22, wallTop + 5, 2, 2, Glass);
                c.Set(21, wallTop + 4, Pal.H("f2eee2")); c.Set(22, wallTop + 4, Pal.H("f2eee2")); c.Set(22, wallTop + 5, Black);
                c.Set(3, base_ - 1, WoodDark); c.Set(W - 4, wallTop - 1, Pal.H("6e7a6a"));
            }
            else if (lv == 2)
            {
                int wallTop = base_ - 18;
                Planks(c, 3, wallTop, W - 6, 18, Pal.H("8a6440"));
                Bricks(c, 3, base_ - 5, W - 6, 5, Stone);
                Roof(c, 0, wallTop - 16, W, 18, Shingle, 1);
                Door(c, 5, base_ - 10, 6, 10, WoodDark);
                Window(c, 18, wallTop + 4, 7, 6, true, false);
                c.Art(19, wallTop + 5, sheepFace);
                s.smoke.Add(Chimney(c, 22, wallTop - 20, 8, Stone, false));
                // laundry line with tiny sweaters
                c.HLine(0, 12, wallTop - 1, Pal.H("c8b898"));
                c.Rect(2, wallTop, 3, 3, Pal.H("c83a30")); c.Rect(7, wallTop, 3, 3, Pal.H("3a64a8"));
            }
            else
            {
                int wallTop = base_ - 34;
                Bricks(c, 2, wallTop, W - 4, 34, Pal.H("8a5a48"));
                c.Rect(1, wallTop - 3, W - 2, 3, MetalDark);
                for (int fl = 0; fl < 3; fl++)
                    for (int wi = 0; wi < 2; wi++)
                    {
                        int wx = 5 + wi * 13, wy = wallTop + 3 + fl * 10;
                        if (fl == 2 && wi == 0) continue;
                        Window(c, wx, wy, 8, 7, (fl + wi) % 2 == 0, fl == 1 && wi == 1);
                        if ((fl * 2 + wi) % 3 == 0) c.Art(wx + 2, wy + 2, sheepFace);
                        c.HLine(wx - 1, wx + 8, wy + 7, Pal.H("5a5e64"));
                    }
                Door(c, 5, base_ - 9, 7, 9, MetalDark);
                // AC unit + satellite dish
                c.Rect(W - 7, wallTop + 14, 5, 4, Tin); c.Set(W - 5, wallTop + 18, Pal.H("7aa0b8"));
                c.Ellipse(W - 6, wallTop - 6, 3, 2, Pal.H("c8ccd0")); c.VLine(W - 6, wallTop - 4, wallTop - 3, MetalDark);
                s.steam.Add(L(c, W - 5, wallTop + 19));
            }
        }

        static void Farm(PixelCanvas c, int lv, BuildingSprite s)
        {
            int W = c.w, base_ = c.h - 2, top = Top + 2;
            // soil field
            Color32 soil = Pal.H("6a4a30");
            c.Rect(1, top, W - 2, base_ - top, soil);
            c.Frame(0, top - 1, W, base_ - top + 2, WoodDark);
            for (int j = top + 2; j < base_ - 1; j += 4)
            {
                c.HLine(2, W - 3, j + 2, Pal.Shade(soil, 0.75f));
                for (int i = 3; i < W - 3; i += 3)
                {
                    if (lv == 1) { c.Set(i, j, Pal.H("5aa040")); if (c.rng.NextDouble() < 0.5) c.Set(i, j - 1, Pal.H("7ac050")); }
                    else if (lv == 2) { c.Set(i, j, Pal.H("e07a28")); c.Set(i, j - 1, Pal.H("5aa040")); c.Set(i + 1, j - 2, Pal.H("5aa040")); c.Set(i - 1, j - 2, Pal.H("4a8a30")); }
                    else { c.Set(i, j, Pal.H("d8b048")); c.Set(i, j - 1, Pal.H("e8c860")); c.Set(i + 1, j - 1, Pal.H("c8a038")); c.Set(i, j - 2, Pal.H("e8c860")); }
                }
            }
            // fence posts on top edge
            for (int i = 0; i < W; i += 6) c.VLine(i, top - 4, top, WoodDark);
            c.HLine(0, W - 1, top - 3, Wood);
            if (lv < 3)
            {
                // scarecrow (it's a sheep in a hat. it volunteered)
                int sx = W - 10, sy = top + 2;
                c.VLine(sx + 3, sy + 3, sy + 12, WoodDark); c.HLine(sx, sx + 6, sy + 6, WoodDark);
                c.Ellipse(sx + 3, sy + 3, 3, 2.5f, Pal.H("f2eee2")); c.Rect(sx + 3, sy + 2, 3, 3, Pal.H("2c2626")); c.Set(sx + 4, sy + 2, Pal.H("ffffff"));
                c.Rect(sx, sy - 1, 7, 1, Straw); c.Rect(sx + 1, sy - 3, 5, 2, Straw);
                // small tool shed
                Planks(c, 2, top - 14, 12, 12, Wood);
                Roof(c, 0, top - 22, 16, 9, Straw, 0);
            }
            else
            {
                // silo + combine harvester
                int sx = 2;
                for (int j = 0; j < 32; j++) c.HLine(sx, sx + 9, top + 6 - j, Pal.Shade(Tin, j % 4 == 0 ? 0.75f : 1f));
                c.Ellipse(sx + 4.5f, top - 26, 5, 3, Pal.H("a83228"));
                int hx = W - 22, hy = base_ - 14;
                c.Rect(hx, hy, 16, 9, Pal.H("c83a30")); c.Rect(hx + 9, hy - 6, 7, 6, Pal.H("c83a30")); c.Rect(hx + 10, hy - 5, 5, 4, Glass);
                c.Ellipse(hx + 4, hy + 10, 3, 3, Black); c.Ellipse(hx + 13, hy + 10, 3, 3, Black);
                for (int i = 0; i < 6; i++) c.Line(hx - 4, hy + 2 + i, hx, hy + i, Pal.H("c8ccd0"));
                Stripes(c, hx, hy + 8, 16, 1);
                Stains(c, hx - 6, hy + 4, 8, 4);
                s.smoke.Add(L(c, hx + 14, hy - 7));
                Pipe(c, sx + 10, top - 10, W - 3, top - 10);
            }
        }

        static void Market(PixelCanvas c, int lv, BuildingSprite s)
        {
            int W = c.w, base_ = c.h - 2;
            int stalls = lv;
            int sw = (W - 4) / stalls;
            string[] aw = { "c83a30", "3a64a8", "5aa040" };
            for (int i = 0; i < stalls; i++)
            {
                int x = 2 + i * sw, wdt = sw - 2;
                // counter
                Planks(c, x + 1, base_ - 9, wdt - 2, 9, Wood);
                // posts
                c.VLine(x + 1, base_ - 22, base_ - 1, WoodDark); c.VLine(x + wdt - 2, base_ - 22, base_ - 1, WoodDark);
                // awning
                for (int j = 0; j < 6; j++) for (int k = x - (5 - j) / 3; k < x + wdt + (5 - j) / 3; k++)
                        c.Set(k, base_ - 26 + j, ((k - x) / 3) % 2 == 0 ? Pal.H(aw[i % 3]) : Pal.H("f0ead8"));
                for (int k = x; k < x + wdt; k += 3) c.Set(k + 1, base_ - 20, ((k - x) / 3) % 2 == 0 ? Pal.H(aw[i % 3]) : Pal.H("f0ead8"));
                // goods on counter
                WoolPile(c, x + 2, base_ - 13);
                if (wdt > 14) c.Art(x + wdt - 10, base_ - 17, Icons.Art[i == 0 ? "coin" : i == 1 ? "yarn" : "clothing"]);
            }
            Crate(c, W - 7, base_ - 6, 6);
            if (lv >= 2)
            {
                // bunting
                for (int i = 0; i < W; i += 4) { c.Set(i, 4 + (i % 8 == 0 ? 0 : 1), Pal.H("e8c030")); c.Set(i + 1, 5 + (i % 8 == 0 ? 0 : 1), Pal.H("c83a30")); }
                Barrel(c, 0, base_ - 7);
            }
            if (lv == 3)
            {
                Text(c, W / 2 - 12, 2, "WOOL", Pal.H("ffe070"), Pal.H("8a2a20"));
                s.sparks.Add(L(c, W / 2, 6));
            }
        }

        static void Wheel(PixelCanvas c, int lv, BuildingSprite s)
        {
            int W = c.w, base_ = c.h - 2;
            float r = lv == 1 ? 12 : lv == 2 ? 14 : 15;
            float cx = W / 2f, cy = base_ - r - 1;
            Color32 rim = lv == 1 ? Wood : Metal;
            // A-frame
            c.Line((int)cx, (int)cy, 3, base_, WoodDark); c.Line((int)cx, (int)cy, W - 4, base_, WoodDark);
            c.Ring(cx, cy, r, 2.2f, rim);
            c.Ring(cx, cy, r - 2.2f, 0.8f, Pal.Shade(rim, 0.7f));
            for (int a = 0; a < 8; a++)
            {
                float ang = a * Mathf.PI / 4;
                c.Line((int)cx, (int)cy, (int)(cx + Mathf.Cos(ang) * (r - 2)), (int)(cy + Mathf.Sin(ang) * (r - 2)), Pal.Shade(rim, 0.75f));
            }
            c.Ellipse(cx, cy, 2, 2, MetalDark);
            if (lv >= 2)
            {
                c.Rect(W - 10, base_ - 9, 8, 9, Metal);
                c.Art(W - 10, base_ - 9, Icons.Art["power"]);
                Pipe(c, W - 6, base_ - 14, W - 6, base_ - 10);
            }
            if (lv == 3) { Stripes(c, 2, base_ - 2, W - 4, 2); Stains(c, 4, base_ - 3, W - 8, 3); }
            s.sparks.Add(L(c, W - 6, base_ - 12));
        }

        static void Guard(PixelCanvas c, int lv, BuildingSprite s)
        {
            int W = c.w, base_ = c.h - 2;
            if (lv < 3)
            {
                int h = lv == 1 ? 26 : 34;
                c.Line(6, base_, 10, base_ - h, WoodDark); c.Line(W - 7, base_, W - 11, base_ - h, WoodDark);
                c.Line(8, base_ - 6, W - 9, base_ - 16, Wood); c.Line(W - 9, base_ - 6, 8, base_ - 16, Wood);
                Planks(c, 6, base_ - h - 8, W - 12, 8, Wood);
                Roof(c, 4, base_ - h - 18, W - 8, 10, lv == 1 ? Straw : Shingle, lv == 1 ? 0 : 1);
                c.Art(W / 2 - 4, base_ - h - 7, Icons.Art["shield"]);
                // spikes
                for (int i = 0; i < W; i += 4) { c.VLine(i, base_ - 6, base_ - 1, Wood); c.Set(i, base_ - 7, Pal.H("c8ccd0")); }
            }
            else
            {
                Bricks(c, 2, base_ - 30, W - 4, 30, Stone);
                for (int i = 2; i < W - 2; i += 6) c.Rect(i, base_ - 34, 4, 4, Stone);
                Door(c, W / 2 - 4, base_ - 12, 8, 12, WoodDark);
                c.VLine(W - 6, 0, base_ - 34, WoodDark);
                c.Rect(W - 5, 1, 7, 5, Pal.H("c83a30")); c.Art(W - 5, 0, "..w..|.www.|wwwww");
                c.Art(4, base_ - 26, Icons.Art["shield"]);
            }
        }

        static void Spa(PixelCanvas c, int lv, BuildingSprite s)
        {
            int W = c.w, base_ = c.h - 2;
            float cx = W / 2f, cy = base_ - 9;
            c.Ellipse(cx, cy, W / 2f - 1, 8, lv == 1 ? Pal.H("6a4a30") : Wood);
            c.Ellipse(cx, cy, W / 2f - 3, 6, Pal.H("5a3e28"));
            c.SpeckleOn(0, 0, W, c.h, Pal.H("7a5a3a"), 0.15f);
            s.steam.Add(L(c, (int)cx - 4, (int)cy - 2)); s.steam.Add(L(c, (int)cx + 5, (int)cy));
            if (lv >= 2)
            {
                Sign(c, 1, base_ - 30, "spa", false);
                c.VLine(7, base_ - 19, base_ - 14, WoodDark);
            }
            if (lv == 3)
            {
                // palm tree + umbrella
                c.VLine(W - 4, base_ - 32, base_ - 10, Pal.H("8a6a3a"));
                c.Ellipse(W - 4, base_ - 33, 6, 2.5f, Pal.H("3a8a3a"));
                c.Ellipse(W - 14, base_ - 26, 6, 2.5f, Pal.H("e05a5a")); c.VLine(W - 14, base_ - 25, base_ - 14, Pal.H("d8d0c0"));
            }
        }

        static void Statue(PixelCanvas c, int lv, BuildingSprite s)
        {
            int W = c.w, base_ = c.h - 2;
            c.Rect(1, base_ - 10, W - 2, 10, Stone);
            c.Frame(1, base_ - 10, W - 2, 10, Pal.Shade(Stone, 0.7f));
            c.Rect(3, base_ - 13, W - 6, 3, Pal.Shade(Stone, 1.1f));
            var look = new SheepLook { woolStage = 4, expr = Expr.Normal };
            var sc = SheepArt.Build(look);
            Color32 tint = lv == 1 ? Pal.H("a8a4a0") : lv == 2 ? Pal.H("e8c040") : Pal.H("9098a0");
            int ox = W / 2 - 10, oy = base_ - 13 - 20;
            for (int y = 0; y < SheepArt.H; y++)
                for (int x = 0; x < SheepArt.W; x++)
                {
                    var p = sc.Get(x, y);
                    if (p.a == 0 || p.Equals(Pal.Ink)) continue;
                    float lum = (p.r + p.g + p.b) / 765f;
                    c.Set(ox + x, oy + y, Pal.Shade(tint, 0.55f + lum * 0.6f));
                }
            if (lv == 3) { c.Set(ox + 14, oy + 8, Pal.H("ff2020")); c.Set(ox + 17, oy + 8, Pal.H("ff2020")); s.sparks.Add(L(c, ox + 15, oy + 8)); }
            if (lv == 2) s.sparks.Add(L(c, ox + 6, oy + 6));
        }

        static void Graveyard(PixelCanvas c, int lv, BuildingSprite s)
        {
            int W = c.w, base_ = c.h - 2, top = Top + 4;
            c.Rect(1, top, W - 2, base_ - top, Pal.H("4a5a36"));
            c.SpeckleOn(1, top, W - 2, base_ - top, Pal.H("5a4a30"), 0.25f);
            Color32 f = lv >= 2 ? Pal.H("3a3a40") : WoodDark;
            for (int i = 0; i < W; i += 3) { c.VLine(i, top - 3, top, f); c.VLine(i, base_ - 3, base_, f); }
            c.HLine(0, W - 1, top - 2, f); c.HLine(0, W - 1, base_ - 2, f);
            for (int j = top; j < base_; j += 3) { c.Set(0, j, f); c.Set(W - 1, j, f); }
            // dead tree
            c.Line(W - 6, base_ - 4, W - 7, top - 18, Pal.H("4a3a2e"));
            c.Line(W - 7, top - 12, W - 12, top - 17, Pal.H("4a3a2e")); c.Line(W - 7, top - 10, W - 2, top - 15, Pal.H("4a3a2e"));
            if (lv == 3)
            {
                Bricks(c, 3, top - 16, 14, 16, Pal.H("8a8a90"));
                Roof(c, 1, top - 24, 18, 9, Pal.H("5a5a62"), 1);
                c.Rect(8, top - 9, 4, 9, Black);
                c.Set(5, base_ - 6, Pal.H("e888a8")); c.Set(W - 10, base_ - 5, Pal.H("f0c840"));
            }
        }

        static void RocketPad(PixelCanvas c, BuildingSprite s)
        {
            int W = c.w, base_ = c.h - 2;
            c.Rect(2, base_ - 6, W - 4, 6, Pal.H("5a5e64"));
            Stripes(c, 2, base_ - 7, W - 4, 2);
            // tower lattice
            int tx = W - 12;
            for (int j = base_ - 6; j > 4; j--) { c.Set(tx, j, Pal.H("a83228")); c.Set(tx + 6, j, Pal.H("a83228")); }
            for (int j = base_ - 6; j > 8; j -= 6) { c.Line(tx, j, tx + 6, j - 6, Pal.H("c84838")); c.HLine(tx, tx + 6, j, Pal.H("c84838")); }
            c.HLine(tx - 8, tx, 20, Pal.H("a83228"));
            Stains(c, 4, base_ - 4, W - 8, 3);
            s.steam.Add(L(c, W / 2 - 4, base_ - 8));
        }

        public static Sprite Rocket()
        {
            BuildingSprite bs;
            if (cache.TryGetValue("rocketship", out bs)) return bs.sprite;
            var c = new PixelCanvas(18, 48, 77);
            Color32 w = Pal.H("e8e4dc"), r = Pal.H("c83a30");
            for (int j = 0; j < 40; j++)
            {
                float half = j < 12 ? 1 + j * 0.55f : 7.5f;
                for (int i = 0; i < 18; i++) if (Mathf.Abs(i + 0.5f - 9) <= half) c.Set(i, j + 2, Pal.Shade(w, i < 9 ? 1f : 0.85f));
            }
            for (int j = 0; j < 6; j++) c.HLine(9 - (int)(1 + j * 0.55f), 9 + (int)(j * 0.55f), j + 2, r);
            // wool insulation patches
            c.Ellipse(5, 24, 2.5f, 2, Pal.H("f8f6f0")); c.Ellipse(12, 32, 2.5f, 2, Pal.H("f8f6f0"));
            // porthole with a sheep face
            c.Ellipse(9, 16, 3.5f, 3.5f, Pal.H("5a5e64")); c.Ellipse(9, 16, 2.5f, 2.5f, Pal.H("8ad8e8"));
            c.Rect(8, 15, 3, 3, Pal.H("2c2626")); c.Set(8, 15, Pal.H("ffffff")); c.Set(10, 15, Pal.H("ffffff"));
            c.Text2("BAA", 1, 34);
            // fins
            for (int j = 0; j < 8; j++) { c.HLine(0, 2 - j / 4, 41 - j + 4, r); c.HLine(15 + j / 4, 17, 41 - j + 4, r); }
            c.Rect(6, 42, 6, 4, Pal.H("4a4e56"));
            c.Outline(Pal.Ink);
            bs = new BuildingSprite { sprite = c.ToSprite(new Vector2(0.5f, 0f)) };
            cache["rocketship"] = bs;
            return bs.sprite;
        }

        static void Vat(PixelCanvas c, BuildingSprite s)
        {
            int W = c.w, base_ = c.h - 2;
            c.Rect(4, base_ - 6, W - 8, 6, MetalDark);
            c.Rect(6, base_ - 34, W - 12, 28, Pal.H("4a9a5a"));
            c.Frame(6, base_ - 34, W - 12, 28, Pal.H("c8ccd0"));
            for (int j = base_ - 33; j < base_ - 6; j++) c.Set(8, j, Pal.H("8ae8a0"));
            // floating sheep silhouette
            c.Ellipse(W / 2f, base_ - 20, 6, 4, Pal.H("2a6a3a")); c.Ellipse(W / 2f + 6, base_ - 22, 2.5f, 3, Pal.H("2a6a3a"));
            c.Rect(5, base_ - 37, W - 10, 3, Metal);
            Pipe(c, W - 4, base_ - 36, W - 4, base_ - 4);
            // monitor
            c.Rect(0, base_ - 14, 5, 5, Black); c.HLine(1, 3, base_ - 12, Pal.H("60f080"));
            s.steam.Add(L(c, W / 2, base_ - 37)); s.sparks.Add(L(c, W / 2 - 2, base_ - 26));
        }

        static void Flowers(PixelCanvas c)
        {
            int base_ = c.h - 2;
            string[] cols = { "e888a8", "f0c840", "e86060", "a070e0", "f2eee2" };
            for (int i = 0; i < 7; i++)
            {
                int x = 2 + c.rng.Next(12), y = base_ - 2 - c.rng.Next(10);
                c.VLine(x, y, y + 3, Pal.H("4a8a30"));
                var col = Pal.H(cols[c.rng.Next(cols.Length)]);
                c.Set(x, y - 1, col); c.Set(x - 1, y, col); c.Set(x + 1, y, col); c.Set(x, y, Pal.H("f0e060"));
            }
        }

        static void SafetySign(PixelCanvas c)
        {
            int base_ = c.h - 2;
            c.VLine(7, base_ - 14, base_, WoodDark); c.VLine(8, base_ - 14, base_, Wood);
            for (int j = 0; j < 9; j++) c.HLine(8 - j * 7 / 9 - 0, 7 + j * 7 / 9, base_ - 24 + j, Yellow);
            c.VLine(7, base_ - 21, base_ - 18, Black); c.Set(7, base_ - 17, Black);
            c.HLine(1, 14, base_ - 15, Black);
        }

        static void Tent(PixelCanvas c, BuildingSprite s)
        {
            int W = c.w, base_ = c.h - 2;
            for (int j = 0; j < 26; j++)
            {
                int half = 2 + j * (W / 2 - 3) / 26;
                for (int i = W / 2 - half; i <= W / 2 + half; i++) c.Set(i, base_ - 26 + j, Pal.Shade(Pal.H("d8d0b0"), ((i - W / 2) / 3) % 2 == 0 ? 1f : 0.88f));
            }
            for (int j = 0; j < 12; j++) c.HLine(W / 2 - j / 3, W / 2 + j / 3, base_ - 12 + j, Black);
            c.VLine(W / 2, base_ - 30, base_ - 26, WoodDark);
            c.Art(W / 2 + 4, base_ - 12, Icons.Art["research"]);
            c.Rect(2, base_ - 5, 6, 5, WoodDark); c.Set(4, base_ - 7, Pal.H("60e080")); c.Set(5, base_ - 6, Pal.H("60e080"));
            s.sparks.Add(L(c, 5, base_ - 7));
            Stains(c, 4, base_ - 2, 8, 1);
        }

        static void Plant(PixelCanvas c, int lv, BuildingSprite s)
        {
            int W = c.w, base_ = c.h - 2;
            int wallTop = base_ - 30;
            Corrugated(c, 10, wallTop, W - 20, 30, lv == 3 ? Pal.H("5a5a62") : Metal, 9);
            c.Rect(8, wallTop - 4, W - 16, 4, MetalDark);
            // intake hopper (left) and output chute (right)
            for (int j = 0; j < 12; j++) c.HLine(1 + j / 3, 12 - j / 3, base_ - 22 + j, Pal.Shade(Tin, 0.9f));
            c.Rect(0, base_ - 4, 12, 3, Black); for (int i = 0; i < 12; i += 2) c.Set(i, base_ - 4, Pal.H("5a5e64"));
            c.Rect(W - 12, base_ - 4, 12, 3, Black); for (int i = W - 12; i < W; i += 2) c.Set(i, base_ - 4, Pal.H("5a5e64"));
            c.Rect(W - 12, base_ - 14, 4, 10, Metal);
            // big maw
            c.Rect(W / 2 - 9, base_ - 16, 18, 14, Black);
            for (int i = W / 2 - 9; i < W / 2 + 9; i += 3) { c.Set(i, base_ - 16, Pal.H("c8ccd0")); c.Set(i + 1, base_ - 15, Pal.H("c8ccd0")); }
            Stripes(c, W / 2 - 11, base_ - 19, 22, 3);
            Stains(c, W / 2 - 10, base_ - 4, 20, 6 + lv * 2);
            // windows (red eyes at L3)
            Color32 eye = lv == 3 ? Pal.H("ff3020") : Lit;
            c.Rect(16, wallTop + 4, 6, 4, eye); c.Rect(W - 22, wallTop + 4, 6, 4, eye);
            // stacks
            int stacks = 1 + lv;
            for (int k = 0; k < stacks; k++)
            {
                int sx = 12 + k * ((W - 28) / Mathf.Max(1, stacks - 1 + 1)) + 4;
                s.smoke.Add(Chimney(c, sx, 1 + (k % 2) * 4, wallTop - 5 - (k % 2) * 4, Pal.H("8a8a8a"), true));
            }
            Gear(c, W - 16, wallTop + 14, 5, Tin);
            c.Art(14, wallTop + 12, Icons.Art["skull"]);
            c.Art(W / 2 - 4, wallTop + 4, Icons.Art["warning"]);
            Pipe(c, 9, wallTop, 9, base_ - 3);
            s.steam.Add(L(c, W - 10, base_ - 14)); s.steam.Add(L(c, 6, base_ - 22));
            if (lv >= 3) s.sparks.Add(L(c, W / 2, wallTop + 6));
            if (lv >= 4)
            {
                Text(c, W / 2 - 28, wallTop - 14, lv == 5 ? "SINGULARITY" : "THE MACHINE", lv == 5 ? C("50f0ff") : C("ff4fa0"), C("120e14"));
                if (lv == 5)
                {
                    c.EllipseBlend(W / 2f, wallTop - 24, 18, 8, Pal.A(C("60ff90"), 200));
                    c.Ellipse(W / 2f, wallTop - 24, 9, 4, C("c0ffd0"));
                    s.sparks.Add(L(c, W / 2, wallTop - 24));
                }
                s.sparks.Add(L(c, W / 2, wallTop - 10));
            }
        }

        // ---------- Level 4/5: neon industrial madness ----------
        static string MegaWord(BKind k, int lv)
        {
            switch (k)
            {
                case BKind.Farm: return lv == 5 ? "GRASS" : "FARM";
                case BKind.Shed: return lv == 5 ? "SNIP!" : "SNIP";
                case BKind.Market: return lv == 5 ? "$WOOL$" : "MALL";
                case BKind.Workshop: return "YARN";
                case BKind.Barn: return lv == 5 ? "LAMBS" : "LOVE";
                case BKind.Lab: return lv == 5 ? "IQ++" : "LAB";
                case BKind.Storage: return "$$$$";
                case BKind.Textile: return lv == 5 ? "SWEATER" : "TEXTILE";
                case BKind.Atelier: return "LUXE";
                case BKind.GuardPost: return "SAFE";
            }
            return "WOOL";
        }

        static void Mega(PixelCanvas c, BuildingDef d, int lv, BuildingSprite s)
        {
            int W = c.w, base_ = c.h - 2;
            int top = 16;
            Color32 wall = lv == 4 ? C("3e444c") : C("2c2636");
            Color32 seam = Pal.Shade(wall, 0.7f), hi = Pal.Shade(wall, 1.3f);
            Color32 glow = lv == 4 ? C("f0d070") : C("7af0a0");
            Color32 neon = lv == 4 ? C("ff4fa0") : C("50f0ff");
            int inset = W >= 48 ? 6 : 3;
            int step = top + (base_ - top) / 3;
            for (int y = top; y < base_; y++)
            {
                int ins = y < step ? inset : 1;
                for (int x = ins; x < W - ins; x++)
                {
                    Color32 col = wall;
                    if ((x - ins) % 8 == 0 || (y - top) % 10 == 0) col = seam;
                    if ((x - ins) % 8 == 1 && (y - top) % 10 == 1) col = hi;
                    c.Set(x, y, col);
                }
            }
            c.HLine(inset - 1, W - inset, top, C("8a9098"));
            c.HLine(0, W - 1, step, C("8a9098"));
            for (int y = top + 5; y < base_ - 14; y += 7)
                for (int x = (y < step ? inset : 1) + 3; x < W - 6; x += 6)
                {
                    bool on = c.rng.NextDouble() < 0.75;
                    c.Rect(x, y, 3, 4, on ? glow : C("141418"));
                    if (on && c.rng.NextDouble() < 0.25) { c.Set(x + 1, y + 1, C("f2eee2")); c.Set(x + 1, y + 2, C("2c2626")); }
                }
            string word = MegaWord(d.kind, lv);
            int tw = PixelFont.StampWidth(word);
            while (tw > W - 6 && word.Length > 2) { word = word.Substring(0, word.Length - 1); tw = PixelFont.StampWidth(word); }
            int sy = step - 12;
            c.Rect(W / 2 - tw / 2 - 2, sy - 2, tw + 4, 11, C("120e14"));
            PixelFont.Stamp(c, W / 2 - tw / 2, sy, word, neon);
            c.Frame(W / 2 - tw / 2 - 2, sy - 2, tw + 4, 11, Pal.Shade(neon, 0.6f));
            s.sparks.Add(L(c, W / 2, sy + 3));
            int stacks = W >= 48 ? 3 : 2;
            for (int k = 0; k < stacks; k++)
            {
                int sx = inset + 2 + k * ((W - inset * 2 - 6) / Mathf.Max(1, stacks - 1));
                s.smoke.Add(Chimney(c, Mathf.Min(sx, W - 5), 1 + (k % 2) * 3, top - 1 - (k % 2) * 3, C("8a8a8a"), true));
            }
            Pipe(c, 0, top + 4, 0, base_ - 2);
            Pipe(c, W - 2, top + 8, W - 2, base_ - 2);
            Pipe(c, 0, top + 4, W / 3, top + 4);
            int dw = Mathf.Min(W - 8, 14), dx = W / 2 - dw / 2;
            c.Rect(dx, base_ - 12, dw, 12, C("1a1a20"));
            for (int y = base_ - 12; y < base_; y += 2) c.HLine(dx, dx + dw - 1, y, C("2a2a32"));
            Stripes(c, dx - 1, base_ - 14, dw + 2, 2);
            Stains(c, 2, base_ - 3, W - 4, 4 + lv * 2);
            c.Art(2, base_ - 10, Icons.Art[Icons.ForBuilding(d.kind)]);
            c.Set(inset, top - 1, C("ff3020")); c.Set(W - inset - 1, top - 1, C("ff3020"));
            s.sparks.Add(L(c, inset, top - 1));
            if (W >= 48) Gear(c, W - 9, base_ - 22, 5, Tin);
            if (lv == 5)
            {
                c.EllipseBlend(W / 2f, top - 2, W / 2.6f, 6, Pal.A(C("60ff90"), 200));
                c.Ellipse(W / 2f, top - 2, W / 4f, 4, C("c0ffd0"));
                for (int k = 0; k < 3; k++) c.Line(W / 2 - 4 + k * 4, top - 8, W / 2 - 6 + k * 5, Mathf.Max(0, top - 14 - k), C("e0fff0"));
                int fy = step + 4, fx = W / 2;
                c.Ellipse(fx, fy + 6, Mathf.Min(10, W / 3f), 7, C("e8e2d4"));
                c.Ellipse(fx, fy + 7, Mathf.Min(6, W / 5f), 5, C("2c2626"));
                c.Rect(fx - 4, fy + 5, 2, 2, C("ff2020")); c.Rect(fx + 2, fy + 5, 2, 2, C("ff2020"));
                c.HLine(fx - 3, fx + 3, fy + 10, C("f0f0f0"));
                s.sparks.Add(L(c, fx, fy + 5)); s.sparks.Add(L(c, W / 2, top - 6));
            }
            s.steam.Add(L(c, 1, top + 4));
        }

        static void HouseMega(PixelCanvas c, int lv, BuildingSprite s)
        {
            int W = c.w, base_ = c.h - 2;
            int top = lv == 4 ? 14 : 24;
            Bricks(c, 3, top, W - 6, base_ - top, lv == 4 ? C("7a4a3a") : C("5a5a6a"));
            for (int y = top + 3; y < base_ - 10; y += 8)
                for (int x = 5; x < W - 7; x += 7)
                {
                    Window(c, x, y, 6, 6, c.rng.NextDouble() < 0.7, false);
                    if (c.rng.NextDouble() < 0.45) c.Art(x + 1, y + 1, "kwwk|wkwk");
                }
            Door(c, W / 2 - 4, base_ - 9, 8, 9, MetalDark);
            if (lv == 4)
            {
                Text(c, W / 2 - 8, top - 11, "BAA", C("ff4fa0"), C("120e14"));
                c.VLine(W / 2, 0, top - 14, MetalDark); c.Set(W / 2, 0, C("ff3020"));
                s.sparks.Add(L(c, W / 2, 0));
            }
            else
            {
                Color32 wl = C("f2eee2");
                c.Ellipse(W / 2f, top - 8, W / 2f - 1, 8, wl);
                for (int i = 0; i < 5; i++) c.Ellipse(4 + i * (W - 8) / 4f, top - 12 + (i % 2) * 3, 4, 3.5f, wl);
                c.SpeckleOn(0, 0, W, top, C("d8d2c4"), 0.2f);
                c.Rect(W / 2 - 3, top - 10, 6, 4, C("2c2626")); c.Set(W / 2 - 2, top - 9, C("ffffff")); c.Set(W / 2 + 1, top - 9, C("ffffff"));
                Text(c, W / 2 - 8, top + 2, "BAA", C("50f0ff"), C("120e14"));
            }
            s.steam.Add(L(c, W - 4, base_ - 12));
        }

        static void WheelMega(PixelCanvas c, int lv, BuildingSprite s)
        {
            int W = c.w, base_ = c.h - 2;
            int n = lv == 4 ? 2 : 3;
            float r = W / 2f - 2;
            for (int i = 0; i < n; i++)
            {
                float cy = base_ - r - 1 - i * (r * 1.5f);
                c.Line(W / 2, (int)cy, 2, (int)(cy + r + 1), MetalDark); c.Line(W / 2, (int)cy, W - 3, (int)(cy + r + 1), MetalDark);
                c.Ring(W / 2f, cy, r, 2.2f, i % 2 == 0 ? Metal : C("8a3e2e"));
                for (int a = 0; a < 8; a++) { float ang = a * Mathf.PI / 4 + i; c.Line(W / 2, (int)cy, (int)(W / 2f + Mathf.Cos(ang) * (r - 2)), (int)(cy + Mathf.Sin(ang) * (r - 2)), Pal.Shade(Metal, 0.75f)); }
                c.Ellipse(W / 2f, cy, 2, 2, C("ffe070"));
                s.sparks.Add(L(c, W / 2, (int)cy));
            }
            Stripes(c, 1, base_ - 2, W - 2, 2);
            Text(c, W / 2 - 11, 2, "WATT", C("ffe070"), C("120e14"));
            Stains(c, 3, base_ - 3, W - 6, 4);
        }

        static void TroughPreview(PixelCanvas c)
        {
            int base_ = c.h - 2;
            c.Rect(1, base_ - 8, 30, 8, Wood);
            c.HLine(1, 30, base_ - 8, C("9a7448"));
            for (int x = 4; x < 30; x += 6) c.VLine(x, base_ - 7, base_ - 1, WoodDark);
            c.Rect(3, base_ - 7, 26, 3, C("c8a050"));
            c.SpeckleOn(3, base_ - 9, 26, 5, C("6a9a3a"), 0.35f);
        }

        /// Conveyor machine: a riveted box with a rubber-flapped mouth on each side and the belt running through.
        static void Machine(PixelCanvas c, BuildingDef d, int lv, BuildingSprite s)
        {
            int W = c.w, base_ = c.h - 2;
            bool dairy = d.kind == BKind.Dairy, chop = d.kind == BKind.Slaughter, gene = d.kind == BKind.GeneLab;
            Color32 body = dairy ? C("dfe2e2") : chop ? C("6a3a30") : gene ? C("cfe0e4") : C("7a8290"), bodyLo = Pal.Shade(body, 0.72f), trim = dairy ? C("3a64a8") : chop ? C("a01e1e") : gene ? C("2fae98") : C("c8a030");
            int bodyH = 19 + lv * 3;
            int top = base_ - bodyH;
            // the belt slot along the bottom
            c.Rect(0, base_ - 3, W, 3, C("2a2a2e"));
            for (int x = 1; x < W; x += 4) c.Set(x, base_ - 2, C("54545a"));
            // body
            c.Rect(2, top, W - 4, bodyH - 3, body);
            c.HLine(2, W - 3, top, Pal.Shade(body, 1.15f));
            c.VLine(2, top, base_ - 4, bodyLo); c.VLine(W - 3, top, base_ - 4, bodyLo);
            for (int x = 5; x < W - 4; x += 5) { c.Set(x, top + 2, bodyLo); c.Set(x, base_ - 15, bodyLo); }
            c.HLine(3, W - 4, base_ - 14, trim);
            // in / out mouths with rubber flaps (the slaughterhouse only has a way in)
            for (int k = 0; k < (chop ? 1 : 2); k++)
            {
                int mx = k == 0 ? 2 : W - 8;
                c.Rect(mx, base_ - 11, 6, 8, C("141218"));
                for (int i = 0; i < 6; i += 2) c.VLine(mx + i, base_ - 10, base_ - 5, C("3e3e46"));
                Stripes(c, mx, base_ - 13, 6, 2);
            }
            // big sign on the front
            c.Rect(11, top + 3, 10, 10, C("ece4d4")); c.Frame(11, top + 3, 10, 10, Black);
            if (gene) Helix(c, 12, top + 4); else c.Art(12, top + 4, Icons.Art[dairy ? "milk" : chop ? "mutton" : "shears"]);
            if (chop)
            {
                // a smoking chimney, a cleaver sign and some honest stains
                s.smoke.Add(Chimney(c, 4, Mathf.Max(1, top - 10), 10, C("5a5250"), false));
                c.Rect(W - 8, top + 4, 4, 2, C("c8ccd0")); c.Rect(W - 7, top + 6, 2, 3, C("5a3e28"));
                Stains(c, 3, base_ - 3, W - 6, 3);
            }
            else if (gene)
            {
                // a glass tank of green goo with a sheep floating in it
                int tw = 10 + lv * 2, th = 8 + lv * 2;
                int tx = W / 2 - tw / 2;
                c.Rect(tx, top - th, tw, th, C("bfe8ee"));
                c.Rect(tx + 1, top - th + 3, tw - 2, th - 3, C("62d870"));
                c.HLine(tx, tx + tw - 1, top - th, C("ffffff"));
                c.VLine(tx, top - th, top - 1, C("8ab8c0")); c.VLine(tx + tw - 1, top - th, top - 1, C("8ab8c0"));
                c.Rect(tx + tw / 2 - 2, top - th / 2 - 1, 4, 3, C("f2eee2"));
                c.Set(tx + tw / 2 + 2, top - th / 2, C("2c2626"));
                c.Set(tx + 2, top - th + 5, C("d8ffd0")); c.Set(tx + tw - 3, top - 4, C("d8ffd0"));
                Pipe(c, 3, top - 4, tx, top - 4);
                s.steam.Add(L(c, tx + tw / 2, top - th - 1));
                s.sparks.Add(L(c, tx + 2, top - th));
                if (lv >= 3) { c.Rect(W - 8, top - 6, 4, 6, C("2fae98")); c.Set(W - 7, top - 5, C("9cf070")); }
            }
            else if (dairy)
            {
                // milk tank on the roof
                int tw = 10 + lv * 2, th = 7 + lv;
                int tx = W / 2 - tw / 2;
                c.Rect(tx, top - th, tw, th, C("f4f4f0"));
                c.HLine(tx, tx + tw - 1, top - th, C("ffffff"));
                c.HLine(tx, tx + tw - 1, top - th / 2, trim);
                c.VLine(tx, top - th, top - 1, C("c8ccd0"));
                Pipe(c, tx + tw, top - th / 2, W - 4, top - th / 2);
                if (lv >= 2) { Pipe(c, 3, top - 4, 3, top + 6); s.steam.Add(L(c, 3, top - 5)); }
                if (lv >= 3) { c.Art(2, base_ - 24, Icons.Art["milk"]); Stains(c, 4, base_ - 3, W - 8, 2); }
            }
            else
            {
                // warning light and spinning blades
                c.Rect(W / 2 - 1, top - 3, 2, 3, MetalDark);
                c.Rect(W / 2 - 2, top - 5, 4, 2, C("e83a30"));
                s.sparks.Add(L(c, W / 2, top - 5));
                Gear(c, 6, top + 7, 2.6f, Tin);
                if (lv >= 2) Gear(c, W - 7, top + 7, 2.6f, Tin);
                if (lv >= 3)
                {
                    int sx = W / 2, sy = Mathf.Max(2, top - 16);
                    c.Line(sx - 8, sy, sx + 2, sy + 10, Pal.H("c8ccd0")); c.Line(sx - 7, sy, sx + 3, sy + 10, Pal.H("8a9098"));
                    c.Line(sx + 8, sy, sx - 2, sy + 10, Pal.H("c8ccd0")); c.Line(sx + 7, sy, sx - 3, sy + 10, Pal.H("8a9098"));
                    c.Ring(sx - 4, sy + 12, 2.5f, 1.2f, Pal.H("c83a30")); c.Ring(sx + 4, sy + 12, 2.5f, 1.2f, Pal.H("c83a30"));
                    Stains(c, W / 2 - 6, base_ - 4, 12, 4);
                }
                WoolPile(c, W - 11, base_ - 19);
            }
            if (lv >= 2) s.smoke.Add(Chimney(c, W - 7, top - 7, 7, Stone, lv == 3));
        }

        /// a little DNA double helix for the Gene Lab sign (8x8)
        static void Helix(PixelCanvas c, int x, int y)
        {
            for (int i = 0; i < 8; i++)
            {
                int a = Mathf.RoundToInt(3.5f + 3f * Mathf.Sin(i * 0.8f)), b = Mathf.RoundToInt(3.5f - 3f * Mathf.Sin(i * 0.8f));
                if (i % 2 == 0) for (int k = Mathf.Min(a, b) + 1; k < Mathf.Max(a, b); k++) c.Set(x + k, y + i, C("9aa4a8"));
                c.Set(x + a, y + i, C("3a8aff"));
                c.Set(x + b, y + i, C("3cc860"));
            }
        }

        /// ghost / menu picture of a room: fence all round, a red door in the middle of every side, troughs in a feeding lot
        static void PenPreview(PixelCanvas c, BuildingDef d)
        {
            int W = c.w, base_ = c.h - 2, topY = base_ - d.h * 16;
            Color32 w = Pal.H("8a6440"), dk = Pal.H("5a3e28"), red = Pal.H("b0402c");
            int gx0 = (d.w / 2) * 16, gx1 = gx0 + 15;
            if (d.kind == BKind.FeedLot)
                for (int k = 0; k + 1 < d.w; k += 2)
                {
                    int tx = k * 16 + 2, ty = topY + 6;
                    c.Rect(tx, ty, 28, 8, Pal.H("7a5638")); c.Rect(tx + 2, ty + 1, 24, 3, Pal.H("c8a050"));
                }
            for (int pass = 0; pass < 2; pass++)
            {
                int y = pass == 0 ? topY : base_;
                for (int x = 0; x < W; x++)
                {
                    bool gate = x > gx0 && x < gx1;
                    Color32 col = gate ? red : w;
                    c.Set(x, y - 8, col); c.Set(x, y - 7, Pal.Shade(col, 0.8f));
                    c.Set(x, y - 4, col); c.Set(x, y - 3, Pal.Shade(col, 0.8f));
                    if (gate) for (int j = y - 8; j <= y - 3; j++) if ((x - gx0) % 3 == 0) c.Set(x, j, Pal.Shade(red, 0.75f));
                }
                for (int x = 0; x <= W; x += 16) c.Rect(Mathf.Min(x, W - 3), y - 11, 3, 11, dk);
                c.Rect(gx1, y - 11, 3, 11, dk);
            }
            for (int y = topY; y <= base_; y += 16) { c.Rect(0, y - 11, 3, 11, dk); c.Rect(W - 3, y - 11, 3, 11, dk); }
            int sideY = base_ - (d.h / 2) * 16 - 14;
            c.Rect(0, sideY, 4, 13, red); c.Rect(W - 4, sideY, 4, 13, red);
        }

        static void Funnel(PixelCanvas c, int lv, BuildingSprite s)
        {
            int W = c.w, base_ = c.h - 2;
            for (int j = 0; j < 16; j++)
            {
                int half = 14 - j * 9 / 16;
                for (int i = W / 2 - half; i <= W / 2 + half; i++) c.Set(i, base_ - 30 + j, Pal.Shade(Tin, (i % 3 == 0) ? 0.85f : 1f));
            }
            c.HLine(W / 2 - 14, W / 2 + 14, base_ - 30, MetalDark);
            c.Rect(W / 2 - 5, base_ - 14, 10, 12, Metal);
            Stripes(c, W / 2 - 5, base_ - 4, 10, 2);
            c.Art(W / 2 - 4, base_ - 27, Icons.Art["sheep"]);
            c.VLine(4, base_ - 16, base_, MetalDark); c.VLine(W - 5, base_ - 16, base_, MetalDark);
            Text(c, 4, Mathf.Max(2, base_ - 42), lv == 3 ? "FIRE" : "IN", C("ffe070"), C("8a2a20"));
            if (lv >= 2) { Pipe(c, 1, base_ - 22, 1, base_ - 2); Gear(c, W - 6, base_ - 10, 3, Tin); }
            if (lv == 3)
            {
                c.Line(W / 2, base_ - 30, W - 2, Mathf.Max(1, base_ - 44), MetalDark); c.Line(W / 2 + 1, base_ - 30, W - 1, Mathf.Max(1, base_ - 44), MetalDark);
                s.smoke.Add(L(c, W - 2, Mathf.Max(1, base_ - 44)));
            }
            Stains(c, 4, base_ - 2, W - 8, 2);
        }

        // ---------- conveyor belts & items ----------
        /// conveyor tile; tier 0 rubber, 1 steel (red, faster), 2 mega (gold, wider, fastest)
        public static Sprite Belt(int dir, int frame, int tier)
        {
            tier = Mathf.Clamp(tier, 0, 2);
            string key = "belt" + dir + "_" + frame + "_" + tier;
            BuildingSprite bs;
            if (cache.TryGetValue(key, out bs)) return bs.sprite;
            var c = new PixelCanvas(16, 16, 9);
            Color32 rail = tier == 2 ? C("d8a830") : tier == 1 ? C("b04a3a") : C("6a6e74"), railD = Pal.Shade(rail, 0.6f);
            Color32 rub = tier == 2 ? C("24242a") : C("2a2a2e"), tread = tier == 2 ? C("5a5a62") : tier == 1 ? C("52484a") : C("48484e");
            Color32 arrow = tier == 2 ? C("ffe070") : tier == 1 ? C("ff9a7a") : C("8a8a90"), rivet = tier == 2 ? C("fff3b0") : C("a0a4aa");
            int r0 = tier == 2 ? 1 : 2, r1 = tier == 2 ? 14 : 13;
            int f = (frame * (tier + 1)) % 4;
            for (int x = 0; x < 16; x++)
            {
                c.Set(x, r0, railD); c.Set(x, r0 + 1, rail); c.Set(x, r1 - 1, rail); c.Set(x, r1, railD);
                for (int y = r0 + 2; y < r1 - 1; y++) c.Set(x, y, ((x - f + 32) % 4 == 0) ? tread : rub);
                if (x % 5 == 0) { c.Set(x, r0 + 1, rivet); c.Set(x, r1 - 1, rivet); }
            }
            int[] chevrons = tier == 0 ? new[] { 6 } : new[] { 3, 9 };
            foreach (int ax in chevrons)
            {
                c.Set(ax, 6, arrow); c.Set(ax + 1, 7, arrow); c.Set(ax + 2, 8, arrow); c.Set(ax + 1, 9, arrow); c.Set(ax, 10, arrow);
            }
            var src = (Color32[])c.px.Clone();
            for (int r = 0; r < dir; r++)
            {
                // rotate 90 degrees counter-clockwise in texture space (y up)
                var dst = new Color32[256];
                for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++) dst[x * 16 + (15 - y)] = src[y * 16 + x];
                src = dst;
            }
            System.Array.Copy(src, c.px, 256);
            bs = new BuildingSprite { sprite = c.ToSprite(Vector2.zero) };
            cache[key] = bs;
            return bs.sprite;
        }

        public static Sprite Item(Res r)
        {
            string key = "item" + r;
            BuildingSprite bs;
            if (cache.TryGetValue(key, out bs)) return bs.sprite;
            var art = Icons.Art[ResInfo.Icon[(int)r]];
            var rows = art.Split('|');
            int w = 0; foreach (var row in rows) w = Mathf.Max(w, row.Length);
            var c = new PixelCanvas(w + 2, rows.Length + 2, 3);
            c.Art(1, 1, art);
            c.Outline(Pal.Ink);
            bs = new BuildingSprite { sprite = c.ToSprite(new Vector2(0.5f, 0.35f)) };
            cache[key] = bs;
            return bs.sprite;
        }

        // ---------- scaffold ----------
        public static Sprite Scaffold(int w, int h)
        {
            string key = "scaffold_" + w + "_" + h;
            BuildingSprite bs;
            if (cache.TryGetValue(key, out bs)) return bs.sprite;
            var c = new PixelCanvas(w * 16, h * 16 + 10, 5);
            int W = c.w, base_ = c.h - 1;
            Color32 pole = Pal.H("9a7448");
            for (int i = 1; i < W; i += 10) c.VLine(i, 2, base_, pole);
            for (int j = base_ - 2; j > 2; j -= 8) { c.HLine(0, W - 1, j, pole); c.HLine(0, W - 1, j + 1, Pal.Shade(pole, 0.7f)); }
            for (int i = 1; i + 10 < W; i += 10) c.Line(i, base_ - 2, i + 10, Mathf.Max(2, base_ - 18), Pal.Shade(pole, 0.8f));
            c.Art(W / 2 - 4, base_ - 12, Icons.Art["hammer"]);
            Stripes(c, 2, base_ - 1, W - 4, 1);
            c.Outline(Pal.Ink);
            bs = new BuildingSprite { sprite = c.ToSprite(Vector2.zero) };
            cache[key] = bs;
            return bs.sprite;
        }
    }

    public static class CanvasExt
    {
        public static void Text2(this PixelCanvas c, string t, int x, int y)
        {
            PixelFont.Stamp(c, x, y, t, Pal.H("c83a30"));
        }
    }
}
