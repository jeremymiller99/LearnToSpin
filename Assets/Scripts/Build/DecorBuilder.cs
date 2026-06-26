using UnityEngine;

namespace LearnToSpin
{
    /// <summary>
    /// Non-gameplay set dressing generated per streamed slice by <see cref="WorldStreamer"/>:
    /// distance-marker lamps, the roadside fences, and the scattered trees/bushes/ivy outside the
    /// fences. None of this affects the run — lamps and fences are placed with colliders stripped,
    /// and scenery never gets a collider or gameplay component. Every Build*Slice produces one
    /// z-range so it can be streamed and recycled.
    /// </summary>
    public static class DecorBuilder
    {
        /// <summary>
        /// Learns the fence segment's length and the rotation that runs its long axis down +Z.
        /// Returns false (and skips fences) when no fence prefab is wired. Cached by the streamer.
        /// </summary>
        public static bool FenceMetric(GameBootstrap boot, out float segLen, out Quaternion rot)
        {
            segLen = 1f;
            rot = Quaternion.identity;
            if (boot.fencePrefab == null) return false;

            var sample = Object.Instantiate(boot.fencePrefab);
            sample.transform.position = Vector3.zero;
            Bounds sb = BuildUtils.RendererBounds(sample);
            Object.Destroy(sample);

            bool longAlongX = sb.size.x >= sb.size.z;
            segLen = Mathf.Max(0.5f, Mathf.Max(sb.size.x, sb.size.z));
            rot = longAlongX ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
            return true;
        }

        /// <summary>Tiles white fences down both edges of the road across [z0,z1), on a global grid.</summary>
        public static void BuildFenceSlice(GameBootstrap boot, Transform parent, float z0, float z1,
                                           float segLen, Quaternion rot)
        {
            if (boot.fencePrefab == null) return;

            float origin = segLen * 0.5f - 10f;
            int lo = Mathf.CeilToInt((z0 - origin) / segLen);
            int hi = Mathf.FloorToInt((z1 - 0.0001f - origin) / segLen);
            float x = boot.roadHalfWidth;
            for (int n = lo; n <= hi; n++)
            {
                float z = origin + n * segLen;
                BuildUtils.Place(boot.fencePrefab, x, z, rot, 1f, Sit.Base, parent, true).name = $"Fence_R_{z:0}m";
                BuildUtils.Place(boot.fencePrefab, -x, z, rot, 1f, Sit.Base, parent, true).name = $"Fence_L_{z:0}m";
            }
        }

        /// <summary>Distance markers across [z0,z1): a lamp every 100 m on each side (or fallback posts every 50 m).</summary>
        public static void BuildMarkersSlice(GameBootstrap boot, Transform parent, float z0, float z1)
        {
            if (boot.lampPrefab != null)
            {
                float x = boot.roadHalfWidth + 0.8f;
                int lo = Mathf.Max(1, Mathf.CeilToInt(z0 / 100f));
                int hi = Mathf.FloorToInt((z1 - 0.0001f) / 100f);
                for (int k = lo; k <= hi; k++)
                {
                    int z = k * 100;
                    BuildUtils.Place(boot.lampPrefab, x, z, Quaternion.Euler(0f, -90f, 0f), 1f, Sit.Base, parent, true)
                        .name = $"Lamp_R_{z}m";
                    BuildUtils.Place(boot.lampPrefab, -x, z, Quaternion.Euler(0f, 90f, 0f), 1f, Sit.Base, parent, true)
                        .name = $"Lamp_L_{z}m";
                }
                return;
            }

            // Fallback: simple posts every 50 m.
            int plo = Mathf.Max(1, Mathf.CeilToInt(z0 / 50f));
            int phi = Mathf.FloorToInt((z1 - 0.0001f) / 50f);
            for (int k = plo; k <= phi; k++)
            {
                int z = k * 50;
                var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.Destroy(post.GetComponent<Collider>());
                post.transform.SetParent(parent, true);
                post.name = $"Marker_{z}m";
                post.transform.position = new Vector3(13f, 1f, z);
                post.transform.localScale = new Vector3(0.3f, 2f, 0.3f);
                post.GetComponent<MeshRenderer>().sharedMaterial =
                    BuildUtils.Lit(z % 100 == 0 ? new Color(0.9f, 0.85f, 0.2f) : new Color(0.7f, 0.7f, 0.7f));
            }
        }

        /// <summary>
        /// Decorative trees / bushes / ivy scattered across [z0,z1) in the band OUTSIDE the fences
        /// (purely set dressing — never colliders or hazards), fitted inside the apron so nothing
        /// floats. Uses Unity Random, which the streamer seeds per chunk.
        /// </summary>
        public static void BuildScenerySlice(GameBootstrap boot, Transform parent, float z0, float z1)
        {
            float tW = HasAny(boot.sceneryTreePrefabs) ? 1.2f : 0f;
            float bW = HasAny(boot.sceneryBushPrefabs) ? 1.5f : 0f;
            float iW = HasAny(boot.sceneryIvyPrefabs) ? 1f : 0f;
            float sum = tW + bW + iW;
            if (sum <= 0f) return;

            float inner = boot.roadHalfWidth + 2f;
            float outer = Mathf.Max(inner + 6f, boot.groundApronHalfWidth - 6f);
            const float groundY = -0.05f;
            const float gap = 14f; // average spacing along Z, per side

            for (int side = -1; side <= 1; side += 2)
            {
                float z = z0 + Random.Range(0f, gap);
                while (z < z1)
                {
                    float x = side * Random.Range(inner, outer);
                    float r = Random.Range(0f, sum);

                    GameObject prefab;
                    Vector2 sc;
                    if (r < tW) { prefab = Pick(boot.sceneryTreePrefabs); sc = new Vector2(0.8f, 1.6f); }
                    else if (r < tW + bW) { prefab = Pick(boot.sceneryBushPrefabs); sc = new Vector2(0.7f, 1.4f); }
                    else { prefab = Pick(boot.sceneryIvyPrefabs); sc = new Vector2(0.8f, 1.6f); }

                    if (prefab != null) PlaceScenery(prefab, x, z, Random.Range(sc.x, sc.y), groundY, parent);

                    z += Random.Range(gap * 0.6f, gap * 1.4f);
                }
            }
        }

        static bool HasAny(GameObject[] a) => a != null && a.Length > 0;
        static GameObject Pick(GameObject[] a) => a[Random.Range(0, a.Length)];

        static void PlaceScenery(GameObject prefab, float x, float z, float scale, float groundY, Transform parent)
        {
            var go = Object.Instantiate(prefab, parent);
            go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            go.transform.localScale *= scale;
            go.transform.position = Vector3.zero;

            // Decoration only — never let it touch the tire.
            foreach (var c in go.GetComponentsInChildren<Collider>()) Object.Destroy(c);

            Bounds b = BuildUtils.RendererBounds(go);
            go.transform.position = new Vector3(x - b.center.x, groundY - b.min.y, z - b.center.z);
        }
    }
}
