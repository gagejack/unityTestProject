using System.Collections.Generic;
using UnityEngine;

namespace StarterGame
{
    public sealed class RocketProjectile : MonoBehaviour
    {
        private WeaponSettings settings;
        private Transform owner;
        private CombatFeedback feedback;
        private Vector3 direction;
        private float travelled;
        private float nextSmoke;
        private bool exploded;
        public static RocketProjectile Launch(WeaponSettings weapon, Transform shooter, CombatFeedback effects, Vector3 position, Vector3 heading)
        {
            var go = CombatFeedback.Effect("Bazooka rocket", position, new Vector3(0.1f, 0.1f, 0.38f), new Color(0.8f, 0.85f, 0.7f), weapon.range / Mathf.Max(1, weapon.rocketSpeed) + 1f);
            Destroy(go.GetComponent<CombatTransient>());
            go.transform.rotation = Quaternion.LookRotation(heading);
            var rocket = go.AddComponent<RocketProjectile>();
            rocket.settings = weapon;
            rocket.owner = shooter;
            rocket.feedback = effects;
            rocket.direction = heading.normalized;
            return rocket;
        }

        private void Update()
        {
            if (exploded || !settings) return;
            float distance = Mathf.Min(settings.rocketSpeed * Time.deltaTime, settings.range - travelled);
            if (CombatPhysics.Cast(transform.position, direction, distance, owner, out var hit, 0.06f))
            {
                Explode(hit.point + hit.normal * 0.04f);
                return;
            }
            transform.position += direction * distance;
            travelled += distance;
            if (Time.time >= nextSmoke)
            {
                nextSmoke = Time.time + 0.035f;
                var smoke = CombatFeedback.Effect("Rocket smoke", transform.position - direction * 0.15f, Vector3.one * 0.12f, new Color(0.4f, 0.45f, 0.48f), 0.5f);
                smoke.GetComponent<CombatTransient>().Velocity = Vector3.up * 0.3f;
            }
            if (travelled >= settings.range) Explode(transform.position);
        }

        public void Explode(Vector3 position)
        {
            if (exploded) return;
            exploded = true;
            var damaged = new HashSet<TargetHealth>();
            bool confirmed = false;
            foreach (var collider in Physics.OverlapSphere(position, settings.blastRadius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (owner && collider.transform.IsChildOf(owner)) continue;
                var health = collider.GetComponentInParent<TargetHealth>();
                if (!health || damaged.Contains(health)) continue;
                Vector3 point = collider.ClosestPoint(position);
                Vector3 path = point - position;
                if (CombatPhysics.Cast(position, path, path.magnitude, owner, out var cover) && cover.collider.GetComponentInParent<TargetHealth>() != health) continue;
                damaged.Add(health);
                float falloff = Mathf.Lerp(1f, 0.25f, Mathf.Clamp01(path.magnitude / settings.blastRadius));
                confirmed |= health.TakeDamage(settings.damage * falloff);
            }
            if (feedback)
            {
                feedback.PlayGunSound(settings.explosionSound, position, 0.8f, true, 0f, settings.shotPitch);
                if (confirmed) feedback.ConfirmHit();
            }
            float visualScale = Mathf.Max(0.1f, settings.explosionVisualScale);
            CombatFeedback.Effect("Rocket explosion", position, Vector3.one * (1.3f * visualScale), new Color(1f, 0.55f, 0.12f), 0.18f);
            for (int i = 0; i < 10; i++)
            {
                var debris = CombatFeedback.Effect("Explosion smoke", position, Vector3.one * (Random.Range(0.2f, 0.45f) * visualScale), new Color(0.25f, 0.29f, 0.32f), 0.6f);
                debris.GetComponent<CombatTransient>().Velocity = (Random.insideUnitSphere * 3f + Vector3.up) * visualScale;
            }
            Destroy(gameObject);
        }
    }
}
