using System.Collections;
using System.Linq;
using Game.Core;
using Game.TestAreas;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Animation.Tests
{
    /// <summary>
    /// Play-mode checks for the running pose targets in docs/FEEL_SPEC.md (Animation section), at 30, 60 and 144 fps.
    /// Loads the Main scene, starts from the MC-Lane marker and sprints (Shift + W), measuring the final pose each frame
    /// the same way the baseline harness does.
    /// </summary>
    public sealed class RunningPoseTests : InputTestFixture
    {
        // FEEL_SPEC > Animation: "Run torso lean" 35 and "Run arm angle (unarmed)" 38.5, the same at all frame rates.
        const float RunLean = 35f, LeanTolerance = 1f;
        const float RunArmOut = 38.5f, ArmTolerance = 1f;

        Keyboard keyboard;
        Transform player;
        Animator rig;

        [TearDown]
        public void RestoreTime() => Time.captureDeltaTime = 0f;

        [UnityTest]
        public IEnumerator RunningKeepsItsLeanAndArmAngle([Values(30, 60, 144)] int fps)
        {
            yield return StartOnLane(fps);
            Press(keyboard.leftShiftKey);
            Press(keyboard.wKey);
            // Skip the first 1.6 s (start and gait blend), like the baseline's steady window.
            for (int i = 0; i < fps * 1.6f; i++) yield return null;
            float lean = 0f, arms = 0f;
            int frames = Mathf.RoundToInt(fps * 2.4f);
            for (int i = 0; i < frames; i++)
            {
                yield return null;
                lean += TorsoLean();
                arms += (ArmOut(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, -1f) +
                         ArmOut(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, 1f)) * 0.5f;
            }
            Release(keyboard.wKey);
            Release(keyboard.leftShiftKey);
            Assert.That(lean / frames, Is.EqualTo(RunLean).Within(LeanTolerance), $"Run torso lean at {fps} fps");
            Assert.That(arms / frames, Is.EqualTo(RunArmOut).Within(ArmTolerance), $"Run arm angle at {fps} fps");
        }

        // FEEL_SPEC > Weapons: "Support hand gap, running with rifle" ≤ 5 cm.
        const float RunSupportGap = 0.05f;

        [UnityTest]
        public IEnumerator SprintingWithARifleKeepsTheSupportHandOnTheGun([Values(30, 60, 144)] int fps)
        {
            yield return StartOnLane(fps);
            // Weapons is in another assembly; equip and read it by name, as the debug menu tests do.
            var weapons = Object.FindObjectsByType<MonoBehaviour>().Single(b => b.GetType().Name == "WeaponCombatController");
            var offers = ((System.Collections.IEnumerable)weapons.GetType().GetProperty("WeaponOffers").GetValue(weapons)).Cast<Object>().ToList();
            int rifle = offers.FindIndex(o => o && o.name == "AssaultRifle");
            Assert.IsTrue((bool)weapons.GetType().GetMethod("EquipFromDebug").Invoke(weapons, new object[] { rifle }), "Could not equip AssaultRifle.");
            var settings = offers[rifle];
            var grip = (Vector3)settings.GetType().GetField("supportHandLocalPosition").GetValue(settings);
            yield return null; // a gun held before is destroyed at the end of the equip frame
            Transform gun = rig.GetBoneTransform(HumanBodyBones.RightHand).Cast<Transform>().Single(t => t.name.EndsWith("(equipped)"));
            Press(keyboard.leftShiftKey);
            Press(keyboard.wKey);
            for (int i = 0; i < fps * 1.6f; i++) yield return null;
            float worst = 0f;
            for (int i = 0; i < Mathf.RoundToInt(fps * 2.4f); i++)
            {
                yield return null;
                worst = Mathf.Max(worst, Vector3.Distance(Bone(HumanBodyBones.LeftHand), gun.TransformPoint(grip)));
            }
            Release(keyboard.wKey);
            Release(keyboard.leftShiftKey);
            Assert.That(worst, Is.LessThanOrEqualTo(RunSupportGap), $"Support hand gap while sprinting at {fps} fps");
        }

        // Side-view angle of the hips-to-neck line from vertical (degrees, + = leaning forward).
        float TorsoLean()
        {
            var v = player.InverseTransformDirection(Bone(HumanBodyBones.Neck) - Bone(HumanBodyBones.Hips));
            return Mathf.Atan2(v.z, v.y) * Mathf.Rad2Deg;
        }

        // Front-view angle of the upper arm out from hanging straight down (degrees).
        float ArmOut(HumanBodyBones upper, HumanBodyBones lower, float side)
        {
            var v = player.InverseTransformDirection(Bone(lower) - Bone(upper));
            return Mathf.Atan2(side * v.x, -v.y) * Mathf.Rad2Deg;
        }

        Vector3 Bone(HumanBodyBones bone) => rig.GetBoneTransform(bone).position;

        // Runs inside each test, after InputTestFixture.Setup has given the test its own clean input system.
        IEnumerator StartOnLane(int fps)
        {
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return null;
            // The debug menu starts open and takes control away from the player; switch it off for the test.
            foreach (var behaviour in Object.FindObjectsByType<MonoBehaviour>().Where(b => b.GetType().Name == "ViewModeDebugMenu"))
                behaviour.enabled = false;
            keyboard = InputSystem.AddDevice<Keyboard>();
            player = PlayerPresence.Current;
            Assert.IsNotNull(player, "The Main scene has no active player.");
            rig = player.GetComponentsInChildren<Animator>().FirstOrDefault(a => a.isHuman);
            Assert.IsNotNull(rig, "The player has no Humanoid Animator.");
            Time.captureDeltaTime = 1f / fps;
            var marker = TestStartMarker.Find("MC-Lane");
            Assert.IsNotNull(marker, "Start marker MC-Lane is missing from the Movement Course.");
            marker.TeleportPlayer();
            for (int i = 0; i < fps / 2; i++) yield return null;
        }
    }
}
