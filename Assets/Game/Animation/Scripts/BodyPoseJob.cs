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
    /// The pose adjustments made inside the animation graph, after the clips are mixed and before the bones are
    /// written, in this order: running lean, running-arm tuck, chest aim pitch, then the gun hands (right arm aims,
    /// left arm holds the support grip or aims a second handgun). Values are set by <see cref="MaleWalkRetarget"/>
    /// each frame before the graph is evaluated.
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

        public void ProcessRootMotion(AnimationStream stream) { }

        public void ProcessAnimation(AnimationStream stream)
        {
            Vector3 right = body * Vector3.right;
            if (leanDegrees > 0f)
            {
                // Spine and chest take half each, for a gentle curve.
                var back = Quaternion.AngleAxis(-0.5f * leanDegrees, right);
                spine.SetRotation(stream, back * spine.GetRotation(stream));
                chest.SetRotation(stream, back * chest.GetRotation(stream));
            }
            if (armWeight > 0f)
            {
                TuckArm(stream, leftUpperArm, leftLowerArm, -1f);
                TuckArm(stream, rightUpperArm, rightLowerArm, 1f);
            }
            if (aimPitch != 0f)
                chest.SetRotation(stream, Quaternion.AngleAxis(aimPitch, right) * chest.GetRotation(stream));
            if (gun.active) PoseGunHands(stream);
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

        private void PoseGunHands(AnimationStream stream)
        {
            float aim = gun.aim;
            if (aim > 0f)
            {
                // Raise the right hand by the aim offset and point the gun at the crosshair, then turn the hand
                // until the barrel, measured from the muzzle, lines up with the crosshair point.
                Vector3 target = rightHand.GetPosition(stream) + gun.aimHandOffset;
                Quaternion gunRotation = SafeAimRotation((gun.aimPoint - target).normalized);
                Quaternion handRotation = gunRotation * Quaternion.Inverse(gun.aimedGrip);
                SolveArm(stream, rightUpperArm, rightLowerArm, rightHand, target,
                    Quaternion.Slerp(rightHand.GetRotation(stream), handRotation, aim));
                ConvergeOnAimPoint(stream, rightHand, gun.muzzle, gun.barrel, aim);
            }
            if (gun.dual)
            {
                if (aim <= 0f) return;
                Vector3 target = leftHand.GetPosition(stream) + gun.leftAimHandOffset;
                Quaternion rotation = SafeAimRotation((gun.aimPoint - target).normalized) * Quaternion.Inverse(gun.leftAimedGrip);
                SolveArm(stream, leftUpperArm, leftLowerArm, leftHand, target, Quaternion.Slerp(leftHand.GetRotation(stream), rotation, aim));
                Vector3 position = leftHand.GetPosition(stream);
                Quaternion hand = leftHand.GetRotation(stream);
                for (int i = 0; i < 2; i++)
                    hand = Quaternion.Slerp(Quaternion.identity,
                        Quaternion.FromToRotation(hand * gun.leftBarrel, gun.aimPoint - (position + hand * gun.leftMuzzle)), aim) * hand;
                leftHand.SetRotation(stream, hand);
                return;
            }
            if (gun.support <= 0f) return;
            Vector3 handPosition = rightHand.GetPosition(stream);
            Quaternion gripHand = rightHand.GetRotation(stream);
            Vector3 supportTarget = Vector3.Lerp(leftHand.GetPosition(stream), handPosition + gripHand * gun.supportPoint, gun.support);
            Quaternion supportRotation = Quaternion.Slerp(leftHand.GetRotation(stream), gripHand * gun.supportRotation, gun.support);
            SolveArm(stream, leftUpperArm, leftLowerArm, leftHand, supportTarget, supportRotation);
        }

        // Converges at the crosshair's actual target, including the shoulder camera's horizontal offset and the
        // muzzle's offset above the grip.
        private void ConvergeOnAimPoint(AnimationStream stream, TransformStreamHandle hand, Vector3 muzzle, Vector3 barrel, float weight)
        {
            Vector3 position = hand.GetPosition(stream);
            Quaternion animated = hand.GetRotation(stream), rotation = animated;
            for (int i = 0; i < 8; i++)
            {
                Vector3 direction = gun.aimPoint - (position + rotation * muzzle);
                Vector3 forward = rotation * barrel;
                if (Vector3.Angle(forward, direction) < 0.02f) break;
                rotation = Quaternion.FromToRotation(forward, direction) * rotation;
            }
            hand.SetRotation(stream, Quaternion.Slerp(animated, rotation, weight));
        }

        private Quaternion SafeAimRotation(Vector3 direction)
        {
            // Keep a stable roll even when the camera looks straight up.
            Vector3 up = Vector3.ProjectOnPlane(body * Vector3.up, direction);
            if (up.sqrMagnitude < 0.001f) up = Vector3.Cross(direction, body * Vector3.right);
            return Quaternion.LookRotation(direction, up);
        }

        // Two-bone arm solve: the hand reaches the target (or as close as the arm allows), the elbow stays on the
        // side it already bends toward, and the hand takes the given rotation.
        private void SolveArm(AnimationStream stream, TransformStreamHandle upper, TransformStreamHandle lower,
            TransformStreamHandle hand, Vector3 target, Quaternion rotation)
        {
            Vector3 shoulder = upper.GetPosition(stream), elbowNow = lower.GetPosition(stream);
            float a = Vector3.Distance(shoulder, elbowNow);
            float b = Vector3.Distance(elbowNow, hand.GetPosition(stream));
            if (a < 0.001f || b < 0.001f) return;
            Vector3 toward = target - shoulder;
            if (toward.sqrMagnitude < 0.000001f) return;
            float length = Mathf.Clamp(toward.magnitude, Mathf.Abs(a - b) + 0.001f, a + b - 0.001f);
            Vector3 axis = toward.normalized;
            Vector3 pole = Vector3.ProjectOnPlane(elbowNow - shoulder, axis).normalized;
            if (pole.sqrMagnitude < 0.01f) pole = Vector3.ProjectOnPlane(body * Vector3.down, axis).normalized;
            if (pole.sqrMagnitude < 0.01f) pole = Vector3.ProjectOnPlane(body * Vector3.right, axis).normalized;
            float along = (a * a - b * b + length * length) / (2f * length);
            Vector3 elbow = shoulder + axis * along + pole * Mathf.Sqrt(Mathf.Max(0f, a * a - along * along));
            upper.SetRotation(stream, Quaternion.FromToRotation(elbowNow - shoulder, elbow - shoulder) * upper.GetRotation(stream));
            Vector3 reachable = shoulder + axis * length;
            Vector3 lowerPosition = lower.GetPosition(stream);
            lower.SetRotation(stream, Quaternion.FromToRotation(hand.GetPosition(stream) - lowerPosition, reachable - lowerPosition)
                * lower.GetRotation(stream));
            hand.SetRotation(stream, rotation);
        }
    }
}
