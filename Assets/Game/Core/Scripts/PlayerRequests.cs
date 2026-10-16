using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Requests that test tools (test areas, the baseline harness) make of the player.
    /// The owning systems listen: movement teleports, the camera re-frames, weapons refill.
    /// </summary>
    public static class PlayerRequests
    {
        /// <summary>Place the player at a position and facing, clearing any movement in progress.</summary>
        public static event Action<Vector3, Quaternion> TeleportRequested;

        /// <summary>Fill the held gun's magazine and reserve.</summary>
        public static event Action RefillAmmoRequested;

        public static void Teleport(Vector3 position, Quaternion facing) => TeleportRequested?.Invoke(position, facing);

        public static void RefillAmmo() => RefillAmmoRequested?.Invoke();
    }
}
