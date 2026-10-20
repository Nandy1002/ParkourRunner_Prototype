using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
	public class StarterAssetsInputs : MonoBehaviour
	{
		[Header("Character Input Values")]
		public Vector2 move;
		public Vector2 look;
		public bool jump;
		public bool sprint;

		[Header("Movement Settings")]
		public bool analogMovement;

		[Header("Mouse Cursor Settings")]
		public bool cursorLocked = true;
		public bool cursorInputForLook = true;

		private void Start()
		{
			SetCursorState(cursorLocked);
		}

#if ENABLE_INPUT_SYSTEM
		private void Update()
		{
			// Direct fallback so controls ALWAYS respond even if PlayerInput message routing is inactive
			if (Keyboard.current != null)
			{
				Vector2 kbMove = Vector2.zero;
				if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) kbMove.y += 1f;
				if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) kbMove.y -= 1f;
				if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) kbMove.x -= 1f;
				if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) kbMove.x += 1f;

				if (kbMove.sqrMagnitude > 0.01f)
				{
					MoveInput(kbMove.normalized);
				}
				else if (Keyboard.current.wKey.wasReleasedThisFrame || Keyboard.current.sKey.wasReleasedThisFrame ||
				         Keyboard.current.aKey.wasReleasedThisFrame || Keyboard.current.dKey.wasReleasedThisFrame ||
				         Keyboard.current.upArrowKey.wasReleasedThisFrame || Keyboard.current.downArrowKey.wasReleasedThisFrame ||
				         Keyboard.current.leftArrowKey.wasReleasedThisFrame || Keyboard.current.rightArrowKey.wasReleasedThisFrame)
				{
					MoveInput(Vector2.zero);
				}

				if (Keyboard.current.spaceKey.wasPressedThisFrame)
				{
					JumpInput(true);
				}
				else if (Keyboard.current.spaceKey.wasReleasedThisFrame)
				{
					JumpInput(false);
				}

				if (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed)
				{
					SprintInput(true);
				}
				else if (Keyboard.current.leftShiftKey.wasReleasedThisFrame || Keyboard.current.rightShiftKey.wasReleasedThisFrame)
				{
					SprintInput(false);
				}
			}

			if (cursorInputForLook && Mouse.current != null)
			{
				Vector2 mouseDelta = Mouse.current.delta.ReadValue();
				LookInput(mouseDelta);
			}
		}

		public void OnMove(InputValue value)
		{
			MoveInput(value.Get<Vector2>());
		}

		public void OnLook(InputValue value)
		{
			if(cursorInputForLook)
			{
				LookInput(value.Get<Vector2>());
			}
		}

		public void OnJump(InputValue value)
		{
			JumpInput(value.isPressed);
		}

		public void OnSprint(InputValue value)
		{
			SprintInput(value.isPressed);
		}
#endif

		public void MoveInput(Vector2 newMoveDirection)
		{
			move = newMoveDirection;
		} 

		public void LookInput(Vector2 newLookDirection)
		{
			look = newLookDirection;
		}

		public void JumpInput(bool newJumpState)
		{
			jump = newJumpState;
		}

		public void SprintInput(bool newSprintState)
		{
			sprint = newSprintState;
		}
		
		private void OnApplicationFocus(bool hasFocus)
		{
			SetCursorState(cursorLocked);
		}

		private void SetCursorState(bool newState)
		{
			Cursor.lockState = newState ? CursorLockMode.Locked : CursorLockMode.None;
		}
	}
}
