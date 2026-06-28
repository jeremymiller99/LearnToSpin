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
                // Most hazards bias off-centre so the ramp line stays mostly runnable and the side
                // varies, but a fraction sit on the centre line — otherwise going dead-straight down
                // the middle was a free, risk-free path (hazards are non-solid, so a centre hit just
                // bleeds speed/spin and the player can still weave or power through).
                float x = Random.value < 0.35f
                    ? Random.Range(-2.5f, 2.5f)
                    : Random.Range(2.5f, LaneHalf) * (Random.value < 0.5f ? -1f : 1f);

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

            // A trigger box over the footprint — non-solid so it never hard-stops the player, just
            // bleeds speed/spin (see Hazard). Sized from the final bounds, but CAPPED and anchored at
            // the base: a tree's renderer bounds span the whole trunk-to-canopy height and the full
            // canopy width, and using that raw gave phantom hits — clipping the canopy box while flying
            // OVER the tree, or the canopy's sideways overhang while driving past in an adjacent lane.
            // Cap the height so big air clears it, and the footprint so only the trunk-ish base bleeds.
            const float MaxTriggerHeight = 3f; // fly above this and you clear the hazard
            const float MaxTriggerWidth = 2.5f; // clamp wide canopies toward the trunk footprint
            Bounds b = BuildUtils.RendererBounds(go);
            Vector3 ls = go.transform.lossyScale;

            float wsx = Mathf.Min(b.size.x, MaxTriggerWidth);
            float wsz = Mathf.Min(b.size.z, MaxTriggerWidth);
            float wsy = Mathf.Min(b.size.y, MaxTriggerHeight);
            // World-space box: keep the prefab's horizontal centre but sit on the ground (b.min.y) so
            // the capped height covers the base, not a slab floating at the canopy's mid-height.
            Vector3 worldCenter = new Vector3(b.center.x, b.min.y + wsy * 0.5f, b.center.z);

            var bc = go.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.center = go.transform.InverseTransformPoint(worldCenter);
            bc.size = new Vector3(
                wsx / Mathf.Max(0.0001f, Mathf.Abs(ls.x)),
                wsy / Mathf.Max(0.0001f, Mathf.Abs(ls.y)),
                wsz / Mathf.Max(0.0001f, Mathf.Abs(ls.z)));

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
