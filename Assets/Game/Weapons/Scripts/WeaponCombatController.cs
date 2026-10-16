using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace StarterGame
{
    /// <summary>Camera-directed, muzzle-origin weapons with deliberate pickups and editable settings.</summary>
    [DefaultExecutionOrder(-25)]
    public sealed class WeaponCombatController : MonoBehaviour
    {
        [Header("Weapon catalogue")]
        [SerializeField] private WeaponSettings[] offers = new WeaponSettings[5];
        [Header("Starting weapon")]
        [SerializeField] private int startingPoints = 10000;
        // Kept serialized for scene migration; the equipped settings asset supplies these values.
        [SerializeField, HideInInspector] private float fireRate = 0.22f;
        [SerializeField, HideInInspector] private float range = 80f;
        [SerializeField, HideInInspector] private float damage = 25f;
        [SerializeField, HideInInspector] private int magazineSize = 12;
        [SerializeField, HideInInspector] private int startingReserveAmmo = 60;
        [Header("Shooting movement")]
        [Tooltip("Fraction of normal movement speed while aiming. Shooting applies its additional slowdown afterward.")]
        [SerializeField, Range(0.1f, 1f)] private float aimingMoveSpeedMultiplier = 0.6f;
        [Tooltip("Additional movement multiplier while firing, combined with the equipped weapon's Movement Multiplier.")]
        [SerializeField, Range(0f, 1f)] private float shootingMoveSpeedMultiplier = 0.75f;
        [SerializeField, Min(0f)] private float shootingSlowdownHoldTime = 0.12f;
        [SerializeField, Min(0.01f)] private float shootingRecoveryTime = 0.15f;
        [SerializeField, Min(0.01f)] private float combatSlowdownTime = 0.05f;
        [Header("Recoil and support hand")]
        [SerializeField, Range(0f, 1f)] private float weaponRecoilScale = 1f;
        [SerializeField, Range(0f, 1f)] private float supportHandWeight = 1f;
        [Header("Aiming and accuracy")]
        [Tooltip("Half-angle of the unaimed shot cone, in degrees.")]
        [SerializeField, HideInInspector] private float hipFireSpread = 3f;
        [SerializeField, HideInInspector] private float aimedSpread = 0.25f;
        [SerializeField, HideInInspector] private float bloomPerShot = 0.55f;
        [SerializeField, HideInInspector] private float maximumBloom = 4f;
        [SerializeField, Min(0f)] private float bloomRecoveryPerSecond = 4f;
        [SerializeField, Range(0f, 1f)] private float aimedBloomMultiplier = 0.2f;
        [SerializeField, Min(0.01f)] private float aimBlendTime = 0.14f;
        [SerializeField, HideInInspector] private Vector3 pistolGripEuler;
        [SerializeField, HideInInspector] private Vector3 pistolAimedGripEuler;
        [SerializeField, HideInInspector] private Vector3 assaultRifleGripEuler;
        [SerializeField, HideInInspector] private Vector3 rifleGripEuler;
        [SerializeField, HideInInspector] private Vector3 bazookaGripEuler;
        [Header("Soldier pack models")]
        [SerializeField, Range(0.5f, 2f)] private float weaponModelScale = 1.2f;
        [SerializeField, HideInInspector] private GameObject pistolModel;
        [SerializeField, HideInInspector] private GameObject assaultRifleModel;
        [SerializeField, HideInInspector] private GameObject rifleModel;
        [SerializeField, HideInInspector] private GameObject bazookaModel;
        [SerializeField] private Material soldierWeaponMaterial;
        [SerializeField, HideInInspector] private Texture2D[] weaponIcons = new Texture2D[4];
        [SerializeField] private Material[] wallOutlineMaterials = new Material[4];
        // Retained only so the editor migration can remove an existing physical armory.
        [SerializeField, HideInInspector] private Transform shopRoot;

        private int points;
        private int ammoInMagazine;
        private int reserveAmmo;
        private int equippedOffer;
        private float nextShotTime;
        private float reloadEndsAt;
        private float reloadStartedAt;
        private float reloadDuration;
        private int nextReloadStage;
        private bool reloading;
        private MaleWalkRetarget characterAnimations;
        private GameObject heldWeapon;
        private GameObject heldWeaponLeft;
        private bool nextShotLeft;
        private bool pendingShotLeft;
        private float leftRecoil;
        private float leftRecoilTarget;
        private float leftRecoilVelocity;
        private float leftRecoilHoldUntil;
        public bool IsDualWielding => HasWeapon && CurrentWeapon && CurrentWeapon.dualWield && heldWeaponLeft;
        public bool LastShotUsedLeftHand { get; private set; }
        private Transform weaponHand;
        private Animator poseAnimator;
        private Transform rightUpperArm, rightLowerArm, rightHand;
        private Transform leftUpperArm, leftLowerArm, leftHand;
        private Text pointsText;
        private Canvas weaponHud;
        private Text ammoText;
        private Text weaponText;
        private Text promptText;
        private RawImage gunIconImage;
        private RectTransform[] crosshairParts = new RectTransform[4];
        private Image[] crosshairImages = new Image[4];
        private float crosshairGap = 20f;
        private float bloom;
        private CapsuleMovement movement;
        private FollowCamera cameraView;
        private ViewModeDebugMenu viewMenu;
        private RectTransform ammoFill;
        private Camera aimCamera;
        private CombatFeedback feedback;
        private bool pendingShot;
        private float nextEmptySound;
        private float shootingSpeedWeight = 1f;
        private float recoil;
        private float recoilTarget;
        private float recoilVelocity;
        private float recoilHoldUntil;
        private float gripWeight;
        private readonly Collider[] pickupHits = new Collider[24];
        private DroppedWeapon nearbyPickup;
        private float nextPickupScan;
        private float shotTimeBeforePending = float.NegativeInfinity;
        private MysteryWeaponBox[] mysteryBoxes;
        private float promptUntil;
        private bool pumpBackPlayed, pumpForwardPlayed;
        public int Points => points;
        public IReadOnlyList<WeaponSettings> WeaponOffers => offers;
        public WeaponSettings CurrentWeapon => offers != null && equippedOffer >= 0 && equippedOffer < offers.Length ? offers[equippedOffer] : null;
        public Vector3 MuzzlePosition => heldWeapon && CurrentWeapon ? heldWeapon.transform.TransformPoint(CurrentWeapon.muzzleLocalPosition) : transform.position + Vector3.up;
        public Vector3 OffhandMuzzlePosition => heldWeaponLeft && CurrentWeapon ? heldWeaponLeft.transform.TransformPoint(CurrentWeapon.muzzleLocalPosition) : MuzzlePosition;
        public int MagazineAmmo => ammoInMagazine;
        public int ReserveAmmo => reserveAmmo;
        public WeaponSettings.SoldierClips AnimationSetForFamily(int family)
        {
            if (offers != null) foreach (var offer in offers)
                if (offer && offer.animationFamily == family) return offer.animations;
            return null;
        }

        public bool HasWeapon => isActiveAndEnabled && heldWeapon != null;
        public bool IsAiming { get; private set; }
        public float AimWeight { get; private set; }
        public float CurrentSpreadDegrees => Mathf.Lerp(hipFireSpread, aimedSpread, AimWeight)
            + bloom * Mathf.Lerp(1f, aimedBloomMultiplier, AimWeight);
        public Vector3 AimWorldDirection => cameraView && cameraView.ThirdPerson ? cameraView.AimDirection : aimCamera ? aimCamera.transform.forward : transform.forward;
        public Vector3 AimPlanarForward => cameraView && cameraView.ThirdPerson
            ? cameraView.PlanarForward : Vector3.ProjectOnPlane(AimWorldDirection, Vector3.up).normalized;
        public int SoldierAnimationFamily => CurrentWeapon ? CurrentWeapon.animationFamily : 0;
        public bool IsReloading => HasWeapon && reloading;
        public float LastShotTime { get; private set; } = float.NegativeInfinity;
        public float ReloadProgress => reloading ? Mathf.Clamp01((Time.time - reloadStartedAt) / Mathf.Max(0.01f, reloadDuration)) : 0f;
        public float ShootingMovementMultiplier => HasWeapon ? shootingSpeedWeight : 1f;
        public float EquippedMovementMultiplier => HasWeapon && CurrentWeapon ? Mathf.Clamp01(CurrentWeapon.movementMultiplier) : 1f;
        public bool IsRecoveringFromShot => HasWeapon && Time.time - LastShotTime < shootingSlowdownHoldTime + shootingRecoveryTime;
        public float AimingMovementMultiplier => HasWeapon ? Mathf.Lerp(1f, aimingMoveSpeedMultiplier, AimWeight) : 1f;

        public float FireRate
        {
            get => fireRate;
            set => fireRate = Mathf.Max(0.05f, value);
        }

        private void Awake()
        {
            if (offers == null || offers.Length == 0 || System.Array.Exists(offers, item => !item))
            {
                Debug.LogError("Assign all Weapon Settings on Player > Weapon Combat Controller (Tools > Combat Feel > Set Up).", this);
                enabled = false;
                return;
            }
            points = Mathf.Max(0, startingPoints);
            equippedOffer = 0;
            ammoInMagazine = Mathf.Max(1, magazineSize);
            reserveAmmo = Mathf.Max(0, startingReserveAmmo);
            aimCamera = Camera.main;
            cameraView = aimCamera ? aimCamera.GetComponent<FollowCamera>() : null;
            viewMenu = aimCamera ? aimCamera.GetComponent<ViewModeDebugMenu>() : null;
            movement = GetComponent<CapsuleMovement>();
            characterAnimations = GetComponent<MaleWalkRetarget>();
            feedback = GetComponent<CombatFeedback>();
            mysteryBoxes = FindObjectsByType<MysteryWeaponBox>();
            ApplySettings(offers[0]);
            ammoInMagazine = magazineSize;
            reserveAmmo = startingReserveAmmo;
            BuildHud();
            AttachWeapon();
            RefreshHud();
            Game.Core.PlayerRequests.RefillAmmoRequested += RefillAmmo;
        }

        private void RefillAmmo()
        {
            if (!HasWeapon) return;
            reloading = false;
            ammoInMagazine = CurrentMagazineSize;
            reserveAmmo = Mathf.Max(0, CurrentWeapon.reserve);
            RefreshHud();
        }

        private void Update()
        {
            bool overMenu = Cursor.lockState != CursorLockMode.Locked && viewMenu && Mouse.current != null && viewMenu.ContainsPointer(Mouse.current.position.ReadValue());
            bool controlsEnabled = (!movement || movement.PlayerControlEnabled) && (!viewMenu || !viewMenu.IsOpen);
            SetAiming(controlsEnabled && Application.isFocused && !overMenu && Mouse.current != null && Mouse.current.rightButton.isPressed);
            AimWeight = Mathf.MoveTowards(AimWeight, IsAiming ? 1f : 0f, Time.deltaTime / Mathf.Max(0.01f, aimBlendTime));
            bloom = Mathf.MoveTowards(bloom, 0f, bloomRecoveryPerSecond * Time.deltaTime);
            UpdateShootingSlowdown(Time.deltaTime);
            ScanPickups();
            bool gameplayInput = controlsEnabled && Application.isFocused && !overMenu;
            if (gameplayInput && Keyboard.current != null)
            {
                if (Keyboard.current.fKey.wasPressedThisFrame)
                {
                    var box = NearbyMysteryBox();
                    if (box) box.TryInteract(this);
                    else if (nearbyPickup && !HasWeapon) nearbyPickup.TryCollect(this);
                }
                if (Keyboard.current.rKey.wasPressedThisFrame) BeginReload();
                if (Keyboard.current.qKey.wasPressedThisFrame) DropWeapon();
            }
            if (gameplayInput && Mouse.current != null)
                HandleFireInput(Mouse.current.leftButton.isPressed, Mouse.current.leftButton.wasPressedThisFrame);
            else if (feedback) feedback.StopFiringSound();
            UpdatePumpSounds();
            if (reloading)
            {
                PlayReloadStages();
                if (Time.time >= reloadEndsAt) FinishReload();
            }
            UpdateInteractionPrompt();
            UpdateCrosshair();
        }

        private void SetAiming(bool requested)
        {
            bool wasAiming = IsAiming;
            IsAiming = requested && HasWeapon && !reloading &&
                (!movement || (!movement.IsRolling && !movement.IsDiving && !movement.IsSliding));
            if (IsAiming != wasAiming && HasWeapon && feedback)
                feedback.Handling(CurrentWeapon.aimSounds, MuzzlePosition, CurrentWeapon.shotPitch);
        }

        private void OnDisable()
        {
            if (feedback) feedback.StopFiringSound();
            IsAiming = false;
            AimWeight = 0f;
            pendingShot = false;
            shootingSpeedWeight = 1f;
        }

        private void OnDestroy()
        {
            Game.Core.PlayerRequests.RefillAmmoRequested -= RefillAmmo;
            if (weaponHud) Destroy(weaponHud.gameObject);
        }

        private Vector3 SpreadDirection(Vector3 forward)
        {
            Vector3 reference = Mathf.Abs(forward.y) > 0.99f ? Vector3.forward : Vector3.up;
            Vector3 right = Vector3.Cross(reference, forward).normalized;
            Vector3 up = Vector3.Cross(forward, right);
            Vector2 sample = Random.insideUnitCircle * Mathf.Tan(CurrentSpreadDegrees * Mathf.Deg2Rad);
            return (forward + right * sample.x + up * sample.y).normalized;
        }

        private void TryFire()
        {
            if (movement && !movement.PlayerControlEnabled) return;
            if (reloading || !HasWeapon || pendingShot || Time.time < nextShotTime) return;
            if (movement && (movement.IsRolling || movement.IsDiving || movement.IsSliding)) return;
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;
            if (Cursor.lockState != CursorLockMode.Locked && viewMenu && Mouse.current != null && viewMenu.ContainsPointer(Mouse.current.position.ReadValue())) return;
            if (ammoInMagazine <= 0)
            {
                if (Time.time >= nextEmptySound)
                {
                    if (feedback) feedback.PlayGunSound(CurrentWeapon.emptySound, MuzzlePosition, 0.4f, false, 0f, CurrentWeapon.shotPitch);
                    nextEmptySound = Time.time + 0.3f;
                }
                BeginReload();
                return;
            }
            nextShotTime = Time.time + Mathf.Max(0.05f, fireRate);
            shotTimeBeforePending = LastShotTime;
            LastShotTime = Time.time;
            ammoInMagazine--;
            pendingShotLeft = IsDualWielding && nextShotLeft;
            pendingShot = true;
            pumpBackPlayed = pumpForwardPlayed = false;
            RefreshHud();
        }

        internal void HandleFireInput(bool held, bool pressed)
        {
            if (!held && feedback) feedback.StopFiringSound();
            if (!HasWeapon) return;
            if (CurrentWeapon.triggerMode == WeaponSettings.TriggerMode.Automatic ? held : pressed) TryFire();
        }

        private void UpdatePumpSounds()
        {
            if (!HasWeapon || IsReloading || CurrentWeapon.triggerMode != WeaponSettings.TriggerMode.PumpAction) return;
            float elapsed = Time.time - LastShotTime;
            if (elapsed >= fireRate || elapsed < 0f) return;
            if (!pumpBackPlayed && elapsed >= fireRate * 0.25f)
            {
                pumpBackPlayed = true;
                if (feedback) feedback.PlayGunSound(CurrentWeapon.pumpBackSound, MuzzlePosition, 0.45f, false, 0f, CurrentWeapon.shotPitch);
            }
            if (!pumpForwardPlayed && elapsed >= fireRate * 0.55f)
            {
                pumpForwardPlayed = true;
                if (feedback) feedback.PlayGunSound(CurrentWeapon.pumpForwardSound, MuzzlePosition, 0.45f, false, 0f, CurrentWeapon.shotPitch);
            }
        }

        internal void ResolvePendingShot()
        {
            if (!pendingShot) return;
            pendingShot = false;
            if (!HasWeapon) return;
            if (movement && (!movement.PlayerControlEnabled || movement.IsRolling || movement.IsDiving || movement.IsSliding))
            {
                ammoInMagazine = Mathf.Min(CurrentMagazineSize, ammoInMagazine + 1);
                LastShotTime = shotTimeBeforePending;
                nextShotTime = Time.time;
                RefreshHud();
                return;
            }
            Vector3 cameraOrigin = aimCamera ? aimCamera.transform.position : transform.position + Vector3.up;
            Vector3 direction = SpreadDirection(AimWorldDirection);
            Vector3 target = cameraOrigin + direction * range;
            if (CombatPhysics.Cast(cameraOrigin, direction, range, transform, out var cameraHit)) target = cameraHit.point;
            LastShotUsedLeftHand = IsDualWielding && pendingShotLeft;
            nextShotLeft = IsDualWielding && !LastShotUsedLeftHand;
            Vector3 muzzle = LastShotUsedLeftHand ? OffhandMuzzlePosition : MuzzlePosition;
            Vector3 toTarget = target - muzzle;
            Vector3 shotDirection = toTarget.sqrMagnitude > 0.001f ? toTarget.normalized : direction;
            var body = GetComponent<CharacterController>();
            Vector3 safetyOrigin = body ? body.bounds.center : transform.position + Vector3.up * 0.7f;
            bool blocked = CombatPhysics.MuzzleObstruction(safetyOrigin, muzzle, transform, out var hit);
            if (blocked) muzzle = hit.point + hit.normal * 0.025f;
            if (feedback) feedback.Shot(CurrentWeapon, muzzle, shotDirection);
            if (cameraView) cameraView.AddRecoil(CurrentWeapon.cameraKick, CurrentWeapon.kickTime, CurrentWeapon.recoilRecovery);
            if (LastShotUsedLeftHand)
            {
                leftRecoilTarget = Mathf.Min(leftRecoilTarget + 1f, 2.5f);
                leftRecoilHoldUntil = Time.time + CurrentWeapon.kickTime;
            }
            else
            {
                recoilTarget = Mathf.Min(recoilTarget + 1f, 2.5f);
                recoilHoldUntil = Time.time + CurrentWeapon.kickTime;
            }
            if (CurrentWeapon.firesRocket && !blocked)
                RocketProjectile.Launch(CurrentWeapon, transform, feedback, muzzle, shotDirection);
            else
            {
                bool didHit = blocked || CombatPhysics.Cast(muzzle, shotDirection, Mathf.Min(toTarget.magnitude + 0.03f, range), transform, out hit);
                Vector3 end = didHit ? hit.point : muzzle + shotDirection * Mathf.Min(toTarget.magnitude, range);
                if (CurrentWeapon.firesRocket)
                {
                    var rocket = RocketProjectile.Launch(CurrentWeapon, transform, feedback, muzzle, shotDirection);
                    rocket.Explode(muzzle);
                }
                else
                {
                    ApplyPelletHit(muzzle, end, didHit, hit);
                    for (int pellet = 1; pellet < Mathf.Clamp(CurrentWeapon.pelletCount, 1, 16); pellet++)
                    {
                        Vector3 pelletDirection = SpreadDirection(AimWorldDirection);
                        Vector3 pelletTarget = cameraOrigin + pelletDirection * range;
                        if (CombatPhysics.Cast(cameraOrigin, pelletDirection, range, transform, out var pelletCameraHit))
                            pelletTarget = pelletCameraHit.point;
                        Vector3 path = pelletTarget - muzzle;
                        RaycastHit pelletHit = hit;
                        bool pelletDidHit = blocked || CombatPhysics.Cast(muzzle, path.normalized,
                            Mathf.Min(path.magnitude + 0.03f, range), transform, out pelletHit);
                        ApplyPelletHit(muzzle, pelletDidHit ? pelletHit.point : muzzle + path.normalized * Mathf.Min(path.magnitude, range), pelletDidHit, pelletHit);
                    }
                }
            }
            bloom = Mathf.Min(maximumBloom, bloom + bloomPerShot);
        }

        private void ApplyPelletHit(Vector3 muzzle, Vector3 end, bool didHit, RaycastHit hit)
        {
            if (didHit)
            {
                var health = hit.collider.GetComponentInParent<TargetHealth>();
                bool accepted = health && health.TakeDamage(damage);
                if (feedback) feedback.Impact(hit.point, hit.normal, accepted, CurrentWeapon.impactSound);
            }
            CombatFeedback.Tracer(muzzle, end);
        }

        private void UpdateShootingSlowdown(float dt)
        {
            float sinceShot = Time.time - LastShotTime;
            float recovery = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(shootingSlowdownHoldTime,
                shootingSlowdownHoldTime + Mathf.Max(0.01f, shootingRecoveryTime), sinceShot));
            float target = HasWeapon ? Mathf.Lerp(shootingMoveSpeedMultiplier, 1f, recovery) : 1f;
            shootingSpeedWeight = Mathf.MoveTowards(shootingSpeedWeight, target,
                dt * Mathf.Max(0.01f, 1f - shootingMoveSpeedMultiplier) / Mathf.Max(0.01f, target < shootingSpeedWeight ? combatSlowdownTime : shootingRecoveryTime));
        }

        private void BeginReload()
        {
            if (!heldWeapon || reloading || ammoInMagazine >= CurrentMagazineSize || reserveAmmo <= 0) return;
            reloading = true;
            if (feedback) feedback.StopFiringSound();
            pendingShot = false;
            SetAiming(false);
            reloadDuration = characterAnimations ? characterAnimations.SoldierReloadDuration(SoldierAnimationFamily) : 1.25f;
            reloadStartedAt = Time.time;
            reloadEndsAt = reloadStartedAt + reloadDuration;
            nextReloadStage = 0;
            if (CurrentWeapon.reloadStages == null || CurrentWeapon.reloadStages.Length == 0)
            {
                if (feedback) feedback.PlayGunSound(CurrentWeapon.reloadSound, MuzzlePosition, 0.45f, false, 0f, CurrentWeapon.shotPitch);
            }
            else PlayReloadStages();
            RefreshHud();
        }

        private void PlayReloadStages()
        {
            if (!reloading || !HasWeapon) return;
            var stages = CurrentWeapon ? CurrentWeapon.reloadStages : null;
            if (stages == null) return;
            while (nextReloadStage < stages.Length && ReloadProgress >=
                (stages.Length <= 1 ? 0f : nextReloadStage * 0.9f / (stages.Length - 1)))
            {
                if (feedback) feedback.PlayGunSound(stages[nextReloadStage], MuzzlePosition, 0.45f, false, 0f, CurrentWeapon.shotPitch);
                nextReloadStage++;
            }
        }

        private void FinishReload()
        {
            int needed = CurrentMagazineSize - ammoInMagazine;
            int loaded = Mathf.Min(needed, reserveAmmo);
            ammoInMagazine += loaded;
            reserveAmmo -= loaded;
            reloading = false;
            if (feedback && (CurrentWeapon.reloadStages == null || CurrentWeapon.reloadStages.Length == 0))
                feedback.PlayGunSound(CurrentWeapon.reloadCompleteSound, MuzzlePosition, 0.4f, false, 0f, CurrentWeapon.shotPitch);
            RefreshHud();
        }

        private MysteryWeaponBox NearbyMysteryBox()
        {
            MysteryWeaponBox nearest = null;
            float distance = float.PositiveInfinity;
            if (mysteryBoxes == null) return null;
            foreach (var box in mysteryBoxes)
            {
                if (!box || !box.CanReach(this)) continue;
                float next = (box.transform.position - transform.position).sqrMagnitude;
                if (next < distance) { distance = next; nearest = box; }
            }
            return nearest;
        }

        public bool TrySpendPoints(int amount)
        {
            if (amount < 0 || points < amount) return false;
            points -= amount;
            RefreshHud();
            return true;
        }

        public bool ReceiveMysteryWeapon(WeaponSettings weapon)
        {
            int slot = System.Array.IndexOf(offers, weapon);
            if (slot < 0 || !weapon || !weapon.model) return false;
            if (HasWeapon) DropWeapon();
            equippedOffer = slot;
            ApplySettings(weapon);
            ammoInMagazine = weapon.magazine;
            reserveAmmo = weapon.reserve;
            AttachWeapon();
            RefreshHud();
            return true;
        }

        /// <summary>Equip a catalogue weapon freely, with its normal ammunition.</summary>
        public bool EquipFromDebug(int index)
        {
            if (!Application.isPlaying || !isActiveAndEnabled || index < 0 || index >= offers.Length || !offers[index] || !offers[index].model) return false;
            equippedOffer = index;
            ApplySettings(offers[index]);
            ammoInMagazine = offers[index].magazine;
            reserveAmmo = offers[index].reserve;
            AttachWeapon();
            ShowPrompt(offers[index].displayName + " EQUIPPED");
            return HasWeapon;
        }

        /// <summary>Place a full-ammo pickup on clear ground near the player.</summary>
        public bool SpawnFromDebug(int index, out DroppedWeapon pickup, out string status)
        {
            pickup = null;
            status = "This weapon is unavailable.";
            if (!Application.isPlaying || !isActiveAndEnabled || index < 0 || index >= offers.Length || !offers[index] || !offers[index].model) return false;
            var weapon = offers[index];
            var dropped = new GameObject(weapon.displayName + " (debug pickup)");
            dropped.transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 90f);
            int models = weapon.dualWield ? 2 : 1;
            for (int i = 0; i < models; i++)
            {
                var model = Instantiate(weapon.model, dropped.transform, false);
                model.transform.localScale = weapon.model.transform.localScale * weaponModelScale;
                model.transform.localPosition = models == 2 ? Vector3.right * (i == 0 ? -.15f : .15f) : Vector3.zero;
                foreach (var collider in model.GetComponentsInChildren<Collider>()) collider.enabled = false;
            }
            var renderers = dropped.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { Destroy(dropped); return false; }
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            Vector3 halfSize = bounds.extents + Vector3.one * .03f;
            var controller = GetComponent<CharacterController>();
            Vector3 floor = transform.position;
            if (controller) floor.y = controller.bounds.min.y;
            bool found = false;
            // Test the front first, then nearby alternatives; never place through a wall.
            for (int i = 0; i < 16; i++)
            {
                int step = i % 8;
                float angle = step == 0 ? 0f : ((step + 1) / 2) * 45f * (step % 2 == 1 ? 1f : -1f);
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * transform.forward;
                float distance = i < 8 ? 1.6f : 2.3f;
                Vector3 origin = floor + Vector3.up * .6f;
                if (CombatPhysics.Cast(origin, direction, distance, transform, out _, .2f)) continue;
                Vector3 candidate = floor + direction * distance;
                if (!Physics.Raycast(candidate + Vector3.up * 1.5f, Vector3.down, out var ground, 3.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) || ground.normal.y < .65f) continue;
                Vector3 center = ground.point + Vector3.up * (halfSize.y + .08f);
                if (Physics.CheckBox(center, halfSize, Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                dropped.transform.position += center - bounds.center;
                found = true;
                break;
            }
            if (!found)
            {
                Destroy(dropped);
                status = "No clear ground nearby. Move to an open area and try again.";
                return false;
            }
            // Fit one body collider around both guns, including dual-handgun pickups.
            Bounds localBounds = new Bounds();
            bool first = true;
            foreach (var renderer in renderers)
            {
                var world = renderer.bounds;
                for (int c = 0; c < 8; c++)
                {
                    Vector3 corner = dropped.transform.InverseTransformPoint(world.center + Vector3.Scale(world.extents,
                        new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1)));
                    if (first) { localBounds = new Bounds(corner, Vector3.zero); first = false; }
                    else localBounds.Encapsulate(corner);
                }
            }
            var bodyCollider = dropped.AddComponent<BoxCollider>();
            bodyCollider.center = localBounds.center;
            bodyCollider.size = localBounds.size + Vector3.one * .02f;
            var trigger = dropped.AddComponent<SphereCollider>();
            trigger.center = localBounds.center;
            trigger.radius = .65f;
            trigger.isTrigger = true;
            var body = dropped.AddComponent<Rigidbody>();
            body.mass = 1f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            pickup = dropped.AddComponent<DroppedWeapon>();
            pickup.Initialize(index, weapon, weapon.magazine, weapon.reserve);
            if (controller)
                foreach (var collider in dropped.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(controller, collider);
            status = "Spawned " + weapon.displayName + ". Press Q to free your hands, then F to pick it up.";
            return true;
        }

        private void UpdateInteractionPrompt()
        {
            if (Time.time < promptUntil) return;
            var box = NearbyMysteryBox();
            if (box) { promptText.text = box.PromptFor(this); return; }
            if (nearbyPickup && !HasWeapon)
            {
                promptText.text = "F  TO PICK UP  " + nearbyPickup.DisplayName + "  •  " + nearbyPickup.Magazine + " / " + nearbyPickup.Reserve;
                return;
            }
            promptText.text = "";
        }

        private void ShowPrompt(string message)
        {
            promptText.text = message;
            promptUntil = Time.time + 1.5f;
            CancelInvoke(nameof(ClearPrompt));
            Invoke(nameof(ClearPrompt), 1.5f);
        }

        private void ClearPrompt() { if (promptText) promptText.text = ""; }

        private void DropWeapon()
        {
            if (feedback) feedback.StopFiringSound();
            if (!heldWeapon) return;
            if (feedback) feedback.Handling(CurrentWeapon.equipSounds, MuzzlePosition, CurrentWeapon.shotPitch);
            GameObject dropped = heldWeapon;
            if (heldWeaponLeft)
            {
                dropped = new GameObject(CurrentWeapon.displayName + " (dropped pair)");
                heldWeapon.transform.SetParent(dropped.transform, false);
                heldWeapon.transform.localPosition = Vector3.left * 0.1f;
                heldWeapon.transform.localRotation = Quaternion.identity;
                heldWeaponLeft.transform.SetParent(dropped.transform, false);
                heldWeaponLeft.transform.localPosition = Vector3.right * 0.1f;
                heldWeaponLeft.transform.localRotation = Quaternion.identity;
            }
            heldWeapon = null;
            heldWeaponLeft = null;
            IsAiming = false;
            AimWeight = 0f;
            bloom = 0f;
            reloading = false;
            pendingShot = false;
            shootingSpeedWeight = 1f;
            recoil = recoilTarget = recoilVelocity = gripWeight = 0f;
            leftRecoil = leftRecoilTarget = leftRecoilVelocity = 0f;
            LastShotTime = float.NegativeInfinity;
            dropped.transform.SetParent(null, true);
            dropped.transform.position = transform.position + transform.forward * 0.8f + Vector3.up * 0.8f;
            dropped.transform.rotation = transform.rotation * Quaternion.Euler(15f, 0f, 0f);
            foreach (Collider c in dropped.GetComponentsInChildren<Collider>()) c.enabled = true;
            BoxCollider pickupBodyCollider = dropped.GetComponent<BoxCollider>();
            bool hasFittedCollider = pickupBodyCollider;
            if (!pickupBodyCollider) pickupBodyCollider = dropped.AddComponent<BoxCollider>();
            var mesh = dropped.GetComponentInChildren<MeshFilter>();
            if (CurrentWeapon.dualWield)
            {
                Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
                foreach (var renderer in dropped.GetComponentsInChildren<Renderer>())
                {
                    var world = renderer.bounds;
                    for (int corner = 0; corner < 8; corner++)
                        bounds.Encapsulate(dropped.transform.InverseTransformPoint(world.center + Vector3.Scale(world.extents,
                            new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1))));
                }
                pickupBodyCollider.center = bounds.center;
                pickupBodyCollider.size = bounds.size + Vector3.one * 0.02f;
            }
            else if (!hasFittedCollider && mesh && mesh.sharedMesh)
            {
                pickupBodyCollider.center = mesh.sharedMesh.bounds.center;
                pickupBodyCollider.size = mesh.sharedMesh.bounds.size + Vector3.one * 0.035f;
            }
            else if (!hasFittedCollider) pickupBodyCollider.size = new Vector3(0.3f, 0.3f, 0.95f);
            SphereCollider pickupTrigger = dropped.AddComponent<SphereCollider>();
            pickupTrigger.radius = 0.7f;
            pickupTrigger.isTrigger = true;
            Rigidbody body = dropped.AddComponent<Rigidbody>();
            body.mass = 1f;
            body.AddForce(transform.forward * 2.5f + Vector3.up * 2f, ForceMode.VelocityChange);
            dropped.AddComponent<DroppedWeapon>().Initialize(equippedOffer, CurrentWeapon, ammoInMagazine, reserveAmmo);
            foreach (Collider collider in dropped.GetComponentsInChildren<Collider>())
                Physics.IgnoreCollision(GetComponent<CharacterController>(), collider, true);
            RefreshHud();
        }

        internal bool PickUpDropped(int offerIndex, WeaponSettings settings, int magazineAmmo, int reserve)
        {
            if (heldWeapon || offerIndex < 0 || offerIndex >= offers.Length || offers[offerIndex] != settings) return false;
            WeaponSettings offer = offers[offerIndex];
            equippedOffer = offerIndex;
            ApplySettings(offer);
            ammoInMagazine = Mathf.Clamp(magazineAmmo, 0, magazineSize);
            reserveAmmo = Mathf.Max(0, reserve);
            AttachWeapon();
            RefreshHud();
            return true;
        }

        private void ScanPickups()
        {
            if (Time.time < nextPickupScan) return;
            nextPickupScan = Time.time + 0.08f;
            nearbyPickup = null;
            if (HasWeapon) return;
            float best = 2.2f;
            int count = Physics.OverlapSphereNonAlloc(transform.position, best, pickupHits, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                var pickup = pickupHits[i].GetComponentInParent<DroppedWeapon>();
                if (!pickup || !pickup.CanCollect) continue;
                float distance = Vector3.Distance(transform.position, pickup.transform.position);
                Vector3 origin = transform.position + Vector3.up * 0.5f;
                Vector3 path = pickup.transform.position - origin;
                if (distance >= best || CombatPhysics.Cast(origin, path, path.magnitude, transform, out var cover) && !cover.collider.transform.IsChildOf(pickup.transform)) continue;
                nearbyPickup = pickup;
                best = distance;
            }
        }

        private void ApplySettings(WeaponSettings settings)
        {
            magazineSize = Mathf.Max(1, settings.magazine);
            startingReserveAmmo = Mathf.Max(0, settings.reserve);
            fireRate = Mathf.Max(0.05f, settings.fireRate);
            damage = settings.damage;
            range = settings.range;
            hipFireSpread = settings.hipFireSpread;
            aimedSpread = settings.aimedSpread;
            bloomPerShot = settings.bloomPerShot;
            maximumBloom = settings.maximumBloom;
        }

        internal void PlayMeleeSwing() { if (feedback) feedback.MeleeSwing(); }

        internal void PerformMeleeHit(float amount, float reach, float radius, bool kick, Vector3 localDirection)
        {
            if (HasWeapon) return;
            var body = GetComponent<CharacterController>();
            Vector3 origin = body ? body.bounds.center : transform.position + Vector3.up * 0.8f;
            if (kick) origin += Vector3.up * 0.15f;
            Vector3 direction = localDirection.sqrMagnitude > 0.001f ? transform.TransformDirection(localDirection.normalized) : transform.forward;
            if (!CombatPhysics.Cast(origin, direction, reach, transform, out var hit, radius)) return;
            ApplyMeleeContact(hit.collider, hit.point, hit.normal, amount);
        }

        internal void ApplyMeleeContact(Collider collider, Vector3 point, Vector3 normal, float amount)
        {
            if (HasWeapon || !collider) return;
            var health = collider.GetComponentInParent<TargetHealth>();
            bool accepted = health && health.TakeDamage(amount);
            if (feedback) feedback.MeleeImpact(point, normal, accepted);
        }

        private void AttachWeapon()
        {
            if (feedback) feedback.StopFiringSound();
            if (heldWeapon) Destroy(heldWeapon);
            if (heldWeaponLeft) Destroy(heldWeaponLeft);
            heldWeaponLeft = null;
            Transform hand = transform;
            MaleWalkRetarget animation = GetComponent<MaleWalkRetarget>();
            if (animation && animation.RigAnimator)
            {
                Transform rightHand = animation.RigAnimator.GetBoneTransform(HumanBodyBones.RightHand);
                if (rightHand) hand = rightHand;
            }
            weaponHand = hand;
            GameObject prefab = CurrentWeapon.model;
            if (!prefab)
            {
                Debug.LogError("Assign a model on the equipped Weapon Settings asset.", this);
                heldWeapon = null;
                RefreshHud();
                return;
            }
            heldWeapon = Instantiate(prefab, hand, false);
            heldWeapon.transform.localScale = prefab.transform.localScale * weaponModelScale;
            heldWeapon.name = CurrentWeapon.displayName + " (equipped)";
            foreach (Collider collider in heldWeapon.GetComponentsInChildren<Collider>()) collider.enabled = false;
            Vector3 grip = CurrentWeapon.gripEuler;
            heldWeapon.transform.localRotation = Quaternion.Euler(grip);
            // The supplied models have their grip at the root, matching the Soldier clips.
            heldWeapon.transform.localPosition = Vector3.zero;
            if (CurrentWeapon.dualWield)
            {
                Transform left = animation && animation.RigAnimator ? animation.RigAnimator.GetBoneTransform(HumanBodyBones.LeftHand) : null;
                if (!left)
                {
                    Debug.LogError("Dual handguns need a Humanoid left-hand bone.", this);
                    Destroy(heldWeapon);
                    heldWeapon = null;
                    RefreshHud();
                    return;
                }
                heldWeaponLeft = Instantiate(prefab, left, false);
                heldWeaponLeft.name = CurrentWeapon.displayName + " (left hand)";
                heldWeaponLeft.transform.localScale = prefab.transform.localScale * weaponModelScale;
                heldWeaponLeft.transform.localPosition = Vector3.zero;
                heldWeaponLeft.transform.localRotation = Quaternion.Euler(CurrentWeapon.leftGripEuler);
                foreach (var collider in heldWeaponLeft.GetComponentsInChildren<Collider>()) collider.enabled = false;
            }
            reloading = false;
            LastShotTime = float.NegativeInfinity;
            nextShotTime = Time.time;
            IsAiming = false;
            AimWeight = 0f;
            bloom = 0f;
            pendingShot = false;
            shootingSpeedWeight = 1f;
            recoil = recoilTarget = recoilVelocity = gripWeight = 0f;
            leftRecoil = leftRecoilTarget = leftRecoilVelocity = 0f;
            nextShotLeft = pendingShotLeft = LastShotUsedLeftHand = false;
            if (feedback) feedback.Handling(CurrentWeapon.equipSounds, MuzzlePosition, CurrentWeapon.shotPitch);
            if (characterAnimations) characterAnimations.CancelFightingMotion();
            RefreshHud();
        }

        internal void ApplyAnimatedWeaponPose(float dt, bool allowSupportHand)
        {
            if (!HasWeapon || !CurrentWeapon) return;
            if (Time.time >= recoilHoldUntil) recoilTarget = 0f;
            recoil = Mathf.SmoothDamp(recoil, recoilTarget, ref recoilVelocity,
                recoilTarget > recoil ? CurrentWeapon.kickTime : CurrentWeapon.recoilRecovery, Mathf.Infinity, dt);
            if (Time.time >= leftRecoilHoldUntil) leftRecoilTarget = 0f;
            leftRecoil = Mathf.SmoothDamp(leftRecoil, leftRecoilTarget, ref leftRecoilVelocity,
                leftRecoilTarget > leftRecoil ? CurrentWeapon.kickTime : CurrentWeapon.recoilRecovery, Mathf.Infinity, dt);
            float running = characterAnimations ? characterAnimations.RunningPoseWeight : 0f;
            float aim = allowSupportHand && !IsReloading ? AimWeight * (1f - running) : 0f;
            Quaternion grip = Quaternion.Slerp(Quaternion.Euler(CurrentWeapon.gripEuler), Quaternion.Euler(CurrentWeapon.aimedGripEuler), aim);
            CacheArmBones();
            Vector3 aimPoint = aim > 0f ? AimTargetPoint() : Vector3.zero;
            if (aim > 0f && rightHand && rightUpperArm && rightLowerArm)
            {
                Vector3 target = rightHand.position + transform.TransformDirection(CurrentWeapon.aimHandOffset) * aim;
                Vector3 direction = (aimPoint - target).normalized;
                Quaternion gunRotation = SafeAimRotation(direction);
                Quaternion handRotation = gunRotation * Quaternion.Inverse(Quaternion.Euler(CurrentWeapon.aimedGripEuler));
                SolveArm(rightUpperArm, rightLowerArm, rightHand, target,
                    Quaternion.Slerp(rightHand.rotation, handRotation, aim));
            }
            heldWeapon.transform.localRotation = grip;
            heldWeapon.transform.localPosition = grip * Vector3.back * (recoil * CurrentWeapon.weaponKickDistance * weaponRecoilScale);
            if (aim > 0f && rightHand)
            {
                // Converge at the crosshair's actual target, including the shoulder
                // camera's horizontal offset and the muzzle's offset above the grip.
                Quaternion animatedHand = rightHand.rotation;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 direction = aimPoint - MuzzlePosition;
                    if (Vector3.Angle(heldWeapon.transform.forward, direction) < 0.02f) break;
                    Quaternion correction = Quaternion.FromToRotation(heldWeapon.transform.forward, direction);
                    rightHand.rotation = correction * rightHand.rotation;
                }
                rightHand.rotation = Quaternion.Slerp(animatedHand, rightHand.rotation, aim);
            }
            heldWeapon.transform.localRotation = grip * Quaternion.Euler(-recoil * CurrentWeapon.weaponKickAngle * weaponRecoilScale, 0f, 0f);
            if (IsDualWielding)
            {
                ApplyOffhandPose(aim, aimPoint);
                return;
            }
            float support = CurrentWeapon.supportHand ? 1f : aim;
            gripWeight = Mathf.MoveTowards(gripWeight, allowSupportHand && !IsReloading ? supportHandWeight * support * (1f - running) : 0f, dt / 0.1f);
            if (gripWeight <= 0f || !allowSupportHand || !leftUpperArm || !leftLowerArm || !leftHand) return;
            Vector3 supportTarget = Vector3.Lerp(leftHand.position,
                heldWeapon.transform.TransformPoint(CurrentWeapon.supportHandLocalPosition), gripWeight);
            if (CurrentWeapon.triggerMode == WeaponSettings.TriggerMode.PumpAction && !IsReloading)
            {
                float cycle = Mathf.InverseLerp(0.2f, 0.65f, (Time.time - LastShotTime) / Mathf.Max(0.05f, fireRate));
                supportTarget -= heldWeapon.transform.forward * (Mathf.Sin(cycle * Mathf.PI) * 0.1f * gripWeight);
            }
            Quaternion supportRotation = Quaternion.Slerp(leftHand.rotation,
                heldWeapon.transform.rotation * Quaternion.Euler(CurrentWeapon.supportHandLocalEuler), gripWeight);
            SolveArm(leftUpperArm, leftLowerArm, leftHand, supportTarget, supportRotation);
        }

        private void ApplyOffhandPose(float aim, Vector3 aimPoint)
        {
            if (!heldWeaponLeft || !leftHand || !leftUpperArm || !leftLowerArm) return;
            Quaternion grip = Quaternion.Slerp(Quaternion.Euler(CurrentWeapon.leftGripEuler), Quaternion.Euler(CurrentWeapon.leftAimedGripEuler), aim);
            if (aim > 0f)
            {
                Vector3 target = leftHand.position + transform.TransformDirection(CurrentWeapon.leftAimHandOffset) * aim;
                Quaternion rotation = SafeAimRotation((aimPoint - target).normalized) * Quaternion.Inverse(Quaternion.Euler(CurrentWeapon.leftAimedGripEuler));
                SolveArm(leftUpperArm, leftLowerArm, leftHand, target, Quaternion.Slerp(leftHand.rotation, rotation, aim));
            }
            heldWeaponLeft.transform.localRotation = grip;
            heldWeaponLeft.transform.localPosition = grip * Vector3.back * (leftRecoil * CurrentWeapon.weaponKickDistance * weaponRecoilScale);
            if (aim > 0f)
                for (int i = 0; i < 2; i++)
                    leftHand.rotation = Quaternion.Slerp(Quaternion.identity,
                        Quaternion.FromToRotation(heldWeaponLeft.transform.forward, aimPoint - OffhandMuzzlePosition), aim) * leftHand.rotation;
            heldWeaponLeft.transform.localRotation = grip * Quaternion.Euler(-leftRecoil * CurrentWeapon.weaponKickAngle * weaponRecoilScale, 0f, 0f);
        }

        private Vector3 AimTargetPoint()
        {
            Vector3 origin = aimCamera ? aimCamera.transform.position : transform.position + Vector3.up;
            Vector3 direction = AimWorldDirection;
            return CombatPhysics.Cast(origin, direction, range, transform, out var hit)
                ? hit.point : origin + direction * range;
        }

        private Quaternion SafeAimRotation(Vector3 direction)
        {
            // Keep a stable roll even when the camera looks straight up.
            Vector3 up = Vector3.ProjectOnPlane(transform.up, direction);
            if (up.sqrMagnitude < 0.001f) up = Vector3.Cross(direction, transform.right);
            return Quaternion.LookRotation(direction, up);
        }

        private void CacheArmBones()
        {
            var animator = characterAnimations ? characterAnimations.RigAnimator : null;
            if (!animator || poseAnimator == animator) return;
            poseAnimator = animator;
            rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            rightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            leftLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        }

        private static void SolveArm(Transform upper, Transform lower, Transform hand, Vector3 target, Quaternion rotation)
        {
            float a = Vector3.Distance(upper.position, lower.position);
            float b = Vector3.Distance(lower.position, hand.position);
            if (a < 0.001f || b < 0.001f) return;
            Vector3 toward = target - upper.position;
            if (toward.sqrMagnitude < 0.000001f) return;
            float length = Mathf.Clamp(toward.magnitude, Mathf.Abs(a - b) + 0.001f, a + b - 0.001f);
            Vector3 axis = toward.normalized;
            Vector3 pole = Vector3.ProjectOnPlane(lower.position - upper.position, axis).normalized;
            if (pole.sqrMagnitude < 0.01f) pole = Vector3.ProjectOnPlane(-upper.root.up, axis).normalized;
            if (pole.sqrMagnitude < 0.01f) pole = Vector3.ProjectOnPlane(upper.root.right, axis).normalized;
            float along = (a * a - b * b + length * length) / (2f * length);
            Vector3 elbow = upper.position + axis * along + pole * Mathf.Sqrt(Mathf.Max(0f, a * a - along * along));
            upper.rotation = Quaternion.FromToRotation(lower.position - upper.position, elbow - upper.position) * upper.rotation;
            Vector3 reachable = upper.position + axis * length;
            lower.rotation = Quaternion.FromToRotation(hand.position - lower.position, reachable - lower.position) * lower.rotation;
            hand.rotation = rotation;
        }

        private void BuildHud()
        {
            GameObject canvasObject = new GameObject("Weapon HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            weaponHud = canvas;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            CreateHudText(canvas.transform, "PointsText", "$10,000", 25, new Vector2(1f, 0f), new Vector2(-34f, 115f), new Vector2(350f, 48f), TextAnchor.MiddleRight, out pointsText);
            CreateHudText(canvas.transform, "AmmoText", "12 / 60", 23, new Vector2(1f, 0f), new Vector2(-34f, 62f), new Vector2(270f, 44f), TextAnchor.MiddleRight, out ammoText);
            CreateHudText(canvas.transform, "WeaponText", "SIDEARM", 14, new Vector2(1f, 0f), new Vector2(-34f, 32f), new Vector2(270f, 28f), TextAnchor.MiddleRight, out weaponText);
            CreateHudText(canvas.transform, "PromptText", "", 20, new Vector2(0.5f, 0f), new Vector2(0f, 125f), new Vector2(800f, 45f), TextAnchor.MiddleCenter, out promptText);
            EnsureCrosshair(canvas.transform);
            if (feedback) feedback.CreateHitMarker(canvas.transform);
            GameObject icon = new GameObject("Gun Icon", typeof(RawImage));
            icon.transform.SetParent(canvas.transform, false);
            RectTransform rect = icon.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-302f, 57f);
            rect.sizeDelta = new Vector2(62f, 42f);
            RawImage raw = icon.GetComponent<RawImage>();
            gunIconImage = raw;
            raw.raycastTarget = false;
            raw.texture = BuildGunIcon();
            raw.color = Color.white;
        }

        private static void CreateHudText(Transform parent, string name, string value, int size, Vector2 anchor,
            Vector2 offset, Vector2 dimensions, TextAnchor textAnchor, out Text output)
        {
            GameObject go = new GameObject(name, typeof(Text));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(anchor.x, anchor.y);
            rect.anchoredPosition = offset;
            rect.sizeDelta = dimensions;
            output = go.GetComponent<Text>();
            output.raycastTarget = false;
            output.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            output.fontSize = size;
            output.alignment = textAnchor;
            output.color = Color.white;
            output.text = value;
            output.horizontalOverflow = HorizontalWrapMode.Overflow;
            output.verticalOverflow = VerticalWrapMode.Overflow;
        }

        private static Texture2D BuildGunIcon()
        {
            Texture2D texture = new Texture2D(64, 40, TextureFormat.RGBA32, false);
            Color clear = new Color(0f, 0f, 0f, 0f);
            Color white = Color.white;
            Color[] pixels = new Color[64 * 40];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;
            for (int y = 17; y <= 22; y++) for (int x = 8; x <= 54; x++) pixels[y * 64 + x] = white;
            for (int y = 10; y <= 16; y++) for (int x = 25; x <= 42; x++) pixels[y * 64 + x] = white;
            for (int y = 5; y <= 16; y++) for (int x = 19; x <= 24; x++) pixels[y * 64 + x] = white;
            for (int y = 23; y <= 34; y++) for (int x = 31; x <= 37; x++) pixels[y * 64 + x] = white;
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private void UpdateCrosshair()
        {
            // Runtime UI survives an Editor script reload, but cached arrays may not.
            if (crosshairParts == null || crosshairParts.Length != 4 || !crosshairParts[0] || crosshairImages == null || crosshairImages.Length != 4)
            {
                if (!pointsText || !pointsText.canvas) return;
                EnsureCrosshair(pointsText.canvas.transform);
            }
            float fov = aimCamera ? aimCamera.fieldOfView : 60f;
            float gap = Mathf.Clamp(Mathf.Tan(CurrentSpreadDegrees * Mathf.Deg2Rad) /
                Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * 540f, 4f, 90f);
            crosshairGap = Mathf.Lerp(crosshairGap, gap, 1f - Mathf.Exp(-Time.deltaTime / 0.05f));
            for (int i = 0; i < crosshairParts.Length; i++)
            {
                if (!crosshairParts[i] || !crosshairImages[i]) continue;
                crosshairParts[i].anchoredPosition = (i == 0 ? Vector2.left : i == 1 ? Vector2.right : i == 2 ? Vector2.up : Vector2.down) * (crosshairGap + 4f);
                crosshairImages[i].enabled = HasWeapon && (!movement || movement.PlayerControlEnabled);
                crosshairImages[i].color = Color.Lerp(Color.white, new Color(0.45f, 0.95f, 1f), AimWeight);
            }
        }

        private void EnsureCrosshair(Transform parent)
        {
            if (crosshairParts == null || crosshairParts.Length != 4) crosshairParts = new RectTransform[4];
            if (crosshairImages == null || crosshairImages.Length != 4) crosshairImages = new Image[4];
            for (int i = 0; i < crosshairParts.Length; i++)
            {
                Transform existing = parent.Find("Crosshair " + i);
                GameObject stroke = existing ? existing.gameObject : new GameObject("Crosshair " + i, typeof(Image));
                stroke.transform.SetParent(parent, false);
                var rect = stroke.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
                rect.sizeDelta = i < 2 ? new Vector2(8f, 2f) : new Vector2(2f, 8f);
                crosshairParts[i] = rect;
                crosshairImages[i] = stroke.GetComponent<Image>();
                crosshairImages[i].raycastTarget = false;
            }
        }

        private void RefreshHud()
        {
            if (!pointsText) return;
            pointsText.text = "POINTS  $" + points.ToString("N0");
            ammoText.text = reloading ? "RELOADING..." : ammoInMagazine + " / " + reserveAmmo;
            weaponText.text = heldWeapon ? offers[equippedOffer].displayName : "UNARMED";
            if (!heldWeapon) ammoText.text = "LEFT CLICK TO ATTACK";
            if (gunIconImage)
            {
                gunIconImage.enabled = HasWeapon;
                if (CurrentWeapon && CurrentWeapon.icon) gunIconImage.texture = CurrentWeapon.icon;
            }
        }

        private int CurrentMagazineSize => Mathf.Max(1, magazineSize);
    }

}
