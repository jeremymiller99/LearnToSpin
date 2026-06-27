using UnityEngine;

namespace LearnToSpin
{
    /// <summary>
    /// The game's win condition: four tracked stats — Distance, Top Speed, Max Height, Air Time —
    /// each with five escalating tiers (20 goals total). Clear every tier of every stat and the
    /// game is won (<see cref="GameDirector"/> fires the win screen). Completion is measured against
    /// the LIFETIME bests banked on <see cref="PlayerProgress"/> (the best* fields), so a goal stays
    /// cleared once hit — you don't have to nail all four in a single run.
    ///
    /// Thresholds are plain constants below; tweak the <see cref="Thresholds"/> table to retune.
    /// </summary>
    public static class Objectives
    {
        public const int StatCount = 4;
        public const int TierCount = 5;
        public const int TotalTiers = StatCount * TierCount; // 20

        public static readonly string[] Titles = { "Distance", "Top Speed", "Max Height", "Air Time" };
        public static readonly string[] Units  = { "m", "m/s", "m", "s" };

        // Per-stat display format (distance/speed/height round to whole, air time keeps a decimal).
        static readonly string[] Fmt = { "0", "0", "0", "0.0" };

        /// <summary>[stat][tier] target values, escalating per tier. Edit these to retune the goals.</summary>
        public static readonly float[][] Thresholds =
        {
            new[] { 1000f, 2500f, 5000f, 8000f, 12000f }, // Distance   (m)
            new[] {   50f,  100f,  150f,  200f,   275f }, // Top Speed  (m/s)
            new[] {   15f,   35f,   70f,  120f,   200f }, // Max Height (m)
            new[] {    3f,    6f,   10f,   15f,    22f }, // Air Time   (s)
        };

        public static float Threshold(int stat, int tier) => Thresholds[stat][tier];

        /// <summary>Lifetime best the player has ever banked for a stat — what completion is measured against.</summary>
        public static float Best(PlayerProgress p, int stat) =>
            stat == 0 ? p.bestDistance :
            stat == 1 ? p.bestTopSpeed :
            stat == 2 ? p.bestHeight  :
                        p.bestAirTime;

        public static bool TierDone(PlayerProgress p, int stat, int tier) =>
            Best(p, stat) >= Threshold(stat, tier);

        /// <summary>How many of a stat's tiers are cleared (0..TierCount).</summary>
        public static int TiersDone(PlayerProgress p, int stat)
        {
            int n = 0;
            for (int t = 0; t < TierCount; t++) if (TierDone(p, stat, t)) n++;
            return n;
        }

        /// <summary>Total cleared tiers across all four stats (0..TotalTiers).</summary>
        public static int TotalDone(PlayerProgress p)
        {
            int n = 0;
            for (int s = 0; s < StatCount; s++) n += TiersDone(p, s);
            return n;
        }

        public static bool AllComplete(PlayerProgress p) => TotalDone(p) == TotalTiers;

        /// <summary>"Distance: 1000 m" style label for a single tier goal.</summary>
        public static string Label(int stat, int tier) =>
            $"{Titles[stat]}: {Threshold(stat, tier).ToString(Fmt[stat])} {Units[stat]}";

        /// <summary>Format a raw stat value with its unit ("5,210 m", "8.4 s", ...).</summary>
        public static string Format(int stat, float value) =>
            $"{FormatNumber(stat, value)} {Units[stat]}";

        public static string FormatNumber(int stat, float value) =>
            value.ToString(stat == 0 ? "N0" : Fmt[stat]);
    }
}
