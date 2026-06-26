using UnityEngine;
using UnityEngine.InputSystem;

namespace LearnToSpin
{
    /// <summary>
    /// Performance knob: how far the world renders. Lives on the camera and drives two
    /// things together so they always agree —
    ///   • the camera's far clip plane (objects past it are never drawn → fewer draw calls), and
    ///   • linear distance fog that fades the world out just BEFORE the clip plane, so the
    ///     hard cut never shows: distant trees/lamps melt into the horizon instead of popping.
    /// Lower <see cref="viewDistance"/> = cheaper to render (good for WebGL / weak machines),
    /// higher = see further. Tweak it in the inspector, live in Play with [ and ], and it's
    /// remembered across runs via PlayerPrefs.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class ViewDistance : MonoBehaviour
    {
        [Header("Render distance")]
        [Tooltip("Metres the world renders out to. This becomes the camera's far clip plane and " +
                 "the fog's far edge. Lower is faster.")]
        [Range(50f, 2000f)] public float viewDistance = 450f;

        [Tooltip("Where the fog STARTS, as a fraction of the view distance. 0.55 means the world " +
                 "is clear out to 55% of the range, then fades to fully fogged at the clip plane. " +
                 "Lower = thicker, earlier fog (hides more, but a hazier look).")]
        [Range(0.1f, 0.95f)] public float fogStartFraction = 0.55f;

        [Header("Look")]
        [Tooltip("Fog / horizon colour. Pick something close to the sky so the fade is invisible.")]
        public Color fogColor = new Color(0.74f, 0.80f, 0.88f);
        [Tooltip("Also paint the camera background this colour (clears to a solid horizon). Hides the " +
                 "clip plane completely; turn off if you want a skybox behind the fog instead.")]
        public bool matchBackgroundToFog = true;

        [Header("Live tweak")]
        [Tooltip("Allow adjusting the view distance in Play with the bracket keys.")]
        public bool allowHotkeys = true;
        [Tooltip("Metres per second the range changes while [ or ] is held (Shift = 4x faster).")]
        public float adjustStep = 250f;
        [Range(50f, 5000f)] public float minDistance = 100f;
        [Range(50f, 5000f)] public float maxDistance = 1500f;

        const string PrefsKey = "lts_view_distance";

        Camera _cam;

        /// <summary>Current render distance in metres (for the HUD).</summary>
        public float Current => viewDistance;

        void Awake()
        {
            _cam = GetComponent<Camera>();
            if (PlayerPrefs.HasKey(PrefsKey))
                viewDistance = PlayerPrefs.GetFloat(PrefsKey);
            Apply();
        }

        void Update()
        {
            if (!allowHotkeys) return;
            var kb = Keyboard.current;
            if (kb == null) return;

            float delta = 0f;
            // hold-to-repeat with the bracket keys; Shift for a coarse 4x step
            if (kb.leftBracketKey.isPressed) delta -= 1f;
            if (kb.rightBracketKey.isPressed) delta += 1f;
            if (delta == 0f) return;

            float scale = (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed) ? 4f : 1f;
            SetDistance(viewDistance + delta * adjustStep * scale * Time.unscaledDeltaTime);
        }

        /// <summary>Set the render distance (metres), clamp it, apply, and remember it.</summary>
        public void SetDistance(float metres)
        {
            viewDistance = Mathf.Clamp(metres, minDistance, maxDistance);
            Apply();
            PlayerPrefs.SetFloat(PrefsKey, viewDistance);
        }

        /// <summary>
        /// Apply a default render distance, but only if the player hasn't saved their own (a tweak
        /// from a previous run wins). Used by GameBootstrap so its inspector value seeds the first
        /// run without overwriting a remembered preference. Re-applies regardless so the latest
        /// fog colour / start fraction take effect.
        /// </summary>
        public void SetDefault(float metres)
        {
            if (!PlayerPrefs.HasKey(PrefsKey))
                viewDistance = Mathf.Clamp(metres, minDistance, maxDistance);
            Apply();
        }

        /// <summary>Push the current settings onto the camera and the scene's fog.</summary>
        public void Apply()
        {
            if (_cam == null) _cam = GetComponent<Camera>();

            // Keep the far plane a hair past the fog so geometry is fully fogged before it clips.
            _cam.farClipPlane = viewDistance * 1.02f;
            if (matchBackgroundToFog)
            {
                _cam.clearFlags = CameraClearFlags.SolidColor;
                _cam.backgroundColor = fogColor;
            }

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogStartDistance = viewDistance * fogStartFraction;
            RenderSettings.fogEndDistance = viewDistance;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (maxDistance < minDistance) maxDistance = minDistance;
            viewDistance = Mathf.Clamp(viewDistance, minDistance, maxDistance);
            if (isActiveAndEnabled) Apply();
        }
#endif
    }
}
