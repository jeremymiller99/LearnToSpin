using UnityEngine;

namespace LearnToSpin
{
    /// <summary>
    /// Builds the surface the tire rolls on. The physics is a single long plane that
    /// <see cref="WorldStreamer"/> re-centres under the tire each frame, so the ground never runs
    /// out. The visuals (tiled sand_road path, the wide cosmetic apron underneath, and the launch
    /// ramps) are generated per streamed slice via the Build*Slice methods, so any z-range can be
    /// produced on demand and recycled. Falls back to the bare plane when no road art is wired.
    /// </summary>
    public static class GroundBuilder
    {
        /// <summary>
        /// The persistent physics floor: one long flat plane (re-centred on the tire by the
        /// streamer). Visible+coloured when there's no road art; an invisible collider when the
        /// road tiles provide the visuals; omitted entirely when the road meshes are the collider.
        /// </summary>
        public static GameObject BuildPhysicsFloor(GameBootstrap boot, PhysicsMaterial grip)
        {
            bool hasRoadArt = boot.roadPrefabs != null && boot.roadPrefabs.Length > 0;
            if (hasRoadArt && boot.useRoadMeshColliders) return null; // road meshes are the ground

            var g = GameObject.CreatePrimitive(PrimitiveType.Plane);
            g.name = "Runway";
            // Unity plane is 10x10 units at scale 1 → 30 m wide (±15, past the lane) x 1000 m long.
            // The streamer keeps it centred under the tire, so 1000 m always reaches past the fog.
            g.transform.localScale = new Vector3(3f, 1f, 100f);
            g.transform.position = Vector3.zero;
            g.GetComponent<MeshRenderer>().sharedMaterial = BuildUtils.Lit(new Color(0.78f, 0.69f, 0.5f));
            g.GetComponent<Collider>().material = grip;

            // The streamer re-centres this every frame. A kinematic Rigidbody makes that a cheap
            // body move instead of forcing PhysX to rebuild the static collider tree each time.
            var rb = g.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            // With road art the tiles are the visible ground; the plane is just the collider.
            if (hasRoadArt) g.GetComponent<MeshRenderer>().enabled = false;
            return g;
        }

        /// <summary>
        /// Baseline road cell length: the first prefab's natural Z once fitted to the road width.
        /// Cached by the streamer so every slice tiles on the same global grid (seamless across
        /// chunk boundaries). Returns 0 when no road art is wired.
        /// </summary>
        public static float RoadCellZ(GameBootstrap boot)
        {
            if (boot.roadPrefabs == null || boot.roadPrefabs.Length == 0) return 0f;
            var sample = Object.Instantiate(boot.roadPrefabs[0]);
            sample.transform.position = Vector3.zero;
            Bounds sb = BuildUtils.RendererBounds(sample);
            Object.Destroy(sample);
            float widthFit = sb.size.x > 0.01f ? (boot.roadHalfWidth * 2f) / sb.size.x : 1f;
            return Mathf.Max(0.5f, sb.size.z * widthFit);
        }

        /// <summary>
        /// Tiles sand_road prefabs across [z0,z1), top surface flush with y=0. Tiles sit on a global
        /// grid (centre = r*step), so neighbouring slices line up with no seam, and a RANDOM mix of
        /// prefabs still fits the same cell. Each tile belongs to exactly the slice that contains
        /// its centre.
        /// </summary>
        public static void BuildRoadSlice(GameBootstrap boot, PhysicsMaterial grip, Transform parent,
                                          float z0, float z1, float cellZ)
        {
            if (boot.roadPrefabs == null || boot.roadPrefabs.Length == 0 || cellZ <= 0f) return;

            float step = cellZ * boot.roadTileSpacing;
            float origin = cellZ * 0.5f - 10f;            // matches the old r=0 tile centre
            int lo = Mathf.CeilToInt((z0 - origin) / step);
            int hi = Mathf.FloorToInt((z1 - 0.0001f - origin) / step);
            for (int r = lo; r <= hi; r++)
            {
                float z = origin + r * step;
                var prefab = boot.roadPrefabs[Random.Range(0, boot.roadPrefabs.Length)];
                FitTile(grip, prefab, 0f, z, boot.roadHalfWidth * 2f, cellZ, 0f, parent,
                        boot.useRoadMeshColliders, r, Quaternion.identity).name = $"Road_{z:0}m";
            }
        }

        /// <summary>
        /// The wide cosmetic sand floor for [z0,z1): the same road tiles laid on a global grid,
        /// stepped out past the fences and sitting just below the path so the world reads as resting
        /// on ground instead of floating. Always cosmetic (colliders stripped).
        /// </summary>
        public static void BuildApronSlice(GameBootstrap boot, Transform parent, float z0, float z1)
        {
            if (boot.roadPrefabs == null || boot.roadPrefabs.Length == 0) return;
            if (boot.groundApronHalfWidth <= boot.roadHalfWidth) return;

            float cell = Mathf.Max(8f, boot.groundApronCell);
            float step = cell;
            float tile = cell * Mathf.Max(1f, boot.groundApronOverlap); // oversized so tiles overlap
            int cols = Mathf.CeilToInt((boot.groundApronHalfWidth * 2f) / step) + 1;
            float x0 = -(cols - 1) * 0.5f * step;
            float origin = cell * 0.5f;

            int lo = Mathf.CeilToInt((z0 - origin) / step);
            int hi = Mathf.FloorToInt((z1 - 0.0001f - origin) / step);
            for (int r = lo; r <= hi; r++)
            {
                float z = origin + r * step;
                for (int c = 0; c < cols; c++)
                {
                    var prefab = boot.roadPrefabs[Random.Range(0, boot.roadPrefabs.Length)];
                    // Random quarter-turn: square (oversized) cells keep their footprint, no seams reopen.
                    var rot = Quaternion.Euler(0f, 90f * Random.Range(0, 4), 0f);
                    FitTile(null, prefab, x0 + c * step, z, tile, tile, -0.06f, parent, false,
                            r * cols + c, rot).name = "Apron";
                }
            }
        }

        /// <summary>
        /// Launch ramps for air time across [z0,z1). Placed on a global cadence (first at 140 m,
        /// then every 220 m) with size growing gently before it caps, so there's always a ramp line
        /// ahead no matter how far the run goes.
        /// </summary>
        public static void BuildRampSlice(GameBootstrap boot, PhysicsMaterial grip, Transform parent, float z0, float z1)
        {
            const float first = 140f, spacing = 220f;
            int lo = Mathf.CeilToInt((z0 - first) / spacing);
            int hi = Mathf.FloorToInt((z1 - 0.0001f - first) / spacing);
            for (int k = Mathf.Max(0, lo); k <= hi; k++)
            {
                float z = first + k * spacing;
                float angle = Mathf.Min(22f, 12f + k * 1.2f);
                float length = Mathf.Min(15f, 9f + k * 0.8f);
                // Nudge it lightly off-centre (a side each time) so the ramp line isn't always dead
                // middle — still easily reachable within the lane, just enough to make you steer.
                float x = Random.Range(2f, 4.5f) * (Random.value < 0.5f ? -1f : 1f);
                BuildRamp(boot, grip, parent, z, angle, length, x);
            }
        }

        /// <summary>
        /// Instantiates a road prefab, non-uniformly fits it to a cellX x cellZ footprint (so any
        /// prefab mix lines up), sits its top at <paramref name="topY"/>, and optionally gives it
        /// a mesh collider. A sub-mm alternating drop keeps overlap bands from z-fighting.
        /// </summary>
        static GameObject FitTile(PhysicsMaterial grip, GameObject prefab, float x, float z, float cellX,
                                  float cellZ, float topY, Transform parent, bool meshCollider, int index,
                                  Quaternion rot)
        {
            var go = Object.Instantiate(prefab);
            go.transform.SetParent(parent, true);
            go.transform.rotation = Quaternion.identity;
            go.transform.position = Vector3.zero;

            // Fit to the cell at identity first, then orient. With a square cell a 90° turn keeps
            // the footprint, so seamless tiling survives the rotation.
            Bounds b0 = BuildUtils.RendererBounds(go);
            float sx = b0.size.x > 0.01f ? cellX / b0.size.x : 1f;
            float sz = b0.size.z > 0.01f ? cellZ / b0.size.z : 1f;
            go.transform.localScale = Vector3.Scale(go.transform.localScale, new Vector3(sx, 1f, sz));
            go.transform.rotation = rot;

            if (meshCollider)
            {
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                {
                    if (mf.sharedMesh == null) continue;
                    var mc = mf.GetComponent<MeshCollider>();
                    if (mc == null) mc = mf.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                    mc.material = grip;
                }
            }
            else
            {
                foreach (var c in go.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            }

            Bounds b = BuildUtils.RendererBounds(go);
            float yNudge = (index & 1) * 0.002f;
            go.transform.position = new Vector3(x - b.center.x, topY - b.max.y - yNudge, z - b.center.z);
            return go;
        }

        /// <summary>
        /// One ramp — hit it hot and you fly. When <see cref="GameBootstrap.rampPrefab"/> is wired we
        /// instantiate that model (it ships with its own colliders) and just tip it from upright into
        /// the ramp angle; otherwise we fall back to the old primitive wedge.
        /// </summary>
        static void BuildRamp(GameBootstrap boot, PhysicsMaterial grip, Transform parent, float z, float angleDeg, float length, float x)
        {
            if (boot == null || boot.rampPrefab == null)
            {
                BuildPrimitiveRamp(grip, parent, z, angleDeg, length, x);
                return;
            }

            var ramp = Object.Instantiate(boot.rampPrefab);
            ramp.transform.SetParent(parent, true);
            ramp.name = $"Ramp_{z:0}m";

            // The model is authored standing straight up (long axis +Y). First yaw it -90° about its
            // own Y to face the right way, then tip it forward about X so it rises toward +Z: 90°
            // would leave it vertical, so (90 - angle) lands it at the shallow ramp slope. Negate the
            // X angle if it ends up leaning the wrong way.
            ramp.transform.rotation = Quaternion.Euler(90f - angleDeg, 0f, 0f) * Quaternion.Euler(0f, -90f, 0f);

            // Sit it on the ground at this z, nudged to x across the lane — robust to the prefab's pivot.
            Bounds b = BuildUtils.RendererBounds(ramp);
            ramp.transform.position += new Vector3(x - b.center.x, -b.min.y, z - b.center.z);

            // Roll on the launch grip surface, using whatever solid colliders the prefab ships with.
            foreach (var c in ramp.GetComponentsInChildren<Collider>())
                if (!c.isTrigger) c.material = grip;
        }

        /// <summary>Fallback wedge when no ramp model is wired — a plain angled box.</summary>
        static void BuildPrimitiveRamp(PhysicsMaterial grip, Transform parent, float z, float angleDeg, float length, float x)
        {
            var ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.transform.SetParent(parent, true);
            ramp.name = $"Ramp_{z:0}m";
            ramp.transform.localScale = new Vector3(7f, 0.4f, length);
            // rotate around the axle so the +Z end rises; sink the base into the ground
            ramp.transform.rotation = Quaternion.Euler(-angleDeg, 0f, 0f);
            float rise = Mathf.Sin(angleDeg * Mathf.Deg2Rad) * length * 0.5f;
            ramp.transform.position = new Vector3(x, rise - 0.2f, z);
            ramp.GetComponent<MeshRenderer>().sharedMaterial = BuildUtils.Lit(new Color(0.5f, 0.35f, 0.2f));
            ramp.GetComponent<Collider>().material = grip;
        }
    }
}
