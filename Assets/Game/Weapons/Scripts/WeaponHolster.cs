using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StarterGame
{
    /// <summary>
    /// Stows the equipped gun on the body and draws it again: H toggles, right mouse draws while holstered. A holstered
    /// gun is still the equipped gun (it keeps its ammo) but the player is unarmed, so punches and kicks work. Where the
    /// gun rides comes from its category: pistols and SMGs on the right hip (dual handguns on both hips), everything
    /// else across the back, plus the gun's own Holster Offset, Holster Euler and Holster On Back on its WeaponSettings asset.
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
        }

        [Tooltip("Pistols and SMGs: on the right hip, muzzle down along the thigh. Dual handguns also use its mirror image on the left hip.")]
        [SerializeField] private Spot hip = new Spot { bone = HumanBodyBones.Hips, position = new Vector3(0.3f, 0.03f, 0f), euler = new Vector3(90f, 0f, 0f) };
        [Tooltip("Assault rifles, shotguns, rifles and snipers, machine guns and launchers: across the back, muzzle up over the right shoulder.")]
        [SerializeField] private Spot back = new Spot { bone = HumanBodyBones.Chest, position = new Vector3(-0.1f, -0.3f, -0.17f), euler = new Vector3(-60f, 90f, 0f) };

        /// <summary>True while the equipped gun is on the body instead of in the hands.</summary>
        public bool IsHolstered { get; private set; }

        WeaponCombatController weaponsCache;
        MaleWalkRetarget bodyCache;
        // Looked up on first use: the weapon controller places its first gun in its own Awake, which can run before ours.
        WeaponCombatController weapons => weaponsCache ? weaponsCache : weaponsCache = GetComponent<WeaponCombatController>();
        MaleWalkRetarget body => bodyCache ? bodyCache : bodyCache = GetComponent<MaleWalkRetarget>();

        void Update()
        {
            if (!weapons.OwnsWeapon) { IsHolstered = false; return; }
            if (!PlayerControl.Enabled) return;
            if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame) SetHolstered(!IsHolstered);
            else if (IsHolstered && Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame) SetHolstered(false);
        }

        /// <summary>Puts the equipped gun on the body (true) or in the hands (false).</summary>
        public void SetHolstered(bool holstered)
        {
            if (!weapons.OwnsWeapon || holstered == IsHolstered) return;
            IsHolstered = holstered;
            Place();
            weapons.OnHolsterChanged(holstered);
        }

        /// <summary>A newly equipped gun starts in the hands.</summary>
        internal void ShowInHand()
        {
            IsHolstered = false;
            Place();
        }

        // Parents the gun models to their holster bones or back to the hands. In the hands the weapon controller sets
        // their grip every frame; on the body they ride the bone with the spot's fixed offset.
        void Place()
        {
            var rig = body ? body.RigAnimator : null;
            var settings = weapons.CurrentWeapon;
            if (!rig || !settings) return;
            Place(weapons.HeldModel, rig, settings, HumanBodyBones.RightHand, false);
            Place(weapons.OffhandModel, rig, settings, HumanBodyBones.LeftHand, true);
        }

        void Place(GameObject model, Animator rig, WeaponSettings settings, HumanBodyBones hand, bool mirrored)
        {
            if (!model) return;
            bool hipCategory = settings.category == WeaponSettings.WeaponCategory.Pistols || settings.category == WeaponSettings.WeaponCategory.SMGs;
            Spot spot = hipCategory && !settings.holsterOnBack ? hip : back;
            Transform parent = rig.GetBoneTransform(IsHolstered ? spot.bone : hand);
            if (!parent) return;
            model.transform.SetParent(parent, false);
            if (!IsHolstered) return;
            // The rig's bones rest aligned with the character, so character axes are the bone's local axes.
            Vector3 position = spot.position + settings.holsterOffset;
            Quaternion rotation = Quaternion.Euler(spot.euler) * Quaternion.Euler(settings.holsterEuler);
            if (mirrored)
            {
                // Mirror image across the body's middle: the left-hand gun of a pair goes on the left hip.
                position.x = -position.x;
                rotation = new Quaternion(rotation.x, -rotation.y, -rotation.z, rotation.w);
            }
            model.transform.SetLocalPositionAndRotation(position, rotation);
        }
    }
}
