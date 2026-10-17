using UnityEngine;
using UnityEngine.Animations;

namespace StarterGame
{
    /// <summary>
    /// The pose adjustments made inside the animation graph, after the clips are mixed and before the
    /// bones are written: running lean, then the running-arm tuck, then the chest aim pitch. Weights and
    /// angles are set by <see cref="MaleWalkRetarget"/> each frame before the graph is evaluated.
    /// </summary>
    public struct BodyPoseJob : IAnimationJob
    {
        public TransformStreamHandle spine, chest, leftUpperArm, leftLowerArm, rightUpperArm, rightLowerArm;
        // The character's facing in world space; "back" and "across the body" are measured from it.
        public Quaternion body;
        public float leanDegrees;
        public float armWeight;
        public float armDownAngle;
        public float aimPitch;

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
    }
}
