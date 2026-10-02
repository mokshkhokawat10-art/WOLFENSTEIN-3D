using UnityEngine;

namespace Wolfenstein.Weapons
{
    /// <summary>
    /// The first-person weapon model. It kicks back when a shot is fired, settles again, and shows a brief
    /// muzzle flash. It is purely visual and only listens to the <see cref="WeaponController"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeaponView : MonoBehaviour
    {
        [SerializeField]
        private WeaponController weapon;

        [SerializeField, Tooltip("The part of the model that moves with recoil.")]
        private Transform model;

        [SerializeField, Tooltip("Shown for a moment after each shot. Can include a light.")]
        private GameObject muzzleFlash;

        [SerializeField, Min(0f), Tooltip("How long the muzzle flash stays visible, in seconds.")]
        private float muzzleFlashDuration = 0.05f;

        [Header("Recoil")]
        [SerializeField, Tooltip("How far the model moves at the peak of the kick, in local space.")]
        private Vector3 recoilOffset = new Vector3(0f, 0.01f, -0.05f);

        [SerializeField, Tooltip("How far the model tilts up at the peak of the kick, in degrees.")]
        private float recoilPitch = 10f;

        [SerializeField, Min(0.01f), Tooltip("How quickly the model settles after a shot. Higher is faster.")]
        private float recoverySpeed = 14f;

        private Vector3 restPosition;
        private Quaternion restRotation;
        private float kick;
        private float muzzleFlashHideTime;

        public bool IsMuzzleFlashVisible => muzzleFlash.activeSelf;

        private void Awake()
        {
            model.GetLocalPositionAndRotation(out restPosition, out restRotation);
            muzzleFlash.SetActive(false);
        }

        private void OnEnable() => weapon.Fired += HandleFired;

        private void OnDisable() => weapon.Fired -= HandleFired;

        private void LateUpdate()
        {
            // Exponential decay settles the same way at any frame rate.
            kick *= Mathf.Exp(-recoverySpeed * Time.deltaTime);
            model.SetLocalPositionAndRotation(
                restPosition + recoilOffset * kick,
                restRotation * Quaternion.Euler(-recoilPitch * kick, 0f, 0f));

            if (muzzleFlash.activeSelf && Time.time >= muzzleFlashHideTime)
                muzzleFlash.SetActive(false);
        }

        private void HandleFired(ShotResult shot)
        {
            kick = 1f;

            // Rolling the flash each shot keeps repeated shots from looking identical.
            muzzleFlash.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            muzzleFlash.SetActive(true);
            muzzleFlashHideTime = Time.time + muzzleFlashDuration;
        }
    }
}
