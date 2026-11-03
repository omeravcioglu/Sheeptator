namespace Fleeced
{
    /// The arithmetic of upgrade paths, kept free of UnityEngine so Tools/unit.sh can test it on plain .NET.
    /// A path's level L costs base x growth^L coins to raise to L+1. Buying n levels costs the geometric sum
    /// base x growth^L x (growth^n - 1) / (growth - 1). MAX is the largest n whose sum the coins cover, solved in closed form
    /// and then checked with the same Cost function the purchase charges, so it can never overspend.
    /// Milestones at 10, 25, 50, 100, 250, 500, 1000, 2500 and 5000 each double the path's bonus.
    public static class UpgradeMath
    {
        public static readonly int[] Milestones = { 10, 25, 50, 100, 250, 500, 1000, 2500, 5000 };

        /// coins to raise a path from `level` by `n` levels, rounded up to whole coins (0 for n <= 0)
        public static double Cost(double baseCost, double growth, int level, int n)
        {
            if (n <= 0) return 0;
            double first = baseCost * System.Math.Pow(growth, level);
            if (growth == 1) return System.Math.Ceiling(first * n);
            return System.Math.Ceiling(first * (System.Math.Pow(growth, n) - 1) / (growth - 1)); // whole coins
        }

        /// the most levels `coins` buys, at most `room` (the levels left below the path's cap)
        public static int MaxAffordable(double baseCost, double growth, int level, double coins, int room)
        {
            if (room <= 0 || !(coins > 0) || double.IsInfinity(coins)) return 0;
            double first = baseCost * System.Math.Pow(growth, level);
            if (!(first > 0) || double.IsInfinity(first)) return 0;
            double est = growth == 1 ? coins / first
                : System.Math.Log(coins * (growth - 1) / first + 1) / System.Math.Log(growth);
            int n = est >= room ? room : (int)System.Math.Floor(System.Math.Max(0, est));
            // the closed form can land one off either way at the edges of double precision: settle it with Cost itself
            while (n > 0 && Cost(baseCost, growth, level, n) > coins) n--;
            while (n < room && Cost(baseCost, growth, level, n + 1) <= coins) n++;
            return n;
        }

        /// how many milestones `level` has passed
        public static int MilestonesReached(int level)
        {
            int m = 0;
            foreach (int s in Milestones) if (level >= s) m++;
            return m;
        }

        /// the next milestone above `level`, or 0 when all are passed
        public static int NextMilestone(int level)
        {
            foreach (int s in Milestones) if (level < s) return s;
            return 0;
        }

        /// a multiplying path: (1 + per x level), doubled at every milestone. Exactly 1 at level 0.
        public static double Mult(double per, int level)
        {
            if (level <= 0) return 1;
            return (1 + per * level) * System.Math.Pow(2, MilestonesReached(level));
        }

        /// an adding path: per x level, doubled at every milestone. Exactly 0 at level 0.
        public static double Add(double per, int level)
        {
            if (level <= 0) return 0;
            return per * level * System.Math.Pow(2, MilestonesReached(level));
        }

        /// the highest level whose next step still has a finite price (keeps every cost inside double's range)
        public static int CapFor(double baseCost, double growth, int wanted)
        {
            if (growth <= 1) return wanted;
            int cap = (int)System.Math.Floor((300 - System.Math.Log10(System.Math.Max(1, baseCost))) / System.Math.Log10(growth));
            return System.Math.Max(1, System.Math.Min(wanted, cap));
        }
    }
}
