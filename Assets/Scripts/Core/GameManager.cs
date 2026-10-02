using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Wolfenstein.Core
{
    /// <summary>
    /// Owns the high-level game state and the behaviour that follows directly from it,
    /// such as whether the mouse cursor is captured.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameManager : MonoBehaviour
    {
        public event Action<GameState> StateChanged;

        /// <summary>Raised with the new value whenever the mouse is captured or freed.</summary>
        public event Action<bool> CursorCaptureChanged;

        public GameState CurrentState { get; private set; } = GameState.Loading;

        /// <summary>
        /// Whether the mouse is captured for play. It is captured while playing, except that in the editor
        /// Escape frees it until the next click in the Game view.
        /// </summary>
        public bool IsCursorCaptured { get; private set; }

        public void SetState(GameState newState)
        {
            if (newState == CurrentState)
                return;

            CurrentState = newState;
            ApplyCursorState();
            StateChanged?.Invoke(newState);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
                ApplyCursorState();
        }

#if UNITY_EDITOR
        // The editor frees the cursor when Escape is pressed, and clicking in the Game view captures it again.
        // The capture waits for the button to be released, so the click is not also taken as a shot.
        // Cursor.lockState is checked as well because the editor can free the cursor in other ways, but it cannot
        // be the only check: in batch mode the lock never takes effect.
        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (IsCursorCaptured && keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                SetCursorCaptured(false);

            Pointer pointer = Pointer.current;
            bool cursorFree = !IsCursorCaptured || Cursor.lockState != CursorLockMode.Locked;
            if (cursorFree && pointer != null && pointer.press.wasReleasedThisFrame)
                ApplyCursorState();
        }
#endif

        private void ApplyCursorState() => SetCursorCaptured(CurrentState == GameState.Playing);

        private void SetCursorCaptured(bool captured)
        {
            Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !captured;

            if (captured == IsCursorCaptured)
                return;

            IsCursorCaptured = captured;
            CursorCaptureChanged?.Invoke(captured);
        }
    }
}
