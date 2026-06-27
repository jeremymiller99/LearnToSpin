using UnityEngine;

namespace LearnToSpin
{
    /// <summary>
    /// The shop's data: every tire you can buy, the three upgrade tracks, and the payout rates
    /// for a run. Lives as an asset (created + art-wired by the LearnToSpin menu) but the game
    /// never hard-depends on it — <see cref="CreateDefault"/> builds an identical in-code catalog
    /// so the prototype still runs (with plain-cylinder tires) if the asset isn't present, matching
    /// the "falls back to primitives" philosophy of the procedural builders.
    /// </summary>
    [CreateAssetMenu(menuName = "LearnToSpin/Tire Catalog", fileName = "TireCatalog")]
    public class TireCatalog : ScriptableObject
    {
        public TireDef[] tires;

        [Header("Upgrade tracks")]
        // Base costs are tuned to the new economy (a ~1500 first run): a single early upgrade is an
        // affordable alternative to saving for the first tire, while the costGrowth + per-tier cost
        // multiplier (GameDirector.UpgradeCost) makes fully maxing a premium tire a real investment.
        public UpgradeDef boostUpgrade = new UpgradeDef
        { label = "Boost Capacity", perLevel = 25f, maxLevel = 8, baseCost = 250, costGrowth = 1.55f };
        public UpgradeDef spinUpgrade = new UpgradeDef
        { label = "Wind-Up Speed", perLevel = 12f, maxLevel = 8, baseCost = 300, costGrowth = 1.6f };
        public UpgradeDef bounceUpgrade = new UpgradeDef
        { label = "Bounciness", perLevel = 0.05f, maxLevel = 6, baseCost = 200, costGrowth = 1.5f };

        [Header("Run payout (money earned)")]
        [Tooltip("Cash per metre of distance.")]
        public float moneyPerMetre = 1.0f;
        [Tooltip("Cash per m/s of top speed reached.")]
        public float moneyPerTopSpeed = 6f;
        [Tooltip("Cash per metre of max height reached.")]
        public float moneyPerHeight = 4f;
        [Tooltip("Cash per second of the longest airborne stretch — rewards big air off ramps.")]
        public float moneyPerAirTime = 30f;

        /// <summary>Names used for the 12 wheels, in order, getting fancier as they get pricier.</summary>
        static readonly string[] Names =
        {
            "Standard", "Trainer", "Street", "Rally", "Off-Road", "Mud Terrain",
            "Drag Slick", "Monster", "Industrial", "Hover", "Carbon", "Plasma"
        };

        /// <summary>
        /// Builds the default 12-tire catalog (stats only — no art). Used both as the runtime
        /// fallback and as the seed the editor menu fills wheel prefabs/icons into.
        /// </summary>
        public static TireCatalog CreateDefault()
        {
            var c = CreateInstance<TireCatalog>();
            c.tires = new TireDef[Names.Length];
            for (int i = 0; i < Names.Length; i++)
            {
                // Bases climb on an accelerating (linear + quadratic) curve so the gap between
                // tiers WIDENS as you go up — a high tier is dramatically stronger off the bare
                // base, before a single upgrade is bought. Top tier (~i=11): ideal ~252 vs 90,
                // boost ~436 vs 100. maxSpin keeps a fixed +20 over-rev margin above ideal.
                float ideal = 90f + i * 7f + i * i * 0.7f;
                c.tires[i] = new TireDef
                {
                    displayName = Names[i],
                    // Steep geometric price curve tuned to the earn-multiplier economy below: a ~1500
                    // first run buys only the FIRST tire, and every tier after is a deliberate next
                    // goal funded by the rising payout — you can't skip the early tiers on one good
                    // run. The ~2.05× per-tier growth makes each new tire roughly double the last, so
                    // the top tiers are genuine long-haul investments (Trainer $1k → Plasma ~$1.3M).
                    price = i == 0 ? 0 : Mathf.RoundToInt(1000f * Mathf.Pow(2.05f, i - 1) / 50f) * 50,
                    // size/weight wobble so tiers feel distinct rather than a flat ramp
                    mass = 16f + (i % 3) * 4f + i * 0.5f,
                    radius = 0.45f + (i % 4) * 0.05f,
                    bounciness = Mathf.Clamp01(0.5f + i * 0.02f),
                    baseIdealSpin = ideal,
                    baseMaxSpin = ideal + 20f,
                    baseBoostReserve = 100f + i * 14f + i * i * 1.5f,
                    // Upgrades pay off MUCH harder on better tires (1.0 → ~2.8×), so maxing a
                    // premium tire stacks into borderline-OP launches while the starter stays tame.
                    upgradePotency = 1f + i * 0.16f,
                    // Better tires also EARN more from every run stat (1× → ~7.7×), on an
                    // accelerating curve so premium tires snowball: they fly further AND bank more
                    // per metre/speed/height/air. The steeper earn ramp keeps pace with the steeper
                    // (~doubling) price curve above so each next tier stays a reachable goal.
                    earnMultiplier = 1f + i * 0.22f + i * i * 0.035f,
                };
            }
            return c;
        }
    }
}
