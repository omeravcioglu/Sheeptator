using System.Collections.Generic;
using UnityEngine;

namespace Fleeced
{
    /// The woolly SHEEPTATOR wordmark: fluffy letters on a blood-red extrusion, with a few drips.
    public static class LogoArt
    {
        static Texture2D tex;

        static readonly Dictionary<char, string[]> Font = new Dictionary<char, string[]>
        {
            { 'F', new[] { "######", "######", "##....", "#####.", "##....", "##....", "##...." } },
            { 'S', new[] { ".#####", "######", "##....", ".####.", "....##", "######", "#####." } },
            { 'H', new[] { "##..##", "##..##", "##..##", "######", "##..##", "##..##", "##..##" } },
            { 'P', new[] { "#####.", "######", "##..##", "######", "#####.", "##....", "##...." } },
            { 'T', new[] { "######", "######", "..##..", "..##..", "..##..", "..##..", "..##.." } },
            { 'A', new[] { ".####.", "######", "##..##", "######", "######", "##..##", "##..##" } },
            { 'O', new[] { ".####.", "######", "##..##", "##..##", "##..##", "######", ".####." } },
            { 'R', new[] { "#####.", "######", "##..##", "#####.", "####..", "##.##.", "##..##" } },
            { 'L', new[] { "##....", "##....", "##....", "##....", "##....", "######", "######" } },
            { 'E', new[] { "######", "######", "##....", "#####.", "##....", "######", "######" } },
            { 'C', new[] { ".#####", "######", "##....", "##....", "##....", "######", ".#####" } },
            { 'D', new[] { "#####.", "######", "##..##", "##..##", "##..##", "######", "#####." } },
        };

        public static Texture2D Tex()
        {
            if (tex != null) return tex;
            const string text = "SHEEPTATOR";
            const int sub = 3, M = 4;
            var rnd = new System.Random(5);
            int gw = text.Length - 1;
            foreach (char ch in text) gw += Font[ch][0].Length;
            int W = gw * sub + 2 * M, H = 7 * sub + 2 * M + 8;
            var mask = new bool[W, H];
            int ox = M;
            foreach (char ch in text)
            {
                var l = Font[ch];
                for (int j = 0; j < l.Length; j++)
                    for (int i = 0; i < l[j].Length; i++)
                        if (l[j][i] == '#')
                            for (int a = 0; a < sub; a++)
                                for (int b = 0; b < sub; b++)
                                    mask[ox + i * sub + b, M + j * sub + a] = true;
                ox += (l[0].Length + 1) * sub;
            }
            // fluff: random bumps on top and side edges
            var add = new List<Vector2Int>();
            for (int y = 1; y < H - 1; y++)
                for (int x = 1; x < W - 1; x++)
                {
                    if (mask[x, y]) continue;
                    if (mask[x, y + 1] && rnd.NextDouble() < 0.42) add.Add(new Vector2Int(x, y));
                    else if ((mask[x - 1, y] || mask[x + 1, y]) && rnd.NextDouble() < 0.18) add.Add(new Vector2Int(x, y));
                }
            foreach (var p in add) mask[p.x, p.y] = true;
            // blood extrusion
            var red = new bool[W, H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    if (mask[x, y])
                        for (int d = 1; d <= 3; d++)
                            if (y + d < H) red[x, y + d] = true;
            // drips from the lower edges
            var bottoms = new List<int>();
            for (int x = M; x < W - M; x++)
                for (int y = H / 2; y < H - 1; y++)
                    if (mask[x, y] && !mask[x, y + 1]) { bottoms.Add(x); break; }
            for (int i = bottoms.Count - 1; i > 0; i--) { int k = rnd.Next(i + 1); int t = bottoms[i]; bottoms[i] = bottoms[k]; bottoms[k] = t; }
            for (int n = 0; n < Mathf.Min(5, bottoms.Count); n++)
            {
                int x = bottoms[n], maxY = 0;
                for (int y = 0; y < H; y++) if (mask[x, y]) maxY = y;
                int yb = maxY + 3, ln = rnd.Next(2, 6);
                int e = Mathf.Min(H - 2, yb + ln);
                for (int y = yb; y < e; y++) red[x, y] = true;
                for (int dx = 0; dx <= 1; dx++)
                    for (int dy = 0; dy <= 1; dy++)
                        if (x + dx < W && e + dy < H) red[x + dx, e + dy] = true;
            }
            var c = new PixelCanvas(W, H);
            var ink = Pal.H("120e0c");
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    if (mask[x, y] || red[x, y])
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= 1; dx++) c.Set(x + dx, y + dy, ink);
            Color32 blood = Pal.H("8c2f2f"), bloodLo = Pal.H("5e1e1e"), wool = Pal.H("f4efe6"), woolLo = Pal.H("cfc4b2"), woolHi = Pal.H("ffffff");
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    if (red[x, y] && !mask[x, y]) c.Set(x, y, (y + 1 < H && !red[x, y + 1]) ? bloodLo : blood);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    if (!mask[x, y]) continue;
                    bool below = y + 1 < H && mask[x, y + 1];
                    bool above = y > 0 && mask[x, y - 1];
                    var col = wool;
                    if (!below || (y + 2 < H && !mask[x, y + 2])) col = woolLo;
                    else if (!above && rnd.NextDouble() < 0.5) col = woolHi;
                    c.Set(x, y, col);
                }
            tex = c.ToTexture();
            return tex;
        }
    }
}
