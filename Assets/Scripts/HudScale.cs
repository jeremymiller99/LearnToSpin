using UnityEngine;

namespace LearnToSpin
{
    /// <summary>
    /// Shared scaling for the code-only IMGUI screens. The game is authored for a 960×600 web canvas,
    /// so every <c>OnGUI</c> lays out in that fixed "design" space and this helper stretches it onto
    /// the real window with a single <see cref="GUI.matrix"/>. The scale is uniform (min of the two
    /// axis ratios) so nothing distorts or overflows, and <see cref="VW"/>/<see cref="VH"/> give the
    /// design-space window size to anchor edges against — at 960×600 they are exactly 960/600, and at
    /// any larger/fullscreen size the UI grows proportionally instead of clinging to the top-left.
    ///
    /// Usage in an OnGUI: call <see cref="Begin"/> first, lay everything out using VW/VH instead of
    /// Screen.width/height, and call <see cref="End"/> last so the next screen starts from identity.
    /// World-anchored UI (e.g. a label over the tire) converts its pixel point with <see cref="ToVirtual"/>.
    /// </summary>
    public static class HudScale
    {
        /// <summary>The design canvas the UI is laid out against (the web target resolution).</summary>
        public const float RefW = 960f, RefH = 600f;

        /// <summary>Upper bound on the design→screen factor. 1 keeps the UI at its authored pixel size on
        /// larger/fullscreen windows (only ever shrinking to fit smaller ones); raise it to let the UI grow.</summary>
        public const float MaxScale = 1f;

        /// <summary>Uniform design→screen factor. Shrinks below 1 to fit windows smaller than the design
        /// canvas, but is capped at <see cref="MaxScale"/> so it never blows the UI up on big displays.</summary>
        public static float Scale => Mathf.Clamp(Mathf.Min(Screen.width / RefW, Screen.height / RefH), 0.0001f, MaxScale);

        /// <summary>Window width/height expressed in design units (≥ RefW/RefH; matches the real aspect).</summary>
        public static float VW => Screen.width / Scale;
        public static float VH => Screen.height / Scale;

        /// <summary>Install the design→screen transform. Layout after this uses design units / VW / VH.</summary>
        public static void Begin()
        {
            float s = Scale;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s, s, 1f));
        }

        /// <summary>Restore the identity transform so a later OnGUI isn't left in scaled space.</summary>
        public static void End() => GUI.matrix = Matrix4x4.identity;

        /// <summary>Convert a real-pixel screen point (e.g. from Camera.WorldToScreenPoint) into the
        /// design space, so world-anchored UI lines up once <see cref="Begin"/> is active.</summary>
        public static Vector2 ToVirtual(Vector2 screenPixels) => screenPixels / Scale;
    }
}
