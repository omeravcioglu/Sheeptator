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
        public bool herdHand; public double drove; public float droveWool, droveMilk, droveHunger; // version 11: herd hands and droves
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
        public double[] herdEwes, herdRams, herdLambs, herdCarry; public float herdWool, herdMilk, herdHunger; // version 11: the room's herd
    }

    [System.Serializable]
    public class SaveData
    {
        public int version = Game.CurrentSaveVersion;
        /// the oldest save version that can read this file (version 10 on). A build refuses a file whose minReader is above its
        /// own version (or, for files without one, whose version is). Declared second so the first 256 bytes hold it.
        public int minReader = Game.CurrentSaveVersion;
        public int tutStep = -1; public bool tutDone; public int bubbleHarvests, milkCount;
        public int marketBeltSales, machineSheep, gateToggles; public float beltCoins;
        public int lotMeals, slaughtered, beltTierSel; public float trafficT;
        public int soldAlive; public double foodEaten; // version 7 (a double since 11: a herd eats millions)
        public int dna, dnaTotal, splicedTotal; public List<string> genes; // version 8 (genetics)
        public double simTime; public int dayIndex, speed; // simTime: a double since M2 (older builds read it as a float)
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
        public string savedUtc;                    // version 10: when, as ISO 8601 UTC
        public int mapW, mapH;                     // version 10: the map the terrain layers were written for
        public string objectiveId, tutStepId;      // version 10: progress by id (objectiveIndex and tutStep are still written)
        public float gateCharge = -1; public int gateReleased, pathBought; // version 10: the Sheep Gate; -1 = a full gate
        public List<string> pathKeys = new List<string>(); public List<int> pathVals = new List<int>(); // version 10: upgrade paths
        public double lifetimeEarned;              // version 10: every coin earned (not refunds or tutorial top-ups)
        public double dayIncome, incomeToday;      // version 10: the farm's own income, last day and today (offline earnings)
        public double dayStartPop = -1, liveToday; public int soldToday, slaughteredToday; // version 11: what the day's income measure takes out
    }

    public partial class Game
    {
        /// the save format this build writes. Bump it with every released format change.
        public const int CurrentSaveVersion = 12; // 12: gate stages, breeding chambers, the M12 paths and decrees (M12); 11: herds and droves (M11)

        static string pendingLoadJson, pendingLoadFrom; // what the next world loads, and the file it came from
        static bool skipTitle;
        static bool loadRetried;   // a save that fails to load falls back to its .bak at most once, so it can never loop
        static bool backupChosen;  // the player pressed LOAD BACKUP: that world may replace a newer save once it has archived it
        static bool bootChecked;   // FinishInterruptedSave runs once per run of the game
        static string titleNotice; // why the title shows instead of the save: a failed or refused load
        static readonly HashSet<ulong> quarantined = new HashSet<ulong>(); // saves that failed to load and were kept this run
        static readonly HashSet<ulong> failedLoads = new HashSet<ulong>(); // saves that failed to load this run, kept or not
        static bool damagedCopyFailed; // the last failed load couldn't keep its copy, so the notices mustn't promise one
        public static int worldsCreated;
        float autosaveNoteT;
        string lastSaveError; // null after a successful save
        bool inert;           // a load failed half-way: this world does nothing and is replaced next frame
        bool loadedByChoice;  // this world came from LOAD BACKUP
        bool wroteOnce;       // this world's save has reached the disk at least once
        int lossy;            // how much of the loaded save this build had to skip or repair (unknown kinds, NaN...)
        // ArchiveOnce: the file this world loaded (length, hash, version), whether its first write has checked the disk yet,
        // and whether the save itself is already kept (so a retry after a failed .bak copy doesn't copy it twice)
        bool archiveChecked, mainKept; int loadedLen = -1; ulong loadedHash; int loadedVersion;

        static readonly string NewerSaveNotice = "This save is from a newer version of " + Title + ". Update the game to continue it.";

        public static string SavePath { get { return Path.Combine(Application.persistentDataPath, "fleeced_save.json"); } }
        public static bool HasSave { get { try { return File.Exists(SavePath); } catch { return false; } } }

        /// format version of the save on disk (0 if none or unreadable)
        public static int SaveVersion()
        {
            try { return HasSave ? JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath)).version : 0; }
            catch { return 0; }
        }

        /// the version field at the start of the save on disk, read without loading the rest (0 if there is none)
        static int VersionOnDisk()
        {
            try
            {
                using (var fs = new FileStream(SavePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    var head = new byte[256];
                    int n = fs.Read(head, 0, head.Length);
                    return NeedOf(System.Text.Encoding.UTF8.GetString(head, 0, n));
                }
            }
            catch { return 0; }
        }

        /// the .bak can stand in for a damaged save: it parses, this version can read it, and it hasn't failed to load already
        static bool BackupReadable()
        {
            try
            {
                string bak = SavePath + ".bak";
                if (!File.Exists(bak)) return false;
                string json = File.ReadAllText(bak);
                return NeedOf(json) <= CurrentSaveVersion && !failedLoads.Contains(Fnv(json)) && Parses(json);
            }
            catch { return false; }
        }

        public static string SaveSummary()
        {
            try { return Summary(JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath))); }
            catch { return "saved game"; }
        }

        static string Summary(SaveData d)
        {
            int day = Mathf.Max(0, d.dayIndex);
            return Seasons[(day / DaysPerSeason) % 4] + " " + (day % DaysPerSeason + 1) + ", Year " + (day / (DaysPerSeason * 4) + 1) + " - " + UI.Fmt(SavedSheep(d)) + " sheep";
        }

        // ---------------- save ----------------
        public void SaveGame(bool silent)
        {
            if (!started || launchSeq > 0 || inert) return;
            try
            {
                var d = new SaveData();
                d.simTime = simClock; d.dayIndex = dayIndex; d.speed = speed == 0 ? 1 : speed;
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
                        kills = s.kills, milk = s.milk, penId = s.pen != null && buildings.Contains(s.pen) ? s.pen.id : 0, mods = new List<ModSave>(), strain = s.strain,
                        herdHand = s.herdHand, drove = s.drove != null ? s.drove.n : 0, droveWool = s.drove != null ? s.drove.wool : 0,
                        droveMilk = s.drove != null ? s.drove.milk : 0, droveHunger = s.drove != null ? s.drove.hunger : 0
                    };
                    foreach (var m in s.mods) ss.mods.Add(new ModSave { label = m.label, value = m.value, until = m.until });
                    d.sheep.Add(ss);
                }
                foreach (var b in buildings)
                {
                    var bsv = new BuildingSave
                    {
                        id = b.id, kind = (int)b.def.kind, level = b.level, x = b.x, y = b.y, recipe = b.recipe, accidents = b.accidents,
                        construct = b.construct, progress = b.progress, buffMult = b.buffMult, buffUntil = b.buffUntil, safetyUntil = b.safetyUntil,
                        upgrading = b.upgrading, unsafeMode = b.unsafeMode, damaged = b.damaged, featureMode = b.featureMode,
                        pairProgress = new List<float>(b.pairProgress), inbuf = (float[])b.inbuf.Clone(),
                        doors = (bool[])b.doorOpen.Clone(), filters = (int[])b.doorFilter.Clone(), touched = (bool[])b.doorTouched.Clone(), breedProgress = b.breedProgress, geneMode = b.geneMode
                    };
                    SaveHerd(b, bsv);
                    d.buildings.Add(bsv);
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
                d.savedUtc = System.DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture);
                d.mapW = MapW; d.mapH = MapH;
                d.gateCharge = gateCharge; d.gateReleased = gateReleased; d.pathBought = pathBought; d.lifetimeEarned = lifetimeEarned;
                d.dayIncome = dayIncome; d.incomeToday = incomeToday;
                d.dayStartPop = dayStartPop; d.liveToday = liveToday; d.soldToday = soldToday; d.slaughteredToday = slaughteredToday;
                foreach (var kv in pathLevels) { d.pathKeys.Add(kv.Key); d.pathVals.Add(kv.Value); }
                d.objectiveId = CurrentObjective != null ? CurrentObjective.id : "end";
                if (steps == null) BuildTutorial();
                d.tutStepId = !tutDone && tutStep >= 0 && tutStep < steps.Count ? steps[tutStep].id : null;
                int bad = CleanNumbers(d);
                if (bad > 0) Debug.LogWarning("Save: " + bad + " NaN or infinite values were written as 0");
                string json = JsonUtility.ToJson(d);
                // a newer version's save is only ever replaced by a new game that has just archived it (its first write)
                if ((loadedLen >= 0 || wroteOnce) && !(loadedByChoice && !wroteOnce) && VersionOnDisk() > CurrentSaveVersion)
                {
                    lastSaveError = "the save on disk is from a newer version";
                    Toast("^rNot saved: the save on disk is from a newer version of the game.");
                    return;
                }
                if (!ArchiveOnce())
                {
                    lastSaveError = "couldn't keep a copy of the old save";
                    Toast("^rNot saved: couldn't keep a copy of the old save.");
                    return;
                }
                WriteSaveFile(json);
                wroteOnce = true;
                lastSaveError = null;
                if (!silent) { Toast("^gGame saved."); Sfx.Play("coin", 0.5f); }
                else autosaveNoteT = 2f;
            }
            catch (System.Exception e)
            {
                lastSaveError = e.Message;
                Debug.LogWarning("Save failed: " + e.Message);
                Toast("^rSave failed: " + e.Message);
            }
        }

        /// Writes the save without ever deleting it first: write .tmp, then swap it in, keeping the old file as .bak.
        static void WriteSaveFile(string json)
        {
            string tmp = SavePath + ".tmp";
            byte[] bytes = new System.Text.UTF8Encoding(false).GetBytes(json);
            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                fs.Write(bytes, 0, bytes.Length);
                fs.Flush(true);
            }
            SaveFault("tmp");
            if (!File.Exists(SavePath)) { File.Move(tmp, SavePath); return; }
            try
            {
                SaveFault("replace");
                File.Replace(tmp, SavePath, SavePath + ".bak");
            }
            catch (System.Exception e)
            {
                // something holds the save open (a backup tool, a virus scan): write over it in place, flushed, without a .bak
                // this time. The .tmp goes only once the save is on disk.
                Debug.LogWarning("Save replace failed (" + e.Message + "), writing over the save instead");
                using (var fs = new FileStream(SavePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read))
                {
                    fs.Write(bytes, 0, bytes.Length);
                    fs.SetLength(bytes.Length);
                    fs.Flush(true);
                }
                try { File.Delete(tmp); } catch { }
            }
        }

        /// Before a world first writes the save, it keeps a copy of the file on disk, unless that file is exactly the
        /// current-version save this world loaded. That covers BEGIN or NEW GAME over a save, a failed load and an
        /// older version that was migrated. When the save is missing or unreadable, the .bak is kept too, because the
        /// first writes rotate it away. Archives (fleeced_save.v[N]-[time].bak) are never overwritten or deleted.
        /// False when a copy failed: the caller must not write over a file it couldn't keep.
        bool ArchiveOnce()
        {
            if (archiveChecked) return true;
            bool mainGood = false;
            if (File.Exists(SavePath))
            {
                string json;
                try { json = File.ReadAllText(SavePath); }
                catch (System.Exception e) { Debug.LogWarning("Archive check failed: " + e.Message); return false; }
                // a save loaded with parts skipped (lossy) is kept too: this world's saves would drop those parts for good
                bool ours = Ours(json) && loadedVersion == CurrentSaveVersion && lossy == 0;
                if (!ours && !mainKept && !quarantined.Contains(Fnv(json)))
                {
                    if (!Archive(SavePath, VersionOf(json))) return false;
                    mainKept = true;
                }
                mainGood = ours || Parses(json);
            }
            string bak = SavePath + ".bak";
            if (!mainGood && File.Exists(bak))
            {
                string json;
                try { json = File.ReadAllText(bak); }
                catch (System.Exception e) { Debug.LogWarning("Archive check failed: " + e.Message); return false; }
                // a .bak this world loaded in full lives on in its saves
                if (!(Ours(json) && loadedVersion == CurrentSaveVersion && lossy == 0) && !quarantined.Contains(Fnv(json)) && !Archive(bak, VersionOf(json))) return false;
            }
            archiveChecked = true;
            return true;
        }

        /// the text this world loaded
        bool Ours(string json) { return loadedLen >= 0 && json.Length == loadedLen && Fnv(json) == loadedHash; }

        static bool Archive(string file, int version)
        {
            string dst = ArchivePath(version);
            try { File.Copy(file, dst, false); }
            catch (System.Exception e) { Debug.LogWarning("Archive failed: " + e.Message); return false; }
            Debug.Log("Archived " + Path.GetFileName(file) + " to " + dst);
            return true;
        }

        /// readable the way loading reads it: JsonUtility parses it and it has coins
        static bool Parses(string json)
        {
            try { var d = JsonUtility.FromJson<SaveData>(json); return d != null && d.res != null; }
            catch { return false; }
        }

        static string ArchivePath(int version)
        {
            string stamp = System.DateTime.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            string stem = Path.Combine(Application.persistentDataPath, "fleeced_save.v" + version + "-" + stamp);
            string p = stem + ".bak";
            for (int i = 2; File.Exists(p); i++) p = stem + "-" + i + ".bak";
            return p;
        }

        /// the "version" field of a save, read without parsing the rest (0 if there is none)
        static int VersionOf(string json)
        {
            var m = System.Text.RegularExpressions.Regex.Match(json ?? "", "\"version\"\\s*:\\s*(-?\\d+)");
            int v;
            return m.Success && int.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out v) ? v : 0;
        }

        /// the save version a build needs to read this text: its "minReader" when it has one (version 10 on), else its "version"
        static int NeedOf(string json)
        {
            var m = System.Text.RegularExpressions.Regex.Match(json ?? "", "\"minReader\"\\s*:\\s*(-?\\d+)");
            int v;
            if (m.Success && int.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out v) && v > 0) return v;
            return VersionOf(json);
        }

        static ulong Fnv(string s)
        {
            ulong h = 14695981039346656037UL;
            for (int i = 0; i < s.Length; i++) { h ^= s[i]; h *= 1099511628211UL; }
            return h;
        }

        /// test builds can fail a save at a named point (Tools/Harness); release builds compile the calls away
        [System.Diagnostics.Conditional("FLEECED_DEV")]
        static void SaveFault(string at)
        {
#if FLEECED_DEV
            if (DevHooks.SaveFault != null) DevHooks.SaveFault(at);
#endif
        }

        void OnApplicationQuit() { SaveGame(true); }

        /// The day-change autosave, at most once every AutosaveGap seconds of real time: a game day is 6 s at 4x, and writing a big
        /// farm is a hitch. The test harness sets the gap to 0 so its suites see a save every day.
        public static float AutosaveGap = 20f;
        float lastAutosaveT = -1e6f;
        void Autosave()
        {
            if (Time.unscaledTime - lastAutosaveT < AutosaveGap) return;
            lastAutosaveT = Time.unscaledTime;
            SaveGame(true);
        }

        /// the window loses focus (alt-tab, the Steam overlay, a lid closing): save now
        void OnApplicationFocus(bool focus)
        {
#if FLEECED_DEV
            if (!DevHooks.FocusSaves) return; // a test run must not write saves when some other window takes the focus
#endif
            if (!focus) SaveGame(true);
        }

        // ---------------- load / restart ----------------
        public void LoadGame()
        {
            if (!HasSave) { Toast("^rNo saved game found."); return; }
            string json;
            try { json = File.ReadAllText(SavePath); }
            catch (System.Exception e) { Toast("^rLoad failed: " + e.Message); return; }
            if (NeedOf(json) > CurrentSaveVersion) { RefuseNewer(); return; } // the world in play stays as it is
            if (started && !Parses(json) && !BackupReadable())
            {
                // F9 on a damaged save with nothing to fall back on: the game in play is the newest good copy, so it stays
                failedLoads.Add(Fnv(json));
                bool kept = Quarantine(SavePath);
                Toast("^rThe save on disk is damaged." + (kept ? " A copy of it is kept." : "") + " This game goes on.");
                return;
            }
            pendingLoadJson = json; pendingLoadFrom = SavePath;
            loadRetried = false; damagedCopyFailed = false;
            RestartWorld();
        }

        /// The best save to fall back on when the main one can't be continued: the .bak, else the newest archive, if this
        /// version can read it. Null when there is none. `info` says what it holds.
        static string FindBackup(out string info)
        {
            info = null;
            var files = new List<string>();
            if (File.Exists(SavePath + ".bak")) files.Add(SavePath + ".bak");
            try
            {
                var arch = new List<FileInfo>(new DirectoryInfo(Application.persistentDataPath).GetFiles("fleeced_save.v*.bak"));
                arch.Sort((a, b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc)); // newest first: a copy keeps its save's time
                foreach (var f in arch) files.Add(f.FullName);
            }
            catch { }
            foreach (var f in files)
            {
                try
                {
                    string json = File.ReadAllText(f);
                    if (failedLoads.Contains(Fnv(json))) continue; // it already failed to load this run
                    int v = VersionOf(json);
                    if (v < 1 || NeedOf(json) > CurrentSaveVersion) continue;
                    var d = JsonUtility.FromJson<SaveData>(json);
                    if (d == null || d.res == null) continue;
                    info = Summary(d) + (string.IsNullOrEmpty(d.savedAt) ? "" : ", saved " + d.savedAt);
                    return f;
                }
                catch { }
            }
            return null;
        }

        /// LOAD BACKUP on the title: an older copy, loaded on purpose. The save on disk is archived before anything replaces it.
        public void LoadBackup(string path)
        {
            string json;
            try { json = File.ReadAllText(path); }
            catch (System.Exception e) { titleNotice = "The backup couldn't be read: " + e.Message; return; }
            pendingLoadJson = json; pendingLoadFrom = path;
            loadRetried = true; // no second fallback: if this one fails too, the title says so
            backupChosen = true;
            RestartWorld();
        }

        void RefuseNewer()
        {
            titleNotice = NewerSaveNotice;
            if (started) Toast("^r" + NewerSaveNotice);
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

        /// Loads the pending save into this new world. False when there is nothing to load (or it was refused): the caller
        /// makes a new game. True when the save loaded, or when it failed and this world is now inert (see LoadFailed).
        bool TryApplyPendingLoad()
        {
            if (pendingLoadJson == null) return false;
            string json = pendingLoadJson, from = pendingLoadFrom ?? SavePath;
            pendingLoadJson = null; pendingLoadFrom = null;
            if (NeedOf(json) > CurrentSaveVersion) { RefuseNewer(); return false; }
            SaveData d = ParseSave(json, from);
            if (d == null) { LoadFailed(from); return true; }
            try
            {
                lossy = CleanNumbers(d);
                if (lossy > 0) Debug.LogWarning("Load: " + lossy + " NaN or infinite values in " + Path.GetFileName(from) + " were read as 0");
                ApplySave(d); // adds to `lossy` for anything it has to skip or repair
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Load failed (" + Path.GetFileName(from) + "): " + e);
                LoadFailed(from);
                return true;
            }
            loadedLen = json.Length; loadedHash = Fnv(json); loadedVersion = d.version;
            loadRetried = false;
            titleNotice = null;
            if (lossy > 0) Log("^oPart of this save couldn't be loaded^w (" + lossy + " things skipped or repaired). The file as it was is archived before the next save.");
            loadedByChoice = backupChosen; backupChosen = false;
            if (loadedByChoice) Log("^oLoaded a backup.^w The save it replaces is archived before the first autosave.");
            else if (from != SavePath) LoadedBackupNotice();
            return true;
        }

        /// the save as JsonUtility reads it, or null when it is unreadable (JsonUtility throws on a cut-off file).
        /// Bare NaN and Infinity tokens, which JsonUtility writes, read back fine; CleanNumbers then zeroes them.
        static SaveData ParseSave(string json, string from)
        {
            SaveData d = null;
            try { d = JsonUtility.FromJson<SaveData>(json); }
            catch (System.Exception e) { Debug.LogWarning("Unreadable save " + Path.GetFileName(from) + ": " + e.Message); }
            return d != null && d.res != null ? d : null;
        }

        /// The save in `from` couldn't be loaded. Keep a copy of it, stop this half-built world (Update replaces it next
        /// frame), and load the .bak instead, once. With no usable .bak the title shows, with a notice.
        void LoadFailed(string from)
        {
            backupChosen = false;
            try { failedLoads.Add(Fnv(File.ReadAllText(from))); } catch { }
            if (!Quarantine(from)) damagedCopyFailed = true;
            inert = true; started = false;
            string bak = SavePath + ".bak";
            bool newerBak = false;
            if (from == SavePath && !loadRetried && File.Exists(bak))
            {
                loadRetried = true;
                try
                {
                    string bj = File.ReadAllText(bak);
                    if (NeedOf(bj) > CurrentSaveVersion) newerBak = true; // this build can't read it: never loaded, never written over
                    else if (!failedLoads.Contains(Fnv(bj))) { pendingLoadJson = bj; pendingLoadFrom = bak; }
                }
                catch (System.Exception e) { Debug.LogWarning("Backup unreadable: " + e.Message); }
            }
            if (pendingLoadJson == null)
            {
                loadRetried = false;
                titleNotice = newerBak ? "Your save is damaged, and its backup is from a newer version of " + Title + ". Update the game to load the backup."
                    : damagedCopyFailed ? "Your save couldn't be loaded, and no copy of it could be made. The file is left as it is."
                    : "Your save couldn't be loaded. A copy of it is kept next to it.";
                damagedCopyFailed = false;
            }
        }

        /// keeps a byte-identical copy of a save that failed to load, under an archive name, so nothing overwrites the only copy.
        /// True when a copy exists (made now or earlier this run).
        static bool Quarantine(string file)
        {
            try
            {
                string json = File.ReadAllText(file);
                ulong h = Fnv(json);
                if (quarantined.Contains(h)) return true;
                if (!Archive(file, VersionOf(json))) return false;
                quarantined.Add(h);
                return true;
            }
            catch (System.Exception e) { Debug.LogWarning("Couldn't keep a copy of " + Path.GetFileName(file) + ": " + e.Message); return false; }
        }

        void LoadedBackupNotice()
        {
            Log("^oYour last save was damaged, so the backup was loaded.^w " + (damagedCopyFailed ? "No copy of the damaged save could be made." : "A copy of the damaged save is kept."));
            damagedCopyFailed = false;
            Toast("^oLoaded the backup save.");
            LeaderPop("Your last save was damaged, Hoof. I restored the backup. Whatever happened since then never happened.", LExpr.Stern);
        }

        /// Once per run, before anything reads the save. A complete .tmp is a save that was cut off before it was swapped
        /// in, and it is newer than the save, so that save is finished now. A cut-off .tmp is left for the next save (the
        /// save itself is intact). A complete .tmp this version can't finish (a newer version's) is archived first.
        static void FinishInterruptedSave()
        {
            string tmp = SavePath + ".tmp", json = null;
            int v = 0;
            try
            {
                if (!File.Exists(tmp)) return;
                json = File.ReadAllText(tmp);
                v = VersionOf(json);
                if (v < 1 || !Parses(json)) { json = null; Debug.LogWarning("Left an unfinished save alone: " + Path.GetFileName(tmp)); return; }
                if (NeedOf(json) > CurrentSaveVersion) { Archive(tmp, v); Debug.LogWarning("Kept a newer version's unfinished save as an archive"); return; }
                if (!File.Exists(SavePath)) File.Move(tmp, SavePath);
                else if (File.ReadAllText(SavePath) == json) File.Delete(tmp);
                else File.Replace(tmp, SavePath, SavePath + ".bak");
                Debug.Log("Finished an interrupted save");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Couldn't finish an interrupted save: " + e.Message);
                if (json != null) Archive(tmp, v); // the next save overwrites the .tmp, so keep it
            }
        }

        /// a room's herd into its save record (nothing for a room without one)
        static void SaveHerd(Building b, BuildingSave bs)
        {
            var h = b.herd;
            if (h == null || h.Total < 1) return;
            bs.herdEwes = (double[])h.ewes.Clone(); bs.herdRams = (double[])h.rams.Clone(); bs.herdLambs = (double[])h.lambs.Clone();
            bs.herdWool = h.wool; bs.herdMilk = h.milk; bs.herdHunger = h.hunger;
            bs.herdCarry = new[] { h.breedT, h.matureT, h.starveT, h.mealT, h.splitT, h.wolfT, h.litterT }; // litterT: version 12
        }

        /// a room's herd from its save record: whole, non-negative counts and averages in range (anything else is a repair).
        /// JsonUtility writes a null array as an empty one, so empty arrays mean no herd too.
        void LoadHerd(Building b, BuildingSave bs)
        {
            if (NoCounts(bs.herdEwes) && NoCounts(bs.herdRams) && NoCounts(bs.herdLambs)) return;
            if (!IsRoom(b)) { lossy++; return; }
            var h = new Flock();
            lossy += CopyCounts(bs.herdEwes, h.ewes) + CopyCounts(bs.herdRams, h.rams) + CopyCounts(bs.herdLambs, h.lambs);
            h.wool = Mathf.Clamp(bs.herdWool, 0, 1.25f); h.milk = Mathf.Clamp01(bs.herdMilk); h.hunger = Mathf.Clamp(bs.herdHunger, 0, 4.5f);
            var c = bs.herdCarry;
            if (c != null && c.Length >= 6) { h.breedT = Frac(c[0]); h.matureT = Frac(c[1]); h.starveT = Frac(c[2]); h.mealT = Frac(c[3]); h.splitT = Frac(c[4]); h.wolfT = Frac(c[5]); }
            if (c != null && c.Length >= 7) h.litterT = Frac(c[6]); // the breeding chambers' litter on the way (version 12)
            if (h.Total >= 1) b.herd = h;
        }

        /// whole counts from a save's array into a herd's; returns how many had to be repaired (negative, fractional, too big)
        static int CopyCounts(double[] from, double[] to)
        {
            if (from == null) return 0;
            int bad = from.Length > to.Length ? 1 : 0;
            for (int i = 0; i < to.Length && i < from.Length; i++)
            {
                double v = from[i];
                if (v >= 0 && v < 1e15 && v == System.Math.Floor(v)) to[i] = v;
                else { to[i] = v > 0 && v < 1e15 ? System.Math.Floor(v) : 0; bad++; }
            }
            return bad;
        }
        static double Frac(double v) { return v > 0 && v <= 1 ? v : 0; } // a carried fraction may be exactly 1 (a lamb due, no room yet)
        static bool NoCounts(double[] a) { return a == null || a.Length == 0; }

        /// every sheep a save holds: named, in droves and in herds (for the title's summary)
        static double SavedSheep(SaveData d)
        {
            double n = 0;
            if (d.sheep != null) foreach (var s in d.sheep) if (s != null) { n++; if (s.drove >= 1 && s.drove < 1e15) n += System.Math.Floor(s.drove); }
            if (d.buildings != null) foreach (var b in d.buildings) if (b != null) n += SumCounts(b.herdEwes) + SumCounts(b.herdRams) + SumCounts(b.herdLambs);
            return n;
        }
        static double SumCounts(double[] a) { double n = 0; if (a != null) foreach (var v in a) if (v > 0 && v < 1e15) n += System.Math.Floor(v); return n; }

        /// Replaces NaN and infinities in a save with 0, so one bad number never stops a save or spreads through a reload.
        /// Walks every public float and double field, array and list of the save classes. Returns how many it replaced.
        static int CleanNumbers(object o, int depth = 0)
        {
            if (o == null || depth > 8) return 0;
            int n = 0;
            foreach (var f in o.GetType().GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
            {
                var t = f.FieldType;
                if (t == typeof(float)) { float v = (float)f.GetValue(o); if (float.IsNaN(v) || float.IsInfinity(v)) { f.SetValue(o, 0f); n++; } }
                else if (t == typeof(double)) { double v = (double)f.GetValue(o); if (double.IsNaN(v) || double.IsInfinity(v)) { f.SetValue(o, 0d); n++; } }
                else if (t == typeof(float[])) { var a = (float[])f.GetValue(o); if (a != null) for (int i = 0; i < a.Length; i++) if (float.IsNaN(a[i]) || float.IsInfinity(a[i])) { a[i] = 0; n++; } }
                else if (t == typeof(double[])) { var a = (double[])f.GetValue(o); if (a != null) for (int i = 0; i < a.Length; i++) if (double.IsNaN(a[i]) || double.IsInfinity(a[i])) { a[i] = 0; n++; } }
                else if (t == typeof(List<float>)) { var l = (List<float>)f.GetValue(o); if (l != null) for (int i = 0; i < l.Count; i++) if (float.IsNaN(l[i]) || float.IsInfinity(l[i])) { l[i] = 0; n++; } }
                else if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>) && t.GetGenericArguments()[0].IsClass && t.GetGenericArguments()[0] != typeof(string))
                {
                    var l = f.GetValue(o) as System.Collections.IList;
                    if (l != null) foreach (var e in l) n += CleanNumbers(e, depth + 1);
                }
                else if (t.IsClass && t != typeof(string) && !t.IsArray && t.IsDefined(typeof(System.SerializableAttribute), false)) n += CleanNumbers(f.GetValue(o), depth + 1);
            }
            return n;
        }

        /// The current decree of a save. Version 10 saves its id. Older versions saved an index into their own list, which the
        /// frozen tables (DecreeIdsFor) turn into an id; past the end of the list means every decree is done.
        int DecreeFromSave(SaveData d)
        {
            if (d.version >= 10 && !string.IsNullOrEmpty(d.objectiveId))
            {
                if (d.objectiveId == "end") return d.version < 11 ? AfterOldDecrees() : Objectives.Length;
                int i = ObjectiveIndexOf(d.objectiveId);
                if (i >= 0) return i;
                lossy++; // a decree this version doesn't know: keep the saved index, as far as it goes
                return Mathf.Clamp(d.objectiveIndex, 0, Objectives.Length);
            }
            if (d.objectiveIndex < 0) { lossy++; return 0; }
            if (d.objectiveIndex == 0) return 0; // still at the first decree (or in the tutorial): start with the decrees new in v10
            var ids = DecreeIdsFor(d.version);
            if (d.objectiveIndex >= ids.Length) return AfterOldDecrees();
            int k = ObjectiveIndexOf(ids[d.objectiveIndex]);
            return k >= 0 ? k : Objectives.Length;
        }

        /// where a save that had done every decree of its own version goes on: the first decree version 11 added after them
        static int AfterOldDecrees() { int i = ObjectiveIndexOf("flock10k"); return i >= 0 ? i : Objectives.Length; }

        /// Builds the world from a save. Tolerant of damage and of saves from other versions: unknown building kinds and
        /// goods are skipped, short or missing arrays keep their defaults, and out-of-range values are clamped.
        void ApplySave(SaveData d)
        {
            simClock = d.simTime; dayIndex = d.dayIndex; speed = Mathf.Clamp(d.speed, 1, 3);
            if (dayIndex < 0) { dayIndex = 0; lossy++; }
            for (int i = 0; i < ResInfo.Count; i++)
            {
                if (i < d.res.Length) res[i] = d.res[i];
                if (d.totalMade != null && i < d.totalMade.Length) totalMade[i] = d.totalMade[i];
                if (d.totalSold != null && i < d.totalSold.Length) totalSold[i] = d.totalSold[i];
                lastRes[i] = res[i];
            }
            tech = new HashSet<string>(d.tech ?? new List<string>());
            flags = new HashSet<string>(d.flags ?? new List<string>());
            usedNames = new HashSet<string>(d.usedNames ?? new List<string>());
            founderName = d.founderName;
            wagePolicy = d.wagePolicy; deaths = d.deaths; accidents = d.accidents; births = d.births; lastAccidentDay = d.lastAccidentDay;
            shearCount = d.shearCount; objectiveIndex = d.objectiveIndex; beltDelivered = d.beltDelivered;
            landLevel = Mathf.Clamp(d.landLevel, 0, LandCost.Length);
            if (landLevel != d.landLevel) lossy++;
            var saved = new RectInt(d.ox, d.oy, d.ow, d.oh);
            var land = saved;
            land.xMin = Mathf.Max(land.xMin, 0); land.yMin = Mathf.Max(land.yMin, 0);
            land.xMax = Mathf.Min(land.xMax, MapW); land.yMax = Mathf.Min(land.yMax, MapH);
            if (land.width >= 4 && land.height >= 4) owned = land; // otherwise the starting land
            if (!owned.Equals(saved)) lossy++;
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
            foreach (var bs in d.buildings ?? new List<BuildingSave>())
            {
                BuildingDef def;
                if (bs == null || !Buildings.TryGet((BKind)bs.kind, out def) || bs.x < 0 || bs.y < 0 || bs.x + def.w > MapW || bs.y + def.h > MapH)
                {
                    if (bs != null) Debug.LogWarning("Load: skipped building kind " + bs.kind + " at " + bs.x + "," + bs.y + " (unknown kind or off the map)");
                    lossy++;
                    continue;
                }
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
                LoadHerd(b, bs);
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
                        if (i >= be.Length || be[i] == 0) continue;
                        int dir = (be[i] & 7) - 1;
                        if (dir >= 0 && dir < 4) SetBelt(x, y, dir, true, be[i] >> 3); else lossy++;
                    }
            }
            catch (System.Exception e) { Debug.LogWarning("Belt load: " + e.Message); }
            foreach (var it in d.items ?? new List<ItemSave>())
            {
                if (it == null || it.res < 0 || it.res >= ResInfo.Count) { Debug.LogWarning("Load: skipped an item of unknown good " + (it != null ? it.res : -1)); lossy++; continue; }
                if (!InMap(it.x, it.y) || tiles[it.x, it.y].belt < 0) { res[it.res] += it.amount; continue; }
                var dv = Dir(tiles[it.x, it.y].belt);
                var bi = SpawnItem((Res)it.res, it.amount, it.x, it.y, new Vector2(it.x + 0.5f - dv.x * 0.5f, it.y + 0.5f - dv.y * 0.5f));
                bi.p = it.p;
            }

            // sheep
            foreach (var ss in d.sheep ?? new List<SheepSave>())
            {
                if (ss == null) continue;
                if (!System.Enum.IsDefined(typeof(Variant), ss.variant) || !System.Enum.IsDefined(typeof(Expr), ss.baseExpr)) lossy++;
                var s = new Sheep
                {
                    id = ss.id, name = ss.name ?? "Sheep", male = ss.male, level = ss.level, xp = ss.xp, mood = ss.mood, hunger = ss.hunger, wool = ss.wool,
                    traits = ss.traits ?? new List<string>(), woolCol = ss.woolCol, faceCol = ss.faceCol,
                    variant = System.Enum.IsDefined(typeof(Variant), ss.variant) ? (Variant)ss.variant : Variant.None,
                    baseExpr = System.Enum.IsDefined(typeof(Expr), ss.baseExpr) ? (Expr)ss.baseExpr : Expr.Normal,
                    job = (Job)ss.job, ageDays = ss.ageDays, generation = ss.generation, parents = ss.parents, born = ss.born,
                    injured = ss.injured, flattened = ss.flattened, burned = ss.burned, muddy = ss.muddy, bloody = ss.bloody, sick = ss.sick, refuse = ss.refuse,
                    kills = ss.kills, milk = ss.milk, strain = Mathf.Clamp(ss.strain, 0, Genetics.Strains.Length - 1)
                };
                s.herdHand = ss.herdHand;
                if (ss.drove >= 1 && ss.drove < 1e15)
                    s.drove = new Drove { n = System.Math.Floor(ss.drove), wool = Mathf.Clamp(ss.droveWool, 0, 1.25f), milk = Mathf.Clamp01(ss.droveMilk), hunger = Mathf.Clamp(ss.droveHunger, 0, 4.5f) };
                else if (ss.drove != 0) lossy++;
                s.pos = new Vector2(ss.x, ss.y); s.target = s.pos;
                s.moodTarget = s.mood;
                s.statusTimer = (float)rng.NextDouble() * 5; s.baaTimer = 5 + (float)rng.NextDouble() * 20;
                if (ss.mods != null) foreach (var m in ss.mods) s.mods.Add(new MoodMod(m.label, m.value, m.until));
                if (!s.herdHand) usedNames.Add(s.name); // herd hands' names are not kept
                sheep.Add(s);
                SpawnSheepGO(s);
                s.job = Job.Grazer; s.workplace = null; // version 7: sheep never work, they are managed
                Building home;
                if (ss.penId != 0 && bmap.TryGetValue(ss.penId, out home) && IsPen(home)) s.pen = home;
                if (ss.hidden) s.act = Act.Hidden;
            }
            graveyard = d.graveyard ?? new List<DeadRecord>();
            int tombCount = Mathf.Min(d.tombs, 5000);
            if (tombCount < d.tombs) lossy++;
            for (int i = 0; i < tombCount; i++) AddTombstone();
            LoadFault("sheep");

            // economy & meta
            if (d.priceMod != null)
                for (int i = 0; i < priceMod.Length && i < d.priceMod.Length; i++)
                {
                    // only impossible prices are repaired: events push valid ones outside the daily walk's range
                    float v = d.priceMod[i];
                    if (v > 0f && v <= 100f) priceMod[i] = v; else { priceMod[i] = 1f; lossy++; }
                }
            if (d.autoSell != null) for (int i = 0; i < autoSell.Length && i < d.autoSell.Length; i++) autoSell[i] = d.autoSell[i];
            rpPool = d.rpPool; totalRP = d.totalRP; currentResearch = string.IsNullOrEmpty(d.currentResearch) ? null : d.currentResearch;
            if (currentResearch != null && Research.Get(currentResearch) == null) { currentResearch = null; lossy++; }
            rprog.Clear();
            if (d.rprogKeys != null && d.rprogVals != null)
                for (int i = 0; i < d.rprogKeys.Count && i < d.rprogVals.Count; i++) if (d.rprogKeys[i] != null) rprog[d.rprogKeys[i]] = d.rprogVals[i];
            globalMods.Clear();
            if (d.globalMods != null) foreach (var m in d.globalMods) if (m != null) globalMods.Add(new MoodMod(m.label, m.value, m.until));
            scheduled.Clear();
            if (d.scheduled != null) foreach (var sc in d.scheduled) if (sc != null) scheduled.Add(sc);
            if (d.hasContract && d.contractRes >= 0 && d.contractRes < ResInfo.Count) contract = new Contract { res = (Res)d.contractRes, amount = d.contractAmount, reward = d.contractReward, deadline = d.contractDeadline };
            else if (d.hasContract) lossy++;
            gameWon = d.gameWon;
            marketBeltSales = d.marketBeltSales; machineSheep = d.machineSheep; gateToggles = d.gateToggles; beltCoins = d.beltCoins;
            lotMeals = d.lotMeals; slaughtered = d.slaughtered; trafficT = d.trafficT; beltTierSel = Mathf.Clamp(d.beltTierSel, 0, MaxBeltTier);
            if (d.version < 5) { tutDone = true; milkCount = d.milkCount; }   // older saves skip the (newer) tutorial
            else
            {
                tutDone = d.tutDone; tutStep = d.tutStep; bubbleHarvests = d.bubbleHarvests; milkCount = d.milkCount;
                if (d.version < 6 && !tutDone && tutStep >= MachineDoorStep) tutStep++; // version 6 added the machine-door lesson
            }
            if (d.version < 6 && tutDone) machineDoorNews = true;               // the Supreme Leader announces machine doors
            soldAlive = d.soldAlive; foodEaten = d.foodEaten;
            dna = d.dna; dnaTotal = d.dnaTotal; splicedTotal = d.splicedTotal;
            genes.Clear(); if (d.genes != null) foreach (var gid in d.genes) genes.Add(gid);
            ApplyGenes();
            objectiveIndex = DecreeFromSave(d);
            gateCharge = d.gateCharge >= 0 ? d.gateCharge : GateCapBase; gateReleased = Mathf.Max(0, d.gateReleased); pathBought = Mathf.Max(0, d.pathBought);
            lifetimeEarned = d.lifetimeEarned > 0 ? d.lifetimeEarned : totalMade[0]; // older saves start from what they had earned
            dayIncome = System.Math.Max(0, d.dayIncome);
            incomeToday = d.incomeToday; // may be negative: a debt from sheep bought today survives the save (CleanNumbers took NaN)
            dayStartPop = d.dayStartPop >= 0 ? d.dayStartPop : -1; liveToday = System.Math.Max(0, d.liveToday);
            soldToday = System.Math.Max(0, d.soldToday); slaughteredToday = System.Math.Max(0, d.slaughteredToday);
            pathLevels.Clear();
            if (d.pathKeys != null && d.pathVals != null)
                for (int i = 0; i < d.pathKeys.Count && i < d.pathVals.Count; i++)
                {
                    var pd = Upgrades.Get(d.pathKeys[i]);
                    if (pd == null || d.pathVals[i] < 0) { lossy++; continue; } // a path this version doesn't know
                    if (d.pathVals[i] > 0) pathLevels[pd.id] = Mathf.Min(d.pathVals[i], pd.MaxLevel);
                }
            FindOfflineAward(d);
            if (d.version >= 10 && !tutDone && !string.IsNullOrEmpty(d.tutStepId))
            {
                if (steps == null) BuildTutorial();
                int ti = steps.FindIndex(st => st.id == d.tutStepId);
                if (ti >= 0) tutStep = ti; else lossy++;
            }
            if (d.mapW > 0 && (d.mapW != MapW || d.mapH != MapH)) { lossy++; Debug.LogWarning("Load: the save's map is " + d.mapW + "x" + d.mapH); }
            CountPens();
            foreach (var s in sheep) if (s.pen == null) AssignPen(s);
            ResetDayStats();
            if (flags.Contains("gerald")) SpawnGerald();
            nextId = Mathf.Max(nextId, d.nextId);

            BuildFence();
            RepaintGround(true);
            RefreshOffers();
            ComputeGlobals();
            pixelScale = d.pixelScale > 0 ? Mathf.Clamp(d.pixelScale, 1, 64) : pixelScale;
            camTarget = new Vector3(d.camX, d.camY, -10);
            ApplyCamera();
            cam.transform.position = camTarget;
            started = true;
            if (!tutDone) ResumeTutorial();
            Log("^gGame loaded.^w Welcome back to " + DateString() + ", Year " + Year + ".");
            LoadFault("end");
        }

        /// test builds can fail a load at a named point inside ApplySave (Tools/Harness); release builds compile the calls away
        [System.Diagnostics.Conditional("FLEECED_DEV")]
        static void LoadFault(string at)
        {
#if FLEECED_DEV
            if (DevHooks.LoadFault != null) DevHooks.LoadFault(at);
#endif
        }
    }
}
