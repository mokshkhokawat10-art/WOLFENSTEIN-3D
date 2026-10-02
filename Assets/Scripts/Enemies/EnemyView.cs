using UnityEngine;
using Wolfenstein.Combat;

namespace Wolfenstein.Enemies
{
    /// <summary>
    /// Animates an enemy's model. The legs swing while it walks, the rifle comes up to aim and kicks with a muzzle flash
    /// when it fires, a hit makes the body flinch and flash red, and death tips the body over backwards.
    /// It is purely visual and only listens to the <see cref="EnemyController"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField]
        private EnemyController enemy;

        [SerializeField, Tooltip("The whole model, pivoting at the feet.")]
        private Transform model;

        [SerializeField, Tooltip("Leg pivots at the hips.")]
        private Transform leftLeg;

        [SerializeField]
        private Transform rightLeg;

        [SerializeField, Tooltip("Pivot at the shoulders carrying the arms and rifle.")]
        private Transform weapon;

        [SerializeField, Tooltip("Shown for a moment after each shot. Can include a light.")]
        private GameObject muzzleFlash;

        [Header("Walking")]
        [SerializeField, Min(0.1f), Tooltip("Distance covered by one full stride, in metres.")]
        private float strideLength = 1.6f;

        [SerializeField, Range(0f, 60f), Tooltip("How far the legs swing forward and back at full speed, in degrees.")]
        private float legSwing = 28f;

        [Header("Aiming")]
        [SerializeField, Range(0f, 90f), Tooltip("How far the rifle points down while it is not aimed, in degrees.")]
        private float loweredWeaponPitch = 35f;

        [SerializeField, Min(0.01f), Tooltip("How quickly the rifle is raised and lowered. Higher is faster.")]
        private float aimSpeed = 14f;

        [SerializeField, Range(0f, 90f), Tooltip("How far the rifle points down once the enemy is dead, so it lies along the body.")]
        private float deadWeaponPitch = 80f;

        [Header("Firing")]
        [SerializeField, Min(0f), Tooltip("How long the muzzle flash stays visible, in seconds.")]
        private float muzzleFlashDuration = 0.08f;

        [SerializeField, Tooltip("How far the rifle tips up at the peak of its kick, in degrees.")]
        private float recoilPitch = 12f;

        [Header("Pain")]
        [SerializeField, Tooltip("How far the body rocks back when hit, in degrees.")]
        private float flinchPitch = 14f;

        [SerializeField] private Color hurtColour = new Color(0.9f, 0.05f, 0.05f);

        [SerializeField, Min(0.01f), Tooltip("Seconds the flinch and red flash take to fade.")]
        private float hurtDuration = 0.3f;

        [Header("Death")]
        [SerializeField, Min(0.01f), Tooltip("Seconds the body takes to fall.")]
        private float fallDuration = 0.45f;

        [SerializeField, Min(0f), Tooltip("How far the fallen body is lifted so it lies on the floor rather than in it, in metres.")]
        private float lyingHeight = 0.14f;

        private Health health;
        private Renderer[] renderers;
        private Color[] baseColours;
        private MaterialPropertyBlock propertyBlock;
        private Quaternion weaponRestRotation;
        private float stridePhase;
        private float weaponPitch;
        private float kick;
        private float hurt;
        private float fall;
        private float muzzleFlashHideTime;

        public bool IsMuzzleFlashVisible => muzzleFlash.activeSelf;

        private void Awake()
        {
            health = enemy.GetComponent<Health>();
            weaponRestRotation = weapon.localRotation;
            weaponPitch = loweredWeaponPitch;
            muzzleFlash.SetActive(false);

            renderers = model.GetComponentsInChildren<Renderer>(true);
            baseColours = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                Material material = renderers[i].sharedMaterial;
                baseColours[i] = material != null && material.HasProperty(BaseColorId) ? material.GetColor(BaseColorId) : Color.white;
            }

            propertyBlock = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            enemy.Fired += HandleFired;
            health.Damaged += HandleDamaged;
        }

        private void OnDisable()
        {
            enemy.Fired -= HandleFired;
            health.Damaged -= HandleDamaged;
        }

        private void LateUpdate()
        {
            float deltaTime = Time.deltaTime;
            bool dead = enemy.State == EnemyState.Dead;

            // Exponential decay settles the same way at any frame rate.
            kick *= Mathf.Exp(-12f * deltaTime);
            SetHurt(hurt - deltaTime / hurtDuration);

            AnimateLegs(dead ? Vector3.zero : enemy.Motor.Velocity, deltaTime);
            AnimateWeapon(dead, deltaTime);
            AnimateBody(dead, deltaTime);

            if (muzzleFlash.activeSelf && (dead || Time.time >= muzzleFlashHideTime))
                muzzleFlash.SetActive(false);
        }

        private void AnimateLegs(Vector3 velocity, float deltaTime)
        {
            float speed = velocity.magnitude;
            stridePhase = Mathf.Repeat(stridePhase + speed * deltaTime / strideLength, 1f);

            // The swing grows with speed, so an enemy that stops settles with its feet together.
            float swingScale = Mathf.Clamp01(speed / Mathf.Max(enemy.Definition.ChaseSpeed, 0.01f));
            float angle = legSwing * swingScale * Mathf.Sin(stridePhase * 2f * Mathf.PI);
            leftLeg.localRotation = Quaternion.Euler(angle, 0f, 0f);
            rightLeg.localRotation = Quaternion.Euler(-angle, 0f, 0f);
        }

        // Level while aiming, lowered otherwise, and along the body once dead.
        private void AnimateWeapon(bool dead, float deltaTime)
        {
            float targetPitch = loweredWeaponPitch;
            if (dead)
                targetPitch = deadWeaponPitch;
            else if (enemy.State == EnemyState.Attack)
                targetPitch = 0f;

            weaponPitch = Mathf.Lerp(weaponPitch, targetPitch, 1f - Mathf.Exp(-aimSpeed * deltaTime));
            weapon.localRotation = weaponRestRotation * Quaternion.Euler(weaponPitch - recoilPitch * kick, 0f, 0f);
        }

        private void AnimateBody(bool dead, float deltaTime)
        {
            if (dead)
                fall = Mathf.MoveTowards(fall, 1f, deltaTime / fallDuration);

            // Easing in makes the body gather speed as it topples, and the flinch rocks it back from the feet.
            float fallAngle = 90f * fall * fall;
            model.localRotation = Quaternion.Euler(-fallAngle - flinchPitch * hurt, 0f, 0f);
            model.localPosition = Vector3.up * (lyingHeight * fall);
        }

        private void HandleFired(bool hit)
        {
            kick = 1f;

            // Rolling the flash each shot keeps repeated shots from looking identical.
            muzzleFlash.transform.localRotation = Quaternion.Euler(0f, 180f, Random.Range(0f, 360f));
            muzzleFlash.SetActive(true);
            muzzleFlashHideTime = Time.time + muzzleFlashDuration;
        }

        private void HandleDamaged(DamageInfo damage) => SetHurt(1f);

        private void SetHurt(float strength)
        {
            float previous = hurt;
            hurt = Mathf.Clamp01(strength);
            if (hurt == 0f && previous == 0f)
                return;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (hurt == 0f)
                {
                    // Clearing the block rather than restoring the colour lets the renderer batch again.
                    renderers[i].SetPropertyBlock(null);
                    continue;
                }

                propertyBlock.SetColor(BaseColorId, Color.Lerp(baseColours[i], hurtColour, hurt * 0.7f));
                renderers[i].SetPropertyBlock(propertyBlock);
            }
        }
    }
}
