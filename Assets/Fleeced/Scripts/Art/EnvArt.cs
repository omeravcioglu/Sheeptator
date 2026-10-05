using System.Collections.Generic;
using UnityEngine;

namespace Fleeced
{
    public static class EnvArt
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        static Sprite Cached(string key, System.Func<Sprite> make)
        {
            Sprite s;
            if (!cache.TryGetValue(key, out s)) { s = make(); cache[key] = s; }
            return s;
        }
        static Vector2 BottomCenter = new Vector2(0.5f, 0f);

        public static Sprite Pixel()
        {
            return Cached("pixel", () =>
            {
                var c = new PixelCanvas(2, 2);
                c.Rect(0, 0, 2, 2, new Color32(255, 255, 255, 255));
                return c.ToSprite(new Vector2(0.5f, 0.5f), 16);
            });
        }

        public static Sprite Shadow()
        {
            return Cached("shadow", () =>
            {
                var c = new PixelCanvas(14, 5);
                c.Ellipse(7, 2.5f, 7, 2.5f, new Color32(255, 255, 255, 255));
                return c.ToSprite(new Vector2(0.5f, 0.5f), 16);
            });
        }

        public static Sprite Tree(int v)
        {
            return Cached("tree" + v, () =>
            {
                var c = new PixelCanvas(20, 28, 100 + v);
                Color32 trunk = Pal.H("5a3e28");
                c.Rect(9, 18, 3, 10, trunk); c.Set(8, 27, trunk); c.Set(12, 27, trunk);
                if (v == 3)
                {
                    // dead tree
                    c.Line(10, 18, 4, 6, trunk); c.Line(10, 16, 16, 4, trunk); c.Line(10, 18, 10, 2, trunk); c.Line(6, 9, 3, 8, trunk);
                }
                else
                {
                    Color32 g = v == 1 ? Pal.H("3a6a30") : Pal.H("2e5a2a"), hi = Pal.H("5a8a3c");
                    c.Ellipse(10, 11, 8.5f, 8, g); c.Ellipse(6, 14, 5, 4, g); c.Ellipse(14, 14, 5, 4, g);
                    c.Ellipse(8, 8, 3, 2.5f, hi); c.Ellipse(13, 11, 2, 2, hi);
                    c.SpeckleOn(0, 0, 20, 20, Pal.Shade(g, 0.75f), 0.15f);
                    if (v == 2) { c.Set(6, 10, Pal.H("c83a30")); c.Set(13, 7, Pal.H("c83a30")); c.Set(11, 15, Pal.H("c83a30")); }
                }
                c.Outline(Pal.Ink);
                return c.ToSprite(BottomCenter);
            });
        }

        public static Sprite Rock(int v)
        {
            return Cached("rock" + v, () =>
            {
                var c = new PixelCanvas(12, 9, 200 + v);
                c.Ellipse(6, 5, 5, 3.6f, Pal.H("7a7672")); c.Ellipse(5, 4, 2.5f, 1.6f, Pal.H("9a9692"));
                c.SpeckleOn(0, 0, 12, 9, Pal.H("5a5652"), 0.2f);
                if (v == 1) c.SpeckleOn(0, 0, 12, 4, Pal.H("4a7a3a"), 0.4f);
                c.Outline(Pal.Ink);
                return c.ToSprite(BottomCenter);
            });
        }

        public static Sprite Bush()
        {
            return Cached("bush", () =>
            {
                var c = new PixelCanvas(14, 10, 300);
                c.Ellipse(7, 6, 6, 4, Pal.H("3e6e32")); c.Ellipse(5, 5, 2, 1.5f, Pal.H("5a8a3c"));
                c.Outline(Pal.Ink);
                return c.ToSprite(BottomCenter);
            });
        }

        /// horizontal fence segment (16 px) with post on the left
        public static Sprite FenceH(bool broken)
        {
            return Cached("fenceh" + broken, () =>
            {
                var c = new PixelCanvas(17, 12, broken ? 401 : 400);
                Color32 w = Pal.H("8a6440"), d = Pal.H("5a3e28");
                c.Rect(0, 1, 3, 11, d); c.Set(1, 2, w);
                if (broken) { c.Line(3, 4, 15, 9, w); c.HLine(3, 8, 8, w); }
                else { c.HLine(3, 16, 4, w); c.HLine(3, 16, 5, Pal.Shade(w, 0.8f)); c.HLine(3, 16, 8, w); c.HLine(3, 16, 9, Pal.Shade(w, 0.8f)); }
                c.Outline(Pal.Ink);
                return c.ToSprite(new Vector2(1.5f / 17f, 0f));
            });
        }
        // room fence styles by level: wood, painted, steel, blue steel with hazard paint, gold
        static readonly string[][] FencePal =
        {
            new[] { "8a6440", "5a3e28", "a07a50" },
            new[] { "ece4d4", "b0a490", "ffffff" },
            new[] { "9aa0a8", "5a6068", "c8ccd0" },
            new[] { "3a64a8", "24385e", "e8c030" },
            new[] { "e0b030", "8a6a20", "fff3b0" },
        };

        public static Sprite RoomFenceH(int style)
        {
            style = Mathf.Clamp(style, 0, 4);
            return Cached("rfh" + style, () =>
            {
                var c = new PixelCanvas(17, 12, 410 + style);
                var pl = FencePal[style];
                Color32 w = Pal.H(pl[0]), d = Pal.H(pl[1]), acc = Pal.H(pl[2]);
                c.Rect(0, 1, 3, 11, d); c.Set(1, 2, acc);
                c.HLine(3, 16, 4, w); c.HLine(3, 16, 5, Pal.Shade(w, 0.8f)); c.HLine(3, 16, 8, w); c.HLine(3, 16, 9, Pal.Shade(w, 0.8f));
                if (style == 3) for (int x = 4; x < 16; x += 4) { c.Set(x, 4, acc); c.Set(x + 1, 4, acc); c.Set(x + 2, 8, acc); c.Set(x + 3, 8, acc); }
                if (style == 4) { c.Set(1, 0, acc); c.Set(9, 4, acc); }
                c.Outline(Pal.Ink);
                return c.ToSprite(new Vector2(1.5f / 17f, 0f));
            });
        }

        public static Sprite RoomPost(int style)
        {
            style = Mathf.Clamp(style, 0, 4);
            return Cached("rpost" + style, () =>
            {
                var c = new PixelCanvas(5, 13, 420 + style);
                var pl = FencePal[style];
                c.Rect(1, 1, 3, 12, Pal.H(pl[1])); c.Set(2, 2, Pal.H(pl[2])); c.HLine(1, 3, 6, Pal.H(pl[0]));
                if (style == 4) c.Set(2, 0, Pal.H(pl[2]));
                c.Outline(Pal.Ink);
                return c.ToSprite(new Vector2(0.5f, 0f));
            });
        }

        /// the Gene Lab's tank of goo with bubbles rising (3 frames) and a sheep floating in it
        public static Sprite Goo(Color32 col, int frame, int w, int h, bool bright)
        {
            return Cached("goo" + col.r + "_" + col.g + "_" + col.b + "_" + frame + "_" + w + "_" + h + (bright ? "b" : ""), () =>
            {
                var white = new Color32(255, 255, 255, 255);
                var c = new PixelCanvas(w, h, 500 + frame);
                Color32 g = bright ? Pal.Lerp(col, white, 0.4f) : col;
                c.Rect(0, 0, w, h, g);
                c.HLine(0, w - 1, 0, Pal.Lerp(g, white, 0.45f));
                for (int i = 0; i < 3; i++)
                {
                    int bx = (i * 4 + 1 + frame) % Mathf.Max(1, w);
                    int by = h - 1 - ((frame * 2 + i * 3) % Mathf.Max(1, h - 1));
                    c.Set(bx, by, Pal.Lerp(g, white, 0.75f));
                }
                int sx = w / 2 - 2, sy = h / 2 - 1;
                c.Rect(sx, sy, 4, 3, Pal.H("f2eee2"));
                c.Set(sx + 4, sy + 1, Pal.H("2c2626"));
                c.Set(sx, sy + 3, Pal.H("2c2626")); c.Set(sx + 3, sy + 3, Pal.H("2c2626"));
                return c.ToSprite(new Vector2(0, 0));
            });
        }

        /// a roll-up shutter on a conveyor machine with a lamp on top: green = open, red = shut (with a slot at the bottom for goods).
        /// wide = front/back door (12x10), else a side door (9x12). Pivot bottom-left.
        static readonly Sprite[] machineDoors = new Sprite[4];
        public static Sprite MachineDoor(bool wide, bool open)
        {
            int idx = (wide ? 2 : 0) + (open ? 1 : 0);
            if (machineDoors[idx] != null) return machineDoors[idx];
            return machineDoors[idx] = Cached("mdoor" + wide + open, () =>
            {
                int w = wide ? 12 : 9, h = wide ? 10 : 12;
                var c = new PixelCanvas(w, h, open ? 412 : 411);
                Color32 frame = Pal.H("3e3e46"), frameHi = Pal.H("6a6e78"), red = Pal.H("b0402c"), redLo = Pal.H("7e2a1e"), redHi = Pal.H("d0603c"),
                    dark = Pal.H("141218"), lamp = open ? Pal.H("5ae050") : Pal.H("ff4a3a"), lampHi = open ? Pal.H("b8ffb0") : Pal.H("ffb0a0");
                int fx0 = 1, fx1 = w - 2, fy0 = 3, fy1 = h - 2;
                int lx = w / 2 - 1;
                c.Rect(lx, 0, 2, 2, lamp); c.Set(lx, 0, lampHi);
                c.Rect(fx0, fy0, fx1 - fx0 + 1, fy1 - fy0 + 1, frame);
                c.HLine(fx0, fx1, fy0, frameHi);
                int ix0 = fx0 + 1, ix1 = fx1 - 1, iy0 = fy0 + 1, iy1 = fy1;
                if (open)
                {
                    c.Rect(ix0, iy0, ix1 - ix0 + 1, iy1 - iy0 + 1, dark);
                    c.HLine(ix0, ix1, iy0, redHi); c.HLine(ix0, ix1, iy0 + 1, redLo); // rolled up out of the way
                }
                else
                {
                    for (int y = iy0; y <= iy1; y++) c.HLine(ix0, ix1, y, (y - iy0) % 2 == 0 ? red : redLo);
                    c.HLine(ix0, ix1, iy0, redHi);
                    c.HLine(ix0, ix1, iy1, dark); // the goods slot
                }
                c.Outline(Pal.Ink);
                return c.ToSprite(new Vector2(0, 0));
            });
        }

        /// a door in a horizontal fence (16 px wide, post on the left like FenceH). Always red so it reads as a DOOR.
        public static Sprite Gate(bool open, int style = 0)
        {
            style = Mathf.Clamp(style, 0, 4);
            return Cached("gate" + open + style, () =>
            {
                var c = new PixelCanvas(17, 13, open ? 404 : 403);
                Color32 post = Pal.H(FencePal[style][1]), red = Pal.H("b0402c"), redLo = Pal.H("7e2a1e"), brace = Pal.H("ecdcb4");
                c.Rect(0, 1, 3, 12, post); c.Set(1, 2, Pal.H(FencePal[style][2]));
                if (open)
                {
                    // swung open towards the viewer: a narrow slanted panel by the hinge
                    for (int j = 0; j < 9; j++) { c.Set(3, 3 + j, red); c.Set(4, 2 + j, red); c.Set(5, 1 + j, redLo); }
                    c.Set(4, 6, brace); c.Set(3, 7, brace);
                }
                else
                {
                    c.Rect(3, 3, 13, 8, red);
                    for (int x = 5; x < 16; x += 3) c.VLine(x, 3, 10, redLo);
                    c.HLine(3, 15, 3, Pal.H("c8583c"));
                    c.Line(3, 4, 15, 9, brace); c.Line(3, 9, 15, 4, brace);
                    c.Set(15, 6, Pal.H("e8c030")); c.Set(15, 7, Pal.H("e8c030")); // latch
                }
                c.Outline(Pal.Ink);
                return c.ToSprite(new Vector2(1.5f / 17f, 0f));
            });
        }

        /// a door in a side fence (seen edge-on): a red panel between two posts, or swung open
        public static Sprite GateSide(bool open, int style = 0)
        {
            style = Mathf.Clamp(style, 0, 4);
            return Cached("gateside" + open + style, () =>
            {
                var c = new PixelCanvas(9, 19, open ? 406 : 405);
                Color32 post = Pal.H(FencePal[style][1]), red = Pal.H("b0402c"), redLo = Pal.H("7e2a1e"), brace = Pal.H("ecdcb4");
                if (open)
                {
                    // the panel hangs open, out to the side
                    for (int j = 0; j < 10; j++) { c.Set(6 + j / 4, 5 + j, red); c.Set(7 + j / 4, 5 + j, redLo); }
                    c.Set(6, 9, brace);
                }
                else
                {
                    c.Rect(2, 4, 5, 13, red);
                    c.VLine(4, 4, 16, redLo);
                    c.Line(2, 5, 6, 15, brace);
                    c.Set(6, 10, Pal.H("e8c030"));
                }
                c.Rect(0, 3, 2, 16, post); // hinge post
                c.Outline(Pal.Ink);
                return c.ToSprite(new Vector2(0.5f, 0f));
            });
        }

        public static Sprite FencePost()
        {
            return Cached("fencepost", () =>
            {
                var c = new PixelCanvas(5, 13, 402);
                c.Rect(1, 1, 3, 12, Pal.H("5a3e28")); c.Set(2, 2, Pal.H("8a6440")); c.HLine(1, 3, 6, Pal.H("8a6440"));
                c.Outline(Pal.Ink);
                return c.ToSprite(BottomCenter);
            });
        }

        public static Sprite Tomb(int v)
        {
            return Cached("tomb" + v, () =>
            {
                var c = new PixelCanvas(10, 13, 500 + v);
                Color32 st = Pal.H("8a8a90");
                if (v % 3 == 0) { c.Rect(2, 3, 6, 10, st); c.Ellipse(5, 4, 3, 2.5f, st); c.HLine(3, 6, 6, Pal.Shade(st, 0.6f)); c.VLine(4, 5, 7, Pal.Shade(st, 0.6f)); }
                else if (v % 3 == 1) { c.Rect(4, 1, 2, 12, Pal.H("6a5040")); c.Rect(1, 4, 8, 2, Pal.H("6a5040")); }
                else { c.Rect(2, 5, 6, 8, st); c.Rect(1, 4, 8, 2, st); c.SpeckleOn(0, 0, 10, 13, Pal.H("4a7a3a"), 0.2f); }
                c.Rect(1, 12, 8, 1, Pal.H("5a4030"));
                c.Outline(Pal.Ink);
                return c.ToSprite(BottomCenter);
            });
        }

        public static Sprite Decal(string kind, int v)
        {
            return Cached("decal_" + kind + v, () =>
            {
                var c = new PixelCanvas(12, 8, kind.GetHashCode() + v * 31);
                switch (kind)
                {
                    case "blood":
                        {
                            Color32 b = Pal.A(Pal.H("8a1414"), 220);
                            c.Ellipse(6, 4, 3 + v % 2, 2, b);
                            for (int i = 0; i < 5; i++) c.Set(c.rng.Next(12), c.rng.Next(8), b);
                            c.Ellipse(9, 5, 1.2f, 1, b);
                            break;
                        }
                    case "wool":
                        {
                            Color32 w = Pal.H("eee8dc");
                            for (int i = 0; i < 4; i++) { int x = 2 + c.rng.Next(8), y = 2 + c.rng.Next(4); c.Set(x, y, w); c.Set(x + 1, y, w); c.Set(x, y - 1, w); }
                            break;
                        }
                    case "bones":
                        {
                            Color32 w = Pal.H("e8e0cc");
                            c.HLine(2, 8, 4, w); c.Set(1, 3, w); c.Set(1, 5, w); c.Set(9, 3, w); c.Set(9, 5, w);
                            c.Ellipse(9, 2, 1.6f, 1.4f, w); c.Set(9, 2, Pal.H("201818"));
                            c.Line(3, 6, 6, 7, w);
                            c.Outline(Pal.A(Pal.Ink, 200));
                            break;
                        }
                    case "poop":
                        {
                            Color32 p = Pal.H("4a3020");
                            c.Set(5, 4, p); c.Set(6, 4, p); c.Set(7, 5, p); c.Set(4, 5, p); c.Set(6, 3, p);
                            break;
                        }
                    case "mud":
                        {
                            c.Ellipse(6, 4, 5.5f, 3, Pal.A(Pal.H("4a3424"), 200));
                            c.Ellipse(5, 3.5f, 2, 1, Pal.A(Pal.H("6a4c34"), 200));
                            break;
                        }
                    case "oil":
                        {
                            c.Ellipse(6, 4, 4.5f, 2.5f, Pal.A(Pal.H("141418"), 200));
                            c.Set(5, 3, Pal.H("6a4a8a")); c.Set(6, 3, Pal.H("4a8a6a"));
                            break;
                        }
                    case "scorch":
                        {
                            c.Ellipse(6, 4, 5.5f, 3.2f, Pal.A(Pal.H("201410"), 160));
                            break;
                        }
                }
                return c.ToSprite(new Vector2(0.5f, 0.5f));
            });
        }

        public static Sprite Clutter(int v)
        {
            return Cached("clutter" + v, () =>
            {
                var c = new PixelCanvas(12, 12, 600 + v);
                switch (v % 6)
                {
                    case 0: BuildingArt.Crate(c, 2, 4, 8); break;
                    case 1: BuildingArt.Barrel(c, 3, 4); break;
                    case 2: c.Ring(6, 6, 5, 1.5f, Pal.H("6a4a2a")); c.Line(6, 1, 6, 11, Pal.H("5a3e28")); c.Line(1, 6, 11, 6, Pal.H("5a3e28")); break; // broken wheel
                    case 3: c.Line(2, 11, 9, 2, Pal.H("8a6440")); c.HLine(8, 11, 1, Pal.H("8a9098")); c.VLine(8, 0, 2, Pal.H("8a9098")); c.VLine(10, 0, 2, Pal.H("8a9098")); break; // pitchfork
                    case 4: c.Rect(2, 7, 8, 4, Pal.H("c8a050")); c.Rect(3, 5, 6, 2, Pal.H("b08c40")); c.SpeckleOn(0, 0, 12, 12, Pal.H("e0c070"), 0.3f); break; // hay bale
                    case 5: c.Rect(5, 4, 2, 8, Pal.H("5a3e28")); c.Rect(1, 1, 10, 5, Pal.H("e8c030")); c.HLine(2, 9, 3, Pal.H("201818")); break; // sign
                }
                c.Outline(Pal.Ink);
                return c.ToSprite(BottomCenter);
            });
        }

        public static Sprite Wolf(int frame, bool gerald)
        {
            return Cached("wolf" + frame + gerald, () =>
            {
                var c = new PixelCanvas(24, 18, 700);
                Color32 fur = gerald ? Pal.H("8a8a92") : Pal.H("5a5a64"), belly = Pal.H("b8b8c0"), dark = Pal.H("3a3a44");
                // legs
                int[] lx = { 5, 8, 14, 17 };
                for (int i = 0; i < 4; i++) { int sh = frame == 1 ? (i % 2 == 0 ? -1 : 1) : 0; c.VLine(lx[i], 13, 16, dark); c.Set(lx[i] + sh, 17, dark); }
                c.Ellipse(11, 10, 8, 4, fur); c.Ellipse(11, 12, 6, 2, belly);
                // tail
                c.Line(3, 9, 0, 5 + frame, fur); c.Line(3, 8, 1, 4 + frame, fur);
                // head
                c.Ellipse(19, 7, 4, 3.5f, fur);
                c.Rect(20, 8, 4, 3, fur);
                c.Set(17, 3, fur); c.Set(17, 2, fur); c.Set(20, 3, fur); c.Set(20, 2, fur);
                c.Set(23, 8, Pal.H("101010"));
                // eyes + teeth
                c.Set(19, 6, gerald ? Pal.H("f0f0f0") : Pal.H("ff3020")); c.Set(21, 6, gerald ? Pal.H("f0f0f0") : Pal.H("ff3020"));
                if (gerald) { c.Set(19, 6, Pal.H("101010")); c.Set(22, 12, Pal.H("e888a8")); c.Set(23, 11, Pal.H("f0c840")); c.Set(22, 11, Pal.H("4a8a30")); }
                else { c.Set(21, 11, Pal.H("ffffff")); c.Set(23, 11, Pal.H("ffffff")); c.HLine(20, 23, 10, Pal.H("7a1e1a")); }
                c.Outline(Pal.Ink);
                return c.ToSprite(BottomCenter);
            });
        }

        public static Sprite Trough()
        {
            return Cached("trough", () =>
            {
                var c = new PixelCanvas(24, 12, 800);
                c.Rect(1, 4, 22, 6, Pal.H("7a5638")); c.Rect(2, 4, 20, 2, Pal.H("6a9a3a"));
                c.SpeckleOn(2, 3, 20, 3, Pal.H("8ab84a"), 0.4f);
                c.VLine(2, 10, 11, Pal.H("5a3e28")); c.VLine(21, 10, 11, Pal.H("5a3e28"));
                c.Outline(Pal.Ink);
                return c.ToSprite(BottomCenter);
            });
        }
    }
}
