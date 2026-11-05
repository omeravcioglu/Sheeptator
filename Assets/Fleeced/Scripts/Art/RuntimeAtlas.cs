using UnityEngine;

namespace Fleeced
{
    /// The runtime sprite atlas (M12): sheep looks, drove tails, belts, items, the shadow, chamber huts and lane doors are painted
    /// into at most 4 shared 1024 x 1024 pages instead of a Texture2D each, so neighbours on screen share a texture and batch.
    /// A page is a grid of 26 x 22 cells, 39 x 46 = 1,794 of them. A sprite of up to 24 x 20 sits at (1, 1) in its cell, inside a
    /// 1-px gutter that repeats its edge texels, so nothing seams. Art/AtlasCells.cs decides which cell: looks can be evicted (the
    /// owner hears of it through Evicted, and Epoch moves on so every sheep fetches its look again), everything else is pinned.
    /// Static, so it survives a world restart like the other art caches.
    public static class RuntimeAtlas
    {
        const int PageSize = 1024, CellW = 26, CellH = 22, MaxW = CellW - 2, MaxH = CellH - 2, MaxPages = 4;
        const int Cols = PageSize / CellW, Rows = PageSize / CellH, CellsPerPage = Cols * Rows; // 39 x 46 = 1,794

        /// the kill switch: when false, Put returns null and every caller keeps its own c.ToSprite()
        public static bool Enabled = true;
        /// hears the key of a look whose cell went to another sprite (the sheep look cache sets it, to drop that look)
        public static System.Action<long> Evicted;
        /// the visual frame, counted up once a frame by the game: a cell touched in the current frame is never taken
        public static int Frame;
        /// moves on with every eviction: a look fetched in an older epoch may have lost its cell, so its sheep fetches it again
        public static int Epoch;
        /// sprites that found no cell (the caller fell back to a texture of its own); tests demand 0
        public static int Overflow;

        static AtlasCells cells = new AtlasCells(MaxPages, CellsPerPage);
        static readonly Texture2D[] pages = new Texture2D[MaxPages];
        static readonly bool[] dirty = new bool[MaxPages];
        static bool anyDirty;
        static readonly long[] keys = new long[MaxPages * CellsPerPage];
        static readonly Sprite[] sprites = new Sprite[MaxPages * CellsPerPage];
        static int pinnedUsed;
        static readonly Color32[] block = new Color32[CellW * CellH];
        static Texture2D staging; static int gpuCopy = -1; // a cell's block on its way to the GPU, and whether CopyTexture works here

        /// pages opened so far
        public static int Pages { get { return cells.PagesUsed; } }
        /// unpinned cells in use (sheep looks)
        public static int Looks { get { return cells.Used - pinnedUsed; } }
        /// cells taken from an earlier look
        public static int Evictions { get { return cells.Evictions; } }

        /// Paints canvas c into a cell and returns its sprite (pivot as in ToSprite, 16 px a unit), or null when the atlas is off,
        /// c is bigger than 24 x 20 or no cell can be had: the caller then keeps c.ToSprite(). key names the sprite to its owner
        /// (Evicted hands it back), pin keeps the cell for good, and cell is what to Touch in every frame the sprite is drawn (-1
        /// with null).
        public static Sprite Put(PixelCanvas c, Vector2 pivot, bool pin, long key, out int cell)
        {
            cell = -1;
            if (!Enabled || c == null || c.w < 1 || c.h < 1 || c.w > MaxW || c.h > MaxH) return null;
            bool evicted;
            int at = cells.Alloc(Frame, pin, out evicted);
            if (at < 0) { Overflow++; return null; }
            Sprite old = sprites[at];
            long oldKey = keys[at];
            int p = at / CellsPerPage, slot = at % CellsPerPage;
            if (pages[p] == null) { pages[p] = NewPage(p); dirty[p] = true; anyDirty = true; } // a new page goes up whole, once
            int cx = slot % Cols * CellW, cy = slot / Cols * CellH, w = c.w, h = c.h;
            // the 26 x 22 block, rows bottom-up like c.px: the sprite at (1, 1), its edge texels repeated around it, the rest clear
            for (int i = 0; i < block.Length; i++) block[i] = Pal.Clear;
            for (int by = 0; by < h + 2; by++)
            {
                int row = (by == 0 ? 0 : by > h ? h - 1 : by - 1) * w;
                for (int bx = 0; bx < w + 2; bx++) block[by * CellW + bx] = c.px[row + (bx == 0 ? 0 : bx > w ? w - 1 : bx - 1)];
            }
            pages[p].SetPixels32(cx, cy, CellW, CellH, block); // the CPU copy: tests read it, and a whole upload sends it
            if (dirty[p] || !GpuCopy()) { dirty[p] = true; anyDirty = true; }
            else
            {
                // only this cell goes to the GPU (2.3 KB), not the whole 4 MB page
                staging.SetPixels32(block);
                staging.Apply(false, false);
                Graphics.CopyTexture(staging, 0, 0, 0, 0, CellW, CellH, pages[p], 0, 0, cx, cy);
            }
            var s = Sprite.Create(pages[p], new Rect(cx + 1, cy + 1, w, h), pivot, 16f, 0, SpriteMeshType.FullRect);
            sprites[at] = s;
            keys[at] = key;
            if (pin) pinnedUsed++;
            cell = at;
            if (evicted)
            {
                Epoch++;
                if (old != null) Object.Destroy(old); // the sprite only: the page stays
                Evicted?.Invoke(oldKey);
            }
            return s;
        }

        /// true where the GPU can copy a texture region (any desktop graphics device), with the staging block made
        static bool GpuCopy()
        {
            if (gpuCopy < 0)
            {
                gpuCopy = (SystemInfo.copyTextureSupport & UnityEngine.Rendering.CopyTextureSupport.Basic) != 0 ? 1 : 0;
                if (gpuCopy == 1) staging = new Texture2D(CellW, CellH, TextureFormat.RGBA32, false) { name = "atlas staging", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            }
            return gpuCopy == 1;
        }

        static Texture2D NewPage(int i)
        {
            var t = new Texture2D(PageSize, PageSize, TextureFormat.RGBA32, false);
            t.name = "atlas" + i;
            t.filterMode = FilterMode.Point;
            t.wrapMode = TextureWrapMode.Clamp;
            var px = t.GetRawTextureData<Color32>(); // start transparent, whatever a new texture holds
            for (int j = 0; j < px.Length; j++) px[j] = Pal.Clear;
            return t;
        }

        /// the sprite in this cell is drawn this frame, so the clock hand leaves it alone (-1, a standalone sprite, is ignored)
        public static void Touch(int cell)
        {
            if (cell >= 0) cells.Touch(cell, Frame);
        }

        /// uploads every page painted since the last flush without a cell copy (a new page, or no CopyTexture), once each
        /// (World's LateUpdate, after the frame's Puts)
        public static void Flush()
        {
            if (!anyDirty) return;
            anyDirty = false;
            for (int i = 0; i < MaxPages; i++)
            {
                if (!dirty[i]) continue;
                dirty[i] = false;
                if (pages[i] != null) pages[i].Apply(false, false); // kept readable
            }
        }

        /// Flush for callers that run many times a frame (UI portraits in OnGUI): one test when nothing was painted
        public static void FlushIfDirty()
        {
            if (anyDirty) Flush();
        }

        /// true for one of the atlas pages
        public static bool IsPage(Texture t)
        {
            if (t == null) return false;
            for (int i = 0; i < MaxPages; i++) if (pages[i] != null && pages[i] == t) return true;
            return false;
        }

        /// page i, or null when it is not opened (tests read its pixels)
        public static Texture2D Page(int i)
        {
            return i >= 0 && i < MaxPages ? pages[i] : null;
        }
    }
}
