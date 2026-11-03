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

        /// the working Sheep Gate (built), or null
        public Building GateBuilding { get { foreach (var b in buildings) if (b.def.kind == BKind.SheepGate && b.Built) return b; return null; } }
        public float GateCap { get { return GateCapBase + (float)PathAdd("gate_cap"); } }
        public float GateRefillTime { get { return GateRefillBase / (float)PathMult("gate_refill"); } }
        public float GateReleaseInterval { get { return GateReleaseBase / (float)PathMult("gate_speed"); } }
        public double LambsPerRelease { get { return 1 + PathAdd("gate_lambs"); } }

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
            var gate = GateBuilding;
            if (gate == null) return;
            gate.status = GatePenAt(gate.x, gate.y) == null ? "No pen: it must stand against a Breeding Pen's fence" : "Hold SPACE: lambs into the pen";
            float cap = GateCap;
            if (gateCharge < cap) gateCharge = Mathf.Min(cap, gateCharge + dt / GateRefillTime);
            if (!gateHeld)
            {
                gateCd = 0; // so the next press releases at once
                double perMin = PathAdd("gate_auto");
                if (perMin > 0 && gateCharge >= 1f)
                {
                    gateAutoT += dt * (float)perMin / 60f;
                    if (gateAutoT >= 1f) { gateAutoT -= 1f; ReleaseLambs(gate, true); }
                }
                return;
            }
            gateCd -= dt;
            if (gateCd > 0) return;
            gateCd = Mathf.Max(0, gateCd + GateReleaseInterval); // at most one release a step
            ReleaseLambs(gate);
        }

        /// one release: a lamb (more with the "lambs per release" upgrade) for one charge, if the pen has room
        void ReleaseLambs(Building gate, bool quiet = false)
        {
            var pen = GatePenAt(gate.x, gate.y);
            if (pen == null)
            {
                if (quiet) return;
                if (gateFullT <= 0) { gateFullT = 1.2f; FloatText(gate.Center + Vector2.up * 1.6f, "NO PEN", new Color(1f, 0.4f, 0.35f)); }
                return;
            }
            if (gateCharge < 1f)
            {
                if (quiet) return;
                if (gateEmptyT <= 0) { gateEmptyT = 1f; FloatText(gate.Center + Vector2.up * 1.6f, "REFILLING", new Color(0.7f, 0.7f, 0.7f)); }
                return;
            }
            double room = System.Math.Floor(System.Math.Min(RoomCapacity(pen) - Members(pen), FlockCap - Population));
            if (room <= 0) { if (!quiet) PenFull(gate); return; }
            lambDebt += LambsPerRelease;
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
            SpawnParticles(at, new Color(0.6f, 0.5f, 0.38f), 8, 1.6f);
            FloatText(at + Vector2.up * 1.1f, "+" + UI.Fmt(n), new Color(1f, 0.92f, 0.7f));
            Sfx.Play("pop", 0.6f, 0.9f + (float)fxRng.NextDouble() * 0.35f);
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

        /// every frame: the gate's door, and the feedback timers (real time)
        void UpdateGateVisual(float rdt)
        {
            gateDoorT -= rdt; gateFullT -= rdt; gateEmptyT -= rdt;
            HerdIntroCheck(rdt); // the herd's introduction waits until SPACE has been up for a moment (Core/Herd.cs)
            var gate = GateBuilding;
            if (gate == null || gate.sr == null) return;
            var want = BuildingArt.GateSprite(gateDoorT > 0); // cached sprites: also undoes a refresh that shut the door mid-release
            if (gate.sr.sprite != want) gate.sr.sprite = want;
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

        /// the HUD: the gate's charge and a button to hold, at the bottom of the screen, while a gate stands
        void DrawGateHud()
        {
            var gate = GateBuilding;
            if (gate == null || !started || placing != null || ToolActive) return;
            float w = 170, h = 30;
            float x = Mathf.Round((FreeLeft + FreeRight) / 2f - w / 2f), y = H - h - 8;
            if (FreeRight - FreeLeft < w + 8) return;
            var r = new Rect(x, y, w, h);
            var e = Event.current;
            bool over = r.Contains(e.mousePosition) && UI.inputEnabled;
            if (over && e.type == EventType.MouseDown && e.button == 0) { gateMouseHeld = true; e.Use(); }
            if (!UI.Repaint) return; // the rest only draws and sets the tooltip, which only a Repaint shows: the pen and the flock are looked up once a frame
            UI.Card(r, gateHeld, false, over);
            UI.Block(r);
            UI.Icon(x + 5, y + 5, "gate");
            PixelFont.Draw(x + 17, y + 5, "SHEEP GATE", UI.Gold);
            var pen = GatePenAt(gate.x, gate.y);
            bool full = pen != null && (Members(pen) >= RoomCapacity(pen) || Population >= FlockCap);
            string hint = pen == null ? "^rNO PEN" : full ? "^rPEN FULL" : "^kHOLD SPACE";
            PixelFont.Draw(x + w - 5 - PixelFont.Width(hint), y + 5, hint, UI.Dim);
            // the charge: one light per release, the next one filling up
            float cap = GateCap;
            int pips = Mathf.Min(12, Mathf.FloorToInt(cap));
            float px = x + 6, pw = (w - 12) / Mathf.Max(1, pips);
            for (int i = 0; i < pips; i++)
            {
                float f = Mathf.Clamp01(gateCharge * pips / cap - i);
                var pr = new Rect(Mathf.Round(px + i * pw) + 1, y + 18, Mathf.Max(2, Mathf.Round(pw) - 2), 7);
                UI.Fill(pr, new Color(0.18f, 0.14f, 0.11f));
                if (f > 0) UI.Fill(new Rect(pr.x, pr.y, Mathf.Max(1, Mathf.Round(pr.width * f)), pr.height), f >= 1 ? UI.Gold : UI.GoldLo);
            }
            if (UI.Hover(r)) // the long tooltip is only built while the mouse is on the HUD
                UI.Tip(r, "SHEEP GATE\nHold SPACE (or hold this button) and lambs pop out into the pen.\nCharge " + Mathf.FloorToInt(gateCharge) + "/" + Mathf.FloorToInt(cap)
                    + ", one more every " + GateRefillTime.ToString("0.0") + " s. " + UI.Fmt(LambsPerRelease) + " lamb" + (LambsPerRelease >= 2 ? "s" : "") + " a release.\nLambs let out: " + UI.Fmt(gateReleased));
        }
    }
}
