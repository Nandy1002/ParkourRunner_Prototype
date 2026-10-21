using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
	public class MainInputAssets : MonoBehaviour
	{
		[Header("Character Input Values")]
		public Vector2 move;
		public Vector2 look;
		public bool jump;
		public bool run;
		public bool sprintParkour;

		[Header("Movement Settings")]
		public bool analogMovement;

		[Header("Mouse Cursor Settings")]
		public bool cursorLocked = true;
		public bool cursorInputForLook = true;

		private void Start()
		{
			SetCursorState(cursorLocked);
		}

		private bool _inputEnabled = true;

#if ENABLE_INPUT_SYSTEM
		private void Update()
		{
			if (!_inputEnabled) return;
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

				RunInput(Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
			}

			if (cursorInputForLook && Mouse.current != null)
			{
				Vector2 mouseDelta = Mouse.current.delta.ReadValue();
				LookInput(mouseDelta);
			}

			if (Mouse.current != null)
			{
				SprintParkourInput(Mouse.current.rightButton.isPressed);
			}
		}

		public void OnMove(InputValue value)
		{
			if (!_inputEnabled) return;
			MoveInput(value.Get<Vector2>());
		}

		public void OnLook(InputValue value)
		{
			if (!_inputEnabled) return;
			if(cursorInputForLook)
			{
				LookInput(value.Get<Vector2>());
			}
		}

		public void OnJump(InputValue value)
		{
			if (!_inputEnabled) return;
			JumpInput(value.isPressed);
		}

		public void OnRun(InputValue value)
		{
			if (!_inputEnabled) return;
			RunInput(value.isPressed);
		}

		public void OnSprintParkour(InputValue value)
		{
			if (!_inputEnabled) return;
			SprintParkourInput(value.isPressed);
		}
#endif

		public void MoveInput(Vector2 newMoveDirection)
		{
			if (!_inputEnabled) return;
			move = newMoveDirection;
		} 

		public void LookInput(Vector2 newLookDirection)
		{
			if (!_inputEnabled) return;
			look = newLookDirection;
		}

		public void JumpInput(bool newJumpState)
		{
			if (!_inputEnabled) return;
			jump = newJumpState;
		}

		public void RunInput(bool newRunState)
		{
			if (!_inputEnabled) return;
			run = newRunState;
		}

		public void SprintParkourInput(bool newState)
		{
			if (!_inputEnabled) return;
			sprintParkour = newState;
		}

		// Keeps existing mobile sprint buttons mapped to Run.
		public void SprintInput(bool newSprintState)
		{
			RunInput(newSprintState);
		}

		public void SetInputEnabled(bool enabled)
		{
			_inputEnabled = enabled;
			if (!enabled) ClearInput();
		}

		public void ClearInput()
		{
			move = Vector2.zero;
			look = Vector2.zero;
			jump = false;
			run = false;
			sprintParkour = false;
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
