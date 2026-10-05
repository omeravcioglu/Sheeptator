using System.Collections.Generic;
using UnityEngine;

namespace Fleeced
{
    public enum BtnStyle { Normal, Primary, Danger }

    /// Immediate-mode pixel widget kit. Walnut panels, cream text, gold for what matters, blood for what hurts.
    /// All rects are in virtual UI pixels; one UI pixel is one art pixel (the scale is always an integer).
    public static class UI
    {
        public static float scale = 2f;
        static Texture2D white;
        public static Texture2D White
        {
            get
            {
                if (white == null) { white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply(); white.filterMode = FilterMode.Point; }
                return white;
            }
        }

        public static readonly Color
            Ink = Pal.C("120e0c"), BgDark = Pal.C("1c1714"), Bg = Pal.C("2a221e"), Hi = Pal.C("3a302a"), Hi2 = Pal.C("4a3d33"),
            Edge = Pal.C("6b5a45"), Rivet = Pal.C("a08a6a"),
            Btn = Pal.C("6b4f35"), BtnHi = Pal.C("8a6a44"), BtnLo = Pal.C("4a3624"), BtnHover = Pal.C("7d5d3d"), BtnHoverHi = Pal.C("9a7a54"),
            BtnOff = Pal.C("3e3632"), BtnOffHi = Pal.C("4a423c"), BtnOffLo = Pal.C("2e2824"),
            Gold = Pal.C("ffdc5a"), GoldHi = Pal.C("fff3b0"), GoldLo = Pal.C("c9a227"),
            Banner = Pal.C("8c2f2f"), BannerHi = Pal.C("b04545"), BannerLo = Pal.C("5e1e1e"), BannerHover = Pal.C("a03838"),
            Text = Pal.C("f0e8d8"), Dim = Pal.C("a09888"), Good = Pal.C("80ea6a"), Bad = Pal.C("ff6454"), Xp = Pal.C("6ab0ff"),
            Wool = Pal.C("f4efe6"), Grass = Pal.C("4f8a3a"), GrassLo = Pal.C("2f5424"), GrassHi = Pal.C("6aa84f");
        public static readonly Color EdgeDark = Ink, BtnSel = Gold;
        static readonly Color CardOff = Pal.C("241d19"), CardHoverHi = Pal.C("5a4a3e");

        public static string tooltip;
        public static readonly List<Rect> blockers = new List<Rect>();
        public static readonly List<Rect> blockersPrev = new List<Rect>();
        public static bool clipActive; public static Rect clipRect;
        /// multiplies the alpha of everything drawn by Fill / Icon (used to fade banners)
        public static float alpha = 1f;
        public static bool Repaint { get { return Event.current.type == EventType.Repaint; } }

        public static void BeginFrame()
        {
            if (Event.current.type == EventType.Repaint)
            {
                blockersPrev.Clear(); blockersPrev.AddRange(blockers); blockers.Clear();
                tooltip = null;
                marks.Clear();
            }
            alpha = 1f;
        }

        /// Named rects (top-level virtual coords) recorded while drawing, so the tutorial can point at them.
        public static readonly Dictionary<string, Rect> marks = new Dictionary<string, Rect>();
        static Vector2 groupOffset; static Rect groupClip; static bool groupClipOn;

        public static void Mark(string id, Rect r)
        {
            if (!Repaint) return;
            var vr = new Rect(r.x + groupOffset.x, r.y + groupOffset.y, r.width, r.height);
            if (groupClipOn)
            {
                if (!vr.Overlaps(groupClip)) return;
                vr = Rect.MinMaxRect(Mathf.Max(vr.x, groupClip.x), Mathf.Max(vr.y, groupClip.y), Mathf.Min(vr.xMax, groupClip.xMax), Mathf.Min(vr.yMax, groupClip.yMax));
            }
            marks[id] = vr;
        }

        public static void Block(Rect r)
        {
            if (Repaint) blockers.Add(new Rect(r.x * scale, r.y * scale, r.width * scale, r.height * scale));
        }

        public static Rect Inset(Rect r, float d) { return new Rect(r.x + d, r.y + d, r.width - d * 2, r.height - d * 2); }

        static Color A(Color c, float a) { c.a *= a; return c; }

        // ---------------- primitives ----------------
        public static void Fill(Rect r, Color c)
        {
            if (!Repaint || r.width <= 0 || r.height <= 0) return;
            var prev = GUI.color;
            c.a *= alpha;
            GUI.color = c;
            GUI.DrawTexture(r, White);
            GUI.color = prev;
        }

        public static void Frame(Rect r, Color c, float t = 1)
        {
            Fill(new Rect(r.x, r.y, r.width, t), c);
            Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
            Fill(new Rect(r.x, r.y + t, t, r.height - t * 2), c);
            Fill(new Rect(r.xMax - t, r.y + t, t, r.height - t * 2), c);
        }

        /// A filled rect with its four corner pixels knocked out (pixel-art rounded corner).
        public static void PxFill(Rect r, Color c)
        {
            Fill(new Rect(r.x + 1, r.y, r.width - 2, r.height), c);
            Fill(new Rect(r.x, r.y + 1, 1, r.height - 2), c);
            Fill(new Rect(r.xMax - 1, r.y + 1, 1, r.height - 2), c);
        }

        /// Ink-bordered notched box, optionally with a colored inner ring.
        public static void Box(Rect r, Color fill, Color? ring = null)
        {
            PxFill(r, Ink);
            if (ring != null) { Fill(Inset(r, 1), ring.Value); Fill(Inset(r, 2), fill); }
            else Fill(Inset(r, 1), fill);
        }

        /// Inset well: lists, gauges, portraits.
        public static void Well(Rect r, Color? fill = null)
        {
            Fill(r, Ink);
            var i = Inset(r, 1);
            Fill(i, fill ?? BgDark);
            Fill(new Rect(i.x, i.y, i.width, 1), A(Ink, 0.6f));
            Fill(new Rect(i.x, i.y + 1, 1, i.height - 1), A(Ink, 0.6f));
            Fill(new Rect(i.x + 1, i.yMax - 1, i.width - 1, 1), A(Color.white, 0.07f));
            Fill(new Rect(i.xMax - 1, i.y + 1, 1, i.height - 2), A(Color.white, 0.07f));
        }

        /// List row / card. Selected cards get a gold ring.
        public static void Card(Rect r, bool selected = false, bool off = false, bool hover = false)
        {
            if (selected)
            {
                PxFill(r, Gold);
                Fill(Inset(r, 1), Ink);
                Fill(Inset(r, 2), Hi2);
                return;
            }
            PxFill(r, Ink);
            var i = Inset(r, 1);
            Fill(i, off ? CardOff : hover ? Hi2 : Hi);
            if (!off) Fill(new Rect(i.x, i.y, i.width, 1), hover ? CardHoverHi : Hi2);
        }

        // ---------------- panels ----------------
        public const float HeaderH = 26;

        /// Riveted walnut panel. With a title, a red tab sits in the header row (content starts at r.y + HeaderH).
        public static void Panel(Rect r, string title = null, bool block = true, string icon = null)
        {
            if (block) Block(r);
            Fill(new Rect(r.x + 3, r.yMax, r.width - 2, 2), new Color(0, 0, 0, 0.35f));
            Fill(new Rect(r.xMax, r.y + 3, 2, r.height - 3), new Color(0, 0, 0, 0.35f));
            PxFill(r, Ink);
            Fill(Inset(r, 1), Edge);
            Fill(Inset(r, 2), Ink);
            Fill(Inset(r, 3), Bg);
            Fill(new Rect(r.x + 3, r.y + 3, r.width - 6, 1), Hi);
            Fill(new Rect(r.x + 5, r.y + 5, 2, 2), Rivet); Fill(new Rect(r.xMax - 7, r.y + 5, 2, 2), Rivet);
            Fill(new Rect(r.x + 5, r.yMax - 7, 2, 2), Rivet); Fill(new Rect(r.xMax - 7, r.yMax - 7, 2, 2), Rivet);
            if (title != null) Tab(r.x + 9, r.y + 6, title, icon);
        }

        /// Red title tab. Returns its width.
        public static float Tab(float x, float y, string title, string icon = null)
        {
            float tw = PixelFont.Width(title);
            float w = tw + 12 + (icon != null ? 13 : 0);
            var r = new Rect(x, y, w, 15);
            PxFill(r, Ink);
            Fill(Inset(r, 1), Banner);
            Fill(new Rect(r.x + 1, r.y + 1, r.width - 2, 1), BannerHi);
            Fill(new Rect(r.x + 1, r.yMax - 2, r.width - 2, 1), BannerLo);
            float tx = r.x + 6;
            if (icon != null) { var t = Icons.Tex(icon); Icon(r.x + 5, Mathf.Round(r.y + 7.5f - t.height / 2f), icon); tx += 13; }
            PixelFont.Draw(tx, r.y + 4, title, A(Text, alpha));
            return w;
        }

        /// Close button in a panel's header row.
        public static bool CloseButton(Rect panel)
        {
            return IconButton(new Rect(panel.xMax - 23, panel.y + 6, 15, 15), "close", true, "Close [Esc]");
        }

        public static void Heading(float x, float y, string s, Color? c = null)
        {
            PixelFont.Draw(x, y, s, A(c ?? Gold, alpha));
        }

        // ---------------- input ----------------
        public static bool inputEnabled = true;

        public static bool Hover(Rect r)
        {
            if (!inputEnabled) return false;
            var m = Event.current.mousePosition;
            if (clipActive && !clipRect.Contains(m)) return false;
            return r.Contains(m);
        }

        static bool Pressed(Rect r)
        {
            var e = Event.current;
            if (Hover(r) && e.type == EventType.MouseDown && e.button == 0) { e.Use(); return true; }
            return false;
        }

        // ---------------- buttons ----------------
        public static bool Button(Rect r, string label, bool enabled = true, string tip = null, bool selected = false, Color? col = null)
        {
            return ButtonEx(r, label, null, null, enabled, tip, selected ? BtnStyle.Primary : BtnStyle.Normal);
        }

        public static bool IconButton(Rect r, string icon, bool enabled = true, string tip = null, bool selected = false)
        {
            return ButtonEx(r, null, icon, null, enabled, tip, selected ? BtnStyle.Primary : BtnStyle.Normal);
        }

        /// Chunky pixel button: optional icon, label and keycap. left = left-align the content; keyLeft = keycap before the label.
        public static bool ButtonEx(Rect r, string label, string icon = null, string key = null, bool enabled = true, string tip = null,
            BtnStyle style = BtnStyle.Normal, bool left = false, bool keyLeft = false)
        {
            bool hover = Hover(r);
            if (Repaint)
            {
                bool down = hover && enabled && Input.GetMouseButton(0);
                Color c, hi, lo, tc = Text; bool shadow = true;
                if (!enabled) { c = BtnOff; hi = BtnOffHi; lo = BtnOffLo; tc = Dim; shadow = false; }
                else if (style == BtnStyle.Primary) { c = Gold; hi = GoldHi; lo = GoldLo; tc = Ink; shadow = false; }
                else if (style == BtnStyle.Danger) { c = hover ? BannerHover : Banner; hi = BannerHi; lo = BannerLo; }
                else { c = hover ? BtnHover : Btn; hi = hover ? BtnHoverHi : BtnHi; lo = BtnLo; }
                PxFill(r, Ink);
                var i = Inset(r, 1);
                Fill(i, c);
                if (down) Fill(new Rect(i.x, i.y, i.width, 2), lo);
                else
                {
                    Fill(new Rect(i.x, i.y, i.width, 1), hi);
                    Fill(new Rect(i.x, i.yMax - 2, i.width, 2), lo);
                }
                float oy = down ? 1 : 0;
                float mid = r.y + (r.height - 1) / 2f + oy;
                string ic = icon;
                if (ic != null && style == BtnStyle.Primary && enabled && Icons.Has(ic + "_ink")) ic += "_ink";
                Texture2D it = ic != null ? Icons.Tex(ic) : null;
                float iw = it != null ? it.width : 0;
                float lw = string.IsNullOrEmpty(label) ? 0 : PixelFont.Width(label);
                float kw = key != null ? KeyW(key) : 0;
                float cw = iw + lw + (iw > 0 && lw > 0 ? 4 : 0);
                float cx;
                if (left) cx = r.x + 5 + (key != null && keyLeft ? kw + 5 : 0);
                else cx = r.x + Mathf.Round((r.width - cw - (key != null ? kw + 4 : 0)) / 2f) + (key != null && keyLeft ? kw + 4 : 0);
                if (it != null)
                {
                    float a = enabled ? 1f : 0.45f;
                    var prev = GUI.color;
                    GUI.color = new Color(1, 1, 1, a * alpha);
                    GUI.DrawTexture(new Rect(Mathf.Round(cx), Mathf.Round(mid - it.height / 2f), it.width, it.height), it);
                    GUI.color = prev;
                    cx += iw + 4;
                }
                if (lw > 0) PixelFont.Draw(Mathf.Round(cx), Mathf.Round(mid - 3.5f), label, A(tc, alpha), 1, shadow);
                if (key != null)
                {
                    float kx = keyLeft ? r.x + 4 : r.xMax - 4 - kw;
                    Key(kx, Mathf.Round(mid - 6), key);
                }
            }
            if (hover && tip != null) tooltip = tip;
            if (Pressed(r))
            {
                if (enabled) { Sfx.Play("click", 0.5f); return true; }
                Sfx.Play("click", 0.3f, 0.6f);
            }
            return false;
        }

        public static float KeyW(string k) { return PixelFont.Width(k) + 6; }

        /// Keycap: every hotkey is shown where it applies.
        public static void Key(float x, float y, string k)
        {
            var r = new Rect(x, y, KeyW(k), 12);
            PxFill(r, Ink);
            Fill(Inset(r, 1), Bg);
            Fill(new Rect(r.x + 1, r.yMax - 2, r.width - 2, 1), Ink);
            PixelFont.Draw(r.x + 3, r.y + 2, k, A(Text, alpha), 1, false);
        }

        /// invisible click area
        public static bool Clickable(Rect r, string tip = null, bool highlight = true)
        {
            bool hover = Hover(r);
            if (hover && tip != null) tooltip = tip;
            if (hover && highlight && Repaint) Fill(r, new Color(1, 1, 1, 0.06f));
            if (Pressed(r)) { Sfx.Play("click", 0.4f); return true; }
            return false;
        }

        public static void Tip(Rect r, string tip)
        {
            if (Hover(r)) tooltip = tip;
        }

        // ---------------- text ----------------
        public static void Label(float x, float y, string s, Color? c = null, float sc = 1)
        {
            PixelFont.Draw(x, y, s, A(c ?? Text, alpha), sc);
        }

        public static float Wrapped(Rect r, string s, Color? c = null)
        {
            return PixelFont.DrawWrapped(r, s, A(c ?? Text, alpha));
        }

        /// Outlined trait tag (border and text share the color). Returns width.
        public static float Tag(float x, float y, string text, Color col, string tip = null)
        {
            float w = PixelFont.Width(text) + 8;
            var r = new Rect(x, y, w, 13);
            Fill(r, col);
            Fill(Inset(r, 1), BgDark);
            PixelFont.Draw(x + 4, y + 3, text, A(col, alpha), 1, false);
            if (tip != null) Tip(r, tip);
            return w;
        }

        // ---------------- gauges ----------------
        /// Segmented gauge. stripes = construction look.
        public static void Bar(Rect r, float t, Color c, string label = null, bool stripes = false)
        {
            Fill(r, Ink);
            var i = Inset(r, 1);
            Fill(i, BgDark);
            float fw = Mathf.Round(i.width * Mathf.Clamp01(t));
            if (fw > 0)
            {
                if (stripes)
                {
                    Color lo = Color.Lerp(c, Color.black, 0.25f);
                    for (float x = 0; x < fw; x += 4)
                    {
                        Fill(new Rect(i.x + x, i.y, Mathf.Min(3, fw - x), i.height), c);
                        if (x + 3 < fw) Fill(new Rect(i.x + x + 3, i.y, 1, i.height), lo);
                    }
                }
                else Fill(new Rect(i.x, i.y, fw, i.height), c);
                if (i.height >= 3)
                {
                    Fill(new Rect(i.x, i.y, fw, 1), A(Color.white, 0.35f));
                    Fill(new Rect(i.x, i.yMax - 1, fw, 1), A(Color.black, 0.28f));
                }
            }
            if (i.width >= 24 && !stripes)
                for (float x = i.x + 5; x < i.xMax - 1; x += 6) Fill(new Rect(x, i.y, 1, i.height), new Color(0.07f, 0.055f, 0.047f, 0.5f));
            if (label != null) PixelFont.DrawAligned(r, label, A(Text, alpha), 1, 1);
        }

        // ---------------- icons & sprites ----------------
        public static void Icon(float x, float y, string name, float sc = 1)
        {
            if (!Repaint) return;
            var t = Icons.Tex(name);
            var prev = GUI.color;
            GUI.color = new Color(1, 1, 1, alpha);
            GUI.DrawTexture(new Rect(x, y, t.width * sc, t.height * sc), t);
            GUI.color = prev;
        }

        /// Draws an icon at the largest integer scale that fits the rect, centered.
        public static void IconFit(Rect r, string name, bool dim = false)
        {
            if (!Repaint) return;
            var t = Icons.Tex(name);
            float s = Mathf.Max(1, Mathf.Floor(Mathf.Min(r.width / t.width, r.height / t.height)));
            var prev = GUI.color;
            GUI.color = dim ? new Color(0.45f, 0.42f, 0.4f, 0.8f * alpha) : new Color(1, 1, 1, alpha);
            GUI.DrawTexture(new Rect(Mathf.Round(r.center.x - t.width * s / 2f), Mathf.Round(r.center.y - t.height * s / 2f), t.width * s, t.height * s), t);
            GUI.color = prev;
        }

        public static void DrawSprite(Rect r, Sprite s, bool flip = false)
        {
            if (!Repaint || s == null) return;
            var tr = s.textureRect;
            var tex = s.texture;
            var uv = new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);
            if (flip) { uv.x += uv.width; uv.width = -uv.width; }
            var prev = GUI.color;
            GUI.color = new Color(1, 1, 1, alpha);
            GUI.DrawTextureWithTexCoords(r, tex, uv);
            GUI.color = prev;
        }

        // ---------------- scroll areas ----------------
        public static void BeginScroll(Rect r, ref float scroll, float contentH)
        {
            var e = Event.current;
            if (inputEnabled && e.type == EventType.ScrollWheel && r.Contains(e.mousePosition))
            {
                scroll += e.delta.y * 12f;
                e.Use();
            }
            scroll = Mathf.Clamp(scroll, 0, Mathf.Max(0, contentH - r.height));
            GUI.BeginGroup(r);
            clipActive = true; clipRect = new Rect(0, 0, r.width, r.height);
            if (contentH > r.height)
            {
                var track = new Rect(r.width - 4, 0, 4, r.height);
                Fill(track, Ink);
                Fill(new Rect(track.x + 1, 1, 2, r.height - 2), BgDark);
                float bh = Mathf.Max(12, (r.height - 2) * r.height / contentH);
                float by = 1 + Mathf.Round((r.height - 2 - bh) * (scroll / Mathf.Max(1, contentH - r.height)));
                Fill(new Rect(track.x + 1, by, 2, bh), Edge);
                Fill(new Rect(track.x + 1, by, 2, 1), Rivet);
            }
            GUI.BeginGroup(new Rect(0, -scroll, r.width - 6, contentH + r.height));
            groupOffset = new Vector2(r.x, r.y - scroll); groupClip = r; groupClipOn = true;
        }

        public static void EndScroll()
        {
            GUI.EndGroup();
            GUI.EndGroup();
            clipActive = false;
            groupOffset = Vector2.zero; groupClipOn = false;
        }

        /// Tooltip. A multi-line tip shows its first line as a gold title.
        public static void DrawTooltip(float W, float H)
        {
            if (string.IsNullOrEmpty(tooltip) || !Repaint) return;
            alpha = 1f;
            var m = Event.current.mousePosition;
            string title = null, body = tooltip;
            int nl = tooltip.IndexOf('\n');
            if (nl > 0) { title = tooltip.Substring(0, nl); body = tooltip.Substring(nl + 1); }
            float inner = Mathf.Min(212, Mathf.Max(title != null ? PixelFont.Width(title) : 0, PixelFont.Width(body)));
            float h = (title != null ? 12 : 0) + PixelFont.WrappedHeight(body, inner) + 9;
            float w = inner + 12;
            float x = Mathf.Round(Mathf.Min(m.x + 10, W - w - 2)), y = Mathf.Round(Mathf.Min(m.y + 14, H - h - 2));
            var r = new Rect(x, y, w, h);
            Fill(new Rect(r.x + 2, r.y + 2, r.width, r.height), new Color(0, 0, 0, 0.4f));
            Box(r, BgDark, Rivet);
            float ty = r.y + 5;
            if (title != null) { PixelFont.Draw(r.x + 6, ty, title, Gold); ty += 12; }
            PixelFont.DrawWrapped(new Rect(r.x + 6, ty, inner, h), body, Text);
        }

        public static string Fmt(double v)
        {
            double a = System.Math.Abs(v);
            if (a >= 1000000) return (v / 1000000.0).ToString("0.0") + "M";
            if (a >= 10000) return (v / 1000.0).ToString("0.0") + "k";
            return ((int)System.Math.Floor(v)).ToString();
        }
        public static string Rate(float perMin)
        {
            if (Mathf.Abs(perMin) < 0.5f) return "^k+0/m";
            return (perMin > 0 ? "^g+" : "^r") + (Mathf.Abs(perMin) >= 1000 ? (perMin / 1000f).ToString("0.0") + "k" : Mathf.RoundToInt(perMin).ToString()) + "/m";
        }
    }
}
