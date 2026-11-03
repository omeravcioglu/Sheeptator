using System.Globalization;

namespace Fleeced
{
    /// Every number the player sees goes through here (UI.Fmt calls Fmt).
    /// - Below a billion it matches the v9 HUD: whole numbers up to 9999, then 12.3k and 4.5M.
    /// - From a billion on, short-scale names: B, T, Qa, Qi, Sx, Sp, Oc, No, Dc, then generated Latin names (Ud, Dd...
    ///   Vg, Uvg... Ce for a centillion, 1e303). Past the names, or from a million on with the SCIENTIFIC option, 1.23e45.
    /// - A value that would round up to "1000.0" of one unit is shown in the next unit (1.0M, never 1000.0k).
    /// - NaN and infinities show as "?". The pixel font's colour escape '^' never appears.
    /// No UnityEngine here: Tools/unit.sh tests this file on plain .NET.
    public static class Num
    {
        /// the NUMBERS option on the title screen: false = named (4.5Qa), true = scientific (4.50e15)
        public static bool Scientific;

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly string[] Units = { "", "U", "D", "T", "Qa", "Qi", "Sx", "Sp", "Oc", "No" };
        static readonly string[] Tens = { "", "Dc", "Vg", "Tg", "Qag", "Qig", "Sxg", "Spg", "Ocg", "Nog" };
        static string[] names;

        /// the suffix for 1000^n: n = 1 is "k", 2 "M", 3 "B" ... 101 "Ce" (a centillion, 1e303); null past that
        public static string Suffix(int n)
        {
            if (names == null)
            {
                var l = new string[102];
                l[0] = ""; l[1] = "k"; l[2] = "M"; l[3] = "B"; l[4] = "T";
                // n = 5 .. 101 is the (n-1)-illion: 4 quadrillion ... 100 centillion
                for (int i = 4; i <= 100; i++)
                {
                    string s;
                    if (i < 10) s = Units[i];
                    else if (i == 100) s = "Ce";
                    else if (i % 10 == 0) s = Tens[i / 10];
                    else if (i < 20) s = Units[i % 10] + "d";
                    else s = Units[i % 10] + Tens[i / 10].ToLowerInvariant();
                    l[i + 1] = s;
                }
                names = l;
            }
            return n >= 0 && n < names.Length ? names[n] : null;
        }

        /// compact form for the HUD, panels and costs: 1234, 12.3k, 4.5M, 6.7B ... (or 1.23e45)
        public static string Fmt(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "?";
            double a = System.Math.Abs(v);
            if (a < 10000) return ((long)System.Math.Floor(v)).ToString(Inv);
            if (Scientific && a >= 1000000) return Sci(v);
            // the unit: k from 10,000 (as in v9), then every factor of 1000
            int n = 1;
            while (n < 101 && a >= System.Math.Pow(1000, n + 1)) n++;
            for (; n <= 101; n++)
            {
                string s = (v / System.Math.Pow(1000, n)).ToString("0.0", Inv);
                if (s.TrimStart('-').Length >= 6) continue; // 999.95k is 1.0M, not 1000.0k
                return s + Suffix(n);
            }
            return Sci(v); // past the names: 1000 centillion and up
        }

        /// scientific form: 1.23e45 (the mantissa is 1.00 to 9.99)
        public static string Sci(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "?";
            if (v == 0) return "0";
            double a = System.Math.Abs(v);
            int e = (int)System.Math.Floor(System.Math.Log10(a));
            double m = a / System.Math.Pow(10, e);
            if (m < 1) { m *= 10; e--; } // Log10 can land one below for exact powers
            string ms = m.ToString("0.00", Inv);
            if (ms.StartsWith("10")) { e++; ms = (m / 10).ToString("0.00", Inv); }
            return (v < 0 ? "-" : "") + ms + "e" + e.ToString(Inv);
        }

        /// a whole number for a count the player reads exactly (a price, a stock): 1234567 up to a billion, then Fmt
        public static string Whole(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "?";
            if (System.Math.Abs(v) < 1e9) return ((long)System.Math.Round(v)).ToString(Inv);
            return Fmt(v);
        }
    }
}
