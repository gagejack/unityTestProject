using UnityEngine;

namespace StarterGame
{
    [CreateAssetMenu(menuName = "Starter Game/Weapon Settings")]
    public sealed class WeaponSettings : ScriptableObject
    {
        public enum WeaponCategory { Pistols, SMGs, AssaultRifles, Shotguns, RiflesAndSnipers, MachineGuns, Launchers }
        public WeaponCategory category;
        public string creator;
        public string sourcePack;
        public enum TriggerMode { Automatic, SemiAutomatic, PumpAction }
        [System.Serializable]
        public sealed class SoldierClips
        {
            public AnimationClip aim;
            public AnimationClip shoot;
            public AnimationClip reload;
            public AnimationClip precisionAim;
            public AnimationClip precisionShoot;
            [Tooltip("Upper-body pose for sprinting with this gun; the legs keep the sprint and the support hand stays on the gun. Leave empty for a one-handed carry (arms swing with the sprint).")]
            public AnimationClip hold;
        }

        public string displayName;
        [Min(0)] public int price;
        [Min(1)] public int magazine = 12;
        [Min(0)] public int reserve = 60;
        [Min(0.05f)] public float fireRate = 0.24f;
        [Tooltip("Automatic fires while held. Semi automatic and pump action require a fresh click per shot.")]
        public TriggerMode triggerMode;
        public AudioClip pumpBackSound;
        public AudioClip pumpForwardSound;
        [Min(0)] public float damage = 24f;
        [Tooltip("Projectiles per trigger pull. Damage is per pellet; ammunition and firing sound are used once.")]
        [Range(1, 16)] public int pelletCount = 1;
        [Min(1)] public float range = 80f;
        [Header("Movement")]
        [Tooltip("Fraction of normal walking, running and crouching speed while equipped. Heavier weapons use lower values; 1 keeps full speed. Combines with aiming and shooting slowdowns.")]
        [Range(0f, 1f)] public float movementMultiplier = 1f;
        [Header("Model and animation")]
        [Range(0, 4)] public int animationFamily;
        public GameObject model;
        public Texture2D icon;
        [Tooltip("Sprite-sheet icon shown beside the equipped weapon's ammo in the HUD. Variants may share a sprite.")]
        public Sprite hudSprite;
        public Material wallOutlineMaterial;
        public SoldierClips animations = new SoldierClips();
        public Vector3 gripEuler;
        public Vector3 aimedGripEuler;
        [Header("Dual handguns")]
        public bool dualWield;
        public Vector3 leftGripEuler;
        public Vector3 leftAimedGripEuler;
        public Vector3 leftAimHandOffset = new Vector3(0f, 0.1f, -0.08f);
        [Tooltip("Right-hand shift while aiming, relative to the character: X sideways, Y upward, Z forward. Both arms follow the grips.")]
        public Vector3 aimHandOffset = new Vector3(0f, 0.24f, -0.06f);
        public Vector3 muzzleLocalPosition;
        public bool supportHand;
        public Vector3 supportHandLocalPosition;
        public Vector3 supportHandLocalEuler;
        [Header("Holster")]
        [Tooltip("Extra shift of this gun on its holster spot, in meters along the character: X right, Y up, Z forward. Zero uses the category's spot as it is.")]
        public Vector3 holsterOffset;
        [Tooltip("Extra rotation of this gun on its holster spot, in degrees around the gun's own axes (Z runs along the barrel, so Z 180 rolls it over).")]
        public Vector3 holsterEuler;
        [Tooltip("Ride on the back like a rifle even though this gun's category goes on the hip (for long guns such as SMGs with stocks).")]
        public bool holsterOnBack;
        [Header("Accuracy")]
        [Range(0, 15)] public float hipFireSpread = 3f;
        [Range(0, 5)] public float aimedSpread = 0.25f;
        [Min(0)] public float bloomPerShot = 0.55f;
        [Min(0)] public float maximumBloom = 4f;
        [Header("Feedback")]
        public AudioClip fireSound;
        [Tooltip("Play fireSound continuously while firing instead of starting a sound for each bullet.")]
        public bool loopFireSound;
        public AudioClip[] shotVariations = new AudioClip[0];
        public AudioClip[] shotTails = new AudioClip[0];
        [Tooltip("Reload stages played in order across the reload animation. Cancelling reload stops future stages.")]
        public AudioClip[] reloadStages = new AudioClip[0];
        public AudioClip[] equipSounds = new AudioClip[0];
        public AudioClip[] aimSounds = new AudioClip[0];
        public AudioClip emptySound;
        public AudioClip reloadSound;
        public AudioClip reloadCompleteSound;
        public AudioClip impactSound;
        [Range(0, 1)] public float soundVolume = 0.55f;
        [Tooltip("Pitch of this weapon's firing, reload, handling and mechanism sounds. Lower values sound deeper.")]
        [Range(0.5f, 1.5f)] public float shotPitch = 1f;
        public Color flashColor = new Color(1f, 0.72f, 0.22f);
        [Min(0.01f)] public float flashSize = 0.13f;
        [Tooltip("Camera kick in degrees: horizontal variation and upward kick.")]
        public Vector2 cameraKick = new Vector2(0.12f, 0.65f);
        [Min(0)] public float weaponKickDistance = 0.035f;
        [Min(0)] public float weaponKickAngle = 3f;
        [Min(0.01f)] public float kickTime = 0.04f;
        [Min(0.01f)] public float recoilRecovery = 0.14f;
        [Header("Rocket (bazooka)")]
        public bool firesRocket;
        [Min(1)] public float rocketSpeed = 24f;
        [Min(0.1f)] public float blastRadius = 3.5f;
        [Tooltip("Size of the explosion flash and smoke. Does not change damage range.")]
        [Min(0.1f)] public float explosionVisualScale = 2f;
        public AudioClip explosionSound;
    }
}
