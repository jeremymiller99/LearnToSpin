using UnityEngine;
using UnityEngine.SceneManagement;

namespace LearnToSpin
{
    /// <summary>
    /// The Menu scene's front-end, drawn in IMGUI to match the rest of the game's code-only UI (no
    /// Canvas wiring — same Fill/Outline/GUIStyle approach as <see cref="ShopUI"/>). Three screens:
    /// the title (PLAY / SETTINGS / QUIT), a profile picker with the three save slots, and a
    /// (currently empty) settings screen. Picking a slot sets <see cref="PlayerProgress.ActiveSlot"/>
    /// and loads the Game scene, which then loads that profile. Each existing slot can be deleted
    /// (with a confirm) to reset it back to empty.
    ///
    /// Every screen is laid out as a single block centred on the window, so nothing clips or runs off
    /// regardless of the Game-view resolution. Just drop this on a GameObject in the Menu scene —
    /// OnGUI needs no camera/Canvas/EventSystem.
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        [Tooltip("Scene loaded when a profile is played. Must be in Build Settings.")]
        public string gameSceneName = "Game";

        enum Screen { Title, Profiles, Settings }
        Screen _screen = Screen.Title;
        int _confirmDelete = -1; // slot index awaiting a delete confirm, or -1

        GUIStyle _title, _heading, _subtitle, _slotName, _slotInfo, _btn, _btnBig, _hint;
        Texture2D _white;

        void EnsureStyles()
        {
            if (_title != null) return;
            _white = Texture2D.whiteTexture;
            _title = new GUIStyle(GUI.skin.label)
            { fontSize = 52, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Overflow };
            _heading = new GUIStyle(GUI.skin.label)
            { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Overflow };
            _subtitle = new GUIStyle(GUI.skin.label)
            { fontSize = 17, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.8f, 0.85f, 0.95f) } };
            _slotName = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            _slotInfo = new GUIStyle(GUI.skin.label) { fontSize = 15, normal = { textColor = new Color(0.8f, 0.85f, 0.95f) } };
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

        void Outline(Rect r, float t, Color c)
        {
            Fill(new Rect(r.x, r.y, r.width, t), c);
            Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
            Fill(new Rect(r.x, r.y, t, r.height), c);
            Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
        }

        // Top of a vertically-centred block of the given height (never above a small top margin).
        static float TopFor(float blockH) => Mathf.Max(24f, (HudScale.VH - blockH) * 0.5f);

        void OnGUI()
        {
            EnsureStyles();
            HudScale.Begin();

            // Backdrop — a dark wash so the IMGUI reads on any skybox, plus the gold accent bar.
            Fill(new Rect(0, 0, HudScale.VW, HudScale.VH), new Color(0.05f, 0.06f, 0.09f, 0.92f));
            Fill(new Rect(0, 0, HudScale.VW, 4f), new Color(1f, 0.8f, 0.2f, 0.9f));

            switch (_screen)
            {
                case Screen.Title: DrawTitle(); break;
                case Screen.Profiles: DrawProfiles(); break;
                case Screen.Settings: DrawSettings(); break;
            }

            HudScale.End();
        }

        void DrawTitle()
        {
            float bw = Mathf.Min(360f, HudScale.VW - 80f);
            float bx = (HudScale.VW - bw) * 0.5f;
            float full = HudScale.VW; // title spans the window so it never clips

            const float titleH = 64f, subH = 26f, spacer = 40f, btnH = 54f, btnGap = 16f;
            float blockH = titleH + subH + spacer + btnH * 3f + btnGap * 2f;
            float y = TopFor(blockH);

            GUI.Label(new Rect(0, y, full, titleH), "LEARN TO SPIN", _title);
            y += titleH;
            GUI.Label(new Rect(0, y, full, subH), "rev it up. poke it off. send the tire.", _subtitle);
            y += subH + spacer;

            var prev = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.2f, 0.7f, 0.3f);
            if (GUI.Button(new Rect(bx, y, bw, btnH), "PLAY", _btnBig)) _screen = Screen.Profiles;
            y += btnH + btnGap;

            GUI.backgroundColor = new Color(0.28f, 0.5f, 0.78f);
            if (GUI.Button(new Rect(bx, y, bw, btnH), "SETTINGS", _btnBig)) _screen = Screen.Settings;
            y += btnH + btnGap;

            GUI.backgroundColor = new Color(0.35f, 0.38f, 0.45f);
            if (GUI.Button(new Rect(bx, y, bw, btnH), "QUIT", _btnBig)) Quit();
            GUI.backgroundColor = prev;
        }

        void DrawProfiles()
        {
            float w = Mathf.Min(620f, HudScale.VW - 40f);
            float x = (HudScale.VW - w) * 0.5f;

            const float headH = 44f, headGap = 22f, slotH = 96f, slotGap = 14f, backGap = 14f, backH = 42f;
            float blockH = headH + headGap + (slotH + slotGap) * PlayerProgress.SlotCount + backGap + backH;
            float y = TopFor(blockH);

            GUI.Label(new Rect(x, y, w, headH), "SELECT PROFILE", _heading);
            y += headH + headGap;

            for (int i = 0; i < PlayerProgress.SlotCount; i++)
            {
                DrawSlot(i, new Rect(x, y, w, slotH));
                y += slotH + slotGap;
            }

            y += backGap;
            if (GUI.Button(new Rect(x, y, w, backH), "Back", _btn)) { _screen = Screen.Title; _confirmDelete = -1; }
        }

        void DrawSlot(int slot, Rect r)
        {
            var sum = PlayerProgress.Peek(slot);

            Fill(r, new Color(1f, 1f, 1f, 0.05f));
            Outline(r, 1f, new Color(1f, 1f, 1f, 0.12f));

            float pad = 18f;
            GUI.Label(new Rect(r.x + pad, r.y + 14f, r.width * 0.5f, 28f), $"Profile {slot + 1}", _slotName);
            GUI.Label(new Rect(r.x + pad, r.y + 48f, r.width * 0.5f, 24f),
                      sum.exists ? $"Day {sum.day}   ·   ${sum.money:N0}" : "Empty — start a new game", _slotInfo);

            // Delete-confirm takes over the right side of the row so it can't be missed.
            if (_confirmDelete == slot) { DrawDeleteConfirm(slot, r); return; }

            float bh = 46f, bgap = 10f;
            float by = r.y + (r.height - bh) * 0.5f;
            float right = r.xMax - pad;
            var prev = GUI.backgroundColor;

            // Delete (only for occupied slots) sits to the left of the primary action.
            if (sum.exists)
            {
                float dw = 92f;
                GUI.backgroundColor = new Color(0.7f, 0.25f, 0.25f);
                if (GUI.Button(new Rect(right - 150f - bgap - dw, by, dw, bh), "Delete", _btn))
                    _confirmDelete = slot;
            }

            // Primary action: continue an existing profile, or start a new one in this slot.
            float cw = 150f;
            GUI.backgroundColor = new Color(0.2f, 0.7f, 0.3f);
            if (GUI.Button(new Rect(right - cw, by, cw, bh), sum.exists ? "Continue" : "New Game", _btnBig))
            {
                if (sum.exists) PlayerProgress.ActiveSlot = slot;
                else PlayerProgress.CreateNewProfile(slot);
                LoadGame();
            }
            GUI.backgroundColor = prev;
        }

        void DrawDeleteConfirm(int slot, Rect r)
        {
            float pad = 18f;
            GUI.Label(new Rect(r.x + pad, r.y + 48f, r.width * 0.4f, 24f), "Delete this profile?",
                      new GUIStyle(_slotInfo) { normal = { textColor = new Color(1f, 0.7f, 0.7f) } });

            float bw = 112f, bh = 46f, bgap = 10f;
            float by = r.y + (r.height - bh) * 0.5f;
            float right = r.xMax - pad;
            var prev = GUI.backgroundColor;

            GUI.backgroundColor = new Color(0.7f, 0.25f, 0.25f);
            if (GUI.Button(new Rect(right - bw, by, bw, bh), "Delete", _btn))
            {
                PlayerProgress.DeleteSlot(slot);
                _confirmDelete = -1;
            }
            GUI.backgroundColor = new Color(0.35f, 0.38f, 0.45f);
            if (GUI.Button(new Rect(right - bw - bgap - bw, by, bw, bh), "Cancel", _btn))
                _confirmDelete = -1;
            GUI.backgroundColor = prev;
        }

        void DrawSettings()
        {
            float w = Mathf.Min(520f, HudScale.VW - 40f);
            float x = (HudScale.VW - w) * 0.5f;

            const float headH = 44f, headGap = 22f, boxH = 160f, boxGap = 16f, backH = 42f;
            float blockH = headH + headGap + boxH + boxGap + backH;
            float y = TopFor(blockH);

            GUI.Label(new Rect(x, y, w, headH), "SETTINGS", _heading);
            y += headH + headGap;

            var box = new Rect(x, y, w, boxH);
            Fill(box, new Color(1f, 1f, 1f, 0.05f));
            Outline(box, 1f, new Color(1f, 1f, 1f, 0.12f));
            GUI.Label(new Rect(box.x + 20f, box.y, box.width - 40f, box.height),
                      "Nothing here yet —\nsettings will live on this screen.", _hint);
            y += boxH + boxGap;

            if (GUI.Button(new Rect(x, y, w, backH), "Back", _btn)) _screen = Screen.Title;
        }

        void LoadGame()
        {
            SceneManager.LoadScene(gameSceneName);
        }

        void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
