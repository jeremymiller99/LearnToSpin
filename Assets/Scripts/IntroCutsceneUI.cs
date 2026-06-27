using UnityEngine;
using UnityEngine.InputSystem;

namespace LearnToSpin
{
    /// <summary>
    /// The one-time opening cutscene — deliberately simple: a few funny one-liners shown over the
    /// LIVE game view (so you're looking at the actual tire model on the launcher, not drawn art),
    /// then a dramatic "LEARN TO SPIN" title that drops in and slams with the tire-impact SFX. A
    /// "DAY 1" header sits up top the whole time and fades out with everything else at the end.
    /// Click / Space steps through the lines (first press finishes the typewriter, second advances);
    /// Esc skips.
    ///
    /// Drawn in IMGUI to match the rest of the code-only UI (HudScale design space). Spawned by
    /// <see cref="GameDirector"/>, which freezes the launcher while it runs and calls
    /// <see cref="GameDirector.EndIntro"/> when it finishes/skips.
    /// </summary>
    public class IntroCutsceneUI : MonoBehaviour
    {
        public GameDirector director;

        // Keep it punchy. Each string is one beat the player clicks through, then the title slams in.
        static readonly string[] Lines =
        {
            "Meet your tire.",
            "Round. Rubber. Utterly convinced it can fly.",
            "Every engineer we asked said no. A few just hung up.",
            "So we bolted it to an engine instead.",
            "Rev it up, poke it loose, and let physics take over.",
        };

        enum Phase { Lines, Title }
        Phase _phase;

        bool _active;
        int _index;
        float _slideT;     // unscaled seconds the current line has been on screen
        float _titleT;     // unscaled seconds into the title-drop phase
        bool _impactPlayed;
        bool _finishing;
        float _finishT;    // fade-out timer once it's all done

        const float FadeIn = 0.35f;      // per-line fade-in
        const float SecPerChar = 0.028f; // typewriter speed
        const float TitleFall = 0.5f;    // how long the title takes to drop in
        const float TitleHold = 1.5f;    // beat held after the slam before it all clears
        const float FinishDur = 0.6f;    // fade the overlay away to reveal the game

        GUIStyle _caption, _hint, _skip, _day, _title;
        Texture2D _white;

        static readonly Color Ink = new Color(0.95f, 0.96f, 0.99f);
        static readonly Color Gold = new Color(1f, 0.82f, 0.22f);

        /// <summary>Begin playing the cutscene (called by the director when the profile is fresh).</summary>
        public void Play()
        {
            _active = true;
            _phase = Phase.Lines;
            _index = 0;
            _slideT = 0f;
            _titleT = 0f;
            _impactPlayed = false;
            _finishing = false;
            _finishT = 0f;
        }

        void Update()
        {
            if (!_active) return;
            float dt = Time.unscaledDeltaTime;

            if (_finishing)
            {
                _finishT += dt;
                if (_finishT >= FinishDur)
                {
                    _active = false;
                    if (director != null) director.EndIntro();
                    Destroy(gameObject);
                }
                return;
            }

            var kb = Keyboard.current;
            bool skip = kb != null && kb.escapeKey.wasPressedThisFrame;
            bool advance = (kb != null && (kb.spaceKey.wasPressedThisFrame ||
                                           kb.enterKey.wasPressedThisFrame ||
                                           kb.numpadEnterKey.wasPressedThisFrame))
                           || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);

            if (skip) { Finish(); return; }

            if (_phase == Phase.Lines)
            {
                _slideT += dt;
                if (advance) Advance();
            }
            else // Title
            {
                _titleT += dt;
                // Slam the moment it lands: tire-impact SFX, once.
                if (!_impactPlayed && _titleT >= TitleFall)
                {
                    _impactPlayed = true;
                    if (AudioManager.Instance != null) AudioManager.Instance.PlayTireImpact();
                }
                // Auto-clear after the held beat (or a click once it has landed).
                if (_titleT >= TitleFall + TitleHold || (advance && _impactPlayed)) Finish();
            }
        }

        /// <summary>First press completes the typewriter on the current line; the next advances.
        /// Advancing past the last line starts the title drop.</summary>
        void Advance()
        {
            if (!TypewriterComplete())
            {
                _slideT = Mathf.Max(_slideT, FadeIn + Lines[_index].Length * SecPerChar + 0.001f);
                return;
            }
            if (_index >= Lines.Length - 1) { _phase = Phase.Title; _titleT = 0f; return; }
            _index++;
            _slideT = 0f;
        }

        void Finish()
        {
            if (_finishing) return;
            _finishing = true;
            _finishT = 0f;
        }

        bool TypewriterComplete() => (_slideT - FadeIn) / SecPerChar >= Lines[_index].Length;

        static float Smooth(float p) { p = Mathf.Clamp01(p); return p * p * (3f - 2f * p); }

        void EnsureStyles()
        {
            if (_caption != null) return;
            _white = Texture2D.whiteTexture;
            _caption = new GUIStyle(GUI.skin.label)
            { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
              wordWrap = true, normal = { textColor = Ink } };
            _hint = new GUIStyle(GUI.skin.label)
            { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight,
              normal = { textColor = new Color(0.8f, 0.83f, 0.9f) } };
            _skip = new GUIStyle(GUI.skin.label)
            { fontSize = 14, alignment = TextAnchor.MiddleLeft,
              normal = { textColor = new Color(0.7f, 0.73f, 0.8f) } };
            _day = new GUIStyle(GUI.skin.label)
            { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
              wordWrap = false, clipping = TextClipping.Overflow, normal = { textColor = Gold } };
            _title = new GUIStyle(GUI.skin.label)
            { fontSize = 68, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
              wordWrap = false, clipping = TextClipping.Overflow, normal = { textColor = Gold } };
        }

        void Fill(Rect r, Color c) { var p = GUI.color; GUI.color = c; GUI.DrawTexture(r, _white); GUI.color = p; }

        void OnGUI()
        {
            if (!_active) return;
            EnsureStyles();
            HudScale.Begin();

            float vw = HudScale.VW, vh = HudScale.VH;
            float g = _finishing ? 1f - Smooth(_finishT / FinishDur) : 1f; // overlay alpha (fades to reveal game)

            // Cinematic letterbox bars + gold rails — the only chrome. The live tire/world shows between.
            float bar = Mathf.Round(vh * 0.12f);
            Fill(new Rect(0, 0, vw, bar), new Color(0f, 0f, 0f, 0.82f * g));
            Fill(new Rect(0, vh - bar, vw, bar), new Color(0f, 0f, 0f, 0.82f * g));
            Fill(new Rect(0, bar, vw, 2f), new Color(Gold.r, Gold.g, Gold.b, 0.55f * g));
            Fill(new Rect(0, vh - bar - 2f, vw, 2f), new Color(Gold.r, Gold.g, Gold.b, 0.55f * g));

            // DAY header up top (stays until the whole overlay fades out at the end).
            int day = director != null && director.Progress != null ? Mathf.Max(1, director.Progress.day) : 1;
            _day.normal.textColor = new Color(Gold.r, Gold.g, Gold.b, g);
            GUI.Label(new Rect(0, bar * 0.5f - 18f, vw, 36f), "DAY " + day, _day);

            if (_phase == Phase.Lines) DrawLine(vw, vh, bar, g);
            else DrawTitle(vw, vh, g);

            // Prompts tucked into the bottom bar.
            float py = vh - bar + (bar - 22f) * 0.5f;
            _skip.normal.textColor = new Color(0.7f, 0.73f, 0.8f, 0.9f * g);
            GUI.Label(new Rect(28f, py, 240f, 22f), "Esc  —  skip", _skip);

            bool showLaunch = _phase == Phase.Title && _impactPlayed;
            bool ready = _phase == Phase.Title ? showLaunch : TypewriterComplete();
            _hint.normal.textColor = new Color(0.85f, 0.88f, 0.94f, (ready ? 0.95f : 0.4f) * g);
            GUI.Label(new Rect(vw - 308f, py, 280f, 22f), showLaunch ? "Space  ▸  Launch" : "Space  ▸", _hint);

            HudScale.End();
        }

        // The funny one-liners, parked in the lower third so they never sit over the tire.
        void DrawLine(float vw, float vh, float bar, float g)
        {
            float fa = Smooth(_slideT / FadeIn) * g;
            float capH = 82f;
            float capBottom = vh - bar - 10f;
            var strip = new Rect(0, capBottom - capH, vw, capH);
            Fill(strip, new Color(0f, 0f, 0f, 0.45f * fa));

            int shown = Mathf.Clamp(Mathf.FloorToInt((_slideT - FadeIn) / SecPerChar), 0, Lines[_index].Length);
            _caption.normal.textColor = new Color(Ink.r, Ink.g, Ink.b, fa);
            GUI.Label(new Rect(vw * 0.5f - 380f, strip.y, 760f, strip.height),
                      Lines[_index].Substring(0, shown), _caption);
        }

        // "LEARN TO SPIN" drops from above, slams to centre (impact SFX), then settles with a shake.
        void DrawTitle(float vw, float vh, float g)
        {
            float targetY = vh * 0.42f;
            float y, shakeX = 0f;

            if (_titleT < TitleFall)
            {
                float p = _titleT / TitleFall;
                y = Mathf.Lerp(-120f, targetY, p * p); // accelerate in, gravity-style
            }
            else
            {
                // Damped bounce + jitter after the slam.
                float st = _titleT - TitleFall;
                float damp = Mathf.Exp(-st * 9f);
                y = targetY + Mathf.Sin(st * 55f) * 9f * damp;
                shakeX = Mathf.Sin(st * 43f) * 5f * damp;
            }

            var r = new Rect(vw * 0.5f - 460f + shakeX, y - 50f, 920f, 100f);
            // Drop shadow for punch + legibility over the bright world.
            _title.normal.textColor = new Color(0f, 0f, 0f, 0.55f * g);
            GUI.Label(new Rect(r.x + 4f, r.y + 5f, r.width, r.height), "LEARN TO SPIN", _title);
            _title.normal.textColor = new Color(Gold.r, Gold.g, Gold.b, g);
            GUI.Label(r, "LEARN TO SPIN", _title);
        }
    }
}
