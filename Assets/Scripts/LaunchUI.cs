using UnityEngine;

namespace LearnToSpin
{
    /// <summary>
    /// IMGUI HUD for the run. Surfaces the two skill systems (rev power-band + boost reserve) and
    /// a stat panel showing the live DISTANCE / SPEED / HEIGHT. The stat panel is themed off the
    /// equipped tire: every tire gets its own accent colour, and the pricier ones unlock extra
    /// flourishes (gradient bars, a pulsing glow, corner brackets, an animated scan line) so the
    /// HUD visibly levels up as you progress. Code-only so the prototype needs zero Canvas wiring.
    /// </summary>
    public class LaunchUI : MonoBehaviour
    {
        public TireLauncher launcher;
        [Tooltip("Optional — shows the live render-distance readout. Auto-found if left empty.")]
        public ViewDistance view;

        GUIStyle _value, _label, _name, _mid, _perfect, _hint;
        Texture2D _white;

        // ---- Equipped-tire theme (set by GameDirector each run) ----
        HudTheme _theme = HudTheme.ForTier(0, 1, "Tire");

        /// <summary>Called by GameDirector when a run starts so the HUD reflects the equipped tire.</summary>
        public void SetTire(int index, int count, string displayName)
            => _theme = HudTheme.ForTier(index, count, displayName);

        void EnsureStyles()
        {
            if (_value != null) return;
            _value = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold };
            _label = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold };
            _name = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
            _mid = new GUIStyle(GUI.skin.label) { fontSize = 18 };
            _perfect = new GUIStyle(GUI.skin.label)
            { fontSize = 40, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _hint = new GUIStyle(GUI.skin.label)
            { fontSize = 13, alignment = TextAnchor.UpperRight };
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
            EnsureStyles();

            DrawStatPanel();
            DrawState();
            DrawRevMeter();
            DrawBoostMeter();
            DrawPerfectFlash();
            DrawViewDistance();
        }

        /// <summary>The themed DISTANCE / SPEED / HEIGHT panel, top-left.</summary>
        void DrawStatPanel()
        {
            const float x = 20f, y = 20f, w = 300f;
            float headH = _theme.showName ? 30f : 0f;
            float rowH = 58f;
            float h = headH + rowH * 3f + 14f;
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
            if (_theme.gradient) DrawGradientStrip(new Rect(panel.x + 4f, panel.y, panel.width - 4f, 3f));
            else Bar(new Rect(panel.x + 4f, panel.y, panel.width - 4f, 2f), _theme.accent);

            if (_theme.brackets) DrawCorners(panel, _theme.accent);
            if (_theme.scanline) DrawScanline(panel);

            float iy = y + 8f;
            if (_theme.showName)
            {
                var prev = _name.normal.textColor;
                _name.normal.textColor = _theme.accent;
                GUI.Label(new Rect(x + 14f, iy, w - 20f, 22f), _theme.name.ToUpperInvariant(), _name);
                _name.normal.textColor = prev;
                iy += headH;
            }

            // Distance is normalised against the best so far so the bar means something.
            float distRef = Mathf.Max(50f, launcher.BestDistance, launcher.Distance);
            DrawStatRow(new Rect(x, iy, w, rowH), "DISTANCE", $"{launcher.Distance:0.0}", "m",
                        Mathf.Clamp01(launcher.Distance / distRef), best: launcher.BestDistance);
            iy += rowH;
            DrawStatRow(new Rect(x, iy, w, rowH), "SPEED", $"{launcher.Speed:0.0}", "m/s",
                        Mathf.Clamp01(launcher.Speed / 60f));
            iy += rowH;
            DrawStatRow(new Rect(x, iy, w, rowH), "HEIGHT", $"{Mathf.Max(0f, launcher.Height):0.0}", "m",
                        Mathf.Clamp01(launcher.Height / 30f));
        }

        void DrawStatRow(Rect r, string label, string value, string unit, float fill, float best = -1f)
        {
            float padX = 16f;
            float lx = r.x + padX;

            var lprev = _label.normal.textColor;
            _label.normal.textColor = new Color(0.7f, 0.74f, 0.8f, 1f);
            GUI.Label(new Rect(lx, r.y + 6f, 120f, 16f), label, _label);
            _label.normal.textColor = lprev;

            if (best >= 0f)
            {
                var bprev = _hint.normal.textColor;
                _hint.normal.textColor = new Color(1f, 1f, 1f, 0.45f);
                GUI.Label(new Rect(r.x, r.y + 6f, r.width - 16f, 16f), $"best {best:0.0} m", _hint);
                _hint.normal.textColor = bprev;
            }

            var vprev = _value.normal.textColor;
            _value.normal.textColor = Color.white;
            GUI.Label(new Rect(lx, r.y + 18f, 180f, 34f), value, _value);
            // unit trailing the number
            float vw = _value.CalcSize(new GUIContent(value)).x;
            _value.normal.textColor = vprev;
            var uprev = _mid.normal.textColor;
            _mid.normal.textColor = new Color(0.7f, 0.74f, 0.8f, 1f);
            GUI.Label(new Rect(lx + vw + 6f, r.y + 28f, 60f, 24f), unit, _mid);
            _mid.normal.textColor = uprev;

            // mini progress bar
            var track = new Rect(lx, r.y + r.height - 8f, r.width - padX * 2f, 4f);
            Bar(track, new Color(1f, 1f, 1f, 0.08f));
            var filled = new Rect(track.x, track.y, track.width * fill, track.height);
            if (_theme.gradient) DrawGradientStrip(filled);
            else Bar(filled, _theme.accent);
        }

        void DrawState()
        {
            string msg = launcher.CurrentState switch
            {
                TireLauncher.State.Ready => "HOLD [Space] to rev",
                TireLauncher.State.Charging => "RELEASE in the green band — don't over-rev!",
                TireLauncher.State.Launched => "HOLD [Space] boost  ·  [← →] steer",
                TireLauncher.State.Stopped => $"Landed at {launcher.Distance:0.0} m — opening shop…",
                _ => "",
            };
            var style = new GUIStyle(_mid) { alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = new Color(1f, 1f, 1f, 0.9f);
            GUI.Label(new Rect(0, Screen.height - 104f, Screen.width, 24f), msg, style);
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

        /// <summary>Top-right readout for the render-distance perf knob ([ / ] to adjust).</summary>
        void DrawViewDistance()
        {
            if (view == null) view = FindFirstObjectByType<ViewDistance>();
            if (view == null) return;
            GUI.color = new Color(1f, 1f, 1f, 0.75f);
            GUI.Label(new Rect(Screen.width - 270, 16, 250, 20),
                      $"View: {view.Current:0} m   [ / ] adjust", _hint);
            GUI.color = Color.white;
        }

        void DrawRevMeter()
        {
            bool show = launcher.CurrentState == TireLauncher.State.Charging
                        || launcher.ChargeNormalized > 0.01f
                        && launcher.CurrentState != TireLauncher.State.Launched;
            if (!show) return;

            float w = 360f, h = 26f;
            float x = (Screen.width - w) * 0.5f, y = Screen.height - 70f;

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
            float x = (Screen.width - w) * 0.5f, y = Screen.height - 38f;
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
            GUI.Label(new Rect(0, Screen.height * 0.3f, Screen.width, 60), "PERFECT LAUNCH!", _perfect);
            GUI.color = Color.white;
        }
    }

    /// <summary>
    /// Per-tire HUD styling. Each tire has its own accent (palette below); pricier tiers
    /// progressively unlock a glow, gradient bars, corner brackets and a scan line so the HUD
    /// looks cooler the further you've progressed.
    /// </summary>
    public struct HudTheme
    {
        public Color accent, accent2;
        public float panelAlpha;
        public bool showName, glow, gradient, brackets, scanline;
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
                glow = idx >= 4,
                gradient = idx >= 6,
                brackets = idx >= 8,
                scanline = idx >= 10,
            };
        }
    }
}
