using System;
using System.Collections;
using System.Collections.Generic;
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
    public ParkourActionSO CurrentParkourAction { get; private set; }
    public bool IsPerformingAction => inAction;
    public bool ShouldApplyRootMotionRotation => inAction &&
        CurrentParkourAction != null && !CurrentParkourAction.RotateToObstacle;

    public List<ParkourActionSO> parkourActions;
    public AudioClip femaleGruntVoice;
    [SerializeField, Min(0f)] private float parkourRotationSpeed = 500f;

    private CharacterController _controller;
    private MainInputAssets _input;
    private ThirdPersonController _thirdPerson;
    private ObstacleScanner _obstacleScanner;
    private Animator _animator;
    private bool _jumpInProgress;
    private Vector3 _jumpForwardVelocity;
    private Vector3 _jumpForwardTarget;
    private float _jumpBuffer;
    private float _parkourInputBuffer;
    private bool inAction;

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
        if (inAction)
        {
            _input.jump = false;
            _jumpBuffer = 0f;
            _parkourInputBuffer = 0f;
            return;
        }

        CheckGround();
        bool wasGrounded = Grounded;

        // Space is reserved for requesting a parkour action; normal jumping stays disabled.
        bool parkourRequested = _input.jump;
        _input.jump = false;
        _jumpBuffer = 0f;
        if (parkourRequested)
            _parkourInputBuffer = JumpBufferTime;
        else
            _parkourInputBuffer = Mathf.Max(0f, _parkourInputBuffer - Time.deltaTime);
        ApplyGravity();
        MoveCharacter();

        CheckGround();
        HandleLanding(wasGrounded);
        UpdateJumpAnimation();

        if (_obstacleScanner != null)
            _obstacleScanner.GetObstacleData(GetMaximumActionTriggerDistance());

        TryDoParkour(_parkourInputBuffer > 0f);
        
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
    private State GetCurrentPlayerState()
    {
        if (_thirdPerson != null && _thirdPerson.IsSprinting)
            return State.Sprint;

        if (_input != null && _input.run)
            return State.Run;

        return State.Walk;
    }

    private float GetMaximumActionTriggerDistance()
    {
        float maxDistance = 0f;
        if (parkourActions == null)
            return maxDistance;

        foreach (ParkourActionSO action in parkourActions)
        {
            if (action != null)
                maxDistance = Mathf.Max(maxDistance, action.TriggerDistance);
        }

        return maxDistance;
    }

    private void TryDoParkour(bool parkourRequested)
    {
        if (!parkourRequested || inAction || !Grounded || _jumpInProgress ||
            _obstacleScanner == null || parkourActions == null)
            return;

        ObstacleData obstacle = _obstacleScanner.currentObstacle;
        if (obstacle == null)
        {
            CurrentParkourAction = null;
            return;
        }

        State state = GetCurrentPlayerState();
        foreach (ParkourActionSO action in parkourActions)
        {
            if (action == null || !action.CanDOAction(obstacle, state))
                continue;

            _parkourInputBuffer = 0f;
            CurrentParkourAction = action;
            action.MatchPos = obstacle.heightHitPoint;
            Vector3 wallNormal = Vector3.ProjectOnPlane(obstacle.obstacleNormal, Vector3.up).normalized;
            Vector3 startPositionOffset;
            if (action.RotateToObstacle && wallNormal.sqrMagnitude > 0.0001f)
            {
                // Measure and correct distance perpendicular to the wall. Backing
                // up along the incoming direction leaves the player too close when
                // approaching at an angle, so root motion can carry them into it.
                float distanceFromWall = Vector3.Dot(
                    transform.position - obstacle.heightHitPoint, wallNormal);
                float distanceToRestore = Mathf.Max(0f, action.TriggerDistance - distanceFromWall);
                startPositionOffset = wallNormal * distanceToRestore;
            }
            else
            {
                Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
                float distanceToRestore = Mathf.Max(0f, action.TriggerDistance - obstacle.distanceToObstacle);
                startPositionOffset = -forward * distanceToRestore;
            }
            StartCoroutine(DoParkourAction(action, obstacle, startPositionOffset));
            return;
        }

        CurrentParkourAction = null;
    }
    private IEnumerator DoParkourAction(
        ParkourActionSO parkourAction,
        ObstacleData obstacleData,
        Vector3 startPositionOffset)
    {
        if (_animator == null || string.IsNullOrWhiteSpace(parkourAction.AnimationStateName))
        {
            Debug.LogWarning(
                $"Parkour action '{parkourAction.name}' needs an Animator and an animation state name.",
                this);
            CurrentParkourAction = null;
            yield break;
        }

        inAction = true;
        CurrentParkourAction = parkourAction;
        AudioSource.PlayClipAtPoint(femaleGruntVoice,transform.position);
        bool restoreControl = _thirdPerson != null && _thirdPerson.GetControl();
        bool restoreController = _controller.enabled;
        if (_thirdPerson != null)
        {
            _thirdPerson.SetControl(false);
            _thirdPerson.SetJumpHorizontalVelocity(Vector3.zero);
        }

        Vector3 targetForward = Vector3.ProjectOnPlane(-obstacleData.obstacleNormal, Vector3.up);
        if (targetForward.sqrMagnitude < 0.0001f && _obstacleScanner != null)
            targetForward = Vector3.ProjectOnPlane(_obstacleScanner.transform.forward, Vector3.up);
        if (targetForward.sqrMagnitude < 0.0001f)
            targetForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        Quaternion targetRotation = targetForward.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(targetForward.normalized, Vector3.up)
            : transform.rotation;

        // The upright CharacterController blocks the animation's path over the
        // obstacle, so root motion moves the transform directly during this action.
        _controller.enabled = false;
        transform.position += startPositionOffset;
        if (parkourAction.RotateToObstacle)
            transform.rotation = targetRotation;

        _jumpBuffer = 0f;
        SetAnimation(JumpId, false);
        SetAnimation(FreeFallId, false);

        _animator.Play(parkourAction.AnimationStateName, 0, 0f);
        yield return null;

        float elapsed = 0f;
        AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
        if (!stateInfo.IsName(parkourAction.AnimationStateName))
        {
            Debug.LogWarning(
                $"Animator state '{parkourAction.AnimationStateName}' for parkour action " +
                $"'{parkourAction.name}' was not found on layer 0.",
                this);
        }
        else
        {
            float timeout = Mathf.Max(stateInfo.length + 1f, 1f);
            while (stateInfo.IsName(parkourAction.AnimationStateName) &&
                   stateInfo.normalizedTime < 1f && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                if (parkourAction.RotateToObstacle)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, parkourRotationSpeed * Time.deltaTime);

                if (parkourAction.EnableTargetMatching)
                {
                    MatchTarget(parkourAction);
                }

                yield return null;
                stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
            }
        }

        FinishParkourAction(restoreControl, restoreController);
    }

    private void FinishParkourAction(bool restoreControl, bool restoreController)
    {
        if (restoreControl)
            _thirdPerson.SetControl(true);

        _controller.enabled = restoreController;
        inAction = false;
        CurrentParkourAction = null;
    }
    private void MatchTarget(ParkourActionSO action)
    {
        if(_animator.isMatchingTarget) return;

        _animator.MatchTarget(action.MatchPos,
                            transform.rotation,
                            action.MatchBodyPart,
                            new MatchTargetWeightMask(action.MatchPosWeight,0),
                            action.MatchStartTime,
                            action.MatchTargetTime);
    }
}
