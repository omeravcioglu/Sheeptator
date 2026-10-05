using System.Collections.Generic;
using UnityEngine;

namespace Fleeced
{
    public enum Res { Coins, Wool, CleanWool, Yarn, Fabric, Clothing, Luxury, Food, Milk, Cheese, Mutton, Silk, GoldFleece, Cream, GlowWool, ChocoMilk, CottonCandy }
    public enum Job { Grazer, Farmer, Shearer, Processor, Operator, Tailor, Merchant, Researcher, Breeder, Builder, Guard, Warehouse, Astronaut, Milker, Cheesemaker }
    public enum Cat { Graze, Farm, Wool, Industry, Research, Commerce, Breed, Build, Guard, Labor, Craft, Space }
    public enum Variant { None, Golden, Black, Rainbow, ThreeEyed, Spiky, TwoHeaded }
    public enum BKind { House, Farm, Shed, Market, Workshop, Barn, Lab, Storage, Textile, Wheel, Atelier, GuardPost, Trading, Bank, Plant, Spa, Statue, Graveyard, Rocket, CloneVat, Flowers, Sign, Funnel, Palace, Dairy, Creamery, Trough, Pen, FeedLot, Slaughter, GeneLab }

    public static class ResInfo
    {
        public const int Count = 17;
        public static readonly string[] Names = { "Coins", "Wool", "Clean Wool", "Yarn", "Fabric", "Clothing", "Luxury Goods", "Food", "Milk", "Cheese", "Mutton", "Silk", "Golden Fleece", "Cream", "Glow Wool", "Chocolate Milk", "Cotton Candy" };
        public static readonly float[] BasePrice = { 1, 3, 8, 18, 42, 100, 320, 1, 4, 22, 7, 14, 45, 12, 28, 18, 20 };
        public static readonly string[] Icon = { "coin", "wool", "cleanwool", "yarn", "fabric", "clothing", "luxury", "food", "milk", "cheese", "mutton", "silk", "goldfleece", "cream", "glowwool", "chocomilk", "cottoncandy" };
        /// goods that can be sold, in market display order (cheapest first)
        public static readonly Res[] Sellable = { Res.Wool, Res.Milk, Res.Mutton, Res.CleanWool, Res.Cream, Res.Silk, Res.Yarn, Res.ChocoMilk, Res.CottonCandy, Res.Cheese, Res.GlowWool, Res.Fabric, Res.GoldFleece, Res.Clothing, Res.Luxury };
        /// rare goods that only DNA-altered sheep make
        public static bool IsRare(Res r) { return r == Res.Silk || r == Res.GoldFleece || r == Res.Cream || r == Res.GlowWool; }
        public static readonly string[] Desc =
        {
            "Shiny. The sheep do not understand it, but they fear it.",
            "Raw wool. Smells like sheep, because it is sheep.",
            "Washed wool. Fewer bugs. Not zero bugs.",
            "Spun yarn. Cats are banned from the settlement.",
            "Woven fabric. Some of it is still warm.",
            "Sweaters, socks, and tiny hats. Humans love them.",
            "Designer wool couture. Costs more than the sheep who made it.",
            "Grass, hay and carrots. Every sheep eats ~4 per day.",
            "Sheep milk. Only ewes make it. Rams make excuses.",
            "Aged sheep cheese. Smells like a decision you will regret.",
            "Former sheep. Sells well. Do not mention it in front of the others.",
            "Silk from Silkfleece sheep. Smooth, shiny, about 5x the price of wool. Level 4 upgrades need it.",
            "Golden Fleece. Actual gold fibre, from very proud and very heavy sheep. Level 5 upgrades need it.",
            "Thick cream from Creamer ewes. Tastes like money. Level 4 upgrades need it.",
            "Glow Wool from Glowfleece sheep. Faintly radioactive. Lights up the market. Level 5 upgrades need it.",
            "Chocolate milk from Chocolate Sheep. Nobody can explain it. Everybody buys it.",
            "Cotton candy, shorn from Cotton Candy Sheep. Sticky. Sweet. Slightly woolly.",
        };
    }

    public class JobInfo
    {
        public Job job; public string name; public Cat cat; public string desc; public string[] statuses;
        public JobInfo(Job j, string n, Cat c, string d, params string[] s) { job = j; name = n; cat = c; desc = d; statuses = s; }
    }

    public static class Jobs
    {
        public static readonly JobInfo[] All =
        {
            new JobInfo(Job.Grazer, "Grazer", Cat.Graze, "Lives in a pen: grazes, makes lambs, grows wool and milk. The DOORS decide where it goes next.",
                "Chewing aggressively", "Staring at the door", "Staring at nothing", "Grazing", "Eating a weird flower", "Contemplating grass"),
            new JobInfo(Job.Farmer, "Farmer", Cat.Farm, "Works at a Farm producing Food.",
                "Planting carrots", "Yelling at crows", "Watering crops", "Eating the produce", "Digging a hole"),
            new JobInfo(Job.Shearer, "Shearer", Cat.Wool, "Retired. Shearing Machines on the conveyor do this now.",
                "Sharpening shears", "Shearing a friend", "Avoiding eye contact", "Collecting fluff", "Snipping"),
            new JobInfo(Job.Processor, "Wool Processor", Cat.Wool, "Works at a Wool Workshop turning Wool into Clean Wool and Yarn.",
                "Cleaning machine", "Washing wool", "Spinning yarn", "Picking out bugs", "Untangling"),
            new JobInfo(Job.Operator, "Machine Operator", Cat.Industry, "Runs factories, power wheels and the Wool Plant. Dangerous.",
                "Pulling levers", "Pressing red button", "Oiling gears", "Ignoring alarms", "Running very fast"),
            new JobInfo(Job.Tailor, "Tailor", Cat.Craft, "Works at the Luxury Atelier making Luxury Goods.",
                "Sewing tiny hats", "Judging fashion", "Measuring a cousin", "Gluing sequins"),
            new JobInfo(Job.Merchant, "Merchant", Cat.Commerce, "Works at the Market, Trading Office or Bank. Sells goods automatically.",
                "Haggling", "Overcharging tourists", "Counting coins badly", "Yelling prices"),
            new JobInfo(Job.Researcher, "Researcher", Cat.Research, "Works at a Research Lab producing Research Points.",
                "Doing science", "Mixing flasks", "Reading upside down", "Calculating grass", "Licking a battery"),
            new JobInfo(Job.Breeder, "Breeder", Cat.Breed, "Retired. Lambs are made in Breeding Pens now.",
                "Being romantic", "Awkward small talk", "Nesting", "Looking for love"),
            new JobInfo(Job.Builder, "Builder", Cat.Build, "Goes to construction sites and builds/repairs. No building needed.",
                "Hammering", "Hammering own hoof", "Carrying planks", "Building, allegedly", "Looking for nails"),
            new JobInfo(Job.Guard, "Guard", Cat.Guard, "Works at a Guard Post. Chases off wolves and suspicious men.",
                "On patrol", "Glaring at the hills", "Sleeping on duty", "Practicing headbutts"),
            new JobInfo(Job.Warehouse, "Warehouse Worker", Cat.Labor, "Works at Storage. Each worker adds +25% storage capacity there.",
                "Stacking crates", "Counting boxes", "Lost in aisle 4", "Sitting on a barrel"),
            new JobInfo(Job.Astronaut, "Astronaut", Cat.Space, "Sits in the rocket. Waits. Hopes.",
                "Training for space", "Wearing a fishbowl", "Spinning in a chair", "Saying goodbye"),
            new JobInfo(Job.Milker, "Milker", Cat.Farm, "Retired. Milking Machines on the conveyor do this now.",
                "Warming hooves", "Milking a friend", "Apologizing", "Carrying buckets", "Avoiding kicks"),
            new JobInfo(Job.Cheesemaker, "Cheesemaker", Cat.Craft, "Works at the Creamery turning Milk into Cheese.",
                "Stirring curds", "Smelling cheese", "Aging cheese", "Crying into the vat"),
        };
        public static JobInfo Get(Job j) { return All[(int)j]; }
    }

    public class TraitInfo
    {
        public string id, name, desc; public int kind; // 1 good, -1 bad, 0 weird
        public TraitInfo(string i, string n, int k, string d) { id = i; name = n; kind = k; desc = d; }
    }

    public static class Traits
    {
        public static readonly TraitInfo[] All =
        {
            new TraitInfo("lazy", "Lazy", -1, "Waddles 25% slower through the factory. Weirdly content (+5 mood)."),
            new TraitInfo("fast", "Speedy", 1, "Moves 30% faster. Through the line, out of the line, away from wolves."),
            new TraitInfo("glutton", "Glutton", -1, "Eats twice as much food. Makes a lot more mutton."),
            new TraitInfo("genius", "Genius", 1, "Has Ideas: now and then +12 RP for the Lab. Stares at the conveyor knowingly."),
            new TraitInfo("stupid", "Stupid", -1, "+50% machine accidents. Very, very happy."),
            new TraitInfo("accident", "Accident Prone", -1, "2.5x more likely to get hurt in a machine."),
            new TraitInfo("greedy", "Greedy", -1, "Steals coins now and then. Nobody knows where it keeps them."),
            new TraitInfo("lucky", "Lucky", 1, "Half the machine accidents. Never dies in one. Wolves miss."),
            new TraitInfo("coward", "Coward", -1, "Trembles through the machines (strangely, 20% fewer accidents). Wolves love it."),
            new TraitInfo("strong", "Strong", 1, "Built like a fridge: +2 mutton. Headbutts wolves."),
            new TraitInfo("messy", "Messy", -1, "Leaves mud everywhere. Dirty wool (-10%). More accidents."),
            new TraitInfo("fluffy", "Fluffy", 1, "Grows wool 60% faster."),
            new TraitInfo("carrots", "Loves Carrots", 0, "Very happy if a Carrot Farm exists."),
            new TraitInfo("charismatic", "Charismatic", 1, "Sells for 50% more alive. Everyone wants one."),
            new TraitInfo("brave", "Brave", 1, "Stands up to wolves. Doesn't fear machines (maybe should: +20% accidents)."),
            new TraitInfo("fertile", "Fertile", 1, "Breeds twice as fast."),
            new TraitInfo("sickly", "Sickly", -1, "Gets sick, grows wool slower, eats more."),
            new TraitInfo("workaholic", "Overachiever", 1, "Grows wool and milk 20% faster. Insufferable about it."),
            new TraitInfo("philosopher", "Philosopher", 0, "Occasionally has a Thought (+RP). Nobody understands it."),
            new TraitInfo("loyal", "Loyal", 1, "Never strikes or escapes, no matter what you do."),
            new TraitInfo("dramatic", "Dramatic", 0, "Mood swings twice as hard. Faints a lot."),
            new TraitInfo("tough", "Tough", 1, "Survives accidents that would kill others."),
            new TraitInfo("clone", "Clone", -1, "Grown in a vat. Grows wool 10% slower. Stares a lot."),
        };
        static Dictionary<string, TraitInfo> map;
        public static TraitInfo Get(string id)
        {
            if (map == null) { map = new Dictionary<string, TraitInfo>(); foreach (var t in All) map[t.id] = t; }
            TraitInfo r; return map.TryGetValue(id, out r) ? r : All[0];
        }
        // traits that can be rolled naturally
        public static readonly string[] Natural = { "lazy", "fast", "glutton", "genius", "stupid", "accident", "greedy", "lucky", "coward", "strong", "messy", "fluffy", "carrots", "charismatic", "brave", "fertile", "sickly", "workaholic", "philosopher", "loyal", "dramatic", "tough" };
        public static readonly string[][] Opposites = { new[] { "lazy", "fast" }, new[] { "lazy", "workaholic" }, new[] { "genius", "stupid" }, new[] { "coward", "brave" }, new[] { "lucky", "accident" }, new[] { "strong", "sickly" }, new[] { "tough", "sickly" } };
        public static bool Conflicts(List<string> have, string t)
        {
            if (have.Contains(t)) return true;
            foreach (var p in Opposites)
                if ((p[0] == t && have.Contains(p[1])) || (p[1] == t && have.Contains(p[0]))) return true;
            return false;
        }
    }

    public class BuildingDef
    {
        public BKind kind; public string name; public int w, h; public int[] cost; public int[] slots; public Job job = Job.Grazer;
        public string tech; public string[] levelTech = { null, null, null }; public float[] danger = { 0, 0, 0 };
        public float[] buildTime = { 20, 30, 45 }; public string desc; public string[] levelNames; public int maxLevel = 3;
        public float[] power = { 0, 0, 0 }; public bool decor; public int unique; // 1 = only one allowed
        public bool hidden; // never shown in the build menu
        public BuildingDef(BKind k, string n, int w, int h) { kind = k; name = n; this.w = w; this.h = h; }
        /// standard production multiplier per level
        public static readonly float[] LevelMult = { 1f, 1.6f, 2.5f, 4f, 6f };
        public static T At<T>(T[] arr, int level) { return arr[Mathf.Clamp(level - 1, 0, arr.Length - 1)]; }
        public bool Accepts(Res r)
        {
            switch (kind)
            {
                case BKind.Workshop: return r == Res.Wool || r == Res.CleanWool;
                case BKind.Textile: return r == Res.Yarn || r == Res.Fabric;
                case BKind.Atelier: return r == Res.Clothing || r == Res.Wool;
                case BKind.Creamery: return r == Res.Milk;
                case BKind.Trough: return r == Res.Food;
                case BKind.Market: return r != Res.Food && r != Res.Coins;
                case BKind.Storage: return r != Res.Coins;
                case BKind.House: return r == Res.Food;
            }
            return false;
        }
    }

    public static class Buildings
    {
        public static readonly List<BuildingDef> All = new List<BuildingDef>();
        static Dictionary<BKind, BuildingDef> map = new Dictionary<BKind, BuildingDef>();
        public static BuildingDef Get(BKind k) { return map[k]; }

        static BuildingDef Add(BuildingDef d) { All.Add(d); map[d.kind] = d; return d; }

        static Buildings()
        {
            Add(new BuildingDef(BKind.House, "Sheep House", 2, 2) { hidden = true, cost = new[] { 45, 110, 380 }, slots = new[] { 0, 0, 0 }, levelTech = new[] { null, "bunk_beds", "apartments" }, buildTime = new float[] { 15, 25, 40 }, levelNames = new[] { "Crooked Shack", "Cottage", "Sheep Apartments" }, desc = "Old sheep housing. The flock lives in Breeding Pens now." });
            Add(new BuildingDef(BKind.Farm, "Farm", 3, 3) { cost = new[] { 40, 140, 420 }, slots = new[] { 2, 3, 4 }, job = Job.Farmer, levelTech = new[] { null, "carrots", "hay" }, danger = new[] { 0.002f, 0.004f, 0.02f }, buildTime = new float[] { 15, 25, 40 }, levelNames = new[] { "Grass Patch", "Carrot Farm", "Industrial Hay Field" }, desc = "Grows FOOD by itself for the Feeding Lots. A Lv1 farm feeds about 12 sheep. Upgrade it or build more as the flock grows.", power = new float[] { 0, 0, 4 } });
            Add(new BuildingDef(BKind.Shed, "Shearing Machine", 2, 2) { cost = new[] { 55, 170, 550 }, slots = new[] { 0, 0, 0 }, levelTech = new[] { null, null, "auto_shear" }, danger = new[] { 0.004f, 0.008f, 0.03f }, levelNames = new[] { "Shear Box", "Shearing Machine", "Automated Shear-o-Matic" }, desc = "Put it ON a conveyor. Sheep that ride in come out the other side shaved. Wool leaves on any belt pointing away from its side (or goes to storage).", power = new float[] { 0, 0, 5 } });
            Add(new BuildingDef(BKind.Market, "Market", 3, 2) { cost = new[] { 50, 190, 500 }, slots = new[] { 1, 2, 3 }, job = Job.Merchant, levelTech = new[] { null, "haggling", "trading" }, danger = new[] { 0.001f, 0.002f, 0.004f }, levelNames = new[] { "Roadside Stall", "Market", "Wool Bazaar" }, desc = "Everything that rides a conveyor in through its DOORS is SOLD (+10%): wool, milk, mutton, cheese... and live sheep. It also sells stored goods you mark AUTO. Build as many as your belts need." });
            Add(new BuildingDef(BKind.Workshop, "Wool Workshop", 3, 2) { cost = new[] { 110, 280, 750 }, slots = new[] { 2, 3, 4 }, job = Job.Processor, tech = "wool_cleaning", levelTech = new[] { null, "yarn", "mass_production" }, danger = new[] { 0.004f, 0.012f, 0.025f }, levelNames = new[] { "Wash Shack", "Spinning Workshop", "Wool Processing Works" }, desc = "Belt WOOL into it: it washes it into Clean Wool, then spins Yarn. Runs by itself.", power = new float[] { 0, 0, 6 } });
            Add(new BuildingDef(BKind.Barn, "Breeding Barn", 3, 2) { hidden = true, cost = new[] { 90, 240, 500 }, slots = new[] { 2, 4, 6 }, job = Job.Breeder, levelTech = new[] { null, "matchmaking", "selective" }, danger = new[] { 0, 0, 0.003f }, levelNames = new[] { "Romantic Barn", "Love Barn", "Breeding Facility" }, desc = "Pairs of ewes and rams make lambs. Lambs inherit traits." });
            Add(new BuildingDef(BKind.Lab, "Research Lab", 2, 2) { cost = new[] { 130, 330, 850 }, slots = new[] { 2, 3, 4 }, job = Job.Researcher, levelTech = new[] { null, "machinery", "mass_production" }, danger = new[] { 0.005f, 0.01f, 0.02f }, levelNames = new[] { "Science Tent", "Laboratory", "Tesla Tower" }, desc = "Makes Research Points by itself. Pick a project in the RESEARCH menu. Explosions are part of science.", power = new float[] { 0, 0, 5 } });
            Add(new BuildingDef(BKind.Storage, "Storage Shed", 2, 2) { cost = new[] { 45, 150, 400 }, slots = new[] { 0, 1, 2 }, job = Job.Warehouse, levelTech = new[] { null, null, "trading" }, danger = new[] { 0, 0.002f, 0.006f }, levelNames = new[] { "Storage Shed", "Storage Barn", "Warehouse" }, desc = "+150 / +350 / +700 storage for every good." });
            Add(new BuildingDef(BKind.Textile, "Textile Factory", 3, 3) { cost = new[] { 380, 850, 1900 }, slots = new[] { 2, 4, 6 }, job = Job.Operator, tech = "machinery", levelTech = new[] { null, "loom", "mass_production" }, danger = new[] { 0.012f, 0.02f, 0.035f }, buildTime = new float[] { 30, 40, 60 }, levelNames = new[] { "Loom Shed", "Textile Factory", "Textile Megafactory" }, desc = "Belt YARN into it: it weaves Fabric, then sews Clothing. Runs by itself.", power = new float[] { 0, 4, 10 } });
            Add(new BuildingDef(BKind.Wheel, "Power Wheel", 2, 2) { cost = new[] { 160, 360, 700 }, slots = new[] { 1, 2, 3 }, job = Job.Operator, tech = "wheel_power", danger = new[] { 0.01f, 0.015f, 0.025f }, levelNames = new[] { "Hamster Wheel", "Big Wheel", "Wheel of Industry" }, desc = "A wheel that turns by itself. Nobody asks how. Makes Power for Lv3 buildings and the Wool Plant." });
            Add(new BuildingDef(BKind.Atelier, "Luxury Atelier", 3, 2) { cost = new[] { 600, 1200, 2600 }, slots = new[] { 1, 2, 3 }, job = Job.Tailor, tech = "branding", levelTech = new[] { null, "baalenciaga", "baalenciaga" }, danger = new[] { 0.003f, 0.005f, 0.008f }, levelNames = new[] { "Boutique", "Atelier", "House of Baa-lenciaga" }, desc = "2 Clothing + 4 Wool -> 1 Luxury Goods. Pure profit." });
            Add(new BuildingDef(BKind.GuardPost, "Guard Post", 2, 2) { cost = new[] { 60, 160, 400 }, slots = new[] { 1, 2, 3 }, job = Job.Guard, danger = new[] { 0.002f, 0.003f, 0.004f }, levelNames = new[] { "Lookout", "Watchtower", "Wool Fortress" }, desc = "Scares off wolves. Higher levels scare harder." });
            Add(new BuildingDef(BKind.Trading, "Trading Office", 2, 2) { cost = new[] { 450, 900, 1800 }, slots = new[] { 1, 2, 3 }, job = Job.Merchant, tech = "trading", danger = new[] { 0.001f, 0.001f, 0.002f }, levelNames = new[] { "Office", "Trading Floor", "Wool Exchange" }, desc = "Offers big delivery contracts in the Market panel. Higher levels pay more.", unique = 1 });
            Add(new BuildingDef(BKind.Bank, "Bank", 3, 2) { cost = new[] { 900, 2000, 4000 }, slots = new[] { 1, 2, 3 }, job = Job.Merchant, tech = "stock", levelNames = new[] { "Piggy Bank", "Bank", "Sheep Street Bank" }, desc = "Pays daily interest on your coins. Sheep don't understand it.", unique = 1 });
            Add(new BuildingDef(BKind.Plant, "Industrial Wool Plant", 4, 3) { cost = new[] { 2500, 4000, 7000 }, slots = new[] { 0, 0, 0 }, tech = "wool_plant", danger = new[] { 0.035f, 0.045f, 0.06f }, buildTime = new float[] { 60, 60, 80 }, levelNames = new[] { "Wool Plant", "Wool Plant Mk II", "THE MACHINE" }, desc = "A gigantic conveyor machine. Shears every sheep that rides through at 2.5x yield, very fast. In goes fluff. Out comes... mostly sheep.", power = new float[] { 12, 20, 30 }, unique = 1 });
            Add(new BuildingDef(BKind.Spa, "Mud Spa", 2, 2) { cost = new[] { 150, 300, 600 }, slots = new[] { 0, 0, 0 }, tech = "mud_spa", decor = true, levelNames = new[] { "Mud Puddle", "Mud Spa", "Luxury Mud Resort" }, desc = "+4 / +7 / +10 mood for the whole flock." });
            Add(new BuildingDef(BKind.Statue, "Statue", 1, 2) { cost = new[] { 300, 600, 1200 }, slots = new[] { 0, 0, 0 }, tech = "statue", decor = true, levelNames = new[] { "Stone Statue", "Golden Statue", "Animatronic Statue" }, desc = "A statue of your first sheep. +6 / +9 / +12 mood.", unique = 1 });
            Add(new BuildingDef(BKind.Graveyard, "Graveyard", 3, 2) { cost = new[] { 40, 120, 300 }, slots = new[] { 0, 0, 0 }, decor = true, levelNames = new[] { "Burial Patch", "Graveyard", "Memorial Gardens" }, desc = "Shortens mourning. Fills up with tombstones over time. +0 / +2 / +4 mood.", unique = 1 });
            Add(new BuildingDef(BKind.Rocket, "Rocket Pad", 3, 4) { cost = new[] { 6000, 0, 0 }, slots = new[] { 1, 1, 1 }, job = Job.Astronaut, tech = "space_program", maxLevel = 1, danger = new[] { 0.002f, 0, 0 }, buildTime = new float[] { 90, 1, 1 }, levelNames = new[] { "Sheep Space Program", "", "" }, desc = "Press LAUNCH: the sheep you selected (or a random volunteer) goes to the moon. The moon might contain grass.", power = new float[] { 20, 0, 0 }, unique = 1 });
            Add(new BuildingDef(BKind.CloneVat, "Cloning Vat", 2, 2) { cost = new[] { 700, 0, 0 }, slots = new[] { 0, 0, 0 }, tech = "cloning", maxLevel = 1, levelNames = new[] { "Cloning Vat", "", "" }, desc = "Select a sheep and press CLONE. The copy is mostly the same sheep.", unique = 1 });
            Add(new BuildingDef(BKind.Flowers, "Flower Patch", 1, 1) { cost = new[] { 15, 0, 0 }, slots = new[] { 0, 0, 0 }, maxLevel = 1, decor = true, buildTime = new float[] { 4, 1, 1 }, levelNames = new[] { "Flower Patch", "", "" }, desc = "+1 mood (max +10 from flowers). The sheep will eat it eventually." });
            Add(new BuildingDef(BKind.Sign, "Safety Sign", 1, 1) { cost = new[] { 10, 0, 0 }, slots = new[] { 0, 0, 0 }, maxLevel = 1, decor = true, buildTime = new float[] { 3, 1, 1 }, levelNames = new[] { "Safety Sign", "", "" }, desc = "-10% accidents in buildings within 3 tiles. Mostly decorative." });

            Add(new BuildingDef(BKind.Funnel, "Fluff Funnel", 2, 2) { hidden = true, cost = new[] { 120, 300, 700 }, slots = new[] { 0, 0, 0 }, tech = "conveyors", levelTech = new[] { null, "express_belts", "mass_production" }, danger = new[] { 0.004f, 0.008f, 0.02f }, levelNames = new[] { "Fluff Funnel", "Fluff Hopper", "Sheep Cannon" }, desc = "Grabs fluffy Grazers and drops them onto an adjacent conveyor belt pointing away from it. Point that belt at a Shearing Shed. You monster." });

            Add(new BuildingDef(BKind.Palace, "Supreme Palace", 3, 3) { cost = new[] { 0, 0, 0 }, slots = new[] { 0, 0, 0 }, maxLevel = 1, hidden = true, unique = 1, levelNames = new[] { "Supreme Palace", "", "" }, desc = "Home of the Supreme Leader. Do not knock." });
            Add(new BuildingDef(BKind.Dairy, "Milking Machine", 2, 2) { cost = new[] { 60, 180, 560 }, slots = new[] { 0, 0, 0 }, levelTech = new[] { null, null, "mass_production" }, danger = new[] { 0.003f, 0.006f, 0.025f }, levelNames = new[] { "Milking Box", "Milking Machine", "Udder-Matic 3000" }, desc = "Put it ON a conveyor. Ewes that ride in come out milked. Rams come out insulted. Milk leaves on a belt pointing away from its side (or goes to storage).", power = new float[] { 0, 0, 5 } });
            Add(new BuildingDef(BKind.Creamery, "Creamery", 3, 2) { cost = new[] { 140, 320, 800 }, slots = new[] { 2, 3, 4 }, job = Job.Cheesemaker, tech = "cheese", levelTech = new[] { null, "udder_science", "mass_production" }, danger = new[] { 0.003f, 0.008f, 0.02f }, levelNames = new[] { "Cheese Shack", "Creamery", "Cheese Refinery" }, desc = "Belt MILK into it: 3 Milk -> 1 Cheese. Cheese sells for a lot more. Runs by itself.", power = new float[] { 0, 0, 6 } });
            Add(new BuildingDef(BKind.Trough, "Feeding Trough", 2, 1) { hidden = true, cost = new[] { 20, 0, 0 }, slots = new[] { 0, 0, 0 }, maxLevel = 1, decor = false, buildTime = new float[] { 6, 1, 1 }, levelNames = new[] { "Feeding Trough", "", "" }, desc = "Hungry sheep walk here to eat from the food store. Put one INSIDE every pen." });
            Add(new BuildingDef(BKind.Pen, "Breeding Pen", 8, 5) { cost = new[] { 40, 150, 400, 1000, 2400 }, slots = new[] { 0, 0, 0, 0, 0 }, maxLevel = 5, levelTech = new[] { null, null, null, "hyper_industry", "wool_singularity" }, danger = new[] { 0f, 0, 0, 0, 0 }, power = new float[] { 0, 0, 0, 0, 0 }, buildTime = new float[] { 1, 8, 12, 20, 30 }, levelNames = new[] { "Breeding Pen", "Big Breeding Pen", "Breeding Ranch", "Breeding Complex", "Lamb Megadome" }, desc = "Home of the flock. Fed, happy rams and ewes make LAMBS here, and they nibble the grass. A DOOR on each side: open a door with a conveyor under it and the sheep ride away. Holds 16 / 24 / 34 / 46 / 60 and breeds faster every level. The flock can never outgrow its pens." });
            Add(new BuildingDef(BKind.FeedLot, "Feeding Lot", 6, 4) { cost = new[] { 50, 160, 420, 1100, 2600 }, slots = new[] { 0, 0, 0, 0, 0 }, maxLevel = 5, levelTech = new[] { null, null, null, "hyper_industry", "wool_singularity" }, danger = new[] { 0f, 0, 0, 0, 0 }, power = new float[] { 0, 0, 0, 0, 0 }, buildTime = new float[] { 1, 8, 12, 20, 30 }, levelNames = new[] { "Feeding Lot", "Big Feeding Lot", "Feeding Hall", "Feeding Factory", "Hunger Annihilator" }, desc = "Sheep that ride in eat from the food store until they are FULL. Only fed sheep leave, through its open DOORS: to the machines, home, or... the other door. Holds 8 / 14 / 22 / 32 / 48 and eats faster each level." });
            Add(new BuildingDef(BKind.GeneLab, "Gene Lab", 2, 2) { cost = new[] { 160, 380, 900 }, slots = new[] { 0, 0, 0 }, tech = "genetics", levelTech = new[] { null, null, "mass_production" }, danger = new[] { 0.002f, 0.004f, 0.008f }, levelNames = new[] { "Gene Shed", "Gene Lab", "DNA Cathedral" }, desc = "Put it ON a conveyor. SAMPLE mode: every sheep that rides through gives 1 DNA. SPLICE mode: sheep come out DNA-altered (13 strains, some of them secret). CHAOS mode: 1 DNA and anything can happen. Better levels give more DNA and splice for less.", power = new float[] { 0, 0, 5 } });
            Add(new BuildingDef(BKind.Slaughter, "Slaughterhouse", 2, 2) { cost = new[] { 90, 260, 700 }, slots = new[] { 0, 0, 0 }, levelTech = new[] { null, null, "mass_production" }, levelNames = new[] { "Chopping Block", "Slaughterhouse", "Mutton Factory" }, desc = "The door nobody comes back from. Put it ON a conveyor: sheep that ride in become MUTTON, which leaves on a belt pointing away (or goes to storage). The flock notices.", power = new float[] { 0, 0, 4 } });

            // Levels 4 & 5: increasingly unhinged industry
            X(BKind.House, 900, 2500, 0, 0, 0, 0, 4, 8, "Sheep Tower", "Wool Arcology");
            X(BKind.Farm, 1200, 3500, 5, 6, 0.03f, 0.045f, 8, 14, "Hydroponic Grass Factory", "Grass Reactor");
            X(BKind.Shed, 1500, 4000, 0, 0, 0.045f, 0.06f, 10, 18, "Shear-o-Tron 9000", "The Fleecinator");
            X(BKind.Market, 1500, 4000, 4, 5, 0.006f, 0.01f, 4, 8, "Wool Mall", "Wool Stock Exchange");
            X(BKind.Workshop, 2000, 5000, 5, 6, 0.035f, 0.05f, 12, 20, "Hyperspinner Complex", "The Yarn Singularity");
            X(BKind.Barn, 1200, 3000, 8, 10, 0.006f, 0.01f, 6, 12, "Lamb Factory", "Infinite Lamb Engine");
            X(BKind.Lab, 2000, 5000, 5, 6, 0.03f, 0.045f, 10, 18, "Mad Science Tower", "Sheep Brain Nexus");
            X(BKind.Storage, 1000, 2500, 3, 4, 0.008f, 0.01f, 2, 4, "Mega Warehouse", "Wool Vault");
            X(BKind.Textile, 4000, 9000, 8, 10, 0.05f, 0.07f, 18, 28, "Textile Megaplex", "Sweater Singularity");
            X(BKind.Wheel, 1400, 3500, 4, 5, 0.035f, 0.05f, 0, 0, "Twin Wheel Reactor", "Wheel of Eternity");
            X(BKind.Atelier, 5000, 10000, 4, 5, 0.012f, 0.02f, 8, 14, "Couture Megastore", "Baa-lenciaga Prime");
            X(BKind.GuardPost, 900, 2000, 4, 5, 0.006f, 0.01f, 6, 10, "Laser Sheep Bunker", "Wolf Defense Grid");
            X(BKind.Plant, 12000, 25000, 0, 0, 0.07f, 0.09f, 45, 70, "THE MACHINE MK IV", "WOOL SINGULARITY");
            X(BKind.Slaughter, 1500, 4000, 0, 0, 0f, 0f, 8, 14, "Mutton Megaworks", "The Lamb Chopper");
            X(BKind.Dairy, 1500, 4000, 0, 0, 0.04f, 0.055f, 10, 18, "Lactation Station", "The Milk Reactor");
            X(BKind.Creamery, 2000, 5000, 5, 6, 0.03f, 0.045f, 12, 20, "Cheese Megavault", "Cheese Singularity");
        }

        static void X(BKind k, int c4, int c5, int s4, int s5, float d4, float d5, float p4, float p5, string n4, string n5)
        {
            var d = map[k];
            d.maxLevel = 5;
            d.cost = new[] { d.cost[0], d.cost[1], d.cost[2], c4, c5 };
            d.slots = new[] { d.slots[0], d.slots[1], d.slots[2], s4, s5 };
            d.danger = new[] { d.danger[0], d.danger[1], d.danger[2], d4, d5 };
            d.power = new[] { d.power[0], d.power[1], d.power[2], p4, p5 };
            d.levelNames = new[] { d.levelNames[0], d.levelNames[1], d.levelNames[2], n4, n5 };
            d.levelTech = new[] { d.levelTech[0], d.levelTech[1], d.levelTech[2], "hyper_industry", "wool_singularity" };
            d.buildTime = new[] { d.buildTime[0], d.buildTime[1], d.buildTime[2], 60f, 80f };
        }
    }

    public class ResearchDef
    {
        public string id, name, cat, desc; public int rp; public string[] pre;
        public ResearchDef(string i, string n, string c, int r, string d, params string[] p) { id = i; name = n; cat = c; rp = r; desc = d; pre = p; }
    }

    public static class Research
    {
        public static readonly string[] Cats = { "Farming", "Wool", "Industry", "Commerce", "Breeding", "Housing", "Space" };
        public static readonly ResearchDef[] All =
        {
            new ResearchDef("better_grass", "Better Grass", "Farming", 15, "Grass regrows 60% faster. Science!"),
            new ResearchDef("carrots", "Carrot Cultivation", "Farming", 40, "Unlocks Farm Lv2. Carrots make sheep weirdly happy.", "better_grass"),
            new ResearchDef("fertilizer", "Suspicious Fertilizer", "Farming", 90, "+30% farm output. Do not ask what is in it.", "carrots"),
            new ResearchDef("cheese", "Cheese Making", "Farming", 30, "Unlocks the Creamery: 3 Milk -> 1 Cheese. The smell is part of the process."),
            new ResearchDef("udder_science", "Udder Science", "Farming", 60, "+25% milk per milking. Unlocks Creamery Lv2. Do not ask.", "cheese"),
            new ResearchDef("fluff_detector", "Smart Doors", "Farming", 25, "Every door gets a FILTER: only HUNGRY, FED, READY, FLUFFY, MILKY sheep, RAMS or EWES go through. Leave the doors open and the factory runs itself."),
            new ResearchDef("hay", "Industrial Hay", "Farming", 160, "Unlocks Farm Lv3 with a combine harvester. Steering is optional.", "fertilizer", "machinery"),

            new ResearchDef("improved_shears", "Improved Shears", "Wool", 15, "+25% wool per shear. The sheep look cold."),
            new ResearchDef("wool_cleaning", "Wool Cleaning", "Wool", 25, "Unlocks the Wool Workshop. Clean wool sells for more."),
            new ResearchDef("yarn", "Yarn Spinning", "Wool", 60, "Workshops can spin Yarn. Unlocks Workshop Lv2.", "wool_cleaning"),
            new ResearchDef("auto_shear", "Automated Shearing", "Wool", 150, "Unlocks Shearing Machine Lv3. Faster. Hungrier.", "improved_shears", "machinery"),
            new ResearchDef("wool_plant", "Industrial Wool Plant", "Wool", 320, "Unlocks the Industrial Wool Plant. In goes fluff.", "auto_shear", "mass_production"),

            new ResearchDef("conveyors", "Greased Belts", "Industry", 20, "Every conveyor runs 30% faster. Sheep reach the machines sooner. They scream sooner, too."),
            new ResearchDef("mega_belts", "Mega Belts", "Industry", 220, "Unlocks MEGA BELTS: very fast, extra wide, and they carry sheep packed tight. Lay them, or UPGRADE ALL BELTS in the Build menu.", "express_belts"),
            new ResearchDef("machinery", "Basic Machinery", "Industry", 70, "Unlocks the Textile Factory, Lab Lv2. Gears!", "yarn"),
            new ResearchDef("express_belts", "Steel Belts", "Industry", 60, "Unlocks STEEL BELTS: 80% faster, and they carry sheep closer together. Lay them, or UPGRADE ALL BELTS in the Build menu.", "conveyors"),
            new ResearchDef("loom", "Industrial Loom", "Industry", 130, "Factories can sew Clothing. Unlocks Factory Lv2.", "machinery"),
            new ResearchDef("wheel_power", "Sheep Wheel Power", "Industry", 100, "Unlocks the Power Wheel. Sheep run. Lights turn on.", "machinery"),
            new ResearchDef("mass_production", "Mass Production", "Industry", 260, "Unlocks Lv3 Workshops, Factories and Labs. +15% output everywhere.", "loom", "wheel_power"),
            new ResearchDef("automation", "Automation", "Industry", 450, "Every production building works 25% faster. Nobody misses the workers. There never were any.", "mass_production"),
            new ResearchDef("hyper_industry", "Hyper-Industrialization", "Industry", 600, "Unlocks Level 4 for most buildings. Skyscrapers. Neon. Regret.", "mass_production", "express_belts"),
            new ResearchDef("wool_singularity", "The Wool Singularity", "Industry", 1000, "Unlocks Level 5. Buildings achieve a level of wool beyond comprehension.", "hyper_industry", "wool_plant"),

            new ResearchDef("haggling", "Aggressive Haggling", "Commerce", 30, "+10% sell prices. Unlocks Market Lv2."),
            new ResearchDef("branding", "Wool Branding", "Commerce", 110, "Unlocks the Luxury Atelier.", "haggling", "loom"),
            new ResearchDef("trading", "Trading Office", "Commerce", 150, "Unlocks the Trading Office, Market Lv3 and Warehouses.", "haggling"),
            new ResearchDef("stock", "Stock Market", "Commerce", 260, "Unlocks the Bank. Money makes money.", "trading"),
            new ResearchDef("baalenciaga", "Baa-lenciaga", "Commerce", 380, "Luxury Goods sell for +50%. Unlocks Atelier upgrades.", "branding", "stock"),

            new ResearchDef("matchmaking", "Matchmaking", "Breeding", 35, "Every Breeding Pen makes lambs 50% faster."),
            new ResearchDef("selective", "Selective Breeding", "Breeding", 100, "Good traits inherit more, bad ones less.", "matchmaking"),
            new ResearchDef("advanced_breeding", "Advanced Breeding", "Breeding", 200, "Rare sheep variants are 3x more likely.", "selective"),
            new ResearchDef("genetics", "Sheep Genetics", "Breeding", 40, "Unlocks the GENE LAB and the GENETICS menu (N). Sheep give DNA. DNA rewrites sheep: Silk, Cream, Jumbo, Gold... and worse."),
            new ResearchDef("cloning", "Sheep Cloning", "Breeding", 340, "Unlocks the Cloning Vat. Ethically vat-shaped.", "advanced_breeding", "machinery"),

            new ResearchDef("bunk_beds", "Bunk Beds", "Housing", 25, "Every Breeding Pen holds 4 more sheep."),
            new ResearchDef("mud_spa", "Mud Spa", "Housing", 60, "Unlocks the Mud Spa (+mood).", "bunk_beds"),
            new ResearchDef("statue", "Monumental Vanity", "Housing", 120, "Unlocks a Statue of your first sheep.", "mud_spa"),
            new ResearchDef("apartments", "Sheep Apartments", "Housing", 170, "Every Breeding Pen holds 8 more sheep. Stack them high.", "bunk_beds", "machinery"),

            new ResearchDef("moon_grass", "Moon Grass Hypothesis", "Space", 500, "The moon might contain grass. Someone has to check.", "wool_plant", "stock"),
            new ResearchDef("space_program", "Sheep Space Program", "Space", 800, "Unlocks the Rocket Pad. What could go wrong.", "moon_grass"),
        };
        static Dictionary<string, ResearchDef> map;
        public static ResearchDef Get(string id)
        {
            if (map == null) { map = new Dictionary<string, ResearchDef>(); foreach (var r in All) map[r.id] = r; }
            ResearchDef d; return map.TryGetValue(id, out d) ? d : null;
        }
    }

    public static class Names
    {
        public static readonly string[] First =
        {
            "Gurgle", "Bartholomew", "Woolfgang", "Baarbara", "Lambert", "Fluffernutter", "Dolly", "Mutton", "Chop", "Kevin",
            "Gary", "Doug", "Brenda", "Clump", "Lint", "Fleecy", "Ewegene", "Ramsey", "Baab", "Sheryl", "Tufty", "Wooliam",
            "Lambo", "Cotton", "Nugget", "Clover", "Pudding", "Sgt. Fluff", "Noodle", "Biscuit", "Gristle", "Scrumpy",
            "Mildew", "Wobbles", "Grimble", "Spud", "Haggis", "Boris", "Moss", "Crumpet", "Dingus", "Pickle", "Muffin",
            "Wendell", "Gavin", "Ramona", "Baarnaby", "Fuzz", "Woolma", "Meryl", "Lamb Chop", "Shirley", "Blorp", "Steve",
            "Tim", "Margaret", "Professor", "Bleat", "Cumulus", "Fabio", "Lord Fluff", "Wooly", "Dave", "Socks", "Chunk",
            "Esmeralda", "Gerty", "Hamish", "Ignatius", "Jumper", "Knitting", "Lumpy", "Mabel", "Nigel", "Ovine", "Pippa",
            "Quilt", "Rufus", "Squish", "Thimble", "Udder", "Velma", "Wiggles", "Yarnold", "Zeb", "Bobbin", "Crouton",
            "Doughnut", "Ewan", "Felt", "Gobbo", "Hoof", "Itchy", "Jeff", "Kale", "Lanolin", "Moop", "Nubbin", "Oatmeal",
        };
        public static string Random(System.Random rng, HashSet<string> used)
        {
            for (int i = 0; i < 40; i++)
            {
                var n = First[rng.Next(First.Length)];
                if (!used.Contains(n)) return n;
            }
            string[] suff = { " Jr.", " II", " III", " the Lesser", " Again", " 2" };
            return First[rng.Next(First.Length)] + suff[rng.Next(suff.Length)];
        }
    }
}
