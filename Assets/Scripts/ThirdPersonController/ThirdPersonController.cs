using Unity.Cinemachine;
using UnityEngine;
using StarterAssets;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Third person controller (CharacterController based).
/// Handles horizontal movement and camera control. ParkourController owns
/// ground checks, jumping, gravity, and vertical CharacterController movement.
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

    public float CurrentSpeed => _horizontalVelocity.magnitude;
    public Vector3 HorizontalVelocity => _horizontalVelocity;
    public Vector3 Velocity => _horizontalVelocity + Vector3.up * (_parkour != null ? _parkour.VerticalVelocity : 0f);
    public bool IsSprinting => _input != null && _input.move != Vector2.zero && _input.run && _input.sprintParkour;

    public void SetJumpHorizontalVelocity(Vector3 velocity)
    {
        _horizontalVelocity = velocity;
    }

    public Vector3 GetJumpTargetVelocity()
    {
        if (_input == null || _input.move == Vector2.zero) return Vector3.zero;

        if (_mainCamera == null && Camera.main != null) _mainCamera = Camera.main.gameObject;
        float cameraYaw = _mainCamera != null ? _mainCamera.transform.eulerAngles.y : _cinemachineTargetYaw;
        Vector3 direction = Quaternion.Euler(0f,
            Mathf.Atan2(_input.move.x, _input.move.y) * Mathf.Rad2Deg + cameraYaw, 0f) * Vector3.forward;
        float speed = _input.run ? (IsSprinting ? SprintSpeed : RunSpeed) : MoveSpeed;
        float inputMagnitude = _input.analogMovement ? Mathf.Min(1f, _input.move.magnitude) : 1f;
        return direction * speed * inputMagnitude;
    }

    // ---------------------------------------------------------------- private state

    // cinemachine
    private float _cinemachineTargetYaw;
    private float _cinemachineTargetPitch;

    // player
    private Vector3 _horizontalVelocity;
    private float _animationBlend;
    private float _targetRotation;
    private float _rotationVelocity;

    // animation IDs
    private int _animIDSpeed;
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
        if (_hasControl) Move();
    }

    private void LateUpdate()
    {
        CameraRotation();
    }

    // --------------------------- setup helpers -------------------------------------

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
        _animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
    }

    // ---------------------------------------------------------------- camera

    private void CameraRotation()
    {
        if (CinemachineCameraTarget == null) return;

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
        if (!_parkour.Grounded) accel *= AirControl;
        _horizontalVelocity = Vector3.MoveTowards(
            _horizontalVelocity, desiredVelocity, accel * dt);

        // ---- follow slopes so we don't bounce down ramps
        Vector3 horizontal = _horizontalVelocity;
        if (_parkour.Grounded && _parkour.GroundNormal != Vector3.up)
        {
            Vector3 onSlope = Vector3.ProjectOnPlane(horizontal, _parkour.GroundNormal);
            if (onSlope.sqrMagnitude > 0.0001f)
            {
                horizontal = onSlope.normalized * _horizontalVelocity.magnitude;
            }
        }

        // ---- move
        _controller.Move(horizontal * dt);

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
        // Keep locomotion script-driven, but use the animation's authored root motion
        // while a parkour action is running.
        if (_parkour == null || !_parkour.IsPerformingAction || _animator == null)
            return;

        transform.position += _animator.deltaPosition;
        if (_parkour.ShouldApplyRootMotionRotation)
            transform.rotation *= _animator.deltaRotation;
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
        if (!hasControl) _parkour.ClearJumpInput();
    }
    public bool GetControl()
    {
        return _hasControl;
    }
}
