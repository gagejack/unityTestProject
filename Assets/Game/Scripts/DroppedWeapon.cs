using UnityEngine;

namespace StarterGame
{
    public sealed class DroppedWeapon : MonoBehaviour
    {
        private int offerIndex;
        private WeaponSettings settings;
        private float canBePickedUpAt;
        private bool collected;
        public int Magazine { get; private set; }
        public int Reserve { get; private set; }
        public string DisplayName => settings ? settings.displayName : "WEAPON";
        public bool CanCollect => !collected && Time.time >= canBePickedUpAt;

        internal void Initialize(int index, WeaponSettings weapon, int magazine, int reserve)
        {
            offerIndex = index;
            settings = weapon;
            Magazine = magazine;
            Reserve = reserve;
            canBePickedUpAt = Time.time + 0.4f;
        }

        public bool TryCollect(WeaponCombatController player)
        {
            if (!CanCollect || !player || Vector3.Distance(player.transform.position, transform.position) > 2.2f) return false;
            Vector3 origin = player.transform.position + Vector3.up * 0.5f;
            Vector3 path = transform.position - origin;
            if (CombatPhysics.Cast(origin, path, path.magnitude, player.transform, out var hit) && !hit.collider.transform.IsChildOf(transform)) return false;
            if (!player.PickUpDropped(offerIndex, settings, Magazine, Reserve)) return false;
            collected = true;
            // Destroy is deferred until the end of the frame; a collected gun
            // must stop blocking the newly equipped gun's shots immediately.
            foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
            Destroy(gameObject);
            return true;
        }
    }
}
