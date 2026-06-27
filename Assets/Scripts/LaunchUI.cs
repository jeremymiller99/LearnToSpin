using UnityEngine;

namespace LearnToSpin
{
    /// <summary>
    /// IMGUI HUD for the run. Surfaces the two skill systems (rev power-band + boost reserve) and
    /// a stat panel showing the live DISTANCE / SPEED / HEIGHT. The stat panel is themed off the
    /// equipped tire: every tire gets its own accent colour AND its own panelling — a tier badge,
    /// progress pips, and a stack of structural flourishes (inner frame, row dividers, spine rivets,
    /// glow, segmented bars, gradients, a second rail, corner brackets, a scan line) that switch on
    /// roughly one per tier, so the HUD is visibly more advanced on a better tire rather than just
    /// recoloured. Code-only so the prototype needs zero Canvas wiring.
    /// </summary>
    public class LaunchUI : MonoBehaviour
    {
        public TireLauncher launcher;
        [Tooltip("Set by GameDirector — used to hide the in-run HUD while the shop is open.")]
        public GameDirector director;

        GUIStyle _value, _label, _name, _mid, _perfect, _hint, _badge;
        Texture2D _white;

        // ---- Equipped-tire theme (set by GameDirector each run) ----
        HudTheme _theme = HudTheme.ForTier(0, 1, "Tire");

        /// <summary>Called by GameDirector when a run starts so the HUD reflects the equipped tire.</summary>
        public void SetTire(int index, int count, string displayName)
            => _theme = HudTheme.ForTier(index, count, displayName);

        void EnsureStyles()
        {
            if (_value != null) return;
            // clipping = Overflow on every text style: in a build the default font renders a touch
            // larger than in the editor, so glyph descenders were getting clipped by these tight
            // label rects. Overflow lets the text spill past the rect instead of being cut off.
            _value = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, clipping = TextClipping.Overflow };
            _label = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, clipping = TextClipping.Overflow };
            _name = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, clipping = TextClipping.Overflow };
            _mid = new GUIStyle(GUI.skin.label) { fontSize = 18, clipping = TextClipping.Overflow };
            _perfect = new GUIStyle(GUI.skin.label)
            { fontSize = 40, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Overflow };
            _hint = new GUIStyle(GUI.skin.label)
            { fontSize = 13, alignment = TextAnchor.UpperRight, clipping = TextClipping.Overflow };
            _badge = new GUIStyle(GUI.skin.label)
            { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight, clipping = TextClipping.Overflow };
            _white = Texture2D.whiteTexture;
        }

        void Bar(Rect r, Color c)
        {
            GUI.color = c;
            GUI.DrawTexture(r, _white);
            GUI.color = Color.white;
        }

        void OnGUI()
        {
            if (launcher == null) return;
            // While the meta screens are up (results → shop → day transition) the player isn't flying,
            // so the live DISTANCE/SPEED/HEIGHT HUD is just stale clutter behind the panels — hide it.
            if (director != null && (director.ShopOpen || director.ResultsOpen || director.TransitionOpen || director.IntroOpen))
                return;
            EnsureStyles();
            HudScale.Begin();

            DrawStatPanel();
            DrawState();
            DrawRevMeter();
            DrawBoostMeter();
            DrawPerfectFlash();
            HudScale.End();
        }

        /// <summary>The themed DISTANCE / SPEED / HEIGHT panel, top-left. Generously spaced, and the
        /// structure (badge, pips, frame, dividers, rivets, …) graduates with the equipped tire.</summary>
        void DrawStatPanel()
        {
            const float x = 24f, y = 24f, w = 360f;
            const float padTop = 16f, padBot = 18f, rowH = 70f;
            float headH = _theme.showName ? 50f : 0f;
            float h = padTop + headH + rowH * 3f + padBot;
            var panel = new Rect(x, y, w, h);

            // Pulsing glow for the fancier tires.
            if (_theme.glow)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3f);
                for (int i = 4; i >= 1; i--)
                {
                    float g = i * 3f;
                    var a = _theme.accent;
                    a.a = 0.05f * pulse * i;
                    Bar(new Rect(panel.x - g, panel.y - g, panel.width + g * 2f, panel.height + g * 2f), a);
                }
            }

            Bar(panel, new Color(0.04f, 0.05f, 0.08f, _theme.panelAlpha));

            // Accent edge — a left spine plus (for higher tiers) a top gradient strip.
            Bar(new Rect(panel.x, panel.y, 4f, panel.height), _theme.accent);
            if (_theme.doubleSpine)  // a second, thinner inboard rail
            {
                var s = _theme.accent; s.a = 0.5f;
                Bar(new Rect(panel.x + 7f, panel.y + 6f, 1.5f, panel.height - 12f), s);
            }
            if (_theme.gradient) DrawGradientStrip(new Rect(panel.x + 4f, panel.y, panel.width - 4f, 3f));
            else Bar(new Rect(panel.x + 4f, panel.y, panel.width - 4f, 2f), _theme.accent);

            if (_theme.frame) DrawFrame(panel);
            if (_theme.rivets) DrawRivets(panel);
            if (_theme.brackets) DrawCorners(panel, _theme.accent);
            if (_theme.scanline) DrawScanline(panel);

            float iy = y + padTop;
            if (_theme.showName) iy = DrawHeader(new Rect(x, iy, w, headH));

            // Distance is normalised against the best so far so the bar means something.
            float distRef = Mathf.Max(50f, launcher.BestDistance, launcher.Distance);
            DrawStatRow(new Rect(x, iy, w, rowH), "DISTANCE", $"{launcher.Distance:0.0}", "m",
                        Mathf.Clamp01(launcher.Distance / distRef), best: launcher.BestDistance);
            iy += rowH;
            if (_theme.dividers) DrawDivider(x, iy, w);
            DrawStatRow(new Rect(x, iy, w, rowH), "SPEED", $"{launcher.Speed:0.0}", "m/s",
                        Mathf.Clamp01(launcher.Speed / 60f));
            iy += rowH;
            if (_theme.dividers) DrawDivider(x, iy, w);
            DrawStatRow(new Rect(x, iy, w, rowH), "HEIGHT", $"{Mathf.Max(0f, launcher.Height):0.0}", "m",
                        Mathf.Clamp01(launcher.Height / 30f));
        }

        /// <summary>Tire name + tier badge ("MK III") + the progress pips. Returns the next y.</summary>
        float DrawHeader(Rect r)
        {
            float padX = 18f;
            var prev = _name.normal.textColor;
            _name.normal.textColor = _theme.accent;
            GUI.Label(new Rect(r.x + padX, r.y, r.width - padX * 2f - 70f, 22f),
                      _theme.name.ToUpperInvariant(), _name);
            _name.normal.textColor = prev;

            // Tier badge, top-right of the header — names the rank so it reads per-tire even at a glance.
            var bprev = _badge.normal.textColor;
            _badge.normal.textColor = new Color(_theme.accent.r, _theme.accent.g, _theme.accent.b, 0.9f);
            GUI.Label(new Rect(r.x + padX, r.y + 1f, r.width - padX * 2f, 18f), _theme.badge, _badge);
            _badge.normal.textColor = bprev;

            // Progress pips: one per tire, filled up to (and including) the equipped one.
            float py = r.y + 30f, ps = 9f, gap = 4f;
            for (int i = 0; i < _theme.tierCount; i++)
            {
                var pr = new Rect(r.x + padX + i * (ps + gap), py, ps, ps);
                bool on = i <= _theme.tier;
                Color c = on ? (i == _theme.tier ? _theme.accent2 : _theme.accent)
                             : new Color(1f, 1f, 1f, 0.12f);
                Bar(pr, c);
            }
            return r.y + r.height;
        }

        void DrawStatRow(Rect r, string label, string value, string unit, float fill, float best = -1f)
        {
            float padX = 20f;
            float lx = r.x + padX;

            var lprev = _label.normal.textColor;
            _label.normal.textColor = new Color(0.7f, 0.74f, 0.8f, 1f);
            GUI.Label(new Rect(lx, r.y + 10f, 120f, 16f), label, _label);
            _label.normal.textColor = lprev;

            if (best >= 0f)
            {
                var bprev = _hint.normal.textColor;
                _hint.normal.textColor = new Color(1f, 1f, 1f, 0.45f);
                GUI.Label(new Rect(r.x, r.y + 10f, r.width - 20f, 16f), $"best {best:0.0} m", _hint);
                _hint.normal.textColor = bprev;
            }

            var vprev = _value.normal.textColor;
            _value.normal.textColor = Color.white;
            GUI.Label(new Rect(lx, r.y + 26f, 180f, 36f), value, _value);
            // unit trailing the number
            float vw = _value.CalcSize(new GUIContent(value)).x;
            _value.normal.textColor = vprev;
            var uprev = _mid.normal.textColor;
            _mid.normal.textColor = new Color(0.7f, 0.74f, 0.8f, 1f);
            GUI.Label(new Rect(lx + vw + 8f, r.y + 37f, 60f, 24f), unit, _mid);
            _mid.normal.textColor = uprev;

            // progress bar — segmented (ticked) on higher tiers, a smooth fill otherwise
            var track = new Rect(lx, r.y + r.height - 12f, r.width - padX * 2f, 4f);
            Bar(track, new Color(1f, 1f, 1f, 0.08f));
            if (_theme.segments) DrawSegmentedBar(track, fill);
            else
            {
                var filled = new Rect(track.x, track.y, track.width * fill, track.height);
                if (_theme.gradient) DrawGradientStrip(filled);
                else Bar(filled, _theme.accent);
            }
        }

        void DrawState()
        {
            string msg = launcher.CurrentState switch
            {
                TireLauncher.State.Ready => "HOLD [Space] to rev",
                TireLauncher.State.Charging => "RELEASE in the green band — don't over-rev!",
                TireLauncher.State.Launched => "HOLD [Space] boost  ·  [< >] steer",
                TireLauncher.State.Stopped => $"Landed at {launcher.Distance:0.0} m — opening shop…",
                _ => "",
            };
            var style = new GUIStyle(_mid) { alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = new Color(1f, 1f, 1f, 0.9f);
            // While launched the active bar is the low boost meter (VH-38), so sit the prompt right
            // above it; other states show the rev meter higher up (VH-70), so keep the prompt above that.
            float stateY = launcher.CurrentState == TireLauncher.State.Launched
                ? HudScale.VH - 64f
                : HudScale.VH - 98f;
            GUI.Label(new Rect(0, stateY, HudScale.VW, 24f), msg, style);
        }

        // ---- Theme flourishes ----

        void DrawGradientStrip(Rect r)
        {
            // cheap 2-stop horizontal gradient: a handful of vertical slices lerping accent→accent2
            const int slices = 12;
            float sw = r.width / slices;
            for (int i = 0; i < slices; i++)
            {
                Color c = Color.Lerp(_theme.accent, _theme.accent2, i / (float)(slices - 1));
                Bar(new Rect(r.x + i * sw, r.y, sw + 1f, r.height), c);
            }
        }

        /// <summary>A faint inset outline that gives the panel a machined "frame" look.</summary>
        void DrawFrame(Rect r)
        {
            const float m = 6f;
            var c = _theme.accent; c.a = 0.22f;
            var f = new Rect(r.x + m, r.y + m, r.width - m * 2f, r.height - m * 2f);
            Bar(new Rect(f.x, f.y, f.width, 1f), c);
            Bar(new Rect(f.x, f.yMax - 1f, f.width, 1f), c);
            Bar(new Rect(f.x, f.y, 1f, f.height), c);
            Bar(new Rect(f.xMax - 1f, f.y, 1f, f.height), c);
        }

        /// <summary>Small accent studs running down the spine — panelling detail for mid+ tiers.</summary>
        void DrawRivets(Rect r)
        {
            var c = _theme.accent; c.a = 0.85f;
            for (float ry = r.y + 14f; ry < r.yMax - 10f; ry += 22f)
                Bar(new Rect(r.x + 1f, ry, 2f, 2f), c);
        }

        /// <summary>Hairline separator between stat rows.</summary>
        void DrawDivider(float x, float y, float w)
        {
            Bar(new Rect(x + 16f, y, w - 32f, 1f), new Color(1f, 1f, 1f, 0.07f));
            var c = _theme.accent; c.a = 0.35f;
            Bar(new Rect(x + 16f, y, 26f, 1f), c); // accent nub at the left of the divider
        }

        /// <summary>Progress bar drawn as discrete ticks rather than a solid fill.</summary>
        void DrawSegmentedBar(Rect track, float fill)
        {
            const int segs = 16;
            float sw = track.width / segs;
            int lit = Mathf.RoundToInt(Mathf.Clamp01(fill) * segs);
            for (int i = 0; i < segs; i++)
            {
                var seg = new Rect(track.x + i * sw + 1f, track.y, sw - 2f, track.height);
                if (i < lit)
                {
                    Color c = _theme.gradient
                        ? Color.Lerp(_theme.accent, _theme.accent2, i / (float)(segs - 1))
                        : _theme.accent;
                    Bar(seg, c);
                }
                else Bar(seg, new Color(1f, 1f, 1f, 0.05f));
            }
        }

        void DrawCorners(Rect r, Color c)
        {
            const float L = 14f, T = 2f;
            Bar(new Rect(r.x, r.y, L, T), c); Bar(new Rect(r.x, r.y, T, L), c);
            Bar(new Rect(r.xMax - L, r.y, L, T), c); Bar(new Rect(r.xMax - T, r.y, T, L), c);
            Bar(new Rect(r.x, r.yMax - T, L, T), c); Bar(new Rect(r.x, r.yMax - L, T, L), c);
            Bar(new Rect(r.xMax - L, r.yMax - T, L, T), c); Bar(new Rect(r.xMax - T, r.yMax - L, T, L), c);
        }

        void DrawScanline(Rect r)
        {
            float t = Time.unscaledTime * 0.6f % 1f;
            float ly = r.y + 4f + t * (r.height - 8f);
            var c = _theme.accent2; c.a = 0.25f;
            Bar(new Rect(r.x + 4f, ly, r.width - 8f, 2f), c);
        }

        void DrawRevMeter()
        {
            bool show = launcher.CurrentState == TireLauncher.State.Charging
                        || launcher.ChargeNormalized > 0.01f
                        && launcher.CurrentState != TireLauncher.State.Launched;
            if (!show) return;

            float w = 360f, h = 26f;
            float x = (HudScale.VW - w) * 0.5f, y = HudScale.VH - 70f;

            Bar(new Rect(x, y, w, h), new Color(0f, 0f, 0f, 0.55f));

            // power band (green) and overheat zone (red) drawn as backdrop regions
            float idealX = x + w * launcher.IdealNormalized;
            float bandW = w * launcher.PerfectHalfWidthNormalized;
            Bar(new Rect(idealX + bandW, y, (x + w) - (idealX + bandW), h), new Color(1f, 0.2f, 0.1f, 0.45f)); // overheat
            Bar(new Rect(idealX - bandW, y, bandW * 2f, h), new Color(0.2f, 1f, 0.2f, 0.6f));                 // sweet spot

            // current charge fill — tinted with the tire accent
            Bar(new Rect(x, y, w * launcher.ChargeNormalized, h),
                Color.Lerp(new Color(1f, 0.85f, 0.2f, 0.85f), _theme.accent, 0.4f));

            // ideal tick
            Bar(new Rect(idealX - 1f, y - 4f, 2f, h + 8f), Color.white);
        }

        void DrawBoostMeter()
        {
            if (launcher.CurrentState != TireLauncher.State.Launched) return;
            float w = 360f, h = 18f;
            float x = (HudScale.VW - w) * 0.5f, y = HudScale.VH - 38f;
            Bar(new Rect(x, y, w, h), new Color(0f, 0f, 0f, 0.55f));
            Color fill = launcher.IsBoosting
                ? new Color(1f, 0.55f, 0.1f, 0.95f)   // burning fuel
                : Color.Lerp(new Color(0.2f, 0.7f, 1f, 0.9f), _theme.accent2, 0.5f);
            Bar(new Rect(x, y, w * launcher.BoostNormalized, h), fill);
        }

        void DrawPerfectFlash()
        {
            if (launcher.PerfectFlash <= 0f || !launcher.LastWasPerfect) return;
            GUI.color = new Color(0.2f, 1f, 0.3f, Mathf.Clamp01(launcher.PerfectFlash));
            GUI.Label(new Rect(0, HudScale.VH * 0.3f, HudScale.VW, 60), "PERFECT LAUNCH!", _perfect);
            GUI.color = Color.white;
        }
    }

    /// <summary>
    /// Per-tire HUD styling. Each tire has its own accent (palette below) AND its own panelling:
    /// the structural flourishes (frame, dividers, rivets, glow, segmented bars, gradient, second
    /// rail, corner brackets, scan line) switch on roughly one per tier, and a tier badge + pip row
    /// always reflect the equipped rank — so a better tire looks measurably more advanced, not just
    /// a different colour.
    /// </summary>
    public struct HudTheme
    {
        public Color accent, accent2;
        public float panelAlpha;
        public bool showName, glow, gradient, brackets, scanline;
        public bool frame, dividers, rivets, doubleSpine, segments;
        public int tier, tierCount;
        public string badge;
        public string name;

        // Distinct accent per tire, climbing from muted greys to neon.
        static readonly Color[] Accents =
        {
            new Color(0.70f, 0.74f, 0.80f), // Standard  – grey
            new Color(0.45f, 0.85f, 0.55f), // Trainer   – green
            new Color(0.35f, 0.70f, 1.00f), // Street    – blue
            new Color(1.00f, 0.62f, 0.20f), // Rally     – orange
            new Color(0.80f, 0.78f, 0.35f), // Off-Road  – olive
            new Color(0.50f, 0.78f, 0.45f), // Mud       – moss
            new Color(1.00f, 0.30f, 0.25f), // Drag Slick– red
            new Color(0.70f, 0.40f, 1.00f), // Monster   – purple
            new Color(1.00f, 0.82f, 0.25f), // Industrial– amber
            new Color(0.25f, 0.95f, 0.95f), // Hover     – cyan
            new Color(0.85f, 0.90f, 1.00f), // Carbon    – silver-white
            new Color(1.00f, 0.25f, 0.85f), // Plasma    – magenta neon
        };

        public static HudTheme ForTier(int index, int count, string displayName)
        {
            count = Mathf.Max(1, count);
            int idx = Mathf.Clamp(index, 0, count - 1);
            float t = count > 1 ? idx / (float)(count - 1) : 0f;

            Color accent = idx < Accents.Length ? Accents[idx]
                                                : Color.HSVToRGB((idx * 0.13f) % 1f, 0.7f, 1f);
            // brighter, hotter second stop for gradients/scan
            Color accent2 = Color.Lerp(accent, Color.white, 0.45f);

            return new HudTheme
            {
                name = displayName,
                accent = accent,
                accent2 = accent2,
                panelAlpha = Mathf.Lerp(0.55f, 0.82f, t),
                showName = true,
                tier = idx,
                tierCount = count,
                badge = "MK " + Roman(idx + 1),
                // One new piece of panelling per tier, so adjacent tires never look the same.
                frame = idx >= 1,
                dividers = idx >= 2,
                rivets = idx >= 3,
                glow = idx >= 4,
                segments = idx >= 5,
                gradient = idx >= 6,
                doubleSpine = idx >= 7,
                brackets = idx >= 8,
                scanline = idx >= 10,
            };
        }

        static readonly string[] RomanOnes = { "", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX" };
        static readonly string[] RomanTens = { "", "X", "XX", "XXX" };

        /// <summary>Compact Roman numeral for the tier badge (covers the 1–12 range comfortably).</summary>
        static string Roman(int n)
        {
            n = Mathf.Clamp(n, 1, 39);
            return RomanTens[n / 10] + RomanOnes[n % 10];
        }
    }
}
