using UnityEngine;

namespace StarterGame
{
    /// <summary>Stops an assigned fist or foot on contact, then releases it into recovery.</summary>
    [DisallowMultipleComponent]
    public sealed class StraightPunchContact : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float fistRadius = 0.10f;
        [SerializeField, Min(0.01f)] private float footRadius = 0.12f;
        [SerializeField, Min(0f)] private float surfaceGap = 0.012f;
        [SerializeField, Range(0f, 0.1f)] private float impactPause = 0.035f;
        [SerializeField, Min(0.01f)] private float releaseBlendTime = 0.08f;
        [SerializeField, Range(0f, 0.04f)] private float compression = 0.015f;
        private MaleWalkRetarget animation;
        private WeaponCombatController weapons;
        private CapsuleMovement movement;
        private MaleWalkRetarget.AttackTiming timing;
        private Transform upper, lower, tip;
        private Vector3 previousTip;
        private bool isKick;
        private Collider contactCollider;
        private Vector3 localPoint, localNormal;
        private bool contacted;
        private float pauseRemaining, contactAge, releaseAge;
        public bool HandlesCurrentStrike => isActiveAndEnabled && timing != null && tip;
        public bool HasContact => contacted;
        public Collider ContactCollider => contactCollider;
        public float ConstraintWeight { get; private set; }
        public float FistRadius => fistRadius;
        public float ContactRadius => isKick ? footRadius : fistRadius;

        public void BeginStrike(MaleWalkRetarget.AttackTiming attack)
        {
            CancelStrike();
            if (!isActiveAndEnabled || attack == null) return;
            isKick = attack.kick && attack.contactFoot != MaleWalkRetarget.AttackTiming.ContactFoot.None;
            if (!isKick && attack.contactHand == MaleWalkRetarget.AttackTiming.ContactHand.None) return;
            if (!animation) animation = GetComponent<MaleWalkRetarget>();
            if (!weapons) weapons = GetComponent<WeaponCombatController>();
            if (!movement) movement = GetComponent<CapsuleMovement>();
            var animator = animation ? animation.RigAnimator : null;
            if (!animator || !animator.isHuman) return;
            bool left = isKick ? attack.contactFoot == MaleWalkRetarget.AttackTiming.ContactFoot.Left
                : attack.contactHand == MaleWalkRetarget.AttackTiming.ContactHand.Left;
            upper = animator.GetBoneTransform(isKick
                ? (left ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg)
                : (left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm));
            lower = animator.GetBoneTransform(isKick
                ? (left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg)
                : (left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm));
            tip = animator.GetBoneTransform(isKick
                ? (left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot)
                : (left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand));
            if (!upper || !lower || !tip) return;
            timing = attack;
            previousTip = tip.position;
        }

        public void CancelStrike()
        {
            timing = null;
            contactCollider = null;
            contacted = false;
            pauseRemaining = contactAge = releaseAge = ConstraintWeight = 0f;
        }

        public float AdvanceImpactPause(float dt)
        {
            float held = Mathf.Min(Mathf.Max(0f, dt), pauseRemaining);
            pauseRemaining -= held;
            return dt - held;
        }

        // Called after the animation and block body's grounding have produced the final pose.
        public void ResolvePose(float dt)
        {
            if (!HandlesCurrentStrike) return;
            if (!animation.IsAttacking || (weapons && weapons.HasWeapon) || (movement && !movement.PlayerControlEnabled))
            {
                CancelStrike();
                return;
            }
            Vector3 animatedTip = tip.position;
            float radius = ContactRadius;
            float progress = animation.AttackProgress;
            float contactStart = isKick ? Mathf.Max(0.08f, timing.contact * 0.45f) : 0.08f;
            if (!contacted && progress >= contactStart && progress <= timing.recovery)
            {
                // Check the current limb and sweep its tip between sampled poses. Kicks
                // ignore upward-facing support surfaces so the floor is not an impact.
                Vector3 limb = animatedTip - upper.position;
                bool hit = CombatPhysics.Cast(upper.position, limb, limb.magnitude, transform, out var contact, radius, isKick);
                if (!hit)
                {
                    Vector3 travel = animatedTip - previousTip;
                    hit = CombatPhysics.Cast(previousTip, travel, travel.magnitude, transform, out contact, radius, isKick);
                }
                Vector3 strike = isKick && timing.strikeDirection.sqrMagnitude > 0.001f
                    ? transform.TransformDirection(timing.strikeDirection.normalized) : transform.forward;
                if (hit && Vector3.Dot(contact.point - upper.position, strike) > -radius)
                    RegisterContact(contact);
            }
            previousTip = animatedTip;
            if (!contacted || !contactCollider || !contactCollider.enabled || !contactCollider.gameObject.activeInHierarchy)
            {
                ConstraintWeight = 0f;
                return;
            }
            contactAge += Mathf.Max(0f, dt);
            if (progress >= timing.recovery) releaseAge += Mathf.Max(0f, dt);
            Vector3 normal = contactCollider.transform.TransformDirection(localNormal).normalized;
            Vector3 surface = contactCollider.transform.TransformPoint(localPoint);
            // A tiny outward rebound reads as compression without entering the collider.
            float rebound = compression * Mathf.Sin(Mathf.Clamp01(contactAge / Mathf.Max(0.01f, impactPause)) * Mathf.PI);
            Vector3 stoppedTip = surface + normal * (radius + surfaceGap + rebound);
            float upperLength = Vector3.Distance(upper.position, lower.position);
            float lowerLength = Vector3.Distance(lower.position, tip.position);
            float reach = Vector3.Distance(upper.position, stoppedTip);
            if (reach > upperLength + lowerLength + 0.04f || reach < Mathf.Abs(upperLength - lowerLength) + 0.01f)
            {
                // A target moving out of reach must not stretch or drag the limb.
                contactCollider = null;
                pauseRemaining = ConstraintWeight = 0f;
                return;
            }
            float release = Mathf.Max(releaseAge / Mathf.Max(0.01f, releaseBlendTime),
                Mathf.InverseLerp(timing.recovery, 1f, progress));
            ConstraintWeight = 1f - Mathf.SmoothStep(0f, 1f, release);
            // Keep the contact plane solid while the sampled animation is still inside it.
            float clearance = Vector3.Dot(animatedTip - surface, normal);
            if (clearance < radius + surfaceGap) ConstraintWeight = 1f;
            SolveLimb(Vector3.Lerp(animatedTip, stoppedTip, ConstraintWeight), upperLength, lowerLength);
        }

        private void RegisterContact(RaycastHit hit)
        {
            contacted = true; // Latch before damage: destruction/cancels cannot cause a second hit.
            contactCollider = hit.collider;
            localPoint = hit.collider.transform.InverseTransformPoint(hit.point);
            localNormal = hit.collider.transform.InverseTransformDirection(hit.normal);
            pauseRemaining = impactPause;
            contactAge = releaseAge = 0f;
            if (weapons) weapons.ApplyMeleeContact(hit.collider, hit.point, hit.normal, timing.damage);
        }

        private void SolveLimb(Vector3 target, float upperLength, float lowerLength)
        {
            Quaternion tipRotation = tip.rotation;
            Vector3 delta = target - upper.position;
            float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(upperLength - lowerLength) + 0.001f,
                upperLength + lowerLength - 0.001f);
            Vector3 axis = delta.normalized;
            Vector3 pole = Vector3.ProjectOnPlane(lower.position - upper.position, axis);
            if (pole.sqrMagnitude < 0.00001f) pole = Vector3.ProjectOnPlane(isKick ? transform.forward : -transform.up, axis);
            if (pole.sqrMagnitude < 0.00001f) pole = Vector3.ProjectOnPlane(transform.right, axis);
            float along = (upperLength * upperLength - lowerLength * lowerLength + distance * distance) / (2f * distance);
            float height = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
            Vector3 joint = upper.position + axis * along + pole.normalized * height;
            target = upper.position + axis * distance;
            upper.rotation = Quaternion.FromToRotation(lower.position - upper.position, joint - upper.position) * upper.rotation;
            lower.rotation = Quaternion.FromToRotation(tip.position - lower.position, target - lower.position) * lower.rotation;
            tip.rotation = tipRotation;
        }

        private void OnDisable() => CancelStrike();
    }
}
