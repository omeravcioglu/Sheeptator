using System.Collections.Generic;
using UnityEngine;

namespace Fleeced
{
    public enum Act { Wander, Graze, GoWork, Working, Summoned, BeingSheared, Launched, Dying, Ghost, Hidden, Building, Idle, Eat, ToGate, InMachine }

    public class MoodMod
    {
        public string label; public float value; public float until; // until = sim day
        public MoodMod(string l, float v, float u) { label = l; value = v; until = u; }
    }

    public class Sheep
    {
        public int id;
        public string name;
        public bool male;
        public int level = 1;
        public float xp;
        public float mood = 65;
        public float moodTarget = 65;
        public float hunger = 0.2f;
        public float wool = 0.6f;
        public float milk;           // ewes only: 0..1, 1 = full udder
        public Building eatAt;
        public List<string> traits = new List<string>();
        public Variant variant;
        public int woolCol, faceCol;
        public Expr baseExpr = Expr.Normal;
        public Job job = Job.Grazer;
        public Building workplace;
        public string status = "Grazing";
        public float ageDays = 5;
        public int generation = 1;
        public string parents = "Unknown";
        public float born;

        public float injured, flattened, burned, muddy, bloody, tremble, unconscious, refuse, sick;
        public bool dead;
        public string deathCause;
        public float deathDay;

        public List<MoodMod> mods = new List<MoodMod>();

        // sim/visual
        public Act act = Act.Wander;
        public Vector2 pos, target;
        public float actTimer, animT, walkPhase;
        public bool faceRight = true;
        public Building summonedBy;
        public float launchT, launchDur;
        public Vector2 launchFrom, launchTo;
        public float statusTimer;
        public float baaTimer;
        public float hiddenUntil;
        public bool homeless;
        public int kills; // guards
        public float workDone; // lifetime work
        public float ridingT, beltImmune;
        public float jamT; // seconds stuck behind another rider
        public bool onBelt;          // carried (or queueing) on a conveyor this tick
        public Building pen;         // the room it lives in right now: a Breeding Pen or a Feeding Lot (null = roams free)
        public int exitDoor = -1;    // the door it is heading for
        public Vector2 eatSpot; public bool hasEatSpot;

        public GameObject go;
        public SpriteRenderer sr;
        public SpriteRenderer shadow;

        public bool Has(string t) { return traits.Contains(t); }
        public bool Lamb { get { return ageDays < LambAge; } }
        public static float LambAge = 3f; // Quick Lambs (genetics) makes it 2
        public int strain;                // DNA-altered breed (Genetics.*), 0 = plain sheep
        public bool Alive { get { return !dead && act != Act.Hidden; } }
        public string He { get { return male ? "he" : "she"; } }
        public string Him { get { return male ? "him" : "her"; } }
        public string His { get { return male ? "his" : "her"; } }
        public string Gender { get { return male ? "Ram" : "Ewe"; } }
        public int WoolStage
        {
            get
            {
                if (wool < 0.12f) return 0;
                if (wool < 0.35f) return 1;
                if (wool < 0.65f) return 2;
                if (wool < 0.98f) return 3;
                return 4;
            }
        }
        public bool Busy
        {
            get { return act == Act.Summoned || act == Act.BeingSheared || act == Act.InMachine || act == Act.Launched || act == Act.Dying || act == Act.Ghost || act == Act.Hidden; }
        }
        public bool CanWork
        {
            get { return !dead && !Lamb && unconscious <= 0 && refuse <= 0 && sick <= 0 && act != Act.Hidden; }
        }
        public float XpToNext { get { return 30 + level * 25; } }
    }

    public class Building
    {
        public int id;
        public BuildingDef def;
        public int level = 1;
        public int x, y;            // tile of bottom-left
        public float construct = 1f; // 0..1, <1 = under construction
        public bool upgrading;
        public List<Sheep> workers = new List<Sheep>();
        public float progress;
        public int recipe;
        public bool unsafeMode;
        public float buffMult = 1f, buffUntil;
        public float safetyUntil;
        public bool damaged;
        public int accidents;
        public string status = "";
        public Sheep client;         // shed: being sheared
        public List<Sheep> clients = new List<Sheep>(); // plant
        public float clientTimer;
        public List<float> pairProgress = new List<float>();
        public float produced;
        public bool featureMode; // "call it a feature"
        public float lastCycleOut;
        public float accTimer;
        // rooms (Breeding Pens, Feeding Lots): one door per side, 0 bottom, 1 right, 2 top, 3 left
        public bool[] doorOpen = new bool[4];
        public int[] doorFilter = new int[4];
        public int[] doorPicks = new int[4];
        public float[] doorCd = new float[4];
        public int penCount, penReady, penOut, penMembers, penHungry, penFed;
        public float breedProgress;
        public GameObject fenceRoot;
        // conveyor machines (shearing / milking / wool plant / slaughterhouse): a door on every side a belt touches,
        // using doorOpen/doorFilter/doorPicks above. Until the player clicks a door it follows the belts (doorTouched).
        public bool[] doorTouched = new bool[4];
        public Sheep inMachine;
        public float machineT, machineDur;
        public int machineIn = -1, machineLane; // the side and lane the sheep came in by
        public bool machineDone, machineDid;    // processed yet / did it get anything out of the sheep
        public Vector2 machineOut;              // where the last sheep popped out
        public float doorWaitT;                 // sheep-seconds spent waiting at a shut door (for the Supreme Leader)
        public int geneMode;                    // Gene Lab: 0 = SAMPLE DNA, else the strain it splices in
        public float geneNoDnaT;                // Gene Lab: splices skipped for lack of DNA (for the Supreme Leader)
        public float refuseWaitT;               // sheep-seconds spent waiting at a building that takes no sheep (or is not built)
        public float noExitT;                   // seconds a processed sheep sat inside with no open way out
        public List<SpriteRenderer> doorSprites = new List<SpriteRenderer>();

        public GameObject go;
        public SpriteRenderer sr, scaffold;
        public BuildingSprite art;
        public List<GameObject> extras = new List<GameObject>();
        public float fxTimer;
        public int cyclesDone;

        public bool Built { get { return construct >= 1f && !upgrading; } }
        public int Slots { get { return BuildingDef.At(def.slots, level); } }
        public float LM { get { return BuildingDef.At(BuildingDef.LevelMult, level); } }
        public float[] inbuf = new float[ResInfo.Count];   // belt-delivered inputs
        public float beltFedT;                               // >0 = recently belt-fed (speed bonus)
        public Vector2 Center { get { return new Vector2(x + def.w / 2f, y + def.h / 2f); } }
        public Vector2 Door { get { return new Vector2(x + def.w / 2f, y - 0.35f); } }
        public float Danger
        {
            get
            {
                float d = BuildingDef.At(def.danger, level);
                if (unsafeMode) d += 0.03f;
                if (featureMode) d += 0.01f;
                return d;
            }
        }
        public string Title { get { return BuildingDef.At(def.levelNames, level); } }
        public Vector2 WorkSpot(int i)
        {
            int n = Mathf.Max(1, Slots);
            float span = def.w - 0.6f;
            float fx = n == 1 ? def.w / 2f : 0.3f + span * i / (n - 1);
            float fy = -0.25f - (i % 2) * 0.35f;
            if (def.kind == BKind.Wheel) return new Vector2(x + def.w / 2f + (i - (n - 1) / 2f) * 0.5f, y + 0.25f);
            if (def.kind == BKind.Farm) return new Vector2(x + 0.6f + (i % 3) * 0.9f, y + 0.5f + (i / 3) * 0.9f + (i % 2) * 0.4f);
            return new Vector2(x + fx, y + fy);
        }
    }

    public class Tile
    {
        public float grass = 1f;   // 0..1
        public float wear;         // foot traffic 0..1
        public float dead;         // pollution 0..1
        public bool path;
        public Building building;
        public Building pen;       // tile belongs to a sheep pen (grass, no buildings or belts)
        public int sig = -1;       // last painted signature
        public int belt = -1;      // conveyor direction 0 right,1 up,2 left,3 down; -1 none
        public int beltTier;       // 0 rubber, 1 steel, 2 mega
        public GameObject beltGo; public SpriteRenderer beltSr;
    }

    public class BeltItem
    {
        public Res res; public float amount;
        public int x, y; public float p;      // tile + progress across it
        public Vector2 from;                  // entry point (world)
        public float stuckT;                  // time spent unable to move on
        public GameObject go; public SpriteRenderer sr;
    }

    public class Decal
    {
        public GameObject go; public float born;
    }

    public class Wolf
    {
        public GameObject go; public SpriteRenderer sr;
        public Vector2 pos; public Sheep target; public int state; // 0 approach, 1 flee, 2 eat, 3 resident
        public float timer, anim; public bool gerald;
        public Vector2 wander;
    }

    [System.Serializable]
    public class DeadRecord
    {
        public string name, cause, traits; public int level; public float day; public float age;
    }

    public class Offer
    {
        public Sheep sheep; public int price;
    }

    [System.Serializable]
    public class Contract
    {
        public Res res; public int amount; public int reward; public float deadline;
    }
}
