using System.Collections.Generic;
using UnityEngine;

namespace Fleeced
{
    /// The Sheep Gate. HOLD SPACE and lambs pop out of it into the Breeding Pen it stands against.
    /// - A charge limits it: 6 releases, refilling one every 6 s of game time. The first press releases at once, then one every
    ///   0.5 s while SPACE is held. The Gate's upgrade paths (Core/Upgrades.cs) raise all of these.
    /// - The pen limits it too: every lamb needs a free place in that pen and under the population cap. Otherwise "PEN FULL".
    /// - Releases happen inside Simulate, in game time, so they stop when the game pauses and a seeded run is reproducible.
    ///   The keyboard (or the mouse held on the gate's HUD button) only sets gateHeld.
    /// - Five stages (M12), on the building's level: every stage adds a lane with its own door, and one press lets a litter out of
    ///   every lane (x1, x2, x6, x16, x50 the lambs). From the Double Gate on, every 6th held release in a row is a STAMPEDE.
    ///   ReleaseLambs only counts what the screen should show; UpdateGateVisual opens the doors and shows one burst.
    public partial class Game
    {
        public const float GateCapBase = 6f, GateRefillBase = 6f, GateReleaseBase = 0.5f;
        public float gateCharge = GateCapBase; // saved
        public int gateReleased;              // lambs let out, ever (saved; the gate decrees count them)
        public bool gateHeld;                 // the gate is being held (a fresh SPACE press, or the HUD button)
        bool gateArmed, gateMouseHeld;
        float gateCd, gateDoorT, gateFullT, gateEmptyT;
        double lambDebt;                      // a fraction of a lamb per release, carried to the next release
        float gateAutoT;                      // the Gate Keeper's progress to its next release
        int gateStreak; float gateIdleT;      // held releases in a row (a STAMPEDE every 6th) and the time since the last hold; not saved
        // what the simulation asks the screen to show (runtime, read only by the visual pass): lambs let out since the last burst,
        // whether a held release or a stampede was among them, releases whose lane doors still have to open (and their lambs, for
        // the runners' size), a stage that opened
        double gateShowN, gateRunN; bool gateShowHeld, gateShowStampede, gatePendingStampede; int gatePending, gateOpenedShow;
        float gateShowT, gateIntroT; int gateLaneStage; float[] gateLaneT = new float[5];

        /// the working Sheep Gate (built), or null
        public Building GateBuilding { get { foreach (var b in buildings) if (b.def.kind == BKind.SheepGate && b.Built) return b; return null; } }
        /// the Sheep Gate, built or being upgraded to its next stage, or null
        public Building GateAny { get { foreach (var b in buildings) if (b.def.kind == BKind.SheepGate && (b.Built || b.upgrading)) return b; return null; } }
        /// the gate's stage (M12): 1 Sheep Gate .. 5 Singularity Gate, 0 without a gate. An upgrade in progress keeps the stage
        /// it had, so nothing it unlocked locks again while it rebuilds.
        public int GateStage { get { var g = GateAny; return g != null ? g.level : 0; } }
        public float GateCap { get { return GateCapBase + (float)PathAdd("gate_cap"); } }
        public float GateRefillTime { get { return GateRefillBase / (float)PathMult("gate_refill"); } }
        public float GateReleaseInterval { get { return GateReleaseBase / (float)PathMult("gate_speed"); } }
        public double LambsPerRelease { get { return 1 + PathAdd("gate_lambs"); } }
        /// lambs one press lets out at the gate's stage: every lane's litter
        public double LambsPerPress { get { var g = GateAny; return LambsPerRelease * ScaleMath.GateTotalMult(g != null ? g.level : 1); } }
        /// a STAMPEDE's multiplier: x2 from the Double Gate on, more with the Stampede path
        public double StampedeMult { get { return 2 * PathMult("gate_burst"); } }

        /// The Breeding Pen a 2x2 gate with its bottom-left tile at (x, y) stands against: on the pen's top, left or right side,
        /// touching it along the whole side, and not covering a pen door tile. Null if none. (Not below a pen: the gate's sprite
        /// would cover the pen.)
        public Building GatePenAt(int x, int y)
        {
            foreach (var p in buildings)
            {
                if (p.def.kind != BKind.Pen) continue;
                int w = p.def.w, h = p.def.h, doorX = p.x + w / 2, doorY = p.y + h / 2;
                if (y == p.y + h && x >= p.x && x <= p.x + w - 2 && x != doorX - 1 && x != doorX) return p;
                if ((x == p.x + w || x == p.x - 2) && y >= p.y && y <= p.y + h - 2 && y != doorY - 1 && y != doorY) return p;
            }
            return null;
        }

        /// a free spot for a gate against `pen`: on top, nearest the middle first, then the right side, then the left
        public bool FindGateSpot(Building pen, out int gx, out int gy)
        {
            var d = Buildings.Get(BKind.SheepGate);
            int w = pen.def.w, h = pen.def.h;
            var tries = new List<Vector2Int>();
            for (int k = 0; k < w; k++) tries.Add(new Vector2Int(pen.x + w / 2 - 1 + (k % 2 == 0 ? -k / 2 : (k + 1) / 2), pen.y + h));
            for (int y = pen.y; y <= pen.y + h - 2; y++) tries.Add(new Vector2Int(pen.x + w, y));
            for (int y = pen.y; y <= pen.y + h - 2; y++) tries.Add(new Vector2Int(pen.x - 2, y));
            foreach (var t in tries) if (CanPlace(d, t.x, t.y) && Untouched(t.x, t.y, d.w, d.h)) { gx = t.x; gy = t.y; return true; }
            gx = gy = -1;
            return false;
        }

        /// no belt and no path on these tiles: the Supreme Leader's own placement must never delete the player's work
        bool Untouched(int x0, int y0, int w, int h)
        {
            for (int x = x0; x < x0 + w; x++) for (int y = y0; y < y0 + h; y++) if (tiles[x, y].belt >= 0 || tiles[x, y].path) return false;
            return true;
        }

        /// where a lamb comes in: just inside the fence, next to the gate
        static Vector2 GateEntry(Building gate, Building pen)
        {
            var r = PenInterior(pen);
            float cx = Mathf.Clamp(gate.x + 1f, r.xMin + 0.3f, r.xMax - 0.3f), cy = Mathf.Clamp(gate.y + 1f, r.yMin + 0.3f, r.yMax - 0.3f);
            if (gate.y >= pen.y + pen.def.h) return new Vector2(cx, r.yMax - 0.3f);
            if (gate.x >= pen.x + pen.def.w) return new Vector2(r.xMax - 0.3f, cy);
            return new Vector2(r.xMin + 0.3f, cy);
        }

        /// every simulation step: refill the charge, and release while the gate is held
        void SimGate(float dt)
        {
#if FLEECED_DEV
            if (DevHooks.GateHeld.HasValue) gateHeld = DevHooks.GateHeld.Value; // tests and the bot simulate without frames
#endif
            var gate = GateAny;
            if (gate == null) return;
            float cap = GateCap;
            if (gate.upgrading)
            {
                // rebuilding for its next stage: nothing comes out, but the charge keeps refilling (it reopens full anyway)
                if (gateCharge < cap) gateCharge = Mathf.Min(cap, gateCharge + dt / GateRefillTime);
                gateCd = 0; gateStreak = 0;
                return;
            }
            gate.status = GatePenAt(gate.x, gate.y) == null ? "No pen: it must stand against a Breeding Pen's fence" : "Hold SPACE: lambs into the pen";
            if (gateCharge < cap) gateCharge = Mathf.Min(cap, gateCharge + dt / GateRefillTime);
            if (!gateHeld)
            {
                gateCd = 0; // so the next press releases at once
                gateIdleT += dt;
                if (gateIdleT >= 1f) gateStreak = 0; // a second without holding breaks the stampede's streak
                double perMin = PathAdd("gate_auto");
                if (perMin > 0 && gateCharge >= 1f)
                {
                    gateAutoT += dt * (float)perMin / 60f;
                    if (gateAutoT >= 1f) { gateAutoT -= 1f; ReleaseLambs(gate, true); }
                }
                return;
            }
            gateIdleT = 0;
            gateCd -= dt;
            if (gateCd > 0) return;
            gateCd = Mathf.Max(0, gateCd + GateReleaseInterval); // at most one release a step
            ReleaseLambs(gate);
        }

        /// one release: a lamb (more with the "lambs per release" upgrade) out of every lane for one charge, if the pen has room.
        /// `quiet` is the Gate Keeper: it never stampedes and never breaks the player's streak.
        void ReleaseLambs(Building gate, bool quiet = false)
        {
            var pen = GatePenAt(gate.x, gate.y);
            if (pen == null)
            {
                if (quiet) return;
                gateStreak = 0;
                if (gateFullT <= 0) { gateFullT = 1.2f; FloatText(gate.Center + Vector2.up * 1.6f, "NO PEN", new Color(1f, 0.4f, 0.35f)); }
                return;
            }
            if (gateCharge < 1f)
            {
                if (quiet) return;
                gateStreak = 0;
                if (gateEmptyT <= 0) { gateEmptyT = 1f; FloatText(gate.Center + Vector2.up * 1.6f, "REFILLING", new Color(0.7f, 0.7f, 0.7f)); }
                return;
            }
            double room = System.Math.Floor(System.Math.Min(RoomCapacity(pen) - Members(pen), FlockCap - Population));
            if (room <= 0) { if (!quiet) { gateStreak = 0; PenFull(gate); } return; }
            // every 6th held release in a row from the Double Gate on (at stage 1 the && stops before the streak moves)
            bool stampede = !quiet && gate.level >= 2 && ++gateStreak % ScaleMath.StampedeEvery == 0;
            lambDebt += LambsPerRelease * ScaleMath.GateTotalMult(gate.level) * (stampede ? StampedeMult : 1.0); // exactly today's at stage 1
            double want = System.Math.Floor(lambDebt);
            lambDebt -= want;
            double n = System.Math.Min(want, room);
            gateCharge -= 1f;
            var at = GateEntry(gate, pen);
            // named lambs while the pen and the farm have names to give (at most 8 a release); the rest join the pen's herd
            int named = 0;
            for (; named < n && named < 8 && CanName(pen); named++)
            {
                // spread along the fence the gate stands on, never through it
                float j = ((float)rng.NextDouble() - 0.5f) * 0.8f;
                var ri = PenInterior(pen);
                var p = gate.y >= pen.y + pen.def.h ? at + new Vector2(j, 0) : at + new Vector2(0, j);
                p = new Vector2(Mathf.Clamp(p.x, ri.xMin + 0.2f, ri.xMax - 0.2f), Mathf.Clamp(p.y, ri.yMin + 0.2f, ri.yMax - 0.2f));
                var s = CreateSheep(p, null, null, null);
                s.ageDays = 0; s.wool = 0.15f; s.hunger = 0; s.parents = "the Sheep Gate";
                if (s.pen != pen) { if (s.pen != null) s.pen.penMembers--; s.pen = pen; pen.penMembers++; }
                gateReleased = Bump(gateReleased, 1);
            }
            double toHerd = n - named;
            if (toHerd >= 1) { HerdAddLambs(pen, Genetics.None, toHerd); Ledger(toHerd); gateReleased = Bump(gateReleased, toHerd); }
            gateDoorT = 0.35f;
            // the screen shows it (UpdateGateVisual): the lane doors open, runners hop out, and one burst counts a moment's releases
            gateShowN += n; gateRunN += n; gateShowHeld |= !quiet; gateShowStampede |= stampede; gatePendingStampede |= stampede; gatePending++;
        }

        /// a new stage has opened (FinishConstruction): a full charge as a gift, the Supreme Leader's pop, and at the Double Gate the
        /// breeding chambers' introduction. The burst, the thump and the dust are the visual pass's (gateOpenedShow).
        void GateStageOpened(Building g)
        {
            gateCharge = GateCap;
            gateStreak = 0;
            gateOpenedShow = g.level;
            string pop = StagePop(g.level);
            if (pop != null) AnnounceWhenFree("gate_stage", () => LeaderPop(pop, LExpr.Shout, 8f));
            if (g.level >= 2 && !flags.Contains("chamber_intro") && !IsWaiting("chambers")) SayWhenQuiet("chambers", ChamberIntro); // it marks itself said
        }

        static string StagePop(int stage)
        {
            switch (stage)
            {
                case 2: return "THE DOUBLE GATE IS OPEN. Two lanes, twice the lambs a press. Hold SPACE and empty it: every SIXTH release in a row is a STAMPEDE.";
                case 3: return "THE FLOCK PORTAL. Three lanes, one on the roof, and every lane a double litter. SIX times the lambs.";
                case 4: return "THE SHEEP LAUNCHER. The upper lanes FIRE lambs into the pen. Sixteen times the lambs. The landing is their problem.";
                case 5: return "THE SINGULARITY GATE. Five lanes, FIFTY times the lambs, falling out of a cloud of pure wool. Physics has been sent to the Slaughterhouse.";
            }
            return null;
        }

        void PenFull(Building gate)
        {
            if (gateFullT > 0) return;
            gateFullT = 1.2f;
            FloatText(gate.Center + Vector2.up * 1.6f, "PEN FULL", new Color(1f, 0.4f, 0.35f));
            Sfx.Play("thud", 0.35f);
            if (tutDone) AdviseNow("gate_full", "The pen is FULL, Hoof. UPGRADE it, build another BREEDING PEN, or send some sheep to market. Lambs do not fold.", LExpr.Shout, 2f);
        }

        /// The keyboard: a fresh SPACE press arms the gate and holding it keeps releasing. The press that closes a dialogue page
        /// can't arm it (HandleInput doesn't run while a dialogue is open), and pausing disarms it. P and Pause pause instead.
        void UpdateGateKey()
        {
#if FLEECED_DEV
            if (DevHooks.GateHeld.HasValue) { gateHeld = DevHooks.GateHeld.Value; return; }
#endif
            if (showHelp || ToolActive) { gateArmed = false; gateMouseHeld = false; gateHeld = false; return; } // SPACE does nothing behind help or tools
            if (Input.GetKeyDown(KeyCode.Space)) gateArmed = true;
            if (!Input.GetKey(KeyCode.Space) || speed == 0) gateArmed = false;
            if (!Input.GetMouseButton(0)) gateMouseHeld = false;
            gateHeld = (gateArmed || gateMouseHeld) && speed != 0;
        }

        /// whenever input is blocked (a dialogue, an event card, the title): nothing stays held
        void DisarmGate()
        {
            gateArmed = false; gateMouseHeld = false;
#if FLEECED_DEV
            if (DevHooks.GateHeld.HasValue) return;
#endif
            gateHeld = false;
        }

        /// every frame: the gate's doors, its runners and bursts, and the feedback timers (real time; nothing here feeds the simulation)
        void UpdateGateVisual(float rdt)
        {
            gateDoorT -= rdt; gateFullT -= rdt; gateEmptyT -= rdt;
            HerdIntroCheck(rdt); // the herd's introduction waits until SPACE has been up for a moment (Core/Herd.cs)
            M12IntroCheck(rdt);
            var gate = GateAny;
            if (gate == null || gate.sr == null || gate.go == null) { ClearGateShow(); gateOpenedShow = 0; return; }
            if (gate.upgrading)
            {
                // under its scaffold: the last releases before the click still get their burst, then nothing is left over
                ShowGateLanes(gate, 0);
                if (gateShowN >= 1) ShowBurst(gate.Center + Vector2.up * 1.1f, gateShowN, gateShowStampede ? BurstKind.Stampede : BurstKind.Gate, null, gateShowHeld);
                ClearGateShow();
                return;
            }
            int stage = gate.level;
            var pen = GatePenAt(gate.x, gate.y);
            Vector2 entry = pen != null ? GateEntry(gate, pen) : gate.Center;
            if (stage <= 1)
            {
                ShowGateLanes(gate, 0);
                var want = BuildingArt.GateSprite(gateDoorT > 0); // cached sprites: also undoes a refresh that shut the door mid-release
                if (gate.sr.sprite != want) gate.sr.sprite = want;
                if (gatePending > 0 && pen != null && IsVisible(entry)) SpawnParticles(entry, new Color(0.6f, 0.5f, 0.38f), 8, 1.6f);
            }
            else
            {
                if (gate.art != null && gate.sr.sprite != gate.art.sprite) gate.sr.sprite = gate.art.sprite;
                ShowGateLanes(gate, stage);
                if (gatePending > 0 && pen != null)
                {
                    // every lane opens, a moment apart, and sends its litter hopping (or flying) into the pen
                    int lanes = BuildingArt.GateLaneCount(stage);
                    bool vis = IsVisible(gate.Center);
                    double perLane = System.Math.Max(1, gateRunN / System.Math.Max(1, lanes)); // this frame's releases
                    for (int j = 0; j < lanes; j++)
                    {
                        float delay = j * 0.06f;
                        gateLaneT[j] = -delay; // opens when it reaches 0, shows open for 0.3 s
                        if (!vis) continue;
                        Vector2 door = new Vector2(gate.x, gate.y) + BuildingArt.GateLaneLocal(stage, j) + new Vector2(4.5f / 16f, 0.1f);
                        int row = BuildingArt.GateLaneRow(stage, j);
                        var ri = PenInterior(pen);
                        Vector2 to = entry + new Vector2(((float)fxRng.NextDouble() - 0.5f) * 3f, ((float)fxRng.NextDouble() - 0.5f) * 2f);
                        // a runner is drawn up to a tile to one side of its point (its pivot is a corner): it lands clear of the fence
                        to = new Vector2(Mathf.Clamp(to.x, ri.xMin + 0.95f, ri.xMax - 1.35f), Mathf.Clamp(to.y, ri.yMin + 0.2f, ri.yMax - 0.2f));
                        float arc = row == 0 ? 0.3f : row == 1 ? 2.5f : 3f, dur = row == 0 ? 0.45f : row == 1 ? 0.8f : 0.9f;
                        int throws = gatePendingStampede ? 2 : 1;
                        for (int k = 0; k < throws; k++) Run(door, to + new Vector2(k * 0.4f, 0), perLane, 0, arc, dur, delay + k * 0.1f, true, gate.sr.sortingOrder + 2);
                        SpawnParticles(door, new Color(0.6f, 0.5f, 0.38f), 4, 1.2f);
                    }
                }
                for (int j = 0; j < gateLaneT.Length; j++) gateLaneT[j] = Mathf.Min(1f, gateLaneT[j] + rdt);
            }
            gatePending = 0; gatePendingStampede = false; gateRunN = 0;
            // one burst for the releases of the last quarter second
            gateShowT -= rdt;
            if (gateShowN >= 1 && gateShowT <= 0)
            {
                ShowBurst(entry + Vector2.up * 1.1f, gateShowN, gateShowStampede ? BurstKind.Stampede : BurstKind.Gate, null, gateShowHeld);
                gateShowN = 0; gateShowHeld = false; gateShowStampede = false; gateShowT = 0.25f;
            }
            if (gateOpenedShow > 0)
            {
                // a new stage opens: its name in gold, a thump and dust
                ShowBurst(gate.Center + Vector2.up * 2.2f, 0, BurstKind.Stage, gate.Title.ToUpper() + "!", true);
                if (IsVisible(gate.Center)) { shake = Mathf.Max(shake, 0.4f); SpawnParticles(gate.Center, new Color(0.75f, 0.68f, 0.5f), 30, 3f); }
                gateOpenedShow = 0;
            }
        }

        /// forgets what the screen was still to show of the gate's releases (no gate, or one rebuilding)
        void ClearGateShow()
        {
            gatePending = 0; gatePendingStampede = false; gateRunN = 0;
            gateShowN = 0; gateShowHeld = false; gateShowStampede = false;
        }

        /// the lane doors of a stage drawn over the gate's shut ones while they are open (0 lanes hides them)
        void ShowGateLanes(Building gate, int stage)
        {
            int lanes = stage >= 2 ? BuildingArt.GateLaneCount(stage) : 0;
            if (gateLaneStage != stage)
            {
                // a new stage: its doors sit elsewhere on the taller house
                foreach (var d in gate.doorSprites) if (d != null) Destroy(d.gameObject);
                gate.doorSprites.Clear();
                for (int j = 0; j < lanes; j++)
                {
                    var go = new GameObject("lane");
                    go.transform.SetParent(gate.go.transform, false);
                    go.transform.localPosition = BuildingArt.GateLaneLocal(stage, j);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = BuildingArt.GateLaneDoor();
                    sr.enabled = false;
                    gate.doorSprites.Add(sr);
                }
                for (int j = 0; j < gateLaneT.Length; j++) gateLaneT[j] = 1f;
                gateLaneStage = stage;
            }
            for (int j = 0; j < gate.doorSprites.Count; j++)
            {
                var sr = gate.doorSprites[j];
                if (sr == null) continue;
                bool open = j < gateLaneT.Length && gateLaneT[j] >= 0 && gateLaneT[j] < 0.3f;
                if (sr.enabled != open) sr.enabled = open;
                if (open) sr.sortingOrder = gate.sr.sortingOrder + 1;
            }
        }

        /// Every second (frame code): the gate stages' introduction once the flock is big enough for the Double Gate. Like the herd's,
        /// it waits until SPACE has been up for a moment (SayWhenQuiet).
        /// Each intro marks itself said when it opens (or is dropped), so one still waiting when the game is saved is queued again
        /// after a load: the chambers' at the Double Gate, and the pass dialogue after the first drove too big for a machine.
        void M12IntroCheck(float rdt)
        {
            gateIntroT -= rdt;
            if (gateIntroT > 0) return;
            gateIntroT = 1f;
            if (!tutDone || !started) return;
            var ga = GateAny;
            if (!flags.Contains("gate_stage_intro") && !IsWaiting("gate_stage_intro") && ga != null && ga.level == 1 && !ga.upgrading && Population >= ScaleMath.GateStageFlock[1])
                SayWhenQuiet("gate_stage_intro", GateStageIntro, StageIntroWanted);
            if (!flags.Contains("chamber_intro") && !IsWaiting("chambers") && GateStage >= 2) SayWhenQuiet("chambers", ChamberIntro);
            if (flags.Contains("pass_intro") && !flags.Contains("pass_said") && !IsWaiting("passes")) SayWhenQuiet("passes", () => PassIntro(null, 0, -1, 0));
        }

        /// the stage intro still makes sense: a stage-1 gate that is not rebuilding (once it is not, it is dropped, and marked said)
        bool StageIntroWanted()
        {
            var ga = GateAny;
            if (ga != null && ga.level == 1 && !ga.upgrading) return true;
            flags.Add("gate_stage_intro");
            return false;
        }

        void GateStageIntro()
        {
            if (flags.Contains("gate_stage_intro")) return;
            flags.Add("gate_stage_intro"); // said, or dropped for good just below
            var gate = GateAny;
            if (gate == null || gate.level != 1 || gate.upgrading) return; // it grew while this waited: the stage pops say the rest
            FocusOn(gate.Center);
            LeaderSay(new[]
            {
                new LeaderPage("FIVE HUNDRED sheep, Hoof, and still ONE little door. The neighbours are laughing. I can hear them through the fence.", LExpr.Smug),
                new LeaderPage("Click the ^ySHEEP GATE^w and UPGRADE it: a ^yDOUBLE GATE^w has two lanes, each with its own door. Every press lets a litter out of EVERY lane.", LExpr.Stern),
                new LeaderPage("And the flock keeps paying: a ^yFLOCK PORTAL^w at 5,000, a ^ySHEEP LAUNCHER^w at 50,000, and at 300,000... the ^ySINGULARITY GATE^w. Do not look directly into it.", LExpr.Shout),
            });
        }

        /// After the tutorial, and once for saves that finished it before the gate existed (flag "gate_intro"): the Supreme Leader
        /// puts a Sheep Gate on a pen and explains SPACE.
        void GateIntro()
        {
            if (flags.Contains("gate_intro")) return;
            flags.Add("gate_intro");
            bool have = HasBuilding(BKind.SheepGate), placed = false;
            if (!have)
                foreach (var p in buildings.ToArray())
                {
                    int gx, gy;
                    if (p.def.kind == BKind.Pen && FindGateSpot(p, out gx, out gy)) { PlaceBuilding(Buildings.Get(BKind.SheepGate), gx, gy, true); placed = true; break; }
                }
            var gate = GateBuilding;
            if (gate != null) FocusOn(gate.Center);
            LeaderSay(new[]
            {
                new LeaderPage(have || placed ? "One more thing, Hoof. I had a ^ySHEEP GATE^w built onto the pen. Every great nation needs a front door."
                    : "One more thing, Hoof. Every great nation needs a front door: a ^ySHEEP GATE^w.", LExpr.Smug),
                new LeaderPage("HOLD ^ySPACE^w and lambs pop out of it, straight into the pen. Free lambs! The lights on the gate's bar at the bottom of the screen are its charge. It refills by itself.", LExpr.Stern),
                have || placed
                    ? new LeaderPage("Lambs grow into sheep, sheep grow wool, wool grows money. A full pen takes no more: upgrade it or build another. Now. Show me some lambs.", LExpr.Shout)
                    : new LeaderPage("There was no room for it by the pens. Build a SHEEP GATE against a pen's fence yourself. It is free. I am generous like that.", LExpr.Shout),
            });
        }

        /// the HUD: the gate's charge and a button to hold, at the bottom of the screen, while a gate stands. From the Double Gate on
        /// it names the stage, shows a pip per lane and counts down to the next STAMPEDE while SPACE is held.
        int gateHudFrame = -1; float gateHudW; string gateHudTitle, gateHudPress, gateHudUp; // worked out once a frame (OnGUI runs several events)

        void DrawGateHud()
        {
            var gate = GateAny;
            if (gate == null || !started || placing != null || ToolActive) return;
            int stage = gate.level;
            if (gateHudFrame != Time.frameCount)
            {
                gateHudFrame = Time.frameCount;
                gateHudTitle = stage <= 1 ? "SHEEP GATE" : gate.Title.ToUpper();
                gateHudPress = stage >= 2 ? "^kx" + LambsText(LambsPerPress) + " A PRESS" : null;
                string upTo = gate.upgrading ? "UPGRADING TO " + BuildingDef.At(gate.def.levelNames, stage + 1).ToUpper() + " " : null;
                gateHudUp = upTo != null ? upTo + Mathf.FloorToInt(gate.construct * 100) + "%" : null;
                float hw = 170;
                if (upTo != null) hw = Mathf.Max(hw, 17 + PixelFont.Width(upTo + "100%") + 6); // the upgrade replaces the title; as wide at any percentage
                else if (stage >= 2) hw = Mathf.Max(hw, 17 + PixelFont.Width(gateHudTitle) + 4 + 4 * stage + 8 + Mathf.Max(PixelFont.Width("STAMPEDE IN 6"), PixelFont.Width(gateHudPress)) + 6);
                gateHudW = hw;
            }
            float w = gateHudW, h = 30;
            float x = Mathf.Round((FreeLeft + FreeRight) / 2f - w / 2f), y = H - h - 8;
            if (FreeRight - FreeLeft < w + 8) return;
            var r = new Rect(x, y, w, h);
            var e = Event.current;
            bool over = r.Contains(e.mousePosition) && UI.inputEnabled && !gate.upgrading;
            if (over && e.type == EventType.MouseDown && e.button == 0) { gateMouseHeld = true; e.Use(); }
            if (!UI.Repaint) return; // the rest only draws and sets the tooltip, which only a Repaint shows: the pen and the flock are looked up once a frame
            UI.Card(r, gateHeld, false, over);
            UI.Block(r);
            UI.Icon(x + 5, y + 5, "gate");
            if (gateHudUp != null)
            {
                // rebuilding: what it becomes in place of the title, a gold bar in place of the charge, and no input
                PixelFont.Draw(x + 17, y + 5, gateHudUp, UI.Gold);
                var br = new Rect(x + 6, y + 18, w - 12, 7);
                UI.Fill(br, new Color(0.18f, 0.14f, 0.11f));
                UI.Fill(new Rect(br.x, br.y, Mathf.Max(1, Mathf.Round(br.width * Mathf.Clamp01(gate.construct))), br.height), UI.GoldLo);
                return;
            }
            PixelFont.Draw(x + 17, y + 5, gateHudTitle, UI.Gold);
            if (stage >= 2) for (int j = 0; j < stage; j++) UI.Fill(new Rect(x + 17 + PixelFont.Width(gateHudTitle) + 4 + j * 4, y + 6, 3, 5), new Color(0.69f, 0.25f, 0.17f));
            var pen = GatePenAt(gate.x, gate.y);
            bool full = pen != null && (Members(pen) >= RoomCapacity(pen) || Population >= FlockCap);
            int toStampede = ScaleMath.StampedeEvery - gateStreak % ScaleMath.StampedeEvery;
            string hint = pen == null ? "^rNO PEN" : full ? "^rPEN FULL" : stage <= 1 ? "^kHOLD SPACE" : gateHeld ? "^pSTAMPEDE IN " + toStampede : gateHudPress;
            PixelFont.Draw(x + w - 5 - PixelFont.Width(hint), y + 5, hint, UI.Dim);
            // the charge: one light per release (a light holds several past 12), the next one filling up. While SPACE is held, the light
            // whose charge the stampede will use glows pink (charges go from the top: the k-th release from now uses charge floor - k).
            float cap = GateCap;
            int pips = Mathf.Min(12, Mathf.FloorToInt(cap));
            float px = x + 6, pw = (w - 12) / Mathf.Max(1, pips);
            int pink = -1;
            if (stage >= 2 && gateHeld)
            {
                int k = Mathf.FloorToInt(gateCharge) - toStampede;
                if (k >= 0) pink = Mathf.Min(pips - 1, Mathf.FloorToInt(k * pips / cap));
            }
            for (int i = 0; i < pips; i++)
            {
                float f = Mathf.Clamp01(gateCharge * pips / cap - i);
                var pr = new Rect(Mathf.Round(px + i * pw) + 1, y + 18, Mathf.Max(2, Mathf.Round(pw) - 2), 7);
                UI.Fill(pr, new Color(0.18f, 0.14f, 0.11f));
                if (f > 0) UI.Fill(new Rect(pr.x, pr.y, Mathf.Max(1, Mathf.Round(pr.width * f)), pr.height), i == pink ? new Color(1f, 0.55f, 0.75f) : f >= 1 ? UI.Gold : UI.GoldLo);
            }
            if (UI.Hover(r)) // the long tooltip is only built while the mouse is on the HUD
                UI.Tip(r, gateHudTitle + "\nHold SPACE (or hold this button) and lambs pop out into the pen.\nCharge " + Mathf.FloorToInt(gateCharge) + "/" + Mathf.FloorToInt(cap)
                    + ", one more every " + GateRefillTime.ToString("0.0") + " s. "
                    + (stage >= 2 ? stage + " lanes x " + LambsText(LambsPerRelease * ScaleMath.LaneLitter[stage - 1]) + " lambs = " + LambsText(LambsPerPress) + " a release.\nEvery 6th release in a row while you hold SPACE: STAMPEDE x" + UI.Fmt(StampedeMult) + "."
                       : LambsText(LambsPerRelease) + " lamb" + (LambsPerRelease == 1 ? "" : "s") + " a release.")
                    + "\nLambs let out: " + UI.Fmt(gateReleased));
        }

        /// a lamb count as the HUD and the inspector show it: Bigger Litters adds tenths, so one decimal below 100
        public static string LambsText(double v) { return v < 10000 && v != System.Math.Floor(v) ? v.ToString("0.#") : UI.Fmt(v); }
    }
}
