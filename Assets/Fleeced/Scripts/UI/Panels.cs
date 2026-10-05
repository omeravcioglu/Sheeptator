using System.Collections.Generic;
using UnityEngine;

namespace Fleeced
{
    public partial class Game
    {
        float marketContentH = 300;

        // ---------------- shared panel helpers ----------------
        /// Left panel frame with its close button. Returns false if it was closed this frame.
        bool PanelFrame(Rect r, string title, string icon)
        {
            UI.Panel(r, title, true, icon);
            if (UI.CloseButton(r)) { TogglePanel(panel); return false; }
            return true;
        }

        /// Equal-width segmented tabs. Returns the selected index.
        int Tabs(Rect r, string[] labels, int sel)
        {
            float gap = 2, bw = (r.width - gap * (labels.Length - 1)) / labels.Length;
            for (int i = 0; i < labels.Length; i++)
            {
                float x0 = Mathf.Round(r.x + i * (bw + gap)), x1 = Mathf.Round(r.x + i * (bw + gap) + bw);
                if (UI.ButtonEx(new Rect(x0, r.y, x1 - x0, r.height), labels[i], null, null, true, null, i == sel ? BtnStyle.Primary : BtnStyle.Normal)) sel = i;
            }
            return sel;
        }

        /// Gold section heading with a rule (and an optional dim note on the right).
        void Section(float x, float y, float w, string title, string note = null)
        {
            UI.Heading(x, y, title);
            float tw = PixelFont.Width(title);
            float nw = note != null ? PixelFont.Width(note) + 6 : 0;
            if (note != null) PixelFont.Draw(x + w - nw + 6, y, note, UI.Dim);
            UI.Fill(new Rect(x + tw + 6, y + 3, Mathf.Max(0, w - tw - 6 - nw - 2), 1), UI.Hi2);
        }

        static int DangerPips(float d) { return d <= 0 ? 0 : d < 0.003f ? 1 : d < 0.01f ? 2 : 3; }

        void Skulls(float x, float y, int pips)
        {
            for (int k = 0; k < 3; k++) UI.Icon(x + k * 11, y, k < pips ? "skull" : "skull_off");
        }

        static string Trunc(string s, int n) { return s == null ? "" : s.Length <= n ? s : s.Substring(0, n - 2) + ".."; }

        // ---------------- BUILD ----------------
        static readonly string[] BuildTabs = { "FACTORY", "PENS", "TRADE", "DECOR" };

        static int BuildCat(BuildingDef d)
        {
            switch (d.kind)
            {
                case BKind.House: case BKind.GuardPost: case BKind.Trough: case BKind.Pen: case BKind.FeedLot: return 1;
                case BKind.Market: case BKind.Trading: case BKind.Bank: case BKind.Storage: return 2;
            }
            return d.decor ? 3 : 0;
        }

        void DrawBuildPanel()
        {
            var r = PanelRect();
            if (!PanelFrame(r, "BUILD", "hammer")) return;
            float x = r.x + 8, w = r.width - 16, y = r.y + UI.HeaderH;
            if (TutorialActive && CurStep.target != null)
            {
                var tt = CurStep.target();
                if (tt != null && tt.ui != null && tt.ui.StartsWith("build_"))
                    foreach (var bd in Buildings.All) if ("build_" + bd.kind == tt.ui) buildTab = BuildCat(bd);
            }
            buildTab = Tabs(new Rect(x, y, w, 18), BuildTabs, buildTab);
            y += 22;
            if (tutDone || UiOn("belts"))
            {
                string beltTip = "CONVEYOR BELTS [C] (" + BeltTierCost[beltTierSel] + "c/tile)\nA belt that starts under a DOOR carries sheep away: into another room, through machines, or worse. Goods ride belts from a building's side into the next building, or into a Market (sold +10%). Buildings can be placed on top of belts. R rotates.";
                if (tutDone)
                {
                    float tw3 = Mathf.Floor((w - 4) / 3f);
                    if (UI.Button(new Rect(x, y, tw3, 16), "PATHS", true, "PATH TOOL (2c/tile)\nPaint dirt paths. Sheep walk 40% faster on them. Looks organized. It isn't.", pathTool)) { pathTool = !pathTool; placing = null; }
                    var cr = new Rect(x + tw3 + 2, y, tw3, 16);
                    UI.Mark("tool_conveyor", cr);
                    if (UI.Button(cr, "CONVEYOR", true, beltTip, beltTool && !clearTool)) { if (beltTool && !clearTool) beltTool = false; else { BeginBeltTool(); panel = Panel.None; } }
                    if (UI.Button(new Rect(x + (tw3 + 2) * 2, y, w - (tw3 + 2) * 2, 16), "CLEAR", true, "CLEAR TOOL\nRemove belts and paths (50% refund).", clearTool)) { if (clearTool) { clearTool = false; beltTool = false; } else { BeginBeltTool(); clearTool = true; } }
                    if (MaxBeltTier > 0)
                    {
                        y += 20;
                        string[] tn = { "RUBBER", "STEEL", "MEGA" };
                        for (int k = 0; k < 3; k++)
                        {
                            var tr = new Rect(x + (tw3 + 2) * k, y, k == 2 ? w - (tw3 + 2) * 2 : tw3, 16);
                            string tip = BeltTierNames[k].ToUpper() + " (" + BeltTierCost[k] + "c/tile)\n" + (k == 0 ? "Slow and cheap." : k == 1 ? "80% faster and sheep ride closer together." : "Very fast, extra wide, sheep packed tight.") + (k <= MaxBeltTier ? "\nLaying over an old belt upgrades it." : "\nNeeds research.");
                            if (UI.Button(tr, tn[k] + " " + BeltTierCost[k] + "c", k <= MaxBeltTier, tip, beltTierSel == k)) { beltTierSel = k; if (!beltTool || clearTool) { BeginBeltTool(); panel = Panel.None; } }
                        }
                        int ut; int uc = UpgradeAllCost(MaxBeltTier, out ut);
                        if (ut > 0)
                        {
                            y += 20;
                            UI.Mark("upgrade_belts", new Rect(x, y, w, 16));
                            if (UI.ButtonEx(new Rect(x, y, w, 16), "UPGRADE ALL BELTS: " + ut + " TILES " + uc + "c", null, null, res[0] >= uc, "UPGRADE ALL BELTS\nTurn every slower conveyor into a " + BeltTierNames[MaxBeltTier] + ". Faster sheep, more sheep, more money.", BtnStyle.Primary)) UpgradeAllBelts(MaxBeltTier);
                        }
                    }
                }
                else
                {
                    var cr = new Rect(x, y, w, 16);
                    UI.Mark("tool_conveyor", cr);
                    if (UI.Button(cr, "CONVEYOR TOOL [C]", true, beltTip, beltTool && !clearTool)) { if (beltTool && !clearTool) beltTool = false; else { BeginBeltTool(); panel = Panel.None; } }
                }
                y += 22;
            }

            const float detailH = 92, CardH = 38, Gap = 3;
            var area = new Rect(x, y, w, r.yMax - 8 - detailH - 6 - y);
            var list = new List<BuildingDef>();
            foreach (var d in Buildings.All) if (BuildCat(d) == buildTab && BuildVisible(d)) list.Add(d);
            UI.BeginScroll(area, ref scrollBuild, list.Count * (CardH + Gap) + (tutDone ? 0 : 34));
            float cw = area.width - 6, yy = 0;
            BuildingDef hov = null;
            foreach (var d in list)
            {
                bool locked = d.tech != null && !tech.Contains(d.tech);
                bool uniq = UniqueBlocked(d);
                bool afford = res[0] >= d.cost[0];
                var row = new Rect(0, yy, cw, CardH);
                UI.Mark("build_" + d.kind, row);
                bool hover = UI.Hover(row);
                if (hover) hov = d;
                UI.Card(row, placing == d, locked || uniq, hover);
                if (!locked && !uniq && UI.Clickable(row, null, false)) BeginPlacing(d);
                var iw = new Rect(3, yy + 3, 32, 32);
                UI.Well(iw);
                UI.IconFit(UI.Inset(iw, 3), Icons.ForBuilding(d.kind), locked || uniq);
                if (locked) UI.Icon(iw.xMax - 10, iw.yMax - 10, "lock");
                float tx = 41;
                PixelFont.Draw(tx, yy + 4, d.name.ToUpper(), locked || uniq ? UI.Dim : placing == d ? UI.Gold : UI.Text);
                string sub = locked ? "Needs " + Research.Get(d.tech).name : uniq ? "Only one allowed" : FlowLine(d.kind) ?? "Lv1 " + d.levelNames[0];
                PixelFont.Draw(tx, yy + 14, Trunc(sub, 34), locked ? UI.Bad : UI.Dim);
                float by = yy + 25, bx = tx;
                if (locked)
                {
                    UI.Icon(bx, by - 1, "research");
                    PixelFont.Draw(bx + 12, by, Research.Get(d.tech).rp + " RP", UI.Xp);
                }
                else
                {
                    string cs = d.cost[0].ToString();
                    UI.Icon(bx, by - 1, "coin");
                    PixelFont.Draw(bx + 12, by, cs, afford ? UI.Gold : UI.Bad);
                    bx += 12 + PixelFont.Width(cs) + 10;
                }
                int pips = DangerPips(d.danger[0]);
                if (pips > 0 && IsSheepMachineKind(d.kind)) Skulls(cw - 37, by - 1, pips);
                yy += CardH + Gap;
            }
            if (!tutDone) PixelFont.DrawWrapped(new Rect(4, yy + 4, cw - 8, 30), "^k" + (list.Count == 0 ? "Nothing here yet. " : "") + "More buildings unlock as the Supreme Leader decrees.", UI.Dim);
            UI.EndScroll();

            // details of the hovered (or currently placed) building
            var dr = new Rect(x, r.yMax - 8 - detailH, w, detailH);
            UI.Well(dr);
            var dd = hov ?? placing;
            if (dd == null)
            {
                PixelFont.DrawWrapped(new Rect(dr.x + 8, dr.y + 8, dr.width - 16, dr.height - 16), "^kHover a building to see what it does. Click to place it. Hold Shift to place several.", UI.Dim);
                return;
            }
            var bs = BuildingArt.Get(dd.kind, 1).sprite;
            float sw = bs.rect.width, sh = bs.rect.height, ix = dr.x + 6, imgW;
            if (sh <= dr.height - 6 && sw <= 70) { UI.DrawSprite(new Rect(ix, dr.yMax - 3 - sh, sw, sh), bs); imgW = sw + 8; }
            else { UI.IconFit(new Rect(ix, dr.y + 8, 36, 36), Icons.ForBuilding(dd.kind)); imgW = 44; }
            float dx = ix + imgW, dw = dr.xMax - 8 - dx;
            string size = dd.w + "x" + dd.h;
            PixelFont.Draw(dx, dr.y + 6, Trunc(dd.name.ToUpper(), 24), UI.Gold);
            PixelFont.Draw(dr.xMax - 8 - PixelFont.Width(size), dr.y + 6, size, UI.Dim);
            var dl = PixelFont.Wrap(dd.desc ?? "", dw, 1);
            int maxL = Mathf.FloorToInt((dr.height - 34) / 10f);
            for (int i = 0; i < dl.Count && i < maxL; i++)
                PixelFont.Draw(dx, dr.y + 18 + i * 10, (i == maxL - 1 && dl.Count > maxL) ? Trunc(dl[i], 28) + ".." : dl[i], UI.Text);
            string stats = "^gRuns by itself" + (IsSheepMachineKind(dd.kind) && dd.danger[0] > 0 ? "  ^kDanger ^r" + (dd.danger[0] * 100).ToString("0.0") + "%" : "") + (dd.maxLevel > 1 ? "  ^kLv ^w1-" + dd.maxLevel : "");
            PixelFont.Draw(dx, dr.yMax - 13, stats, UI.Text);
        }

        // ---------------- SHEEP ----------------
        void DrawSheepPanel()
        {
            var r = PanelRect();
            if (!PanelFrame(r, "THE FLOCK", "flock")) return;
            float x = r.x + 8, w = r.width - 16, y = r.y + UI.HeaderH;
            string[] tabs = { "FLOCK " + Population, "MEMORIAL " + graveyard.Count, "LOG" };
            int nt = Tabs(new Rect(x, y, w, 18), tabs, sheepTab);
            if (nt != sheepTab) { sheepTab = nt; scrollSheep = 0; }
            y += 24;
            var area = new Rect(x, y, w, r.yMax - 8 - y);
            float aw = area.width - 6;
            if (sheepTab == 0)
            {
                var list = new List<Sheep>();
                foreach (var s in sheep) if (!s.dead && s.act != Act.Hidden) list.Add(s);
                const float RH = 32;
                UI.BeginScroll(area, ref scrollSheep, list.Count * (RH + 3));
                float yy = 0;
                foreach (var s in list)
                {
                    var row = new Rect(0, yy, aw, RH);
                    UI.Card(row, s == selSheep, false, UI.Hover(row));
                    if (UI.Clickable(row, TraitList(s), false)) { Select(s); FocusOn(s.pos); }
                    var pw = new Rect(3, yy + 3, 26, 26);
                    UI.Well(pw, UI.Grass);
                    if (s.sr != null) UI.DrawSprite(new Rect(pw.x + 3, pw.y + 3, 20, 20), s.sr.sprite);
                    float tx = 34;
                    PixelFont.Draw(tx, yy + 3, s.name + " ^k" + (s.Lamb ? "lamb" : s.Gender + " Lv" + s.level) + (s.variant != Variant.None ? " ^p" + VariantName(s.variant) : "") + (s.strain != 0 ? " ^p" + Genetics.ShortName(s.strain) : ""), UI.Text);
                    PixelFont.Draw(tx, yy + 12, "^y" + (s.pen != null ? (IsLot(s.pen) ? "Lot" : "Pen") : "Loose") + "^k: " + Trunc(s.status, 22), UI.Text);
                    PixelFont.Draw(tx, yy + 21, "^k" + Trunc(TraitList(s), 30), UI.Dim);
                    Color mc = s.mood > 60 ? UI.Good : s.mood > 30 ? UI.Gold : UI.Bad;
                    UI.Bar(new Rect(aw - 38, yy + 4, 34, 6), s.mood / 100f, mc);
                    string st = s.injured > 0 || s.sick > 0 ? "^rhurt" : s.hunger > 1.2f ? "^ohungry" : s.wool >= 0.98f ? "^wfluffy" : null;
                    if (st != null) PixelFont.Draw(aw - 38, yy + 13, st, UI.Text);
                    yy += RH + 3;
                }
                UI.EndScroll();
            }
            else if (sheepTab == 1)
            {
                float content = 0;
                foreach (var d in graveyard) content += 24 + PixelFont.WrappedHeight("^k" + d.cause, aw - 12) + 3;
                if (graveyard.Count == 0) content = 30;
                UI.BeginScroll(area, ref scrollSheep, content);
                float yy = 0;
                if (graveyard.Count == 0) PixelFont.Draw(4, 4, "^kNobody has died yet. Give it time.", UI.Dim);
                for (int i = graveyard.Count - 1; i >= 0; i--)
                {
                    var d = graveyard[i];
                    float ch = PixelFont.WrappedHeight("^k" + d.cause, aw - 12);
                    var row = new Rect(0, yy, aw, 22 + ch);
                    UI.Card(row);
                    UI.Icon(5, yy + 4, "tomb");
                    PixelFont.Draw(18, yy + 5, "^r" + d.name + "^k  Lv" + d.level + ", lived " + d.age.ToString("0.0") + " days", UI.Text);
                    PixelFont.DrawWrapped(new Rect(6, yy + 16, aw - 12, ch), "^k" + d.cause, UI.Dim);
                    UI.Tip(row, d.name + "\n" + d.traits);
                    yy += 22 + ch + 3;
                }
                UI.EndScroll();
            }
            else
            {
                float content = 0;
                foreach (var l in logHistory) content += PixelFont.WrappedHeight(l, aw - 4) + 2;
                UI.BeginScroll(area, ref scrollSheep, content);
                float yy = 0;
                for (int i = logHistory.Count - 1; i >= 0; i--)
                {
                    float hh = PixelFont.WrappedHeight(logHistory[i], aw - 4);
                    PixelFont.DrawWrapped(new Rect(2, yy, aw - 4, hh), logHistory[i], UI.Text);
                    yy += hh + 2;
                }
                UI.EndScroll();
            }
        }

        // ---------------- RESEARCH ----------------
        void DrawResearchPanel()
        {
            var r = PanelRect();
            if (!PanelFrame(r, "RESEARCH", "research")) return;
            float x = r.x + 8, w = r.width - 16, y = r.y + UI.HeaderH;
            int labs = 0; foreach (var lb in buildings) if (lb.def.kind == BKind.Lab && lb.Built) labs++;
            PixelFont.Draw(x, y + 1, "^b" + Mathf.FloorToInt(rpPool) + " RP^k unspent", UI.Text);
            string rs = "Labs: " + labs;
            PixelFont.Draw(x + w - PixelFont.Width(rs), y + 1, rs, UI.Dim);
            y += 13;
            if (!HasBuilding(BKind.Lab)) PixelFont.Draw(x, y, "^rBuild a Research Lab. It researches by itself.", UI.Text);
            else if (currentResearch == null) PixelFont.Draw(x, y, "^yPick a project below.", UI.Text);
            else
            {
                var cr = Research.Get(currentResearch);
                UI.Bar(new Rect(x, y - 1, w, 11), Progress(cr.id) / cr.rp, UI.Xp, cr.name + "  " + Mathf.FloorToInt(Progress(cr.id)) + "/" + cr.rp);
            }
            y += 15;
            var area = new Rect(x, y, w, r.yMax - 8 - y);
            float aw = area.width - 6;
            float content = Research.Cats.Length * 16 + Research.All.Length * 25;
            UI.BeginScroll(area, ref scrollResearch, content);
            float yy = 0;
            foreach (var cat in Research.Cats)
            {
                Section(2, yy + 4, aw - 4, cat.ToUpper());
                yy += 16;
                foreach (var rd in Research.All)
                {
                    if (rd.cat != cat) continue;
                    bool done = tech.Contains(rd.id);
                    bool avail = ResearchAvailable(rd);
                    bool cur = currentResearch == rd.id;
                    var row = new Rect(0, yy, aw, 22);
                    UI.Mark("research_" + rd.id, row);
                    UI.Card(row, cur, !avail && !done, avail && !cur && UI.Hover(row));
                    var pre = new List<string>(); foreach (var p in rd.pre) pre.Add((tech.Contains(p) ? "^g" : "^r") + Research.Get(p).name);
                    string tip = rd.name + "\n" + rd.desc + (pre.Count > 0 ? "\n^kRequires: " + string.Join("^k, ", pre.ToArray()) : "");
                    if (avail && !cur) { if (UI.Clickable(row, tip, false)) { currentResearch = rd.id; Sfx.Play("click"); } }
                    else UI.Tip(row, tip);
                    Color nc = done ? UI.Good : avail ? UI.Text : UI.Dim;
                    PixelFont.Draw(5, yy + 3, rd.name, nc);
                    string cost = done ? "DONE" : rd.rp + " RP";
                    PixelFont.Draw(aw - PixelFont.Width(cost) - 5, yy + 3, cost, done ? UI.Good : UI.Xp);
                    if (cur || (!done && Progress(rd.id) > 0)) UI.Bar(new Rect(5, yy + 13, aw - 10, 6), Progress(rd.id) / rd.rp, UI.Xp);
                    else if (!done && !avail) PixelFont.Draw(5, yy + 12, "^kNeeds: " + Trunc(string.Join(", ", NamesOf(rd.pre)), 38), UI.Dim);
                    else if (!done) PixelFont.Draw(5, yy + 12, "^k" + Trunc(rd.desc, 40), UI.Dim);
                    yy += 25;
                }
            }
            UI.EndScroll();
        }

        string[] NamesOf(string[] ids)
        {
            var l = new List<string>();
            foreach (var i in ids) if (!tech.Contains(i)) l.Add(Research.Get(i).name);
            return l.ToArray();
        }

        // ---------------- MARKET ----------------
        void DrawMarketPanel()
        {
            var r = PanelRect();
            if (!PanelFrame(r, "MARKET", "bag")) return;
            float x = r.x + 8, w = r.width - 16, y = r.y + UI.HeaderH;
            bool hasMarket = HasBuilding(BKind.Market);
            PixelFont.Draw(x, y + 1, hasMarket ? "^gMarket open.^w Merchants sell goods marked AUTO." : "^rNo Market:^w 60% prices. Build a Market!", UI.Text);
            y += 14;
            var area = new Rect(x, y, w, r.yMax - 8 - y);
            float aw = area.width - 6;
            UI.BeginScroll(area, ref scrollMarket, marketContentH);
            float yy = 0;
            foreach (var rr in ResInfo.Sellable)
            {
                int i = (int)rr;
                if (rr != Res.Wool && rr != Res.Milk && res[i] < 0.5 && totalMade[i] < 0.5) continue;
                var row = new Rect(0, yy, aw, 22);
                UI.Card(row);
                UI.Icon(5, yy + 6, ResInfo.Icon[i]);
                PixelFont.Draw(18, yy + 3, ResInfo.Names[i], UI.Text);
                PixelFont.Draw(18, yy + 12, "^k" + UI.Fmt(res[i]) + "/" + UI.Fmt(storageCap), UI.Dim);
                float price = SellPrice(rr);
                string trend = priceTrend[i] > 0.01f ? "^g+" : priceTrend[i] < -0.01f ? "^r-" : "^k=";
                PixelFont.Draw(92, yy + 8, "^y" + price.ToString(price < 10 ? "0.0" : "0") + "c " + trend, UI.Text);
                UI.Tip(new Rect(90, yy, 40, 22), "PRICE\nPrice per unit. Selling lots at once lowers the price; it recovers daily.");
                int have = Mathf.FloorToInt((float)res[i]);
                float bx = 132;
                if (UI.Button(new Rect(bx, yy + 4, 18, 14), "1", have >= 1)) Sell(rr, 1, false); bx += 20;
                if (UI.Button(new Rect(bx, yy + 4, 22, 14), "10", have >= 10)) Sell(rr, 10, false); bx += 24;
                UI.Mark("sell_" + rr, new Rect(bx, yy + 4, 26, 14));
                if (UI.Button(new Rect(bx, yy + 4, 26, 14), "ALL", have >= 1)) { int e = Sell(rr, have, false); Toast("^ySold for " + e + "c"); }
                bx += 28;
                if (UI.Button(new Rect(bx, yy + 4, aw - bx - 4, 14), "AUTO", hasMarket, "AUTO-SELL\nMerchants sell this automatically.", autoSell[i])) autoSell[i] = !autoSell[i];
                yy += 25;
            }
            // food
            {
                var row = new Rect(0, yy, aw, 22);
                UI.Card(row);
                UI.Icon(5, yy + 6, "food");
                PixelFont.Draw(18, yy + 3, "Food", UI.Text);
                PixelFont.Draw(18, yy + 12, "^k" + UI.Fmt(res[7]) + "/" + UI.Fmt(CapOf(Res.Food)), UI.Dim);
                if (UI.Button(new Rect(92, yy + 4, aw - 96, 14), "BUY 40 FOOD: " + FoodBuyCost + "c", res[0] >= FoodBuyCost, "EMERGENCY HAY\nPrice rises over time.")) BuyFood();
                yy += 30;
            }
            Section(2, yy, aw - 4, "SHEEP DEALER", "new stock every 2 days");
            yy += 14;
            foreach (var o in offers.ToArray())
            {
                var s = o.sheep;
                var row = new Rect(0, yy, aw, 34);
                UI.Card(row);
                var look = new SheepLook { woolStage = s.WoolStage, woolCol = s.woolCol, faceCol = s.faceCol, variant = s.variant, expr = s.baseExpr };
                var pw = new Rect(3, yy + 4, 26, 26);
                UI.Well(pw, UI.Grass);
                UI.DrawSprite(new Rect(pw.x + 3, pw.y + 3, 20, 20), SheepArt.Get(look));
                PixelFont.Draw(34, yy + 4, s.name + " ^k" + s.Gender + (s.level > 1 ? " Lv" + s.level : "") + (s.variant != Variant.None ? " ^p" + VariantName(s.variant) : ""), UI.Text);
                PixelFont.DrawWrapped(new Rect(34, yy + 14, aw - 96, 20), "^k" + Trunc(TraitList(s), 52), UI.Dim);
                var tips = new List<string>(); foreach (var t in s.traits) tips.Add(Traits.Get(t).name + ": " + Traits.Get(t).desc);
                if (s.variant != Variant.None) tips.Insert(0, VariantName(s.variant) + ": " + VariantDesc(s.variant));
                tips.Insert(0, s.name.ToUpper());
                UI.Tip(new Rect(0, yy, aw - 58, 34), string.Join("\n", tips.ToArray()));
                if (UI.Button(new Rect(aw - 56, yy + 9, 52, 16), o.price + "c", res[0] >= o.price, "Buy " + s.name)) BuyOffer(o);
                yy += 37;
            }
            if (offers.Count == 0) { PixelFont.Draw(4, yy + 2, "^kSold out.", UI.Dim); yy += 14; }
            yy += 6;
            Section(2, yy, aw - 4, "CONTRACTS");
            yy += 14;
            if (!HasBuilding(BKind.Trading)) { PixelFont.Draw(4, yy, "^kBuild a Trading Office for big contracts.", UI.Dim); yy += 12; }
            else if (contract == null) { PixelFont.Draw(4, yy, "^kNo contract right now. New ones every 2 days.", UI.Dim); yy += 12; }
            else
            {
                var row = new Rect(0, yy, aw, 26);
                UI.Card(row);
                PixelFont.Draw(5, yy + 4, "Deliver ^y" + contract.amount + " " + ResInfo.Names[(int)contract.res] + "^w for ^y" + contract.reward + "c", UI.Text);
                PixelFont.Draw(5, yy + 14, "^kDue in " + Mathf.Max(0, Mathf.CeilToInt(contract.deadline - Day)) + " days. You have " + UI.Fmt(res[(int)contract.res]) + ".", UI.Dim);
                if (UI.ButtonEx(new Rect(aw - 66, yy + 5, 62, 16), "DELIVER", null, null, Has(contract.res, contract.amount), null, BtnStyle.Primary)) FulfillContract();
                yy += 30;
            }
            marketContentH = yy + 4;
            UI.EndScroll();
        }

        // ---------------- MAP ----------------
        void DrawMapPanel()
        {
            var r = PanelRect();
            if (!PanelFrame(r, "MAP & STATS", "map")) return;
            float x = r.x + 8, w = r.width - 16, y = r.y + UI.HeaderH;
            PixelFont.Draw(x, y + 1, "Land: ^y" + owned.width + " x " + owned.height + "^w tiles", UI.Text);
            y += 13;
            if (landLevel < LandCost.Length)
            {
                if (UI.Button(new Rect(x, y, 160, 18), "EXPAND LAND: " + LandCost[landLevel] + "c", res[0] >= LandCost[landLevel], "EXPAND LAND\nPush the fence out 3 tiles in every direction. More grass, more room for questionable buildings.")) ExpandLand();
            }
            else PixelFont.Draw(x, y + 6, "^kYou own all the land worth owning.", UI.Dim);
            if (UI.Button(new Rect(x + 166, y, w - 166, 18), "OVERVIEW", true, "OVERVIEW\nZoom out to see the whole settlement.")) { pixelScale = Mathf.Max(1, Mathf.FloorToInt(Screen.height / (16f * (owned.height + 6)))); FocusOn(owned.center); }
            y += 22;
            float b3 = Mathf.Floor((w - 4) / 3f);
            if (UI.Button(new Rect(x, y, b3, 18), "SAVE [F5]", true, "SAVE\nSave to " + SavePath)) SaveGame(false);
            if (UI.Button(new Rect(x + b3 + 2, y, b3, 18), "LOAD [F9]", HasSave, "LOAD\nLoad the last save (autosaves every day).")) { LoadGame(); return; }
            bool nconf = newGameConfirmT > Time.unscaledTime;
            if (UI.ButtonEx(new Rect(x + (b3 + 2) * 2, y, w - (b3 + 2) * 2, 18), nconf ? "SURE?" : "NEW GAME", null, null, true, "NEW GAME\nStart a brand new flock.", nconf ? BtnStyle.Danger : BtnStyle.Normal)) { if (nconf) { NewGameFromMenu(); return; } newGameConfirmT = Time.unscaledTime + 2.5f; }
            y += 26;
            // safety sign
            int since = dayIndex - lastAccidentDay;
            var sign = new Rect(x, y, w, 34);
            UI.Fill(sign, Pal.C("e8c030"));
            UI.Frame(sign, Pal.C("201818"), 2);
            PixelFont.DrawAligned(new Rect(sign.x, sign.y + 3, sign.width, 10), "DAYS WITHOUT AN ACCIDENT:", Pal.C("201818"), 1, 1, false);
            PixelFont.DrawAligned(new Rect(sign.x, sign.y + 14, sign.width, 16), accidents == 0 ? "ALL OF THEM" : since.ToString(), Pal.C("a01818"), 2, 1, false);
            y += 42;
            string[] stats =
            {
                "Day ^y" + (dayIndex + 1) + "^w (" + DateString() + ", Year " + Year + ")",
                "Settlement: ^y" + TierName(),
                "Sheep: ^y" + Population + "^w  (pen space " + popCap + ")",
                "Buildings: ^y" + CountBuildings(),
                "Lambs born: ^p" + births,
                "Sheep lost: ^r" + graveyard.Count + "^w  (deaths " + deaths + ")",
                "Machine accidents: ^o" + accidents,
                "Times sheared: ^y" + shearCount,
                "Wool made: ^y" + UI.Fmt(totalMade[(int)Res.Wool]),
                "Coins earned: ^y" + UI.Fmt(totalMade[0]),
                "Conveyor tiles: ^y" + beltTiles + "^w  items moved: ^y" + beltDelivered,
                "Research done: ^b" + tech.Count + "/" + Research.All.Length,
                "Storage per good: ^y" + UI.Fmt(storageCap),
                "Power: ^y" + Mathf.RoundToInt(powerMade) + "/" + Mathf.RoundToInt(powerNeed),
                "Guard power: ^y" + guardPower.ToString("0.0"),
                "Decoration mood: ^g+" + Mathf.RoundToInt(decorMood),
            };
            var sr = new Rect(x, y, w, r.yMax - 8 - y);
            UI.Well(sr);
            float sy = sr.y + 6;
            foreach (var s in stats) { if (sy > sr.yMax - 12) break; PixelFont.Draw(sr.x + 6, sy, s, UI.Text); sy += 11; }
        }

        // ---------------- SHEEP INSPECTOR ----------------
        float WorkTime(Building b)
        {
            switch (b.def.kind)
            {
                case BKind.Farm: return 6; case BKind.Workshop: return 5; case BKind.Textile: return 8; case BKind.Atelier: return 14;
                case BKind.Lab: return 5; case BKind.Market: return 4; case BKind.Creamery: return 6;
            }
            return 0;
        }

        Rect InspectorRect() { return new Rect(W - InspW - 8, NavY, InspW, Mathf.Clamp(inspContentH + UI.HeaderH + 8, 160, Mathf.Max(160, H - NavY - 8))); }

        void StatRow(float y, float aw, string label, float t, Color c, string val, string tip)
        {
            PixelFont.Draw(0, y + 1, label, UI.Text);
            UI.Bar(new Rect(34, y, aw - 34 - 30, 9), t, c);
            PixelFont.Draw(aw - PixelFont.Width(val), y + 1, val, c);
            UI.Tip(new Rect(0, y - 1, aw, 11), tip);
        }

        void MoodRow(ref float y, float aw, string val, string label)
        {
            PixelFont.Draw(0, y, val, UI.Text);
            float lh = PixelFont.WrappedHeight(label, aw - 30);
            PixelFont.DrawWrapped(new Rect(28, y, aw - 30, lh), label, UI.Text);
            y += Mathf.Max(10, lh);
        }

        void DrawSheepInspector(Sheep s)
        {
            var r = InspectorRect();
            UI.Panel(r, Trunc(s.name.ToUpper(), 20), true, "flock");
            if (UI.CloseButton(r)) { selSheep = null; followSheep = null; return; }
            var area = new Rect(r.x + 8, r.y + UI.HeaderH, r.width - 12, r.height - UI.HeaderH - 6);
            float aw = area.width - 6;
            UI.BeginScroll(area, ref scrollInsp, inspContentH);
            float y = 0;
            // portrait + identity
            UI.Well(new Rect(0, y, 46, 46), UI.Grass);
            if (s.sr != null) UI.DrawSprite(new Rect(3, y + 3, 40, 40), s.sr.sprite);
            float x2 = 52;
            PixelFont.Draw(x2, y + 2, (s.Lamb ? "^pLAMB" : "^yLV " + s.level) + "  ^k" + s.Gender, UI.Text);
            PixelFont.Draw(x2, y + 13, "XP", UI.Xp);
            UI.Bar(new Rect(x2 + 14, y + 13, aw - x2 - 14, 7), s.xp / s.XpToNext, UI.Xp);
            UI.Tip(new Rect(x2, y + 11, aw - x2, 10), "EXPERIENCE\nXP " + Mathf.FloorToInt(s.xp) + "/" + Mathf.FloorToInt(s.XpToNext) + ". Sheep gain XP every time a machine shears or milks them. Each level: +5% wool and milk, more mutton, a higher price.");
            PixelFont.Draw(x2, y + 25, "^kAge " + s.ageDays.ToString("0.0") + " days, Gen " + s.generation, UI.Dim);
            if (s.variant != Variant.None) { PixelFont.Draw(x2, y + 36, "^p" + VariantName(s.variant), UI.Text); UI.Tip(new Rect(x2, y + 34, aw - x2, 10), VariantName(s.variant).ToUpper() + "\n" + VariantDesc(s.variant)); }
            else PixelFont.Draw(x2, y + 36, "^kParents: " + Trunc(s.parents, 16), UI.Dim);
            y += 52;
            // sheep are managed, never employed: where it lives and what it is worth
            string home = s.pen != null && buildings.Contains(s.pen) ? s.pen.def.name : "^rnowhere (no pen!)";
            PixelFont.Draw(0, y, "^kLives in: ^w" + home, UI.Text); y += 11;
            if (s.strain != 0)
            {
                var sdef = Genetics.Get(s.strain);
                UI.Fill(new Rect(0, y + 1, 7, 7), Pal.C(sdef.color));
                PixelFont.Draw(10, y, "^kDNA: ^p" + sdef.name.ToUpper(), UI.Text);
                UI.Tip(new Rect(0, y - 1, aw, 10), sdef.name.ToUpper() + "\n" + sdef.desc);
                y += 11;
            }
            var slh = FindBuilding(BKind.Slaughter);
            PixelFont.Draw(0, y, "^kWorth: ^y" + LiveSheepPrice(s, null) + "c^k alive, ^y~" + MuttonYield(s, slh) + "^k mutton", UI.Text);
            UI.Tip(new Rect(0, y - 1, aw, 10), "WORTH\nSold alive (Market door or SELL): about " + LiveSheepPrice(s, null) + " coins.\nSlaughtered: about " + MuttonYield(s, slh) + " mutton.\nKept: wool every few days" + (s.male ? "" : ", milk too") + ", and lambs.");
            y += 13;
            // needs
            Color mc = s.mood > 60 ? UI.Good : s.mood > 30 ? UI.Gold : UI.Bad;
            string ms = Mathf.RoundToInt(s.mood) + (s.moodTarget > s.mood + 1 ? "^g+" : s.moodTarget < s.mood - 1 ? "^r-" : "");
            StatRow(y, aw, "MOOD", s.mood / 100f, mc, ms, "MOOD\nHeading for " + Mathf.RoundToInt(s.moodTarget) + ". Unhappy sheep work slowly, strike and escape.");
            y += 12;
            string hs = s.hunger < 0.6f ? "Fed" : s.hunger < 1.2f ? "Peckish" : s.hunger < 2f ? "Hungry" : "STARVING";
            float fed = Mathf.Clamp01(1 - s.hunger / 3.5f);
            StatRow(y, aw, "FED", fed, s.hunger < 1.2f ? UI.Good : UI.Bad, Mathf.RoundToInt(fed * 100).ToString(), "HUNGER\n" + hs + ". Every sheep eats ~4 food a day.");
            y += 12;
            StatRow(y, aw, "WOOL", s.wool / 1.25f, UI.Wool, Mathf.RoundToInt(s.wool * 100).ToString(), s.wool >= 0.98f ? "WOOL\nFluffy! Open the pen gate and send " + s.Him + " through a Shearing Machine." : "WOOL\nStill growing. Grows faster in the pen.");
            y += 12;
            if (!s.male)
            {
                StatRow(y, aw, "MILK", s.milk, Pal.C("cfe0ff"), Mathf.RoundToInt(s.milk * 100).ToString(), s.Lamb ? "MILK\nLambs don't make milk yet." : s.milk >= 0.98f ? "MILK\nFull udder! Open the pen gate and send her through a Milking Machine." : "MILK\nFilling up.");
                y += 12;
            }
            y += 3;
            // traits
            UI.Heading(0, y, "TRAITS");
            y += 11;
            float tx = 0;
            foreach (var t in s.traits)
            {
                var ti = Traits.Get(t);
                string tn = ti.name.ToUpper();
                float tw = PixelFont.Width(tn) + 8;
                if (tx > 0 && tx + tw > aw) { tx = 0; y += 15; }
                UI.Tag(tx, y, tn, ti.kind > 0 ? UI.Good : ti.kind < 0 ? UI.Bad : UI.Gold, tn + "\n" + ti.desc);
                tx += tw + 3;
            }
            if (s.traits.Count == 0) PixelFont.Draw(0, y + 3, "^kNone. Aggressively average.", UI.Dim);
            y += 18;
            float sh = PixelFont.WrappedHeight("^kStatus: ^w" + s.status, aw);
            PixelFont.DrawWrapped(new Rect(0, y, aw, sh), "^kStatus: ^w" + s.status, UI.Text); y += sh;
            y += 4;
            // actions
            float bw = Mathf.Floor((aw - 3) / 2f);
            if (s.pen != null && buildings.Contains(s.pen) && s.job == Job.Grazer && UiOn("gate"))
            {
                var pen = s.pen;
                int md = MainDoor(pen);
                if (DoorConnected(pen, md))
                {
                    bool op = pen.doorOpen[md];
                    if (UI.ButtonEx(new Rect(0, y, aw, 18), (op ? "CLOSE " : "OPEN ") + DoorNames[md] + " DOOR", op ? "close" : "play", null, true,
                        (op ? "CLOSE THE DOOR\n" : "OPEN THE DOOR\n") + "Door of the " + pen.def.name + ", leading to: " + DoorDestination(pen, md), op ? BtnStyle.Danger : BtnStyle.Primary)) ToggleDoor(pen, md);
                    y += 21;
                }
            }
            bool canPet = Day - lastPetDay(s) > 0.5f;
            if (UI.ButtonEx(new Rect(0, y, bw, 18), "PET", "heart", null, canPet, "PET\nPet " + s.Him + ". +8 mood. Once per half day.")) PetSheep(s);
            if (UI.ButtonEx(new Rect(bw + 3, y, aw - bw - 3, 18), followSheep == s ? "UNFOLLOW" : "FOLLOW", "eye", null, true, "FOLLOW\nCamera follows " + s.Him + ".", followSheep == s ? BtnStyle.Primary : BtnStyle.Normal)) followSheep = followSheep == s ? null : s;
            y += 21;
            if (UI.ButtonEx(new Rect(0, y, aw, 18), "SELL", "coin", null, !s.Lamb, "SELL\nSell to a 'nice farm upstate' for " + SheepPrice(s) / 2 + "c. The flock will notice.", BtnStyle.Danger)) { SellSheep(s); UI.EndScroll(); return; }
            y += 21;
            if (HasBuilding(BKind.CloneVat))
            {
                if (UI.ButtonEx(new Rect(0, y, aw, 18), "CLONE (400c)", null, null, res[0] >= 400 && Population < popCap + 2, "CLONE\nMake a copy of " + s.name + ". Mostly the same sheep.")) CloneSheep(s);
                y += 21;
            }
            y += 4;
            // mood reasons
            reasonsBuf.Clear();
            ComputeMoodTarget(s, reasonsBuf);
            UI.Heading(0, y, "MOOD FACTORS");
            string tg = "target " + Mathf.RoundToInt(s.moodTarget);
            PixelFont.Draw(aw - PixelFont.Width(tg), y, tg, UI.Dim);
            y += 12;
            MoodRow(ref y, aw, "^k+58", "^kBeing a sheep");
            foreach (var rs in reasonsBuf)
            {
                int sp = rs.IndexOf(" ^w");
                if (sp > 0) MoodRow(ref y, aw, rs.Substring(0, sp), rs.Substring(sp + 3));
                else { PixelFont.Draw(0, y, rs, UI.Text); y += 10; }
            }
            y += 4;
            inspContentH = y;
            UI.EndScroll();
        }

        float newGameConfirmT;
        readonly Dictionary<int, float> petDays = new Dictionary<int, float>();
        float lastPetDay(Sheep s) { float d; return petDays.TryGetValue(s.id, out d) ? d : -99; }
        void PetSheep(Sheep s)
        {
            petDays[s.id] = Day;
            AddMod(s, "Petted by management", 8, 1);
            petT = 1.5f;
            SpawnParticles(s.pos + Vector2.up * 0.8f, new Color(1f, 0.5f, 0.6f), 8, 1.5f);
            FloatText(s.pos + Vector2.up * 1.5f, "<3", new Color(1f, 0.5f, 0.6f));
            Sfx.Baa(0.7f);
        }

        void CloneSheep(Sheep s)
        {
            res[0] -= 400;
            var vat = FindBuilding(BKind.CloneVat);
            var c = CreateSheep(vat != null ? vat.Door : s.pos);
            usedNames.Remove(c.name);
            c.name = s.name + " " + (s.name.EndsWith(" II") ? "III" : "II");
            c.go.name = c.name; usedNames.Add(c.name);
            c.traits = new List<string>(s.traits);
            if (!c.traits.Contains("clone")) c.traits.Add("clone");
            c.variant = s.variant; c.woolCol = s.woolCol; c.faceCol = s.faceCol; c.baseExpr = s.baseExpr; c.male = s.male;
            c.ageDays = 3; c.generation = s.generation; c.parents = "The Vat (" + s.name + ")";
            SpawnParticles(c.pos + Vector2.up * 0.5f, new Color(0.5f, 1f, 0.6f), 16, 2.5f);
            Sfx.Play("zap");
            Log("^c" + c.name + "^w emerges from the vat, damp and staring.");
            AddMod(s, "Met their clone", -10, 2);
        }

        // ---------------- BUILDING INSPECTOR ----------------
        void DrawBuildingInspector(Building b)
        {
            var r = InspectorRect();
            UI.Panel(r, Trunc(b.def.name.ToUpper(), 22), true, Icons.ForBuilding(b.def.kind));
            if (UI.CloseButton(r)) { selBuilding = null; return; }
            var area = new Rect(r.x + 8, r.y + UI.HeaderH, r.width - 12, r.height - UI.HeaderH - 6);
            float aw = area.width - 6;
            UI.BeginScroll(area, ref scrollInsp, inspContentH);
            float y = 0;
            PixelFont.Draw(0, y, "^yLV " + b.level + "  ^w" + b.Title, UI.Text); y += 12;
            string flow = FlowLine(b.def.kind);
            if (flow != null) { PixelFont.Draw(0, y, "^c" + flow, UI.Text); y += 11; }
            if (b.construct < 1f)
            {
                UI.Bar(new Rect(0, y, aw, 10), b.construct, b.damaged ? UI.Bad : UI.Gold, (b.damaged ? "Repairing " : b.upgrading ? "Upgrading " : "Building ") + Mathf.RoundToInt(b.construct * 100) + "%", !b.damaged);
                y += 13;
            }
            if (!string.IsNullOrEmpty(b.status)) { float sh = PixelFont.WrappedHeight("^kStatus: ^w" + b.status, aw); PixelFont.DrawWrapped(new Rect(0, y, aw, sh), "^kStatus: ^w" + b.status, UI.Text); y += sh; }
            float dh = PixelFont.WrappedHeight("^k" + b.def.desc, aw);
            PixelFont.DrawWrapped(new Rect(0, y, aw, dh), "^k" + b.def.desc, UI.Dim); y += dh + 2;
            // type info
            string info = BuildingInfo(b);
            if (b.Built && tech.Contains("conveyors"))
            {
                string bi = "";
                for (int i = 0; i < ResInfo.Count; i++) if (b.inbuf[i] >= 1) bi += " " + Mathf.FloorToInt(b.inbuf[i]) + " " + ResInfo.Names[i];
                if (bi != "") info = (info == null ? "" : info + "\n") + "^cBelt buffer:^w" + bi;
                if (b.beltFedT > 0) info = (info == null ? "" : info + "\n") + "^gBelt-fed: +50% speed";
                if (IsProducer(b) && HasOutputBelt(b)) info = (info == null ? "" : info + "\n") + "^cOutputs onto conveyor";
            }
            if (info != null) { float ih = PixelFont.WrappedHeight(info, aw); PixelFont.DrawWrapped(new Rect(0, y, aw, ih), info, UI.Text); y += ih + 2; }
            if (IsRoom(b)) y = DrawPenControls(b, y, aw);
            if (HasBeltDoors(b) && !IsSheepMachine(b) && b.Built) y = DrawMachineDoors(b, y, aw);
            if (IsSheepMachine(b) && b.Built)
            {
                if (b.inMachine != null) { UI.Bar(new Rect(0, y, aw, 9), 1f - b.machineT / Mathf.Max(0.01f, b.machineDur), UI.GoldLo, Trunc(b.inMachine.name, 14) + " inside"); y += 12; }
                bool inBelt = HasBeltIn(b);
                PixelFont.Draw(0, y, inBelt ? "^gSheep line connected" : "^rNo conveyor runs INTO it!", UI.Text); y += 11;
                if (b.def.kind == BKind.GeneLab) y = DrawGeneLabControls(b, y, aw);
                y = DrawMachineDoors(b, y, aw);
            }
            if (b.def.kind == BKind.Barn)
                for (int i = 0; i < b.pairProgress.Count; i++) { UI.Bar(new Rect(0, y, aw, 9), b.pairProgress[i], Pal.C("e888a8"), "Pair " + (i + 1) + " " + Mathf.RoundToInt(b.pairProgress[i] * 100) + "%"); y += 11; }
            // recipe
            if (b.def.kind == BKind.Workshop || b.def.kind == BKind.Textile)
            {
                var rec = Recipes[b.def.kind == BKind.Workshop ? 0 : 1];
                string needTech = b.def.kind == BKind.Workshop ? "yarn" : "loom";
                UI.Heading(0, y + 4, "MAKE");
                float rx = 30;
                for (int i = 0; i < rec.Length; i++)
                {
                    float bw = PixelFont.Width(rec[i]) + 10;
                    bool en = i < 2 || tech.Contains(needTech);
                    string tip = i == 0 ? "Automatically balance both steps" : i == 1 ? (b.def.kind == BKind.Workshop ? "2 Wool -> 1 Clean Wool" : "2 Yarn -> 1 Fabric") : (b.def.kind == BKind.Workshop ? "2 Clean Wool -> 1 Yarn" : "2 Fabric -> 1 Clothing") + (en ? "" : " (needs research)");
                    if (UI.Button(new Rect(rx, y, bw, 15), rec[i], en, tip, b.recipe == i)) b.recipe = i;
                    rx += bw + 2;
                }
                y += 18;
            }
            float wt = WorkTime(b);
            if (wt > 0 && b.Built) { UI.Bar(new Rect(0, y, aw, 9), b.progress / wt, UI.GoldLo, "Cycle " + Mathf.Min(wt, b.progress).ToString("0.0") + "/" + wt); y += 12; }
            // nobody works here: it runs by itself
            string auto = AutoOutputText(b);
            if (auto != null) { float ah = PixelFont.WrappedHeight(auto, aw); PixelFont.DrawWrapped(new Rect(0, y, aw, ah), auto, UI.Text); y += ah + 2; }
            if (b.def.kind == BKind.Rocket)
            {
                string bl = LaunchBlocker(b);
                if (UI.ButtonEx(new Rect(0, y, aw, 22), "LAUNCH!", "star", null, bl == null, bl != null ? "LAUNCH\n" + bl : "LAUNCH\nSend a sheep to the moon. Probably.", BtnStyle.Primary)) { StartLaunch(b); UI.EndScroll(); return; }
                y += 25;
                if (bl != null) { float bh = PixelFont.WrappedHeight("^r" + bl, aw); PixelFont.DrawWrapped(new Rect(0, y, aw, bh), "^r" + bl, UI.Text); y += bh + 2; }
            }
            // danger
            if (b.Danger > 0 && IsSheepMachine(b))
            {
                float d = b.Danger * 100;
                UI.Heading(0, y + 1, "DANGER", UI.Bad);
                Skulls(46, y, DangerPips(b.Danger));
                string dp = d.ToString("0.0") + (IsSheepMachine(b) ? "% per sheep" : "% per cycle");
                PixelFont.Draw(aw - PixelFont.Width(dp), y + 1, dp, d >= 3 ? UI.Bad : d >= 1 ? UI.Gold : UI.Text);
                UI.Tip(new Rect(0, y - 1, aw, 24), "DANGER\nChance that a sheep gets hurt each time it goes through. Trait-dependent. Accidents range from 'shaved' to 'gone'.");
                y += 12;
                PixelFont.Draw(0, y, "^kAccidents here so far: ^w" + b.accidents, UI.Text);
                y += 12;
                if (UI.ButtonEx(new Rect(0, y, aw, 18), b.unsafeMode ? "RESTORE SAFETY GUARDS" : "REMOVE SAFETY GUARDS", null, null, true, b.unsafeMode ? "SAFETY GUARDS\nBack to normal danger." : "REMOVE SAFETY GUARDS\n+35% output. +3% accident chance per cycle. Workers -6 mood.", b.unsafeMode ? BtnStyle.Normal : BtnStyle.Danger))
                {
                    b.unsafeMode = !b.unsafeMode;
                    if (b.unsafeMode) Log("Safety guards removed from the ^y" + b.def.name + "^w. Productivity up. Eye contact down.");
                }
                y += 21;
                if (b.featureMode) { PixelFont.Draw(0, y, "^r'Turbo Mode' (exploded once): +20% output", UI.Text); y += 11; }
            }
            // upgrade
            if (b.def.maxLevel > 1)
            {
                if (b.level < b.def.maxLevel)
                {
                    string bl = UpgradeBlocker(b);
                    string label = "UPGRADE: " + b.def.levelNames[b.level].ToUpper() + " (" + b.def.cost[b.level] + "c)";
                    if (PixelFont.Width(label) > aw - 12) label = "UPGRADE TO LV" + (b.level + 1) + " (" + b.def.cost[b.level] + "c)";
                    string eff = UpgradeEffect(b);
                    if (UI.ButtonEx(new Rect(0, y, aw, 18), label, null, null, bl == null, bl != null ? "UPGRADE\n" + bl : "UPGRADE: " + b.def.levelNames[b.level].ToUpper() + "\n" + (eff ?? "Bigger, faster, more dangerous."))) Upgrade(b);
                    y += 21;
                    if (eff != null) { float eh = PixelFont.WrappedHeight(eff, aw); PixelFont.DrawWrapped(new Rect(0, y, aw, eh), eff, UI.Text); y += eh + 2; }
                    if (bl != null && bl != "Under construction") { float bh = PixelFont.WrappedHeight("^r" + bl, aw); PixelFont.DrawWrapped(new Rect(0, y, aw, bh), "^r" + bl, UI.Text); y += bh + 2; }
                }
                else { PixelFont.Draw(0, y + 2, "^gMax level.", UI.Text); y += 13; }
            }
            // demolish
            bool confirm = demolishConfirmT > Time.unscaledTime;
            if (UI.ButtonEx(new Rect(0, y, aw, 16), confirm ? "CLICK AGAIN TO DEMOLISH" : "DEMOLISH (+" + b.def.cost[0] / 2 + "c)", null, null, !(b.def.kind == BKind.House && CountKind(BKind.House) <= 1) && b.def.kind != BKind.Palace, "DEMOLISH\nTear it down. Workers go back to grazing.", BtnStyle.Danger))
            {
                if (confirm) { demolishConfirmT = 0; Demolish(b); UI.EndScroll(); return; }
                demolishConfirmT = Time.unscaledTime + 2.5f;
            }
            y += 20;
            inspContentH = y;
            UI.EndScroll();
        }

        public int CountKind(BKind k) { int n = 0; foreach (var b in buildings) if (b.def.kind == k) n++; return n; }
        float DrawPenControls(Building b, float y, float aw)
        {
            bool lot = IsLot(b);
            int cap = RoomCapacity(b);
            PixelFont.Draw(0, y, (lot ? "In the lot: ^y" : "In the pen: ^y") + b.penCount + "^w/" + cap + "   hungry: ^o" + b.penHungry + "^w   fed: ^g" + b.penFed, UI.Text); y += 11;
            if (!lot)
            {
                PixelFont.Draw(0, y, "^kLives here: ^w" + b.penMembers + "^k   out and about: ^w" + b.penOut, UI.Text); y += 11;
                if (b.penMembers > cap) { PixelFont.Draw(0, y, "^rOvercrowded! Upgrade it or build another pen.", UI.Text); y += 11; }
                UI.Bar(new Rect(0, y, aw, 9), b.breedProgress, Pal.C("e888a8"), "Next lamb " + Mathf.RoundToInt(b.breedProgress * 100) + "%"); y += 12;
                PixelFont.Draw(0, y, "^kBreeding speed: ^yx" + (PenBreedMult(b) * (tech.Contains("matchmaking") ? 1.5f : 1f) * (HasGene("love_songs") ? 1.3f : 1f)).ToString("0.0#") + "^k (upgrades, Matchmaking, Love Songs)", UI.Text); y += 11;
            }
            else
            {
                PixelFont.Draw(0, y, "^kEats ^w" + LotEatRate(b).ToString("0.0") + "^k hunger/s. Food in store: ^y" + UI.Fmt(res[(int)Res.Food]), UI.Text); y += 11;
                PixelFont.Draw(0, y, "^kOnly FED sheep leave a Feeding Lot.", UI.Dim); y += 11;
            }
            UI.Heading(0, y + 3, "DOORS"); y += 14;
            if (!UiOn("gate")) { PixelFont.Draw(0, y, "^kThe Supreme Leader holds the keys.", UI.Dim); return y + 14; }
            bool filters = tech.Contains("fluff_detector");
            for (int i = 0; i < 4; i++)
            {
                bool con = DoorConnected(b, i);
                var br = new Rect(0, y, 44, 15);
                UI.Mark("roomdoor_" + i, br);
                if (con)
                {
                    bool op = b.doorOpen[i];
                    if (UI.ButtonEx(br, op ? "CLOSE" : "OPEN", null, null, true, (op ? "CLOSE" : "OPEN") + " THE " + DoorNames[i] + " DOOR\nLeads to: " + DoorDestination(b, i), op ? BtnStyle.Danger : BtnStyle.Primary)) ToggleDoor(b, i);
                }
                else UI.ButtonEx(br, "-", null, null, false, DoorNames[i] + " DOOR\nNo conveyor under this door. Lay a belt that starts right outside it and leads away.");
                string fn = filters && con ? FilterNames[b.doorFilter[i]] : null;
                float fw = fn != null ? PixelFont.Width(fn) + 10 : 0;
                string txt = "^y" + DoorNames[i] + "^w " + (con ? "to " + Trunc(DoorDestination(b, i), fn != null ? 14 : 22) : "^k(no conveyor)");
                PixelFont.Draw(48, y + 4, txt, UI.Text);
                if (fn != null && UI.ButtonEx(new Rect(aw - fw, y, fw, 15), fn, null, null, true, "DOOR FILTER (click to change)\n" + FilterTips[b.doorFilter[i]], b.doorFilter[i] > 0 ? BtnStyle.Primary : BtnStyle.Normal)) CycleFilter(b, i);
                y += 17;
            }
            if (!filters) { PixelFont.Draw(0, y, "^kResearch Smart Doors to add door filters.", UI.Dim); y += 11; }
            return y + 3;
        }

        float DrawMachineDoors(Building b, float y, float aw)
        {
            UI.Heading(0, y + 3, "DOORS"); y += 14;
            if (!UiOn("gate")) { PixelFont.Draw(0, y, "^kThe Supreme Leader holds the keys.", UI.Dim); return y + 14; }
            bool filters = tech.Contains("fluff_detector");
            bool any = false;
            for (int i = 0; i < 4; i++)
            {
                string role = MachineDoorRole(b, i);
                if (role == null) continue;
                any = true;
                string dest = MachineDoorDestination(b, i);
                if (role == "MUTTON")
                {
                    PixelFont.Draw(0, y + 4, "^y" + DoorNames[i] + "^w chute: mutton to " + Trunc(dest ?? "?", 16), UI.Text);
                    UI.Tip(new Rect(0, y, aw, 15), MachineDoorTip(b, i));
                    y += 17; continue;
                }
                bool op = MachineDoorOpen(b, i);
                var br = new Rect(0, y, 44, 15);
                UI.Mark("machinedoor_" + i, br);
                if (UI.ButtonEx(br, op ? "CLOSE" : "OPEN", null, null, true, MachineDoorTip(b, i), op ? BtnStyle.Danger : BtnStyle.Primary)) ToggleMachineDoor(b, i);
                string fn = filters && role == "OUT" && op ? FilterNames[b.doorFilter[i]] : null;
                float fw = fn != null ? PixelFont.Width(fn) + 10 : 0;
                string what = role == "IN" ? (b.def.kind == BKind.Market ? (op ? "^gIN^w: all sold" : "^rIN shut^w: they wait") : !IsSheepMachine(b) ? (op ? "^gIN^w: goods in" : "^rIN shut^w: goods wait") : op ? "^gIN^w: sheep ride in" : "^rIN shut^w: they wait")
                            : op ? "^gOUT^w to " + Trunc(dest ?? "?", fn != null ? 9 : 15) : "^r" + MachineGoods(b) + "^w to " + Trunc(dest ?? "?", fn != null ? 8 : 13);
                PixelFont.Draw(48, y + 4, "^y" + DoorNames[i] + "^w " + what, UI.Text);
                if (fn != null && UI.ButtonEx(new Rect(aw - fw, y, fw, 15), fn, null, null, true, "DOOR FILTER (click to change)\n" + FilterTips[b.doorFilter[i]], b.doorFilter[i] > 0 ? BtnStyle.Primary : BtnStyle.Normal)) CycleFilter(b, i);
                y += 17;
            }
            if (!any) { PixelFont.Draw(0, y, "^kNo conveyor touches it yet.", UI.Dim); y += 11; }
            else if (IsSheepMachine(b) && b.def.kind != BKind.Slaughter && !AnySheepExit(b)) { PixelFont.Draw(0, y, "^rNo OUT door open: sheep wait inside!", UI.Text); y += 11; }
            if (any && IsSheepMachine(b) && b.def.kind != BKind.Slaughter && filters) { PixelFont.Draw(0, y, "^kFilters sort sheep on the way OUT.", UI.Dim); y += 11; }
            return y + 3;
        }

        int BuildersAt(Building b) { int n = 0; foreach (var s in sheep) if (!s.dead && s.job == Job.Builder && s.workplace == b && s.act == Act.Building) n++; return n; }

        string BuildingInfo(Building b)
        {
            switch (b.def.kind)
            {
                case BKind.House: return "Old sheep housing. The flock lives in Breeding Pens now.";
                case BKind.Farm: return null;
                case BKind.Shed: return "Wool per fluffy sheep: ^yx" + BuildingDef.At(ShedYield, b.level).ToString("0.0") + "^w. Sheep through here: ^y" + b.cyclesDone + "^w.";
                case BKind.Plant: return "Shears at ^y2.5x^w yield, very fast. Sheep processed: ^y" + b.cyclesDone + "^w. Power " + Mathf.RoundToInt(powerFactor * 100) + "%.";
                case BKind.Storage: return "Holds +" + StorageCapOf(b.level) * 1.25f + " of every good. Total cap: ^y" + UI.Fmt(storageCap);
                case BKind.Wheel: return "Power made: ^y" + Mathf.RoundToInt(powerMade) + "^w / needed " + Mathf.RoundToInt(powerNeed);
                case BKind.Lab: return null;
                case BKind.Market: return "Also sells up to " + (2 + b.level * 2) + " stored AUTO goods every few seconds. Prices +" + (b.level - 1) * 5 + "%.";
                case BKind.GuardPost: return "Total guard power: ^y" + guardPower.ToString("0.0");
                case BKind.Bank: return "Daily interest on coins, per banker. Max " + 150 * b.level + "c/day.";
                case BKind.Trading: return contract != null ? "Contract: " + contract.amount + " " + ResInfo.Names[(int)contract.res] + " for " + contract.reward + "c" : "Waiting for a contract.";
                case BKind.Graveyard: return "Tombstones: ^y" + tombs.Count + "^w. Mourning is shorter.";
                case BKind.Barn: return "Ewes + rams in pairs. Lambs need free beds.";
                case BKind.Funnel: return "Loads up to " + b.level + " sheep at a time onto its output belt.";
                case BKind.Palace: return "The Supreme Leader lives here. He is watching.";
                case BKind.Trough: return "Food in store: ^y" + UI.Fmt(res[(int)Res.Food]) + "^w. Hungry sheep walk here to eat. Farms and Grazers fill it.";
                case BKind.Dairy: return "Milk per full ewe: ^yx" + BuildingDef.At(ShedYield, b.level).ToString("0.0") + "^w. Sheep through here: ^y" + b.cyclesDone + "^w. ^kRams pass through, offended.";
                case BKind.Pen: case BKind.FeedLot: return null;
                case BKind.GeneLab: return "Sheep through here: ^y" + b.cyclesDone + "^w. DNA in the bank: ^p" + dna + "^w.";
                case BKind.Slaughter: return "Mutton per sheep: ^y~" + MuttonYield(null, b) + "^w x size. Sheep processed: ^y" + b.cyclesDone + "^w.";
                case BKind.Creamery: return "3 Milk -> 1 Cheese. Milk in store: ^y" + UI.Fmt(res[(int)Res.Milk]);
                case BKind.Spa: case BKind.Statue: case BKind.Flowers: return "Flock decoration mood: ^g+" + Mathf.RoundToInt(decorMood);
            }
            return null;
        }
    }
}
