using UnityEngine;

namespace Fleeced
{
    /// The GENETICS menu (N): the strain book (13 strains, six of them secret hybrids) and permanent breeding upgrades, bought with DNA.
    public partial class Game
    {
        float scrollGenes, genesContentH;
        int geneTab;

        void DrawGeneticsPanel()
        {
            var r = PanelRect();
            if (!PanelFrame(r, "GENETICS", "dna")) return;
            float x = r.x + 8, w = r.width - 16, y = r.y + UI.HeaderH;
            string[] tabs = { "STRAINS " + StrainsKnown() + "/" + (Genetics.Strains.Length - 1), "BREEDING UPGRADES" };
            int nt = Tabs(new Rect(x, y, w, 18), tabs, geneTab);
            if (nt != geneTab) { geneTab = nt; scrollGenes = 0; }
            y += 24;
            var area = new Rect(x, y, w, r.yMax - 8 - y);
            float aw = area.width - 6;
            UI.BeginScroll(area, ref scrollGenes, genesContentH);
            float yy = 0;

            // the DNA bank
            UI.Icon(0, yy, "dna");
            PixelFont.Draw(13, yy + 1, "^p" + dna + " DNA^k   (" + dnaTotal + " collected, " + splicedTotal + " splices, " + HybridsKnown() + "/" + Genetics.HybridCount + " hybrids found)", UI.Text);
            yy += 13;

            if (geneTab == 0) DrawStrainBook(ref yy, aw);
            else DrawGeneUpgrades(ref yy, aw);

            genesContentH = yy + 4;
            UI.EndScroll();
        }

        void DrawStrainBook(ref float yy, float aw)
        {
            FLine(ref yy, aw, "^kGet DNA: a GENE LAB on a conveyor in SAMPLE mode, or the Slaughterhouse (+1 per sheep, +3 for special ones).");

            // what the flock is made of
            string mix = "^w" + CountStrain(0) + "^k plain";
            foreach (var d in Genetics.Strains) if (d != null) { int n = CountStrain(d.id); if (n > 0) mix += ", ^p" + n + " " + d.shortName; }
            FLine(ref yy, aw, "^kYour flock: " + mix + "^k.");

            // the Gene Lab(s): SAMPLE and CHAOS buttons
            Building lab = null; int labs = 0;
            foreach (var b in buildings) if (b.def.kind == BKind.GeneLab) { labs++; if (lab == null) lab = b; }
            if (lab == null) FLine(ref yy, aw, "^oNo Gene Lab yet. Build one ON a conveyor (BUILD > FACTORY).");
            else
            {
                FLine(ref yy, aw, "^kGene Lab" + (labs > 1 ? "s (" + labs + ")" : "") + ": ^y" + GeneModeName(lab));
                var sr = new Rect(0, yy, 96, 15);
                UI.Mark("gene_sample", sr);
                if (UI.ButtonEx(sr, "SAMPLE DNA", "dna", null, true, "SAMPLE MODE\nEvery sheep that rides through gives " + SampleDna(lab) + " DNA (more at higher Gene Lab levels). Nothing else happens. Mostly.", lab.geneMode == 0 ? BtnStyle.Primary : BtnStyle.Normal)) SetAllGeneLabs(0);
                var cr = new Rect(100, yy, aw - 100, 15);
                UI.Mark("gene_chaos", cr);
                if (UI.ButtonEx(cr, "CHAOS (1 DNA)", "star", null, true, "CHAOS MODE\n1 DNA per sheep and ANYTHING can happen: any strain (even the secret hybrids), a mutation, a baby again, a fluff bomb... or KA-BOOM: wool and mutton, no sheep.", lab.geneMode == Genetics.Chaos ? BtnStyle.Primary : BtnStyle.Danger)) SetAllGeneLabs(Genetics.Chaos);
                yy += 18;
            }
            yy += 3;

            UI.Heading(0, yy, "THE STRAIN BOOK"); yy += 12;
            foreach (var d in Genetics.Strains)
            {
                if (d == null) continue;
                bool known = StrainKnown(d.id);
                if (!known)
                {
                    // a secret hybrid: show the hint, not the strain
                    string hint = "^kA secret hybrid. Put a ^w" + Genetics.Get(d.parentA).name.ToUpper() + "^k and a ^w" + Genetics.Get(d.parentB).name.ToUpper() + "^k in one pen and wait for a lucky lamb, or gamble with CHAOS.";
                    float hh = PixelFont.WrappedHeight(hint, aw - 12);
                    var hr = new Rect(0, yy, aw, 16 + hh);
                    UI.Card(hr, false, true, false);
                    UI.Fill(new Rect(5, yy + 4, 8, 8), UI.Ink);
                    PixelFont.Draw(7, yy + 4, "?", UI.Dim);
                    PixelFont.Draw(17, yy + 4, "^k??? ^k(undiscovered)", UI.Dim);
                    PixelFont.DrawWrapped(new Rect(6, yy + 14, aw - 12, hh), hint, UI.Dim);
                    yy += hr.height + 3;
                    continue;
                }
                bool unlocked = StrainUnlocked(d.id);
                string bl = StrainBlocker(d);
                string costTxt = d.unlockDna + " DNA" + (d.needAmount > 0 ? " + " + d.needAmount + " " + ResInfo.Names[(int)d.needRes] : "");
                float dh = PixelFont.WrappedHeight("^k" + d.desc, aw - 12);
                float bh = bl != null && !unlocked ? PixelFont.WrappedHeight("^r" + bl, aw - 12) + 2 : 0;
                float ch = 14 + dh + 20 + bh;
                var row = new Rect(0, yy, aw, ch);
                UI.Card(row, lab != null && lab.geneMode == d.id, false, UI.Hover(row));
                UI.Fill(new Rect(5, yy + 4, 8, 8), UI.Ink);
                UI.Fill(new Rect(6, yy + 5, 6, 6), Pal.C(d.color));
                PixelFont.Draw(17, yy + 4, (unlocked ? "^y" : "^w") + d.name.ToUpper() + "^k  (" + CountStrain(d.id) + " in the flock)" + (d.Hybrid ? " ^phybrid" : ""), UI.Text);
                PixelFont.DrawWrapped(new Rect(6, yy + 15, aw - 12, dh), "^k" + d.desc, UI.Dim);
                var br = new Rect(6, yy + 17 + dh, aw - 12, 15);
                UI.Mark("gene_strain_" + d.key, br);
                if (unlocked)
                {
                    bool sel = lab != null && lab.geneMode == d.id;
                    int cost = lab != null ? SpliceCost(lab, d) : d.spliceDna;
                    string lbl = lab == null ? "UNLOCKED (build a Gene Lab)" : sel ? "SPLICING IN THE GENE LAB" : "SPLICE IN THE GENE LAB (" + cost + " DNA each)";
                    if (UI.ButtonEx(br, lbl, null, null, lab != null && !sel, "SPLICE " + d.shortName + "\nEvery Gene Lab turns the sheep that ride through into " + d.name.ToUpper() + " for " + cost + " DNA each. Their lambs keep it half the time.", sel ? BtnStyle.Primary : BtnStyle.Normal))
                        SetAllGeneLabs(d.id);
                }
                else
                {
                    if (UI.ButtonEx(br, "UNLOCK: " + costTxt, null, null, bl == null, "UNLOCK " + d.name.ToUpper() + "\n" + d.desc + (bl != null ? "\n^r" + bl : ""), BtnStyle.Primary)) UnlockStrain(d);
                    if (bl != null) PixelFont.DrawWrapped(new Rect(6, br.yMax + 1, aw - 12, bh), "^r" + bl, UI.Text);
                }
                yy += ch + 3;
            }
        }

        void DrawGeneUpgrades(ref float yy, float aw)
        {
            FLine(ref yy, aw, "^kPermanent upgrades for the whole flock. Paid in DNA (and coins).");
            foreach (var g in Genetics.Upgrades)
            {
                bool owned = HasGene(g.id);
                string bl = GeneBlocker(g);
                string costTxt = g.dna + " DNA + " + g.coins + "c" + (g.needAmount > 0 ? " + " + g.needAmount + " " + ResInfo.Names[(int)g.needRes] : "");
                float dh = PixelFont.WrappedHeight("^k" + g.desc, aw - 12);
                float gbh = !owned && bl != null && bl.Contains("storage") ? PixelFont.WrappedHeight("^r" + bl, aw - 12) + 2 : 0;
                float ch = 14 + dh + 20 + gbh;
                var row = new Rect(0, yy, aw, ch);
                UI.Card(row, owned, false, UI.Hover(row));
                PixelFont.Draw(6, yy + 4, (owned ? "^g" : "^w") + g.name.ToUpper() + (owned ? "^k  OWNED" : ""), UI.Text);
                PixelFont.DrawWrapped(new Rect(6, yy + 15, aw - 12, dh), "^k" + g.desc, UI.Dim);
                var br = new Rect(6, yy + 17 + dh, aw - 12, 15);
                UI.Mark("gene_up_" + g.id, br);
                if (!owned && UI.ButtonEx(br, "BUY: " + costTxt, null, null, bl == null, g.name.ToUpper() + "\n" + g.desc + (bl != null ? "\n^r" + bl : ""), BtnStyle.Primary)) BuyGene(g);
                if (owned) PixelFont.Draw(6, br.y + 4, "^gIn effect.", UI.Text);
                if (gbh > 0) PixelFont.DrawWrapped(new Rect(6, br.yMax + 1, aw - 12, gbh), "^r" + bl, UI.Text);
                yy += ch + 3;
            }
        }

        /// the Gene Lab's own panel: what it does to the sheep riding through
        float DrawGeneLabControls(Building b, float y, float aw)
        {
            UI.Heading(0, y + 3, "GENE LAB MODE"); y += 14;
            PixelFont.Draw(0, y, "^y" + GeneModeName(b), UI.Text); y += 10;
            PixelFont.Draw(0, y, "^kDNA in the bank: ^p" + dna, UI.Text); y += 12;
            float bx = 0;
            System.Action<string, int, string, BtnStyle> btn = (label, mode, tip, style) =>
            {
                float bw = PixelFont.Width(label) + 12;
                if (bx > 0 && bx + bw > aw) { bx = 0; y += 17; }
                if (UI.ButtonEx(new Rect(bx, y, bw, 15), label, null, null, true, tip, b.geneMode == mode ? BtnStyle.Primary : style)) SetGeneMode(b, mode);
                bx += bw + 3;
            };
            btn("SAMPLE", 0, "SAMPLE\n+" + SampleDna(b) + " DNA for every sheep that rides through.", BtnStyle.Normal);
            btn("CHAOS", Genetics.Chaos, "CHAOS\n1 DNA per sheep. Any strain (even secret ones), a mutation, a baby again, a fluff bomb... or KA-BOOM.", BtnStyle.Danger);
            foreach (var d in Genetics.Strains)
            {
                if (d == null || !StrainUnlocked(d.id)) continue;
                btn(d.shortName, d.id, "SPLICE " + d.shortName + "\n" + d.desc + "\nCosts " + SpliceCost(b, d) + " DNA per sheep. Without DNA it samples instead.", BtnStyle.Normal);
            }
            y += 18;
            return y + 2;
        }
    }
}
