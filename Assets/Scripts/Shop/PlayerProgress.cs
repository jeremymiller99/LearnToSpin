using System;
using UnityEngine;

namespace LearnToSpin
{
    /// <summary>At-a-glance state of one save slot for the profile-select menu (no catalog needed).</summary>
    public struct ProfileSummary
    {
        public bool exists; // false → the slot is empty ("New Game")
        public int day;
        public int money;
    }

    /// <summary>
    /// The persistent meta-game state: wallet, which tires are owned, which is equipped, and the
    /// three upgrade levels. Serialized to PlayerPrefs as JSON (WebGL-friendly). Knows how to fold
    /// the upgrade levels into the equipped tire's bases to produce the launcher's live tunables.
    /// </summary>
    [Serializable]
    public class PlayerProgress
    {
        public int money;
        public int equippedIndex;
        public bool[] owned;

        // Upgrade levels are now PER TIRE — each tire carries its own boost/spin/bounce progression,
        // indexed parallel to the catalog. Buying an upgrade improves only the tire it was bought for.
        public int[] boostLevels;
        public int[] spinLevels;
        public int[] bounceLevels;

        // Legacy pre-per-tire global levels. Only read once, in EnsureValid, to migrate an old save
        // onto its equipped tire; cleared afterwards so they never apply again.
        public int boostLevel;
        public int spinLevel;
        public int bounceLevel;

        public int day = 1; // current day; advances by 1 each time a new run is launched from the shop

        // ---- Win-condition objectives (see Objectives.cs) ------------------------------------
        // Lifetime bests across EVERY run of this profile. Objective tiers clear against these (not
        // a single run), so a goal once hit stays hit. `won`/`winDay` record the victory — the day
        // the final tier of the last stat was cleared. All default to 0/false on pre-objective saves.
        public float bestDistance, bestTopSpeed, bestHeight, bestAirTime;
        public bool won;
        public int winDay;

        // True once the player has watched the intro cutscene on this profile, so it only plays the
        // first time the Game scene is entered for a save (defaults false → plays for old saves too).
        public bool introSeen;

        // ---- Profiles -------------------------------------------------------------------------
        // There are SlotCount independent save profiles. Each is stored under its own PlayerPrefs
        // key (KeyForSlot); the menu picks which one is "active" before loading the Game scene, and
        // the Game loads/saves whichever slot is active. `slot` records which profile THIS instance
        // came from so Save() writes back to the right key (NonSerialized so it never lands in JSON).
        public const int SlotCount = 3;

        const string LegacyKey = "lts_progress_v1";          // pre-profiles single save
        const string KeyPrefix = "lts_progress_v1_";         // per-slot keys: lts_progress_v1_0..2
        const string ActiveSlotKey = "lts_active_profile";

        [NonSerialized] public int slot;

        static string KeyForSlot(int slot) => KeyPrefix + Mathf.Clamp(slot, 0, SlotCount - 1);

        /// <summary>Which profile the Game scene will load/save. Set by the menu before launch.</summary>
        public static int ActiveSlot
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(ActiveSlotKey, 0), 0, SlotCount - 1);
            set { PlayerPrefs.SetInt(ActiveSlotKey, Mathf.Clamp(value, 0, SlotCount - 1)); PlayerPrefs.Save(); }
        }

        /// <summary>One-time move of a pre-profiles save into slot 0 so old players keep their run.</summary>
        static void MigrateLegacy()
        {
            if (PlayerPrefs.HasKey(LegacyKey) && !PlayerPrefs.HasKey(KeyForSlot(0)))
            {
                PlayerPrefs.SetString(KeyForSlot(0), PlayerPrefs.GetString(LegacyKey));
                PlayerPrefs.DeleteKey(LegacyKey);
                PlayerPrefs.Save();
            }
        }

        public static bool HasSave(int slot)
        {
            MigrateLegacy();
            return PlayerPrefs.HasKey(KeyForSlot(slot));
        }

        /// <summary>Light read of a slot for the menu list — no catalog needed (day/money only).</summary>
        public static ProfileSummary Peek(int slot)
        {
            MigrateLegacy();
            var sum = new ProfileSummary();
            string key = KeyForSlot(slot);
            if (!PlayerPrefs.HasKey(key)) return sum;
            try
            {
                var p = JsonUtility.FromJson<PlayerProgress>(PlayerPrefs.GetString(key));
                if (p != null) { sum.exists = true; sum.day = Mathf.Max(1, p.day); sum.money = p.money; }
            }
            catch { /* corrupt → treat as empty */ }
            return sum;
        }

        /// <summary>Wipe a profile so its slot reads as empty again (the "delete to reset").</summary>
        public static void DeleteSlot(int slot)
        {
            PlayerPrefs.DeleteKey(KeyForSlot(slot));
            PlayerPrefs.Save();
        }

        /// <summary>Seed an empty save into a slot and make it active, so a brand-new profile shows
        /// up in the list immediately (before the Game has saved anything). The Game's Load() then
        /// repairs it against the catalog. Used by the menu's "New Game".</summary>
        public static void CreateNewProfile(int slot)
        {
            slot = Mathf.Clamp(slot, 0, SlotCount - 1);
            new PlayerProgress { slot = slot, day = 1, money = 0 }.Save();
            ActiveSlot = slot;
        }

        public static PlayerProgress Load(TireCatalog cat) => Load(cat, ActiveSlot);

        public static PlayerProgress Load(TireCatalog cat, int slot)
        {
            MigrateLegacy();
            slot = Mathf.Clamp(slot, 0, SlotCount - 1);
            string key = KeyForSlot(slot);
            if (PlayerPrefs.HasKey(key))
            {
                try
                {
                    var p = JsonUtility.FromJson<PlayerProgress>(PlayerPrefs.GetString(key));
                    if (p != null) { p.slot = slot; p.EnsureValid(cat); return p; }
                }
                catch { /* corrupt save → start fresh */ }
            }
            var np = new PlayerProgress { slot = slot };
            np.EnsureValid(cat);
            return np;
        }

        public void Save()
        {
            PlayerPrefs.SetString(KeyForSlot(slot), JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }

        /// <summary>Resize/repair the owned mask to match the catalog and guarantee a valid equip.</summary>
        void EnsureValid(TireCatalog cat)
        {
            int n = cat != null && cat.tires != null ? cat.tires.Length : 1;
            if (owned == null || owned.Length != n)
            {
                var o = new bool[n];
                if (owned != null) Array.Copy(owned, o, Mathf.Min(owned.Length, n));
                owned = o;
            }
            owned[0] = true; // the starter tire is always owned
            equippedIndex = Mathf.Clamp(equippedIndex, 0, n - 1);
            if (!owned[equippedIndex]) equippedIndex = 0;
            if (day < 1) day = 1; // old saves (pre-day-system) deserialize day as 0

            // Size the per-tire upgrade arrays to the catalog (preserving any saved levels).
            boostLevels = Resize(boostLevels, n);
            spinLevels = Resize(spinLevels, n);
            bounceLevels = Resize(bounceLevels, n);

            // One-time migration: fold a pre-per-tire save's global levels onto its equipped tire,
            // then clear the legacy fields so this never runs again.
            if (boostLevel > 0 || spinLevel > 0 || bounceLevel > 0)
            {
                boostLevels[equippedIndex] = Mathf.Max(boostLevels[equippedIndex], boostLevel);
                spinLevels[equippedIndex] = Mathf.Max(spinLevels[equippedIndex], spinLevel);
                bounceLevels[equippedIndex] = Mathf.Max(bounceLevels[equippedIndex], bounceLevel);
                boostLevel = spinLevel = bounceLevel = 0;
            }
        }

        static int[] Resize(int[] src, int n)
        {
            var dst = new int[n];
            if (src != null) Array.Copy(src, dst, Mathf.Min(src.Length, n));
            return dst;
        }

        /// <summary>Fold one run's stats into the lifetime bests (used for objective completion).
        /// Returns true if any best improved this run.</summary>
        public bool RecordBests(float distance, float topSpeed, float height, float airTime)
        {
            bool changed = false;
            if (distance > bestDistance) { bestDistance = distance; changed = true; }
            if (topSpeed > bestTopSpeed) { bestTopSpeed = topSpeed; changed = true; }
            if (height   > bestHeight)   { bestHeight   = height;   changed = true; }
            if (airTime  > bestAirTime)  { bestAirTime  = airTime;  changed = true; }
            return changed;
        }

        public bool Owns(int index) => owned != null && index >= 0 && index < owned.Length && owned[index];

        int[] ArrFor(int track) => track == 0 ? boostLevels : track == 1 ? spinLevels : bounceLevels;

        /// <summary>This tire's level on one upgrade track (0 if out of range).</summary>
        public int LevelFor(int track, int index)
        {
            var arr = ArrFor(track);
            return arr != null && index >= 0 && index < arr.Length ? arr[index] : 0;
        }

        /// <summary>Bump one upgrade track on one tire by a level (the purchase is paid for elsewhere).</summary>
        public void AddLevel(int track, int index)
        {
            var arr = ArrFor(track);
            if (arr != null && index >= 0 && index < arr.Length) arr[index]++;
        }

        /// <summary>Combine the equipped tire's bases with ITS OWN bought upgrade levels.</summary>
        public EffectiveStats Effective(TireCatalog cat) => Effective(cat, equippedIndex);

        /// <summary>Combine a specific tire's bases with that tire's own upgrade levels (shop preview).</summary>
        public EffectiveStats Effective(TireCatalog cat, int index)
        {
            index = Mathf.Clamp(index, 0, cat.tires.Length - 1);
            var def = cat.tires[index];
            // Per-tire potency makes each upgrade level worth more on better tires. An unset/old
            // value (<=0, e.g. a pre-potency serialized asset) falls back to a neutral 1× rather
            // than zeroing every upgrade out.
            float pot = def.upgradePotency > 0.01f ? def.upgradePotency : 1f;
            float spinGain = LevelFor(1, index) * cat.spinUpgrade.perLevel * pot;
            return new EffectiveStats
            {
                idealSpin = def.baseIdealSpin + spinGain,
                maxSpin = def.baseMaxSpin + spinGain,
                boostReserve = def.baseBoostReserve + LevelFor(0, index) * cat.boostUpgrade.perLevel * pot,
                bounciness = Mathf.Clamp01(def.bounciness + LevelFor(2, index) * cat.bounceUpgrade.perLevel * pot),
                mass = def.mass,
                radius = def.radius,
            };
        }
    }
}
