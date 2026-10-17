using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StarterGame
{
    /// <summary>
    /// The holster: a second gun slot on the body, and the draw and holster motion. H swaps the gun in the hands with the
    /// holstered one (holsters it when the holster is empty, draws when the hands are empty); right mouse with empty
    /// hands draws. A holster moves the right hand (both hands for dual handguns) to the holster; at the moment it
    /// arrives the gun leaves the hand and settles onto its spot. A draw is the reverse. A swap is a holster followed by
    /// a draw. Back guns play the Kevin Iglesias sheathe clips on the upper body; hip guns reach by IK only (the pack's
    /// hip clip is a cross-draw from the left hip). The weapon controller owns both guns and their ammo.
    /// Where a holstered gun rides comes from its category: pistols and SMGs on the right hip (dual handguns on both
    /// hips), everything else across the back, plus the gun's own Holster Offset, Holster Euler and Holster On Back.
    /// </summary>
    [RequireComponent(typeof(WeaponCombatController))]
    public sealed class WeaponHolster : MonoBehaviour
    {
        [System.Serializable]
        public sealed class Spot
        {
            [Tooltip("Body bone the holstered gun rides on, so it moves with the body.")]
            public HumanBodyBones bone = HumanBodyBones.Hips;
            [Tooltip("Where the gun's grip sits, in meters from the bone, along the character: X right, Y up, Z forward.")]
            public Vector3 position;
            [Tooltip("The gun's rotation on the body, in degrees around the character's axes.")]
            public Vector3 euler;
            [Tooltip("How far along the gun from its grip, in meters, the hand takes hold of it on this spot (0 = at the grip).")]
            [Min(0f)] public float handHold;
            [Tooltip("Fraction of the draw or holster (0-1) at which the hand reaches this spot and the gun changes hands.")]
            [Range(0.1f, 0.9f)] public float contact = 0.5f;
        }

        [Tooltip("Pistols and SMGs: on the right hip, muzzle down along the thigh. Dual handguns also use its mirror image on the left hip.")]
        [SerializeField] private Spot hip = new Spot { bone = HumanBodyBones.Hips, position = new Vector3(0.3f, 0.03f, 0f), euler = new Vector3(90f, 0f, 0f), contact = 0.45f };
        [Tooltip("Assault rifles, shotguns, rifles and snipers, machine guns and launchers: across the back, muzzle up over the right shoulder.")]
        [SerializeField] private Spot back = new Spot { bone = HumanBodyBones.Chest, position = new Vector3(-0.1f, -0.3f, -0.17f), euler = new Vector3(-60f, 90f, 0f), handHold = 0.45f, contact = 0.5f };
        [Header("Draw and holster motion")]
        [Tooltip("Seconds from pressing H to the drawn gun being ready to fire.")]
        [SerializeField, Min(0.05f)] private float drawTime = 0.5f;
        [Tooltip("Seconds from pressing H to the hands being free after holstering. A swap takes Holster Time plus Draw Time.")]
        [SerializeField, Min(0.05f)] private float holsterTime = 0.5f;
        [Tooltip("Seconds the gun takes to swing from where the hand lets go or takes hold into the holster or the grip.")]
        [SerializeField, Min(0f)] private float settleTime = 0.15f;
        [Tooltip("Upper-body clip for drawing a gun from the back (Kevin Iglesias UnsheatheBack01_R).")]
        [SerializeField] private AnimationClip drawFromBack;
        [Tooltip("Upper-body clip for putting a gun on the back (Kevin Iglesias SheatheBack01_R).")]
        [SerializeField] private AnimationClip holsterToBack;

        enum Motion { None, Holstering, Drawing }
        Motion motion;
        float progress;
        bool contactDone, drawAfter;
        WeaponSettings motionGun;
        // A gun just put on the body swings from where the hand let go onto its spot.
        GameObject settlingRight, settlingLeft;
        WeaponSettings settlingGun;
        Vector3 settleFromRight, settleFromLeft;
        Quaternion settleFromRightRotation, settleFromLeftRotation;
        float bodySettle = 1f;

        /// <summary>True while drawing, holstering or swapping.</summary>
        public bool IsSwitching => motion != Motion.None;
        /// <summary>The upper-body clip of the motion in progress (null for hip guns, which reach by IK only).</summary>
        public AnimationClip ActiveClip => motion == Motion.None || !OnBack(motionGun) ? null : motion == Motion.Drawing ? drawFromBack : holsterToBack;
        /// <summary>Where the active clip is, in seconds.</summary>
        public float ActiveClipTime => ActiveClip ? progress * ActiveClip.length : 0f;
        /// <summary>How much of the active clip is on the upper body (0-1): in while reaching, out after the contact.</summary>
        public float ActiveClipWeight => ActiveClip ? Envelope(0.15f) : 0f;
        public AnimationClip DrawFromBackClip => drawFromBack;
        public AnimationClip HolsterToBackClip => holsterToBack;

        WeaponCombatController weaponsCache;
        MaleWalkRetarget bodyCache;
        // Looked up on first use: the weapon controller places its first gun in its own Awake, which can run before ours.
        WeaponCombatController weapons => weaponsCache ? weaponsCache : weaponsCache = GetComponent<WeaponCombatController>();
        MaleWalkRetarget body => bodyCache ? bodyCache : bodyCache = GetComponent<MaleWalkRetarget>();

        void Update()
        {
            float dt = Time.deltaTime;
            if (motion != Motion.None) Advance(dt);
            SettleOnBody(dt);
            if (motion != Motion.None || !PlayerControl.Enabled) return;
            if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame) RequestSwap();
            else if (!weapons.HasWeapon && weapons.HolsteredWeapon && Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
                RequestSwap();
        }

        /// <summary>
        /// H: holsters the gun in the hands (then draws the holstered one, if any), or draws the holstered gun into empty hands.
        /// </summary>
        public void RequestSwap()
        {
            if (motion != Motion.None || !weapons.isActiveAndEnabled) return;
            if (weapons.HasWeapon) Begin(Motion.Holstering, weapons.CurrentWeapon, weapons.HolsteredWeapon);
            else if (weapons.HolsteredWeapon)
            {
                // The holstered gun becomes the hand's gun now; it counts as held once the hand takes it.
                weapons.SwapSlots();
                Begin(Motion.Drawing, weapons.CurrentWeapon, false);
            }
        }

        void Begin(Motion next, WeaponSettings gun, bool thenDraw)
        {
            motion = next;
            motionGun = gun;
            progress = 0f;
            contactDone = false;
            drawAfter = thenDraw;
        }

        void Advance(float dt)
        {
            progress += dt / (motion == Motion.Drawing ? drawTime : holsterTime);
            if (!contactDone && progress >= SpotFor(motionGun).contact)
            {
                contactDone = true;
                if (motion == Motion.Holstering)
                {
                    // The hand has reached the holster: the gun leaves it and settles onto the body.
                    GameObject right = weapons.HeldModel, left = weapons.OffhandModel;
                    weapons.SwapSlots();
                    PlaceOnBody(right, left, motionGun, true);
                }
                else
                {
                    // The hand has taken the gun: it moves into the hands from where it lay and settles into the grip.
                    PlaceInHands(weapons.HeldModel, weapons.OffhandModel, true);
                    weapons.SetHandReady(settleTime);
                }
            }
            if (progress < 1f) return;
            motion = Motion.None;
            if (drawAfter && weapons.CurrentWeapon && weapons.HeldModel && !weapons.HasWeapon)
                Begin(Motion.Drawing, weapons.CurrentWeapon, false);
        }

        /// <summary>Ends a draw or holster at once (a new gun was equipped): every gun goes straight to its final place.</summary>
        internal void CancelMotion()
        {
            if (motion == Motion.Drawing && !contactDone)
            {
                PlaceInHands(weapons.HeldModel, weapons.OffhandModel, false);
                weapons.SetHandReady(0f);
            }
            // A holster that has not reached the body yet changed nothing: the gun is still in the hands.
            motion = Motion.None;
            drawAfter = false;
            bodySettle = 1f;
            if (weapons.HolsteredModel) PlaceOnBody(weapons.HolsteredModel, weapons.HolsteredOffhandModel, weapons.HolsteredWeapon, false);
        }

        // 0 to 1 while reaching (rising over the first fadeIn of the motion), 1 at the contact, back to 0 by the end.
        float Envelope(float fadeIn)
        {
            float contact = SpotFor(motionGun).contact;
            if (progress < contact) return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / Mathf.Max(0.01f, fadeIn)));
            return Mathf.SmoothStep(1f, 0f, Mathf.Clamp01((progress - contact) / Mathf.Max(0.01f, 1f - contact)));
        }

        /// <summary>
        /// Where the hands reach this frame (for the pose step in the animation graph): toward the point where they take
        /// hold of the gun on its holster spot, fully at the contact. groundShift is how far grounding will move the body.
        /// </summary>
        internal HandReach PrepareReach(Vector3 groundShift)
        {
            var reach = new HandReach();
            var rig = body ? body.RigAnimator : null;
            if (motion == Motion.None || !rig || !motionGun) return reach;
            float contact = SpotFor(motionGun).contact;
            float weight = progress < contact ? Mathf.SmoothStep(0f, 1f, progress / contact) : Envelope(0f);
            reach.right = weight;
            reach.rightPoint = HoldPoint(rig, motionGun, false) - groundShift;
            if (motionGun.dualWield)
            {
                reach.left = weight;
                reach.leftPoint = HoldPoint(rig, motionGun, true) - groundShift;
            }
            return reach;
        }

        /// <summary>Where the right hand takes hold of a gun on its holster spot this frame, in world space.</summary>
        public Vector3 HandHoldPoint(WeaponSettings gun)
        {
            var rig = body ? body.RigAnimator : null;
            return rig && gun ? HoldPoint(rig, gun, false) : transform.position;
        }

        // Where the hand takes hold of a gun on its holster spot, in world space.
        Vector3 HoldPoint(Animator rig, WeaponSettings gun, bool mirrored)
        {
            Spot spot = SpotFor(gun);
            Transform bone = rig.GetBoneTransform(spot.bone);
            SpotPose(spot, gun, mirrored, out Vector3 position, out Quaternion rotation);
            return bone.TransformPoint(position + rotation * Vector3.forward * spot.handHold);
        }

        bool OnBack(WeaponSettings gun) => gun && SpotFor(gun) == back;

        /// <summary>Which spot a gun rides on when holstered.</summary>
        public Spot SpotFor(WeaponSettings settings)
        {
            if (!settings) return hip;
            bool hipCategory = settings.category == WeaponSettings.WeaponCategory.Pistols || settings.category == WeaponSettings.WeaponCategory.SMGs;
            return hipCategory && !settings.holsterOnBack ? hip : back;
        }

        // The gun's local pose on its spot's bone. The rig's bones rest aligned with the character, so character axes
        // are the bone's local axes. The left gun of a pair is the mirror image, on the left hip.
        void SpotPose(Spot spot, WeaponSettings settings, bool mirrored, out Vector3 position, out Quaternion rotation)
        {
            position = spot.position + settings.holsterOffset;
            rotation = Quaternion.Euler(spot.euler) * Quaternion.Euler(settings.holsterEuler);
            if (!mirrored) return;
            position.x = -position.x;
            rotation = new Quaternion(rotation.x, -rotation.y, -rotation.z, rotation.w);
        }

        // Moves a gun's models (the second one is the left gun of a pair) into the hands. keepPose leaves them where they
        // are in the world, for the weapon controller to settle into the grip.
        void PlaceInHands(GameObject right, GameObject left, bool keepPose)
        {
            var rig = body ? body.RigAnimator : null;
            if (!rig) return;
            if (right) right.transform.SetParent(rig.GetBoneTransform(HumanBodyBones.RightHand), keepPose);
            if (left) left.transform.SetParent(rig.GetBoneTransform(HumanBodyBones.LeftHand), keepPose);
            if (settlingRight == right) bodySettle = 1f;
        }

        // Puts a gun's models on its holster spot. settle swings them there from where the hand let go.
        void PlaceOnBody(GameObject right, GameObject left, WeaponSettings settings, bool settle)
        {
            var rig = body ? body.RigAnimator : null;
            if (!rig || !settings) return;
            Transform bone = rig.GetBoneTransform(SpotFor(settings).bone);
            if (!bone) return;
            if (right) right.transform.SetParent(bone, true);
            if (left) left.transform.SetParent(bone, true);
            settlingRight = right;
            settlingLeft = left;
            settlingGun = settings;
            if (right) (settleFromRight, settleFromRightRotation) = (right.transform.localPosition, right.transform.localRotation);
            if (left) (settleFromLeft, settleFromLeftRotation) = (left.transform.localPosition, left.transform.localRotation);
            bodySettle = settle && settleTime > 0f ? 0f : 1f;
            SettleOnBody(0f);
        }

        void SettleOnBody(float dt)
        {
            if (!settlingGun) return;
            bodySettle = Mathf.MoveTowards(bodySettle, 1f, dt / Mathf.Max(0.01f, settleTime));
            float t = Mathf.SmoothStep(0f, 1f, bodySettle);
            Spot spot = SpotFor(settlingGun);
            if (settlingRight && settlingRight.transform.parent == body.RigAnimator.GetBoneTransform(spot.bone))
            {
                SpotPose(spot, settlingGun, false, out Vector3 position, out Quaternion rotation);
                settlingRight.transform.SetLocalPositionAndRotation(Vector3.Lerp(settleFromRight, position, t), Quaternion.Slerp(settleFromRightRotation, rotation, t));
            }
            if (settlingLeft && settlingLeft.transform.parent == body.RigAnimator.GetBoneTransform(spot.bone))
            {
                SpotPose(spot, settlingGun, true, out Vector3 position, out Quaternion rotation);
                settlingLeft.transform.SetLocalPositionAndRotation(Vector3.Lerp(settleFromLeft, position, t), Quaternion.Slerp(settleFromLeftRotation, rotation, t));
            }
            if (bodySettle >= 1f) settlingGun = null;
        }
    }
}
