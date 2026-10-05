using System.Collections.Generic;
using UnityEngine;

namespace Fleeced
{
    public class EventCard
    {
        public string id, title, text, icon = "warning";
        public Sheep subject;
        public List<EventOption> options = new List<EventOption>();
        public bool result;
    }

    public class EventOption
    {
        public string label, hint; public System.Func<string> act; public System.Func<bool> enabled;
        public EventOption(string l, string h, System.Func<string> a, System.Func<bool> e = null) { label = l; hint = h; act = a; enabled = e; }
    }

    public partial class Game
    {
        public EventCard currentEvent;
        readonly List<EventCard> eventQueue = new List<EventCard>();
        readonly List<SchedSave> scheduled = new List<SchedSave>();
        readonly Dictionary<string, int> eventCooldown = new Dictionary<string, int>();
        int lastEventDay = -10;
        public float launchSeq;
        GameObject rocketGo; Building rocketPad; Sheep astronaut;
        readonly List<Wolf> wolves = new List<Wolf>();

        public void QueueEvent(EventCard c)
        {
            if (c == null) return;
            eventQueue.Add(c);
        }

        void Schedule(float daysFromNow, string id, int sheepId = 0)
        {
            scheduled.Add(new SchedSave { day = Day + daysFromNow, id = id, sheepId = sheepId });
        }

        EventCard ResolveScheduled(SchedSave sc)
        {
            switch (sc.id)
            {
                case "revenge": return Ev_Revenge();
                case "gerald": return Ev_GeraldReveal();
                case "scandal": return Ev_FakeScandal();
                case "strike": return Ev_Strike();
                case "cultdemand": return Ev_CultDemand();
                case "inspection": return Ev_Inspection();
                case "returns":
                    foreach (var s in sheep) if (s.id == sc.sheepId && !s.dead) return SheepReturns(s);
                    return null;
            }
            return null;
        }

        void SimEvents(float dt)
        {
            SimResearch(dt);
            for (int i = scheduled.Count - 1; i >= 0; i--)
            {
                if (Day >= scheduled[i].day)
                {
                    var sc = scheduled[i];
                    scheduled.RemoveAt(i);
                    QueueEvent(ResolveScheduled(sc));
                }
            }
            if (currentEvent == null && eventQueue.Count > 0)
            {
                currentEvent = eventQueue[0];
                eventQueue.RemoveAt(0);
                if (!currentEvent.result) Sfx.Play("event", 0.7f);
            }
        }

        public void ChooseOption(EventOption o)
        {
            var card = currentEvent;
            currentEvent = null;
            Sfx.Play("click");
            string r = o.act != null ? o.act() : null;
            if (!string.IsNullOrEmpty(r))
            {
                var res_ = new EventCard { title = card.title, text = r, icon = card.icon, subject = card.subject != null && card.subject.go != null ? card.subject : null, result = true };
                res_.options.Add(new EventOption("OK", "", null));
                eventQueue.Insert(0, res_);
            }
        }

        // ---------------- random events ----------------
        Sheep RandomSheep(System.Func<Sheep, bool> filter = null)
        {
            var list = new List<Sheep>();
            foreach (var s in sheep) if (!s.dead && s.act != Act.Hidden && !s.Busy && (filter == null || filter(s))) list.Add(s);
            return list.Count == 0 ? null : list[rng.Next(list.Count)];
        }

        static string N(Sheep s) { return "^y" + s.name + "^w"; }

        void RollDailyEvent()
        {
            if (!tutDone || dayIndex < 2 || dayIndex - lastEventDay < 2) return;
            if (rng.NextDouble() > 0.55) return;
            var pool = new List<KeyValuePair<string, float>>();
            System.Action<string, float> add = (id, w) =>
            {
                int last;
                if (w <= 0) return;
                if (eventCooldown.TryGetValue(id, out last) && dayIndex - last < 9) return;
                pool.Add(new KeyValuePair<string, float>(id, w));
            };
            int pop = Population;
            add("merchant", 1f);
            add("wolfveg", flags.Contains("gerald_asked") ? 0 : (dayIndex >= 4 ? 1.2f : 0));
            add("luxury", HasBuilding(BKind.Market) && dayIndex >= 6 && !flags.Contains("luxury_decided") ? 1f : 0);
            add("strike", AvgMood() < 35 && pop >= 5 ? 3f : 0);
            add("crash", HasBuilding(BKind.Market) && totalSold[(int)Res.Wool] > 30 ? 0.8f : 0);
            add("union", pop >= 10 && !flags.Contains("union_decided") ? 1f : 0);
            add("disease", pop >= 6 ? 0.7f : 0);
            add("explosion", ExplodableBuilding() != null ? 0.9f : 0);
            add("cult", pop >= 8 && !flags.Contains("cult_decided") ? 0.9f : 0);
            add("missing", pop >= 4 ? 0.7f : 0);
            add("suspicious", dayIndex >= 4 && !flags.Contains("shears_decided") ? 1f : 0);
            add("immigrant", 0.7f);
            add("truck", dayIndex >= 5 ? 0.7f : 0);
            add("tax", res[0] >= 300 ? 0.7f : 0);
            add("talent", pop >= 6 ? 0.6f : 0);
            add("heat", Season == 1 ? 1.2f : 0);
            add("influencer", pop >= 8 ? 0.6f : 0);
            add("moon", !flags.Contains("moon_vision") && (pop >= 15 || dayIndex >= 40) ? 0.8f : 0);
            add("golden", GoldenSheep() != null ? 0.6f : 0);
            add("machine", HasBuilding(BKind.Plant) && !flags.Contains("machine_named") ? 1f : 0);
            if (pool.Count == 0) return;
            float total = 0; foreach (var p in pool) total += p.Value;
            float roll = (float)rng.NextDouble() * total;
            string pick = pool[0].Key;
            foreach (var p in pool) { roll -= p.Value; if (roll <= 0) { pick = p.Key; break; } }
            var card = BuildEvent(pick);
            if (card != null)
            {
                eventCooldown[pick] = dayIndex;
                lastEventDay = dayIndex;
                QueueEvent(card);
            }
        }

        EventCard BuildEvent(string id)
        {
            switch (id)
            {
                case "merchant": return Ev_Merchant();
                case "wolfveg": return Ev_VegWolf();
                case "luxury": return Ev_Luxury();
                case "strike": return Ev_Strike();
                case "crash": return Ev_Crash();
                case "union": return Ev_Union();
                case "disease": return Ev_Disease();
                case "explosion": return Ev_Explosion();
                case "cult": return Ev_Cult();
                case "missing": return Ev_Missing();
                case "suspicious": return Ev_Suspicious();
                case "immigrant": return Ev_Immigrant();
                case "truck": return Ev_Truck();
                case "tax": return Ev_Tax();
                case "talent": return Ev_Talent();
                case "heat": return Ev_Heat();
                case "influencer": return Ev_Influencer();
                case "moon": return Ev_Moon();
                case "golden": return Ev_Golden();
                case "machine": return Ev_Machine();
            }
            return null;
        }

        EventCard Card(string title, string text, string icon, Sheep subject = null)
        {
            return new EventCard { title = title, text = text, icon = icon, subject = subject };
        }

        // ---- individual events ----
        public EventCard Ev_WorkshopAccident(Sheep s, Building b)
        {
            int cost = 60 + 80 * b.level;
            var c = Card("WORKSHOP ACCIDENT", N(s) + " got caught in the " + b.def.name + ".\n" + Cap(s.He) + " survived.\nTechnically.", "warning", s);
            c.options.Add(new EventOption("Fix the machine", "-" + cost + " coins. Fewer accidents here for 5 days.", () =>
            {
                res[0] -= cost; b.safetyUntil = Day + 5;
                foreach (var w in b.workers) AddMod(w, "Management cares (briefly)", 6, 3);
                return "The machine is fixed. It hums menacingly, but politely.";
            }, () => res[0] >= cost));
            c.options.Add(new EventOption("Blame " + s.name, "+10% productivity here. -15 happiness for everyone.", () =>
            {
                b.buffMult = 1.1f; b.buffUntil = Day + 3;
                AddGlobalMod("Blame culture", -15, 2);
                AddMod(s, "Publicly blamed", -10, 3);
                return s.name + " has been written up. Productivity rises, out of fear.";
            }));
            c.options.Add(new EventOption("Give " + s.name + " a lollipop", "-10 coins. " + s.name + " feels much better.", () =>
            {
                res[0] -= 10; AddMod(s, "Got a lollipop", 20, 3);
                return s.name + " holds the lollipop with both hooves. " + Cap(s.He) + " will never forget this.";
            }, () => res[0] >= 10));
            return c;
        }

        EventCard Ev_Merchant()
        {
            var c = Card("TRAVELING MERCHANT", "A traveling merchant offers 'premium grass seeds'.\nThey are glowing slightly.", "coin");
            c.options.Add(new EventOption("Buy the seeds", "-80 coins. Probably faster grass.", () =>
            {
                res[0] -= 80;
                if (rng.NextDouble() < 0.7) { flags.Add("premium_seeds"); return "The grass grows back 40% faster. It also hums at night."; }
                return "The seeds were painted gravel. The merchant is already gone. So is your wallet's dignity.";
            }, () => res[0] >= 80));
            c.options.Add(new EventOption("Decline", "Nothing happens.", () => "The merchant shrugs and leaves. Something in his cart goes 'baa'."));
            c.options.Add(new EventOption("Rob him", "+60 coins. Karma has been notified.", () =>
            {
                res[0] += 60; flags.Add("robbed_merchant");
                Schedule(4 + (float)rng.NextDouble() * 4, "revenge");
                return "You stole 60 coins and a hat. The sheep look at you differently now.";
            }));
            return c;
        }

        EventCard Ev_Revenge()
        {
            var c = Card("THE MERCHANT RETURNS", "The merchant you robbed is back.\nHe brought his cousins.\nThey all have sticks.", "skull");
            c.options.Add(new EventOption("Pay damages", "-150 coins.", () => { res[0] -= Mathf.Min(150, (float)res[0]); return "The cousins count the coins slowly and leave. One of them pats a sheep. Menacingly."; }));
            c.options.Add(new EventOption("Fight!", "Your guards (" + guardPower.ToString("0.0") + " power) vs. the cousins.", () =>
            {
                if (rng.NextDouble() < 0.3f + guardPower * 0.2f) { res[0] += 100; totalMade[0] += 100; return "Your guards headbutt everyone. The cousins flee, dropping 100 coins and a family photo."; }
                var v = RandomSheep(x => !x.Lamb);
                if (v != null) { v.injured = 3; v.bloody = 2; AddDecal("blood", v.pos); }
                res[0] -= Mathf.Min(100, (float)res[0]);
                return "It did not go well. " + (v != null ? v.name + " got bonked with a stick. " : "") + "They took 100 coins.";
            }));
            var s = RandomSheep(x => !x.Lamb);
            if (s != null)
                c.options.Add(new EventOption("Offer " + s.name + " as an apology", "Lose " + s.name + ".", () =>
                {
                    Vanish(s, "Left with the merchant. Sends postcards.");
                    return s.name + " leaves with the merchant to become a traveling salesman. " + Cap(s.He) + " sends postcards. They just say 'baa'.";
                }));
            return c;
        }

        EventCard Ev_VegWolf()
        {
            flags.Add("gerald_asked");
            var c = Card("WOLF AT THE GATE", "A wolf claims he has become vegetarian.\nHe is holding a carrot. Badly.\n\nAllow him into the village?", "wolf");
            c.options.Add(new EventOption("YES", "Something unpredictable will happen.", () =>
            {
                flags.Add("gerald");
                SpawnGerald();
                Schedule(4 + (float)rng.NextDouble() * 4, "gerald");
                return "Gerald moves in. He sleeps with one eye open. So does everyone else.";
            }));
            c.options.Add(new EventOption("NO", "He leaves.", () => "The wolf leaves, muttering about prejudice."));
            c.options.Add(new EventOption("PROBABLY NOT", "Honesty is the best policy?", () =>
            {
                Gain(Res.Wool, 12);
                return "The wolf respects your honesty and leaves a gift: 12 'wool'. It is wolf hair. Nobody will know.";
            }));
            return c;
        }

        EventCard Ev_GeraldReveal()
        {
            if (!flags.Contains("gerald")) return null;
            if (rng.NextDouble() < 0.5)
            {
                var c = Card("ABOUT GERALD", "Gerald has been eating the carrots.\nONLY the carrots.\nHe is genuinely vegetarian.", "wolf");
                c.options.Add(new EventOption("Make Gerald head of security", "+2 guard power, forever.", () => { flags.Add("gerald_guard"); return "Gerald now guards the flock. Other wolves are deeply confused."; }));
                c.options.Add(new EventOption("Let him be", "+6 happiness for a while.", () => { AddGlobalMod("Gerald is nice actually", 6, 5); return "Gerald knits now. He is surprisingly good at it."; }));
                return c;
            }
            var v = RandomSheep(x => !x.Lamb);
            if (v == null) return null;
            v.act = Act.Hidden; RemoveFromJob(v);
            var d = Card("ABOUT GERALD", N(v) + " is missing.\nGerald looks... round.", "wolf", null);
            d.options.Add(new EventOption("Exile Gerald", "Gerald leaves. " + v.name + " does not come back.", () =>
            {
                RemoveGerald(); v.act = Act.Wander; Kill(v, "Eaten by Gerald. We trusted him.");
                return "Gerald is exiled. He leaves a note: 'sorry'. It is written in something red.";
            }));
            d.options.Add(new EventOption("Ask Gerald nicely", "50/50.", () =>
            {
                v.act = Act.Wander;
                if (rng.NextDouble() < 0.5 || HasTrait(v, "lucky"))
                {
                    v.pos = GeraldPos(); v.muddy = 2; v.tremble = 15; AddMod(v, "Was inside a wolf", -25, 5);
                    return "Gerald coughs up " + v.name + ". " + Cap(v.He) + " is fine. Damp, but fine.";
                }
                Kill(v, "Eaten by Gerald. He said sorry.");
                return "Gerald says he doesn't know what you mean. Then he burps.";
            }));
            d.options.Add(new EventOption("Promote Gerald to security", "+2 guard power. " + v.name + " is gone.", () =>
            {
                v.act = Act.Wander; Kill(v, "Eaten by the Head of Security.");
                flags.Add("gerald_guard"); AddGlobalMod("Horrified by management", -10, 4);
                return "Gerald is promoted. The flock is horrified. Wolf attacks are, somehow, no longer a problem.";
            }));
            return d;
        }

        static bool HasTrait(Sheep s, string t) { return s.traits.Contains(t); }

        EventCard Ev_Luxury()
        {
            flags.Add("luxury_decided");
            var s = RandomSheep(x => !x.Lamb);
            if (s == null) return null;
            var c = Card("LUXURY OPPORTUNITY", N(s) + " discovered that humans will pay ridiculous amounts for 'artisanal luxury wool'.", "luxury", s);
            c.options.Add(new EventOption("Launch a luxury wool brand", "+15% price on Clothing & Luxury forever. Sell 30 wool at 2.5x now. -6 happiness.", () =>
            {
                flags.Add("luxury_brand");
                int n = Mathf.Min(30, Mathf.FloorToInt((float)res[(int)Res.Wool]));
                int earned = Mathf.RoundToInt(n * SellPrice(Res.Wool) * 2.5f);
                res[(int)Res.Wool] -= n; res[0] += earned; totalMade[0] += earned;
                AddGlobalMod("Endless branding meetings", -6, 3);
                return "Introducing 'Baa-sics by " + s.name + "'. You sold " + n + " wool for " + earned + " coins. The sheep now have a logo.";
            }));
            c.options.Add(new EventOption("Keep production ethical", "+8 happiness for 5 days.", () => { AddGlobalMod("Ethical management", 8, 5); return "The flock is proud of you. This feeling will not last."; }));
            c.options.Add(new EventOption("Fake the wool quality", "+20% raw & clean wool prices... for now.", () =>
            {
                flags.Add("fake_wool");
                Schedule(5 + (float)rng.NextDouble() * 4, "scandal");
                return "You glue labels that say 'PREMIUM' on regular wool. Sales are up. Somewhere, a watchdog wakes up.";
            }));
            return c;
        }

        EventCard Ev_FakeScandal()
        {
            if (!flags.Contains("fake_wool")) return null;
            int fine = Mathf.Max(150, Mathf.RoundToInt((float)res[0] * 0.25f));
            var c = Card("CONSUMER WATCHDOG", "Your 'premium wool' was found to be 40% lint, 30% dog, and 30% regret.\nFine: " + fine + " coins.", "skull");
            c.options.Add(new EventOption("Pay the fine", "-" + fine + " coins.", () => { res[0] = Mathf.Max(0, (float)res[0] - fine); flags.Remove("fake_wool"); return "Fine paid. The PREMIUM labels are removed. The sheep keep one as a souvenir."; }));
            c.options.Add(new EventOption("Bribe the inspector", "-" + fine / 2 + " coins. Might work.", () =>
            {
                res[0] = Mathf.Max(0, (float)res[0] - fine / 2);
                if (rng.NextDouble() < 0.5) return "The inspector pockets the coins and says 'what fraud?'. The scam continues.";
                res[0] = Mathf.Max(0, (float)res[0] - fine); flags.Remove("fake_wool");
                priceMod[1] *= 0.7f; priceMod[2] *= 0.7f;
                return "It did not work. You paid the bribe AND the fine. Wool prices drop from shame.";
            }));
            var s = RandomSheep(x => !x.Lamb);
            if (s != null)
                c.options.Add(new EventOption("Blame " + s.name, s.name + " goes to sheep jail.", () =>
                {
                    flags.Remove("fake_wool");
                    Vanish(s, "Arrested for wool fraud. Innocent.");
                    AddGlobalMod("Scapegoat culture", -8, 3);
                    return s.name + " is led away in tiny handcuffs. " + Cap(s.He) + " did nothing wrong.";
                }));
            return c;
        }

        EventCard Ev_Strike()
        {
            var lead = RandomSheep(x => !x.Lamb);
            if (lead == null) return null;
            int cost = Population * 6;
            var c = Card("SHEEP STRIKE", "The flock has unionized against the conveyor.\n" + N(lead) + " is holding a tiny sign that says 'BAA'.", "sad", lead);
            c.options.Add(new EventOption("Pay them", "-" + cost + " coins. +18 happiness for 4 days.", () => { res[0] -= cost; AddGlobalMod("Strike settlement", 18, 4); return "The flock returns to the line, rich beyond its wildest dreams (6 coins each)."; }, () => res[0] >= cost));
            c.options.Add(new EventOption("Give them carrots", "-30 food. +10 happiness for 3 days.", () => { Use(Res.Food, 30); AddGlobalMod("Strike carrots", 10, 3); return "Carrots are distributed. The strike dissolves into crunching."; }, () => Has(Res.Food, 30)));
            c.options.Add(new EventOption("Ignore them", "Nobody leaves the pens for a day.", () =>
            {
                foreach (var s in sheep) if (!s.dead) s.refuse = 1f;
                AddGlobalMod("Ignored by management", -5, 3);
                return "Nobody works. " + lead.name + " gives a speech. It's just 'baa' for nine minutes. It is moving.";
            }));
            return c;
        }

        EventCard Ev_Crash()
        {
            priceMod[(int)Res.Wool] *= 0.45f; priceMod[(int)Res.CleanWool] *= 0.6f;
            var c = Card("WOOL MARKET CRASH", "Wool prices collapse after a viral video of a naked sheep.\nWool now sells for " + SellPrice(Res.Wool).ToString("0.0") + "c.", "chart");
            c.options.Add(new EventOption("Hold your stock", "Prices will recover. Probably.", () => "You hold. The market wobbles. Nobody knows anything."));
            c.options.Add(new EventOption("Panic sell all raw wool", "Sell everything at crash prices.", () => { int e = Sell(Res.Wool, 99999, false); return "You panic-sold for " + e + " coins. An analyst somewhere is laughing."; }));
            c.options.Add(new EventOption("Buy cheap wool", "-100 coins, +50 wool.", () => { res[0] -= 100; Gain(Res.Wool, 50); return "You bought the dip. The dip is mostly lint."; }, () => res[0] >= 100));
            return c;
        }

        EventCard Ev_Union()
        {
            flags.Add("union_decided");
            var rep = RandomSheep(x => !x.Lamb);
            if (rep == null) return null;
            var c = Card("SHEEP UNION", "Sheep Union Local 404 demands a lunch break.\nRepresentative: " + N(rep) + ".", "pop", rep);
            c.options.Add(new EventOption("Accept", "-8% production forever, +10 happiness forever.", () => { flags.Add("union_break"); AddGlobalMod("Lunch breaks", 10, 99999); return "Lunch breaks are approved. The sheep eat grass during them. They also eat grass during work. It's fine."; }));
            c.options.Add(new EventOption("Refuse", "-10 happiness for 4 days. Possible strike.", () =>
            {
                AddGlobalMod("Union refused", -10, 4);
                if (rng.NextDouble() < 0.4) Schedule(1.5f, "strike");
                return "The union is displeased. " + rep.name + " writes something in a tiny notebook.";
            }));
            c.options.Add(new EventOption("Bribe " + rep.name, "-100 coins. " + rep.name + " becomes Greedy.", () =>
            {
                res[0] -= 100; if (!rep.Has("greedy")) rep.traits.Add("greedy");
                return "The union dissolves overnight. " + rep.name + " buys a tiny top hat.";
            }, () => res[0] >= 100));
            return c;
        }

        EventCard Ev_Disease()
        {
            var victims = new List<Sheep>();
            for (int i = 0; i < 2 + rng.Next(3); i++) { var v = RandomSheep(x => !victims.Contains(x)); if (v != null) victims.Add(v); }
            if (victims.Count == 0) return null;
            var names = new List<string>(); foreach (var v in victims) names.Add(v.name);
            var c = Card("DISEASE OUTBREAK", "Several sheep are coughing: ^y" + string.Join(", ", names.ToArray()) + "^w.\nTheir wool is coming out slightly green.", "skull", victims[0]);
            c.options.Add(new EventOption("Quarantine them", "They can't work for 2 days.", () => { foreach (var v in victims) v.sick = 2f; return "The sick sheep are moved to a corner. They cough in unison. It's almost musical."; }));
            c.options.Add(new EventOption("Buy medicine", "-120 coins. Everyone recovers.", () => { res[0] -= 120; return "The medicine tastes like grass. Everyone recovers. Suspicious."; }, () => res[0] >= 120));
            c.options.Add(new EventOption("It's probably fine", "Risky.", () =>
            {
                string dead = "";
                foreach (var v in victims)
                {
                    if (!v.Has("tough") && rng.NextDouble() < 0.22) { Kill(v, "Died of the Green Cough."); dead += v.name + " "; }
                    else { v.sick = 1f; if (rng.NextDouble() < 0.2 && !v.Has("sickly")) v.traits.Add("sickly"); }
                }
                return dead == "" ? "It was, in fact, fine. Mostly. Some of them are Sickly now." : "It was not fine. RIP " + dead.Trim() + ".";
            }));
            return c;
        }

        Building ExplodableBuilding()
        {
            var list = new List<Building>();
            foreach (var b in buildings) if (b.Built && !b.damaged && b.level >= 2 && b.Danger >= 0.008f) list.Add(b);
            return list.Count == 0 ? null : list[rng.Next(list.Count)];
        }

        EventCard Ev_Explosion()
        {
            var b = ExplodableBuilding();
            if (b == null) return null;
            b.damaged = true; b.construct = 0.5f; RefreshBuildingSprite(b);
            Sfx.Play("boom"); shake = 2f;
            SpawnParticles(b.Center + Vector2.up, new Color(1f, 0.6f, 0.2f), 30, 5f);
            for (int i = 0; i < 8; i++) SpawnSmoke(b.Center + new Vector2((float)rng.NextDouble() * 2 - 1, (float)rng.NextDouble()), 1.5f);
            AddDecal("scorch", b.Door);
            Sheep flyer = null;
            foreach (var w in b.workers) if (w.act == Act.Working) { flyer = w; break; }
            if (flyer != null) Launch(flyer);
            int cost = b.def.cost[Mathf.Max(0, b.level - 1)] / 3;
            var c = Card("MACHINE EXPLOSION", "The " + b.def.name + " exploded." + (flyer != null ? "\n" + N(flyer) + " was seen flying over the hills." : ""), "skull", flyer);
            c.options.Add(new EventOption("Rebuild it properly", "-" + cost + " coins. Fixed instantly.", () => { res[0] -= cost; FinishConstruction(b); return "Rebuilt. It's shinier now. The bloodstains are still there, but shinier."; }, () => res[0] >= cost));
            c.options.Add(new EventOption("Salvage the parts", "+" + cost / 2 + " coins, building loses a level.", () =>
            {
                res[0] += cost / 2; if (b.level > 1) b.level--;
                FinishConstruction(b);
                return "You sold the good parts. The rest is held together with string.";
            }));
            c.options.Add(new EventOption("Call it a feature", "+20% output forever, more danger. It still repairs itself first.", () => { b.featureMode = true; return "Marketing calls it 'Turbo Mode'. The sheep call it 'the screaming machine'."; }));
            return c;
        }

        EventCard Ev_Cult()
        {
            flags.Add("cult_decided");
            var s = RandomSheep(x => !x.Lamb);
            if (s == null) return null;
            var c = Card("SHEEP CULT", N(s) + " has started a cult worshipping the Great Shears.\nAttendance is mandatory. Attire is naked.", "shears", s);
            c.options.Add(new EventOption("Join the cult", "+12 happiness for 6 days. They will want something later.", () =>
            {
                flags.Add("cult"); AddGlobalMod("Blessed by the Shears", 12, 6);
                Schedule(5 + (float)rng.NextDouble() * 3, "cultdemand");
                return "You wear the ceremonial robe. It's a sheep. You are wearing a sheep.";
            }));
            c.options.Add(new EventOption("Ban it", "-8 happiness for 3 days.", () => { AddGlobalMod("Cult banned", -8, 3); AddMod(s, "Prophet silenced", -20, 4); return s.name + " is very upset. " + Cap(s.He) + " stares at the shears a lot now."; }));
            c.options.Add(new EventOption("Monetize it", "+1 coin per sheep per day. They will want something later.", () =>
            {
                flags.Add("cult"); flags.Add("cult_money");
                Schedule(5 + (float)rng.NextDouble() * 3, "cultdemand");
                return "Donation boxes are installed. Faith has never been so profitable.";
            }));
            return c;
        }

        EventCard Ev_CultDemand()
        {
            if (!flags.Contains("cult")) return null;
            var s = RandomSheep(x => !x.Lamb);
            var c = Card("THE GREAT SHEARS HUNGER", "The Cult of the Great Shears demands an offering.\nThe chanting is getting louder.", "shears");
            c.options.Add(new EventOption("Offer 50 wool", "-50 wool.", () => { Use(Res.Wool, 50); return "The wool is burned in a big ceremony. The Shears seem satisfied. For now."; }, () => Has(Res.Wool, 50)));
            if (s != null)
                c.options.Add(new EventOption("Offer " + s.name, "Lose " + s.name + ". The cult is ecstatic.", () =>
                {
                    Kill(s, "Offered to the Great Shears. Came back as a sweater.");
                    AddGlobalMod("The Shears are pleased", 10, 5);
                    Gain(Res.Clothing, 1);
                    return "You receive one (1) sweater. It is very warm. Nobody talks about it.";
                }));
            c.options.Add(new EventOption("Disband the cult", "-10 happiness for 3 days. No more donations.", () =>
            {
                flags.Remove("cult"); flags.Remove("cult_money");
                AddGlobalMod("Lost their faith", -10, 3);
                return "The cult disbands. Several sheep take up yoga instead.";
            }));
            return c;
        }

        EventCard Ev_Missing()
        {
            var s = RandomSheep(x => !x.Lamb);
            if (s == null) return null;
            s.act = Act.Hidden; RemoveFromJob(s);
            var c = Card("MISSING SHEEP", N(s) + " hasn't been seen since yesterday.", "pop");
            c.options.Add(new EventOption("Send a search party", "Probably finds " + s.Him + ".", () =>
            {
                if (rng.NextDouble() < 0.7 || s.Has("lucky"))
                {
                    s.act = Act.Wander; s.pos = new Vector2(owned.center.x, owned.yMin + 1); s.muddy = 1;
                    string[] where = { "stuck in a tree", "inside a barrel", "in the neighbor's field, eating everything", "on the roof. Nobody knows how", "pretending to be a cloud" };
                    return s.name + " was found " + where[rng.Next(where.Length)] + ".";
                }
                s.act = Act.Wander; Kill(s, "Found... partially. The wolves send their regards.");
                return s.name + " was found. Partially.";
            }));
            c.options.Add(new EventOption("Put up posters", "-10 coins. Maybe " + s.He + " comes back.", () =>
            {
                res[0] -= 10;
                if (rng.NextDouble() < 0.55) { Schedule(1.5f, "returns", s.id); return "The posters say 'HAVE YOU SEEN THIS SHEEP' and show a cloud. You wait."; }
                Vanish(s, "Never found. The posters are still up.");
                return "Nobody calls. The posters fade in the rain.";
            }));
            c.options.Add(new EventOption("Who?", "Forget about " + s.Him + ".", () => { Vanish(s, "Forgotten. Nobody looked."); AddGlobalMod("Management doesn't care", -6, 3); return "Nobody looks. The flock notices that nobody looks."; }));
            return c;
        }

        EventCard SheepReturns(Sheep s)
        {
            if (s.dead) return null;
            s.act = Act.Wander; s.pos = new Vector2(owned.center.x, owned.yMin + 0.6f);
            if (!s.Has("coward") && !s.Has("brave")) s.traits.Add(rng.NextDouble() < 0.5 ? "coward" : "brave");
            var c = Card("THE PRODIGAL SHEEP", N(s) + " came back!\n" + Cap(s.He) + " won't say where " + s.He + " was. " + Cap(s.He) + " has a new scar and a new attitude.", "pop", s);
            c.options.Add(new EventOption("Welcome back", "", () => null));
            return c;
        }

        EventCard Ev_Suspicious()
        {
            flags.Add("shears_decided");
            var c = Card("SUSPICIOUS MERCHANT", "A man in a trenchcoat offers 'definitely legal' industrial shears.\nThey are still warm.", "shears");
            c.options.Add(new EventOption("Buy them", "-150 coins. +50% wool per shear.", () =>
            {
                res[0] -= 150; flags.Add("illegal_shears");
                Schedule(5 + (float)rng.NextDouble() * 5, "inspection");
                return "The shears are incredible. They also have a serial number scratched off.";
            }, () => res[0] >= 150));
            c.options.Add(new EventOption("Decline", "", () => "The man vanishes into a bush. The bush leaves too."));
            c.options.Add(new EventOption("Report him", "+50 coins reward.", () => { res[0] += 50; return "The authorities thank you. The man in the trenchcoat will remember this."; }));
            return c;
        }

        EventCard Ev_Inspection()
        {
            if (!flags.Contains("illegal_shears")) return null;
            int fine = Mathf.Max(200, Mathf.RoundToInt((float)res[0] * 0.3f));
            var c = Card("ILLEGAL WOOL OPERATION", "A Wool Inspector found your 'definitely legal' shears.\nThey are very illegal.\nFine: " + fine + " coins.", "skull");
            c.options.Add(new EventOption("Pay the fine", "-" + fine + " coins, lose the shears.", () => { res[0] = Mathf.Max(0, (float)res[0] - fine); flags.Remove("illegal_shears"); return "The shears are confiscated. The inspector keeps them. For evidence. At home."; }));
            c.options.Add(new EventOption("Bribe the inspector", "-120 coins. 60% chance to keep the shears.", () =>
            {
                res[0] = Mathf.Max(0, (float)res[0] - 120);
                if (rng.NextDouble() < 0.6) return "The inspector says 'nice shears' and leaves. The operation continues.";
                res[0] = Mathf.Max(0, (float)res[0] - fine); flags.Remove("illegal_shears");
                return "The inspector was wearing a wire. You pay the bribe AND the fine.";
            }));
            var s = RandomSheep(x => !x.Lamb);
            if (s != null)
                c.options.Add(new EventOption("Blame " + s.name, s.name + " is arrested. You keep nothing.", () =>
                {
                    flags.Remove("illegal_shears"); Vanish(s, "Arrested for illegal shearing. Framed.");
                    AddGlobalMod("Scapegoat culture", -8, 3);
                    return s.name + " is arrested. " + Cap(s.He) + " stares at you the whole way.";
                }));
            return c;
        }

        public EventCard Ev_ResearchMutation(Sheep s)
        {
            var c = Card("RESEARCH ACCIDENT", N(s) + " mixed two flasks.\nNow " + s.He + " has three eyes.", "research", s);
            c.options.Add(new EventOption("Science!", "+30 RP.", () => { AddRP(30); return "Peer review is pending. The peers are scared."; }));
            c.options.Add(new EventOption("Clean up", "-40 coins, " + s.name + " feels better.", () => { res[0] -= 40; AddMod(s, "Got a nice eyepatch", 10, 4); return s.name + " gets a tiny eyepatch for the extra eye. It doesn't help, but it's stylish."; }, () => res[0] >= 40));
            c.options.Add(new EventOption("Ask the third eye what it sees", "???", () => { flags.Add("moon_vision"); AddRP(10); return "'The moon,' it says. 'There is grass on the moon.' Then it blinks. Separately."; }));
            return c;
        }

        EventCard Ev_Immigrant()
        {
            var v = RandomVariant();
            string name = Names.Random(rng, usedNames);
            var c = Card("RARE IMMIGRANT", "A " + VariantName(v) + " sheep named ^y" + name + "^w wandered in from the hills.\n^k" + VariantDesc(v), "sheep");
            System.Func<Sheep> make = () =>
            {
                var s = CreateSheep(new Vector2(owned.center.x, owned.yMin + 0.6f));
                usedNames.Remove(s.name); s.name = name; s.go.name = name; usedNames.Add(name); s.variant = v;
                return s;
            };
            c.options.Add(new EventOption("Welcome!", "+1 rare sheep.", () => { var s = make(); return s.name + " moves in and immediately eats a flower."; }));
            c.options.Add(new EventOption("Charge an entry fee", "+1 rare sheep, +25 coins, " + name + " is annoyed.", () => { var s = make(); res[0] += 25; AddMod(s, "Paid to get in", -15, 4); return name + " pays in buttons. You accept buttons."; }));
            c.options.Add(new EventOption("Shoo", "", () => name + " wanders back into the hills, looking betrayed."));
            return c;
        }

        EventCard Ev_Truck()
        {
            int n = 2 + rng.Next(2);
            var c = Card("TRUCK CRASH", "A truck carrying " + n + " sheep to a 'farm' crashed nearby.\nThe driver is fine. The sheep are looking at you.", "sheep");
            c.options.Add(new EventOption("Rescue them", "+" + n + " sheep. +5 happiness (heroes!).", () =>
            {
                for (int i = 0; i < n; i++) { var s = CreateSheep(new Vector2(owned.center.x + i * 0.7f, owned.yMin + 0.6f)); AddMod(s, "Rescued!", 15, 4); }
                AddGlobalMod("We are heroes", 5, 3);
                return "The rescued sheep cry with joy. Then they eat your grass.";
            }));
            c.options.Add(new EventOption("Loot the truck", "+coins.", () => { int amt = 80 + rng.Next(80); res[0] += amt; return "You find " + amt + " coins and a sad sandwich. The sheep wander off."; }));
            c.options.Add(new EventOption("Ignore it", "", () => "You look away. The flock does not."));
            return c;
        }

        EventCard Ev_Tax()
        {
            int tax = Mathf.RoundToInt((float)res[0] * 0.1f);
            var s = RandomSheep(x => !x.Lamb && !x.Has("glutton") || x.Has("glutton"));
            var c = Card("TAX COLLECTOR", "The Royal Wool Tax Collector demands 10% of your coins (" + tax + ").", "coin");
            c.options.Add(new EventOption("Pay", "-" + tax + " coins.", () => { res[0] -= tax; return "The tax collector stamps a form, smells a sheep, and leaves."; }));
            if (s != null)
                c.options.Add(new EventOption("Hide the coins in " + s.name, "Risky.", () =>
                {
                    if (rng.NextDouble() < 0.5) return "The tax collector finds nothing. " + s.name + " jingles slightly when walking.";
                    res[0] -= tax * 2;
                    return s.name + " swallowed the coins. Then the tax collector found them anyway. You pay double. " + s.name + " still jingles.";
                }));
            if (flags.Contains("gerald"))
                c.options.Add(new EventOption("Release Gerald", "", () => "The tax collector leaves. Quickly. Without his hat."));
            return c;
        }

        EventCard Ev_Talent()
        {
            var s = RandomSheep(x => !x.Lamb);
            if (s == null) return null;
            var c = Card("TALENT SHOW", "The flock wants to hold a talent show.\n" + N(s) + " has been practicing something.", "happy", s);
            c.options.Add(new EventOption("Allow it", "-15 food. +12 happiness for 3 days.", () => { Use(Res.Food, Mathf.Min(15, (float)res[(int)Res.Food])); AddGlobalMod("Talent show", 12, 3); AddMod(s, "Star of the show", 15, 4); return s.name + " juggles three lambs. Standing ovation."; }));
            c.options.Add(new EventOption("Deny it", "-4 happiness.", () => { AddGlobalMod("No talent show", -4, 2); return "The flock stares at you. " + s.name + " juggles anyway, sadly, alone."; }));
            c.options.Add(new EventOption("Sell tickets", "+60 coins, +5 happiness.", () => { res[0] += 60; AddGlobalMod("Talent show", 5, 2); return "Humans pay to watch " + s.name + ". The act is mostly screaming. It sells out."; }));
            return c;
        }

        EventCard Ev_Heat()
        {
            var c = Card("HEAT WAVE", "It is very hot.\nThe fluffy ones are suffering.", "warning");
            c.options.Add(new EventOption("Mass shearing!", "Shear every sheep with some wool.", () =>
            {
                int n = 0;
                foreach (var s in sheep) if (!s.dead && !s.Busy && s.act != Act.Hidden && s.wool >= 0.35f) { DoShear(s, null, false); n++; }
                return "You shear " + n + " sheep in an hour. It's raining wool. Everyone is cold now. In summer.";
            }));
            c.options.Add(new EventOption("Build sprinklers", "-80 coins. +6 happiness.", () => { res[0] -= 80; AddGlobalMod("Sprinkler party", 6, 3); return "The sprinklers work great. Several sheep have fallen asleep in the mud."; }, () => res[0] >= 80));
            c.options.Add(new EventOption("Do nothing", "Fluffy sheep will suffer.", () =>
            {
                foreach (var s in sheep) if (!s.dead && s.wool > 0.8f) { AddMod(s, "Heatstroke", -20, 2); if (rng.NextDouble() < 0.3) s.unconscious = 10; }
                return "Several fluffy sheep faint dramatically. One of them was the Dramatic one. The others were real.";
            }));
            return c;
        }

        EventCard Ev_Winter()
        {
            int n = Population;
            var c = Card("WINTER IS HERE", "It is snowing.\nThe shaved sheep regret everything.", "warning");
            c.options.Add(new EventOption("Buy blankets", "-" + n * 6 + " coins. +10 happiness all winter.", () => { res[0] -= n * 6; AddGlobalMod("Blankets", 10, DaysPerSeason); return "Every sheep gets a tiny blanket. They wear them like capes."; }, () => res[0] >= n * 6));
            c.options.Add(new EventOption("Knit sweaters from their own wool", "-" + n * 2 + " wool. +10 happiness all winter.", () => { Use(Res.Wool, n * 2); AddGlobalMod("Wearing themselves", 10, DaysPerSeason); return "The sheep are wearing their own wool. It's fine. Don't think about it."; }, () => Has(Res.Wool, n * 2)));
            c.options.Add(new EventOption("Let them huddle", "-3 happiness.", () => { AddGlobalMod("Huddling", -3, 3); return "The sheep form a giant fluffy ball. Some are in the middle. Some are not."; }));
            return c;
        }

        EventCard Ev_Influencer()
        {
            var c = Card("SHEEP INFLUENCER", "A famous sheep influencer wants a photoshoot at your farm.\n4.2 million followers. Mostly bots. Mostly sheep.", "happy");
            c.options.Add(new EventOption("Accept", "+100 coins. Prices +10%.", () => { res[0] += 100; for (int i = 1; i < ResInfo.Count - 1; i++) priceMod[i] *= 1.1f; return "Your farm goes viral. #fleeced is trending."; }));
            c.options.Add(new EventOption("Decline", "", () => "The influencer posts a vague, sad photo of your fence."));
            c.options.Add(new EventOption("Demand payment", "+200 coins. Might backfire.", () =>
            {
                if (rng.NextDouble() < 0.7) { res[0] += 200; return "They pay. The photos are 90% filters. Your sheep look fantastic."; }
                for (int i = 1; i < ResInfo.Count - 1; i++) priceMod[i] *= 0.9f;
                return "The influencer storms off and posts a 40-minute video about you. Prices drop 10%.";
            }));
            return c;
        }

        EventCard Ev_Moon()
        {
            var s = RandomSheep(x => !x.Lamb);
            if (s == null) return null;
            var c = Card("MOON STARING", N(s) + " has been staring at the moon for three nights.\n'Grass,' " + s.He + " whispers.", "moon", s);
            c.options.Add(new EventOption("Interesting...", "+20 RP.", () => { flags.Add("moon_vision"); AddRP(20); return "The lab takes notes. Something in it draws a rocket. Badly."; }));
            c.options.Add(new EventOption("Go to bed, " + s.name, "", () => { flags.Add("moon_vision"); return s.name + " goes to bed. " + Cap(s.He) + " dreams of space grass."; }));
            return c;
        }

        Sheep GoldenSheep()
        {
            foreach (var s in sheep) if (!s.dead && s.act != Act.Hidden && s.variant == Variant.Golden) return s;
            return null;
        }

        EventCard Ev_Golden()
        {
            var s = GoldenSheep();
            if (s == null) return null;
            int price = 900 + rng.Next(600);
            var c = Card("THE COLLECTOR", "A rich collector wants to buy " + N(s) + " the Golden Fleece for " + price + " coins.\nHe has a very large glass case.", "coin", s);
            c.options.Add(new EventOption("Sell " + s.name, "+" + price + " coins.", () => { res[0] += price; totalMade[0] += price; Vanish(s, "Sold to a collector. Lives in a glass case now."); AddGlobalMod("A friend was sold", -8, 3); return s.name + " is carried away in a glass case. " + Cap(s.He) + " waves."; }));
            c.options.Add(new EventOption("Not for sale", s.name + " is happy.", () => { AddMod(s, "Not for sale!", 20, 6); return s.name + " is deeply touched. " + Cap(s.He) + " sheds a golden tear (+1 wool)."; }));
            return c;
        }

        EventCard Ev_Machine()
        {
            var b = FindBuilding(BKind.Plant);
            if (b == null) return null;
            var c = Card("THE MACHINE", "The Industrial Wool Plant has started making noises that sound like 'baa'.\nThe operators are concerned.", "skull");
            c.options.Add(new EventOption("Ignore it", "", () => "The noises continue. They are getting more confident."));
            c.options.Add(new EventOption("Unplug it", "Plant needs repair. +8 happiness.", () => { b.damaged = true; b.construct = 0.5f; RefreshBuildingSprite(b); AddGlobalMod("The machine is quiet", 8, 3); return "Silence. Then, faintly, from inside: 'baa'."; }));
            c.options.Add(new EventOption("Give it a name", "+25% plant output forever. -3 happiness forever.", () =>
            {
                flags.Add("machine_named"); b.buffMult = 1.25f; b.buffUntil = 999999; AddGlobalMod("It knows our names", -3, 99999);
                return "You name it 'Unit Baa'. It purrs. Production rises. The sheep sleep with the lights on.";
            }));
            return c;
        }

        EventCard Ev_SpaceReady()
        {
            var c = Card("SHEEP SPACE PROGRAM", "The Sheep Space Program is GO.\nBuild the Rocket Pad, give it power, select one brave sheep, then press LAUNCH.\n\nThe moon might contain grass.", "rocket");
            c.options.Add(new EventOption("For the flock!", "", () => null));
            return c;
        }

        void Vanish(Sheep s, string cause)
        {
            RemoveFromJob(s);
            s.dead = true; s.deathCause = cause;
            graveyard.Add(new DeadRecord { name = s.name, cause = cause, level = s.level, day = Day, age = s.ageDays, traits = TraitList(s) });
            if (s.go != null) { Destroy(s.go); Destroy(s.shadow.gameObject); s.go = null; }
            sheep.Remove(s);
            if (selSheep == s) selSheep = null;
            if (followSheep == s) followSheep = null;
        }

        // ---------------- wolves ----------------
        void ScheduleWolves()
        {
            if (dayIndex == DaysPerSeason * 3 || (Season == 3 && DayOfSeason == 1)) QueueEvent(Ev_Winter());
            if (flags.Contains("cult_money")) { int d = Population; res[0] += d; totalMade[0] += d; }
            if (dayIndex < 8 || !tutDone) return;
            if (flags.Contains("gerald_guard") && rng.NextDouble() < 0.5) return;
            if (rng.NextDouble() < Mathf.Min(0.16f, 0.06f + Population * 0.002f))
            {
                int n = Population > 25 && rng.NextDouble() < 0.3 ? 2 : 1;
                for (int i = 0; i < n; i++) SpawnWolf();
                Sfx.Play("howl", 0.6f);
                Log("^rA wolf approaches!^w " + (guardPower > 0 ? "The guards get ready." : "You have no guards. Good luck."));
                AdviseNow("wolf", guardPower > 0 ? "A WOLF approaches! Our guards are ready. Probably." : "A WOLF! We have NO GUARDS. Build a GUARD POST, or say goodbye to someone.", LExpr.Shout, 2f);
            }
        }

        void SpawnWolf()
        {
            var target = RandomSheep(s => !s.Lamb || true);
            if (target == null) return;
            var w = new Wolf();
            int side = rng.Next(4);
            float x = side == 0 ? 0.5f : side == 1 ? MapW - 0.5f : Random.Range(5f, MapW - 5f);
            float y = side == 2 ? 0.5f : side == 3 ? MapH - 0.5f : Random.Range(5f, MapH - 5f);
            w.pos = new Vector2(x, y);
            w.target = target;
            w.go = new GameObject("wolf");
            w.go.transform.SetParent(worldRoot.transform);
            w.sr = w.go.AddComponent<SpriteRenderer>();
            wolves.Add(w);
        }

        void SpawnGerald()
        {
            var w = new Wolf { gerald = true, state = 3 };
            w.pos = new Vector2(owned.center.x, owned.yMin + 0.5f);
            w.wander = w.pos;
            w.go = new GameObject("Gerald");
            w.go.transform.SetParent(worldRoot.transform);
            w.sr = w.go.AddComponent<SpriteRenderer>();
            wolves.Add(w);
        }
        void RemoveGerald()
        {
            flags.Remove("gerald"); flags.Remove("gerald_guard");
            foreach (var w in wolves) if (w.gerald) { w.state = 1; w.gerald = true; }
        }
        Vector2 GeraldPos()
        {
            foreach (var w in wolves) if (w.gerald) return w.pos;
            return new Vector2(owned.center.x, owned.center.y);
        }

        void SimWolves(float dt)
        {
            for (int i = wolves.Count - 1; i >= 0; i--)
            {
                var w = wolves[i];
                w.anim += dt;
                if (w.state == 3)
                {
                    // Gerald wanders around with the flock
                    if ((w.wander - w.pos).sqrMagnitude < 0.05f || w.timer <= 0)
                    {
                        w.wander = new Vector2(Random.Range(owned.xMin + 1f, owned.xMax - 1f), Random.Range(owned.yMin + 1f, owned.yMax - 1f));
                        w.timer = 6;
                    }
                    w.timer -= dt;
                    w.pos = Vector2.MoveTowards(w.pos, w.wander, dt * 0.9f);
                    continue;
                }
                if (w.state == 0)
                {
                    if (w.target == null || w.target.dead || w.target.act == Act.Hidden)
                    {
                        w.target = RandomSheep();
                        if (w.target == null) { w.state = 1; continue; }
                    }
                    Vector2 d = w.target.pos - w.pos;
                    w.pos += d.normalized * Mathf.Min(d.magnitude, dt * 2.4f);
                    if (d.magnitude < 0.45f) ResolveWolf(w);
                }
                else if (w.state == 1)
                {
                    Vector2 away = (w.pos - new Vector2(MapW / 2f, MapH / 2f)).normalized;
                    w.pos += away * dt * 4f;
                    if (w.pos.x < -2 || w.pos.y < -2 || w.pos.x > MapW + 2 || w.pos.y > MapH + 2) { Destroy(w.go); wolves.RemoveAt(i); }
                }
            }
        }

        void ResolveWolf(Wolf w)
        {
            var s = w.target;
            float repel = 0.45f + guardPower / (guardPower + 1.5f) * 0.55f;
            if (s.variant == Variant.Spiky) repel += 0.3f;
            if (s.Has("lucky")) repel += 0.25f;
            if (s.Has("strong") || s.Has("brave")) repel += 0.15f;
            if (s.Has("coward")) repel -= 0.1f;
            if (rng.NextDouble() < repel)
            {
                w.state = 1;
                Sfx.Play("thud"); Sfx.Play("howl", 0.4f, 1.5f);
                FloatText(w.pos + Vector2.up, "BONK", new Color(1f, 0.9f, 0.5f));
                SpawnParticles(w.pos + Vector2.up * 0.5f, new Color(0.6f, 0.6f, 0.65f), 10, 3f);
                s.kills++;
                Log(guardPower > 0 ? "^gThe Guard Post^w scared a wolf away from " + s.name + "." : "^g" + s.name + "^w fought off a wolf alone. Nobody helped.");
            }
            else
            {
                w.state = 1;
                Kill(s, "Eaten by a wolf. Mostly.");
                Sfx.Play("crunch");
            }
        }

        void UpdateWolfVisuals(float rdt)
        {
            foreach (var w in wolves)
            {
                if (w.go == null) continue;
                bool moving = true;
                w.sr.sprite = EnvArt.Wolf(moving ? (int)(Time.time * 8) % 2 : 0, w.gerald);
                Vector2 dir = w.state == 3 ? w.wander - w.pos : w.state == 0 && w.target != null ? w.target.pos - w.pos : w.pos - new Vector2(MapW / 2f, MapH / 2f);
                if (Mathf.Abs(dir.x) > 0.05f) w.sr.flipX = dir.x < 0;
                w.go.transform.position = new Vector3(w.pos.x, w.pos.y + Mathf.Abs(Mathf.Sin(Time.time * 10)) * 0.05f, 0);
                w.sr.sortingOrder = SortY(w.pos.y);
            }
        }

        // ---------------- space ----------------
        void SpawnRocket(Building b)
        {
            rocketPad = b;
            rocketGo = new GameObject("rocket");
            rocketGo.transform.SetParent(b.go.transform, false);
            rocketGo.transform.localPosition = new Vector3(1.25f, 0.45f, 0);
            var sr = rocketGo.AddComponent<SpriteRenderer>();
            sr.sprite = BuildingArt.Rocket();
            sr.sortingOrder = SortY(b.y) + 3;
            b.extras.Add(rocketGo);
        }

        public string LaunchBlocker(Building b)
        {
            if (!b.Built) return "Finish construction first";
            if (LaunchVolunteer() == null) return "No sheep to launch";
            if (powerFactor < 0.99f) return "Needs full power (" + Mathf.RoundToInt(powerMade) + "/" + Mathf.RoundToInt(powerNeed) + ")";
            return null;
        }

        /// the selected sheep if there is one, else a random adult
        Sheep LaunchVolunteer()
        {
            if (selSheep != null && !selSheep.dead && !selSheep.Lamb && selSheep.act != Act.InMachine && selSheep.act != Act.Hidden) return selSheep;
            return RandomSheep(x => !x.Lamb && x.act != Act.InMachine && !x.onBelt);
        }

        public void StartLaunch(Building b)
        {
            if (LaunchBlocker(b) != null) return;
            astronaut = LaunchVolunteer();
            astronaut.act = Act.Hidden; RemoveFromJob(astronaut);
            launchSeq = 0.001f;
            followSheep = null; selBuilding = null; selSheep = null; panel = Panel.None;
            FocusOn(new Vector2(b.x + 1.5f, b.y + 3));
            Sfx.Play("rocket", 1f);
            Log("^y3... 2... 1... BAA!");
        }

        void SimLaunch(float rdt)
        {
            if (launchSeq <= 0 || rocketGo == null) return;
            launchSeq += rdt;
            float t = launchSeq;
            shake = Mathf.Max(shake, t < 4 ? 0.8f : 0.3f);
            if (t > 1f)
            {
                float h = (t - 1f) * (t - 1f) * 2.2f;
                rocketGo.transform.localPosition = new Vector3(1.25f, 0.45f + h, 0);
                rocketGo.GetComponent<SpriteRenderer>().sortingOrder = 29000;
                Vector2 bp = (Vector2)rocketGo.transform.position;
                SpawnParticles(bp, new Color(1f, 0.6f, 0.15f), 3, 2f);
                SpawnSmoke(bp, 1.5f);
                if (t < 5) camTarget = new Vector3(bp.x, bp.y + 1, camTarget.z);
            }
            else SpawnSmoke((Vector2)rocketGo.transform.position + new Vector2(Random.Range(-1f, 1f), 0), 1f);
            if (t > 6.5f)
            {
                launchSeq = 0;
                Destroy(rocketGo); rocketGo = null;
                flags.Add("launched");
                gameWon = true;
                string n = astronaut != null ? astronaut.name : "The astronaut";
                if (astronaut != null) Vanish(astronaut, "Went to the moon. Is still there.");
                foreach (var s in sheep) if (!s.dead) AddMod(s, "WE WENT TO SPACE", 25, 10);
                var c = Card("THE MOON", "Transmission from " + n + ":\n\n'baa.'\n\nThe moon contains no grass. " + n + " is up there forever. The flock considers this a total success.\n\n^kDays: " + dayIndex + "   Sheep: " + Population + "   Deaths: " + deaths + "   Accidents: " + accidents + "   Coins earned: " + Mathf.RoundToInt((float)totalMade[0]), "moon");
                c.options.Add(new EventOption("Keep managing", "The flock goes on.", () => null));
                QueueEvent(c);
                Sfx.Play("fanfare", 1f);
            }
        }
    }
}
