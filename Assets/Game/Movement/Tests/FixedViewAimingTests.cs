using System.Collections;
using System.Linq;
using Game.Core;
using Game.TestAreas;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Movement.Tests
{
    /// <summary>
    /// Play-mode checks for fixed view aiming (docs/FEEL_SPEC.md, Fixed view aiming). Each test loads the Main scene,
    /// switches to the fixed view, closes the debug menu, starts on the GR-Ring pad and moves a test mouse to points
    /// in the world (converted to screen positions every frame, since the camera follows the player).
    /// </summary>
    public sealed class FixedViewAimingTests : InputTestFixture
    {
        // FEEL_SPEC > Fixed view aiming: "Turn to cursor" 0.25 s ±1 frame at all frame rates.
        const float TurnTime = 0.25f;
        // FEEL_SPEC > Fixed view aiming: "Dead-zone stability" 0°.
        const float DeadZoneDrift = 0f;
        // Saved on Main Camera > Follow Camera: Cursor Dead Zone 0.75 m, Floor Aim Height 1.2 m.
        const float DeadZone = 0.75f, FloorAimHeight = 1.2f;

        Mouse mouse;
        Transform player;
        Camera view;
        IViewHeading heading;
        float feet;

        [TearDown]
        public void Restore()
        {
            Time.captureDeltaTime = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        [UnityTest]
        public IEnumerator TurningToTheCursorTakesAQuarterSecond([Values(30, 60, 144)] int fps)
        {
            yield return StartOnRing(fps);
            Vector3 behind = Floor(player.position - player.forward * 4f);
            int frames = 0;
            do
            {
                PointAt(behind);
                yield return null;
                frames++;
            } while (Mathf.Abs(Mathf.DeltaAngle(player.eulerAngles.y, YawTo(heading.AimPoint))) > 0.01f && frames < fps * 2);
            Assert.That(frames / (float)fps, Is.EqualTo(TurnTime).Within(1f / fps + 0.0001f), $"180° turn to the cursor at {fps} fps");
        }

        [UnityTest]
        public IEnumerator TheCursorInsideTheDeadZoneDoesNotTurnTheBody()
        {
            yield return StartOnRing(60);
            float settled = player.eulerAngles.y, drift = 0f;
            // One slow lap of the cursor at two thirds of the dead-zone radius around the character's feet.
            for (int i = 0; i < 60; i++)
            {
                PointAt(Floor(player.position + Quaternion.Euler(0f, i * 6f, 0f) * Vector3.forward * (DeadZone * 0.66f)));
                yield return null;
                drift = Mathf.Max(drift, Mathf.Abs(Mathf.DeltaAngle(settled, player.eulerAngles.y)));
            }
            Assert.That(drift, Is.LessThanOrEqualTo(DeadZoneDrift), "Facing change with the cursor inside the dead zone");

            // Just outside the dead zone, to the right, the body turns again.
            Vector3 right = player.right;
            for (int i = 0; i < 30; i++)
            {
                PointAt(Floor(player.position + right * (DeadZone + 0.5f)));
                yield return null;
            }
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(settled + 90f, player.eulerAngles.y)), Is.LessThan(1f), "Turn once the cursor leaves the dead zone");
        }

        [UnityTest]
        public IEnumerator TheAimPointFollowsTheFloorRule()
        {
            yield return StartOnRing(60);

            // Plain floor: chest height above the point under the cursor.
            Vector3 floor = Floor(player.position + Vector3.left * 4f);
            yield return Hold(floor);
            Assert.That(Vector3.Distance(heading.AimPoint, floor + Vector3.up * FloorAimHeight), Is.LessThan(0.05f), "Aim point over plain floor");

            // A box top counts as floor: chest height above the box, so a target standing on it is aimed at its chest.
            var box = GameObject.Find("Aim ring box 45°");
            Assert.IsNotNull(box, "The aim ring is missing (Tools > Test Areas > Rebuild Gun Range and Movement Course).");
            var top = box.GetComponent<Collider>().bounds;
            Vector3 boxTop = new Vector3(top.center.x - 0.5f, top.max.y, top.center.z - 0.5f);
            yield return Hold(boxTop);
            Assert.That(Vector3.Distance(heading.AimPoint, boxTop + Vector3.up * FloorAimHeight), Is.LessThan(0.05f), "Aim point over a box top");

            // A target: the exact point the cursor touches.
            var target = GameObject.Find("Ring target 0°").transform;
            yield return Hold(target.position + Vector3.up * 0.85f);
            float distance = target.GetComponentsInChildren<Collider>().Min(c => Vector3.Distance(c.ClosestPoint(heading.AimPoint), heading.AimPoint));
            Assert.That(distance, Is.LessThan(0.02f), "Aim point over a target is on the target");
        }

        // Owner decision (spec, 2026-10-17): Shift sprints in any direction, not only forward.
        [UnityTest]
        public IEnumerator ShiftSprintsInEveryDirection()
        {
            yield return StartOnRing(60);
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var movement = player.GetComponent<CapsuleMovement>();
            // The fixed camera looks along +Z, so screen-up is +Z and screen-right is +X.
            var directions = new (string name, Key[] keys, Vector3 travel)[]
            {
                ("up", new[] { Key.W }, new Vector3(0f, 0f, 1f)), ("up-right", new[] { Key.W, Key.D }, new Vector3(1f, 0f, 1f)),
                ("right", new[] { Key.D }, new Vector3(1f, 0f, 0f)), ("down-right", new[] { Key.S, Key.D }, new Vector3(1f, 0f, -1f)),
                ("down", new[] { Key.S }, new Vector3(0f, 0f, -1f)), ("down-left", new[] { Key.S, Key.A }, new Vector3(-1f, 0f, -1f)),
                ("left", new[] { Key.A }, new Vector3(-1f, 0f, 0f)), ("up-left", new[] { Key.W, Key.A }, new Vector3(-1f, 0f, 1f)),
            };
            foreach (var (name, keys, travel) in directions)
            {
                TestStartMarker.Find("GR-Ring").TeleportPlayer();
                // Set the whole keyboard at once: releasing single keys on the test keyboard does not always take effect.
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys.Append(Key.LeftShift).ToArray()));
                InputSystem.Update();
                // Keep the cursor ahead of the character (up the screen) while it runs.
                for (int i = 0; i < 48; i++)
                {
                    PointAt(Floor(player.position + Vector3.forward * 4f));
                    yield return null;
                }
                bool running = movement.IsRunning;
                Vector3 velocity = movement.PlanarVelocity;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                Assert.IsTrue(running, $"Shift + {name} does not sprint");
                Assert.That(Vector3.Angle(velocity, travel), Is.LessThan(2f), $"Shift + {name} travels the wrong way");
                yield return null;
            }
        }

        IEnumerator Hold(Vector3 world)
        {
            for (int i = 0; i < 5; i++)
            {
                PointAt(world);
                yield return null;
            }
        }

        void PointAt(Vector3 world) => Set(mouse.position, (Vector2)view.WorldToScreenPoint(world));

        Vector3 Floor(Vector3 point) => new Vector3(point.x, feet, point.z);

        float YawTo(Vector3 point)
        {
            Vector3 flat = Vector3.ProjectOnPlane(point - player.position, Vector3.up);
            return Quaternion.LookRotation(flat).eulerAngles.y;
        }

        // Runs inside each test, after InputTestFixture.Setup has given the test its own clean input system.
        IEnumerator StartOnRing(int fps)
        {
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return null;
            // The camera and debug menu are in the default assembly: reach them by name, as the other tests do.
            var menu = Object.FindObjectsByType<MonoBehaviour>().Single(b => b.GetType().Name == "ViewModeDebugMenu");
            var setMode = menu.GetType().GetMethod("SetMode");
            setMode.Invoke(menu, new[] { System.Enum.Parse(setMode.GetParameters()[0].ParameterType, "FixedCamera") });
            // The debug menu starts open and takes control away from the player; switch it off for the test.
            menu.enabled = false;
            mouse = InputSystem.AddDevice<Mouse>();
            view = Camera.main;
            heading = view.GetComponent<IViewHeading>();
            // Give the game the pointer, as closing the menu does.
            heading.GetType().GetMethod("SetMouseCaptured").Invoke(heading, new object[] { true });
            player = PlayerPresence.Current;
            Assert.IsNotNull(player, "The Main scene has no active player.");
            Time.captureDeltaTime = 1f / fps;
            var marker = TestStartMarker.Find("GR-Ring");
            Assert.IsNotNull(marker, "Start marker GR-Ring is missing (Tools > Test Areas > Rebuild Gun Range and Movement Course).");
            marker.TeleportPlayer();
            // The pad lies on the floor.
            feet = marker.transform.position.y;
            for (int i = 0; i < fps / 2; i++)
            {
                PointAt(Floor(player.position + Vector3.forward * 4f));
                yield return null;
            }
            Assert.IsTrue(heading.FacesAimPoint, "Fixed view is not following the cursor.");
            Assert.That(Mathf.DeltaAngle(player.eulerAngles.y, 0f), Is.EqualTo(0f).Within(0.5f), "The player did not settle facing the ring's front.");
        }
    }
}
