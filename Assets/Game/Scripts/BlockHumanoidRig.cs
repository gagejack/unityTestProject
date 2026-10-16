using System;
using UnityEngine;

namespace StarterGame
{
    /// <summary>Follows the Humanoid bones while leaving each block's Transform scale user-controlled.</summary>
    [DefaultExecutionOrder(160)]
    public sealed class BlockHumanoidRig : MonoBehaviour
    {
        [Serializable] public struct Attachment
        {
            public Transform block;
            public Transform bone;
            public Vector3 position;
            public Quaternion rotation;
        }
        [Serializable] public struct RestBone
        {
            public Transform bone;
            public Vector3 position;
            public Quaternion rotation;
        }
        [SerializeField] private Animator animator;
        [SerializeField] private Attachment[] attachments;
        [SerializeField] private RestBone[] restBones;
        [Header("Crouch pose")]
        [SerializeField, Range(60f, 100f)] private float crouchForwardBend = 85f;
        [SerializeField, Range(5f, 30f)] private float crouchStepAngle = 18f;
        [SerializeField, Min(0.1f)] private float crouchStrideLength = 1.15f;
        [Header("Pose transitions")]
        [SerializeField, Min(0.01f)] private float specialPoseBlendTime = 0.16f;
        private Vector3[] lastPosePositions;
        private Quaternion[] lastPoseRotations;
        private Vector3[] transitionPositions;
        private Quaternion[] transitionRotations;
        private float transitionElapsed;
        private bool transitioningPose;
        private float crouchPhase;
        private float crouchWalkBlend;
        private enum SpecialPose { Crouch, Roll, Dive }
        private SpecialPose specialPose;
        private bool slidePose;
        private bool backwardDivePose;
        private Attachment headAttachment;
        private Attachment chestAttachment;
        private CapsuleMovement movement;
        private WeaponCombatController weapons;
        private MaleWalkRetarget fightingAnimation;
        private Vector3[] positions;
        private Quaternion[] rotations;
        private bool initialized;
        private float specialBlend;
        private Vector3[] animationPositions;
        private Quaternion[] animationRotations;
        private CharacterController controller;
        private Renderer[] blockRenderers;
        public bool UsingDownloadedAnimation => initialized && isActiveAndEnabled;

        private void Awake()
        {
            movement = GetComponent<CapsuleMovement>();
            weapons = GetComponent<WeaponCombatController>();
            fightingAnimation = GetComponent<MaleWalkRetarget>();
            positions = new Vector3[attachments.Length];
            rotations = new Quaternion[attachments.Length];
            for (int i = 0; i < attachments.Length; i++)
            {
                positions[i] = attachments[i].block.localPosition;
                rotations[i] = attachments[i].block.localRotation;
                if (attachments[i].bone == animator.GetBoneTransform(HumanBodyBones.Head)) headAttachment = attachments[i];
                if (attachments[i].bone == animator.GetBoneTransform(HumanBodyBones.Chest)) chestAttachment = attachments[i];
            }
            animationPositions = new Vector3[restBones.Length];
            animationRotations = new Quaternion[restBones.Length];
            lastPosePositions = new Vector3[restBones.Length];
            lastPoseRotations = new Quaternion[restBones.Length];
            transitionPositions = new Vector3[restBones.Length];
            transitionRotations = new Quaternion[restBones.Length];
            controller = GetComponent<CharacterController>();
            blockRenderers = new Renderer[attachments.Length];
            for (int i = 0; i < attachments.Length; i++) blockRenderers[i] = attachments[i].block.GetComponent<Renderer>();
            initialized = true;
        }

        private void RestoreBody()
        {
            for (int i = 0; i < attachments.Length; i++)
            {
                var block = attachments[i].block;
                if (!block) continue;
                block.localPosition = positions[i];
                block.localRotation = rotations[i];
            }
        }

        private void LateUpdate()
        {
            if (!initialized || !animator) return;
            PoseSpecialMoves();
            CopyAttachments();
            GroundBody();
            if (fightingAnimation)
            {
                fightingAnimation.ApplyPunchContact(Time.deltaTime);
                CopyAttachments();
            }
            if (weapons && weapons.HasWeapon)
            {
                bool special = movement && (movement.IsRolling || movement.IsDiving || movement.IsSliding || movement.IsCrouching);
                // Grounding moves the skeleton. Aim and solve both arms from that
                // final position, then copy the solved hands onto the visible blocks.
                weapons.ApplyAnimatedWeaponPose(Time.deltaTime, !special);
                CopyAttachments();
            }
        }

        private void CopyAttachments()
        {
            foreach (var item in attachments)
            {
                item.block.SetPositionAndRotation(item.bone.TransformPoint(item.position), item.bone.rotation * item.rotation);
            }
        }

        private void GroundBody()
        {
            if (!controller.isGrounded) return;
            float lowest = float.PositiveInfinity;
            foreach (var renderer in blockRenderers)
                if (renderer) lowest = Mathf.Min(lowest, renderer.bounds.min.y);
            if (float.IsInfinity(lowest)) return;
            float floor = controller.bounds.min.y - controller.skinWidth;
            Vector3 shift = Vector3.up * (floor + 0.005f - lowest);
            animator.transform.position += shift;
            foreach (var item in attachments) item.block.position += shift;
        }

        private void PoseSpecialMoves()
        {
            SpecialPose previousPose = specialPose;
            bool previousSlide = slidePose;
            bool hadSpecialPose = specialBlend > 0f;
            bool tuck = movement && (movement.IsCrouching || movement.IsRolling);
            bool dive = movement && (movement.IsDiving || movement.IsSliding);
            // Retain the outgoing pose during its fade instead of briefly switching to a roll tuck.
            if (dive)
            {
                specialPose = SpecialPose.Dive;
                slidePose = movement.IsSliding;
                backwardDivePose = movement.IsDivingBackward;
            }
            else if (movement && movement.IsRolling) specialPose = SpecialPose.Roll;
            else if (movement && movement.IsCrouching) specialPose = SpecialPose.Crouch;
            if (hadSpecialPose && (tuck || dive) &&
                (specialPose != previousPose || (dive && slidePose != previousSlide)))
            {
                Array.Copy(lastPosePositions, transitionPositions, restBones.Length);
                Array.Copy(lastPoseRotations, transitionRotations, restBones.Length);
                transitionElapsed = 0f;
                transitioningPose = true;
            }
            Vector3 velocity = transform.InverseTransformDirection(controller.velocity);
            float speed = new Vector2(velocity.x, velocity.z).magnitude;
            bool crouchWalking = movement && movement.IsCrouching && !movement.IsRolling && !dive && controller.isGrounded;
            crouchWalkBlend = Mathf.MoveTowards(crouchWalkBlend,
                crouchWalking ? Mathf.Clamp01(speed / 0.8f) : 0f, Time.deltaTime / 0.12f);
            if (crouchWalking && speed > 0.05f)
                crouchPhase = Mathf.Repeat(crouchPhase + speed * Time.deltaTime * Mathf.PI * 2f / crouchStrideLength, Mathf.PI * 2f);
            specialBlend = Mathf.MoveTowards(specialBlend, tuck || dive ? 1f : 0f,
                Time.deltaTime / Mathf.Max(0.01f, specialPoseBlendTime));
            if (specialBlend <= 0f)
            {
                transitioningPose = false;
                return;
            }
            // Each frame starts from the sampled animation; these poses never accumulate.

            for (int i = 0; i < restBones.Length; i++)
            {
                var rest = restBones[i];
                animationPositions[i] = rest.bone.localPosition;
                animationRotations[i] = rest.bone.localRotation;
                rest.bone.localPosition = rest.position;
                rest.bone.localRotation = rest.rotation;
            }
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (specialPose == SpecialPose.Dive)
            {
                hips.localRotation = Quaternion.Euler(backwardDivePose ? -85f : 85f, 0f, 0f);
                if (slidePose) hips.localPosition += Vector3.down * 0.52f;
                PoseDiveArm(true, hips.rotation);
                PoseDiveArm(false, hips.rotation);
            }
            else if (specialPose == SpecialPose.Crouch)
            {
                PoseCrouch(hips, velocity);
            }
            else
            {
                hips.localPosition += Vector3.down * 0.30f;
                Rotate(HumanBodyBones.LeftUpperLeg, -65, 0, 0);
                Rotate(HumanBodyBones.RightUpperLeg, -65, 0, 0);
                Rotate(HumanBodyBones.LeftLowerLeg, 110, 0, 0);
                Rotate(HumanBodyBones.RightLowerLeg, 110, 0, 0);
                Rotate(HumanBodyBones.Spine, 40, 0, 0);
                Rotate(HumanBodyBones.Chest, 25, 0, 0);
                Rotate(HumanBodyBones.Head, 20, 0, 0);
                Rotate(HumanBodyBones.LeftUpperArm, 0, -20, 65);
                Rotate(HumanBodyBones.RightUpperArm, 0, 20, -65);
                Rotate(HumanBodyBones.LeftLowerArm, 0, -135, 0);
                Rotate(HumanBodyBones.RightLowerArm, 0, 135, 0);
                if (movement && movement.IsRolling)
                {
                    // Roll around the axis across the travel direction, including strafes
                    // and diagonals, while leaving the player's camera-facing yaw intact.
                    Vector3 worldAxis = Vector3.Cross(Vector3.up, movement.RollDirection).normalized;
                    Vector3 localAxis = hips.parent ? hips.parent.InverseTransformDirection(worldAxis) : worldAxis;
                    hips.localRotation = Quaternion.AngleAxis(360f * movement.RollProgress, localAxis) * hips.localRotation;
                }
            }
            float transitionWeight = transitioningPose
                ? Mathf.SmoothStep(0f, 1f, transitionElapsed / Mathf.Max(0.01f, specialPoseBlendTime)) : 1f;
            for (int i = 0; i < restBones.Length; i++)
            {
                Transform bone = restBones[i].bone;
                bone.localPosition = Vector3.Lerp(animationPositions[i], bone.localPosition, specialBlend);
                bone.localRotation = Quaternion.Slerp(animationRotations[i], bone.localRotation, specialBlend);
                if (transitioningPose)
                {
                    bone.localPosition = Vector3.Lerp(transitionPositions[i], bone.localPosition, transitionWeight);
                    bone.localRotation = Quaternion.Slerp(transitionRotations[i], bone.localRotation, transitionWeight);
                }
                lastPosePositions[i] = bone.localPosition;
                lastPoseRotations[i] = bone.localRotation;
            }
            if (transitioningPose)
            {
                transitionElapsed += Time.deltaTime;
                if (transitionElapsed >= specialPoseBlendTime) transitioningPose = false;
            }
        }

        private void Rotate(HumanBodyBones bone, float x, float y, float z)
        {
            var target = animator.GetBoneTransform(bone);
            target.localRotation *= Quaternion.Euler(x, y, z);
        }

        private void PoseCrouch(Transform hips, Vector3 velocity)
        {
            float step = Mathf.Sin(crouchPhase) * crouchWalkBlend;
            Vector3 direction = velocity.sqrMagnitude > 0.01f ? velocity.normalized : Vector3.forward;
            hips.localPosition += new Vector3(step * 0.025f, -0.40f, -0.08f);
            hips.localRotation *= Quaternion.Euler(0f, step * 3f, step * 4f);
            // Deeply folded knees with a short alternating shuffle, driven by actual travel.
            Rotate(HumanBodyBones.LeftUpperLeg, -105f + step * crouchStepAngle * direction.z, 0f, -step * crouchStepAngle * direction.x);
            Rotate(HumanBodyBones.RightUpperLeg, -105f - step * crouchStepAngle * direction.z, 0f, step * crouchStepAngle * direction.x);
            Rotate(HumanBodyBones.LeftLowerLeg, 145f - Mathf.Max(0f, step) * 18f, 0f, 0f);
            Rotate(HumanBodyBones.RightLowerLeg, 145f - Mathf.Max(0f, -step) * 18f, 0f, 0f);
            // Fold the torso almost horizontal; counter-rotate the head so it faces forward.
            float bend = crouchForwardBend + step * 2f;
            Rotate(HumanBodyBones.Spine, bend - 15f, 0f, 0f);
            Rotate(HumanBodyBones.Chest, 15f, 0f, 0f);
            Rotate(HumanBodyBones.Head, -bend, 0f, 0f);
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            // Account for the visible head's offset above its bone when lowering it below the back.
            Vector3 chestCenter = chestAttachment.bone.TransformPoint(chestAttachment.position);
            Vector3 headCenter = chestCenter + transform.rotation * new Vector3(0f, -0.13f, 0.40f);
            head.position = headCenter - head.rotation * headAttachment.position;
            PoseCrouchArm(true, headCenter);
            PoseCrouchArm(false, headCenter);
        }

        private void PoseCrouchArm(bool left, Vector3 headCenter)
        {
            float side = left ? -1f : 1f;
            Vector3 target = headCenter + transform.rotation * new Vector3(side * 0.31f, 0.02f, 0f);
            // Elbows lead forward above the ears; forearms fold down beside the head.
            Vector3 elbowPole = transform.rotation * new Vector3(side * 0.10f, 1f, 0.35f);
            PoseArm(left, target, elbowPole);
        }

        private void PoseDiveArm(bool left, Quaternion bodyRotation)
        {
            Transform upper = animator.GetBoneTransform(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
            Transform lower = animator.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
            Transform hand = animator.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            float side = left ? -1f : 1f;
            float reach = Vector3.Distance(upper.position, lower.position) + Vector3.Distance(lower.position, hand.position);
            // Almost full extension alongside the head, with only a slight elbow bend.
            Vector3 direction = bodyRotation * new Vector3(-side * 0.15f, 1f, 0f).normalized;
            PoseArm(left, upper.position + direction * (reach * 0.985f),
                bodyRotation * new Vector3(side * 0.1f, 0f, -1f));
        }

        private void PoseArm(bool left, Vector3 target, Vector3 elbowPole)
        {
            Transform upper = animator.GetBoneTransform(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
            Transform lower = animator.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
            Transform hand = animator.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            float upperLength = Vector3.Distance(upper.position, lower.position);
            float lowerLength = Vector3.Distance(lower.position, hand.position);
            Vector3 delta = target - upper.position;
            float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(upperLength - lowerLength) + 0.001f,
                upperLength + lowerLength - 0.001f);
            Vector3 axis = delta.normalized;
            Vector3 pole = Vector3.ProjectOnPlane(elbowPole, axis).normalized;
            float along = (upperLength * upperLength - lowerLength * lowerLength + distance * distance) / (2f * distance);
            float height = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
            Vector3 elbow = upper.position + axis * along + pole * height;
            target = upper.position + axis * distance;
            upper.rotation = Quaternion.FromToRotation(lower.position - upper.position, elbow - upper.position) * upper.rotation;
            lower.rotation = Quaternion.FromToRotation(hand.position - lower.position, target - lower.position) * lower.rotation;
        }

        private void OnDisable()
        {
            if (!initialized) return;
            RestoreBody();
        }
    }
}


