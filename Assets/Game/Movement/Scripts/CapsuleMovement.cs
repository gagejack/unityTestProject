using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Movement
{
    /// <summary>
    /// Owns the player's travel: reads movement input, decides the movement state (walk, run, crouch, jump,
    /// wall jump, roll, dive, slide) and moves the CharacterController once per frame. Other systems change
    /// movement only through <see cref="IMovementModifier"/> components on the player and read the result
    /// from the public state below.
    /// </summary>
    [RequireComponent(typeof(CharacterController), typeof(PlayerStamina))]
    public sealed class CapsuleMovement : MonoBehaviour
    {
        [Tooltip("Walking speed in meters per second.")]
        [SerializeField, Min(0f)] private float moveSpeed = 4.5f;
        [Header("Running — hold Shift (any direction) or double-tap W")]
        [Tooltip("Running speed as a multiple of walking speed.")]
        [SerializeField, Min(1f)] private float runSpeedMultiplier = 1.5f;
        [Tooltip("Seconds allowed between two W presses for a double-tap run.")]
        [SerializeField, Range(0.1f, 0.5f)] private float forwardDoubleTapWindow = 0.28f;
        [Tooltip("Turning speed in degrees per second in fixed-camera mode. Third-person facing follows mouse-look directly.")]
        [InspectorName("Steering Speed")]
        [SerializeField, Min(0f)] private float turnSpeed = 360f;
        [Tooltip("Degrees per second when turning toward the aim direction outside third person.")]
        [SerializeField, Min(0f)] private float aimTurnSpeed = 720f;
        [Tooltip("Degrees per second the body turns toward the mouse cursor in fixed view, armed or not. 720 turns 180° in 0.25 s.")]
        [SerializeField, Min(0f)] private float cursorTurnSpeed = 720f;
        [Tooltip("Fixed view: while sprinting, the body faces the cursor only while it is within this many degrees of the running direction. Further round it faces the run, until the cursor comes back 20° inside this limit.")]
        [SerializeField, Range(20f, 180f)] private float sprintFacingLimit = 100f;
        [Tooltip("Height of an ordinary jump in meters, the same at every frame rate.")]
        [SerializeField, Min(0f)] private float jumpHeight = 1.6f;
        [Tooltip("Downward acceleration in meters per second squared (negative). Stronger gravity feels less floaty.")]
        [SerializeField] private float gravity = -22f;
        [Tooltip("Midair steering strength: 0 keeps takeoff momentum, 1 gives full control. Also scales dash-jump and wall-jump steering. Ground movement and dives are unaffected.")]
        [SerializeField, Range(0f, 1f)] private float airControl = 1f;
        [Header("Movement response")]
        [Tooltip("Seconds to go from standing to walking speed. Lower is snappier.")]
        [SerializeField, Min(0f)] private float accelerationTime = 0.1f;
        [Tooltip("Seconds to stop from walking speed. Lower stops shorter.")]
        [SerializeField, Min(0f)] private float brakingTime = 0.08f;
        [Tooltip("Seconds to ease into and out of an attack's slowdown.")]
        [SerializeField, Min(0.01f)] private float combatSlowdownTime = 0.05f;
        [Tooltip("Farthest drop, in meters, the character follows while walking or running down slopes and steps instead of falling. Larger drops are real falls.")]
        [SerializeField, Min(0f)] private float groundSnapDistance = 0.35f;
        [Header("Stamina costs")]
        [Tooltip("Stamina spent by a dash roll.")]
        [SerializeField, Min(0f)] private float dashStaminaCost = 25f;
        [Tooltip("Stamina spent by an air dive.")]
        [SerializeField, Min(0f)] private float diveStaminaCost = 20f;
        [Tooltip("Stamina spent by a jump or wall jump.")]
        [SerializeField, Min(0f)] private float jumpStaminaCost = 10f;
        [Header("Wall jump")]
        [Tooltip("Maximum successful wall jumps before touching the ground. Set to 0 to disable wall jumps.")]
        [InspectorName("Max Wall Jumps")]
        [SerializeField, Min(0)] private int maxWallJumps = 2;
        [Tooltip("Height of a wall jump in meters.")]
        [SerializeField, Min(0f)] private float wallJumpHeight = 1.8f;
        [Tooltip("Push away from the wall in meters per second.")]
        [SerializeField, Min(0f)] private float wallJumpSpeed = 8f;
        [Tooltip("Time for air steering to change horizontal speed by Wall Jump Speed. Lower values respond faster.")]
        [InspectorName("Wall Jump Control Time")]
        [SerializeField, Min(0.05f)] private float wallJumpPushTime = 0.2f;
        [Tooltip("How much camera-relative WASD input affects the initial bounce away from the wall.")]
        [SerializeField, Range(0f, 1f)] private float wallJumpInputInfluence = 0.5f;
        [Header("Crouch")]
        [Tooltip("Collision capsule height while crouched, in meters.")]
        [SerializeField, Min(0.1f)] private float crouchHeight = 1.3f;
        [Tooltip("Crouch-walking speed as a fraction of walking speed.")]
        [SerializeField, Range(0f, 1f)] private float crouchSpeedMultiplier = 0.5f;
        [Header("Dash roll")]
        [Tooltip("Roll speed as a multiple of walking speed.")]
        [SerializeField, Min(0f)] private float dashSpeedMultiplier = 1f;
        [Tooltip("Seconds a roll lasts.")]
        [SerializeField, Min(0.1f)] private float dashDuration = 0.5f;
        [Tooltip("Seconds after a roll or slide before the next roll.")]
        [SerializeField, Min(0f)] private float dashCooldown = 0.35f;
        [Tooltip("Degrees per second that held WASD turns momentum carried from a roll into the air. Releasing input keeps the current velocity.")]
        [SerializeField, Min(0f)] private float dashAirTurnSpeed = 180f;
        [Header("Air dive")]
        [Tooltip("Dive speed as a multiple of walking speed. Caps Lock in the air dives; hold S to dive backward.")]
        [SerializeField, Min(0f)] private float diveSpeedMultiplier = 1f;
        [Header("Dive landing slide")]
        [Tooltip("How quickly the landing slide slows down, in meters per second squared.")]
        [SerializeField, Min(0.1f)] private float slideDeceleration = 26f;

        // ---- State other systems read (calculated once per frame here) ----
        public bool PlayerControlEnabled => PlayerControl.Enabled;
        public bool IsGrounded => controller && controller.isGrounded;
        public float VerticalVelocity => verticalSpeed;
        public Vector3 PlanarVelocity => horizontalVelocity;
        public bool IsRunning { get; private set; }
        public bool IsCrouching { get; private set; }
        public float CrouchDrop => standingHeight - controller.height;
        public bool IsRolling { get; private set; }
        public bool IsDiving { get; private set; }
        public bool IsSliding { get; private set; }
        public float SlideSpeed { get; private set; }
        public Vector3 RollDirection => carryingDashMomentum && dashAirVelocity.sqrMagnitude > 0.001f ? dashAirVelocity.normalized : rollDirection;
        public bool IsRollingBackward => IsRolling && Vector3.Dot(rollDirection, transform.forward) < 0f;
        public bool IsDivingBackward => (IsDiving || IsSliding) && Vector3.Dot(rollDirection, transform.forward) < 0f;
        public float RollProgress => IsRolling ? Mathf.Clamp01(rollElapsed / activeRollDuration) : 0f;
        /// <summary>Fixed view: sprinting with the cursor too far behind, so the body faces the run instead of the cursor.</summary>
        public bool SprintFacesTravel { get; private set; }

        /// <summary>Third-person facing: the body faces the view heading. Fixed view faces the cursor's aim point; parked views turn toward travel.</summary>
        public bool FaceCameraDirection { get; set; } = true;

        /// <summary>The camera the player steers by.</summary>
        public Transform MovementCamera
        {
            get => movementCamera;
            set { movementCamera = value; view = value ? value.GetComponent<IViewHeading>() : null; }
        }

        // ---- Collaborators ----
        CharacterController controller;
        PlayerStamina stamina;
        Transform movementCamera;
        IViewHeading view;
        readonly List<IMovementModifier> modifiers = new List<IMovementModifier>();
        InputAction moveAction, jumpAction, rollAction, crouchAction;

        // ---- Movement state ----
        Vector3 horizontalVelocity;
        float verticalSpeed;
        float combatSpeedWeight = 1f;
        Vector3 spawnPosition;
        bool runRequested, holdRunRequested;
        float lastForwardTap = float.NegativeInfinity;
        float standingHeight;
        Vector3 standingCenter;
        readonly Collider[] standingObstacles = new Collider[32];
        bool steeringWallJump;
        float wallJumpCooldown;
        int wallJumpsSinceGrounded;
        Vector3 wallJumpVelocity;
        Collider lastWallJumpCollider;
        readonly RaycastHit[] wallHits = new RaycastHit[16];
        readonly RaycastHit[] groundHits = new RaycastHit[16];
        readonly Vector3[] wallNormals = new Vector3[8];
        bool carryingDashMomentum;
        Vector3 dashAirVelocity;
        float rollElapsed, activeRollDuration, cooldownRemaining;
        Vector3 rollDirection;

        // ---- This frame's combined modifier effect ----
        float directSpeedMultiplier = 1f, easedSpeedTarget = 1f;
        bool runningBlocked, committedMovesBlocked, momentumLimited;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            stamina = GetComponent<PlayerStamina>();
            GetComponents(modifiers);
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
            PlayerRequests.TeleportRequested += ResetTo;
        }

        private void OnEnable()
        {
            PlayerPresence.Register(transform);
            moveAction.Enable();
            jumpAction.Enable();
            rollAction.Enable();
            crouchAction.Enable();
        }

        private void OnDisable()
        {
            PlayerPresence.Unregister(transform);
            runRequested = holdRunRequested = IsRunning = false;
            lastForwardTap = float.NegativeInfinity;
            horizontalVelocity = Vector3.zero;
            combatSpeedWeight = 1f;
            moveAction.Disable();
            jumpAction.Disable();
            rollAction.Disable();
            crouchAction.Disable();
            IsRolling = IsDiving = IsSliding = false;
            SlideSpeed = 0f;
            cooldownRemaining = 0f;
            EndWallJumpSteering();
            wallJumpCooldown = 0f;
            lastWallJumpCollider = null;
            ClearDashMomentum();
        }

        private void OnDestroy()
        {
            PlayerRequests.TeleportRequested -= ResetTo;
            moveAction.Dispose();
            jumpAction.Dispose();
            rollAction.Dispose();
            crouchAction.Dispose();
        }

        private void Update()
        {
            ReadModifiers(Time.deltaTime);
            var keyboard = Keyboard.current;
            Vector2 move = moveAction.ReadValue<Vector2>();
            UpdateRunInput(Time.unscaledTime, keyboard != null && keyboard.wKey.wasPressedThisFrame,
                keyboard != null && keyboard.wKey.isPressed, move.sqrMagnitude > 0.01f,
                keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed));
            Step(Time.deltaTime, move, jumpAction.WasPressedThisFrame(), rollAction.WasPressedThisFrame());
        }

        /// <summary>One frame of movement: decide the state, then move the capsule once.</summary>
        void Step(float dt, Vector2 moveInput, bool jumpPressed, bool rollPressed)
        {
            if (dt <= 0f) return;
            if (!PlayerControlEnabled) { moveInput = Vector2.zero; jumpPressed = rollPressed = false; }
            // Steering and walking stay available; only an attack's swing commits jump, roll and crouch.
            if (committedMovesBlocked) jumpPressed = rollPressed = false;
            AdvanceTimers(dt);
            if (!committedMovesBlocked && !IsRolling && !IsDiving && !IsSliding) UpdateCrouch();

            Vector2 input = Vector2.ClampMagnitude(new Vector2(Mathf.Clamp(moveInput.x, -1f, 1f), Mathf.Clamp(moveInput.y, -1f, 1f)), 1f);
            bool grounded = controller.isGrounded;
            if (grounded) ResetAirAllowances();
            if (grounded && verticalSpeed < 0f) verticalSpeed = -2f;

            Vector3 forward = Heading();
            Vector3 direction = forward * input.y + Vector3.Cross(Vector3.up, forward) * input.x;
            if (!IsRolling && !IsDiving && !IsSliding)
            {
                UpdateFacing(forward, direction, dt);
                SteerDashMomentum(direction, dt);
            }

            if (rollPressed && !grounded) TryStartDive(input, forward);
            if (rollPressed && grounded) TryStartRoll(direction);
            if (jumpPressed)
            {
                if (grounded) TryJump();
                else TryWallJump(direction);
            }

            UpdateRunning(input);
            combatSpeedWeight = Mathf.MoveTowards(combatSpeedWeight, easedSpeedTarget, dt / Mathf.Max(0.01f, combatSlowdownTime));
            Vector3 requestedVelocity = direction * moveSpeed * (IsRunning ? runSpeedMultiplier : 1f) *
                combatSpeedWeight * directSpeedMultiplier * (IsCrouching ? crouchSpeedMultiplier : 1f);

            Vector3 endVelocity = TravelVelocity(dt, grounded, direction, requestedVelocity, out Vector3 horizontalStep);
            bool slidingThisFrame = IsSliding;
            horizontalStep = CommittedMoveStep(dt, direction, horizontalStep);
            // Retained dash or wall-jump momentum cannot bypass an attack's speed limit.
            if (momentumLimited) horizontalStep = Vector3.ClampMagnitude(horizontalStep, moveSpeed * combatSpeedWeight * dt);

            // Exact constant-acceleration step: the same jump height at every frame rate.
            Vector3 verticalStep = Vector3.up * (verticalSpeed * dt + 0.5f * gravity * dt * dt);
            verticalSpeed += gravity * dt;

            Vector3 before = transform.position;
            CollisionFlags collisions = controller.Move(horizontalStep + verticalStep);
            if (grounded && !controller.isGrounded) collisions |= SnapToGround();
            KeepVelocityAfterMove(transform.position - before, horizontalStep, endVelocity, dt);
            ResolveCollisions(collisions, slidingThisFrame);
            if (transform.position.y < -10f) ResetTo(spawnPosition, transform.rotation);
        }

        // ---------------------------------------------------------------- Modifiers and input

        void ReadModifiers(float dt)
        {
            directSpeedMultiplier = easedSpeedTarget = 1f;
            runningBlocked = committedMovesBlocked = momentumLimited = false;
            foreach (var modifier in modifiers)
            {
                if (modifier is Behaviour b && !b.isActiveAndEnabled) continue;
                if (modifier.EaseSpeedChange) easedSpeedTarget *= Mathf.Clamp01(modifier.SpeedMultiplier);
                else directSpeedMultiplier *= Mathf.Clamp01(modifier.SpeedMultiplier);
                runningBlocked |= modifier.BlocksRunning;
                committedMovesBlocked |= modifier.BlocksCommittedMoves;
                momentumLimited |= modifier.LimitsMomentum;
            }
        }

        void Announce(MovementAction action)
        {
            foreach (var modifier in modifiers) modifier.OnMovementAction(action);
        }

        bool TryGetModifierFacing(out Vector3 planarForward)
        {
            foreach (var modifier in modifiers)
                if (!(modifier is Behaviour b && !b.isActiveAndEnabled) && modifier.TryGetFacing(out planarForward)) return true;
            planarForward = Vector3.zero;
            return false;
        }

        void UpdateRunInput(float now, bool forwardPressed, bool forwardHeld, bool moveHeld, bool runModifierHeld)
        {
            // No focus check: the Input System already releases held keys when the window loses focus.
            bool blocked = !PlayerControlEnabled || IsCrouching || IsRolling || IsDiving || IsSliding || runningBlocked;
            // Either Shift key requests running in any direction; a double-tap run needs W.
            holdRunRequested = !blocked && moveHeld && runModifierHeld;
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

        void UpdateRunning(Vector2 input)
        {
            bool canRun = PlayerControlEnabled && input.sqrMagnitude > 0.01f && !IsCrouching && !IsRolling && !IsDiving && !IsSliding && !runningBlocked;
            if (!canRun) runRequested = holdRunRequested = false;
            // Shift runs in any direction; a double-tap W run only while moving forward.
            IsRunning = canRun && (holdRunRequested || (runRequested && input.y > 0.1f));
        }

        // ---------------------------------------------------------------- Heading and facing

        // This frame's view heading, independent of camera follow smoothing and pitch.
        Vector3 Heading()
        {
            Vector3 forward = FaceCameraDirection && view != null && view.FacesViewHeading ? view.PlanarForward
                : movementCamera ? Vector3.ProjectOnPlane(movementCamera.forward, Vector3.up).normalized
                : Vector3.forward;
            if (forward.sqrMagnitude < 0.001f) forward = view != null ? view.PlanarForward : Vector3.forward;
            return forward;
        }

        void UpdateFacing(Vector3 forward, Vector3 direction, float dt)
        {
            bool directView = FaceCameraDirection && view != null && view.FacesViewHeading;
            bool wasSprintingAway = SprintFacesTravel;
            SprintFacesTravel = false;
            if (view != null && view.FacesAimPoint)
            {
                // Fixed view: the cursor sets the facing, including while aiming, unless a sprint runs away from it.
                // Turning about the vertical axis keeps a 180° turn upright.
                Vector3 toAim = Vector3.ProjectOnPlane(view.AimPoint - transform.position, Vector3.up);
                SprintFacesTravel = SprintsAwayFrom(toAim, direction, wasSprintingAway);
                Vector3 face = SprintFacesTravel ? direction : toAim;
                if (face.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.Euler(0f, Mathf.MoveTowardsAngle(transform.eulerAngles.y,
                        Quaternion.LookRotation(face).eulerAngles.y, cursorTurnSpeed * dt), 0f);
            }
            else if (TryGetModifierFacing(out Vector3 aimForward))
            {
                var aim = Quaternion.LookRotation(aimForward.sqrMagnitude > 0.001f ? aimForward : forward);
                // Mouse-facing movement keeps the same direct response when aiming starts.
                transform.rotation = directView ? aim : Quaternion.RotateTowards(transform.rotation, aim, aimTurnSpeed * dt);
            }
            else if (FaceCameraDirection) transform.rotation = Quaternion.LookRotation(forward);
            else if (direction.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), turnSpeed * dt);
        }

        // Sprinting with the cursor beyond the Sprint Facing Limit from the run. Once facing the run, the cursor must come
        // 20° back inside the limit before the body turns to it again, so a cursor near the limit does not flicker.
        bool SprintsAwayFrom(Vector3 toAim, Vector3 direction, bool wasSprintingAway)
        {
            if (!IsRunning || runningBlocked || direction.sqrMagnitude < 0.001f || toAim.sqrMagnitude < 0.0001f) return false;
            return Vector3.Angle(direction, toAim) > (wasSprintingAway ? sprintFacingLimit - 20f : sprintFacingLimit);
        }

        void SteerDashMomentum(Vector3 direction, float dt)
        {
            if (carryingDashMomentum && direction.sqrMagnitude > 0.001f)
                dashAirVelocity = Vector3.RotateTowards(dashAirVelocity, direction * dashAirVelocity.magnitude,
                    dashAirTurnSpeed * Mathf.Clamp01(airControl) * Mathf.Deg2Rad * dt, 0f);
        }

        // ---------------------------------------------------------------- Timers and allowances

        void AdvanceTimers(float dt)
        {
            // Capture before the roll timer expires, including a roll that just left a ledge.
            if (IsRolling && !controller.isGrounded && !carryingDashMomentum) CaptureDashMomentum();
            wallJumpCooldown = Mathf.Max(0f, wallJumpCooldown - dt);
            cooldownRemaining = Mathf.Max(0f, cooldownRemaining - dt);
            if (IsRolling && rollElapsed >= activeRollDuration)
            {
                IsRolling = false;
                cooldownRemaining = dashCooldown;
            }
        }

        void ResetAirAllowances()
        {
            lastWallJumpCollider = null;
            wallJumpsSinceGrounded = 0;
            EndWallJumpSteering();
            ClearDashMomentum();
        }

        // ---------------------------------------------------------------- Committed moves

        void TryJump()
        {
            // Jumping works throughout a grounded roll; the roll's momentum carries into the air.
            if (IsDiving || IsSliding || !stamina.TrySpend(jumpStaminaCost)) return;
            Announce(MovementAction.Jump);
            verticalSpeed = Mathf.Sqrt(jumpHeight * -2f * gravity);
            if (IsRolling) CaptureDashMomentum();
        }

        void TryStartRoll(Vector3 direction)
        {
            if (IsRolling || IsDiving || IsSliding || IsCrouching || cooldownRemaining > 0f || !stamina.TrySpend(dashStaminaCost)) return;
            IsRolling = true;
            Announce(MovementAction.Roll);
            EndWallJumpSteering();
            rollElapsed = 0f;
            activeRollDuration = Mathf.Max(0.1f, dashDuration);
            if (!FaceCameraDirection && direction.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(direction);
            rollDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : transform.forward;
        }

        // Caps Lock in the air: a committed dive from exactly where the player is (no upward impulse).
        // A fresh press can also turn a roll that left a ledge into a dive. Hold S to dive backward.
        void TryStartDive(Vector2 input, Vector3 forward)
        {
            if (IsCrouching || IsDiving || IsSliding || !stamina.TrySpend(diveStaminaCost)) return;
            IsRolling = false;
            IsDiving = true;
            Announce(MovementAction.Dive);
            ClearDashMomentum();
            EndWallJumpSteering();
            // A backward dive launches away from the view heading while the body keeps facing forward.
            bool backward = input.y < -0.1f;
            if (backward) transform.rotation = Quaternion.LookRotation(forward);
            rollDirection = backward ? -forward : transform.forward;
            verticalSpeed = Mathf.Min(verticalSpeed, 0f);
        }

        bool TryWallJump(Vector3 requestedDirection)
        {
            if (controller.isGrounded || wallJumpCooldown > 0f || wallJumpsSinceGrounded >= maxWallJumps) return false;
            if (!FindWall(out Vector3 outward, out int normalCount, out Collider wall)) return false;
            if (!stamina.TrySpend(jumpStaminaCost)) return false;
            // Push away from the wall, then add held input. Input into the wall brakes the bounce
            // instead of being reflected into a full-strength push.
            Vector3 launch = outward * wallJumpSpeed + requestedDirection * moveSpeed * wallJumpInputInfluence * Mathf.Clamp01(airControl);
            for (int n = 0; n < normalCount; n++)
            {
                float separationSpeed = Vector3.Dot(launch, wallNormals[n]);
                if (separationSpeed < 1f) launch += wallNormals[n] * (1f - separationSpeed);
            }
            lastWallJumpCollider = wall;
            Announce(MovementAction.WallJump);
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

        // Probes current wall contact around the capsule rather than trusting a stale collision.
        bool FindWall(out Vector3 outward, out int normalCount, out Collider jumpWall)
        {
            Vector3 scale = transform.lossyScale;
            float radius = controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float skin = controller.skinWidth * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            Vector3 center = transform.TransformPoint(controller.center);
            Vector3 extent = Vector3.up * Mathf.Max(0f, controller.height * Mathf.Abs(scale.y) * 0.5f - radius);
            normalCount = 0;
            outward = Vector3.zero;
            jumpWall = null;
            float closestWall = float.PositiveInfinity;
            for (int side = 0; side < 8; side++)
            {
                Vector3 probe = Quaternion.Euler(0f, side * 45f, 0f) * Vector3.forward;
                int hits = Physics.CapsuleCastNonAlloc(center - extent, center + extent, Mathf.Max(0.01f, radius - skin), probe,
                    wallHits, skin + 0.07f, Physics.AllLayers, QueryTriggerInteraction.Ignore);
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
                    for (int n = 0; n < normalCount; n++) if (Vector3.Dot(normal, wallNormals[n]) > 0.95f) duplicate = true;
                    if (duplicate || normalCount == wallNormals.Length) continue;
                    wallNormals[normalCount++] = normal;
                    outward += normal;
                }
            }
            if (normalCount == 0 || jumpWall == null) return false;
            outward.Normalize();
            return true;
        }

        void EndWallJumpSteering()
        {
            steeringWallJump = false;
            wallJumpVelocity = Vector3.zero;
        }

        void ClearDashMomentum()
        {
            carryingDashMomentum = false;
            dashAirVelocity = Vector3.zero;
        }

        void CaptureDashMomentum()
        {
            carryingDashMomentum = true;
            dashAirVelocity = rollDirection * (moveSpeed * dashSpeedMultiplier);
        }

        // ---------------------------------------------------------------- Integration

        /// <summary>
        /// Ordinary walking/running/air travel. Returns the velocity at the end of the frame and the step for this frame.
        /// On the ground, speed changes at a constant rate and the step is integrated exactly, so start and stopping
        /// distances are the same at every frame rate.
        /// </summary>
        Vector3 TravelVelocity(float dt, bool grounded, Vector3 direction, Vector3 requestedVelocity, out Vector3 step)
        {
            if (grounded)
            {
                // Turn immediately; ease speed only, so steering stays responsive.
                float targetSpeed = requestedVelocity.magnitude;
                float currentSpeed = horizontalVelocity.magnitude;
                float response = targetSpeed > currentSpeed ? accelerationTime : brakingTime;
                Vector3 heading = direction.sqrMagnitude > 0.000001f ? direction.normalized : horizontalVelocity.normalized;
                if (response <= 0f)
                {
                    step = heading * (targetSpeed * dt);
                    return heading * targetSpeed;
                }
                float rate = moveSpeed / response;
                float reachTime = Mathf.Abs(targetSpeed - currentSpeed) / Mathf.Max(0.0001f, rate);
                float endSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, rate * dt);
                float distance = reachTime >= dt
                    ? (currentSpeed + endSpeed) * 0.5f * dt
                    : (currentSpeed + targetSpeed) * 0.5f * reachTime + targetSpeed * (dt - reachTime);
                step = heading * distance;
                return heading * endSpeed;
            }
            float control = Mathf.Clamp01(airControl);
            // Full control keeps the immediate response; lower values accelerate toward input instead.
            Vector3 next = control >= 1f ? requestedVelocity : Vector3.MoveTowards(horizontalVelocity, requestedVelocity,
                moveSpeed * 5f * control / Mathf.Max(0.001f, 1f - control) * dt);
            step = control >= 1f ? next * dt : (horizontalVelocity + next) * (0.5f * dt);
            return next;
        }

        /// <summary>Rolls, carried dash momentum, dives, landing slides and wall-jump steering replace ordinary travel.</summary>
        Vector3 CommittedMoveStep(float dt, Vector3 direction, Vector3 step)
        {
            if (IsRolling)
            {
                // Clamp the last step so roll distance is independent of frame rate.
                float rollStep = Mathf.Min(dt, activeRollDuration - rollElapsed);
                step = carryingDashMomentum ? dashAirVelocity * dt : rollDirection * (moveSpeed * dashSpeedMultiplier) * rollStep;
                rollElapsed += rollStep;
            }
            else if (carryingDashMomentum) step = dashAirVelocity * dt;
            if (IsDiving) step = rollDirection * (moveSpeed * diveSpeedMultiplier) * dt;
            if (IsSliding)
            {
                // Constant friction, including a partial final frame.
                float friction = Mathf.Max(0.1f, slideDeceleration);
                float slideTime = Mathf.Min(dt, SlideSpeed / friction);
                float nextSpeed = Mathf.Max(0f, SlideSpeed - friction * slideTime);
                step = rollDirection * ((SlideSpeed + nextSpeed) * 0.5f * slideTime);
                SlideSpeed = nextSpeed;
            }
            if (steeringWallJump)
            {
                Vector3 target = direction * moveSpeed * (IsCrouching ? crouchSpeedMultiplier : 1f);
                float acceleration = Mathf.Max(1f, wallJumpSpeed) / Mathf.Max(0.05f, wallJumpPushTime);
                Vector3 previous = wallJumpVelocity;
                if (direction.sqrMagnitude > 0.000001f)
                    wallJumpVelocity = Vector3.MoveTowards(previous, target, acceleration * Mathf.Clamp01(airControl) * dt);
                step = (previous + wallJumpVelocity) * (0.5f * dt);
            }
            return step;
        }

        // Keep the end-of-frame speed, reduced in proportion when a wall or slope shortened the step,
        // and follow the direction actually travelled (sliding along walls).
        void KeepVelocityAfterMove(Vector3 moved, Vector3 intendedStep, Vector3 endVelocity, float dt)
        {
            Vector3 actual = Vector3.ProjectOnPlane(moved, Vector3.up) / dt;
            Vector3 intended = Vector3.ProjectOnPlane(intendedStep, Vector3.up) / dt;
            if (intended.sqrMagnitude < 0.000001f || IsRolling || IsDiving || IsSliding || steeringWallJump || carryingDashMomentum)
            {
                horizontalVelocity = actual;
                return;
            }
            float kept = Mathf.Clamp01(actual.magnitude / intended.magnitude);
            Vector3 heading = actual.sqrMagnitude > 0.000001f ? actual.normalized : endVelocity.normalized;
            horizontalVelocity = heading * (endVelocity.magnitude * kept);
        }

        /// <summary>
        /// Walking or running off the top of a slope or a step would otherwise leave the ground for a few frames
        /// (a fall, a bounce and the airborne pose). When the character was grounded at the start of the frame and
        /// is not jumping, diving or wall jumping, it follows walkable ground up to Ground Snap Distance below.
        /// </summary>
        CollisionFlags SnapToGround()
        {
            if (verticalSpeed > 0f || IsDiving || steeringWallJump || groundSnapDistance <= 0f) return CollisionFlags.None;
            Vector3 scale = transform.lossyScale;
            float radius = controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float skin = controller.skinWidth;
            Vector3 center = transform.TransformPoint(controller.center);
            Vector3 extent = Vector3.up * Mathf.Max(0f, controller.height * Mathf.Abs(scale.y) * 0.5f - radius);
            int count = Physics.CapsuleCastNonAlloc(center + extent, center - extent, Mathf.Max(0.01f, radius - skin), Vector3.down,
                groundHits, groundSnapDistance + skin, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = groundHits[i];
                if (hit.distance <= 0f || hit.collider.transform.IsChildOf(transform) || Physics.GetIgnoreCollision(controller, hit.collider)) continue;
                if (Vector3.Angle(hit.normal, Vector3.up) > controller.slopeLimit) continue;
                nearest = Mathf.Min(nearest, hit.distance);
            }
            if (float.IsPositiveInfinity(nearest)) return CollisionFlags.None;
            CollisionFlags snapped = controller.Move(Vector3.down * (nearest + skin));
            if ((snapped & CollisionFlags.Below) != 0) verticalSpeed = -2f;
            return snapped;
        }

        void ResolveCollisions(CollisionFlags collisions, bool slidingThisFrame)
        {
            bool below = (collisions & CollisionFlags.Below) != 0;
            if (below)
            {
                wallJumpsSinceGrounded = 0;
                ClearDashMomentum();
            }
            if (IsDiving && below)
            {
                IsDiving = false;
                IsSliding = true;
                SlideSpeed = moveSpeed * diveSpeedMultiplier;
                verticalSpeed = -2f;
            }
            if (IsSliding && ((collisions & CollisionFlags.Sides) != 0 || (slidingThisFrame && (SlideSpeed <= 0f || !below))))
            {
                IsSliding = false;
                SlideSpeed = 0f;
                cooldownRemaining = dashCooldown;
            }
            if ((collisions & CollisionFlags.Above) != 0 && verticalSpeed > 0f) verticalSpeed = 0f;
        }

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (Mathf.Abs(hit.normal.y) > 0.2f) return;
            // Walls remove momentum that points into them.
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

        // ---------------------------------------------------------------- Crouch

        void UpdateCrouch()
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

        bool CanStand()
        {
            Vector3 scale = transform.lossyScale;
            float radius = Mathf.Max(0.01f, controller.radius - controller.skinWidth) * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float halfHeight = standingHeight * Mathf.Abs(scale.y) * 0.5f;
            Vector3 center = transform.TransformPoint(standingCenter);
            Vector3 offset = transform.up * Mathf.Max(0f, halfHeight - controller.radius * Mathf.Abs(scale.y));
            int count = Physics.OverlapCapsuleNonAlloc(center - offset, center + offset, radius, standingObstacles, Physics.AllLayers, QueryTriggerInteraction.Ignore);
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

        // ---------------------------------------------------------------- Teleport and fall reset

        /// <summary>Places the player and clears every movement state (fall reset and test teleports).</summary>
        void ResetTo(Vector3 position, Quaternion facing)
        {
            controller.enabled = false;
            transform.SetPositionAndRotation(position, facing);
            horizontalVelocity = Vector3.zero;
            combatSpeedWeight = 1f;
            controller.height = standingHeight;
            controller.center = standingCenter;
            IsCrouching = false;
            SprintFacesTravel = false;
            verticalSpeed = 0f;
            IsRolling = IsDiving = IsSliding = false;
            SlideSpeed = 0f;
            cooldownRemaining = 0f;
            EndWallJumpSteering();
            wallJumpCooldown = 0f;
            wallJumpsSinceGrounded = 0;
            lastWallJumpCollider = null;
            controller.enabled = true;
            ClearDashMomentum();
            stamina.Refill();
        }
    }
}
