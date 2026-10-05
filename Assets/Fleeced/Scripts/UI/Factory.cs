using System.Collections.Generic;
using UnityEngine;

namespace Fleeced
{
    /// The FACTORY overview (yesterday's numbers: lambs born vs. sheep lost, food grown vs. eaten, what every machine did),
    /// what each building does (flow lines and the name plates over the buildings) and the daily stats behind them.
    public partial class Game
    {
        // ---------------- daily stats ----------------
        public class DayStats
        {
            public int born, slaughtered, sold, died, pop;
            public float eaten;
            public float[] made = new float[ResInfo.Count];
            public Dictionary<int, int> cycles = new Dictionary<int, int>();
        }
        DayStats statSnap, statYesterday;
        public DayStats Yesterday { get { return statYesterday; } }
        public float foodEaten;      // every bite, for the food balance
        public int soldAlive;        // sheep sold alive (Market doors and the SELL button)
        public bool showPlates = true;
        float factoryContentH;

        public void CountAdults(out int rams, out int ewes)
        {
            rams = 0; ewes = 0;
            foreach (var s in sheep) { if (s.dead || s.act == Act.Hidden || s.Lamb) continue; if (s.male) rams++; else ewes++; }
        }

        DayStats TakeStatSnap()
        {
            var d = new DayStats { born = births, slaughtered = slaughtered, sold = soldAlive, died = deaths, eaten = foodEaten, pop = Population };
            for (int i = 0; i < ResInfo.Count; i++) d.made[i] = (float)totalMade[i];
            foreach (var b in buildings) d.cycles[b.id] = b.cyclesDone;
            return d;
        }

        public void ResetDayStats() { statSnap = TakeStatSnap(); statYesterday = null; }

        /// called at the start of every new day: yesterday = now - the snapshot from this time yesterday
        void RollDayStats()
        {
            var now = TakeStatSnap();
            if (statSnap != null)
            {
                var y = new DayStats
                {
                    born = now.born - statSnap.born, slaughtered = now.slaughtered - statSnap.slaughtered, sold = now.sold - statSnap.sold,
                    died = now.died - statSnap.died, eaten = now.eaten - statSnap.eaten, pop = now.pop - statSnap.pop,
                };
                for (int i = 0; i < ResInfo.Count; i++) y.made[i] = now.made[i] - statSnap.made[i];
                foreach (var kv in now.cycles) { int before; y.cycles[kv.Key] = kv.Value - (statSnap.cycles.TryGetValue(kv.Key, out before) ? before : 0); }
                statYesterday = y;
            }
            statSnap = now;
        }

        // ---------------- what every building does ----------------
        /// one line: what goes in and what comes out
        public static string FlowLine(BKind k)
        {
            switch (k)
            {
                case BKind.Pen: return "Home: sheep live here, make lambs";
                case BKind.FeedLot: return "Hungry sheep in, fed sheep out";
                case BKind.Shed: return "Sheep in: WOOL off, sheep out";
                case BKind.Dairy: return "Ewes in: MILK off, ewes out";
                case BKind.Plant: return "Sheep in: LOTS of wool, sheep out";
                case BKind.Slaughter: return "Sheep in: MUTTON out. No way back";
                case BKind.GeneLab: return "Sheep in: DNA, or a new strain";
                case BKind.Market: return "Goods and sheep in: COINS out";
                case BKind.Farm: return "Grows FOOD for the Feeding Lots";
                case BKind.Lab: return "Makes RESEARCH points";
                case BKind.Creamery: return "Milk in: CHEESE out";
                case BKind.Workshop: return "Wool in: CLEAN WOOL, YARN out";
                case BKind.Textile: return "Yarn in: FABRIC, CLOTHING out";
                case BKind.Atelier: return "Clothing + wool in: LUXURY out";
                case BKind.Storage: return "Holds more of every good";
                case BKind.Wheel: return "Makes POWER for big machines";
                case BKind.GuardPost: return "Keeps wolves away";
                case BKind.Trading: return "Big delivery contracts";
                case BKind.Bank: return "Your coins earn interest";
                case BKind.Spa: case BKind.Statue: case BKind.Flowers: case BKind.Graveyard: return "Cheers the flock up";
                case BKind.Sign: return "Fewer machine accidents nearby";
                case BKind.CloneVat: return "Copies a sheep";
                case BKind.Rocket: return "One sheep to the moon";
                case BKind.Palace: return "The Supreme Leader lives here";
            }
            return null;
        }

        /// short name and in -> out icons for the plate over a building (false = no plate)
        public static bool PlateFor(BKind k, out string name, out string inIcon, out string outIcon)
        {
            name = null; inIcon = null; outIcon = null;
            switch (k)
            {
                case BKind.Shed: name = "SHEARING"; inIcon = "sheep"; outIcon = "wool"; return true;
                case BKind.Dairy: name = "MILKING"; inIcon = "sheep"; outIcon = "milk"; return true;
                case BKind.Plant: name = "WOOL PLANT"; inIcon = "sheep"; outIcon = "wool"; return true;
                case BKind.Slaughter: name = "SLAUGHTER"; inIcon = "sheep"; outIcon = "mutton"; return true;
                case BKind.GeneLab: name = "GENE LAB"; inIcon = "sheep"; outIcon = "dna"; return true;
                case BKind.Market: name = "MARKET"; inIcon = "bag"; outIcon = "coin"; return true;
                case BKind.Farm: name = "FARM"; outIcon = "food"; return true;
                case BKind.Lab: name = "LAB"; outIcon = "research"; return true;
                case BKind.Creamery: name = "CREAMERY"; inIcon = "milk"; outIcon = "cheese"; return true;
                case BKind.Workshop: name = "WORKSHOP"; inIcon = "wool"; outIcon = "yarn"; return true;
                case BKind.Textile: name = "TEXTILES"; inIcon = "yarn"; outIcon = "clothing"; return true;
                case BKind.Atelier: name = "ATELIER"; inIcon = "clothing"; outIcon = "luxury"; return true;
                case BKind.Storage: name = "STORAGE"; outIcon = "crate"; return true;
                case BKind.Wheel: name = "POWER"; outIcon = "power"; return true;
                case BKind.GuardPost: name = "GUARDS"; inIcon = "wolf"; outIcon = "shield"; return true;
                case BKind.Trading: name = "TRADING"; inIcon = "crate"; outIcon = "coin"; return true;
                case BKind.Bank: name = "BANK"; inIcon = "coin"; outIcon = "coin"; return true;
                case BKind.CloneVat: name = "CLONING"; inIcon = "sheep"; outIcon = "sheep"; return true;
                case BKind.Rocket: name = "ROCKET"; inIcon = "sheep"; outIcon = "moon"; return true;
            }
            return false;
        }

        /// name plates: what each building is and what it turns into what. L toggles them.
        void DrawBuildingPlates(float sc, List<Rect> placed)
        {
            if (!showPlates || pixelScale < 2) return;
            foreach (var b in buildings)
            {
                string name, inI, outI;
                if (IsRoom(b) || b.construct < 1f && !b.upgrading || !PlateFor(b.def.kind, out name, out inI, out outI)) continue;
                Vector2 g = ToGui(new Vector2(b.x + b.def.w / 2f, b.y + b.def.h + 0.9f), sc);
                float iw = (inI != null ? 10 : 0) + 6 + 10;
                float nw = PixelFont.Width(name);
                float pw = Mathf.Max(nw + 6, iw + 6);
                var pill = new Rect(Mathf.Round(g.x - (iw + 6) / 2), Mathf.Round(g.y - 12), iw + 6, 12);
                var nameR = new Rect(Mathf.Round(g.x - nw / 2), pill.y - 9, nw, 8);
                if (pill.xMax < -20 || pill.x > W + 20 || pill.yMax < -20 || nameR.y > H + 20) continue;
                placed.Add(Rect.MinMaxRect(Mathf.Min(pill.x, nameR.x), nameR.y, Mathf.Max(pill.xMax, nameR.xMax), pill.yMax));
                if (!UI.Repaint) continue;
                PixelFont.DrawOutlined(nameR.x, nameR.y, name, b.damaged ? UI.Bad : UI.Gold);
                UI.Fill(pill, UI.Ink);
                UI.Fill(UI.Inset(pill, 1), new Color(0.11f, 0.09f, 0.08f, 0.92f));
                float ix = pill.x + 3;
                if (inI != null) { UI.Icon(ix, pill.y + 1, inI); ix += 10; }
                PixelFont.Draw(ix, pill.y + 2, ">", UI.Dim, 1, false);
                ix += 6;
                UI.Icon(ix, pill.y + 1, outI);
            }
        }

        // ---------------- inspector helpers ----------------
        /// "runs by itself" line: what the building makes in a day at its current speed
        string AutoOutputText(Building b)
        {
            if (!b.Built) return null;
            float crew = AutoCrew(b), m = Mult(b), day = DayLength;
            switch (b.def.kind)
            {
                case BKind.Farm:
                    {
                        float f = FarmOut(b) * crew * m / 6f * day;
                        return "^gRuns by itself.^w Grows ~^y" + Mathf.RoundToInt(f) + " food a day^w: enough for ~" + Mathf.RoundToInt(f / 4f) + " sheep.";
                    }
                case BKind.Lab: return "^gRuns by itself.^w Makes ~^c" + Mathf.RoundToInt(1.5f * b.LM * crew * m / 5f * day) + " RP a day^w.";
                case BKind.Creamery: return "^gRuns by itself.^w Up to ~^y" + Mathf.RoundToInt(b.LM * crew * m / 6f * day) + " cheese a day^w while milk keeps coming.";
                case BKind.Workshop: return "^gRuns by itself.^w Up to ~^y" + Mathf.RoundToInt(b.LM * crew * m / 5f * day) + " batches a day^w while wool keeps coming.";
                case BKind.Textile: return "^gRuns by itself.^w Up to ~^y" + Mathf.RoundToInt(b.LM * crew * m / 8f * day) + " batches a day^w while yarn keeps coming.";
                case BKind.Atelier: return "^gRuns by itself.^w Up to ~^y" + Mathf.RoundToInt(b.LM * crew * m / 14f * day) + " luxury goods a day^w.";
                case BKind.Market: return "^gRuns by itself.^w Sells everything that rides in (+10%), live sheep for ~" + LiveSheepPrice(null, b) + "c each, and stored goods marked AUTO.";
                case BKind.Wheel: return "^gRuns by itself.^w " + Mathf.RoundToInt(WheelPower(b.level) * crew) + " power.";
                case BKind.GuardPost: return "^gRuns by itself.^w Guard power " + (crew * (1 + 0.5f * (b.level - 1))).ToString("0.0") + ".";
                case BKind.Bank: case BKind.Trading: case BKind.Storage: return "^gRuns by itself.";
            }
            return null;
        }

        static float MachineSpeedAt(int level) { return BuildingDef.At(new[] { 1f, 1.35f, 1.8f, 2.5f, 3.5f }, level); }
        static float MuttonMultAt(int level) { return BuildingDef.At(new[] { 1f, 1.25f, 1.5f, 2f, 2.6f }, level); }

        /// what the next level gives, shown next to the UPGRADE button
        /// what the next level gives, plus the rare goods it needs (Level 4-5)
        public string UpgradeEffect(Building b)
        {
            string e = UpgradeEffectBase(b);
            string rare = b.level < b.def.maxLevel ? RareUpgradeText(b) : null;
            if (rare != null) e = (e ?? "Next level") + ". ^wAlso needs ^p" + rare;
            return e;
        }

        string UpgradeEffectBase(Building b)
        {
            int n = b.level + 1;
            if (n > b.def.maxLevel) return null;
            switch (b.def.kind)
            {
                case BKind.Pen:
                    return "Next level: holds ^y" + RoomCapAt(false, n) + "^w (now " + RoomCapacity(b) + "), lambs ^yx" + BuildingDef.At(PenBreed, n).ToString("0.0#") + "^w faster (now x" + BuildingDef.At(PenBreed, b.level).ToString("0.0#") + ")";
                case BKind.FeedLot:
                    return "Next level: holds ^y" + RoomCapAt(true, n) + "^w (now " + RoomCapacity(b) + "), eats ^yx" + (BuildingDef.At(new[] { 1f, 1.4f, 1.9f, 2.6f, 3.5f }, n) / BuildingDef.At(new[] { 1f, 1.4f, 1.9f, 2.6f, 3.5f }, b.level)).ToString("0.0#") + "^w faster";
                case BKind.Shed: case BKind.Dairy:
                    return "Next level: ^yx" + (MachineSpeedAt(n) / MachineSpeedAt(b.level)).ToString("0.0#") + "^w faster, ^yx" + BuildingDef.At(ShedYield, n).ToString("0.0#") + "^w " + MachineGoods(b) + " per sheep (now x" + BuildingDef.At(ShedYield, b.level).ToString("0.0#") + ")";
                case BKind.Plant:
                    return "Next level: ^yx" + (MachineSpeedAt(n) / MachineSpeedAt(b.level)).ToString("0.0#") + "^w faster";
                case BKind.GeneLab:
                    return "Next level: ^yx" + (MachineSpeedAt(n) / MachineSpeedAt(b.level)).ToString("0.0#") + "^w faster, ^p" + Mathf.Min(3, n) + " DNA^w per sample, splices 1 DNA cheaper";
                case BKind.Slaughter:
                    return "Next level: ^yx" + (MachineSpeedAt(n) / MachineSpeedAt(b.level)).ToString("0.0#") + "^w faster, ^yx" + MuttonMultAt(n).ToString("0.0#") + "^w mutton per sheep (now x" + MuttonMultAt(b.level).ToString("0.0#") + ")";
                case BKind.Farm: case BKind.Lab: case BKind.Creamery: case BKind.Workshop: case BKind.Textile: case BKind.Atelier:
                    {
                        float now = BuildingDef.At(BuildingDef.LevelMult, b.level) * Mathf.Max(1f, BuildingDef.At(b.def.slots, b.level) * 0.6f);
                        float next = BuildingDef.At(BuildingDef.LevelMult, n) * Mathf.Max(1f, BuildingDef.At(b.def.slots, n) * 0.6f);
                        return "Next level: ^yx" + (next / now).ToString("0.0#") + "^w output";
                    }
                case BKind.Storage: return "Next level: +" + StorageCapOf(n) * 1.25f + " storage (now +" + StorageCapOf(b.level) * 1.25f + ")";
            }
            return null;
        }

        // ---------------- the FACTORY panel ----------------
        void FLine(ref float y, float aw, string text)
        {
            float h = PixelFont.WrappedHeight(text, aw);
            PixelFont.DrawWrapped(new Rect(0, y, aw, h), text, UI.Text);
            y += h + 1;
        }

        /// the one-line verdict at the top of the FACTORY panel
        public string FactoryVerdict()
        {
            int pop = Population;
            var yd = statYesterday;
            if (popCap > 0 && pop >= popCap) return "^oThe pens are FULL: no more lambs. Upgrade or build a Breeding Pen, or slaughter / sell the extras.";
            int rams, ewes; CountAdults(out rams, out ewes);
            if (pop > 0 && (rams == 0 || ewes == 0)) return "^rNo " + (rams == 0 ? "RAMS" : "EWES") + " left: no lamb will ever be born. Buy one in the Market panel.";
            if (Mathf.Min(rams, ewes) * 2 < Mathf.Max(rams, ewes))
                return "^oOnly " + Mathf.Min(rams, ewes) + " breeding couples (" + rams + " rams, " + ewes + " ewes). Keep the " + (rams > ewes ? "ewes; slaughter the extra RAMS (door filter RAMS)." : "rams; slaughter the extra EWES (door filter EWES).");
            if (yd != null && yd.eaten > yd.made[(int)Res.Food] + 4 && res[(int)Res.Food] < 150) return "^rThe flock eats more food than the farms grow. Build or upgrade Farms.";
            if (yd != null && yd.slaughtered + yd.sold > yd.born + 1) return "^oYou are thinning the flock faster than it breeds. Fewer sheep means less wool and milk tomorrow.";
            if (yd != null && yd.born > 0 && pop < popCap) return "^gThe flock is growing. Keep the Feeding Lots and Farms ahead of it.";
            return "^kKeep the doors routing: Pen > Feeding Lot > machines > back to the Pen. Slaughter only what you cannot feed or house.";
        }

        void DrawFactoryPanel()
        {
            var r = PanelRect();
            if (!PanelFrame(r, "FACTORY", "chart")) return;
            float x = r.x + 8, w = r.width - 16, y = r.y + UI.HeaderH;
            var area = new Rect(x, y, w, r.yMax - 8 - y);
            float aw = area.width - 6;
            UI.BeginScroll(area, ref scrollJobs, factoryContentH);
            float yy = 0;
            int pop = Population, lambs = 0, rams = 0, ewes = 0, fluffy = 0, milky = 0;
            foreach (var s in sheep)
            {
                if (s.dead || s.act == Act.Hidden) continue;
                if (s.Lamb) lambs++; else if (s.male) rams++; else ewes++;
                if (!s.Lamb && s.wool >= 0.98f) fluffy++;
                if (!s.Lamb && !s.male && s.milk >= 0.98f) milky++;
            }
            var yd = statYesterday;
            FLine(ref yy, aw, FactoryVerdict());
            yy += 3;

            // the flock: the one resource everything else comes from
            UI.Heading(0, yy, "FLOCK"); yy += 12;
            FLine(ref yy, aw, "^w" + pop + "^k sheep, ^w" + popCap + "^k pen space.  ^w" + rams + "^k rams, ^w" + ewes + "^k ewes, ^p" + lambs + "^k lambs.");
            UI.Bar(new Rect(0, yy, aw, 8), popCap > 0 ? Mathf.Clamp01(pop / (float)popCap) : 1f, pop >= popCap ? UI.Bad : UI.Good, pop + " / " + popCap); yy += 11;
            FLine(ref yy, aw, "^kReady now: ^w" + fluffy + "^k fluffy, ^w" + milky + "^k full udders.");
            if (yd == null) FLine(ref yy, aw, "^kYesterday's numbers appear after one full day.");
            else
            {
                int net = yd.born - yd.slaughtered - yd.sold - yd.died;
                FLine(ref yy, aw, "^kYesterday: ^p+" + yd.born + " born^k, ^r-" + yd.slaughtered + " slaughtered^k, ^o-" + yd.sold + " sold^k, ^r-" + yd.died + " died^k.  Net " + (net > 0 ? "^g+" : net < 0 ? "^r" : "^w") + net);
            }
            yy += 3;

            // food
            UI.Heading(0, yy, "FOOD"); yy += 12;
            if (yd != null)
            {
                float made = yd.made[(int)Res.Food], ate = yd.eaten, bal = made - ate;
                FLine(ref yy, aw, "^kYesterday: grown ^g" + Mathf.RoundToInt(made) + "^k, eaten ^o" + Mathf.RoundToInt(ate) + "^k.  " + (bal >= 0 ? "^g+" : "^r") + Mathf.RoundToInt(bal) + "^k a day.");
            }
            FLine(ref yy, aw, "^kIn store: ^y" + UI.Fmt(res[(int)Res.Food]) + "^k. The flock needs ~^w" + Mathf.RoundToInt((pop - lambs) * 4f) + "^k a day.");
            yy += 3;

            // what the factory made
            UI.Heading(0, yy, "MADE YESTERDAY"); yy += 12;
            if (yd == null) FLine(ref yy, aw, "^k(wait for tomorrow)");
            else
            {
                string made = "";
                foreach (var rr in new[] { Res.Wool, Res.Milk, Res.Mutton, Res.Silk, Res.Cream, Res.GoldFleece, Res.GlowWool, Res.ChocoMilk, Res.CottonCandy, Res.Cheese, Res.CleanWool, Res.Yarn, Res.Fabric, Res.Clothing, Res.Luxury })
                    if (yd.made[(int)rr] >= 0.5f) made += "^w" + Mathf.RoundToInt(yd.made[(int)rr]) + "^k " + ResInfo.Names[(int)rr].ToLower() + ", ";
                FLine(ref yy, aw, made == "" ? "^kNothing yet." : made.Substring(0, made.Length - 2) + ".");
                FLine(ref yy, aw, "^kCoins earned: ^y" + Mathf.RoundToInt(yd.made[0]) + "^k.");
            }
            yy += 3;

            // rooms
            UI.Heading(0, yy, "ROOMS"); yy += 12;
            int nRooms = 0;
            foreach (var b in buildings)
            {
                if (!IsRoom(b) || !RoomActive(b)) continue;
                nRooms++;
                var row = new Rect(0, yy, aw, 12);
                if (UI.Clickable(row, b.def.name.ToUpper() + "\n" + b.status, true)) { selBuilding = b; selSheep = null; FocusOn(b.Center); }
                string extra = IsPen(b) ? "lambs x" + PenBreedMult(b).ToString("0.0#") : "eats " + LotEatRate(b).ToString("0.0") + "/s";
                PixelFont.Draw(2, yy + 2, "^y" + Trunc(b.def.name, 14) + " L" + b.level + "^w " + b.penCount + "/" + RoomCapacity(b) + "^k  " + extra, UI.Text);
                yy += 13;
            }
            if (nRooms == 0) FLine(ref yy, aw, "^rNo rooms! Build a Breeding Pen.");
            yy += 3;

            // machines: what each one did yesterday
            UI.Heading(0, yy, "MACHINES"); yy += 12;
            int nMach = 0;
            foreach (var b in buildings)
            {
                if (!IsSheepMachine(b) && b.def.kind != BKind.Market) continue;
                nMach++;
                int did = -1;
                if (yd != null) yd.cycles.TryGetValue(b.id, out did);
                var row = new Rect(0, yy, aw, 22);
                if (UI.Clickable(row, b.def.name.ToUpper() + "\n" + b.status, true)) { selBuilding = b; selSheep = null; FocusOn(b.Center); }
                string what = b.def.kind == BKind.Market ? "" : yd == null ? "" : "^w" + Mathf.Max(0, did) + "^k sheep yesterday";
                PixelFont.Draw(2, yy + 1, "^y" + Trunc(b.def.name, 16) + " L" + b.level + "  " + what, UI.Text);
                bool bad = b.status.StartsWith("NO WAY") || b.status.StartsWith("Not connected") || b.status.StartsWith("IN door") || b.status.StartsWith("Exit");
                PixelFont.Draw(10, yy + 11, (bad ? "^o" : "^k") + Trunc(b.status, 34), UI.Text);
                yy += 24;
            }
            if (nMach == 0) FLine(ref yy, aw, "^kNo machines yet.");
            yy += 3;
            FLine(ref yy, aw, "^kEvery sheep slaughtered or sold is wool and milk you never get again. Breed faster (pen upgrades, Matchmaking) or slaughter slower.");
            factoryContentH = yy + 4;
            UI.EndScroll();
        }
    }
}
