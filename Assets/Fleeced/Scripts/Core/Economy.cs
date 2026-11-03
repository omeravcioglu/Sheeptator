using System.Collections.Generic;
using UnityEngine;

namespace Fleeced
{
    public class Objective
    {
        /// `id` names the decree in saves (version 10 on): decrees can be added anywhere without shifting old saves.
        /// `have` and `need` are the progress bar, read as numbers (null when the decree has no bar).
        public string id, text; public int reward; public System.Func<Game, bool> done; public System.Func<Game, double> have; public double need; public string say;
        /// the flag that marks this decree as already done in the tutorial ("skip_" + id), built once instead of every step
        public readonly string skipFlag;
        public Objective(string id, string t, int r, System.Func<Game, bool> d, System.Func<Game, double> have = null, double need = 0, string say = null) { this.id = id; text = t; reward = r; done = d; this.have = have; this.need = need; this.say = say; skipFlag = "skip_" + id; }
    }

    public partial class Game
    {
        public float[] priceMod = Ones(ResInfo.Count);
        static float[] Ones(int n) { var a = new float[n]; for (int i = 0; i < n; i++) a[i] = 1; return a; }
        public float[] priceTrend = new float[ResInfo.Count];
        public List<Offer> offers = new List<Offer>();
        public Contract contract;
        public float rpPool;
        public string currentResearch;
        public Dictionary<string, float> rprog = new Dictionary<string, float>();
        public float Progress(string id) { float v; return rprog.TryGetValue(id, out v) ? v : 0; }
        public int objectiveIndex;
        public float tierShownT;
        string lastTier = "";

        // ---------------- new game ----------------
        void NewGame()
        {
            res[(int)Res.Coins] = 0;
            CreditCoins(StartCoins, CoinSrc.Tutorial); // a gift from the Supreme Treasury, not earnings
            res[(int)Res.Food] = 220;
            simClock = DayLength * 0.3f; // the Supreme Leader rises at dawn
            for (int i = 0; i < ResInfo.Count; i++) lastRes[i] = res[i];
            for (int i = 0; i < autoSell.Length; i++) autoSell[i] = false;
            foreach (var r in new[] { Res.CleanWool, Res.Yarn, Res.Fabric, Res.Clothing, Res.Luxury, Res.Cheese }) autoSell[(int)r] = true;

            // the Supreme Palace sits at the top of the field
            int ox = owned.xMin, oy = owned.yMin;
            int px = ox + owned.width / 2 - 1, py = owned.yMax - 4;
            ClearPropsIn(owned);
            PlaceBuilding(Buildings.Get(BKind.Palace), px, py, true);

            // the Breeding Pen (top left) and the Feeding Lot under it
            var pen = PlaceBuilding(Buildings.Get(BKind.Pen), ox + 3, oy + 10, true);
            var lot = PlaceBuilding(Buildings.Get(BKind.FeedLot), ox + 3, oy + 3, true);

            // pen bottom door -> down into the feeding lot (two tiles are missing: the Supreme Leader makes you lay them)
            var pd = DoorTile(pen, 0);
            SetBelt(pd.x, pd.y, 3, true);
            // feeding lot right door -> the shearing line -> up the side -> back into the pen's right side
            var ld = DoorTile(lot, 1);
            int lineY = ld.y, endX = ox + 18, backY = pen.y + 2;
            for (int x = ld.x; x < endX; x++) SetBelt(x, lineY, 0, true);
            for (int y = lineY; y < backY; y++) SetBelt(endX, y, 1, true);
            for (int x = endX; x > pen.x + pen.def.w - 1; x--) SetBelt(x, backY, 2, true);
            PlaceBuilding(Buildings.Get(BKind.Shed), ld.x + 3, lineY, true);
            // feeding lot bottom door -> a line to where the Slaughterhouse will stand, and a mutton line on to the market spot
            var sd = DoorTile(lot, 0);
            SetBelt(sd.x, sd.y, 3, true);
            for (int x = sd.x; x < ox + 10; x++) SetBelt(x, sd.y - 1, 0, true);
            for (int x = ox + 12; x < ox + 19; x++) SetBelt(x, sd.y - 1, 0, true);
            SetBelt(ox + 19, sd.y - 1, 1, true);

            // ten hungry sheep in the pen, facing the palace for the Supreme Leader's address
            var inner = PenInterior(pen);
            for (int i = 0; i < 10; i++)
            {
                Vector2 p = new Vector2(inner.xMin + 0.3f + (i % 5) * (inner.width - 0.6f) / 4.5f + (float)rng.NextDouble() * 0.5f,
                                        inner.yMin + 0.25f + (i / 5) * 1.25f + (float)rng.NextDouble() * 0.3f);
                var s = CreateSheep(p, null, null, i % 2 == 0);
                s.pen = pen;
                s.act = Act.Idle;
                s.faceRight = true;
                s.hunger = 0.9f + (float)rng.NextDouble() * 0.4f;
                s.wool = i < 6 ? 1.0f + (float)rng.NextDouble() * 0.1f : 0.35f + (float)rng.NextDouble() * 0.4f;
                s.milk = s.male ? 0 : (i < 7 ? 1f : 0.45f);
                if (i == 0)
                {
                    usedNames.Remove(s.name);
                    s.name = "Gurgle"; usedNames.Add("Gurgle"); s.go.name = "Gurgle";
                    s.traits.Clear(); s.traits.Add("messy"); s.traits.Add("glutton"); s.traits.Add("accident");
                    s.baseExpr = Expr.Tongue; s.variant = Variant.None;
                    founderName = s.name;
                }
            }
            Vector2 stage = new Vector2(pen.x + 5.5f, pen.y + 3.2f); // pen, palace balcony and the start of the line

            RefreshOffers();
            ComputeGlobals();
            CountPens(); // room badges show the flock while the Supreme Leader is still talking
            ResetGenetics();
            ResetDayStats();
            camTarget = new Vector3(stage.x, stage.y - 0.8f, -10);
            ApplyCamera();
            cam.transform.position = camTarget;
            panel = Panel.None;
        }

        // ---------------- logging ----------------
        public List<KeyValuePair<string, float>> log = new List<KeyValuePair<string, float>>();
        public List<string> logHistory = new List<string>();
        public void Log(string msg)
        {
            log.Add(new KeyValuePair<string, float>(msg, Time.unscaledTime));
            if (log.Count > 7) log.RemoveAt(0);
            logHistory.Add("^k[" + Seasons[Season].Substring(0, 3) + " " + DayOfSeason + "]^w " + msg);
            if (logHistory.Count > 120) logHistory.RemoveAt(0);
        }
        public string toast; public float toastT;
        public void Toast(string msg) { toast = msg; toastT = 2.5f; }

        // ---------------- market ----------------
        /// the price of one unit sold by hand or by an event: the best Market's level bonus applies
        public float SellPrice(Res r) { return SellPrice(r, null); }

        /// `at` is the Market that makes the sale (goods riding into it, or its own AUTO batch); null = the best Market
        public float SellPrice(Res r, Building at)
        {
            float p = ResInfo.BasePrice[(int)r] * priceMod[(int)r];
            if (!HasBuilding(BKind.Market)) p *= 0.6f;
            else
            {
                var m = at ?? BestMarket();
                if (m != null) p *= 1f + 0.05f * (m.level - 1);
            }
            if (tech.Contains("haggling")) p *= 1.1f;
            if (r == Res.Luxury && tech.Contains("baalenciaga")) p *= 1.5f;
            if (flags.Contains("luxury_brand") && r >= Res.Clothing && r <= Res.Luxury) p *= 1.15f;
            if (flags.Contains("fake_wool") && r <= Res.CleanWool) p *= 1.2f;
            p *= (float)PathMult("market_price"); // Haggling (x1 until upgraded)
            p *= CoinTierMult;                    // Coin Evolutions (x1 at the first tier)
            return p;
        }

        /// the highest-level building of a kind (built or upgrading), or null
        public Building BestBuilding(BKind k)
        {
            Building best = null;
            foreach (var b in buildings)
                if (b.def.kind == k && (b.Built || b.upgrading) && (best == null || b.level > best.level)) best = b;
            return best;
        }
        public Building BestMarket() { return BestBuilding(BKind.Market); }

        public Building FindBuilding(BKind k)
        {
            foreach (var b in buildings) if (b.def.kind == k && (b.Built || b.upgrading)) return b;
            return null;
        }

        /// sells n units from storage: `at` is the Market selling its own AUTO batch, null = a sale by hand (the best Market)
        public int Sell(Res r, int n, bool auto, Building at = null)
        {
            n = (int)System.Math.Min(n, System.Math.Floor(res[(int)r]));
            if (n <= 0) return 0;
            var m = at ?? BestMarket(); // one lookup for the whole sale
            float total = 0;
            for (int i = 0; i < n; i++)
            {
                total += SellPrice(r, m);
                priceMod[(int)r] = Mathf.Max(0.35f, priceMod[(int)r] * (1f - 0.0035f / Mathf.Max(1, (int)r)));
            }
            int earned = (int)System.Math.Min(System.Math.Round(total), int.MaxValue);
            res[(int)r] -= n;
            CreditCoins(earned, CoinSrc.Sale);
            totalMade[0] += earned;
            totalSold[(int)r] += n;
            if (!auto) Sfx.Play("coin", 0.6f);
            return earned;
        }

        /// v9 raised every sheep's price by 4% per sheep in the flock. With free lambs from the Sheep Gate that would make the gate a
        /// money printer, so the price is fixed at what it was with the 10 founders.
        public const float SheepPriceFactor = 1.4f;
        /// a new game's coins (the plan said 0; 150 keeps the tutorial's slack and gives SKIP TUTORIAL something to spend)
        public const int StartCoins = 150;

        public int SheepPrice(Sheep s)
        {
            float p = 55;
            foreach (var t in s.traits) p += Traits.Get(t).kind * 22;
            if (s.variant != Variant.None) p += 120;
            p += (s.level - 1) * 15;
            p += StrainValue(s);
            return Mathf.Max(25, Mathf.RoundToInt(p * SheepPriceFactor));
        }

        public void RefreshOffers()
        {
            foreach (var o in offers) if (o.sheep.go != null) { Destroy(o.sheep.go); Destroy(o.sheep.shadow.gameObject); }
            offers.Clear();
            for (int i = 0; i < 3; i++)
            {
                var s = new Sheep { id = NextId(), male = rng.NextDouble() < 0.5 };
                s.name = Names.Random(rng, usedNames);
                int n = 1 + rng.Next(3);
                for (int k = 0; k < n; k++)
                    for (int tries = 0; tries < 20; tries++) { var t = Traits.Natural[rng.Next(Traits.Natural.Length)]; if (!Traits.Conflicts(s.traits, t)) { s.traits.Add(t); break; } }
                if (rng.NextDouble() < 0.06) s.variant = RandomVariant();
                s.woolCol = RandomWoolCol(); s.faceCol = rng.Next(10) < 6 ? 0 : rng.Next(4); s.baseExpr = RandomExpr();
                s.wool = 0.5f + (float)rng.NextDouble() * 0.5f;
                if (rng.NextDouble() < 0.3) s.level = 1 + rng.Next(3);
                offers.Add(new Offer { sheep = s, price = 0 });
            }
            foreach (var o in offers) o.price = SheepPrice(o.sheep);
        }

        /// what the dealer asks: the sheep's price times the coin tier and Haggling, like every sale (twice its SELL price at any tier)
        public int OfferPrice(Offer o)
        {
            double p = o.price * (double)CoinTierMult * PathMult("market_price");
            return (int)System.Math.Min(int.MaxValue / 2, System.Math.Ceiling(p));
        }

        public void BuyOffer(Offer o)
        {
            int price = OfferPrice(o);
            if (res[0] < price) { Toast("^rNot enough coins."); return; }
            if (!CanName(null)) { Toast("^rThe farm knows " + NamedCap + " names already. Not one more."); return; }
            SpendCoins(price, CoinSrc.SheepBuy);
            offers.Remove(o);
            var s = o.sheep;
            usedNames.Add(s.name);
            Vector2 gate = new Vector2(owned.xMin + owned.width / 2f, owned.yMin + 0.6f);
            s.pos = gate; s.target = gate; s.born = Day;
            s.statusTimer = 2; s.baaTimer = 3;
            sheep.Add(s);
            SpawnSheepGO(s);
            AssignPen(s);
            Ledger(1);
            SpawnParticles(gate + Vector2.up * 0.5f, new Color(1, 1, 0.8f), 10, 2);
            Sfx.Baa(0.8f);
            Log("Bought ^y" + s.name + "^w (" + TraitList(s) + "). No refunds.");
            if (Population > popCap) Toast("^oThe pens are overcrowded! Upgrade a Breeding Pen or build another.");
        }

        /// what the inspector's SELL button pays (and says it pays): half the dealer's price, with the coin tier
        public int SellSheepPrice(Sheep s)
        {
            int p = SheepPrice(s) / 2;
            if (CoinTier > 0) p = Mathf.RoundToInt(p * CoinTierMult);
            return p;
        }

        /// the most a sheep can fetch: SELL, the best Market's door, or its mutton from the best Slaughterhouse belted into a Market
        public int BestResale(Sheep s)
        {
            double mutton = MuttonYield(s, BestBuilding(BKind.Slaughter)) * SellPrice(Res.Mutton) * 1.1;
            double best = System.Math.Max(System.Math.Max(SellSheepPrice(s), LiveSheepPrice(s, BestMarket())), mutton);
            return (int)System.Math.Min(int.MaxValue / 2, System.Math.Ceiling(best));
        }

        /// the Cloning Vat's price: 400, or a quarter more than the copy could ever fetch. The copy is priced as the Vat makes it
        /// (an adult at level 1 with the same traits plus "clone", the same variant and strain; a lamb's copy is an adult) and as
        /// the original is, whichever is worth more. Copies come out shorn, never rise again and the Collector wants originals,
        /// so no clone resells at a profit, whatever the coin tier.
        public int CloneCost(Sheep s)
        {
            vatCopy.male = s.male; vatCopy.variant = s.variant; vatCopy.strain = s.strain; vatCopy.level = 1; vatCopy.ageDays = 3;
            vatCopy.traits.Clear(); vatCopy.traits.AddRange(s.traits);
            if (!vatCopy.traits.Contains("clone")) vatCopy.traits.Add("clone");
            double best = System.Math.Max(BestResale(s), BestResale(vatCopy));
            return Mathf.Max(400, (int)System.Math.Min(int.MaxValue / 2, System.Math.Ceiling(best * 1.25)));
        }
        readonly Sheep vatCopy = new Sheep(); // the copy CloneCost prices (never in the world)

        public void SellSheep(Sheep s)
        {
            if (s.dead) return; // already gone (sold, or back in its herd)
            int p = SellSheepPrice(s);
            CreditCoins(p, CoinSrc.LiveSale); soldAlive = Bump(soldAlive, 1);
            liveToday += p; soldToday = Bump(soldToday, 1); // for the day's income measure (Core/Money.cs)
            DisbandDrove(s); Ledger(-1); // its drove goes home: SELL sells one sheep
            RemoveFromJob(s);
            s.act = Act.Hidden; s.dead = true; s.deathCause = "Sold";
            if (s.go != null) { Destroy(s.go); Destroy(s.shadow.gameObject); s.go = null; }
            sheep.Remove(s);
            AddGlobalMod("A friend was sold", -5, 1.5f);
            Log("Sold ^y" + s.name + "^w to a 'nice farm upstate' for " + p + " coins.");
            Sfx.Play("coin");
            if (selSheep == s) selSheep = null;
        }

        public int FoodBuyCost { get { return 18 + dayIndex / 4; } }
        public void BuyFood()
        {
            if (res[0] < FoodBuyCost) { Toast("^rNot enough coins."); return; }
            SpendCoins(FoodBuyCost, CoinSrc.Purchase); Gain(Res.Food, 40); Sfx.Play("coin", 0.5f);
        }

        // ---------------- research ----------------
        public bool ResearchAvailable(ResearchDef r)
        {
            if (tech.Contains(r.id)) return false;
            foreach (var p in r.pre) if (!tech.Contains(p)) return false;
            return true;
        }

        public void AddRP(float amt)
        {
            rpPool += amt;
            totalRP += amt;
        }
        public float totalRP, rpRate; float lastRP;

        void SimResearch(float dt)
        {
            if (currentResearch == null) return;
            var r = Research.Get(currentResearch);
            float p = Progress(currentResearch);
            float take = Mathf.Min(rpPool, r.rp - p);
            rprog[currentResearch] = p + take; rpPool -= take;
            if (p + take >= r.rp - 0.001f) CompleteResearch(r);
        }

        public void CompleteResearch(ResearchDef r)
        {
            tech.Add(r.id);
            currentResearch = null;
            Sfx.Play("fanfare", 0.7f);
            Log("^cResearch complete: ^y" + r.name + "^w. " + r.desc);
            Toast("^cRESEARCHED: " + r.name);
            if (r.id == "space_program") QueueEvent(Ev_SpaceReady());
            if (r.id == "express_belts" || r.id == "mega_belts") beltTierSel = MaxBeltTier;
            if (r.id == "genetics") AdviseNow("genetics", "SCIENCE! We can rewrite the sheep. Build a GENE LAB on a conveyor: every sheep that rides through gives DNA. Spend it in the GENETICS menu (N) on better lambs and new STRAINS. Silk. Cream. GOLD.", LExpr.Smug, 99f);
        }

        // ---------------- objectives & tiers ----------------
        public static readonly Objective[] Objectives =
        {
            new Objective("gate5", "Let 5 lambs out of the Sheep Gate (hold SPACE)", 30, g => g.gateReleased >= 5, g => g.gateReleased, 5, "Hold SPACE: let FIVE lambs out of the SHEEP GATE. Free lambs. Do not ask where they come from."),
            new Objective("upgrade1", "Buy an upgrade in UPGRADES (U)", 40, g => g.pathBought >= 1, g => g.pathBought, 1, "Open UPGRADES (U) and buy anything. Faster gates, fatter fleeces, bigger pens. Progress has a price."),
            new Objective("shear15", "Shear 15 sheep in the machines", 30, g => g.shearCount >= 15, g => g.shearCount, 15, "Shear FIFTEEN sheep. Pen door, feeding lot, shearing machine. Snip."),
            new Objective("feed30", "Feed 30 sheep in a Feeding Lot", 30, g => g.lotMeals >= 30, g => g.lotMeals, 30, "Feed THIRTY sheep in the Feeding Lot. Fed sheep make wool. Starving sheep make complaints."),
            new Objective("market", "Build a Market", 30, g => g.HasBuilding(BKind.Market), null, 0, "Build a MARKET. Wool is worthless until someone overpays for it."),
            new Objective("beltcoins150", "Earn 150 coins from goods sold by conveyor", 50, g => g.beltCoins >= 150, g => g.beltCoins, 150, "Earn 150 coins from goods that RIDE A BELT into a Market. Money that walks in by itself."),
            new Objective("farm2", "Build a second Farm (or upgrade one)", 40, g => g.CountKind(BKind.Farm) >= 2 || g.HasBuildingLevel(BKind.Farm, 2), null, 0, "More sheep, more mouths. Build a second FARM or upgrade the first. The Feeding Lot is only as good as the food store."),
            new Objective("flock16", "Grow the flock to 16 sheep", 80, g => g.Population >= 16, g => g.Population, 16, "SIXTEEN sheep. Keep them fed and in a closed pen and nature does the rest."),
            new Objective("lot2", "Upgrade the Feeding Lot to Level 2", 80, g => g.HasBuildingLevel(BKind.FeedLot, 2), null, 0, "UPGRADE the FEEDING LOT. More sheep need more feeding space. Simple maths. My favourite kind."),
            new Objective("mutton20", "Sell 20 Mutton", 60, g => g.totalSold[(int)Res.Mutton] >= 20, g => g.totalSold[(int)Res.Mutton], 20, "Sell 20 MUTTON. Too many sheep? Not anymore."),
            new Objective("gate100", "Let 100 lambs out of the Sheep Gate", 150, g => g.gateReleased >= 100, g => g.gateReleased, 100, "ONE HUNDRED lambs through the Sheep Gate. Upgrade the gate, make room in the pens."),
            new Objective("pen2", "Build a second Breeding Pen", 80, g => g.CountKind(BKind.Pen) >= 2, null, 0, "Build a SECOND BREEDING PEN and connect its doors with conveyors. More pens. More lambs. More of everything."),
            new Objective("lab", "Build a Research Lab", 60, g => g.HasBuilding(BKind.Lab), null, 0, "Build a RESEARCH LAB. Science! Explosions are part of it."),
            new Objective("smartdoors", "Research Smart Doors", 60, g => g.tech.Contains("fluff_detector"), null, 0, "Research SMART DOORS. Give every door a filter, leave them open, and the factory runs itself."),
            new Objective("upgrade10", "Buy 10 upgrade levels", 100, g => g.pathBought >= 10, g => g.pathBought, 10, "TEN upgrade levels, Hoof. Open UPGRADES (U): x10 buys ten at once, if you can afford ambition."),
            new Objective("genetics", "Research Sheep Genetics", 60, g => g.tech.Contains("genetics"), null, 0, "Research SHEEP GENETICS. We will improve the sheep. The sheep have not been consulted."),
            new Objective("genelab_dna15", "Build a Gene Lab and collect 15 DNA", 80, g => g.dnaTotal >= 15, g => g.dnaTotal, 15, "Put a GENE LAB on a conveyor. Every sheep that rides through gives DNA. So does every sheep in the Slaughterhouse. Collect 15."),
            new Objective("splice3", "Unlock a strain and splice 3 sheep", 100, g => g.splicedTotal >= 3, g => g.splicedTotal, 3, "Open GENETICS (N), unlock SILK or CREAM, set the Gene Lab to SPLICE and send three sheep through. Science!"),
            new Objective("silkcream10", "Sell 10 Silk or Cream", 150, g => g.totalSold[(int)Res.Silk] + g.totalSold[(int)Res.Cream] >= 10, g => g.totalSold[(int)Res.Silk] + g.totalSold[(int)Res.Cream], 10, "Shear the Silkfleece, milk the Creamers, belt it into a Market. Luxury, by the bucket."),
            new Objective("gold3", "Own 3 Golden Fleece sheep", 400, g => g.CountStrain(Genetics.Gold) >= 3, g => g.CountStrain(Genetics.Gold), 3, "Unlock GOLDEN FLEECE (20 Silk + DNA), then splice or breed THREE of them. Golden sheep. With my face on them. Eventually."),
            new Objective("hybrid1", "Discover a hybrid strain", 200, g => g.HybridsKnown() >= 1, g => g.HybridsKnown(), 1, "Put two DIFFERENT strains in one pen and wait for a lucky lamb, or gamble with the Gene Lab's CHAOS mode. Find me a HYBRID."),
            new Objective("hybrids_all", "Discover all 6 hybrid strains", 800, g => g.HybridsKnown() >= Genetics.HybridCount, g => g.HybridsKnown(), Genetics.HybridCount, "SIX secret strains. Cotton candy. Chocolate. Wolves. Lightning. Sheep that split. Sheep that will not stay dead. Find them ALL."),
            new Objective("split", "Split a sheep line: a machine with 2 open OUT doors", 80, g => g.SplitMachines() > 0, null, 0, "Give a MACHINE two open OUT doors, each with a belt. Add door filters: rams one way, ewes the other. Sorting is caring."),
            new Objective("steelbelts", "Research Steel Belts", 80, g => g.tech.Contains("express_belts"), null, 0, "Research STEEL BELTS. My sheep are stuck in traffic. Faster belts. Fatter belts."),
            new Objective("steel30", "Have 30 tiles of Steel (or better) belt", 100, g => g.BeltTilesAtLeast(1) >= 30, g => g.BeltTilesAtLeast(1), 30, "Upgrade THIRTY conveyor tiles to Steel. Use UPGRADE ALL BELTS in the Build menu if you are lazy. I am."),
            new Objective("flock25", "Grow the flock to 25 sheep", 150, g => g.Population >= 25, g => g.Population, 25, "TWENTY-FIVE sheep. A small army of wool. Bigger pens, bigger lots, more farms."),
            new Objective("gatekeeper", "Hire a Gate Keeper (UPGRADES)", 250, g => g.PathLevel("gate_auto") >= 1, null, 0, "Buy a GATE KEEPER in UPGRADES. A sheepdog in a cap opens the Sheep Gate for you. Delegation!"),
            new Objective("cheese_research", "Research Cheese Making", 80, g => g.tech.Contains("cheese"), null, 0, "Research CHEESE MAKING. Milk is for peasants. Cheese is for empires."),
            new Objective("cheese10", "Build a Creamery and make 10 Cheese", 120, g => g.totalMade[(int)Res.Cheese] >= 10, g => g.totalMade[(int)Res.Cheese], 10, "Build a CREAMERY, belt the milk INTO it, and make 10 cheese. Three milk in, one cheese out."),
            new Objective("cleaning_research", "Research Wool Cleaning", 80, g => g.tech.Contains("wool_cleaning"), null, 0, "Research WOOL CLEANING. Dirty wool sells cheap. Clean wool sells less cheap."),
            new Objective("cleanwool10", "Produce 10 Clean Wool", 100, g => g.totalMade[(int)Res.CleanWool] >= 10, g => g.totalMade[(int)Res.CleanWool], 10, "Build a WOOL WORKSHOP and belt raw wool into it. Make 10 CLEAN WOOL."),
            new Objective("beltgoods100", "Deliver 100 goods into Markets by conveyor", 150, g => g.marketBeltSales >= 100, g => g.marketBeltSales, 100, "Belt ONE HUNDRED loads into Markets. Build more Markets if the belts back up."),
            new Objective("pen3", "Upgrade a Breeding Pen to Level 3", 200, g => g.HasBuildingLevel(BKind.Pen, 3), null, 0, "Upgrade a pen to a BREEDING RANCH. Room for thirty-four. Lambs everywhere."),
            new Objective("yarn10", "Produce 10 Yarn", 200, g => g.totalMade[(int)Res.Yarn] >= 10, g => g.totalMade[(int)Res.Yarn], 10, "Spin 10 YARN. Cats are banned."),
            new Objective("earn5000", "Earn 5,000 coins in total", 250, g => g.totalMade[0] >= 5000, g => g.totalMade[0], 5000, "Earn 5,000 coins. Money is the only language the moon understands."),
            new Objective("flock40", "Grow the flock to 40 sheep", 400, g => g.Population >= 40, g => g.Population, 40, "FORTY sheep. Feeding Halls. Mega belts. A palace basement full of snoring."),
            new Objective("milestone25", "Take any upgrade path to level 25", 500, g => g.MaxPathLevel(25) >= 25, g => g.MaxPathLevel(25), 25, "Level 25 on an upgrade path is the next MILESTONE: its bonus doubles AGAIN. Then 50. Then 100. Collect them, Hoof."),
            new Objective("textile", "Build a Textile Factory", 300, g => g.HasBuilding(BKind.Textile), null, 0, "Build a TEXTILE FACTORY. Real industry. Real danger."),
            new Objective("megabelts", "Research Mega Belts", 300, g => g.tech.Contains("mega_belts"), null, 0, "Research MEGA BELTS. Wider. Faster. Louder."),
            new Objective("clothing5", "Produce 5 Clothing", 400, g => g.totalMade[(int)Res.Clothing] >= 5, g => g.totalMade[(int)Res.Clothing], 5, "Make 5 CLOTHING. Tiny sweaters for humans."),
            new Objective("anylevel3", "Get any building to Level 3", 500, g => g.MaxBuildingLevel() >= 3, null, 0, "Reach LEVEL 3. Smokestacks. Pipes. Regret."),
            new Objective("luxury3", "Make 3 Luxury Goods", 800, g => g.totalMade[(int)Res.Luxury] >= 3, g => g.totalMade[(int)Res.Luxury], 3, "Make 3 LUXURY GOODS. Rich humans will cry with joy."),
            new Objective("plant", "Build the Industrial Wool Plant", 1000, g => g.HasBuilding(BKind.Plant), null, 0, "Build THE MACHINE on the conveyor. The Industrial Wool Plant. Do not look inside."),
            new Objective("anylevel4", "Get any building to Level 4", 1500, g => g.MaxBuildingLevel() >= 4, null, 0, "LEVEL 4. Neon. Skyscrapers. A skyline built on fluff."),
            new Objective("flock80", "Grow the flock to 80 sheep", 2000, g => g.Population >= 80, g => g.Population, 80, "EIGHTY sheep. An empire needs subjects. And feeding factories."),
            new Objective("drove5", "Buy 5 levels of Bigger Droves (UPGRADES)", 1500, g => g.PathLevel("drove_size") >= 5, g => g.PathLevel("drove_size"), 5, "Past 24 named sheep a room keeps a HERD, and the herd follows the named ones out of the doors. Buy BIGGER DROVES (U): one sheep leads, a crowd follows, the machines take them all at once."),
            new Objective("flock250", "Grow the flock to 250 sheep", 2500, g => g.Population >= 250, g => g.Population, 250, "TWO HUNDRED AND FIFTY sheep. I have stopped learning their names. SHEEP STACKING (U) makes room for more."),
            new Objective("flock1000", "Grow the flock to 1,000 sheep", 5000, g => g.Population >= 1000, g => g.Population, 1000, "ONE THOUSAND sheep. A herd. A horde. A HERD-HORDE. Feed it."),
            new Objective("moongrass", "Research the Moon Grass Hypothesis", 1500, g => g.tech.Contains("moon_grass"), null, 0, "Research the MOON GRASS HYPOTHESIS. I have a feeling about the moon."),
            new Objective("rocket", "Build the Rocket Pad", 0, g => g.HasBuilding(BKind.Rocket), null, 0, "Build the ROCKET PAD. Someone has to check."),
            new Objective("launch", "Launch a sheep into space", 0, g => g.flags.Contains("launched"), null, 0, "LAUNCH a sheep into space. Choose someone expendable. So, anyone."),
            new Objective("singularity", "Reach the Wool Singularity (any building Level 5)", 0, g => g.MaxBuildingLevel() >= 5, null, 0, "Reach LEVEL 5. The Wool Singularity. Then even I will be afraid."),
            new Objective("flock10k", "Grow the flock to 10,000 sheep", 25000, g => g.Population >= 1e4, g => g.Population, 1e4, "TEN THOUSAND sheep. Stack them. Herd them. Count them, if you must."),
            new Objective("flock1m", "Grow the flock to 1,000,000 sheep", 0, g => g.Population >= 1e6, g => g.Population, 1e6, "ONE MILLION SHEEP, Hoof. Every one of them mine. Most of them nameless. All of them fluffy."),
        };

        /// the decree with this id, or -1
        public static int ObjectiveIndexOf(string id) { for (int i = 0; i < Objectives.Length; i++) if (Objectives[i].id == id) return i; return -1; }

        /// The decrees as a save of `version` numbered them (versions up to 9 saved an index). Frozen: never edit, only add
        /// a table when a new version changes the list. v9 had these 43; v8 lacked the two hybrid decrees, v6-v7 also the five
        /// genetics decrees, and v2-v5 also "split" (that one was shifted with the wrong index before v10: saves at 11-17 landed early).
        static readonly string[] DecreesV9 = { "shear15", "feed30", "market", "beltcoins150", "farm2", "flock16", "lot2", "mutton20", "pen2", "lab", "smartdoors", "genetics", "genelab_dna15", "splice3", "silkcream10", "gold3", "hybrid1", "hybrids_all", "split", "steelbelts", "steel30", "flock25", "cheese_research", "cheese10", "cleaning_research", "cleanwool10", "beltgoods100", "pen3", "yarn10", "earn5000", "flock40", "textile", "megabelts", "clothing5", "anylevel3", "luxury3", "plant", "anylevel4", "flock80", "moongrass", "rocket", "launch", "singularity" };
        public static string[] DecreeIdsFor(int version)
        {
            var l = new List<string>(DecreesV9);
            if (version < 9) l.RemoveRange(16, 2);
            if (version < 8) l.RemoveRange(11, 5);
            if (version < 6) l.Remove("split");
            return l.ToArray();
        }
        /// machines with two or more open OUT doors
        public int SplitMachines() { int n = 0; foreach (var b in buildings) if (IsSheepMachine(b) && b.Built && SheepExitCount(b) >= 2) n++; return n; }

        public Objective CurrentObjective { get { return objectiveIndex < Objectives.Length ? Objectives[objectiveIndex] : null; } }

        void CheckObjectives()
        {
            var o = tutDone ? CurrentObjective : null;
            if (o != null && flags.Contains(o.skipFlag)) { flags.Remove(o.skipFlag); objectiveIndex++; return; } // done in the tutorial
            if (o != null && o.done(this))
            {
                objectiveIndex++;
                while (CurrentObjective != null && flags.Contains(CurrentObjective.skipFlag)) { flags.Remove(CurrentObjective.skipFlag); objectiveIndex++; }
                if (o.reward > 0) { CreditCoins(o.reward, CoinSrc.Decree); totalMade[0] += o.reward; }
                Sfx.Play("fanfare", 0.6f);
                Log("^gDecree fulfilled:^w " + o.text + (o.reward > 0 ? " ^y+" + o.reward + "c" : ""));
                var next = CurrentObjective;
                leaderWaveT = 1.5f;
                LeaderPop("Decree fulfilled!" + (o.reward > 0 ? " +" + o.reward + " coins." : "") + (next != null ? " NEXT: " + (next.say ?? next.text) : " That was the last one. I am almost... proud."), LExpr.Smug);
            }
            string tier = TierName();
            if (tier != lastTier)
            {
                if (lastTier != "")
                {
                    Log("^yNew settlement tier: " + tier + "!");
                    if (!IsWaiting("tier")) tierBefore = lastTier;
                    string tn = tier;
                    if (tier == tierBefore) DropWaiting("tier"); // back where it was before he could say anything: no news
                    else AnnounceWhenFree("tier", () =>
                    {
                        tierShownT = 4f; Sfx.Play("fanfare", 0.8f);
                        if (tutDone) AdviseNow("tier", "We are now a " + tn.ToUpper() + "! History will be written in wool. Mostly by me.", LExpr.Smug, 0.5f);
                    });
                }
                lastTier = tier;
            }
        }

        string tierBefore; // the settlement tier before the change that is waiting to be announced

        public string TierName()
        {
            double pop = Population; int bc = CountBuildings();
            if (MaxBuildingLevel() >= 5) return flags.Contains("launched") ? "Interplanetary Wool Singularity" : "The Wool Singularity";
            if (flags.Contains("launched")) return "Interplanetary Sheep Civilization";
            if (MaxBuildingLevel() >= 4 && pop >= 40) return "Sheep Metropolis";
            if (tech.Contains("space_program")) return "Absurd Sheep Civilization";
            if (HasBuilding(BKind.Plant) && pop >= 25) return "Wool Megacorporation";
            if (MaxBuildingLevel() >= 3 && pop >= 18) return "Industrial Wool Town";
            if (HasBuilding(BKind.Textile) && pop >= 12) return "Wool Town";
            if (pop >= 10 && bc >= 7) return "Sheep Village";
            if (pop >= 6 && bc >= 4) return "Sheep Settlement";
            if (pop >= 4) return "Small Flock";
            return "Sad Little Field";
        }

        // ---------------- daily tick ----------------
        void OnNewDay()
        {
            MeasureDayIncome();
            RollDayStats();
            HerdDayLog(); // the herds' deaths of the day that ended
            DailyStrains();
            // prices random walk, mean reverting
            foreach (var rr in ResInfo.Sellable)
            {
                int i = (int)rr;
                float before = priceMod[i];
                priceMod[i] += (1f - priceMod[i]) * 0.18f + (float)(rng.NextDouble() - 0.5) * 0.14f;
                priceMod[i] = Mathf.Clamp(priceMod[i], 0.35f, 1.8f);
                priceTrend[i] = priceMod[i] - before;
            }
            if (dayIndex % 2 == 0) RefreshOffers();

            // wages
            int workers = 0;
            foreach (var s in sheep) if (!s.dead && s.job != Job.Grazer && !s.Lamb) workers++;
            if (wagePolicy == 1)
            {
                float cost = workers * 0.5f;
                if (res[(int)Res.Food] >= cost) res[(int)Res.Food] -= cost; else { wagePolicy = 0; Log("^rOut of carrots for wages.^w The workers noticed."); }
            }
            else if (wagePolicy == 2)
            {
                float cost = workers * 2f;
                if (res[0] >= cost) SpendCoins(cost, CoinSrc.Wage); else { wagePolicy = 0; Log("^rCan't afford coin wages.^w Wage policy set to Nothing."); AddGlobalMod("Wages cut", -10, 2); }
            }

            // bank interest
            var bank = FindBuilding(BKind.Bank);
            if (bank != null && bank.Built)
            {
                float e = AutoCrew(bank);
                if (e > 0)
                {
                    int interest = (int)System.Math.Min(System.Math.Round(res[0] * 0.006 * e * bank.level), 150 * bank.level); // a double: no overflow
                    if (interest > 0) { CreditCoins(interest, CoinSrc.Interest); totalMade[0] += interest; Log("^yBank interest: +" + interest + "c^w. The sheep are confused but impressed."); }
                }
            }

            // contracts
            var office = FindBuilding(BKind.Trading);
            if (office != null && office.Built)
            {
                if (contract != null && dayIndex >= contract.deadline) { Log("^rContract expired.^w The humans are disappointed in you."); contract = null; }
                if (contract == null && dayIndex % 2 == 0) NewContract(office);
            }
            else contract = null;

            // per-sheep daily rolls
            foreach (var s in sheep.ToArray())
            {
                if (s.dead || s.act == Act.Hidden || s.Lamb) continue;
                if (!tutDone) continue;
                if (s.mood < 25 && !s.Has("loyal") && rng.NextDouble() < 0.35 && s.refuse <= 0) { s.refuse = 1f; FloatText(s.pos + Vector2.up * 1.3f, "NOPE", new Color(1, 0.4f, 0.4f)); }
                if (s.mood < 12 && !s.Has("loyal") && rng.NextDouble() < 0.12) { Escape(s); continue; }
                if (s.Has("greedy") && rng.NextDouble() < 0.06 && res[0] > 30)
                {
                    int amt = 5 + rng.Next(20);
                    SpendCoins(amt, CoinSrc.Theft);
                    Log("^o" + s.name + "^w pocketed " + amt + " coins. " + Cap(s.He) + " denies everything.");
                }
                if (s.Has("sickly") && rng.NextDouble() < 0.05) { s.sick = 1f; Log("^k" + s.name + " is sick again."); }
                if (s.Has("philosopher") && rng.NextDouble() < 0.08) { AddRP(8); FloatText(s.pos + Vector2.up * 1.3f, "*a Thought*", new Color(0.55f, 0.92f, 0.95f)); Log("^c" + s.name + "^w had a Thought. +8 RP. Nobody understands it."); }
                if (s.Has("genius") && rng.NextDouble() < 0.15) { AddRP(12); FloatText(s.pos + Vector2.up * 1.3f, "*an Idea*", new Color(0.55f, 0.92f, 0.95f)); }
                if (s.Has("dramatic") && rng.NextDouble() < 0.06) { s.unconscious = 6; FloatText(s.pos + Vector2.up * 1.3f, "*faints*", new Color(1, 0.6f, 0.75f)); }
            }
            // food warning
            if (Season == 2 && DayOfSeason == 8) Log("^oWinter is coming.^w Grass will barely grow. Stock up on food.");
            ScheduleWolves();
            RollDailyEvent();
            Autosave();
        }

        void Escape(Sheep s)
        {
            if (s.dead) return;
            string[] where = { "to join a wolf commune", "to become a cloud", "to start a podcast", "into the woods, screaming", "to find a better manager", "and took the good shears" };
            Log("^r" + s.name + " escaped " + where[rng.Next(where.Length)] + ".^w Morale was too low.");
            RemoveFromJob(s);
            DisbandDrove(s); Ledger(-1);
            s.dead = true; s.deathCause = "Escaped";
            if (!s.herdHand) graveyard.Add(new DeadRecord { name = s.name, cause = "Escaped. Morale was too low.", level = s.level, day = Day, age = s.ageDays, traits = TraitList(s) });
            if (s.go != null) { Destroy(s.go); Destroy(s.shadow.gameObject); s.go = null; }
            sheep.Remove(s);
            if (selSheep == s) selSheep = null;
            AddGlobalMod("Someone escaped", -3, 1);
        }

        void NewContract(Building office)
        {
            Res[] options = { Res.Wool, Res.Milk, Res.CleanWool, Res.Yarn, Res.Fabric, Res.Clothing, Res.Luxury };
            int maxIdx = 2;
            if (tech.Contains("yarn")) maxIdx = 3;
            if (tech.Contains("machinery")) maxIdx = 4;
            if (tech.Contains("loom")) maxIdx = 5;
            if (tech.Contains("branding")) maxIdx = 6;
            var r = options[rng.Next(maxIdx + 1)];
            int amount = Mathf.Max(3, Mathf.RoundToInt(240f / ResInfo.BasePrice[(int)r] * (0.6f + (float)rng.NextDouble())));
            float e = AutoCrew(office);
            float bonus = 1.6f + 0.25f * e + 0.15f * (office.level - 1);
            contract = new Contract { res = r, amount = amount, reward = Mathf.RoundToInt(amount * ResInfo.BasePrice[(int)r] * bonus * CoinTierMult), deadline = dayIndex + 5 };
            Log("^yNew contract:^w deliver " + amount + " " + ResInfo.Names[(int)r] + " for " + contract.reward + "c (Market panel).");
        }

        public void FulfillContract()
        {
            if (contract == null || !Has(contract.res, contract.amount)) return;
            Use(contract.res, contract.amount);
            CreditCoins(contract.reward, CoinSrc.Contract); totalMade[0] += contract.reward;
            Sfx.Play("coin"); Sfx.Play("fanfare", 0.5f);
            Log("^gContract fulfilled! ^y+" + contract.reward + "c");
            contract = null;
        }

        public string DateString() { return Seasons[Season] + " " + DayOfSeason; }
    }
}
