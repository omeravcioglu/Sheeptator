using System.Collections.Generic;
using UnityEngine;

namespace Fleeced
{
    /// a DNA-altered breed of sheep, made in the Gene Lab (or born from two other strains)
    public class StrainDef
    {
        public int id; public string key, name, shortName, color, desc;
        public int unlockDna, spliceDna;
        public Res needRes; public int needAmount; // rare goods needed to unlock it
        public string requires;                     // another strain that must be unlocked first
        public int parentA, parentB;                // hybrids: hidden until a lamb of these two strains (or CHAOS) reveals them
        public bool Hybrid { get { return parentA != 0; } }
    }

    /// a permanent breeding upgrade bought in the GENETICS menu
    public class GeneDef
    {
        public string id, name, desc;
        public int dna, coins;
        public Res needRes; public int needAmount;
        public string requires;
    }

    public static class Genetics
    {
        public const int None = 0, Silk = 1, Cream = 2, Jumbo = 3, Gold = 4, Glow = 5, Turbo = 6, Teacup = 7,
            Candy = 8, Choco = 9, Wolf = 10, Sparky = 11, Splitter = 12, Phoenix = 13;
        /// Gene Lab mode: a random splice. Anything can happen.
        public const int Chaos = 100;

        public static readonly StrainDef[] Strains =
        {
            null,
            new StrainDef { id = Silk, key = "silk", name = "Silkfleece", shortName = "SILK", color = "f6dcef", unlockDna = 6, spliceDna = 2,
                desc = "Shorn for SILK instead of wool: about 3x the money per shearing." },
            new StrainDef { id = Cream, key = "cream", name = "Creamer", shortName = "CREAM", color = "fff0c4", unlockDna = 6, spliceDna = 2,
                desc = "Ewes are milked for CREAM instead of milk: about 2x the money. Rams just get confused." },
            new StrainDef { id = Jumbo, key = "jumbo", name = "Jumbo", shortName = "JUMBO", color = "e8d8bc", unlockDna = 8, spliceDna = 2,
                desc = "Twice the MUTTON. Eats 50% more. Big boned." },
            new StrainDef { id = Gold, key = "gold", name = "Golden Fleece", shortName = "GOLD", color = "f0c040", unlockDna = 15, spliceDna = 4, needRes = Res.Silk, needAmount = 20, requires = "silk",
                desc = "Shorn for GOLDEN FLEECE: about 5x wool money, but the fleece grows 30% slower. Level 5 upgrades need it." },
            new StrainDef { id = Glow, key = "glow", name = "Glowfleece", shortName = "GLOW", color = "9cf070", unlockDna = 15, spliceDna = 4, needRes = Res.Cream, needAmount = 20, requires = "cream",
                desc = "Shorn for GLOW WOOL: about 4x wool money. Glows in the dark. Slightly radioactive. Level 5 upgrades need it." },
            new StrainDef { id = Turbo, key = "turbo", name = "Turbo Sheep", shortName = "TURBO", color = "e83a30", unlockDna = 8, spliceDna = 2,
                desc = "Machines handle it 3x faster and it walks 50% faster. Racing stripe included. Gets a slow line moving." },
            new StrainDef { id = Teacup, key = "teacup", name = "Teacup Sheep", shortName = "TEACUP", color = "ffe8d8", unlockDna = 8, spliceDna = 2,
                desc = "Tiny! Eats 60% less food but gives 60% of the wool and milk. Everyone adores it (+5 mood)." },
            new StrainDef { id = Candy, key = "candy", name = "Cotton Candy Sheep", shortName = "CANDY", color = "f8b8e0", unlockDna = 12, spliceDna = 3, parentA = Silk, parentB = Cream,
                desc = "Shorn for COTTON CANDY: about 3x wool money, and the fleece grows 20% faster. Sugar rush." },
            new StrainDef { id = Choco, key = "choco", name = "Chocolate Sheep", shortName = "CHOCO", color = "8a5a3a", unlockDna = 12, spliceDna = 3, parentA = Cream, parentB = Jumbo,
                desc = "Ewes are milked for CHOCOLATE MILK: about 2.7x milk money. Tastes like a crime." },
            new StrainDef { id = Wolf, key = "wolf", name = "Wolfsheep", shortName = "WOLF", color = "8c8c96", unlockDna = 14, spliceDna = 3, parentA = Jumbo, parentB = Turbo,
                desc = "Part wolf. Scares wolves away (+1.5 guard power each) and gives 50% more mutton. Sometimes eats a pen-mate." },
            new StrainDef { id = Sparky, key = "sparky", name = "Sparky", shortName = "SPARKY", color = "f8e060", unlockDna = 18, spliceDna = 4, parentA = Glow, parentB = Turbo,
                desc = "Electric! Every Sparky alive makes 3 POWER for the big machines. Zaps the neighbours." },
            new StrainDef { id = Splitter, key = "splitter", name = "Splitter", shortName = "SPLIT", color = "c8b4f0", unlockDna = 22, spliceDna = 4, parentA = Teacup, parentB = Glow,
                desc = "Mitosis: now and then it splits into TWO Splitters (if the pen has room). Free sheep. Do not ask how." },
            new StrainDef { id = Phoenix, key = "phoenix", name = "Phoenix Sheep", shortName = "PHOENIX", color = "f07838", unlockDna = 30, spliceDna = 5, parentA = Gold, parentB = Glow,
                desc = "When it dies, even in the Slaughterhouse, it is usually REBORN as a Phoenix lamb. Mutton that comes back." },
        };
        public static StrainDef Get(int id) { return id > 0 && id < Strains.Length ? Strains[id] : null; }
        public static string ShortName(int id) { return id == Chaos ? "CHAOS" : Get(id) != null ? Get(id).shortName : "PLAIN"; }
        public static int HybridCount { get { int n = 0; foreach (var d in Strains) if (d != null && d.Hybrid) n++; return n; } }

        /// the hybrid two strains make together (either order), or null
        public static StrainDef HybridOf(int a, int b)
        {
            foreach (var d in Strains)
                if (d != null && d.Hybrid && (d.parentA == a && d.parentB == b || d.parentA == b && d.parentB == a)) return d;
            return null;
        }

        public static readonly string[] Quips = { "IT'S ALIVE!", "SCIENCE!", "*bzzzt*", "baa?", "What have we done", "Patent pending", "Mostly sheep", "Do not tell anyone" };

        public static readonly GeneDef[] Upgrades =
        {
            new GeneDef { id = "love_songs", name = "Love Songs", dna = 6, coins = 100, desc = "Every Breeding Pen makes lambs 30% faster. Soft music. Nobody asks." },
            new GeneDef { id = "quick_lambs", name = "Quick Lambs", dna = 8, coins = 150, desc = "Lambs grow up in 2 days instead of 3." },
            new GeneDef { id = "twin_genes", name = "Twin Genes", dna = 12, coins = 250, desc = "1 birth in 5 brings TWINS (if the pen has room)." },
            new GeneDef { id = "strong_genes", name = "Strong Genes", dna = 12, coins = 250, desc = "Lambs keep their parents' strain 90% of the time (normally 50%), and hybrids turn up more often. Breed strains instead of splicing them." },
            new GeneDef { id = "safe_splice", name = "Safe Splicing", dna = 10, coins = 200, desc = "Gene Lab side effects are 70% rarer, and CHAOS blows up half as many sheep." },
            new GeneDef { id = "big_fleece", name = "Big Fleece", dna = 18, coins = 300, needRes = Res.Silk, needAmount = 10, requires = "silk", desc = "Every sheep grows its fleece 25% faster." },
            new GeneDef { id = "rich_milk", name = "Rich Milk", dna = 18, coins = 300, needRes = Res.Cream, needAmount = 10, requires = "cream", desc = "Every ewe's udder fills 25% faster." },
        };
    }

    /// Genetics: DNA, the Gene Lab (SAMPLE / SPLICE / CHAOS), strains and hybrids, breeding upgrades, and rare goods for big upgrades.
    public partial class Game
    {
        public int dna, dnaTotal, splicedTotal;
        public readonly HashSet<string> genes = new HashSet<string>(); // bought upgrades, unlocked strains ("strain_<key>"), discovered hybrids ("known_<key>")

        public bool HasGene(string id) { return genes.Contains(id); }
        public bool StrainUnlocked(int id) { var d = Genetics.Get(id); return d != null && genes.Contains("strain_" + d.key); }
        /// a hybrid stays a mystery until a lamb (or CHAOS) reveals it
        public bool StrainKnown(int id) { var d = Genetics.Get(id); return d != null && (!d.Hybrid || genes.Contains("known_" + d.key)); }
        public int HybridsKnown() { int n = 0; foreach (var d in Genetics.Strains) if (d != null && d.Hybrid && genes.Contains("known_" + d.key)) n++; return n; }
        public int StrainsKnown() { int n = 0; foreach (var d in Genetics.Strains) if (d != null && StrainKnown(d.id)) n++; return n; }
        public int CountStrain(int id) { int n = 0; foreach (var s in sheep) if (!s.dead && s.act != Act.Hidden && s.strain == id) n++; return n; }

        /// settings that follow from the bought genes (after a purchase and after loading)
        public void ApplyGenes() { Sheep.LambAge = genes.Contains("quick_lambs") ? 2f : 3f; }

        public void ResetGenetics() { dna = 0; dnaTotal = 0; splicedTotal = 0; genes.Clear(); ApplyGenes(); }

        public void GainDna(int n, Vector2 at)
        {
            if (n <= 0) return;
            dna += n; dnaTotal += n;
            if (tech.Contains("genetics") && IsVisible(at)) FloatText(at + Vector2.up * 1.9f, "+" + n + " DNA", new Color(0.55f, 1f, 0.75f));
        }

        // ---------------- buying ----------------
        public string GeneBlocker(GeneDef g)
        {
            if (genes.Contains(g.id)) return "Owned";
            if (g.requires != null && !genes.Contains("strain_" + g.requires)) return "Unlock the " + StrainByKey(g.requires).name + " strain first";
            if (dna < g.dna) return "Need " + g.dna + " DNA (you have " + dna + ")";
            if (res[0] < g.coins) return "Need " + g.coins + " coins";
            if (g.needAmount > 0 && res[(int)g.needRes] < g.needAmount) return NeedRare(g.needRes, g.needAmount);
            return null;
        }

        public void BuyGene(GeneDef g)
        {
            if (GeneBlocker(g) != null) return;
            dna -= g.dna; res[0] -= g.coins;
            if (g.needAmount > 0) res[(int)g.needRes] -= g.needAmount;
            genes.Add(g.id);
            ApplyGenes();
            Sfx.Play("fanfare", 0.5f, 1.2f);
            Log("^pGENETICS:^w " + g.name + ". " + g.desc);
        }

        static StrainDef StrainByKey(string key) { foreach (var d in Genetics.Strains) if (d != null && d.key == key) return d; return null; }

        public string StrainBlocker(StrainDef d)
        {
            if (StrainUnlocked(d.id)) return "Unlocked";
            if (!tech.Contains("genetics")) return "Research Sheep Genetics first";
            if (!StrainKnown(d.id)) return "Not discovered yet";
            if (d.requires != null && !genes.Contains("strain_" + d.requires)) return "Unlock " + StrainByKey(d.requires).name + " first";
            if (dna < d.unlockDna) return "Need " + d.unlockDna + " DNA (you have " + dna + ")";
            if (d.needAmount > 0 && res[(int)d.needRes] < d.needAmount) return NeedRare(d.needRes, d.needAmount);
            return null;
        }

        /// rare goods have to be in storage, and Markets sell whatever rides in: say how to keep some
        string NeedRare(Res r, int n)
        {
            return "Need " + n + " " + ResInfo.Names[(int)r] + " in storage (you have " + Mathf.FloorToInt((float)res[(int)r]) + "). Markets sell whatever rides in: SHUT a Market door and it goes to storage instead.";
        }

        public void UnlockStrain(StrainDef d)
        {
            if (StrainBlocker(d) != null) return;
            dna -= d.unlockDna;
            if (d.needAmount > 0) res[(int)d.needRes] -= d.needAmount;
            genes.Add("strain_" + d.key);
            Sfx.Play("zap", 0.6f);
            Log("^pNEW STRAIN:^w " + d.name + ". " + d.desc);
            bool anyLab = false;
            foreach (var b in buildings) if (b.def.kind == BKind.GeneLab) { anyLab = true; if (b.geneMode == 0) SetGeneMode(b, d.id); }
            AdviseNow("strain_" + d.key, "The " + d.name.ToUpper() + " strain is ours. " + (anyLab ? "My GENE LAB now splices it into every sheep that rides through. Mind the DNA." : "Build a GENE LAB on a conveyor and splice it in."), LExpr.Smug, 99f);
        }

        /// a hybrid shows itself for the first time
        void Discover(int id, string how)
        {
            var d = Genetics.Get(id);
            if (d == null || StrainKnown(id)) return;
            genes.Add("known_" + d.key);
            Log("^pHYBRID DISCOVERED:^w the " + d.name + " (" + how + "). " + d.desc);
            Toast("^pDISCOVERED: " + d.name.ToUpper());
            Sfx.Play("fanfare", 0.6f, 1.3f);
            LeaderPop("A NEW KIND OF SHEEP: the " + d.name.ToUpper() + ". " + (how.StartsWith("CHAOS") ? "Science rolled the dice and won." : "Nature did the work. I take the credit.") + " Unlock it in GENETICS (N).", LExpr.Smug, 8f);
        }

        // ---------------- the Gene Lab ----------------
        /// Gene Lab mode: 0 = SAMPLE (collect DNA), Genetics.Chaos = random splices, else the strain it splices in
        public void SetGeneMode(Building b, int mode)
        {
            if (mode != 0 && mode != Genetics.Chaos && !StrainUnlocked(mode)) return;
            b.geneMode = mode;
            Sfx.Play("click", 0.5f, 1.3f);
        }
        public void SetAllGeneLabs(int mode) { foreach (var b in buildings) if (b.def.kind == BKind.GeneLab) SetGeneMode(b, mode); }

        /// DNA one sheep gives in SAMPLE mode: 1 / 2 / 3 by Gene Lab level
        public static int SampleDna(Building b) { return Mathf.Clamp(b.level, 1, 3); }
        /// a better Gene Lab splices for less
        public static int SpliceCost(Building b, StrainDef d) { return Mathf.Max(1, d.spliceDna - (Mathf.Clamp(b.level, 1, 3) - 1)); }

        public string GeneModeName(Building b)
        {
            if (b.geneMode == Genetics.Chaos) return "CHAOS (1 DNA: anything can happen)";
            var d = Genetics.Get(b.geneMode);
            return d == null ? "SAMPLE (+" + SampleDna(b) + " DNA per sheep)" : "SPLICE " + d.shortName + " (" + SpliceCost(b, d) + " DNA per sheep)";
        }

        /// called when a sheep has been inside the Gene Lab long enough
        void ProcessGeneLab(Building b, Sheep s)
        {
            if (b.geneMode == Genetics.Chaos && !s.Lamb)
            {
                if (dna >= 1) { dna -= 1; ChaosSplice(b, s); return; }
                b.geneNoDnaT += 1f;
            }
            var sd = Genetics.Get(b.geneMode);
            bool splice = sd != null && StrainUnlocked(sd.id) && s.strain != sd.id && !s.Lamb;
            int cost = sd != null ? SpliceCost(b, sd) : 0;
            if (splice && dna < cost) { splice = false; b.geneNoDnaT += 1f; }
            if (!splice)
            {
                // sampling: a needle, a vial, some DNA
                GainDna(SampleDna(b), b.Center);
                s.tremble = Mathf.Max(s.tremble, 1f);
                AddMod(s, "Poked with a needle", -2, 0.5f);
                if (IsVisible(b.Center) && rng.NextDouble() < 0.25) SpeechText(b.Center + new Vector2(0.3f, 1.4f), rng.NextDouble() < 0.5 ? "ow" : "*needle*");
                return;
            }
            dna -= cost;
            Splice(s, sd.id, b);
        }

        /// rewrites the sheep. Usually it works.
        public void Splice(Sheep s, int strain, Building b)
        {
            var sd = Genetics.Get(strain);
            s.strain = strain;
            splicedTotal++;
            GiveXp(s, 10);
            Vector2 at = b != null ? b.Center : s.pos;
            if (sd.Hybrid && !StrainKnown(strain)) Discover(strain, "CHAOS in the Gene Lab");
            if (IsVisible(at))
            {
                FloatText(at + Vector2.up * 1.6f, sd.shortName + "!", Pal.C(sd.color));
                if (rng.NextDouble() < 0.5) SpeechText(at + new Vector2(0.4f, 1.2f), Genetics.Quips[rng.Next(Genetics.Quips.Length)]);
                SpawnParticles(at + Vector2.up * 0.9f, Pal.C(sd.color), 12, 2.4f);
                Sfx.Play("zap", 0.35f, 1.2f + (float)rng.NextDouble() * 0.5f);
            }
            if (splicedTotal == 1) LeaderPop("IT WORKED. " + s.name + " is now a " + sd.name.ToUpper() + ". The ethics committee has been dissolved. Again.", LExpr.Smug, 7f);
            else if (splicedTotal == 4) LeaderPop("Now put two DIFFERENT strains in one pen. Their lambs might be something NEW. Hybrids. Secret ones.", LExpr.Smug, 8f);
            // side effects
            float side = 0.08f * (HasGene("safe_splice") ? 0.3f : 1f);
            if (rng.NextDouble() >= side) return;
            SideEffect(s);
        }

        void SideEffect(Sheep s)
        {
            double r = rng.NextDouble();
            if (r < 0.4 && s.variant == Variant.None)
            {
                Variant[] v = { Variant.TwoHeaded, Variant.ThreeEyed, Variant.Spiky };
                s.variant = v[rng.Next(v.Length)];
                Log("^pSide effect:^w " + s.name + " came out of the Gene Lab " + VariantName(s.variant) + ". Science is a journey.");
            }
            else if (r < 0.8)
            {
                string[] t = { "dramatic", "stupid", "fluffy", "fertile", "glutton", "lucky" };
                string pick = t[rng.Next(t.Length)];
                if (!s.traits.Contains(pick) && !Traits.Conflicts(s.traits, pick) && s.traits.Count < 5) s.traits.Add(pick);
                Log("^pSide effect:^w " + s.name + " came out " + Traits.Get(pick).name.ToLower() + ".");
            }
            else
            {
                s.unconscious = 4f;
                Log("^pSide effect:^w " + s.name + " fainted in the Gene Lab. Probably fine.");
            }
        }

        /// CHAOS mode: 1 DNA, one roll of the dice
        void ChaosSplice(Building b, Sheep s)
        {
            Vector2 at = b.Center;
            bool vis = IsVisible(at);
            float boom = HasGene("safe_splice") ? 0.025f : 0.05f;
            double r = rng.NextDouble();
            if (vis) SpawnParticles(at + Vector2.up * 1f, Color.HSVToRGB((float)rng.NextDouble(), 0.6f, 1f), 14, 3f);
            if (r < 0.45)
            {
                // any strain at all, even the secret ones
                int st = 1 + rng.Next(Genetics.Strains.Length - 1);
                if (st == s.strain) st = 1 + (st % (Genetics.Strains.Length - 1));
                if (vis) FloatText(at + Vector2.up * 2.2f, "CHAOS!", new Color(1f, 0.5f, 1f));
                Splice(s, st, b);
                return;
            }
            if (r < 0.60 && s.variant == Variant.None)
            {
                Variant[] v = { Variant.TwoHeaded, Variant.ThreeEyed, Variant.Spiky, Variant.Rainbow, Variant.Golden, Variant.Black };
                s.variant = v[rng.Next(v.Length)];
                if (vis) FloatText(at + Vector2.up * 1.8f, "MUTATION!", new Color(1f, 0.6f, 1f));
                Log("^pCHAOS:^w " + s.name + " came out " + VariantName(s.variant) + ".");
                return;
            }
            if (r < 0.70)
            {
                s.ageDays = 0.3f;
                if (vis) FloatText(at + Vector2.up * 1.8f, "BABY AGAIN!", new Color(1f, 0.75f, 0.85f));
                Log("^pCHAOS:^w " + s.name + " came out a LAMB again. Nobody knows how old it really is now.");
                return;
            }
            if (r < 0.80)
            {
                int n = 8 + rng.Next(8);
                if (!Output(b, Res.Wool, n) && vis) FlyFromWorld("wool", at + Vector2.up, 3);
                s.wool = 1.25f;
                if (vis) { FloatText(at + Vector2.up * 1.8f, "FLUFF BOMB! +" + n, new Color(1f, 1f, 0.9f)); SpawnParticles(at + Vector2.up, new Color(0.96f, 0.94f, 0.88f), 24, 4f); }
                return;
            }
            if (r < 0.87)
            {
                SideEffect(s);
                if (vis) FloatText(at + Vector2.up * 1.8f, "...SOMETHING CHANGED", new Color(0.8f, 0.8f, 1f));
                return;
            }
            if (r < 1 - boom)
            {
                if (vis) FloatText(at + Vector2.up * 1.8f, "*the machine burps*", new Color(0.7f, 0.9f, 0.7f));
                return;
            }
            // KA-BOOM: the sheep becomes wool and mutton, all at once
            int w = 12 + rng.Next(8), m = 4 + rng.Next(4);
            if (!Output(b, Res.Wool, w) && vis) FlyFromWorld("wool", at + Vector2.up, 3);
            if (!Output(b, Res.Mutton, m) && vis) FlyFromWorld("mutton", at + Vector2.up, 2);
            if (vis) { FloatText(at + Vector2.up * 2.2f, "KA-BOOM!", new Color(1f, 0.45f, 0.3f)); SpawnParticles(at + Vector2.up, new Color(0.96f, 0.94f, 0.88f), 30, 5f); shake = Mathf.Max(shake, 1.2f); Sfx.Play("boom", 0.6f); }
            GainDna(2, at);
            Kill(s, "Exploded in the Gene Lab's CHAOS mode. Into wool. And mutton.");
        }

        // ---------------- breeding: strains, hybrids, rebirths, splits ----------------
        /// a lamb keeps a parent's strain half the time (Strong Genes: 90%); two different strains may make a hybrid
        void InheritStrain(Sheep s, Sheep a, Sheep b)
        {
            int sa = a.strain, sb = b.strain;
            if (sa == 0 && sb == 0) return;
            if (sa != 0 && sb != 0 && sa != sb)
            {
                var hy = Genetics.HybridOf(sa, sb);
                if (hy != null && rng.NextDouble() < (HasGene("strong_genes") ? 0.35 : 0.25))
                {
                    s.strain = hy.id;
                    Discover(hy.id, "a lamb of a " + Genetics.Get(sa).name + " and a " + Genetics.Get(sb).name);
                    return;
                }
            }
            float keep = HasGene("strong_genes") ? 0.9f : 0.5f;
            if (sa != 0 && sa == sb) keep = Mathf.Max(keep, 0.85f);
            int pick = sa != 0 && sb != 0 ? (rng.NextDouble() < 0.5 ? sa : sb) : (sa != 0 ? sa : sb);
            if (rng.NextDouble() < keep) s.strain = pick;
        }

        /// Phoenix deaths waiting for their rebirth (handled at the start of the next tick, never in the middle of a loop over the flock)
        readonly List<Sheep> phoenixQueue = new List<Sheep>();
        void QueueRebirth(Sheep s) { if (s.strain == Genetics.Phoenix && !phoenixQueue.Contains(s)) phoenixQueue.Add(s); }
        void ProcessRebirths()
        {
            if (phoenixQueue.Count == 0) return;
            var q = phoenixQueue.ToArray();
            phoenixQueue.Clear();
            foreach (var s in q) PhoenixRebirth(s);
        }

        /// a Phoenix that dies is usually reborn as a lamb in its pen
        void PhoenixRebirth(Sheep s)
        {
            if (s.strain != Genetics.Phoenix || rng.NextDouble() >= 0.6) return;
            var pen = s.pen != null && buildings.Contains(s.pen) && IsPen(s.pen) ? s.pen : FirstPen();
            if (pen == null) return;
            var inner = PenInterior(pen);
            var lamb = CreateSheep(inner.center, null, null, s.male);
            usedNames.Remove(lamb.name);
            lamb.name = s.name.EndsWith(" II") ? s.name + "I" : s.name + " II";
            usedNames.Add(lamb.name);
            if (lamb.go != null) lamb.go.name = lamb.name;
            lamb.strain = Genetics.Phoenix; lamb.ageDays = 0; lamb.wool = 0.2f; lamb.hunger = 0;
            lamb.traits = new List<string>(s.traits);
            lamb.parents = s.name + " (reborn from the ashes)";
            lamb.pen = pen;
            births++;
            if (IsVisible(inner.center))
            {
                FloatText(inner.center + Vector2.up * 1.4f, "REBORN!", new Color(1f, 0.55f, 0.2f));
                SpawnParticles(inner.center + Vector2.up * 0.5f, new Color(1f, 0.5f, 0.15f), 18, 3f);
            }
            Log("^o" + s.name + " rose from the ashes^w as a lamb. The Phoenix strain does not believe in endings.");
        }

        /// once a day: Wolfsheep get peckish, Sparkies zap, Splitters split
        void DailyStrains()
        {
            var list = new List<Sheep>(sheep);
            foreach (var s in list)
            {
                if (s.dead || s.act == Act.Hidden || s.Lamb || s.pen == null || !buildings.Contains(s.pen)) continue;
                bool home = s.act != Act.InMachine && !s.onBelt && PenInterior(s.pen).Contains(s.pos);
                if (!home) continue;
                if (s.strain == Genetics.Wolf && tutDone && rng.NextDouble() < 0.04)
                {
                    Sheep victim = null;
                    foreach (var o in sheep)
                        if (o != s && !o.dead && o.pen == s.pen && o.strain != Genetics.Wolf && o.act != Act.Hidden && o.act != Act.InMachine && !o.onBelt && PenInterior(s.pen).Contains(o.pos)) { victim = o; break; }
                    if (victim != null) { s.hunger = 0; Kill(victim, "Eaten by " + s.name + " the Wolfsheep. It was family."); }
                }
                else if (s.strain == Genetics.Sparky && rng.NextDouble() < 0.2)
                {
                    foreach (var o in sheep)
                        if (o != s && !o.dead && o.pen == s.pen && rng.NextDouble() < 0.3) { AddMod(o, "Zapped by " + s.name, -3, 1f); o.tremble = 2f; if (IsVisible(o.pos)) FloatText(o.pos + Vector2.up * 1.3f, "ZAP", new Color(1f, 0.95f, 0.4f)); break; }
                }
                else if (s.strain == Genetics.Splitter && rng.NextDouble() < 0.12 && s.pen.penMembers < RoomCapacity(s.pen) && Population < popCap)
                {
                    var c = CreateSheep(s.pos + new Vector2(0.4f, 0f), null, null, s.male);
                    c.strain = Genetics.Splitter;
                    c.traits = new List<string>(s.traits);
                    c.woolCol = s.woolCol; c.faceCol = s.faceCol; c.baseExpr = s.baseExpr; c.variant = s.variant;
                    c.ageDays = s.ageDays; c.generation = s.generation; c.level = s.level;
                    c.wool = s.wool * 0.5f; s.wool *= 0.5f;
                    c.parents = s.name + " (split)";
                    c.pen = s.pen;
                    births++;
                    if (IsVisible(s.pos)) { FloatText(s.pos + Vector2.up * 1.5f, "MITOSIS!", Pal.C(Genetics.Get(Genetics.Splitter).color)); SpawnParticles(s.pos + Vector2.up * 0.5f, new Color(0.8f, 0.7f, 1f), 12, 2f); }
                    Log("^p" + s.name + " split in two.^w Meet " + c.name + ". Same sheep. Different sheep.");
                }
            }
        }

        // ---------------- what strains do ----------------
        public static Res ShearRes(Sheep s)
        {
            switch (s.strain)
            {
                case Genetics.Silk: return Res.Silk; case Genetics.Gold: return Res.GoldFleece; case Genetics.Glow: return Res.GlowWool;
                case Genetics.Candy: return Res.CottonCandy;
            }
            return Res.Wool;
        }
        public static float ShearAmount(Sheep s)
        {
            switch (s.strain)
            {
                case Genetics.Silk: return 0.6f; case Genetics.Gold: return 0.35f; case Genetics.Glow: return 0.5f;
                case Genetics.Candy: return 0.5f; case Genetics.Teacup: return 0.6f;
            }
            return 1f;
        }
        public static Res MilkRes(Sheep s) { return s.strain == Genetics.Cream ? Res.Cream : s.strain == Genetics.Choco ? Res.ChocoMilk : Res.Milk; }
        public static float MilkAmount(Sheep s) { return s.strain == Genetics.Cream ? 0.7f : s.strain == Genetics.Choco ? 0.6f : s.strain == Genetics.Teacup ? 0.6f : 1f; }

        /// extra coins a strain adds to a sheep's price
        public static int StrainValue(Sheep s)
        {
            switch (s.strain)
            {
                case Genetics.None: return 0;
                case Genetics.Gold: case Genetics.Glow: return 120;
                case Genetics.Splitter: case Genetics.Sparky: return 150;
                case Genetics.Phoenix: return 250;
                case Genetics.Candy: case Genetics.Choco: case Genetics.Wolf: return 80;
            }
            return 40;
        }

        /// power and guard power the living strains add (Sparky, Wolfsheep)
        void StrainGlobals()
        {
            int sparky = 0, wolves = 0;
            foreach (var s in sheep) { if (s.dead || s.act == Act.Hidden) continue; if (s.strain == Genetics.Sparky) sparky++; else if (s.strain == Genetics.Wolf) wolves++; }
            powerMade += sparky * 3f;
            guardPower += wolves * 1.5f;
        }

        // ---------------- the bubbling tank ----------------
        /// the Gene Lab's tank of goo: coloured by its mode, bubbling, flashing while it splices
        void UpdateGeneLabVisual(Building b)
        {
            if (b.go == null) return;
            if (b.extras.Count == 0 || b.extras[0] == null)
            {
                var go = new GameObject("goo");
                go.transform.SetParent(b.go.transform, false);
                go.AddComponent<SpriteRenderer>();
                if (b.extras.Count == 0) b.extras.Add(go); else b.extras[0] = go;
            }
            var sr = b.extras[0].GetComponent<SpriteRenderer>();
            bool show = b.construct >= 1f && !b.damaged;
            sr.enabled = show;
            if (!show) return;
            int lv = Mathf.Clamp(b.level, 1, 3);
            int W = b.def.w * 16, H = b.def.h * 16 + BuildingArt.Top;
            int top = (H - 2) - (19 + lv * 3), tw = 10 + lv * 2, th = 8 + lv * 2, tx = W / 2 - tw / 2;
            Color32 col;
            if (b.geneMode == Genetics.Chaos) col = Pal.H(Genetics.Strains[1 + Mathf.FloorToInt(Time.time * 3f) % (Genetics.Strains.Length - 1)].color);
            else if (Genetics.Get(b.geneMode) != null) col = Pal.H(Genetics.Get(b.geneMode).color);
            else col = Pal.H("62d870");
            bool busy = b.inMachine != null && !b.machineDone;
            bool bright = busy && Mathf.FloorToInt(Time.time * 10f) % 2 == 0;
            int frame = Mathf.FloorToInt(Time.time * (busy ? 9f : 3f)) % 3;
            sr.sprite = EnvArt.Goo(col, frame, tw - 2, th - 3, bright);
            sr.transform.localPosition = new Vector3((tx + 1) / 16f, (H - top) / 16f, 0);
            sr.sortingOrder = b.sr.sortingOrder + 1;
            if (busy && IsVisible(b.Center) && rng.NextDouble() < Time.deltaTime * 4) SpawnParticles(new Vector2(b.x + (tx + tw / 2f) / 16f, b.y + (H - top + th) / 16f), (Color)col, 1, 1.4f);
        }

        // ---------------- rare goods for big upgrades ----------------
        /// Level 4 needs Silk and Cream, Level 5 needs Golden Fleece and Glow Wool (on top of coins)
        public int RareUpgradeNeed(Building b, out Res r1, out Res r2)
        {
            int next = b.level + 1;
            r1 = Res.Silk; r2 = Res.Cream;
            if (next == 4) return 15;
            if (next == 5) { r1 = Res.GoldFleece; r2 = Res.GlowWool; return 15; }
            return 0;
        }

        public string RareUpgradeText(Building b)
        {
            Res r1, r2;
            int n = RareUpgradeNeed(b, out r1, out r2);
            return n > 0 ? n + " " + ResInfo.Names[(int)r1] + " + " + n + " " + ResInfo.Names[(int)r2] : null;
        }
    }
}
