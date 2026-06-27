using UnityEngine;

namespace LearnToSpin
{
    /// <summary>
    /// One-shot impact burst played where the tire clips a hazard: a soft dust puff plus a
    /// scatter of bright sparks that fly out and fall. Like the rest of the game it's built
    /// entirely in code (no prefabs/assets) and tears itself down when it finishes, so it's
    /// WebGL-safe and needs no wiring — call <see cref="Play"/> from anywhere.
    /// </summary>
    public static class HazardHitFX
    {
        static Material _dustMat;
        static Material _sparkMat;
        static Texture2D _dot;

        /// <summary>
        /// Spawn the burst at <paramref name="position"/>. <paramref name="strength"/> (0..1, how
        /// hard the hit was) scales particle count and speed so a fast clip pops harder than a
        /// crawl. <paramref name="dustTint"/> colours the puff (e.g. the hazard's own colour).
        /// </summary>
        public static void Play(Vector3 position, float strength, Color dustTint)
        {
            strength = Mathf.Clamp01(strength);

            var root = new GameObject("HazardHitFX");
            root.transform.position = position;

            float life = 0.85f;
            BuildDust(root.transform, strength, dustTint);
            BuildSparks(root.transform, strength);

            // Outlive the longest particle (dust life + sparks' gravity fall) before cleanup.
            Object.Destroy(root, life + 0.6f);
        }

        static void BuildDust(Transform parent, float strength, Color tint)
        {
            var go = new GameObject("Dust");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 0.4f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f + 3f * strength, 3f + 6f * strength);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.7f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = -0.15f;            // drifts gently upward as it expands
            main.maxParticles = 64;
            main.startColor = tint;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.RoundToInt(10 + 18 * strength)) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.35f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = FadeGradient(1f);

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.6f));

            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.material = DustMaterial();
            psr.sortingOrder = 0;
            ps.Play();
        }

        static void BuildSparks(Transform parent, float strength)
        {
            var go = new GameObject("Sparks");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 0.3f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f + 6f * strength, 9f + 10f * strength);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
            main.gravityModifier = 2.2f;              // arc up then fall
            main.maxParticles = 48;
            // warm spark colour, slightly varied
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.85f, 0.4f), new Color(1f, 0.55f, 0.15f));

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.RoundToInt(6 + 14 * strength)) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 55f;
            shape.radius = 0.1f;
            shape.rotation = new Vector3(-90f, 0f, 0f); // spray upward/outward from the ground

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = FadeGradient(1f);

            // stretch the sparks along their travel so they read as streaks
            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.renderMode = ParticleSystemRenderMode.Stretch;
            psr.lengthScale = 2.5f;
            psr.velocityScale = 0.08f;
            psr.material = SparkMaterial();
            psr.sortingOrder = 1;
            ps.Play();
        }

        /// <summary>White→transparent fade so particles dissolve over their lifetime.</summary>
        static Gradient FadeGradient(float startAlpha)
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(startAlpha, 0f), new GradientAlphaKey(startAlpha, 0.5f),
                        new GradientAlphaKey(0f, 1f) });
            return g;
        }

        static Material DustMaterial()
        {
            if (_dustMat != null) return _dustMat;
            _dustMat = ParticleMaterial();
            return _dustMat;
        }

        static Material SparkMaterial()
        {
            if (_sparkMat != null) return _sparkMat;
            _sparkMat = ParticleMaterial();
            return _sparkMat;
        }

        /// <summary>
        /// A soft-dot, alpha-blended, vertex-coloured particle material. Tries the URP particle
        /// shader, then falls back through the always-available ones so it survives a stripped
        /// WebGL build the same way <see cref="BuildUtils.Lit"/> resolves its shader.
        /// </summary>
        static Material ParticleMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");

            var m = new Material(shader);
            var tex = DotTexture();
            m.mainTexture = tex;
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            // Nudge the URP particle shader to transparent/alpha if those properties exist.
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
            return m;
        }

        /// <summary>A 32px radial soft dot, generated once, so particles are round and feathered.</summary>
        static Texture2D DotTexture()
        {
            if (_dot != null) return _dot;
            const int s = 32;
            _dot = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var c = new Vector2((s - 1) * 0.5f, (s - 1) * 0.5f);
            float r = s * 0.5f;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), c) / r;
                    float a = Mathf.Clamp01(1f - d);
                    a *= a; // feather the edge
                    _dot.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            _dot.Apply();
            return _dot;
        }
    }
}
