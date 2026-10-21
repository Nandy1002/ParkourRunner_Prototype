using StarterAssets;
using UnityEngine;

/// <summary>Handles grounded state, jumping, and vertical movement.</summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(MainInputAssets))]
public class ParkourController : MonoBehaviour
{
    public LayerMask GroundLayers = 1;
    public float JumpHeight = 1.2f;
    public float Gravity = -15f;
    public float JumpBufferTime = 0.15f;
    [Range(0f, 1f)] public float JumpForwardMultiplier = 0.25f;

    public bool Grounded { get; private set; }
    public Vector3 GroundNormal { get; private set; } = Vector3.up;
    public float VerticalVelocity { get; private set; }

    private CharacterController _controller;
    private MainInputAssets _input;
    private ThirdPersonController _thirdPerson;
    private ObstacleScanner _obstacleScanner;
    private Animator _animator;
    private bool _jumpInProgress;
    private Vector3 _jumpForwardVelocity;
    private Vector3 _jumpForwardTarget;
    private float _jumpBuffer;

    private static readonly int GroundedId = Animator.StringToHash("Grounded");
    private static readonly int JumpId = Animator.StringToHash("Jump");
    private static readonly int FreeFallId = Animator.StringToHash("FreeFall");

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _input = GetComponent<MainInputAssets>();
        _thirdPerson = GetComponent<ThirdPersonController>();
        _obstacleScanner = GetComponent<ObstacleScanner>();
        _animator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        CheckGround();
        bool wasGrounded = Grounded;

        ReadJumpInput();
        ApplyGravity();
        MoveCharacter();

        CheckGround();
        HandleLanding(wasGrounded);
        UpdateJumpAnimation();

        _obstacleScanner.CheckForObstacle();
        
    }

    private void ReadJumpInput()
    {
        bool jumpPressed = _input.jump;
        _input.jump = false;
        bool hasControl = _thirdPerson == null || _thirdPerson.GetControl();

        if (jumpPressed && hasControl) _jumpBuffer = JumpBufferTime;
        else _jumpBuffer = Mathf.Max(0f, _jumpBuffer - Time.deltaTime);

        if (_jumpBuffer <= 0f || !Grounded || !hasControl) return;

        _jumpBuffer = 0f;
        VerticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
        _jumpForwardVelocity = _thirdPerson != null
            ? _thirdPerson.HorizontalVelocity * JumpForwardMultiplier
            : Vector3.zero;
        _jumpForwardTarget = _thirdPerson != null
            ? _thirdPerson.GetJumpTargetVelocity() * JumpForwardMultiplier
            : Vector3.zero;
        _jumpInProgress = true;
        if (_thirdPerson != null) _thirdPerson.SetControl(false);
        SetAnimation(JumpId, true);
    }

    private void ApplyGravity()
    {
        if (Grounded && VerticalVelocity < 0f) VerticalVelocity = -2f;
        if (!Grounded || VerticalVelocity > 0f)
            VerticalVelocity += Gravity * Time.deltaTime;
    }

    private void MoveCharacter()
    {
        Vector3 jumpMovement = _jumpInProgress ? _jumpForwardVelocity : Vector3.zero;
        if (_jumpInProgress && _thirdPerson != null)
        {
            float airAcceleration = _thirdPerson.Acceleration * _thirdPerson.AirControl;
            _jumpForwardVelocity = Vector3.MoveTowards(
                _jumpForwardVelocity, _jumpForwardTarget, airAcceleration * Time.deltaTime);
            jumpMovement = _jumpForwardVelocity;
            _thirdPerson.SetJumpHorizontalVelocity(jumpMovement);
        }
        Vector3 movement = jumpMovement + Vector3.up * VerticalVelocity;
        CollisionFlags hit = _controller.Move(movement * Time.deltaTime);
        if ((hit & CollisionFlags.Above) != 0 && VerticalVelocity > 0f)
            VerticalVelocity = 0f;
    }

    private void HandleLanding(bool wasGrounded)
    {
        if (!Grounded || VerticalVelocity > 0f) return;

        if (!wasGrounded && _jumpInProgress)
        {
            _jumpInProgress = false;
            _jumpForwardVelocity = Vector3.zero;
            _jumpForwardTarget = Vector3.zero;
            if (_thirdPerson != null) _thirdPerson.SetControl(true);
        }

        if (VerticalVelocity < 0f) VerticalVelocity = -2f;
    }

    private void UpdateJumpAnimation()
    {
        if (Grounded && VerticalVelocity <= 0f)
        {
            SetAnimation(JumpId, false);
            SetAnimation(FreeFallId, false);
        }
        else if (VerticalVelocity <= 0f)
        {
            SetAnimation(FreeFallId, true);
        }
    }

    private void CheckGround()
    {
        Grounded = _controller.isGrounded && VerticalVelocity <= 0f;
        GroundNormal = Vector3.up;

        Vector3 origin = transform.TransformPoint(_controller.center);
        origin.y -= _controller.height * transform.lossyScale.y * 0.5f - _controller.radius * transform.lossyScale.x;
        bool nearGround = Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 0.4f,
            GroundLayers, QueryTriggerInteraction.Ignore);
        Grounded = (Grounded || nearGround) && VerticalVelocity <= 0f;
        SetAnimation(GroundedId, Grounded);
        if (Grounded && nearGround)
        {
            GroundNormal = hit.normal;
        }
    }

    private void SetAnimation(int parameter, bool value)
    {
        if (_animator != null) _animator.SetBool(parameter, value);
    }

    public void ClearJumpInput()
    {
        _input.jump = false;
    }
}
