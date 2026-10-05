using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Fleeced
{
    public class LeaderPage
    {
        public string text; public LExpr expr;
        public LeaderPage(string t, LExpr e = LExpr.Stern) { text = t; expr = e; }
    }

    public class TutTarget
    {
        public string ui; public bool world; public Vector2 pos; public string label = "CLICK";
        public static TutTarget Ui(string id) { return new TutTarget { ui = id }; }
        public static TutTarget At(Vector2 p, string label = "CLICK") { return new TutTarget { world = true, pos = p, label = label }; }
    }

    public class TutStep
    {
        public string id;
        public LeaderPage[] intro;
        public System.Func<string> decree;
        public System.Func<bool> done;
        public System.Func<TutTarget> target;
        public System.Func<string> progress;
        public System.Action onStart;
        public string praise; public LExpr praiseExpr = LExpr.Smug;
    }

    /// The Supreme Leader (a ram in a military cap): tutorial decrees, dialogue, shouting.
    public partial class Game
    {
        public const string LeaderName = "THE SUPREME LEADER";

        // ---------------- state ----------------
        public int tutStep = -1;
        public bool tutDone;
        public int bubbleHarvests, milkCount; // bubbleHarvests: kept for old saves
        int tutBase;
        List<TutStep> steps;
        bool tutWaitNext; float tutNextT;
        string pendingPop; LExpr pendingPopExpr;

        readonly List<LeaderPage> dlgPages = new List<LeaderPage>();
        int dlgIndex; float dlgChars; bool dlgSkippable; int dlgBlip;
        System.Action dlgOnClose;
        public bool LeaderModal { get { return dlgPages.Count > 0; } }

        string popText; float popT, popDur; LExpr popExpr;
        public float leaderWaveT;
        readonly Dictionary<string, float> adviceNext = new Dictionary<string, float>();
        float adviceCheckT, panT;
        string lastPanKey;
        Sheep tutSheep; // the sheep the current step points at

        /// index of the machine-door lesson (saves from before it existed are shifted on load)
        public const int MachineDoorStep = 12;
        /// old saves that already finished the tutorial hear about machine doors once
        public bool machineDoorNews;

        public TutStep CurStep { get { return !tutDone && steps != null && tutStep >= 0 && tutStep < steps.Count ? steps[tutStep] : null; } }
        public bool TutorialActive { get { return CurStep != null; } }

        // when each UI element becomes available during the tutorial
        static readonly Dictionary<string, int> UnlockAt = new Dictionary<string, int>
        {
            { "flock", 2 }, { "build", 2 }, { "belts", 2 }, { "gate", 3 }, { "jobs", 7 }, { "market", 10 }, { "research", 15 }, { "map", 16 },
        };
        static readonly Dictionary<BKind, int> BuildAt = new Dictionary<BKind, int>
        {
            { BKind.Farm, 7 }, { BKind.Market, 8 }, { BKind.Shed, 11 }, { BKind.Dairy, 11 }, { BKind.Slaughter, 13 }, { BKind.Pen, 14 }, { BKind.FeedLot, 14 }, { BKind.House, 14 }, { BKind.Lab, 15 },
        };

        public bool UiOn(string id)
        {
            if (tutDone) return true;
            int at;
            return !UnlockAt.TryGetValue(id, out at) || tutStep >= at;
        }

        public bool BuildVisible(BuildingDef d)
        {
            if (d.hidden) return false;
            if (tutDone) return true;
            int at;
            return BuildAt.TryGetValue(d.kind, out at) && tutStep >= at;
        }

        // ---------------- dialogue ----------------
        public void LeaderSay(LeaderPage[] pages, System.Action onClose = null, bool skippable = false)
        {
            dlgPages.Clear();
            dlgPages.AddRange(pages);
            dlgIndex = 0; dlgChars = 0; dlgBlip = 0;
            dlgOnClose = onClose; dlgSkippable = skippable;
            popT = 0;
            leaderWaveT = 1.2f;
            Sfx.Play("event", 0.5f, 0.8f);
        }

        public void LeaderPop(string text, LExpr e = LExpr.Stern, float dur = 0)
        {
            if (LeaderModal) { pendingPop = text; pendingPopExpr = e; return; }
            popText = text; popExpr = e;
            popDur = dur > 0 ? dur : 4.5f + text.Length * 0.045f;
            popT = popDur;
            dlgBlip = 0;
            Sfx.Play("blip", 0.45f, e == LExpr.Shout ? 0.8f : 1f);
            Sfx.Play("blip", 0.35f, 1.1f);
        }

        static int VisibleLen(string s)
        {
            int n = 0;
            for (int i = 0; i < s.Length; i++) { if (s[i] == '^' && i + 1 < s.Length) { i++; continue; } n++; }
            return n;
        }

        static string VisiblePrefix(string s, int n)
        {
            var sb = new StringBuilder();
            int vis = 0;
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '^' && i + 1 < s.Length) { sb.Append(s[i]).Append(s[i + 1]); i++; continue; }
                if (vis >= n) break;
                sb.Append(s[i]); vis++;
            }
            return sb.ToString();
        }

        void AdvanceDialog()
        {
            if (!LeaderModal) return;
            int total = VisibleLen(dlgPages[dlgIndex].text);
            if (dlgChars < total) { dlgChars = total; return; }
            dlgIndex++;
            dlgChars = 0;
            Sfx.Play("click", 0.4f);
            if (dlgIndex >= dlgPages.Count) CloseDialog();
        }

        void CloseDialog()
        {
            dlgPages.Clear();
            var cb = dlgOnClose; dlgOnClose = null;
            if (cb != null) cb();
            if (pendingPop != null && !LeaderModal) { var p = pendingPop; pendingPop = null; LeaderPop(p, pendingPopExpr); }
        }

        void UpdateLeader(float rdt)
        {
            if (leaderWaveT > 0) leaderWaveT -= rdt;
            if (LeaderModal)
            {
                var page = dlgPages[dlgIndex];
                int total = VisibleLen(page.text);
                if (dlgChars < total)
                {
                    float before = dlgChars;
                    dlgChars = Mathf.Min(total, dlgChars + rdt * 52f);
                    int a = (int)before, b = (int)dlgChars;
                    for (int i = a; i < b; i++)
                        if (++dlgBlip % 3 == 0) Sfx.Play("blip", 0.32f, (page.expr == LExpr.Shout ? 0.82f : 1f) + Random.Range(-0.08f, 0.08f));
                }
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) AdvanceDialog();
                else if (dlgSkippable && dlgIndex == 0 && Input.GetKeyDown(KeyCode.Escape)) SkipTutorial();
                return;
            }
            if (popT > 0) popT -= rdt;
            if (!started || currentEvent != null || launchSeq > 0) return;
            UpdateTutorial();
            adviceCheckT -= rdt;
            if (adviceCheckT <= 0) { adviceCheckT = 1.5f; CheckAdvice(); }
        }

        // ---------------- tutorial flow ----------------
        public void BeginTutorial()
        {
            BuildTutorial();
            tutDone = false;
            StartStep(0);
        }

        void StartStep(int i)
        {
            tutStep = i;
            tutWaitNext = false;
            tutSheep = null;
            lastPanKey = null;
            if (steps == null) BuildTutorial();
            if (i >= steps.Count) { FinishTutorial(); return; }
            var st = steps[i];
            if (st.onStart != null) st.onStart();
            if (st.intro != null && st.intro.Length > 0) LeaderSay(st.intro, null, i == 0);
        }

        /// after loading a save in the middle of the tutorial: rebuild blueprints etc. for the current step
        public void ResumeTutorial()
        {
            BuildTutorial();
            if (tutStep < 1) tutStep = 1;
            if (tutStep >= steps.Count) { FinishTutorial(); return; }
            var st = steps[tutStep];
            if (st.onStart != null) st.onStart();
        }

        void UpdateTutorial()
        {
            var st = CurStep;
            if (st == null) return;
            if (tutWaitNext)
            {
                if (Time.unscaledTime >= tutNextT) StartStep(tutStep + 1);
                return;
            }
            if (st.done != null && st.done())
            {
                tutWaitNext = true;
                tutNextT = Time.unscaledTime + (st.praise != null ? 2.6f : 0.4f);
                if (st.praise != null)
                {
                    LeaderPop(st.praise.Replace("{sel}", selSheep != null ? selSheep.name : "that one"), st.praiseExpr, 4.2f);
                    leaderWaveT = 1.5f;
                    Sfx.Play("fanfare", 0.35f, 1.2f);
                }
            }
        }

        public void SkipTutorial()
        {
            dlgPages.Clear(); dlgOnClose = null;
            FinishTutorial();
            LeaderPop("Fine. Figure it out yourself, Hoof. Open a DOOR and the flock rides the conveyor: pen, Feeding Lot, machines, home. Close it and they stay. Follow my DECREES.", LExpr.Shout);
        }

        void FinishTutorial()
        {
            tutDone = true;
            tutStep = steps != null ? steps.Count : 99;
            FixStarterLine();
            beltPlan.Clear();
            blueprints.Clear();
            // decrees already fulfilled during the tutorial are skipped silently
            while (CurrentObjective != null && CurrentObjective.done(this)) objectiveIndex++;
            var o = CurrentObjective;
            if (o != null) LeaderPop("FIRST DECREE: " + (o.say ?? o.text), LExpr.Shout);
        }

        // ---------------- tutorial helpers ----------------
        Building FirstOf(BKind k)
        {
            foreach (var b in buildings) if (b.def.kind == k) return b;
            return null;
        }

        Sheep PickSheep(System.Func<Sheep, bool> ok)
        {
            if (tutSheep != null && !tutSheep.dead && tutSheep.act != Act.Hidden && tutSheep.act != Act.InMachine && ok(tutSheep)) return tutSheep;
            Vector2 c = new Vector2(cam.transform.position.x, cam.transform.position.y);
            Sheep best = null; float bd = 1e9f;
            foreach (var s in sheep)
            {
                if (s.dead || s.act == Act.Hidden || s.act == Act.InMachine || s.go == null || !ok(s)) continue;
                float d = (s.pos - c).sqrMagnitude;
                // prefer sheep standing in the open (not behind a building or on top of each other)
                var tl = TileAt(s.pos + Vector2.up * 0.6f);
                if (tl != null && tl.building != null) d += 400;
                foreach (var o in sheep) if (o != s && !o.dead && (o.pos - s.pos).sqrMagnitude < 0.5f) { d += 30; break; }
                if (d < bd) { bd = d; best = s; }
            }
            tutSheep = best;
            return best;
        }

        TutTarget SheepTarget(Sheep s) { return s == null ? null : TutTarget.At(s.pos + Vector2.up * (s.Lamb ? 1.0f : 1.55f)); }

        TutTarget BuildingTarget(Building b, string label) { return b == null ? null : TutTarget.At(new Vector2(b.x + b.def.w / 2f, b.y + b.def.h + 1.2f), label); }

        /// points at the OPEN/CLOSE button of a door (or at the door itself when the button is off screen)
        TutTarget DoorTarget(Building p, int i)
        {
            if (p == null) return null;
            if (UI.marks.ContainsKey("door_" + p.id + "_" + i)) return TutTarget.Ui("door_" + p.id + "_" + i);
            return TutTarget.At(DoorOuter(p, i) + Vector2.up * 1.6f, "DOOR");
        }

        void EnsureCoins(int amount)
        {
            if (res[0] < amount)
            {
                double gift = amount - res[0] + 10;
                res[0] += gift;
                Toast("^yThe Supreme Treasury printed " + Mathf.RoundToInt((float)gift) + " coins.");
            }
        }

        void EnsureFood(int amount)
        {
            if (res[(int)Res.Food] >= amount) return;
            res[(int)Res.Food] += amount + 60;
            Toast("^yThe Supreme Treasury bought emergency hay.");
        }

        /// put a world point in the middle of the free strip of screen (not under the side panels)
        void FocusFree(Vector2 p)
        {
            float midGui = (FreeLeft + FreeRight) / 2f;
            float dxWorld = (midGui - W / 2f) * UI.scale / (16f * pixelScale);
            FocusOn(p - new Vector2(dxWorld, 0));
        }

        Vector2 PenSpot(float dx, float dy) { var p = FirstPen(); return p == null ? (Vector2)owned.center : new Vector2(p.x + dx, p.y + dy); }

        void EnsureFluffy(int n)
        {
            int have = 0;
            foreach (var s in sheep) if (!s.dead && !s.Lamb && s.wool >= 0.98f) have++;
            foreach (var s in sheep)
            {
                if (have >= n) break;
                if (s.dead || s.Lamb || s.wool >= 0.98f || s.job != Job.Grazer) continue;
                s.wool = 1.05f; have++;
            }
        }

        void EnsureMilky(int n)
        {
            int have = 0;
            foreach (var s in sheep) if (!s.dead && !s.Lamb && !s.male && s.milk >= 0.98f) have++;
            foreach (var s in sheep)
            {
                if (have >= n) break;
                if (s.dead || s.Lamb || s.male || s.milk >= 0.98f || s.job != Job.Grazer) continue;
                s.milk = 1f; have++;
            }
        }

        /// grazers that are not inside a Breeding Pen (out on the line, in a Feeding Lot, wandering)
        public int SheepNotHome()
        {
            int n = 0;
            foreach (var s in sheep)
            {
                if (s.dead || s.act == Act.Hidden || s.job != Job.Grazer) continue;
                if (s.pen == null || !IsPen(s.pen) || s.act == Act.InMachine || !PenInterior(s.pen).Contains(s.pos)) n++;
            }
            return n;
        }

        /// the two conveyor tiles between the first pen's bottom door and the feeding lot that the tutorial makes you lay
        List<Vector3Int> StarterGap()
        {
            var l = new List<Vector3Int>();
            var p = FirstPen();
            if (p == null) return l;
            var d = DoorTile(p, 0);
            l.Add(new Vector3Int(d.x, d.y - 1, 3));
            l.Add(new Vector3Int(d.x, d.y - 2, 3));
            return l;
        }

        void FixStarterLine()
        {
            var p = FirstPen();
            if (p == null) return;
            var d = DoorTile(p, 0);
            if (BeltDirAt(d.x, d.y) != 3) return; // not the starting layout
            foreach (var v in StarterGap()) if (BeltDirAt(v.x, v.y) < 0 && InMap(v.x, v.y) && tiles[v.x, v.y].building == null && tiles[v.x, v.y].pen == null) SetBelt(v.x, v.y, v.z, true);
        }

        /// the product conveyor from under the machines to the Market spot
        void SetProductPlan()
        {
            beltPlan.Clear();
            var shed = FirstOf(BKind.Shed);
            var market = FirstOf(BKind.Market);
            if (shed == null || market == null) return;
            int row = shed.y - 2, endX = market.x - 1;
            beltPlan.Add(new Vector3Int(shed.x, shed.y - 1, 3));
            for (int x = shed.x; x <= endX; x++) beltPlan.Add(new Vector3Int(x, row, 0));
            // a spur for the milking machine that comes later
            int dx = shed.x + 3;
            if (dx < endX) beltPlan.Add(new Vector3Int(dx, shed.y - 1, 3));
            beltPlan.RemoveAll(v => !InMap(v.x, v.y) || tiles[v.x, v.y].building != null || tiles[v.x, v.y].pen != null);
        }

        /// Decree + pointer for "build X (and give it a worker)" steps.
        string BuildStep(BKind k, bool needWorker, out TutTarget target)
        {
            var d = Buildings.Get(k);
            var b = FirstOf(k);
            string nm = d.name.ToUpper();
            target = null;
            if (b == null)
            {
                var bp = BlueprintFor(k);
                if (placing == d)
                {
                    if (bp != null) { target = TutTarget.At(new Vector2(bp.x + d.w / 2f, bp.y + d.h + 0.3f)); return "Click the glowing BLUEPRINT to place the " + nm; }
                    return "Click on the grass to place the " + nm + " (right-click cancels)";
                }
                if (panel != Panel.Build) { target = TutTarget.Ui("nav_build"); return "Open the BUILD menu"; }
                target = TutTarget.Ui("build_" + k);
                return "Click the " + nm + " in the build menu";
            }
            if (b.construct < 1f)
            {
                target = BuildingTarget(b, "WAIT");
                return "The " + nm + " is being built... " + Mathf.RoundToInt(b.construct * 100) + "%";
            }
            return "Done!";
        }

        /// Decree + pointer for "lay the glowing conveyor tiles" steps.
        string BeltStep(out TutTarget target)
        {
            target = null;
            if (PlanDone()) return "Done!";
            if (!beltTool || clearTool)
            {
                if (panel != Panel.Build) { target = TutTarget.Ui("nav_build"); return "Open the BUILD menu"; }
                target = TutTarget.Ui("tool_conveyor");
                return "Pick the CONVEYOR tool";
            }
            var m = FirstMissingPlan();
            if (m.HasValue) target = TutTarget.At(new Vector2(m.Value.x + 0.5f, m.Value.y + 1.1f), "DRAG");
            return "Drag over the glowing tiles (" + PlanLeft() + " left)";
        }

        bool BuiltWithWorker(BKind k)
        {
            foreach (var b in buildings) if (b.def.kind == k && b.Built) return true;
            return false;
        }

        bool PenDoorOpen() { var p = FirstPen(); return p != null && p.doorOpen[0]; }

        /// the machine the door lesson uses: the Milking Machine if a belt runs into it, else the Shearing Machine
        Building TutDoorMachine()
        {
            foreach (var k in new[] { BKind.Dairy, BKind.Shed })
                foreach (var b in buildings) if (b.def.kind == k && b.Built && TutInDoor(b) >= 0) return b;
            return null;
        }
        int TutInDoor(Building b) { for (int i = 0; i < 4; i++) if (SideHasIn(b, i)) return i; return -1; }

        /// points at the IN / OUT tag of a machine door (or at the door itself when the tag is off screen)
        TutTarget MachineDoorTarget(Building b, int i)
        {
            if (b == null || i < 0) return null;
            string id = "mdoor_" + b.id + "_" + i;
            if (UI.marks.ContainsKey(id)) return TutTarget.Ui(id);
            return TutTarget.At(MachineDoorSpot(b, i) + Vector2.up * 1.6f, "DOOR");
        }

        // ---------------- the tutorial script ----------------
        void BuildTutorial()
        {
            steps = new List<TutStep>();

            steps.Add(new TutStep
            {
                id = "intro",
                intro = new[]
                {
                    new LeaderPage("ATTENTION! I am the SUPREME LEADER. Yes, I am a SHEEP. The biggest, woolliest, most important ram in this field. Welcome to my SHEEPTATORSHIP.", LExpr.Shout),
                    new LeaderPage("These ten sheep in the ^yBREEDING PEN^w are MY flock. And you... you are my HOOF. When I point, you click."),
                    new LeaderPage("Every room has ^yDOORS^w. Open a door and the flock rides the ^yCONVEYOR^w: to the ^yFEEDING LOT^w to eat, then through my ^yMACHINES^w for wool and milk, then home to make MORE sheep.", LExpr.Smug),
                    new LeaderPage("And when there are too many of them... well. There is one more door. Let us begin."),
                },
                decree = () => "Listen to the Supreme Leader",
                done = () => true,
            });

            steps.Add(new TutStep
            {
                id = "inspect",
                intro = new[] { new LeaderPage("First, inspect the workforce. CLICK on one of the sheep in the pen. Any of them. They are all equally replaceable.") },
                decree = () => "Click on a sheep to inspect it",
                target = () => SheepTarget(PickSheep(s => !s.Lamb)),
                done = () => selSheep != null,
                praise = "That is {sel}. Look at the FED bar: HUNGRY. Hungry sheep make no lambs and grow no wool.",
            });

            steps.Add(new TutStep
            {
                id = "belts",
                intro = new[]
                {
                    new LeaderPage("The pen's bottom door leads down to the ^yFEEDING LOT^w by ^yCONVEYOR^w. Some idiot stole two pieces of it.", LExpr.Shout),
                    new LeaderPage("Open ^yBUILD^w, pick the ^yCONVEYOR^w tool and drag over the glowing tiles. The arrows show which way the belt runs. I will find the thief later."),
                },
                onStart = () => { selSheep = null; EnsureCoins(30); beltPlan.Clear(); beltPlan.AddRange(StarterGap()); FocusOn(PenSpot(4, -1)); },
                decree = () => { TutTarget t; return BeltStep(out t); },
                target = () => { TutTarget t; BeltStep(out t); return t; },
                done = () => PlanDone(),
                praise = "The conveyor is whole again. Glorious.",
            });

            steps.Add(new TutStep
            {
                id = "pendoor",
                intro = new[]
                {
                    new LeaderPage("Now the important part: the ^yDOORS^w. Closed, the flock stays in the pen. Open, they walk out onto the conveyor under the door."),
                    new LeaderPage("They are HUNGRY. ^yOPEN THE PEN'S BOTTOM DOOR^w and they will ride straight into the Feeding Lot.", LExpr.Shout),
                },
                onStart = () => { beltTool = false; clearTool = false; beltPlan.Clear(); panel = Panel.None; FocusOn(PenSpot(5, -2)); },
                decree = () => "Click OPEN next to the pen's bottom door",
                target = () => DoorTarget(FirstPen(), 0),
                done = () => PenDoorOpen(),
            });

            steps.Add(new TutStep
            {
                id = "lotdoor",
                onStart = () =>
                {
                    FocusOn(PenSpot(6, -5));
                    LeaderPop("Look at them go. In the FEEDING LOT they eat until they are FULL. Only FULL sheep may leave. When some are fed, open the lot's RIGHT door: it leads to my SHEARING MACHINE.", LExpr.Smug, 9f);
                },
                decree = () => "When they are fed, OPEN the Feeding Lot's right door",
                progress = () => { var l = FirstLot(); return l != null ? Mathf.Min(l.penFed, 4) + "/4 fed" : null; },
                target = () => { var l = FirstLot(); return l != null && l.penFed > 0 ? DoorTarget(l, 1) : BuildingTarget(l, "WATCH"); },
                done = () => { var l = FirstLot(); return l != null && l.doorOpen[1]; },
            });

            steps.Add(new TutStep
            {
                id = "watch",
                onStart = () =>
                {
                    tutBase = shearCount;
                    LeaderPop("Fed and fluffy, they ride into my SHEARING MACHINE: fluff goes in, a sheep comes out, and the belt carries it home. Watch the WOOL counter.", LExpr.Smug, 7f);
                },
                decree = () => "Watch the machine shear 3 sheep",
                progress = () => Mathf.Min(3, shearCount - tutBase) + "/3",
                target = () => BuildingTarget(FirstOf(BKind.Shed), "WATCH"),
                done = () => shearCount - tutBase >= 3,
                praise = "WOOL! Look at it pile up. They did not even have to agree to it.",
            });

            steps.Add(new TutStep
            {
                id = "close",
                intro = new[]
                {
                    new LeaderPage("A shaved sheep is useless until the wool grows back. And nobody makes lambs on a conveyor belt."),
                    new LeaderPage("^yCLOSE THE PEN DOOR^w. The ones still out finish their round and the belt brings them home. Then they stay, and breed.", LExpr.Shout),
                },
                decree = () =>
                {
                    if (PenDoorOpen()) return "Click CLOSE next to the pen's bottom door";
                    return "Wait for the flock to come home (" + SheepNotHome() + " still out)";
                },
                target = () => PenDoorOpen() ? DoorTarget(FirstPen(), 0) : null,
                done = () => !PenDoorOpen() && SheepNotHome() == 0,
                praise = "Everyone is home. OPEN to work them, CLOSE to rest them. That is the whole system.",
            });

            steps.Add(new TutStep
            {
                id = "farm",
                intro = new[]
                {
                    new LeaderPage("The Feeding Lot eats from the ^yFOOD STORE^w (top bar, the carrot). The store does not fill itself, and every fed sheep costs food."),
                    new LeaderPage("Build a ^yFARM^w on the blueprint. It grows food all by itself. My sheep do not WORK. They are WORKED ON.", LExpr.Shout),
                },
                onStart = () =>
                {
                    EnsureCoins(Buildings.Get(BKind.Farm).cost[0]); EnsureFood(60);
                    var p = FirstPen();
                    if (p != null) AddBlueprint(BKind.Farm, p.x + 12, p.y + 4);
                    FocusOn(PenSpot(10, 3));
                },
                decree = () => { TutTarget t; return BuildStep(BKind.Farm, true, out t); },
                target = () => { TutTarget t; BuildStep(BKind.Farm, true, out t); return t; },
                done = () => BuiltWithWorker(BKind.Farm),
                praise = "A farm! Grow, carrots. Grow so they may grow wool.",
            });

            steps.Add(new TutStep
            {
                id = "market",
                intro = new[]
                {
                    new LeaderPage("A pile of wool is worthless. We need COINS. Open ^yBUILD^w and place a ^yMARKET^w on the glowing blueprint."),
                    new LeaderPage("A Market has DOORS too. Whatever rides in through them is SOLD: wool, milk, mutton. Even live sheep, if you send them. Every building runs itself. My sheep NEVER work.", LExpr.Smug),
                },
                onStart = () =>
                {
                    EnsureCoins(Buildings.Get(BKind.Market).cost[0]); EnsureFood(60);
                    var shed = FirstOf(BKind.Shed);
                    if (shed != null) AddBlueprint(BKind.Market, shed.x + 7, shed.y - 3);
                    FocusOn(PenSpot(12, -7));
                },
                decree = () => { TutTarget t; return BuildStep(BKind.Market, false, out t); },
                target = () => { TutTarget t; BuildStep(BKind.Market, false, out t); return t; },
                done = () => { var b = FirstOf(BKind.Market); return b != null && b.Built; },
                praise = "The Market is open! Capitalism has arrived in the field.",
            });

            steps.Add(new TutStep
            {
                id = "productline",
                intro = new[]
                {
                    new LeaderPage("Selling by hoof is for peasants. My machines push their goods onto any ^yCONVEYOR^w leading AWAY from their side."),
                    new LeaderPage("Lay a belt from under the ^ySHEARING MACHINE^w into the ^yMARKET^w. Follow the glowing tiles. Wool will ride to the Market and sell ITSELF.", LExpr.Smug),
                },
                onStart = () => { EnsureCoins(70); SetProductPlan(); FocusOn(PenSpot(12, -7)); },
                decree = () => { TutTarget t; return BeltStep(out t); },
                target = () => { TutTarget t; BeltStep(out t); return t; },
                done = () => PlanDone(),
                praise = "A product line. I can already smell the money.",
            });

            steps.Add(new TutStep
            {
                id = "sellbybelt",
                onStart = () =>
                {
                    beltTool = false; clearTool = false; beltPlan.Clear(); panel = Panel.None;
                    EnsureFluffy(4); EnsureFood(80); tutBase = marketBeltSales;
                    LeaderPop("Some of them are fluffy again. Lucky. OPEN THE PEN DOOR: sheep out, wool off, wool rides to the Market, coins come to ME.", LExpr.Smug, 7f);
                },
                decree = () => !PenDoorOpen() && marketBeltSales - tutBase < 3 ? "Open the pen's bottom door" : "Watch the wool ride into the Market",
                progress = () => Mathf.Min(3, marketBeltSales - tutBase) + "/3",
                target = () => !PenDoorOpen() ? DoorTarget(FirstPen(), 0) : BuildingTarget(FirstOf(BKind.Market), "WATCH"),
                done = () => marketBeltSales - tutBase >= 3,
                praise = "Coins! And I did not lift a hoof. Neither did you, really.",
            });

            steps.Add(new TutStep
            {
                id = "dairy",
                intro = new[]
                {
                    new LeaderPage("Wool is only half the business. ^yEWES^w also make ^yMILK^w. A pink udder means full. Rams make excuses."),
                    new LeaderPage("Build a ^yMILKING MACHINE^w on the glowing spot ON the conveyor. Ewes get milked on the way past. The milk takes the same belt to the Market.", LExpr.Smug),
                },
                onStart = () =>
                {
                    EnsureCoins(Buildings.Get(BKind.Dairy).cost[0]); EnsureFood(60); EnsureMilky(3);
                    var shed = FirstOf(BKind.Shed);
                    if (shed != null) AddBlueprint(BKind.Dairy, shed.x + 3, shed.y);
                    FocusOn(PenSpot(11, -5));
                },
                decree = () => { TutTarget t; return BuildStep(BKind.Dairy, false, out t); },
                target = () => { TutTarget t; BuildStep(BKind.Dairy, false, out t); return t; },
                done = () => { var b = FirstOf(BKind.Dairy); return b != null && b.Built; },
                praise = "The milking machine is ready. The ewes do not know yet.",
            });

            steps.Add(new TutStep
            {
                id = "mdoor",
                intro = new[]
                {
                    new LeaderPage("My machines have ^yDOORS^w too: one on every side a belt touches. A ^gGREEN^w lamp means open. A ^rRED^w lamp means shut."),
                    new LeaderPage("SHUT an ^yIN^w door and the sheep wait on the belt. SHUT an ^yOUT^w door and it turns into a ^yWOOL^w or ^yMILK^w hatch: goods only. Open TWO out doors and the line SPLITS.", LExpr.Smug),
                    new LeaderPage("Practice. Click the ^yIN^w tag on my ^yMILKING MACHINE^w to shut it. Then open it again. Power is a door you can close.", LExpr.Stern),
                },
                onStart = () => { tutBase = 0; var m = TutDoorMachine(); if (m != null) FocusOn(m.Center); },
                decree = () =>
                {
                    var m = TutDoorMachine();
                    if (m == null) return "Watch the machines work";
                    return tutBase == 0 ? "SHUT the " + m.def.name + "'s IN door (click its IN tag)" : "Now OPEN the IN door again";
                },
                target = () => { var m = TutDoorMachine(); return m != null ? MachineDoorTarget(m, TutInDoor(m)) : null; },
                done = () =>
                {
                    var m = TutDoorMachine();
                    if (m == null) return true;
                    bool open = MachineDoorOpen(m, TutInDoor(m));
                    if (tutBase == 0 && !open) { tutBase = 1; LeaderPop("See? They queue politely. They think it is a bus stop. Now OPEN it.", LExpr.Smug, 5f); }
                    return tutBase == 1 && open;
                },
                praise = "Open, shut, open. You have the hooves of a born gatekeeper.",
            });

            steps.Add(new TutStep
            {
                id = "slaughter",
                intro = new[]
                {
                    new LeaderPage("One day there will be too many sheep. Too many mouths. That is what the ^yFEEDING LOT'S BOTTOM DOOR^w is for.", LExpr.Smug),
                    new LeaderPage("Build a ^ySLAUGHTERHOUSE^w on the blueprint. Sheep go in. ^yMUTTON^w comes out, and rides to the Market. Then open that door. Just for a moment.", LExpr.Stern),
                },
                onStart = () =>
                {
                    EnsureCoins(Buildings.Get(BKind.Slaughter).cost[0]); EnsureFood(80); tutBase = slaughtered;
                    var l = FirstLot();
                    if (l != null) AddBlueprint(BKind.Slaughter, l.x + 7, l.y - 3);
                    FocusOn(PenSpot(8, -8));
                },
                decree = () =>
                {
                    TutTarget t;
                    var b = FirstOf(BKind.Slaughter);
                    if (b == null || !b.Built) return BuildStep(BKind.Slaughter, false, out t);
                    var l = FirstLot();
                    if (l != null && !l.doorOpen[0]) return "Open the Feeding Lot's BOTTOM door";
                    if (!PenDoorOpen()) return "Open the pen door so somebody walks into the lot";
                    return "Somebody is about to become mutton...";
                },
                target = () =>
                {
                    TutTarget t;
                    var b = FirstOf(BKind.Slaughter);
                    if (b == null || !b.Built) { BuildStep(BKind.Slaughter, false, out t); return t; }
                    var l = FirstLot();
                    if (l != null && !l.doorOpen[0]) return DoorTarget(l, 0);
                    if (!PenDoorOpen()) return DoorTarget(FirstPen(), 0);
                    return BuildingTarget(b, "WATCH");
                },
                done = () =>
                {
                    if (slaughtered <= tutBase) return false;
                    var l = FirstLot();
                    if (l != null) SetDoor(l, 0, false, false); // the Supreme Leader closes it himself
                    return true;
                },
                praise = "MUTTON. Sells very well. I closed that door again. We are not THAT rich yet.",
            });

            steps.Add(new TutStep
            {
                id = "breed",
                intro = new[]
                {
                    new LeaderPage("Now the most important product of all: MORE SHEEP. Lambs are made in a Breeding Pen, by a FED, happy ^yRAM^w and ^yEWE^w. Do not ask questions.", LExpr.Smug),
                    new LeaderPage("Close the pen door if it is open, so they stay home. Then use the ^ySPEED^w buttons at the top right. Love takes a little while."),
                },
                onStart = () => { tutBase = births; EnsureFood(80); FocusOn(PenSpot(5, 2)); },
                decree = () => PenDoorOpen() ? "Close the pen's bottom door" : "Wait for a lamb (speed up time!)",
                progress = () => { var p = FirstPen(); return p != null ? Mathf.RoundToInt(p.breedProgress * 100) + "/100" : null; },
                target = () => PenDoorOpen() ? DoorTarget(FirstPen(), 0) : (speed < 3 ? TutTarget.Ui("speed_3") : null),
                done = () => births > tutBase,
                praise = "A LAMB! A brand new wool factory. Useful in about three days.",
            });

            steps.Add(new TutStep
            {
                id = "lab",
                intro = new[]
                {
                    new LeaderPage("Finally: SCIENCE. Build a ^yRESEARCH LAB^w (it thinks by itself), then choose a project in the ^yRESEARCH^w menu."),
                    new LeaderPage("Science gives the doors FILTERS, the belts more speed and the machines more teeth. Bigger factory, braver sheep.", LExpr.Smug),
                },
                onStart = () =>
                {
                    EnsureCoins(Buildings.Get(BKind.Lab).cost[0]);
                    var p = FirstPen();
                    if (p != null) AddBlueprint(BKind.Lab, p.x + 17, p.y + 4);
                    FocusOn(PenSpot(12, 3));
                },
                decree = () =>
                {
                    TutTarget t;
                    if (!BuiltWithWorker(BKind.Lab)) return BuildStep(BKind.Lab, true, out t);
                    return panel != Panel.Research ? "Open the RESEARCH menu" : "Click a project to research it";
                },
                target = () =>
                {
                    TutTarget t;
                    if (!BuiltWithWorker(BKind.Lab)) { BuildStep(BKind.Lab, true, out t); return t; }
                    if (panel != Panel.Research) return TutTarget.Ui("nav_research");
                    foreach (var r in Research.All) if (ResearchAvailable(r)) return TutTarget.Ui("research_" + r.id);
                    return null;
                },
                done = () => BuiltWithWorker(BKind.Lab) && (currentResearch != null || tech.Count > 0),
                praise = "The lab is thinking. I can hear it humming.",
            });

            steps.Add(new TutStep
            {
                id = "finale",
                intro = new[]
                {
                    new LeaderPage("You have done... adequately. This is a FACTORY now. It will grow: more sheep need more feeding space, bigger pens, faster belts and more Markets.", LExpr.Smug),
                    new LeaderPage("From now on I issue ^yDECREES^w at the top of the screen. Fulfill them for coins. I will SHOUT when something goes wrong. Things WILL go wrong.", LExpr.Shout),
                    new LeaderPage("Open the doors. Close the doors. Upgrade everything. Long live the flock! (Long live ME.)"),
                },
                decree = () => "Long live the Supreme Leader",
                done = () => true,
            });
        }

        // ---------------- reactive shouting ----------------
        bool AdviceReady(string key) { float t; return !adviceNext.TryGetValue(key, out t) || Day >= t; }
        void Advise(string key, string text, LExpr e, float cooldownDays)
        {
            if (!AdviceReady(key) || popT > 0.5f || LeaderModal) return;
            adviceNext[key] = Day + cooldownDays;
            LeaderPop(text, e);
        }
        public void AdviseNow(string key, string text, LExpr e, float cooldownDays)
        {
            if (!AdviceReady(key)) return;
            adviceNext[key] = Day + cooldownDays;
            LeaderPop(text, e);
        }

        void CheckAdvice()
        {
            float traffic = trafficT; trafficT *= 0.5f;
            if (popT > 0.5f) return;
            bool afterFood = tutDone || tutStep >= 8;
            int pop = Population;
            float perDay = 0; int starving = 0;
            foreach (var s in sheep) if (!s.dead && s.act != Act.Hidden) { perDay += 4f * FoodMult(s); if (s.hunger > 2f) starving++; }
            if (afterFood)
            {
                if (starving > 0) { Advise("starving", "Someone is STARVING. Open the door to the FEEDING LOT, and keep FOOD in the store: Farms, or hay from the Market.", LExpr.Shout, 1.5f); return; }
                if (perDay > 0 && res[(int)Res.Food] < perDay * 0.6f && rate[(int)Res.Food] <= 0.01f) { Advise("food", "The food store is almost EMPTY! The Feeding Lot cannot feed them air. Build or upgrade FARMS.", LExpr.Shout, 1.5f); return; }
                if (pop > popCap) { Advise("homeless", "The pens are OVERCROWDED. Upgrade a BREEDING PEN or build another. Or thin the flock: the Slaughterhouse, or sell them alive at a Market.", LExpr.Stern, 2f); return; }
            }
            if (!tutDone) return;
            // the factory itself
            Building anyPen = null;
            foreach (var b in buildings)
            {
                if (!IsRoom(b) || !RoomActive(b)) continue;
                int cap = RoomCapacity(b);
                if (IsPen(b))
                {
                    anyPen = b;
                    if (b.penHungry >= Mathf.Max(3, b.penCount / 2) && !AnyDoorUsable(b)) { Advise("hungry_" + b.id, "Half the pen is HUNGRY. Open the door that leads to the FEEDING LOT.", LExpr.Shout, 1f); return; }
                    if (b.penMembers > cap) { Advise("crowd_" + b.id, "The pen is OVERCROWDED. Upgrade it, build another one, or... the Slaughterhouse has a door too.", LExpr.Smug, 2f); return; }
                }
                else
                {
                    if (b.penCount >= cap && b.penHungry > 0) { Advise("lotfull_" + b.id, "The Feeding Lot is FULL and sheep are queueing on the belt. UPGRADE it, or build a second one.", LExpr.Stern, 2f); return; }
                    if (b.penCount >= cap && !AnyDoorUsable(b)) { Advise("lotshut_" + b.id, "The Feeding Lot is full of FED sheep and every door is closed. Open a door so they move on.", LExpr.Shout, 1f); return; }
                }
            }
            if (machineDoorNews)
            {
                machineDoorNews = false;
                LeaderPop("NEW DECREE: my machines have DOORS now. Click the IN / OUT tags on a machine. Shut one and the sheep wait, open two and the line splits.", LExpr.Smug, 9f);
                return;
            }
            foreach (var b in buildings)
            {
                if (b.def.kind != BKind.GeneLab) continue;
                float nd = b.geneNoDnaT; b.geneNoDnaT *= 0.5f;
                if (nd > 2f) { Advise("nodna_" + b.id, "My GENE LAB is out of DNA, so it only samples. Let it SAMPLE for a while, or send a few sheep through the Slaughterhouse. Science needs donors.", LExpr.Stern, 2f); return; }
            }
            foreach (var b in buildings)
            {
                if (!HasBeltDoors(b)) continue;
                float w = b.doorWaitT, rw = b.refuseWaitT, nx = b.noExitT;
                b.doorWaitT *= 0.5f; b.refuseWaitT *= 0.5f; b.noExitT *= 0.5f;
                if (nx > 4f) { Advise("noexit_" + b.id, "A sheep is TRAPPED in my " + b.def.name.ToUpper() + ": no open OUT door lets it through. Open one, or fix its filter. The whole line is waiting.", LExpr.Shout, 1f); return; }
                if (w > 12f) { Advise("mshut_" + b.id, "Sheep are piling up at the SHUT door of my " + b.def.name.ToUpper() + ". Open it. Or enjoy the queue. I do.", LExpr.Smug, 1.5f); return; }
                if (rw > 12f) { Advise("refuse_" + b.id, "Sheep are queueing at my " + b.def.name.ToUpper() + ". It does not take sheep (or is not finished). Point that conveyor somewhere useful.", LExpr.Stern, 1.5f); return; }
            }
            foreach (var b in buildings)
            {
                if (HasBeltDoors(b) || IsRoom(b)) continue;
                float rw = b.refuseWaitT; b.refuseWaitT *= 0.5f;
                if (rw > 12f) { Advise("refuse_" + b.id, "Sheep are queueing at my " + b.def.name.ToUpper() + ". It does not take sheep. Point that conveyor somewhere useful.", LExpr.Stern, 1.5f); return; }
            }
            {
                float de = deadEndT; deadEndT *= 0.5f;
                if (de > 10f) { Advise("deadend", "Sheep are stuck at the END of a conveyor. Every belt must lead somewhere: a room, a machine, the Slaughterhouse or a Market.", LExpr.Shout, 1.5f); return; }
            }
            {
                int rams, ewes; CountAdults(out rams, out ewes);
                if (pop >= 4 && pop < popCap && Mathf.Min(rams, ewes) * 2 < Mathf.Max(rams, ewes))
                { Advise("couples", "Only " + Mathf.Min(rams, ewes) + " breeding couples. Too many " + (rams > ewes ? "RAMS" : "EWES") + "! Lambs need both. Send the extras to the Slaughterhouse, not the ones we need.", LExpr.Stern, 3f); return; }
            }
            if (anyPen == null && pop > 0) { Advise("nopen", "My sheep are roaming FREE. Free sheep think free thoughts. Build a BREEDING PEN and connect its doors with conveyors.", LExpr.Shout, 2f); return; }
            if (traffic > 30f) { Advise("traffic", "SHEEP TRAFFIC! The belts are jammed. Research STEEL BELTS and UPGRADE ALL BELTS, add machines, or build a second line.", LExpr.Shout, 2f); return; }
            foreach (var b in buildings)
            {
                if (!IsSheepMachine(b) || !b.Built || HasBeltIn(b)) continue;
                Advise("nobelt_" + b.id, "Our " + b.def.name.ToUpper() + " has no conveyor running INTO it. A machine without sheep is a very expensive box.", LExpr.Stern, 2f);
                return;
            }
            if (AvgMood() < 40) { Advise("mood", "The flock is grumbling. Let them rest in a closed pen, pay WAGES in the Jobs menu, or build a Mud Spa. Or wait for the riot.", LExpr.Stern, 3f); return; }
            foreach (var r in ResInfo.Sellable)
                if (res[(int)r] >= CapOf(r) - 0.5f) { Advise("storage", "Our storage is FULL of " + ResInfo.Names[(int)r] + ". Belt it into a Market, sell it, or build a STORAGE SHED.", LExpr.Stern, 2f); return; }
            if (HasBuilding(BKind.Lab) && currentResearch == null) { Advise("research", "The Research Lab is idle. Pick a RESEARCH project. Idle labs invent excuses.", LExpr.Stern, 2f); return; }
            int fluffy = 0, full = 0; bool penShut = true;
            foreach (var b in buildings) if (IsPen(b) && AnyDoorUsable(b)) penShut = false;
            foreach (var s in sheep) { if (s.dead || s.Lamb || s.job != Job.Grazer) continue; if (s.wool >= 0.98f) fluffy++; if (!s.male && s.milk >= 0.98f) full++; }
            if (fluffy >= 5 && penShut && lineShears) { Advise("fluff", "Look at all that FLUFF standing around in the pen. OPEN THE DOOR. My machines are hungry too.", LExpr.Shout, 2f); return; }
            if (full >= 4 && !lineMilks) { Advise("udders", "The ewes are about to BURST. Build a MILKING MACHINE on the conveyor!", LExpr.Shout, 2f); return; }
            if (res[0] > 1500 + dayIndex * 40) { Advise("rich", "We are RICH. Money in a pile is lazy money. Upgrade the rooms. Upgrade the belts. Build something terrible.", LExpr.Smug, 4f); return; }
        }

        // ---------------- flying resource icons ----------------
        class Flier { public string icon; public Vector2 from; public float t, delay; }
        readonly List<Flier> fliers = new List<Flier>();
        readonly Dictionary<string, float> chipPulse = new Dictionary<string, float>();

        public void FlyIcon(string icon, Vector2 fromGui, int count)
        {
            for (int i = 0; i < count; i++)
                fliers.Add(new Flier { icon = icon, from = fromGui + new Vector2(Random.Range(-6f, 6f), Random.Range(-4f, 4f)), t = 0, delay = i * 0.06f });
        }

        void UpdateFliers(float rdt)
        {
            for (int i = fliers.Count - 1; i >= 0; i--)
            {
                var f = fliers[i];
                if (f.delay > 0) { f.delay -= rdt; continue; }
                f.t += rdt / 0.7f;
                if (f.t >= 1f) { fliers.RemoveAt(i); chipPulse[f.icon] = 0.35f; Sfx.Play("click", 0.15f, 1.6f); }
            }
            var keys = new List<string>(chipPulse.Keys);
            foreach (var k in keys) chipPulse[k] = Mathf.Max(0, chipPulse[k] - rdt);
        }

        void DrawFliers()
        {
            foreach (var f in fliers)
            {
                if (f.delay > 0) continue;
                Rect target;
                Vector2 to = UI.marks.TryGetValue("chip_" + f.icon, out target) ? target.center : new Vector2(20, 12);
                float e = 1 - (1 - f.t) * (1 - f.t);
                Vector2 p = Vector2.Lerp(f.from, to, e) + Vector2.up * (-Mathf.Sin(f.t * Mathf.PI) * 40);
                var t = Icons.Tex(f.icon);
                UI.Icon(Mathf.Round(p.x - t.width / 2f), Mathf.Round(p.y - t.height / 2f), f.icon);
            }
        }

        public float ChipPulse(string icon) { float v; return chipPulse.TryGetValue(icon, out v) ? v : 0; }

        // ---------------- drawing: decree box ----------------
        void DrawDecree()
        {
            float left = FreeLeft, right = FreeRight;
            float avail = right - left;
            if (avail < 170) return;
            var st = CurStep;
            string head, text, prog = null, reward = null, side;
            if (st != null)
            {
                head = "SUPREME DECREE";
                text = st.decree != null ? st.decree() : "";
                prog = st.progress != null ? st.progress() : null;
                side = "STEP " + Mathf.Max(1, tutStep) + "/" + (steps.Count - 1);
            }
            else
            {
                var o = CurrentObjective;
                head = o != null ? "DECREE #" + (objectiveIndex + 1) : "NO DECREES";
                text = o == null ? "^gAll decrees fulfilled. Rule as you see fit." : o.text;
                prog = o != null && o.progress != null ? o.progress(this) : null;
                reward = o != null && o.reward > 0 ? "+" + o.reward : null;
                side = TierName().ToUpper();
            }
            float ratio = -1;
            if (prog != null)
            {
                int slash = prog.IndexOf('/');
                float a, b;
                if (slash > 0 && float.TryParse(prog.Substring(0, slash), out a) && float.TryParse(prog.Substring(slash + 1), out b) && b > 0) ratio = Mathf.Clamp01(a / b);
            }
            const float face = 30;
            float rw = reward != null ? PixelFont.Width(reward) + 13 : 0;
            float want = Mathf.Max(face + 14 + PixelFont.Width(text) + rw + 16, face + 14 + PixelFont.Width(head) + PixelFont.Width(side) + 24);
            float w = Mathf.Round(Mathf.Clamp(want, 220, Mathf.Min(avail, 400)));
            float tw = w - face - 22 - (rw > 0 ? rw + 6 : 0);
            var lines = PixelFont.Wrap(text, tw, 1);
            float h = Mathf.Max(face + 10, 6 + 10 + lines.Count * 10 + (ratio >= 0 ? 10 : 0) + 6);
            float cx = Mathf.Clamp(W / 2, left + w / 2, right - w / 2);
            var r = new Rect(Mathf.Round(cx - w / 2), TopH + 6, w, h);
            UI.Block(r);
            bool flash = st != null && tutWaitNext;
            UI.Box(r, UI.BgDark, flash ? UI.Good : UI.Gold);
            var fr = new Rect(r.x + 5, r.y + 5, face, face);
            UI.Well(fr, UI.BannerLo);
            if (UI.Repaint) GUI.DrawTexture(new Rect(fr.x + 3, fr.y + 3, 24, 24), LeaderArt.Portrait(LExpr.Stern, false));
            float tx = fr.xMax + 7;
            PixelFont.Draw(tx, r.y + 6, head, UI.Gold);
            float sw = PixelFont.Width(side);
            if (tx + PixelFont.Width(head) + 10 + sw < r.xMax - 8) PixelFont.Draw(r.xMax - 8 - sw, r.y + 6, side, UI.Dim);
            float y = r.y + 18;
            foreach (var l in lines) { PixelFont.Draw(tx, y, l, flash ? UI.Good : UI.Text); y += 10; }
            if (reward != null)
            {
                float rx = r.xMax - 8 - rw;
                PixelFont.Draw(rx, r.y + 18, reward, UI.Gold);
                UI.Icon(rx + rw - 9, r.y + 17, "coin");
            }
            if (ratio >= 0)
            {
                float pw = PixelFont.Width(prog);
                UI.Bar(new Rect(tx, y + 1, r.xMax - 8 - tx - pw - 6, 6), ratio, UI.Gold);
                PixelFont.Draw(r.xMax - 8 - pw, y, prog, UI.Dim);
            }
            UI.Tip(r, st != null ? "SUPREME DECREE\nDo what the Supreme Leader says. The arrow shows where." : "DECREES\nFulfill decrees for coins. They get bigger. So do the consequences.");
        }

        // ---------------- drawing: pointer ----------------
        void DrawTutorialPointer(float s)
        {
            var st = CurStep;
            if (st == null || st.target == null || LeaderModal || currentEvent != null || tutWaitNext) return;
            var t = st.target();
            if (t == null) return;
            float bob = Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f)) * 5f;
            if (t.ui != null)
            {
                Rect r;
                if (!UI.marks.TryGetValue(t.ui, out r)) return;
                float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 8f);
                var pr = new Rect(r.x - 4, r.y - 4, r.width + 8, r.height + 8);
                var c = Color.Lerp(UI.Gold, Color.white, pulse * 0.5f);
                UI.Frame(pr, UI.Ink, 1);
                UI.Frame(UI.Inset(pr, 1), c, 3);
                UI.Frame(UI.Inset(pr, 4), UI.Ink, 1);
                int dir;
                if (r.y < TopH + 30) dir = 1;                 // under the top bar: arrow below, pointing up
                else if (r.center.x > W * 0.62f) dir = 2;     // right-hand panels: arrow on the left, pointing right
                else if (r.center.x < W * 0.3f) dir = 3;      // left-hand panels: arrow on the right, pointing left
                else dir = 0;
                Vector2 tip = dir == 1 ? new Vector2(r.center.x, pr.yMax + 2 + bob) : dir == 2 ? new Vector2(pr.x - 2 - bob, r.center.y)
                            : dir == 3 ? new Vector2(pr.xMax + 2 + bob, r.center.y) : new Vector2(r.center.x, pr.y - 2 - bob);
                DrawBigArrow(tip, dir);
            }
            else if (t.world)
            {
                Vector2 p = ToGui(t.pos, s);
                bool on = p.x > FreeLeft + 24 && p.x < FreeRight - 24 && p.y > TopH + 56 && p.y < H - 40;
                if (!on)
                {
                    // bring the camera to the thing we are pointing at
                    string key = tutStep + ":" + Mathf.RoundToInt(t.pos.x) + "," + Mathf.RoundToInt(t.pos.y);
                    if (key != lastPanKey && Time.unscaledTime > panT) { lastPanKey = key; panT = Time.unscaledTime + 1.5f; FocusFree(t.pos - Vector2.up * 1.2f); }
                    if (p.x < FreeLeft || p.x > FreeRight || p.y < TopH || p.y > H) return;
                }
                // a pulsing ring on the ground under the target
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7f);
                DrawBigArrow(new Vector2(p.x, p.y - bob), 0);
                string lbl = t.label ?? "CLICK";
                float lw = PixelFont.Width(lbl);
                var lr = new Rect(Mathf.Round(p.x - lw / 2 - 4), Mathf.Round(p.y - bob - 48), lw + 8, 13);
                UI.Box(lr, UI.Gold);
                var tc = UI.Ink; tc.a = 0.7f + 0.3f * pulse;
                PixelFont.Draw(lr.x + 4, lr.y + 3, lbl, tc, 1, false);
            }
        }

        /// A fat gold arrow whose tip touches `tip`. dir: 0 points down, 1 up, 2 right, 3 left.
        void DrawBigArrow(Vector2 tip, int dir)
        {
            const int head = 11, shaftLen = 14, shaftW = 9;
            float tx = Mathf.Round(tip.x), ty = Mathf.Round(tip.y);
            for (int pass = 0; pass < 2; pass++)
            {
                // pass 0 = ink outline, pass 1 = gold body
                float o = pass == 0 ? 1 : 0;
                for (int i = 0; i < head; i++)
                {
                    float hw = i + o;
                    Color c = pass == 0 ? UI.Ink : (i < 2 ? UI.GoldHi : UI.Gold);
                    if (dir == 0) UI.Fill(new Rect(tx - hw, ty - 1 - i, hw * 2 + 1, 1), c);
                    else if (dir == 1) UI.Fill(new Rect(tx - hw, ty + i, hw * 2 + 1, 1), c);
                    else if (dir == 2) UI.Fill(new Rect(tx - 1 - i, ty - hw, 1, hw * 2 + 1), c);
                    else UI.Fill(new Rect(tx + i, ty - hw, 1, hw * 2 + 1), c);
                }
                float sw = shaftW / 2f + o;
                Color sc = pass == 0 ? UI.Ink : UI.Gold;
                if (dir == 0) UI.Fill(new Rect(tx - sw, ty - head - shaftLen - o, sw * 2 + 1, shaftLen + o), sc);
                else if (dir == 1) UI.Fill(new Rect(tx - sw, ty + head, sw * 2 + 1, shaftLen + o), sc);
                else if (dir == 2) UI.Fill(new Rect(tx - head - shaftLen - o, ty - sw, shaftLen + o, sw * 2 + 1), sc);
                else UI.Fill(new Rect(tx + head, ty - sw, shaftLen + o, sw * 2 + 1), sc);
            }
        }

        // ---------------- drawing: dialogue ----------------
        void DrawLeaderDialog()
        {
            if (!LeaderModal) return;
            UI.inputEnabled = true;
            var page = dlgPages[dlgIndex];
            UI.Fill(new Rect(0, 0, W, H), new Color(0.05f, 0.03f, 0.03f, 0.38f));
            float w = Mathf.Min(560, W - 24), h = 124;
            var r = new Rect(Mathf.Round(W / 2 - w / 2), Mathf.Round(H - h - 14), w, h);
            UI.Panel(r, null);
            // portrait
            var pr = new Rect(r.x + 9, r.y + 9, 106, 106);
            UI.Well(pr, UI.BannerLo);
            UI.Fill(new Rect(pr.x + 1, pr.y + 1, pr.width - 2, 34), UI.Banner);
            int total = VisibleLen(page.text);
            bool typing = dlgChars < total;
            bool open = typing && (Mathf.FloorToInt(Time.unscaledTime * 10) % 2 == 0);
            float shake = page.expr == LExpr.Shout && typing ? Mathf.Round(Mathf.Sin(Time.unscaledTime * 60) * 1) : 0;
            if (UI.Repaint) GUI.DrawTexture(new Rect(pr.x + 5 + shake, pr.y + 5, 96, 96), LeaderArt.Portrait(page.expr, open));
            // name + page dots
            float tx = pr.xMax + 10;
            UI.Tab(tx - 2, r.y + 7, LeaderName, "crown");
            for (int i = 0; i < dlgPages.Count && dlgPages.Count > 1; i++)
            {
                var d = new Rect(r.xMax - 14 - (dlgPages.Count - 1 - i) * 8, r.y + 12, 5, 5);
                UI.Fill(d, UI.Ink);
                UI.Fill(UI.Inset(d, 1), i == dlgIndex ? UI.Gold : i < dlgIndex ? UI.GoldLo : UI.Hi2);
            }
            // typed text (wrapped once so words never jump)
            float textW = r.xMax - 12 - tx;
            var lines = PixelFont.Wrap(page.text, textW, 1);
            int budget = Mathf.FloorToInt(dlgChars);
            float y = r.y + 30;
            foreach (var l in lines)
            {
                int n = VisibleLen(l);
                if (budget <= 0) break;
                PixelFont.Draw(tx, y, budget >= n ? l : VisiblePrefix(l, budget), UI.Text);
                budget -= n + 1;
                y += 11;
            }
            // buttons
            bool last = dlgIndex == dlgPages.Count - 1;
            string label = typing ? "..." : last ? "YES, SUPREME LEADER!" : "NEXT";
            float bw = PixelFont.Width(label) + 22 + UI.KeyW("SPACE");
            var br = new Rect(r.xMax - 10 - bw, r.yMax - 28, bw, 20);
            UI.Mark("dlg_next", br);
            if (UI.ButtonEx(br, label, null, "SPACE", true, null, last && !typing ? BtnStyle.Primary : BtnStyle.Normal)) { AdvanceDialog(); return; }
            if (dlgSkippable && dlgIndex == 0)
            {
                if (UI.ButtonEx(new Rect(tx, r.yMax - 28, 112, 20), "SKIP TUTORIAL", null, null, true, "SKIP TUTORIAL\nFor experienced dictators. Everything unlocks at once.")) { SkipTutorial(); return; }
            }
            // a click anywhere else advances
            if (UI.Clickable(new Rect(0, 0, W, H), null, false)) AdvanceDialog();
        }

        // ---------------- drawing: shout bubble ----------------
        void DrawLeaderPop()
        {
            if (popT <= 0 || string.IsNullOrEmpty(popText) || LeaderModal) return;
            float a = Mathf.Clamp01(Mathf.Min(popT * 3f, (popDur - popT) * 6f));
            UI.alpha = a;
            float maxText = Mathf.Min(250, (FreeRight - FreeLeft) - 80);
            if (maxText < 120) { UI.alpha = 1; return; }
            var lines = PixelFont.Wrap(popText, maxText, 1);
            float lw = 0; foreach (var l in lines) lw = Mathf.Max(lw, PixelFont.Width(l));
            float w = 58 + Mathf.Max(lw, PixelFont.Width(LeaderName)) + 14;
            float h = Mathf.Max(58, 20 + lines.Count * 10 + 10);
            float bottom = H - 10 - (ToolActive ? 30 : 0);
            var r = new Rect(Mathf.Round(FreeRight - w), Mathf.Round(bottom - h), w, h);
            UI.Block(r);
            UI.Box(r, UI.Bg, popExpr == LExpr.Shout ? UI.Bad : UI.Gold);
            var pr = new Rect(r.x + 4, r.y + 4, 50, 50);
            UI.Well(pr, UI.BannerLo);
            bool open = popT > popDur - 1.2f && Mathf.FloorToInt(Time.unscaledTime * 10) % 2 == 0;
            if (UI.Repaint)
            {
                var prev = GUI.color; GUI.color = new Color(1, 1, 1, a);
                GUI.DrawTexture(new Rect(pr.x + 1, pr.y + 1, 48, 48), LeaderArt.Portrait(popExpr, open));
                GUI.color = prev;
            }
            var gc = UI.Gold; gc.a = a;
            var tc = UI.Text; tc.a = a;
            PixelFont.Draw(r.x + 60, r.y + 6, LeaderName, gc);
            float y = r.y + 18;
            foreach (var l in lines) { PixelFont.Draw(r.x + 60, y, l, tc); y += 10; }
            UI.Fill(new Rect(r.x + 60, r.yMax - 5, (r.width - 66) * Mathf.Clamp01(popT / popDur), 1), new Color(gc.r, gc.g, gc.b, 0.5f * a));
            UI.alpha = 1;
            if (UI.Clickable(r, null, false)) popT = Mathf.Min(popT, 0.3f);
        }
    }
}
