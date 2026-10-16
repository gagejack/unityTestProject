using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Diagnostics.Editor
{
    /// <summary>Menu commands for the baseline harness. Results go to Logs/Baseline/latest/.</summary>
    public static class BaselineMenu
    {
        const string Folder = "Assets/Game/Diagnostics/Sequences";
        const string RunOnPlayKey = "Game.Diagnostics.RunBaselineOnPlay";
        public static readonly int[] FrameRates = { 30, 60, 144 };

        [MenuItem("Tools/Baseline/Run All Sequences (Play Mode)")]
        public static void RunAll()
        {
            if (!EditorApplication.isPlaying)
            {
                SessionState.SetBool(RunOnPlayKey, true);
                EditorApplication.isPlaying = true;
                return;
            }
            Start();
        }

        [InitializeOnLoadMethod]
        static void Hook()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(RunOnPlayKey, false)) return;
                SessionState.SetBool(RunOnPlayKey, false);
                EditorApplication.delayCall += Start;
            };
        }

        public static void Start()
        {
            var sequences = AssetDatabase.FindAssets("t:ActionSequence", new[] { Folder })
                .Select(g => AssetDatabase.LoadAssetAtPath<ActionSequence>(AssetDatabase.GUIDToAssetPath(g)))
                .OrderBy(s => s.name).ToArray();
            BaselineRunner.Run(sequences, FrameRates, "latest");
            Debug.Log($"Baseline: running {sequences.Length} sequences at {string.Join("/", FrameRates)} fps. Results in Logs/Baseline/latest/.");
        }

        [MenuItem("Tools/Baseline/Create Or Update Sequences")]
        public static void CreateSequences()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Game/Diagnostics", "Sequences");
            const HeldKeys W = HeldKeys.W, S = HeldKeys.S, A = HeldKeys.A, D = HeldKeys.D, Run = HeldKeys.W | HeldKeys.Shift, None = HeldKeys.None;

            Make("M01_WalkStartStop", "MC-Lane", SequenceKind.Movement, "", true, K("walk", 3f, W), K("stop", 1.5f, None));
            Make("M02_RunStartStop", "MC-Lane", SequenceKind.Movement, "", false, K("run", 4f, Run), K("stop", 1.5f, None));
            Make("M03_Reverse", "MC-Lane", SequenceKind.Movement, "", false, K("walk", 2f, W), K("reverse", 2f, S), K("stop", 1f, None));
            Make("M04_Strafe", "MC-Open", SequenceKind.Movement, "", false, K("right", 2f, D), K("left", 2f, A), K("stop", 1f, None));
            Make("M05_WalkArmed", "MC-Lane", SequenceKind.Movement, "AssaultRifle", false, K("walk", 3f, W), K("stop", 1.5f, None));
            Make("M06_RunArmed", "MC-Lane", SequenceKind.Movement, "AssaultRifle", true, K("run", 4f, Run), K("stop", 1.5f, None));
            Make("J01_Jump", "MC-Lane", SequenceKind.Jump, "", false, K("idle", 0.3f, None), K("jump", 0.1f, HeldKeys.Space), K("air", 2f, None));
            Make("J02_JumpDive", "MC-Lane", SequenceKind.Jump, "", false, K("idle", 0.3f, None), K("jump", 0.1f, HeldKeys.Space),
                K("rise", 0.2f, None), K("dive", 0.05f, HeldKeys.CapsLock), K("dive and slide", 2f, None));

            // The default third-person view looks about 20° down; "level" raises it to roughly horizontal (118 px at 0.15°/px).
            var level = new Vector2(0f, 118f);
            Make("W01_AutoFireAimed", "GR-Firing", SequenceKind.Weapon, "AssaultRifle", false, K("level", 0.3f, None, look: level),
                K("aim", 0.6f, None, rmb: true), K("fire", 2f, None, lmb: true, rmb: true), K("release", 0.8f, None));
            Make("W02_AutoFireStrafing", "GR-Strafe", SequenceKind.Weapon, "AssaultRifle", true, K("level", 0.3f, None, look: level),
                K("strafe", 1f, D), K("strafe+fire", 2.5f, D, lmb: true), K("stop", 0.8f, None));
            Make("W03_PistolTaps", "GR-Firing", SequenceKind.Weapon, "Sidearm", false, K("level", 0.3f, None, look: level),
                K("aim", 0.5f, None, rmb: true), K("taps", 2f, None, rmb: true, click: 0.2f), K("release", 0.5f, None));
            Make("W04_RocketBlast", "GR-Rocket", SequenceKind.Weapon, "Bazooka", false, K("level", 0.3f, None, look: level),
                K("aim", 0.8f, None, rmb: true), K("fire", 0.1f, None, lmb: true, rmb: true), K("watch", 2.5f, None));

            Make("U01_PunchCombo", "GR-Melee", SequenceKind.Unarmed, "", false, K("idle", 0.3f, None), K("clicks", 2.5f, None, click: 0.35f), K("recover", 1f, None));
            Make("U02_PunchWhileWalking", "MC-Lane", SequenceKind.Unarmed, "", false, K("walk", 1f, W), K("walk+clicks", 2f, W, click: 0.4f), K("stop", 1f, None));

            foreach (string mob in new[] { "Dwarf", "Giant", "Skeleton", "Soldier", "Warrior", "Zombie" })
            {
                var seq = Make("X_Mob_" + mob, "TA-Start", SequenceKind.Mob, "", false, K("watch", 8f, None));
                seq.mob = mob;
                EditorUtility.SetDirty(seq);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Baseline sequences written to " + Folder);
        }

        static ActionSequence.Step K(string label, float seconds, HeldKeys keys, bool lmb = false, bool rmb = false, float click = 0f, Vector2 look = default) =>
            new ActionSequence.Step { label = label, seconds = seconds, keys = keys, leftMouse = lmb, rightMouse = rmb, clickInterval = click, lookPixels = look };

        static ActionSequence Make(string name, string marker, SequenceKind kind, string weapon, bool capture, params ActionSequence.Step[] steps)
        {
            string path = Folder + "/" + name + ".asset";
            var seq = AssetDatabase.LoadAssetAtPath<ActionSequence>(path);
            if (!seq) { seq = ScriptableObject.CreateInstance<ActionSequence>(); AssetDatabase.CreateAsset(seq, path); }
            seq.startMarker = marker;
            seq.kind = kind;
            seq.weapon = weapon;
            seq.captureFrames = capture;
            seq.steps = steps;
            EditorUtility.SetDirty(seq);
            return seq;
        }
    }
}
