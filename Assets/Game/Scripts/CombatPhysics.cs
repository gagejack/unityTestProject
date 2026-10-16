using UnityEngine;

namespace StarterGame
{
    /// <summary>Shared nearest-hit queries that ignore the shooter and trigger volumes.</summary>
    public static class CombatPhysics
    {
        public static bool Cast(Vector3 origin, Vector3 direction, float distance, Transform owner,
            out RaycastHit closest, float radius = 0f, bool ignoreGround = false)
        {
            closest = default;
            if (distance <= 0f || direction.sqrMagnitude < 0.000001f) return false;
            RaycastHit[] hits = radius > 0f
                ? Physics.SphereCastAll(origin, radius, direction.normalized, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                : Physics.RaycastAll(origin, direction.normalized, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float best = float.PositiveInfinity;
            foreach (var hit in hits)
            {
                if (!hit.collider || (owner && hit.collider.transform.IsChildOf(owner)) || hit.distance >= best) continue;
                // A sweep starting with a planted foot overlapping the floor can return
                // distance zero and a synthetic normal opposite travel. The current
                // hip-to-foot probe handles real barriers; skip ambiguous initial hits.
                if (ignoreGround && (hit.normal.y > 0.65f || hit.distance <= Mathf.Epsilon)) continue;
                best = hit.distance;
                closest = hit;
            }
            return best < float.PositiveInfinity;
        }

        public static bool MuzzleObstruction(Vector3 body, Vector3 muzzle, Transform owner, out RaycastHit hit)
        {
            Vector3 path = muzzle - body;
            return Cast(body, path, path.magnitude, owner, out hit);
        }
    }
}
