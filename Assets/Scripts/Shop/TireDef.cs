using UnityEngine;

namespace LearnToSpin
{
    /// <summary>
    /// One buyable tire. Carries the art (a wheel prefab + its shop icon, wired from the
    /// Assets/wheel pack by the LearnToSpin setup menu) and the BASE stats it launches with —
    /// weight and size change the feel, while the spin/boost bases set how good it is before
    /// any upgrades are bought. Better (more expensive) tires simply start with stronger bases.
    /// </summary>
    [System.Serializable]
    public class TireDef
    {
        public string displayName = "Tire";
        [Tooltip("Wheel visual from Assets/wheel/Prefabs (null → falls back to a plain cylinder).")]
        public GameObject prefab;
        [Tooltip("Shop icon from Assets/wheel/İcon (null → a coloured swatch is drawn instead).")]
        public Texture2D icon;

        [Tooltip("Cost to unlock in the shop. 0 = owned from the start (the starter tire).")]
        public int price;

        [Header("Physical")]
        [Tooltip("Mass, kg. Heavier carries momentum further but feels more sluggish.")]
        public float mass = 18f;
        [Tooltip("Radius, m. Bigger tire = larger collider and more reach per turn.")]
        public float radius = 0.5f;
        [Range(0f, 1f)] public float bounciness = 0.55f;

        [Header("Base launch stats (before upgrades)")]
        [Tooltip("Sweet-spot spin — the peak of the rev power band. Higher = stronger base launch.")]
        public float baseIdealSpin = 90f;
        [Tooltip("Rev ceiling. Kept a fixed margin above ideal so the over-rev window stays fair.")]
        public float baseMaxSpin = 110f;
        [Tooltip("Boost fuel the tire launches with.")]
        public float baseBoostReserve = 100f;

        [Header("Upgrade scaling")]
        [Tooltip("Multiplier on EVERY upgrade's per-level gain for THIS tire. Top-tier tires scale " +
                 "much harder with upgrades, so a fully-upgraded premium tire becomes borderline OP " +
                 "while the starter tire barely moves. 1 = stock gains.")]
        public float upgradePotency = 1f;

        [Header("Economy")]
        [Tooltip("Multiplier on the cash this tire earns from EVERY run stat (distance/speed/height/" +
                 "air time). Better tires don't just go further — they bank MORE per metre, so a " +
                 "premium tire compounds: it reaches further AND pays more for the same feat. 1 = " +
                 "stock payout. (A value ≤0, e.g. from a stale serialized asset, is treated as 1×.)")]
        public float earnMultiplier = 1f;
    }

    /// <summary>
    /// A single upgradeable stat track (boost capacity / wind-up speed / bounciness). Each level
    /// adds <see cref="perLevel"/> to the equipped tire's base, and costs more than the last.
    /// </summary>
    [System.Serializable]
    public class UpgradeDef
    {
        public string label = "Upgrade";
        [Tooltip("How much one level adds to the stat.")]
        public float perLevel = 10f;
        public int maxLevel = 8;
        [Tooltip("Cost of the first level.")]
        public int baseCost = 150;
        [Tooltip("Each level multiplies the previous cost by this.")]
        public float costGrowth = 1.6f;

        public int CostFor(int currentLevel) =>
            Mathf.RoundToInt(baseCost * Mathf.Pow(costGrowth, currentLevel));
    }

    /// <summary>The launch tunables for the currently equipped tire, after upgrades are folded in.</summary>
    public struct EffectiveStats
    {
        public float maxSpin, idealSpin, boostReserve, bounciness, mass, radius;
    }
}
