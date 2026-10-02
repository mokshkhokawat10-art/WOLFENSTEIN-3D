using UnityEngine;

namespace Wolfenstein.Pickups
{
    /// <summary>
    /// Makes an item easy to spot: its model turns slowly and bobs gently above the floor.
    /// It is purely visual and moves only the model, never the item's collider.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PickupView : MonoBehaviour
    {
        [SerializeField, Tooltip("The item's model. Its position in the prefab is the lowest point of the bob.")]
        private Transform model;

        [SerializeField, Tooltip("How fast the model turns, in degrees per second.")]
        private float spinSpeed = 90f;

        [SerializeField, Min(0f), Tooltip("How far the model rises and falls, in metres.")]
        private float bobHeight = 0.06f;

        [SerializeField, Min(0.01f), Tooltip("Seconds for one rise and fall.")]
        private float bobPeriod = 1.6f;

        private Vector3 restPosition;
        private float bobPhase;

        private void Awake()
        {
            restPosition = model.localPosition;

            // Items near each other start at different points, so they do not move in step.
            bobPhase = Random.value;
            model.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * model.localRotation;
        }

        private void LateUpdate()
        {
            model.localRotation = Quaternion.Euler(0f, spinSpeed * Time.deltaTime, 0f) * model.localRotation;

            float rise = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * (Time.time / bobPeriod + bobPhase));
            model.localPosition = restPosition + Vector3.up * (bobHeight * rise);
        }
    }
}
