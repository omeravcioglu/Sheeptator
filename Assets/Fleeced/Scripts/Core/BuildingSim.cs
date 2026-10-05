using System.Collections.Generic;
using UnityEngine;

namespace Fleeced
{
    public partial class Game
    {
        GameObject buildRoot;
        public int popCap;
        public float storageCap = 200;
        public float powerMade, powerNeed, powerFactor = 1f;
        public float guardPower;
        public float decorMood;
        public bool[] autoSell = new bool[ResInfo.Count];
        readonly List<GameObject> tombs = new List<GameObject>();

        public static readonly string[][] Recipes =
        {
            new[] { "Auto", "Wash Wool", "Spin Yarn" },        // workshop
            new[] { "Auto", "Weave Fabric", "Sew Clothing" },  // textile
        };

        public static readonly float[] ShedYield = { 1f, 1.3f, 1.7f, 2.2f, 3f };
        public static readonly float[] BarnSpeed = { 1f, 1.2f, 1.5f, 2.2f, 3f };
        public int HouseBeds(int lv) { return BuildingDef.At(new[] { 4, 7, 12, 20, 32 }, lv) + (tech.Contains("bunk_beds") ? 2 : 0); }
        public float StorageCapOf(int lv) { return BuildingDef.At(new float[] { 150, 350, 700, 1500, 3000 }, lv); }
        public float WheelPower(int lv) { return BuildingDef.At(new float[] { 4, 6, 9, 13, 18 }, lv); }
        public float FarmOut(Building b) { return 10f * b.LM * (tech.Contains("fertilizer") ? 1.3f : 1f); }

        // ---------------- placement / lifecycle ----------------
        public Building PlaceBuilding(BuildingDef d, int tx, int ty, bool instant)
        {
            if (!instant) res[0] -= d.cost[0];
            bool pen = IsRoomKind(d.kind);
            var b = new Building { id = NextId(), def = d, x = tx, y = ty, construct = instant || pen ? 1f : 0f };
            for (int x = tx; x < tx + d.w; x++)
                for (int y = ty; y < ty + d.h; y++)
                {
                    if (pen) { tiles[x, y].pen = b; tiles[x, y].path = false; continue; }
                    if (tiles[x, y].belt >= 0) RemoveBelt(x, y, false);
                    tiles[x, y].building = b; tiles[x, y].grass = 0; tiles[x, y].path = false;
                }
            ClearClutterIn(tx, ty, d.w, d.h);
            if (!instant) blueprints.RemoveAll(bp => bp.kind == d.kind);
            buildings.Add(b);
            if (buildRoot == null) { buildRoot = new GameObject("Buildings"); buildRoot.transform.SetParent(worldRoot.transform); }
            b.go = new GameObject(d.name);
            b.go.transform.SetParent(buildRoot.transform);
            b.go.transform.position = new Vector3(tx, ty, 0);
            b.sr = b.go.AddComponent<SpriteRenderer>();
            var sc = new GameObject("scaffold");
            sc.transform.SetParent(b.go.transform, false);
            b.scaffold = sc.AddComponent<SpriteRenderer>();
            b.scaffold.sprite = BuildingArt.Scaffold(d.w, d.h);
            RefreshBuildingSprite(b);
            if (!instant)
            {
                Sfx.Play("build");
                SpawnParticles(b.Center, new Color(0.6f, 0.5f, 0.38f), 16, 2.5f);
                Log("Construction started: ^y" + d.name + "^w. It builds itself.");
            }
            if (d.kind == BKind.Rocket) SpawnRocket(b);
            if (pen)
            {
                SpawnPenFence(b);
                // homeless grazers move in
                foreach (var s in sheep) if (s.pen == null) AssignPen(s);
            }
            if (d.kind == BKind.Palace)
            {
                var lg = new GameObject("SupremeLeader");
                lg.transform.SetParent(b.go.transform, false);
                lg.transform.localPosition = new Vector3(LeaderArt.BalconyOffset.x, LeaderArt.BalconyOffset.y, 0);
                var lsr = lg.AddComponent<SpriteRenderer>();
                lsr.sprite = LeaderArt.Balcony(0);
                lsr.sortingOrder = SortY(b.y + 0.05f) + 2;
                b.extras.Add(lg);
            }
            return b;
        }

        public void RefreshBuildingSprite(Building b)
        {
            b.art = BuildingArt.Get(b.def.kind, b.level);
            b.sr.sprite = IsRoom(b) ? null : b.art.sprite; // rooms are drawn as separate fence pieces
            b.sr.sortingOrder = SortY(b.y + 0.05f);
            b.scaffold.sortingOrder = b.sr.sortingOrder + 1;
            bool building = b.construct < 1f;
            b.scaffold.gameObject.SetActive(building && !IsRoom(b));
            b.sr.color = (building && !b.upgrading && !b.damaged) ? new Color(1, 1, 1, 0.35f) : Color.white;
            if (b.damaged) b.sr.color = new Color(0.55f, 0.5f, 0.5f);
        }

        public void Demolish(Building b)
        {
            int refund = b.def.cost[0] / 2;
            res[0] += refund;
            foreach (var w in b.workers.ToArray()) { RemoveFromJob(w); w.job = Job.Grazer; }
            for (int i = 0; i < ResInfo.Count; i++) res[i] += b.inbuf[i];
            if (b.client != null) { b.client.act = Act.Wander; b.client = null; }
            foreach (var c in b.clients) c.act = Act.Wander;
            EjectFromMachine(b);
            var homeless = new List<Sheep>();
            if (IsRoom(b)) { foreach (var s in sheep) if (s.pen == b) homeless.Add(s); ClearPen(b); }
            else for (int x = b.x; x < b.x + b.def.w; x++) for (int y = b.y; y < b.y + b.def.h; y++) tiles[x, y].building = null;
            foreach (var e in b.extras) Destroy(e);
            Destroy(b.go);
            buildings.Remove(b);
            foreach (var s in homeless) AssignPen(s);
            foreach (var s in sheep) if (s.workplace == b) s.workplace = null;
            if (selBuilding == b) selBuilding = null;
            SpawnParticles(b.Center, new Color(0.6f, 0.5f, 0.38f), 24, 3f);
            Sfx.Play("crunch");
            Log("Demolished ^y" + b.def.name + "^w. Refunded " + refund + " coins.");
        }

        public string UpgradeBlocker(Building b)
        {
            if (b.level >= b.def.maxLevel) return "Max level";
            if (b.construct < 1f) return "Under construction";
            string t = b.def.levelTech[b.level];
            if (t != null && !tech.Contains(t)) return "Research: " + Research.Get(t).name;
            if (res[0] < b.def.cost[b.level]) return "Need " + b.def.cost[b.level] + " coins";
            Res r1, r2;
            int rn = RareUpgradeNeed(b, out r1, out r2);
            if (rn > 0 && (res[(int)r1] < rn || res[(int)r2] < rn)) return "Need " + RareUpgradeText(b) + " (DNA-altered sheep make them)";
            return null;
        }

        public void Upgrade(Building b)
        {
            if (UpgradeBlocker(b) != null) return;
            EjectFromMachine(b);
            res[0] -= b.def.cost[b.level];
            {
                Res r1, r2;
                int rn = RareUpgradeNeed(b, out r1, out r2);
                if (rn > 0) { res[(int)r1] -= rn; res[(int)r2] -= rn; }
            }
            b.upgrading = true;
            b.construct = 0f;
            RefreshBuildingSprite(b);
            Sfx.Play("build");
            Log("Upgrading ^y" + b.def.name + "^w to " + b.def.levelNames[b.level] + ".");
        }

        public bool HasBuilding(BKind k) { foreach (var b in buildings) if (b.def.kind == k && b.construct >= 1f || b.def.kind == k && b.upgrading) return true; return false; }
        public bool HasBuildingLevel(BKind k, int lv) { foreach (var b in buildings) if (b.def.kind == k && b.level >= lv && (b.Built || b.upgrading)) return true; return false; }
        public int CountBuildings() { int n = 0; foreach (var b in buildings) if (!b.def.decor && b.Built) n++; return n; }
        public int MaxBuildingLevel() { int m = 0; foreach (var b in buildings) if (b.Built) m = Mathf.Max(m, b.level); return m; }

        // ---------------- globals ----------------
        void ComputeGlobals()
        {
            popCap = 0; storageCap = 200; powerMade = 0; powerNeed = 0; guardPower = 0; float decor = 0, flowers = 0;
            foreach (var b in buildings)
            {
                bool ok = b.Built || b.upgrading;
                int lv = b.upgrading ? b.level : b.level;
                if (!ok) continue;
                switch (b.def.kind)
                {
                    case BKind.Pen: popCap += RoomCapacity(b); break; // the flock lives in its pens
                    case BKind.Storage: storageCap += StorageCapOf(lv) * 1.25f; break;
                    case BKind.Spa: decor += lv == 1 ? 4 : lv == 2 ? 7 : 10; break;
                    case BKind.Statue: decor += lv == 1 ? 6 : lv == 2 ? 9 : 12; break;
                    case BKind.Graveyard: decor += lv == 1 ? 0 : lv == 2 ? 2 : 4; break;
                    case BKind.Flowers: flowers += 1; break;
                }
                if (b.Built)
                {
                    float need = BuildingDef.At(b.def.power, b.level);
                    if (need > 0) powerNeed += need;
                }
            }
            decorMood = decor + Mathf.Min(10, flowers);
            if (flags.Contains("cult_joined")) decorMood += 0; // handled by mods
            foreach (var b in buildings)
            {
                if (!b.Built) continue;
                if (b.def.kind == BKind.Wheel) powerMade += WheelPower(b.level) * AutoCrew(b);
                if (b.def.kind == BKind.GuardPost) guardPower += AutoCrew(b) * (1 + 0.5f * (b.level - 1));
            }
            StrainGlobals();
            if (flags.Contains("gerald_guard")) guardPower += 2f;
            powerFactor = powerNeed <= 0 ? 1f : Mathf.Clamp01(powerMade / powerNeed);
            globalMods.RemoveAll(m => m.until < Day);

            // housing: the newest sheep beyond capacity are homeless
            int housed = 0;
            foreach (var s in sheep)
            {
                if (s.dead || s.act == Act.Hidden) { s.homeless = false; continue; }
                housed++;
                s.homeless = housed > popCap;
            }
        }

        void ClampStorage()
        {
            for (int i = 1; i < ResInfo.Count; i++)
            {
                float cap = i == (int)Res.Food ? storageCap + 100 : storageCap;
                if (res[i] > cap) { res[i] = cap; storageFull = true; }
                if (res[i] < 0) res[i] = 0;
            }
        }
        public bool storageFull;
        public float CapOf(Res r) { return r == Res.Food ? storageCap + 100 : r == Res.Coins ? float.MaxValue : storageCap; }

        public void Gain(Res r, double amt)
        {
            res[(int)r] += amt;
            if (amt > 0) totalMade[(int)r] += amt;
        }

        // ---------------- per-tick building sim ----------------
        /// Sheep never work: every building runs by itself. This is the crew a level provides.
        public float AutoCrew(Building b)
        {
            if (!b.Built) return 0;
            float c = Mathf.Max(1f, b.Slots * 0.6f);
            if (tech.Contains("automation")) c *= 1.25f;
            return c;
        }
        float EffSum(Building b, Cat cat) { return AutoCrew(b); }
        public static bool IsProducer(Building b)
        {
            switch (b.def.kind) { case BKind.Farm: case BKind.Workshop: case BKind.Textile: case BKind.Atelier: case BKind.Lab: case BKind.Creamery: return true; }
            return false;
        }

        float Mult(Building b)
        {
            float m = 1f;
            if (b.unsafeMode) m *= 1.35f;
            if (b.featureMode) m *= 1.2f;
            if (b.buffUntil > Day) m *= b.buffMult;
            if (tech.Contains("mass_production")) m *= 1.15f;
            float need = BuildingDef.At(b.def.power, b.level);
            if (need > 0) m *= 0.25f + 0.75f * powerFactor;
            if (b.beltFedT > 0) m *= 1.5f;
            if (flags.Contains("union_break") && b.def.job != Job.Grazer) m *= 0.92f;
            return m;
        }

        void SimBuilding(Building b, float dt)
        {
            // construction
            if (b.construct < 1f)
            {
                float t = BuildingDef.At(b.def.buildTime, b.upgrading ? b.level + 1 : b.level);
                if (b.damaged) t = 20;
                b.construct += dt / t * 1.45f; // builds itself
                b.status = b.damaged ? "Being repaired" : b.upgrading ? "Upgrading" : "Under construction";
                if (b.construct >= 1f) FinishConstruction(b);
                else if (IsRoom(b) && b.upgrading) SimPen(b, dt); // rooms keep working while they grow
                return;
            }
            if (b.damaged) { if (b.inMachine != null) EjectFromMachine(b); return; }
            if (b.beltFedT > 0) b.beltFedT -= dt;
            float mult = Mult(b);
            switch (b.def.kind)
            {
                case BKind.Farm: Produce(b, dt, Cat.Farm, 6f, mult, () =>
                    {
                        float amt = FarmOut(b);
                        Output(b, Res.Food, amt); b.lastCycleOut = amt; return true;
                    }, "Growing food"); break;
                case BKind.Workshop: SimWorkshop(b, dt, mult); break;
                case BKind.Textile: SimTextile(b, dt, mult); break;
                case BKind.Atelier: Produce(b, dt, Cat.Craft, 14f, mult * b.LM, () =>
                    {
                        if (!HasIn(b, Res.Clothing, 2) || !HasIn(b, Res.Wool, 4)) { b.status = "Needs 2 Clothing + 4 Wool"; return false; }
                        UseIn(b, Res.Clothing, 2); UseIn(b, Res.Wool, 4); Output(b, Res.Luxury, 1); return true;
                    }, "Making couture"); break;
                case BKind.Lab: Produce(b, dt, Cat.Research, 5f, mult * b.LM, () => { AddRP(1.5f); return true; }, "Doing science"); break;
                case BKind.Market: SimMarket(b, dt, mult); break;
                case BKind.Shed: SimMachine(b, dt, mult); break;
                case BKind.Plant: SimMachine(b, dt, mult); PolluteAround(b, dt); break;
                case BKind.Barn: SimBarn(b, dt); break;
                case BKind.Dairy: SimMachine(b, dt, mult); break;
                case BKind.Pen: SimPen(b, dt); break;
                case BKind.FeedLot: SimPen(b, dt); break;
                case BKind.Slaughter: SimMachine(b, dt, mult); break;
                case BKind.GeneLab: SimMachine(b, dt, mult); break;
                case BKind.Creamery: Produce(b, dt, Cat.Craft, 6f, mult * b.LM, () =>
                    {
                        if (!HasIn(b, Res.Milk, 3)) { b.status = "Needs 3 Milk (milk some ewes!)"; return false; }
                        UseIn(b, Res.Milk, 3); Output(b, Res.Cheese, 1); return true;
                    }, "Making cheese"); break;
                case BKind.Funnel: SimFunnel(b, dt); break;
                case BKind.Wheel:
                case BKind.GuardPost:
                case BKind.Storage:
                case BKind.Trading:
                case BKind.Bank:
                case BKind.Rocket:
                    {
                        b.status = "Operating";
                        if (b.def.kind == BKind.Wheel) b.status = "Generating " + Mathf.RoundToInt(WheelPower(b.level) * AutoCrew(b)) + " power";
                        if (b.def.kind == BKind.Rocket) b.status = LaunchBlocker(b) ?? "Ready to launch";
                        break;
                    }
            }
            if (b.level == 3 && (b.def.kind == BKind.Textile || b.def.kind == BKind.Workshop || b.def.kind == BKind.Shed || b.def.kind == BKind.Farm)) PolluteAround(b, dt * 0.5f);
        }

        void FinishConstruction(Building b)
        {
            b.construct = 1f;
            bool wasRepair = b.damaged;
            b.damaged = false;
            if (b.upgrading) { b.upgrading = false; b.level++; }
            RefreshBuildingSprite(b);
            SpawnParticles(b.Center + Vector2.up * 0.5f, new Color(0.9f, 0.85f, 0.6f), 20, 3f);
            Sfx.Play("build");
            if (wasRepair) Log("^g" + b.def.name + " repaired.^w Blood stains remain.");
            else if (b.level > 1) { Log("^g" + b.def.name + " upgraded:^y " + b.Title + "^w!"); Sfx.Play("fanfare", 0.5f); }
            else Log("^g" + b.def.name + " finished!" + (IsSheepMachine(b) ? "^w Sheep riding a conveyor into it get processed." : IsRoom(b) ? "" : "^w It runs by itself."));
            if (IsRoom(b)) SpawnPenFence(b); // new level, new fence
            else if (!wasRepair) SpawnClutterNear(b);
        }

        public bool Has(Res r, double n) { return res[(int)r] >= n - 0.0001; }
        public void Use(Res r, double n) { res[(int)r] -= n; }

        void Produce(Building b, float dt, Cat cat, float workTime, float mult, System.Func<bool> cycle, string okStatus)
        {
            float e = EffSum(b, cat);
            if (e <= 0) { b.status = "Not running"; return; }
            b.progress += e * mult * dt;
            if (b.status == "" || b.status == "Not running") b.status = okStatus;
            if (b.progress >= workTime)
            {
                if (cycle())
                {
                    b.progress -= workTime;
                    b.cyclesDone++;
                    b.status = okStatus;
                    CycleFx(b);
                }
                else b.progress = workTime; // wait for inputs
            }
        }

        void CycleFx(Building b)
        {
            if (!IsVisible(b.Center)) return;
            switch (b.def.kind)
            {
                case BKind.Farm: FloatText(b.Center + Vector2.up, "+" + Mathf.RoundToInt(b.lastCycleOut) + " FOOD", new Color(0.6f, 0.95f, 0.4f)); break;
                case BKind.Lab: SpawnParticles(b.Center + Vector2.up, new Color(0.5f, 1f, 0.6f), 4, 1.5f); break;
                default: SpawnParticles(b.Door + Vector2.up * 0.5f, new Color(0.95f, 0.93f, 0.88f), 4, 1.5f); break;
            }
        }

        void SimWorkshop(Building b, float dt, float mult)
        {
            float lm = b.LM;
            bool canSpin = tech.Contains("yarn");
            Produce(b, dt, Cat.Wool, 5f, mult * lm, () =>
            {
                bool spin = b.recipe == 2 || (b.recipe == 0 && canSpin && HasIn(b, Res.CleanWool, 2) && (b.inbuf[(int)Res.CleanWool] + res[(int)Res.CleanWool] >= (b.inbuf[(int)Res.Wool] + res[(int)Res.Wool]) * 0.5f || !HasIn(b, Res.Wool, 2)));
                if (spin && canSpin)
                {
                    if (!HasIn(b, Res.CleanWool, 2)) { b.status = "Needs Clean Wool"; return false; }
                    UseIn(b, Res.CleanWool, 2); Output(b, Res.Yarn, 1); b.status = "Spinning yarn"; return true;
                }
                if (!HasIn(b, Res.Wool, 2)) { b.status = "Needs Wool (shear sheep!)"; return false; }
                UseIn(b, Res.Wool, 2); Output(b, Res.CleanWool, 1); b.status = "Washing wool"; return true;
            }, "Processing wool");
        }

        void SimTextile(Building b, float dt, float mult)
        {
            float lm = b.LM;
            bool canSew = tech.Contains("loom");
            Produce(b, dt, Cat.Industry, 8f, mult * lm, () =>
            {
                bool sew = b.recipe == 2 || (b.recipe == 0 && canSew && HasIn(b, Res.Fabric, 2) && (b.inbuf[(int)Res.Fabric] + res[(int)Res.Fabric] >= (b.inbuf[(int)Res.Yarn] + res[(int)Res.Yarn]) * 0.5f || !HasIn(b, Res.Yarn, 2)));
                if (sew && canSew)
                {
                    if (!HasIn(b, Res.Fabric, 2)) { b.status = "Needs Fabric"; return false; }
                    UseIn(b, Res.Fabric, 2); Output(b, Res.Clothing, 1); b.status = "Sewing clothing"; return true;
                }
                if (!HasIn(b, Res.Yarn, 2)) { b.status = "Needs Yarn"; return false; }
                UseIn(b, Res.Yarn, 2); Output(b, Res.Fabric, 1); b.status = "Weaving fabric"; return true;
            }, "Running looms");
        }

        void SimMarket(Building b, float dt, float mult)
        {
            float e = EffSum(b, Cat.Commerce);
            if (e <= 0) return;
            b.progress += e * mult * dt;
            bool anyAuto = false;
            foreach (var sr in ResInfo.Sellable) if (autoSell[(int)sr] && res[(int)sr] >= 1) anyAuto = true;
            b.status = anyAuto ? "Selling stored goods marked AUTO" : "Selling whatever rides in";
            if (b.progress >= 4f)
            {
                b.progress = 0;
                int batch = 2 + b.level * 2;
                // sell the most valuable auto-sell good
                for (int k = ResInfo.Sellable.Length - 1; k >= 0; k--)
                {
                    int r = (int)ResInfo.Sellable[k];
                    if (!autoSell[r] || res[r] < 1) continue;
                    int n = Mathf.Min(batch, Mathf.FloorToInt((float)res[r]));
                    int earned = Sell((Res)r, n, true);
                    if (earned > 0 && IsVisible(b.Center)) FloatText(b.Center + Vector2.up * 1.3f, "+" + earned + "c", new Color(1f, 0.86f, 0.35f));
                    break;
                }
            }
        }

        // ---------------- shearing ----------------
        public int ShearYield(Sheep s, Building b)
        {
            float y = s.wool * 8f;
            if (tech.Contains("improved_shears")) y *= 1.25f;
            if (b != null && b.def.kind == BKind.Shed) y *= BuildingDef.At(ShedYield, b.level);
            if (b != null && b.def.kind == BKind.Plant) y *= 2.5f;
            if (s.variant == Variant.Golden) y *= 3f;
            if (s.variant == Variant.Rainbow) y *= 2f;
            y *= 1f + (s.level - 1) * 0.05f; // veterans of the line grow better wool
            if (s.Has("fluffy")) y *= 1.1f;
            if (s.Has("messy")) y *= 0.9f;
            if (flags.Contains("illegal_shears")) y *= 1.5f;
            return Mathf.Max(1, Mathf.RoundToInt(y));
        }

        public void DoShear(Sheep s, Building b, bool manual)
        {
            Res wr = ShearRes(s);
            int y = Mathf.Max(1, Mathf.RoundToInt(ShearYield(s, b) * ShearAmount(s)));
            bool belted = false;
            if (b != null) belted = Output(b, wr, y); else Gain(wr, y);
            if (!belted) FlyFromWorld(ResInfo.Icon[(int)wr], s.pos + Vector2.up * 1.2f, Mathf.Clamp(y / 3, 1, 5));
            s.wool = 0;
            s.tremble = 5f;
            AddMod(s, "Recently shaved (cold)", -4, 0.6f);
            FloatText(s.pos + Vector2.up * 1.2f, ResInfo.Names[(int)wr].ToUpper() + " +" + y, wr == Res.Wool ? new Color(0.95f, 0.95f, 0.9f) : new Color(1f, 0.85f, 0.4f));
            SpawnParticles(s.pos + Vector2.up * 0.5f, new Color(0.96f, 0.94f, 0.88f), 12, 2.5f);
            if (rng.NextDouble() < 0.4) AddDecal("wool", s.pos + new Vector2((float)rng.NextDouble() - 0.5f, -0.1f));
            Sfx.Play("snip", 0.7f);
            shearCount++;
            if (manual && rng.NextDouble() < 0.025 * AccidentMult(s))
            {
                s.bloody = 0.5f; AddMod(s, "You nicked me", -8, 1);
                FloatText(s.pos + Vector2.up * 1.6f, "ow", new Color(1, 0.4f, 0.4f));
                AddDecal("blood", s.pos);
            }
            if (s.variant == Variant.Spiky && manual) { Toast("^rOw. The spiky one fought back."); }
        }
        public int shearCount;

        public int MilkYield(Sheep s, Building b)
        {
            float y = s.milk * 6f;
            if (tech.Contains("udder_science")) y *= 1.25f;
            if (b != null && b.def.kind == BKind.Dairy) y *= BuildingDef.At(ShedYield, b.level);
            if (s.Has("fertile")) y *= 1.15f;
            if (s.variant == Variant.Golden) y *= 2f;
            y *= 1f + (s.level - 1) * 0.05f;
            return Mathf.Max(1, Mathf.RoundToInt(y));
        }

        public void DoMilk(Sheep s, Building b, bool manual)
        {
            Res mr = MilkRes(s);
            int y = Mathf.Max(1, Mathf.RoundToInt(MilkYield(s, b) * MilkAmount(s)));
            bool belted = false;
            if (b != null) belted = Output(b, mr, y); else Gain(mr, y);
            if (!belted) FlyFromWorld(ResInfo.Icon[(int)mr], s.pos + Vector2.up * 1.2f, Mathf.Clamp(y / 2, 1, 5));
            s.milk = 0;
            s.tremble = Mathf.Max(s.tremble, 1.5f);
            AddMod(s, "Milked against my will", -2, 0.5f);
            FloatText(s.pos + Vector2.up * 1.2f, ResInfo.Names[(int)mr].ToUpper() + " +" + y, mr == Res.Milk ? new Color(0.82f, 0.9f, 1f) : new Color(1f, 0.92f, 0.6f));
            SpawnParticles(s.pos + Vector2.up * 0.35f, new Color(0.96f, 0.97f, 1f), 9, 2f);
            Sfx.Play("squirt", 0.6f, 0.9f + Random.Range(0f, 0.25f));
            milkCount++;
            if (manual && rng.NextDouble() < 0.03 * AccidentMult(s))
            {
                float lost = Mathf.Min((float)res[(int)Res.Milk], y * 0.5f);
                res[(int)Res.Milk] -= lost;
                FloatText(s.pos + Vector2.up * 1.6f, "*KICK*", new Color(1, 0.5f, 0.4f));
                Toast("^r" + s.name + " kicked the bucket. Literally. -" + Mathf.RoundToInt(lost) + " milk.");
            }
        }

        public Building NearestTrough(Vector2 p)
        {
            Building best = null; float bd = 1e9f;
            foreach (var b in buildings)
            {
                if (b.def.kind != BKind.Trough || !b.Built) continue;
                float d = (b.Center - p).sqrMagnitude;
                if (d < bd) { bd = d; best = b; }
            }
            return best;
        }

        bool IsClaimed(Sheep s)
        {
            foreach (var b in buildings) if (b.client == s || b.clients.Contains(s)) return true;
            return false;
        }

        void ReleaseClient(Building b)
        {
            if (b.client != null && !b.client.dead && (b.client.act == Act.Summoned || b.client.act == Act.BeingSheared)) { b.client.act = Act.Wander; b.client.summonedBy = null; }
            b.client = null;
            foreach (var c in b.clients) if (!c.dead && (c.act == Act.Summoned || c.act == Act.BeingSheared)) { c.act = Act.Wander; c.summonedBy = null; }
            b.clients.Clear();
        }

        // ---------------- breeding ----------------
        void SimBarn(Building b, float dt)
        {
            var males = new List<Sheep>(); var females = new List<Sheep>();
            foreach (var w in b.workers) if (w.act == Act.Working && w.CanWork) { if (w.male) males.Add(w); else females.Add(w); }
            int pairs = Mathf.Min(males.Count, females.Count);
            while (b.pairProgress.Count < pairs) b.pairProgress.Add(0);
            if (pairs == 0) { b.status = b.workers.Count == 0 ? "No breeders" : "Needs a ewe AND a ram"; return; }
            b.status = "Romance in progress";
            if (rng.NextDouble() < dt * 0.9 && IsVisible(b.Center)) SpawnParticles(b.Door + Vector2.up * 1.1f, new Color(1f, 0.45f, 0.62f), 2, 1.1f);
            for (int i = 0; i < pairs; i++)
            {
                var m = males[i]; var f = females[i];
                float sp = 0.5f; // 2 days per lamb
                if (m.Has("fertile") || f.Has("fertile")) sp *= 1.8f;
                if (tech.Contains("matchmaking")) sp *= 1.5f;
                sp *= BuildingDef.At(BarnSpeed, b.level);
                sp *= Mathf.Clamp((m.mood + f.mood) / 140f, 0.3f, 1.3f);
                b.pairProgress[i] = Mathf.Min(1f, b.pairProgress[i] + dt / DayLength * sp);
                if (b.pairProgress[i] >= 1f)
                {
                    if (Population >= popCap) { b.status = "The pens are full"; continue; }
                    b.pairProgress[i] = 0;
                    var lamb = CreateSheep(b.Door + new Vector2((float)rng.NextDouble() - 0.5f, -0.3f), m, f);
                    births++;
                    SpawnParticles(lamb.pos + Vector2.up * 0.5f, new Color(1f, 0.6f, 0.75f), 12, 2f);
                    Sfx.Baa(0.7f);
                    string extra = lamb.variant != Variant.None ? " ^p(" + VariantName(lamb.variant) + "!)" : "";
                    Log("^pA lamb is born!^w Meet ^y" + lamb.name + "^w (" + TraitList(lamb) + ")" + extra);
                    if (tutDone) AdviseNow("lamb", "A new lamb: " + lamb.name + ". Another mouth. Another future wool factory.", LExpr.Smug, 3f);
                    FloatText(lamb.pos + Vector2.up, "NEW LAMB", new Color(1f, 0.6f, 0.75f));
                }
            }
        }

        // ---------------- accidents ----------------
        void RollAccident(Building b, Sheep victimOverride = null)
        {
            if (b.Danger <= 0) return;
            Sheep v = victimOverride;
            if (v == null)
            {
                var ws = new List<Sheep>();
                foreach (var w in b.workers) if (w.act == Act.Working) ws.Add(w);
                if (ws.Count == 0) return;
                v = ws[rng.Next(ws.Count)];
            }
            float chance = b.Danger * AccidentMult(v);
            if (b.safetyUntil > Day) chance *= 0.3f;
            if (SignNear(b)) chance *= 0.9f;
            if (rng.NextDouble() < chance) Accident(v, b);
        }

        bool SignNear(Building b)
        {
            foreach (var o in buildings)
                if (o.def.kind == BKind.Sign && o.Built && Mathf.Abs(o.Center.x - b.Center.x) < 3 + b.def.w / 2f && Mathf.Abs(o.Center.y - b.Center.y) < 3 + b.def.h / 2f) return true;
            return false;
        }

        static readonly Dictionary<BKind, string[]> DeathCauses = new Dictionary<BKind, string[]>
        {
            { BKind.Shed, new[] { "Sheared a little too thoroughly.", "Got confused for a pile of wool.", "The shears won." } },
            { BKind.Dairy, new[] { "Milked to completion.", "The machine did not know when to stop.", "Became a dairy product." } },
            { BKind.Workshop, new[] { "Got caught in the wool machine.", "Washed. Rinsed. Repeated.", "Became yarn." } },
            { BKind.Textile, new[] { "Was woven into fabric. Lovely pattern though.", "Fell into the loom.", "Became a sweater." } },
            { BKind.Plant, new[] { "Went into THE MACHINE fluffy. Came out as wool.", "Processed.", "THE MACHINE was hungry." } },
            { BKind.Lab, new[] { "Licked the wrong battery.", "Experiment succeeded. Sheep did not.", "Exploded, scientifically." } },
            { BKind.Wheel, new[] { "Ran too fast. Reached the speed of sheep.", "Flung out of the wheel into the sun.", "Generated their last watt." } },
            { BKind.Farm, new[] { "Run over by the combine harvester.", "Ate the suspicious fertilizer.", "Became fertilizer." } },
            { BKind.GuardPost, new[] { "Lost a headbutt contest with a wolf.", "Fell off the watchtower while sleeping." } },
            { BKind.Rocket, new[] { "Tested the rocket engine. From the wrong end." } },
        };

        public void Accident(Sheep s, Building b)
        {
            if (s.dead) return;
            accidents++;
            b.accidents++;
            AdviseNow("accident", "An ACCIDENT at the " + b.def.name.ToUpper() + ". Nobody could have predicted this. (I predicted this.)", LExpr.Smug, 2.5f);
            lastAccidentDay = dayIndex;
            bool lucky = s.Has("lucky");
            float deathChance = 0.045f * (b.unsafeMode ? 1.6f : 1f) * (b.level >= 3 ? 1f + 0.5f * (b.level - 2) : 1f) * (b.def.kind == BKind.Plant ? 1.5f : 1f);
            if (lucky || s.Has("tough")) deathChance = 0;
            double r = rng.NextDouble();
            string bn = b.def.name;
            Vector2 at = s.pos;
            shake = Mathf.Max(shake, 0.4f);
            Sfx.Play("crunch", 0.7f);
            if (r < deathChance)
            {
                string[] causes;
                if (!DeathCauses.TryGetValue(b.def.kind, out causes)) causes = new[] { "Workplace accident. Paperwork has been filed." };
                Kill(s, causes[rng.Next(causes.Length)]);
                AddDecal("blood", b.Door + new Vector2((float)rng.NextDouble() - 0.5f, 0));
                return;
            }
            r = rng.NextDouble();
            if (b.def.kind == BKind.Lab && r < 0.2 && s.variant == Variant.None)
            {
                s.variant = Variant.ThreeEyed;
                SpawnParticles(at + Vector2.up * 0.6f, new Color(0.4f, 1f, 0.5f), 18, 3f);
                Sfx.Play("zap");
                QueueEvent(Ev_ResearchMutation(s));
                return;
            }
            if (r < 0.22 && s.wool > 0.2f)
            {
                int y = Mathf.RoundToInt(ShearYield(s, null) * 1.5f);
                Gain(Res.Wool, y);
                s.wool = 0; s.tremble = 10f;
                AddMod(s, "Traumatically shaved", -15, 2);
                FloatText(at + Vector2.up * 1.3f, "WOOL +" + y, new Color(0.95f, 0.95f, 0.9f));
                SpawnParticles(at + Vector2.up * 0.5f, new Color(0.96f, 0.94f, 0.88f), 22, 4f);
                Log("^o" + s.name + "^w came out of the " + bn + " completely shaved. ^kWOOL +" + y + ". " + Cap(s.He) + " is trembling.");
            }
            else if (r < 0.5)
            {
                s.injured = 2.5f; s.bloody = 1.5f;
                AddMod(s, "Workplace injury", -15, 3);
                AddDecal("blood", at);
                SpawnParticles(at + Vector2.up * 0.5f, new Color(0.75f, 0.1f, 0.1f), 12, 3f);
                Log("^r" + s.name + "^w got hurt at the " + bn + ". Bandages applied. Productivity concerns raised.");
                if (rng.NextDouble() < 0.45) QueueEvent(Ev_WorkshopAccident(s, b));
            }
            else if (r < 0.62)
            {
                Launch(s);
                AddMod(s, "Was launched", -10, 2);
                Log("^o" + s.name + "^w was launched out of the " + bn + ". Witnesses describe it as 'majestic'.");
            }
            else if (r < 0.72)
            {
                s.flattened = 1.5f;
                AddMod(s, "Flattened", -8, 2);
                Log("^o" + s.name + "^w was flattened at the " + bn + ". " + Cap(s.He) + " is now wider.");
            }
            else if (r < 0.82)
            {
                s.burned = 1f;
                AddDecal("scorch", at);
                AddMod(s, "Set on fire", -10, 2);
                Log("^o" + s.name + "^w is slightly on fire. " + (b.accidents > 2 ? "Again." : ""));
            }
            else
            {
                s.unconscious = 12f;
                s.muddy = 1f;
                AddMod(s, "Bonked on head", -5, 1);
                FloatText(at + Vector2.up * 1.2f, "BONK", new Color(1f, 0.9f, 0.5f));
                Log("^o" + s.name + "^w was knocked unconscious by the " + bn + ". The machine seems fine.");
            }
            if (!s.Has("coward") && !s.Has("brave") && rng.NextDouble() < 0.12 && s.traits.Count < 5) { s.traits.Add("coward"); Log("^k" + s.name + " is now a Coward. Understandably."); }
            if (b.accidents >= 3 && rng.NextDouble() < 0.3) AddDecal("blood", b.Door + new Vector2((float)rng.NextDouble() * 1.5f - 0.75f, 0.1f));
        }

        public static string Cap(string s) { return string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1); }

        // ---------------- graveyard ----------------
        void AddTombstone()
        {
            Building yard = null;
            foreach (var b in buildings) if (b.def.kind == BKind.Graveyard && b.Built) { yard = b; break; }
            if (yard == null) return;
            int i = tombs.Count;
            int cols = yard.def.w * 3 - 2, rows = 2;
            if (i >= cols * rows) return;
            float px = yard.x + 0.6f + (i % cols) * 0.7f, py = yard.y + 1.25f - (i / cols) * 0.8f;
            var go = new GameObject("tomb");
            go.transform.SetParent(yard.go.transform);
            go.transform.position = new Vector3(px, py, 0);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = EnvArt.Tomb(rng.Next(3));
            sr.sortingOrder = SortY(py) + 2;
            tombs.Add(go);
        }

        // ---------------- building visuals ----------------
        void FlyFromWorld(string icon, Vector2 world, int n)
        {
            if (cam == null || !IsVisible(world)) return;
            FlyIcon(icon, ToGui(world, UI.scale), n);
        }

        void UpdateBuildingVisual(Building b, float rdt, float dt)
        {
            if (IsRoom(b)) { if (b.construct < 1f && rng.NextDouble() < rdt * 2) SpawnParticles(new Vector2(b.x + (float)rng.NextDouble() * b.def.w, b.y + 0.3f), new Color(0.6f, 0.5f, 0.38f), 2, 1f); UpdatePenVisual(b); return; }
            if (IsSheepMachine(b) && b.construct >= 1f)
            {
                // the machine rattles while somebody is inside
                float j = b.inMachine != null && b.machineDur > 0.5f && !b.machineDone ? ((Mathf.FloorToInt(Time.time * 18) % 2) * 2 - 1) / 16f : 0f;
                b.go.transform.position = new Vector3(b.x + j, b.y, 0);
            }
            if (HasBeltDoors(b)) UpdateMachineDoors(b);
            if (b.def.kind == BKind.GeneLab) UpdateGeneLabVisual(b);
            if (b.def.kind == BKind.Palace && b.extras.Count > 0 && b.extras[0] != null)
            {
                var lsr = b.extras[0].GetComponent<SpriteRenderer>();
                bool talking = LeaderModal || popT > popDur - 1.5f;
                lsr.sprite = LeaderArt.Balcony(leaderWaveT > 0 ? 1 : 0);
                float bob = talking ? (Mathf.FloorToInt(Time.unscaledTime * 8) % 2) / 16f : 0;
                b.extras[0].transform.localPosition = new Vector3(LeaderArt.BalconyOffset.x, LeaderArt.BalconyOffset.y + bob, 0);
            }
            if (b.def.kind == BKind.Trough && b.construct >= 1f)
            {
                double f = res[(int)Res.Food];
                b.sr.sprite = LeaderArt.Trough(f <= 1 ? 0 : f < 25 ? 1 : f < 90 ? 2 : 3);
            }
            if (b.construct < 1f)
            {
                if (!b.scaffold.gameObject.activeSelf) RefreshBuildingSprite(b);
                if (rng.NextDouble() < rdt * 2) SpawnParticles(new Vector2(b.x + (float)rng.NextDouble() * b.def.w, b.y + 0.3f), new Color(0.6f, 0.5f, 0.38f), 2, 1f);
                return;
            }
            if (b.scaffold.gameObject.activeSelf) RefreshBuildingSprite(b);
            if (dt <= 0 || b.art == null) return;
            bool active = b.Slots > 0 || b.def.kind == BKind.House || b.def.kind == BKind.Spa || b.def.kind == BKind.Statue || b.def.kind == BKind.CloneVat || (IsSheepMachine(b) && b.inMachine != null);
            if (!active || !IsVisible(b.Center)) return;
            b.fxTimer -= dt;
            if (b.fxTimer > 0) return;
            b.fxTimer = 0.35f;
            Vector2 o = new Vector2(b.x, b.y);
            foreach (var p in b.art.smoke) SpawnSmoke(o + p, b.level >= 3 ? 1.2f + (b.level - 3) * 0.3f : 0.8f);
            foreach (var p in b.art.steam) if (rng.NextDouble() < 0.5) SpawnSteam(o + p);
            foreach (var p in b.art.sparks) if (rng.NextDouble() < 0.35) SpawnParticles(o + p, b.def.kind == BKind.Lab ? new Color(0.5f, 1f, 0.7f) : new Color(1f, 0.9f, 0.4f), 3, 1.5f);
            if (b.Danger >= 0.02f && rng.NextDouble() < 0.15) SpawnParticles(b.Door + Vector2.up * 0.3f, new Color(1f, 0.85f, 0.3f), 2, 2f);
        }
    }
}
