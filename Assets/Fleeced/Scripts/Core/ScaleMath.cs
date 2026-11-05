namespace Fleeced
{
    /// The numbers and formulas of M12 (flocks at scale), kept free of UnityEngine so Tools/unit.sh can test them on plain .NET:
    /// the gate's stages and lanes, machine passes, the breeding chambers' staggered timers and shares, litters, the burst shake
    /// and drove cluster sizes. At stage 1 and level 0 every multiplier here is exactly 1.
    public static class ScaleMath
    {
        /// the flock that lets the gate grow from stage s to s + 1, by the current stage s (index 0 unused)
        public static readonly double[] GateStageFlock = { 0, 500, 5000, 50000, 300000 };
        /// the litter of each lane, by stage - 1 (stage s has s lanes)
        public static readonly int[] LaneLitter = { 1, 1, 2, 4, 10 };
        /// every 6th held release in a row is a STAMPEDE
        public const int StampedeEvery = 6;
        /// sheep a machine takes in one pass, by machine level - 1; the Wool Plant takes PlantBatchFactor times as many
        public static readonly double[] BatchByLevel = { 250, 1000, 4000, 16000, 64000 };
        public const double PlantBatchFactor = 2;
        /// a drove never takes more passes than this
        public const double MaxPasses = 30;
        /// a breeding chamber's cycle in game seconds (a quarter of a day), and the most chambers a pen holds
        public const double ChamberCycle = 6;
        public const int MaxChambers = 8;
        /// chamber k starts BitRev8[k] eighths of a cycle in, so the first 2, 4 or 8 chambers are evenly spread
        public static readonly int[] BitRev8 = { 0, 4, 2, 6, 1, 5, 3, 7 };
        /// lambs a couple a day inside a chamber, before the multipliers
        public const double CoupleRate = 0.45;
        /// visible agents at most (named sheep, ghosts and runners), runners at most, and how many of those the gate may hold
        public const int AgentCap = 400, RunnerCap = 64, GateRunnerCap = 40;

        const double BelowOne = 0.99999999999999989; // the largest double below 1

        /// the lambs a press at this stage as a multiple of stage 1's: lanes x each lane's litter = 1, 2, 6, 16, 50. Exactly 1 at
        /// stage 1 and below.
        public static double GateTotalMult(int stage)
        {
            int s = stage < 1 ? 1 : stage > 5 ? 5 : stage;
            return s * LaneLitter[s - 1];
        }

        /// the passes a machine needs for `load` sheep at `cap` a pass: 1 up to the cap, then load / cap (no step), at most MaxPasses.
        /// Exactly 1 for a cap that is 0, negative, NaN or infinite, and for a NaN load.
        public static double Passes(double load, double cap)
        {
            if (!(cap > 0) || !(load > cap)) return 1; // also a NaN cap or load, and an infinite cap
            return System.Math.Min(MaxPasses, load / cap);
        }

        /// where chamber k's cycle starts, in seconds after a multiple of ChamberCycle: 0, 3, 1.5, 4.5, 0.75, 3.75, 2.25, 5.25
        public static double ChamberOffset(int k)
        {
            return BitRev8[k & 7] * ChamberCycle / 8;
        }

        /// true when chamber k's cycle ends inside the step (clock - dt, clock]. The timers come from the clock alone, so nothing
        /// is saved, and adding a chamber never moves the others.
        public static bool ChamberFires(double clock, double dt, int k)
        {
            double o = ChamberOffset(k);
            return System.Math.Floor((clock - o) / ChamberCycle) > System.Math.Floor((clock - dt - o) / ChamberCycle);
        }

        /// how far chamber k is through its cycle at `clock`, in [0, 1) (its hut's lamp)
        public static double ChamberPhase(double clock, int k)
        {
            double x = (clock - ChamberOffset(k)) / ChamberCycle, f = x - System.Math.Floor(x);
            if (!(f >= 0)) return 0; // a NaN or infinite clock
            return f < 1 ? f : BelowOne; // a hair before a cycle's end can round up to 1
        }

        /// chamber k's couples when floor(inCh) couples split evenly over `chambers` chambers: the first floor(inCh) mod chambers
        /// take one more, so the shares add up to floor(inCh) exactly (whole numbers below 2^53). 0 for a chamber that is not there.
        public static double ChamberShare(double inCh, int chambers, int k)
        {
            if (chambers <= 0 || k < 0 || k >= chambers || !(inCh >= 1)) return 0;
            double n = System.Math.Floor(inCh), q = System.Math.Floor(n / chambers), r = n - q * chambers;
            if (r < 0) { q--; r += chambers; }
            else if (r >= chambers) { q++; r -= chambers; } // only beyond 2^53, where the quotient can land one off
            return k < r ? q + 1 : q;
        }

        /// lambs a birth with Twin Season and Triplet Season: 1 + twins% / 100 + 2 x triplets% / 100 (exactly 1 with neither)
        public static double BirthMult(double twinsPct, double tripletsPct)
        {
            return 1 + twinsPct / 100 + 2 * tripletsPct / 100;
        }

        /// the camera shake for a burst of n lambs: 0 below 10, then 0.05 for every factor of ten, at most 0.3 (from a million)
        public static double ShakeFor(double n)
        {
            if (!(n >= 10)) return 0; // also NaN
            if (n >= 1e6) return 0.3;
            return System.Math.Min(0.3, 0.05 * System.Math.Log10(n));
        }

        /// the size of a drove tail for n followers: 1 below 10, one more for every factor of ten, 6 from 100,000
        public static int ClusterTier(double n)
        {
            if (!(n >= 10)) return 1; // also NaN and negative
            if (n < 100) return 2;
            if (n < 1000) return 3;
            if (n < 1e4) return 4;
            if (n < 1e5) return 5;
            return 6;
        }
    }
}
