namespace Fleeced
{
    /// where a coin change comes from (or goes to). Later coin tiers and prestige will multiply only sales, exactly once.
    public enum CoinSrc { Sale, BeltSale, LiveSale, Decree, Interest, Contract, Event, Refund, Tutorial, Build, Upgrade, Path, Purchase, Fine, Wage, Theft, Offline, SheepBuy }

    /// One money path: every coin change goes through CreditCoins and SpendCoins, tagged with its source.
    /// lifetimeEarned counts every credit except refunds and the tutorial's Treasury top-ups. totalMade[0] keeps its old meaning
    /// (the "earn 5,000" decree and the FACTORY panel read it), so decree pacing doesn't change.
    public partial class Game
    {
        public double lifetimeEarned; // saved (version 10)

        /// The farm's own income: sales (by hand, AUTO, by belt, live sheep) and bank interest. Not decree rewards, events,
        /// contracts, refunds, gifts or offline awards, so an award can never feed the next one. What it made in the last whole
        /// game day, and so far today (both saved). Offline earnings pay a share of it.
        public double dayIncome, incomeToday;

        static bool IsIncome(CoinSrc src) { return src == CoinSrc.Sale || src == CoinSrc.BeltSale || src == CoinSrc.LiveSale || src == CoinSrc.Interest; }

        /// at every day change
        /// the day's income becomes the measure for offline earnings; a net debt (sheep bought for more than was earned) carries
        /// into the next day, so buying a sheep today and selling it tomorrow is no income either
        void MeasureDayIncome()
        {
            // Selling off a stored herd is not what the farm earns every day. The measure takes out the sheep the flock shrank by, as
            // far as they were turned into coins (sold alive or slaughtered today; sheep that starved or were eaten earned nothing),
            // at the day's price for a live sheep: one day of selling off can't set the rate of a whole night's offline earnings.
            double pop = Population, income = incomeToday;
            if (dayStartPop >= 0 && pop < dayStartPop)
            {
                double gone = System.Math.Min(dayStartPop - pop, (double)soldToday + slaughteredToday);
                income -= gone * (soldToday > 0 ? liveToday / soldToday : HerdLivePrice(0, false));
            }
            dayIncome = System.Math.Max(0, income); incomeToday = System.Math.Min(0, incomeToday);
            dayStartPop = pop; liveToday = 0; soldToday = 0; slaughteredToday = 0;
        }
        /// the flock when the day began, what sheep sold alive brought in today, and how many were sold or slaughtered today
        /// (saved, version 11; -1 = not measured yet)
        public double dayStartPop = -1, liveToday; public int soldToday, slaughteredToday;

        /// Offline earnings: for the time the game was closed, a share of the last day's own income for every second away (one game
        /// day is DayLength seconds at 1x), for at most 8 hours. Only a save written before this run of the game started earns it,
        /// so pausing and loading again earns nothing. Night Watch raises the share, to at most 75%.
        public const double OfflineShare = 0.25, OfflineMaxShare = 0.75, OfflineMaxHours = 8;
        public double OfflineRate { get { return System.Math.Min(OfflineMaxShare, OfflineShare * PathMult("offline")); } }
        static System.DateTime runStartUtc = System.DateTime.MinValue; // set by the first world of this run (Game.Awake)
        double offlineAward, offlineSecs; // found at load, paid and announced once the world runs

        /// at load: the time since the save was written, as the save's own UTC stamp says
        void FindOfflineAward(SaveData d)
        {
            offlineAward = 0; offlineSecs = 0;
            coinTierShown = CoinTier; // the save's own tier is no news; one the award reaches is
            System.DateTime saved;
            if (d.dayIncome <= 0 || string.IsNullOrEmpty(d.savedUtc)) return;
            if (!System.DateTime.TryParse(d.savedUtc, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out saved)) return;
            saved = saved.ToUniversalTime();
            if (runStartUtc != System.DateTime.MinValue && saved >= runStartUtc) return; // written during this run: the game wasn't closed
            double away = (System.DateTime.UtcNow - saved).TotalSeconds;
            if (!(away >= 60) || away > 30 * 86400.0) return; // a minute at least; never a clock that went backwards or a gap over 30 days
            offlineSecs = System.Math.Min(away, OfflineMaxHours * 3600);
            offlineAward = System.Math.Floor(d.dayIncome / DayLength * offlineSecs * OfflineRate);
        }

        /// pays the offline award once the world is running, nothing else is on screen, and any load notice has been shown
        void PayOfflineAward()
        {
            if (offlineAward <= 0 || !started || LeaderModal || currentEvent != null || eventQueue.Count > 0 || toastT > 0) return;
            double a = offlineAward; offlineAward = 0;
            CreditCoins(a, CoinSrc.Offline);
            lastRes[0] += a; // the HUD's coins-a-minute is income, not this
            int mins = (int)(offlineSecs / 60), h = mins / 60, m = mins % 60;
            string away = h > 0 ? h + "h " + m + "m" : m + " minutes";
            Log("^yWhile you were away^w (" + away + "): +" + UI.Fmt(a) + " coins.");
            Toast("^y+" + UI.Fmt(a) + " coins while you were away");
            Sfx.Play("coin", 0.7f);
            LeaderPop("While you were away (" + away + ") the factory made " + UI.Fmt(a) + " coins. I counted every one. Twice.", LExpr.Smug, 9f);
        }

        public bool CanAfford(double amount) { return res[0] >= amount; }

        public void CreditCoins(double amount, CoinSrc src)
        {
            res[0] += amount;
            if (src != CoinSrc.Refund && src != CoinSrc.Tutorial) lifetimeEarned += amount;
            if (IsIncome(src)) incomeToday += amount;
        }

        public void SpendCoins(double amount, CoinSrc src)
        {
            res[0] -= amount;
            // a sheep bought can be sold again (or slaughtered): its price is negative income, or the round trip would count the
            // whole sale as income, and the offline award pays about 300 days of a day's income
            if (src == CoinSrc.SheepBuy) incomeToday -= amount;
        }
    }
}
