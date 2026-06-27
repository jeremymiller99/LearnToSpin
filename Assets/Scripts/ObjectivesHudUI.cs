using System.Collections.Generic;
using UnityEngine;

namespace LearnToSpin
{
    /// <summary>
    /// Compact live objectives tracker pinned to the TOP-RIGHT of the run HUD: the four tracked stats,
    /// each with its five tier pips (lit as cleared) and a bar showing progress toward the next
    /// uncleared tier. Progress uses max(lifetime best, this run's value), so a pip lights the instant
    /// you cross a threshold mid-flight. Hidden whenever a meta screen is up (shop / results / day card
    /// / win), matching <see cref="LaunchUI"/> — it only shows while you're actually flying.
    ///
    /// Crossing a threshold mid-flight also fires a juicy top-middle "OBJECTIVE COMPLETE!" toast (pop-in
    /// + glow + fade) and flashes the panel's gold outline. Completions are detected live here (not just
    /// at run-end) so the celebration lands the moment you earn it. Spawned by <see cref="GameDirector"/>;
    /// reads the live launcher off the director so it survives day rebuilds.
    /// </summary>
    public class ObjectivesHudUI : MonoBehaviour
    {
        public GameDirector director;

        GUIStyle _head, _count, _label, _target, _done, _toastBig, _toastSmall;
        Texture2D _white;

        static readonly Color Gold = new Color(1f, 0.8f, 0.2f);
        static readonly Color Dim = new Color(0.62f, 0.66f, 0.72f);

        // ---- Live completion tracking ----
        // Monotonic "already celebrated" mask, seeded once from the save's lifetime bests so loading a
        // profile mid-progress doesn't replay old completions. A tier only ever flips false→true.
        bool[,] _cleared;
        bool _seeded;

        // Toast queue: one completion shown at a time (handles two tiers crossing in the same instant).
        readonly Queue<string> _queue = new Queue<string>();
        string _toastLabel;
        float _toastT;
        const float ToastDur = 2.6f;

        // Panel-outline flash, refreshed on each completion.
        float _flash;
        const float FlashDur = 0.8f;

        void EnsureStyles()
        {
            if (_head != null) return;
            _white = Texture2D.whiteTexture;
            // clipping = Overflow on every text style: in a build the default font renders a touch
            // larger than in the editor, so glyph descenders were getting clipped by these tight
            // label rects. Overflow lets the text spill past the rect instead of being cut off.
            _head = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, clipping = TextClipping.Overflow };
            _count = new GUIStyle(GUI.skin.label)
            { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight, clipping = TextClipping.Overflow };
            _label = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, clipping = TextClipping.Overflow };
            _target = new GUIStyle(GUI.skin.label)
            { fontSize = 11, alignment = TextAnchor.MiddleRight, clipping = TextClipping.Overflow };
            _done = new GUIStyle(GUI.skin.label)
            { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight, clipping = TextClipping.Overflow };
            _toastBig = new GUIStyle(GUI.skin.label)
            { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
              wordWrap = false, clipping = TextClipping.Overflow };
            _toastSmall = new GUIStyle(GUI.skin.label)
            { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
              wordWrap = false, clipping = TextClipping.Overflow };
        }

        void Fill(Rect r, Color c)
        {
            var prev = GUI.color; GUI.color = c; GUI.DrawTexture(r, _white); GUI.color = prev;
        }

        static float Smooth(float p) { p = Mathf.Clamp01(p); return p * p * (3f - 2f * p); }

        /// <summary>Overshooting ease for the pop-in punch.</summary>
        static float EaseOutBack(float p)
        {
            p = Mathf.Clamp01(p);
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float u = p - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        /// <summary>This run's live value for a stat (matches what RecordBests will bank at run-end).</summary>
        float LiveCurrent(int stat)
        {
            var l = director.Launcher;
            if (l == null) return 0f;
            return stat == 0 ? l.Distance
                 : stat == 1 ? l.TopSpeed
                 : stat == 2 ? l.MaxHeight
                 :             l.LongestAirTime;
        }

        void Update()
        {
            if (director == null) return;
            var p = director.Progress;
            if (p == null) return;

            if (_cleared == null) _cleared = new bool[Objectives.StatCount, Objectives.TierCount];
            if (!_seeded)
            {
                // Seed from existing lifetime completions so a loaded save doesn't spam popups.
                for (int s = 0; s < Objectives.StatCount; s++)
                    for (int t = 0; t < Objectives.TierCount; t++)
                        _cleared[s, t] = Objectives.TierDone(p, s, t);
                _seeded = true;
            }

            // Detect newly-crossed tiers — only while actually flying.
            if (director.InActiveRun)
            {
                bool any = false;
                for (int s = 0; s < Objectives.StatCount; s++)
                {
                    float eff = Mathf.Max(Objectives.Best(p, s), LiveCurrent(s));
                    for (int t = 0; t < Objectives.TierCount; t++)
                        if (!_cleared[s, t] && eff >= Objectives.Threshold(s, t))
                        {
                            _cleared[s, t] = true;
                            _queue.Enqueue(Objectives.Label(s, t));
                            any = true;
                        }
                }
                if (any)
                {
                    _flash = FlashDur;
                    if (AudioManager.Instance != null) AudioManager.Instance.PlayTrickNice();
                }
            }

            // Advance the active toast / pull the next from the queue.
            if (_toastLabel == null && _queue.Count > 0) { _toastLabel = _queue.Dequeue(); _toastT = 0f; }
            if (_toastLabel != null)
            {
                _toastT += Time.unscaledDeltaTime;
                if (_toastT >= ToastDur) _toastLabel = null;
            }
            if (_flash > 0f) _flash -= Time.unscaledDeltaTime;
        }

        void OnGUI()
        {
            if (director == null) return;
            // Only while flying — the meta screens (shop/results/day card/win) draw their own panels.
            if (director.ShopOpen || director.ResultsOpen || director.TransitionOpen || director.WinOpen || director.IntroOpen)
                return;
            EnsureStyles();
            HudScale.Begin();

            DrawPanel();
            DrawToast();

            HudScale.End();
        }

        void DrawPanel()
        {
            const float w = 250f, pad = 12f, headH = 24f, rowH = 46f;
            int n = Objectives.StatCount;
            float h = pad + headH + rowH * n + pad - 6f;
            float x = HudScale.VW - w - 24f, y = 24f;
            var panel = new Rect(x, y, w, h);

            // Gold glow ring while flashing (pulses out from the border on a completion).
            float fk = Mathf.Clamp01(_flash / FlashDur);
            if (fk > 0f)
            {
                for (int i = 4; i >= 1; i--)
                {
                    float g = i * 4f;
                    Fill(new Rect(x - g, y - g, w + g * 2f, h + g * 2f),
                         new Color(1f, 0.82f, 0.25f, 0.05f * fk * (5 - i)));
                }
            }

            Fill(panel, new Color(0.04f, 0.05f, 0.08f, 0.72f));

            // Outline — brightens and thickens during the flash.
            Color edge = Color.Lerp(Gold, Color.white, 0.65f * fk);
            float bw = Mathf.Lerp(2f, 4f, fk);
            Fill(new Rect(x, y, w, bw), edge);                       // top
            Fill(new Rect(x, y + h - bw, w, bw), edge);              // bottom
            Fill(new Rect(x, y, bw, h), edge);                       // left
            Fill(new Rect(x + w - bw, y, bw, h), edge);              // right

            var p = director.Progress;
            int done = Objectives.TotalDone(p);

            var hprev = _head.normal.textColor; _head.normal.textColor = Gold;
            GUI.Label(new Rect(x + pad, y + 4f, w - pad * 2f, 20f), "OBJECTIVES", _head);
            _head.normal.textColor = hprev;
            var cprev = _count.normal.textColor;
            _count.normal.textColor = done == Objectives.TotalTiers ? Gold : Color.white;
            GUI.Label(new Rect(x + pad, y + 4f, w - pad * 2f, 20f),
                      $"{done}/{Objectives.TotalTiers}", _count);
            _count.normal.textColor = cprev;

            float ry = y + pad + headH - 4f;
            for (int s = 0; s < n; s++)
            {
                DrawStatRow(s, new Rect(x + pad, ry, w - pad * 2f, rowH), p);
                ry += rowH;
            }
        }

        void DrawStatRow(int stat, Rect r, PlayerProgress p)
        {
            float eff = Mathf.Max(Objectives.Best(p, stat), LiveCurrent(stat));

            // Which tier are we working on (first not yet reached), and how many are cleared.
            int next = -1, cleared = 0;
            for (int t = 0; t < Objectives.TierCount; t++)
            {
                if (eff >= Objectives.Threshold(stat, t)) cleared++;
                else if (next < 0) next = t;
            }
            bool allDone = next < 0;

            // Label (left) + status/target (right).
            var lprev = _label.normal.textColor;
            _label.normal.textColor = allDone ? Gold : Color.white;
            GUI.Label(new Rect(r.x, r.y, r.width * 0.5f, 16f), Objectives.Titles[stat], _label);
            _label.normal.textColor = lprev;

            if (allDone)
            {
                var dprev = _done.normal.textColor; _done.normal.textColor = Gold;
                GUI.Label(new Rect(r.x, r.y, r.width, 16f), "✔ COMPLETE", _done);
                _done.normal.textColor = dprev;
            }
            else
            {
                float target = Objectives.Threshold(stat, next);
                var tprev = _target.normal.textColor; _target.normal.textColor = Dim;
                GUI.Label(new Rect(r.x, r.y + 1f, r.width, 14f),
                          $"{Objectives.FormatNumber(stat, eff)} / {Objectives.Format(stat, target)}", _target);
                _target.normal.textColor = tprev;
            }

            // One pip per tier.
            float pipS = 8f, pipGap = 4f;
            float pipsW = Objectives.TierCount * pipS + (Objectives.TierCount - 1) * pipGap;
            float pipY = r.y + 19f;
            for (int t = 0; t < Objectives.TierCount; t++)
            {
                var pr = new Rect(r.x + t * (pipS + pipGap), pipY, pipS, pipS);
                bool lit = eff >= Objectives.Threshold(stat, t);
                Fill(pr, lit ? Gold : new Color(1f, 1f, 1f, 0.14f));
            }

            // Progress bar toward the next tier (full when all cleared).
            var track = new Rect(r.x + pipsW + 10f, r.y + 20f, r.width - pipsW - 10f, 5f);
            Fill(track, new Color(1f, 1f, 1f, 0.10f));
            float lo = next <= 0 ? 0f : Objectives.Threshold(stat, next - 1);
            float hi = allDone ? 1f : Objectives.Threshold(stat, next);
            float fill = allDone ? 1f : Mathf.Clamp01((eff - lo) / Mathf.Max(0.0001f, hi - lo));
            Fill(new Rect(track.x, track.y, track.width * fill, track.height),
                 allDone ? Gold : new Color(0.3f, 0.62f, 0.9f, 0.95f));
        }

        /// <summary>The juicy top-middle "OBJECTIVE COMPLETE!" popup: pop-in punch, glow, then fade.</summary>
        void DrawToast()
        {
            if (_toastLabel == null) return;
            float t = _toastT;

            // Fade in fast, hold, fade out over the last 0.55s.
            float alphaIn = Smooth(t / 0.22f);
            float alphaOut = 1f - Mathf.Clamp01((t - (ToastDur - 0.55f)) / 0.55f);
            float alpha = Mathf.Min(alphaIn, alphaOut);
            if (alpha <= 0.001f) return;

            float pop = EaseOutBack(t / 0.34f);                 // overshoot punch on entry
            float scale = Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(pop));
            float rise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - (ToastDur - 0.55f)) / 0.55f)) * 18f;

            float vw = HudScale.VW;
            float bw = 440f, bh = 84f;
            float bx = (vw - bw) * 0.5f;
            float by = 52f - rise;
            var box = new Rect(bx, by, bw, bh);
            var center = new Vector2(bx + bw * 0.5f, by + bh * 0.5f);

            Matrix4x4 m = GUI.matrix;
            GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), center);

            // Soft gold glow behind the banner.
            for (int i = 5; i >= 1; i--)
            {
                float g = i * 6f;
                Fill(new Rect(box.x - g, box.y - g, box.width + g * 2f, box.height + g * 2f),
                     new Color(1f, 0.82f, 0.25f, 0.045f * alpha * (6 - i)));
            }

            // Banner body + gold rails.
            Fill(box, new Color(0.06f, 0.07f, 0.10f, 0.92f * alpha));
            Fill(new Rect(box.x, box.y, box.width, 3f), new Color(Gold.r, Gold.g, Gold.b, alpha));
            Fill(new Rect(box.x, box.yMax - 3f, box.width, 3f), new Color(Gold.r, Gold.g, Gold.b, alpha));

            // Entry flash — a quick white wash that fades over the first ~0.18s.
            float flashA = (1f - Smooth(t / 0.18f)) * 0.5f;
            if (flashA > 0.001f) Fill(box, new Color(1f, 1f, 1f, flashA));

            var bprev = _toastBig.normal.textColor;
            _toastBig.normal.textColor = new Color(1f, 0.86f, 0.32f, alpha);
            GUI.Label(new Rect(box.x, box.y + 10f, box.width, 34f), "★  OBJECTIVE COMPLETE  ★", _toastBig);
            _toastBig.normal.textColor = bprev;

            var sprev = _toastSmall.normal.textColor;
            _toastSmall.normal.textColor = new Color(0.95f, 0.97f, 1f, alpha);
            GUI.Label(new Rect(box.x, box.y + 44f, box.width, 30f), _toastLabel, _toastSmall);
            _toastSmall.normal.textColor = sprev;

            GUI.matrix = m;
        }
    }
}
