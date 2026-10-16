using UnityEngine;

namespace Game.Core
{
    /// <summary>Where the player is, for systems that need a target without knowing the movement system.</summary>
    public static class PlayerPresence
    {
        /// <summary>The active player's root transform, or null when there is none.</summary>
        public static Transform Current { get; private set; }

        public static void Register(Transform player) => Current = player;

        public static void Unregister(Transform player)
        {
            if (Current == player) Current = null;
        }
    }
}
