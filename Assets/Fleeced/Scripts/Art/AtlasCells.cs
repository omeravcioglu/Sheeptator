namespace Fleeced
{
    /// The cells of the runtime atlas (Art/RuntimeAtlas.cs), kept free of UnityEngine so Tools/unit.sh can test them on plain .NET.
    /// Up to maxPages pages of cellsPerPage cells; a cell's index is page x cellsPerPage + slot. A pinned cell (a tail, a belt, an
    /// item...) is kept for good. An unpinned one (a sheep look) can be taken for a new sprite once nothing touches it, but never in
    /// a frame that touched it. Alloc takes, in this order: a free cell (lowest first), a cell untouched for StaleFrames, the first
    /// cell of a new page, the clock hand's next cell untouched for 2 frames (else one untouched this frame), and gives -1 when
    /// nothing is left. Nothing is allocated after the constructor.
    public sealed class AtlasCells
    {
        /// an unpinned cell untouched for this many frames is taken before a new page opens
        public const int StaleFrames = 3600;
        public readonly int CellsPerPage, MaxPages;

        readonly int[] lastTouch;
        readonly bool[] inUse, pinned;
        int pagesUsed, used, evictions;
        int freeCount, freeHint; // free cells in the opened pages; every cell below freeHint is in use
        int hand, staleHand;     // where the clock hand's scan and the search for a stale cell start
        int minTouch = int.MaxValue; // at or below the last touch of every unpinned cell in use

        public AtlasCells(int maxPages, int cellsPerPage)
        {
            MaxPages = maxPages > 0 ? maxPages : 0;
            CellsPerPage = cellsPerPage > 0 ? cellsPerPage : 0;
            int n = MaxPages * CellsPerPage;
            lastTouch = new int[n];
            inUse = new bool[n];
            pinned = new bool[n];
        }

        /// pages opened so far
        public int PagesUsed { get { return pagesUsed; } }
        /// cells taken from an earlier owner
        public int Evictions { get { return evictions; } }
        /// cells in use
        public int Used { get { return used; } }

        public bool InUse(int cell) { return cell >= 0 && cell < inUse.Length && inUse[cell]; }
        public bool Pinned(int cell) { return cell >= 0 && cell < pinned.Length && pinned[cell]; }

        /// a cell for a new sprite in `frame`, or -1 when every cell is pinned or touched this frame. evicted: the cell was taken from
        /// an earlier owner, whose sprite must go.
        public int Alloc(int frame, bool pin, out bool evicted)
        {
            int n = pagesUsed * CellsPerPage;
            // 1. a free cell of an opened page, the lowest first
            if (freeCount > 0)
            {
                for (int i = freeHint; i < n; i++)
                    if (!inUse[i]) { freeHint = i + 1; freeCount--; return Take(i, frame, pin, false, out evicted); }
                freeCount = 0; // (never: the count and the hint agree)
            }
            // 2. an unpinned cell untouched for StaleFrames. Only when the oldest touch says one can exist, from where the last one was
            // found; a turn that finds none records the oldest touch, so the next search waits until that one could be stale.
            long stale = (long)frame - StaleFrames;
            if (minTouch <= stale)
            {
                int oldest = int.MaxValue;
                for (int j = 0, i = staleHand < n ? staleHand : 0; j < n; j++)
                {
                    if (inUse[i] && !pinned[i])
                    {
                        if (lastTouch[i] <= stale) { staleHand = i + 1 < n ? i + 1 : 0; return Take(i, frame, pin, true, out evicted); }
                        if (lastTouch[i] < oldest) oldest = lastTouch[i];
                    }
                    if (++i == n) i = 0;
                }
                minTouch = oldest;
            }
            // 3. the first cell of a new page (the rest of it is free)
            if (pagesUsed < MaxPages && CellsPerPage > 0)
            {
                pagesUsed++;
                freeCount = CellsPerPage - 1;
                freeHint = n + 1;
                return Take(n, frame, pin, false, out evicted);
            }
            // 4. the clock hand: from where it stands, the first unpinned cell untouched for 2 frames, else the first untouched this frame
            long old = (long)frame - 2;
            int take = -1, recent = -1;
            for (int j = 0, i = hand < n ? hand : 0; j < n; j++)
            {
                if (inUse[i] && !pinned[i])
                {
                    if (lastTouch[i] <= old) { take = i; break; }
                    if (recent < 0 && lastTouch[i] < frame) recent = i;
                }
                if (++i == n) i = 0;
            }
            if (take < 0) take = recent;
            if (take >= 0)
            {
                hand = take + 1 < n ? take + 1 : 0;
                return Take(take, frame, pin, true, out evicted);
            }
            // 5. nothing can be had
            evicted = false;
            return -1;
        }

        int Take(int cell, int frame, bool pin, bool evict, out bool evicted)
        {
            if (!inUse[cell]) { inUse[cell] = true; used++; }
            pinned[cell] = pin;
            lastTouch[cell] = frame;
            if (!pin && frame < minTouch) minTouch = frame;
            if (evict) evictions++;
            evicted = evict;
            return cell;
        }

        /// the cell was drawn in `frame`: it is not taken in that frame, nor in the next while an older cell is left. Cells out of
        /// range and free cells are ignored.
        public void Touch(int cell, int frame)
        {
            if (cell < 0 || cell >= inUse.Length || !inUse[cell]) return;
            lastTouch[cell] = frame;
            if (!pinned[cell] && frame < minTouch) minTouch = frame;
        }

        /// gives a cell back to the free pool, unpinned. Cells out of range and free cells are ignored.
        public void Free(int cell)
        {
            if (cell < 0 || cell >= inUse.Length || !inUse[cell]) return;
            inUse[cell] = false;
            pinned[cell] = false;
            used--;
            freeCount++;
            if (cell < freeHint) freeHint = cell;
        }
    }
}
