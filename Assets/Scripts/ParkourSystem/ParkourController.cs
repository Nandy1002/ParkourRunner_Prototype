using System;
using StarterAssets;
using UnityEngine;

/// <summary>Owns the player's grounded jump and vertical gravity.</summary>
[RequireComponent(typeof(MainInputAssets))]
public class ParkourController : MonoBehaviour
{
    [Header("Jump & Gravity")]
    [Tooltip("The height the player can jump")]
    public float JumpHeight = 1.2f;

    [Tooltip("The character's gravity")]
    public float Gravity = -15.0f;

    [Tooltip("Maximum falling speed in m/s")]
    public float TerminalVelocity = 53.0f;

    [Tooltip("Small downward velocity applied while grounded")]
    public float GroundedStickVelocity = -2.0f;

    public float VerticalVelocity { get; private set; }

    private MainInputAssets _input;
    private Animator _animator;
    private bool _hasAnimator;

    private static readonly int JumpId = Animator.StringToHash("Jump");
    private static readonly int FreeFallId = Animator.StringToHash("FreeFall");

    private void Awake()
    {
        _input = GetComponent<MainInputAssets>();
        if (!TryGetComponent(out _animator)) _animator = GetComponentInChildren<Animator>();
        _hasAnimator = _animator != null;
    }

    public void Tick(bool grounded, bool hasControl)
    {
        bool jumpPressed = hasControl && _input.jump;
        _input.jump = false;

        if (grounded)
        {
            if (VerticalVelocity < 0f) VerticalVelocity = GroundedStickVelocity;
            if (_hasAnimator)
            {
                _animator.SetBool(JumpId, false);
                _animator.SetBool(FreeFallId, false);
            }
        }

        if (jumpPressed && grounded)
        {
            VerticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
            if (_hasAnimator) _animator.SetBool(JumpId, true);
        }

        if (!grounded || VerticalVelocity > 0f)
        {
            VerticalVelocity = Mathf.Max(VerticalVelocity + Gravity * Time.deltaTime, -TerminalVelocity);
            if (_hasAnimator && VerticalVelocity <= 0f) _animator.SetBool(FreeFallId, true);
        }
    }

    public void ClearJumpInput()
    {
        if (_input != null) _input.jump = false;
    }

    public void StopUpwardMovement()
    {
        if (VerticalVelocity > 0f) VerticalVelocity = 0f;
    }
}
