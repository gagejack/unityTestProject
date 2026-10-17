using System.Collections;
using System.Linq;
using Game.Core;
using Game.TestAreas;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Movement.Tests
{
    /// <summary>
    /// Play-mode checks for the movement targets in docs/FEEL_SPEC.md (Movement section), at 30, 60 and 144 fps.
    /// Each test loads the Main scene, closes the debug menu, starts from the MC-Lane marker on the Movement
    /// Course, and presses keys on a test keyboard.
    /// </summary>
    public sealed class MovementFeelTests : InputTestFixture
    {
        // FEEL_SPEC > Movement: "Jump apex 1.6 ±0.03 at all frame rates".
        const float JumpApex = 1.6f, JumpTolerance = 0.03f;
        // FEEL_SPEC > Movement: "Walk stopping distance" 0.18 m, the same at all frame rates (±0.02).
        const float WalkStop = 0.18f, StopTolerance = 0.02f;
        // FEEL_SPEC > Movement: walking down slopes and steps never leaves the ground (0 airborne frames).
        const int AllowedAirborneFrames = 0;

        Keyboard keyboard;
        Transform player;

        [TearDown]
        public void RestoreTime() => Time.captureDeltaTime = 0f;

        [UnityTest]
        public IEnumerator JumpReachesItsHeightAtEveryFrameRate([Values(30, 60, 144)] int fps)
        {
            yield return StartOnLane(fps);
            float start = player.position.y, highest = start;
            Press(keyboard.spaceKey);
            yield return null;
            Release(keyboard.spaceKey);
            for (int i = 0; i < fps * 1.5f; i++)
            {
                highest = Mathf.Max(highest, player.position.y);
                yield return null;
            }
            Assert.That(highest - start, Is.EqualTo(JumpApex).Within(JumpTolerance), $"Jump apex at {fps} fps");
        }

        [UnityTest]
        public IEnumerator WalkStopsInTheSameDistanceAtEveryFrameRate([Values(30, 60, 144)] int fps)
        {
            yield return StartOnLane(fps);
            Press(keyboard.wKey);
            for (int i = 0; i < fps * 2; i++) yield return null;
            Release(keyboard.wKey);
            Vector3 releasedAt = player.position;
            for (int i = 0; i < fps; i++) yield return null;
            float distance = Vector3.ProjectOnPlane(player.position - releasedAt, Vector3.up).magnitude;
            Assert.That(distance, Is.EqualTo(WalkStop).Within(StopTolerance), $"Walk stopping distance at {fps} fps");
        }

        [UnityTest]
        public IEnumerator WalkingDownSlopesAndStepsStaysOnTheGround(
            [Values("MC-Slope20-Top", "MC-Slope40-Top", "MC-Steps-Top")] string start, [Values(30, 60, 144)] int fps)
        {
            yield return StartOnLane(fps, start);
            var movement = player.GetComponent<CapsuleMovement>();
            float startHeight = player.position.y;
            int airborne = 0;
            Press(keyboard.wKey);
            yield return null;
            for (int i = 0; i < fps * 2.5f; i++)
            {
                if (!movement.IsGrounded) airborne++;
                yield return null;
            }
            Release(keyboard.wKey);
            Assert.That(startHeight - player.position.y, Is.GreaterThan(2f), $"Did not reach the bottom from {start}");
            Assert.That(airborne, Is.LessThanOrEqualTo(AllowedAirborneFrames), $"Airborne frames walking down from {start} at {fps} fps");
        }

        // Runs inside each test, after InputTestFixture.Setup has given the test its own clean input system
        // (Unity runs [UnitySetUp] before [SetUp], so loading here keeps the scene's input actions valid).
        IEnumerator StartOnLane(int fps, string startMarker = "MC-Lane")
        {
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return null;
            // The debug menu starts open and takes control away from the player; switch it off for the test.
            foreach (var behaviour in Object.FindObjectsByType<MonoBehaviour>().Where(b => b.GetType().Name == "ViewModeDebugMenu"))
                behaviour.enabled = false;
            keyboard = InputSystem.AddDevice<Keyboard>();
            player = PlayerPresence.Current;
            Assert.IsNotNull(player, "The Main scene has no active player.");
            Time.captureDeltaTime = 1f / fps;
            var marker = TestStartMarker.Find(startMarker);
            Assert.IsNotNull(marker, $"Start marker {startMarker} is missing from the Movement Course.");
            marker.TeleportPlayer();
            for (int i = 0; i < fps / 2; i++) yield return null;
        }
    }
}
