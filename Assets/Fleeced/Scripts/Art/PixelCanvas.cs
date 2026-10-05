using System.Collections.Generic;
using UnityEngine;

namespace Fleeced
{
    public static class Pal
    {
        public static readonly Color32 Clear = new Color32(0, 0, 0, 0);
        public static readonly Color32 Ink = H("1c1416");

        public static Color32 H(string hex)
        {
            int v = System.Convert.ToInt32(hex, 16);
            return new Color32((byte)((v >> 16) & 255), (byte)((v >> 8) & 255), (byte)(v & 255), 255);
        }
        public static Color32 Shade(Color32 c, float f)
        {
            return new Color32((byte)Mathf.Clamp(c.r * f, 0, 255), (byte)Mathf.Clamp(c.g * f, 0, 255), (byte)Mathf.Clamp(c.b * f, 0, 255), c.a);
        }
        public static Color32 Lerp(Color32 a, Color32 b, float t) { return Color32.Lerp(a, b, t); }
        public static Color32 A(Color32 c, byte a) { c.a = a; return c; }
        public static Color C(string hex) { return H(hex); }
    }

    /// Simple software canvas. Coordinates: x right, y DOWN (0 = top row).
    public class PixelCanvas
    {
        public readonly int w, h;
        public readonly Color32[] px;
        public System.Random rng;

        public PixelCanvas(int w, int h, int seed = 1)
        {
            this.w = w; this.h = h; px = new Color32[w * h]; rng = new System.Random(seed);
        }

        public bool In(int x, int y) { return x >= 0 && y >= 0 && x < w && y < h; }
        public void Set(int x, int y, Color32 c) { if (In(x, y)) px[(h - 1 - y) * w + x] = c; }
        public void Blend(int x, int y, Color32 c)
        {
            if (!In(x, y)) return;
            int i = (h - 1 - y) * w + x;
            var d = px[i];
            if (d.a == 0) { px[i] = c; return; }
            float t = c.a / 255f;
            px[i] = new Color32((byte)(d.r + (c.r - d.r) * t), (byte)(d.g + (c.g - d.g) * t), (byte)(d.b + (c.b - d.b) * t), d.a);
        }
        public Color32 Get(int x, int y) { return In(x, y) ? px[(h - 1 - y) * w + x] : Pal.Clear; }
        public bool Opaque(int x, int y) { return In(x, y) && px[(h - 1 - y) * w + x].a > 0; }

        public void Rect(int x, int y, int rw, int rh, Color32 c)
        {
            for (int j = y; j < y + rh; j++) for (int i = x; i < x + rw; i++) Set(i, j, c);
        }
        public void RectBlend(int x, int y, int rw, int rh, Color32 c)
        {
            for (int j = y; j < y + rh; j++) for (int i = x; i < x + rw; i++) Blend(i, j, c);
        }
        public void Frame(int x, int y, int rw, int rh, Color32 c)
        {
            for (int i = x; i < x + rw; i++) { Set(i, y, c); Set(i, y + rh - 1, c); }
            for (int j = y; j < y + rh; j++) { Set(x, j, c); Set(x + rw - 1, j, c); }
        }
        public void HLine(int x0, int x1, int y, Color32 c) { for (int i = x0; i <= x1; i++) Set(i, y, c); }
        public void VLine(int x, int y0, int y1, Color32 c) { for (int j = y0; j <= y1; j++) Set(x, j, c); }
        public void Line(int x0, int y0, int x1, int y1, Color32 c)
        {
            int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1, dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1, err = dx + dy;
            for (int guard = 0; guard < 2000; guard++)
            {
                Set(x0, y0, c);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }
        public void Ellipse(float cx, float cy, float rx, float ry, Color32 c)
        {
            if (rx <= 0 || ry <= 0) return;
            int x0 = Mathf.FloorToInt(cx - rx), x1 = Mathf.CeilToInt(cx + rx), y0 = Mathf.FloorToInt(cy - ry), y1 = Mathf.CeilToInt(cy + ry);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    if (dx * dx + dy * dy <= 1f) Set(x, y, c);
                }
        }
        public void EllipseBlend(float cx, float cy, float rx, float ry, Color32 c)
        {
            int x0 = Mathf.FloorToInt(cx - rx), x1 = Mathf.CeilToInt(cx + rx), y0 = Mathf.FloorToInt(cy - ry), y1 = Mathf.CeilToInt(cy + ry);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    if (dx * dx + dy * dy <= 1f) Blend(x, y, c);
                }
        }
        public void Ring(float cx, float cy, float r, float thick, Color32 c)
        {
            int x0 = Mathf.FloorToInt(cx - r - 1), x1 = Mathf.CeilToInt(cx + r + 1);
            int y0 = Mathf.FloorToInt(cy - r - 1), y1 = Mathf.CeilToInt(cy + r + 1);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                    if (d <= r && d >= r - thick) Set(x, y, c);
                }
        }
        public void Speckle(int x, int y, int rw, int rh, Color32 c, float density)
        {
            for (int j = y; j < y + rh; j++) for (int i = x; i < x + rw; i++) if (rng.NextDouble() < density) Set(i, j, c);
        }
        /// Only speckle on already-opaque pixels.
        public void SpeckleOn(int x, int y, int rw, int rh, Color32 c, float density)
        {
            for (int j = y; j < y + rh; j++) for (int i = x; i < x + rw; i++) if (Opaque(i, j) && rng.NextDouble() < density) Set(i, j, c);
        }
        public void ShadeRegion(int x, int y, int rw, int rh, float f)
        {
            for (int j = y; j < y + rh; j++) for (int i = x; i < x + rw; i++) if (Opaque(i, j)) Set(i, j, Pal.Shade(Get(i, j), f));
        }

        /// Adds a 1px outline around opaque regions (cartoon look).
        public void Outline(Color32 c)
        {
            var copy = (Color32[])px.Clone();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (copy[(h - 1 - y) * w + x].a > 0) continue;
                    bool n = false;
                    if (x > 0 && copy[(h - 1 - y) * w + x - 1].a > 0) n = true;
                    else if (x < w - 1 && copy[(h - 1 - y) * w + x + 1].a > 0) n = true;
                    else if (y > 0 && copy[(h - y) * w + x].a > 0) n = true;
                    else if (y < h - 1 && copy[(h - 2 - y) * w + x].a > 0) n = true;
                    if (n) px[(h - 1 - y) * w + x] = c;
                }
        }

        /// Draws string-art: rows separated by '|', palette chars mapped by Icons.Color.
        public void Art(int ox, int oy, string art, float shade = 1f)
        {
            var rows = art.Split('|');
            for (int j = 0; j < rows.Length; j++)
                for (int i = 0; i < rows[j].Length; i++)
                {
                    char ch = rows[j][i];
                    if (ch == '.' || ch == ' ') continue;
                    var col = Icons.Color(ch);
                    Set(ox + i, oy + j, shade == 1f ? col : Pal.Shade(col, shade));
                }
        }

        public Texture2D ToTexture()
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Point;
            t.wrapMode = TextureWrapMode.Clamp;
            t.SetPixels32(px);
            t.Apply(false, false);
            return t;
        }

        public Sprite ToSprite(Vector2 pivot, float ppu = 16f)
        {
            var t = ToTexture();
            return Sprite.Create(t, new Rect(0, 0, w, h), pivot, ppu, 0, SpriteMeshType.FullRect);
        }
    }
}
