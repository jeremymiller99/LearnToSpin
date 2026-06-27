using UnityEngine;
using UnityEngine.InputSystem;

namespace LearnToSpin
{
    /// <summary>
    /// The objectives panel, opened from the shop's OBJECTIVES button. Lists the four tracked stats,
    /// each with its five escalating tiers (cleared = gold check, locked = grey), the lifetime best,
    /// and a bar showing progress toward the next uncleared tier. Cleared everything → the win screen
    /// has already fired; here it just reads as 12/12 complete. Modal: while open, the shop pauses its
    /// own input (see <see cref="ShopUI"/>) so this overlay owns the screen.
    /// </summary>
    public class GoalsUI : MonoBehaviour
    {
        public GameDirector director;

        GUIStyle _title, _h, _body, _small, _btn, _pill;
        Texture2D _white;

        static readonly Color Gold = new Color(1f, 0.8f, 0.2f);

        void EnsureStyles()
        {
            if (_title != null) return;
            _white = Texture2D.whiteTexture;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold };
            _h = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            _body = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            _btn = new GUIStyle(GUI.skin.button) { fontSize = 15, fontStyle = FontStyle.Bold };
            _pill = new GUIStyle(GUI.skin.label)
            { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        }

        void Fill(Rect r, Color c)
        {
            var prev = GUI.color; GUI.color = c; GUI.DrawTexture(r, _white); GUI.color = prev;
        }

        void Update()
        {
            if (director == null || !director.GoalsOpen) return;
            var kb = Keyboard.current;
            if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.tabKey.wasPressedThisFrame))
            {
                AudioManager.Instance?.PlayBtnClick();
                director.CloseGoals();
            }
        }

        void OnGUI()
        {
            if (director == null || !director.GoalsOpen) return;
            EnsureStyles();
            GUI.depth = -60; // above the shop, below the win screen
            HudScale.Begin();

            float vw = HudScale.VW, vh = HudScale.VH;
            Fill(new Rect(0, 0, vw, vh), new Color(0.04f, 0.05f, 0.08f, 0.9f));

            float pw = Mathf.Min(760f, vw - 40f);
            float ph = Mathf.Min(560f, vh - 40f);
            float px = (vw - pw) * 0.5f, py = (vh - ph) * 0.5f;
            Fill(new Rect(px, py, pw, ph), new Color(0.11f, 0.12f, 0.16f, 0.98f));
            Fill(new Rect(px, py, pw, 3f), Gold);

            var p = director.Progress;
            float pad = 24f;
            float ix = px + pad, iw = pw - pad * 2f, iy = py + pad;

            GUI.Label(new Rect(ix, iy, iw * 0.6f, 38f), "OBJECTIVES", _title);
            int done = Objectives.TotalDone(p);
            GUI.Label(new Rect(ix + iw * 0.5f, iy + 6f, iw * 0.5f, 28f),
                      $"{done} / {Objectives.TotalTiers} complete",
                      new GUIStyle(_h) { alignment = TextAnchor.MiddleRight,
                                         normal = { textColor = done == Objectives.TotalTiers ? Gold : Color.white } });
            iy += 40f;
            GUI.Label(new Rect(ix, iy, iw, 20f),
                      "Clear every tier of all four to win.",
                      _small);
            iy += 26f;

            float bottom = py + ph - pad - 44f; // leave room for the close button
            float rowGap = 10f;
            float rowH = (bottom - iy - rowGap * (Objectives.StatCount - 1)) / Objectives.StatCount;
            for (int s = 0; s < Objectives.StatCount; s++)
            {
                DrawStatRow(s, new Rect(ix, iy, iw, rowH));
                iy += rowH + rowGap;
            }

            var prev = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.28f, 0.5f, 0.78f);
            if (GUI.Button(new Rect(ix, py + ph - pad - 38f, iw, 38f), "CLOSE  (Esc)", _btn))
            {
                AudioManager.Instance?.PlayBtnClick();
                director.CloseGoals();
            }
            GUI.backgroundColor = prev;

            HudScale.End();
        }

        void DrawStatRow(int stat, Rect r)
        {
            var p = director.Progress;
            Fill(r, new Color(1f, 1f, 1f, 0.04f));
            float best = Objectives.Best(p, stat);
            int tiersDone = Objectives.TiersDone(p, stat);

            float pad = 12f;
            float x = r.x + pad, w = r.width - pad * 2f;

            // Header: stat name (with a check if all tiers are done) + lifetime best.
            bool allDone = tiersDone >= Objectives.TierCount;
            GUI.Label(new Rect(x, r.y + 6f, w * 0.6f, 22f),
                      (allDone ? "✔  " : "") + Objectives.Titles[stat],
                      new GUIStyle(_h) { normal = { textColor = allDone ? Gold : Color.white } });
            GUI.Label(new Rect(x + w * 0.5f, r.y + 6f, w * 0.5f, 22f),
                      $"best  {Objectives.Format(stat, best)}",
                      new GUIStyle(_body) { alignment = TextAnchor.MiddleRight });

            // Tier pills side by side, one per tier.
            float pillY = r.y + 32f, pillH = r.height - 40f;
            float gap = 8f;
            float pillW = (w - gap * (Objectives.TierCount - 1)) / Objectives.TierCount;
            for (int t = 0; t < Objectives.TierCount; t++)
            {
                var pr = new Rect(x + t * (pillW + gap), pillY, pillW, pillH);
                DrawTierPill(stat, t, best, pr);
            }
        }

        void DrawTierPill(int stat, int tier, float best, Rect r)
        {
            float threshold = Objectives.Threshold(stat, tier);
            bool cleared = best >= threshold;
            bool prevCleared = tier == 0 || best >= Objectives.Threshold(stat, tier - 1);

            Fill(r, cleared ? new Color(1f, 0.8f, 0.2f, 0.18f) : new Color(1f, 1f, 1f, 0.04f));
            if (cleared)
            {
                Fill(new Rect(r.x, r.y, r.width, 2f), Gold);
                Fill(new Rect(r.x, r.yMax - 2f, r.width, 2f), Gold);
            }

            // "Tier 1   1000 m   ✓ / 🔒"
            GUI.Label(new Rect(r.x + 8f, r.y + 4f, r.width - 16f, 18f),
                      $"Tier {tier + 1}",
                      new GUIStyle(_small) { normal = { textColor = cleared ? Gold : new Color(0.7f, 0.72f, 0.78f) } });
            GUI.Label(new Rect(r.x + 8f, r.y + 4f, r.width - 16f, 18f),
                      cleared ? "✔" : "○",
                      new GUIStyle(_pill) { alignment = TextAnchor.MiddleRight,
                                            normal = { textColor = cleared ? Gold : new Color(0.55f, 0.57f, 0.62f) } });

            GUI.Label(new Rect(r.x + 8f, r.y + 22f, r.width - 16f, 20f),
                      Objectives.Format(stat, threshold),
                      new GUIStyle(_body) { fontStyle = FontStyle.Bold,
                                            normal = { textColor = cleared ? Color.white : new Color(0.8f, 0.82f, 0.88f) } });

            // Progress bar toward THIS tier (full when cleared). Only the first uncleared tier shows
            // partial fill — earlier tiers are full, later tiers read as not-yet-started.
            var track = new Rect(r.x + 8f, r.yMax - 12f, r.width - 16f, 6f);
            Fill(track, new Color(1f, 1f, 1f, 0.08f));
            float lo = tier == 0 ? 0f : Objectives.Threshold(stat, tier - 1);
            float n = cleared ? 1f : prevCleared ? Mathf.Clamp01((best - lo) / Mathf.Max(0.0001f, threshold - lo)) : 0f;
            Fill(new Rect(track.x, track.y, track.width * n, track.height),
                 cleared ? Gold : new Color(0.3f, 0.6f, 0.85f, 0.9f));
        }
    }
}
