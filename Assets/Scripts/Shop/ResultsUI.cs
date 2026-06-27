using UnityEngine;
using UnityEngine.InputSystem;

namespace LearnToSpin
{
    /// <summary>
    /// The Learn-to-Fly-style "round over" screen: pops up when a run ends and tallies the three
    /// tracked stats one by one, each spelling out <c>value × rate = +$reward</c> so it's obvious
    /// WHERE the money comes from and WHY, then sums to a total and counts the wallet up. Press
    /// Space/Enter once to fast-forward the animation, again (or click Continue) to open the shop.
    /// IMGUI to match the rest of the code-only UI.
    /// </summary>
    public class ResultsUI : MonoBehaviour
    {
        public GameDirector director;

        // animation timeline (seconds), unscaled
        const float IntroDur = 0.30f;
        const float RowsStart = 0.40f;
        const float RowStagger = 0.55f;
        const float RowCount = 0.45f;
        const float TotalAt = RowsStart + 4 * RowStagger;     // 2.60
        const float WalletStart = TotalAt + 0.15f;            // 2.20
        const float WalletDur = 0.60f;
        const float ContinueAt = WalletStart + WalletDur + 0.15f; // 2.95
        const float FullDur = ContinueAt + 0.05f;

        float _t;
        bool _wasOpen;

        GUIStyle _title, _sub, _label, _value, _rate, _reward, _total, _wallet, _btn, _hint;
        Texture2D _white;

        void EnsureStyles()
        {
            if (_title != null) return;
            _white = Texture2D.whiteTexture;
            _title = new GUIStyle(GUI.skin.label)
            { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _sub = new GUIStyle(GUI.skin.label)
            { fontSize = 14, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            _label = new GUIStyle(GUI.skin.label)
            { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            _value = new GUIStyle(GUI.skin.label) { fontSize = 17, alignment = TextAnchor.MiddleLeft };
            _rate = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
            _reward = new GUIStyle(GUI.skin.label)
            { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
            _total = new GUIStyle(GUI.skin.label)
            { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            _wallet = new GUIStyle(GUI.skin.label)
            { fontSize = 16, alignment = TextAnchor.MiddleRight };
            _btn = new GUIStyle(GUI.skin.button) { fontSize = 16, fontStyle = FontStyle.Bold };
            _hint = new GUIStyle(GUI.skin.label)
            { fontSize = 12, alignment = TextAnchor.MiddleCenter };
        }

        static readonly Color Gold = new Color(1f, 0.82f, 0.22f);

        static float Ease(float p) { p = Mathf.Clamp01(p); return 1f - Mathf.Pow(1f - p, 3f); }
        static float Smooth(float p) { p = Mathf.Clamp01(p); return p * p * (3f - 2f * p); }

        void Fill(Rect r, Color c)
        {
            var prev = GUI.color; GUI.color = c; GUI.DrawTexture(r, _white); GUI.color = prev;
        }

        bool _playedFinalDing = false; // Add this variable to your class

        void Update()
        {
            if (director == null) return;
            if (!director.ResultsOpen) { _wasOpen = false; _playedFinalDing = false; return; }
        
            if (!_wasOpen) { _t = 0f; _wasOpen = true; _playedFinalDing = false; }
            _t += Time.unscaledDeltaTime;
        
            // --- AUDIO: Loop money counting ---
            if (AudioManager.Instance != null)
            {
                bool isCounting = false;
                for (int i = 0; i < 4; i++) {
                    if (_t > (RowsStart + i * RowStagger) && _t < (RowsStart + i * RowStagger + RowCount)) isCounting = true;
                }
                if (_t > WalletStart && _t < (WalletStart + WalletDur)) isCounting = true;
        
                AudioManager.Instance.SetMoneyCounting(isCounting);
        
                // --- AUDIO: Final Ding Logic ---
                // Play only once when the wallet animation finishes
                if (_t >= (WalletStart + WalletDur) && !_playedFinalDing)
                {
                    AudioManager.Instance.PlayMoneyStop();
                    _playedFinalDing = true;
                }
            }
            // ==========================================
        
            var kb = Keyboard.current;
            if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
            {
                if (_t < FullDur) 
                {
                    _t = FullDur;     // first press: skip to the end
                    
                    // AUDIO: Immediately cut off the counting sound if the player skips the animation
                    if (AudioManager.Instance != null) AudioManager.Instance.SetMoneyCounting(false);
                }
                else 
                {
                    // AUDIO: Button click sound
                    if (AudioManager.Instance != null) AudioManager.Instance.PlayBtnClick();
                    
                    director.ContinueToShop();      // second press: move on
                }
            }
        }

        void OnGUI()
        {
            if (director == null || !director.ResultsOpen) return;
            EnsureStyles();
            var s = director.LastRun;

            Fill(new Rect(0, 0, Screen.width, Screen.height),
                 new Color(0.05f, 0.06f, 0.09f, 0.82f * Smooth(_t / IntroDur)));

            float pw = Mathf.Min(700f, Screen.width - 40f);
            float ph = Mathf.Min(524f, Screen.height - 40f);
            float px = (Screen.width - pw) * 0.5f;
            float py = (Screen.height - ph) * 0.5f;

            // pop-in: scale the whole panel up from 92%
            var center = new Vector2(px + pw * 0.5f, py + ph * 0.5f);
            float scale = Mathf.Lerp(0.92f, 1f, Smooth(_t / IntroDur));
            Matrix4x4 m = GUI.matrix;
            GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), center);

            var panel = new Rect(px, py, pw, ph);
            Fill(panel, new Color(0.11f, 0.12f, 0.16f, 0.98f));
            Fill(new Rect(px, py, pw, 3f), Gold);

            float ix = px + 26f, iw = pw - 52f;
            GUI.Label(new Rect(ix, py + 18f, iw, 40f), "LAUNCH COMPLETE!", _title);
            GUI.Label(new Rect(ix, py + 58f, iw, 20f),
                      "You earn cash for how FAR, how HIGH, how FAST, and how LONG you hung in the air.", _sub);

            // Better tires pay more per stat — surface the bonus so the higher rates below make sense.
            float em = s.earnMultiplier > 0.01f ? s.earnMultiplier : 1f;
            if (em > 1.001f)
            {
                SetAlpha(_sub, new Color(Gold.r, Gold.g, Gold.b, 1f));
                GUI.Label(new Rect(ix, py + 76f, iw, 18f),
                          $"⚡ {s.tireName}:  ×{em:0.0} earnings on every stat", _sub);
                SetAlpha(_sub, new Color(0.92f, 0.94f, 0.98f, 1f));
            }

            // stat rows — rates shown INCLUDE the tire's earnings multiplier so value × rate = reward.
            float rowY = py + 96f, rowH = 50f;
            var cat = director.Catalog;
            DrawRow(0, new Rect(ix, rowY + 0 * rowH, iw, rowH), "DISTANCE",
                    s.distance, "0", "m", cat.moneyPerMetre * em, "/m", s.distanceMoney);
            DrawRow(1, new Rect(ix, rowY + 1 * rowH, iw, rowH), "TOP SPEED",
                    s.topSpeed, "0.0", "m/s", cat.moneyPerTopSpeed * em, " per m/s", s.speedMoney);
            DrawRow(2, new Rect(ix, rowY + 2 * rowH, iw, rowH), "MAX HEIGHT",
                    s.maxHeight, "0.0", "m", cat.moneyPerHeight * em, "/m", s.heightMoney);
            DrawRow(3, new Rect(ix, rowY + 3 * rowH, iw, rowH), "AIR TIME",
                    s.airTime, "0.00", "s", cat.moneyPerAirTime * em, "/s", s.airMoney);

            // total + wallet
            float totY = rowY + 4 * rowH + 6f;
            float totA = Smooth((_t - TotalAt) / 0.2f);
            if (totA > 0.001f)
            {
                Fill(new Rect(ix, totY, iw, 2f), new Color(1f, 1f, 1f, 0.18f * totA));
                int total = Mathf.RoundToInt(Ease((_t - WalletStart) / WalletDur) * s.earned);
                SetAlpha(_total, totA);
                GUI.Label(new Rect(ix, totY + 8f, iw * 0.5f, 28f), "TOTAL EARNED", _total);
                SetAlpha(_total, new Color(Gold.r, Gold.g, Gold.b, totA));
                _total.alignment = TextAnchor.MiddleRight;
                GUI.Label(new Rect(ix + iw * 0.4f, totY + 8f, iw * 0.6f, 28f), $"+${total:N0}", _total);
                _total.alignment = TextAnchor.MiddleLeft;

                int wallet = s.moneyBefore + Mathf.RoundToInt(Ease((_t - WalletStart) / WalletDur) * s.earned);
                SetAlpha(_wallet, totA);
                GUI.Label(new Rect(ix, totY + 40f, iw, 22f),
                          $"Wallet:  ${s.moneyBefore:N0}  →  ${wallet:N0}", _wallet);
            }

            // continue
            if (_t >= ContinueAt)
            {
                var prev = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.2f, 0.7f, 0.3f);
                if (GUI.Button(new Rect(ix, py + ph - 58f, iw, 42f), "Continue to Shop  ▶", _btn))
                {
                    if (AudioManager.Instance != null) AudioManager.Instance.PlayBtnClick(); // AUDIO
                    director.ContinueToShop();
                }
                GUI.backgroundColor = prev;
            }
            else
            {
                GUI.Label(new Rect(ix, py + ph - 50f, iw, 22f),
                          "press [Space] to skip", _hint);
            }

            GUI.matrix = m;
            GUI.color = Color.white;
        }

        /// <summary>
        /// One animated tally line: the stat counts up, then its reward, laid out as
        /// <c>LABEL   value unit   × $rate/unit   = +$reward</c>.
        /// </summary>
        void DrawRow(int i, Rect r, string label, float target, string fmt, string unit,
                     float rate, string rateSuffix, int reward)
        {
            float start = RowsStart + i * RowStagger;
            float alpha = Smooth((_t - start) / 0.18f);
            if (alpha <= 0.001f) return;
            float p = Ease((_t - start) / RowCount);

            Fill(r, new Color(1f, 1f, 1f, 0.05f * alpha));

            float val = p * target;
            int paid = Mathf.RoundToInt(p * reward);

            float cy = r.y + 4f, ch = r.height - 8f;
            SetAlpha(_label, alpha);
            GUI.Label(new Rect(r.x + 12f, cy, 150f, ch), label, _label);

            SetAlpha(_value, alpha);
            GUI.Label(new Rect(r.x + 150f, cy, 120f, ch), $"{val.ToString(fmt)} {unit}", _value);

            SetAlpha(_rate, new Color(0.8f, 0.85f, 0.95f, alpha));
            GUI.Label(new Rect(r.x + 270f, cy, 150f, ch), $"×  ${rate:0.#}{rateSuffix}", _rate);

            SetAlpha(_reward, new Color(Gold.r, Gold.g, Gold.b, alpha));
            GUI.Label(new Rect(r.xMax - 150f, cy, 138f, ch), $"=  +${paid:N0}", _reward);
        }

        void SetAlpha(GUIStyle st, float a) => st.normal.textColor = new Color(0.92f, 0.94f, 0.98f, a);
        void SetAlpha(GUIStyle st, Color c) => st.normal.textColor = c;
    }
}
