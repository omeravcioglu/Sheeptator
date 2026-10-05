using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Fleeced
{
    [System.Serializable] public class ModSave { public string label; public float value, until; }
    [System.Serializable] public class ItemSave { public int res; public float amount, p; public int x, y; }
    [System.Serializable] public class SchedSave { public float day; public string id; public int sheepId; }

    [System.Serializable]
    public class SheepSave
    {
        public int id; public string name; public bool male; public int level; public float xp, mood, hunger, wool;
        public List<string> traits; public int variant, woolCol, faceCol, baseExpr, job, workplaceId;
        public float ageDays; public int generation; public string parents; public float born;
        public float injured, flattened, burned, muddy, bloody, sick, refuse;
        public bool hidden; public float x, y; public int kills; public float milk; public int penId; public int strain;
        public List<ModSave> mods;
    }

    [System.Serializable]
    public class BuildingSave
    {
        public int id, kind, level, x, y, recipe, accidents; public float construct, progress, buffMult, buffUntil, safetyUntil;
        public bool upgrading, unsafeMode, damaged, featureMode;
        public List<float> pairProgress; public float[] inbuf;
        public bool gateOpen, onlyReady; public int gateMode; public float breedProgress; // gateOpen/onlyReady/gateMode: version 4
        public bool[] doors; public int[] filters; public bool[] touched; // touched: version 6 (machine doors)
        public int geneMode; // version 8 (Gene Lab)
    }

    [System.Serializable]
    public class SaveData
    {
        public int version = 9;
        public int tutStep = -1; public bool tutDone; public int bubbleHarvests, milkCount;
        public int marketBeltSales, machineSheep, gateToggles; public float beltCoins;
        public int lotMeals, slaughtered, beltTierSel; public float trafficT;
        public int soldAlive; public float foodEaten; // version 7
        public int dna, dnaTotal, splicedTotal; public List<string> genes; // version 8 (genetics)
        public float simTime; public int dayIndex, speed;
        public double[] res, totalMade, totalSold;
        public List<SheepSave> sheep = new List<SheepSave>();
        public List<BuildingSave> buildings = new List<BuildingSave>();
        public List<DeadRecord> graveyard = new List<DeadRecord>();
        public List<string> tech, flags, usedNames;
        public string founderName; public int nextId;
        public int wagePolicy, deaths, accidents, births, lastAccidentDay, shearCount, objectiveIndex, beltDelivered;
        public int ox, oy, ow, oh, landLevel;
        public float[] priceMod; public bool[] autoSell; public float rpPool, totalRP; public string currentResearch;
        public List<string> rprogKeys = new List<string>(); public List<float> rprogVals = new List<float>();
        public List<ModSave> globalMods = new List<ModSave>();
        public string grass, wear, dead, paths, belts;
        public List<ItemSave> items = new List<ItemSave>();
        public List<SchedSave> scheduled = new List<SchedSave>();
        public bool hasContract; public int contractRes, contractAmount, contractReward; public float contractDeadline;
        public int tombs, pixelScale; public float camX, camY;
        public bool gameWon;
        public string savedAt;
    }

    public partial class Game
    {
        static string pendingLoadJson;
        static bool skipTitle;
        float autosaveNoteT;

        public static string SavePath { get { return Path.Combine(Application.persistentDataPath, "fleeced_save.json"); } }
        public static bool HasSave { get { try { return File.Exists(SavePath); } catch { return false; } } }

        /// format version of the save on disk (0 if none or unreadable)
        public static int SaveVersion()
        {
            try { return HasSave ? JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath)).version : 0; }
            catch { return 0; }
        }

        public static string SaveSummary()
        {
            try
            {
                var d = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
                return Seasons[(d.dayIndex / DaysPerSeason) % 4] + " " + (d.dayIndex % DaysPerSeason + 1) + ", Year " + (d.dayIndex / (DaysPerSeason * 4) + 1) + " - " + d.sheep.Count + " sheep";
            }
            catch { return "saved game"; }
        }

        // ---------------- save ----------------
        public void SaveGame(bool silent)
        {
            if (!started || launchSeq > 0) return;
            try
            {
                var d = new SaveData();
                d.simTime = simTime; d.dayIndex = dayIndex; d.speed = speed == 0 ? 1 : speed;
                d.res = (double[])res.Clone(); d.totalMade = (double[])totalMade.Clone(); d.totalSold = (double[])totalSold.Clone();
                foreach (var s in sheep)
                {
                    if (s.dead) continue;
                    var ss = new SheepSave
                    {
                        id = s.id, name = s.name, male = s.male, level = s.level, xp = s.xp, mood = s.mood, hunger = s.hunger, wool = s.wool,
                        traits = new List<string>(s.traits), variant = (int)s.variant, woolCol = s.woolCol, faceCol = s.faceCol, baseExpr = (int)s.baseExpr,
                        job = (int)s.job, workplaceId = s.workplace != null && s.job != Job.Builder ? s.workplace.id : 0,
                        ageDays = s.ageDays, generation = s.generation, parents = s.parents, born = s.born,
                        injured = s.injured, flattened = s.flattened, burned = s.burned, muddy = s.muddy, bloody = s.bloody, sick = s.sick, refuse = s.refuse,
                        hidden = s.act == Act.Hidden, x = s.act == Act.InMachine && s.summonedBy != null ? s.summonedBy.Door.x : s.pos.x, y = s.act == Act.InMachine && s.summonedBy != null ? s.summonedBy.Door.y - 0.35f : s.pos.y,
                        kills = s.kills, milk = s.milk, penId = s.pen != null && buildings.Contains(s.pen) ? s.pen.id : 0, mods = new List<ModSave>(), strain = s.strain
                    };
                    foreach (var m in s.mods) ss.mods.Add(new ModSave { label = m.label, value = m.value, until = m.until });
                    d.sheep.Add(ss);
                }
                foreach (var b in buildings)
                {
                    d.buildings.Add(new BuildingSave
                    {
                        id = b.id, kind = (int)b.def.kind, level = b.level, x = b.x, y = b.y, recipe = b.recipe, accidents = b.accidents,
                        construct = b.construct, progress = b.progress, buffMult = b.buffMult, buffUntil = b.buffUntil, safetyUntil = b.safetyUntil,
                        upgrading = b.upgrading, unsafeMode = b.unsafeMode, damaged = b.damaged, featureMode = b.featureMode,
                        pairProgress = new List<float>(b.pairProgress), inbuf = (float[])b.inbuf.Clone(),
                        doors = (bool[])b.doorOpen.Clone(), filters = (int[])b.doorFilter.Clone(), touched = (bool[])b.doorTouched.Clone(), breedProgress = b.breedProgress, geneMode = b.geneMode
                    });
                }
                d.graveyard = new List<DeadRecord>(graveyard);
                d.tech = new List<string>(tech); d.flags = new List<string>(flags); d.usedNames = new List<string>(usedNames);
                d.founderName = founderName; d.nextId = nextId;
                d.wagePolicy = wagePolicy; d.deaths = deaths; d.accidents = accidents; d.births = births; d.lastAccidentDay = lastAccidentDay;
                d.shearCount = shearCount; d.objectiveIndex = objectiveIndex; d.beltDelivered = beltDelivered;
                d.ox = owned.x; d.oy = owned.y; d.ow = owned.width; d.oh = owned.height; d.landLevel = landLevel;
                d.priceMod = (float[])priceMod.Clone(); d.autoSell = (bool[])autoSell.Clone();
                d.rpPool = rpPool; d.totalRP = totalRP; d.currentResearch = currentResearch;
                foreach (var kv in rprog) { d.rprogKeys.Add(kv.Key); d.rprogVals.Add(kv.Value); }
                foreach (var m in globalMods) d.globalMods.Add(new ModSave { label = m.label, value = m.value, until = m.until });
                int n = MapW * MapH;
                byte[] g = new byte[n], w = new byte[n], de = new byte[n], pa = new byte[n], be = new byte[n];
                for (int x = 0; x < MapW; x++)
                    for (int y = 0; y < MapH; y++)
                    {
                        int i = y * MapW + x; var t = tiles[x, y];
                        g[i] = (byte)Mathf.Clamp(Mathf.RoundToInt(t.grass * 255), 0, 255);
                        w[i] = (byte)Mathf.Clamp(Mathf.RoundToInt(t.wear * 255), 0, 255);
                        de[i] = (byte)Mathf.Clamp(Mathf.RoundToInt(t.dead * 255), 0, 255);
                        pa[i] = (byte)(t.path ? 1 : 0);
                        be[i] = (byte)((t.belt + 1) | (t.beltTier << 3));
                    }
                d.grass = System.Convert.ToBase64String(g); d.wear = System.Convert.ToBase64String(w); d.dead = System.Convert.ToBase64String(de);
                d.paths = System.Convert.ToBase64String(pa); d.belts = System.Convert.ToBase64String(be);
                foreach (var it in items) d.items.Add(new ItemSave { res = (int)it.res, amount = it.amount, x = it.x, y = it.y, p = it.p });
                foreach (var sc in scheduled) d.scheduled.Add(sc);
                if (contract != null) { d.hasContract = true; d.contractRes = (int)contract.res; d.contractAmount = contract.amount; d.contractReward = contract.reward; d.contractDeadline = contract.deadline; }
                d.tombs = tombs.Count; d.pixelScale = pixelScale; d.camX = camTarget.x; d.camY = camTarget.y;
                d.gameWon = gameWon;
                d.soldAlive = soldAlive; d.foodEaten = foodEaten;
                d.dna = dna; d.dnaTotal = dnaTotal; d.splicedTotal = splicedTotal; d.genes = new List<string>(genes);
                d.tutStep = tutStep; d.tutDone = tutDone; d.bubbleHarvests = bubbleHarvests; d.milkCount = milkCount;
                d.marketBeltSales = marketBeltSales; d.machineSheep = machineSheep; d.gateToggles = gateToggles; d.beltCoins = beltCoins;
                d.lotMeals = lotMeals; d.slaughtered = slaughtered; d.beltTierSel = beltTierSel; d.trafficT = trafficT;
                d.savedAt = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                string json = JsonUtility.ToJson(d);
                string tmp = SavePath + ".tmp";
                File.WriteAllText(tmp, json);
                if (File.Exists(SavePath)) File.Delete(SavePath);
                File.Move(tmp, SavePath);
                if (!silent) { Toast("^gGame saved."); Sfx.Play("coin", 0.5f); }
                else autosaveNoteT = 2f;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Save failed: " + e.Message);
                Toast("^rSave failed: " + e.Message);
            }
        }

        void OnApplicationQuit() { SaveGame(true); }

        // ---------------- load / restart ----------------
        public void LoadGame()
        {
            if (!HasSave) { Toast("^rNo saved game found."); return; }
            try { pendingLoadJson = File.ReadAllText(SavePath); }
            catch (System.Exception e) { Toast("^rLoad failed: " + e.Message); return; }
            RestartWorld();
        }

        public void NewGameFromMenu()
        {
            pendingLoadJson = null;
            skipTitle = true;
            RestartWorld();
        }

        void RestartWorld()
        {
            currentEvent = null;
            if (worldRoot != null) Destroy(worldRoot);
            I = null;
            Destroy(gameObject);
            new GameObject("SHEEPTATOR").AddComponent<Game>();
        }

        bool TryApplyPendingLoad()
        {
            if (pendingLoadJson == null) return false;
            string json = pendingLoadJson;
            pendingLoadJson = null;
            SaveData d;
            try { d = JsonUtility.FromJson<SaveData>(json); }
            catch (System.Exception e) { Debug.LogWarning("Corrupt save: " + e.Message); return false; }
            if (d == null || d.res == null) return false;
            ApplySave(d);
            return true;
        }

        void ApplySave(SaveData d)
        {
            simTime = d.simTime; dayIndex = d.dayIndex; speed = Mathf.Clamp(d.speed, 1, 3);
            for (int i = 0; i < ResInfo.Count && i < d.res.Length; i++) { res[i] = d.res[i]; totalMade[i] = d.totalMade[i]; totalSold[i] = d.totalSold[i]; lastRes[i] = res[i]; }
            tech = new HashSet<string>(d.tech ?? new List<string>());
            flags = new HashSet<string>(d.flags ?? new List<string>());
            usedNames = new HashSet<string>(d.usedNames ?? new List<string>());
            founderName = d.founderName;
            wagePolicy = d.wagePolicy; deaths = d.deaths; accidents = d.accidents; births = d.births; lastAccidentDay = d.lastAccidentDay;
            shearCount = d.shearCount; objectiveIndex = d.objectiveIndex; beltDelivered = d.beltDelivered;
            landLevel = d.landLevel;
            owned = new RectInt(d.ox, d.oy, d.ow, d.oh);
            ClearPropsIn(owned);

            // terrain
            try
            {
                byte[] g = System.Convert.FromBase64String(d.grass), w = System.Convert.FromBase64String(d.wear), de = System.Convert.FromBase64String(d.dead), pa = System.Convert.FromBase64String(d.paths);
                for (int x = 0; x < MapW; x++)
                    for (int y = 0; y < MapH; y++)
                    {
                        int i = y * MapW + x; if (i >= g.Length) continue;
                        tiles[x, y].grass = g[i] / 255f; tiles[x, y].wear = w[i] / 255f; tiles[x, y].dead = de[i] / 255f; tiles[x, y].path = pa[i] == 1;
                    }
            }
            catch (System.Exception e) { Debug.LogWarning("Terrain load: " + e.Message); }

            // buildings
            var bmap = new Dictionary<int, Building>();
            foreach (var bs in d.buildings)
            {
                var def = Buildings.Get((BKind)bs.kind);
                var b = PlaceBuilding(def, bs.x, bs.y, true);
                b.id = bs.id; b.level = Mathf.Clamp(bs.level, 1, def.maxLevel); b.construct = bs.construct; b.upgrading = bs.upgrading;
                b.recipe = bs.recipe; b.accidents = bs.accidents; b.progress = bs.progress; b.buffMult = bs.buffMult <= 0 ? 1 : bs.buffMult; b.buffUntil = bs.buffUntil;
                b.safetyUntil = bs.safetyUntil; b.unsafeMode = bs.unsafeMode; b.damaged = bs.damaged; b.featureMode = bs.featureMode;
                if (bs.pairProgress != null) b.pairProgress = new List<float>(bs.pairProgress);
                if (bs.inbuf != null) for (int i = 0; i < ResInfo.Count && i < bs.inbuf.Length; i++) b.inbuf[i] = bs.inbuf[i];
                if (bs.doors != null && bs.doors.Length == 4) for (int i = 0; i < 4; i++) b.doorOpen[i] = bs.doors[i];
                else { b.doorOpen[0] = bs.gateOpen; if (bs.onlyReady) b.doorFilter[0] = 3; }
                if (bs.filters != null && bs.filters.Length == 4) for (int i = 0; i < 4; i++) b.doorFilter[i] = Mathf.Clamp(bs.filters[i], 0, FilterNames.Length - 1);
                if (bs.touched != null && bs.touched.Length == 4) for (int i = 0; i < 4; i++) b.doorTouched[i] = bs.touched[i];
                b.breedProgress = bs.breedProgress;
                b.geneMode = bs.geneMode == Genetics.Chaos ? Genetics.Chaos : Mathf.Clamp(bs.geneMode, 0, Genetics.Strains.Length - 1);
                RefreshBuildingSprite(b);
                bmap[b.id] = b;
            }
            if (flags.Contains("launched") && rocketGo != null) { Destroy(rocketGo); rocketGo = null; }

            // belts + items
            try
            {
                byte[] be = System.Convert.FromBase64String(d.belts);
                for (int x = 0; x < MapW; x++)
                    for (int y = 0; y < MapH; y++)
                    {
                        int i = y * MapW + x;
                        if (i < be.Length && be[i] > 0) SetBelt(x, y, (be[i] & 7) - 1, true, be[i] >> 3);
                    }
            }
            catch (System.Exception e) { Debug.LogWarning("Belt load: " + e.Message); }
            foreach (var it in d.items)
            {
                if (!InMap(it.x, it.y) || tiles[it.x, it.y].belt < 0) { res[it.res] += it.amount; continue; }
                var dv = Dir(tiles[it.x, it.y].belt);
                var bi = SpawnItem((Res)it.res, it.amount, it.x, it.y, new Vector2(it.x + 0.5f - dv.x * 0.5f, it.y + 0.5f - dv.y * 0.5f));
                bi.p = it.p;
            }

            // sheep
            foreach (var ss in d.sheep)
            {
                var s = new Sheep
                {
                    id = ss.id, name = ss.name, male = ss.male, level = ss.level, xp = ss.xp, mood = ss.mood, hunger = ss.hunger, wool = ss.wool,
                    traits = ss.traits ?? new List<string>(), variant = (Variant)ss.variant, woolCol = ss.woolCol, faceCol = ss.faceCol, baseExpr = (Expr)ss.baseExpr,
                    job = (Job)ss.job, ageDays = ss.ageDays, generation = ss.generation, parents = ss.parents, born = ss.born,
                    injured = ss.injured, flattened = ss.flattened, burned = ss.burned, muddy = ss.muddy, bloody = ss.bloody, sick = ss.sick, refuse = ss.refuse,
                    kills = ss.kills, milk = ss.milk, strain = Mathf.Clamp(ss.strain, 0, Genetics.Strains.Length - 1)
                };
                s.pos = new Vector2(ss.x, ss.y); s.target = s.pos;
                s.moodTarget = s.mood;
                s.statusTimer = (float)rng.NextDouble() * 5; s.baaTimer = 5 + (float)rng.NextDouble() * 20;
                if (ss.mods != null) foreach (var m in ss.mods) s.mods.Add(new MoodMod(m.label, m.value, m.until));
                usedNames.Add(s.name);
                sheep.Add(s);
                SpawnSheepGO(s);
                s.job = Job.Grazer; s.workplace = null; // version 7: sheep never work, they are managed
                Building home;
                if (ss.penId != 0 && bmap.TryGetValue(ss.penId, out home) && IsPen(home)) s.pen = home;
                if (ss.hidden) s.act = Act.Hidden;
            }
            graveyard = d.graveyard ?? new List<DeadRecord>();
            for (int i = 0; i < d.tombs; i++) AddTombstone();

            // economy & meta
            if (d.priceMod != null) for (int i = 0; i < priceMod.Length && i < d.priceMod.Length; i++) priceMod[i] = d.priceMod[i];
            if (d.autoSell != null) for (int i = 0; i < autoSell.Length && i < d.autoSell.Length; i++) autoSell[i] = d.autoSell[i];
            rpPool = d.rpPool; totalRP = d.totalRP; currentResearch = string.IsNullOrEmpty(d.currentResearch) ? null : d.currentResearch;
            rprog.Clear();
            for (int i = 0; i < d.rprogKeys.Count && i < d.rprogVals.Count; i++) rprog[d.rprogKeys[i]] = d.rprogVals[i];
            globalMods.Clear();
            foreach (var m in d.globalMods) globalMods.Add(new MoodMod(m.label, m.value, m.until));
            scheduled.Clear();
            foreach (var sc in d.scheduled) scheduled.Add(sc);
            if (d.hasContract) contract = new Contract { res = (Res)d.contractRes, amount = d.contractAmount, reward = d.contractReward, deadline = d.contractDeadline };
            gameWon = d.gameWon;
            marketBeltSales = d.marketBeltSales; machineSheep = d.machineSheep; gateToggles = d.gateToggles; beltCoins = d.beltCoins;
            lotMeals = d.lotMeals; slaughtered = d.slaughtered; trafficT = d.trafficT; beltTierSel = Mathf.Clamp(d.beltTierSel, 0, MaxBeltTier);
            if (d.version < 5) { tutDone = true; milkCount = d.milkCount; }   // older saves skip the (newer) tutorial
            else
            {
                tutDone = d.tutDone; tutStep = d.tutStep; bubbleHarvests = d.bubbleHarvests; milkCount = d.milkCount;
                if (d.version < 6 && !tutDone && tutStep >= MachineDoorStep) tutStep++; // version 6 added the machine-door lesson
            }
            if (d.version < 6)
            {
                if (objectiveIndex >= SplitObjective) objectiveIndex++;          // and the "split a sheep line" decree
                if (tutDone) machineDoorNews = true;                             // the Supreme Leader announces machine doors
            }
            soldAlive = d.soldAlive; foodEaten = d.foodEaten;
            dna = d.dna; dnaTotal = d.dnaTotal; splicedTotal = d.splicedTotal;
            genes.Clear(); if (d.genes != null) foreach (var gid in d.genes) genes.Add(gid);
            ApplyGenes();
            if (d.version < 8 && objectiveIndex >= GeneticsObjective) objectiveIndex += 5; // version 8 inserted the genetics decrees
            if (d.version < 9 && objectiveIndex >= HybridObjective) objectiveIndex += 2;    // version 9 inserted the hybrid decrees
            CountPens();
            foreach (var s in sheep) if (s.pen == null) AssignPen(s);
            ResetDayStats();
            if (flags.Contains("gerald")) SpawnGerald();
            nextId = Mathf.Max(nextId, d.nextId);

            BuildFence();
            RepaintGround(true);
            RefreshOffers();
            ComputeGlobals();
            pixelScale = d.pixelScale > 0 ? d.pixelScale : pixelScale;
            camTarget = new Vector3(d.camX, d.camY, -10);
            ApplyCamera();
            cam.transform.position = camTarget;
            started = true;
            if (!tutDone) ResumeTutorial();
            Log("^gGame loaded.^w Welcome back to " + DateString() + ", Year " + Year + ".");
        }
    }
}
