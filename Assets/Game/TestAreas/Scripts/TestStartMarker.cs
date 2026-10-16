using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.TestAreas
{
    /// <summary>A labeled spot where a test or a fixed action sequence starts. The player faces the marker's forward (blue arrow).</summary>
    public sealed class TestStartMarker : MonoBehaviour
    {
        static readonly List<TestStartMarker> all = new List<TestStartMarker>();

        [Tooltip("Short unique id used by action sequences and shortcuts, for example GR-Firing or MC-Lane.")]
        [SerializeField] private string id = "";
        [Tooltip("Name shown in the scene and the shortcut hint.")]
        [SerializeField] private string displayName = "";
        [Tooltip("Height above the marker where the player's capsule centre is placed, in meters.")]
        [SerializeField, Min(0f)] private float playerCentreHeight = 1.05f;

        public static IReadOnlyList<TestStartMarker> All => all;
        public string Id => id;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? id : displayName;

        public static TestStartMarker Find(string markerId) => all.Find(m => m && m.id == markerId);

        /// <summary>Moves the player here, facing the marker's forward, with all movement cleared.</summary>
        public void TeleportPlayer()
        {
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            PlayerRequests.Teleport(transform.position + Vector3.up * playerCentreHeight,
                Quaternion.LookRotation(forward.sqrMagnitude > 0.001f ? forward : Vector3.forward));
        }

        public void Configure(string markerId, string label)
        {
            id = markerId;
            displayName = label;
        }

        private void OnEnable() => all.Add(this);
        private void OnDisable() => all.Remove(this);
    }
}
