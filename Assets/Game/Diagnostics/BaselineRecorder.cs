using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using Game.Core;
using StarterGame;
using Game.Mobs;
using Game.Movement;
using UnityEngine;

namespace Game.Diagnostics
{
    /// <summary>One frame of the baseline log, sampled after every other script has run.</summary>
    public struct FrameRow
    {
        public int frame, step, input;
        public float time, dt;
        public Vector3 position, leftFoot, rightFoot, rightHandLocal, cameraPosition;
        public float yaw, cameraYaw, cameraPitch, fov;
        public bool grounded, running, crouching, rolling, diving, sliding, armed, shot, focused;
        public float poseWeight, runPose, fightWeight, soldierWeight, aimWeight, armOutLeft, armOutRight;
        public string fight, soldierAnim;
        public int ammo, damageCount;
        public float damage, supportGap, gunAimError;
        public Vector3 mobPosition, mobLeftFoot, mobRightFoot;
        public string mobState;
        public int mobAttacks;
    }

    /// <summary>
    /// Samples the player, camera, weapon and (optionally) one mob at the very end of each frame.
    /// Runs last in the update order (see docs/ARCHITECTURE.md) so it sees the final pose.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class BaselineRecorder : MonoBehaviour
    {
        public readonly List<FrameRow> Rows = new List<FrameRow>();
        public bool Recording;
        public int CurrentStep = -1;
        public int CurrentInput;
        public GameObject Mob;

        static readonly FieldInfo HeldWeaponField = typeof(WeaponCombatController).GetField("heldWeapon", BindingFlags.NonPublic | BindingFlags.Instance);

        CapsuleMovement movement;
        MaleWalkRetarget animation;
        WeaponCombatController weapons;
        Animator rig;
        Camera view;
        float lastShotTime = float.NegativeInfinity;
        float frameDamage;
        int frameDamageCount;

        public void Bind(GameObject player)
        {
            movement = player.GetComponent<CapsuleMovement>();
            animation = player.GetComponent<MaleWalkRetarget>();
            weapons = player.GetComponent<WeaponCombatController>();
            rig = animation ? animation.RigAnimator : null;
            view = Camera.main;
        }

        private void OnEnable() => CombatEvents.Damaged += OnDamaged;
        private void OnDisable() => CombatEvents.Damaged -= OnDamaged;

        private void OnDamaged(DamageReport report)
        {
            frameDamage += report.Amount;
            frameDamageCount++;
        }

        private void LateUpdate()
        {
            if (!movement) return;
            bool shot = weapons && weapons.LastShotTime != lastShotTime && weapons.LastShotTime > lastShotTime;
            if (weapons) lastShotTime = weapons.LastShotTime;
            if (Recording) Rows.Add(Sample(shot));
            frameDamage = 0f;
            frameDamageCount = 0;
        }

        FrameRow Sample(bool shot)
        {
            var t = movement.transform;
            var row = new FrameRow
            {
                frame = Time.frameCount, step = CurrentStep, input = CurrentInput, time = Time.time, dt = Time.deltaTime,
                focused = Application.isFocused,
                position = t.position, yaw = t.eulerAngles.y,
                grounded = movement.IsGrounded, running = movement.IsRunning, crouching = movement.IsCrouching,
                rolling = movement.IsRolling, diving = movement.IsDiving, sliding = movement.IsSliding,
                poseWeight = animation.PoseWeight, runPose = animation.RunningPoseWeight, fightWeight = animation.FightingWeight,
                fight = animation.CurrentFightingMotion, soldierWeight = animation.SoldierAnimationWeight, soldierAnim = animation.CurrentSoldierAnimation,
                armed = weapons.HasWeapon, aimWeight = weapons.AimWeight, ammo = weapons.MagazineAmmo, shot = shot,
                damage = frameDamage, damageCount = frameDamageCount,
                cameraPosition = view.transform.position, cameraYaw = view.transform.eulerAngles.y,
                cameraPitch = Mathf.DeltaAngle(0f, view.transform.eulerAngles.x), fov = view.fieldOfView,
                supportGap = -1f, gunAimError = -1f, mobState = ""
            };
            if (rig)
            {
                row.leftFoot = rig.GetBoneTransform(HumanBodyBones.LeftFoot).position;
                row.rightFoot = rig.GetBoneTransform(HumanBodyBones.RightFoot).position;
                row.rightHandLocal = t.InverseTransformPoint(rig.GetBoneTransform(HumanBodyBones.RightHand).position);
                row.armOutLeft = ArmOut(t, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, -1f);
                row.armOutRight = ArmOut(t, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, 1f);
            }
            var held = weapons.HasWeapon ? HeldWeaponField?.GetValue(weapons) as GameObject : null;
            if (held && rig)
            {
                var settings = weapons.CurrentWeapon;
                if (!settings.dualWield)
                    row.supportGap = Vector3.Distance(rig.GetBoneTransform(HumanBodyBones.LeftHand).position,
                        held.transform.TransformPoint(settings.supportHandLocalPosition));
                row.gunAimError = Vector3.Angle(held.transform.forward, AimPoint(settings.range) - weapons.MuzzlePosition);
            }
            if (Mob)
            {
                row.mobPosition = Mob.transform.position;
                var brain = Mob.GetComponent<MobBrain>();
                row.mobState = brain ? brain.State.ToString() : "";
                row.mobAttacks = brain ? brain.AttacksStarted : 0;
                var mobRig = Mob.GetComponent<BlockCharacterAnimations>()?.Animator;
                if (mobRig && mobRig.isHuman)
                {
                    row.mobLeftFoot = mobRig.GetBoneTransform(HumanBodyBones.LeftFoot).position;
                    row.mobRightFoot = mobRig.GetBoneTransform(HumanBodyBones.RightFoot).position;
                }
            }
            return row;
        }

        // What the crosshair is on: the nearest hit along the camera's aim, ignoring the player.
        Vector3 AimPoint(float range)
        {
            Vector3 origin = view.transform.position, direction = weapons.AimWorldDirection;
            float best = range;
            foreach (var hit in Physics.RaycastAll(origin, direction, range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(movement.transform) && hit.distance < best) best = hit.distance;
            return origin + direction * best;
        }

        // Front-view angle of the upper arm away from the body, in degrees (0 = hanging straight down).
        float ArmOut(Transform body, HumanBodyBones upper, HumanBodyBones lower, float side)
        {
            var v = body.InverseTransformDirection(rig.GetBoneTransform(lower).position - rig.GetBoneTransform(upper).position);
            return Mathf.Atan2(side * v.x, -v.y) * Mathf.Rad2Deg;
        }

        public static string ToCsv(List<FrameRow> rows)
        {
            var c = CultureInfo.InvariantCulture;
            var sb = new StringBuilder("frame,time,dt,step,input,focused,px,py,pz,yaw,grounded,running,crouching,rolling,diving,sliding," +
                "poseWeight,runPose,fightWeight,fight,soldierWeight,soldierAnim,armed,aimWeight,ammo,shot,damage,damageCount," +
                "lfx,lfy,lfz,rfx,rfy,rfz,rhx,rhy,rhz,armOutL,armOutR,supportGap,gunAimError,camX,camY,camZ,camYaw,camPitch,fov," +
                "mobX,mobY,mobZ,mobState,mobAttacks,mlfx,mlfy,mlfz,mrfx,mrfy,mrfz\n");
            string V(Vector3 v) => v.x.ToString("0.####", c) + "," + v.y.ToString("0.####", c) + "," + v.z.ToString("0.####", c);
            string F(float f) => f.ToString("0.#####", c);
            string B(bool b) => b ? "1" : "0";
            foreach (var r in rows)
                sb.Append(r.frame).Append(',').Append(F(r.time)).Append(',').Append(F(r.dt)).Append(',').Append(r.step).Append(',').Append(r.input).Append(',').Append(B(r.focused)).Append(',')
                  .Append(V(r.position)).Append(',').Append(F(r.yaw)).Append(',').Append(B(r.grounded)).Append(',').Append(B(r.running)).Append(',')
                  .Append(B(r.crouching)).Append(',').Append(B(r.rolling)).Append(',').Append(B(r.diving)).Append(',')
                  .Append(B(r.sliding)).Append(',').Append(F(r.poseWeight)).Append(',').Append(F(r.runPose)).Append(',').Append(F(r.fightWeight)).Append(',')
                  .Append(r.fight).Append(',').Append(F(r.soldierWeight)).Append(',').Append(r.soldierAnim).Append(',').Append(B(r.armed)).Append(',')
                  .Append(F(r.aimWeight)).Append(',').Append(r.ammo).Append(',').Append(B(r.shot)).Append(',').Append(F(r.damage)).Append(',').Append(r.damageCount).Append(',')
                  .Append(V(r.leftFoot)).Append(',').Append(V(r.rightFoot)).Append(',').Append(V(r.rightHandLocal)).Append(',')
                  .Append(F(r.armOutLeft)).Append(',').Append(F(r.armOutRight)).Append(',').Append(F(r.supportGap)).Append(',').Append(F(r.gunAimError)).Append(',')
                  .Append(V(r.cameraPosition)).Append(',').Append(F(r.cameraYaw)).Append(',').Append(F(r.cameraPitch)).Append(',').Append(F(r.fov)).Append(',')
                  .Append(V(r.mobPosition)).Append(',').Append(r.mobState).Append(',').Append(r.mobAttacks).Append(',')
                  .Append(V(r.mobLeftFoot)).Append(',').Append(V(r.mobRightFoot)).Append('\n');
            return sb.ToString();
        }
    }
}
