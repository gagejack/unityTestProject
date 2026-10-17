using UnityEngine;
using UnityEngine.Animations;

namespace StarterGame
{
    /// <summary>
    /// Where the gun wants the hands this frame, filled by <c>WeaponCombatController.PrepareGunHands</c> before the
    /// graph is evaluated. Offsets marked "hand frame" are relative to the hand bone's position and rotation, so they
    /// stay valid wherever the animation puts the hand.
    /// </summary>
    public struct GunHands
    {
        public bool active;
        // 0-1: how far the right hand is raised to aim (0 while running, reloading or in a special move).
        public float aim;
        // 0-1: how far the hands turn the barrels onto the aim point without raising them (fixed view hip fire).
        public float point;
        // The crosshair's world point, moved by the expected grounding shift so it lines up after grounding.
        public Vector3 aimPoint;
        // World offset added to the animated right-hand position when aiming (already scaled by aim).
        public Vector3 aimHandOffset;
        public Quaternion aimedGrip;
        // Muzzle point and barrel direction, hand frame (grip without the recoil tilt).
        public Vector3 muzzle, barrel;
        // 0-1: how firmly the left hand holds the gun's support grip.
        public float support;
        // Support grip point and the left hand's rotation there, hand frame (gun with recoil tilt).
        public Vector3 supportPoint;
        public Quaternion supportRotation;
        // Dual handguns: the left hand aims its own gun instead of holding the right one.
        public bool dual;
        public Vector3 leftAimHandOffset;
        public Quaternion leftAimedGrip;
        public Vector3 leftMuzzle, leftBarrel;
    }

    /// <summary>
    /// Where the hands reach during a draw or holster (filled by <c>WeaponHolster.PrepareReach</c>): the hand is pulled
    /// toward its point by its weight (0 = the animated hand, 1 = exactly on the point). World space.
    /// </summary>
    public struct HandReach
    {
        public float right, left;
        public Vector3 rightPoint, leftPoint;
        public bool Active => right > 0f || left > 0f;
    }

    /// <summary>
    /// The pose adjustments made inside the animation graph, after the clips are mixed and before the bones are
    /// written. First the gun hands are placed with Humanoid IK (right hand aims, left hand holds the support grip or
    /// aims a second handgun, and both reach for the holster while drawing or holstering); then the running lean,
    /// running-arm tuck and chest aim pitch rotate the bones. The
    /// hand goals are given in the pose before those rotations, so the hands end up on target after them.
    /// Values are set by <see cref="MaleWalkRetarget"/> each frame before the graph is evaluated.
    /// </summary>
    public struct BodyPoseJob : IAnimationJob
    {
        public TransformStreamHandle spine, chest, leftUpperArm, leftLowerArm, leftHand, rightUpperArm, rightLowerArm, rightHand;
        // The character's facing in world space; "back" and "across the body" are measured from it.
        public Quaternion body;
        public float leanDegrees;
        public float armWeight;
        public float armDownAngle;
        public float aimPitch;
        public GunHands gun;
        public HandReach reach;

        public void ProcessRootMotion(AnimationStream stream) { }

        public void ProcessAnimation(AnimationStream stream)
        {
            Vector3 right = body * Vector3.right;
            // Lean: spine and chest take half each, for a gentle curve. Aim pitch turns the chest on top.
            Quaternion lean = Quaternion.AngleAxis(-0.5f * leanDegrees, right);
            Quaternion pitch = Quaternion.AngleAxis(aimPitch, right);
            if ((gun.active || reach.Active) && stream.isHumanStream) PlaceGunHands(stream, lean, pitch * lean);
            if (leanDegrees > 0f)
            {
                spine.SetRotation(stream, lean * spine.GetRotation(stream));
                chest.SetRotation(stream, lean * chest.GetRotation(stream));
            }
            if (armWeight > 0f)
            {
                TuckArm(stream, leftUpperArm, leftLowerArm, -1f);
                TuckArm(stream, rightUpperArm, rightLowerArm, 1f);
            }
            if (aimPitch != 0f) chest.SetRotation(stream, pitch * chest.GetRotation(stream));
        }

        // Changes only the front-view spread of the upper arm, keeping its length and the clip's forward/back swing.
        private void TuckArm(AnimationStream stream, TransformStreamHandle upper, TransformStreamHandle lower, float side)
        {
            Vector3 current = lower.GetPosition(stream) - upper.GetPosition(stream);
            Vector3 local = Quaternion.Inverse(body) * current;
            float acrossBody = new Vector2(local.x, local.y).magnitude;
            float angle = armDownAngle * Mathf.Deg2Rad;
            Vector3 target = new Vector3(side * Mathf.Cos(angle) * acrossBody, -Mathf.Sin(angle) * acrossBody, local.z);
            Quaternion rotation = upper.GetRotation(stream);
            Quaternion corrected = Quaternion.FromToRotation(current, body * target) * rotation;
            upper.SetRotation(stream, Quaternion.Slerp(rotation, corrected, armWeight));
        }

        // Works out where the hands must end up (after the lean and aim pitch), then gives Humanoid IK the matching
        // goals in the pose before those rotations and solves both arms.
        private void PlaceGunHands(AnimationStream stream, Quaternion spineTurn, Quaternion chestTurn)
        {
            var human = stream.AsHuman();
            // The spine and chest rotations move the arms rigidly: map points and rotations through them and back.
            Vector3 spinePivot = spine.GetPosition(stream);
            Vector3 chestPivot = spinePivot + spineTurn * (chest.GetPosition(stream) - spinePivot);
            Quaternion turn = chestTurn * spineTurn;
            Vector3 Forward(Vector3 p) => chestPivot + chestTurn * (spinePivot + spineTurn * (p - spinePivot) - chestPivot);
            Vector3 Back(Vector3 p) => spinePivot + Quaternion.Inverse(spineTurn) * (chestPivot + Quaternion.Inverse(chestTurn) * (p - chestPivot) - spinePivot);

            // IK goal rotations differ from the hand bones' rotations by a fixed offset; measure it on the clip pose.
            Quaternion rightOffset = Quaternion.Inverse(human.GetGoalRotationFromPose(AvatarIKGoal.RightHand)) * rightHand.GetRotation(stream);
            Quaternion leftOffset = Quaternion.Inverse(human.GetGoalRotationFromPose(AvatarIKGoal.LeftHand)) * leftHand.GetRotation(stream);
            Vector3 rightPosition = Forward(rightHand.GetPosition(stream));
            Quaternion rightRotation = turn * rightHand.GetRotation(stream);
            Vector3 leftPosition = Forward(leftHand.GetPosition(stream));
            Quaternion leftRotation = turn * leftHand.GetRotation(stream);
            bool rightGoal = false, leftGoal = false;

            float aim = gun.aim;
            // Raising the gun (aim) always points it too; pointing alone only turns the hand where the clip holds it.
            float point = Mathf.Max(aim, gun.point);
            if (point > 0f)
            {
                // Raise the right hand by the aim offset and point the gun at the crosshair, then turn the hand
                // until the barrel, measured from the muzzle, lines up with the crosshair point.
                rightPosition += gun.aimHandOffset;
                Quaternion animated = Quaternion.Slerp(rightRotation,
                    SafeAimRotation((gun.aimPoint - rightPosition).normalized) * Quaternion.Inverse(gun.aimedGrip), aim);
                rightRotation = Quaternion.Slerp(animated, ConvergeOnAimPoint(rightPosition, animated, gun.muzzle, gun.barrel), point);
                rightGoal = true;
            }
            if (gun.dual)
            {
                if (point > 0f)
                {
                    leftPosition += gun.leftAimHandOffset;
                    Quaternion hand = Quaternion.Slerp(leftRotation,
                        SafeAimRotation((gun.aimPoint - leftPosition).normalized) * Quaternion.Inverse(gun.leftAimedGrip), aim);
                    for (int i = 0; i < 2; i++)
                        hand = Quaternion.Slerp(Quaternion.identity,
                            Quaternion.FromToRotation(hand * gun.leftBarrel, gun.aimPoint - (leftPosition + hand * gun.leftMuzzle)), point) * hand;
                    leftRotation = hand;
                    leftGoal = true;
                }
            }
            else if (gun.support > 0f)
            {
                // The support grip is on the gun, which the right hand carries.
                leftPosition = Vector3.Lerp(leftPosition, rightPosition + rightRotation * gun.supportPoint, gun.support);
                leftRotation = Quaternion.Slerp(leftRotation, rightRotation * gun.supportRotation, gun.support);
                leftGoal = true;
            }
            // Drawing or holstering: the hands reach for the holster, keeping the animated hand rotation.
            if (reach.right > 0f)
            {
                rightPosition = Vector3.Lerp(rightPosition, reach.rightPoint, reach.right);
                rightGoal = true;
            }
            if (reach.left > 0f)
            {
                leftPosition = Vector3.Lerp(leftPosition, reach.leftPoint, reach.left);
                leftGoal = true;
            }
            if (!rightGoal && !leftGoal) return;
            // Both hands get a goal every time: one left unset keeps whatever goal it had in an earlier frame.
            Quaternion untilt = Quaternion.Inverse(turn);
            SetGoal(human, AvatarIKGoal.RightHand, Back(rightPosition), untilt * rightRotation * Quaternion.Inverse(rightOffset), rightGoal);
            SetGoal(human, AvatarIKGoal.LeftHand, Back(leftPosition), untilt * leftRotation * Quaternion.Inverse(leftOffset), leftGoal);
            human.SolveIK();
        }

        private static void SetGoal(AnimationHumanStream human, AvatarIKGoal goal, Vector3 position, Quaternion rotation, bool on)
        {
            human.SetGoalPosition(goal, position);
            human.SetGoalRotation(goal, rotation);
            human.SetGoalWeightPosition(goal, on ? 1f : 0f);
            human.SetGoalWeightRotation(goal, on ? 1f : 0f);
        }

        // Converges at the crosshair's actual target, including the shoulder camera's horizontal offset and the
        // muzzle's offset above the grip.
        private Quaternion ConvergeOnAimPoint(Vector3 position, Quaternion rotation, Vector3 muzzle, Vector3 barrel)
        {
            for (int i = 0; i < 8; i++)
            {
                Vector3 direction = gun.aimPoint - (position + rotation * muzzle);
                Vector3 forward = rotation * barrel;
                if (Vector3.Angle(forward, direction) < 0.02f) break;
                rotation = Quaternion.FromToRotation(forward, direction) * rotation;
            }
            return rotation;
        }

        private Quaternion SafeAimRotation(Vector3 direction)
        {
            // Keep a stable roll even when the camera looks straight up.
            Vector3 up = Vector3.ProjectOnPlane(body * Vector3.up, direction);
            if (up.sqrMagnitude < 0.001f) up = Vector3.Cross(direction, body * Vector3.right);
            return Quaternion.LookRotation(direction, up);
        }
    }
}
