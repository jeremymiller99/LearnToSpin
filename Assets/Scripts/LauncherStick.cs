using UnityEngine;

namespace LearnToSpin
{
    /// <summary>
    /// Animates the launch "stick" that pokes the spinning engine. While the player holds Space the
    /// tire is revving (<see cref="TireLauncher.State.Charging"/>), so the stick SLIDES BACK (+X) in
    /// step with the stored charge; the instant Space is released and the tire launches, the stick
    /// shoots forward to its original position with a springy overshoot (easeOutBack), reading as the
    /// poke that kicks the wheel off.
    ///
    /// Drop this on the stick GameObject. The tire/launcher is built procedurally at runtime by
    /// <see cref="GameBootstrap"/>, so this finds the live <see cref="TireLauncher"/> on its own and
    /// re-acquires it after each day's rebuild — no inspector wiring to the tire required.
    /// </summary>
    public class LauncherStick : MonoBehaviour
    {
        [Tooltip("The transform to move. Defaults to this object.")]
        public Transform stick;

        [Header("Wind-back (while holding Space)")]
        [Tooltip("Local direction the stick slides as it winds back (default +X).")]
        public Vector3 windAxis = Vector3.right;
        [Tooltip("Distance the stick is pulled back at FULL charge. Negative flips the direction.")]
        public float maxWindDistance = 0.6f;
        [Tooltip("How snappily the stick chases the charge as it revs. Higher = tighter follow.")]
        public float windSharpness = 12f;

        [Header("Release (spring forward)")]
        [Tooltip("Seconds for the stick to snap back to rest after release.")]
        public float returnDuration = 0.35f;
        [Tooltip("Overshoot strength of the snap-back. 0 = no overshoot, ~1.7 = classic, higher = whippier.")]
        public float overshoot = 2.5f;

        TireLauncher _launcher;
        Vector3 _restPos;
        float _offset;         // current wind distance applied this frame
        bool _wasCharging;
        bool _returning;
        float _returnT;
        float _returnFrom;     // wind distance the snap-back starts from

        void Awake()
        {
            if (stick == null) stick = transform;
            _restPos = stick.localPosition;
        }

        void Update()
        {
            if (_launcher == null) _launcher = FindFirstObjectByType<TireLauncher>();
            var state = _launcher != null ? _launcher.CurrentState : TireLauncher.State.Ready;

            if (state == TireLauncher.State.Charging)
            {
                // Track the stored charge: the harder it's revved, the further the stick winds back.
                _returning = false;
                _wasCharging = true;
                float target = maxWindDistance * _launcher.ChargeNormalized;
                // Exponential smoothing so the follow is smooth, not jittery, at any framerate.
                _offset = Mathf.Lerp(_offset, target, 1f - Mathf.Exp(-windSharpness * Time.deltaTime));
                Apply();
                return;
            }

            // Left the charge state (launched, or reset) — kick off the springy snap to rest once.
            if (_wasCharging)
            {
                _wasCharging = false;
                _returning = true;
                _returnT = 0f;
                _returnFrom = _offset;
            }

            if (_returning)
            {
                _returnT += Time.deltaTime / Mathf.Max(0.01f, returnDuration);
                float k = EaseOutBack(Mathf.Clamp01(_returnT), overshoot);
                _offset = Mathf.LerpUnclamped(_returnFrom, 0f, k); // k>1 carries it PAST rest, then settles
                if (_returnT >= 1f) { _returning = false; _offset = 0f; }
                Apply();
            }
        }

        void Apply()
        {
            Vector3 dir = windAxis.sqrMagnitude > 0f ? windAxis.normalized : Vector3.right;
            stick.localPosition = _restPos + dir * _offset;
        }

        /// <summary>easeOutBack: shoots past the target then settles — the overshoot on the snap-back.</summary>
        static float EaseOutBack(float t, float s)
        {
            float t1 = t - 1f;
            return 1f + (s + 1f) * t1 * t1 * t1 + s * t1 * t1;
        }
    }
}
