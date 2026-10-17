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
    /// Play-mode checks for holstering (docs/spec/Holstering/SPEC.md, checkpoint 1): every gun goes to its category's
    /// spot on the body, a holstered gun leaves the player unarmed but keeps its ammo, right mouse draws, and Q leaves a
    /// holstered gun alone. Weapons live in the default assembly, so they are reached by name, as the other tests do.
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
                yield return Tap(Key.H);
                string category = Field(settings, "category").ToString();
                bool hip = (category == "Pistols" || category == "SMGs") && !(bool)Field(settings, "holsterOnBack");
                Transform bone = rig.GetBoneTransform(hip ? HumanBodyBones.Hips : HumanBodyBones.Chest);
                var model = Model("HeldModel");
                Assert.IsFalse(Get<bool>(weapons, "HasWeapon"), $"{settings.name}: a holstered gun should leave the hands empty");
                Assert.AreEqual(bone, model.transform.parent, $"{settings.name}: {category} should ride on the {(hip ? "hip" : "back")}");
                Assert.That(model.GetComponentsInChildren<Renderer>().Min(r => r.bounds.min.y) - feet, Is.GreaterThan(0.2f),
                    $"{settings.name}: the holstered gun hangs too low (meters above the feet)");
                if ((bool)Field(settings, "dualWield"))
                {
                    var left = Model("OffhandModel");
                    Assert.AreEqual(bone, left.transform.parent, $"{settings.name}: the second gun should ride on the hip too");
                    Assert.That(left.transform.localPosition.x, Is.LessThan(0f), $"{settings.name}: the second gun belongs on the left hip");
                }
            }
        }

        [UnityTest]
        public IEnumerator HolsteredPunchesKeepsAmmoAndRightMouseDraws()
        {
            yield return StartAtGunRange();
            yield return Equip("AssaultRifle");
            int ammo = Get<int>(weapons, "MagazineAmmo");
            yield return Tap(Key.H);
            Assert.IsTrue(Get<bool>(weapons, "OwnsWeapon") && !Get<bool>(weapons, "HasWeapon"), "H should holster the gun");

            // Left click while holstered is an unarmed attack.
            var body = player.GetComponents<MonoBehaviour>().Single(b => b.GetType().Name == "MaleWalkRetarget");
            yield return Click(MouseButton.Left);
            Assert.IsTrue(Get<bool>(body, "IsAttacking"), "Left click while holstered should punch");
            for (int i = 0; i < 60; i++) yield return null;

            // Q leaves a holstered gun alone (owner's choice).
            yield return Tap(Key.Q);
            Assert.IsTrue(Get<bool>(weapons, "OwnsWeapon"), "Q should not drop a holstered gun");

            // Right mouse draws straight into aim.
            SetMouse(MouseButton.Right, true);
            for (int i = 0; i < 20; i++) yield return null;
            Assert.IsTrue(Get<bool>(weapons, "HasWeapon"), "Right mouse should draw the holstered gun");
            Assert.IsTrue(Get<bool>(weapons, "IsAiming"), "Holding right mouse should aim once drawn");
            SetMouse(MouseButton.Right, false);
            Assert.AreEqual(ammo, Get<int>(weapons, "MagazineAmmo"), "Holstering should keep the magazine");
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
        IEnumerator StartAtGunRange()
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
            Time.captureDeltaTime = 1f / 60f;
            TestStartMarker.Find("GR-Firing").TeleportPlayer();
            for (int i = 0; i < 30; i++) yield return null;
        }
    }
}
