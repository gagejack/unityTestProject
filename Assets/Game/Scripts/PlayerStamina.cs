using UnityEngine;

namespace StarterGame
{
    public sealed class PlayerStamina : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float maximumStamina = 100f;
        [SerializeField, Min(0f)] private float recoveryPerSecond = 25f;
        [SerializeField, Min(0f)] private float recoveryDelay = 1f;
        public float Current { get; private set; }
        public float Maximum => maximumStamina;
        public float Fraction => Mathf.Clamp01(Current / maximumStamina);
        public bool IsInfinite { get; private set; }
        private float recoveryWait;
        private CharacterController controller;
        private CapsuleMovement movement;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            movement = GetComponent<CapsuleMovement>();
        }

        private void OnEnable() => SetInfinite(true);

        public bool TrySpend(float amount)
        {
            if (IsInfinite)
            {
                Refill();
                return true;
            }
            amount = Mathf.Max(0f, amount);
            if (Current < amount) return false;
            Current = Mathf.Max(0f, Current - amount);
            if (amount > 0f) recoveryWait = recoveryDelay;
            return true;
        }

        public void Refill()
        {
            Current = maximumStamina;
            recoveryWait = 0f;
        }

        public void SetInfinite(bool value)
        {
            IsInfinite = value;
            if (value) Refill();
        }

        private void Update()
        {
            if (controller && !controller.isGrounded) return;
            if (movement && (movement.IsRolling || movement.IsDiving || movement.IsSliding)) return;
            Recover(Time.deltaTime);
        }

        private void Recover(float dt)
        {
            // Only recover for the part of this frame after the delay has elapsed.
            float recoveryTime = Mathf.Max(0f, dt - recoveryWait);
            recoveryWait = Mathf.Max(0f, recoveryWait - dt);
            Current = Mathf.Min(maximumStamina, Current + recoveryPerSecond * recoveryTime);
        }
    }
}
