using UnityEngine;

namespace StarterGame
{
    /// <summary>Optional damage receiver for simple targets in the sample scene.</summary>
    public sealed class TargetHealth : MonoBehaviour, Game.Core.IDamageable
    {
        [SerializeField, Min(1f)] private float hitPoints = 100f;
        [SerializeField, Min(0.01f)] private float hitFlashDuration = 0.09f;
        private Renderer[] renderers;
        private Color[] originalColors;
        private MaterialPropertyBlock properties;
        private float flashUntil;
        private bool flashing;
        public float HitPoints => hitPoints;
        public bool IsAlive => hitPoints > 0f;

        private void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>();
            originalColors = new Color[renderers.Length];
            properties = new MaterialPropertyBlock();
            for (int i = 0; i < renderers.Length; i++)
            {
                var material = renderers[i].sharedMaterial;
                originalColors[i] = material && material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : Color.white;
            }
        }

        public bool TakeDamage(float amount)
        {
            if (!IsAlive || amount <= 0f) return false;
            hitPoints = Mathf.Max(0f, hitPoints - amount);
            flashUntil = Time.time + hitFlashDuration;
            flashing = true;
            SetFlash(true);
            if (hitPoints <= 0f) Destroy(gameObject);
            return true;
        }

        private void Update()
        {
            if (!flashing || Time.time < flashUntil) return;
            SetFlash(false);
            flashing = false;
        }

        private void SetFlash(bool hit)
        {
            if (renderers == null || properties == null || originalColors == null) Awake();
            for (int i = 0; i < renderers.Length; i++)
            {
                if (!renderers[i]) continue;
                renderers[i].GetPropertyBlock(properties);
                properties.SetColor("_BaseColor", hit ? Color.Lerp(originalColors[i], Color.white, 0.85f) : originalColors[i]);
                renderers[i].SetPropertyBlock(properties);
            }
        }
    }
}
