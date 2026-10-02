using UnityEngine;
using UnityEngine.InputSystem;
using Wolfenstein.Interaction;
using Wolfenstein.Weapons;

namespace Wolfenstein.Player
{
    /// <summary>
    /// Entry point for the player. Reads input once per frame and drives look, then interaction and firing,
    /// then movement, so aiming, shooting and movement all use the facing direction from the same frame.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerMovement), typeof(PlayerLook), typeof(InteractionController))]
    [RequireComponent(typeof(WeaponController))]
    public sealed class PlayerController : MonoBehaviour
    {
        [Header("Input Actions")]
        [SerializeField] private InputActionReference moveAction;
        [SerializeField] private InputActionReference lookAction;
        [SerializeField] private InputActionReference jumpAction;
        [SerializeField] private InputActionReference interactAction;
        [SerializeField] private InputActionReference attackAction;

        private PlayerMovement movement;
        private PlayerLook look;
        private InteractionController interaction;
        private WeaponController weapon;

        public bool InputEnabled { get; set; } = true;

        /// <summary>Whether the Attack input can fire the weapon, while input is enabled at all.</summary>
        public bool FireEnabled { get; set; } = true;

        public PlayerMovement Movement => movement;
        public PlayerLook Look => look;
        public InteractionController Interaction => interaction;
        public WeaponController Weapon => weapon;

        private void Awake()
        {
            movement = GetComponent<PlayerMovement>();
            look = GetComponent<PlayerLook>();
            interaction = GetComponent<InteractionController>();
            weapon = GetComponent<WeaponController>();
        }

        private void OnEnable() => SetActionsEnabled(true);

        private void OnDisable() => SetActionsEnabled(false);

        private void Update()
        {
            if (InputEnabled)
            {
                ApplyLookInput();

                // Read as a plain press so the action's Hold interaction does not delay the response.
                if (interactAction.action.WasPressedThisFrame())
                    interaction.TryInteract();

                if (FireEnabled)
                {
                    InputAction attack = attackAction.action;
                    weapon.UpdateTrigger(attack.WasPressedThisFrame(), attack.IsPressed());
                }
            }

            Vector2 moveInput = InputEnabled ? moveAction.action.ReadValue<Vector2>() : Vector2.zero;
            bool jumpRequested = InputEnabled && jumpAction.action.WasPressedThisFrame();
            movement.Tick(moveInput, jumpRequested, Time.deltaTime);
        }

        public void PlaceAt(Pose pose)
        {
            movement.Teleport(pose.position);
            look.SetOrientation(pose.rotation.eulerAngles.y, 0f);
        }

        private void SetActionsEnabled(bool enable)
        {
            foreach (InputActionReference reference in new[] { moveAction, lookAction, jumpAction, interactAction, attackAction })
            {
                if (enable)
                    reference.action.Enable();
                else
                    reference.action.Disable();
            }
        }

        private void ApplyLookInput()
        {
            InputAction action = lookAction.action;
            Vector2 value = action.ReadValue<Vector2>();
            if (value == Vector2.zero)
                return;

            // A pointer delta is already a distance covered this frame; a stick value is a rate over time.
            if (action.activeControl?.device is Pointer)
                look.ApplyPointerDelta(value);
            else
                look.ApplyStickInput(value, Time.deltaTime);
        }
    }
}
