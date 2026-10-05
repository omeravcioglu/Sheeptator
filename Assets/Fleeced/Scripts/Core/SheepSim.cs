using System.Collections.Generic;
using UnityEngine;

namespace Fleeced
{
    public partial class Game
    {
        GameObject sheepRoot;
        static readonly string[] Chatter = { "baa", "baa", "baa", "BAA", "baa?", "meh", "baaa", "BAAAA", "grass", "...", "baa!", "mbaa" };
        static readonly string[] SadChatter = { "help", "why", "baa...", "no", "it's cold", "mom?", "i saw things" };
        static readonly string[] WorkChatter = { "*clank*", "baa (work)", "ow", "is this safe", "*bonk*", "productivity!", "my hoof" };

        // ---------------- creation ----------------
        public Sheep CreateSheep(Vector2 pos, Sheep pa = null, Sheep pb = null, bool? forceMale = null)
        {
            var s = new Sheep();
            s.id = NextId();
            s.name = Names.Random(rng, usedNames);
            usedNames.Add(s.name);
            s.male = forceMale ?? rng.NextDouble() < 0.5;
            s.pos = pos; s.target = pos;
            s.born = Day;
            s.wool = 0.3f + (float)rng.NextDouble() * 0.6f;
            s.mood = 60 + rng.Next(15);
            s.woolCol = RandomWoolCol();
            s.faceCol = rng.Next(10) < 6 ? 0 : rng.Next(4);
            s.baseExpr = RandomExpr();
            s.statusTimer = (float)rng.NextDouble() * 5;
            s.baaTimer = 4 + (float)rng.NextDouble() * 20;
            if (pa != null && pb != null)
            {
                s.generation = Mathf.Max(pa.generation, pb.generation) + 1;
                s.parents = pa.name + " & " + pb.name;
                s.ageDays = 0;
                s.wool = 0.15f;
                s.hunger = 0;
                Inherit(s, pa, pb);
            }
            else
            {
                int n = 1 + rng.Next(3);
                for (int i = 0; i < n; i++) AddRandomTrait(s);
                if (rng.NextDouble() < 0.015) s.variant = RandomVariant();
            }
            if (s.Has("stupid") && rng.NextDouble() < 0.6) s.baseExpr = rng.NextDouble() < 0.5 ? Expr.Derp : Expr.Tongue;
            sheep.Add(s);
            SpawnSheepGO(s);
            AssignPen(s);
            return s;
        }

        int RandomWoolCol()
        {
            int r = rng.Next(100);
            if (r < 55) return 0; if (r < 75) return 1; if (r < 87) return 3; if (r < 96) return 2; return 4;
        }
        Expr RandomExpr()
        {
            int r = rng.Next(100);
            if (r < 62) return Expr.Normal; if (r < 82) return Expr.Derp; return Expr.Tongue;
        }
        Variant RandomVariant()
        {
            Variant[] v = { Variant.Golden, Variant.Black, Variant.Black, Variant.Rainbow, Variant.ThreeEyed, Variant.Spiky, Variant.TwoHeaded };
            return v[rng.Next(v.Length)];
        }

        public void AddRandomTrait(Sheep s)
        {
            for (int tries = 0; tries < 20; tries++)
            {
                var t = Traits.Natural[rng.Next(Traits.Natural.Length)];
                if (!Traits.Conflicts(s.traits, t)) { s.traits.Add(t); return; }
            }
        }

        void Inherit(Sheep s, Sheep a, Sheep b)
        {
            bool sel = tech.Contains("selective");
            var pool = new List<string>(a.traits);
            foreach (var t in b.traits) if (!pool.Contains(t)) pool.Add(t);
            foreach (var t in pool)
            {
                if (t == "clone") continue;
                var info = Traits.Get(t);
                float chance = 0.42f;
                if (sel) chance = info.kind > 0 ? 0.7f : info.kind < 0 ? 0.25f : 0.45f;
                if (a.traits.Contains(t) && b.traits.Contains(t)) chance += 0.3f;
                if (s.traits.Count < 4 && rng.NextDouble() < chance && !Traits.Conflicts(s.traits, t)) s.traits.Add(t);
            }
            if (rng.NextDouble() < 0.18 || s.traits.Count == 0) AddRandomTrait(s);
            // looks
            s.woolCol = rng.NextDouble() < 0.5 ? a.woolCol : b.woolCol;
            s.faceCol = rng.NextDouble() < 0.5 ? a.faceCol : b.faceCol;
            float rare = 0.025f * (tech.Contains("advanced_breeding") ? 3f : 1f);
            if (a.variant != Variant.None && rng.NextDouble() < 0.35) s.variant = a.variant;
            else if (b.variant != Variant.None && rng.NextDouble() < 0.35) s.variant = b.variant;
            else if (rng.NextDouble() < rare) s.variant = RandomVariant();
            InheritStrain(s, a, b);
        }

        void SpawnSheepGO(Sheep s)
        {
            if (sheepRoot == null) { sheepRoot = new GameObject("Sheep"); sheepRoot.transform.SetParent(worldRoot.transform); }
            s.go = new GameObject(s.name);
            s.go.transform.SetParent(sheepRoot.transform);
            s.sr = s.go.AddComponent<SpriteRenderer>();
            var sh = new GameObject("shadow");
            sh.transform.SetParent(sheepRoot.transform);
            s.shadow = sh.AddComponent<SpriteRenderer>();
            s.shadow.sprite = EnvArt.Shadow();
            s.shadow.color = new Color(0, 0, 0, 0.22f);
            s.shadow.sortingOrder = -30500;
            UpdateSheepVisual(s, 0);
        }

        public static string VariantName(Variant v)
        {
            switch (v)
            {
                case Variant.Golden: return "Golden Fleece";
                case Variant.Black: return "Black Sheep";
                case Variant.Rainbow: return "Rainbow";
                case Variant.ThreeEyed: return "Three-Eyed";
                case Variant.Spiky: return "Spiky";
                case Variant.TwoHeaded: return "Two-Headed";
            }
            return "";
        }
        public static string VariantDesc(Variant v)
        {
            switch (v)
            {
                case Variant.Golden: return "Golden wool. Shears for 3x wool. Grows slowly.";
                case Variant.Black: return "Edgy. +50% accidents, but +10 mood (it's an aesthetic).";
                case Variant.Rainbow: return "Shears for 2x wool. Nobody knows why.";
                case Variant.ThreeEyed: return "Research x2. Sees things.";
                case Variant.Spiky: return "Guard x2. Painful to shear.";
                case Variant.TwoHeaded: return "Works x1.6. Eats x2. Argues with itself.";
            }
            return "";
        }

        // ---------------- efficiency & mood ----------------
        public float WorkEff(Sheep s, Cat cat, bool dangerous)
        {
            float e = 1f;
            foreach (var t in s.traits)
            {
                switch (t)
                {
                    case "lazy": e *= 0.6f; break;
                    case "fast": e *= 1.35f; break;
                    case "workaholic": e *= 1.2f; break;
                    case "sickly": e *= 0.85f; break;
                    case "clone": e *= 0.9f; break;
                    case "genius": if (cat == Cat.Research) e *= 2f; else if (cat == Cat.Farm) e *= 0.6f; else if (cat == Cat.Craft) e *= 1.2f; break;
                    case "stupid": if (cat == Cat.Research) e *= 0.3f; else if (cat == Cat.Labor || cat == Cat.Graze || cat == Cat.Build) e *= 1.15f; break;
                    case "strong": if (cat == Cat.Industry || cat == Cat.Labor) e *= 1.4f; else if (cat == Cat.Build) e *= 1.5f; else if (cat == Cat.Guard) e *= 1.3f; break;
                    case "coward": if (cat == Cat.Guard) e *= 0.3f; else if (dangerous) e *= 0.6f; break;
                    case "brave": if (cat == Cat.Guard) e *= 1.6f; else if (dangerous) e *= 1.15f; break;
                    case "charismatic": if (cat == Cat.Commerce) e *= 1.6f; break;
                    case "greedy": if (cat == Cat.Commerce) e *= 1.3f; break;
                    case "fertile": if (cat == Cat.Breed) e *= 2f; break;
                    case "philosopher": if (cat == Cat.Research) e *= 1.4f; else e *= 0.85f; break;
                    case "messy": if (cat == Cat.Craft) e *= 0.6f; else if (cat == Cat.Wool) e *= 0.8f; break;
                    case "carrots": if (cat == Cat.Farm) e *= 1.4f; break;
                }
            }
            if (s.variant == Variant.ThreeEyed && cat == Cat.Research) e *= 2f;
            if (s.variant == Variant.Spiky && cat == Cat.Guard) e *= 2f;
            if (s.variant == Variant.TwoHeaded) e *= 1.6f;
            e *= 1f + (s.level - 1) * 0.08f;
            e *= 0.55f + s.mood / 100f * 0.75f;
            if (s.injured > 0) e *= 0.5f;
            if (s.flattened > 0) e *= 0.7f;
            if (s.hunger > 1.5f) e *= 0.6f;
            return e;
        }

        public float JobFit(Sheep s, Job j)
        {
            var info = Jobs.Get(j);
            bool dangerous = j == Job.Operator || j == Job.Shearer || j == Job.Processor;
            float e = WorkEff(s, info.cat, dangerous);
            // remove mood/level/injury so fit is about traits
            e /= (0.55f + s.mood / 100f * 0.75f);
            e /= 1f + (s.level - 1) * 0.08f;
            if (s.injured > 0) e /= 0.5f;
            if (s.flattened > 0) e /= 0.7f;
            if (s.hunger > 1.5f) e /= 0.6f;
            return e;
        }

        public float AccidentMult(Sheep s)
        {
            float m = 1f;
            if (s.Has("accident")) m *= 2.5f;
            if (s.Has("stupid")) m *= 1.5f;
            if (s.Has("messy")) m *= 1.3f;
            if (s.Has("lucky")) m *= 0.5f;
            if (s.Has("brave")) m *= 1.2f;
            if (s.Has("coward")) m *= 0.8f;
            if (s.variant == Variant.Black) m *= 1.5f;
            return m;
        }

        public float FoodMult(Sheep s)
        {
            float m = 1f;
            if (s.Has("glutton")) m *= 2f;
            if (s.Has("sickly")) m *= 1.2f;
            if (s.variant == Variant.TwoHeaded) m *= 2f;
            if (s.strain == Genetics.Jumbo) m *= 1.5f;
            if (s.strain == Genetics.Teacup) m *= 0.4f;
            if (s.Lamb) m *= 0.5f;
            return m;
        }

        public float ComputeMoodTarget(Sheep s, List<string> reasons)
        {
            float t = 58;
            System.Action<string, float> add = (l, v) => { t += v; if (reasons != null && Mathf.Abs(v) >= 0.5f) reasons.Add((v > 0 ? "^g+" : "^r") + Mathf.RoundToInt(v) + " ^w" + l); };
            if (s.Has("lazy")) add("Lazy (content)", 5);
            if (s.Has("stupid")) add("Blissfully stupid", 8);
            if (s.Has("loyal")) add("Loyal", 3);
            if (s.Has("philosopher")) add("Existential dread", -3);
            if (s.variant == Variant.Black) add("Edgy aesthetic", 10);
            if (s.hunger < 0.6f) add("Well fed", 8);
            else if (s.hunger > 2f) add("STARVING", -40);
            else if (s.hunger > 1.2f) add("Hungry", -18);
            if (s.homeless) add("Homeless", -20);
            if (wagePolicy == 1 && s.job != Job.Grazer) add("Carrot wages", 6);
            if (wagePolicy == 2 && s.job != Job.Grazer) add("Coin wages", 12);
            if (s.Has("greedy") && wagePolicy != 2 && s.job != Job.Grazer) add("Greedy: wants coins", -10);
            if (decorMood != 0) add("Decorations", decorMood);
            if (s.workplace != null)
            {
                float d = s.workplace.Danger;
                if (d >= 0.03f) add("Terrifying workplace", -12);
                else if (d >= 0.015f) add("Dangerous workplace", -7);
                else if (d >= 0.008f) add("Risky workplace", -3);
                if (s.workplace.unsafeMode) add("Safety guards removed", -6);
                if (s.workplace.buffUntil > Day && s.workplace.buffMult > 1.05f) add("Blamed & pressured", -5);
            }
            if (s.job == Job.Grazer && !s.Lamb) add("Grazing life", 5);
            if (s.strain == Genetics.Gold) add("Feels expensive", 4);
            else if (s.strain == Genetics.Glow) add("Feels warm (radioactive)", -3);
            else if (s.strain == Genetics.Silk) add("Silky smooth", 3);
            else if (s.strain == Genetics.Jumbo) add("Big boned", 2);
            else if (s.strain == Genetics.Cream) add("Creamy", 2);
            else if (s.strain == Genetics.Turbo) add("Zoom zoom", 2);
            else if (s.strain == Genetics.Teacup) add("Tiny and adored", 5);
            else if (s.strain == Genetics.Candy) add("Sugar rush", 3);
            else if (s.strain == Genetics.Choco) add("Chocolatey", 2);
            else if (s.strain == Genetics.Wolf) add("Hungry for... something", -2);
            else if (s.strain == Genetics.Sparky) add("Static cling", -1);
            else if (s.strain == Genetics.Splitter) add("Feels like two sheep", 1);
            else if (s.strain == Genetics.Phoenix) add("Eternal", 4);
            if (s.pen != null && s.job == Job.Grazer && s.pen.penMembers > RoomCapacity(s.pen)) add("Crowded " + (IsLot(s.pen) ? "feeding lot" : "pen"), -8);
            if (Season == 3 && s.wool < 0.3f) add("Freezing (shaved in winter)", -12);
            if (Season == 1 && s.wool > 0.98f) add("Too fluffy for summer", -6);
            if (s.injured > 0) add("Injured", -10);
            if (s.flattened > 0) add("Flattened", -5);
            if (s.burned > 0) add("Slightly on fire", -6);
            if (s.sick > 0) add("Sick", -10);
            if (s.Has("carrots") && HasBuildingLevel(BKind.Farm, 2)) add("CARROTS EXIST", 8);
            foreach (var m in s.mods) add(m.label, m.value);
            foreach (var m in globalMods) add(m.label, m.value);
            if (s.Has("dramatic")) t = 50 + (t - 50) * 1.6f;
            return Mathf.Clamp(t, 0, 100);
        }

        public void AddMod(Sheep s, string label, float v, float days)
        {
            s.mods.RemoveAll(m => m.label == label);
            s.mods.Add(new MoodMod(label, v, Day + days));
        }
        public List<MoodMod> globalMods = new List<MoodMod>();
        public void AddGlobalMod(string label, float v, float days)
        {
            globalMods.RemoveAll(m => m.label == label);
            globalMods.Add(new MoodMod(label, v, Day + days));
        }

        // ---------------- per-tick sim ----------------
        void SimSheep(Sheep s, float dt)
        {
            if (s.dead)
            {
                s.actTimer += dt;
                return;
            }
            if (s.act == Act.Hidden) return;

            float dd = dt / DayLength;
            s.ageDays += dd;
            // timers (in days)
            s.injured = Mathf.Max(0, s.injured - dd);
            s.flattened = Mathf.Max(0, s.flattened - dd);
            s.burned = Mathf.Max(0, s.burned - dd);
            s.muddy = Mathf.Max(0, s.muddy - dd);
            s.bloody = Mathf.Max(0, s.bloody - dd);
            s.refuse = Mathf.Max(0, s.refuse - dd);
            s.sick = Mathf.Max(0, s.sick - dd);
            s.tremble = Mathf.Max(0, s.tremble - dt);
            s.unconscious = Mathf.Max(0, s.unconscious - dt);
            s.mods.RemoveAll(m => m.until < Day);

            // hunger & eating. Lambs drink milk. Room sheep only eat in a Feeding Lot (or by grazing);
            // workers and free-roaming sheep eat from the store.
            if (!s.Lamb) s.hunger += dd * 1.0f;
            if (s.hunger >= 0.5f && s.act != Act.Eat)
            {
                bool roomSheep = s.pen != null && s.job == Job.Grazer;
                float portion = 2f * FoodMult(s);
                if (res[(int)Res.Food] >= portion)
                {
                    bool idle = s.act == Act.Wander || s.act == Act.Graze || s.act == Act.Idle;
                    var trough = idle && !s.onBelt && s.hunger < 1.6f && s.unconscious <= 0 ? TroughFor(s) : null;
                    if (trough != null && s.hunger >= 0.6f)
                    {
                        s.act = Act.Eat; s.eatAt = trough;
                        s.target = new Vector2(trough.x + 0.3f + (float)rng.NextDouble() * (trough.def.w - 0.6f), trough.y - 0.25f);
                    }
                    else if (!roomSheep && (trough == null || s.hunger >= 1.6f || !idle))
                    {
                        res[(int)Res.Food] -= portion; foodEaten += portion; s.hunger -= 0.5f;
                    }
                }
            }
            if (!tutDone) s.hunger = Mathf.Min(s.hunger, 3.5f);
            if (s.hunger > 4.5f) { Kill(s, "Starved. There was grass. Somewhere."); return; }

            // wool
            float wg = 0.55f;
            if (s.Has("fluffy")) wg *= 1.6f;
            if (s.Has("sickly")) wg *= 0.8f;
            if (s.variant == Variant.Golden) wg *= 0.75f;
            if (s.job == Job.Grazer) wg *= 1.2f;
            if (s.Has("workaholic")) wg *= 1.2f;
            if (s.Has("clone")) wg *= 0.9f;
            if (s.strain == Genetics.Gold) wg *= 0.7f;
            if (s.strain == Genetics.Candy) wg *= 1.2f;
            if (HasGene("big_fleece")) wg *= 1.25f;
            if (s.mood > 75) wg *= 1.15f;
            if (s.hunger > 1.5f) wg *= 0.2f;
            s.wool = Mathf.Min(1.25f, s.wool + dd * wg);

            // milk (ewes only)
            if (!s.male && !s.Lamb)
            {
                float mg = 0.8f;
                if (s.Has("fertile")) mg *= 1.2f;
                if (s.Has("workaholic")) mg *= 1.2f;
                if (HasGene("rich_milk")) mg *= 1.25f;
                if (s.mood > 70) mg *= 1.15f;
                if (s.hunger > 1.5f) mg *= 0.2f;
                s.milk = Mathf.Min(1f, s.milk + dd * mg);
            }

            // mood
            s.moodTarget = ComputeMoodTarget(s, null);
            float mrate = 30f * dd * (s.Has("dramatic") ? 2f : 1f);
            s.mood = Mathf.MoveTowards(s.mood, s.moodTarget, mrate);
            if (s.Has("loyal")) s.mood = Mathf.Max(s.mood, 20);

            // messy trail
            if (s.Has("messy") && rng.NextDouble() < dt * 0.03) AddDecal(rng.NextDouble() < 0.5 ? "poop" : "mud", s.pos);

            // conveyor belts carry idle sheep. Into machines, sometimes.
            if ((s.act == Act.Wander || s.act == Act.Graze || s.act == Act.Idle) && s.unconscious <= 0 && RideBelt(s, dt)) return;

            // behaviour
            switch (s.act)
            {
                case Act.Eat:
                    {
                        var tr = s.eatAt;
                        if (tr == null || !buildings.Contains(tr)) { s.act = Act.Wander; break; }
                        s.status = "Walking to the trough";
                        if (!MoveTo(s, s.target, dt, 1f)) return;
                        float portion = 2f * FoodMult(s);
                        int meals = 0;
                        while (s.hunger >= 0.3f && res[(int)Res.Food] >= portion && meals < 3) { res[(int)Res.Food] -= portion; s.hunger -= 0.5f; meals++; }
                        s.hunger = Mathf.Max(0, s.hunger);
                        if (meals > 0 && IsVisible(s.pos))
                        {
                            SpawnParticles(s.pos + new Vector2(s.faceRight ? 0.4f : -0.4f, 0.35f), new Color(0.55f, 0.75f, 0.3f), 4, 1.2f);
                            if (rng.NextDouble() < 0.35) SpeechText(s.pos + new Vector2(0.2f, 1.3f), rng.NextDouble() < 0.5 ? "nom" : "*crunch*");
                            if (rng.NextDouble() < 0.25) Sfx.Play("nom", 0.25f);
                        }
                        s.eatAt = null;
                        s.act = Act.Wander;
                        s.target = PickGrazeTarget(s);
                        return;
                    }
                case Act.Launched: SimLaunched(s, dt); return;
                case Act.Summoned:
                    {
                        var b = s.summonedBy;
                        if (b == null || !buildings.Contains(b) || (b.client != s && !b.clients.Contains(s))) { s.act = Act.Wander; s.summonedBy = null; break; }
                        int ci = Mathf.Max(0, b.clients.IndexOf(s));
                        Vector2 dest = b.Door + new Vector2(b.def.kind == BKind.Plant ? -b.def.w / 2f + 0.4f : 0.15f, -0.1f)
                                       + new Vector2(((ci % 3) - 1) * 0.6f, -(ci / 3) * 0.45f);
                        s.status = b.def.kind == BKind.Dairy ? "Being called for milking" : b.def.kind == BKind.Funnel ? "Being loaded onto the belt" : "Being called for shearing";
                        if (MoveTo(s, dest, dt, 1.15f)) { s.act = Act.BeingSheared; }
                        return;
                    }
                case Act.BeingSheared:
                    s.status = s.summonedBy != null && s.summonedBy.def.kind == BKind.Plant ? "Inside THE MACHINE" : s.summonedBy != null && s.summonedBy.def.kind == BKind.Dairy ? "Being milked" : "Being sheared";
                    if (s.summonedBy == null || !buildings.Contains(s.summonedBy)) s.act = Act.Wander;
                    return;
                case Act.InMachine:
                    if (s.summonedBy == null || !buildings.Contains(s.summonedBy) || s.summonedBy.inMachine != s)
                    {
                        s.act = Act.Wander;
                        if (s.summonedBy != null) s.pos = s.summonedBy.Door + new Vector2(0, -0.35f);
                        s.summonedBy = null;
                        break;
                    }
                    return;
            }

            if (s.unconscious > 0) { s.status = "Unconscious"; s.act = Act.Idle; return; }

            // pens: grazers live inside, the gate decides when they go out to the machines
            if (PenBehaviour(s, dt)) return;

            bool working = s.CanWork && s.job != Job.Grazer;
            if (s.job == Job.Builder && s.CanWork) { SimBuilder(s, dt); return; }
            if (working && s.workplace != null && s.workplace.Built && !s.workplace.damaged)
            {
                int idx = Mathf.Max(0, s.workplace.workers.IndexOf(s));
                Vector2 spot = s.workplace.WorkSpot(idx);
                if (s.act != Act.Working)
                {
                    s.act = Act.GoWork;
                    s.status = "Commuting";
                    Vector2 via = RouteAround(s.pos, spot);
                    if (MoveTo(s, via, dt, 1f) && via == spot) { s.act = Act.Working; s.faceRight = s.workplace.Center.x >= s.pos.x; }
                }
                else
                {
                    if ((s.pos - spot).sqrMagnitude > 0.5f) s.act = Act.GoWork;
                    s.xp += dt * 0.35f;
                    if (s.xp >= s.XpToNext && s.level < 10)
                    {
                        s.xp = 0; s.level++;
                        FloatText(s.pos + Vector2.up * 1.4f, "LEVEL UP!", new Color(1f, 0.86f, 0.35f));
                        if (s.level % 3 == 0) Log("^y" + s.name + "^w reached level " + s.level + ". Promotion denied.");
                    }
                    s.statusTimer -= dt;
                    if (s.statusTimer <= 0)
                    {
                        s.statusTimer = 5 + (float)rng.NextDouble() * 6;
                        var st = Jobs.Get(s.job).statuses;
                        s.status = st[rng.Next(st.Length)];
                        if (!string.IsNullOrEmpty(s.workplace.status) && rng.NextDouble() < 0.4) s.status = s.workplace.status;
                    }
                }
                return;
            }

            // idle / grazing
            if (s.refuse > 0) s.status = "On strike (personally)";
            else if (s.sick > 0) s.status = "Sick. Coughing up wool";
            else if (s.Lamb) s.status = "Being a lamb";
            else if (s.job != Job.Grazer && s.workplace == null) s.status = "Looking for a " + Jobs.Get(s.job).name + " job";
            else if (s.job != Job.Grazer && s.workplace != null && !s.workplace.Built) s.status = "Waiting for construction";
            else if (s.job != Job.Grazer && s.workplace != null && s.workplace.damaged) s.status = "Workplace is broken";
            SimGraze(s, dt, s.job == Job.Grazer && s.CanWork);
        }

        void SimGraze(Sheep s, float dt, bool producesFood)
        {
            if (s.act == Act.Graze)
            {
                s.actTimer -= dt;
                if (producesFood) s.status = Jobs.Get(Job.Grazer).statuses[(int)(simTime / 6 + s.id) % 4];
                if (s.actTimer <= 0)
                {
                    var t = TileAt(s.pos);
                    if (t != null && t.building == null && !t.path)
                    {
                        float take = Mathf.Min(t.grass, 0.5f);
                        t.grass -= take;
                        // room sheep nibble the grass themselves; it helps, but a growing flock needs a Feeding Lot
                        if (s.pen != null) s.hunger = Mathf.Max(0, s.hunger - take * 0.5f);
                        // starving free-roaming sheep eat the grass themselves instead of sharing it
                        else if (s.hunger > 0.9f && res[(int)Res.Food] < 2f * FoodMult(s)) s.hunger = Mathf.Max(0, s.hunger - take * 0.9f);
                        else if (producesFood)
                        {
                            float food = take * 3.2f * WorkEff(s, Cat.Graze, false);
                            res[(int)Res.Food] += food;
                            totalMade[(int)Res.Food] += food;
                        }
                        else s.hunger = Mathf.Max(0, s.hunger - take * 0.3f);
                    }
                    s.act = Act.Wander;
                    s.target = PickGrazeTarget(s);
                }
                return;
            }
            if (s.act != Act.Wander) { s.act = Act.Wander; s.target = PickGrazeTarget(s); }
            if (MoveTo(s, s.target, dt, 0.75f))
            {
                s.act = Act.Graze;
                s.actTimer = 1.2f + (float)rng.NextDouble() * 1.4f;
            }
        }

        void SimBuilder(Sheep s, float dt)
        {
            Building site = null; float bd = 9999;
            if (s.workplace != null && s.workplace.construct < 1f && buildings.Contains(s.workplace)) { site = s.workplace; bd = -1; }
            foreach (var b in buildings)
            {
                if (bd < 0) break;
                if (b.construct >= 1f) continue;
                float d = (b.Center - s.pos).sqrMagnitude;
                if (d < bd) { bd = d; site = b; }
            }
            if (site == null)
            {
                s.status = "Looking for things to hammer";
                SimGraze(s, dt, false);
                return;
            }
            if (s.workplace != site) { s.workplace = site; s.target = site.WorkSpot(rng.Next(3)) + new Vector2((float)rng.NextDouble() - 0.5f, 0); }
            if (s.act != Act.Building)
            {
                s.status = "Walking to construction";
                Vector2 via = RouteAround(s.pos, s.target);
                if (MoveTo(s, via, dt, 1f) && via == s.target) s.act = Act.Building;
            }
            else
            {
                s.status = Jobs.Get(Job.Builder).statuses[(int)(simTime / 5 + s.id) % 5];
                s.xp += dt * 0.35f;
                if (s.xp >= s.XpToNext && s.level < 10) { s.xp = 0; s.level++; FloatText(s.pos + Vector2.up * 1.4f, "LEVEL UP!", new Color(1f, 0.86f, 0.35f)); }
                if (rng.NextDouble() < dt * 0.002 * AccidentMult(s)) Accident(s, site);
            }
        }

        /// returns true when arrived
        bool MoveTo(Sheep s, Vector2 dest, float dt, float speedMult)
        {
            Vector2 d = dest - s.pos;
            float dist = d.magnitude;
            if (dist < 0.08f) { s.walkPhase = 0; return true; }
            float sp = 1.5f * speedMult;
            if (s.Has("fast")) sp *= 1.3f;
            if (s.strain == Genetics.Turbo) sp *= 1.5f;
            if (s.Has("lazy")) sp *= 0.75f;
            if (s.Lamb) sp *= 0.85f;
            if (s.flattened > 0) sp *= 0.6f;
            if (s.injured > 0) sp *= 0.6f;
            var t = TileAt(s.pos);
            if (t != null)
            {
                if (t.path) sp *= 1.4f;
                else t.wear = Mathf.Min(1, t.wear + dt * (t.pen != null ? 0.003f : 0.012f));
            }
            float stepLen = Mathf.Min(dist, sp * dt);
            s.pos += d / dist * stepLen;
            s.walkPhase += dt * sp * 4f;
            if (Mathf.Abs(d.x) > 0.05f) s.faceRight = d.x > 0;
            return false;
        }

        // ---------------- launches (accident) ----------------
        public void Launch(Sheep s)
        {
            s.act = Act.Launched;
            s.launchFrom = s.pos;
            s.launchTo = new Vector2(Random.Range(owned.xMin + 1f, owned.xMax - 1f), Random.Range(owned.yMin + 1f, owned.yMax - 1f));
            s.launchT = 0;
            s.launchDur = 1.6f + Vector2.Distance(s.launchFrom, s.launchTo) * 0.08f;
            Sfx.Play("boing");
        }

        void SimLaunched(Sheep s, float dt)
        {
            s.launchT += dt;
            float k = Mathf.Clamp01(s.launchT / s.launchDur);
            s.pos = Vector2.Lerp(s.launchFrom, s.launchTo, k);
            s.status = "Airborne";
            if (k >= 1)
            {
                s.act = Act.Wander;
                s.flattened = Mathf.Max(s.flattened, 0.8f);
                s.unconscious = 4f;
                shake = Mathf.Max(shake, 1f);
                Sfx.Play("thud");
                AddDecal("mud", s.pos);
                SpawnParticles(s.pos, new Color(0.5f, 0.38f, 0.25f), 10, 3f);
                FloatText(s.pos + Vector2.up, "THWUMP", new Color(1, 0.8f, 0.6f));
            }
        }

        // ---------------- death ----------------
        public void Kill(Sheep s, string cause)
        {
            if (s.dead) return;
            s.dead = true;
            s.deathCause = cause;
            s.deathDay = Day;
            s.act = Act.Dying;
            s.actTimer = 0;
            RemoveFromJob(s);
            foreach (var b in buildings) { if (b.client == s) b.client = null; b.clients.Remove(s); if (b.inMachine == s) b.inMachine = null; }
            deaths++;
            QueueRebirth(s);
            graveyard.Add(new DeadRecord { name = s.name, cause = cause, level = s.level, day = Day, age = s.ageDays, traits = TraitList(s) });
            AddDecal("blood", s.pos);
            AddDecal("bones", s.pos + new Vector2(0.3f, -0.1f));
            SpawnParticles(s.pos + Vector2.up * 0.5f, new Color(0.7f, 0.08f, 0.08f), 14, 3.5f);
            SpawnParticles(s.pos + Vector2.up * 0.5f, new Color(0.95f, 0.93f, 0.88f), 10, 3f);
            deathBanner = "NOOO, " + s.name.ToUpper() + " DIED";
            deathCauseText = cause;
            deathBannerT = 4.5f;
            Sfx.Play("sad", 0.8f);
            shake = Mathf.Max(shake, 0.6f);
            bool yard = HasBuilding(BKind.Graveyard);
            float days = yard ? 1f : 2.5f;
            AddGlobalMod("Mourning " + s.name, yard ? -4 : -8, days);
            Log("^r" + s.name + " died.^w " + cause);
            if (started) AdviseNow("death", s.name + " is DEAD. " + cause + " A tragedy. Anyway. The line keeps moving.", LExpr.Shout, 1f);
            AddTombstone();
            if (selSheep == s) selSheep = null;
            if (followSheep == s) followSheep = null;
        }

        public string TraitList(Sheep s)
        {
            var names = new List<string>();
            foreach (var t in s.traits) names.Add(Traits.Get(t).name);
            if (s.variant != Variant.None) names.Insert(0, VariantName(s.variant));
            return string.Join(", ", names.ToArray());
        }

        // ---------------- jobs ----------------
        public void RemoveFromJob(Sheep s)
        {
            if (s.workplace != null) s.workplace.workers.Remove(s);
            s.workplace = null;
            if (s.act == Act.Working || s.act == Act.GoWork || s.act == Act.Building) s.act = Act.Wander;
        }

        public bool AssignJob(Sheep s, Job j, Building prefer = null)
        {
            if (s.dead) return false;
            if (s.Lamb && j != Job.Grazer) { Toast("^r" + s.name + " is a lamb. Child labor is... later."); return false; }
            RemoveFromJob(s);
            s.job = j;
            if (j == Job.Grazer) { s.status = "Going back to the pen"; AssignPen(s); return true; }
            if (j == Job.Builder) { s.status = "Looking for things to hammer"; return true; }
            Building target = prefer;
            if (target != null && (target.def.job != j || target.workers.Count >= target.Slots)) target = null;
            if (target == null)
            {
                float bd = 99999;
                foreach (var b in buildings)
                {
                    if (b.def.job != j || b.workers.Count >= b.Slots) continue;
                    float d = (b.Center - s.pos).sqrMagnitude;
                    if (d < bd) { bd = d; target = b; }
                }
            }
            if (target != null)
            {
                target.workers.Add(s);
                s.workplace = target;
                s.act = Act.GoWork;
                return true;
            }
            s.status = "Looking for a " + Jobs.Get(j).name + " job";
            return true;
        }

        public int FreeSlots(Job j)
        {
            int n = 0;
            foreach (var b in buildings) if (b.def.job == j) n += Mathf.Max(0, b.Slots - b.workers.Count);
            return n;
        }
        public int TotalSlots(Job j)
        {
            int n = 0;
            foreach (var b in buildings) if (b.def.job == j) n += b.Slots;
            return n;
        }
        public int CountJob(Job j)
        {
            int n = 0;
            foreach (var s in sheep) if (!s.dead && s.job == j) n++;
            return n;
        }

        /// assign the best idle sheep to job j (optionally a building). Returns the sheep or null.
        public Sheep AssignBest(Job j, Building b = null)
        {
            Sheep best = null; float bf = -1;
            // breeding barns need a ram AND a ewe: prefer whichever is missing
            int wantMale = -1;
            if (j == Job.Breeder)
            {
                Building barn = b;
                if (barn == null) foreach (var x in buildings) if (x.def.kind == BKind.Barn && x.workers.Count < x.Slots) { barn = x; break; }
                if (barn != null)
                {
                    int m = 0, f = 0;
                    foreach (var w in barn.workers) { if (w.male) m++; else f++; }
                    wantMale = m <= f ? 1 : 0;
                    bool any = false;
                    foreach (var s in sheep) if (!s.dead && !s.Lamb && s.job == Job.Grazer && s.act != Act.Hidden && s.male == (wantMale == 1)) { any = true; break; }
                    if (!any) wantMale = -1;
                }
            }
            foreach (var s in sheep)
            {
                if (s.dead || s.Lamb || s.job != Job.Grazer || s.act == Act.Hidden || s.act == Act.InMachine) continue;
                if (wantMale >= 0 && s.male != (wantMale == 1)) continue;
                float f = JobFit(s, j) + (float)rng.NextDouble() * 0.01f;
                if (f > bf) { bf = f; best = s; }
            }
            if (best == null) { Toast("^rNo idle (Grazer) sheep available."); return null; }
            AssignJob(best, j, b);
            Sfx.Baa(0.4f);
            return best;
        }

        public void UnassignWorst(Job j)
        {
            Sheep worst = null; float wf = 999;
            foreach (var s in sheep)
            {
                if (s.dead || s.job != j) continue;
                float f = JobFit(s, j);
                if (f < wf) { wf = f; worst = s; }
            }
            if (worst != null) AssignJob(worst, Job.Grazer);
        }

        // ---------------- visuals ----------------
        void UpdateSheepVisual(Sheep s, float rdt)
        {
            if (s.go == null) return;
            if (s.act == Act.Hidden || s.act == Act.InMachine) { s.go.SetActive(false); s.shadow.gameObject.SetActive(false); return; }
            if (!s.go.activeSelf) { s.go.SetActive(true); s.shadow.gameObject.SetActive(true); }
            s.animT += rdt;
            var look = new SheepLook
            {
                woolStage = s.WoolStage,
                woolCol = s.woolCol,
                faceCol = s.faceCol,
                variant = s.variant,
                bandage = s.injured > 0,
                mud = s.muddy > 0 || s.Has("messy"),
                burned = s.burned > 0,
                blood = s.bloody > 0,
                clone = s.Has("clone"),
                udder = !s.male && !s.Lamb && s.milk >= 0.98f && !s.dead,
                strain = s.strain,
            };
            Expr e = s.baseExpr;
            if (s.dead) e = Expr.Dead;
            else if (s.unconscious > 0) e = Expr.Sleepy;
            else if (s.tremble > 0 || s.act == Act.Launched || s.act == Act.Summoned && s.summonedBy != null && s.summonedBy.def.kind == BKind.Plant) e = Expr.Panic;
            else if (petT > 0 && s == selSheep) e = Expr.Happy;
            else if (s.ridingT > 0.2f) e = Expr.Panic;
            else if (s.mood < 25 || s.hunger > 1.5f) e = Expr.Sad;
            else if (s.act == Act.Graze) e = Expr.Sleepy;
            look.expr = e;
            bool walking = (s.act == Act.Wander && !s.onBelt) || s.act == Act.GoWork || s.act == Act.Summoned || s.act == Act.Eat || s.act == Act.ToGate;
            bool moving = walking && s.walkPhase > 0;
            look.frame = moving ? ((int)(s.animT * 7) % 2) : 0;

            if (s.dead && s.act == Act.Ghost) look.ghost = true;
            s.sr.sprite = SheepArt.Get(look);
            s.sr.flipX = !s.faceRight;
            if (s.strain == Genetics.Glow && !s.dead && rng.NextDouble() < rdt * 1.2 && IsVisible(s.pos)) SpawnParticles(s.pos + Vector2.up * 0.6f, new Color(0.6f, 1f, 0.45f), 1, 0.8f);
            if (s.strain == Genetics.Sparky && !s.dead && rng.NextDouble() < rdt * 1.5 && IsVisible(s.pos)) SpawnParticles(s.pos + Vector2.up * 0.7f, new Color(1f, 0.95f, 0.45f), 2, 1.6f);
            if (s.strain == Genetics.Phoenix && !s.dead && rng.NextDouble() < rdt * 1.8 && IsVisible(s.pos)) SpawnParticles(s.pos + Vector2.up * 0.7f, new Color(1f, 0.55f, 0.15f), 1, 1.1f);

            float scale = (s.Lamb ? 0.62f : 1f) * (s.strain == Genetics.Jumbo ? 1.22f : s.strain == Genetics.Teacup ? 0.72f : 1f);
            Vector3 p = new Vector3(s.pos.x, s.pos.y, 0);
            Vector3 sc = new Vector3(scale, scale, 1);
            float rot = 0;
            if (moving) p.y += Mathf.Abs(Mathf.Sin(s.walkPhase * 2.2f)) * 0.08f;
            if (s.act == Act.Working)
            {
                float freq = s.job == Job.Operator ? 14 : 8;
                p.y += Mathf.Abs(Mathf.Sin(s.animT * freq + s.id)) * 0.07f;
                if (s.workplace != null && s.workplace.def.kind == BKind.Wheel) { p.y += 0.15f; look.frame = (int)(s.animT * 12) % 2; s.sr.sprite = SheepArt.Get(look); }
            }
            if (s.act == Act.Building) { p.y += Mathf.Abs(Mathf.Sin(s.animT * 10 + s.id)) * 0.1f; }
            if (s.act == Act.Graze) p.y -= 0.03f;
            if (s.tremble > 0 || s.act == Act.BeingSheared) p.x += (Mathf.PerlinNoise(s.animT * 30, s.id) - 0.5f) * 0.12f;
            if (s.flattened > 0 && !s.dead) { sc.x *= 1.35f; sc.y *= 0.4f; }
            if (s.act == Act.Launched)
            {
                float k = Mathf.Clamp01(s.launchT / s.launchDur);
                p.y += Mathf.Sin(k * Mathf.PI) * (3f + s.launchDur);
                rot = s.launchT * 720f;
            }
            if (s.dead)
            {
                if (s.act == Act.Dying)
                {
                    sc.y = -scale; p.y += 1.25f * scale * 0.8f;
                    if (s.actTimer > 2.5f) { s.act = Act.Ghost; s.actTimer = 0; Sfx.Play("howl", 0.25f, 1.6f); }
                }
                else if (s.act == Act.Ghost)
                {
                    p.y += s.actTimer * 0.9f;
                    p.x += Mathf.Sin(s.actTimer * 3) * 0.2f;
                    s.sr.color = new Color(1, 1, 1, Mathf.Clamp01(1 - s.actTimer / 3.5f));
                    if (s.actTimer > 3.5f) { Destroy(s.go); Destroy(s.shadow.gameObject); s.go = null; return; }
                }
            }
            else s.sr.color = Color.white;

            s.go.transform.position = p;
            s.go.transform.localScale = sc;
            s.go.transform.rotation = Quaternion.Euler(0, 0, rot);
            s.sr.sortingOrder = SortY(s.pos.y) + (s.act == Act.Launched ? 20000 : 0) + (s.act == Act.Ghost ? 25000 : 0);
            s.shadow.transform.position = new Vector3(s.pos.x, s.pos.y + 0.04f, 0);
            s.shadow.transform.localScale = new Vector3(scale * (s.flattened > 0 ? 1.3f : 1f), scale, 1);
            s.shadow.gameObject.SetActive(!s.dead || s.act == Act.Dying);

            // ambient chatter
            if (!s.dead && started)
            {
                s.baaTimer -= rdt * Speeds[Mathf.Max(1, speed)];
                if (s.baaTimer <= 0)
                {
                    s.baaTimer = 10 + (float)rng.NextDouble() * 30;
                    string[] pool = s.mood < 30 ? SadChatter : (s.act == Act.Working && s.workplace != null && s.workplace.Danger > 0.008f ? WorkChatter : Chatter);
                    SpeechText(s.pos + new Vector2(0.2f, 1.3f * scale), pool[rng.Next(pool.Length)]);
                    if (rng.NextDouble() < 0.35 && IsVisible(s.pos)) Sfx.Baa(0.25f);
                }
                if (s.burned > 0 && rng.NextDouble() < rdt * 4) SpawnSmoke(s.pos + Vector2.up * 0.7f, 0.5f);
            }
        }

        public bool IsVisible(Vector2 p)
        {
            var v = cam.WorldToViewportPoint(p);
            return v.x > -0.05f && v.x < 1.05f && v.y > -0.05f && v.y < 1.05f;
        }

        void UpdateVisuals(float rdt, float dt)
        {
            VisualWorldUpdate(rdt);
            for (int i = sheep.Count - 1; i >= 0; i--)
            {
                var s = sheep[i];
                UpdateSheepVisual(s, rdt);
                if (s.dead && s.go == null) sheep.RemoveAt(i);
            }
            foreach (var b in buildings) UpdateBuildingVisual(b, rdt, dt);
            UpdateBeltVisuals(rdt, dt);
            UpdateWolfVisuals(rdt);
            if (petT > 0) petT -= rdt;
        }
        public float petT;
    }
}
