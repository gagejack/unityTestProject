using Game.Core;
using Game.Movement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.InputSystem;

namespace StarterGame
{
    /// <summary>Plays imported Humanoid clips on the block character's own skeleton.</summary>
    // Read attack clicks before CapsuleMovement.Update so movement uses the current attack phase.
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(CharacterController))]
    public sealed class MaleWalkRetarget : MonoBehaviour, IMovementModifier
    {
        [SerializeField] private Animator rigAnimator;
        [Header("Unarmed animations")]
        [Tooltip("Forward, forward-right, right, back-right, back, back-left, left, forward-left.")]
        [SerializeField] private AnimationClip[] directionalClips = new AnimationClip[8];
        [SerializeField] private AnimationClip idleClip;
        [SerializeField] private AnimationClip airborneClip;
        [SerializeField, Min(0.1f)] private float referenceWalkSpeed = 1.9f;
        [Header("Running animations")]
        [Tooltip("Same eight directions as the walking clips. Used only while running.")]
        [SerializeField] private AnimationClip[] runningDirectionalClips = new AnimationClip[8];
        [SerializeField, Min(0.1f)] private float referenceRunSpeed = 3.8f;
        [SerializeField, Min(0.01f)] private float gaitBlendTime = 0.18f;
        private AnimationClipPlayable[] runningPlayables;
        private AnimationMixerPlayable[] gaitMixers;
        private float runBlend;
        public float RunningPoseWeight => runBlend;
        public string CurrentGait => runBlend > 0.5f ? "Run" : "Walk";
        [SerializeField, Min(0.01f)] private float blendTime = 0.12f;
        [Tooltip("Seconds to settle most of a direction change. Higher values soften animation changes without delaying movement.")]
        [SerializeField, Min(0.01f)] private float directionBlendTime = 0.16f;
        private readonly float[] directionWeights = new float[8];
        private readonly float[] targetDirectionWeights = new float[8];
        [Header("Armed movement — hip-fire and aiming")]
        [Tooltip("Forward, forward-right, right, back-right, back, back-left, left, forward-left. Use forward-facing strafe clips. Empty entries use the regular movement clip.")]
        [SerializeField] private AnimationClip[] aimingDirectionalClips = new AnimationClip[8];
        [Tooltip("Authored speed of each aimed clip at normal playback. Forward/back use walking; lateral directions use forward-facing strafes.")]
        [SerializeField] private float[] aimingReferenceSpeeds = { 1.9f, 3.8f, 3.8f, 3.8f, 1.9f, 3.8f, 3.8f, 3.8f };
        private const int AimingInputStart = 10;
        private float locomotionPhase;
        [Header("Running arms")]
        [Tooltip("Upper-arm angle down from horizontal when viewed from the front. 90 puts the arms straight beside the torso. Forward/backward swing follows the run clip.")]
        [SerializeField, Range(0f, 90f)] private float runningArmDownAngle = 70f;
        [Tooltip("Degrees to tilt the upper body back toward upright while running, on top of the run clip's own forward lean. 0 keeps the clip's posture. Fades in with the run; the legs are not changed.")]
        [SerializeField, Range(0f, 30f)] private float runningLeanCorrection = 15f;
        [Header("Unarmed attacks — Fighting Motions Vol1")]
        [Tooltip("Left-click plays the next clip, or queues one follow-up during an attack.")]
        [SerializeField] private AnimationClip[] fightingClips = new AnimationClip[0];
        [SerializeField, Min(0.01f)] private float fightingBlendTime = 0.1f;
        [Tooltip("Movement speed during the main swing, as a fraction of normal speed.")]
        [SerializeField, Range(0f, 1f)] private float attackMoveSpeedMultiplier = 0.4f;
        [Tooltip("Fraction of the clip where recovery starts: queued attacks can chain and dodges can cancel.")]
        [SerializeField, Range(0.1f, 0.9f)] private float attackRecoveryStart = 0.6f;
        [System.Serializable]
        public sealed class AttackTiming
        {
            public enum ContactHand { None, Left, Right }
            public enum ContactFoot { None, Left, Right }
            public string label;
            public ContactHand contactHand;
            public ContactFoot contactFoot;
            [Range(0f, 0.4f)] public float start = 0.02f;
            [Range(0.5f, 1f)] public float end = 0.96f;
            [Range(0.05f, 0.9f)] public float contact = 0.35f;
            [Range(0.1f, 0.9f)] public float recovery = 0.6f;
            [Range(0f, 1f)] public float movementMultiplier = 0.4f;
            [Min(0f)] public float damage = 12f;
            [Min(0.1f)] public float reach = 1.25f;
            [Min(0.01f)] public float radius = 0.22f;
            public bool kick;
            public Vector3 strikeDirection = Vector3.forward;
        }
        [Tooltip("One entry per Fighting Clip. Start/end trim the clip; contact/recovery are fractions of the trimmed motion.")]
        [SerializeField] private AttackTiming[] attackTimings = new AttackTiming[0];
        [SerializeField, Range(0.1f, 0.15f)] private float attackInputBuffer = 0.14f;
        private float queuedAttackUntil;
        private bool attackContactDone;
        private AttackTiming pendingContact;
        private StraightPunchContact punchContact;
        [Header("Soldier animations — equipped gun")]
        [SerializeField, HideInInspector] private WeaponSettings.SoldierClips soldierPistol = new WeaponSettings.SoldierClips();
        [SerializeField, HideInInspector] private WeaponSettings.SoldierClips soldierAssaultRifle = new WeaponSettings.SoldierClips();
        [SerializeField, HideInInspector] private WeaponSettings.SoldierClips soldierRifle = new WeaponSettings.SoldierClips();
        [SerializeField, HideInInspector] private WeaponSettings.SoldierClips soldierBazooka = new WeaponSettings.SoldierClips();
        [SerializeField, Min(0.01f)] private float soldierBlendTime = 0.16f;
        private WeaponCombatController weapons;
        private BlockHumanoidRig blockRig;
        private AnimationLayerMixerPlayable weaponLayers;
        private AnimationMixerPlayable soldierMixer;
        private AnimationMixerPlayable soldierFullBodyMixer;
        private AnimationClipPlayable[] soldierPlayables;
        private AnimationClipPlayable[] soldierFullBodyPlayables;
        private const int SoldierActions = 5;
        private const int SoldierFamilies = 5;
        private readonly float[] soldierWeights = new float[SoldierFamilies * SoldierActions];
        private AvatarMask soldierUpperBodyMask;
        private float soldierWeight;
        private Transform aimChest;
        private Transform leanSpine;
        private float upperBodyAimPitch;
        public string AnimationMode => weapons && weapons.HasWeapon ? "Soldier" : "Unarmed";
        public float SoldierAnimationWeight => soldierWeight;
        public string CurrentSoldierAnimation { get; private set; } = "";
        private bool attackQueued;
        private const int LocomotionInputs = 18;
        private float[] fightingWeights;
        private int activeFight = -1;
        private int nextFight;
        private ViewModeDebugMenu viewMenu;
        public int NextFightingMotionIndex => nextFight;
        public string CurrentFightingMotion => activeFight >= 0 ? fightingClips[activeFight].name : "";
        public float FightingWeight { get; private set; }
        public bool IsAttacking => isActiveAndEnabled && activeFight >= 0 && graph.IsValid();
        public bool HasQueuedAttack => attackQueued;
        private AttackTiming CurrentAttackTiming => activeFight >= 0 && attackTimings != null && activeFight < attackTimings.Length ? attackTimings[activeFight] : null;
        public float AttackProgress => IsAttacking
            ? Mathf.InverseLerp(CurrentAttackTiming != null ? CurrentAttackTiming.start : 0f,
                CurrentAttackTiming != null ? CurrentAttackTiming.end : 1f,
                (float)playables[LocomotionInputs + activeFight].GetTime() / fightingClips[activeFight].length) : 1f;
        private float RecoveryStart => Mathf.Clamp(CurrentAttackTiming != null ? CurrentAttackTiming.recovery : attackRecoveryStart, 0.1f, 0.9f);
        public bool IsAttackCommitted => IsAttacking && AttackProgress < RecoveryStart;
        public float AttackMovementMultiplier
        {
            get
            {
                if (!IsAttacking) return 1f;
                float fullSpeedPoint = Mathf.Lerp(RecoveryStart, 1f, 0.5f);
                float recovery = Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(RecoveryStart, fullSpeedPoint, AttackProgress));
                return Mathf.Lerp(Mathf.Clamp01(CurrentAttackTiming != null ? CurrentAttackTiming.movementMultiplier : attackMoveSpeedMultiplier), 1f, recovery);
            }
        }
        private bool SpecialMoveActive => movement &&
            (movement.IsRolling || movement.IsDiving || movement.IsSliding || movement.IsCrouching);

        private Transform leftUpperArm;
        private Transform leftLowerArm;
        private Transform rightUpperArm;
        private Transform rightLowerArm;
        private CharacterController controller;
        private CapsuleMovement movement;
        private PlayableGraph graph;
        private AnimationMixerPlayable mixer;
        private AnimationClipPlayable[] playables;
        private Vector3 previousPosition;
        private float poseWeight;
        private float airWeight;
        public Animator RigAnimator => rigAnimator;
        public float PoseWeight => poseWeight;
        private Vector3 BonePosition(HumanBodyBones bone) => rigAnimator ? rigAnimator.GetBoneTransform(bone).position : transform.position;
        public Vector3 LeftFootPosition => BonePosition(HumanBodyBones.LeftFoot);
        public Vector3 RightFootPosition => BonePosition(HumanBodyBones.RightFoot);
        public Vector3 LeftHandPosition => BonePosition(HumanBodyBones.LeftHand);
        public Vector3 RightHandPosition => BonePosition(HumanBodyBones.RightHand);

        // The rig root's resting position under the player (at the feet); grounding nudges it each frame.
        private Vector3 rigRestLocalPosition;

        private void Awake()
        {
            if (rigAnimator) rigRestLocalPosition = rigAnimator.transform.localPosition;
        }

        private void OnEnable()
        {
            controller = GetComponent<CharacterController>();
            movement = GetComponent<CapsuleMovement>();
            weapons = GetComponent<WeaponCombatController>();
            blockRig = GetComponent<BlockHumanoidRig>();
            punchContact = GetComponent<StraightPunchContact>();
            viewMenu = FindAnyObjectByType<ViewModeDebugMenu>();
            activeFight = -1;
            attackQueued = false;
            nextFight = 0;
            FightingWeight = 0f;
            fightingWeights = new float[fightingClips == null ? 0 : fightingClips.Length];
            previousPosition = transform.position;
            System.Array.Clear(directionWeights, 0, directionWeights.Length);
            directionWeights[0] = 1f;
            poseWeight = airWeight = 0f;
            locomotionPhase = 0f;
            runBlend = 0f;
            if (!rigAnimator || !rigAnimator.avatar || !rigAnimator.avatar.isHuman ||
                directionalClips == null || directionalClips.Length != 8 || !idleClip || !airborneClip)
            {
                Debug.LogError("Male Walk Retarget needs the block Humanoid Animator, idle, airborne, and eight walk clips.", this);
                enabled = false;
                return;
            }
            foreach (var clip in directionalClips)
                if (!clip) { Debug.LogError("Assign every directional walk clip.", this); enabled = false; return; }
            rigAnimator.applyRootMotion = false;
            rigAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            rigAnimator.Rebind();
            leftUpperArm = rigAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            leftLowerArm = rigAnimator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            rightUpperArm = rigAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            rightLowerArm = rigAnimator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            aimChest = rigAnimator.GetBoneTransform(HumanBodyBones.Chest);
            leanSpine = rigAnimator.GetBoneTransform(HumanBodyBones.Spine);
            graph = PlayableGraph.Create("Block Humanoid Animation");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            mixer = AnimationMixerPlayable.Create(graph, LocomotionInputs + fightingWeights.Length);
            playables = new AnimationClipPlayable[LocomotionInputs + fightingWeights.Length];
            runningPlayables = new AnimationClipPlayable[8];
            gaitMixers = new AnimationMixerPlayable[8];
            for (int i = 0; i < 10; i++)
            {
                var clip = i < 8 ? directionalClips[i] : i == 8 ? idleClip : airborneClip;
                playables[i] = AnimationClipPlayable.Create(graph, clip);
                playables[i].SetApplyFootIK(false);
                if (i < 8)
                {
                    var runClip = runningDirectionalClips != null && i < runningDirectionalClips.Length && runningDirectionalClips[i]
                        ? runningDirectionalClips[i] : clip;
                    runningPlayables[i] = AnimationClipPlayable.Create(graph, runClip);
                    runningPlayables[i].SetApplyFootIK(false);
                    runningPlayables[i].SetSpeed(0d);
                    gaitMixers[i] = AnimationMixerPlayable.Create(graph, 2);
                    graph.Connect(playables[i], 0, gaitMixers[i], 0);
                    graph.Connect(runningPlayables[i], 0, gaitMixers[i], 1);
                    gaitMixers[i].SetInputWeight(0, 1f);
                    graph.Connect(gaitMixers[i], 0, mixer, i);
                }
                else graph.Connect(playables[i], 0, mixer, i);
            }
            for (int i = 0; i < 8; i++)
            {
                var clip = aimingDirectionalClips != null && i < aimingDirectionalClips.Length && aimingDirectionalClips[i]
                    ? aimingDirectionalClips[i] : directionalClips[i];
                int input = AimingInputStart + i;
                playables[input] = AnimationClipPlayable.Create(graph, clip);
                playables[input].SetApplyFootIK(false);
                graph.Connect(playables[input], 0, mixer, input);
            }
            for (int i = 0; i < fightingWeights.Length; i++)
            {
                if (!fightingClips[i] || !fightingClips[i].isHumanMotion || fightingClips[i].length <= 0f)
                {
                    Debug.LogError("Assign a valid Humanoid clip to every Fighting Clips entry.", this);
                    enabled = false;
                    return;
                }
                int input = LocomotionInputs + i;
                playables[input] = AnimationClipPlayable.Create(graph, fightingClips[i]);
                playables[input].SetApplyFootIK(false);
                playables[input].SetSpeed(0f);
                graph.Connect(playables[input], 0, mixer, input);
            }
            var output = AnimationPlayableOutput.Create(graph, "Block Body", rigAnimator);
            if (CreateSoldierLayers()) output.SetSourcePlayable(weaponLayers);
            else output.SetSourcePlayable(mixer);
            mixer.SetInputWeight(8, 1f);
            graph.Play();
            graph.Evaluate(0f);
        }

        private void Update()
        {
            // Left click belongs to the equipped gun; empty hands use unarmed attacks.
            if ((movement && !movement.PlayerControlEnabled) || (weapons && weapons.HasWeapon)) return;
            var mouse = Mouse.current;
            if (!Application.isFocused || mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            if (Cursor.lockState != CursorLockMode.Locked && viewMenu &&
                viewMenu.ContainsPointer(mouse.position.ReadValue())) return;
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;
            PlayNextFightingMotion();
        }

        public bool PlayNextFightingMotion()
        {
            if (!isActiveAndEnabled || !graph.IsValid() || fightingWeights.Length == 0 ||
                SpecialMoveActive || (movement && !movement.PlayerControlEnabled) || (weapons && weapons.HasWeapon)) return false;
            if (IsAttacking)
            {
                // Buffer only one follow-up, so extra clicks cannot create a long unwanted combo.
                attackQueued = true;
                queuedAttackUntil = Time.time + attackInputBuffer;
                return true;
            }
            StartNextFightingMotion();
            return true;
        }

        private void StartNextFightingMotion()
        {
            attackQueued = false;
            activeFight = nextFight;
            attackContactDone = false;
            nextFight = (nextFight + 1) % fightingWeights.Length;
            var playable = playables[LocomotionInputs + activeFight];
            playable.SetTime((CurrentAttackTiming != null ? CurrentAttackTiming.start : 0f) * fightingClips[activeFight].length);
            playable.SetDone(false);
            playable.SetSpeed(0d);
            if (punchContact) punchContact.BeginStrike(CurrentAttackTiming);
            if (weapons) weapons.PlayMeleeSwing();
        }

        public void CancelFightingMotion()
        {
            activeFight = -1;
            attackQueued = false;
            pendingContact = null;
            if (punchContact) punchContact.CancelStrike();
            // Outgoing pose weights fade away; movement is immediately available.
        }

        private void UpdateFightingBlend(float dt)
        {
            if (SpecialMoveActive || (movement && !movement.PlayerControlEnabled) || (weapons && weapons.HasWeapon)) CancelFightingMotion();
            float attackStep = punchContact ? punchContact.AdvanceImpactPause(dt) : dt;
            for (int i = 0; i < fightingWeights.Length; i++)
            {
                var playable = playables[LocomotionInputs + i];
                // Sample manually so imported looping settings cannot repeat an attack.
                if (i == activeFight || fightingWeights[i] > 0f)
                    playable.SetTime(System.Math.Min(playable.GetTime() + (i == activeFight ? attackStep : dt), fightingClips[i].length - 0.001f));
                playable.SetSpeed(0d);
            }
            if (IsAttacking)
            {
                if (attackQueued && Time.time > queuedAttackUntil) attackQueued = false;
                if (!attackContactDone && AttackProgress >= (CurrentAttackTiming != null ? CurrentAttackTiming.contact : 0.35f))
                {
                    attackContactDone = true;
                    if (!punchContact || !punchContact.HandlesCurrentStrike)
                        pendingContact = CurrentAttackTiming ?? new AttackTiming();
                }
                if (attackQueued && !IsAttackCommitted)
                    StartNextFightingMotion();
                else if (AttackProgress >= 0.999f)
                {
                    // A long frame may pass contact and completion together.
                    var contact = pendingContact;
                    CancelFightingMotion();
                    pendingContact = contact;
                }
            }

            // Fade back toward locomotion during recovery rather than waiting for the clip to end.
            float attackPose = IsAttacking
                ? 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(RecoveryStart, 1f, AttackProgress))
                : 0f;
            float alpha = 1f - Mathf.Exp(-3f * dt / Mathf.Max(0.01f, fightingBlendTime));
            FightingWeight = 0f;
            for (int i = 0; i < fightingWeights.Length; i++)
            {
                fightingWeights[i] = Mathf.Lerp(fightingWeights[i], i == activeFight ? attackPose : 0f, alpha);
                if (i != activeFight && fightingWeights[i] < 0.001f) fightingWeights[i] = 0f;
                mixer.SetInputWeight(LocomotionInputs + i, fightingWeights[i]);
                FightingWeight += fightingWeights[i];
            }
            FightingWeight = Mathf.Clamp01(FightingWeight);
        }

        private void LateUpdate() => SimulatePose(Time.deltaTime);

        private void SimulatePose(float dt)
        {
            if (!graph.IsValid() || dt <= 0f) return;
            Vector3 measured = (transform.position - previousPosition) / dt;
            previousPosition = transform.position;
            Vector3 local = transform.InverseTransformDirection(new Vector3(measured.x, 0f, measured.z));
            float speed = local.magnitude;
            bool special = SpecialMoveActive;
            UpdateDirectionalBlend(local, dt);
            poseWeight = Mathf.MoveTowards(poseWeight, controller.isGrounded && !special && speed > 0.12f ? 1f : 0f, dt / blendTime);
            airWeight = Mathf.MoveTowards(airWeight, !controller.isGrounded && !special ? 1f : 0f, dt / blendTime);
            UpdateFightingBlend(dt);
            float locomotionWeight = 1f - FightingWeight;
            float walk = poseWeight * (1f - airWeight) * locomotionWeight;
            runBlend = Mathf.MoveTowards(runBlend, movement && movement.IsRunning && !special ? 1f : 0f,
                dt / Mathf.Max(0.01f, gaitBlendTime));
            // Armed walking keeps hips facing the reticle even without right mouse.
            // Running retains its own gait; firing cancels running and blends to strafing.
            float armedStrafe = weapons && weapons.HasWeapon && !special ? soldierWeight * (1f - runBlend) : 0f;
            for (int i = 0; i < 8; i++)
            {
                gaitMixers[i].SetInputWeight(0, 1f - runBlend);
                gaitMixers[i].SetInputWeight(1, runBlend);
                mixer.SetInputWeight(i, walk * directionWeights[i] * (1f - armedStrafe));
                mixer.SetInputWeight(AimingInputStart + i, walk * directionWeights[i] * armedStrafe);
            }
            mixer.SetInputWeight(8, (1f - poseWeight) * (1f - airWeight) * locomotionWeight);
            mixer.SetInputWeight(9, airWeight * locomotionWeight);
            UpdateLocomotionPhase(dt, speed, armedStrafe);
            for (int i = 8; i < 10; i++)
            {
                playables[i].SetSpeed(1f);
                // Imported FBX clips may not have Loop Time enabled.
                double duration = playables[i].GetAnimationClip().length;
                if (duration > 0 && playables[i].GetTime() >= duration)
                    playables[i].SetTime(playables[i].GetTime() % duration);
            }
            rigAnimator.transform.localPosition = rigRestLocalPosition;
            UpdateSoldierLayers(dt);
            graph.Evaluate(dt);
            ApplyRunningLean(walk * runBlend);
            ApplyRunningArmPose(walk * runBlend);
            if (aimChest && weapons && weapons.HasWeapon)
            {
                Vector3 aim = weapons.AimWorldDirection;
                float pitch = Mathf.Atan2(-aim.y, new Vector2(aim.x, aim.z).magnitude) * Mathf.Rad2Deg;
                float targetPitch = Mathf.Clamp(pitch, -90f, 45f) * weapons.AimWeight;
                upperBodyAimPitch = Mathf.Lerp(upperBodyAimPitch, targetPitch, 1f - Mathf.Exp(-dt / 0.08f));
                aimChest.rotation = Quaternion.AngleAxis(upperBodyAimPitch, transform.right) * aimChest.rotation;
            }
            else upperBodyAimPitch = 0f;
            if (!blockRig || !blockRig.UsingDownloadedAnimation) ApplyPunchContact(dt);
            // Block characters finish their weapon pose after grounding the body.
            if (weapons && (!blockRig || !blockRig.UsingDownloadedAnimation))
                weapons.ApplyAnimatedWeaponPose(dt, !special);
            if (pendingContact != null)
            {
                if (weapons) weapons.PerformMeleeHit(pendingContact.damage, pendingContact.reach, pendingContact.radius, pendingContact.kick, pendingContact.strikeDirection);
                pendingContact = null;
            }
        }

        public void ApplyPunchContact(float dt)
        {
            if (punchContact) punchContact.ResolvePose(dt);
        }

        private void UpdateLocomotionPhase(float dt, float speed, float armedStrafe)
        {
            // Share a normalized step cycle across all directions and both stances.
            // Blending aim or reversing direction must not restart a footstep.
            // Blend distance per cycle, not duration alone: a strafe and a walk
            // cover different distances even when their normalized phases match.
            float strideDistance = 0f;
            for (int i = 0; i < 8; i++)
            {
                float aimSpeed = aimingReferenceSpeeds != null && i < aimingReferenceSpeeds.Length
                    ? Mathf.Max(0.1f, aimingReferenceSpeeds[i]) : referenceWalkSpeed;
                strideDistance += directionWeights[i] * Mathf.Lerp(
                    Mathf.Lerp(playables[i].GetAnimationClip().length * referenceWalkSpeed,
                        runningPlayables[i].GetAnimationClip().length * referenceRunSpeed, runBlend),
                    playables[AimingInputStart + i].GetAnimationClip().length * aimSpeed, armedStrafe);
            }
            locomotionPhase = Mathf.Repeat(locomotionPhase + dt * speed / Mathf.Max(0.1f, strideDistance), 1f);
            for (int i = 0; i < 8; i++)
            {
                var regular = playables[i];
                var aimed = playables[AimingInputStart + i];
                regular.SetSpeed(0d);
                aimed.SetSpeed(0d);
                regular.SetTime(locomotionPhase * regular.GetAnimationClip().length);
                aimed.SetTime(locomotionPhase * aimed.GetAnimationClip().length);
                runningPlayables[i].SetTime(locomotionPhase * runningPlayables[i].GetAnimationClip().length);
            }
        }

        private WeaponSettings.SoldierClips SoldierSet(int index) => (weapons ? weapons.AnimationSetForFamily(index) : null)
            ?? (index == 0 ? soldierPistol : index == 1 ? soldierAssaultRifle : index == 2 ? soldierRifle : soldierBazooka);

        public float SoldierReloadDuration(int index)
        {
            AnimationClip clip = SoldierSet(index)?.reload;
            return clip ? clip.length : 1.25f;
        }

        private bool CreateSoldierLayers()
        {
            soldierWeight = 0f;
            CurrentSoldierAnimation = "";
            if (!weapons) return false;
            var clips = new AnimationClip[SoldierFamilies * SoldierActions];
            for (int family = 0; family < SoldierFamilies; family++)
            {
                WeaponSettings.SoldierClips set = SoldierSet(family);
                clips[family * SoldierActions] = set?.aim;
                clips[family * SoldierActions + 1] = set?.shoot;
                clips[family * SoldierActions + 2] = set?.reload;
                clips[family * SoldierActions + 3] = set != null && set.precisionAim ? set.precisionAim : set?.aim;
                clips[family * SoldierActions + 4] = set != null && set.precisionShoot ? set.precisionShoot : set?.shoot;
            }
            foreach (AnimationClip clip in clips)
            {
                if (clip && clip.isHumanMotion && clip.length > 0f) continue;
                Debug.LogError("Assign the Soldier aim, shoot, and reload clips for pistol, assault rifle, rifle, and bazooka on Male Walk Retarget.", this);
                return false;
            }
            soldierMixer = AnimationMixerPlayable.Create(graph, clips.Length);
            soldierFullBodyMixer = AnimationMixerPlayable.Create(graph, clips.Length);
            soldierPlayables = new AnimationClipPlayable[clips.Length];
            soldierFullBodyPlayables = new AnimationClipPlayable[clips.Length];
            System.Array.Clear(soldierWeights, 0, soldierWeights.Length);
            soldierWeights[0] = 1f;
            for (int i = 0; i < clips.Length; i++)
            {
                soldierPlayables[i] = AnimationClipPlayable.Create(graph, clips[i]);
                soldierPlayables[i].SetApplyFootIK(false);
                soldierPlayables[i].SetSpeed(0d);
                graph.Connect(soldierPlayables[i], 0, soldierMixer, i);
                soldierMixer.SetInputWeight(i, soldierWeights[i]);
                // Animation graphs require a tree: the full-body and masked branches
                // need separate playable instances, sampled at the same time.
                soldierFullBodyPlayables[i] = AnimationClipPlayable.Create(graph, clips[i]);
                soldierFullBodyPlayables[i].SetApplyFootIK(false);
                soldierFullBodyPlayables[i].SetSpeed(0d);
                graph.Connect(soldierFullBodyPlayables[i], 0, soldierFullBodyMixer, i);
                soldierFullBodyMixer.SetInputWeight(i, soldierWeights[i]);
            }
            // At rest use the Soldier's whole stance; during movement retain the run
            // footwork shared with the Soldier pack and layer the weapon over the torso.
            weaponLayers = AnimationLayerMixerPlayable.Create(graph, 3);
            graph.Connect(mixer, 0, weaponLayers, 0);
            graph.Connect(soldierFullBodyMixer, 0, weaponLayers, 1);
            graph.Connect(soldierMixer, 0, weaponLayers, 2);
            weaponLayers.SetInputWeight(0, 1f);
            soldierUpperBodyMask = new AvatarMask();
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                soldierUpperBodyMask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
            foreach (AvatarMaskBodyPart part in new[] { AvatarMaskBodyPart.Body, AvatarMaskBodyPart.Head,
                AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.RightArm, AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers })
                soldierUpperBodyMask.SetHumanoidBodyPartActive(part, true);
            weaponLayers.SetLayerMaskFromAvatarMask(2, soldierUpperBodyMask);
            return true;
        }

        private void UpdateSoldierLayers(float dt)
        {
            if (!weaponLayers.IsValid()) return;
            bool armed = weapons && weapons.HasWeapon;
            int family = weapons ? weapons.SoldierAnimationFamily : 0;
            float sinceShot = weapons ? Time.time - weapons.LastShotTime : float.PositiveInfinity;
            bool precise = weapons && weapons.IsAiming;
            var set = SoldierSet(family);
            AnimationClip shotClip = precise && set.precisionShoot ? set.precisionShoot : set.shoot;
            int action = weapons && weapons.IsReloading ? 2 : sinceShot < shotClip.length ? (precise ? 4 : 1) : (precise ? 3 : 0);
            int selected = family * SoldierActions + action;
            float alpha = 1f - Mathf.Exp(-3f * dt / Mathf.Max(0.01f, soldierBlendTime));
            for (int i = 0; i < soldierPlayables.Length; i++)
            {
                var playable = soldierPlayables[i];
                float duration = playable.GetAnimationClip().length;
                if (i % SoldierActions == 0 || i % SoldierActions == 3) playable.SetTime((playable.GetTime() + dt) % duration);
                else if (i == selected)
                    playable.SetTime(Mathf.Clamp(action == 2 ? weapons.ReloadProgress * duration : sinceShot, 0f, duration - 0.001f));
                soldierWeights[i] = Mathf.Lerp(soldierWeights[i], i == selected ? 1f : 0f, alpha);
                soldierMixer.SetInputWeight(i, soldierWeights[i]);
                soldierFullBodyPlayables[i].SetTime(playable.GetTime());
                soldierFullBodyMixer.SetInputWeight(i, soldierWeights[i]);
            }
            soldierWeight = Mathf.MoveTowards(soldierWeight, armed ? 1f : 0f, dt / Mathf.Max(0.01f, soldierBlendTime));
            // Fade the weapon stance off the torso and arms during regular running.
            // Weapons remain parented to the animated hands throughout the transition.
            float weaponPoseWeight = soldierWeight * (1f - runBlend);
            weaponLayers.SetInputWeight(1, weaponPoseWeight * (1f - poseWeight) * (1f - airWeight));
            weaponLayers.SetInputWeight(2, weaponPoseWeight);
            CurrentSoldierAnimation = armed ? soldierPlayables[selected].GetAnimationClip().name : "";
        }

        private void UpdateDirectionalBlend(Vector3 localVelocity, float dt)
        {
            // Keep the outgoing direction during the fade to idle.
            if (localVelocity.sqrMagnitude <= 0.0144f) return;
            System.Array.Clear(targetDirectionWeights, 0, targetDirectionWeights.Length);
            float octant = Mathf.Repeat(Mathf.Atan2(localVelocity.x, localVelocity.z) * Mathf.Rad2Deg, 360f) / 45f;
            int first = Mathf.FloorToInt(octant) % 8;
            float fraction = octant - Mathf.Floor(octant);
            targetDirectionWeights[first] = 1f - fraction;
            targetDirectionWeights[(first + 1) % 8] = fraction;
            // Exponential interpolation is consistent across frame rates and handles interrupted turns.
            // At rest there is no visible run pose to preserve: take the first step
            // in the requested direction, then soften subsequent moving turns.
            float alpha = poseWeight <= 0.001f ? 1f
                : 1f - Mathf.Exp(-3f * Mathf.Max(0f, dt) / Mathf.Max(0.01f, directionBlendTime));
            for (int i = 0; i < 8; i++)
                directionWeights[i] = Mathf.Lerp(directionWeights[i], targetDirectionWeights[i], alpha);
        }
        // Tilts the spine and chest back (half each, for a gentle curve) around the character's side axis.
        private void ApplyRunningLean(float weight)
        {
            if (weight <= 0f || runningLeanCorrection <= 0f || (movement && (movement.IsCrouching || movement.IsRolling ||
                movement.IsDiving || movement.IsSliding))) return;
            var back = Quaternion.AngleAxis(-0.5f * runningLeanCorrection * weight, transform.right);
            if (leanSpine) leanSpine.rotation = back * leanSpine.rotation;
            if (aimChest) aimChest.rotation = back * aimChest.rotation;
        }

        private void ApplyRunningArmPose(float weight)
        {
            if (weight <= 0f || (movement && (movement.IsCrouching || movement.IsRolling ||
                movement.IsDiving || movement.IsSliding))) return;
            TuckRunningArm(leftUpperArm, leftLowerArm, -1f, weight);
            TuckRunningArm(rightUpperArm, rightLowerArm, 1f, weight);
        }

        private void TuckRunningArm(Transform upper, Transform lower, float side, float weight)
        {
            if (!upper || !lower) return;
            Vector3 current = lower.position - upper.position;
            Vector3 local = transform.InverseTransformDirection(current);
            float acrossBody = new Vector2(local.x, local.y).magnitude;
            float angle = runningArmDownAngle * Mathf.Deg2Rad;
            // Change only the front-view spread, retaining length and the clip's forward/back swing.
            Vector3 target = new Vector3(side * Mathf.Cos(angle) * acrossBody,
                -Mathf.Sin(angle) * acrossBody, local.z);
            Quaternion corrected = Quaternion.FromToRotation(current, transform.TransformDirection(target)) * upper.rotation;
            upper.rotation = Quaternion.Slerp(upper.rotation, corrected, weight);
        }

        // How an unarmed attack changes movement (see IMovementModifier): the swing slows travel (eased),
        // stops running, commits jump/roll/crouch until recovery, and caps carried momentum.
        float IMovementModifier.SpeedMultiplier => AttackMovementMultiplier;
        bool IMovementModifier.EaseSpeedChange => true;
        bool IMovementModifier.BlocksRunning => IsAttacking;
        bool IMovementModifier.BlocksCommittedMoves => IsAttackCommitted;
        bool IMovementModifier.LimitsMomentum => IsAttacking;
        bool IMovementModifier.TryGetFacing(out Vector3 planarForward) { planarForward = Vector3.zero; return false; }
        void IMovementModifier.OnMovementAction(MovementAction action) => CancelFightingMotion();

        private void OnDisable()
        {
            poseWeight = airWeight = 0f;
            CancelFightingMotion();
            FightingWeight = 0f;
            if (graph.IsValid()) graph.Destroy();
            if (soldierUpperBodyMask) Destroy(soldierUpperBodyMask);
            soldierWeight = 0f;
            CurrentSoldierAnimation = "";
        }
    }
}

