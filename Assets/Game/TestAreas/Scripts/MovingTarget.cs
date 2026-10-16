using UnityEngine;

namespace Game.TestAreas
{
    /// <summary>Slides a target back and forth along its local X axis at a constant speed.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class MovingTarget : MonoBehaviour
    {
        [Tooltip("Total length of the path, in meters, centred on the starting position.")]
        [SerializeField, Min(0.5f)] private float pathLength = 8f;
        [Tooltip("Travel speed in meters per second.")]
        [SerializeField, Min(0.1f)] private float speed = 2f;

        Vector3 origin;
        Rigidbody body;
        float travelled;

        public void Configure(float length, float metersPerSecond)
        {
            pathLength = length;
            speed = metersPerSecond;
        }

        private void Awake()
        {
            origin = transform.localPosition;
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
        }

        private void FixedUpdate()
        {
            travelled += speed * Time.fixedDeltaTime;
            float offset = Mathf.PingPong(travelled, pathLength) - pathLength * 0.5f;
            Vector3 local = origin + Vector3.right * offset;
            body.MovePosition(transform.parent ? transform.parent.TransformPoint(local) : local);
        }
    }
}
