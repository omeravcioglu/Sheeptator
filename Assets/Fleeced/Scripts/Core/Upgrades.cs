using System.Collections.Generic;
using UnityEngine;

namespace Fleeced
{
    /// One upgrade path: a stat of one building kind (every building of that kind, so a new pen starts upgraded) or of the
    /// whole farm, raised level by level with coins. The arithmetic is in Core/UpgradeMath.cs.
    public class UpgradeDef
    {
        public string id, name, desc;
        public BKind kind; public bool global;   // global paths need no building
        public double per;                       // effect per level
        public bool add;                         // true: adds per x level; false: multiplies by (1 + per x level). Both double at milestones.
        public string what;                      // what the number is: "x speed", " lambs a release"...
        public double baseCost, growth;
        int cap, levelCap;
        /// M12 paths stay hidden and locked until unlock says so (null: always open). The unlock sticks (a flag), unlockText
        /// says why it is still locked, and say is the Supreme Leader's line when it opens. showMult scales the number shown
        /// (a Stampede is x2 before its first level).
        public System.Func<Game, bool> unlock;
        public string unlockText, say;
        public double showMult = 1;
        string unlockFlag;
        public string UnlockFlag { get { return unlockFlag ?? (unlockFlag = "path_" + id); } }
        public int MaxLevel { get { if (cap == 0) cap = UpgradeMath.CapFor(baseCost, growth, levelCap); return cap; } }

        public UpgradeDef(string id, string name, BKind kind, bool global, double per, bool add, string what, double baseCost, double growth, string desc, int maxLevel = 5000)
        {
            levelCap = maxLevel;
            this.id = id; this.name = name; this.kind = kind; this.global = global; this.per = per; this.add = add; this.what = what;
            this.baseCost = baseCost; this.growth = growth; this.desc = desc;
        }

        /// the effect at `level`: a multiplier (exactly 1 at level 0) or an amount added (exactly 0 at level 0)
        public double Effect(int level) { return add ? UpgradeMath.Add(per, level) : UpgradeMath.Mult(per, level); }

        /// the effect as the player reads it: "x1.50 speed" or "+3 places"
        public string Show(int level)
        {
            double e = Effect(level) * showMult;
            return add ? "+" + (e < 10 && e != System.Math.Floor(e) ? e.ToString("0.0") : UI.Fmt(e)) + what : "x" + (e < 100 ? e.ToString("0.00") : UI.Fmt(e)) + what;
        }
    }

    public static class Upgrades
    {
        /// Every path. Ids are saved: only ever add rows. Costs are tuned so the first levels cost about a minute of early income.
        public static readonly UpgradeDef[] All =
        {
            new UpgradeDef("gate_lambs", "Bigger Litters", BKind.SheepGate, false, 0.1, true, " lambs a release", 300, 1.2, "More lambs out of the Sheep Gate each time it opens. Every 10 levels is one more, and every milestone doubles them. Past a pen's names they join its herd.", 5000),
            new UpgradeDef("gate_speed", "Greased Hinges", BKind.SheepGate, false, 0.06, false, " release speed", 150, 1.17, "The gate opens faster while you hold SPACE: a big charge empties sooner.", 24), // beyond 24 it would beat the simulation's step
            new UpgradeDef("gate_cap", "Bigger Gatehouse", BKind.SheepGate, false, 1, true, " charge", 150, 1.18, "More releases stored up in the gate before it has to refill.", 100),
            new UpgradeDef("gate_refill", "Night Shift", BKind.SheepGate, false, 0.08, false, " refill speed", 200, 1.18, "The gate's charge refills faster."),
            new UpgradeDef("gate_auto", "Gate Keeper", BKind.SheepGate, false, 2, true, " releases a minute by itself", 800, 1.22, "A sheepdog in a cap opens the Sheep Gate by itself, a few times a minute, while you do other things. It uses the same charge.", 50),
            new UpgradeDef("pen_breed", "Romance", BKind.Pen, false, 0.1, false, " breeding", 250, 1.17, "Lambs are born faster in every Breeding Pen. Candles. Music. Hay."),
            new UpgradeDef("pen_space", "Pen Space", BKind.Pen, false, 2, true, " places a pen", 400, 1.2, "Every Breeding Pen holds more sheep, named or herd.", 5000),
            new UpgradeDef("herd_space", "Sheep Stacking", BKind.Pen, false, 0.25, false, " room in pens and lots", 800, 1.17, "Every Breeding Pen and Feeding Lot holds more sheep. Stacked. Carefully. In bunks. Past 24 named sheep a room keeps a HERD.", 5000),
            new UpgradeDef("drove_size", "Bigger Droves", BKind.Pen, false, 1, true, " sheep in a drove", 400, 1.17, "More of the HERD follows each named sheep out of a door (4 to start): through the machines, into the Markets, on to the next room.", 5000),
            new UpgradeDef("lot_speed", "Bigger Troughs", BKind.FeedLot, false, 0.1, false, " eating speed", 200, 1.17, "Sheep eat their fill faster in every Feeding Lot."),
            new UpgradeDef("lot_space", "Lot Space", BKind.FeedLot, false, 2, true, " places a lot", 300, 1.2, "Every Feeding Lot holds more sheep.", 5000),
            new UpgradeDef("shear_speed", "Sharper Blades", BKind.Shed, false, 0.08, false, " shearing speed", 250, 1.17, "Shearing Machines (and the Wool Plant) take each sheep faster."),
            new UpgradeDef("shear_yield", "Fluffier Fleece", BKind.Shed, false, 0.1, false, " wool a sheep", 300, 1.18, "More wool off every sheep that is shorn."),
            new UpgradeDef("milk_speed", "Faster Pumps", BKind.Dairy, false, 0.08, false, " milking speed", 280, 1.17, "Milking Machines take each ewe faster."),
            new UpgradeDef("milk_yield", "Richer Milk", BKind.Dairy, false, 0.1, false, " milk a ewe", 350, 1.18, "More milk from every ewe that is milked."),
            new UpgradeDef("farm_food", "Fertilizer", BKind.Farm, false, 0.12, false, " food", 200, 1.17, "Every Farm grows more food."),
            new UpgradeDef("market_price", "Haggling", BKind.Market, false, 0.04, false, " prices", 500, 1.2, "Markets pay more for everything: goods on belts, goods sold by hand and live sheep."),
            new UpgradeDef("storage_space", "Bigger Barns", BKind.Storage, true, 0.15, false, " storage", 250, 1.18, "Room for more of every good in storage."),
            new UpgradeDef("offline", "Night Watch", BKind.Pen, true, 0.1, false, " offline earnings", 1000, 1.22, "While the game is closed the farm earns a quarter of its usual income, for up to 8 hours. This raises that share, to at most three quarters.", 10),
            new UpgradeDef("wool_growth", "Wool Tonic", BKind.Pen, true, 0.05, false, " wool growth", 500, 1.2, "Every sheep grows its fleece faster."),
            // M12: hidden until the Sheep Gate's stages open them
            new UpgradeDef("gate_burst", "Stampede", BKind.SheepGate, false, 0.5, false, " stampede", 12000, 1.2, "From the DOUBLE GATE on, every 6th release in a row while you HOLD SPACE is a STAMPEDE: that release lets out this many times the lambs. The Gate Keeper never stampedes.", 5000)
                { unlock = g => g.GateStage >= 2, unlockText = "Opens with the DOUBLE GATE", showMult = 2, say = "STAMPEDE (U): every sixth release in a row gets BIGGER. The ground will remember." },
            new UpgradeDef("pen_chambers", "Breeding Chambers", BKind.Pen, false, 1, true, " chambers a pen", 6000, 2.5, "Huts in the corners of every Breeding Pen, 8 at most. Couples of the pen's herd breed inside, and on each hut's own timer a LITTER pops out.", 8)
                { unlock = g => g.GateStage >= 2, unlockText = "Opens with the DOUBLE GATE" },
            new UpgradeDef("chamber_pairs", "Bigger Chambers", BKind.Pen, false, 10, true, " couples a hut", 10000, 1.17, "More couples in every Breeding Chamber (10 to start). Every milestone doubles them.", 5000)
                { unlock = g => g.PathLevel("pen_chambers") >= 1, unlockText = "Buy a BREEDING CHAMBER first", say = "BIGGER CHAMBERS (U): more couples in every hut. Privacy is overrated." },
            new UpgradeDef("pen_twins", "Twin Season", BKind.Pen, false, 4, true, "% twins", 25000, 1.55, "A share of the herd's births and of every chamber litter comes as TWINS. Named couples keep their own luck.", 12)
                { unlock = g => g.PathLevel("pen_chambers") >= 1, unlockText = "Buy a BREEDING CHAMBER first", say = "TWIN SEASON (U). Two lambs for the price of one. The price was zero. It is now less." },
            new UpgradeDef("pen_triplets", "Triplet Season", BKind.Pen, false, 2, true, "% triplets", 400000, 1.55, "A share of the herd's births and of every chamber litter comes as TRIPLETS.", 12)
                { unlock = g => g.GateStage >= 3, unlockText = "Opens with the FLOCK PORTAL", say = "TRIPLET SEASON (U). Three at once. The ewes filed a complaint. I ate it." },
            new UpgradeDef("machine_batch", "Bigger Hoppers", BKind.Shed, true, 0.25, false, " sheep a pass", 15000, 1.17, "Shearing Machines, the Wool Plant and Milking Machines take more of a drove in one PASS, so big droves go through faster. Only time changes: every sheep is still shorn or milked.", 5000)
                { unlock = g => g.flags.Contains("pass_intro") || g.GateStage >= 3, unlockText = "Opens when a drove is too big for a machine", say = "BIGGER HOPPERS (U): every machine swallows more sheep in one pass. Chew faster, machines." },
            new UpgradeDef("wool_value", "Wool Grading", BKind.Shed, false, 0.05, false, " wool prices", 8000, 1.18, "Wool, Clean Wool, Silk, Golden Fleece, Glow Wool and Cotton Candy sell for more, everywhere they are sold.", 5000)
                { unlock = g => g.GateStage >= 2, unlockText = "Opens with the DOUBLE GATE", say = "WOOL GRADING (U): graded wool sells for more. I do the grading. Everything is grade A." },
        };

        static Dictionary<string, UpgradeDef> map;
        public static UpgradeDef Get(string id)
        {
            if (map == null) { map = new Dictionary<string, UpgradeDef>(); foreach (var d in All) map[d.id] = d; }
            UpgradeDef r;
            return id != null && map.TryGetValue(id, out r) ? r : null;
        }
    }

    public partial class Game
    {
        /// upgrade path levels by path id (saved, version 10)
        public readonly Dictionary<string, int> pathLevels = new Dictionary<string, int>();

        public int PathLevel(string id) { int l; return pathLevels.TryGetValue(id, out l) ? l : 0; }

        /// the highest level reached on a path that can go at least `canReach` levels (a decree asks for a milestone)
        public int MaxPathLevel(int canReach = 0)
        {
            int m = 0;
            foreach (var kv in pathLevels)
            {
                if (kv.Value <= m) continue;
                var d = Upgrades.Get(kv.Key);
                if (d != null && d.MaxLevel >= canReach) m = kv.Value;
            }
            return m;
        }

        /// a multiplying path's factor: exactly 1 until it is upgraded
        public double PathMult(string id)
        {
            int l = PathLevel(id);
            if (l <= 0) return 1;
            var d = Upgrades.Get(id);
            return d == null ? 1 : UpgradeMath.Mult(d.per, l);
        }

        /// an adding path's amount: exactly 0 until it is upgraded
        public double PathAdd(string id)
        {
            int l = PathLevel(id);
            if (l <= 0) return 0;
            var d = Upgrades.Get(id);
            return d == null ? 0 : UpgradeMath.Add(d.per, l);
        }

        /// coins for the next `n` levels of a path
        public double PathCost(UpgradeDef d, int n) { return UpgradeMath.Cost(d.baseCost, d.growth, PathLevel(d.id), n); }

        /// the most levels the coins in hand buy (MAX)
        public int PathMaxBuy(UpgradeDef d) { int l = PathLevel(d.id); return UpgradeMath.MaxAffordable(d.baseCost, d.growth, l, res[0], d.MaxLevel - l); }

        /// false while an M12 path waits for the Sheep Gate's stage that opens it
        public bool PathUnlocked(UpgradeDef d) { return d.unlock == null || flags.Contains(d.UnlockFlag); }

        /// Every step: M12 paths open as the gate grows, for good (a flag each, so demolishing the gate never locks them again).
        /// One toast names every path that opens in a step (the toast has one slot), and the Supreme Leader says each one's line
        /// in turn.
        void CheckPathUnlocks()
        {
            string names = null; int opened = 0;
            foreach (var d in Upgrades.All)
            {
                if (d.unlock == null || flags.Contains(d.UnlockFlag) || !d.unlock(this)) continue;
                flags.Add(d.UnlockFlag);
                if (++opened <= 3) names = names == null ? d.name.ToUpper() : names + ", " + d.name.ToUpper(); // the toast is one line
                if (d.say != null) { string line = d.say; AnnounceWhenFree(d.UnlockFlag, () => LeaderPop(line, LExpr.Smug, 7f)); }
            }
            if (names != null) Toast("^yNEW UPGRADES:^w " + names + (opened > 3 ? " and " + (opened - 3) + " more" : ""));
        }

        /// why `n` more levels can't be bought now, or null
        public string PathBlocker(UpgradeDef d, int n)
        {
            if (!PathUnlocked(d)) return d.unlockText ?? "Locked";
            if (!d.global && !HasBuilding(d.kind)) return "Build a " + Buildings.Get(d.kind).name + " first";
            if (PathLevel(d.id) >= d.MaxLevel) return "Fully upgraded";
            if (n <= 0) return "Not enough coins";
            if (PathLevel(d.id) + n > d.MaxLevel) return "Only " + (d.MaxLevel - PathLevel(d.id)) + " levels left";
            if (!CanAfford(PathCost(d, n))) return "Need " + UI.Fmt(PathCost(d, n)) + " coins";
            return null;
        }

        /// buys `n` levels of a path; false (and nothing spent) when it can't
        public bool BuyPath(UpgradeDef d, int n)
        {
            if (PathBlocker(d, n) != null) return false;
            int before = PathLevel(d.id);
            SpendCoins(PathCost(d, n), CoinSrc.Path);
            pathLevels[d.id] = before + n;
            pathBought += n;
            Sfx.Play("build", 0.5f, 1.2f);
            int m = UpgradeMath.MilestonesReached(before + n) - UpgradeMath.MilestonesReached(before);
            if (m > 0) { Log("^yMILESTONE:^w " + d.name + " reached level " + (before + n) + ". Its bonus doubles!"); Sfx.Play("fanfare", 0.6f); }
            return true;
        }

        /// path levels bought, ever (saved; a decree counts them)
        public int pathBought;
    }
}
