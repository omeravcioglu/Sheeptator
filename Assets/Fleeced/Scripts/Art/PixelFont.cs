using System.Collections.Generic;
using UnityEngine;

namespace Fleeced
{
    /// Hand-authored 5x7 pixel font rendered through IMGUI DrawTextureWithTexCoords.
    public static class PixelFont
    {
        public const int CellH = 9;     // rows incl. descenders
        public const int LineH = 10;
        struct Glyph { public int x, w; }
        static Dictionary<char, Glyph> glyphs;
        static Texture2D atlas;
        public static Texture2D Atlas { get { if (atlas == null) Init(); return atlas; } }

        static readonly string[] Defs =
        {
            "A.###.|#...#|#...#|#####|#...#|#...#|#...#",
            "B####.|#...#|#...#|####.|#...#|#...#|####.",
            "C.###.|#...#|#....|#....|#....|#...#|.###.",
            "D####.|#...#|#...#|#...#|#...#|#...#|####.",
            "E#####|#....|#....|####.|#....|#....|#####",
            "F#####|#....|#....|####.|#....|#....|#....",
            "G.###.|#...#|#....|#.###|#...#|#...#|.###.",
            "H#...#|#...#|#...#|#####|#...#|#...#|#...#",
            "I###|.#.|.#.|.#.|.#.|.#.|###",
            "J..###|...#.|...#.|...#.|#..#.|#..#.|.##..",
            "K#...#|#..#.|#.#..|##...|#.#..|#..#.|#...#",
            "L#....|#....|#....|#....|#....|#....|#####",
            "M#...#|##.##|#.#.#|#.#.#|#...#|#...#|#...#",
            "N#...#|##..#|#.#.#|#..##|#...#|#...#|#...#",
            "O.###.|#...#|#...#|#...#|#...#|#...#|.###.",
            "P####.|#...#|#...#|####.|#....|#....|#....",
            "Q.###.|#...#|#...#|#...#|#.#.#|#..#.|.##.#",
            "R####.|#...#|#...#|####.|#.#..|#..#.|#...#",
            "S.####|#....|#....|.###.|....#|....#|####.",
            "T#####|..#..|..#..|..#..|..#..|..#..|..#..",
            "U#...#|#...#|#...#|#...#|#...#|#...#|.###.",
            "V#...#|#...#|#...#|#...#|#...#|.#.#.|..#..",
            "W#...#|#...#|#...#|#.#.#|#.#.#|#.#.#|.#.#.",
            "X#...#|#...#|.#.#.|..#..|.#.#.|#...#|#...#",
            "Y#...#|#...#|.#.#.|..#..|..#..|..#..|..#..",
            "Z#####|....#|...#.|..#..|.#...|#....|#####",
            "a.....|.....|.###.|....#|.####|#...#|.####",
            "b#....|#....|####.|#...#|#...#|#...#|####.",
            "c....|....|.###|#...|#...|#...|.###",
            "d....#|....#|.####|#...#|#...#|#...#|.####",
            "e.....|.....|.###.|#...#|#####|#....|.###.",
            "f..##|.#..|####|.#..|.#..|.#..|.#..",
            "g.....|.....|.####|#...#|#...#|.####|....#|#...#|.###.",
            "h#....|#....|####.|#...#|#...#|#...#|#...#",
            "i#|.|#|#|#|#|#",
            "j..#|...|..#|..#|..#|..#|..#|#.#|.#.",
            "k#...|#...|#..#|#.#.|##..|#.#.|#..#",
            "l#.|#.|#.|#.|#.|#.|.#",
            "m.....|.....|##.#.|#.#.#|#.#.#|#.#.#|#.#.#",
            "n.....|.....|####.|#...#|#...#|#...#|#...#",
            "o.....|.....|.###.|#...#|#...#|#...#|.###.",
            "p.....|.....|####.|#...#|#...#|####.|#....|#....|#....",
            "q.....|.....|.####|#...#|#...#|.####|....#|....#|....#",
            "r....|....|#.##|##..|#...|#...|#...",
            "s.....|.....|.####|#....|.###.|....#|####.",
            "t.#..|.#..|####|.#..|.#..|.#..|..##",
            "u.....|.....|#...#|#...#|#...#|#...#|.####",
            "v.....|.....|#...#|#...#|#...#|.#.#.|..#..",
            "w.....|.....|#...#|#...#|#.#.#|#.#.#|.#.#.",
            "x.....|.....|#...#|.#.#.|..#..|.#.#.|#...#",
            "y.....|.....|#...#|#...#|#...#|.####|....#|#...#|.###.",
            "z.....|.....|#####|...#.|..#..|.#...|#####",
            "0.###.|#...#|#..##|#.#.#|##..#|#...#|.###.",
            "1.#.|##.|.#.|.#.|.#.|.#.|###",
            "2.###.|#...#|....#|...#.|..#..|.#...|#####",
            "3####.|....#|....#|.###.|....#|....#|####.",
            "4...#.|..##.|.#.#.|#..#.|#####|...#.|...#.",
            "5#####|#....|####.|....#|....#|#...#|.###.",
            "6..##.|.#...|#....|####.|#...#|#...#|.###.",
            "7#####|....#|...#.|..#..|.#...|.#...|.#...",
            "8.###.|#...#|#...#|.###.|#...#|#...#|.###.",
            "9.###.|#...#|#...#|.####|....#|...#.|.##..",
            " ...",
            "..|.|.|.|.|.|#",
            ",..|..|..|..|..|..|.#|#.",
            "!#|#|#|#|#|.|#",
            "?.###.|#...#|....#|...#.|..#..|.....|..#..",
            ":.|.|#|.|.|#|.",
            ";..|..|.#|..|..|.#|#.",
            "'#|#|.|.|.|.|.",
            "\"#.#|#.#|...|...|...|...|...",
            "-....|....|....|####|....|....|....",
            "+.....|..#..|..#..|#####|..#..|..#..|.....",
            "/....#|...#.|...#.|..#..|.#...|.#...|#....",
            "(.#|#.|#.|#.|#.|#.|.#",
            ")#.|.#|.#|.#|.#|.#|#.",
            "%##..#|##..#|...#.|..#..|.#...|#..##|#..##",
            "$..#..|.####|#.#..|.###.|..#.#|####.|..#..",
            "#.#.#.|.#.#.|#####|.#.#.|#####|.#.#.|.#.#.",
            "*.....|#.#.#|.###.|#####|.###.|#.#.#|.....",
            "=....|....|####|....|####|....|....",
            "<...#|..#.|.#..|#...|.#..|..#.|...#",
            ">#...|.#..|..#.|...#|..#.|.#..|#...",
            "_.....|.....|.....|.....|.....|.....|.....|#####",
            "[##|#.|#.|#.|#.|#.|##",
            "]##|.#|.#|.#|.#|.#|##",
            "&.##..|#..#.|#.#..|.#...|#.#.#|#..#.|.##.#",
            "@.###.|#...#|#.###|#.#.#|#.##.|#....|.###.",
            "~.....|.....|.....|.#..#|#.##.|.....|.....",
            "|#|#|#|#|#|#|#",
        };

        public static void Init()
        {
            if (atlas != null) return;
            glyphs = new Dictionary<char, Glyph>();
            int totalW = 0;
            foreach (var d in Defs) totalW += d.Substring(1).Split('|')[0].Length + 1;
            var canvas = new PixelCanvas(totalW, CellH);
            var white = new Color32(255, 255, 255, 255);
            int x = 0;
            foreach (var d in Defs)
            {
                char ch = d[0];
                var rows = d.Substring(1).Split('|');
                int gw = rows[0].Length;
                for (int j = 0; j < rows.Length && j < CellH; j++)
                    for (int i = 0; i < rows[j].Length && i < gw; i++)
                        if (rows[j][i] == '#') canvas.Set(x + i, j, white);
                glyphs[ch] = new Glyph { x = x, w = gw };
                x += gw + 1;
            }
            atlas = canvas.ToTexture();
        }

        static Glyph G(char c)
        {
            if (glyphs == null) Init();
            Glyph g;
            if (glyphs.TryGetValue(c, out g)) return g;
            return glyphs['?'];
        }

        /// Color codes: ^r red ^g green ^y yellow ^o orange ^w white(default) ^k grey ^b blue ^p pink ^c cyan
        public static Color CodeColor(char c, Color def)
        {
            switch (c)
            {
                case 'r': return new Color(1f, 0.38f, 0.33f);
                case 'g': return new Color(0.5f, 0.92f, 0.42f);
                case 'y': return new Color(1f, 0.86f, 0.35f);
                case 'o': return new Color(1f, 0.6f, 0.25f);
                case 'k': return new Color(0.62f, 0.6f, 0.58f);
                case 'b': return new Color(0.5f, 0.7f, 1f);
                case 'p': return new Color(1f, 0.6f, 0.75f);
                case 'c': return new Color(0.55f, 0.92f, 0.95f);
                default: return def;
            }
        }

        /// Stamps text pixels into a canvas (for signs on buildings).
        public static void Stamp(PixelCanvas c, int x, int y, string s, Color32 col)
        {
            var at = Atlas;
            int cx = x;
            foreach (char ch in s)
            {
                var g = G(ch);
                for (int j = 0; j < 7; j++)
                    for (int i = 0; i < g.w; i++)
                        if (at.GetPixel(g.x + i, CellH - 1 - j).a > 0.5f) c.Set(cx + i, y + j, col);
                cx += g.w + 1;
            }
        }

        public static int StampWidth(string s)
        {
            int w = 0; foreach (char ch in s) w += G(ch).w + 1; return w - 1;
        }

        public static float Width(string s, float scale = 1f)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            float w = 0, best = 0;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '^' && i + 1 < s.Length) { i++; continue; }
                if (c == '\n') { best = Mathf.Max(best, w); w = 0; continue; }
                w += (G(c).w + 1) * scale;
            }
            return Mathf.Max(best, w) - (w > 0 ? scale : 0);
        }

        /// Draws a single line (handles color codes). Returns width.
        public static float Draw(float x, float y, string s, Color col, float scale = 1f, bool shadow = true)
        {
            if (string.IsNullOrEmpty(s) || Event.current.type != EventType.Repaint) return Width(s, scale);
            var at = Atlas;
            float tw = at.width;
            var prev = GUI.color;
            if (shadow) DrawRaw(x + scale, y + scale, s, new Color(0, 0, 0, col.a * 0.75f), scale, at, tw, true);
            float w = DrawRaw(x, y, s, col, scale, at, tw, false);
            GUI.color = prev;
            return w;
        }

        /// Text with a full 1px ink outline (reads on grass, snow and blood alike).
        public static float DrawOutlined(float x, float y, string s, Color col, float scale = 1f)
        {
            if (string.IsNullOrEmpty(s) || Event.current.type != EventType.Repaint) return Width(s, scale);
            var at = Atlas;
            float tw = at.width;
            var prev = GUI.color;
            var ink = new Color(0.07f, 0.055f, 0.047f, col.a);
            for (int oy = -1; oy <= 1; oy++)
                for (int ox = -1; ox <= 1; ox++)
                    if (ox != 0 || oy != 0) DrawRaw(x + ox * scale, y + oy * scale, s, ink, scale, at, tw, true);
            float w = DrawRaw(x, y, s, col, scale, at, tw, false);
            GUI.color = prev;
            return w;
        }

        static float DrawRaw(float x, float y, string s, Color col, float scale, Texture2D at, float tw, bool mono)
        {
            float cx = x;
            Color cur = col;
            GUI.color = cur;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '^' && i + 1 < s.Length)
                {
                    i++;
                    if (!mono) { cur = CodeColor(s[i], col); cur.a = col.a; GUI.color = cur; }
                    continue;
                }
                if (c == '\n') { cx = x; y += LineH * scale; continue; }
                var g = G(c);
                if (c != ' ')
                    GUI.DrawTextureWithTexCoords(new Rect(cx, y, g.w * scale, CellH * scale), at, new Rect(g.x / tw, 0, g.w / tw, 1));
                cx += (g.w + 1) * scale;
            }
            return cx - x;
        }

        public static void DrawAligned(Rect r, string s, Color col, float scale = 1f, int align = 0, bool shadow = true)
        {
            float w = Width(s, scale);
            float x = align == 0 ? r.x : align == 1 ? r.x + (r.width - w) / 2f : r.xMax - w;
            float y = r.y + (r.height - 7 * scale) / 2f;
            Draw(Mathf.Round(x), Mathf.Round(y), s, col, scale, shadow);
        }

        /// Word-wrapped text. Returns height used. Color codes persist across wrapped lines.
        public static float DrawWrapped(Rect r, string s, Color col, float scale = 1f, bool draw = true)
        {
            var lines = Wrap(s, r.width, scale);
            float y = r.y;
            foreach (var l in lines)
            {
                if (draw) Draw(r.x, y, l, col, scale);
                y += LineH * scale;
            }
            return y - r.y;
        }

        public static float WrappedHeight(string s, float width, float scale = 1f)
        {
            return Wrap(s, width, scale).Count * LineH * scale;
        }

        static readonly Dictionary<long, List<string>> wrapCache = new Dictionary<long, List<string>>();

        public static List<string> Wrap(string s, float width, float scale)
        {
            long key = ((long)s.GetHashCode() << 20) ^ ((long)(width * 4) << 4) ^ (long)(scale * 4);
            List<string> cached;
            if (wrapCache.TryGetValue(key, out cached)) return cached;
            if (wrapCache.Count > 2000) wrapCache.Clear();
            var result = new List<string>();
            string carry = "";
            foreach (var para in s.Split('\n'))
            {
                var words = para.Split(' ');
                string line = carry;
                string lastCode = carry;
                bool lineEmpty = true;
                foreach (var w in words)
                {
                    string test = lineEmpty ? line + w : line + " " + w;
                    if (!lineEmpty && Width(test, scale) > width)
                    {
                        result.Add(line);
                        line = lastCode + w;
                    }
                    else line = test;
                    lineEmpty = false;
                    int ci = w.LastIndexOf('^');
                    if (ci >= 0 && ci + 1 < w.Length) lastCode = "^" + w[ci + 1];
                }
                result.Add(line);
                carry = "";
            }
            wrapCache[key] = result;
            return result;
        }
    }
}
