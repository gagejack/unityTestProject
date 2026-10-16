using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>Committed movement actions the movement system announces to modifiers.</summary>
    public enum MovementAction { Jump, WallJump, Roll, Dive }

    /// <summary>
    /// Something that changes how the player moves without owning movement: a gun slowing aimed walking,
    /// an attack committing the player to a swing. Put it on the player object; movement finds it.
    /// </summary>
    public interface IMovementModifier
    {
        /// <summary>Multiplies ordinary walking, running and crouching speed (1 = no change).</summary>
        float SpeedMultiplier { get; }

        /// <summary>True when movement should ease toward SpeedMultiplier over its slowdown time instead of applying it at once.</summary>
        bool EaseSpeedChange { get; }

        /// <summary>True while running must stop.</summary>
        bool BlocksRunning { get; }

        /// <summary>True while jumping, rolling, diving and crouch changes must wait.</summary>
        bool BlocksCommittedMoves { get; }

        /// <summary>True while retained jump or dash momentum must not exceed the eased speed.</summary>
        bool LimitsMomentum { get; }

        /// <summary>A heading the body must face (for example while aiming). Return false to leave facing alone.</summary>
        bool TryGetFacing(out Vector3 planarForward);

        /// <summary>Called when a committed movement action starts.</summary>
        void OnMovementAction(MovementAction action);
    }

    /// <summary>The view the player steers by (the camera).</summary>
    public interface IViewHeading
    {
        /// <summary>True when the character should face the view heading directly (third person).</summary>
        bool FacesViewHeading { get; }

        /// <summary>The view's horizontal forward direction.</summary>
        Vector3 PlanarForward { get; }
    }

    /// <summary>Something that can take control away from the player, such as an open menu or a flying camera.</summary>
    public interface IControlBlocker
    {
        bool BlocksPlayerControl { get; }
    }

    /// <summary>Whether the player currently has control. Blockers register while they exist.</summary>
    public static class PlayerControl
    {
        static readonly List<IControlBlocker> blockers = new List<IControlBlocker>();

        public static bool Enabled
        {
            get
            {
                foreach (var blocker in blockers)
                    if (blocker != null && !(blocker is Object o && !o) && blocker.BlocksPlayerControl) return false;
                return true;
            }
        }

        public static void Register(IControlBlocker blocker) { if (!blockers.Contains(blocker)) blockers.Add(blocker); }
        public static void Unregister(IControlBlocker blocker) => blockers.Remove(blocker);
    }
}
