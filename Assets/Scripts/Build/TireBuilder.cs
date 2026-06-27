using UnityEngine;

namespace LearnToSpin
{
    /// <summary>
    /// Builds the player's tire: the visual wheel + readable spoke, the sphere collider with a
    /// bouncy-but-grippy physics material, and a rigidbody tuned to behave like a flywheel so
    /// stored spin converts into forward motion. Returns the root (carrying the TireLauncher).
    ///
    /// The equipped <see cref="TireDef"/> chooses the wheel art, mass, radius and bounciness, and
    /// the <see cref="EffectiveStats"/> (tire base + bought upgrades) seed the launcher's tunables.
    /// </summary>
    public static class TireBuilder
    {
        public static GameObject Build(GameBootstrap boot, TireDef def, EffectiveStats stats)
        {
            float radius = stats.radius > 0f ? stats.radius : boot.tireRadius;

            var t = new GameObject("Tire");
            t.transform.position = new Vector3(0f, radius, 0f);

            AddVisual(t.transform, def, radius);

            var col = t.AddComponent<SphereCollider>();
            col.radius = radius;
            // Landing bounce is handled in TireLauncher.OnCollisionEnter so the impact is biased
            // FORWARD (a skip) instead of straight up. PhysX restitution is left at zero here so it
            // doesn't fight that custom bounce; friction stays high for spin→forward grip.
            col.material = new PhysicsMaterial("BouncyTire")
            {
                bounciness = 0f,
                bounceCombine = PhysicsMaterialCombine.Average,
                dynamicFriction = 0.8f,
                staticFriction = 0.9f,
                frictionCombine = PhysicsMaterialCombine.Average
            };

            float mass = stats.mass > 0f ? stats.mass : boot.tireMass;
            var rb = t.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.linearDamping = 0.02f;
            rb.angularDamping = 0.04f;
            // Dynamic (not Speculative): speculative mode resolves contacts BEFORE the tire actually
            // touches, so landings register in mid-air — the trick lands, then the tire visibly drops
            // to the real ground. Dynamic keeps solid landings crisp; the per-step travel (~0.6 m at
            // 30 m/s, 0.02s) is far smaller than the hazard trigger boxes, so hazards aren't tunneled.
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            // Keep it upright (no tip/yaw) but free to steer left/right and move forward.
            rb.constraints = RigidbodyConstraints.FreezeRotationY
                           | RigidbodyConstraints.FreezeRotationZ;
            // Override inertia toward a ring/flywheel (more spin converts to motion than a solid sphere).
            float inertia = mass * radius * radius;
            rb.inertiaTensor = new Vector3(inertia, inertia * 0.5f, inertia * 0.5f);

            var launcher = t.AddComponent<TireLauncher>();
            launcher.ApplyTuning(stats.maxSpin, stats.idealSpin, stats.boostReserve, stats.bounciness);
            return t;
        }

        /// <summary>
        /// Adds the wheel visual. With a wired wheel prefab we instantiate it and align its thinnest
        /// axis (the tread width) to the world X axle so it spins forward like the cylinder did,
        /// scaled to the tire's diameter. With no prefab we fall back to the original cylinder.
        /// </summary>
        static void AddVisual(Transform root, TireDef def, float radius)
        {
            if (def != null && def.prefab != null)
            {
                var vis = Object.Instantiate(def.prefab);
                vis.name = "WheelVisual";
                foreach (var c in vis.GetComponentsInChildren<Collider>()) Object.Destroy(c);
                vis.transform.SetParent(root, false);
                vis.transform.localPosition = Vector3.zero;
                vis.transform.localRotation = Quaternion.identity;

                // Measure the raw mesh, point its slimmest axis along X, and scale to the diameter.
                Vector3 size = BuildUtils.RendererBounds(vis).size;
                int thin = (size.x <= size.y && size.x <= size.z) ? 0
                         : (size.y <= size.z) ? 1 : 2;
                float dia = thin == 0 ? Mathf.Max(size.y, size.z)
                          : thin == 1 ? Mathf.Max(size.x, size.z)
                          : Mathf.Max(size.x, size.y);
                vis.transform.localRotation = thin == 0 ? Quaternion.identity
                                            : thin == 1 ? Quaternion.Euler(0f, 0f, 90f)
                                            : Quaternion.Euler(0f, 90f, 0f);
                if (dia > 0.0001f) vis.transform.localScale = Vector3.one * (radius * 2f / dia);

                // Recentre on the axle now that it's rotated/scaled.
                Vector3 localCentre = root.InverseTransformPoint(BuildUtils.RendererBounds(vis).center);
                vis.transform.localPosition -= localCentre;
                return;
            }

            // Fallback: a fat wheel built from a cylinder laid on its side (axle along X).
            var cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.Destroy(cyl.GetComponent<Collider>());
            cyl.transform.SetParent(root, false);
            cyl.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            cyl.transform.localScale = new Vector3(radius * 2f, 0.35f, radius * 2f);
            cyl.GetComponent<MeshRenderer>().sharedMaterial = BuildUtils.Lit(new Color(0.08f, 0.08f, 0.09f));
        }
    }
}
