using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace LearnToSpin
{
    /// <summary>
    /// The victory screen, shown once every objective tier is cleared (see <see cref="Objectives"/>).
    /// Reports how many days it took — the day the final goal fell — and recaps the lifetime bests,
    /// then offers KEEP PLAYING (back to the shop for free play) or MAIN MENU. Drawn in the same
    /// IMGUI / dark-wash + gold style as the pause and main-menu screens.
    /// </summary>
    public class WinUI : MonoBehaviour
    {
        public GameDirector director;

        [Tooltip("Scene loaded by 'Main Menu'. Must be in Build Settings.")]
        public string menuSceneName = "Menu";

        static readonly Color Gold = new Color(1f, 0.82f, 0.22f);

        GUIStyle _title, _sub, _stat, _btn, _hint;
        Texture2D _white;
        float _t;
        bool _wasOpen, _playedFanfare;

        static float Smooth(float p) { p = Mathf.Clamp01(p); return p * p * (3f - 2f * p); }

        void Update()
        {
            if (director == null || !director.WinOpen) { _wasOpen = false; _playedFanfare = false; return; }
            if (!_wasOpen) { _t = 0f; _wasOpen = true; }
            _t += Time.unscaledDeltaTime;

            if (!_playedFanfare)
            {
                _playedFanfare = true;
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.SetMusicMode(0);   // celebratory: drop to menu music bed
                    AudioManager.Instance.PlayLaunchPerfect(); // reuse the "perfect" sting as a fanfare
                }
            }
        }

        void EnsureStyles()
        {
            if (_title != null) return;
            _white = Texture2D.whiteTexture;
            _title = new GUIStyle(GUI.skin.label)
            { fontSize = 46, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
              wordWrap = false, clipping = TextClipping.Overflow };
            _sub = new GUIStyle(GUI.skin.label)
            { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
              wordWrap = true };
            _stat = new GUIStyle(GUI.skin.label)
            { fontSize = 16, alignment = TextAnchor.MiddleCenter };
            _btn = new GUIStyle(GUI.skin.button) { fontSize = 19, fontStyle = FontStyle.Bold };
            _hint = new GUIStyle(GUI.skin.label)
            { fontSize = 13, alignment = TextAnchor.MiddleCenter, wordWrap = true,
              normal = { textColor = new Color(0.65f, 0.7f, 0.8f) } };
        }

        void Fill(Rect r, Color c)
        {
            var prev = GUI.color; GUI.color = c; GUI.DrawTexture(r, _white); GUI.color = prev;
        }

        void OnGUI()
        {
            if (director == null || !director.WinOpen) return;
            EnsureStyles();
            GUI.depth = -100; // in front of everything
            HudScale.Begin();

            float vw = HudScale.VW, vh = HudScale.VH;
            float a = Smooth(_t / 0.5f);
            Fill(new Rect(0, 0, vw, vh), new Color(0.05f, 0.06f, 0.09f, 0.95f * a));
            Fill(new Rect(0, 0, vw, 4f), Gold);
            Fill(new Rect(0, vh - 4f, vw, 4f), Gold);

            var p = director.Progress;
            int days = Mathf.Max(1, p.winDay > 0 ? p.winDay : p.day);

            const float titleH = 58f, subH = 56f, gap = 20f, statH = 26f, btnH = 54f, btnGap = 16f;
            float statsBlock = statH * Objectives.StatCount;
            float blockH = titleH + subH + gap + statsBlock + gap + btnH * 2f + btnGap;
            float y = Mathf.Max(20f, (vh - blockH) * 0.5f);

            var goldTitle = new GUIStyle(_title) { normal = { textColor = Gold } };
            GUI.Label(new Rect(0, y, vw, titleH), "CONGRATULATIONS!", goldTitle);
            y += titleH;

            GUI.Label(new Rect(0, y, vw, subH),
                      $"You learned to spin in {days} {(days == 1 ? "day" : "days")}!", _sub);
            y += subH + gap;

            // Lifetime-best recap — every objective stat, with the value that beat its top tier.
            for (int s = 0; s < Objectives.StatCount; s++)
            {
                GUI.Label(new Rect(0, y, vw, statH),
                          $"{Objectives.Titles[s]}:  {Objectives.Format(s, Objectives.Best(p, s))}", _stat);
                y += statH;
            }
            y += gap;

            float bw = Mathf.Min(360f, vw - 80f);
            float bx = (vw - bw) * 0.5f;
            var prev = GUI.backgroundColor;

            GUI.backgroundColor = new Color(0.2f, 0.7f, 0.3f);
            if (GUI.Button(new Rect(bx, y, bw, btnH), "KEEP PLAYING", _btn))
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlayBtnClick();
                director.DismissWin();
            }
            y += btnH + btnGap;

            GUI.backgroundColor = new Color(0.28f, 0.5f, 0.78f);
            if (GUI.Button(new Rect(bx, y, bw, btnH), "MAIN MENU", _btn))
            {
                if (AudioManager.Instance != null) { AudioManager.Instance.PlayBtnClick(); AudioManager.Instance.SetMusicMode(1); }
                Time.timeScale = 1f;
                SceneManager.LoadScene(menuSceneName);
            }
            GUI.backgroundColor = prev;

            HudScale.End();
        }
    }
}
