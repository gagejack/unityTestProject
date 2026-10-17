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

namespace Game.Weapons.Tests
{
    /// <summary>
    /// Play-mode checks for holstering (docs/spec/Holstering/SPEC.md): every gun goes to its category's spot on the body,
    /// empty hands are unarmed, H swaps the gun in hand with the holstered one (each keeps its ammo), right mouse draws,
    /// and Q drops only the gun in hand. Weapons live in the default assembly, so they are reached by name, as the other
    /// tests do.
    /// </summary>
    public sealed class HolsterTests : InputTestFixture
    {
        Keyboard keyboard;
        Mouse mouse;
        Transform player;
        Animator rig;
        MonoBehaviour weapons, holster;

        [TearDown]
        public void RestoreTime() => Time.captureDeltaTime = 0f;

        [UnityTest]
        public IEnumerator EveryGunHolstersOnItsCategorySpot()
        {
            yield return StartAtGunRange();
            var offers = Offers();
            float feet = player.position.y - 1f;
            foreach (var settings in offers.Where(o => o))
            {
                yield return Equip(settings.name);
                yield return Swap();
                string category = Field(settings, "category").ToString();
                bool hip = (category == "Pistols" || category == "SMGs") && !(bool)Field(settings, "holsterOnBack");
                Transform bone = rig.GetBoneTransform(hip ? HumanBodyBones.Hips : HumanBodyBones.Chest);
                var model = Model("HolsteredModel");
                Assert.IsNotNull(model, $"{settings.name}: H should holster the gun in hand");
                Assert.IsFalse(Get<bool>(weapons, "HasWeapon"), $"{settings.name}: a holstered gun should leave the hands empty");
                Assert.AreEqual(bone, model.transform.parent, $"{settings.name}: {category} should ride on the {(hip ? "hip" : "back")}");
                Assert.That(model.GetComponentsInChildren<Renderer>().Min(r => r.bounds.min.y) - feet, Is.GreaterThan(0.2f),
                    $"{settings.name}: the holstered gun hangs too low (meters above the feet)");
                if ((bool)Field(settings, "dualWield"))
                {
                    var left = Model("HolsteredOffhandModel");
                    Assert.AreEqual(bone, left.transform.parent, $"{settings.name}: the second gun should ride on the hip too");
                    Assert.That(left.transform.localPosition.x, Is.LessThan(0f), $"{settings.name}: the second gun belongs on the left hip");
                }
                yield return Swap(); // draw it again, so the next equip replaces it instead of keeping two guns
            }
        }

        // Owner change: with a gun holstered the hands can carry another, and H swaps the two.
        [UnityTest]
        public IEnumerator HSwapsTheGunInHandWithTheHolsteredOne()
        {
            yield return StartAtGunRange();
            yield return Equip("AssaultRifle");
            var rifle = Offers().Single(o => o && o.name == "AssaultRifle");
            int rifleAmmo = Get<int>(weapons, "MagazineAmmo");
            yield return Swap();
            yield return Equip("Sidearm");
            var pistol = Offers().Single(o => o && o.name == "Sidearm");
            int pistolAmmo = Get<int>(weapons, "MagazineAmmo");
            Assert.AreEqual(rifle, Get<Object>(weapons, "HolsteredWeapon"), "Equipping into empty hands should keep the holstered rifle");

            yield return Swap();
            Assert.AreEqual(rifle, Get<Object>(weapons, "CurrentWeapon"), "H should draw the rifle");
            Assert.AreEqual(pistol, Get<Object>(weapons, "HolsteredWeapon"), "H should holster the pistol");
            Assert.AreEqual(rifleAmmo, Get<int>(weapons, "MagazineAmmo"), "The rifle keeps its own magazine");
            Assert.AreEqual(rig.GetBoneTransform(HumanBodyBones.Hips), Model("HolsteredModel").transform.parent, "The holstered pistol rides on the hip");
            Assert.AreEqual(rig.GetBoneTransform(HumanBodyBones.RightHand), Model("HeldModel").transform.parent, "The rifle is in the right hand");

            yield return Swap();
            Assert.AreEqual(pistol, Get<Object>(weapons, "CurrentWeapon"), "H again swaps back to the pistol");
            Assert.AreEqual(pistolAmmo, Get<int>(weapons, "MagazineAmmo"), "The pistol keeps its own magazine");

            // Q drops only the gun in the hands; the holstered rifle stays.
            yield return Tap(Key.Q);
            Assert.IsFalse(Get<bool>(weapons, "HasWeapon"), "Q should drop the pistol in hand");
            Assert.AreEqual(rifle, Get<Object>(weapons, "HolsteredWeapon"), "Q should leave the holstered rifle");
        }

        [UnityTest]
        public IEnumerator HolsteredPunchesKeepsAmmoAndRightMouseDraws()
        {
            yield return StartAtGunRange();
            yield return Equip("AssaultRifle");
            int ammo = Get<int>(weapons, "MagazineAmmo");
            yield return Swap();
            Assert.IsTrue(Get<Object>(weapons, "HolsteredWeapon") && !Get<bool>(weapons, "HasWeapon"), "H should holster the gun");

            // Left click while holstered is an unarmed attack.
            var body = player.GetComponents<MonoBehaviour>().Single(b => b.GetType().Name == "MaleWalkRetarget");
            yield return Click(MouseButton.Left);
            Assert.IsTrue(Get<bool>(body, "IsAttacking"), "Left click while holstered should punch");
            for (int i = 0; i < 60; i++) yield return null;

            // Q leaves a holstered gun alone (owner's choice).
            yield return Tap(Key.Q);
            Assert.IsTrue(Get<Object>(weapons, "HolsteredWeapon"), "Q should not drop a holstered gun");

            // Right mouse draws straight into aim.
            SetMouse(MouseButton.Right, true);
            // The draw takes Draw Time (0.5 s = 30 frames); aiming starts once it ends.
            for (int i = 0; i < 50; i++) yield return null;
            Assert.IsTrue(Get<bool>(weapons, "HasWeapon"), "Right mouse should draw the holstered gun");
            Assert.IsTrue(Get<bool>(weapons, "IsAiming"), "Holding right mouse should aim once drawn");
            SetMouse(MouseButton.Right, false);
            Assert.AreEqual(ammo, Get<int>(weapons, "MagazineAmmo"), "Holstering should keep the magazine");
        }

        // FEEL_SPEC > Holstering: draw and holster each take 0.5 s (±1 frame) at all frame rates; the hand is within
        // 5 cm of where it takes hold of the gun when it changes hands; the gun never jumps: at 144 fps no single frame
        // moves it more than 15 cm relative to the body (a teleport onto the body would be a step of about half a meter).
        const float MotionTime = 0.5f, HoldDistance = 0.05f, MaxStepAt144 = 0.15f;

        [UnityTest]
        public IEnumerator DrawAndHolsterReachTheGunAndTakeTheirTime([Values("AssaultRifle", "Sidearm")] string gun, [Values(30, 60, 144)] int fps)
        {
            yield return StartAtGunRange(fps);
            yield return Equip(gun);
            Transform hand = rig.GetBoneTransform(HumanBodyBones.RightHand);
            var gunSettings = Offers().Single(o => o && o.name == gun);
            foreach (bool holstering in new[] { true, false })
            {
                var model = Model("HeldModel") ?? Model("HolsteredModel");
                Vector3 last = player.InverseTransformPoint(model.transform.position);
                float fastest = 0f, handGap = -1f, time = 0f;
                bool wasHeld = Get<bool>(weapons, "HasWeapon");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.H));
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                for (int i = 0; i < fps * 2 && Get<bool>(weapons, "IsSwitchingWeapon"); i++)
                {
                    time += 1f / fps;
                    yield return null;
                    Vector3 now = player.InverseTransformPoint(model.transform.position);
                    fastest = Mathf.Max(fastest, Vector3.Distance(now, last) * fps);
                    last = now;
                    bool held = Get<bool>(weapons, "HasWeapon");
                    // The gun changes hands on this frame: measure the hand against where it takes hold of the gun.
                    if (held != wasHeld && handGap < 0f) handGap = Vector3.Distance(hand.position, HoldPoint(gunSettings));
                    wasHeld = held;
                }
                string what = $"{gun} {(holstering ? "holster" : "draw")} at {fps} fps";
                Assert.That(time, Is.EqualTo(MotionTime).Within(1f / fps + 0.0001f), $"{what}: time (s)");
                Assert.That(handGap, Is.InRange(0f, HoldDistance), $"{what}: hand distance from the gun's hold point when it changes hands (m)");
                if (fps == 144) Assert.That(fastest / fps, Is.LessThan(MaxStepAt144), $"{what}: largest one-frame gun step relative to the body (m)");
                Assert.AreEqual(!holstering, Get<bool>(weapons, "HasWeapon"), $"{what}: should end with the gun {(holstering ? "holstered" : "in hand")}");
                for (int i = 0; i < fps / 4; i++) yield return null;
            }
        }

        // Where the hand takes hold of the gun on its holster spot (on the back, partway along it; on the hip, the grip).
        Vector3 HoldPoint(Object settings) => (Vector3)holster.GetType().GetMethod("HandHoldPoint").Invoke(holster, new object[] { settings });

        // H, then wait for the draw, holster or swap to finish.
        IEnumerator Swap()
        {
            yield return Tap(Key.H);
            for (int i = 0; i < 240 && Get<bool>(weapons, "IsSwitchingWeapon"); i++) yield return null;
            yield return null;
        }

        // Events are only queued: the frame's own input update delivers them, so "pressed this frame" holds during that frame.
        IEnumerator Tap(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            yield return null;
        }

        IEnumerator Click(MouseButton button)
        {
            SetMouse(button, true);
            yield return null;
            SetMouse(button, false);
            yield return null;
            yield return null;
        }

        // Sets the whole mouse state at once: releasing single controls on test devices does not always take effect.
        void SetMouse(MouseButton button, bool down) => InputSystem.QueueStateEvent(mouse,
            new MouseState { position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f) }.WithButton(button, down));

        System.Collections.Generic.List<ScriptableObject> Offers() =>
            ((IEnumerable)weapons.GetType().GetProperty("WeaponOffers").GetValue(weapons)).Cast<ScriptableObject>().ToList();

        IEnumerator Equip(string gun)
        {
            int index = Offers().FindIndex(o => o && o.name == gun);
            Assert.IsTrue((bool)weapons.GetType().GetMethod("EquipFromDebug").Invoke(weapons, new object[] { index }), $"Could not equip {gun}.");
            yield return null; // a gun held before is destroyed at the end of the equip frame
        }

        GameObject Model(string property) => (GameObject)weapons.GetType()
            .GetProperty(property, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(weapons);

        static object Field(Object settings, string name) => settings.GetType().GetField(name).GetValue(settings);
        static T Get<T>(Object target, string property) => (T)target.GetType().GetProperty(property).GetValue(target);

        // Runs inside each test, after InputTestFixture.Setup has given the test its own clean input system.
        IEnumerator StartAtGunRange(int fps = 60)
        {
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return null;
            // The debug menu starts open and takes control away from the player; switch it off for the test.
            foreach (var behaviour in Object.FindObjectsByType<MonoBehaviour>().Where(b => b.GetType().Name == "ViewModeDebugMenu"))
                behaviour.enabled = false;
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            player = PlayerPresence.Current;
            Assert.IsNotNull(player, "The Main scene has no active player.");
            rig = player.GetComponentsInChildren<Animator>().First(a => a.isHuman);
            weapons = player.GetComponents<MonoBehaviour>().Single(b => b.GetType().Name == "WeaponCombatController");
            holster = player.GetComponents<MonoBehaviour>().SingleOrDefault(b => b.GetType().Name == "WeaponHolster");
            Assert.IsNotNull(holster, "The Player has no WeaponHolster.");
            Time.captureDeltaTime = 1f / fps;
            TestStartMarker.Find("GR-Firing").TeleportPlayer();
            for (int i = 0; i < fps / 2; i++) yield return null;
        }
    }
}
