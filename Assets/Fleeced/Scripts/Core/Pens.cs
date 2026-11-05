using System.Collections.Generic;
using UnityEngine;

namespace Fleeced
{
    public class Blueprint { public BKind kind; public int x, y; }

    /// Rooms (Breeding Pens and Feeding Lots) with a door on every side, and the conveyor machines.
    /// A sheep leaves a room through an OPEN door that has a conveyor under it, rides the belt to wherever it leads
    /// (another room, a machine, the Slaughterhouse) and a belt pointing into a room drops it inside.
    public partial class Game
    {
        const float GateInterval = 0.55f;
        bool lineShears, lineMilks; // is there any machine that can use fluffy / milky sheep?
        public int lotMeals, slaughtered, gateToggles, machineSheep;

        public static readonly string[] DoorNames = { "BOTTOM", "RIGHT", "TOP", "LEFT" };
        // "Heading for the " + DoorNames[i].ToLower() + " door" and the queueing text, built once (Boot makes the culture invariant)
        static readonly string[] HeadingForDoor = { "Heading for the bottom door", "Heading for the right door", "Heading for the top door", "Heading for the left door" };
        static readonly string[] QueueingAtDoor = { "Queueing at the bottom door", "Queueing at the right door", "Queueing at the top door", "Queueing at the left door" };
        public static readonly string[] FilterNames = { "ALL", "HUNGRY", "FED", "READY", "FLUFFY", "MILKY", "RAMS", "EWES", "PLAIN", "SPLICED" };
        public static readonly string[] FilterTips =
        {
            "Every sheep may use this door.",
            "Only HUNGRY sheep use this door.",
            "Only FED sheep use this door.",
            "Only sheep a machine wants right now (fluffy, or a ewe with a full udder).",
            "Only FLUFFY sheep use this door.",
            "Only ewes with a FULL UDDER use this door.",
            "Only RAMS use this door.",
            "Only EWES use this door.",
            "Only PLAIN sheep (no DNA strain) use this door. Send them to the Gene Lab.",
            "Only DNA-altered sheep (any strain) use this door.",
        };

        // Sheep status texts that name a building, built once per text and kind instead of per sheep per step. The text is
        // always KindPre[msg] + def.name + KindPost[msg]: a slot is rebuilt whenever the def's name is not the one it was built from.
        const int KindLeaving = 0, KindGoingBack = 1, KindBroken = 2, KindBeingBuilt = 3, KindDoorShut = 4, KindQueueFor = 5, KindNoSheep = 6, KindFull = 7, KindSlots = 64;
        static readonly string[] KindPre = { "Leaving the ", "Going back to the ", "Waiting: the ", "Waiting: the ", "Waiting: the ", "Queueing for the ", "Waiting: the ", "Waiting: the " };
        static readonly string[] KindPost = { " for work", "", " is broken", " is being built", "'s door is SHUT", "", " does not take sheep", " is FULL" };
        static readonly string[] kindStatus = new string[8 * KindSlots], kindStatusName = new string[8 * KindSlots];
        static string KindText(int msg, BuildingDef d)
        {
            int k = (int)d.kind;
            if (k < 0 || k >= KindSlots) return KindPre[msg] + d.name + KindPost[msg];
            int i = msg * KindSlots + k;
            string t = kindStatus[i], name = d.name;
            if (t == null || !ReferenceEquals(kindStatusName[i], name)) { t = KindPre[msg] + name + KindPost[msg]; kindStatus[i] = t; kindStatusName[i] = name; }
            return t;
        }

        // ---------------- room geometry ----------------
        public static bool IsPen(Building b) { return b != null && b.def.kind == BKind.Pen; }
        public static bool IsLot(Building b) { return b != null && b.def.kind == BKind.FeedLot; }
        public static bool IsRoom(Building b) { return b != null && IsRoomKind(b.def.kind); }
        public static bool IsRoomKind(BKind k) { return k == BKind.Pen || k == BKind.FeedLot; }
        public static bool RoomActive(Building b) { return b.construct >= 1f || b.upgrading; }
        public static int RoomCapacity(Building b) { return RoomCapAt(IsLot(b), b.level); }
        // per-level tables, allocated once: RoomCapAt and LotEatRate run for room sheep every step
        static readonly int[] LotCapBase = { 8, 14, 22, 32, 48 }, PenCapBase = { 16, 24, 34, 46, 60 };
        static readonly float[] LotEatBase = { 1f, 1.4f, 1.9f, 2.6f, 3.5f };
        public static int RoomCapAt(bool lot, int level)
        {
            double c;
            if (lot) c = BuildingDef.At(LotCapBase, level) + (I != null ? (int)I.PathAdd("lot_space") : 0);
            else
            {
                c = BuildingDef.At(PenCapBase, level);
                if (I != null && I.tech.Contains("bunk_beds")) c += 4;
                if (I != null && I.tech.Contains("apartments")) c += 8;
                if (I != null) c += (int)I.PathAdd("pen_space");
            }
            if (I != null) c *= I.PathMult("herd_space"); // Sheep Stacking (x1 until upgraded): room for a herd
            return (int)System.Math.Min(System.Math.Floor(c), 1e9);
        }
        /// breeding speed of a Breeding Pen by level: upgrades make lambs faster
        public static readonly float[] PenBreed = { 1f, 1.3f, 1.65f, 2.1f, 2.7f };
        public static float PenBreedMult(Building b) { return BuildingDef.At(PenBreed, b.level); }
        public static float LotEatRate(Building b) { return 1.6f * BuildingDef.At(LotEatBase, b.level) * (I != null ? (float)I.PathMult("lot_speed") : 1f); }
        public static Rect PenInterior(Building p) { return new Rect(p.x + 0.35f, p.y + 0.35f, p.def.w - 0.7f, p.def.h - 0.8f); }

        /// the tile just outside door i: 0 bottom, 1 right, 2 top, 3 left
        public static Vector2Int DoorTile(Building p, int i)
        {
            int w = p.def.w, h = p.def.h;
            switch (i)
            {
                case 0: return new Vector2Int(p.x + w / 2, p.y - 1);
                case 1: return new Vector2Int(p.x + w, p.y + h / 2);
                case 2: return new Vector2Int(p.x + w / 2, p.y + h);
                default: return new Vector2Int(p.x - 1, p.y + h / 2);
            }
        }
        public static Vector2 DoorInner(Building p, int i)
        {
            int w = p.def.w, h = p.def.h;
            switch (i)
            {
                case 0: return new Vector2(p.x + w / 2 + 0.5f, p.y + 0.45f);
                case 1: return new Vector2(p.x + w - 0.45f, p.y + h / 2 + 0.45f);
                case 2: return new Vector2(p.x + w / 2 + 0.5f, p.y + h - 0.55f);
                default: return new Vector2(p.x + 0.45f, p.y + h / 2 + 0.45f);
            }
        }
        public static Vector2 DoorOuter(Building p, int i)
        {
            var t = DoorTile(p, i);
            return i == 0 ? new Vector2(t.x + 0.5f, t.y + 0.5f) : i == 2 ? new Vector2(t.x + 0.5f, t.y + 0.45f) : new Vector2(t.x + 0.5f, t.y + 0.4f);
        }
        static Vector2 Inward(int i) { return i == 0 ? Vector2.up : i == 1 ? Vector2.left : i == 2 ? Vector2.down : Vector2.right; }
        static Vector2 Sideways(int i) { return i == 0 || i == 2 ? new Vector2(0.8f, 0) : new Vector2(0, -0.8f); }

        bool InsideFootprint(Building b, int x, int y) { return x >= b.x && x < b.x + b.def.w && y >= b.y && y < b.y + b.def.h; }

        /// a conveyor starts under this door and leads away from the room
        public bool DoorConnected(Building p, int i)
        {
            var t = DoorTile(p, i);
            int dir = BeltDirAt(t.x, t.y);
            if (dir < 0) return false;
            var d = Dir(dir);
            return !InsideFootprint(p, t.x + d.x, t.y + d.y);
        }
        public bool DoorUsable(Building p, int i) { return p.doorOpen[i] && DoorConnected(p, i); }
        bool AnyDoorUsable(Building p) { for (int i = 0; i < 4; i++) if (DoorUsable(p, i)) return true; return false; }
        public int Filter(Building p, int i) { return tech.Contains("fluff_detector") ? p.doorFilter[i] : 0; }

        /// follows a conveyor from (x, y) to the first building or room it reaches
        string TraceBelt(int x, int y, Building self)
        {
            // the tiles seen on this trace are the ones stamped with traceGen (no set to allocate per call)
            if (traceGen == int.MaxValue) { System.Array.Clear(traceMark, 0, traceMark.Length); traceGen = 0; }
            traceGen++;
            for (int step = 0; step < 400; step++)
            {
                if (!InMap(x, y)) return "the edge of the world";
                var t = tiles[x, y];
                if (step > 0)
                {
                    if (t.building != null) return t.building == self ? "back inside" : t.building.def.name;
                    if (t.pen != null) return t.pen == self ? "back inside" : t.pen.def.name;
                }
                if (t.belt < 0) return step == 0 ? null : "a dead end";
                int k = y * MapW + x;
                if (traceMark[k] == traceGen) return "a loop";
                traceMark[k] = traceGen;
                var d = Dir(t.belt);
                x += d.x; y += d.y;
            }
            return "somewhere far away";
        }
        readonly int[] traceMark = new int[MapW * MapH]; int traceGen;

        /// follows the belt from a door to the first building or room it reaches
        public string DoorDestination(Building p, int i) { var t = DoorTile(p, i); return TraceBelt(t.x, t.y, p); }

        int NearestDoor(Building p, Vector2 pos)
        {
            int best = 0; float bd = 1e9f;
            for (int i = 0; i < 4; i++) { float d = (DoorOuter(p, i) - pos).sqrMagnitude; if (d < bd) { bd = d; best = i; } }
            return best;
        }

        /// the door the G key and the tutorial talk about: an open one first, else the first one with a conveyor
        public int MainDoor(Building p)
        {
            for (int i = 0; i < 4; i++) if (DoorUsable(p, i)) return i;
            for (int i = 0; i < 4; i++) if (DoorConnected(p, i)) return i;
            return 0;
        }

        /// a sheep the machines can do something with right now
        public bool Ready(Sheep s) { return !s.Lamb && ((lineShears && s.wool >= 0.98f) || (lineMilks && !s.male && s.milk >= 0.98f)); }

        public bool FilterAllows(int f, Sheep s)
        {
            switch (f)
            {
                case 1: return s.hunger >= 0.7f;
                case 2: return s.hunger < 0.3f;
                case 3: return Ready(s);
                case 4: return s.wool >= 0.98f;
                case 5: return !s.male && s.milk >= 0.98f;
                case 6: return s.male;
                case 7: return !s.male;
                case 8: return s.strain == 0;
                case 9: return s.strain != 0;
            }
            return true;
        }

        public Building FirstPen()
        {
            foreach (var b in buildings) if (IsPen(b) && RoomActive(b)) return b;
            return null;
        }
        public Building FirstLot()
        {
            foreach (var b in buildings) if (IsLot(b) && RoomActive(b)) return b;
            return null;
        }

        /// the pen a homeless grazer should live in (nearest one that is not full)
        public Building PenFor(Vector2 pos)
        {
            Building best = null; float bd = 1e9f;
            foreach (var b in buildings)
            {
                if (!IsPen(b) || !RoomActive(b)) continue;
                float d = (b.Center - pos).sqrMagnitude + (b.penMembers >= RoomCapacity(b) ? 5000 : 0);
                if (d < bd) { bd = d; best = b; }
            }
            return best;
        }

        public void AssignPen(Sheep s)
        {
            if (s.dead || s.job != Job.Grazer) return;
            if (s.pen != null && buildings.Contains(s.pen)) return;
            s.pen = PenFor(s.pos);
            if (s.pen != null) s.pen.penMembers++;
        }

        public void SetDoor(Building p, int i, bool open, bool byHand)
        {
            if (!IsRoom(p) || p.doorOpen[i] == open) return;
            p.doorOpen[i] = open;
            ResetPicks(p);
            var o = DoorOuter(p, i);
            if (IsVisible(o))
            {
                Sfx.Play(open ? "boing" : "thud", 0.45f, open ? 1.7f : 1.3f);
                FloatText(o + Vector2.up * 1.1f, open ? "DOOR OPEN" : "DOOR CLOSED", open ? new Color(0.55f, 1f, 0.5f) : new Color(1f, 0.65f, 0.45f));
                if (open) Sfx.Baa(0.5f);
            }
            if (byHand) gateToggles++;
        }
        public void ToggleDoor(Building p, int i) { SetDoor(p, i, !p.doorOpen[i], true); }
        public void ToggleMainDoor(Building p) { ToggleDoor(p, MainDoor(p)); }
        /// Exits share the traffic by picking the door used least. Counting from zero again whenever the doors change keeps that a
        /// real round robin: a newly opened door used to take every sheep until its count caught up with the old doors' totals.
        static void ResetPicks(Building b) { for (int k = 0; k < b.doorPicks.Length; k++) b.doorPicks[k] = 0; }

        public void CycleFilter(Building p, int i, int dir = 1)
        {
            p.doorFilter[i] = (p.doorFilter[i] + dir + FilterNames.Length) % FilterNames.Length;
            ResetPicks(p);
            Sfx.Play("click", 0.4f, 1.2f);
        }

        /// is another sheep within r of pos? `hint` (one per door: Building.nearHint) is the last sheep found there. It is tested
        /// first, with the same test on live state, so the answer is the full scan's (a sheep that leaves the flock is dead first).
        bool SheepNear(Vector2 pos, float r, Sheep except, ref Sheep hint)
        {
            float r2 = r * r;
            var w = hint;
            if (w != null && w != except && !w.dead && w.act != Act.Hidden && w.act != Act.InMachine && (w.pos - pos).sqrMagnitude < r2) return true;
            foreach (var o in sheep)
            {
                if (o == except || o.dead || o.act == Act.Hidden || o.act == Act.InMachine) continue;
                if ((o.pos - pos).sqrMagnitude < r2) { hint = o; return true; }
            }
            return false;
        }

        // ---------------- sheep in and around rooms ----------------
        static bool SegHits(Rect R, Vector2 a, Vector2 b)
        {
            for (int i = 1; i < 12; i++) if (R.Contains(Vector2.Lerp(a, b, i / 12f))) return true;
            return false;
        }

        /// next point to walk to so the sheep goes around the fence instead of through it
        static Vector2 RoomWaypoint(Building p, Vector2 pos, Vector2 dest)
        {
            var R = new Rect(p.x - 0.3f, p.y - 0.3f, p.def.w + 0.6f, p.def.h + 0.6f);
            if (!SegHits(R, pos, dest)) return dest;
            // the four corners as locals (no array per call), tried in the same order: on a tie the first one wins
            Vector2 c0 = new Vector2(R.xMin - 0.25f, R.yMin - 0.25f), c1 = new Vector2(R.xMax + 0.25f, R.yMin - 0.25f),
                    c2 = new Vector2(R.xMax + 0.25f, R.yMax + 0.25f), c3 = new Vector2(R.xMin - 0.25f, R.yMax + 0.25f);
            Vector2 best = c0; float bs = 1e9f;
            for (int k = 0; k < 4; k++)
            {
                Vector2 c = k == 0 ? c0 : k == 1 ? c1 : k == 2 ? c2 : c3;
                if (SegHits(R, pos, c)) continue;
                float score = (c - pos).magnitude + (c - dest).magnitude + (SegHits(R, c, dest) ? 50 : 0);
                if (score < bs) { bs = score; best = c; }
            }
            return best;
        }

        /// walking outside: the next point to head for, so a sheep goes around rooms instead of through them
        Vector2 RouteAround(Vector2 from, Vector2 dest)
        {
            foreach (var b in buildings)
            {
                if (!IsRoom(b) || !RoomActive(b)) continue;
                var R = new Rect(b.x - 0.3f, b.y - 0.3f, b.def.w + 0.6f, b.def.h + 0.6f);
                if (R.Contains(from) || R.Contains(dest)) continue;
                if (SegHits(R, from, dest)) return RoomWaypoint(b, from, dest);
            }
            return dest;
        }

        bool WantsOutBase(Sheep s, Building p)
        {
            if (s.job != Job.Grazer || s.Lamb || s.unconscious > 0 || s.refuse > 0 || s.sick > 0) return false;
            if (IsLot(p))
            {
                // only fed sheep leave a Feeding Lot; a sheep that started a meal finishes it first
                if (res[(int)Res.Food] < 0.4f) return true;
                if (s.hasEatSpot) return false;
                return s.hunger <= (s.exitDoor >= 0 ? 0.6f : 0.3f);
            }
            return true;
        }

        /// which open door this sheep walks to (round robin between doors it is allowed through)
        int PickExitDoor(Sheep s, Building p)
        {
            if (!RoomActive(p) || !WantsOutBase(s, p)) return -1;
            int cur = s.exitDoor;
            if (cur >= 0 && DoorUsable(p, cur) && FilterAllows(Filter(p, cur), s)) return cur;
            int best = -1, bestN = int.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                if (!DoorUsable(p, i) || !FilterAllows(Filter(p, i), s)) continue;
                if (p.doorPicks[i] < bestN) { bestN = p.doorPicks[i]; best = i; }
            }
            if (best >= 0) p.doorPicks[best]++;
            s.exitDoor = best;
            return best;
        }

        /// returns true when the room logic moved the sheep this tick
        bool PenBehaviour(Sheep s, float dt)
        {
            var p = s.pen;
            if (p == null) return false;
            if (!buildings.Contains(p)) { s.pen = null; AssignPen(s); return false; }
            var inner = PenInterior(p);
            bool inside = inner.Contains(s.pos);

            // workers live outside: they let themselves out through the nearest door
            if (s.job != Job.Grazer)
            {
                bool walking = s.act == Act.Wander || s.act == Act.Graze || s.act == Act.Idle || s.act == Act.ToGate || s.act == Act.GoWork;
                if (!inside || !walking) { if (s.act == Act.ToGate) s.act = Act.Wander; return false; }
                int wd = s.workplace != null ? NearestDoor(p, s.workplace.Center) : NearestDoor(p, s.pos);
                s.act = Act.ToGate; s.status = KindText(KindLeaving, p.def);
                if (MoveTo(s, DoorInner(p, wd), dt, 1.1f)) { s.pos = DoorOuter(p, wd) + Sideways(wd); s.act = Act.Wander; s.target = s.pos; s.beltImmune = 1.5f; }
                return true;
            }

            if (s.act != Act.Wander && s.act != Act.Graze && s.act != Act.Idle && s.act != Act.ToGate) return false;
            if (inside)
            {
                int door = PickExitDoor(s, p);
                if (door < 0)
                {
                    if (s.act == Act.ToGate) { s.act = Act.Wander; s.target = PickGrazeTarget(s); }
                    return IsLot(p) && LotEat(s, p, dt);
                }
                s.act = Act.ToGate;
                if (MoveTo(s, DoorInner(p, door), dt, 1.1f))
                {
                    var o = DoorOuter(p, door);
                    if (p.doorCd[door] <= 0 && !SheepNear(o, 0.6f, s, ref p.nearHint[door]))
                    {
                        p.doorCd[door] = GateInterval;
                        s.pos = o; s.target = o;
                        s.act = Act.Wander; s.ridingT = 0; s.beltImmune = 0; s.exitDoor = -1; s.hasEatSpot = false;
                        s.status = "Riding to the " + (DoorDestination(p, door) ?? "conveyor");
                        TakeDrove(s, p, door); // the herd of its kind follows it out
                    }
                    else s.status = QueueingAtDoor[door];
                }
                else s.status = HeadingForDoor[door];
                return true;
            }

            // outside (belts carry riders before we get here): walk back in through the nearest door
            if (s.onBelt) return false;
            int hd = NearestDoor(p, s.pos);
            Vector2 go = DoorOuter(p, hd);
            s.act = Act.ToGate;
            s.status = KindText(KindGoingBack, p.def);
            MoveTo(s, RoomWaypoint(p, s.pos, go), dt, 1.05f);
            if ((s.pos - go).sqrMagnitude < 0.03f)
            {
                s.pos = DoorInner(p, hd) + Inward(hd) * 0.45f;
                s.act = Act.Wander; s.target = PickGrazeTarget(s); s.exitDoor = -1;
                MergeDrove(s, p);
            }
            return true;
        }

        /// inside a Feeding Lot: walk to the troughs and eat until full
        bool LotEat(Sheep s, Building p, float dt)
        {
            if (!s.hasEatSpot && s.hunger <= 0.3f) return false; // fed: mill about until a door opens
            if (res[(int)Res.Food] < 0.4f) { s.hasEatSpot = false; s.status = "Waiting for food. The store is EMPTY"; return false; }
            if (!s.hasEatSpot)
            {
                var inner = PenInterior(p);
                s.eatSpot = new Vector2(inner.xMin + 0.2f + (float)rng.NextDouble() * (inner.width - 0.4f), p.y + p.def.h - 1.2f);
                s.hasEatSpot = true;
            }
            if (!MoveTo(s, s.eatSpot, dt, 1.1f)) { s.act = Act.Wander; s.status = "Running to the trough"; return true; }
            s.act = Act.Graze;
            s.status = "Eating like it is the last day";
            float bite = Mathf.Min(s.hunger, dt * LotEatRate(p));
            float cost = bite * 4f * FoodMult(s);
            if (res[(int)Res.Food] < cost) { bite *= (float)(res[(int)Res.Food] / cost); cost = (float)res[(int)Res.Food]; }
            res[(int)Res.Food] -= cost; foodEaten += cost;
            s.hunger -= bite;
            if (IsVisible(s.pos) && fxRng.NextDouble() < dt * 2.5) SpawnParticles(s.pos + new Vector2(s.faceRight ? 0.4f : -0.4f, 0.5f), new Color(0.55f, 0.75f, 0.3f), 2, 1f);
            if (s.hunger <= 0.12f)
            {
                lotMeals = Bump(lotMeals, 1);
                s.hasEatSpot = false;
                s.act = Act.Wander; s.target = PickGrazeTarget(s);
                if (IsVisible(s.pos) && fxRng.NextDouble() < 0.4) SpeechText(s.pos + new Vector2(0.2f, 1.3f), fxRng.NextDouble() < 0.5 ? "full" : "*burp*");
                if (fxRng.NextDouble() < 0.3) Sfx.Play("nom", 0.25f);
            }
            return true;
        }

        /// a belt dumps a rider into a room
        void DropIntoPen(Sheep s, Building room, Vector2 at)
        {
            var inner = PenInterior(room);
            s.pos = new Vector2(Mathf.Clamp(at.x, inner.xMin + 0.15f, inner.xMax - 0.15f), Mathf.Clamp(at.y, inner.yMin + 0.15f, inner.yMax - 0.15f));
            s.onBelt = false; s.ridingT = 0; s.beltImmune = 0;
            s.act = Act.Wander; s.target = s.pos; s.exitDoor = -1; s.hasEatSpot = false;
            if (s.job == Job.Grazer) s.pen = room;
            room.penCount++;
            MergeDrove(s, room); // its drove joins the room's herd
            if (IsVisible(s.pos) && fxRng.NextDouble() < 0.4)
            {
                string[] lines = IsLot(room) ? LotDropLines : PenDropLines;
                SpeechText(s.pos + new Vector2(0.2f, 1.3f), lines[fxRng.Next(lines.Length)]);
            }
        }
        static readonly string[] LotDropLines = { "FOOD", "*plop*", "finally", "hay!" }, PenDropLines = { "*plop*", "home!", "again?", "never again", "*thud*", "wheee" };

        /// grazing spot: inside its room for room sheep, anywhere else (but never in a room) for the rest
        Vector2 PickGrazeTarget(Sheep s)
        {
            bool inPen = s.pen != null && PenInterior(s.pen).Contains(s.pos);
            Rect area = inPen ? PenInterior(s.pen) : new Rect(owned.xMin + 0.4f, owned.yMin + 0.3f, owned.width - 0.8f, owned.height - 0.9f);
            Vector2 best = s.pos; float bestScore = -99;
            for (int i = 0; i < 7; i++)
            {
                Vector2 c = s.pos + new Vector2((float)(rng.NextDouble() * 2 - 1) * (inPen ? 3 : 5), (float)(rng.NextDouble() * 2 - 1) * (inPen ? 2.5f : 4));
                c.x = Mathf.Clamp(c.x, area.xMin, area.xMax);
                c.y = Mathf.Clamp(c.y, area.yMin, area.yMax);
                var t = TileAt(c);
                if (t == null || t.building != null || t.belt >= 0) continue;
                if (!inPen && t.pen != null) continue;
                float score = t.grass - (t.path ? 1 : 0) + (float)rng.NextDouble() * 0.3f;
                if (score > bestScore) { bestScore = score; best = c; }
            }
            return best;
        }

        /// legacy troughs (old saves): a trough inside a pen feeds that pen
        Building TroughFor(Sheep s)
        {
            Building pen = s.pen != null && PenInterior(s.pen).Contains(s.pos) ? s.pen : null;
            if (s.pen != null && pen == null) return null;
            Building best = null; float bd = 1e9f;
            foreach (var b in buildings)
            {
                if (b.def.kind != BKind.Trough || !b.Built) continue;
                if (tiles[b.x, b.y].pen != pen) continue;
                float d = (b.Center - s.pos).sqrMagnitude;
                if (d < bd) { bd = d; best = b; }
            }
            return best;
        }

        // ---------------- room upkeep and breeding ----------------
        void CountPens()
        {
            lineShears = false; lineMilks = false;
            foreach (var b in buildings)
            {
                if (IsRoom(b)) { b.penCount = 0; b.penReady = 0; b.penOut = 0; b.penMembers = 0; b.penHungry = 0; b.penFed = 0; b.leaderMask = 0; b.spareHand = null; }
                if (!b.Built || b.damaged) continue;
                if (b.def.kind == BKind.Shed || b.def.kind == BKind.Plant) lineShears = true;
                if (b.def.kind == BKind.Dairy) lineMilks = true;
            }
            foreach (var s in sheep)
            {
                if (s.dead || s.act == Act.Hidden || s.pen == null || s.job != Job.Grazer) continue;
                var p = s.pen;
                p.penMembers++;
                if (s.act != Act.InMachine && PenInterior(p).Contains(s.pos))
                {
                    p.penCount++;
                    if (Ready(s)) p.penReady++;
                    if (s.hunger >= 0.7f) p.penHungry++;
                    if (s.hunger < 0.3f) p.penFed++;
                    if (s.drove != null && !s.onBelt) MergeDrove(s, p); // a drove that got in some other way joins the herd
                    // who could lead the herd out right now, and which herd hand has nothing to do (Core/Herd.cs)
                    if (s.act == Act.ToGate || (!s.Lamb && CanLeave(s, p))) { if (!s.Lamb) p.leaderMask |= 1 << (s.strain * 2 + (s.male ? 1 : 0)); }
                    else if (s.herdHand) p.spareHand = s; // simulation state only: what the inspector shows must not change the run
                }
                else p.penOut++;
            }
            foreach (var b in buildings)
            {
                var h = b.herd;
                if (h == null || !IsRoom(b) || h.Adults < 1) continue;
                if (h.hunger >= 0.7f) b.penHungry = Bump(b.penHungry, h.Adults);
                if (h.hunger < 0.3f) b.penFed = Bump(b.penFed, h.Adults);
            }
        }

        public int SheepOutOfRooms()
        {
            int n = 0;
            foreach (var b in buildings) if (IsPen(b)) n += b.penOut;
            return n;
        }

        readonly List<Sheep> penMales = new List<Sheep>(), penFemales = new List<Sheep>();
        void SimPen(Building b, float dt)
        {
            for (int i = 0; i < 4; i++) if (b.doorCd[i] > 0) b.doorCd[i] -= dt;
            if (IsLot(b))
            {
                if (Inside(b) < 1) b.status = "Empty. Run a conveyor from a pen door into it";
                else if (res[(int)Res.Food] < 1) b.status = "NO FOOD in the store! Build farms";
                else if (b.penHungry > 0) b.status = "Feeding " + UI.Fmt(b.penHungry) + " hungry sheep";
                else b.status = AnyDoorUsable(b) ? "Everyone is fed and moving on" : "Everyone is fed. Open a door to send them on";
                return;
            }
            // Breeding Pen: fed, happy rams and ewes inside make lambs
            var males = penMales; var females = penFemales; // reused lists (no garbage per pen per step), filled in the same order
            males.Clear(); females.Clear();
            var inner = PenInterior(b);
            bool fertile = false;
            foreach (var s in sheep)
            {
                if (s.dead || s.pen != b || s.Lamb || s.job != Job.Grazer || s.act == Act.Hidden || s.act == Act.ToGate) continue;
                if (!inner.Contains(s.pos) || s.hunger > 1.2f || s.mood < 30 || s.sick > 0) continue;
                if (s.male) males.Add(s); else females.Add(s);
                if (s.Has("fertile")) fertile = true;
            }
            int pairs = Mathf.Min(males.Count, females.Count);
            if (pairs == 0)
            {
                var hh = b.herd;
                double hp = hh != null ? System.Math.Min(System.Math.Floor(hh.Ewes), System.Math.Floor(hh.Rams)) : 0;
                if (hp >= 1) b.status = Members(b) >= RoomCapacity(b) || Population >= FlockCap ? "FULL: no room for lambs. Upgrade it, SHEEP STACKING, or another pen"
                    : hh.hunger > 1.2f ? "The herd is too HUNGRY to breed. Send it to a Feeding Lot" : HerdBreedingStatus(b, hp);
                else b.status = Inside(b) < 1 ? "Empty" : b.penHungry > 0 ? "Too HUNGRY to breed. Send them to a Feeding Lot" : "Resting (lambs need a fed, happy RAM and EWE)";
                return;
            }
            if (Members(b) >= RoomCapacity(b)) { b.status = "FULL: no room for lambs. Upgrade it or build another pen"; return; }
            if (Population >= FlockCap) { b.status = "ALL PENS FULL: upgrade a pen, build another, or thin the flock"; return; }
            float sp = 0.45f * Mathf.Pow(pairs, 0.6f) * (fertile ? 1.3f : 1f) * (tech.Contains("matchmaking") ? 1.5f : 1f) * PenBreedMult(b) * (HasGene("love_songs") ? 1.3f : 1f);
            float avgMood = 0; foreach (var s in males) avgMood += s.mood; foreach (var s in females) avgMood += s.mood;
            sp *= Mathf.Clamp(avgMood / (males.Count + females.Count) / 65f, 0.4f, 1.3f);
            if (!tutDone) sp *= CurStep != null && CurStep.id == "breed" ? 2.5f : 0.6f;
            sp *= (float)PathMult("pen_breed");
            b.breedProgress += dt / DayLength * sp;
            b.status = "Breeding (" + pairs + " couple" + (pairs > 1 ? "s" : "") + ")";
            if (fxRng.NextDouble() < dt * 0.5 * Mathf.Min(3, pairs) && IsVisible(b.Center))
            {
                var pick = fxRng.NextDouble() < 0.5 ? males[fxRng.Next(males.Count)] : females[fxRng.Next(females.Count)];
                SpawnParticles(pick.pos + Vector2.up * 1.0f, new Color(1f, 0.45f, 0.62f), 2, 1f);
            }
            if (b.breedProgress >= 1f)
            {
                b.breedProgress = 0;
                var pa = males[rng.Next(males.Count)]; var pb = females[rng.Next(females.Count)];
                Vector2 at = Vector2.Lerp(pa.pos, pb.pos, 0.5f);
                at.x = Mathf.Clamp(at.x, inner.xMin, inner.xMax); at.y = Mathf.Clamp(at.y, inner.yMin, inner.yMax);
                if (!CanName(b)) // the room's names are used up: the lamb (and a twin) joins the herd
                {
                    HerdLambOf(b, pa, pb, at);
                    if (HasGene("twin_genes") && rng.NextDouble() < 0.2 && Members(b) + 1 <= RoomCapacity(b) && Population + 1 <= FlockCap) HerdLambOf(b, pa, pb, at);
                    return;
                }
                var lamb = CreateSheep(at, pa, pb);
                lamb.pen = b;
                births = Bump(births, 1);
                if (HasGene("twin_genes") && rng.NextDouble() < 0.2 && Members(b) + 2 <= RoomCapacity(b) && Population + 2 <= FlockCap)
                {
                    if (CanName(b)) { var twin = CreateSheep(at + new Vector2(0.35f, 0.1f), pa, pb); twin.pen = b; births = Bump(births, 1); }
                    else HerdLambOf(b, pa, pb, at + new Vector2(0.35f, 0.1f));
                    if (IsVisible(at)) FloatText(at + Vector2.up * 1.6f, "TWINS!", new Color(1f, 0.6f, 0.75f));
                }
                SpawnParticles(lamb.pos + Vector2.up * 0.5f, new Color(1f, 0.6f, 0.75f), 14, 2f);
                Sfx.Baa(0.7f);
                string extra = lamb.variant != Variant.None ? " ^p(" + VariantName(lamb.variant) + "!)" : "";
                Log("^pA lamb is born in the pen!^w Meet ^y" + lamb.name + "^w (" + TraitList(lamb) + ")" + extra);
                FloatText(lamb.pos + Vector2.up, "NEW LAMB", new Color(1f, 0.6f, 0.75f));
                if (tutDone) AdviseNow("lamb", "A new lamb: " + lamb.name + ". Born in MY pen. Another future wool factory.", LExpr.Smug, 3f);
            }
        }

        // ---------------- fences, doors and troughs ----------------
        static int RoomStyle(Building b) { return Mathf.Clamp(b.level - 1, 0, 4); }

        void SpawnPenFence(Building b)
        {
            if (b.fenceRoot != null) Destroy(b.fenceRoot);
            b.extras.Clear();
            var root = new GameObject("fence");
            root.transform.SetParent(b.go.transform, false);
            b.fenceRoot = root;
            int st = RoomStyle(b);
            System.Func<Sprite, Vector2, int, GameObject> add = (spr, pos, bump) =>
            {
                var go = new GameObject("f");
                go.transform.SetParent(root.transform, true);
                go.transform.position = new Vector3(pos.x, pos.y, 0);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = spr; sr.sortingOrder = SortY(pos.y) + bump;
                return go;
            };
            var d0 = DoorTile(b, 0); var d1 = DoorTile(b, 1); var d2 = DoorTile(b, 2); var d3 = DoorTile(b, 3);
            int w = b.def.w, h = b.def.h;
            // the doors first: extras[0..3]
            b.extras.Add(add(EnvArt.Gate(b.doorOpen[0], st), new Vector2(d0.x, b.y - 0.1f), 1));
            b.extras.Add(add(EnvArt.GateSide(b.doorOpen[1], st), new Vector2(b.x + w, d1.y), 1));
            b.extras.Add(add(EnvArt.Gate(b.doorOpen[2], st), new Vector2(d2.x, b.y + h - 0.1f), 1));
            b.extras.Add(add(EnvArt.GateSide(b.doorOpen[3], st), new Vector2(b.x + 0.1f, d3.y), 1));
            for (int x = b.x; x < b.x + w; x++)
            {
                if (x != d0.x) add(EnvArt.RoomFenceH(st), new Vector2(x, b.y - 0.1f), 0);
                if (x != d2.x) add(EnvArt.RoomFenceH(st), new Vector2(x, b.y + h - 0.1f), 0);
            }
            add(EnvArt.RoomPost(st), new Vector2(d0.x + 1, b.y - 0.1f), 0);
            add(EnvArt.RoomPost(st), new Vector2(d2.x + 1, b.y + h - 0.1f), 0);
            for (int y = b.y + 1; y < b.y + h; y++)
            {
                add(EnvArt.RoomPost(st), new Vector2(b.x + 0.1f, y - 0.1f), 0);
                add(EnvArt.RoomPost(st), new Vector2(b.x + w, y - 0.1f), 0);
            }
            add(EnvArt.RoomPost(st), new Vector2(b.x + w, b.y - 0.1f), 0);
            add(EnvArt.RoomPost(st), new Vector2(b.x + w, b.y + h - 0.1f), 0);
            // feeding lots: a row of troughs along the back fence (extras[4..])
            if (IsLot(b))
                for (int k = 0; k + 1 < w; k += 2)
                    b.extras.Add(add(LeaderArt.Trough(3), new Vector2(b.x + k + (w % 2) * 0.5f, b.y + h - 1f), -1));
        }

        void UpdatePenVisual(Building b)
        {
            int st = RoomStyle(b);
            for (int i = 0; i < 4 && i < b.extras.Count; i++)
            {
                if (b.extras[i] == null) continue;
                var sr = b.extras[i].GetComponent<SpriteRenderer>();
                if (sr != null) sr.sprite = i % 2 == 0 ? EnvArt.Gate(b.doorOpen[i], st) : EnvArt.GateSide(b.doorOpen[i], st);
            }
            if (IsLot(b))
            {
                double f = res[(int)Res.Food];
                var tr = LeaderArt.Trough(f <= 1 ? 0 : f < 25 ? 1 : f < 90 ? 2 : 3);
                for (int i = 4; i < b.extras.Count; i++) if (b.extras[i] != null) b.extras[i].GetComponent<SpriteRenderer>().sprite = tr;
            }
            UpdateHerdVisual(b);
            UpdateChamberHuts(b);
        }

        void ClearPen(Building b)
        {
            for (int x = b.x; x < b.x + b.def.w; x++) for (int y = b.y; y < b.y + b.def.h; y++) if (InMap(x, y) && tiles[x, y].pen == b) tiles[x, y].pen = null;
            foreach (var s in sheep) if (s.pen == b) s.pen = null;
        }

        // ---------------- conveyor machines ----------------
        public static bool IsSheepMachine(Building b) { return b != null && IsSheepMachineKind(b.def.kind); }
        public static bool IsSheepMachineKind(BKind k) { return k == BKind.Shed || k == BKind.Dairy || k == BKind.Plant || k == BKind.Slaughter || k == BKind.GeneLab; }

        static readonly float[] MachineSpeedByLevel = { 1f, 1.35f, 1.8f, 2.5f, 3.5f };
        float MachineTime(Building b)
        {
            float t = b.def.kind == BKind.Plant ? 0.5f : b.def.kind == BKind.Slaughter ? 1.4f : b.def.kind == BKind.GeneLab ? 1.0f : 1.2f;
            t /= BuildingDef.At(MachineSpeedByLevel, b.level);
            if (b.def.kind == BKind.Shed || b.def.kind == BKind.Plant) t /= (float)PathMult("shear_speed");
            else if (b.def.kind == BKind.Dairy) t /= (float)PathMult("milk_speed");
            return t;
        }

        /// Sheep a machine takes in one pass (M12): 250, 1000, 4000, 16000 or 64000 by its level, twice that for the Wool Plant, more
        /// with Bigger Hoppers. A bigger drove takes more passes: only time, every one of it is still shorn or milked.
        public double MachineBatch(Building b)
        {
#if FLEECED_DEV
            if (DevHooks.BatchCapOverride > 0) return DevHooks.BatchCapOverride;
#endif
            double c = ScaleMath.BatchByLevel[Mathf.Clamp(b.level, 1, ScaleMath.BatchByLevel.Length) - 1];
            if (b.def.kind == BKind.Plant) c *= ScaleMath.PlantBatchFactor;
            return c * PathMult("machine_batch");
        }

        /// the machines that take a drove in passes: shearing and milking. The Slaughterhouse and the Gene Lab take one pass.
        static bool PassMachine(Building b) { var k = b.def.kind; return k == BKind.Shed || k == BKind.Plant || k == BKind.Dairy; }

        /// the first drove too big for a machine (once a world, after the tutorial): the Supreme Leader explains passes. It marks
        /// itself said when it opens; queued again after a load (followers -1: the numbers are gone, so he speaks in general).
        void PassIntro(Building b, double batch, double followers, double passes)
        {
            if (flags.Contains("pass_said")) return;
            flags.Add("pass_said");
            if (b != null && b.go != null) FocusOn(b.Center);
            string first = b == null || followers < 0
                ? "My machines are CHOKING on big droves, Hoof. A machine takes so many sheep in one PASS: a bigger drove takes several, and the whole line WAITS."
                : "My " + b.def.name.ToUpper() + " is CHOKING, Hoof. It takes " + UI.Fmt(batch) + " sheep a pass. That drove was " + UI.Fmt(followers) + ": " + PassesText(passes) + ", and the whole line WAITED.";
            LeaderSay(new[]
            {
                new LeaderPage(first, LExpr.Shout),
                new LeaderPage("Nothing is lost, only TIME. Upgrade the machine (every level takes FOUR times as many), buy ^yBIGGER HOPPERS^w (U), or split the line with a second OUT door.", LExpr.Stern),
            });
        }

        /// "6.4 passes", "just over one pass"
        static string PassesText(double p) { return p < 1.05 ? "just over one pass" : (p < 10 ? p.ToString("0.#") : UI.Fmt(System.Math.Round(p))) + " passes"; }

        bool UsefulIn(Building b, Sheep s)
        {
            if (b.def.kind == BKind.Slaughter || b.def.kind == BKind.GeneLab) return true;
            if (b.def.kind == BKind.Dairy) return (!s.male && !s.Lamb && s.milk >= 0.2f) || DroveMilk(s, b) >= 1;
            return s.wool >= 0.2f || DroveShear(s, b) >= 1; // a bald leader with a fluffy drove takes the full time
        }

        int BeltDirAt(int x, int y) { return InMap(x, y) ? tiles[x, y].belt : -1; }

        // ---------------- machine doors ----------------
        // A machine has a door on every side a belt touches (0 bottom, 1 right, 2 top, 3 left).
        // Sheep ride in through an OPEN door and leave through an OPEN door with a belt leading away.
        // A SHUT door stops sheep: wool, milk and mutton leave through shut doors only.
        // Until the player clicks it, a door follows the belts: open where a belt comes in and straight
        // across from it (the sheep line), shut everywhere else (goods).
        /// buildings that take goods off a conveyor: they get IN doors too (the Market also takes live sheep)
        public static bool TakesGoodsKind(BKind k)
        {
            return k == BKind.Market || k == BKind.Creamery || k == BKind.Workshop || k == BKind.Textile || k == BKind.Atelier || k == BKind.Storage;
        }
        public static bool HasBeltDoors(Building b) { return b != null && (IsSheepMachine(b) || TakesGoodsKind(b.def.kind)); }

        public static int SideLen(Building b, int i) { return i == 0 || i == 2 ? b.def.w : b.def.h; }
        static int Opp(int i) { return (i + 2) % 4; }

        /// the k-th tile just outside side i
        public static Vector2Int SideTile(Building b, int i, int k)
        {
            switch (i)
            {
                case 0: return new Vector2Int(b.x + k, b.y - 1);
                case 1: return new Vector2Int(b.x + b.def.w, b.y + k);
                case 2: return new Vector2Int(b.x + k, b.y + b.def.h);
                default: return new Vector2Int(b.x - 1, b.y + k);
            }
        }

        /// which side the outside tile (x, y) is on (-1 for corners and anything else)
        static int SideOf(Building b, int x, int y)
        {
            bool inX = x >= b.x && x < b.x + b.def.w, inY = y >= b.y && y < b.y + b.def.h;
            if (inY && x == b.x - 1) return 3;
            if (inY && x == b.x + b.def.w) return 1;
            if (inX && y == b.y - 1) return 0;
            if (inX && y == b.y + b.def.h) return 2;
            return -1;
        }

        /// the side a sheep riding in direction d bumps into
        static int EntrySide(Vector2Int d) { return d.x > 0 ? 3 : d.x < 0 ? 1 : d.y > 0 ? 0 : 2; }

        /// 1 = the belt on tile t runs into b, 2 = it leads away, 0 = no belt
        int BeltRole(Building b, Vector2Int t)
        {
            int dir = BeltDirAt(t.x, t.y);
            if (dir < 0) return 0;
            var d = Dir(dir);
            return InsideFootprint(b, t.x + d.x, t.y + d.y) ? 1 : 2;
        }
        public bool SideHasIn(Building b, int i) { for (int k = 0; k < SideLen(b, i); k++) if (BeltRole(b, SideTile(b, i, k)) == 1) return true; return false; }
        public bool SideHasOut(Building b, int i) { for (int k = 0; k < SideLen(b, i); k++) if (BeltRole(b, SideTile(b, i, k)) == 2) return true; return false; }

        /// the first tile along side i whose belt has that role (1 in, 2 out)
        bool SideBelt(Building b, int i, int role, out Vector2Int t)
        {
            for (int k = 0; k < SideLen(b, i); k++) { t = SideTile(b, i, k); if (BeltRole(b, t) == role) return true; }
            t = default(Vector2Int);
            return false;
        }

        public bool MachineDoorOpen(Building b, int i)
        {
            if (b.doorTouched[i]) return b.doorOpen[i];
            if (SideHasIn(b, i)) return true;
            return b.def.kind != BKind.Slaughter && SideHasOut(b, i) && SideHasIn(b, Opp(i));
        }

        /// can a sheep leave through side i? (nobody leaves the Slaughterhouse)
        public bool SheepExitSide(Building b, int i) { return b.def.kind != BKind.Slaughter && SideHasOut(b, i) && MachineDoorOpen(b, i); }
        public bool AnySheepExit(Building b) { for (int i = 0; i < 4; i++) if (SheepExitSide(b, i)) return true; return false; }
        public int SheepExitCount(Building b) { int n = 0; for (int i = 0; i < 4; i++) if (SheepExitSide(b, i)) n++; return n; }
        bool AnyMachineInOpen(Building b) { for (int i = 0; i < 4; i++) if (SideHasIn(b, i) && MachineDoorOpen(b, i)) return true; return false; }

        /// "IN", "OUT", "MUTTON" (the Slaughterhouse's way out is a chute, not a door) or null when no belt touches that side
        public string MachineDoorRole(Building b, int i)
        {
            if (SideHasIn(b, i)) return "IN";
            if (!IsSheepMachine(b)) return null; // goods buildings: their outputs are just chutes
            if (!SideHasOut(b, i)) return null;
            return b.def.kind == BKind.Slaughter ? "MUTTON" : "OUT";
        }
        public bool MachineDoorToggleable(Building b, int i) { string r = MachineDoorRole(b, i); return r == "IN" || r == "OUT"; }
        public static string MachineGoods(Building b) { return b.def.kind == BKind.GeneLab ? null : b.def.kind == BKind.Dairy ? "milk" : b.def.kind == BKind.Slaughter ? "mutton" : "wool"; }

        /// where the belt leaving side i leads
        public string MachineDoorDestination(Building b, int i)
        {
            Vector2Int t;
            return SideBelt(b, i, 2, out t) ? TraceBelt(t.x, t.y, b) : null;
        }

        /// the belt tile of door i (the one coming in, else the one leading away)
        public Vector2 MachineDoorSpot(Building b, int i)
        {
            Vector2Int t;
            if (!SideBelt(b, i, 1, out t) && !SideBelt(b, i, 2, out t)) t = SideTile(b, i, SideLen(b, i) / 2);
            return new Vector2(t.x + 0.5f, t.y + 0.5f);
        }

        public void ToggleMachineDoor(Building b, int i)
        {
            bool open = !MachineDoorOpen(b, i);
            b.doorTouched[i] = true;
            b.doorOpen[i] = open;
            ResetPicks(b);
            gateToggles++;
            var o = MachineDoorSpot(b, i);
            if (IsVisible(o))
            {
                Sfx.Play(open ? "boing" : "thud", 0.45f, open ? 1.9f : 1.5f);
                FloatText(o + Vector2.up * 1.2f, open ? "DOOR OPEN" : "DOOR SHUT", open ? new Color(0.55f, 1f, 0.5f) : new Color(1f, 0.65f, 0.45f));
            }
        }

        public string MachineDoorTip(Building b, int i)
        {
            string role = MachineDoorRole(b, i);
            if (role == null) return null;
            string side = DoorNames[i];
            if (role == "MUTTON") return side + " CHUTE\nMutton leaves here and rides to: " + (MachineDoorDestination(b, i) ?? "?") + ".";
            bool open = MachineDoorOpen(b, i);
            if (b.def.kind == BKind.Market)
                return side + " MARKET DOOR: " + (open ? "OPEN" : "SHUT") + "\n" + (open ? "Everything that rides in here is SOLD: goods (+10%) and live sheep (~" + LiveSheepPrice(null, b) + "c each).\nClick to SHUT it: they wait on the belt." : "Goods and sheep wait on the belt outside.\nClick to OPEN it and sell them.");
            if (!IsSheepMachine(b))
                return side + " DOOR (IN): " + (open ? "OPEN" : "SHUT") + "\n" + (open ? "Goods on this belt go into the " + b.def.name + ". Sheep are NOT welcome here.\nClick to SHUT it: the goods wait on the belt." : "Goods wait on the belt outside.\nClick to OPEN it.");
            if (role == "IN")
                return side + " DOOR (IN): " + (open ? "OPEN" : "SHUT") + "\n" + (open ? "Sheep on this belt ride into the " + b.def.name + ".\nClick to SHUT it: they wait on the belt." : "Sheep wait on the belt outside.\nClick to OPEN it and let them in.");
            string dest = MachineDoorDestination(b, i) ?? "?";
            int f = Filter(b, i);
            string t = side + " DOOR (OUT): " + (open ? "OPEN" : "SHUT") + "\n";
            if (open) t += "Sheep leave here and ride to: " + dest + "." + (f > 0 ? "\nFilter: " + FilterTips[f] : "") + "\nClick to SHUT it: only " + (MachineGoods(b) ?? "nothing") + " comes out here.";
            else t += "Only " + (MachineGoods(b) ?? "nothing") + " comes out here (to: " + dest + ").\nClick to OPEN it: sheep leave this way too.";
            if (!AnySheepExit(b)) t += "\nNo OUT door is open: sheep wait INSIDE the machine until one is.";
            return t;
        }

        /// does any conveyor lead into this building?
        public bool HasBeltIn(Building b) { for (int i = 0; i < 4; i++) if (SideHasIn(b, i)) return true; return false; }

        /// a sheep riding a belt reaches a machine through an open door: in it goes
        void EnterMachine(Sheep s, Building b, Vector2Int d)
        {
            int tx = Mathf.FloorToInt(s.pos.x), ty = Mathf.FloorToInt(s.pos.y);
            b.machineIn = EntrySide(d);
            b.machineLane = d.x != 0 ? Mathf.Clamp(ty - b.y, 0, b.def.h - 1) : Mathf.Clamp(tx - b.x, 0, b.def.w - 1);
            b.inMachine = s;
            b.machineDone = false; b.machineDid = false;
            bool useful = UsefulIn(b, s), inPasses = useful && PassMachine(b);
            // the leader and the followers the machine works on: a drove with nothing to give rides through on the leader's pass
            double load = 1;
            if (inPasses && s.drove != null && (b.def.kind == BKind.Dairy ? DroveMilk(s, b) : DroveShear(s, b)) >= 1) load += s.drove.n;
            b.machinePasses = inPasses ? (float)ScaleMath.Passes(load, MachineBatch(b)) : 1f;
            b.machinePassShown = 0;
            b.machineDur = useful ? MachineTime(b) * b.machinePasses : 0.35f; // x exactly 1 for a drove that fits one pass
            if (b.machinePasses > 1 && tutDone && !flags.Contains("pass_intro"))
            {
                // the first drove too big for a machine: it opens Bigger Hoppers (the flag), and the Supreme Leader explains
                flags.Add("pass_intro");
                double batch = MachineBatch(b), passes = b.machinePasses, n = load - 1;
                var at = b;
                SayWhenQuiet("passes", () => PassIntro(at, batch, n, passes));
            }
            if (s.strain == Genetics.Turbo) b.machineDur *= 0.33f;
            b.machineT = b.machineDur;
            s.act = Act.InMachine;
            s.summonedBy = b;
            s.onBelt = false; s.ridingT = 0;
            s.pos = b.Center;
            s.status = b.def.kind == BKind.GeneLab ? "Inside the Gene Lab. Something hums." : b.def.kind == BKind.Dairy ? "Inside the milking machine" : b.def.kind == BKind.Plant ? "Inside THE MACHINE" : b.def.kind == BKind.Slaughter ? "Inside the Slaughterhouse" : "Inside the shearing machine";
            if (IsVisible(b.Center)) Sfx.Play("thud", 0.2f, 1.6f);
        }

        /// the pass a machine is on now (1 .. its passes rounded up)
        static int MachinePass(Building b)
        {
            int all = Mathf.CeilToInt(b.machinePasses);
            return Mathf.Clamp(1 + Mathf.FloorToInt((1f - b.machineT / Mathf.Max(0.01f, b.machineDur)) * all), 1, all);
        }

        void SimMachine(Building b, float dt, float mult)
        {
            var s = b.inMachine;
            if (s == null)
            {
                bool belt = HasBeltIn(b);
                b.status = !belt ? "Not connected! Run a conveyor INTO it" : !AnyMachineInOpen(b) ? "IN door SHUT: sheep wait on the belt" : "Waiting for sheep on the conveyor";
                return;
            }
            // s.dead also covers "no longer in the flock": every removal from `sheep` sets dead first, and nothing revives a sheep
            if (s.dead || s.act != Act.InMachine) { b.inMachine = null; return; }
            b.machineT -= dt * Mathf.Max(0.2f, mult);
            bool chop = b.def.kind == BKind.Slaughter;
            if (!b.machineDone)
            {
                // a drove in passes: its status changes with the pass, so it is only written then
                int pass = b.machinePasses > 1 ? MachinePass(b) : 0;
                if (pass == 0 || pass != b.machinePassShown)
                {
                    b.status = (b.def.kind == BKind.GeneLab ? (Genetics.Get(b.geneMode) != null && s.strain != b.geneMode ? "Splicing " : "Sampling ") : b.def.kind == BKind.Dairy ? "Milking " : chop ? "Processing " : "Shearing ") + s.name;
                    if (pass > 0) { b.machinePassShown = pass; b.status += " + " + UI.Fmt(s.drove != null ? s.drove.n : 0) + " (pass " + pass + " of " + Mathf.CeilToInt(b.machinePasses) + ")"; }
                }
                if (b.machineDur > 0.5f && IsVisible(b.Center) && fxRng.NextDouble() < dt * (chop ? 3 : 7))
                {
                    if (b.def.kind == BKind.GeneLab) SpawnParticles(b.Center + Vector2.up * 1.1f, new Color(0.5f, 1f, 0.6f), 2, 1.8f);
                    else if (b.def.kind == BKind.Dairy) SpawnParticles(b.Center + Vector2.up * 0.9f, new Color(0.95f, 0.97f, 1f), 2, 1.6f);
                    else if (chop) SpawnParticles(b.Center + Vector2.up * 1.1f, new Color(0.7f, 0.1f, 0.1f), 1, 1.6f);
                    else SpawnParticles(b.Center + Vector2.up * 1.1f, new Color(0.96f, 0.94f, 0.88f), 3, 2.2f);
                    if (fxRng.NextDouble() < 0.25) Sfx.Play(b.def.kind == BKind.Dairy ? "squirt" : chop ? "thud" : "snip", 0.2f);
                }
                if (b.machineT > 0) return;
                if (chop) { b.inMachine = null; b.cyclesDone++; machineSheep = Bump(machineSheep, 1 + (s.drove != null ? s.drove.n : 0)); Slaughter(s, b); return; }
                ProcessInMachine(b, s);
                if (s.dead) { b.inMachine = null; return; } // CHAOS mode is not always kind
            }
            Vector2 pos;
            int door = PickMachineExit(b, s, out pos);
            if (door == ExitJammed) { b.status = "Exit jammed with sheep"; return; }
            if (door == ExitNone)
            {
                b.status = AnySheepExit(b) ? "NO WAY OUT for " + s.name + ": no open OUT door lets " + s.Him + " through (filters!)" : "NO WAY OUT: open an OUT door with a belt";
                b.noExitT += dt;
                return;
            }
            ReleaseFromMachine(b, s, pos, door);
        }

        /// shear or milk the sheep inside (once). Effects show at the machine's front.
        void ProcessInMachine(Building b, Sheep s)
        {
            b.machineDone = true;
            b.cyclesDone++;
            s.pos = b.Door + new Vector2(0, 0.25f);
            bool did = false;
            double follow = s.drove != null ? s.drove.n : 0; // its drove goes through with it, on the same load
            if (b.def.kind == BKind.GeneLab) { ProcessGeneLab(b, s); did = true; }
            else if (b.def.kind == BKind.Dairy)
            {
                double extra = DroveMilk(s, b);
                if (s.male) { if (IsVisible(b.Center)) FloatText(b.Center + Vector2.up * 1.3f, "REJECTED: RAM", new Color(1f, 0.6f, 0.5f)); }
                else if (s.milk >= 0.2f || extra >= 1) { DoMilk(s, b, false, extra); did = true; }
            }
            else
            {
                double extra = DroveShear(s, b);
                if (s.wool >= 0.2f || extra >= 1) { DoShear(s, b, false, extra); did = true; }
            }
            s.pos = b.Center;
            b.machineDid = did;
            if (did) { machineSheep = Bump(machineSheep, 1 + follow); GiveXp(s, 6); }
        }

        const int ExitNone = -1, ExitJammed = -2;
        static Vector2 ExitOffset(int i) { return i == 0 ? new Vector2(0.5f, 0.85f) : i == 1 ? new Vector2(0.12f, 0.35f) : i == 2 ? new Vector2(0.5f, 0.12f) : new Vector2(0.88f, 0.35f); }
        bool FreeGround(int x, int y) { if (!InMap(x, y)) return false; var t = tiles[x, y]; return t.building == null && t.pen == null && t.belt < 0; }

        /// the belt tile a sheep leaving through side i steps onto: in line with where it came in, else the first belt leading away
        bool ExitTile(Building b, int i, out Vector2Int t)
        {
            if (b.machineIn == Opp(i))
            {
                t = SideTile(b, i, Mathf.Clamp(b.machineLane, 0, SideLen(b, i) - 1));
                if (BeltRole(b, t) == 2) return true;
            }
            return SideBelt(b, i, 2, out t);
        }

        /// which open door the sheep inside leaves by (round robin; Smart Door filters apply).
        /// ExitNone: no open way out for this sheep, so it waits inside. ExitJammed: the way out is full, wait.
        int PickMachineExit(Building b, Sheep s, out Vector2 pos)
        {
            int best = -1, bestN = int.MaxValue; bool any = false;
            pos = b.Door;
            for (int i = 0; i < 4; i++)
            {
                if (!SheepExitSide(b, i) || !FilterAllows(Filter(b, i), s)) continue;
                Vector2Int t;
                if (!ExitTile(b, i, out t)) continue;
                any = true;
                var p = new Vector2(t.x, t.y) + ExitOffset(i);
                if (SheepNear(p, 0.5f, s, ref b.nearHint[i])) continue;
                if (b.doorPicks[i] < bestN) { bestN = b.doorPicks[i]; best = i; pos = p; }
            }
            if (best >= 0) { b.doorPicks[best]++; return best; }
            return any ? ExitJammed : ExitNone;
        }

        void ReleaseFromMachine(Building b, Sheep s, Vector2 pos, int door)
        {
            b.inMachine = null;
            b.machineOut = pos;
            s.pos = pos;
            var t = TileAt(pos);
            s.act = Act.Wander; s.summonedBy = null; s.ridingT = 0; s.exitDoor = -1;
            s.beltImmune = 0;
            s.target = s.pos;
            s.status = "Riding to the " + (MachineDoorDestination(b, door) ?? "conveyor");
            if (!b.machineDid || !tutDone) return;
            float danger = b.Danger * AccidentMult(s);
            if (b.safetyUntil > Day) danger *= 0.3f;
            if (SignNear(b)) danger *= 0.9f;
            if (rng.NextDouble() < danger) Accident(s, b);
        }

        /// what a sheep fetches alive at a Market (null = a typical adult, for tooltips)
        public int LiveSheepPrice(Sheep s, Building market)
        {
            int p = s != null ? SheepPrice(s) / 2 : Mathf.Max(25, Mathf.RoundToInt(55 * SheepPriceFactor)) / 2;
            if (s != null && s.Has("charismatic")) p = Mathf.RoundToInt(p * 1.5f);
            if (market != null) p = Mathf.RoundToInt(p * (1f + (market.level - 1) * 0.05f));
            if (PathLevel("market_price") > 0) p = Mathf.RoundToInt(p * (float)PathMult("market_price"));
            if (CoinTier > 0) p = Mathf.RoundToInt(p * CoinTierMult);
            return p;
        }

        /// a sheep rides in through a Market door: sold alive
        void SellAtMarket(Sheep s, Building m)
        {
            double follow = s.drove != null ? s.drove.n : 0; // its drove is sold with it
            double p = LiveSheepPrice(s, m) + DroveLivePrice(s, m);
            int sold0 = soldAlive;
            CreditCoins(p, CoinSrc.LiveSale); totalMade[0] += p; soldAlive = Bump(soldAlive, 1 + follow);
            liveToday += p; soldToday = Bump(soldToday, 1 + follow); // for the day's income measure (Core/Money.cs)
            s.drove = null; Ledger(-(1 + follow));
            RemoveFromJob(s);
            s.act = Act.Hidden; s.dead = true; s.deathCause = "Sold";
            if (s.go != null) { Destroy(s.go); Destroy(s.shadow.gameObject); s.go = null; }
            sheep.Remove(s);
            if (selSheep == s) selSheep = null;
            if (followSheep == s) followSheep = null;
            AddGlobalMod("A friend was sold", -2, 1f);
            if (IsVisible(m.Center)) { FloatText(m.Center + Vector2.up * 1.6f, "SOLD +" + UI.Fmt(p) + "c" + (follow >= 1 ? " (x" + UI.Fmt(follow + 1) + ")" : ""), new Color(1f, 0.86f, 0.35f)); Sfx.Play("coin", 0.5f); Sfx.Baa(0.4f); }
            if (sold0 < 1 && soldAlive >= 1) LeaderPop("You are selling LIVE sheep at the Market. Bold. Every one we sell is wool and milk we never see again.", LExpr.Smug, 6f);
            else if (rng.NextDouble() < 0.25) Log("Sold ^y" + s.name + "^w" + (follow >= 1 ? " and " + UI.Fmt(follow) + " of the herd" : "") + " alive at the Market for " + UI.Fmt(p) + " coins.");
        }

        void GiveXp(Sheep s, float xp)
        {
            s.xp += xp;
            if (s.xp < s.XpToNext || s.level >= 10) return;
            s.xp = 0; s.level++;
            if (IsVisible(s.pos)) FloatText(s.pos + Vector2.up * 1.4f, "LEVEL UP!", new Color(1f, 0.86f, 0.35f));
        }

        /// shutters with a lamp wherever a belt meets a machine: green = open, red = shut
        void UpdateMachineDoors(Building b)
        {
            int n = 0;
            if (b.construct >= 1f && b.go != null)
                for (int i = 0; i < 4; i++)
                {
                    if (!MachineDoorToggleable(b, i)) continue;
                    bool open = MachineDoorOpen(b, i);
                    for (int k = 0; k < SideLen(b, i); k++)
                    {
                        if (BeltRole(b, SideTile(b, i, k)) == 0) continue;
                        SpriteRenderer sr = n < b.doorSprites.Count ? b.doorSprites[n] : null;
                        if (sr == null)
                        {
                            var go = new GameObject("door");
                            go.transform.SetParent(b.go.transform, false);
                            sr = go.AddComponent<SpriteRenderer>();
                            if (n < b.doorSprites.Count) b.doorSprites[n] = sr; else b.doorSprites.Add(sr);
                        }
                        n++;
                        if (!sr.gameObject.activeSelf) sr.gameObject.SetActive(true);
                        sr.sprite = EnvArt.MachineDoor(i == 0 || i == 2, open);
                        int W = b.def.w * 16;
                        Vector2 lp = i == 3 ? new Vector2(0, k + 4 / 16f) : i == 1 ? new Vector2((W - 9) / 16f, k + 4 / 16f)
                                   : i == 0 ? new Vector2(k + 2 / 16f, 0) : new Vector2(k + 2 / 16f, b.def.h - 7 / 16f);
                        sr.transform.localPosition = new Vector3(lp.x, lp.y, 0);
                        sr.sortingOrder = b.sr.sortingOrder + 3;
                    }
                }
            for (int j = n; j < b.doorSprites.Count; j++)
                if (b.doorSprites[j] != null && b.doorSprites[j].gameObject.activeSelf) b.doorSprites[j].gameObject.SetActive(false);
        }

        /// the Slaughterhouse: a sheep goes in, mutton comes out
        public int MuttonYield(Sheep s, Building b)
        {
            float m = s == null ? 6f : s.Lamb ? 3f : 6f + (s.level - 1) * 0.5f;
            if (s != null && s.Has("glutton")) m += 3;
            if (s != null && s.Has("strong")) m += 2;
            if (s != null && s.variant == Variant.TwoHeaded) m += 4;
            if (s != null && s.strain == Genetics.Jumbo) m *= 2f;
            if (s != null && s.strain == Genetics.Wolf) m *= 1.5f;
            if (s != null && s.strain == Genetics.Teacup) m *= 0.5f;
            if (b != null) m *= BuildingDef.At(MuttonByLevel, b.level);
            return Mathf.Max(2, Mathf.RoundToInt(m));
        }
        static readonly float[] MuttonByLevel = { 1f, 1.25f, 1.5f, 2f, 2.6f };

        void Slaughter(Sheep s, Building b)
        {
            double follow = s.drove != null ? s.drove.n : 0; // its drove goes with it
            double meat = MuttonYield(s, b) + DroveMutton(s, b);
            bool belted = Output(b, Res.Mutton, (float)meat);
            if (!belted) FlyFromWorld("mutton", b.Center + Vector2.up, (int)System.Math.Max(1, System.Math.Min(5, System.Math.Floor(meat / 3))));
            int sl0 = slaughtered;
            slaughtered = Bump(slaughtered, 1 + follow);
            slaughteredToday = Bump(slaughteredToday, 1 + follow); // for the day's income measure (Core/Money.cs)
            GainDna(1 + (s.strain != 0 || s.variant != Variant.None ? 2 : 0) + follow * (s.strain != 0 ? 3 : 1), b.Center);
            QueueRebirth(s);
            s.drove = null; Ledger(-(1 + follow));
            if (!s.herdHand) graveyard.Add(new DeadRecord { name = s.name, cause = "Became mutton at the " + b.def.name + "." + (follow >= 1 ? " " + UI.Fmt(follow) + " of the herd went with " + s.Him + "." : ""), level = s.level, day = Day, age = s.ageDays, traits = TraitList(s) });
            RemoveFromJob(s);
            s.dead = true; s.deathCause = "Mutton"; s.deathDay = Day; s.act = Act.Hidden;
            if (s.go != null) { Destroy(s.go); Destroy(s.shadow.gameObject); s.go = null; }
            sheep.Remove(s);
            if (follow >= 1 && s.strain == Genetics.Phoenix) HerdRebirths(s.pen, follow);
            if (selSheep == s) selSheep = null;
            if (followSheep == s) followSheep = null;
            AddGlobalMod("The Slaughterhouse is busy", -4, 1f);
            if (IsVisible(b.Center))
            {
                FloatText(b.Center + Vector2.up * 1.8f, "MUTTON +" + UI.Fmt(meat) + (follow >= 1 ? " (x" + UI.Fmt(follow + 1) + ")" : ""), new Color(1f, 0.55f, 0.5f));
                SpawnParticles(b.Center + Vector2.up * 0.9f, new Color(0.72f, 0.1f, 0.1f), 6, 2f);
                Sfx.Play("crunch", 0.4f, 0.8f);
            }
            if (rng.NextDouble() < 0.12) AddDecal("blood", b.Door + new Vector2((float)rng.NextDouble() - 0.5f, -0.1f));
            if ((sl0 < 1 && slaughtered >= 1) || rng.NextDouble() < 0.25) Log("^r" + s.name + "^w" + (follow >= 1 ? " and " + UI.Fmt(follow) + " of the herd" : "") + " became mutton. The flock is pretending not to notice.");
        }

        void EjectFromMachine(Building b)
        {
            var s = b.inMachine;
            b.inMachine = null;
            if (s == null || s.dead) return;
            s.act = Act.Wander; s.summonedBy = null;
            s.pos = b.Door + new Vector2(0, -0.35f);
        }

        // ---------------- blueprints (where the Supreme Leader wants things) ----------------
        public readonly List<Blueprint> blueprints = new List<Blueprint>();
        public readonly List<Vector3Int> beltPlan = new List<Vector3Int>(); // x, y, dir

        public Blueprint BlueprintFor(BKind k)
        {
            foreach (var bp in blueprints) if (bp.kind == k) return bp;
            return null;
        }

        /// put a blueprint at (x, y), or at the nearest free spot if that one is taken
        public void AddBlueprint(BKind k, int x, int y)
        {
            blueprints.RemoveAll(b => b.kind == k);
            var d = Buildings.Get(k);
            if (!CanPlace(d, x, y))
            {
                bool found = false;
                for (int r = 1; r < 12 && !found; r++)
                    for (int dx = -r; dx <= r && !found; dx++)
                        for (int dy = -r; dy <= r && !found; dy++)
                        {
                            if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue;
                            if (CanPlace(d, x + dx, y + dy)) { x += dx; y += dy; found = true; }
                        }
                if (!found) return;
            }
            blueprints.Add(new Blueprint { kind = k, x = x, y = y });
        }

        public int PlanDirAt(int x, int y)
        {
            foreach (var v in beltPlan) if (v.x == x && v.y == y) return v.z;
            return -1;
        }

        public bool PlanDone()
        {
            foreach (var v in beltPlan) if (BeltDirAt(v.x, v.y) != v.z) return false;
            return true;
        }

        public int PlanLeft()
        {
            int n = 0;
            foreach (var v in beltPlan) if (BeltDirAt(v.x, v.y) != v.z) n++;
            return n;
        }

        Vector3Int? FirstMissingPlan()
        {
            foreach (var v in beltPlan) if (BeltDirAt(v.x, v.y) != v.z) return v;
            return null;
        }

        void DrawBlueprints(float sc)
        {
            if (!UI.Repaint || (blueprints.Count == 0 && beltPlan.Count == 0)) return;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
            foreach (var bp in blueprints)
            {
                var d = Buildings.Get(bp.kind);
                Vector2 a = ToGui(new Vector2(bp.x, bp.y + d.h), sc), c = ToGui(new Vector2(bp.x + d.w, bp.y), sc);
                var r = Rect.MinMaxRect(Mathf.Round(a.x), Mathf.Round(a.y), Mathf.Round(c.x), Mathf.Round(c.y));
                UI.Fill(r, new Color(1f, 0.86f, 0.35f, 0.10f + 0.12f * pulse));
                UI.Frame(r, new Color(1f, 0.86f, 0.35f, 0.55f + 0.45f * pulse), 2);
                string t = d.name.ToUpper();
                float tw = PixelFont.Width(t);
                if (tw + 6 < r.width) PixelFont.DrawOutlined(Mathf.Round(r.center.x - tw / 2), Mathf.Round(r.center.y - 4), t, UI.Gold);
            }
            foreach (var v in beltPlan)
            {
                if (BeltDirAt(v.x, v.y) == v.z) continue;
                Vector2 a = ToGui(new Vector2(v.x, v.y + 1), sc), c = ToGui(new Vector2(v.x + 1, v.y), sc);
                var r = Rect.MinMaxRect(Mathf.Round(a.x) + 1, Mathf.Round(a.y) + 1, Mathf.Round(c.x) - 1, Mathf.Round(c.y) - 1);
                UI.Fill(r, new Color(1f, 0.86f, 0.35f, 0.18f + 0.2f * pulse));
                UI.Frame(r, new Color(1f, 0.86f, 0.35f, 0.6f + 0.4f * pulse), 1);
                var dv = Dir(v.z);
                Vector2 m = r.center - new Vector2(dv.x, -dv.y) * 2f;
                for (int i = 0; i < 4; i++)
                {
                    float hw = 3 - i;
                    Vector2 q = m + new Vector2(dv.x, -dv.y) * i;
                    if (dv.x != 0) UI.Fill(new Rect(Mathf.Round(q.x), Mathf.Round(q.y - hw), 1, hw * 2 + 1), UI.Ink);
                    else UI.Fill(new Rect(Mathf.Round(q.x - hw), Mathf.Round(q.y), hw * 2 + 1, 1), UI.Ink);
                }
            }
        }

        // ---------------- door buttons in the world ----------------
        readonly List<Rect> bubbleRects = new List<Rect>();
        public readonly List<Rect> bubbleRectsPrev = new List<Rect>();

        bool MouseOverPanels()
        {
            Vector2 m = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            foreach (var r in UI.blockersPrev) if (r.Contains(m)) return true;
            return false;
        }

        static string Short(string s, int n) { s = (s ?? "?").ToUpper(); return s.Length <= n ? s : s.Substring(0, n - 1) + "."; }

        void DrawGateLabels(float sc)
        {
            if (UI.Repaint) { bubbleRectsPrev.Clear(); bubbleRectsPrev.AddRange(bubbleRects); bubbleRects.Clear(); }
            if (pixelScale < 2) return;
            bool overPanels = MouseOverPanels();
            bool prevInput = UI.inputEnabled;
            bool doorsOn = UiOn("gate");
            var placed = new List<Rect>(); // labels drawn so far, so machine door tags can dodge them
            DrawBuildingPlates(sc, placed);
            DrawDroveBadges(sc);
            foreach (var b in buildings)
            {
                if (!IsRoom(b) || !RoomActive(b)) continue;
                // room badge: name and how full it is
                {
                    Vector2 tl = ToGui(new Vector2(b.x + 0.1f, b.y + b.def.h + 0.95f), sc);
                    if (tl.x > -80 && tl.y > -20 && tl.x < W && tl.y < H) // built only for a badge on screen
                    {
                        string badge = HerdIn(b) >= 1 ? RoomBadge(b) : (IsLot(b) ? "FEEDING LOT " : "PEN ") + b.penCount + "/" + RoomCapacity(b) + (b.penHungry > 0 ? "  ^o" + b.penHungry + " hungry" : "");
                        PixelFont.DrawOutlined(Mathf.Round(tl.x), Mathf.Round(tl.y), badge, Inside(b) >= RoomCapacity(b) ? UI.Bad : UI.Text);
                    }
                }
                if (!doorsOn) continue;
                for (int i = 0; i < 4; i++)
                {
                    if (!DoorConnected(b, i)) continue;
                    bool open = b.doorOpen[i];
                    string label = open ? "CLOSE" : "OPEN";
                    string dest = DoorDestination(b, i);
                    string sub = "TO " + Short(dest, 16);
                    int f = Filter(b, i);
                    if (f > 0) sub += " (" + FilterNames[f] + ")";
                    float bw = Mathf.Max(PixelFont.Width(label) + 22, PixelFont.Width(sub) + 8);
                    Rect r;
                    var dt = DoorTile(b, i);
                    if (i == 0 || i == 2)
                    {
                        Vector2 g = ToGui(new Vector2(dt.x, (i == 0 ? b.y : b.y + b.def.h) + 0.3f), sc);
                        r = new Rect(Mathf.Round(g.x - 6 - bw), Mathf.Round(g.y - 9), bw, 16);
                    }
                    else
                    {
                        Vector2 g = ToGui(new Vector2(dt.x + 0.5f, dt.y + 1.75f), sc);
                        r = new Rect(Mathf.Round(g.x - bw / 2), Mathf.Round(g.y - 16), bw, 16);
                    }
                    if (r.xMax < -10 || r.y < -30 || r.x > W + 10 || r.y > H + 10) continue;
                    var ir = new Rect(r.x, r.yMax + 1, bw, 11);
                    placed.Add(r); placed.Add(ir);
                    if (UI.Repaint) bubbleRects.Add(new Rect(r.x * UI.scale, r.y * UI.scale, r.width * UI.scale, (r.height + 12) * UI.scale));
                    UI.Mark("door_" + b.id + "_" + i, r);
                    UI.inputEnabled = prevInput && !overPanels;
                    string tip = (open ? "CLOSE" : "OPEN") + " THE " + DoorNames[i] + " DOOR\n" + (open ? "Sheep stay in the " + b.def.name + "." : "Sheep leave the " + b.def.name + " here and ride the conveyor to: " + (dest ?? "?") + ".")
                                 + (IsLot(b) ? "\nOnly FED sheep leave a Feeding Lot." : "") + (f > 0 ? "\nFilter: " + FilterTips[f] : "");
                    if (UI.ButtonEx(r, label, open ? "close" : "play", null, true, tip, open ? BtnStyle.Danger : BtnStyle.Primary)) ToggleDoor(b, i);
                    UI.inputEnabled = prevInput;
                    UI.Fill(ir, new Color(0.07f, 0.055f, 0.047f, 0.82f));
                    PixelFont.DrawAligned(ir, sub, open ? UI.Good : UI.Dim, 1, 1, false);
                }
            }
            if (!doorsOn) return;
            // machine doors: a small IN / OUT tag by each door, green = open, red = shut. Click to flip it.
            foreach (var b in buildings)
            {
                if (!HasBeltDoors(b) || b.construct < 1f) continue;
                for (int i = 0; i < 4; i++)
                {
                    if (!MachineDoorToggleable(b, i)) continue;
                    string role = MachineDoorRole(b, i);
                    Vector2Int t;
                    if (!SideBelt(b, i, role == "IN" ? 1 : 2, out t)) continue;
                    bool open = MachineDoorOpen(b, i);
                    int k = i == 0 || i == 2 ? t.x - b.x : t.y - b.y;
                    // side doors: on the wall above the shutter; front and back doors: on the belt just outside
                    Vector2 wp = i == 3 ? new Vector2(b.x + 0.3f, b.y + k + 1.3f) : i == 1 ? new Vector2(b.x + b.def.w - 0.3f, b.y + k + 1.3f)
                               : i == 0 ? new Vector2(b.x + k + 0.5f, b.y - 0.35f) : new Vector2(b.x + k + 0.5f, b.y + b.def.h + 0.35f);
                    Vector2 g = ToGui(wp, sc);
                    string label = role == "OUT" && !open ? (MachineGoods(b) ?? "shut").ToUpper() : role; // a shut OUT door is a goods hatch
                    float bw = PixelFont.Width(label) + 12, bh = 11;
                    var r = new Rect(Mathf.Round(g.x - bw / 2), Mathf.Round(g.y - bh / 2), bw, bh);
                    for (int tries = 0; tries < 4; tries++)
                    {
                        bool hit = false;
                        foreach (var o in placed) if (o.Overlaps(r)) { hit = true; break; }
                        if (!hit) break;
                        r.y -= bh + 1;
                    }
                    placed.Add(r);
                    if (r.xMax < -10 || r.y < -30 || r.x > W + 10 || r.y > H + 10) continue;
                    if (UI.Repaint) bubbleRects.Add(new Rect(r.x * UI.scale, r.y * UI.scale, r.width * UI.scale, r.height * UI.scale));
                    UI.Mark("mdoor_" + b.id + "_" + i, r);
                    UI.inputEnabled = prevInput && !overPanels;
                    bool hover = UI.Hover(r);
                    if (UI.Repaint)
                    {
                        Color lamp = open ? UI.Good : UI.Bad;
                        UI.Fill(r, UI.Ink);
                        UI.Fill(UI.Inset(r, 1), hover ? UI.Hi2 : UI.BgDark);
                        UI.Fill(new Rect(r.x + 3, r.y + 3, 4, 5), lamp);
                        UI.Fill(new Rect(r.x + 3, r.y + 3, 4, 1), Color.Lerp(lamp, Color.white, 0.5f));
                        PixelFont.Draw(r.x + 9, r.y + 2, label, lamp, 1, false);
                    }
                    if (UI.Clickable(r, hover ? MachineDoorTip(b, i) : null, false)) ToggleMachineDoor(b, i);
                    UI.inputEnabled = prevInput;
                }
            }
        }
    }
}
