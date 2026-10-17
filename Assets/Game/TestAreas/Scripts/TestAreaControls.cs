using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.TestAreas
{
    /// <summary>Keyboard shortcuts for the test areas: jump to a start marker, reset targets and refill ammo.</summary>
    public sealed class TestAreaControls : MonoBehaviour
    {
        [Tooltip("Marker id for F5.")]
        [SerializeField] private string gunRangeMarker = "GR-Firing";
        [Tooltip("Marker id for F6.")]
        [SerializeField] private string movementCourseMarker = "MC-Lane";
        [Tooltip("Marker id for F7.")]
        [SerializeField] private string testAreaMarker = "TA-Start";
        [Tooltip("Marker id for F8.")]
        [SerializeField] private string aimRingMarker = "GR-Ring";
        [Tooltip("Show the shortcut hint in the bottom-left corner.")]
        [SerializeField] private bool showHint = true;

        private void Update()
        {
            var keys = Keyboard.current;
            if (keys == null) return;
            if (keys.f5Key.wasPressedThisFrame) Go(gunRangeMarker);
            if (keys.f6Key.wasPressedThisFrame) Go(movementCourseMarker);
            if (keys.f7Key.wasPressedThisFrame) Go(testAreaMarker);
            if (keys.f8Key.wasPressedThisFrame) Go(aimRingMarker);
            if (keys.f9Key.wasPressedThisFrame)
            {
                ResettableGroup.ResetAll();
                PlayerRequests.RefillAmmo();
            }
        }

        static void Go(string markerId)
        {
            var marker = TestStartMarker.Find(markerId);
            if (marker) marker.TeleportPlayer();
            else Debug.LogWarning("No test start marker with id " + markerId);
        }

        private void OnGUI()
        {
            if (!showHint) return;
            GUI.Label(new Rect(12, Screen.height - 26, 800, 22),
                "F5 Gun range   F6 Movement course   F7 Mob test area   F8 Aim ring   F9 Reset targets + refill ammo");
        }
    }
}
