using UnityEngine;
using UnityEngine.InputSystem;

namespace LearnToSpin
{
    /// <summary>
    /// The between-runs shop, drawn in IMGUI to match the code-only HUD (no Canvas wiring). Shows
    /// the last run's payout, the three upgrade tracks, and the tire grid (wheel icons), then a big
    /// Launch button that kicks off the next run. All money/equip logic lives on GameDirector — this
    /// is purely the screen.
    /// </summary>
    public class ShopUI : MonoBehaviour
    {
        public GameDirector director;

        GUIStyle _title, _money, _h, _body, _small, _btn, _btnSmall, _summary;
        Texture2D _white;

        // Index of the tire shown in the right-hand info column (-1 = default to equipped).
        int _selected = -1;

        static readonly string[] UpgradeBlurb =
        {
            "More boost fuel to burn mid-run.",
            "Stronger wind-up — higher launch spin.",
            "Bouncier landings carry further.",
        };

        void EnsureStyles()
        {
            if (_title != null) return;
            _white = Texture2D.whiteTexture;
            // clipping = Overflow on every label style: in a build the default font renders a touch
            // larger than in the editor, so glyph descenders were getting clipped by tight label
            // rects. Overflow lets the text spill past the rect instead of being cut off.
            _title = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, clipping = TextClipping.Overflow };
            _money = new GUIStyle(GUI.skin.label)
            { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight, clipping = TextClipping.Overflow };
            _h = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, clipping = TextClipping.Overflow };
            _body = new GUIStyle(GUI.skin.label) { fontSize = 15, clipping = TextClipping.Overflow };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true, clipping = TextClipping.Overflow };
            _summary = new GUIStyle(GUI.skin.label) { fontSize = 16, clipping = TextClipping.Overflow };
            _btn = new GUIStyle(GUI.skin.button) { fontSize = 15, fontStyle = FontStyle.Bold };
            _btnSmall = new GUIStyle(GUI.skin.button) { fontSize = 12, fontStyle = FontStyle.Bold };
        }

        void Fill(Rect r, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _white);
            GUI.color = prev;
        }

        void Outline(Rect r, float t, Color c)
        {
            Fill(new Rect(r.x, r.y, r.width, t), c);
            Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
            Fill(new Rect(r.x, r.y, t, r.height), c);
            Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
        }

        void Update()
        {
            if (director == null || !director.ShopOpen || director.GoalsOpen) return;
            var kb = Keyboard.current;
            if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
            {
                AudioManager.Instance?.PlayBtnClick();
                director.StartNextRun();
            }
        }

        void OnGUI()
        {
            if (director == null || !director.ShopOpen) { _selected = -1; return; }
            // While the objectives panel is up it owns the screen — skip drawing the shop entirely so
            // its controls can't catch clicks meant for the modal on top (GoalsUI).
            if (director.GoalsOpen) return;
            EnsureStyles();
            HudScale.Begin();

            // Default the info column to whatever's currently equipped until the player picks another.
            if (_selected < 0 || _selected >= director.Catalog.tires.Length)
                _selected = Mathf.Max(0, director.Progress.equippedIndex);

            float vw = HudScale.VW, vh = HudScale.VH;
            Fill(new Rect(0, 0, vw, vh), new Color(0.05f, 0.06f, 0.09f, 0.78f));

            float pw = Mathf.Min(1180f, vw - 40f);
            float ph = Mathf.Min(640f, vh - 40f);
            float px = (vw - pw) * 0.5f;
            float py = (vh - ph) * 0.5f;
            var panel = new Rect(px, py, pw, ph);
            Fill(panel, new Color(0.11f, 0.12f, 0.16f, 0.97f));
            Fill(new Rect(px, py, pw, 3f), new Color(1f, 0.8f, 0.2f, 0.9f));

            float pad = 22f;
            float ix = px + pad, iy = py + pad, iw = pw - pad * 2f;

            // Header — title + day + wallet
            GUI.Label(new Rect(ix, iy, iw * 0.5f, 44f), "SHOP", _title);
            GUI.Label(new Rect(ix + 110f, iy + 12f, iw * 0.4f, 28f),
                      $"End of Day {director.Progress.day}", _h);
            GUI.Label(new Rect(ix + iw * 0.4f, iy, iw * 0.6f, 44f), $"${director.Progress.money:N0}", _money);
            iy += 50f;

            iy = DrawRunSummary(ix, iy, iw) + 12f;

            // Three columns: upgrades (left), tires (middle), selected-tire info (right)
            float colGap = 24f;
            float leftW = Mathf.Min(300f, iw * 0.28f);
            float infoW = Mathf.Min(320f, iw * 0.30f);
            float midX = ix + leftW + colGap;
            float midW = iw - leftW - infoW - colGap * 2f;
            float infoX = midX + midW + colGap;
            float bottom = py + ph - pad;
            float launchH = 52f;
            float colH = bottom - iy - launchH - 14f;

            DrawUpgrades(ix, iy, leftW, colH);
            DrawTires(midX, iy, midW, colH);
            DrawTireInfo(infoX, iy, infoW, colH);

            // Footer — launch
            var launchRect = new Rect(ix, bottom - launchH, iw, launchH);
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.2f, 0.7f, 0.3f);
            if (GUI.Button(launchRect, "▶  LAUNCH   (Space / Enter)", _btn))
            {
                AudioManager.Instance?.PlayBtnClick();
                director.StartNextRun();
            }
            GUI.backgroundColor = prev;

            HudScale.End();
        }

        float DrawRunSummary(float x, float y, float w)
        {
            var s = director.LastRun;
            float btnW = 168f;
            float sumW = w - btnW - 10f;
            Fill(new Rect(x, y, sumW, 34f), new Color(1f, 1f, 1f, 0.05f));
            string text = s.valid
                ? $"Last run:   {s.distance:0} m   ·   top {s.topSpeed:0.0} m/s   ·   peak {s.maxHeight:0.0} m      "
                  + $"→   earned  ${s.earned:N0}"
                : "Buy upgrades and a tire, then launch.";
            GUI.Label(new Rect(x + 10f, y, sumW - 20f, 34f), text, _summary);

            // OBJECTIVES toggle — opens the goals panel; shows running tier count.
            int done = Objectives.TotalDone(director.Progress);
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.5f, 0.42f, 0.8f);
            if (GUI.Button(new Rect(x + w - btnW, y, btnW, 34f),
                           $"★ OBJECTIVES  {done}/{Objectives.TotalTiers}", _btnSmall))
            {
                AudioManager.Instance?.PlayBtnClick();
                director.ToggleGoals();
            }
            GUI.backgroundColor = prev;

            float ny = y + 34f;

            // Banner for any objective tiers cleared by the run that just ended.
            if (s.valid && s.newObjectives != null && s.newObjectives.Length > 0)
            {
                var nb = new Rect(x, ny + 4f, w, 24f);
                Fill(nb, new Color(1f, 0.8f, 0.2f, 0.13f));
                GUI.Label(new Rect(x + 10f, ny + 4f, w - 20f, 24f),
                          "★ Objective complete:   " + string.Join("    ·    ", s.newObjectives),
                          new GUIStyle(_summary) { fontStyle = FontStyle.Bold,
                                                   normal = { textColor = new Color(1f, 0.85f, 0.4f) } });
                ny += 30f;
            }
            return ny;
        }

        void DrawUpgrades(float x, float y, float w, float h)
        {
            Fill(new Rect(x, y, w, h), new Color(1f, 1f, 1f, 0.03f));

            // Upgrades are per-tire: they apply to whichever tire is selected in the info column.
            int tire = _selected;
            bool owned = director.Progress.Owns(tire);
            var tdef = director.Catalog.tires[tire];

            GUI.Label(new Rect(x + 12f, y + 8f, w - 24f, 24f), "UPGRADES", _h);
            GUI.Label(new Rect(x + 12f, y + 8f, w - 24f, 24f),
                      owned ? tdef.displayName : "— own to upgrade —",
                      new GUIStyle(_small) { alignment = TextAnchor.MiddleRight,
                                             normal = { textColor = owned ? new Color(1f, 0.85f, 0.4f) : Color.gray } });

            float rowH = 92f, gap = 10f;
            float ry = y + 38f;
            for (int track = 0; track < 3; track++)
            {
                DrawUpgradeRow(track, tire, owned, new Rect(x + 12f, ry, w - 24f, rowH));
                ry += rowH + gap;
            }
        }

        void DrawUpgradeRow(int track, int tire, bool owned, Rect r)
        {
            Fill(r, new Color(1f, 1f, 1f, 0.05f));
            var def = director.UpgradeDefFor(track);
            int level = director.UpgradeLevel(track, tire);
            bool maxed = level >= def.maxLevel;

            GUI.Label(new Rect(r.x + 10f, r.y + 6f, r.width - 20f, 22f), def.label, _h);

            // level pips
            float pipY = r.y + 32f, pipW = (r.width - 20f) / def.maxLevel - 3f;
            for (int i = 0; i < def.maxLevel; i++)
            {
                var pip = new Rect(r.x + 10f + i * (pipW + 3f), pipY, pipW, 8f);
                Fill(pip, i < level ? new Color(1f, 0.8f, 0.2f, 0.95f) : new Color(1f, 1f, 1f, 0.12f));
            }

            GUI.Label(new Rect(r.x + 10f, r.y + 44f, r.width - 20f, 18f),
                      UpgradeBlurb[track], _small);

            var btnRect = new Rect(r.x + 10f, r.y + r.height - 28f, r.width - 20f, 24f);
            var prev = GUI.backgroundColor;

            // Can only upgrade an owned tire — otherwise prompt to buy it first.
            if (!owned)
            {
                GUI.enabled = false;
                GUI.Button(btnRect, "Own tire to upgrade", _btn);
                GUI.enabled = true;
                return;
            }
            if (maxed)
            {
                GUI.Label(btnRect, "MAXED", _body);
                return;
            }
            int cost = director.UpgradeCost(track, tire);
            bool afford = director.Progress.money >= cost;
            GUI.backgroundColor = afford ? new Color(0.25f, 0.55f, 0.85f) : new Color(0.4f, 0.4f, 0.4f);
            GUI.enabled = afford;
            if (GUI.Button(btnRect, $"Upgrade  —  ${cost:N0}", _btn))
            {
                AudioManager.Instance?.PlayBtnClick();
                director.TryBuyUpgrade(track, tire);
            }
            GUI.enabled = true;
            GUI.backgroundColor = prev;
        }

        void DrawTires(float x, float y, float w, float h)
        {
            Fill(new Rect(x, y, w, h), new Color(1f, 1f, 1f, 0.03f));
            GUI.Label(new Rect(x + 12f, y + 8f, w - 24f, 24f), "TIRES", _h);

            var tires = director.Catalog.tires;
            int cols = 4;
            int rows = Mathf.CeilToInt(tires.Length / (float)cols);
            float gridX = x + 12f, gridY = y + 38f;
            float gridW = w - 24f, gridH = h - 50f;
            float cellGap = 8f;
            float cellW = (gridW - cellGap * (cols - 1)) / cols;
            float cellH = (gridH - cellGap * (rows - 1)) / rows;

            for (int i = 0; i < tires.Length; i++)
            {
                int cx = i % cols, cy = i / cols;
                var cell = new Rect(gridX + cx * (cellW + cellGap), gridY + cy * (cellH + cellGap), cellW, cellH);
                DrawTireCell(i, cell);
            }
        }

        void DrawTireCell(int i, Rect r)
        {
            var def = director.Catalog.tires[i];
            bool owned = director.Progress.Owns(i);
            bool equipped = director.Progress.equippedIndex == i;
            bool afford = director.Progress.money >= def.price;

            bool selected = _selected == i;
            Fill(r, equipped ? new Color(1f, 0.8f, 0.2f, 0.18f) : new Color(1f, 1f, 1f, 0.05f));
            if (equipped) Outline(r, 2f, new Color(1f, 0.8f, 0.2f, 1f));
            // Selected (shown in the info panel) gets a thin white outline, inset so it reads
            // alongside the gold "equipped" border rather than fighting it.
            if (selected && !equipped) Outline(r, 2f, new Color(0.9f, 0.95f, 1f, 0.9f));

            // icon (or coloured swatch fallback)
            float iconSize = Mathf.Min(r.width - 16f, r.height - 52f);
            var iconRect = new Rect(r.x + (r.width - iconSize) * 0.5f, r.y + 6f, iconSize, iconSize);
            if (def.icon != null)
            {
                if (!owned) GUI.color = new Color(0.55f, 0.55f, 0.6f, 1f); // dim the locked ones
                GUI.DrawTexture(iconRect, def.icon, ScaleMode.ScaleToFit);
                GUI.color = Color.white;
            }
            else
            {
                Fill(iconRect, owned ? new Color(0.3f, 0.55f, 0.8f, 0.6f) : new Color(0.4f, 0.4f, 0.45f, 0.5f));
            }

            GUI.Label(new Rect(r.x + 4f, r.yMax - 44f, r.width - 8f, 18f), def.displayName,
                      new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold });

            // action line
            var actRect = new Rect(r.x + 6f, r.yMax - 26f, r.width - 12f, 22f);
            var prev = GUI.backgroundColor;
            if (equipped)
            {
                GUI.Label(actRect, "Equipped",
                    new GUIStyle(_body) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold });
            }
            else if (owned)
            {
                GUI.backgroundColor = new Color(0.25f, 0.55f, 0.85f);
                if (GUI.Button(actRect, "Equip", _btnSmall))
                {
                    AudioManager.Instance?.PlayBtnClick();
                    director.EquipTire(i);
                }
            }
            else
            {
                GUI.backgroundColor = afford ? new Color(0.2f, 0.6f, 0.3f) : new Color(0.45f, 0.4f, 0.4f);
                GUI.enabled = afford;
                if (GUI.Button(actRect, $"${def.price:N0}", _btnSmall))
                {
                    AudioManager.Instance?.PlayBtnClick();
                    director.TryBuyTire(i);
                }
                GUI.enabled = true;
            }
            GUI.backgroundColor = prev;

            // Cell-wide select: clicking anywhere not already claimed by the action button above
            // makes this the tire shown in the right-hand info panel. Drawn LAST so the earlier
            // Equip/Buy button grabs its own clicks first (IMGUI gives the click to the first
            // overlapping control in draw order).
            if (GUI.Button(r, GUIContent.none, GUIStyle.none)) _selected = i;
        }

        // ---- Selected-tire info column (blown-up icon + stat bars), baked into the shop ----

        void DrawTireInfo(float x, float y, float w, float h)
        {
            Fill(new Rect(x, y, w, h), new Color(1f, 1f, 1f, 0.03f));
            GUI.Label(new Rect(x + 12f, y + 8f, w - 24f, 24f), "TIRE INFO", _h);

            int idx = _selected;
            if (idx < 0 || idx >= director.Catalog.tires.Length) return;
            var def = director.Catalog.tires[idx];
            bool owned = director.Progress.Owns(idx);
            bool equipped = director.Progress.equippedIndex == idx;

            float pad = 12f;
            float ix = x + pad, iw = w - pad * 2f;
            float iy = y + 36f;

            // Blown-up icon.
            float iconBox = Mathf.Min(iw, 96f);
            var iconRect = new Rect(x + (w - iconBox) * 0.5f, iy, iconBox, iconBox);
            Fill(iconRect, new Color(1f, 1f, 1f, 0.04f));
            if (def.icon != null)
            {
                if (!owned) GUI.color = new Color(0.6f, 0.6f, 0.65f, 1f);
                GUI.DrawTexture(iconRect, def.icon, ScaleMode.ScaleToFit);
                GUI.color = Color.white;
            }
            else Fill(iconRect, new Color(0.3f, 0.55f, 0.8f, 0.5f));
            iy = iconRect.yMax + 8f;

            // Name + status.
            GUI.Label(new Rect(ix, iy, iw, 24f), def.displayName,
                      new GUIStyle(_h) { alignment = TextAnchor.MiddleCenter });
            iy += 24f;
            string status = equipped ? "Equipped" : owned ? "Owned" : $"Locked — ${def.price:N0}";
            GUI.Label(new Rect(ix, iy, iw, 18f), status,
                      new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter });
            iy += 22f;

            // Stat bars (include current upgrades).
            var eff = director.EffectiveFor(idx);
            iy = StatBar(ix, iy, iw, "Weight", def.mass, 14f, 40f, "{0:0} kg");
            iy = StatBar(ix, iy, iw, "Size", def.radius, 0.4f, 0.75f, "{0:0.00} m");
            iy = StatBar(ix, iy, iw, "Bounce", eff.bounciness, 0f, 1f, "{0:0.00}");
            // Ranges run well past the stock-tire numbers so a fully-upgraded premium tire reads as
            // a near-full bar (borderline OP) rather than pegging the meter at a mid tier.
            iy = StatBar(ix, iy, iw, "Launch Spin", eff.idealSpin, 80f, 560f, "{0:0}");
            iy = StatBar(ix, iy, iw, "Rev Ceiling", eff.maxSpin, 100f, 580f, "{0:0}");
            iy = StatBar(ix, iy, iw, "Boost Fuel", eff.boostReserve, 90f, 1050f, "{0:0}");
            // Earnings: how much MORE cash this tire banks per run stat than the starter. Tier-based
            // (not upgraded), so it's the headline reason to keep climbing the tire ladder.
            float em = def.earnMultiplier > 0.01f ? def.earnMultiplier : 1f;
            iy = StatBar(ix, iy, iw, "Earnings", em, 1f, 16f, "×{0:0.0}");
            iy += 4f;
            GUI.Label(new Rect(ix, iy, iw, 28f), "(bars include upgrades; Earnings is per-tier)", _small);
        }

        /// <summary>One labelled stat bar normalised between lo..hi, returns the next y.</summary>
        float StatBar(float x, float y, float w, string label, float value, float lo, float hi, string fmt)
        {
            GUI.Label(new Rect(x, y, w * 0.5f, 18f), label, _small);
            GUI.Label(new Rect(x + w * 0.5f, y, w * 0.5f, 18f),
                      string.Format(fmt, value),
                      new GUIStyle(_small) { alignment = TextAnchor.MiddleRight });
            var track = new Rect(x, y + 18f, w, 7f);
            Fill(track, new Color(1f, 1f, 1f, 0.08f));
            float n = Mathf.Clamp01((value - lo) / Mathf.Max(0.0001f, hi - lo));
            Fill(new Rect(track.x, track.y, track.width * n, track.height), new Color(1f, 0.8f, 0.2f, 0.9f));
            return y + 30f;
        }
    }
}
