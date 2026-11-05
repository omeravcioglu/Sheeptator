using UnityEngine;

namespace Fleeced
{
    /// The UPGRADES menu (U): every upgrade path, bought x1, x10, x25, x100 or MAX at a time. Milestones double a path's bonus.
    /// The building inspector's UPGRADES button opens it on that building's paths.
    public partial class Game
    {
        float scrollUpgrades, upgradesContentH;
        int upgradeAmount;            // 0 x1, 1 x10, 2 x25, 3 x100, 4 MAX
        BKind? upgradeFocus;          // the inspector's building kind: its paths come first
        static readonly string[] AmountLabels = { "x1", "x10", "x25", "x100", "MAX" };
        static readonly int[] AmountN = { 1, 10, 25, 100, -1 };

        /// opens UPGRADES with `kind`'s paths on top (null: no focus)
        public void OpenUpgrades(BKind? kind)
        {
            upgradeFocus = kind;
            scrollUpgrades = 0;
            panel = Panel.Upgrades;
        }

        /// how many levels the chosen amount buys of `d` now (MAX: as many as the coins cover)
        int UpgradeN(UpgradeDef d)
        {
            int n = AmountN[upgradeAmount];
            if (n < 0) return PathMaxBuy(d);
            return Mathf.Min(n, d.MaxLevel - PathLevel(d.id));
        }

        void DrawUpgradesPanel()
        {
            var r = PanelRect();
            if (!PanelFrame(r, "UPGRADES", "star")) return;
            float x = r.x + 8, w = r.width - 16, y = r.y + UI.HeaderH;
            int na = Tabs(new Rect(x, y, w, 18), AmountLabels, upgradeAmount);
            if (na != upgradeAmount) upgradeAmount = na;
            y += 24;
            var area = new Rect(x, y, w, r.yMax - 8 - y);
            float aw = area.width - 6;
            UI.BeginScroll(area, ref scrollUpgrades, upgradesContentH);
            float yy = 0;
            UI.Icon(0, yy, "coin");
            PixelFont.Draw(13, yy + 1, "^y" + UI.Fmt(res[0]) + "^k coins.  Milestone levels double a bonus.", UI.Text);
            yy += 14;
            // the inspected building's paths first, then the rest in table order; paths the gate has not unlocked yet stay hidden
            int hidden = 0;
            if (upgradeFocus.HasValue)
                foreach (var d in Upgrades.All) if (!d.global && d.kind == upgradeFocus.Value) { if (PathUnlocked(d)) DrawPathRow(d, ref yy, aw); else hidden++; }
            foreach (var d in Upgrades.All) if (!(upgradeFocus.HasValue && !d.global && d.kind == upgradeFocus.Value)) { if (PathUnlocked(d)) DrawPathRow(d, ref yy, aw); else hidden++; }
            if (hidden > 0) { PixelFont.Draw(6, yy + 2, "^k" + hidden + " more upgrade" + (hidden > 1 ? "s unlock" : " unlocks") + " as the farm grows.", UI.Dim); yy += 14; }
            upgradesContentH = yy + 4;
            UI.EndScroll();
        }

        void DrawPathRow(UpgradeDef d, ref float yy, float aw)
        {
            int lv = PathLevel(d.id), n = UpgradeN(d);
            bool maxed = lv >= d.MaxLevel;
            string bl = PathBlocker(d, n);
            bool locked = !d.global && !HasBuilding(d.kind);
            double cost = n > 0 ? PathCost(d, n) : PathCost(d, 1);
            int next = UpgradeMath.NextMilestone(lv);
            if (next > d.MaxLevel) next = 0; // the path ends before it
            string ms = maxed ? "^gFully upgraded" : next > 0 ? "^kLv " + next + ": x2, " + (next - lv) + " to go"
                : d.MaxLevel < UpgradeMath.Milestones[0] ? "^kNo milestones: Lv " + d.MaxLevel + " is the top" : "^kEvery milestone passed";
            const float ch = 46;
            var row = new Rect(0, yy, aw, ch);
            UI.Card(row, false, locked, UI.Hover(row));
            string where = d.global ? "FARM" : Buildings.Get(d.kind).name.ToUpper();
            PixelFont.Draw(6, yy + 4, (locked ? "^k" : "^w") + d.name.ToUpper() + "^k  " + where, UI.Text);
            string lvText = "Lv " + lv;
            PixelFont.Draw(aw - 6 - PixelFont.Width(lvText), yy + 4, lvText, UI.Gold);
            string eff = maxed || n <= 0 ? d.Show(lv) : d.Show(lv) + " ^k->^g " + d.Show(lv + n);
            PixelFont.Draw(6, yy + 15, "^w" + eff, UI.Text);
            PixelFont.Draw(6, yy + 25, ms, UI.Dim);
            var br = new Rect(aw - 96, yy + 24, 90, 17);
            string label = maxed ? "MAXED" : (n > 0 ? "+" + n + " " : "") + UI.Fmt(cost) + "c";
            UI.Mark("path_" + d.id, br);
            string tip = d.name.ToUpper() + "\n" + d.desc + "\nNow " + d.Show(lv) + (maxed ? "" : ", next " + d.Show(lv + 1)) + (bl != null && !maxed ? "\n^r" + bl : "");
            if (UI.ButtonEx(br, label, "coin", null, bl == null, tip, BtnStyle.Primary)) BuyPath(d, n);
            yy += ch + 3;
        }
    }
}
