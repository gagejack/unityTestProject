using UnityEngine;
using UnityEngine.InputSystem;

namespace StarterGame
{
    [RequireComponent(typeof(CharacterController), typeof(PlayerStamina))]
    public sealed class CapsuleMovement : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float moveSpeed = 4.5f;
        [Header("Running — double-tap W or hold Shift")]
        [SerializeField, Min(1f)] private float runSpeedMultiplier = 1.5f;
        [SerializeField, Range(0.1f, 0.5f)] private float forwardDoubleTapWindow = 0.28f;
        private bool runRequested;
        private bool holdRunRequested;
        private float lastForwardTap = float.NegativeInfinity;
        public bool IsRunning { get; private set; }
        public bool FaceCameraDirection { get; set; } = true;
        private Transform movementCamera;
        private FollowCamera cameraView;
        private ViewModeDebugMenu debugMenu;
        public bool PlayerControlEnabled => (!cameraView || !cameraView.IsFlying) && (!debugMenu || !debugMenu.IsOpen);
        public Transform MovementCamera
        {
            get => movementCamera;
            set
            {
                movementCamera = value;
                cameraView = value != null ? value.GetComponent<FollowCamera>() : null;
                debugMenu = cameraView ? cameraView.GetComponent<ViewModeDebugMenu>() : null;
            }
        }
        [Tooltip("Turning speed in fixed-camera mode. Third-person facing follows mouse-look directly.")]
        [InspectorName("Steering Speed")]
        [SerializeField, Min(0f)] private float turnSpeed = 360f;
        [Tooltip("Degrees per second when turning toward the camera's aim direction, including while strafing or backing up.")]
        [SerializeField, Min(0f)] private float aimTurnSpeed = 720f;
        [SerializeField, Min(0f)] private float jumpHeight = 1.6f;
        [SerializeField] private float gravity = -22f;
        [Tooltip("Midair steering strength: 0 preserves takeoff momentum, 1 gives full control. Also scales dash-jump and wall-jump steering. Ground movement and committed dives are unaffected.")]
        [SerializeField, Range(0f, 1f)] private float airControl = 1f;
        private Vector3 horizontalVelocity;
        [Header("Movement response")]
        [SerializeField, Min(0f)] private float accelerationTime = 0.1f;
        [SerializeField, Min(0f)] private float brakingTime = 0.08f;
        [SerializeField, Min(0.01f)] private float combatSlowdownTime = 0.05f;
        private float combatSpeedWeight = 1f;
        public bool IsGrounded => controller && controller.isGrounded;
        public float VerticalVelocity => verticalSpeed;
        public Vector3 PlanarVelocity => horizontalVelocity;
        [Header("Stamina costs")]
        [SerializeField, Min(0f)] private float dashStaminaCost = 25f;
        [SerializeField, Min(0f)] private float diveStaminaCost = 20f;
        [SerializeField, Min(0f)] private float jumpStaminaCost = 10f;
        private PlayerStamina stamina;
        private MaleWalkRetarget fightingAnimation;
        private WeaponCombatController weaponCombat;

        [Header("Wall jump")]
        [Tooltip("Maximum successful wall jumps before touching the ground. Set to 0 to disable wall jumps.")]
        [InspectorName("Max Wall Jumps")]
        [SerializeField, Min(0)] private int maxWallJumps = 2;
        [SerializeField, Min(0f)] private float wallJumpHeight = 1.8f;
        [SerializeField, Min(0f)] private float wallJumpSpeed = 8f;
        [Tooltip("Time for air steering to change horizontal speed by Wall Jump Speed. Lower values respond faster.")]
        [InspectorName("Wall Jump Control Time")]
        [SerializeField, Min(0.05f)] private float wallJumpPushTime = 0.2f;
        [Tooltip("How much camera-relative WASD input affects the initial bounce away from the wall.")]
        [SerializeField, Range(0f, 1f)] private float wallJumpInputInfluence = 0.5f;
        private bool steeringWallJump;
        private float wallJumpCooldown;
        private int wallJumpsSinceGrounded;
        private Vector3 wallJumpVelocity;
        private Collider lastWallJumpCollider;
        private readonly RaycastHit[] wallHits = new RaycastHit[16];
        private readonly Vector3[] wallNormals = new Vector3[8];

        [Header("Crouch")]
        [SerializeField, Min(0.1f)] private float crouchHeight = 1.3f;
        [SerializeField, Range(0f, 1f)] private float crouchSpeedMultiplier = 0.5f;
        public bool IsCrouching { get; private set; }
        public float CrouchDrop => standingHeight - controller.height;
        private float standingHeight;
        private Vector3 standingCenter;
        private InputAction crouchAction;
        private readonly Collider[] standingObstacles = new Collider[32];

        [Header("Dash roll")]
        [SerializeField, Min(0f)] private float dashSpeedMultiplier = 1f;
        [SerializeField, Min(0.1f)] private float dashDuration = 0.5f;
        [SerializeField, Min(0f)] private float dashCooldown = 0.35f;
        [Tooltip("How quickly held WASD turns retained dash momentum toward the camera-relative input direction. Releasing input keeps the current velocity.")]
        [SerializeField, Min(0f)] private float dashAirTurnSpeed = 180f;
        private bool carryingDashMomentum;
        private Vector3 dashAirVelocity;

        [Header("Air dive")]
        [SerializeField, Min(0f)] private float diveSpeedMultiplier = 1f;
        [Header("Dive landing slide")]
        [SerializeField, Min(0.1f)] private float slideDeceleration = 26f;
        public bool IsSliding { get; private set; }
        public float SlideSpeed { get; private set; }
        public bool IsDiving { get; private set; }
        public bool IsRolling { get; private set; }
        public Vector3 RollDirection => carryingDashMomentum && dashAirVelocity.sqrMagnitude > 0.001f
            ? dashAirVelocity.normalized : rollDirection;
        public bool IsRollingBackward => IsRolling && Vector3.Dot(rollDirection, transform.forward) < 0f;
        public bool IsDivingBackward => (IsDiving || IsSliding) && Vector3.Dot(rollDirection, transform.forward) < 0f;
        public float RollProgress => IsRolling ? Mathf.Clamp01(rollElapsed / activeRollDuration) : 0f;
        private float rollElapsed;
        private float activeRollDuration;
        private float cooldownRemaining;
        private Vector3 rollDirection;
        private InputAction rollAction;
        private CharacterController controller;
        private InputAction moveAction;
        private InputAction jumpAction;
        private float verticalSpeed;
        private Vector3 spawnPosition;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            stamina = GetComponent<PlayerStamina>();
            fightingAnimation = GetComponent<MaleWalkRetarget>();
            weaponCombat = GetComponent<WeaponCombatController>();
            standingHeight = controller.height;
            standingCenter = controller.center;
            spawnPosition = transform.position;
            moveAction = new InputAction("Move", InputActionType.Value);
            // Clamp the combined input below so diagonal movement keeps the same speed.
            moveAction.AddCompositeBinding("2DVector(mode=1)")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            jumpAction = new InputAction("Jump", InputActionType.Button, "<Keyboard>/space");
            rollAction = new InputAction("Dash Roll", InputActionType.Button);
            rollAction.AddBinding("<Keyboard>/capsLock");
            crouchAction = new InputAction("Crouch", InputActionType.Button);
            crouchAction.AddBinding("<Keyboard>/leftCtrl");
            crouchAction.AddBinding("<Keyboard>/rightCtrl");
            Game.Core.PlayerRequests.TeleportRequested += OnTeleportRequested;
        }

        private void OnEnable()
        {
            Game.Core.PlayerPresence.Register(transform);
            moveAction.Enable();
            jumpAction.Enable();
            rollAction.Enable();
            crouchAction.Enable();
        }

        private void OnDisable()
        {
            Game.Core.PlayerPresence.Unregister(transform);
            runRequested = holdRunRequested = IsRunning = false;
            lastForwardTap = float.NegativeInfinity;
            horizontalVelocity = Vector3.zero;
            combatSpeedWeight = 1f;
            moveAction.Disable();
            jumpAction.Disable();
            rollAction.Disable();
            crouchAction.Disable();
            IsRolling = false;
            IsDiving = false;
            IsSliding = false;
            SlideSpeed = 0f;
            cooldownRemaining = 0f;
            steeringWallJump = false;
            wallJumpVelocity = Vector3.zero;
            wallJumpCooldown = 0f;
            lastWallJumpCollider = null;
            ClearDashMomentum();
        }

        private void OnDestroy()
        {
            Game.Core.PlayerRequests.TeleportRequested -= OnTeleportRequested;
            moveAction.Dispose();
            jumpAction.Dispose();
            rollAction.Dispose();
            crouchAction.Dispose();
        }

        private void UpdateCrouch()
        {
            bool requested = PlayerControlEnabled && crouchAction.IsPressed();
            if (!requested && IsCrouching && !CanStand()) requested = true;
            if (requested == IsCrouching) return;
            IsCrouching = requested;
            float height = requested ? Mathf.Clamp(crouchHeight, controller.radius * 2f, standingHeight) : standingHeight;
            // Keep the bottom of the collision capsule at the same world height.
            controller.height = height;
            controller.center = standingCenter - Vector3.up * ((standingHeight - height) * 0.5f);
        }

        private bool CanStand()
        {
            Vector3 scale = transform.lossyScale;
            float radius = Mathf.Max(0.01f, controller.radius - controller.skinWidth) * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float halfHeight = standingHeight * Mathf.Abs(scale.y) * 0.5f;
            Vector3 center = transform.TransformPoint(standingCenter);
            Vector3 offset = transform.up * Mathf.Max(0f, halfHeight - controller.radius * Mathf.Abs(scale.y));
            int count = Physics.OverlapCapsuleNonAlloc(center - offset, center + offset, radius,
                standingObstacles, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            if (count == standingObstacles.Length) return false;
            for (int i = 0; i < count; i++)
            {
                Collider obstacle = standingObstacles[i];
                if (obstacle.transform.IsChildOf(transform) ||
                    Physics.GetIgnoreLayerCollision(gameObject.layer, obstacle.gameObject.layer) ||
                    Physics.GetIgnoreCollision(controller, obstacle)) continue;
                return false;
            }
            return true;
        }

        private bool TryWallJump(Vector3 requestedDirection)
        {
            if (controller.isGrounded || wallJumpCooldown > 0f ||
                wallJumpsSinceGrounded >= maxWallJumps) return false;
            Vector3 scale = transform.lossyScale;
            float radius = controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float skin = controller.skinWidth * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            Vector3 center = transform.TransformPoint(controller.center);
            Vector3 extent = Vector3.up * Mathf.Max(0f, controller.height * Mathf.Abs(scale.y) * 0.5f - radius);
            int normalCount = 0;
            Vector3 outward = Vector3.zero;
            Collider jumpWall = null;
            float closestWall = float.PositiveInfinity;
            // Probe current contact, rather than retaining a stale collision from a previous frame.
            for (int side = 0; side < 8; side++)
            {
                Vector3 probe = Quaternion.Euler(0f, side * 45f, 0f) * Vector3.forward;
                int hits = Physics.CapsuleCastNonAlloc(center - extent, center + extent,
                    Mathf.Max(0.01f, radius - skin), probe, wallHits, skin + 0.07f,
                    Physics.AllLayers, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < hits; i++)
                {
                    RaycastHit hit = wallHits[i];
                    if (hit.collider.transform.IsChildOf(transform) || Mathf.Abs(hit.normal.y) > 0.2f ||
                        Physics.GetIgnoreLayerCollision(gameObject.layer, hit.collider.gameObject.layer) ||
                        Physics.GetIgnoreCollision(controller, hit.collider)) continue;
                    if (hit.collider != lastWallJumpCollider && hit.distance < closestWall)
                    {
                        jumpWall = hit.collider;
                        closestWall = hit.distance;
                    }
                    Vector3 normal = Vector3.ProjectOnPlane(hit.normal, Vector3.up).normalized;
                    bool duplicate = false;
                    for (int n = 0; n < normalCount; n++)
                        if (Vector3.Dot(normal, wallNormals[n]) > 0.95f) duplicate = true;
                    if (duplicate || normalCount == wallNormals.Length) continue;
                    wallNormals[normalCount++] = normal;
                    outward += normal;
                }
            }
            if (normalCount == 0 || jumpWall == null) return false;
            if (!stamina.TrySpend(jumpStaminaCost)) return false;
            outward.Normalize();
            // Start with a push away from the wall, then add the player's held input.
            // Into-wall input brakes the bounce instead of being reflected into a full-strength push.
            Vector3 launch = outward * wallJumpSpeed + requestedDirection * moveSpeed * wallJumpInputInfluence * Mathf.Clamp01(airControl);
            for (int n = 0; n < normalCount; n++)
            {
                float separationSpeed = Vector3.Dot(launch, wallNormals[n]);
                if (separationSpeed < 1f)
                    launch += wallNormals[n] * (1f - separationSpeed);
            }
            lastWallJumpCollider = jumpWall;
            if (fightingAnimation) fightingAnimation.CancelFightingMotion();
            wallJumpVelocity = launch;
            wallJumpsSinceGrounded++;
            steeringWallJump = true;
            ClearDashMomentum();
            wallJumpCooldown = Mathf.Max(0.05f, wallJumpPushTime) + 0.1f;
            verticalSpeed = Mathf.Sqrt(wallJumpHeight * -2f * gravity);
            IsRolling = IsDiving = IsSliding = false;
            SlideSpeed = 0f;
            transform.rotation = Quaternion.LookRotation(launch.normalized);
            return true;
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (Mathf.Abs(hit.normal.y) > 0.2f) return;
            Vector3 normal = Vector3.ProjectOnPlane(hit.normal, Vector3.up).normalized;
            if (steeringWallJump)
            {
                float intoWall = Vector3.Dot(wallJumpVelocity, normal);
                if (intoWall < 0f) wallJumpVelocity -= normal * intoWall;
            }
            if (carryingDashMomentum)
            {
                float intoWall = Vector3.Dot(dashAirVelocity, normal);
                if (intoWall < 0f) dashAirVelocity -= normal * intoWall;
            }
        }

        private void ClearDashMomentum()
        {
            carryingDashMomentum = false;
            dashAirVelocity = Vector3.zero;
        }

        private void CaptureDashMomentum()
        {
            carryingDashMomentum = true;
            dashAirVelocity = rollDirection * (moveSpeed * dashSpeedMultiplier);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            UpdateRunInput(Time.unscaledTime, keyboard != null && keyboard.wKey.wasPressedThisFrame,
                keyboard != null && keyboard.wKey.isPressed,
                keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed));
            SimulateMovement(Time.deltaTime, moveAction.ReadValue<Vector2>(),
                jumpAction.WasPressedThisFrame(), rollAction.WasPressedThisFrame());
        }

        private void UpdateRunInput(float now, bool forwardPressed, bool forwardHeld, bool runModifierHeld)
        {
            bool blocked = !PlayerControlEnabled || !Application.isFocused || IsCrouching || IsRolling || IsDiving || IsSliding ||
                (weaponCombat && (weaponCombat.IsAiming || weaponCombat.IsReloading || weaponCombat.IsRecoveringFromShot || weaponCombat.ShootingMovementMultiplier < 0.999f)) ||
                (fightingAnimation && fightingAnimation.IsAttacking);
            // Either Shift key can request sprinting while W is held.
            holdRunRequested = !blocked && forwardHeld && runModifierHeld;
            if (blocked)
            {
                runRequested = false;
                lastForwardTap = float.NegativeInfinity;
                return;
            }
            if (!forwardHeld) runRequested = false;
            if (!forwardPressed) return;
            bool doubleTap = now - lastForwardTap <= forwardDoubleTapWindow;
            runRequested = doubleTap;
            lastForwardTap = doubleTap ? float.NegativeInfinity : now;
        }

        private void SimulateMovement(float dt, Vector2 moveInput, bool jumpPressed, bool rollPressed)
        {
            if (dt <= 0f) return;
            if (!PlayerControlEnabled)
            {
                moveInput = Vector2.zero;
                jumpPressed = rollPressed = false;
            }
            bool attackCommitted = fightingAnimation && fightingAnimation.IsAttackCommitted;
            // Keep steering and walking available. Only the swing itself commits jump/dash/crouch.
            if (attackCommitted) jumpPressed = rollPressed = false;
            // Capture before the roll timer expires, including a dash that just left a ledge.
            if (IsRolling && !controller.isGrounded && !carryingDashMomentum) CaptureDashMomentum();
            wallJumpCooldown = Mathf.Max(0f, wallJumpCooldown - dt);
            cooldownRemaining = Mathf.Max(0f, cooldownRemaining - dt);
            if (IsRolling && rollElapsed >= activeRollDuration)
            {
                IsRolling = false;
                cooldownRemaining = dashCooldown;
            }
            if (!attackCommitted && !IsRolling && !IsDiving && !IsSliding)
                UpdateCrouch();
            Vector2 input = new Vector2(Mathf.Clamp(moveInput.x, -1f, 1f), Mathf.Clamp(moveInput.y, -1f, 1f));
            bool grounded = controller.isGrounded;
            if (grounded)
            {
                lastWallJumpCollider = null;
                wallJumpsSinceGrounded = 0;
                steeringWallJump = false;
                wallJumpVelocity = Vector3.zero;
                ClearDashMomentum();
            }

            if (grounded && verticalSpeed < 0f)
                verticalSpeed = -2f;

            // Use this frame's mouse heading, independent of camera follow smoothing and pitch.
            Vector3 forward = FaceCameraDirection && cameraView != null && cameraView.ThirdPerson
                ? cameraView.PlanarForward
                : MovementCamera != null
                    ? Vector3.ProjectOnPlane(MovementCamera.forward, Vector3.up).normalized
                    : Vector3.forward;
            if (forward.sqrMagnitude < 0.001f) forward = cameraView ? cameraView.PlanarForward : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector2 planarInput = Vector2.ClampMagnitude(input, 1f);
            Vector3 direction = forward * planarInput.y + right * planarInput.x;
            if (!IsRolling && !IsDiving && !IsSliding)
            {
                if (weaponCombat && weaponCombat.IsAiming)
                {
                    Quaternion aimRotation = Quaternion.LookRotation(weaponCombat.AimPlanarForward.sqrMagnitude > 0.001f
                        ? weaponCombat.AimPlanarForward : forward);
                    // Mouse-facing movement keeps the same direct response when aim starts.
                    transform.rotation = FaceCameraDirection && cameraView != null && cameraView.ThirdPerson
                        ? aimRotation : Quaternion.RotateTowards(transform.rotation, aimRotation, aimTurnSpeed * dt);
                }
                else if (FaceCameraDirection)
                    transform.rotation = Quaternion.LookRotation(forward);
                else if (direction.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation,
                        Quaternion.LookRotation(direction), turnSpeed * dt);

                if (carryingDashMomentum && direction.sqrMagnitude > 0.001f)
                    dashAirVelocity = Vector3.RotateTowards(dashAirVelocity, direction * dashAirVelocity.magnitude,
                        dashAirTurnSpeed * Mathf.Clamp01(airControl) * Mathf.Deg2Rad * dt, 0f);
            }
            // Caps Lock in the air starts a committed dive (hold S to dive backward).
            // Air dives begin exactly where the player is: no teleport or upward impulse.
            // A fresh press can also turn a roll that left a ledge into a dive.
            if (!grounded && !IsCrouching && !IsDiving && !IsSliding && rollPressed && stamina.TrySpend(diveStaminaCost))
            {
                IsRolling = false;
                IsDiving = true;
                if (fightingAnimation) fightingAnimation.CancelFightingMotion();
                ClearDashMomentum();
                steeringWallJump = false;
                wallJumpVelocity = Vector3.zero;
                // S launches away from the camera heading while keeping the body facing
                // forward, including in fixed/parked views that normally turn toward WASD.
                bool backwardDive = input.y < -0.1f;
                if (backwardDive) transform.rotation = Quaternion.LookRotation(forward);
                rollDirection = backwardDive ? -forward : transform.forward;
                verticalSpeed = Mathf.Min(verticalSpeed, 0f);
            }

            if (!IsRolling && !IsDiving && !IsSliding && !IsCrouching && grounded && cooldownRemaining <= 0f && rollPressed && stamina.TrySpend(dashStaminaCost))
            {
                IsRolling = true;
                if (fightingAnimation) fightingAnimation.CancelFightingMotion();
                steeringWallJump = false;
                wallJumpVelocity = Vector3.zero;
                rollElapsed = 0f;
                activeRollDuration = Mathf.Max(0.1f, dashDuration);
                if (!FaceCameraDirection && direction.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.LookRotation(direction);
                rollDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : transform.forward;
            }

            if (jumpPressed)
            {
                if (grounded)
                {
                    // Jumping is available throughout a grounded dash; its forward motion
                    // and roll animation continue for the rest of the dash duration.
                    if (!IsDiving && !IsSliding && stamina.TrySpend(jumpStaminaCost))
                    {
                        if (fightingAnimation) fightingAnimation.CancelFightingMotion();
                        verticalSpeed = Mathf.Sqrt(jumpHeight * -2f * gravity);
                        if (IsRolling) CaptureDashMomentum();
                    }
                }
                else
                    TryWallJump(direction);
            }

            float attackSpeed = fightingAnimation ? fightingAnimation.AttackMovementMultiplier : 1f;
            float shootingSpeed = weaponCombat ? weaponCombat.ShootingMovementMultiplier : 1f;
            float aimingSpeed = weaponCombat ? weaponCombat.AimingMovementMultiplier : 1f;
            float weaponSpeed = weaponCombat ? weaponCombat.EquippedMovementMultiplier : 1f;
            bool canRun = PlayerControlEnabled && input.y > 0.1f && !IsCrouching && !IsRolling && !IsDiving && !IsSliding &&
                !(fightingAnimation && fightingAnimation.IsAttacking) &&
                !(weaponCombat && (weaponCombat.IsAiming || weaponCombat.IsReloading || weaponCombat.IsRecoveringFromShot || shootingSpeed < 0.999f));
            if (!canRun) runRequested = holdRunRequested = false;
            IsRunning = (runRequested || holdRunRequested) && canRun;
            combatSpeedWeight = Mathf.MoveTowards(combatSpeedWeight, attackSpeed, dt / Mathf.Max(0.01f, combatSlowdownTime));
            Vector3 requestedVelocity = direction * moveSpeed * (IsRunning ? runSpeedMultiplier : 1f) *
                combatSpeedWeight * weaponSpeed * aimingSpeed * shootingSpeed * (IsCrouching ? crouchSpeedMultiplier : 1f);
            if (grounded)
            {
                // Turn immediately; ease speed only so steering stays responsive.
                float targetSpeed = requestedVelocity.magnitude;
                float currentSpeed = horizontalVelocity.magnitude;
                float response = targetSpeed > currentSpeed ? accelerationTime : brakingTime;
                float easedSpeed = response <= 0f ? targetSpeed : Mathf.MoveTowards(currentSpeed, targetSpeed, moveSpeed * dt / response);
                horizontalVelocity = direction.sqrMagnitude > 0.000001f ? direction.normalized * easedSpeed
                    : horizontalVelocity.normalized * easedSpeed;
            }
            else
            {
                float control = Mathf.Clamp01(airControl);
                // Preserve the original immediate response at full control. Lower values
                // accelerate toward input instead of reducing takeoff speed.
                horizontalVelocity = control >= 1f ? requestedVelocity : Vector3.MoveTowards(
                    horizontalVelocity, requestedVelocity,
                    moveSpeed * 5f * control / Mathf.Max(0.001f, 1f - control) * dt);
            }
            Vector3 horizontalStep = horizontalVelocity * dt;
            if (IsRolling)
            {
                // Clamp the last step so dash distance is independent of frame rate.
                float rollStep = Mathf.Min(dt, activeRollDuration - rollElapsed);
                horizontalStep = rollDirection * (moveSpeed * dashSpeedMultiplier) * rollStep;
                // Keep full airborne velocity even on the frame containing the end of the roll.
                if (carryingDashMomentum) horizontalStep = dashAirVelocity * dt;
                rollElapsed += rollStep;
            }
            else if (carryingDashMomentum)
            {
                horizontalStep = dashAirVelocity * dt;
            }
            if (IsDiving)
                horizontalStep = rollDirection * (moveSpeed * diveSpeedMultiplier) * dt;
            bool slidingThisFrame = IsSliding;
            if (slidingThisFrame)
            {
                // Integrate constant friction, including a partial final frame.
                float friction = Mathf.Max(0.1f, slideDeceleration);
                float slideTime = Mathf.Min(dt, SlideSpeed / friction);
                float nextSpeed = Mathf.Max(0f, SlideSpeed - friction * slideTime);
                horizontalStep = rollDirection * ((SlideSpeed + nextSpeed) * 0.5f * slideTime);
                SlideSpeed = nextSpeed;
            }
            if (steeringWallJump)
            {
                Vector3 targetVelocity = direction * moveSpeed * (IsCrouching ? crouchSpeedMultiplier : 1f);
                float acceleration = Mathf.Max(1f, wallJumpSpeed) / Mathf.Max(0.05f, wallJumpPushTime);
                Vector3 previousVelocity = wallJumpVelocity;
                if (direction.sqrMagnitude > 0.000001f)
                    wallJumpVelocity = Vector3.MoveTowards(previousVelocity, targetVelocity, acceleration * Mathf.Clamp01(airControl) * dt);
                horizontalStep = (previousVelocity + wallJumpVelocity) * (0.5f * dt);
            }
            // Retained dash/wall-jump momentum cannot bypass the attack speed limit.
            if (fightingAnimation && fightingAnimation.IsAttacking)
                horizontalStep = Vector3.ClampMagnitude(horizontalStep, moveSpeed * combatSpeedWeight * dt);
            verticalSpeed += gravity * dt;
            Vector3 verticalStep = Vector3.up * verticalSpeed * dt;
            Vector3 beforeMove = transform.position;
            CollisionFlags collisions = controller.Move(horizontalStep + verticalStep);
            // Retain actual motion, including wall collisions, for the next airborne frame.
            horizontalVelocity = Vector3.ProjectOnPlane((transform.position - beforeMove) / dt, Vector3.up);

            if ((collisions & CollisionFlags.Below) != 0)
            {
                wallJumpsSinceGrounded = 0;
                ClearDashMomentum();
            }

            if (IsDiving && (collisions & CollisionFlags.Below) != 0)
            {
                IsDiving = false;
                IsSliding = true;
                SlideSpeed = moveSpeed * diveSpeedMultiplier;
                verticalSpeed = -2f;
            }

            if (IsSliding && ((collisions & CollisionFlags.Sides) != 0 ||
                (slidingThisFrame && (SlideSpeed <= 0f || (collisions & CollisionFlags.Below) == 0))))
            {
                IsSliding = false;
                SlideSpeed = 0f;
                cooldownRemaining = dashCooldown;
            }

            if ((collisions & CollisionFlags.Above) != 0 && verticalSpeed > 0f)
                verticalSpeed = 0f;

            if (transform.position.y < -10f) ResetTo(spawnPosition, transform.rotation);
        }

        private void OnTeleportRequested(Vector3 position, Quaternion facing) => ResetTo(position, facing);

        /// <summary>Places the player and clears every movement state, as the fall reset does.</summary>
        private void ResetTo(Vector3 position, Quaternion facing)
        {
            controller.enabled = false;
            transform.SetPositionAndRotation(position, facing);
            horizontalVelocity = Vector3.zero;
            combatSpeedWeight = 1f;
            controller.height = standingHeight;
            controller.center = standingCenter;
            IsCrouching = false;
            verticalSpeed = 0f;
            IsRolling = false;
            IsDiving = false;
            IsSliding = false;
            SlideSpeed = 0f;
            cooldownRemaining = 0f;
            steeringWallJump = false;
            wallJumpVelocity = Vector3.zero;
            wallJumpCooldown = 0f;
            wallJumpsSinceGrounded = 0;
            lastWallJumpCollider = null;
            controller.enabled = true;
            ClearDashMomentum();
            stamina.Refill();
        }
    }
}
