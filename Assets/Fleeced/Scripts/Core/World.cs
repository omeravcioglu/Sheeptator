using System.Collections.Generic;
using UnityEngine;

namespace Fleeced
{
    public partial class Game
    {
        public const int MapW = 96, MapH = 72;
        public Tile[,] tiles;
        public RectInt owned;
        public int landLevel;
        public static readonly int[] LandCost = { 150, 400, 1000, 2200, 4500, 8000, 14000, 22000, 35000 };

        Texture2D groundTex;
        Color32[] tileBuf = new Color32[256];
        float groundRepaintT;
        GameObject worldRoot, propsRoot, fenceRoot, decalRoot;
        readonly List<GameObject> fence = new List<GameObject>();
        readonly List<KeyValuePair<Vector2Int, GameObject>> props = new List<KeyValuePair<Vector2Int, GameObject>>();
        readonly List<KeyValuePair<Vector2, GameObject>> clutter = new List<KeyValuePair<Vector2, GameObject>>();
        readonly List<Decal> decals = new List<Decal>();
        int decalOrder;
        public BuildingDef placing; public bool pathTool;
        GameObject ghost; SpriteRenderer ghostSr;
        Vector2Int ghostTile; bool ghostValid;

        static readonly Color32 GrassA = Pal.H("5a8a3c"), GrassB = Pal.H("4c7a33"), GrassC = Pal.H("6e9e45"),
            Dirt = Pal.H("7a5a3a"), DirtB = Pal.H("6a4c30"), DeadG = Pal.H("8a8448"),
            WildA = Pal.H("3a6530"), WildB = Pal.H("335a2b"), PathA = Pal.H("8a7458"), PathB = Pal.H("a08a68"), Snow = Pal.H("dfe8ee");

        void InitWorld()
        {
            worldRoot = new GameObject("World");
            propsRoot = new GameObject("Props"); propsRoot.transform.SetParent(worldRoot.transform);
            fenceRoot = new GameObject("Fence"); fenceRoot.transform.SetParent(worldRoot.transform);
            decalRoot = new GameObject("Decals"); decalRoot.transform.SetParent(worldRoot.transform);

            tiles = new Tile[MapW, MapH];
            for (int x = 0; x < MapW; x++) for (int y = 0; y < MapH; y++) tiles[x, y] = new Tile { grass = 0.7f + (float)rng.NextDouble() * 0.3f };

            groundTex = new Texture2D(MapW * 16, MapH * 16, TextureFormat.RGBA32, false);
            groundTex.filterMode = FilterMode.Point;
            groundTex.wrapMode = TextureWrapMode.Clamp;
            var g = new GameObject("Ground");
            g.transform.SetParent(worldRoot.transform);
            var sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(groundTex, new Rect(0, 0, groundTex.width, groundTex.height), Vector2.zero, 16f, 0, SpriteMeshType.FullRect);
            sr.sortingOrder = -32000;

            owned = new RectInt(36, 26, 24, 19);
            RepaintGround(true);
            BuildFence();
            SpawnProps();
        }

        public bool Owned(int x, int y) { return owned.Contains(new Vector2Int(x, y)); }
        public bool InMap(int x, int y) { return x >= 0 && y >= 0 && x < MapW && y < MapH; }
        public Tile TileAt(Vector2 p)
        {
            int x = Mathf.FloorToInt(p.x), y = Mathf.FloorToInt(p.y);
            return InMap(x, y) ? tiles[x, y] : null;
        }

        static uint Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263);
                h = (h ^ (h >> 13)) * 1274126177;
                return h ^ (h >> 16);
            }
        }

        int TileSig(int x, int y)
        {
            var t = tiles[x, y];
            int owned_ = Owned(x, y) ? 1 : 0;
            int gl = Mathf.Clamp(Mathf.RoundToInt(t.grass * (1 - t.wear * 0.85f) * 5), 0, 5);
            int dl = Mathf.Clamp(Mathf.RoundToInt(t.dead * 3), 0, 3);
            int b = t.building != null ? 1 : 0;
            int lot = t.pen != null && t.pen.def.kind == BKind.FeedLot ? 1 : 0;
            return gl | (dl << 3) | ((t.path ? 1 : 0) << 5) | (owned_ << 6) | (Season << 7) | (b << 9) | (lot << 10);
        }

        void PaintTile(int tx, int ty)
        {
            var t = tiles[tx, ty];
            bool own = Owned(tx, ty);
            int season = Season;
            float gAmt = t.grass * (1 - t.wear * 0.85f);
            for (int py = 0; py < 16; py++)
                for (int px = 0; px < 16; px++)
                {
                    int wx = tx * 16 + px, wy = ty * 16 + py;
                    uint h = Hash(wx, wy);
                    float n = (h & 1023) / 1023f;
                    float n2 = ((h >> 10) & 1023) / 1023f;
                    // low-frequency noise for blotches
                    uint hb = Hash(wx / 5, wy / 5);
                    float nb = (hb & 255) / 255f;
                    Color32 c;
                    bool isGrass = false;
                    if (!own)
                    {
                        c = Pal.Lerp(WildA, WildB, nb);
                        if (n < 0.08f) c = Pal.Shade(c, 1.2f);
                        if ((h % 211) == 0) c = Pal.H(n2 < 0.5f ? "d8c060" : "c87090");
                        isGrass = true;
                    }
                    else if (t.building != null)
                    {
                        c = Pal.Lerp(Dirt, DirtB, n);
                    }
                    else if (t.pen != null && t.pen.def.kind == BKind.FeedLot)
                    {
                        // trampled feeding-lot dirt with straw
                        c = Pal.Lerp(DirtB, Dirt, nb);
                        if (n < 0.16f) c = Pal.H("c8a050");
                        else if (n > 0.93f) c = Pal.H("a88040");
                    }
                    else if (t.path)
                    {
                        bool stone = ((wx / 4 + (wy / 3) % 2 * 2) % 3 != 0) && ((wy % 3) != 0);
                        c = stone ? Pal.Lerp(PathA, PathB, n * 0.7f) : Pal.Shade(PathA, 0.7f);
                    }
                    else
                    {
                        float thresh = gAmt * 1.15f - nb * 0.25f;
                        if (n2 < thresh)
                        {
                            c = Pal.Lerp(GrassA, GrassB, nb);
                            if (n < 0.1f) c = GrassC;
                            if (n > 0.95f) c = Pal.Shade(GrassB, 0.85f);
                            if (t.dead > 0.05f) c = Pal.Lerp(c, DeadG, Mathf.Clamp01(t.dead * 0.9f + n * 0.1f));
                            isGrass = true;
                        }
                        else
                        {
                            c = Pal.Lerp(Dirt, DirtB, nb);
                            if (n < 0.06f) c = Pal.Shade(Dirt, 1.18f);
                            if (n > 0.97f) c = Pal.H("8a8078");
                        }
                    }
                    if (isGrass)
                    {
                        if (season == 1) c = Pal.Lerp(c, Pal.H("9a9a40"), 0.12f);
                        else if (season == 2) c = Pal.Lerp(c, Pal.H("a07a30"), 0.32f);
                        else if (season == 3) c = Pal.Lerp(c, Snow, n < 0.75f ? 0.62f : 0.3f);
                    }
                    else if (season == 3 && n < 0.3f && t.building == null) c = Pal.Lerp(c, Snow, 0.5f);
                    tileBuf[py * 16 + px] = c;
                }
            groundTex.SetPixels32(tx * 16, ty * 16, 16, 16, tileBuf);
        }

        void RepaintGround(bool force)
        {
            bool any = false;
            for (int x = 0; x < MapW; x++)
                for (int y = 0; y < MapH; y++)
                {
                    int s = TileSig(x, y);
                    if (force || s != tiles[x, y].sig) { tiles[x, y].sig = s; PaintTile(x, y); any = true; }
                }
            if (any) groundTex.Apply(false, false);
        }

        void UpdateGrass(float dt)
        {
            float regrow = dt / DayLength * 0.16f * (tech.Contains("better_grass") ? 1.6f : 1f) * (flags.Contains("premium_seeds") ? 1.4f : 1f);
            float[] seasonMult = { 1.25f, 1f, 0.65f, 0.3f };
            regrow *= seasonMult[Season];
            float wearDecay = dt / DayLength * 0.12f;
            for (int x = owned.xMin; x < owned.xMax; x++)
                for (int y = owned.yMin; y < owned.yMax; y++)
                {
                    var t = tiles[x, y];
                    if (t.building != null || t.path) continue;
                    t.grass = Mathf.Min(1f, t.grass + regrow * (1 - t.dead * 0.8f) * (t.pen != null ? 3f : 1f)); // pens are tended pasture
                    t.wear = Mathf.Max(0, t.wear - wearDecay);
                    t.dead = Mathf.Max(0, t.dead - dt / DayLength * 0.03f);
                }
        }

        void PolluteAround(Building b, float dt)
        {
            int r = b.def.kind == BKind.Plant ? 4 : 2;
            float amt = dt / DayLength * (b.def.kind == BKind.Plant ? 0.25f : 0.12f);
            Vector2 c = b.Center;
            float maxD = Mathf.Max(b.def.w, b.def.h) / 2f + r;
            for (int x = b.x - r; x < b.x + b.def.w + r; x++)
                for (int y = b.y - r; y < b.y + b.def.h + r; y++)
                {
                    if (!InMap(x, y)) continue;
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c) / maxD;
                    float n = (Hash(x, y) & 255) / 255f;
                    float k = Mathf.Clamp01(1.15f - d - n * 0.45f);
                    float cap = Mathf.Clamp01(1.2f - d * 0.9f - n * 0.3f);
                    if (k > 0) tiles[x, y].dead = Mathf.Min(cap, tiles[x, y].dead + amt * k);
                }
        }

        void VisualWorldUpdate(float rdt)
        {
            groundRepaintT -= rdt;
            if (groundRepaintT <= 0) { groundRepaintT = 0.3f; RepaintGround(false); }
        }

        // ---------------- fence & props ----------------
        void BuildFence()
        {
            foreach (var f in fence) Destroy(f);
            fence.Clear();
            var r = owned;
            int gate = r.xMin + r.width / 2 - 1;
            for (int x = r.xMin; x < r.xMax; x++)
            {
                if (x < gate || x > gate + 1) AddFence(EnvArt.FenceH(rng.NextDouble() < 0.12), new Vector2(x, r.yMin - 0.15f));
                AddFence(EnvArt.FenceH(rng.NextDouble() < 0.12), new Vector2(x, r.yMax - 0.05f));
            }
            for (int y = r.yMin; y <= r.yMax; y++)
            {
                AddFence(EnvArt.FencePost(), new Vector2(r.xMin, y - 0.1f));
                AddFence(EnvArt.FencePost(), new Vector2(r.xMax, y - 0.1f));
            }
            // gate sign
            var sign = AddFence(EnvArt.Clutter(5), new Vector2(gate - 0.6f, r.yMin - 0.2f));
            sign.name = "GateSign";
        }

        GameObject AddFence(Sprite s, Vector2 p)
        {
            var go = new GameObject("fence");
            go.transform.SetParent(fenceRoot.transform);
            go.transform.position = new Vector3(p.x, p.y, 0);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s; sr.sortingOrder = SortY(p.y);
            fence.Add(go);
            return go;
        }

        void SpawnProps()
        {
            for (int x = 0; x < MapW; x++)
                for (int y = 0; y < MapH; y++)
                {
                    if (Owned(x, y))
                    {
                        if (rng.NextDouble() < 0.012) AddProp(x, y, EnvArt.Rock(rng.Next(2)));
                        continue;
                    }
                    // keep a clear ring of 1 tile around the fence
                    if (x >= owned.xMin - 1 && x <= owned.xMax && y >= owned.yMin - 1 && y <= owned.yMax) continue;
                    double r = rng.NextDouble();
                    if (r < 0.07) AddProp(x, y, EnvArt.Tree(rng.Next(3)));
                    else if (r < 0.085) AddProp(x, y, EnvArt.Rock(rng.Next(2)));
                    else if (r < 0.11) AddProp(x, y, EnvArt.Bush());
                }
        }

        void AddProp(int x, int y, Sprite s)
        {
            var go = new GameObject("prop");
            go.transform.SetParent(propsRoot.transform);
            Vector2 p = new Vector2(x + 0.5f + (float)(rng.NextDouble() - 0.5) * 0.4f, y + 0.2f);
            go.transform.position = p;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s; sr.sortingOrder = SortY(p.y);
            props.Add(new KeyValuePair<Vector2Int, GameObject>(new Vector2Int(x, y), go));
        }

        void ClearPropsIn(RectInt r)
        {
            for (int i = props.Count - 1; i >= 0; i--)
            {
                var k = props[i].Key;
                if (k.x >= r.xMin - 1 && k.x <= r.xMax && k.y >= r.yMin - 1 && k.y <= r.yMax) { Destroy(props[i].Value); props.RemoveAt(i); }
            }
        }

        void ClearClutterIn(int x, int y, int w, int h)
        {
            for (int i = clutter.Count - 1; i >= 0; i--)
            {
                var p = clutter[i].Key;
                if (p.x >= x - 0.3f && p.x <= x + w + 0.3f && p.y >= y - 0.5f && p.y <= y + h + 0.3f) { Destroy(clutter[i].Value); clutter.RemoveAt(i); }
            }
        }

        public void SpawnClutterNear(Building b)
        {
            int n = 1 + rng.Next(2) + (b.level >= 2 ? 1 : 0);
            for (int i = 0; i < n; i++)
            {
                for (int tries = 0; tries < 8; tries++)
                {
                    int side = rng.Next(3);
                    float px = side == 0 ? b.x - 0.4f : side == 1 ? b.x + b.def.w + 0.4f : b.x + (float)rng.NextDouble() * b.def.w;
                    float py = side == 2 ? b.y + b.def.h + 0.2f : b.y + (float)rng.NextDouble() * b.def.h;
                    int tx = Mathf.FloorToInt(px), ty = Mathf.FloorToInt(py);
                    if (!InMap(tx, ty) || !Owned(tx, ty) || tiles[tx, ty].building != null || tiles[tx, ty].pen != null || tiles[tx, ty].belt >= 0) continue;
                    var go = new GameObject("clutter");
                    go.transform.SetParent(propsRoot.transform);
                    go.transform.position = new Vector3(px, py, 0);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = EnvArt.Clutter(rng.Next(5));
                    sr.sortingOrder = SortY(py);
                    clutter.Add(new KeyValuePair<Vector2, GameObject>(new Vector2(px, py), go));
                    break;
                }
            }
        }

        public bool ExpandLand()
        {
            if (landLevel >= LandCost.Length) return false;
            int cost = LandCost[landLevel];
            if (res[(int)Res.Coins] < cost) return false;
            res[(int)Res.Coins] -= cost;
            landLevel++;
            int nx = Mathf.Max(1, owned.xMin - 3), ny = Mathf.Max(1, owned.yMin - 3);
            int nxMax = Mathf.Min(MapW - 1, owned.xMax + 3), nyMax = Mathf.Min(MapH - 1, owned.yMax + 3);
            owned = new RectInt(nx, ny, nxMax - nx, nyMax - ny);
            ClearPropsIn(owned);
            for (int x = owned.xMin; x < owned.xMax; x++) for (int y = owned.yMin; y < owned.yMax; y++) if (tiles[x, y].building == null) tiles[x, y].grass = 1f;
            BuildFence();
            RepaintGround(true);
            Sfx.Play("build"); Sfx.Play("fanfare", 0.6f);
            Log("^gLand expanded!^w The flock has more grass to ruin.");
            return true;
        }

        // ---------------- decals ----------------
        public void AddDecal(string kind, Vector2 p)
        {
            var go = new GameObject("decal");
            go.transform.SetParent(decalRoot.transform);
            go.transform.position = new Vector3(p.x, p.y, 0);
            if (rng.NextDouble() < 0.5) go.transform.localScale = new Vector3(-1, 1, 1);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = EnvArt.Decal(kind, rng.Next(3));
            sr.sortingOrder = -31000 + (decalOrder++ % 900);
            decals.Add(new Decal { go = go, born = simTime });
            if (decals.Count > 260) { Destroy(decals[0].go); decals.RemoveAt(0); }
        }

        // ---------------- placement ----------------
        public void BeginPlacing(BuildingDef d)
        {
            placing = d; pathTool = false;
            if (ghost == null)
            {
                ghost = new GameObject("ghost");
                ghost.transform.SetParent(worldRoot.transform);
                ghostSr = ghost.AddComponent<SpriteRenderer>();
                ghostSr.sortingOrder = 30000;
            }
        }

        /// Buildings may be dropped onto conveyors (the belt under them goes away). Pens need clear grass and a free gate tile.
        /// Only troughs and small decorations may go inside a pen.
        public bool CanPlace(BuildingDef d, int tx, int ty)
        {
            bool penOk = d.kind == BKind.Trough || d.kind == BKind.Flowers || d.kind == BKind.Sign;
            Building inPen = null;
            for (int x = tx; x < tx + d.w; x++)
                for (int y = ty; y < ty + d.h; y++)
                {
                    if (!InMap(x, y) || !Owned(x, y)) return false;
                    var t = tiles[x, y];
                    if (t.building != null) return false;
                    if (IsRoomKind(d.kind)) { if (t.belt >= 0 || t.pen != null) return false; continue; }
                    if (t.pen != null)
                    {
                        if (!penOk || (inPen != null && inPen != t.pen)) return false;
                        inPen = t.pen;
                    }
                }
            if (inPen != null)
                for (int x = tx; x < tx + d.w; x++) for (int y = ty; y < ty + d.h; y++) if (tiles[x, y].pen != inPen) return false;
            return true;
        }

        void UpdatePlacement(Vector2 m)
        {
            if (pathTool)
            {
                if (ghost != null) ghost.SetActive(false);
                if (Input.GetMouseButton(0) && !mouseOverUI)
                {
                    int tx = Mathf.FloorToInt(m.x), ty = Mathf.FloorToInt(m.y);
                    if (Owned(tx, ty) && !tiles[tx, ty].path && tiles[tx, ty].building == null && tiles[tx, ty].pen == null && res[0] >= 2)
                    {
                        tiles[tx, ty].path = true; res[0] -= 2;
                        Sfx.Play("click", 0.4f);
                        PaintTile(tx, ty); tiles[tx, ty].sig = TileSig(tx, ty); groundTex.Apply(false, false);
                    }
                }
                return;
            }
            if (ghost == null) return;
            ghost.SetActive(!mouseOverUI);
            var d = placing;
            int gx = Mathf.FloorToInt(m.x - d.w / 2f + 0.5f), gy = Mathf.FloorToInt(m.y - d.h / 2f + 0.5f);
            // snap onto the Supreme Leader's blueprint when close to it
            var bp = BlueprintFor(d.kind);
            if (bp != null && Mathf.Abs(gx - bp.x) <= 3 && Mathf.Abs(gy - bp.y) <= 3) { gx = bp.x; gy = bp.y; }
            ghostTile = new Vector2Int(gx, gy);
            ghostSr.sprite = BuildingArt.Get(d.kind, 1).sprite;
            ghost.transform.position = new Vector3(gx, gy, 0);
            bool afford = res[0] >= d.cost[0];
            ghostValid = CanPlace(d, gx, gy) && afford && !UniqueBlocked(d);
            ghostSr.color = ghostValid ? new Color(1, 1, 1, 0.7f) : new Color(1, 0.35f, 0.35f, 0.6f);
            if (Input.GetMouseButtonDown(0) && !mouseOverUI)
            {
                if (ghostValid)
                {
                    PlaceBuilding(d, gx, gy, false);
                    if (!Input.GetKey(KeyCode.LeftShift) || res[0] < d.cost[0] || UniqueBlocked(d)) { placing = null; ghost.SetActive(false); }
                }
                else
                {
                    Sfx.Play("click", 0.5f, 0.6f);
                    if (!afford) Toast("^rNot enough coins.");
                    else if (UniqueBlocked(d)) Toast("^rYou can only have one " + d.name + ".");
                    else Toast(IsRoomKind(d.kind) ? "^rIt needs free grass: no buildings, belts or other pens." : "^rCan't build there. Needs free owned land outside pens.");
                }
            }
        }

        public bool UniqueBlocked(BuildingDef d)
        {
            if (d.unique == 0) return false;
            foreach (var b in buildings) if (b.def == d) return true;
            return false;
        }

        void LateUpdate()
        {
            if (ghost != null && placing == null) ghost.SetActive(false);
        }
    }
}
