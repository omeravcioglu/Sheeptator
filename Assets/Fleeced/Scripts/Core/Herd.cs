using System.Collections.Generic;
using UnityEngine;

namespace Fleeced
{
    /// The counted sheep of one room (the herd, M11): whole numbers of ewes, rams and lambs of each strain, sharing one fleece,
    /// one set of udders and one appetite (the averages over all of them). Doubles, because a herd can hold millions.
    public class Flock
    {
        public const int Strains = 14; // Genetics.None (plain) .. Genetics.Phoenix
        public readonly double[] ewes = new double[Strains], rams = new double[Strains], lambs = new double[Strains];
        /// the fleece of all of them (0..1.25), the ewes' udders (0..1) and the adults' hunger, as one sheep has them
        public float wool = 0.5f, milk, hunger = 0.2f;
        /// fractions carried from step to step: lambs on the way, lambs growing up, starving, meals, splits, wolf bites
        public double breedT, matureT, starveT, mealT, splitT, wolfT;

        public double Ewes { get { return Sum(ewes); } }
        public double Rams { get { return Sum(rams); } }
        public double Lambs { get { return Sum(lambs); } }
        public double Adults { get { return Ewes + Rams; } }
        public double Total { get { return Adults + Lambs; } }
        public double[] Sex(bool male) { return male ? rams : ewes; }
        public double Of(int strain) { return ewes[strain] + rams[strain] + lambs[strain]; }
        static double Sum(double[] a) { double n = 0; for (int i = 0; i < a.Length; i++) n += a[i]; return n; }

        /// the strain most of them have (plain on a tie)
        public int MainStrain()
        {
            int best = 0; double bn = Of(0);
            for (int i = 1; i < Strains; i++) { double n = Of(i); if (n > bn) { bn = n; best = i; } }
            return best;
        }
    }

    /// Herd sheep following a named sheep out of a room: the same sex and strain as their leader, with the fleece, udders and
    /// appetite they left with. They ride with it, go through the machines with it and join the herd of the room it reaches.
    public class Drove
    {
        public double n; public float wool, milk, hunger;
        public string label; public double labelN = -1; // its "+24" on screen, built again only when n changes
    }

    /// The herd (M11). Every sheep keeps a name while its room has fewer than NamedRoomCap named sheep and the farm fewer than
    /// NamedCap; past that, new sheep join the room's counted herd. A named sheep leaving a room through a door takes a DROVE of
    /// the herd along (its own sex and strain, as many as Bigger Droves allows and the door's filter lets through), and the
    /// drove is shorn, milked, spliced, sold or slaughtered with it. When a herd has nobody of its kind to follow out, one of
    /// it steps forward as a herd hand: a named sheep with no history, which melts back into the herd once it is not needed.
    public partial class Game
    {
        public const int NamedCap = 300, NamedRoomCap = 24, MinLeaders = 4;
        /// herd sheep a named sheep takes along out of a door before Bigger Droves
        public const double DroveBase = 4;
        public double DroveSize { get { return System.Math.Floor(DroveBase + PathAdd("drove_size")); } }

        // ---------------- counting ----------------
        /// named sheep alive (the sheep on screen)
        public int NamedAlive { get { int n = 0; foreach (var s in sheep) if (!s.dead) n++; return n; } }
        /// sheep in herds and in droves
        public double HerdCount
        {
            get
            {
                double n = 0;
                foreach (var b in buildings) if (b.herd != null) n += b.herd.Total;
                foreach (var s in sheep) if (!s.dead && s.drove != null) n += s.drove.n;
                return n;
            }
        }
        /// a room's sheep, for its capacity: its named members (with the ones out on the belts) and its herd
        public double Members(Building room) { return room.penMembers + (room.herd != null ? room.herd.Total : 0); }
        /// the sheep inside a room right now: named and herd
        public double Inside(Building room) { return room.penCount + (room.herd != null ? room.herd.Total : 0); }
        /// may a new sheep here have a name? (null = anywhere on the farm)
        public bool CanName(Building room) { return NamedAlive < NamedCap && (room == null || room.penMembers < NamedRoomCap); }
        public double HerdIn(Building room) { return room != null && room.herd != null ? room.herd.Total : 0; }
        Flock HerdOf(Building room) { if (room.herd == null) room.herd = new Flock(); return room.herd; }
        public int FollowersOf(Sheep s) { return s.drove != null ? (int)System.Math.Min(int.MaxValue, s.drove.n) : 0; }

        /// adds n to a statistic, stopping at int.MaxValue (a herd counts in millions; the statistics stay ints in the save)
        public static int Bump(int c, double n)
        {
            double v = c + n;
            return v >= int.MaxValue ? int.MaxValue : v <= int.MinValue ? int.MinValue : (int)v;
        }

        /// the average of a value over `had` sheep once `n` more join with value v
        static float Mix(float cur, double had, float v, double n)
        {
            double t = had + n;
            return t <= 0 ? v : (float)((cur * had + v * n) / t);
        }

        /// Every change to the number of sheep goes through here in test builds, so the harness can check that no step makes
        /// or loses a sheep the code did not mean to (DevHooks.CheckFlock). Release builds compile the calls away.
        [System.Diagnostics.Conditional("FLEECED_DEV")]
        void Ledger(double n)
        {
#if FLEECED_DEV
            DevHooks.Ledger += n;
#endif
        }

        // ---------------- joining a herd ----------------
        /// n adults of one sex and strain join a room's herd with this fleece, udder and hunger
        void HerdAddAdults(Building room, bool male, int strain, double n, float wool, float milk, float hunger)
        {
            if (n <= 0) return;
            var h = HerdOf(room);
            h.wool = Mix(h.wool, h.Total, wool, n);
            h.hunger = Mix(h.hunger, h.Adults, hunger, n);
            if (!male) h.milk = Mix(h.milk, h.Ewes, milk, n);
            h.Sex(male)[strain] += n;
        }

        /// n newborn lambs of a strain join a room's herd, with a lamb's fleece
        void HerdAddLambs(Building room, int strain, double n)
        {
            if (n <= 0) return;
            var h = HerdOf(room);
            h.wool = Mix(h.wool, h.Total, 0.15f, n);
            h.lambs[strain] += n;
        }

        // ---------------- whole sheep ----------------
        readonly double[] share = new double[2 * Flock.Strains], frac = new double[2 * Flock.Strains], scratchW = new double[2 * Flock.Strains];

        /// Splits `total` whole sheep over the buckets in proportion to `w`, into `into` (whole numbers, the remainder by chance).
        /// With w = the buckets' own counts and total at most their sum, no bucket gets more than it holds.
        void Distribute(double total, double[] w, double[] into, int n)
        {
            double W = 0;
            for (int i = 0; i < n; i++) { into[i] = 0; if (w[i] > 0) W += w[i]; }
            total = System.Math.Floor(total);
            if (W <= 0 || total < 1) return;
            double given = 0, fsum = 0;
            for (int i = 0; i < n; i++)
            {
                frac[i] = 0;
                if (w[i] <= 0) continue;
                double x = total * (w[i] / W), f = System.Math.Floor(x);
                into[i] = f; given += f; frac[i] = x - f; fsum += frac[i];
            }
            double left = total - given;
            while (left >= 1 && fsum > 1e-9)
            {
                double r = rng.NextDouble() * fsum; int pick = -1;
                for (int i = 0; i < n; i++)
                {
                    if (frac[i] <= 0) continue;
                    pick = i; r -= frac[i];
                    if (r < 0) break;
                }
                if (pick < 0) break;
                into[pick] += 1; fsum -= frac[pick]; frac[pick] = 0; left -= 1;
            }
        }

        /// a whole number with the expected value x: its floor, and one more with the chance of its fraction
        double Whole(double x)
        {
            if (!(x > 0)) return 0;
            double f = System.Math.Floor(x);
            return f + (rng.NextDouble() < x - f ? 1 : 0);
        }

        /// n of the herd's adults (not of strain `except`) die, spread over them by number. Returns the Phoenixes among them.
        double HerdCut(Flock h, double n, int except)
        {
            int S = Flock.Strains;
            for (int s = 0; s < S; s++) { scratchW[s] = s == except ? 0 : h.ewes[s]; scratchW[S + s] = s == except ? 0 : h.rams[s]; }
            Distribute(n, scratchW, share, 2 * S);
            double phoenix = 0;
            for (int s = 0; s < S; s++)
            {
                h.ewes[s] -= share[s]; h.rams[s] -= share[S + s];
                if (s == Genetics.Phoenix) phoenix += share[s] + share[S + s];
            }
            return phoenix;
        }

        // ---------------- the herd's day ----------------
        /// every step, after the named sheep and the buildings: each room's herd grows up, gets hungry, grows wool and milk,
        /// eats (Feeding Lots) or breeds (Breeding Pens), and finds leaders
        int herdNamed;  // named sheep alive, counted once a step for the herds (Promote and MeltSpare keep it right)
        double herdPop; // the whole flock, counted once a step for the herds (their births add to it)

        bool herdShortStep;      // a herd waited this step for a leader no name was left for
        public float herdStrikeUntil; // the day an ignored strike ends: no herd hand steps out before it (named sheep refuse too)

        void SimHerds(float dt)
        {
            herdNamed = NamedAlive; herdPop = Population;
            herdShortStep = false;
            for (int i = 0; i < buildings.Count; i++)
            {
                var b = buildings[i];
                if (!IsRoom(b) || !RoomActive(b)) continue;
                if (b.promoteCd > 0) b.promoteCd -= dt;
                if (b.herd != null && b.herd.Total >= 1) SimHerd(b, b.herd, dt);
                else
                {
                    if (b.herd != null) { var e = b.herd; e.breedT = 0; e.matureT = 0; e.starveT = 0; e.mealT = 0; }
                    MeltSpare(b);
                }
            }
            if (!herdShortStep) herdNoNamesT = Mathf.Max(0, herdNoNamesT - dt); // a shortage that is over is forgotten
        }

        float gateQuietT; // real seconds since SPACE (or the gate's button) was last held
        /// Every frame: the Supreme Leader introduces the herd once one forms, but only after SPACE has been up for a moment: a player
        /// pumping SPACE at the gate would turn his pages without reading them. (Frame code: nothing here feeds the simulation.)
        void HerdIntroCheck(float rdt)
        {
            gateQuietT = gateHeld ? 0 : gateQuietT + rdt;
            if (flags.Contains("herd_intro") || !tutDone || !started || gateQuietT < 1.5f || HerdCount < 1) return;
            flags.Add("herd_intro");
            AnnounceWhenFree("herd", HerdIntro);
        }

        void SimHerd(Building b, Flock h, float dt)
        {
            float dd = dt / DayLength;
            // lambs grow up, on average after Sheep.LambAge days; half of them are rams
            double lambs = h.Lambs;
            if (lambs >= 1)
            {
                h.matureT += lambs * dd / Sheep.LambAge;
                if (h.matureT >= 1)
                {
                    double grow = System.Math.Min(System.Math.Floor(h.matureT), lambs);
                    h.matureT -= grow;
                    Distribute(grow, h.lambs, share, Flock.Strains);
                    double adults = h.Adults, ewes = h.Ewes, newEwes = 0, grown = 0;
                    for (int s = 0; s < Flock.Strains; s++)
                    {
                        double n = share[s];
                        if (n <= 0) continue;
                        double r = System.Math.Floor(n / 2);
                        if (n - 2 * r >= 1 && rng.NextDouble() < 0.5) r += 1;
                        h.lambs[s] -= n; h.rams[s] += r; h.ewes[s] += n - r; newEwes += n - r; grown += n;
                    }
                    h.hunger = Mix(h.hunger, adults, 0f, grown); // lambs drink milk: they grow up fed
                    h.milk = Mix(h.milk, ewes, 0f, newEwes);
                }
            }
            else h.matureT = 0;
            double adultsNow = h.Adults;
            // hunger, fleece and udders, as for a grazer with no traits
            if (adultsNow >= 1) h.hunger += dd; else h.hunger = 0;
            if (!tutDone) h.hunger = Mathf.Min(h.hunger, 3.5f);
            float mood = HerdMood(b, h);
            float wg = 0.55f * 1.2f;
            if (HasGene("big_fleece")) wg *= 1.25f;
            if (mood > 75) wg *= 1.15f;
            if (h.hunger > 1.5f) wg *= 0.2f;
            wg *= (float)PathMult("wool_growth") * HerdWoolMult(h);
            h.wool = Mathf.Min(1.25f, h.wool + dd * wg);
            if (h.Ewes >= 1)
            {
                float mg = 0.8f;
                if (HasGene("rich_milk")) mg *= 1.25f;
                if (mood > 70) mg *= 1.15f;
                if (h.hunger > 1.5f) mg *= 0.2f;
                h.milk = Mathf.Min(1f, h.milk + dd * mg);
            }
            if (IsLot(b)) HerdEat(b, h, dt);
            else HerdBreed(b, h, dt, mood);
            // starving: about 30% of them a day while hunger is at its worst (a named sheep dies at once)
            if (h.hunger >= 4.5f && adultsNow >= 1)
            {
                h.hunger = 4.5f;
                h.starveT += adultsNow * dd * 0.3;
                double die = System.Math.Min(System.Math.Floor(h.starveT), adultsNow);
                if (die >= 1) { h.starveT -= die; HerdDie(b, h, die, -1, "starved"); }
            }
            else h.starveT = 0;
            LeadOrMelt(b, h, dt);
        }

        /// the herd's mood: a traitless grazer's, in this room (named sheep keep their own)
        public float HerdMood(Building b, Flock h)
        {
            float t = 63f; // 58, and the grazing life
            if (h.hunger < 0.6f) t += 8; else if (h.hunger > 2f) t -= 40; else if (h.hunger > 1.2f) t -= 18;
            t += decorMood;
            for (int i = 0; i < globalMods.Count; i++) t += globalMods[i].value;
            if (Members(b) > RoomCapacity(b)) t -= 8;
            if (Season == 3 && h.wool < 0.3f) t -= 12;
            if (Season == 1 && h.wool > 0.98f) t -= 6;
            return Mathf.Clamp(t, 0, 100);
        }

        /// Golden Fleece grows its fleece slower, Cotton Candy faster: their share of the herd moves the average
        static float HerdWoolMult(Flock h)
        {
            double t = h.Total;
            if (t <= 0) return 1f;
            double gold = h.Of(Genetics.Gold), candy = h.Of(Genetics.Candy);
            return (float)((t - gold - candy + gold * 0.7 + candy * 1.2) / t);
        }

        /// Jumbos eat half again, Teacups 40%
        static double HerdFoodMult(Flock h)
        {
            double a = h.Adults;
            if (a <= 0) return 1;
            double jumbo = h.ewes[Genetics.Jumbo] + h.rams[Genetics.Jumbo], tea = h.ewes[Genetics.Teacup] + h.rams[Genetics.Teacup];
            return (a - jumbo - tea + jumbo * 1.5 + tea * 0.4) / a;
        }

        /// a Feeding Lot's herd eats from the food store, all at once, at the lot's eating speed
        void HerdEat(Building b, Flock h, float dt)
        {
            double adults = h.Adults;
            if (adults < 1 || h.hunger <= 0.12f || res[(int)Res.Food] < 0.4) return;
            float bite = Mathf.Min(h.hunger, dt * LotEatRate(b));
            double cost = bite * 4.0 * HerdFoodMult(h) * adults;
            if (res[(int)Res.Food] < cost) { bite *= (float)(res[(int)Res.Food] / cost); cost = res[(int)Res.Food]; }
            res[(int)Res.Food] -= cost; foodEaten += cost;
            h.hunger = Mathf.Max(0, h.hunger - bite);
            h.mealT += bite * adults / 0.8; // a meal: about 0.8 of hunger eaten
            if (h.mealT >= 1) { double m = System.Math.Floor(h.mealT); h.mealT -= m; lotMeals = Bump(lotMeals, m); }
        }

        /// a Breeding Pen's herd breeds like its named sheep do (fed, happy rams and ewes), all its couples together
        void HerdBreed(Building b, Flock h, float dt, float mood)
        {
            double pairs = System.Math.Min(System.Math.Floor(h.Ewes), System.Math.Floor(h.Rams));
            if (pairs < 1 || h.hunger > 1.2f || mood < 30) return;
            double room = System.Math.Min(RoomCapacity(b) - Members(b), FlockCap - herdPop);
            if (room < 1) return;
            double sp = 0.45 * System.Math.Pow(pairs, 0.6) * (tech.Contains("matchmaking") ? 1.5 : 1) * PenBreedMult(b) * (HasGene("love_songs") ? 1.3 : 1)
                        * Mathf.Clamp(mood / 65f, 0.4f, 1.3f) * PathMult("pen_breed") * (HasGene("twin_genes") ? 1.2 : 1);
            if (!tutDone) sp *= CurStep != null && CurStep.id == "breed" ? 2.5 : 0.6;
            h.breedT += dt / DayLength * sp;
            if (h.breedT < 1) return;
            double born = System.Math.Min(System.Math.Floor(h.breedT), System.Math.Floor(room));
            h.breedT = System.Math.Min(h.breedT - born, 1);
            HerdBirths(b, h, born);
        }

        readonly double[] lambW = new double[Flock.Strains];
        /// the herd's lambs take a parent's strain like a named lamb would (Strong Genes, hybrids and all), by expected value
        void HerdBirths(Building b, Flock h, double born)
        {
            if (born < 1) return;
            double E = h.Ewes, R = h.Rams;
            int S = Flock.Strains;
            for (int i = 0; i < S; i++) lambW[i] = 0;
            float keepBase = HasGene("strong_genes") ? 0.9f : 0.5f, hyChance = HasGene("strong_genes") ? 0.35f : 0.25f;
            for (int i = 0; i < S; i++)
            {
                if (h.ewes[i] <= 0) continue;
                double pe = h.ewes[i] / E;
                for (int j = 0; j < S; j++)
                {
                    if (h.rams[j] <= 0) continue;
                    double p = pe * h.rams[j] / R;
                    if (i == 0 && j == 0) { lambW[0] += p; continue; }
                    if (i != 0 && j != 0 && i != j)
                    {
                        var hy = Genetics.HybridOf(i, j);
                        if (hy != null) { lambW[hy.id] += p * hyChance; p *= 1 - hyChance; }
                        lambW[i] += p * 0.5 * keepBase; lambW[j] += p * 0.5 * keepBase; lambW[0] += p * (1 - keepBase);
                        continue;
                    }
                    int pick = i != 0 ? i : j;
                    float keep = i == j ? Mathf.Max(keepBase, 0.85f) : keepBase;
                    lambW[pick] += p * keep; lambW[0] += p * (1 - keep);
                }
            }
            Distribute(born, lambW, share, S);
            double made = 0;
            for (int s = 0; s < S; s++)
            {
                if (share[s] < 1) continue;
                HerdAddLambs(b, s, share[s]);
                made += share[s];
                var d = Genetics.Get(s);
                if (d != null && d.Hybrid && !StrainKnown(s)) Discover(s, "a lamb of the herd");
            }
            births = Bump(births, made); Ledger(made);
            herdPop += made;
            b.herdShow += made; // shown in bursts by UpdateHerdVisual
        }

        readonly Sheep lambProbe = new Sheep();
        /// a named couple's lamb when the room's names are used up: it joins the herd, with the strain InheritStrain gives it
        void HerdLambOf(Building b, Sheep pa, Sheep pb, Vector2 at)
        {
            lambProbe.strain = 0;
            InheritStrain(lambProbe, pa, pb);
            HerdAddLambs(b, Mathf.Clamp(lambProbe.strain, 0, Flock.Strains - 1), 1);
            births = Bump(births, 1); Ledger(1);
            SpawnParticles(at + Vector2.up * 0.5f, new Color(1f, 0.6f, 0.75f), 8, 1.6f);
            b.herdShow += 1;
        }

        readonly double[] shearByRes = new double[ResInfo.Count];
        /// The heat wave's mass shearing: every herd with some wool is shorn on the spot, into storage, but only as far as storage
        /// has room for what it gives (the rest keeps its fleece, rather than being shorn into nothing). Returns how many.
        double ShearAllHerds(float minWool)
        {
            double n = 0;
            foreach (var b in buildings)
            {
                var h = b.herd;
                if (h == null || h.Adults < 1 || h.wool < minWool) continue;
                for (int r = 0; r < shearByRes.Length; r++) shearByRes[r] = 0;
                for (int s = 0; s < Flock.Strains; s++)
                    for (int m = 0; m < 2; m++)
                    {
                        double c = m == 1 ? h.rams[s] : h.ewes[s];
                        if (c < 1) continue;
                        var p = herdProbe;
                        p.male = m == 1; p.strain = s; p.level = 1; p.variant = Variant.None; p.traits.Clear(); p.ageDays = Sheep.LambAge + 1; p.wool = h.wool;
                        shearByRes[(int)ShearRes(p)] += c * Mathf.Max(1, Mathf.RoundToInt(ShearYield(p, null) * ShearAmount(p)));
                    }
                double f = 1; // the share of the herd storage has room for
                for (int r = 0; r < shearByRes.Length; r++) if (shearByRes[r] > 0) f = System.Math.Min(f, System.Math.Max(0, CapOf((Res)r) - res[r]) / shearByRes[r]);
                if (f <= 0) continue;
                for (int r = 0; r < shearByRes.Length; r++) if (shearByRes[r] > 0) Gain((Res)r, shearByRes[r] * f);
                double shorn = System.Math.Floor(h.Adults * f);
                shearCount = Bump(shearCount, shorn);
                n += shorn;
                h.wool *= (float)(1 - f);
            }
            return n;
        }

        /// n of a room's herd die (`how` for the day's log); the Phoenixes among them usually come back
        void HerdDie(Building b, Flock h, double n, int except, string how)
        {
            if (n < 1) return;
            double phoenix = HerdCut(h, n, except);
            deaths = Bump(deaths, n); Ledger(-n);
            if (how == "starved") b.herdStarved += n; else b.herdEaten += n; // for the evening's log, cause by cause
            if (how == "starved") AdviseNow("herd_starve", "The HERD is STARVING in the " + b.def.name.ToUpper() + ". Droves need a FEEDING LOT, and the lot needs FOOD. Lots of food.", LExpr.Shout, 1f);
            HerdRebirths(b, phoenix);
        }

        /// Phoenixes of a herd that died: most come back as Phoenix lambs in the pen, if it has room (as a named Phoenix does)
        void HerdRebirths(Building room, double phoenixDead)
        {
            if (phoenixDead < 1) return;
            var pen = room != null && IsPen(room) && buildings.Contains(room) && RoomActive(room) ? room : FirstPen();
            if (pen == null) return;
            double back = Whole(phoenixDead * 0.6);
            back = System.Math.Min(back, System.Math.Floor(System.Math.Min(RoomCapacity(pen) - Members(pen), FlockCap - Population)));
            if (back < 1) return;
            HerdAddLambs(pen, Genetics.Phoenix, back);
            births = Bump(births, back); Ledger(back);
            if (IsVisible(pen.Center)) FloatText(pen.Center + Vector2.up * 1.6f, "REBORN x" + UI.Fmt(back), new Color(1f, 0.55f, 0.2f));
        }

        /// once a day, with DailyStrains: the herd's Splitters split and its Wolfsheep get peckish, by expected value
        void DailyHerdStrains()
        {
            foreach (var b in buildings.ToArray())
            {
                var h = b.herd;
                if (h == null || h.Total < 1 || !IsRoom(b) || !RoomActive(b)) continue;
                double split = h.ewes[Genetics.Splitter] + h.rams[Genetics.Splitter];
                if (split >= 1)
                {
                    h.splitT += split * 0.12;
                    double room = System.Math.Floor(System.Math.Min(RoomCapacity(b) - Members(b), FlockCap - Population));
                    double n = System.Math.Min(System.Math.Floor(h.splitT), System.Math.Max(0, room));
                    h.splitT = System.Math.Min(h.splitT - n, 1);
                    if (n >= 1)
                    {
                        double rams = Whole(n * h.rams[Genetics.Splitter] / split);
                        rams = System.Math.Min(rams, n);
                        // the halves share one fleece (none of it new), and a new half's udder is empty, as with a named split
                        h.wool = Mix(h.wool, h.Total, 0f, n);
                        h.milk = Mix(h.milk, h.Ewes, 0f, n - rams);
                        h.rams[Genetics.Splitter] += rams; h.ewes[Genetics.Splitter] += n - rams;
                        births = Bump(births, n); Ledger(n);
                        Log("^pMITOSIS:^w " + UI.Fmt(n) + " Splitters of the herd in the " + b.def.name + " split in two.");
                        if (IsVisible(b.Center)) FloatText(b.Center + Vector2.up * 1.5f, "MITOSIS x" + UI.Fmt(n), Pal.C(Genetics.Get(Genetics.Splitter).color));
                    }
                }
                double wolves = h.ewes[Genetics.Wolf] + h.rams[Genetics.Wolf];
                if (wolves >= 1 && tutDone)
                {
                    h.wolfT += wolves * 0.04;
                    double prey = h.Adults - wolves;
                    double n = System.Math.Min(System.Math.Floor(h.wolfT), prey);
                    h.wolfT = System.Math.Min(h.wolfT - System.Math.Max(0, n), 1);
                    if (n >= 1)
                    {
                        HerdDie(b, h, n, Genetics.Wolf, "eaten by the Wolfsheep");
                        double left = h.Adults;
                        if (left > 0) h.hunger *= (float)System.Math.Max(0, 1 - n / left); // the n wolves that ate are fed, nobody else
                    }
                }
            }
        }

        // ---------------- leaders ----------------
        /// The herd's wish to leave its room (each door's filter decides the rest): a Feeding Lot keeps it until it is fed.
        bool HerdWantsOut(Building b, Flock h) { return !IsLot(b) || res[(int)Res.Food] < 0.4 || h.hunger <= 0.3f; }

        /// may herd sheep of this sex and strain use a door with filter f, judged by the herd's shared fleece, udders and hunger?
        bool HerdAllows(int f, Flock h, bool male, int strain)
        {
            switch (f)
            {
                case 1: return h.hunger >= 0.7f;
                case 2: return h.hunger < 0.3f;
                case 3: return (lineShears && h.wool >= 0.98f) || (lineMilks && !male && h.milk >= 0.98f);
                case 4: return h.wool >= 0.98f;
                case 5: return !male && h.milk >= 0.98f;
                case 6: return male;
                case 7: return !male;
                case 8: return strain == 0;
                case 9: return strain != 0;
            }
            return true;
        }

        /// can this named sheep inside its room leave it right now (it wants out, and an open door with a belt lets it)?
        bool CanLeave(Sheep s, Building p)
        {
            if (!RoomActive(p) || !WantsOutBase(s, p)) return false;
            for (int i = 0; i < 4; i++) if (DoorUsable(p, i) && FilterAllows(Filter(p, i), s)) return true;
            return false;
        }

        /// a herd hand with nothing to do melts back in when the room has leaders enough; a kind of sheep that could leave by an
        /// open door but has nobody of its kind to follow out gets a herd hand
        void LeadOrMelt(Building b, Flock h, float dt)
        {
            if (MeltSpare(b)) return;
            if (b.promoteCd > 0 || h.Adults < 1 || Day < herdStrikeUntil) return; // on strike, nobody steps out to lead
            // A herd hand needs only a name on the farm, not one of the room's 24: those are for the sheep born there, and the
            // room's named members may be out on the belts, lambs, or unable to use the door this kind of sheep could.
            if (herdNamed >= NamedCap)
            {
                if (LeaderWanted(b, h) >= 0) { herdNoNamesT += dt; herdShortStep = true; } // a herd waits for a leader no name is left for
                return;
            }
            int key = LeaderWanted(b, h);
            if (key >= 0 && Promote(b, key, true) != null) herdNamed++;
        }

        float herdNoNamesT; // seconds herds waited for a leader the named budget could not give them

        /// the Supreme Leader on the herd (from CheckAdvice): no names left to lead it, droves far too small for it
        bool HerdAdvice()
        {
            // true only when he really speaks: a silent check must not stop the advice after it in CheckAdvice
            if (popT > 0.5f || LeaderModal) return false;
            if (herdNoNamesT > 20f && AdviceReady("herd_names"))
            {
                herdNoNamesT = 0;
                Advise("herd_names", "THREE HUNDRED NAMES, Hoof. I cannot remember one more, and nobody is left to lead the herd out. SELL or SLAUGHTER a few of the named ones.", LExpr.Stern, 2f);
                return true;
            }
            if (!AdviceReady("droves")) return false;
            double size = DroveSize;
            foreach (var b in buildings)
                if (IsRoom(b) && HerdIn(b) > size * 60 && AnyDoorUsable(b))
                {
                    Advise("droves", "The herd in the " + b.def.name.ToUpper() + " is far bigger than its droves. Buy BIGGER DROVES (U): more of them follow each leader out.", LExpr.Stern, 3f);
                    return true;
                }
            return false;
        }

        /// the evening's news: how many of each room's herd died today, and of what
        void HerdDayLog()
        {
            foreach (var b in buildings)
            {
                if (b.herdStarved >= 1) Log("^r" + UI.Fmt(b.herdStarved) + " sheep of the herd in the " + b.def.name + " starved today.");
                if (b.herdEaten >= 1) Log("^r" + UI.Fmt(b.herdEaten) + " sheep of the herd in the " + b.def.name + " were eaten by the Wolfsheep today.");
                b.herdStarved = 0; b.herdEaten = 0;
            }
        }

        /// the room's idle herd hand (CountPens found it) melts back into the herd, if the room keeps leaders enough without it
        bool MeltSpare(Building b)
        {
            var hand = b.spareHand;
            b.spareHand = null;
            if (hand == null || hand.dead || hand.pen != b || b.penCount <= MinLeaders) return false;
            if (hand.variant != Variant.None || hand.traits.Count > 0)
            {
                // something happened to it in the machines (CHAOS, a splice's side effect): it has a story now, and keeps its name
                hand.herdHand = false; usedNames.Add(hand.name);
                Log("^y" + hand.name + "^w came back from the machines changed. No longer just one of the herd.");
                return false;
            }
            Demote(hand, b);
            herdNamed--;
            return true;
        }

        /// the biggest kind (strain x 2 + ram) of the herd that an open door lets out and that no named sheep inside leads, or -1
        int LeaderWanted(Building b, Flock h)
        {
            if (!HerdWantsOut(b, h)) return -1;
            int best = -1; double bn = 0;
            for (int i = 0; i < 4; i++)
            {
                if (!DoorUsable(b, i)) continue;
                int f = Filter(b, i);
                for (int s = 0; s < Flock.Strains; s++)
                    for (int m = 0; m < 2; m++)
                    {
                        double n = m == 1 ? h.rams[s] : h.ewes[s];
                        int key = s * 2 + m;
                        if (n < 1 || n <= bn || (b.leaderMask & (1 << key)) != 0 || !HerdAllows(f, h, m == 1, s)) continue;
                        bn = n; best = key;
                    }
            }
            return best;
        }

        /// One of the herd (kind = strain x 2 + ram) becomes a named sheep in the room. A herd hand (`hand`) has a name and nothing
        /// else, and melts back in later; one named by the player stays. No sheep is made or lost.
        public Sheep Promote(Building b, int key, bool hand)
        {
            var h = b.herd;
            if (h == null) return null;
            int strain = key / 2; bool male = key % 2 == 1;
            var arr = h.Sex(male);
            if (strain < 0 || strain >= Flock.Strains || arr[strain] < 1) return null;
            arr[strain] -= 1;
            var inner = PenInterior(b);
            var s = new Sheep();
            s.id = NextId();
            s.name = Names.Random(rng, usedNames);
            if (!hand) usedNames.Add(s.name); // herd hands come and go: their names are not kept
            s.male = male; s.strain = strain;
            s.ageDays = Sheep.LambAge + 1f + (float)rng.NextDouble() * 6f;
            s.born = Day - s.ageDays;
            // the one that steps forward can still walk to food: a starving herd's leader would starve on its first step
            s.wool = h.wool; s.milk = male ? 0 : h.milk; s.hunger = Mathf.Min(h.hunger, 3.5f);
            s.mood = s.moodTarget = HerdMood(b, h);
            s.woolCol = RandomWoolCol(); s.faceCol = rng.Next(10) < 6 ? 0 : rng.Next(4); s.baseExpr = RandomExpr();
            s.pos = new Vector2(inner.xMin + 0.2f + (float)rng.NextDouble() * (inner.width - 0.4f), inner.yMin + 0.2f + (float)rng.NextDouble() * (inner.height - 0.4f));
            s.target = s.pos;
            s.statusTimer = (float)rng.NextDouble() * 5; s.baaTimer = 4 + (float)rng.NextDouble() * 20;
            s.parents = "the herd"; s.herdHand = hand;
            s.pen = b; b.penMembers++; b.penCount++;
            b.leaderMask |= 1 << key;
            b.promoteCd = 0.4f;
            sheep.Add(s);
            SpawnSheepGO(s);
            return s;
        }

        /// the player names one of the herd (the room's inspector): the commonest kind of adult, picked by chance
        public Sheep NameOneOfHerd(Building b)
        {
            var h = b != null ? b.herd : null;
            if (h == null || h.Adults < 1 || !CanName(b)) return null;
            int S = Flock.Strains;
            for (int s = 0; s < S; s++) { scratchW[2 * s] = h.ewes[s]; scratchW[2 * s + 1] = h.rams[s]; }
            double r = rng.NextDouble() * h.Adults; int key = -1;
            for (int k = 0; k < 2 * S; k++) { if (scratchW[k] < 1) continue; key = k; r -= scratchW[k]; if (r < 0) break; }
            if (key < 0) return null;
            var sh = Promote(b, key, false);
            if (sh != null) { Log("^y" + sh.name + "^w steps out of the herd in the " + b.def.name + ". Somebody, at last."); Sfx.Baa(0.6f); }
            return sh;
        }

        /// a herd hand goes back into its room's herd: no sheep made or lost, no record
        void Demote(Sheep s, Building b)
        {
            MergeDrove(s, b);
            var h = HerdOf(b);
            if (s.Lamb) { h.wool = Mix(h.wool, h.Total, s.wool, 1); h.lambs[s.strain] += 1; }
            else HerdAddAdults(b, s.male, s.strain, 1, s.wool, s.milk, s.hunger);
            s.deathCause = "Back in the herd";
            bool shown = selSheep == s;
            RemoveNamed(s);
            if (shown) selBuilding = b; // the inspector shows its room and herd instead
            b.penMembers--; b.penCount--;
        }

        /// takes a named sheep out of the world (sold, slaughtered, vanished, back in the herd): its sprites, its job, the selection
        void RemoveNamed(Sheep s)
        {
            RemoveFromJob(s);
            s.act = Act.Hidden; s.dead = true;
            if (s.go != null) { Destroy(s.go); Destroy(s.shadow.gameObject); s.go = null; }
            sheep.Remove(s);
            if (selSheep == s) selSheep = null;
            if (followSheep == s) followSheep = null;
            if (hoverSheep == s) hoverSheep = null;
        }

        // ---------------- droves ----------------
        /// a named sheep steps out of door i of room p: up to DroveSize of the herd of its own sex and strain follow it, if the
        /// door's filter lets them through (judged by the herd's fleece, udders and hunger)
        void TakeDrove(Sheep s, Building p, int door)
        {
            var h = p.herd;
            if (h == null || s.Lamb) return;
            var arr = h.Sex(s.male);
            double have = System.Math.Floor(arr[s.strain]);
            if (have < 1 || !HerdWantsOut(p, h) || !HerdAllows(Filter(p, door), h, s.male, s.strain)) return;
            double n = System.Math.Min(DroveSize, have);
            if (n < 1) return;
            if (s.drove != null && s.drove.n >= 1) MergeDrove(s, p); // never two droves on one sheep
            arr[s.strain] -= n;
            s.drove = new Drove { n = n, wool = h.wool, milk = s.male ? 0 : h.milk, hunger = h.hunger };
        }

        /// the drove reaches a room with its leader: it joins that room's herd
        void MergeDrove(Sheep s, Building room)
        {
            var d = s.drove;
            s.drove = null;
            if (d == null || d.n < 1 || room == null) return;
            HerdAddAdults(room, s.male, s.strain, d.n, d.wool, d.milk, d.hunger);
        }

        /// the leader is gone (dead, sold, missing): its drove goes home to its room, or the nearest pen; with none, it runs off
        void DisbandDrove(Sheep s)
        {
            var d = s.drove;
            s.drove = null;
            if (d == null || d.n < 1) return;
            var home = s.pen != null && buildings.Contains(s.pen) && RoomActive(s.pen) ? s.pen : PenFor(s.pos);
            if (home == null) home = FirstLot();
            if (home == null)
            {
                Ledger(-d.n);
                Log("^o" + UI.Fmt(d.n) + " sheep that followed " + s.name + " ran off.^w There is no pen to go home to.");
                return;
            }
            HerdAddAdults(home, s.male, s.strain, d.n, d.wool, d.milk, d.hunger);
        }

        /// a room is demolished: its herd moves to the nearest other room (crowded or not), or runs off when there is none
        void RehomeHerd(Flock h, Vector2 near, string from)
        {
            if (h == null || h.Total < 1) return;
            var to = PenFor(near) ?? FirstLot();
            if (to == null)
            {
                Ledger(-h.Total);
                Log("^r" + UI.Fmt(h.Total) + " sheep of the herd ran off^w when the " + from + " came down. There is no other pen.");
                return;
            }
            var t = HerdOf(to);
            double total = t.Total, adults = t.Adults, ewes = t.Ewes;
            t.wool = Mix(t.wool, total, h.wool, h.Total);
            t.hunger = Mix(t.hunger, adults, h.hunger, h.Adults);
            t.milk = Mix(t.milk, ewes, h.milk, h.Ewes);
            for (int s = 0; s < Flock.Strains; s++) { t.ewes[s] += h.ewes[s]; t.rams[s] += h.rams[s]; t.lambs[s] += h.lambs[s]; }
            Log("The herd of the " + from + " (" + UI.Fmt(h.Total) + ") moves into the " + to.def.name + ".");
        }

        // ---------------- what a drove gives ----------------
        /// a traitless level-1 sheep of the drove's kind, so the drove's yields and prices are a named sheep's to the coin
        readonly Sheep herdProbe = new Sheep();
        Sheep ProbeFor(Sheep leader, Drove d)
        {
            var p = herdProbe;
            p.male = leader.male; p.strain = leader.strain; p.level = 1; p.variant = Variant.None; p.traits.Clear();
            p.ageDays = Sheep.LambAge + 1; p.wool = d != null ? d.wool : 0.6f; p.milk = d != null ? d.milk : 0; p.hunger = d != null ? d.hunger : 0;
            return p;
        }

        /// wool (or its strain's good) the drove's followers give at a shearing machine, all of them together
        double DroveShear(Sheep s, Building b)
        {
            var d = s.drove;
            if (d == null || d.n < 1 || d.wool < 0.2f) return 0;
            var p = ProbeFor(s, d);
            return d.n * Mathf.Max(1, Mathf.RoundToInt(ShearYield(p, b) * ShearAmount(p)));
        }

        /// milk (or cream, or chocolate milk) the drove's ewes give at a milking machine
        double DroveMilk(Sheep s, Building b)
        {
            var d = s.drove;
            if (d == null || d.n < 1 || s.male || d.milk < 0.2f) return 0;
            var p = ProbeFor(s, d);
            return d.n * Mathf.Max(1, Mathf.RoundToInt(MilkYield(p, b) * MilkAmount(p)));
        }

        /// mutton the drove's followers make at a Slaughterhouse
        double DroveMutton(Sheep s, Building b)
        {
            var d = s.drove;
            return d == null || d.n < 1 ? 0 : d.n * MuttonYield(ProbeFor(s, d), b);
        }

        /// what the drove's followers fetch alive at a Market door
        double DroveLivePrice(Sheep s, Building m)
        {
            var d = s.drove;
            return d == null || d.n < 1 ? 0 : d.n * (double)LiveSheepPrice(ProbeFor(s, d), m);
        }

        /// what one herd sheep of a strain fetches alive at the best Market (the inspector and the FLOCK panel)
        public double HerdLivePrice(int strain, bool male)
        {
            var p = herdProbe;
            p.male = male; p.strain = strain; p.level = 1; p.variant = Variant.None; p.traits.Clear(); p.ageDays = Sheep.LambAge + 1;
            return LiveSheepPrice(p, BestMarket());
        }

        // ---------------- on screen ----------------
        /// the herd's carpet of sheep over the room's floor: thicker as it fills up, its fleece as it grows, its main strain's colour
        void UpdateHerdVisual(Building b)
        {
            var h = b.herd;
            bool show = h != null && h.Total >= 1 && b.go != null && RoomActive(b);
            if (!show) { if (b.herdSr != null && b.herdSr.gameObject.activeSelf) b.herdSr.gameObject.SetActive(false); return; }
            if (b.herdSr == null)
            {
                var go = new GameObject("herd");
                go.transform.SetParent(b.go.transform, false);
                b.herdSr = go.AddComponent<SpriteRenderer>();
                b.herdSr.drawMode = SpriteDrawMode.Tiled;
            }
            if (!b.herdSr.gameObject.activeSelf) b.herdSr.gameObject.SetActive(true);
            // the lambs born since the last burst, at most one burst a room a second (real time: nothing here feeds the simulation)
            b.herdShowT -= Time.unscaledDeltaTime;
            if (b.herdShow >= 1 && b.herdShowT <= 0)
            {
                if (IsVisible(b.Center)) FloatText(b.Center + Vector2.up * 1.4f, "+" + UI.Fmt(b.herdShow) + (b.herdShow >= 2 ? " LAMBS" : " LAMB"), new Color(1f, 0.6f, 0.75f));
                b.herdShow = 0; b.herdShowT = 1.2f;
            }
            double fill = h.Total / System.Math.Max(1, RoomCapacity(b));
            int density = fill < 0.15 ? 1 : fill < 0.4 ? 2 : fill < 0.7 ? 3 : 4;
            int fleece = h.wool < 0.35f ? 0 : h.wool < 0.98f ? 1 : 2;
            int frame = Mathf.FloorToInt(Time.time * 1.6f + b.id) % 2;
            var spr = HerdArt.Floor(density, fleece, h.MainStrain(), frame);
            if (b.herdSr.sprite != spr) b.herdSr.sprite = spr;
            var r = PenInterior(b);
            // a small herd stands at the front of the room, a big one fills it to the back fence
            float rows = Mathf.Clamp((float)fill * 2f, 0.3f, 1f);
            b.herdSr.size = new Vector2(Mathf.Max(1f, Mathf.Floor(r.width * 2f) / 2f), Mathf.Max(0.5f, Mathf.Floor(r.height * rows * 2f) / 2f));
            b.herdSr.transform.position = new Vector3(r.xMin, r.yMin, 0);
            b.herdSr.sortingOrder = SortY(b.y + b.def.h) - 4; // behind every sheep in the room
        }

        /// the drove trotting behind its leader, more of it the bigger it is
        void UpdateDroveTail(Sheep s)
        {
            int n = FollowersOf(s);
            bool show = n >= 1 && !s.dead && s.go != null;
            if (!show) { if (s.tail != null && s.tail.gameObject.activeSelf) s.tail.gameObject.SetActive(false); return; }
            if (s.tail == null)
            {
                var go = new GameObject("drove");
                go.transform.SetParent(s.go.transform, false);
                s.tail = go.AddComponent<SpriteRenderer>();
            }
            if (!s.tail.gameObject.activeSelf) s.tail.gameObject.SetActive(true);
            int size = n < 10 ? 1 : n < 100 ? 2 : n < 1000 ? 3 : 4;
            int fleece = s.drove.wool < 0.35f ? 0 : s.drove.wool < 0.98f ? 1 : 2;
            var spr = HerdArt.Tail(size, fleece, s.strain);
            if (s.tail.sprite != spr) s.tail.sprite = spr;
            s.tail.flipX = !s.faceRight;
            s.tail.transform.localPosition = new Vector3(s.faceRight ? -0.45f : 0.45f, 0.05f, 0);
            s.tail.sortingOrder = s.sr.sortingOrder - 1;
        }

        /// "+1.2K" over every drove on screen (at most 60), with the name plates (L)
        void DrawDroveBadges(float sc)
        {
            if (!UI.Repaint || !showPlates) return;
            int shown = 0;
            foreach (var s in sheep)
            {
                var d = s.drove;
                if (s.dead || d == null || d.n < 1 || s.go == null || !s.go.activeSelf) continue;
                Vector2 g = ToGui(s.pos + new Vector2(0, 1.45f), sc);
                if (g.x < -20 || g.y < -20 || g.x > W + 20 || g.y > H + 20) continue;
                if (d.labelN != d.n) { d.labelN = d.n; d.label = "+" + UI.Fmt(d.n); }
                PixelFont.DrawOutlined(Mathf.Round(g.x - PixelFont.Width(d.label) / 2f), Mathf.Round(g.y), d.label, UI.Gold);
                if (++shown >= 60) break;
            }
        }

        /// the room badge's text with a herd: named sheep inside + the herd / the room's space
        string RoomBadge(Building b)
        {
            return (IsLot(b) ? "FEEDING LOT " : "PEN ") + b.penCount + "+" + UI.Fmt(HerdIn(b)) + "/" + UI.Fmt(RoomCapacity(b)) + (b.penHungry > 0 ? "  ^o" + UI.Fmt(b.penHungry) + " hungry" : "");
        }

        /// what one sheep of this herd fetches alive at the best Market, on average over its kinds
        public double HerdAvgPrice(Flock h)
        {
            double a = h.Adults, sum = 0;
            if (a < 1) return HerdLivePrice(0, false);
            for (int s = 0; s < Flock.Strains; s++)
            {
                if (h.ewes[s] >= 1) sum += h.ewes[s] * HerdLivePrice(s, false);
                if (h.rams[s] >= 1) sum += h.rams[s] * HerdLivePrice(s, true);
            }
            return sum / a;
        }

        /// the Supreme Leader introduces the herd, the first time one forms
        void HerdIntro()
        {
            Building at = null;
            foreach (var b in buildings) if (HerdIn(b) >= 1) { at = b; break; }
            if (at != null) FocusOn(at.Center);
            LeaderSay(new[]
            {
                new LeaderPage("Hoof. The pens are so full I have STOPPED LEARNING THEIR NAMES. The extras are now THE HERD: counted, not named. Efficient. Cold. Correct.", LExpr.Smug),
                new LeaderPage("NAMED sheep lead. When one leaves through a door, a DROVE of the herd follows it: its own kind, through every machine, into every Market. One sheep in, a crowd of wool out.", LExpr.Stern),
                new LeaderPage("Buy BIGGER DROVES and SHEEP STACKING in UPGRADES (U). I want millions, Hoof. MILLIONS.", LExpr.Shout),
            });
        }
    }
}
