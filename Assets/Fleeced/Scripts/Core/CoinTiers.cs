using UnityEngine;

namespace Fleeced
{
    /// Coin Evolutions. As lifetime earnings grow, the farm's coin evolves through 15 tiers, from the Wool Coin to the Golden
    /// Fleece Coin, and every tier makes every sale pay 20% more: goods (by hand, AUTO and by belt) and sheep sold alive.
    /// The multiplier is applied once, where sale prices are computed, so every price on screen already includes it.
    /// It is exactly 1 at the first tier. The tier comes from lifetimeEarned, so it needs no save field of its own.
    public partial class Game
    {
        public static readonly string[] CoinTierNames =
        {
            "Wool Coin", "Copper Coin", "Bronze Coin", "Silver Coin", "Gold Coin", "Platinum Coin", "Ruby Coin", "Sapphire Coin",
            "Emerald Coin", "Diamond Coin", "Moonstone Coin", "Stardust Coin", "Nebula Coin", "Quantum Coin", "Golden Fleece Coin",
        };
        /// lifetime earnings each tier needs
        public static readonly double[] CoinTierAt = { 0, 1e3, 5e3, 2e4, 8e4, 3e5, 1e6, 4e6, 1.5e7, 6e7, 2.5e8, 1e9, 4e9, 1.5e10, 6e10 };
        public const float CoinTierStep = 1.2f;
        int coinTierShown = -1; // the tier last announced (set at load and new game, so only new tiers are celebrated)

        public int CoinTier
        {
            get
            {
                int t = 0;
                for (int i = 1; i < CoinTierAt.Length; i++) if (lifetimeEarned >= CoinTierAt[i]) t = i;
                return t;
            }
        }

        /// what every sale is multiplied by: 1.2 to the power of the tier (exactly 1 at the first)
        public float CoinTierMult { get { int t = CoinTier; return t == 0 ? 1f : Mathf.Pow(CoinTierStep, t); } }

        /// every simulation step: celebrate a new tier
        void CheckCoinTier()
        {
            int t = CoinTier;
            if (coinTierShown < 0) { coinTierShown = t; return; }
            if (t <= coinTierShown) return;
            coinTierShown = t;
            string name = CoinTierNames[t].ToUpper(), mult = CoinTierMult.ToString("0.00");
            Log("^yCOIN EVOLUTION:^w the " + CoinTierNames[t] + ". Every sale pays x" + mult + " now.");
            // the toast and the bubble wait for the Supreme Leader's current line (a decree's NEXT, the offline award)
            AnnounceWhenFree("coin_tier", () =>
            {
                Toast("^yCOIN EVOLUTION: " + name + "!  Sales x" + mult);
                Sfx.Play("fanfare", 0.8f, 1.1f);
                if (tutDone) AdviseNow("coin_tier", "Our coin has EVOLVED, Hoof. Behold: the " + name + ". Every sale is worth more now. Money makes money. That is the whole trick.", LExpr.Smug, 0.2f);
            });
        }

        /// the coin's tier for the HUD tooltip: "Silver Coin: sales x1.73, next at 80k earned"
        public string CoinTierText()
        {
            int t = CoinTier;
            string s = CoinTierNames[t] + " (tier " + (t + 1) + "/" + CoinTierNames.Length + "): sales x" + CoinTierMult.ToString("0.00");
            if (t + 1 < CoinTierAt.Length) s += "\nEvolves into the " + CoinTierNames[t + 1] + " at " + UI.Fmt(CoinTierAt[t + 1]) + " lifetime earnings (now " + UI.Fmt(lifetimeEarned) + ").";
            return s;
        }
    }
}
