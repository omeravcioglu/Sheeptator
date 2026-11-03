using System.Collections.Generic;
using UnityEngine;

namespace Fleeced
{
    /// The herd on screen (M11): a carpet of small sheep, tiled over a room's floor, and the drove that trots behind a named
    /// sheep. Painted at runtime like everything else, and cached. A PixelCanvas counts rows from the TOP (row 0 is the top).
    public static class HerdArt
    {
        // the top-left corner of each sheep of a 32x32 floor tile, in the order the tile fills up: from the BOTTOM rows up, because a
        // small herd's carpet is shorter than a tile and shows only the tile's bottom part. Every sheep stays inside the tile (heads
        // included: left-facing ones, every third from the second, need x >= 2; right-facing ones x <= 21), so none is cut at a seam.
        static readonly int[] Spots = { 20, 25, 6, 25, 2, 17, 14, 18, 9, 9, 21, 10, 2, 1, 17, 2 };
        static readonly Dictionary<int, Sprite> floors = new Dictionary<int, Sprite>(), tails = new Dictionary<int, Sprite>();

        /// the fleece colour of a strain (SheepArt's), plain for 0; a shorn one is mostly pink skin
        static Color32 Wool(int strain, int fleece)
        {
            var c = Pal.H(SheepArt.StrainWoolHex(strain) ?? SheepArt.WoolHex[0]);
            return fleece == 0 ? Pal.Lerp(c, Pal.H("e8b8ac"), 0.45f) : c;
        }

        static int BodyW(int fleece) { return fleece == 0 ? 7 : fleece == 1 ? 8 : 9; }
        static int BodyH(int fleece) { return fleece == 0 ? 4 : 5; }

        /// One small sheep with its top-left corner at (x, y): fluffy bumps on the top row, then the body with a darker belly, the
        /// legs underneath, and the head at the front, level with the top of the body (a row higher on the other frame).
        static void Small(PixelCanvas c, int x, int y, int fleece, int strain, bool right, bool up, bool brown)
        {
            Color32 wool = Wool(strain, fleece), belly = Pal.Shade(wool, 0.8f), face = Pal.H(brown ? "5c4232" : "2c2626"), leg = Pal.H("2e2622");
            int bw = BodyW(fleece), bh = BodyH(fleece), top = y + 1;
            if (fleece == 2) { c.Set(x + 1, y, wool); c.Set(x + 4, y, wool); c.Set(x + 7, y, wool); }
            c.Rect(x, top, bw, bh, wool);
            c.HLine(x, x + bw - 1, top + bh - 1, belly);
            c.Set(x + 1, top + bh, leg); c.Set(x + bw - 2, top + bh, leg);
            int hx = right ? x + bw - 1 : x - 2, hy = top - (up ? 1 : 0);
            c.Rect(hx, hy, 3, 3, face);
            c.Set(right ? hx + 2 : hx, hy, Pal.Shade(face, 1.4f)); // an ear
        }

        /// density 1-4 (2, 4, 6 or 8 sheep a tile), fleece 0 shorn / 1 growing / 2 fluffy, strain, frame 0-1
        public static Sprite Floor(int density, int fleece, int strain, int frame)
        {
            density = Mathf.Clamp(density, 1, 4); fleece = Mathf.Clamp(fleece, 0, 2); strain = Mathf.Clamp(strain, 0, 15); frame &= 1;
            int key = ((density * 3 + fleece) * 16 + strain) * 2 + frame;
            Sprite s;
            if (floors.TryGetValue(key, out s) && s != null) return s;
            var c = new PixelCanvas(32, 32, key);
            int n = density * 2;
            for (int i = 0; i < n; i++) Small(c, Spots[2 * i], Spots[2 * i + 1], fleece, strain, i % 3 != 1, (i + frame) % 2 == 0, i % 4 == 3);
            c.Outline(Pal.A(Pal.Ink, 200));
            s = c.ToSprite(Vector2.zero); // tiled from the room floor's bottom-left corner
            floors[key] = s;
            return s;
        }

        /// size 1-4 (1, 2, 3 or 4 sheep), facing right like its leader (flipped with it), standing on the sprite's bottom row
        public static Sprite Tail(int size, int fleece, int strain)
        {
            size = Mathf.Clamp(size, 1, 4); fleece = Mathf.Clamp(fleece, 0, 2); strain = Mathf.Clamp(strain, 0, 15);
            int key = (size * 3 + fleece) * 16 + strain;
            Sprite s;
            if (tails.TryGetValue(key, out s) && s != null) return s;
            var c = new PixelCanvas(24, 14, key);
            int bw = BodyW(fleece), baseY = 12 - BodyH(fleece); // the legs on row 13, the bottom one
            for (int i = size - 1; i >= 0; i--) // the back ones first, a little higher up; the front one ends at the right edge
            {
                int x = 23 - (bw + 1) - i * 5, y = baseY - (i % 2) * 2 - (i >= 2 ? 1 : 0);
                Small(c, Mathf.Max(0, x), Mathf.Max(0, y), fleece, strain, true, i % 2 == 1, i == 2);
            }
            c.Outline(Pal.A(Pal.Ink, 220));
            s = c.ToSprite(new Vector2(1f, 0f)); // its bottom-right corner sits at the leader's tail
            tails[key] = s;
            return s;
        }
    }
}
