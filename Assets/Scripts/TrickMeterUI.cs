using UnityEngine;

namespace LearnToSpin
{
    /// <summary>
    /// Skate-game style "trick meter" anchored to the tire in world space. Once the wheel has been
    /// airborne past a short threshold it pops a meter beside it that counts the live air time up,
    /// climbs through named tiers (AIR → NICE → SICK → INSANE → GODLIKE), and spins a ring of
    /// indicator pips around the tire that fills as you approach the next tier. On landing it
    /// "stamps" the banked air time + final tier, which floats up and fades. The longest stretch of
    /// the run is also a payout factor (see <see cref="TireLauncher.LongestAirTime"/>), so the meter
    /// is the live, in-world readout of money you're about to earn. IMGUI to match the code-only UI.
    /// </summary>
    public class TrickMeterUI : MonoBehaviour
    {
        public TireLauncher launcher;
        public Transform target;

        [Tooltip("Air time (s) before the meter appears — tiny hops are ignored.")]
        public float showThreshold = 0.45f;

        // Named tiers keyed by seconds aloft: the last whose threshold <= air time is the active one.
        struct Tier { public float at; public string name; public Color color; }
        static readonly Tier[] Tiers =
        {
            new Tier { at = 0.45f, name = "AIR",      color = new Color(0.85f, 0.90f, 1.00f) },
            new Tier { at = 1.00f, name = "NICE!",    color = new Color(0.40f, 0.85f, 1.00f) },
            new Tier { at = 1.80f, name = "SICK!",    color = new Color(0.55f, 1.00f, 0.45f) },
            new Tier { at = 2.80f, name = "INSANE!",  color = new Color(1.00f, 0.65f, 0.20f) },
            new Tier { at = 4.00f, name = "GODLIKE!", color = new Color(1.00f, 0.30f, 0.85f) },
        };

        const float StampDur = 1.4f; // how long the landing stamp lingers

        GUIStyle _tierStyle, _timeStyle, _stampTier, _stampTime;
        Texture2D _white;

        float _prevAir;
        int _prevTier = -1;
        float _tierPop;        // brief scale-pop when a new tier is crossed
        // landing stamp
        float _stamp;          // seconds remaining
        float _stampAir;
        Tier _stampTierData;
        Vector2 _stampAnchor;  // screen point (GUI coords) the tire was at on touchdown

        void EnsureStyles()
        {
            if (_tierStyle != null) return;
            _white = Texture2D.whiteTexture;
            _tierStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _timeStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _stampTier = new GUIStyle(GUI.skin.label)
            { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _stampTime = new GUIStyle(GUI.skin.label)
            { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        }

        static int TierIndex(float air)
        {
            int idx = 0;
            for (int i = 0; i < Tiers.Length; i++)
                if (air >= Tiers[i].at) idx = i;
            return idx;
        }

        void Update()
        {
            float air = launcher != null ? launcher.CurrentAirTime : 0f;

            // Tier-up pop while still in the air.
            if (air > showThreshold)
            {
                int t = TierIndex(air);
                if (t > _prevTier) 
                    {
                        _tierPop = 1f;
                        if (t == 1) AudioManager.Instance.PlayTrickNice();
                        else if (t == 2) AudioManager.Instance.PlayTrickSick();
                        else if (t == 3) AudioManager.Instance.PlayTrickInsane();
                        else if (t == 4) AudioManager.Instance.PlayTrickGodlike();
                    }
                _prevTier = t;
            }
            else
            {
                _prevTier = -1;
            }
            if (_tierPop > 0f) _tierPop = Mathf.Max(0f, _tierPop - Time.deltaTime * 3f);

            // Touchdown after a qualifying air → bank a stamp where the tire just was.
            if (_prevAir > showThreshold && air <= 0.0001f)
            {
                _stampAir = _prevAir;
                _stampTierData = Tiers[TierIndex(_prevAir)];
                _stamp = StampDur;
                _stampAnchor = ScreenAnchor(out _);
                AudioManager.Instance.PlayLand(target != null ? target.position : default);
            }
            if (_stamp > 0f) _stamp -= Time.deltaTime;

            _prevAir = air;
        }

        /// <summary>Tire centre projected into GUI design space (top-left origin, 960×600 units so it
        /// lines up under <see cref="HudScale"/>); <paramref name="onScreen"/> is false when it's
        /// behind the camera or there's nothing to anchor to.</summary>
        Vector2 ScreenAnchor(out bool onScreen)
        {
            onScreen = false;
            var cam = Camera.main;
            if (cam == null || target == null) return Vector2.zero;
            Vector3 sp = cam.WorldToScreenPoint(target.position);
            if (sp.z <= 0f) return Vector2.zero;
            onScreen = true;
            return HudScale.ToVirtual(new Vector2(sp.x, Screen.height - sp.y));
        }

        void OnGUI()
        {
            if (launcher == null) return;
            EnsureStyles();
            HudScale.Begin();

            float air = launcher.CurrentAirTime;
            bool flying = air > showThreshold
                          && launcher.CurrentState == TireLauncher.State.Launched;
            if (flying)
            {
                Vector2 anchor = ScreenAnchor(out bool on);
                if (on) DrawLiveMeter(anchor, air);
            }

            if (_stamp > 0f) DrawStamp();

            HudScale.End();
        }

        // ---- live meter (spinning ring + readout) ----

        void DrawLiveMeter(Vector2 anchor, float air)
        {
            int ti = TierIndex(air);
            Tier tier = Tiers[ti];

            // progress from this tier toward the next (full ring at the top tier)
            float prog = 1f;
            if (ti < Tiers.Length - 1)
                prog = Mathf.Clamp01((air - tier.at) / (Tiers[ti + 1].at - tier.at));

            DrawRing(anchor, tier.color, prog);

            // readout panel floating above-right of the tire
            float pop = 1f + 0.12f * _tierPop;
            float pw = 168f, ph = 70f;
            float px = anchor.x + 38f;
            float py = anchor.y - 96f;
            // keep it on screen
            px = Mathf.Clamp(px, 8f, HudScale.VW - pw - 8f);
            py = Mathf.Clamp(py, 8f, HudScale.VH - ph - 8f);
            var panel = new Rect(px, py, pw, ph);

            Matrix4x4 m = GUI.matrix;
            GUIUtility.ScaleAroundPivot(new Vector2(pop, pop), panel.center);

            Fill(panel, new Color(0.04f, 0.05f, 0.08f, 0.78f));
            Fill(new Rect(panel.x, panel.y, panel.width, 3f), tier.color);

            Tint(_tierStyle, tier.color);
            GUI.Label(new Rect(panel.x, panel.y + 6f, panel.width, 26f), tier.name, _tierStyle);

            Tint(_timeStyle, Color.white);
            GUI.Label(new Rect(panel.x, panel.y + 30f, panel.width, 32f), $"{air:0.00}s", _timeStyle);

            // thin progress-to-next-tier bar along the bottom
            var track = new Rect(panel.x + 10f, panel.yMax - 8f, panel.width - 20f, 4f);
            Fill(track, new Color(1f, 1f, 1f, 0.10f));
            Fill(new Rect(track.x, track.y, track.width * prog, track.height), tier.color);

            GUI.matrix = m;
            GUI.color = Color.white;
        }

        /// <summary>Ring of pips orbiting the tire; pips light up as <paramref name="prog"/> climbs.</summary>
        void DrawRing(Vector2 c, Color color, float prog)
        {
            const int pips = 18;
            int lit = Mathf.CeilToInt(Mathf.Clamp01(prog) * pips);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
            float radius = 52f + 4f * pulse;
            float rot = Time.unscaledTime * 150f; // degrees, spinning

            for (int i = 0; i < pips; i++)
            {
                float ang = (rot + i * (360f / pips)) * Mathf.Deg2Rad;
                Vector2 p = c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * radius;
                bool on = i < lit;
                float s = on ? 7f : 4f;
                Color pc = on ? color : new Color(color.r, color.g, color.b, 0.18f);
                if (on) pc.a = 0.55f + 0.45f * pulse;
                Fill(new Rect(p.x - s * 0.5f, p.y - s * 0.5f, s, s), pc);
            }
        }

        // ---- landing stamp (banked air, floats up + fades) ----

        void DrawStamp()
        {
            float k = _stamp / StampDur;            // 1 → 0
            float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k * 2.2f)); // quick fade at the tail
            float rise = (1f - k) * 46f;            // drift upward as it fades
            float pop = 1f + 0.25f * Mathf.Clamp01((1f - k) * 6f); // tiny punch-in on appear

            float pw = 220f, ph = 64f;
            float px = Mathf.Clamp(_stampAnchor.x - pw * 0.5f, 8f, HudScale.VW - pw - 8f);
            float py = Mathf.Clamp(_stampAnchor.y - 70f - rise, 8f, HudScale.VH - ph - 8f);
            var panel = new Rect(px, py, pw, ph);

            Matrix4x4 m = GUI.matrix;
            GUIUtility.ScaleAroundPivot(new Vector2(pop, pop), panel.center);

            var c = _stampTierData.color;
            Tint(_stampTier, new Color(c.r, c.g, c.b, a));
            GUI.Label(new Rect(panel.x, panel.y, panel.width, 38f), _stampTierData.name, _stampTier);

            Tint(_stampTime, new Color(1f, 1f, 1f, a));
            GUI.Label(new Rect(panel.x, panel.y + 34f, panel.width, 24f),
                      $"{_stampAir:0.00}s air", _stampTime);

            GUI.matrix = m;
            GUI.color = Color.white;
        }

        // ---- helpers ----

        void Fill(Rect r, Color c)
        {
            var prev = GUI.color; GUI.color = c; GUI.DrawTexture(r, _white); GUI.color = prev;
        }

        static void Tint(GUIStyle s, Color c) => s.normal.textColor = c;
    }
}
