using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>What happened when something accepted damage.</summary>
    public readonly struct DamageReport
    {
        public readonly GameObject Target;
        public readonly float Amount;
        public readonly float Remaining;
        public readonly Vector3 Position;

        public DamageReport(GameObject target, float amount, float remaining, Vector3 position)
        {
            Target = target;
            Amount = amount;
            Remaining = remaining;
            Position = position;
        }

        public bool Killed => Remaining <= 0f;
    }

    /// <summary>Combat notifications any system can listen to without knowing who sent them.</summary>
    public static class CombatEvents
    {
        /// <summary>Raised after a damage receiver accepts damage (before it is destroyed on a kill).</summary>
        public static event Action<DamageReport> Damaged;

        public static void RaiseDamaged(DamageReport report) => Damaged?.Invoke(report);
    }
}
