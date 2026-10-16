using UnityEngine;

namespace Game.Diagnostics
{
    /// <summary>Keys the harness can hold down. Combine with |.</summary>
    [System.Flags]
    public enum HeldKeys
    {
        None = 0, W = 1, A = 2, S = 4, D = 8, Shift = 16, Space = 32, Ctrl = 64, CapsLock = 128, Q = 256
    }

    /// <summary>What a sequence measures; selects which metrics are computed.</summary>
    public enum SequenceKind { Movement, Jump, Weapon, Unarmed, Mob }

    /// <summary>
    /// A fixed, repeatable list of inputs played from a test start marker.
    /// The baseline harness plays every sequence at several frame rates and logs each frame.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Diagnostics/Action Sequence")]
    public sealed class ActionSequence : ScriptableObject
    {
        [System.Serializable]
        public sealed class Step
        {
            [Tooltip("Name shown in the log, for example 'walk' or 'release'.")]
            public string label = "";
            [Tooltip("How long this step lasts, in seconds of game time.")]
            [Min(0.01f)] public float seconds = 1f;
            [Tooltip("Keys held for the whole step.")]
            public HeldKeys keys;
            [Tooltip("Hold the left mouse button.")]
            public bool leftMouse;
            [Tooltip("Hold the right mouse button (aim).")]
            public bool rightMouse;
            [Tooltip("When above 0, the left button is clicked (pressed for one frame) every this many seconds instead of held.")]
            [Min(0f)] public float clickInterval;
            [Tooltip("Total mouse movement over the step, in pixels, spread evenly across its frames. Positive Y looks up.")]
            public Vector2 lookPixels;
        }

        [Tooltip("Id of the TestStartMarker the player is placed on before the sequence starts.")]
        public string startMarker = "MC-Lane";
        [Tooltip("Which metrics to compute.")]
        public SequenceKind kind;
        [Tooltip("WeaponSettings asset name to equip first. Leave empty to play unarmed.")]
        public string weapon = "";
        [Tooltip("Mob label from the debug mob spawner (Mob sequences only).")]
        public string mob = "";
        [Tooltip("Seconds of no input after placing the player, so everything settles.")]
        [Min(0f)] public float settleSeconds = 0.75f;
        [Tooltip("Save a screenshot of every frame of the first 1.5 seconds (60 fps run only).")]
        public bool captureFrames;
        public Step[] steps = new Step[0];

        public float Duration
        {
            get { float d = settleSeconds; foreach (var s in steps) d += s.seconds; return d; }
        }
    }
}
