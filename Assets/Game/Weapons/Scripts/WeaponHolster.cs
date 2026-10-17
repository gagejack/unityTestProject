using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StarterGame
{
    /// <summary>
    /// The holster: a second gun slot on the body. H swaps the gun in the hands with the holstered one (holsters it when
    /// the holster is empty, draws when the hands are empty); right mouse with empty hands draws. The weapon controller
    /// owns both guns and their ammo; this component reads the input and places the models. Where a holstered gun rides
    /// comes from its category: pistols and SMGs on the right hip (dual handguns on both hips), everything else across
    /// the back, plus the gun's own Holster Offset, Holster Euler and Holster On Back on its WeaponSettings asset.
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

        WeaponCombatController weaponsCache;
        MaleWalkRetarget bodyCache;
        // Looked up on first use: the weapon controller places its first gun in its own Awake, which can run before ours.
        WeaponCombatController weapons => weaponsCache ? weaponsCache : weaponsCache = GetComponent<WeaponCombatController>();
        MaleWalkRetarget body => bodyCache ? bodyCache : bodyCache = GetComponent<MaleWalkRetarget>();

        void Update()
        {
            if (!PlayerControl.Enabled) return;
            if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame) weapons.SwapWithHolster();
            else if (!weapons.HasWeapon && weapons.HolsteredWeapon && Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
                weapons.SwapWithHolster();
        }

        /// <summary>Which spot a gun rides on when holstered.</summary>
        public Spot SpotFor(WeaponSettings settings)
        {
            bool hipCategory = settings.category == WeaponSettings.WeaponCategory.Pistols || settings.category == WeaponSettings.WeaponCategory.SMGs;
            return hipCategory && !settings.holsterOnBack ? hip : back;
        }

        /// <summary>Puts a gun's models (the second one is the left gun of a pair) in the hands; the controller sets their grip each frame.</summary>
        internal void PlaceInHands(GameObject right, GameObject left)
        {
            var rig = body ? body.RigAnimator : null;
            if (!rig) return;
            if (right) right.transform.SetParent(rig.GetBoneTransform(HumanBodyBones.RightHand), false);
            if (left) left.transform.SetParent(rig.GetBoneTransform(HumanBodyBones.LeftHand), false);
        }

        /// <summary>Puts a gun's models on its holster spot, where they ride the bone with a fixed offset.</summary>
        internal void PlaceOnBody(GameObject right, GameObject left, WeaponSettings settings)
        {
            var rig = body ? body.RigAnimator : null;
            if (!rig || !settings) return;
            PlaceOnBody(right, rig, settings, false);
            PlaceOnBody(left, rig, settings, true);
        }

        void PlaceOnBody(GameObject model, Animator rig, WeaponSettings settings, bool mirrored)
        {
            if (!model) return;
            Spot spot = SpotFor(settings);
            Transform parent = rig.GetBoneTransform(spot.bone);
            if (!parent) return;
            model.transform.SetParent(parent, false);
            // The rig's bones rest aligned with the character, so character axes are the bone's local axes.
            Vector3 position = spot.position + settings.holsterOffset;
            Quaternion rotation = Quaternion.Euler(spot.euler) * Quaternion.Euler(settings.holsterEuler);
            if (mirrored)
            {
                // Mirror image across the body's middle: the left gun of a pair goes on the left hip.
                position.x = -position.x;
                rotation = new Quaternion(rotation.x, -rotation.y, -rotation.z, rotation.w);
            }
            model.transform.SetLocalPositionAndRotation(position, rotation);
        }
    }
}
