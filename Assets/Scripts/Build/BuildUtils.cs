using UnityEngine;

namespace LearnToSpin
{
    /// <summary>How a placed prefab sits relative to y=0: base on the ground, top flush, or centred.</summary>
    public enum Sit { Base, Top, Center }

    /// <summary>
    /// Shared helpers for the procedural world builders: material creation, renderer-bounds
    /// measurement, and pivot-robust placement. Static so every *Builder lays prefabs down the
    /// same way (measured from real renderer bounds) without duplicating the maths.
    /// </summary>
    public static class BuildUtils
    {
        /// <summary>A URP/Lit (or Standard fallback) material of the given colour.</summary>
        public static Material Lit(Color c)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var m = new Material(shader != null ? shader : Shader.Find("Standard"));
            m.color = c;
            return m;
        }

        /// <summary>World-space AABB of every renderer under <paramref name="go"/>.</summary>
        public static Bounds RendererBounds(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return b;
        }

        /// <summary>
        /// Instantiate <paramref name="prefab"/>, scale/rotate it, then position it so its
        /// footprint is centred at (x,z) and it sits on the ground per <paramref name="sit"/>
        /// (base on the ground, top flush with y=0, or centred on y=0). Robust to any pivot.
        /// </summary>
        public static GameObject Place(GameObject prefab, float x, float z, Quaternion rot, float scale,
                                       Sit sit, Transform parent, bool stripColliders)
        {
            var go = Object.Instantiate(prefab);
            go.transform.SetParent(parent, true);
            go.transform.rotation = rot;
            go.transform.localScale *= scale;
            go.transform.position = Vector3.zero;

            if (stripColliders)
                foreach (var c in go.GetComponentsInChildren<Collider>()) Object.Destroy(c);

            Bounds b = RendererBounds(go);
            float y = sit switch
            {
                Sit.Top => -b.max.y,
                Sit.Center => -b.center.y,
                _ => -b.min.y,
            };
            go.transform.position = new Vector3(x - b.center.x, y, z - b.center.z);
            return go;
        }
    }
}
