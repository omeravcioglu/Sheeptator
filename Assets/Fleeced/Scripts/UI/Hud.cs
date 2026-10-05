using System.Collections.Generic;
using UnityEngine;

namespace Fleeced
{
    public enum Panel { None, Build, Sheep, Jobs, Research, Market, Map, Genetics }

    public partial class Game
    {
        public Panel panel;
        public bool showHelp;
        float W, H;
        float scrollBuild, scrollSheep, scrollResearch, scrollMarket, scrollJobs, scrollInsp, scrollMap, scrollHelp;
        float inspContentH = 400;
        int sheepTab, buildTab;
        bool jobDropdown;
        float demolishConfirmT;
        float chipLimit;
        readonly List<string> reasonsBuf = new List<string>();

        // layout (virtual UI pixels)
        const float TopH = 28, NavX = 8, NavY = 36, NavW = 88, NavH = 22, PanelX = 104, PanelW = 282, InspW = 220;

        bool InspectorOpen { get { return (selSheep != null && !selSheep.dead) || (selBuilding != null && buildings.Contains(selBuilding)); } }
        bool ToolActive { get { return placing != null || pathTool || beltTool; } }
        /// the strip of world between the left panel (or nav) and the inspector
        float FreeLeft { get { return panel != Panel.None ? PanelX + PanelW + 8 : NavX + NavW + 8; } }
        float FreeRight { get { return InspectorOpen ? W - InspW - 16 : W - 8; } }

        void UpdateUIScale()
        {
            // integer scales only, so every UI pixel is a crisp block. Aim for a ~540px tall virtual screen,
            // but never shorter than 400 or narrower than 760 virtual pixels.
            float target = Mathf.Round(Screen.height / 540f);
            float most = Mathf.Floor(Mathf.Min(Screen.height / 400f, Screen.width / 760f));
            UI.scale = Mathf.Max(1f, Mathf.Min(target, most));
        }

        bool PointerOverUI()
        {
            if (!started || currentEvent != null || showHelp || launchSeq > 0 || LeaderModal) return true;
            Vector2 m = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            foreach (var r in UI.blockersPrev) if (r.Contains(m)) return true;
            foreach (var r in bubbleRectsPrev) if (r.Contains(m)) return true;
            return false;
        }

        public void TogglePanel(Panel p)
        {
            panel = panel == p ? Panel.None : p;
            if (panel != Panel.Build) { placing = null; pathTool = false; beltTool = false; clearTool = false; }
            Sfx.Play("click", 0.4f);
        }

        void OnGUI()
        {
            float s = UI.scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1));
            W = Screen.width / s; H = Screen.height / s;
            UI.BeginFrame();
            bool modal = currentEvent != null || showHelp || !started || launchSeq > 0 || LeaderModal;
            UI.inputEnabled = !modal;

            DrawNight();
            DrawWorldOverlays(s);
            if (started && launchSeq <= 0) DrawGateLabels(s);
            DrawWorldTexts(s);
            if (!started)
            {
                UI.inputEnabled = !showHelp;
                CheckTitleSave();
                DrawTitle();
                if (showHelp) { UI.inputEnabled = true; DrawHelp(); }
                UI.DrawTooltip(W, H);
                UI.inputEnabled = true;
                return;
            }
            if (launchSeq > 0) { DrawBanners(); return; }

            DrawTopBar();
            DrawNav();
            DrawDecree();
            DrawLog();
            switch (panel)
            {
                case Panel.Build: DrawBuildPanel(); break;
                case Panel.Sheep: DrawSheepPanel(); break;
                case Panel.Jobs: DrawFactoryPanel(); break;
                case Panel.Genetics: DrawGeneticsPanel(); break;
                case Panel.Research: DrawResearchPanel(); break;
                case Panel.Market: DrawMarketPanel(); break;
                case Panel.Map: DrawMapPanel(); break;
            }
            if (selSheep != null && !selSheep.dead) DrawSheepInspector(selSheep);
            else if (selBuilding != null && buildings.Contains(selBuilding)) DrawBuildingInspector(selBuilding);
            DrawPlacementHint();
            DrawToast();
            DrawAutosaveNote();
            DrawBanners();
            DrawTutorialPointer(s);
            DrawLeaderPop();
            if (showHelp) { UI.inputEnabled = true; DrawHelp(); }
            if (currentEvent != null) { UI.inputEnabled = true; DrawEvent(currentEvent); }
            DrawFliers();
            if (LeaderModal && currentEvent == null && !showHelp) DrawLeaderDialog();
            UI.DrawTooltip(W, H);
            UI.inputEnabled = true;
        }

        Rect PanelRect() { return new Rect(PanelX, NavY, PanelW, Mathf.Max(220, H - NavY - 8)); }

        // ---------------- world overlays ----------------
        Vector2 ToGui(Vector2 world, float s)
        {
            Vector3 sp = cam.WorldToScreenPoint(world);
            return new Vector2(sp.x / s, (Screen.height - sp.y) / s);
        }

        void DrawNight()
        {
            float tod = Day - Mathf.Floor(Day);
            float dark = 0;
            if (tod > 0.72f) dark = Mathf.InverseLerp(0.72f, 0.86f, tod);
            if (tod < 0.12f) dark = 1 - Mathf.InverseLerp(0.0f, 0.12f, tod);
            dark = Mathf.Clamp01(dark) * 0.38f;
            if (dark > 0.01f) UI.Fill(new Rect(0, 0, W, H), new Color(0.04f, 0.04f, 0.16f, dark));
            if (Season == 3) UI.Fill(new Rect(0, 0, W, H), new Color(0.6f, 0.7f, 0.9f, 0.06f));
        }

        float Bob { get { return (Mathf.FloorToInt(Time.unscaledTime * 2.5f) % 2) * 2f; } }

        void DrawWorldOverlays(float s)
        {
            if (!UI.Repaint) return;
            float bob = Bob;
            if (started) DrawBlueprints(s);
            // construction bars & problem markers
            foreach (var b in buildings)
            {
                if (IsRoom(b) && b.construct >= 1f) continue;
                Vector2 top = ToGui(new Vector2(b.x + b.def.w / 2f, b.y + b.def.h + 0.9f), s);
                if (b.construct < 1f)
                {
                    UI.Bar(new Rect(Mathf.Round(top.x - 18), Mathf.Round(top.y), 36, 6), b.construct, b.damaged ? UI.Bad : UI.Gold, null, true);
                    continue;
                }
                if (pixelScale >= 2 && IsProducer(b) && (b.status.StartsWith("Needs") || b.status.StartsWith("No room") || b.status.StartsWith("Not enough")))
                    UI.Icon(Mathf.Round(top.x - 3), Mathf.Round(top.y - 6 - bob), "exclaim_red");
                else if (pixelScale >= 2 && IsSheepMachine(b) && !b.damaged && (!HasBeltIn(b) || b.status.StartsWith("NO WAY")))
                    UI.Icon(Mathf.Round(top.x - 3), Mathf.Round(top.y - 6 - bob), "exclaim_red");
            }
            if (selBuilding != null && buildings.Contains(selBuilding))
            {
                var b = selBuilding;
                Vector2 a = ToGui(new Vector2(b.x, b.y + b.def.h), s), c = ToGui(new Vector2(b.x + b.def.w, b.y), s);
                float L = 5;
                Color g = UI.Gold;
                UI.Fill(new Rect(a.x, a.y, L, 1), g); UI.Fill(new Rect(a.x, a.y, 1, L), g);
                UI.Fill(new Rect(c.x - L, a.y, L, 1), g); UI.Fill(new Rect(c.x - 1, a.y, 1, L), g);
                UI.Fill(new Rect(a.x, c.y - 1, L, 1), g); UI.Fill(new Rect(a.x, c.y - L, 1, L), g);
                UI.Fill(new Rect(c.x - L, c.y - 1, L, 1), g); UI.Fill(new Rect(c.x - 1, c.y - L, 1, L), g);
            }
            foreach (var w in wolves)
            {
                if (!w.gerald || w.go == null) continue;
                Vector2 p = ToGui(w.pos + Vector2.up * 1.3f, s);
                float tw = PixelFont.Width("Gerald");
                PixelFont.DrawOutlined(Mathf.Round(p.x - tw / 2), Mathf.Round(p.y), "Gerald", Icons.UiColor('L'));
            }
            Sheep[] tags = { hoverSheep, selSheep };
            foreach (var sh in tags)
            {
                if (sh == null || sh.dead || sh.go == null || sh.act == Act.Hidden) continue;
                if (sh == tags[0] && sh == selSheep) continue; // drawn once, as the selection
                float hgt = sh.Lamb ? 0.9f : 1.45f;
                Vector2 p = ToGui(sh.pos + Vector2.up * hgt, s);
                string nm = sh.name, lv = sh.Lamb ? "lamb" : "Lv" + sh.level;
                float nw = PixelFont.Width(nm), lw = PixelFont.Width(lv);
                float tw = nw + 5 + lw;
                var r = new Rect(Mathf.Round(p.x - tw / 2 - 4), Mathf.Round(p.y - 5), tw + 8, 12);
                UI.Fill(r, new Color(0.07f, 0.055f, 0.047f, 0.8f));
                PixelFont.Draw(r.x + 4, r.y + 2, nm, UI.Text);
                PixelFont.Draw(r.x + 4 + nw + 5, r.y + 2, lv, UI.Dim);
                Color mc = sh.mood > 60 ? UI.Good : sh.mood > 30 ? UI.Gold : UI.Bad;
                UI.Fill(new Rect(r.x, r.yMax, Mathf.Round(r.width * Mathf.Clamp01(sh.mood / 100f)), 1), mc);
                if (sh == selSheep) UI.Icon(Mathf.Round(p.x - 3), r.y - 8 - bob, "arrow");
            }
        }

        // ---------------- top bar ----------------
        float Chip(float x, string icon, string v, string sub, string tip, Color? vcol = null)
        {
            float w = Mathf.Max(46, 18 + Mathf.Max(PixelFont.Width(v), PixelFont.Width(sub)) + 9);
            if (x + w > chipLimit) return x;
            var r = new Rect(x, 0, w, TopH - 2);
            UI.Tip(r, tip);
            if (UI.Hover(r)) UI.Fill(r, new Color(1, 1, 1, 0.05f));
            UI.Mark("chip_" + icon, r);
            float pu = ChipPulse(icon);
            if (pu > 0) UI.Fill(r, new Color(1f, 0.86f, 0.35f, Mathf.Min(0.45f, pu * 1.4f)));
            var t = Icons.Tex(icon);
            UI.Icon(x + 5, Mathf.Round(13 - t.height / 2f), icon);
            PixelFont.Draw(x + 18, 5, v, vcol ?? UI.Text);
            PixelFont.Draw(x + 18, 16, sub, UI.Dim);
            UI.Fill(new Rect(x + w - 1, 5, 1, 17), UI.Bg);
            return x + w;
        }

        bool IsFull(Res r) { return res[(int)r] >= CapOf(r) - 0.5f; }

        void DrawTopBar()
        {
            var bar = new Rect(0, 0, W, TopH);
            UI.Block(bar);
            UI.Fill(bar, UI.BgDark);
            UI.Fill(new Rect(0, TopH - 2, W, 1), UI.Edge);
            UI.Fill(new Rect(0, TopH - 1, W, 1), UI.Ink);

            // right side: help, sound, speed, date
            float rx = W - 4;
            if (UI.ButtonEx(new Rect(rx - 20, 3, 20, 20), "?", null, null, true, "Help [H]", showHelp ? BtnStyle.Primary : BtnStyle.Normal)) showHelp = !showHelp;
            rx -= 22;
            if (UI.IconButton(new Rect(rx - 20, 3, 20, 20), Sfx.muted ? "mute" : "speaker", true, Sfx.muted ? "Sound off" : "Sound on")) Sfx.muted = !Sfx.muted;
            rx -= 28;
            string[] ic = { "pause", "play", "ff", "fff" };
            for (int i = 3; i >= 0; i--)
            {
                float bw = i == 3 ? 24 : 20;
                UI.Mark("speed_" + i, new Rect(rx - bw, 3, bw, 20));
                if (UI.IconButton(new Rect(rx - bw, 3, bw, 20), ic[i], true, i == 0 ? "Pause [Space]" : "Speed x" + Speeds[i] + " [" + i + "]", speed == i)) speed = i;
                rx -= bw + 2;
            }
            rx -= 8;
            string d1 = DateString().ToUpper(), d2 = "Year " + Year;
            float d2w = PixelFont.Width(d2);
            float dw = Mathf.Max(PixelFont.Width(d1), d2w + 4 + 26);
            float dx = rx - dw;
            string sIcon = Season == 0 ? "sprout" : Season == 1 ? "sun" : Season == 2 ? "leaf" : "snow";
            UI.Icon(dx - 13, 9, sIcon);
            PixelFont.Draw(dx, 5, d1, UI.Gold);
            PixelFont.Draw(dx, 16, d2, UI.Dim);
            float tod = Day - Mathf.Floor(Day);
            var track = new Rect(dx + d2w + 4, 16, 26, 7);
            UI.Fill(track, UI.Ink);
            UI.Fill(UI.Inset(track, 1), UI.Bg);
            bool night = tod > 0.72f || tod < 0.12f;
            UI.Fill(new Rect(Mathf.Round(track.x + 1 + (track.width - 5) * tod), track.y + 1, 3, 5), night ? UI.Dim : UI.Gold);
            UI.Tip(new Rect(dx - 15, 0, dw + 15, TopH), Seasons[Season].ToUpper() + "\n" + SeasonTip());
            chipLimit = dx - 22;

            // left side: resources
            float x = 2;
            bool wFull = IsFull(Res.Wool), fFull = IsFull(Res.Food);
            x = Chip(x, "coin", UI.Fmt(res[0]), UI.Rate(rate[0] * 60), "COINS\n" + ResInfo.Desc[0]);
            x = Chip(x, "wool", UI.Fmt(res[1]), wFull ? "^rFULL" : UI.Rate(rate[1] * 60), "WOOL (" + UI.Fmt(res[1]) + "/" + UI.Fmt(storageCap) + ")\n" + ResInfo.Desc[1] + "\nFluffy sheep ride the conveyor through a Shearing Machine." + (wFull ? "\n^rStorage is full. Sell wool or build a Storage Shed." : ""), wFull ? UI.Bad : (Color?)null);
            bool mFull = IsFull(Res.Milk);
            x = Chip(x, "milk", UI.Fmt(res[(int)Res.Milk]), mFull ? "^rFULL" : UI.Rate(rate[(int)Res.Milk] * 60), "MILK (" + UI.Fmt(res[(int)Res.Milk]) + "/" + UI.Fmt(storageCap) + ")\n" + ResInfo.Desc[(int)Res.Milk] + "\nEwes with a full (pink) udder ride through a Milking Machine." + (mFull ? "\n^rStorage is full. Sell milk or make cheese." : ""), mFull ? UI.Bad : (Color?)null);
            x = Chip(x, "food", UI.Fmt(res[7]), fFull ? "^yFULL" : UI.Rate(rate[7] * 60), "FOOD (" + UI.Fmt(res[7]) + "/" + UI.Fmt(CapOf(Res.Food)) + ")\n" + ResInfo.Desc[7] + "\nFarms grow it by themselves. Feeding Lots feed it to the sheep (about 4 a day each).");
            int pop = Population;
            x = Chip(x, "flock", pop + "/" + popCap, pop > popCap ? "^rcrowded" : "^kflock", "POPULATION\nSheep / space in your Breeding Pens. Lambs are only born while there is room: upgrade or build pens to grow.", pop > popCap ? UI.Bad : (Color?)null);
            float hm = AvgMood();
            x = Chip(x, hm >= 60 ? "happy" : hm >= 35 ? "meh" : "sad", Mathf.RoundToInt(hm) + "%", UI.Rate(happinessRate * 60), "HAPPINESS\nAverage flock mood. Low mood = slow work, strikes, escapes. High mood = faster work and breeding.");
            foreach (int i in new[] { 10, 11, 13, 12, 14, 15, 16, 2, 9, 3, 4, 5, 6 })
            {
                if (res[i] < 0.5 && totalMade[i] < 0.5) continue;
                x = Chip(x, ResInfo.Icon[i], UI.Fmt(res[i]), UI.Rate(rate[i] * 60), ResInfo.Names[i].ToUpper() + " (" + UI.Fmt(res[i]) + "/" + UI.Fmt(storageCap) + ")\n" + ResInfo.Desc[i] + "\nSells for " + SellPrice((Res)i).ToString("0.0") + "c");
            }
            if (HasBuilding(BKind.Lab) || rpPool > 0)
                x = Chip(x, "research", UI.Fmt(rpPool), currentResearch == null ? "^ridle!" : "^bRP", "RESEARCH POINTS\nUnspent RP. Pick a research project in the Research panel [R].");
            if (tech.Contains("genetics") || dna > 0)
                x = Chip(x, "dna", dna.ToString(), "^pDNA", "DNA\nA Gene Lab in SAMPLE mode collects 1 per sheep that rides through. The Slaughterhouse gives 1 per sheep (3 for special ones).\nSpend it in GENETICS [N].");
            if (powerNeed > 0 || powerMade > 0)
                x = Chip(x, "power", Mathf.RoundToInt(powerMade) + "/" + Mathf.RoundToInt(powerNeed), powerFactor < 0.99f ? "^rlow" : "^gok", "POWER made / needed\nSheep running in Power Wheels make power. Lv3 buildings and the Wool Plant need it.");
        }

        string SeasonTip()
        {
            switch (Season)
            {
                case 0: return "Grass grows fast. Everything is hopeful.";
                case 1: return "Very fluffy sheep overheat. Shear them.";
                case 2: return "Grass grows slower. Leaves fall. So do sheep.";
                default: return "Grass barely grows. Shaved sheep freeze. Stock up on food!";
            }
        }

        // ---------------- nav ----------------
        void DrawNav()
        {
            string[] labels = { "BUILD", "FLOCK", "FACTORY", "GENETICS", "RESEARCH", "MARKET", "MAP" };
            string[] icons = { "hammer", "flock", "chart", "dna", "research", "bag", "map" };
            string[] keys = { "B", "F", "O", "N", "R", "T", "M" };
            string[] tips =
            {
                "BUILD\nRooms, machines, farms, markets and worse.",
                "THE FLOCK\nEvery sheep, the memorial and the full log.",
                "FACTORY\nYesterday's numbers: lambs born vs. sheep lost, food grown vs. eaten, what every machine did.",
                "GENETICS\nSpend DNA on breeding upgrades and new STRAINS of sheep: Silk, Cream, Jumbo, Gold, Glow.",
                "RESEARCH\nScience. Explosions are part of it.",
                "MARKET\nSell goods, buy food, buy sheep.",
                "MAP & STATS\nLand, saving, and grim statistics.",
            };
            Panel[] ps = { Panel.Build, Panel.Sheep, Panel.Jobs, Panel.Genetics, Panel.Research, Panel.Market, Panel.Map };
            string[] ids = { "build", "flock", "jobs", "genetics", "research", "market", "map" };
            int k = 0;
            for (int i = 0; i < labels.Length; i++)
            {
                if (!UiOn(ids[i]) || ids[i] == "genetics" && !tech.Contains("genetics")) continue;
                var r = new Rect(NavX, NavY + k * (NavH + 4), NavW, NavH);
                k++;
                UI.Mark("nav_" + ids[i], r);
                UI.Block(r);
                bool sel = panel == ps[i];
                if (UI.ButtonEx(r, labels[i], icons[i], keys[i], true, tips[i], sel ? BtnStyle.Primary : BtnStyle.Normal, true)) TogglePanel(ps[i]);
                bool alert = ps[i] == Panel.Research && HasBuilding(BKind.Lab) && currentResearch == null;
                if (alert && !sel)
                {
                    UI.Fill(new Rect(r.x + 11, r.y + 2, 6, 6), UI.Ink);
                    UI.Fill(new Rect(r.x + 12, r.y + 3, 4, 4), UI.Gold);
                }
            }
        }

        bool AnyFreeSlot()
        {
            foreach (var b in buildings) if (b.Built && b.workers.Count < b.Slots && b.def.kind != BKind.Storage) return true;
            return false;
        }

        // ---------------- objective / log / toasts ----------------
        void DrawObjective()
        {
            float left = FreeLeft, right = FreeRight;
            float avail = right - left;
            if (avail < 150) return;
            var o = CurrentObjective;
            string tier = "~ " + TierName().ToUpper() + " ~";
            string goal = o == null ? "^gAll goals complete. Keep the flock going." : "Goal: " + o.text;
            string prog = o != null && o.progress != null ? o.progress(this) : null;
            string reward = o != null && o.reward > 0 ? "+" + o.reward : null;
            float rw = reward != null ? PixelFont.Width(reward) + 13 : 0;
            float ratio = -1;
            if (prog != null)
            {
                int slash = prog.IndexOf('/');
                float a, b;
                if (slash > 0 && float.TryParse(prog.Substring(0, slash), out a) && float.TryParse(prog.Substring(slash + 1), out b) && b > 0) ratio = Mathf.Clamp01(a / b);
                else goal += " ^k(" + prog + ")";
            }
            float want = Mathf.Max(PixelFont.Width(goal) + rw + 24, PixelFont.Width(tier) + 20);
            float w = Mathf.Round(Mathf.Clamp(want, 180, Mathf.Min(avail, 340)));
            float tw = w - 16 - (rw > 0 ? rw + 6 : 0);
            var lines = PixelFont.Wrap(goal, tw, 1);
            float h = 6 + 8 + 4 + lines.Count * 10 + (ratio >= 0 ? 10 : 0) + 3;
            float cx = Mathf.Clamp(W / 2, left + w / 2, right - w / 2);
            var r = new Rect(Mathf.Round(cx - w / 2), TopH + 6, w, h);
            UI.Block(r);
            UI.Box(r, UI.BgDark, UI.Edge);
            PixelFont.DrawAligned(new Rect(r.x, r.y + 5, r.width, 8), tier, UI.Gold, 1, 1);
            float y = r.y + 18;
            foreach (var l in lines) { PixelFont.Draw(r.x + 8, y, l, UI.Text); y += 10; }
            if (reward != null)
            {
                float rx = r.xMax - 8 - rw;
                PixelFont.Draw(rx, r.y + 18, reward, UI.Gold);
                UI.Icon(rx + rw - 9, r.y + 17, "coin");
            }
            if (ratio >= 0)
            {
                float pw = PixelFont.Width(prog);
                UI.Bar(new Rect(r.x + 8, y + 1, r.width - 16 - pw - 6, 6), ratio, UI.Gold);
                PixelFont.Draw(r.xMax - 8 - pw, y, prog, UI.Dim);
            }
        }

        float logTop;

        void DrawLog()
        {
            float x = panel != Panel.None ? PanelX + PanelW + 8 : NavX;
            float maxW = Mathf.Min(440, FreeRight - x);
            float bottom = H - 8 - (ToolActive ? 30 : 0);
            logTop = bottom;
            if (maxW < 150 || log.Count == 0) return;
            for (int i = log.Count - 1; i >= 0; i--)
            {
                float age = Time.unscaledTime - log[i].Value;
                float a = Mathf.Clamp(1.4f - age / 25f, 0.45f, 1f);
                var lines = PixelFont.Wrap(log[i].Key, maxW - 8, 1);
                float lw = 0;
                foreach (var l in lines) lw = Mathf.Max(lw, PixelFont.Width(l));
                float h = lines.Count * 10 + 2;
                float y = bottom - h;
                if (y < H * 0.45f) break;
                UI.Fill(new Rect(x, y, lw + 8, h), new Color(0.07f, 0.055f, 0.047f, 0.72f * a));
                Color tc = age > 12f ? UI.Dim : UI.Text;
                tc.a = a;
                for (int k = 0; k < lines.Count; k++) PixelFont.Draw(x + 4, y + 1 + k * 10, lines[k], tc);
                bottom = y - 2;
                logTop = y;
            }
        }

        void DrawToast()
        {
            if (toastT <= 0 || string.IsNullOrEmpty(toast)) return;
            UI.alpha = Mathf.Clamp01(toastT * 3);
            Color ring = UI.Edge; string icon = null;
            if (toast.StartsWith("^r")) { ring = UI.Bad; icon = "exclaim_red"; }
            else if (toast.StartsWith("^g")) { ring = UI.Good; icon = "check"; }
            else if (toast.StartsWith("^y")) { ring = UI.Gold; icon = "coin"; }
            float tw = PixelFont.Width(toast);
            float w = tw + 16 + (icon != null ? 14 : 0);
            float cx = (FreeLeft + FreeRight) / 2;
            var r = new Rect(Mathf.Round(cx - w / 2), Mathf.Round(Mathf.Min(H - 122, logTop - 26)), w, 20);
            UI.Box(r, UI.Bg, ring);
            float tx = r.x + 8;
            if (icon != null) { var t = Icons.Tex(icon); UI.Icon(tx, Mathf.Round(r.y + 10 - t.height / 2f), icon); tx += 14; }
            var tc = UI.Text; tc.a = UI.alpha;
            PixelFont.Draw(tx, r.y + 6, toast, tc);
            UI.alpha = 1;
        }

        void DrawBanners()
        {
            if (deathBannerT > 0)
            {
                float a = Mathf.Clamp01(deathBannerT);
                UI.alpha = a;
                float sc = W >= 640 ? 3 : 2;
                float tw = PixelFont.Width(deathBanner, sc);
                var cause = PixelFont.Wrap(deathCauseText ?? "", W - 80, 1);
                float cw = 0; foreach (var l in cause) cw = Mathf.Max(cw, PixelFont.Width(l));
                float bh = 8 + 7 * sc + 8 + cause.Count * 10 + 8;
                var band = new Rect(0, Mathf.Round(H * 0.16f), W, bh);
                UI.Fill(new Rect(0, band.y, W, 1), UI.Ink);
                UI.Fill(new Rect(0, band.yMax - 1, W, 1), UI.Ink);
                UI.Fill(new Rect(0, band.y + 1, W, bh - 2), UI.Banner);
                UI.Fill(new Rect(0, band.y + 1, W, 2), UI.BannerHi);
                UI.Fill(new Rect(0, band.yMax - 4, W, 3), UI.BannerLo);
                float wob = Mathf.Sin(Time.unscaledTime * 30) * (deathBannerT > 4 ? 2 : 0);
                var tc = UI.Text; tc.a = a;
                PixelFont.Draw(Mathf.Round(W / 2 - tw / 2 + wob), band.y + 8, deathBanner, tc, sc);
                float cy = band.y + 8 + 7 * sc + 8;
                foreach (var l in cause) { PixelFont.Draw(Mathf.Round(W / 2 - PixelFont.Width(l) / 2), cy, l, tc); cy += 10; }
                float half = Mathf.Max(tw, cw) / 2 + 16;
                float ty = Mathf.Round(band.y + bh / 2 - 13);
                if (W / 2 - half - 27 > 0)
                {
                    UI.Icon(Mathf.Round(W / 2 - half - 27), ty, "tomb", 3);
                    UI.Icon(Mathf.Round(W / 2 + half), ty, "tomb", 3);
                }
                UI.alpha = 1;
            }
            if (tierShownT > 0)
            {
                float a = Mathf.Clamp01(tierShownT);
                UI.alpha = a;
                string t = TierName().ToUpper();
                string cap = "NEW SETTLEMENT TIER";
                float tw = Mathf.Max(PixelFont.Width(t, 2), PixelFont.Width(cap));
                float w = tw + 64;
                float y = Mathf.Round(H * 0.2f) + (deathBannerT > 0 ? 70 : 0);
                var r = new Rect(Mathf.Round(W / 2 - w / 2), y, w, 38);
                UI.Box(r, UI.Bg, UI.Gold);
                UI.Icon(r.x + 10, r.y + 10, "star", 2);
                UI.Icon(r.xMax - 28, r.y + 10, "star", 2);
                var dc = UI.Dim; dc.a = a;
                var gc = UI.Gold; gc.a = a;
                PixelFont.DrawAligned(new Rect(r.x, r.y + 5, r.width, 8), cap, dc, 1, 1);
                PixelFont.Draw(Mathf.Round(W / 2 - PixelFont.Width(t, 2) / 2), r.y + 17, t, gc, 2);
                UI.alpha = 1;
            }
        }

        bool titleChecked, titleHasSave, titleSaveOld; string titleSaveInfo = "";
        void CheckTitleSave()
        {
            if (titleChecked) return;
            titleChecked = true;
            titleHasSave = HasSave;
            if (titleHasSave) { titleSaveInfo = SaveSummary(); titleSaveOld = SaveVersion() < 4; }
        }

        void DrawAutosaveNote()
        {
            if (autosaveNoteT <= 0) return;
            UI.alpha = Mathf.Clamp01(autosaveNoteT);
            string t = "AUTOSAVED";
            float w = PixelFont.Width(t) + 10;
            var r = new Rect(Mathf.Round(FreeRight - w), H - 22, w, 14);
            UI.Box(r, UI.BgDark);
            var c = UI.Dim; c.a = UI.alpha;
            PixelFont.Draw(r.x + 5, r.y + 4, t, c, 1, false);
            UI.alpha = 1;
        }

        void DrawPlacementHint()
        {
            if (!ToolActive) return;
            string title, cost; string[] keys, acts;
            bool afford = true;
            if (beltTool && clearTool) { title = "CLEAR TOOL"; cost = "50% refund"; keys = new[] { "DRAG", "ESC" }; acts = new[] { "remove", "stop" }; }
            else if (beltTool) { title = BeltTierNames[beltTierSel].ToUpper(); cost = BeltTierCost[beltTierSel] + "c/tile"; keys = new[] { "DRAG", "R", "ESC" }; acts = new[] { "lay", "rotate", "stop" }; }
            else if (pathTool) { title = "PATH TOOL"; cost = "2c/tile"; keys = new[] { "DRAG", "ESC" }; acts = new[] { "paint", "stop" }; }
            else
            {
                title = "PLACE " + placing.name.ToUpper(); cost = placing.cost[0] + "c"; afford = res[0] >= placing.cost[0];
                keys = new[] { "CLICK", "SHIFT", "ESC" }; acts = new[] { "place", "several", "cancel" };
            }
            float w = 10 + PixelFont.Width(title) + 6 + PixelFont.Width(cost) + 4;
            for (int i = 0; i < keys.Length; i++) w += 12 + UI.KeyW(keys[i]) + 4 + PixelFont.Width(acts[i]);
            w += 10;
            float cx = (FreeLeft + FreeRight) / 2;
            var r = new Rect(Mathf.Round(cx - w / 2), H - 30, w, 22);
            UI.Block(r);
            UI.Box(r, UI.BgDark, UI.Edge);
            float x = r.x + 10, ty = r.y + 8;
            x += PixelFont.Draw(x, ty, title, UI.Gold) + 6;
            x += PixelFont.Draw(x, ty, cost, afford ? UI.Text : UI.Bad) + 4;
            for (int i = 0; i < keys.Length; i++)
            {
                x += 12;
                UI.Key(x, r.y + 5, keys[i]);
                x += UI.KeyW(keys[i]) + 4;
                x += PixelFont.Draw(x, ty, acts[i], UI.Dim);
            }
        }

        // ---------------- title / help ----------------
        void DrawTitle()
        {
            UI.Fill(new Rect(0, 0, W, H), new Color(0.11f, 0.09f, 0.19f, 0.62f));
            float t = Time.unscaledTime;
            var logo = LogoArt.Tex();
            float ls = Mathf.Clamp(Mathf.Floor(Mathf.Min(W * 0.72f / logo.width, H * 0.27f / logo.height)), 1, 4);
            float lw = logo.width * ls, lh = logo.height * ls;
            float ly = Mathf.Round(H * 0.07f);
            if (UI.Repaint) GUI.DrawTexture(new Rect(Mathf.Round(W / 2 - lw / 2), ly, lw, lh), logo);
            float y = ly + lh + 4;
            PixelFont.DrawAligned(new Rect(0, y, W, 8), "A SHEEP MANAGEMENT TRAGEDY", UI.Gold, 1, 1);
            y += 14;
            PixelFont.DrawAligned(new Rect(0, y, W, 8), "It looks adorable. Then you notice what is happening to those sheep.", UI.Text, 1, 1);
            y += 20;

            bool save = titleHasSave && !titleSaveOld;
            bool oldSave = titleHasSave && titleSaveOld;
            int n = save || oldSave ? 5 : 4;
            float bw = 190, bh = 20, gap = 4;
            float ph = 20 + n * bh + (n - 1) * gap + (save || oldSave ? 12 : 0);
            var pr = new Rect(Mathf.Round(W / 2 - (bw + 20) / 2), Mathf.Round(Mathf.Min(y, H - 28 - ph)), bw + 20, ph);
            // mascots: a hopeful sheep and a less hopeful one
            float ps = Mathf.Max(2, Mathf.Floor(H / 150f));
            float sw = SheepArt.W * ps;
            if (pr.x - sw - 24 > 0)
            {
                float lp = (pr.x - 128 > 4 ? 96 : 48) + 6;
                var lr = new Rect(Mathf.Round(pr.x - lp - 16), Mathf.Round(pr.y + 4), lp, lp);
                if (lr.x > 4)
                {
                    UI.Well(lr, UI.BannerLo);
                    if (UI.Repaint) GUI.DrawTexture(new Rect(lr.x + 3, lr.y + 3, lr.width - 6, lr.height - 6), LeaderArt.Portrait(((int)(t * 0.5f)) % 4 == 3 ? LExpr.Shout : LExpr.Smug, false));
                    string q = titleHasSave && !titleSaveOld ? "\"Back to work, Hoof.\"" : "\"Click BEGIN, Hoof.\"";
                    float qw = PixelFont.Width(q) + 10;
                    var qr = new Rect(Mathf.Round(lr.center.x - qw / 2), lr.yMax + 4, qw, 14);
                    UI.Box(qr, UI.Bg, UI.Gold);
                    PixelFont.Draw(qr.x + 5, qr.y + 4, q, UI.Gold, 1, false);
                }
                var ghostLook = new SheepLook { woolStage = 2, expr = Expr.Sad, ghost = true };
                UI.alpha = 0.6f;
                UI.DrawSprite(new Rect(pr.xMax + 16, pr.y + 8 + Mathf.Round(Mathf.Sin(t * 1.6f) * 2) * ps, sw, sw), SheepArt.Get(ghostLook), true);
                UI.alpha = 1;
            }
            UI.Panel(pr, null);
            float bx = pr.x + 10, by = pr.y + 10;
            if (save)
            {
                if (UI.ButtonEx(new Rect(bx, by, bw, bh), "CONTINUE", null, "ENTER", true, titleSaveInfo, BtnStyle.Primary)) { LoadGame(); return; }
                by += bh + 2;
                PixelFont.DrawAligned(new Rect(bx, by, bw, 9), "^k" + Trunc(titleSaveInfo, 32), UI.Dim, 1, 1);
                by += 12;
                if (UI.Button(new Rect(bx, by, bw, bh), "NEW GAME", true, "Start over. Your autosave will be overwritten tomorrow.")) { StartGame(); return; }
            }
            else if (UI.ButtonEx(new Rect(bx, by, bw, bh), "BEGIN", null, "ENTER", true, oldSave ? "BEGIN\nA new flock, with the Supreme Leader's tutorial." : null, BtnStyle.Primary)) { StartGame(); return; }
            if (oldSave)
            {
                by += bh + 2;
                PixelFont.DrawAligned(new Rect(bx, by, bw, 9), "^kSave from an older version", UI.Dim, 1, 1);
                by += 12;
                if (UI.Button(new Rect(bx, by, bw, bh), "CONTINUE OLD SAVE", true, "CONTINUE OLD SAVE\n" + titleSaveInfo + "\nNo tutorial: the Supreme Leader assumes you know what you are doing.")) { LoadGame(); return; }
            }
            by += bh + gap;
            if (UI.ButtonEx(new Rect(bx, by, bw, bh), "HOW TO PLAY", null, "H")) showHelp = true;
            by += bh + gap;
            if (UI.ButtonEx(new Rect(bx, by, bw, bh), Sfx.muted ? "SOUND: OFF" : "SOUND: ON", Sfx.muted ? "mute" : "speaker")) Sfx.muted = !Sfx.muted;
            by += bh + gap;
            if (UI.Button(new Rect(bx, by, bw, bh), "QUIT")) Application.Quit();

            var fr = new Rect(0, H - 20, W, 20);
            UI.Fill(fr, new Color(0.07f, 0.055f, 0.047f, 0.85f));
            UI.Fill(new Rect(0, fr.y, W, 1), UI.Hi);
            PixelFont.Draw(8, fr.y + 7, "WASD/drag: move   Scroll: zoom   Click: select   Space: pause   H: help", UI.Dim);
            string fb = "FESTIVAL BUILD";
            PixelFont.Draw(W - PixelFont.Width(fb) - 8, fr.y + 7, fb, UI.Dim);
        }

        void DrawHelp()
        {
            UI.Fill(new Rect(0, 0, W, H), new Color(0.07f, 0.055f, 0.047f, 0.6f));
            float w = Mathf.Min(460, W - 20), h = Mathf.Min(380, H - 20);
            var r = new Rect(Mathf.Round(W / 2 - w / 2), Mathf.Round(H / 2 - h / 2), w, h);
            UI.Panel(r, "HOW TO RUN A SHEEP EMPIRE", true, "flock");
            if (UI.CloseButton(r)) { showHelp = false; return; }
            string txt =
                "^yTHE SUPREME LEADER:^w The biggest ram in the field, in a military cap. He gives the orders: follow his DECREES at the top of the screen. During the tutorial a gold arrow shows what to click.\n\n" +
                "^ySHEEP NEVER WORK, THEY ARE MANAGED:^w they live in Breeding Pens, eat in Feeding Lots and ride conveyors through machines: shorn for WOOL, milked for MILK, bred for LAMBS, or turned into MUTTON. Every building runs by itself.\n" +
                "^yROOMS AND DOORS:^w Sheep live in ROOMS: Breeding Pens and Feeding Lots. Every room has a red DOOR in the middle of each side. Lay a CONVEYOR that starts right outside a door, then OPEN the door: the sheep walk out onto the belt and ride wherever it leads. A belt that points into a room drops them inside. CLOSE a door and they stay.\n" +
                "^yBREEDING PEN:^w Home. Fed, happy rams and ewes make LAMBS while there is room. The flock can never be bigger than your pens. Upgrades hold more sheep AND breed faster.\n" +
                "^yFEEDING LOT:^w Sheep that ride in eat from the food store until they are FULL. Only fed sheep leave through its doors. Upgrade it to feed more, faster.\n" +
                "^yMACHINES:^w Shearing Machines, Milking Machines and the Wool Plant sit ON a conveyor: sheep ride in and come out the other side shaved or milked. The SLAUGHTERHOUSE is the door nobody comes back from: sheep in, MUTTON out.\n" +
                "^yMACHINE DOORS:^w Every side of a machine that a belt touches has a door, with an IN or OUT tag (green = open, red = shut). Click a tag to flip it. A shut IN door makes the sheep wait on the belt. Sheep leave through open OUT doors (two open OUT doors split the line; Smart Door filters work there too). Wool and milk only come out through SHUT doors (their tag says WOOL or MILK). No open OUT door: the sheep waits INSIDE until you open one.\n" +
                "^yMARKET:^w Its doors take whatever rides in: goods sell (+10%), and live sheep are sold too. Shut a door to stop selling.\n" +
                "^yKEEP THEM CONTAINED:^w Sheep never wander off the line. A belt that ends nowhere, a building that takes no sheep or a machine with no way out makes them wait right there. Fix the route.\n" +
                "^yGENETICS:^w Research Sheep Genetics and put a GENE LAB on a conveyor. In SAMPLE mode every sheep that rides through gives DNA (so does the Slaughterhouse). Spend DNA in GENETICS (N) on breeding upgrades (faster lambs, twins...) and 13 STRAINS: Silk and Golden Fleece wool, Cream and Chocolate milk, Cotton Candy, Jumbo mutton, Turbo sheep, tiny Teacups, Glow wool, Wolfsheep guards, Sparky power, Splitters and Phoenixes. Six are secret HYBRIDS: put two different strains in one pen, or gamble with the Gene Lab's CHAOS mode. Set the Gene Lab to SPLICE and sheep come out altered; their lambs often keep it. Level 4 and 5 upgrades need these rare goods.\n" +
                "^yTHE BALANCE:^w Every sheep you slaughter or sell is wool and milk you never get again. Breed faster (pen upgrades, Matchmaking) or slaughter slower. The FACTORY panel (O) shows yesterday: lambs vs. losses, food grown vs. eaten, what each machine did.\n" +
                "^yMONEY:^w Goods leave machines on any belt pointing AWAY from their side. Run those belts into MARKETS (build as many as you like) and the goods sell themselves (+10%).\n" +
                "^yGROWTH:^w More sheep need more feeding space, bigger pens, faster belts and more machines. Upgrade rooms (5 levels) and belts (Steel and Mega belts, or UPGRADE ALL BELTS in the Build menu). Research SMART DOORS to give every door a filter (hungry, fed, fluffy, rams...) and the factory runs itself.\n" +
                "^yFOOD:^w Every sheep eats about 4 food a day. Farms grow it by themselves; Feeding Lots feed it to the sheep. Hungry sheep do not breed, grow wool slowly, and eventually starve.\n" +
                "^yDANGER:^w Better machines are faster and hurt more sheep. Removing safety guards is very profitable. Sheep will remember.\n\n" +
                "^kControls: WASD/arrows or right-drag to move, scroll to zoom, left-click to select, Esc to cancel. Space pause, 1-3 speed. G opens/closes the selected room's main door. B Build, C Conveyor (R rotates), F Flock, O Factory, N Genetics, R Research, T Market, M Map, L name plates. F5 save, F9 load. The game autosaves every day.";
            var area = new Rect(r.x + 10, r.y + UI.HeaderH + 2, r.width - 20, r.height - UI.HeaderH - 36);
            UI.BeginScroll(area, ref scrollHelp, PixelFont.WrappedHeight(txt, area.width - 8) + 4);
            UI.Wrapped(new Rect(0, 0, area.width - 8, 9999), txt);
            UI.EndScroll();
            if (UI.ButtonEx(new Rect(r.xMax - 92, r.yMax - 30, 82, 20), "GOT IT", null, null, true, null, BtnStyle.Primary)) showHelp = false;
        }

        // ---------------- event card ----------------
        void DrawEvent(EventCard c)
        {
            var e = Event.current;
            if (e.type == EventType.KeyDown)
            {
                int k = -1;
                if (e.keyCode >= KeyCode.Alpha1 && e.keyCode <= KeyCode.Alpha9) k = e.keyCode - KeyCode.Alpha1;
                else if (e.keyCode >= KeyCode.Keypad1 && e.keyCode <= KeyCode.Keypad9) k = e.keyCode - KeyCode.Keypad1;
                if (k >= 0 && k < c.options.Count && (c.options[k].enabled == null || c.options[k].enabled()))
                {
                    e.Use();
                    Sfx.Play("click", 0.5f);
                    ChooseOption(c.options[k]);
                    return;
                }
            }
            UI.Fill(new Rect(0, 0, W, H), new Color(0.07f, 0.055f, 0.047f, 0.72f));
            float w = Mathf.Min(320, W - 20);
            const float pw = 64;
            float tx = 8 + pw + 10, tw = w - tx - 10;
            float optW = w - 16;
            float textH = PixelFont.WrappedHeight(c.text, tw);
            float rowH = Mathf.Max(pw, 14 + textH);
            float optH = 0;
            foreach (var o in c.options) optH += 22 + (string.IsNullOrEmpty(o.hint) ? 0 : PixelFont.WrappedHeight("^k" + o.hint, optW - 12)) + 5;
            float h = 6 + 26 + 8 + rowH + 10 + optH + 12 + 8;
            var r = new Rect(Mathf.Round(W / 2 - w / 2), Mathf.Round(Mathf.Max(8, H / 2 - h / 2)), w, h);
            UI.Panel(r, null);
            var tr = new Rect(r.x + 6, r.y + 6, r.width - 12, 26);
            UI.PxFill(tr, UI.Ink);
            UI.Fill(UI.Inset(tr, 1), UI.Banner);
            UI.Fill(new Rect(tr.x + 1, tr.y + 1, tr.width - 2, 1), UI.BannerHi);
            UI.Fill(new Rect(tr.x + 1, tr.yMax - 3, tr.width - 2, 2), UI.BannerLo);
            float sc = PixelFont.Width(c.title, 2) <= tr.width - 12 ? 2 : 1;
            PixelFont.DrawAligned(new Rect(tr.x, tr.y - 1, tr.width, tr.height), c.title, UI.Text, sc, 1);
            float y = r.y + 40;
            var pr = new Rect(r.x + 8, y, pw, pw);
            UI.Well(pr, UI.GrassLo);
            if (c.subject != null && c.subject.go != null && c.subject.sr != null) UI.DrawSprite(new Rect(pr.x + 2, pr.y + 2, 60, 60), c.subject.sr.sprite);
            else UI.IconFit(UI.Inset(pr, 4), c.icon);
            PixelFont.Draw(r.x + tx, y + 2, c.result ? "OUTCOME" : "EVENT - " + DateString().ToUpper(), UI.Gold);
            UI.Wrapped(new Rect(r.x + tx, y + 14, tw, textH), c.text);
            y += rowH + 10;
            for (int i = 0; i < c.options.Count; i++)
            {
                var o = c.options[i];
                bool en = o.enabled == null || o.enabled();
                if (UI.ButtonEx(new Rect(r.x + 8, y, optW, 20), o.label, null, (i + 1).ToString(), en, null, BtnStyle.Normal, true, true)) { ChooseOption(o); return; }
                y += 22;
                if (!string.IsNullOrEmpty(o.hint))
                {
                    float hh = PixelFont.WrappedHeight("^k" + o.hint, optW - 12);
                    PixelFont.DrawWrapped(new Rect(r.x + 20, y, optW - 12, hh), "^k" + o.hint, UI.Dim);
                    y += hh;
                }
                y += 5;
            }
            string ft = "TIME IS PAUSED WHILE YOU DECIDE";
            float fw = PixelFont.Width(ft) + 11;
            UI.Icon(Mathf.Round(r.center.x - fw / 2), y + 2, "pause_dim");
            PixelFont.Draw(Mathf.Round(r.center.x - fw / 2 + 11), y + 2, ft, UI.Dim);
        }
    }
}
