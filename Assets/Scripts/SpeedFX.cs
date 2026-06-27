using UnityEngine;

namespace LearnToSpin
{
    /// <summary>
    /// Pure-juice speed feedback on the chase camera: as the tire gets fast we ease the camera
    /// field-of-view outward (the world rushes past) and overlay radial "anime" speed lines that
    /// stream from the edges toward you. Both fade in/out smoothly with speed so slow rolling and
    /// the menus stay clean. Reads the tire through <see cref="ChaseCamera.target"/>, so it needs
    /// no extra wiring and automatically follows a rebuilt tire between runs. Drawn in IMGUI to
    /// match the rest of the project's HUD and stay asset-free / WebGL-safe.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class SpeedFX : MonoBehaviour
    {
        [Header("Speed → intensity (m/s)")]
        [Tooltip("Below this forward speed there is no effect at all.")]
        public float speedStart = 14f;
        [Tooltip("At/above this speed the effect is at full strength.")]
        public float speedFull = 42f;

        [Header("Field of view")]
        [Tooltip("Degrees added to the base FOV at full speed.")]
        public float fovBoost = 14f;
        [Tooltip("Lower = snappier FOV response.")]
        public float fovSmooth = 0.25f;

        [Header("Speed lines")]
        public int lineCount = 30;
        public Color lineColor = new Color(1f, 1f, 1f, 0.5f);
        [Tooltip("How fast the streaks travel inward, cycles/sec.")]
        public float streamSpeed = 1.6f;
        [Tooltip("Clear radius in the screen centre (fraction of the short screen edge).")]
        [Range(0f, 0.6f)] public float innerClear = 0.26f;
        [Tooltip("Streak length at full speed (fraction of the short screen edge).")]
        [Range(0.05f, 0.6f)] public float maxLength = 0.32f;

        Camera _cam;
        ChaseCamera _chase;
        Transform _target;
        Rigidbody _targetBody;

        float _baseFov;
        float _curFov;
        float _fovVel;

        // Per-streak randomised angle + phase offset, seeded once so the pattern is stable.
        float[] _angle;
        float[] _phase;
        float[] _thick;

        void Awake()
        {
            _cam = GetComponent<Camera>();
            _chase = GetComponent<ChaseCamera>();
            _baseFov = _cam.fieldOfView;
            _curFov = _baseFov;

            var rng = new System.Random(12345);
            _angle = new float[lineCount];
            _phase = new float[lineCount];
            _thick = new float[lineCount];
            for (int i = 0; i < lineCount; i++)
            {
                _angle[i] = (float)rng.NextDouble() * 360f;
                _phase[i] = (float)rng.NextDouble();
                _thick[i] = 1.5f + (float)rng.NextDouble() * 2.5f;
            }
        }

        /// <summary>Current 0..1 effect strength from the tire's speed (0 unless it's launched & quick).</summary>
        float Intensity()
        {
            Rigidbody body = ResolveBody();
            float speed = body != null ? body.linearVelocity.magnitude : 0f;
            return Mathf.Clamp01(Mathf.InverseLerp(speedStart, speedFull, speed));
        }

        Rigidbody ResolveBody()
        {
            Transform t = _chase != null ? _chase.target : _target;
            if (t == null) { _targetBody = null; return null; }
            if (t != _target || _targetBody == null) // target swaps on each rebuilt run
            {
                _target = t;
                _targetBody = t.GetComponent<Rigidbody>();
            }
            return _targetBody;
        }

        void LateUpdate()
        {
            // Run after ChaseCamera (also LateUpdate); FOV doesn't depend on the transform anyway.
            float targetFov = _baseFov + fovBoost * Intensity();
            _curFov = Mathf.SmoothDamp(_curFov, targetFov, ref _fovVel, fovSmooth);
            _cam.fieldOfView = _curFov;
        }

        void OnGUI()
        {
            float intensity = Intensity();
            if (intensity <= 0.001f || Event.current.type != EventType.Repaint) return;

            // Lay the streaks out in the shared 960×600 design space so they fill the window and the
            // streak thickness scales with it (instead of staying a fixed pixel width at high res).
            HudScale.Begin();
            float w = HudScale.VW, h = HudScale.VH;
            float cx = w * 0.5f, cy = h * 0.5f;
            float shortEdge = Mathf.Min(w, h);
            float inner = innerClear * shortEdge;
            float len = maxLength * shortEdge * Mathf.Lerp(0.5f, 1f, intensity);
            float t = Time.unscaledTime * streamSpeed;

            Vector2 pivot = new Vector2(cx, cy);
            Matrix4x4 baseMatrix = GUI.matrix;
            Color prev = GUI.color;

            for (int i = 0; i < _angle.Length; i++)
            {
                // Each streak cycles outward→in; alpha swells then fades so it reads as motion.
                float phase = Mathf.Repeat(t + _phase[i], 1f);
                float r0 = Mathf.Lerp(shortEdge * 0.62f, inner, phase);
                float alpha = Mathf.Sin(phase * Mathf.PI) * intensity * lineColor.a;
                if (alpha <= 0.001f) continue;

                GUI.color = new Color(lineColor.r, lineColor.g, lineColor.b, alpha);
                GUI.matrix = baseMatrix;
                GUIUtility.RotateAroundPivot(_angle[i], pivot);
                var rect = new Rect(cx + r0, cy - _thick[i] * 0.5f, len, _thick[i]);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
            }

            GUI.matrix = baseMatrix;
            GUI.color = prev;
            HudScale.End();
        }
    }
}
