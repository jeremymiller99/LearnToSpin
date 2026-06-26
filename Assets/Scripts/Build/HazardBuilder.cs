using UnityEngine;

namespace LearnToSpin
{
    /// <summary>
    /// Scatters hazards down the runway at random positions so each run differs. Driven per streamed
    /// slice by <see cref="WorldStreamer"/> (seeded per chunk, so a recycled chunk regenerates the
    /// same layout). Uses the wired art prefabs (wrapped in a trigger volume) when available,
    /// otherwise primitive fallbacks.
    /// </summary>
    public static class HazardBuilder
    {
        const float LaneHalf = 12f;
        const float StartClear = 90f; // keep the launch area in front of the tire clear

        /// <summary>Scatters hazards across [z0,z1) into <paramref name="parent"/>.</summary>
        public static void BuildSlice(GameBootstrap boot, Transform parent, float z0, float z1)
        {
            bool useArt = boot.hazardPrefabs != null && boot.hazardPrefabs.Length > 0;

            float z = Mathf.Max(z0, StartClear) + Random.Range(0f, 35f);
            while (z < z1)
            {
                // bias off-centre so the central ramp line stays runnable, but vary the side
                float x = Random.Range(2.5f, LaneHalf) * (Random.value < 0.5f ? -1f : 1f);

                if (useArt) BuildArtHazard(boot, x, z, parent);
                else BuildPrimitiveHazard(x, z, parent);

                z += Random.Range(30f, 65f);
            }
        }

        static void BuildArtHazard(GameBootstrap boot, float x, float z, Transform parent)
        {
            var prefab = boot.hazardPrefabs[Random.Range(0, boot.hazardPrefabs.Length)];
            var rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            var go = BuildUtils.Place(prefab, x, z, rot, Random.Range(0.85f, 1.35f),
                                      Sit.Base, parent, stripColliders: true);
            go.name = $"Hazard_{z:0}m";

            // A generous trigger box over the footprint — non-solid so it never hard-stops
            // the player, just bleeds speed/spin (see Hazard). Sized from the final bounds.
            Bounds b = BuildUtils.RendererBounds(go);
            Vector3 ls = go.transform.lossyScale;
            var bc = go.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.center = go.transform.InverseTransformPoint(b.center);
            bc.size = new Vector3(
                b.size.x / Mathf.Max(0.0001f, Mathf.Abs(ls.x)),
                b.size.y / Mathf.Max(0.0001f, Mathf.Abs(ls.y)),
                b.size.z / Mathf.Max(0.0001f, Mathf.Abs(ls.z)));

            var h = go.AddComponent<Hazard>();
            h.keepFactor = Random.Range(0.45f, 0.7f);
        }

        static void BuildPrimitiveHazard(float x, float z, Transform parent)
        {
            bool tall = Random.value < 0.4f;
            var hz = GameObject.CreatePrimitive(tall ? PrimitiveType.Capsule : PrimitiveType.Cube);
            hz.transform.SetParent(parent, true);
            hz.name = $"Hazard_{z:0}m";
            if (tall)
                hz.transform.localScale = new Vector3(1.6f, Random.Range(1.4f, 2.4f), 1.6f);
            else
                hz.transform.localScale = new Vector3(Random.Range(1.5f, 3f), 1.4f, Random.Range(1.5f, 3f));
            hz.transform.position = new Vector3(x, hz.transform.localScale.y * 0.5f, z);
            hz.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            hz.GetComponent<MeshRenderer>().sharedMaterial = BuildUtils.Lit(new Color(0.75f, 0.12f, 0.12f));

            hz.GetComponent<Collider>().isTrigger = true; // Hazard.Reset() won't run when added in code
            var h = hz.AddComponent<Hazard>();
            h.keepFactor = Random.Range(0.45f, 0.7f);
        }
    }
}
