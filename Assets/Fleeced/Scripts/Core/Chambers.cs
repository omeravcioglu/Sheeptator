using UnityEngine;

namespace Fleeced
{
    /// Breeding Chambers (M12): up to 8 little red huts in the corners of every Breeding Pen, bought in UPGRADES. Each takes couples
    /// of the pen's herd, up to the Bigger Chambers capacity, and runs its own 6 s timer; the timers are staggered (bit reversal),
    /// so 8 chambers pop a litter every 0.75 s. Inside a hut a couple breeds at a named couple's pace, linear in couples, while
    /// the rest of the herd keeps M11's crowded pace (pairs^0.6): chambers are bought breeding capacity. A litter needs what all
    /// breeding needs: a fed, content herd and room in the pen and on the farm.
    /// The timers come from the saved game clock (simClock), so only the litter's carried fraction (Flock.litterT) is saved.
    /// The simulation writes counters (chamberPops, litterShow); the huts, runners and bursts are the visual pass's.
    public partial class Game
    {
        /// huts in every Breeding Pen (Breeding Chambers, at most 8)
        public int ChamberCount { get { return Mathf.Min(ScaleMath.MaxChambers, (int)PathAdd("pen_chambers")); } }
        /// couples a hut holds (10, more with Bigger Chambers)
        public double ChamberPairs { get { return 10 + PathAdd("chamber_pairs"); } }
        /// couples of a herd the chambers take (none without chambers)
        double ChambersHold(double couples) { int c = ChamberCount; return c <= 0 ? 0 : System.Math.Min(couples, c * ChamberPairs); }
        /// lambs per herd birth (Twin Season and Triplet Season), by expected value: exactly 1 without them
        public double BirthMult() { return ScaleMath.BirthMult(PathAdd("pen_twins"), PathAdd("pen_triplets")); }

        /// what one couple's litter is multiplied by: everything a pen's breeding speed has, and the Seasons
        double ChamberMult(Building b, float mood)
        {
            return (tech.Contains("matchmaking") ? 1.5 : 1) * PenBreedMult(b) * (HasGene("love_songs") ? 1.3 : 1) * Mathf.Clamp(mood / 65f, 0.4f, 1.3f)
                   * PathMult("pen_breed") * (HasGene("twin_genes") ? 1.2 : 1) * BirthMult();
        }

        /// every step, for a Breeding Pen's herd (SimHerd, after HerdBreed): the chambers whose timers come round this step
        void HerdChambers(Building b, Flock h, float dt, float mood)
        {
            int C = ChamberCount;
            if (C <= 0) return; // before any work: a farm without chambers breeds exactly as M11's
            double couples = System.Math.Min(System.Math.Floor(h.Ewes), System.Math.Floor(h.Rams));
            double inCh = ChambersHold(couples);
            b.chamberWhy = couples < 1 ? "no couples in the herd" : h.hunger > 1.2f ? "the herd is HUNGRY" : mood < 30 ? "the herd is UNHAPPY" : null;
            if (b.chamberWhy == null && System.Math.Min(RoomCapacity(b) - Members(b), FlockCap - herdPop) < 1) b.chamberWhy = "the pen is FULL"; // between firings too
            if (h.hunger > 1.2f && couples >= 1) b.chamberIdleT += dt; else b.chamberIdleT = 0;
            double perCouple = ScaleMath.CoupleRate * ScaleMath.ChamberCycle / DayLength, mult = -1;
            for (int k = 0; k < C; k++)
            {
                if (!ScaleMath.ChamberFires(simClock, dt, k)) continue;
#if FLEECED_DEV
                DevHooks.ChamberFires++;
#endif
                double share = ScaleMath.ChamberShare(inCh, C, k);
                b.chamberLast[k] = (float)share;
                if (b.chamberWhy != null || share < 1) continue;
                double room = System.Math.Min(RoomCapacity(b) - Members(b), FlockCap - herdPop);
                if (room < 1) { b.chamberWhy = "the pen is FULL"; continue; }
                if (mult < 0) mult = ChamberMult(b, mood);
                h.litterT += share * perCouple * mult;
                double born = System.Math.Min(System.Math.Floor(h.litterT), System.Math.Floor(room));
                h.litterT = System.Math.Min(h.litterT - born, 1);
                if (born < 1) continue;
                HerdBirths(b, h, born, false); // its own burst shows it (litterShow), not the herd's
                b.litterShow += born;
                b.chamberBorn[k] = (float)System.Math.Min(born, 1e30);
                b.chamberPops |= 1 << k;
            }
        }

        /// "The herd is breeding (1.2K couples, 840 in chambers)" for the pen's status, written again only when its numbers change
        string HerdBreedingStatus(Building b, double couples)
        {
            double ch = ChamberCount > 0 ? ChambersHold(couples) : -1;
            if (b.herdStatus == null || couples != b.herdStatusN || ch != b.herdStatusCh)
            {
                b.herdStatusN = couples; b.herdStatusCh = ch;
                b.herdStatus = "The herd is breeding (" + UI.Fmt(couples) + " couples" + (ch >= 0 ? ", " + UI.Fmt(ch) + " in chambers)" : ")");
            }
            return b.herdStatus;
        }

        // ---------------- on screen ----------------
        /// a pen whose huts are up shows its litters and its herd's births as one number (UpdateChamberHuts)
        bool HutsShowBursts(Building b) { return IsPen(b) && b.go != null && RoomActive(b) && ChamberCount > 0; }

        /// a hut's bottom-left corner in tiles from the pen's, in the order they are bought (clear of the doors at x + 4 and y + 2)
        static readonly Vector2[] HutSlots = { new Vector2(0.45f, 0.3f), new Vector2(6.55f, 0.3f), new Vector2(1.5f, 0.3f), new Vector2(5.5f, 0.3f),
                                               new Vector2(0.45f, 3.55f), new Vector2(6.55f, 3.55f), new Vector2(1.5f, 3.55f), new Vector2(5.5f, 3.55f) };

        /// every frame for a Breeding Pen (UpdatePenVisual): its huts, the litters hopping out, and their burst every half second
        void UpdateChamberHuts(Building b)
        {
            if (!IsPen(b) || b.go == null) return;
            int C = RoomActive(b) ? ChamberCount : 0;
            while (b.huts.Count < C)
            {
                var go = new GameObject("hut");
                go.transform.SetParent(b.go.transform, false);
                var slot = HutSlots[b.huts.Count];
                go.transform.localPosition = new Vector3(slot.x, slot.y, 0);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = SortY(b.y + slot.y) + 1;
                b.huts.Add(sr);
            }
            float rdt = Time.unscaledDeltaTime;
            int pops = b.chamberPops; b.chamberPops = 0;
            bool vis = IsVisible(b.Center);
            for (int k = 0; k < b.huts.Count; k++)
            {
                var sr = b.huts[k];
                if (sr == null) continue;
                bool on = k < C;
                if (sr.gameObject.activeSelf != on) sr.gameObject.SetActive(on);
                if (!on) continue;
                if ((pops & (1 << k)) != 0)
                {
                    b.hutOpenT[k] = 0.36f;
                    if (vis)
                    {
                        var r = PenInterior(b);
                        Vector2 from = new Vector2(b.x, b.y) + HutSlots[k] + new Vector2(0.5f, 0.2f);
                        // a runner is drawn up to a tile to one side of its point (its pivot is a corner): it lands clear of the fence
                        Vector2 to = new Vector2(r.xMin + 0.95f + (float)fxRng.NextDouble() * (r.width - 1.9f), r.yMin + 0.3f + (float)fxRng.NextDouble() * (r.height - 0.6f));
                        Run(from, to, b.chamberBorn[k], b.herd != null ? b.herd.MainStrain() : 0, 0.4f, 0.5f, 0, false, sr.sortingOrder + 1);
                    }
                }
                int frame;
                if (b.hutOpenT[k] > 0) { b.hutOpenT[k] -= rdt; frame = 4; }
                else if (b.chamberWhy != null || b.chamberLast[k] < 1 || b.herd == null || b.herd.Total < 1) frame = 0;
                else frame = 1 + Mathf.Min(2, Mathf.FloorToInt((float)ScaleMath.ChamberPhase(simClock, k) * 3f));
                var spr = BuildingArt.ChamberHut(frame);
                if (sr.sprite != spr) sr.sprite = spr;
            }
            // one burst for the litters of the last half second
            // One number for the pen (UpdateHerdVisual leaves a pen with huts to this): its litters with the herd's own births every
            // half second, or the herd's births alone on the herd's 1.2 s beat while no litter comes.
            b.litterShowT -= rdt;
            if (b.litterShow >= 1 && b.litterShowT <= 0)
            {
                ShowBurst(b.Center + Vector2.up * 1.4f, b.litterShow + b.herdShow, BurstKind.Litter);
                b.litterShow = 0; b.herdShow = 0; b.litterShowT = 0.5f; b.herdShowT = 1.2f;
            }
            else if (b.litterShow < 1 && b.herdShow >= 1 && b.herdShowT <= 0)
            {
                ShowBurst(b.Center + Vector2.up * 1.4f, b.herdShow, BurstKind.Births);
                b.herdShow = 0; b.herdShowT = 1.2f;
            }
        }

        /// the pen inspector's chambers block (once Breeding Chambers is unlocked)
        float DrawChamberBlock(Building b, float y, float aw)
        {
            var d = Upgrades.Get("pen_chambers");
            if (d == null || !PathUnlocked(d)) return y;
            int C = ChamberCount;
            if (C <= 0) { PixelFont.Draw(0, y, "^kNo chambers yet: UPGRADES (U)", UI.Dim); return y + 11; }
            var h = b.herd;
            double couples = h != null ? System.Math.Min(System.Math.Floor(h.Ewes), System.Math.Floor(h.Rams)) : 0, inCh = ChambersHold(couples);
            PixelFont.Draw(0, y, "^yBREEDING CHAMBERS ^w" + C + "^k/" + ScaleMath.MaxChambers, UI.Text); y += 11;
            PixelFont.Draw(0, y, "^k" + UI.Fmt(ChamberPairs) + " couples in each (" + UI.Fmt(inCh) + " of " + UI.Fmt(couples) + ")", UI.Text); y += 11;
            string why = h == null || couples < 1 ? "no couples in the herd" : b.chamberWhy;
            if (why != null) PixelFont.Draw(0, y, "^oIdle: " + why, UI.Text);
            else
            {
                double litter = inCh / C * ScaleMath.CoupleRate * ScaleMath.ChamberCycle / DayLength * ChamberMult(b, HerdMood(b, h));
                string ls = litter < 0.1 ? "<0.1" : litter < 10 ? litter.ToString("0.#") : UI.Fmt(System.Math.Round(litter)); // a small herd's litter is a fraction, carried
                PixelFont.Draw(0, y, "^kA litter of ^p~" + ls + "^k every " + (ScaleMath.ChamberCycle / C).ToString("0.0#") + " s", UI.Text);
            }
            y += 11;
            float bw = Mathf.Floor(aw / ScaleMath.MaxChambers) - 1;
            for (int k = 0; k < ScaleMath.MaxChambers; k++)
            {
                var r = new Rect(k * (bw + 1), y, bw, 5);
                UI.Fill(r, k < C ? new Color(0.3f, 0.26f, 0.24f) : new Color(0.12f, 0.1f, 0.09f));
                if (k < C && why == null && b.chamberLast[k] >= 1)
                    UI.Fill(new Rect(r.x, r.y, Mathf.Max(1, Mathf.Round(r.width * (float)ScaleMath.ChamberPhase(simClock, k))), r.height), new Color(1f, 0.55f, 0.75f));
            }
            return y + 8;
        }

        /// the chambers' introduction (at the Double Gate, through SayWhenQuiet)
        void ChamberIntro()
        {
            if (flags.Contains("chamber_intro")) return;
            flags.Add("chamber_intro"); // said: a save before it opened would have queued it again (M12IntroCheck)
            foreach (var b in buildings) if (IsPen(b) && b.Built) { FocusOn(b.Center); break; }
            LeaderSay(new[]
            {
                new LeaderPage("Hoof. A big herd breeds like a queue at the post office: slowly, and with a lot of staring. The bigger the herd, the fewer lambs a couple.", LExpr.Stern),
                new LeaderPage("Couples need PRIVACY. Buy ^yBREEDING CHAMBERS^w in UPGRADES (U): huts in the corners of every pen. Couples go in, and on each hut's timer a LITTER pops out.", LExpr.Smug),
                new LeaderPage("Eight huts a pen. Eight timers. One rhythm: POP. POP. POP. Then ^yBIGGER CHAMBERS^w: more couples in every hut.", LExpr.Shout),
            });
        }

        /// the Supreme Leader on M12's systems (from CheckAdvice): true only when he really speaks, so a silent check never stops
        /// the advice after it
        bool M12Advice()
        {
            if (popT > 0.5f || LeaderModal) return false;
            var gate = GateBuilding;
            if (gate != null && gate.level < gate.def.maxLevel && AdviceReady("stage_ready") && UpgradeBlocker(gate) == null)
            {
                Advise("stage_ready", "The flock is big enough for a " + gate.def.levelNames[gate.level].ToUpper() + ". Click the SHEEP GATE. UPGRADE. I will hold the ribbon.", LExpr.Smug, 3f);
                return true;
            }
            if (AdviceReady("chambers_hungry"))
                foreach (var b in buildings)
                    if (IsPen(b) && b.chamberIdleT > 10f)
                    {
                        b.chamberIdleT = 0;
                        Advise("chambers_hungry", "My BREEDING CHAMBERS in the BREEDING PEN are EMPTY: the herd is too HUNGRY to breed. Send it through a FEEDING LOT.", LExpr.Shout, 2f);
                        return true;
                    }
            if (AdviceReady("machine_choke"))
                foreach (var b in buildings)
                    if (IsSheepMachine(b) && b.inMachine != null && b.machinePasses >= 10f)
                    {
                        Advise("machine_choke", "My " + b.def.name.ToUpper() + " chews every drove in " + Mathf.RoundToInt(b.machinePasses) + " PASSES. The line is waiting. A bigger machine, BIGGER HOPPERS (U), or a second OUT door.", LExpr.Stern, 2f);
                        return true;
                    }
            return false;
        }
    }
}
