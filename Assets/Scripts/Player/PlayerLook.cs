using UnityEngine;

namespace Wolfenstein.Player
{
    /// <summary>
    /// First-person view rotation. Yaw turns the whole player body so movement follows the view;
    /// pitch tilts only the camera pivot so the collider stays upright.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerLook : MonoBehaviour
    {
        [SerializeField, Tooltip("Child transform at eye height that carries the camera. Pitch is applied here.")]
        private Transform cameraPivot;

        [Header("Sensitivity")]
        [SerializeField, Min(0f), Tooltip("Degrees of rotation per pixel of mouse movement.")]
        private float mouseSensitivity = 0.1f;

        [SerializeField, Min(0f), Tooltip("Degrees per second at full gamepad stick deflection.")]
        private float stickSensitivity = 180f;

        [SerializeField]
        private bool invertY;

        [Header("Limits")]
        [SerializeField, Range(0f, 89f), Tooltip("How far the view can tilt up or down, in degrees.")]
        private float maxPitch = 85f;

        private float pitch;

        public float MouseSensitivity
        {
            get => mouseSensitivity;
            set => mouseSensitivity = Mathf.Max(0f, value);
        }

        public bool InvertY
        {
            get => invertY;
            set => invertY = value;
        }

        public float MaxPitch => maxPitch;
        public float Pitch => pitch;
        public float Yaw => transform.eulerAngles.y;

        public void ApplyPointerDelta(Vector2 pixelDelta) => Rotate(pixelDelta * mouseSensitivity);

        public void ApplyStickInput(Vector2 stick, float deltaTime) => Rotate(stick * (stickSensitivity * deltaTime));

        public void SetOrientation(float yaw, float newPitch)
        {
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            pitch = ClampPitch(newPitch);
            ApplyPitch();
        }

        private void Rotate(Vector2 degrees)
        {
            // Moving the mouse up gives positive Y, but a positive X rotation tilts the camera down.
            float pitchDelta = invertY ? degrees.y : -degrees.y;
            pitch = ClampPitch(pitch + pitchDelta);

            transform.Rotate(0f, degrees.x, 0f, Space.Self);
            ApplyPitch();
        }

        private float ClampPitch(float value) => Mathf.Clamp(value, -maxPitch, maxPitch);

        private void ApplyPitch() => cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }
}
