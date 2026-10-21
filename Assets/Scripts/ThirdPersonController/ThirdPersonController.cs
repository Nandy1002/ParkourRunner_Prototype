using System;
using Unity.Cinemachine;
using UnityEngine;
using StarterAssets;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Third person controller (CharacterController based).
/// Improvements over the stock version:
///  - Real momentum: acceleration / deceleration on the ground, reduced control in the air
///  - Slope-aware movement (no bouncing when walking down ramps)
///  - Head-bump handling (hitting a ceiling cancels upward velocity)
///  - Landing / jump events for VFX, camera shake, fall damage, etc.
///  - Zero per-frame allocations (NonAlloc physics queries, cached components)
///  - Separate mouse / gamepad sensitivity and optional Y inversion
///  - Hard-coded model fitting is now optional and configurable
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(MainInputAssets))]
[RequireComponent(typeof(ParkourController))]
#if ENABLE_INPUT_SYSTEM
[RequireComponent(typeof(PlayerInput))]
#endif
public class ThirdPersonController : MonoBehaviour
{
    [Header("Player")]
    [Tooltip("Move speed of the character in m/s")]
    public float MoveSpeed = 2.0f;

    [Tooltip("Run speed of the character in m/s")]
    public float RunSpeed = 5f;

    [Tooltip("Sprint speed of the character in m/s")]
    public float SprintSpeed = 8f;

    [Tooltip("How quickly the sprint animation blends up from the run animation")]
    public float SprintAnimationBuildUp = 4.0f;

    [Tooltip("How fast the character turns to face movement direction")]
    [Range(0.0f, 0.3f)]
    public float RotationSmoothTime = 0.12f;

    [Tooltip("How quickly the character speeds up (m/s^2) while there is move input")]
    public float Acceleration = 20.0f;

    [Tooltip("How quickly the character slows down (m/s^2) when there is no move input")]
    public float Deceleration = 30.0f;

    [Tooltip("Multiplier for acceleration / deceleration while airborne. 0 = no air control, 1 = same as ground")]
    [Range(0.0f, 1.0f)]
    public float AirControl = 0.4f;

    [Header("Audio")]
    public AudioClip LandingAudioClip;
    public AudioClip[] FootstepAudioClips;
    [Range(0, 1)] public float FootstepAudioVolume = 0.5f;

    [Header("Player Grounded")]
    [Tooltip("If the character is grounded or not. Not part of the CharacterController built in grounded check")]
    public bool Grounded = true;

    [Tooltip("Useful for rough ground (only used if there is no CharacterController)")]
    public float GroundedOffset = -0.14f;

    [Tooltip("The radius of the grounded check. Should match the radius of the CharacterController")]
    public float GroundedRadius = 0.28f;

    [Tooltip("What layers the character uses as ground. The player itself should NOT be on one of these layers")]
    public LayerMask GroundLayers;

    [Header("Cinemachine")]
    [Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
    public GameObject CinemachineCameraTarget;

    [Tooltip("Automatically assign the camera target to any CinemachineCamera that has no valid target")]
    public bool AutoWireCinemachine = true;

    [Tooltip("How far in degrees can you move the camera up")]
    public float TopClamp = 70.0f;

    [Tooltip("How far in degrees can you move the camera down")]
    public float BottomClamp = -30.0f;

    [Tooltip("Additional degrees to override the camera. Useful for fine tuning camera position when locked")]
    public float CameraAngleOverride = 0.0f;

    [Tooltip("For locking the camera position on all axis")]
    public bool LockCameraPosition = false;

    [Tooltip("Sensitivity multiplier for mouse look rotation")]
    public float CameraSensitivity = 0.1f;

    [Tooltip("Sensitivity multiplier for gamepad look rotation (scaled by deltaTime)")]
    public float GamepadSensitivity = 5.0f;

    [Tooltip("Invert vertical look")]
    public bool InvertY = false;

    [Header("Model Fitting (optional)")]
    [Tooltip("Apply the capsule + camera root values below on Awake. Automatically adapts to model scale")]
    public bool AutoFitToModel = true;
    public float CapsuleHeight = 1.5f;
    public Vector3 CapsuleCenter = new Vector3(0f, 0.75f, 0f);
    public float CapsuleRadius = 0.28f;
    public float StepOffset = 0.25f;
    public float SlopeLimit = 45.0f;
    public float SkinWidth = 0.02f;
    public Vector3 CameraRootLocalPosition = new Vector3(0f, 1.35f, 0f);

    // ---------------------------------------------------------------- public API

    /// <summary>Fired when the player touches the ground. Argument = downward impact speed (m/s).</summary>
    public event Action<float> Landed;

    public float CurrentSpeed => _horizontalVelocity.magnitude;
    public Vector3 Velocity => _horizontalVelocity + Vector3.up * (_parkour != null ? _parkour.VerticalVelocity : 0f);
    public bool IsSprinting => _input != null && _input.move != Vector2.zero && _input.run && _input.sprintParkour;

    // ---------------------------------------------------------------- private state

    // cinemachine
    private float _cinemachineTargetYaw;
    private float _cinemachineTargetPitch;

    // player
    private Vector3 _horizontalVelocity;
    private float _animationBlend;
    private float _targetRotation;
    private float _rotationVelocity;
    private Vector3 _groundNormal = Vector3.up;

    // grounded
    private bool _wasGrounded = true;
    private readonly Collider[] _groundHits = new Collider[16];
    private readonly RaycastHit[] _rayHits = new RaycastHit[8];

    // animation IDs
    private int _animIDSpeed;
    private int _animIDGrounded;
    private int _animIDMotionSpeed;

#if ENABLE_INPUT_SYSTEM
    private PlayerInput _playerInput;
#endif
    private Animator _animator;
    private CharacterController _controller;
    private MainInputAssets _input;
    private ParkourController _parkour;
    private GameObject _mainCamera;

    private const float _threshold = 0.01f;
    private const float SprintAnimationThreshold = 10f;

    private bool _hasAnimator;
    private bool _hasControl = true;

    private bool IsCurrentDeviceMouse
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Mouse.current.delta.ReadValue().sqrMagnitude > 0f) return true;
            if (_playerInput != null && !string.IsNullOrEmpty(_playerInput.currentControlScheme))
            {
                return _playerInput.currentControlScheme.Contains("Keyboard") ||
                       _playerInput.currentControlScheme.Contains("Mouse");
            }
            return true;
#else
            return false;
#endif
        }
    }

    // ---------------------------------------------------------------- lifecycle

    private void Awake()
    {
        AssignAnimationIDs();

        _controller = GetComponent<CharacterController>();
        _input = GetComponent<MainInputAssets>();
        _parkour = GetComponent<ParkourController>();
        if (_parkour == null) _parkour = gameObject.AddComponent<ParkourController>();
        _input.SetInputEnabled(_hasControl);
#if ENABLE_INPUT_SYSTEM
        _playerInput = GetComponent<PlayerInput>();
#endif

        // animator (cached once instead of every frame)
        if (!TryGetComponent(out _animator))
        {
            _animator = GetComponentInChildren<Animator>();
        }
        _hasAnimator = _animator != null;
        if (_hasAnimator) _animator.applyRootMotion = false;

        // main camera
        _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
        if (_mainCamera == null && Camera.main != null) _mainCamera = Camera.main.gameObject;

        SetupCameraTarget();

        if (AutoFitToModel)
        {
            float s = Mathf.Max(0.0001f, transform.lossyScale.y);
            _controller.height = CapsuleHeight / s;
            _controller.center = CapsuleCenter / s;
            _controller.radius = CapsuleRadius / s;
            _controller.stepOffset = StepOffset / s;
            _controller.slopeLimit = SlopeLimit;
            _controller.skinWidth = SkinWidth / s;
            _controller.minMoveDistance = 0f;

            if (CinemachineCameraTarget != null)
            {
                CinemachineCameraTarget.transform.localPosition = CameraRootLocalPosition / s;
            }
        }
    }

    private void Start()
    {
        _cinemachineTargetYaw = CinemachineCameraTarget != null
            ? CinemachineCameraTarget.transform.rotation.eulerAngles.y
            : transform.rotation.eulerAngles.y;

        if (AutoWireCinemachine) WireCinemachineCameras();

    }

    private void Update()
    {
        GroundedCheck();
        _parkour.Tick(Grounded, _hasControl);
        Move();
    }

    private void LateUpdate()
    {
        CameraRotation();
    }

    // ---------------------------------------------------------------- setup helpers

    private void SetupCameraTarget()
    {
        float scaleY = Mathf.Max(0.0001f, transform.lossyScale.y);
        Vector3 targetLocalPos = CameraRootLocalPosition / scaleY;

        if (CinemachineCameraTarget != null)
        {
            if (AutoFitToModel && CinemachineCameraTarget.transform.IsChildOf(transform))
            {
                CinemachineCameraTarget.transform.localPosition = targetLocalPos;
            }
            return;
        }

        Transform found = transform.Find("PlayerCameraRoot");
        if (found == null && transform.parent != null) found = transform.parent.Find("PlayerCameraRoot");

        if (found == null)
        {
            var camRoot = new GameObject("PlayerCameraRoot");
            camRoot.transform.SetParent(transform, false);
            camRoot.transform.localPosition = targetLocalPos;
            camRoot.tag = "CinemachineTarget";
            CinemachineCameraTarget = camRoot;
        }
        else
        {
            CinemachineCameraTarget = found.gameObject;
            if (AutoFitToModel && CinemachineCameraTarget.transform.IsChildOf(transform))
            {
                CinemachineCameraTarget.transform.localPosition = targetLocalPos;
            }
        }
    }

    private void WireCinemachineCameras()
    {
        if (CinemachineCameraTarget == null) return;

        var cams = FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);
        foreach (var cam in cams)
        {
            if (cam.Target.TrackingTarget == null || !cam.Target.TrackingTarget.gameObject.activeInHierarchy)
            {
                cam.Target.TrackingTarget = CinemachineCameraTarget.transform;
                cam.Target.CustomLookAtTarget = true;
                cam.Target.LookAtTarget = null;
            }
        }
    }

    private void AssignAnimationIDs()
    {
        _animIDSpeed = Animator.StringToHash("Speed");
        _animIDGrounded = Animator.StringToHash("Grounded");
        _animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
    }

    // ---------------------------------------------------------------- grounded

    private Vector3 GetGroundSpherePosition()
    {
        if (_controller != null)
        {
            Vector3 worldCenter = transform.TransformPoint(_controller.center);
            float worldHalfHeight = (_controller.height * transform.lossyScale.y) * 0.5f;
            Vector3 capsuleBottom = worldCenter - Vector3.up * worldHalfHeight;
            return capsuleBottom + Vector3.up * (GroundedRadius * 0.5f);
        }

        return new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z);
    }

    private bool IsOwnCollider(Collider col)
    {
        Transform t = col.transform;
        return t.IsChildOf(transform) || transform.IsChildOf(t);
    }

    private void GroundedCheck()
    {
        Vector3 spherePosition = GetGroundSpherePosition();

        int count = Physics.OverlapSphereNonAlloc(spherePosition, GroundedRadius, _groundHits, GroundLayers,
            QueryTriggerInteraction.Ignore);

        bool hasExternalGround = false;
        for (int i = 0; i < count; i++)
        {
            if (!IsOwnCollider(_groundHits[i]))
            {
                hasExternalGround = true;
                break;
            }
        }

        Grounded = hasExternalGround || _controller.isGrounded;

        // if we are moving upwards (just jumped) we are, by definition, not grounded
        if (_parkour.VerticalVelocity > 0f) Grounded = false;

        UpdateGroundNormal();

        if (Grounded && !_wasGrounded)
        {
            Landed?.Invoke(Mathf.Max(0f, -_parkour.VerticalVelocity));
        }
        _wasGrounded = Grounded;

        if (_hasAnimator) _animator.SetBool(_animIDGrounded, Grounded);
    }

    private void UpdateGroundNormal()
    {
        _groundNormal = Vector3.up;
        if (!Grounded) return;

        Vector3 worldCenter = transform.TransformPoint(_controller.center);
        float worldHalfHeight = (_controller.height * transform.lossyScale.y) * 0.5f;
        float worldRadius = _controller.radius * transform.lossyScale.x;
        Vector3 origin = worldCenter - Vector3.up * (worldHalfHeight - worldRadius);

        int count = Physics.RaycastNonAlloc(origin, Vector3.down, _rayHits, worldRadius + 0.3f,
            GroundLayers, QueryTriggerInteraction.Ignore);

        float best = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            if (IsOwnCollider(_rayHits[i].collider)) continue;
            if (_rayHits[i].distance < best)
            {
                best = _rayHits[i].distance;
                _groundNormal = _rayHits[i].normal;
            }
        }

        // ignore walls / too-steep surfaces, the CharacterController already handles those
        if (Vector3.Angle(_groundNormal, Vector3.up) > _controller.slopeLimit + 5f)
        {
            _groundNormal = Vector3.up;
        }
    }

    // ---------------------------------------------------------------- camera

    private void CameraRotation()
    {
        if (!_hasControl || CinemachineCameraTarget == null) return;

        if (_input.look.sqrMagnitude >= _threshold && !LockCameraPosition)
        {
            float sensitivity = IsCurrentDeviceMouse
                ? CameraSensitivity
                : Time.deltaTime * GamepadSensitivity;

            float yInput = InvertY ? -_input.look.y : _input.look.y;

            _cinemachineTargetYaw += _input.look.x * sensitivity;
            _cinemachineTargetPitch += yInput * sensitivity;
        }

        _cinemachineTargetYaw = Mathf.Repeat(_cinemachineTargetYaw, 360f);
        _cinemachineTargetPitch = Mathf.Clamp(_cinemachineTargetPitch, BottomClamp, TopClamp);

        CinemachineCameraTarget.transform.rotation = Quaternion.Euler(
            _cinemachineTargetPitch + CameraAngleOverride, _cinemachineTargetYaw, 0.0f);
    }

    // ---------------------------------------------------------------- movement

    private void Move()
    {
        float dt = Time.deltaTime;
        bool hasInput = _input.move != Vector2.zero;

        float targetSpeed = MoveSpeed;
        if (hasInput && _input.run)
        {
            targetSpeed = IsSprinting ? SprintSpeed : RunSpeed;
        }
        float inputMagnitude = _input.analogMovement ? Mathf.Min(1f, _input.move.magnitude) : 1f;

        // ---- desired velocity (camera relative)
        Vector3 desiredVelocity = Vector3.zero;
        if (hasInput)
        {
            if (_mainCamera == null && Camera.main != null) _mainCamera = Camera.main.gameObject;
            float camYaw = _mainCamera != null ? _mainCamera.transform.eulerAngles.y : _cinemachineTargetYaw;

            Vector2 dirInput = _input.move.normalized;
            _targetRotation = Mathf.Atan2(dirInput.x, dirInput.y) * Mathf.Rad2Deg + camYaw;

            Vector3 moveDir = Quaternion.Euler(0f, _targetRotation, 0f) * Vector3.forward;
            desiredVelocity = moveDir * (targetSpeed * inputMagnitude);

            // face the direction we are moving
            float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetRotation,
                ref _rotationVelocity, RotationSmoothTime);
            transform.rotation = Quaternion.Euler(0f, rotation, 0f);
        }

        // ---- accelerate / decelerate with momentum
        float accel = hasInput ? Acceleration : Deceleration;
        if (!Grounded) accel *= AirControl;
        _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, desiredVelocity, accel * dt);

        // ---- follow slopes so we don't bounce down ramps
        Vector3 horizontal = _horizontalVelocity;
        if (Grounded && _groundNormal != Vector3.up)
        {
            Vector3 onSlope = Vector3.ProjectOnPlane(horizontal, _groundNormal);
            if (onSlope.sqrMagnitude > 0.0001f)
            {
                horizontal = onSlope.normalized * _horizontalVelocity.magnitude;
            }
        }

        // ---- move
        Vector3 velocity = horizontal + Vector3.up * _parkour.VerticalVelocity;
        CollisionFlags flags = _controller.Move(velocity * dt);

        // bonk your head -> stop going up
        if ((flags & CollisionFlags.Above) != 0 && _parkour.VerticalVelocity > 0f)
        {
            _parkour.StopUpwardMovement();
        }

        // ---- animator
        float planarSpeed = _horizontalVelocity.magnitude;
        float targetAnimationSpeed = IsSprinting ? SprintAnimationThreshold : planarSpeed;
        float blendRate = IsSprinting && targetAnimationSpeed > _animationBlend
            ? SprintAnimationBuildUp
            : 25f;
        _animationBlend = Mathf.MoveTowards(_animationBlend, targetAnimationSpeed, dt * blendRate);
        if (_animationBlend < 0.01f) _animationBlend = 0f;

        if (_hasAnimator)
        {
            _animator.SetFloat(_animIDSpeed, _animationBlend);
            _animator.SetFloat(_animIDMotionSpeed, _input.analogMovement ? inputMagnitude : 1f);
        }
    }


    // ---------------------------------------------------------------- misc

    private void OnAnimatorMove()
    {
        // Defining OnAnimatorMove delegates root motion to script, suppressing Mecanim
        // from automatically overwriting transform.position with in-place (0,0,0) motion.
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Grounded ? new Color(0f, 1f, 0f, 0.35f) : new Color(1f, 0f, 0f, 0.35f);
        Gizmos.DrawSphere(GetGroundSpherePosition(), GroundedRadius);
    }

    private void OnFootstep(AnimationEvent animationEvent)
    {
        if (animationEvent.animatorClipInfo.weight <= 0.5f) return;
        if (FootstepAudioClips == null || FootstepAudioClips.Length == 0) return;

        int index = UnityEngine.Random.Range(0, FootstepAudioClips.Length);
        if (FootstepAudioClips[index] == null) return;

        AudioSource.PlayClipAtPoint(FootstepAudioClips[index],
            transform.TransformPoint(_controller.center), FootstepAudioVolume);
    }

    private void OnLand(AnimationEvent animationEvent)
    {
        if (animationEvent.animatorClipInfo.weight <= 0.5f) return;
        if (LandingAudioClip == null) return;

        AudioSource.PlayClipAtPoint(LandingAudioClip,
            transform.TransformPoint(_controller.center), FootstepAudioVolume);
    }
    public void SetControl(bool hasControl)
    {
        if (_hasControl == hasControl) return;
        _hasControl = hasControl;
        _input.SetInputEnabled(hasControl);
        if (!hasControl) _parkour.ClearJumpInput();
    }
    public bool GetControl()
    {
        return _hasControl;
    }
}
