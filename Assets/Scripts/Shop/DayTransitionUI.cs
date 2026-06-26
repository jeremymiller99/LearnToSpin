using UnityEngine;

namespace LearnToSpin
{
    /// <summary>
    /// The between-runs "Day X" card. When the player hits Launch in the shop we fade the whole
    /// screen to black, swap in the fresh tire <em>while it's fully black</em> (so the tire
    /// teleporting back to the start line is invisible), hold a big "DAY X" title, then fade back
    /// in on the new run. GameDirector drives it via <see cref="Begin"/>; the actual tire rebuild
    /// is handed in as the <c>atBlack</c> callback so it fires exactly at the opaque midpoint.
    /// IMGUI to match the rest of the code-only UI.
    /// </summary>
    public class DayTransitionUI : MonoBehaviour
    {
        public GameDirector director;

        enum Phase { Idle, FadeOut, Hold, FadeIn }

        // timeline (seconds), unscaled
        const float FadeOutDur = 0.55f;
        const float HoldDur = 1.10f;
        const float FadeInDur = 0.65f;

        Phase _phase = Phase.Idle;
        float _t;
        int _day;
        System.Action _atBlack;

        GUIStyle _dayStyle, _sub;
        Texture2D _white;

        /// <summary>True while the fade is running — director keeps input frozen until it ends.</summary>
        public bool Active => _phase != Phase.Idle;

        /// <summary>
        /// Kick off the fade. <paramref name="atBlack"/> runs once, at the fully-black midpoint, to
        /// perform the (now-concealed) tire rebuild.
        /// </summary>
        public void Begin(int day, System.Action atBlack)
        {
            _day = day;
            _atBlack = atBlack;
            _phase = Phase.FadeOut;
            _t = 0f;
        }

        static float Smooth(float p) { p = Mathf.Clamp01(p); return p * p * (3f - 2f * p); }

        void Update()
        {
            if (_phase == Phase.Idle) return;
            _t += Time.unscaledDeltaTime;

            switch (_phase)
            {
                case Phase.FadeOut:
                    if (_t >= FadeOutDur)
                    {
                        // Screen is fully black: do the swap now so nobody sees the teleport.
                        _atBlack?.Invoke();
                        _atBlack = null;
                        _phase = Phase.Hold;
                        _t = 0f;
                    }
                    break;
                case Phase.Hold:
                    if (_t >= HoldDur) { _phase = Phase.FadeIn; _t = 0f; }
                    break;
                case Phase.FadeIn:
                    if (_t >= FadeInDur)
                    {
                        _phase = Phase.Idle;
                        _t = 0f;
                        if (director != null) director.OnTransitionDone();
                    }
                    break;
            }
        }

        /// <summary>Opacity of the black curtain for the current phase.</summary>
        float Curtain()
        {
            switch (_phase)
            {
                case Phase.FadeOut: return Smooth(_t / FadeOutDur);
                case Phase.Hold: return 1f;
                case Phase.FadeIn: return 1f - Smooth(_t / FadeInDur);
                default: return 0f;
            }
        }

        void EnsureStyles()
        {
            if (_dayStyle != null) return;
            _white = Texture2D.whiteTexture;
            _dayStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 64, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _sub = new GUIStyle(GUI.skin.label)
            { fontSize = 18, alignment = TextAnchor.MiddleCenter };
        }

        void OnGUI()
        {
            if (_phase == Phase.Idle) return;
            EnsureStyles();

            float curtain = Curtain();

            // Black curtain over everything.
            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, curtain);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _white);
            GUI.color = prev;

            // "DAY X" — only legible once the screen is mostly black, so it never bleeds over the
            // world during the early fade-out / late fade-in.
            float textA = Mathf.Clamp01((curtain - 0.55f) / 0.45f);
            if (textA <= 0.001f) return;

            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;

            _dayStyle.normal.textColor = new Color(1f, 0.82f, 0.22f, textA);
            GUI.Label(new Rect(cx - 300f, cy - 70f, 600f, 80f), $"DAY {_day}", _dayStyle);

            _sub.normal.textColor = new Color(0.85f, 0.88f, 0.94f, textA * 0.85f);
            GUI.Label(new Rect(cx - 300f, cy + 14f, 600f, 24f), "A fresh launch awaits…", _sub);
        }
    }
}
