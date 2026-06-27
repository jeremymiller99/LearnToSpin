using UnityEngine;

namespace LearnToSpin
{
    /// <summary>
    /// A trigger hazard: the first time the tire rolls through it, it bleeds the
    /// tire's speed and spin. Non-solid so it never hard-stops the player — it just
    /// punishes a bad line. Steer around them (or boost through and eat the loss).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Hazard : MonoBehaviour
    {
        [Tooltip("Fraction of speed/spin KEPT on hit (0.5 = lose half).")]
        [Range(0.05f, 1f)] public float keepFactor = 0.5f;

        bool _spent;

        void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            if (_spent) return;
            var rb = other.attachedRigidbody;
            if (rb == null) return;

            _spent = true;

            // How hard the hit reads: the faster the tire (and the more this hazard bleeds),
            // the bigger the burst. Mapped so a ~30 m/s clip is roughly full strength.
            float speed = rb.linearVelocity.magnitude;
            float strength = Mathf.Clamp01(speed / 30f) * (1f - keepFactor) * 2f;

            rb.linearVelocity *= keepFactor;
            rb.angularVelocity *= keepFactor;

            // Burst at the point on the tire nearest this hazard, lifted toward its centre so
            // the puff reads off the ground rather than buried in it.
            Vector3 hit = GetComponent<Collider>().ClosestPoint(rb.worldCenterOfMass);
            hit.y = Mathf.Max(hit.y, 0.5f);

            // Tint the dust from this hazard's colour when it has one, else a dusty tan.
            var mr = GetComponent<MeshRenderer>();
            Color dust = mr != null ? mr.material.color : new Color(0.8f, 0.72f, 0.55f);
            HazardHitFX.Play(hit, strength, dust);

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayTireImpact(hit);

            // grey it out as spent feedback
            if (mr != null) mr.material.color = new Color(0.35f, 0.35f, 0.35f);
        }
    }
}
