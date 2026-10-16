using UnityEngine;
using UnityEngine.InputSystem;

namespace StarterGame
{
    [DefaultExecutionOrder(-100)]
    public sealed class FollowCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 9f, -12f);
        [SerializeField, Min(0.01f)] private float smoothTime = 0.12f;
        [Header("Third-person view")]
        [SerializeField] private Vector3 thirdPersonOffset = new Vector3(0f, 2.3f, -5f);
        [SerializeField] private float lookHeight = 0.5f;
        [Tooltip("Degrees of camera rotation per pixel of mouse movement.")]
        [SerializeField, Min(0f)] private float mouseSensitivity = 0.15f;
        [SerializeField] private bool invertMouseY;
        [Tooltip("Upward look limit. -90 allows aiming straight up.")]
        [SerializeField, Range(-90f, 0f)] private float minimumPitch = -90f;
        [SerializeField, Range(0f, 85f)] private float maximumPitch = 75f;
        [SerializeField, Min(0.01f)] private float collisionRadius = 0.2f;
        [Header("Weapon aiming")]
        [SerializeField, Range(30f, 80f)] private float aimFieldOfView = 50f;
        [SerializeField, Min(0.5f)] private float aimCameraDistance = 3.2f;
        [SerializeField, Range(0f, 1f)] private float aimShoulderOffset = 0.5f;
        [Tooltip("Extra camera height while aiming, blended with the shoulder view to clear the character and gun.")]
        [SerializeField, Range(0f, 1f)] private float aimHeightOffset = 0.3f;
        [Tooltip("Position-follow delay while aiming. Lower values reduce sideways camera lag without changing mouse sensitivity.")]
        [SerializeField, Min(0.01f)] private float aimFollowSmoothTime = 0.06f;
        [Header("Combat and landing feel")]
        [SerializeField, Range(0f, 1f)] private float recoilScale = 0.7f;
        [SerializeField, Min(0.01f)] private float collisionRecoveryTime = 0.1f;
        [SerializeField, Range(0f, 0.15f)] private float maximumLandingDip = 0.045f;
        [Header("Free roam")]
        [SerializeField, Min(0.1f)] private float freeRoamSpeed = 8f;
        [SerializeField, Min(1f)] private float freeRoamBoost = 3f;
        public bool IsFreeRoam { get; private set; }
        public bool IsFlying { get; private set; }
        private Vector2 recoilOffset;
        private Vector2 recoilTarget;
        private Vector2 recoilVelocity;
        private float recoilHoldUntil;
        private float recoilKickTime = 0.04f;
        private float recoilRecovery = 0.14f;
        private float collisionDistance;
        private float collisionVelocity;
        private CapsuleMovement movement;
        private bool wasGrounded;
        private float fallingSpeed;
        private float landingDip;
        private Camera viewCamera;
        private WeaponCombatController weapons;
        private float normalFieldOfView;
        public bool ThirdPerson { get; private set; }
        public Vector3 PlanarForward => Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        private Quaternion AimRotation => Quaternion.Euler(Mathf.Clamp(pitch - recoilOffset.y, minimumPitch, maximumPitch), yaw + recoilOffset.x, 0f);
        public Vector3 AimDirection => AimRotation * Vector3.forward;
        private Vector3 velocity;
        private Vector3 smoothedFocus;
        private float yaw;
        private float pitch;

        private void Awake()
        {
            viewCamera = GetComponent<Camera>();
            normalFieldOfView = viewCamera ? viewCamera.fieldOfView : 60f;
            weapons = target ? target.GetComponent<WeaponCombatController>() : null;
            movement = target ? target.GetComponent<CapsuleMovement>() : null;
        }

        public void SetThirdPerson(bool value)
        {
            IsFreeRoam = false;
            IsFlying = false;
            ThirdPerson = value;
            SetMouseCaptured(value && Application.isFocused);
            velocity = Vector3.zero;
            collisionDistance = 0f;
            recoilTarget = recoilOffset = recoilVelocity = Vector2.zero;

            if (target == null) return;
            yaw = target.eulerAngles.y;
            Vector3 viewOffset = thirdPersonOffset - Vector3.up * lookHeight;
            pitch = Mathf.Clamp(Mathf.Atan2(viewOffset.y, new Vector2(viewOffset.x, viewOffset.z).magnitude)
                * Mathf.Rad2Deg, minimumPitch, maximumPitch);
            UpdateView(true);
        }

        public void BeginFreeRoam()
        {
            // Start exactly at the displayed view, including its current look angle.
            if (!IsFreeRoam)
            {
                if (ThirdPerson)
                {
                    yaw = Mathf.Repeat(yaw + recoilOffset.x, 360f);
                    pitch = Mathf.Clamp(pitch - recoilOffset.y, minimumPitch, maximumPitch);
                }
                else
                {
                    yaw = transform.eulerAngles.y;
                    pitch = Mathf.DeltaAngle(0f, transform.eulerAngles.x);
                }
            }
            IsFreeRoam = true;
            IsFlying = true;
            ThirdPerson = false;
            velocity = Vector3.zero;
            recoilTarget = recoilOffset = recoilVelocity = Vector2.zero;
            if (viewCamera) viewCamera.fieldOfView = normalFieldOfView;
            if (movement) movement.FaceCameraDirection = false;
            SetMouseCaptured(Application.isFocused);
        }

        public void ToggleFreeRoamControl()
        {
            if (!IsFreeRoam) { BeginFreeRoam(); return; }
            IsFlying = !IsFlying;
            SetMouseCaptured(IsFlying && Application.isFocused);
        }

        private void Start()
        {
            if (target == null)
            {
                Debug.LogError("FollowCamera needs the Player assigned as its target.", this);
                enabled = false;
                return;
            }

            if (!IsFreeRoam) SetThirdPerson(ThirdPerson);
        }

        public void SetMouseCaptured(bool captured)
        {
            var menu = GetComponent<ViewModeDebugMenu>();
            captured = captured && (ThirdPerson || IsFlying) && isActiveAndEnabled && (!menu || !menu.IsOpen);
            Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !captured;
        }

        private void OnDisable()
        {
            IsFlying = false;
            SetMouseCaptured(false);
            if (viewCamera) viewCamera.fieldOfView = normalFieldOfView;
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) SetMouseCaptured(false);
        }

        private void Update()
        {
            if ((ThirdPerson || IsFlying) && Application.isFocused)
            {
                bool release = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
                if (release)
                    SetMouseCaptured(false);
                else if (Mouse.current != null)
                {
                    if (Cursor.lockState == CursorLockMode.Locked)
                    {
                        if (IsFlying) ApplyFreeRoamLook(Mouse.current.delta.ReadValue());
                        else ApplyMouseLook(Mouse.current.delta.ReadValue());
                    }
                    else if (Mouse.current.leftButton.wasPressedThisFrame ||
                        (weapons && weapons.HasWeapon && Mouse.current.rightButton.wasPressedThisFrame))
                    {
                        var menu = GetComponent<ViewModeDebugMenu>();
                        if (menu == null || !menu.ContainsPointer(Mouse.current.position.ReadValue()))
                        {
                            if (menu && menu.IsOpen) menu.SetOpen(false);
                            else SetMouseCaptured(true);
                        }
                        // Ignore the capture frame's delta to avoid a cursor-warp camera jump.
                    }
                }
            }
            if (IsFlying && Application.isFocused && Cursor.lockState == CursorLockMode.Locked && Keyboard.current != null)
            {
                var keys = Keyboard.current;
                Vector2 input = new Vector2((keys.dKey.isPressed ? 1f : 0f) - (keys.aKey.isPressed ? 1f : 0f),
                    (keys.wKey.isPressed ? 1f : 0f) - (keys.sKey.isPressed ? 1f : 0f));
                float vertical = (keys.spaceKey.isPressed ? 1f : 0f) - (keys.leftCtrlKey.isPressed || keys.rightCtrlKey.isPressed ? 1f : 0f);
                MoveFreeRoam(Time.deltaTime, input, vertical, keys.leftShiftKey.isPressed || keys.rightShiftKey.isPressed);
            }
        }

        private void ApplyFreeRoamLook(Vector2 delta)
        {
            yaw = Mathf.Repeat(yaw + delta.x * mouseSensitivity, 360f);
            pitch = Mathf.Clamp(pitch + delta.y * mouseSensitivity * (invertMouseY ? 1f : -1f), -90f, 90f);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        private void MoveFreeRoam(float dt, Vector2 input, float vertical, bool boost)
        {
            if (!IsFlying || dt <= 0f) return;
            Vector3 direction = transform.forward * input.y + transform.right * input.x + Vector3.up * vertical;
            transform.position += Vector3.ClampMagnitude(direction, 1f) * (freeRoamSpeed * (boost ? freeRoamBoost : 1f) * dt);
        }

        private void LateUpdate()
        {
            if (IsFreeRoam) return;
            float dt = Time.deltaTime;
            if (Time.time >= recoilHoldUntil) recoilTarget = Vector2.zero;
            recoilOffset = Vector2.SmoothDamp(recoilOffset, recoilTarget, ref recoilVelocity,
                recoilTarget.sqrMagnitude > recoilOffset.sqrMagnitude ? recoilKickTime : recoilRecovery, Mathf.Infinity, dt);
            if (movement)
            {
                if (movement.IsGrounded && !wasGrounded && fallingSpeed > 2f)
                    landingDip = Mathf.Min(maximumLandingDip, fallingSpeed * 0.004f);
                if (!movement.IsGrounded) fallingSpeed = Mathf.Max(0f, -movement.VerticalVelocity);
                wasGrounded = movement.IsGrounded;
            }
            landingDip = Mathf.MoveTowards(landingDip, 0f, dt * maximumLandingDip / 0.15f);
            UpdateView(false);
        }

        public void AddRecoil(Vector2 kick, float response, float recovery)
        {
            if (!ThirdPerson) return;
            recoilTarget.x = Mathf.Clamp(recoilTarget.x + Random.Range(-kick.x, kick.x) * recoilScale, -2f, 2f);
            recoilTarget.y = Mathf.Clamp(recoilTarget.y + kick.y * recoilScale, 0f, 5f);
            recoilKickTime = Mathf.Max(0.01f, response);
            recoilRecovery = Mathf.Max(0.01f, recovery);
            recoilHoldUntil = Time.time + recoilKickTime;
        }
        private void ApplyMouseLook(Vector2 delta)
        {
            // Mouse delta already measures motion this frame; do not multiply by deltaTime.
            yaw = Mathf.Repeat(yaw + delta.x * mouseSensitivity, 360f);
            pitch = Mathf.Clamp(pitch + delta.y * mouseSensitivity * (invertMouseY ? 1f : -1f),
                minimumPitch, maximumPitch);
        }

        private void UpdateView(bool snap)
        {
            if (IsFreeRoam) return;
            if (target == null) return;
            float aim = ThirdPerson && weapons ? weapons.AimWeight : 0f;
            if (viewCamera) viewCamera.fieldOfView = Mathf.Lerp(normalFieldOfView, aimFieldOfView, aim);
            if (!ThirdPerson)
            {
                transform.position = snap ? target.position + offset : Vector3.SmoothDamp(
                    transform.position, target.position + offset, ref velocity, smoothTime);
                transform.rotation = Quaternion.LookRotation(Vector3.up * 0.5f - offset);
                return;
            }


            Vector3 collisionOrigin = target.position + Vector3.up * (lookHeight + aimHeightOffset * aim - landingDip);
            Vector3 focus = collisionOrigin + Quaternion.Euler(0f, yaw, 0f) * Vector3.right * (aimShoulderOffset * aim);
            float orbitDistance = Mathf.Max(0.1f, (thirdPersonOffset - Vector3.up * lookHeight).magnitude);
            orbitDistance = Mathf.Lerp(orbitDistance, aimCameraDistance, aim);
            // Preserve yaw/roll at the vertical pole, where forward alone cannot
            // determine which way is screen-up. Recoil cannot tip past that pole.
            Quaternion lookRotation = AimRotation;
            float followTime = Mathf.Lerp(smoothTime, aimFollowSmoothTime, aim);
            // Smooth the follow anchor, not the orbit or look direction. Looking back
            // at an unsmoothed target would turn sideways whenever strafing creates lag.
            smoothedFocus = snap ? focus : Vector3.SmoothDamp(smoothedFocus, focus, ref velocity, followTime);
            // Continue looking upward without orbiting the camera underneath the
            // character. Keep the previous lowest orbit angle for camera placement.
            Quaternion orbitRotation = Quaternion.Euler(Mathf.Max(-30f, Mathf.Clamp(pitch - recoilOffset.y, minimumPitch, maximumPitch)), yaw + recoilOffset.x, 0f);
            Vector3 candidate = smoothedFocus + orbitRotation * Vector3.back * orbitDistance;
            // Check the final camera path each frame so smoothing cannot carry it through a wall.
            Vector3 path = candidate - collisionOrigin;
            float distance = path.magnitude;
            if (distance > 0.001f)
            {
                float safeDistance = distance;
                foreach (RaycastHit hit in Physics.SphereCastAll(collisionOrigin, collisionRadius, path / distance,
                    distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.transform.IsChildOf(target)) continue;
                    safeDistance = Mathf.Min(safeDistance, Mathf.Max(0.01f, hit.distance - 0.05f));
                }
                if (snap || collisionDistance <= 0f || safeDistance < collisionDistance) { collisionDistance = safeDistance; collisionVelocity = 0f; }
                else collisionDistance = Mathf.SmoothDamp(collisionDistance, safeDistance, ref collisionVelocity, collisionRecoveryTime);
                transform.position = collisionOrigin + path.normalized * Mathf.Min(safeDistance, collisionDistance);
            }
            transform.rotation = lookRotation;
        }
    }
}
