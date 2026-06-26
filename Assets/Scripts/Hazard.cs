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
            rb.linearVelocity *= keepFactor;
            rb.angularVelocity *= keepFactor;

            // grey it out as spent feedback
            var mr = GetComponent<MeshRenderer>();
            if (mr != null) mr.material.color = new Color(0.35f, 0.35f, 0.35f);
        }
    }
}
