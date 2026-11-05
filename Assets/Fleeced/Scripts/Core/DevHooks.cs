#if FLEECED_DEV
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Fleeced
{
    /// Hooks for the test harness (Tools/Harness). This file only compiles in test builds, which define
    /// FLEECED_DEV (Tools/Editor/BuildTest.cs). Release builds contain none of it.
    public static class DevHooks
    {
        /// called at named points while the save is written ("tmp", "replace"): throw to simulate a crash or a failure
        public static System.Action<string> SaveFault;

        /// when set, every new world seeds its simulation generator with it (Game.WorldSeed)
        public static int? Seed;
        /// called at named points inside ApplySave ("sheep", "end"): throw to simulate a load that fails half-way
        public static System.Action<string> LoadFault;
        /// holds (true) or releases (false) the Sheep Gate instead of the keyboard; null = the keyboard
        public static bool? GateHeld;
        /// saving when the window loses focus (off in test runs unless a suite turns it on)
        public static bool FocusSaves;

        /// The flock check (M11): with CheckFlock on, every simulation step compares the number of sheep with the last step's
        /// plus every change the game accounted for in between (Game.Ledger), and checks that no herd holds a bad count.
        public static bool CheckFlock;
        public static double Ledger, FlockLast = -1;
        public static int FlockErrors, FlockWorld = -1;
        public static string FlockError;
        public static void ResetFlock() { Ledger = 0; FlockLast = -1; FlockErrors = 0; FlockError = null; FlockWorld = -1; }

        /// M12: chamber timers that came round, bursts shown (and the lambs they counted), their sounds, runners sent, the last
        /// shake a burst asked for, and a machine batch that replaces every machine's own (0 = off)
        public static int ChamberFires, BurstsShown, BurstSounds, RunnersSpawned;
        public static double BurstLambsShown, BatchCapOverride;
        public static float LastShakeSet;
        /// the m12 look storm: replaces every sheep's look each frame (null = off)
        public static System.Func<Sheep, SheepLook, SheepLook> LookStorm;
        public static void ResetM12() { ChamberFires = 0; BurstsShown = 0; BurstSounds = 0; RunnersSpawned = 0; BurstLambsShown = 0; LastShakeSet = 0; }
    }

    public partial class Game
    {
        /// the Sheep Gate at a stage at once, without its build time (tests)
        public void DevSetGateStage(int stage)
        {
            var g = GateAny;
            if (g == null) return;
            g.level = Mathf.Clamp(stage, 1, g.def.maxLevel); g.upgrading = false; g.construct = 1f;
            RefreshBuildingSprite(g);
        }
        /// held releases in a row (a STAMPEDE every 6th)
        public int DevGateStreak { get { return gateStreak; } }
        /// the lambs the gate will show in its next burst
        public double DevGateShowN { get { return gateShowN; } }
        /// an announcement or dialogue still waiting its turn under this key
        public bool DevIsWaiting(string key) { return IsWaiting(key); }

        /// runs the simulation in fixed steps without waiting for frames (event cards and dialogue are the caller's job)
        public void DevSimulate(float seconds, float step = 0.05f)
        {
            int n = Mathf.CeilToInt(seconds / step);
            for (int i = 0; i < n; i++) Simulate(step);
        }

        /// DevHooks.CheckFlock, at the end of every simulation step
        void DevCheckFlock()
        {
            if (!DevHooks.CheckFlock) return;
            if (DevHooks.FlockWorld != worldsCreated) { DevHooks.FlockWorld = worldsCreated; DevHooks.FlockLast = -1; DevHooks.Ledger = 0; } // a new world
            double pop = Population;
            if (DevHooks.FlockLast >= 0 && System.Math.Abs(pop - (DevHooks.FlockLast + DevHooks.Ledger)) > 0.5)
                DevFlockError("day " + Day.ToString("0.000", CultureInfo.InvariantCulture) + ": " + pop.ToString("R", CultureInfo.InvariantCulture) + " sheep, "
                    + (DevHooks.FlockLast + DevHooks.Ledger).ToString("R", CultureInfo.InvariantCulture) + " accounted for");
            foreach (var b in buildings)
            {
                var h = b.herd;
                if (h == null) continue;
                for (int s = 0; s < Flock.Strains; s++)
                    if (!WholeCount(h.ewes[s]) || !WholeCount(h.rams[s]) || !WholeCount(h.lambs[s])) { DevFlockError("a bad count in the herd of the " + b.def.name); break; }
                if (float.IsNaN(h.wool) || float.IsNaN(h.milk) || float.IsNaN(h.hunger) || h.wool < 0 || h.milk < 0 || h.hunger < 0) DevFlockError("a bad average in the herd of the " + b.def.name);
            }
            foreach (var s in sheep) if (s.drove != null && !WholeCount(s.drove.n)) DevFlockError("a bad drove on " + s.name);
            DevHooks.FlockLast = pop; DevHooks.Ledger = 0;
        }
        static bool WholeCount(double v) { return !double.IsNaN(v) && v >= 0 && v == System.Math.Floor(v); }
        static void DevFlockError(string e) { DevHooks.FlockErrors++; if (DevHooks.FlockError == null) DevHooks.FlockError = e; }

        /// counted sheep into a room's herd from outside the game (the harness's fixtures), accounted for
        public void DevAddHerd(Building room, int strain, double ewes, double rams, double lambs, float wool = 0.6f, float hunger = 0.2f, float milk = 0.5f)
        {
            if (ewes > 0) HerdAddAdults(room, false, strain, ewes, wool, milk, hunger);
            if (rams > 0) HerdAddAdults(room, true, strain, rams, wool, 0f, hunger);
            if (lambs > 0) HerdAddLambs(room, strain, lambs);
            Ledger(ewes + rams + lambs);
        }
        public bool DevCanLeave(Sheep s, Building p) { return CanLeave(s, p); }
        public void DevTakeDrove(Sheep s, Building p, int door) { TakeDrove(s, p, door); }
        public void DevDailyStrains() { DailyStrains(); }
        public void DevSlaughter(Sheep s, Building b) { Slaughter(s, b); }
        public void DevSellAtMarket(Sheep s, Building m) { SellAtMarket(s, m); }
        public void DevProcessInMachine(Building b, Sheep s) { ProcessInMachine(b, s); }
        public float DevHerdMood(Building b) { return b.herd != null ? HerdMood(b, b.herd) : 0; }
        public bool DevUsefulIn(Building b, Sheep s) { return UsefulIn(b, s); }

        /// one press of Space in a Leader dialogue; false when no dialogue is open
        public bool DevAdvanceDialog()
        {
            if (!LeaderModal) return false;
            AdvanceDialog();
            return true;
        }

        /// saves now, like an autosave: "ok", "skipped" (title screen or launch running) or the error
        public string DevSaveNow()
        {
            if (!started || launchSeq > 0) return "skipped";
            lastSaveError = "not attempted";
            SaveGame(true);
            return lastSaveError ?? "ok";
        }

        /// the title screen of a fresh world, as when the game starts (no load, no new game, no notice left over)
        public void DevRestartToTitle()
        {
            pendingLoadJson = null;
            skipTitle = false;
            titleNotice = null;
            RestartWorld();
        }

        /// the next world runs the boot step again (FinishInterruptedSave), as if the game had just started
        public static void DevResetBoot() { bootChecked = false; }

        /// the Enter key on the title screen
        public void DevTitleEnter() { TitleEnter(); }

        /// the Supreme Leader's bubble text, and ending his current line (the frame loop would, in time)
        public string DevPopText { get { return popText; } }
        public void DevEndLine() { popT = 0; toastT = 0; } // the frame loop then says what waits

        /// one item riding into a building, through the belts' own delivery (false when the building refuses it)
        public bool DevDeliver(Building b, Res r, float amount) { return TryDeliver(b, new BeltItem { res = r, amount = amount }); }

        /// one AUTO batch sold by this Market, as SimMarket sells it when its timer runs out
        public void DevMarketBatch(Building b) { b.progress = 4f; SimMarket(b, 0f, 0f); }

        /// the sheep the Collector would ask for, or null
        public Sheep DevGoldenSheep() { return GoldenSheep(); }

        /// the day change's income measure, on its own
        public void DevMeasureDay() { MeasureDayIncome(); }

        /// a sheep done inside a Gene Lab, as ProcessGeneLab handles it
        public void DevGeneLabPass(Building b, Sheep s) { ProcessGeneLab(b, s); }

        /// shows an event card now, as if its turn in the queue had come (ChooseOption answers the card on screen)
        public void DevShowEvent(EventCard c) { currentEvent = c; }

        /// the pre-fix launch: no blocker check, so a pad without a rocket reproduces the old freeze
        public void DevForceLaunch()
        {
            astronaut = LaunchVolunteer();
            if (astronaut != null) { astronaut.act = Act.Hidden; RemoveFromJob(astronaut); }
            launchSeq = 0.001f;
            followSheep = null; selBuilding = null; selSheep = null; panel = Panel.None;
        }

        /// starts tutorial step `step` now, as if the one before were done (only for a test that would otherwise stall)
        public void DevForceStep(int step) { StartStep(step); }

        /// the FLOCK panel tab (0 flock, 1 memorial) and the GENETICS panel tab, for screenshots
        public void DevTabs(int sheep, int genes) { sheepTab = sheep; geneTab = genes; }

        /// an event card by id, as the daily event roll builds it
        public EventCard DevBuildEvent(string id) { return BuildEvent(id); }

        /// puts the camera on `at` at zoom `scale` (pixelScale 1-10) now, as the player could (the bot compares two views)
        public void DevSetView(Vector2 at, int scale)
        {
            pixelScale = Mathf.Clamp(scale, 1, 10);
            followSheep = null;
            camTarget = new Vector3(at.x, at.y, -10);
            ApplyCamera();
            cam.transform.position = camTarget;
        }

        public Sheep DevAstronaut { get { return astronaut; } }
        public bool DevHasRocket { get { return rocketGo != null; } }
        public bool DevInputBlocked { get { return !started || currentEvent != null || launchSeq > 0 || LeaderModal; } }
        public bool DevArchiveChecked { get { return archiveChecked; } }
        public bool DevInert { get { return inert; } }
        public int DevLossy { get { return lossy; } }
        public static int DevWorldsCreated { get { return worldsCreated; } }
        public static string DevTitleNotice { get { return titleNotice; } }
        public bool DevTitleSaveNewer { get { CheckTitleSave(); return titleSaveNewer; } }
        public bool DevTitleCanContinue { get { CheckTitleSave(); return titleHasSave && !titleSaveOld && !titleSaveNewer; } }
        public string DevTitleBackup { get { CheckTitleSave(); return titleBackupPath; } }
        /// the version label the title screen last drew (set in DrawTitle)
        public static string DevTitleFooterDrawn;
        /// a scheduled event's card by id (the fines come this way), as the scheduler resolves it
        public EventCard DevScheduledEvent(string id) { return ResolveScheduled(new SchedSave { id = id, day = Day }); }

        /// what the game does once per run at boot, before anything reads the save
        public static void DevFinishInterruptedSave() { FinishInterruptedSave(); }

        /// the NaN cleaning that saving and loading apply, for checking it on a parsed save
        public static int DevCleanNumbers(SaveData d) { return CleanNumbers(d); }

        /// A canonical summary of the saved state, for comparing a world before and after a save and load. Doubles are
        /// written at float precision: JsonUtility keeps about 16 significant digits, so the last one may change on a
        /// round trip, while a real loss (a wrong or missing value) still shows.
        public string DevStateDigest()
        {
            var ci = CultureInfo.InvariantCulture;
            System.Func<double, string> D = v => ((float)v).ToString("R", ci);
            var sb = new StringBuilder();
            sb.Append("day=").Append(dayIndex).Append(" t=").Append(D(simTime)).Append(" speed=").Append(speed);
            sb.Append(" land=").Append(owned.x).Append(',').Append(owned.y).Append(',').Append(owned.width).Append(',').Append(owned.height).Append('/').Append(landLevel);
            sb.Append("\nres=");
            for (int i = 0; i < res.Length; i++) sb.Append(D(res[i])).Append(i + 1 < res.Length ? "," : "");
            sb.Append("\nmade=");
            for (int i = 0; i < totalMade.Length; i++) sb.Append(D(totalMade[i])).Append(i + 1 < totalMade.Length ? "," : "");
            sb.Append("\nsold=");
            for (int i = 0; i < totalSold.Length; i++) sb.Append(D(totalSold[i])).Append(i + 1 < totalSold.Length ? "," : "");
            sb.Append("\nprices=");
            for (int i = 0; i < priceMod.Length; i++) sb.Append(priceMod[i].ToString("R", ci)).Append(autoSell[i] ? "a" : "").Append(i + 1 < priceMod.Length ? "," : "");
            sb.Append("\nmeta tut=").Append(tutStep).Append('/').Append(tutDone).Append(" obj=").Append(objectiveIndex)
              .Append(" dna=").Append(dna).Append('/').Append(dnaTotal).Append(" births=").Append(births).Append(" deaths=").Append(deaths)
              .Append(" accidents=").Append(accidents).Append(" won=").Append(gameWon).Append(" graves=").Append(graveyard.Count)
              .Append(" research=").Append(currentResearch ?? "-").Append('/').Append(rprog.Count)
              .Append(" contract=").Append(contract != null ? (int)contract.res + "x" + contract.amount + "=" + contract.reward + "@" + contract.deadline.ToString("R", ci) : "-");
            var t = new List<string>(tech); t.Sort(System.StringComparer.Ordinal);
            var f = new List<string>(flags); f.Sort(System.StringComparer.Ordinal);
            var gn = new List<string>(genes); gn.Sort(System.StringComparer.Ordinal);
            sb.Append("\ntech=").Append(string.Join(",", t)).Append("\nflags=").Append(string.Join(",", f)).Append("\ngenes=").Append(string.Join(",", gn));
            var pl = new List<string>();
            foreach (var kv in pathLevels) pl.Add(kv.Key + ":" + kv.Value);
            pl.Sort(System.StringComparer.Ordinal);
            sb.Append("\npaths=").Append(string.Join(",", pl));
            long beltSum = 0; int belts = 0;
            for (int x = 0; x < MapW; x++)
                for (int y = 0; y < MapH; y++)
                    if (tiles[x, y].belt >= 0) { belts++; beltSum += (x * 131L + y) * 7 + tiles[x, y].belt * 3 + tiles[x, y].beltTier; }
            double itemSum = 0; foreach (var it in items) itemSum += it.amount;
            sb.Append("\nbelts=").Append(belts).Append('/').Append(beltSum).Append(" items=").Append(items.Count).Append('/').Append(D(itemSum));
            var bl = new List<string>();
            foreach (var b in buildings)
                bl.Add(b.id + ":" + (int)b.def.kind + ":L" + b.level + "@" + b.x + "," + b.y + " c=" + b.construct.ToString("R", ci)
                    + " d=" + (b.doorOpen[0] ? 1 : 0) + (b.doorOpen[1] ? 1 : 0) + (b.doorOpen[2] ? 1 : 0) + (b.doorOpen[3] ? 1 : 0)
                    + " f=" + b.doorFilter[0] + b.doorFilter[1] + b.doorFilter[2] + b.doorFilter[3] + " herd=" + HerdDigest(b.herd, ci));
            bl.Sort(System.StringComparer.Ordinal);
            sb.Append("\nbuildings=").Append(bl.Count).Append('\n').Append(string.Join("\n", bl));
            var sl = new List<string>();
            foreach (var s in sheep)
            {
                if (s.dead) continue;
                sl.Add(s.id + ":" + s.name + ":" + (s.male ? "M" : "F") + ":L" + s.level + ":s" + s.strain + ":v" + (int)s.variant + ":e" + (int)s.baseExpr
                    + ":age" + s.ageDays.ToString("R", ci) + ":pen" + (s.pen != null ? s.pen.id : 0) + ":w" + s.wool.ToString("R", ci) + ":m" + s.milk.ToString("R", ci)
                    + ":h" + s.hunger.ToString("R", ci) + ":mood" + s.mood.ToString("R", ci) + ":t" + s.traits.Count
                    + (s.drove != null ? ":dr" + s.drove.n.ToString("R", ci) + "/" + s.drove.wool.ToString("R", ci) + "/" + s.drove.milk.ToString("R", ci) + "/" + s.drove.hunger.ToString("R", ci) : "") + (s.herdHand ? ":hand" : ""));
            }
            sl.Sort(System.StringComparer.Ordinal);
            sb.Append("\nsheep=").Append(sl.Count).Append('\n').Append(string.Join("\n", sl));
            return sb.ToString();
        }

        /// a herd as text for the digest: every count, the averages and the carried fractions
        static string HerdDigest(Flock h, CultureInfo ci)
        {
            if (h == null || h.Total < 1) return "-";
            var sb = new StringBuilder();
            for (int s = 0; s < Flock.Strains; s++) sb.Append(h.ewes[s].ToString("R", ci)).Append('/').Append(h.rams[s].ToString("R", ci)).Append('/').Append(h.lambs[s].ToString("R", ci)).Append(',');
            sb.Append(h.wool.ToString("R", ci)).Append(',').Append(h.milk.ToString("R", ci)).Append(',').Append(h.hunger.ToString("R", ci));
            // the carried fractions as floats, like the digest's other doubles: the save keeps them to a float's precision
            sb.Append(',').Append(((float)h.breedT).ToString("R", ci)).Append(',').Append(((float)h.matureT).ToString("R", ci)).Append(',').Append(((float)h.mealT).ToString("R", ci));
            sb.Append(',').Append(((float)h.starveT).ToString("R", ci)).Append(',').Append(((float)h.splitT).ToString("R", ci)).Append(',').Append(((float)h.wolfT).ToString("R", ci));
            sb.Append(',').Append(((float)h.litterT).ToString("R", ci));
            return sb.ToString();
        }
    }
}
#endif
