using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Game.TestAreas;
using StarterGame;
using StarterGame.Characters;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Game.Diagnostics
{
    /// <summary>
    /// Plays action sequences in Play mode with a fixed timestep per frame rate, injecting keyboard and mouse
    /// input through the Input System so the real input path is measured. Writes one CSV per sequence and
    /// a summary to Logs/Baseline/ (not committed; the numbers that matter are copied into docs/FEEL_SPEC.md).
    /// </summary>
    public sealed class BaselineRunner : MonoBehaviour
    {
        public static string Status { get; private set; } = "idle";
        public static string Summary { get; private set; } = "";
        public static bool Busy { get; private set; }

        ActionSequence[] sequences;
        int[] frameRates;
        string outputFolder;
        BaselineRecorder recorder;
        HeldKeys keys;
        bool leftMouse, rightMouse;

        public static void Run(ActionSequence[] sequences, int[] frameRates, string label)
        {
            if (Busy || !Application.isPlaying) return;
            Status = "starting";
            var go = new GameObject("Baseline Runner");
            var runner = go.AddComponent<BaselineRunner>();
            runner.sequences = sequences;
            runner.frameRates = frameRates;
            runner.outputFolder = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Logs", "Baseline", label);
            runner.StartCoroutine(runner.RunAll());
        }

        // Everything the run changes outside itself, so Restore() can undo it even if the run fails.
        List<InputDevice> realDevices = new List<InputDevice>();
        InputSettings.BackgroundBehavior oldBackground;
#if UNITY_EDITOR
        InputSettings.EditorInputBehaviorInPlayMode oldEditor;
#endif
        bool takenOver;

        void TakeOver()
        {
            var settings = InputSystem.settings;
            oldBackground = settings.backgroundBehavior;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            oldEditor = settings.editorInputBehaviorInPlayMode;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            // The harness types on its own virtual keyboard and mouse; the real ones are switched off
            // for the run so neither the owner's typing nor the OS's key-state reports can interfere.
            realDevices = InputSystem.devices.Where(d => (d is Keyboard || d is Pointer) && d.enabled).ToList();
            virtualKeyboard = InputSystem.AddDevice<Keyboard>("Baseline Keyboard");
            virtualMouse = InputSystem.AddDevice<Mouse>("Baseline Mouse");
            foreach (var device in realDevices) InputSystem.DisableDevice(device);
            takenOver = true;
        }

        // Safe to call more than once; also runs if the runner is destroyed or Play mode stops mid-run.
        void Restore()
        {
            if (!takenOver) return;
            takenOver = false;
            Time.captureDeltaTime = 0f;
            if (virtualKeyboard != null) InputSystem.RemoveDevice(virtualKeyboard);
            if (virtualMouse != null) InputSystem.RemoveDevice(virtualMouse);
            virtualKeyboard = null;
            virtualMouse = null;
            foreach (var device in realDevices) if (device != null && device.added) InputSystem.EnableDevice(device);
            Cursor.lockState = CursorLockMode.None;
            InputSystem.settings.backgroundBehavior = oldBackground;
#if UNITY_EDITOR
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditor;
#endif
            if (recorder) Destroy(recorder.gameObject);
            Busy = false;
        }

        private void OnDisable() => Restore();

        void PrepareOutputFolder()
        {
            Directory.CreateDirectory(outputFolder);
            foreach (var file in Directory.GetFiles(outputFolder, "*", SearchOption.AllDirectories))
                try { File.Delete(file); } catch (IOException) { }
        }

        IEnumerator RunAll()
        {
            Busy = true;
            try { PrepareOutputFolder(); }
            catch (System.Exception e) { Debug.LogError("Baseline: cannot prepare " + outputFolder + ": " + e.Message); Busy = false; Destroy(gameObject); yield break; }
            TakeOver();
            var player = FindAnyObjectByType<CapsuleMovement>().gameObject;
            var menu = FindAnyObjectByType<ViewModeDebugMenu>();
            if (menu) menu.SetOpen(false);
            recorder = new GameObject("Baseline Recorder").AddComponent<BaselineRecorder>();
            recorder.Bind(player);
            var summary = new StringBuilder();
            var results = new List<string>();
            foreach (int fps in frameRates)
            {
                Time.captureDeltaTime = 1f / fps;
                foreach (var sequence in sequences)
                {
                    // The baseline ignores some input while the Game view is unfocused; retry a run that lost focus.
                    for (int attempt = 1; attempt <= 3; attempt++)
                    {
                        Status = $"{fps} fps: {sequence.name} (attempt {attempt})";
                        yield return Play(sequence, fps, player);
                        if (recorder.Rows.TrueForAll(r => r.focused)) break;
                    }
                    var metrics = BaselineMetrics.Compute(sequence, recorder.Rows);
                    File.WriteAllText(Path.Combine(outputFolder, $"{sequence.name}_{fps}fps.csv"), BaselineRecorder.ToCsv(recorder.Rows));
                    foreach (var pair in metrics)
                        results.Add(string.Join(",", sequence.name, fps.ToString(), pair.Key, pair.Value.ToString("0.####", CultureInfo.InvariantCulture)));
                    summary.AppendLine($"[{fps} fps] {sequence.name}: " + string.Join("  ", metrics.Select(p => p.Key + "=" + p.Value.ToString("0.###", CultureInfo.InvariantCulture))));
                }
            }
            File.WriteAllText(Path.Combine(outputFolder, "metrics.csv"), "sequence,fps,metric,value\n" + string.Join("\n", results));
            File.WriteAllText(Path.Combine(outputFolder, "summary.txt"), summary.ToString());
            keys = HeldKeys.None; leftMouse = rightMouse = false;
            QueueInput();
            Restore();
            Summary = summary.ToString();
            Status = "done: " + outputFolder;
            Destroy(gameObject);
        }

        IEnumerator Play(ActionSequence sequence, int fps, GameObject player)
        {
            recorder.Recording = false;
            recorder.Rows.Clear();
            recorder.Mob = null;
            keys = HeldKeys.None; leftMouse = rightMouse = false; look = Vector2.zero;
            ResettableGroup.ResetAll();
            var spawner = Camera.main ? Camera.main.GetComponent<DebugMobSpawner>() : null;
            if (spawner) spawner.ClearSpawned();
            var weapons = player.GetComponent<WeaponCombatController>();

            // Arm or disarm. Disarming calls the same drop the Q key uses (which the game ignores while
            // unfocused), then removes the dropped gun so it cannot get in the way.
            if (string.IsNullOrEmpty(sequence.weapon))
            {
                if (weapons.HasWeapon) DropWeaponMethod?.Invoke(weapons, null);
                yield return null;
                foreach (var dropped in FindObjectsByType<DroppedWeapon>()) Destroy(dropped.gameObject);
            }
            else
            {
                int index = -1;
                for (int i = 0; i < weapons.WeaponOffers.Count; i++)
                    if (weapons.WeaponOffers[i] && weapons.WeaponOffers[i].name == sequence.weapon) index = i;
                if (index < 0) Debug.LogError($"Sequence {sequence.name}: no weapon named {sequence.weapon} in the Offers list.");
                else weapons.EquipFromDebug(index);
            }

            var marker = TestStartMarker.Find(sequence.startMarker);
            if (!marker) { Debug.LogError($"Sequence {sequence.name}: no start marker {sequence.startMarker}."); yield break; }
            marker.TeleportPlayer();
            FocusGameView();
            Cursor.lockState = CursorLockMode.Locked;
            float giveUp = Time.realtimeSinceStartup + 3f;
            while (!Application.isFocused && Time.realtimeSinceStartup < giveUp) { FocusGameView(); yield return null; }
            yield return Frames(Mathf.Max(1, Mathf.RoundToInt(sequence.settleSeconds * fps)));

            if (sequence.kind == SequenceKind.Mob && spawner)
            {
                for (int i = 0; i < spawner.Count; i++) { spawner.Select(i); if (spawner.SelectedName == sequence.mob) break; }
                recorder.Mob = spawner.SelectedName == sequence.mob ? spawner.SpawnSelected() : null;
                if (!recorder.Mob) Debug.LogError($"Sequence {sequence.name}: could not spawn {sequence.mob}: {spawner.Status}");
            }

            recorder.Recording = true;
            int captured = 0;
            for (int s = 0; s < sequence.steps.Length; s++)
            {
                var step = sequence.steps[s];
                recorder.CurrentStep = s;
                int frames = Mathf.Max(1, Mathf.RoundToInt(step.seconds * fps));
                int clickEvery = step.clickInterval > 0f ? Mathf.Max(2, Mathf.RoundToInt(step.clickInterval * fps)) : 0;
                for (int f = 0; f < frames; f++)
                {
                    keys = step.keys;
                    rightMouse = step.rightMouse;
                    leftMouse = clickEvery > 0 ? f % clickEvery == 0 : step.leftMouse;
                    look = step.lookPixels / frames;
                    if (sequence.captureFrames && fps == 60 && captured < 90)
                    {
                        string folder = Path.Combine(outputFolder, "frames", sequence.name);
                        Directory.CreateDirectory(folder);
                        ScreenCapture.CaptureScreenshot(Path.Combine(folder, $"f{captured++:000}.png"));
                    }
                    yield return null;
                }
            }
            recorder.Recording = false;
            recorder.CurrentStep = -1;
            keys = HeldKeys.None; leftMouse = rightMouse = false; look = Vector2.zero;
            yield return Frames(2);
            if (spawner) spawner.ClearSpawned();
        }

        // The baseline ignores mouse, Q, R and F input unless the Game view has focus (Application.isFocused),
        // so every sequence starts by focusing it. Frames that ran unfocused are flagged in the metrics.
        static void FocusGameView()
        {
#if UNITY_EDITOR
            var gameView = System.Type.GetType("UnityEditor.GameView, UnityEditor");
            if (gameView != null) UnityEditor.EditorWindow.FocusWindowIfItsOpen(gameView);
#endif
        }

        IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        int queuedInput;
        Vector2 look;
        static readonly System.Reflection.MethodInfo DropWeaponMethod = typeof(WeaponCombatController).GetMethod("DropWeapon",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Runs before the recorder each frame: report the input this frame actually received
        // (queued last frame), then queue the input for the next frame.
        private void LateUpdate()
        {
            if (!Busy) return;
            if (recorder) recorder.CurrentInput = queuedInput;
            QueueInput();
        }

        Keyboard virtualKeyboard;
        Mouse virtualMouse;

        void QueueInput()
        {
            var keyboard = virtualKeyboard;
            var mouse = virtualMouse;
            if (keyboard == null || mouse == null) return;
            keyboard.MakeCurrent();
            mouse.MakeCurrent();
            var state = new KeyboardState();
            if ((keys & HeldKeys.W) != 0) state.Press(Key.W);
            if ((keys & HeldKeys.A) != 0) state.Press(Key.A);
            if ((keys & HeldKeys.S) != 0) state.Press(Key.S);
            if ((keys & HeldKeys.D) != 0) state.Press(Key.D);
            if ((keys & HeldKeys.Shift) != 0) state.Press(Key.LeftShift);
            if ((keys & HeldKeys.Space) != 0) state.Press(Key.Space);
            if ((keys & HeldKeys.Ctrl) != 0) state.Press(Key.LeftCtrl);
            if ((keys & HeldKeys.CapsLock) != 0) state.Press(Key.CapsLock);
            if ((keys & HeldKeys.Q) != 0) state.Press(Key.Q);
            InputSystem.QueueStateEvent(keyboard, state);
            // Keep the pointer in the middle of the screen so UI never swallows clicks.
            var pointer = new MouseState { position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), delta = look }
                .WithButton(MouseButton.Left, leftMouse).WithButton(MouseButton.Right, rightMouse);
            InputSystem.QueueStateEvent(mouse, pointer);
            queuedInput = (int)keys | (leftMouse ? BaselineMetrics.LeftMouseBit : 0) | (rightMouse ? BaselineMetrics.RightMouseBit : 0);
        }
    }
}
