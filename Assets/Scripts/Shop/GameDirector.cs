using UnityEngine;

namespace LearnToSpin
{
    /// <summary>
    /// Read-only snapshot of the run that just ended: the three tracked stats, the cash each one
    /// earned (so the results screen can show value × rate = reward), the total, and the wallet
    /// balance from before the payout (for the count-up).
    /// </summary>
    public struct RunSummary
    {
        public float distance, topSpeed, maxHeight, airTime;
        public int distanceMoney, speedMoney, heightMoney, airMoney, earned;
        public int moneyBefore;
        public float earnMultiplier;  // the equipped tire's payout multiplier this run (1 = stock)
        public string tireName;       // equipped tire, for the results-screen earnings badge
        public bool valid;
    }

    /// <summary>
    /// Owns the meta-game loop that sits around the launch prototype: load progress, build the
    /// equipped tire (+camera/UI/shop), and on each run end pay out money and open the shop. The
    /// shop spends that money; pressing Launch rebuilds the tire with the new equip/upgrades and
    /// starts the next run. GameBootstrap builds the static world, then hands off to this.
    /// </summary>
    public class GameDirector : MonoBehaviour
    {
        public TireCatalog Catalog { get; private set; }
        public PlayerProgress Progress { get; private set; }
        public RunSummary LastRun { get; private set; }
        public bool ResultsOpen { get; private set; }
        public bool ShopOpen { get; private set; }
        public bool TransitionOpen => _dayTransition != null && _dayTransition.Active;

        GameBootstrap _boot;
        GameObject _tire;
        TireLauncher _launcher;
        ChaseCamera _chase;
        LaunchUI _launchUI;
        TrickMeterUI _trick;
        WorldStreamer _streamer;
        DayTransitionUI _dayTransition;

        public void Boot(GameBootstrap boot, PhysicsMaterial grip)
        {
            _boot = boot;
            Catalog = boot.tireCatalog != null ? boot.tireCatalog : TireCatalog.CreateDefault();
            Progress = PlayerProgress.Load(Catalog);

            BuildTire();
            RigBuilder.BuildCamera(boot, _tire.transform);
            _chase = Camera.main != null ? Camera.main.GetComponent<ChaseCamera>() : null;
            RigBuilder.BuildUI(_launcher);
            _launchUI = FindFirstObjectByType<LaunchUI>();
            if (_launchUI != null) _launchUI.director = this;
            ApplyHudTheme();

            // Skate-style trick meter that pops around the tire on big air.
            _trick = new GameObject("TrickMeterUI").AddComponent<TrickMeterUI>();
            _trick.launcher = _launcher;
            _trick.target = _tire.transform;

            // Stream the endless terrain around the tire (needs the tire to exist first). Seeded by
            // the current day so each day's run is a brand-new layout (see WorldSeedForDay).
            _streamer = new GameObject("WorldStreamer").AddComponent<WorldStreamer>();
            _streamer.Init(boot, grip, _tire.transform, WorldSeedForDay());

            var results = new GameObject("ResultsUI").AddComponent<ResultsUI>();
            results.director = this;

            var shop = new GameObject("ShopUI").AddComponent<ShopUI>();
            shop.director = this;

            // Fade-to-black "Day X" card that hides the tire teleport between runs.
            _dayTransition = new GameObject("DayTransitionUI").AddComponent<DayTransitionUI>();
            _dayTransition.director = this;
        }

        void BuildTire()
        {
            var def = Catalog.tires[Mathf.Clamp(Progress.equippedIndex, 0, Catalog.tires.Length - 1)];
            _tire = TireBuilder.Build(_boot, def, Progress.Effective(Catalog));
            _launcher = _tire.GetComponent<TireLauncher>();
            _launcher.RunEnded += OnRunEnded;
        }

        void OnRunEnded()
        {
            // The equipped tire's earnings multiplier scales EVERY stat's payout: a better tire banks
            // more per metre/speed/height/air, so it compounds with how much further it already flies.
            // Defensive ≤0 → 1× so a stale catalog asset (new field deserializes to 0) can't zero pay.
            var def = Catalog.tires[Mathf.Clamp(Progress.equippedIndex, 0, Catalog.tires.Length - 1)];
            float em = def.earnMultiplier > 0.01f ? def.earnMultiplier : 1f;

            // Money is awarded per stat so the results screen can show the breakdown (and the
            // rounded rows sum to exactly the total banked).
            int dM = Mathf.RoundToInt(_launcher.Distance * Catalog.moneyPerMetre * em);
            int sM = Mathf.RoundToInt(_launcher.TopSpeed * Catalog.moneyPerTopSpeed * em);
            int hM = Mathf.RoundToInt(_launcher.MaxHeight * Catalog.moneyPerHeight * em);
            int aM = Mathf.RoundToInt(_launcher.LongestAirTime * Catalog.moneyPerAirTime * em);
            int earned = dM + sM + hM + aM;
            int before = Progress.money;

            LastRun = new RunSummary
            {
                distance = _launcher.Distance,
                topSpeed = _launcher.TopSpeed,
                maxHeight = _launcher.MaxHeight,
                airTime = _launcher.LongestAirTime,
                distanceMoney = dM,
                speedMoney = sM,
                heightMoney = hM,
                airMoney = aM,
                earned = earned,
                moneyBefore = before,
                earnMultiplier = em,
                tireName = def.displayName,
                valid = true,
            };
            // Bank + save now so the payout survives even if the player quits mid-animation; the
            // results screen counts the wallet up from moneyBefore to the new total for show.
            Progress.money += earned;
            Progress.Save();
            OpenResults();
        }

        void OpenResults()
        {
            ResultsOpen = true;
            ShopOpen = false;
            if (_launcher != null) _launcher.enabled = false; // freeze input (R/Space) while the screens are up
        }

        /// <summary>Results screen "Continue": hand off to the shop.</summary>
        public void ContinueToShop()
        {
            ResultsOpen = false;
            ShopOpen = true;
        }

        /// <summary>
        /// Shop "Launch" button: advance the day and start the next run. The actual tire rebuild is
        /// deferred to the fully-black midpoint of the day-transition fade so the tire snapping back
        /// to the start line is hidden; the launcher stays frozen until the fade-in finishes.
        /// </summary>
        public void StartNextRun()
        {
            if (TransitionOpen) return; // already mid-fade — ignore double presses
            ResultsOpen = false;
            ShopOpen = false;

            Progress.day++;
            Progress.Save();
            if (_launcher != null) _launcher.enabled = false; // keep input frozen through the fade

            if (_dayTransition != null) _dayTransition.Begin(Progress.day, RebuildForNextRun);
            else { RebuildForNextRun(); OnTransitionDone(); } // no transition UI → swap immediately
        }

        /// <summary>Tear down the finished tire and build a fresh one at the start line. Runs while
        /// the screen is black (concealed) — see <see cref="StartNextRun"/>.</summary>
        void RebuildForNextRun()
        {
            if (_tire != null)
            {
                _launcher.RunEnded -= OnRunEnded;
                Destroy(_tire);
            }
            BuildTire();
            if (_chase != null) _chase.target = _tire.transform;
            if (_launchUI != null) _launchUI.launcher = _launcher;
            if (_trick != null) { _trick.launcher = _launcher; _trick.target = _tire.transform; }
            ApplyHudTheme();
            // Regenerate the world from the start for the new run — a new day means a new layout.
            if (_streamer != null) _streamer.Retarget(_tire.transform, WorldSeedForDay());
            if (_launcher != null) _launcher.enabled = false; // stay frozen until the fade-in ends
        }

        /// <summary>Terrain seed for the current day. Folds the world-style seed together with the
        /// day number so every day generates a fresh layout, while staying deterministic within the
        /// day (so the endless streamer's recycled chunks regenerate identically as you roll).</summary>
        int WorldSeedForDay() => _boot.worldSeed + Progress.day * 7919;

        /// <summary>Day-transition fade-in finished: hand control back to the player.</summary>
        public void OnTransitionDone()
        {
            if (_launcher != null) _launcher.enabled = true;
        }

        /// <summary>Style the in-run HUD to match the equipped tire (accent + tier flourishes).</summary>
        void ApplyHudTheme()
        {
            if (_launchUI == null) return;
            int i = Mathf.Clamp(Progress.equippedIndex, 0, Catalog.tires.Length - 1);
            _launchUI.SetTire(i, Catalog.tires.Length, Catalog.tires[i].displayName);
        }

        // ---- Shop transactions (called by ShopUI). All persist immediately. ----

        /// <summary>One upgrade track's level on a specific tire (upgrades are per-tire now).</summary>
        public int UpgradeLevel(int track, int tireIndex) => Progress.LevelFor(track, tireIndex);

        /// <summary>Effective stats for any tire (its bases + its own upgrade levels) — shop preview.</summary>
        public EffectiveStats EffectiveFor(int index) => Progress.Effective(Catalog, index);

        public UpgradeDef UpgradeDefFor(int track) =>
            track == 0 ? Catalog.boostUpgrade : track == 1 ? Catalog.spinUpgrade : Catalog.bounceUpgrade;

        /// <summary>Cost of the next level of <paramref name="track"/> on <paramref name="tireIndex"/>.
        /// Scales up mildly with tier so loading a premium tire to OP costs more than the starter.</summary>
        public int UpgradeCost(int track, int tireIndex)
        {
            var def = UpgradeDefFor(track);
            int level = UpgradeLevel(track, tireIndex);
            // Upgrades cost more on higher tiers (they also gain far more — see upgradePotency), so
            // loading a premium tire to OP is a real money sink rather than a rounding error next to
            // its price tag.
            float tierMul = 1f + Mathf.Max(0, tireIndex) * 0.18f;
            return Mathf.RoundToInt(def.CostFor(level) * tierMul / 10f) * 10;
        }

        public bool TryBuyUpgrade(int track, int tireIndex)
        {
            if (!Progress.Owns(tireIndex)) return false; // can only upgrade a tire you own
            var def = UpgradeDefFor(track);
            if (UpgradeLevel(track, tireIndex) >= def.maxLevel) return false;
            int cost = UpgradeCost(track, tireIndex);
            if (Progress.money < cost) return false;

            Progress.money -= cost;
            Progress.AddLevel(track, tireIndex);
            Progress.Save();
            return true;
        }

        public bool TryBuyTire(int index)
        {
            if (Progress.Owns(index)) { EquipTire(index); return true; }
            var def = Catalog.tires[index];
            if (Progress.money < def.price) return false;

            Progress.money -= def.price;
            Progress.owned[index] = true;
            Progress.equippedIndex = index;
            Progress.Save();
            return true;
        }

        public void EquipTire(int index)
        {
            if (!Progress.Owns(index)) return;
            Progress.equippedIndex = index;
            Progress.Save();
        }
    }
}
