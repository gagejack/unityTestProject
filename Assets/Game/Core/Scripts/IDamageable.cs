namespace Game.Core
{
    /// <summary>Anything that guns, fists or mobs can damage. Implemented by targets and mobs.</summary>
    public interface IDamageable
    {
        bool IsAlive { get; }

        /// <summary>Applies damage. Returns false when the hit is rejected (already dead, or amount not positive).</summary>
        bool TakeDamage(float amount);
    }
}
