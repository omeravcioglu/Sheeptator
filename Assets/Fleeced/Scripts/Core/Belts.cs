using System.Collections.Generic;
using UnityEngine;

namespace Fleeced
{
    public partial class Game
    {
        public const int BeltCost = 5;
        public readonly List<BeltItem> items = new List<BeltItem>();
        readonly Dictionary<int, List<BeltItem>> itemsByTile = new Dictionary<int, List<BeltItem>>();
        GameObject beltRoot;
        public bool beltTool, clearTool;
        public int beltDir;
        Vector2Int lastBeltTile = new Vector2Int(-999, -999);
        int beltFrame; float beltFrameT;
        public int beltTiles, beltDelivered;
        GameObject beltGhost; SpriteRenderer beltGhostSr;

        public static Vector2Int Dir(int d)
        {
            switch (d) { case 0: return new Vector2Int(1, 0); case 1: return new Vector2Int(0, 1); case 2: return new Vector2Int(-1, 0); default: return new Vector2Int(0, -1); }
        }
        int Key(int x, int y) { return y * MapW + x; }
        /// base speed of a rubber belt in tiles per second
        public float BeltSpeed { get { return 1.5f * (tech.Contains("conveyors") ? 1.3f : 1f); } }

        // belt tiers: faster, and they carry sheep and goods closer together ("bigger")
        public static readonly string[] BeltTierNames = { "Rubber Belt", "Steel Belt", "Mega Belt" };
        public static readonly int[] BeltTierCost = { 5, 12, 30 };
        static readonly float[] TierSpeed = { 1f, 1.8f, 2.7f };
        static readonly float[] TierSheepGap = { 0.66f, 0.5f, 0.38f };
        static readonly float[] TierItemGap = { 0.5f, 0.4f, 0.3f };
        public int beltTierSel;
        public int MaxBeltTier { get { return tech.Contains("mega_belts") ? 2 : tech.Contains("express_belts") ? 1 : 0; } }
        float BeltSpeedOf(Tile t) { return BeltSpeed * TierSpeed[Mathf.Clamp(t.beltTier, 0, 2)]; }

        /// cost to bring every belt up to a tier
        public int UpgradeAllCost(int tier, out int tilesToDo)
        {
            int c = 0; tilesToDo = 0;
            for (int x = owned.xMin; x < owned.xMax; x++)
                for (int y = owned.yMin; y < owned.yMax; y++)
                {
                    var t = tiles[x, y];
                    if (t.belt < 0 || t.beltTier >= tier) continue;
                    c += BeltTierCost[tier] - BeltTierCost[t.beltTier]; tilesToDo++;
                }
            return c;
        }

        public void UpgradeAllBelts(int tier)
        {
            int n; int cost = UpgradeAllCost(tier, out n);
            if (n == 0) { Toast("^kEvery belt is already a " + BeltTierNames[tier] + "."); return; }
            if (res[0] < cost) { Toast("^rNeed " + cost + " coins to upgrade " + n + " belts."); return; }
            res[0] -= cost;
            for (int x = owned.xMin; x < owned.xMax; x++)
                for (int y = owned.yMin; y < owned.yMax; y++)
                {
                    var t = tiles[x, y];
                    if (t.belt < 0 || t.beltTier >= tier) continue;
                    t.beltTier = tier;
                    if (t.beltSr != null) t.beltSr.sprite = BuildingArt.Belt(t.belt, beltFrame, tier);
                }
            Sfx.Play("build"); Sfx.Play("fanfare", 0.4f);
            Log("^gUpgraded " + n + " conveyor tiles to " + BeltTierNames[tier] + "s.^w The sheep arrive faster. And closer together.");
        }

        public int BeltTilesAtLeast(int tier)
        {
            int n = 0;
            for (int x = owned.xMin; x < owned.xMax; x++) for (int y = owned.yMin; y < owned.yMax; y++) if (tiles[x, y].belt >= 0 && tiles[x, y].beltTier >= tier) n++;
            return n;
        }

        // ---------------- placing / removing ----------------
        /// lays or turns a belt. tier -1 keeps the existing tier (rubber for new belts); a higher tier upgrades it.
        public bool SetBelt(int x, int y, int dir, bool free, int tier = -1)
        {
            if (!InMap(x, y) || !Owned(x, y)) return false;
            var t = tiles[x, y];
            if (t.building != null || t.pen != null) return false;
            int planned = PlanDirAt(x, y);
            if (planned >= 0) dir = planned; // the Supreme Leader's blueprint knows best
            else if (!free && beltPlan.Count > 0) return false; // while he is drawing the plan, only his tiles
            int want = tier >= 0 ? Mathf.Clamp(tier, 0, 2) : (t.belt >= 0 ? t.beltTier : 0);
            if (t.belt >= 0 && want < t.beltTier) want = t.beltTier; // never downgrade by accident
            if (t.belt >= 0 && want > t.beltTier && !free)
            {
                int c = BeltTierCost[want] - BeltTierCost[t.beltTier];
                if (res[0] < c) { Toast("^rNot enough coins to upgrade that belt."); return false; }
                res[0] -= c;
            }
            if (t.belt < 0)
            {
                if (!free)
                {
                    if (res[0] < BeltTierCost[want]) { Toast("^rNot enough coins for belts."); return false; }
                    res[0] -= BeltTierCost[want];
                }
                if (beltRoot == null) { beltRoot = new GameObject("Belts"); beltRoot.transform.SetParent(worldRoot.transform); }
                t.beltGo = new GameObject("belt");
                t.beltGo.transform.SetParent(beltRoot.transform);
                t.beltGo.transform.position = new Vector3(x, y, 0);
                t.beltSr = t.beltGo.AddComponent<SpriteRenderer>();
                t.beltSr.sortingOrder = -30800;
                beltTiles++;
            }
            t.belt = dir; t.beltTier = want;
            t.beltSr.sprite = BuildingArt.Belt(dir, beltFrame, want);
            return true;
        }

        public void RemoveBelt(int x, int y, bool refund)
        {
            if (!InMap(x, y)) return;
            var t = tiles[x, y];
            if (t.belt < 0) return;
            t.belt = -1;
            if (t.beltGo != null) Destroy(t.beltGo);
            t.beltGo = null; t.beltSr = null;
            beltTiles--;
            if (refund) res[0] += BeltTierCost[t.beltTier] / 2;
            t.beltTier = 0;
            List<BeltItem> list;
            if (itemsByTile.TryGetValue(Key(x, y), out list))
            {
                foreach (var it in list.ToArray()) { res[(int)it.res] += it.amount; KillItem(it); }
            }
        }

        public void BeginBeltTool()
        {
            beltTool = true; clearTool = false; placing = null; pathTool = false;
            lastBeltTile = new Vector2Int(-999, -999);
        }

        void UpdateBeltTool(Vector2 m)
        {
            int tx = Mathf.FloorToInt(m.x), ty = Mathf.FloorToInt(m.y);
            if (Input.GetKeyDown(KeyCode.R)) beltDir = (beltDir + 1) % 4;
            if (beltGhost == null)
            {
                beltGhost = new GameObject("beltGhost");
                beltGhost.transform.SetParent(worldRoot.transform);
                beltGhostSr = beltGhost.AddComponent<SpriteRenderer>();
                beltGhostSr.sortingOrder = 30000;
            }
            beltGhost.SetActive(!mouseOverUI && InMap(tx, ty));
            beltGhost.transform.position = new Vector3(tx, ty, 0);
            beltGhostSr.sprite = clearTool ? EnvArt.Decal("scorch", 0) : BuildingArt.Belt(beltDir, beltFrame, beltTierSel);
            if (clearTool) beltGhost.transform.position = new Vector3(tx + 0.5f, ty + 0.5f, 0);
            bool okTile = InMap(tx, ty) && Owned(tx, ty) && tiles[tx, ty].building == null && tiles[tx, ty].pen == null;
            beltGhostSr.color = okTile ? new Color(1, 1, 1, 0.6f) : new Color(1, 0.3f, 0.3f, 0.5f);
            if (mouseOverUI) return;
            if (Input.GetMouseButtonDown(0)) lastBeltTile = new Vector2Int(-999, -999);
            if (!Input.GetMouseButton(0)) return;
            var cur = new Vector2Int(tx, ty);
            if (clearTool)
            {
                if (InMap(tx, ty))
                {
                    if (tiles[tx, ty].belt >= 0) { RemoveBelt(tx, ty, true); Sfx.Play("click", 0.4f, 0.7f); }
                    else if (tiles[tx, ty].path) { tiles[tx, ty].path = false; tiles[tx, ty].sig = -1; }
                }
                return;
            }
            if (cur == lastBeltTile) return;
            if (Mathf.Abs(cur.x - lastBeltTile.x) + Mathf.Abs(cur.y - lastBeltTile.y) == 1)
            {
                // dragging: point the previous belt at the new tile
                var d = cur - lastBeltTile;
                beltDir = d.x > 0 ? 0 : d.y > 0 ? 1 : d.x < 0 ? 2 : 3;
                if (InMap(lastBeltTile.x, lastBeltTile.y) && tiles[lastBeltTile.x, lastBeltTile.y].belt >= 0 && (beltPlan.Count == 0 || PlanDirAt(lastBeltTile.x, lastBeltTile.y) >= 0))
                    SetBelt(lastBeltTile.x, lastBeltTile.y, beltDir, true);
            }
            if (okTile && SetBelt(tx, ty, beltDir, false, beltTierSel)) Sfx.Play("click", 0.35f, 1.3f);
            lastBeltTile = cur;
        }

        // ---------------- items ----------------
        BeltItem SpawnItem(Res r, float amount, int x, int y, Vector2 from)
        {
            var it = new BeltItem { res = r, amount = amount, x = x, y = y, p = 0, from = from };
            it.go = new GameObject("item");
            it.go.transform.SetParent(beltRoot != null ? beltRoot.transform : worldRoot.transform);
            it.sr = it.go.AddComponent<SpriteRenderer>();
            it.sr.sprite = BuildingArt.Item(r);
            it.sr.sortingOrder = -30600;
            items.Add(it);
            AddToTile(it);
            return it;
        }

        void AddToTile(BeltItem it)
        {
            List<BeltItem> l;
            int k = Key(it.x, it.y);
            if (!itemsByTile.TryGetValue(k, out l)) { l = new List<BeltItem>(); itemsByTile[k] = l; }
            l.Add(it);
        }
        void RemoveFromTile(BeltItem it)
        {
            List<BeltItem> l;
            if (itemsByTile.TryGetValue(Key(it.x, it.y), out l)) l.Remove(it);
        }
        void KillItem(BeltItem it)
        {
            RemoveFromTile(it);
            items.Remove(it);
            if (it.go != null) Destroy(it.go);
        }

        float MinP(int x, int y)
        {
            List<BeltItem> l;
            float m = 9;
            if (itemsByTile.TryGetValue(Key(x, y), out l)) foreach (var o in l) m = Mathf.Min(m, o.p);
            return m;
        }

        Vector2 ExitPoint(int x, int y, int dir)
        {
            var d = Dir(dir);
            return new Vector2(x + 0.5f + d.x * 0.5f, y + 0.5f + d.y * 0.5f);
        }

        /// Puts a building's output on an adjacent belt pointing away from it. Returns false if none free.
        bool PushOut(Building b, Res r, float amount)
        {
            for (int pass = 0; pass < 2; pass++)
                for (int x = b.x - 1; x <= b.x + b.def.w; x++)
                    for (int y = b.y - 1; y <= b.y + b.def.h; y++)
                    {
                        bool edge = x == b.x - 1 || x == b.x + b.def.w || y == b.y - 1 || y == b.y + b.def.h;
                        bool corner = (x == b.x - 1 || x == b.x + b.def.w) && (y == b.y - 1 || y == b.y + b.def.h);
                        if (!edge || corner || !InMap(x, y)) continue;
                        var t = tiles[x, y];
                        if (t.belt < 0) continue;
                        var d = Dir(t.belt);
                        int nx = x + d.x, ny = y + d.y;
                        if (nx >= b.x && nx < b.x + b.def.w && ny >= b.y && ny < b.y + b.def.h) continue; // points back in
                        if (IsSheepMachine(b) && SideOf(b, x, y) >= 0 && SheepExitSide(b, SideOf(b, x, y))) continue; // an open door: that belt is for sheep
                        if (MinP(x, y) < TierItemGap[t.beltTier] - 0.05f) continue;
                        Vector2 side = new Vector2(x < b.x ? 1 : x >= b.x + b.def.w ? -1 : 0, y < b.y ? 1 : y >= b.y + b.def.h ? -1 : 0);
                        if (beltRoot == null) { beltRoot = new GameObject("Belts"); beltRoot.transform.SetParent(worldRoot.transform); }
                        SpawnItem(r, amount, x, y, new Vector2(x + 0.5f, y + 0.5f) + side * 0.5f);
                        return true;
                    }
            return false;
        }

        public bool HasOutputBelt(Building b)
        {
            for (int x = b.x - 1; x <= b.x + b.def.w; x++)
                for (int y = b.y - 1; y <= b.y + b.def.h; y++)
                {
                    if (!InMap(x, y) || tiles[x, y].belt < 0) continue;
                    if (x >= b.x && x < b.x + b.def.w && y >= b.y && y < b.y + b.def.h) continue;
                    var d = Dir(tiles[x, y].belt);
                    int nx = x + d.x, ny = y + d.y;
                    if (!(nx >= b.x && nx < b.x + b.def.w && ny >= b.y && ny < b.y + b.def.h)) return true;
                }
            return false;
        }

        /// Production output: onto a belt leading away if there is one, else straight into storage. True = on a belt.
        public bool Output(Building b, Res r, float amount)
        {
            totalMade[(int)r] += amount;
            if (b != null && PushOut(b, r, amount)) return true;
            res[(int)r] += amount;
            return false;
        }

        public bool HasIn(Building b, Res r, float n) { return b.inbuf[(int)r] + res[(int)r] >= n - 0.0001f; }
        public void UseIn(Building b, Res r, float n)
        {
            float local = Mathf.Min(b.inbuf[(int)r], n);
            b.inbuf[(int)r] -= local;
            res[(int)r] -= (n - local);
        }

        bool TryDeliver(Building b, BeltItem it)
        {
            if (b.construct < 1f && !b.upgrading) return false;
            if (!b.def.Accepts(it.res)) return false;
            switch (b.def.kind)
            {
                case BKind.Market:
                    {
                        float earned = SellPrice(it.res) * it.amount * 1.1f;
                        res[0] += earned; totalMade[0] += earned; totalSold[(int)it.res] += it.amount;
                        marketBeltSales++; beltCoins += earned;
                        priceMod[(int)it.res] = Mathf.Max(0.35f, priceMod[(int)it.res] * (1f - 0.002f * it.amount / Mathf.Max(1, (int)it.res)));
                        if (rng.NextDouble() < 0.3 && IsVisible(b.Center)) FloatText(b.Center + Vector2.up, "+" + Mathf.RoundToInt(earned) + "c", new Color(1f, 0.86f, 0.35f));
                        return true;
                    }
                case BKind.Storage:
                case BKind.House:
                case BKind.Trough:
                    res[(int)it.res] += it.amount;
                    return true;
            }
            if (b.inbuf[(int)it.res] >= 30) return false;
            b.inbuf[(int)it.res] += it.amount;
            b.beltFedT = 4f;
            return true;
        }

        void SimBelts(float dt)
        {
            if (items.Count == 0) return;
            for (int i = items.Count - 1; i >= 0; i--)
            {
                if (i >= items.Count) continue;
                var it = items[i];
                var t = tiles[it.x, it.y];
                if (t.belt < 0) { res[(int)it.res] += it.amount; KillItem(it); continue; }
                float sp = BeltSpeedOf(t) * dt, gap = TierItemGap[t.beltTier];
                float limit = 1f;
                List<BeltItem> same;
                if (itemsByTile.TryGetValue(Key(it.x, it.y), out same))
                    foreach (var o in same) if (o != it && o.p > it.p) limit = Mathf.Min(limit, o.p - gap);
                it.p = Mathf.Max(it.p, Mathf.Min(it.p + sp, limit));
                if (it.p < 1f) continue;
                it.p = 1f;
                var d = Dir(t.belt);
                int nx = it.x + d.x, ny = it.y + d.y;
                var nt = InMap(nx, ny) ? tiles[nx, ny] : null;
                if (nt != null && nt.building != null)
                {
                    var nb = nt.building;
                    if (HasBeltDoors(nb) && !MachineDoorOpen(nb, EntrySide(d))) continue; // a shut door: the goods wait on the belt
                    if (TryDeliver(nb, it)) { beltDelivered++; KillItem(it); continue; }
                }
                else if (nt != null && nt.belt >= 0 && (nt.belt + 2) % 4 != t.belt)
                {
                    if (MinP(nx, ny) >= TierItemGap[nt.beltTier])
                    {
                        RemoveFromTile(it);
                        it.from = ExitPoint(it.x, it.y, t.belt);
                        it.x = nx; it.y = ny; it.p = 0; it.stuckT = 0;
                        AddToTile(it);
                    }
                    continue;
                }
                // dead end, or nobody wants it: after a while it gets carried to storage by hoof
                it.stuckT += dt;
                if (it.stuckT > 4f) { res[(int)it.res] += it.amount; KillItem(it); }
            }
        }

        void UpdateBeltVisuals(float rdt, float dt)
        {
            if (dt > 0)
            {
                beltFrameT += dt * BeltSpeed * 4f;
                if (beltFrameT >= 1f)
                {
                    beltFrameT -= Mathf.Floor(beltFrameT);
                    beltFrame = (beltFrame + 1) % 4;
                    for (int x = owned.xMin; x < owned.xMax; x++)
                        for (int y = owned.yMin; y < owned.yMax; y++)
                        {
                            var t = tiles[x, y];
                            if (t.belt >= 0 && t.beltSr != null) t.beltSr.sprite = BuildingArt.Belt(t.belt, beltFrame, t.beltTier);
                        }
                }
            }
            foreach (var it in items)
            {
                var t = tiles[it.x, it.y];
                if (t.belt < 0) continue;
                Vector2 p = Vector2.Lerp(it.from, ExitPoint(it.x, it.y, t.belt), it.p);
                it.go.transform.position = new Vector3(p.x, p.y - 0.15f, 0);
            }
            if (beltGhost != null && !beltTool) beltGhost.SetActive(false);
        }

        // ---------------- sheep on belts ----------------
        public int marketBeltSales; public float beltCoins;
        public float trafficT; // sheep-seconds spent stuck in belt traffic (for the Supreme Leader's complaints)
        public float deadEndT; // sheep-seconds spent at the end of a conveyor that leads nowhere

        /// is another rider just in front of s on its belt?
        /// is another rider just in front of s on its belt? Only riders on s's own tile or on the next tile along
        /// its belt count, so a side belt waits for the main line at a merge instead of both waiting for each other.
        bool RiderAhead(Sheep s, Vector2Int d, float gap)
        {
            int tx = Mathf.FloorToInt(s.pos.x), ty = Mathf.FloorToInt(s.pos.y);
            foreach (var o in sheep)
            {
                if (o == s || !o.onBelt || o.dead) continue;
                Vector2 rel = o.pos - s.pos;
                float along = rel.x * d.x + rel.y * d.y;
                if (along <= 0.02f || along > gap) continue;
                if (Mathf.Abs(rel.x * d.y - rel.y * d.x) >= 0.45f) continue;
                int ox = Mathf.FloorToInt(o.pos.x), oy = Mathf.FloorToInt(o.pos.y);
                if ((ox == tx && oy == ty) || (ox == tx + d.x && oy == ty + d.y)) return true;
            }
            return false;
        }

        /// returns true if the sheep is being carried (or is queueing) on a conveyor this tick
        bool RideBelt(Sheep s, float dt)
        {
            s.onBelt = false;
            if (s.beltImmune > 0) { s.beltImmune -= dt; return false; }
            var t = TileAt(s.pos);
            if (t == null || t.belt < 0) { s.ridingT = 0; return false; }
            var d = Dir(t.belt);
            int tx = Mathf.FloorToInt(s.pos.x), ty = Mathf.FloorToInt(s.pos.y);
            s.onBelt = true;
            s.act = Act.Wander;
            s.walkPhase = 0;
            // centre on the belt lane
            if (d.x != 0) s.pos.y = Mathf.MoveTowards(s.pos.y, ty + 0.35f, dt * 2f);
            else s.pos.x = Mathf.MoveTowards(s.pos.x, tx + 0.5f, dt * 2f);
            if (d.x != 0) s.faceRight = d.x > 0;
            // what is just ahead?
            var nt = TileAt(s.pos + (Vector2)d * 0.42f);
            if (nt != null && nt != t)
            {
                if (nt.building != null)
                {
                    var b = nt.building;
                    if ((IsSheepMachine(b) || b.def.kind == BKind.Market) && (!b.Built || b.damaged))
                    {
                        b.refuseWaitT += dt;
                        HoldOnBelt(s, dt, "Waiting: the " + b.def.name + (b.damaged ? " is broken" : " is being built"));
                        return true;
                    }
                    if (b.def.kind == BKind.Market)
                    {
                        if (!MachineDoorOpen(b, EntrySide(d))) { b.doorWaitT += dt; HoldOnBelt(s, dt, "Waiting: the Market's door is SHUT"); return true; }
                        SellAtMarket(s, b);
                        return true;
                    }
                    if (IsSheepMachine(b))
                    {
                        if (!MachineDoorOpen(b, EntrySide(d)))
                        {
                            s.status = "Waiting: the " + b.def.name + "'s door is SHUT";
                            b.doorWaitT += dt;
                            s.ridingT = Mathf.Max(0, s.ridingT - dt);
                            return true;
                        }
                        if (b.inMachine == null) { EnterMachine(s, b, d); return true; }
                        s.status = "Queueing for the " + b.def.name;
                        return true;
                    }
                    // anything else takes no sheep: they wait at its wall (contained) and the Supreme Leader complains
                    b.refuseWaitT += dt;
                    HoldOnBelt(s, dt, "Waiting: the " + b.def.name + " does not take sheep");
                    return true;
                }
                if (nt.pen != null && nt.belt < 0)
                {
                    var room = nt.pen;
                    if (room.penCount >= RoomCapacity(room)) { s.status = "Waiting: the " + room.def.name + " is FULL"; s.ridingT = Mathf.Max(0, s.ridingT - dt); return true; }
                    DropIntoPen(s, room, s.pos + (Vector2)d * 0.9f);
                    return true;
                }
                if (nt.belt < 0)
                {
                    // the conveyor ends on bare ground: sheep never wander off, so it waits at the end
                    deadEndT += dt;
                    HoldOnBelt(s, dt, "Stuck at the END of the conveyor");
                    return true;
                }
            }
            else if (nt == null) { deadEndT += dt; HoldOnBelt(s, dt, "Stuck at the edge of the world"); return true; }
            if (RiderAhead(s, d, TierSheepGap[t.beltTier]))
            {
                s.status = "Stuck in sheep traffic"; trafficT += dt;
                s.jamT += dt;
                if (s.jamT < 5f) return true;   // after a long jam it squeezes past (never a permanent gridlock)
            }
            else s.jamT = 0;
            s.ridingT += dt;
            s.status = s.ridingT > 15 ? "Still on the conveyor. Help." : "Riding the conveyor";
            s.pos += (Vector2)d * BeltSpeedOf(t) * 0.9f * dt;
            return true;
        }

        /// a rider that cannot go on stays where it is, on the belt
        void HoldOnBelt(Sheep s, float dt, string why)
        {
            s.status = why;
            s.ridingT = Mathf.Max(0, s.ridingT - dt);
        }

        // ---------------- fluff funnel ----------------
        void SimFunnel(Building b, float dt)
        {
            if (!HasOutputBelt(b)) { b.status = "Needs a conveyor belt leading away from it"; ReleaseClient(b); return; }
            int maxClients = b.level;
            b.clients.RemoveAll(c => c.dead || c.summonedBy != b || (c.act != Act.Summoned && c.act != Act.BeingSheared));
            if (b.clients.Count < maxClients)
            {
                Sheep pick = null; float bd = 999999;
                foreach (var s in sheep)
                {
                    if (s.dead || s.Lamb || s.Busy || s.job != Job.Grazer || s.wool < 0.8f || IsClaimed(s)) continue;
                    float d = (s.pos - b.Center).sqrMagnitude;
                    if (d < bd) { bd = d; pick = s; }
                }
                if (pick != null) { pick.act = Act.Summoned; pick.summonedBy = b; b.clients.Add(pick); }
            }
            b.status = b.clients.Count > 0 ? "Loading " + b.clients[0].name : "Waiting for fluffy Grazers";
            foreach (var c in b.clients.ToArray())
            {
                if (c.act != Act.BeingSheared) continue;
                // drop onto the output belt
                for (int x = b.x - 1; x <= b.x + b.def.w; x++)
                    for (int y = b.y - 1; y <= b.y + b.def.h; y++)
                    {
                        if (!InMap(x, y) || tiles[x, y].belt < 0) continue;
                        if (x >= b.x && x < b.x + b.def.w && y >= b.y && y < b.y + b.def.h) continue;
                        var d = Dir(tiles[x, y].belt);
                        int nx = x + d.x, ny = y + d.y;
                        if (nx >= b.x && nx < b.x + b.def.w && ny >= b.y && ny < b.y + b.def.h) continue;
                        c.pos = new Vector2(x + 0.5f, y + 0.35f);
                        c.act = Act.Wander; c.summonedBy = null; c.beltImmune = 0; c.ridingT = 0;
                        b.clients.Remove(c);
                        b.cyclesDone++;
                        if (b.level == 3) { SpawnSmoke(c.pos, 1.2f); Sfx.Play("thud", 0.4f, 1.6f); }
                        Sfx.Baa(0.35f);
                        if (rng.NextDouble() < b.Danger * AccidentMult(c)) Accident(c, b);
                        goto next;
                    }
                next:;
            }
        }
    }
}
