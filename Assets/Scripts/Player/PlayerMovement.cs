using UnityEngine;

namespace Wolfenstein.Player
{
    /// <summary>
    /// Moves the player through a CharacterController: walking with acceleration, gravity and jumping.
    /// It has no Update of its own; <see cref="PlayerController"/> drives it once per frame.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMovement : MonoBehaviour
    {
        [Header("Walking")]
        [SerializeField, Min(0f), Tooltip("Top walking speed in metres per second.")]
        private float moveSpeed = 6f;

        [SerializeField, Min(0f), Tooltip("How quickly the player speeds up and stops on the ground (m/s²).")]
        private float groundAcceleration = 60f;

        [SerializeField, Min(0f), Tooltip("Acceleration while airborne. Lower than ground acceleration for limited air control (m/s²).")]
        private float airAcceleration = 12f;

        [Header("Jumping & Gravity")]
        [SerializeField, Min(0f), Tooltip("Height of the jump apex in metres.")]
        private float jumpHeight = 1f;

        [SerializeField, Min(0f), Tooltip("Downward acceleration in m/s².")]
        private float gravity = 20f;

        [SerializeField, Min(0f), Tooltip("Small downward speed kept while grounded so the controller stays in contact with the floor.")]
        private float groundStickSpeed = 2f;

        private CharacterController controller;
        private Vector3 horizontalVelocity;
        private float verticalVelocity;

        public bool IsGrounded { get; private set; }
        public Vector3 Velocity => horizontalVelocity + Vector3.up * verticalVelocity;
        public float MoveSpeed => moveSpeed;
        public float JumpHeight => jumpHeight;

        private void Awake() => controller = GetComponent<CharacterController>();

        public void Tick(Vector2 moveInput, bool jumpRequested, float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            UpdateHorizontalVelocity(moveInput, deltaTime);
            float verticalDisplacement = UpdateVerticalVelocity(jumpRequested, deltaTime);

            Vector3 displacement = horizontalVelocity * deltaTime + Vector3.up * verticalDisplacement;
            ResolveCollisions(controller.Move(displacement));
        }

        public void Teleport(Vector3 position)
        {
            // CharacterController keeps its own copy of the position, so it must be off while the transform moves.
            controller.enabled = false;
            transform.position = position;
            controller.enabled = true;

            horizontalVelocity = Vector3.zero;
            verticalVelocity = 0f;
            IsGrounded = false;
        }

        private void UpdateHorizontalVelocity(Vector2 moveInput, float deltaTime)
        {
            Vector3 wishDirection = transform.right * moveInput.x + transform.forward * moveInput.y;
            Vector3 targetVelocity = Vector3.ClampMagnitude(wishDirection, 1f) * moveSpeed;
            float acceleration = IsGrounded ? groundAcceleration : airAcceleration;

            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, acceleration * deltaTime);
        }

        private float UpdateVerticalVelocity(bool jumpRequested, float deltaTime)
        {
            if (IsGrounded && !jumpRequested)
            {
                verticalVelocity = -groundStickSpeed;
                return verticalVelocity * deltaTime;
            }

            if (IsGrounded)
                verticalVelocity = Mathf.Sqrt(2f * gravity * jumpHeight);

            // Averaging the start and end velocity gives the exact displacement under constant gravity,
            // so the jump reaches the same height at any frame rate.
            float startVelocity = verticalVelocity;
            verticalVelocity -= gravity * deltaTime;
            return (startVelocity + verticalVelocity) * 0.5f * deltaTime;
        }

        private void ResolveCollisions(CollisionFlags collisions)
        {
            IsGrounded = (collisions & CollisionFlags.Below) != 0;

            if ((collisions & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
                verticalVelocity = 0f;

            // Keep only the velocity the controller could actually follow, so speed pushed into a wall
            // is not stored and released when the player turns away from it.
            if ((collisions & CollisionFlags.Sides) != 0)
                horizontalVelocity = Vector3.ProjectOnPlane(controller.velocity, Vector3.up);
        }
    }
}
