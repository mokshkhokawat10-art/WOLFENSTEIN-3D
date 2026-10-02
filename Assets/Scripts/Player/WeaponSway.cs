using UnityEngine;

namespace Wolfenstein.Player
{
    /// <summary>
    /// Brings the first-person weapon to life without moving the camera: it breathes gently at rest, bobs with the
    /// player's stride and trails a little behind turns of the view. It is purely visual and moves only its own transform,
    /// which carries the weapon model; shots always leave from the camera, whatever the weapon is doing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeaponSway : MonoBehaviour
    {
        [SerializeField]
        private PlayerMovement movement;

        [Header("Breathing")]
        [SerializeField, Min(0f), Tooltip("How far the weapon rises and falls at rest, in metres.")]
        private float breathHeight = 0.002f;

        [SerializeField, Min(0.1f), Tooltip("Seconds for one breath.")]
        private float breathPeriod = 3.5f;

        [Header("Walking")]
        [SerializeField, Min(0.1f), Tooltip("Distance covered by one full sway, left and right, in metres.")]
        private float strideLength = 3.2f;

        [SerializeField, Tooltip("How far the weapon sways sideways and dips at full walking speed, in metres.")]
        private Vector2 strideSway = new Vector2(0.007f, 0.005f);

        [Header("Turning")]
        [SerializeField, Min(0f), Tooltip("How far the weapon trails behind a turn, in degrees per degree turned in a frame.")]
        private float turnLag = 0.25f;

        [SerializeField, Min(0f), Tooltip("The furthest the weapon trails behind a turn, in degrees.")]
        private float maxTurnLag = 2.5f;

        [SerializeField, Min(0.01f), Tooltip("How quickly the weapon catches up with the view. Higher is faster.")]
        private float catchUpSpeed = 9f;

        private Vector3 restPosition;
        private Quaternion restRotation;
        private Quaternion lastViewRotation;
        private float stridePhase;
        private float strideWeight;
        private Vector2 lag;

        private void Awake() => transform.GetLocalPositionAndRotation(out restPosition, out restRotation);

        private void OnEnable() => lastViewRotation = transform.parent.rotation;

        private void LateUpdate()
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
                return;

            Vector3 offset = Vector3.up * (breathHeight * Mathf.Sin(2f * Mathf.PI * Time.time / breathPeriod));
            offset += StrideOffset(deltaTime);

            transform.SetLocalPositionAndRotation(restPosition + offset, restRotation * TurnLag(deltaTime));
        }

        // Sways once to each side per stride and dips with every step, fading in and out as the player starts and stops.
        private Vector3 StrideOffset(float deltaTime)
        {
            float speed = Vector3.ProjectOnPlane(movement.Velocity, Vector3.up).magnitude;
            float targetWeight = movement.IsGrounded ? Mathf.Clamp01(speed / Mathf.Max(movement.MoveSpeed, 0.01f)) : 0f;
            strideWeight = Mathf.MoveTowards(strideWeight, targetWeight, 4f * deltaTime);
            stridePhase = Mathf.Repeat(stridePhase + speed * deltaTime / strideLength, 1f);

            float angle = 2f * Mathf.PI * stridePhase;
            float sideways = Mathf.Sin(angle) * strideSway.x;
            float dip = -0.5f * (1f - Mathf.Cos(2f * angle)) * strideSway.y;
            return new Vector3(sideways, dip, 0f) * strideWeight;
        }

        // The weapon is turned back against the latest turn of the view, then eases back to its rest pose.
        private Quaternion TurnLag(float deltaTime)
        {
            Quaternion viewRotation = transform.parent.rotation;
            Vector3 turned = (Quaternion.Inverse(lastViewRotation) * viewRotation).eulerAngles;
            lastViewRotation = viewRotation;

            var turn = new Vector2(Mathf.DeltaAngle(0f, turned.y), Mathf.DeltaAngle(0f, turned.x));
            lag = Vector2.ClampMagnitude(lag - turn * turnLag, maxTurnLag);
            lag = Vector2.Lerp(lag, Vector2.zero, 1f - Mathf.Exp(-catchUpSpeed * deltaTime));
            return Quaternion.Euler(lag.y, lag.x, -lag.x * 0.5f);
        }
    }
}
