using UnityEngine;

namespace LearnToSpin
{
    /// <summary>
    /// Scatters trees, bushes and ivy in the strip OUTSIDE the fences, purely as set dressing.
    /// Everything placed here has its colliders stripped and no gameplay component — it can
    /// never affect the run. Drop it on a GameObject, assign the prefab lists and the band
    /// (innerEdge..outerEdge, zStart..zEnd), then call <see cref="Populate"/>. GameBootstrap
    /// wires it up automatically from its own world extents.
    /// </summary>
    public class SceneryScatter : MonoBehaviour
    {
        [Header("Prefabs")]
        public GameObject[] treePrefabs;
        public GameObject[] bushPrefabs;
        public GameObject[] ivyPrefabs;

        [Header("Placement band (metres from centre)")]
        [Tooltip("Keep clear inside this — set just past the fence line.")]
        public float innerEdge = 16f;
        [Tooltip("Don't place past this — keep inside the ground apron so nothing floats.")]
        public float outerEdge = 76f;
        public float zStart = -80f;
        public float zEnd = 2080f;
        [Tooltip("Ground height the props sit on (the apron top).")]
        public float groundY = -0.05f;

        [Header("Density & mix")]
        [Tooltip("Average spacing along Z between props on each side, metres.")]
        public float spacing = 14f;
        [Tooltip("Relative how-often weights for each category (a category with no prefabs is skipped).")]
        public float treeWeight = 1.2f;
        public float bushWeight = 1.5f;
        public float ivyWeight = 1f;

        [Header("Scale range per category (min, max)")]
        public Vector2 treeScale = new Vector2(0.8f, 1.6f);
        public Vector2 bushScale = new Vector2(0.7f, 1.4f);
        public Vector2 ivyScale = new Vector2(0.8f, 1.6f);

        [Tooltip("Seed so the layout is repeatable. Change it for a different arrangement.")]
        public int seed = 1337;

        System.Random _rng;

        float Range(float a, float b) => a + (float)_rng.NextDouble() * (b - a);
        int RangeInt(int countExclusive) => _rng.Next(0, countExclusive);

        public void Populate()
        {
            _rng = new System.Random(seed);

            // Clear anything from a previous pass so repeated calls don't stack up.
            for (int k = transform.childCount - 1; k >= 0; k--)
                Destroy(transform.GetChild(k).gameObject);

            float tW = HasAny(treePrefabs) ? Mathf.Max(0f, treeWeight) : 0f;
            float bW = HasAny(bushPrefabs) ? Mathf.Max(0f, bushWeight) : 0f;
            float iW = HasAny(ivyPrefabs) ? Mathf.Max(0f, ivyWeight) : 0f;
            float sum = tW + bW + iW;
            if (sum <= 0f)
            {
                Debug.LogWarning("SceneryScatter: no scenery prefabs assigned — nothing placed.");
                return;
            }

            float lo = Mathf.Min(innerEdge, outerEdge);
            float hi = Mathf.Max(innerEdge, outerEdge);
            float gap = Mathf.Max(1f, spacing);

            for (int side = -1; side <= 1; side += 2)
            {
                float z = zStart;
                while (z < zEnd)
                {
                    z += Range(gap * 0.6f, gap * 1.4f);

                    float x = side * Range(lo, hi);
                    float r = Range(0f, sum);

                    GameObject prefab;
                    Vector2 sc;
                    if (r < tW) { prefab = treePrefabs[RangeInt(treePrefabs.Length)]; sc = treeScale; }
                    else if (r < tW + bW) { prefab = bushPrefabs[RangeInt(bushPrefabs.Length)]; sc = bushScale; }
                    else { prefab = ivyPrefabs[RangeInt(ivyPrefabs.Length)]; sc = ivyScale; }

                    if (prefab != null) Place(prefab, x, z, Range(sc.x, sc.y));
                }
            }
        }

        static bool HasAny(GameObject[] a) => a != null && a.Length > 0;

        void Place(GameObject prefab, float x, float z, float scale)
        {
            var go = Instantiate(prefab, transform);
            go.transform.rotation = Quaternion.Euler(0f, Range(0f, 360f), 0f);
            go.transform.localScale *= scale;
            go.transform.position = Vector3.zero;

            // Decoration only — never let it touch the tire.
            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);

            Bounds b = BuildUtils.RendererBounds(go);
            go.transform.position = new Vector3(x - b.center.x, groundY - b.min.y, z - b.center.z);
        }
    }
}
