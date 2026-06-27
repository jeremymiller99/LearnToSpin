using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace LearnToSpin
{
    /// <summary>
    /// In-run pause overlay. Press [Esc] during a run to freeze the action (Time.timeScale = 0) and
    /// bring up a simple RESUME / MAIN MENU / QUIT panel, drawn in the same IMGUI / Fill+Outline
    /// style as <see cref="MainMenuUI"/>. Spawned by <see cref="GameDirector"/>; it pulls the live
    /// launcher from the director so it can disable flight input while paused without going stale
    /// across day rebuilds. Pausing is blocked while the results/shop/transition screens are up.
    /// </summary>
    public class PauseMenuUI : MonoBehaviour
    {
        [Tooltip("Director that owns the run — used to gate pausing and freeze the current launcher.")]
        public GameDirector director;

        [Tooltip("Scene loaded by 'Main Menu'. Must be in Build Settings.")]
        public string menuSceneName = "Menu";

        bool _paused;
        TireLauncher _frozen;   // the launcher we disabled on pause, to re-enable on resume

        GUIStyle _title, _heading, _btn, _btnBig, _hint;
        Texture2D _white;

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;

            if (_paused) Resume();
            else if (director == null || director.InActiveRun) Pause();
        }

        void Pause()
        {
            _paused = true;
            Time.timeScale = 0f;

            // Freeze flight input so held keys can't leak through the overlay.
            _frozen = director != null ? director.Launcher : null;
            if (_frozen != null) _frozen.enabled = false;

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayBtnClick();
                AudioManager.Instance.SetMusicMode(0); // menu music while paused
            }
        }

        void Resume()
        {
            _paused = false;
            Time.timeScale = 1f;

            if (_frozen != null) _frozen.enabled = true;
            _frozen = null;

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayBtnClick();
                AudioManager.Instance.SetMusicMode(1); // back to gameplay music
            }
        }

        void ToMainMenu()
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayBtnClick();
                AudioManager.Instance.SetMusicMode(1);
            }
            Time.timeScale = 1f; // timeScale carries across scene loads — restore before leaving
            SceneManager.LoadScene(menuSceneName);
        }

        void Quit()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayBtnClick();
            Time.timeScale = 1f;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // Safety net: never leave the game frozen if this object is torn down while paused.
        void OnDisable()
        {
            if (_paused) Time.timeScale = 1f;
        }

        void EnsureStyles()
        {
            if (_title != null) return;
            _white = Texture2D.whiteTexture;
            _title = new GUIStyle(GUI.skin.label)
            { fontSize = 48, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Overflow };
            _heading = new GUIStyle(GUI.skin.label)
            { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Overflow };
            _btn = new GUIStyle(GUI.skin.button) { fontSize = 16, fontStyle = FontStyle.Bold };
            _btnBig = new GUIStyle(GUI.skin.button) { fontSize = 21, fontStyle = FontStyle.Bold };
            _hint = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter, wordWrap = true, normal = { textColor = new Color(0.65f, 0.7f, 0.8f) } };
        }

        void Fill(Rect r, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _white);
            GUI.color = prev;
        }

        void OnGUI()
        {
            if (!_paused) return;
            EnsureStyles();
            GUI.depth = -100; // draw in front of the LaunchUI HUD (lower depth = nearer the front)
            HudScale.Begin();

            // Dark wash + gold accent bar — matches the main menu so the two screens read as a set.
            Fill(new Rect(0, 0, HudScale.VW, HudScale.VH), new Color(0.05f, 0.06f, 0.09f, 0.92f));
            Fill(new Rect(0, 0, HudScale.VW, 4f), new Color(1f, 0.8f, 0.2f, 0.9f));

            float bw = Mathf.Min(360f, HudScale.VW - 80f);
            float bx = (HudScale.VW - bw) * 0.5f;
            float full = HudScale.VW;

            const float titleH = 60f, subH = 24f, spacer = 38f, btnH = 54f, btnGap = 16f;
            float blockH = titleH + subH + spacer + btnH * 3f + btnGap * 2f;
            float y = Mathf.Max(24f, (HudScale.VH - blockH) * 0.5f);

            GUI.Label(new Rect(0, y, full, titleH), "PAUSED", _title);
            y += titleH;
            GUI.Label(new Rect(0, y, full, subH), "take a breather — the tire's not going anywhere", _hint);
            y += subH + spacer;

            var prev = GUI.backgroundColor;

            GUI.backgroundColor = new Color(0.2f, 0.7f, 0.3f);
            if (GUI.Button(new Rect(bx, y, bw, btnH), "RESUME", _btnBig)) Resume();
            y += btnH + btnGap;

            GUI.backgroundColor = new Color(0.28f, 0.5f, 0.78f);
            if (GUI.Button(new Rect(bx, y, bw, btnH), "MAIN MENU", _btnBig)) ToMainMenu();
            y += btnH + btnGap;

            GUI.backgroundColor = new Color(0.35f, 0.38f, 0.45f);
            if (GUI.Button(new Rect(bx, y, bw, btnH), "QUIT", _btnBig)) Quit();

            GUI.backgroundColor = prev;
            HudScale.End();
        }
    }
}
