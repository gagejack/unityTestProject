using UnityEngine;
using UnityEngine.UI;

namespace Game.Movement
{
    /// <summary>Shows the player's horizontal speed in meters per second.</summary>
    public sealed class SpeedReadout : MonoBehaviour
    {
        [Tooltip("The player's movement component; its measured horizontal velocity is displayed.")]
        [SerializeField] private CapsuleMovement movement;
        [Tooltip("Text that shows the speed.")]
        [SerializeField] private Text label;
        [Tooltip("Seconds to settle most of a speed change on screen. 0 shows the raw value every frame.")]
        [SerializeField, Min(0f)] private float smoothing = 0.08f;
        [Tooltip("Digits after the decimal point.")]
        [SerializeField, Range(0, 3)] private int decimals = 1;

        float shown;

        private void OnEnable()
        {
            if (!movement || !label)
            {
                Debug.LogError("Speed Readout needs the player's movement and a text label assigned.", this);
                enabled = false;
            }
        }

        // Runs after movement has moved the player this frame (see the update order in docs/ARCHITECTURE.md).
        private void LateUpdate()
        {
            Vector3 velocity = movement.PlanarVelocity;
            float speed = new Vector2(velocity.x, velocity.z).magnitude;
            shown = smoothing <= 0f ? speed : Mathf.Lerp(shown, speed, 1f - Mathf.Exp(-Time.deltaTime / smoothing));
            label.text = shown.ToString("F" + decimals) + " m/s";
        }
    }
}
