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

        // FEEL_SPEC > Fixed view aiming: "Fixed-view aim error" ≤ 0.5°, hip fire and aimed.
        const float AimError = 0.5f;

        [UnityTest]
        public IEnumerator TheBarrelPointsAtTheAimPoint([Values("AssaultRifle", "Sidearm", "DualHandguns")] string gun)
        {
            yield return StartOnRing(60);
            yield return Equip(gun);
            Vector3 aim = Floor(player.position + new Vector3(4f, 0f, 3f));
            yield return Hold(aim, 60);
            float hip = BarrelErrors(gun).Max();
            right = true;
            yield return Hold(aim, 30);
            float aimed = BarrelErrors(gun).Max();
            right = false;
            Assert.That(hip, Is.LessThanOrEqualTo(AimError), $"{gun} hip-fire barrel error (degrees)");
            Assert.That(aimed, Is.LessThanOrEqualTo(AimError), $"{gun} aimed barrel error (degrees)");
        }

        // Spec check: facing and shooting each ring target in turn, including those behind the player, hits it.
        [UnityTest]
        public IEnumerator AimedShotsHitEveryRingTarget()
        {
            yield return StartOnRing(60);
            yield return Equip("AssaultRifle");
            var hits = new System.Collections.Generic.List<GameObject>();
            void OnDamaged(DamageReport report) { if (report.Target) hits.Add(report.Target); }
            CombatEvents.Damaged += OnDamaged;
            try
            {
                // Clockwise from the front, then straight across to the target behind.
                foreach (float angle in new[] { 0f, 45f, 90f, 135f, 225f, 270f, 315f, 180f })
                {
                    var target = GameObject.Find($"Ring target {angle}°").transform;
                    var body = target.Find("Body").GetComponent<Collider>().bounds.center;
                    right = true;
                    yield return Hold(body, 30);
                    var weapons = WeaponController();
                    float before = (float)weapons.GetType().GetProperty("LastShotTime").GetValue(weapons);
                    left = true;
                    yield return Hold(body, 1);
                    left = false;
                    yield return Hold(body, 12);
                    right = false;
                    bool fired = (float)weapons.GetType().GetProperty("LastShotTime").GetValue(weapons) > before;
                    Assert.IsTrue(hits.Any(h => h.transform.IsChildOf(target)), $"The aimed shot missed the {angle}° ring target " +
                        $"(fired {fired}, aim point {heading.AimPoint}, facing {player.eulerAngles.y:F1}°, following cursor {heading.FacesAimPoint}, " +
                        $"window focused {Application.isFocused}, hit {string.Join(", ", hits.Select(h => h.name))})");
                    hits.Clear();
                }
            }
            finally { CombatEvents.Damaged -= OnDamaged; }
        }

        // Owner decision (spec): sprinting faces the cursor within 100° of the run; further round it faces the run,
        // and a click turns back to the cursor before firing.
        [UnityTest]
        public IEnumerator SprintingAwayFacesTheRunAndAClickTurnsBeforeFiring()
        {
            yield return StartOnRing(60);
            yield return Equip("AssaultRifle");
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var movement = player.GetComponent<CapsuleMovement>();
            var weapons = WeaponController();

            // Sprinting right with the cursor up the screen (90° from the run): keep facing the cursor.
            yield return Sprint(keyboard, Key.D, 40);
            // The cursor point trails a sprinting player slightly (the aim point uses last frame's camera).
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(player.eulerAngles.y, 0f)), Is.LessThan(5f), "Sprinting right should face the cursor up the screen");

            // Sprinting down with the cursor up the screen (180°): face the run.
            TestStartMarker.Find("GR-Ring").TeleportPlayer();
            yield return Sprint(keyboard, Key.S, 40);
            Assert.IsTrue(movement.IsRunning && movement.SprintFacesTravel, "Sprinting away from the cursor should face the run");
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(player.eulerAngles.y, 180f)), Is.LessThan(5f), "Sprinting down should face down the screen");

            // Click once: the sprint stops, the body turns, and the shot waits until the gun is within 45° of the cursor.
            float clickedAt = Time.time, lastShot = (float)weapons.GetType().GetProperty("LastShotTime").GetValue(weapons);
            float gapAtShot = -1f;
            left = true;
            for (int i = 0; i < 30 && gapAtShot < 0f; i++)
            {
                PointAt(Floor(player.position + Vector3.forward * 4f));
                yield return null;
                left = false;
                if ((float)weapons.GetType().GetProperty("LastShotTime").GetValue(weapons) > lastShot)
                    gapAtShot = (float)weapons.GetType().GetProperty("FacingGapToCursor").GetValue(weapons);
            }
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            Assert.That(gapAtShot, Is.InRange(0f, 45f), "The shot should fire once the gun is within 45° of the cursor");
            Assert.That(Time.time - clickedAt, Is.LessThan(0.25f), "The turn before the shot should take under a quarter second");
        }

        IEnumerator Sprint(Keyboard keyboard, Key key, int frames)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key, Key.LeftShift));
            InputSystem.Update();
            for (int i = 0; i < frames; i++)
            {
                PointAt(Floor(player.position + Vector3.forward * 4f));
                yield return null;
            }
        }

        // Angle between each held barrel and the line from its muzzle to the cursor's aim point (degrees).
        System.Collections.Generic.IEnumerable<float> BarrelErrors(string gun)
        {
            var settings = WeaponController().GetType().GetProperty("CurrentWeapon").GetValue(WeaponController());
            var muzzle = (Vector3)settings.GetType().GetField("muzzleLocalPosition").GetValue(settings);
            var rig = player.GetComponentsInChildren<Animator>().First(a => a.isHuman);
            foreach (var hand in new[] { HumanBodyBones.RightHand, HumanBodyBones.LeftHand })
                foreach (Transform held in rig.GetBoneTransform(hand))
                    if (held.name.EndsWith("(equipped)") || held.name.EndsWith("(left hand)"))
                        yield return Vector3.Angle(held.forward, heading.AimPoint - held.TransformPoint(muzzle));
        }

        // Weapons is in another assembly; equip by asset name, as the other tests do.
        MonoBehaviour WeaponController() => player.GetComponents<MonoBehaviour>().Single(b => b.GetType().Name == "WeaponCombatController");

        IEnumerator Equip(string gun)
        {
            var weapons = WeaponController();
            var offers = ((IEnumerable)weapons.GetType().GetProperty("WeaponOffers").GetValue(weapons)).Cast<Object>().ToList();
            int index = offers.FindIndex(o => o && o.name == gun);
            Assert.IsTrue((bool)weapons.GetType().GetMethod("EquipFromDebug").Invoke(weapons, new object[] { index }), $"Could not equip {gun}.");
            yield return null; // a gun held before is destroyed at the end of the equip frame
        }

        IEnumerator Hold(Vector3 world, int frames = 5)
        {
            for (int i = 0; i < frames; i++)
            {
                PointAt(world);
                yield return null;
            }
        }

        bool left, right;

        // Sets the whole mouse state at once (position and buttons): releasing single controls on test devices does
        // not always take effect.
        void PointAt(Vector3 world)
        {
            var state = new MouseState { position = (Vector2)view.WorldToScreenPoint(world) }
                .WithButton(MouseButton.Left, left).WithButton(MouseButton.Right, right);
            InputSystem.QueueStateEvent(mouse, state);
            InputSystem.Update();
        }

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
