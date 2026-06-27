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
        public string[] newObjectives; // objective tiers newly cleared by THIS run (for a results/shop banner)
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

        /// <summary>True while the victory screen is up (every objective tier cleared).</summary>
        public bool WinOpen { get; private set; }
        /// <summary>True while the objectives panel is layered over the shop.</summary>
        public bool GoalsOpen { get; private set; }
        /// <summary>True while the one-time intro cutscene is playing (start of a fresh profile).</summary>
        public bool IntroOpen { get; private set; }

        // Set on the run that completes the LAST objective, so the win screen fires after the player
        // has seen that run's results (Continue → win screen instead of straight to the shop).
        bool _pendingWin;

        /// <summary>The live tire launcher for the current run (reassigned each run). Lets the pause
        /// menu freeze gameplay input without holding a stale reference across day rebuilds.</summary>
        public TireLauncher Launcher => _launcher;

        /// <summary>True only during active flying — i.e. no meta screen is up. The pause menu uses
        /// this so it can't open on top of the results/shop/transition screens.</summary>
        public bool InActiveRun => !ResultsOpen && !ShopOpen && !TransitionOpen && !WinOpen && !IntroOpen;

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

            // Objectives panel (layered over the shop) + the victory screen.
            var goals = new GameObject("GoalsUI").AddComponent<GoalsUI>();
            goals.director = this;

            // Live top-right objectives tracker (hidden on the meta screens).
            var objHud = new GameObject("ObjectivesHudUI").AddComponent<ObjectivesHudUI>();
            objHud.director = this;

            var win = new GameObject("WinUI").AddComponent<WinUI>();
            win.director = this;

            // Esc pause overlay (Resume / Main Menu / Quit).
            var pause = new GameObject("PauseMenuUI").AddComponent<PauseMenuUI>();
            pause.director = this;

            // One-time intro cutscene (comic-strip story) before the first launch of a fresh profile.
            // It freezes input and hides the run HUD while it plays; EndIntro hands control back.
            var intro = new GameObject("IntroCutsceneUI").AddComponent<IntroCutsceneUI>();
            intro.director = this;
            if (!Progress.introSeen) BeginIntro(intro);
            else if (AudioManager.Instance != null) AudioManager.Instance.SetMusicMode(1);
        }

        /// <summary>Start the one-time intro cutscene: freeze the launcher and run the menu music
        /// bed under it. <see cref="EndIntro"/> tears it back down when the player finishes/skips.</summary>
        void BeginIntro(IntroCutsceneUI intro)
        {
            IntroOpen = true;
            if (_launcher != null) _launcher.enabled = false; // no revving until the story is over
            if (AudioManager.Instance != null) AudioManager.Instance.SetMusicMode(0);
            intro.Play();
        }

        /// <summary>Intro cutscene finished (or was skipped): mark it seen, restore gameplay music,
        /// and hand control back to the player for their first launch.</summary>
        public void EndIntro()
        {
            IntroOpen = false;
            Progress.introSeen = true;
            Progress.Save();
            if (_launcher != null) _launcher.enabled = true;
            if (AudioManager.Instance != null) AudioManager.Instance.SetMusicMode(1);
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

            // ---- Objectives: fold this run into the lifetime bests, diff for newly-cleared tiers,
            // and detect the win (every tier of every stat now complete). ----
            bool[,] doneBefore = new bool[Objectives.StatCount, Objectives.TierCount];
            for (int st = 0; st < Objectives.StatCount; st++)
                for (int ti = 0; ti < Objectives.TierCount; ti++)
                    doneBefore[st, ti] = Objectives.TierDone(Progress, st, ti);

            Progress.RecordBests(_launcher.Distance, _launcher.TopSpeed,
                                 _launcher.MaxHeight, _launcher.LongestAirTime);

            var newly = new System.Collections.Generic.List<string>();
            for (int st = 0; st < Objectives.StatCount; st++)
                for (int ti = 0; ti < Objectives.TierCount; ti++)
                    if (!doneBefore[st, ti] && Objectives.TierDone(Progress, st, ti))
                        newly.Add(Objectives.Label(st, ti));

            bool justWon = !Progress.won && Objectives.AllComplete(Progress);
            if (justWon) { Progress.won = true; Progress.winDay = Progress.day; }
            _pendingWin = justWon;

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
                newObjectives = newly.ToArray(),
                valid = true,
            };
            // Bank + save now so the payout (and any objective/win progress) survives even if the
            // player quits mid-animation; the results screen counts the wallet up for show.
            Progress.money += earned;
            Progress.Save();
            OpenResults();
        }

        void OpenResults()
        {
            ResultsOpen = true;
            ShopOpen = false;
            if (_launcher != null) _launcher.enabled = false; // freeze input (R/Space) while the screens are up

            // AUDIO: Switch to Menu Music (0)
            if (AudioManager.Instance != null) AudioManager.Instance.SetMusicMode(0);
        }

        /// <summary>Results screen "Continue": go to the win screen if this run completed the final
        /// objective, otherwise hand off to the shop.</summary>
        public void ContinueToShop()
        {
            ResultsOpen = false;
            if (_pendingWin) { _pendingWin = false; WinOpen = true; return; }
            ShopOpen = true;
        }

        /// <summary>Win screen "Keep Playing": drop into the shop and carry on (free play after the win).</summary>
        public void DismissWin()
        {
            WinOpen = false;
            ShopOpen = true;
        }

        /// <summary>Toggle the objectives panel over the shop (the shop's OBJECTIVES button).</summary>
        public void ToggleGoals() => GoalsOpen = !GoalsOpen;
        public void CloseGoals() => GoalsOpen = false;

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
            GoalsOpen = false;

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

            if (AudioManager.Instance != null) AudioManager.Instance.SetMusicMode(1);
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
