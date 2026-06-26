using UnityEngine;

namespace LearnToSpin
{
    /// <summary>Smooth chase camera that trails the tire down the runway.</summary>
    public class ChaseCamera : MonoBehaviour
    {
        public Transform target;
        public Vector3 offset = new Vector3(0f, 3.5f, -8f);
        public float smoothTime = 0.15f;
        public float lookAhead = 5f;

        Vector3 _vel;

        void LateUpdate()
        {
            if (target == null) return;
            Vector3 desired = target.position + offset;
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref _vel, smoothTime);
            transform.LookAt(target.position + Vector3.forward * lookAhead + Vector3.up * 1f);
        }
    }
}
