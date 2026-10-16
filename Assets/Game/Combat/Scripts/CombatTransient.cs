using UnityEngine;

namespace StarterGame
{
    public sealed class CombatTransient : MonoBehaviour
    {
        private float duration;
        private float age;
        private Vector3 initialScale;
        public Vector3 Velocity { get; set; }
        public void Initialize(float seconds)
        {
            duration = Mathf.Max(0.01f, seconds);
            initialScale = transform.localScale;
            Destroy(gameObject, duration);
        }
        private void Update()
        {
            age += Time.deltaTime;
            transform.position += Velocity * Time.deltaTime;
            transform.localScale = initialScale * Mathf.Clamp01(1f - age / duration);
        }
    }
}
