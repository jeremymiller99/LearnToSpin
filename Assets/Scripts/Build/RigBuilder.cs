using UnityEngine;

namespace LearnToSpin
{
    /// <summary>
    /// Builds the presentation rig that frames the run: the directional sun, the chase camera
    /// (with view-distance fog) following the tire, and the launch HUD.
    /// </summary>
    public static class RigBuilder
    {
        public static void BuildLighting()
        {
            if (Object.FindFirstObjectByType<Light>() != null) return;
            var go = new GameObject("Sun");
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = 1.1f;
            l.shadows = LightShadows.Soft;
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        public static void BuildCamera(GameBootstrap boot, Transform target)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            var chase = cam.GetComponent<ChaseCamera>();
            if (chase == null) chase = cam.gameObject.AddComponent<ChaseCamera>();
            chase.target = target;

            // Speed juice: FOV stretch + radial speed lines once the tire gets quick. Reads the
            // tire through chase.target, so it self-follows a rebuilt tire with no extra wiring.
            if (cam.GetComponent<SpeedFX>() == null) cam.gameObject.AddComponent<SpeedFX>();

            // Render distance + matching fog. Distant scenery fades out before the far-clip
            // plane culls it, so lowering viewDistance is a clean perf win (no horizon seam).
            var view = cam.GetComponent<ViewDistance>();
            if (view == null) view = cam.gameObject.AddComponent<ViewDistance>();
            view.fogStartFraction = boot.fogStartFraction;
            view.fogColor = boot.fogColor;
            view.maxDistance = Mathf.Max(view.maxDistance, boot.runwayLength);
            view.SetDefault(boot.viewDistance); // seeds the first run; a saved tweak from a prior run wins
        }

        public static void BuildUI(TireLauncher launcher)
        {
            var ui = new GameObject("LaunchUI").AddComponent<LaunchUI>();
            ui.launcher = launcher;
        }
    }
}
