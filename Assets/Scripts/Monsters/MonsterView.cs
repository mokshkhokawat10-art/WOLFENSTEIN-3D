using UnityEngine;
using Wolfenstein.Combat;

namespace Wolfenstein.Monsters
{
    /// <summary>
    /// Animates a monster's model from its state, with no animation clips. It rises out of the floor when it spawns,
    /// runs with swinging limbs and a bobbing, forward-leaning body, rears back to wind up a blow and swings through it,
    /// holds out its weapon arm as a shot charges, flinches and flashes when hit, and topples when it dies. Its head turns
    /// toward its target and its jaw gapes as it attacks; a floating monster bobs in the air and sinks when it dies, and
    /// tentacles or a tail sway as it moves. Glowing parts such as eyes brighten while an attack winds up and go dark at
    /// death. Every part of the rig is optional, so one view serves monsters of any shape.
    /// It is purely visual, and its <see cref="MonsterManager"/> drives it once a frame instead of it having an Update.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MonsterView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private MonsterController monster;

        [Header("Rig")]
        [SerializeField, Tooltip("The whole model, pivoting at the feet.")]
        private Transform model;

        [SerializeField, Tooltip("Pivot at the hips carrying the upper body, which leans, bobs and flinches.")]
        private Transform torso;

        [SerializeField] private Transform leftLeg;
        [SerializeField] private Transform rightLeg;

        [SerializeField, Tooltip("Pivots at the shoulders. Each arm hangs down from its pivot.")]
        private Transform leftArm;

        [SerializeField] private Transform rightArm;

        [SerializeField, Tooltip("Pivot of the head, which turns toward the target. Optional.")]
        private Transform head;

        [SerializeField, Tooltip("Pivot of the lower jaw at its hinge, which gapes as the monster attacks or is hurt. Optional.")]
        private Transform jaw;

        [SerializeField, Tooltip("Tentacles, a tail and the like, each hanging from its own pivot, which sway as the monster moves.")]
        private Transform[] swayParts = System.Array.Empty<Transform>();

        [SerializeField, Tooltip("Grows at the weapon arm's tip while a shot charges. Optional.")]
        private Transform chargeGlow;

        [SerializeField, Tooltip("Parts drawn with a glowing unlit material, such as eyes.")]
        private Renderer[] glowParts = System.Array.Empty<Renderer>();

        [Header("Running")]
        [SerializeField, Min(0.1f), Tooltip("Distance covered by one full stride, in metres.")]
        private float strideLength = 2f;

        [SerializeField, Range(0f, 70f)] private float legSwing = 35f;
        [SerializeField, Range(0f, 70f)] private float armSwing = 25f;

        [SerializeField, Range(0f, 45f), Tooltip("How far the body leans into a run at full speed, in degrees.")]
        private float runLean = 12f;

        [SerializeField, Min(0f), Tooltip("How far the body rises and falls with each step, in metres.")]
        private float bobHeight = 0.06f;

        [Header("Head")]
        [SerializeField, Range(0f, 90f), Tooltip("How far the head turns toward the target, in degrees.")]
        private float headTurn = 30f;

        [SerializeField, Range(0f, 60f), Tooltip("How far the jaw drops at its widest, in degrees.")]
        private float jawGape = 25f;

        [Header("Floating And Swaying")]
        [SerializeField, Min(0f), Tooltip("How far a floating monster bobs up and down, in metres. 0 for one that walks.")]
        private float hoverBob;

        [SerializeField, Min(0f), Tooltip("Bobs a second.")]
        private float hoverBobRate = 0.9f;

        [SerializeField, Range(0f, 60f), Tooltip("How far each swaying part swings, in degrees.")]
        private float swayAngle = 12f;

        [SerializeField, Min(0f), Tooltip("Sways a second.")]
        private float swayRate = 1.2f;

        [Header("Attacks")]
        [SerializeField, Tooltip("Arm angle at the top of the wind-up of a blow, in degrees. Negative raises the arms forward.")]
        private float windupArmPitch = -150f;

        [SerializeField, Tooltip("Arm angle at the end of a blow's swing, in degrees.")]
        private float strikeArmPitch = -35f;

        [SerializeField, Tooltip("How far the body rears back while winding up a blow, in degrees.")]
        private float windupLean = -14f;

        [SerializeField, Tooltip("How far the body lunges forward as a blow lands, in degrees.")]
        private float strikeLean = 22f;

        [SerializeField, Tooltip("How far the weapon arm is raised to aim a shot, in degrees. -90 points it straight ahead.")]
        private float aimArmPitch = -88f;

        [SerializeField, Min(0f), Tooltip("How much brighter glowing parts get at the end of a wind-up.")]
        private float windupGlowBoost = 2.5f;

        [Header("Pain")]
        [SerializeField] private Color hurtColour = new Color(1f, 0.2f, 0.15f);

        [SerializeField] private float flinchPitch = -18f;

        [SerializeField, Min(0.01f)] private float hurtDuration = 0.25f;

        [Header("Spawning And Death")]
        [SerializeField, Min(0f), Tooltip("How far below the floor the model starts when it spawns, in metres.")]
        private float riseDepth = 2f;

        [SerializeField, Min(0.01f)] private float fallDuration = 0.5f;

        [SerializeField, Range(-1f, 1f), Tooltip("Which way the body falls: 1 topples it forward onto its face, -1 onto its back, " +
                                                 "0 not at all, for one that sinks instead.")]
        private float fallDirection = -1f;

        [SerializeField, Min(0f), Tooltip("How far the model sinks as the monster dies, for one that floats, in metres.")]
        private float deathDrop;

        [SerializeField, Min(0f), Tooltip("How far the fallen body is lifted so it lies on the floor rather than in it, in metres.")]
        private float lyingHeight = 0.15f;

        [SerializeField, Tooltip("Colour of the gore thrown out when the monster dies.")]
        private Color bloodColour = new Color(0.4f, 0.02f, 0.02f);

        private Health health;
        private Renderer[] renderers;
        private Color[] baseColours;
        private Color[] glowColours;
        private MaterialPropertyBlock propertyBlock;
        private Quaternion leftArmRest;
        private Quaternion rightArmRest;
        private Quaternion headRest;
        private Quaternion jawRest;
        private Quaternion[] swayRests;
        private Vector3 torsoRestPosition;
        private Vector3 chargeGlowScale;
        private float stridePhase;
        private float headYaw;
        private float jawOpen;

        // Each monster bobs and sways out of step with the rest, so a crowd does not move as one.
        private float motionOffset;
        private float stateStartTime;
        private float windup;
        private float strike;
        private float hurt;
        private float fall;
        private float glow = 1f;

        public Color BloodColour => bloodColour;

        /// <summary>How far the model has risen out of the floor, from 0 when it appears to 1 once standing.</summary>
        public float Risen { get; private set; } = 1f;

        private void Awake()
        {
            health = monster.GetComponent<Health>();
            leftArmRest = leftArm != null ? leftArm.localRotation : Quaternion.identity;
            rightArmRest = rightArm != null ? rightArm.localRotation : Quaternion.identity;
            headRest = head != null ? head.localRotation : Quaternion.identity;
            jawRest = jaw != null ? jaw.localRotation : Quaternion.identity;
            swayRests = new Quaternion[swayParts.Length];
            for (int i = 0; i < swayParts.Length; i++)
                swayRests[i] = swayParts[i].localRotation;
            motionOffset = Random.Range(0f, 100f);
            torsoRestPosition = torso != null ? torso.localPosition : Vector3.zero;
            chargeGlowScale = chargeGlow != null ? chargeGlow.localScale : Vector3.one;

            var glowing = new System.Collections.Generic.HashSet<Renderer>(glowParts);
            var tinted = new System.Collections.Generic.List<Renderer>();
            foreach (Renderer part in model.GetComponentsInChildren<Renderer>(true))
            {
                if (!glowing.Contains(part) && (chargeGlow == null || !part.transform.IsChildOf(chargeGlow)))
                    tinted.Add(part);
            }

            renderers = tinted.ToArray();
            baseColours = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
                baseColours[i] = ColourOf(renderers[i]);

            glowColours = new Color[glowParts.Length];
            for (int i = 0; i < glowParts.Length; i++)
                glowColours[i] = ColourOf(glowParts[i]);

            propertyBlock = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            monster.StateChanged += HandleStateChanged;
            monster.Attacked += HandleAttacked;
            health.Damaged += HandleDamaged;
        }

        private void OnDisable()
        {
            monster.StateChanged -= HandleStateChanged;
            monster.Attacked -= HandleAttacked;
            health.Damaged -= HandleDamaged;
        }

        /// <summary>Poses the model for this frame.</summary>
        public void Animate(float deltaTime)
        {
            MonsterState state = monster.State;
            bool dead = state == MonsterState.Dead;
            float now = Time.time;

            // Exponential decay settles the same way at any frame rate.
            strike *= Mathf.Exp(-7f * deltaTime);
            SetHurt(hurt - deltaTime / hurtDuration);

            Risen = state == MonsterState.Spawning
                ? Mathf.Clamp01((now - stateStartTime) / Mathf.Max(monster.Definition.SpawnDuration, 0.01f))
                : 1f;

            float targetWindup = 0f;
            if (state == MonsterState.Attack)
            {
                float length = monster.CurrentAttack == MonsterAttack.Melee ? monster.Definition.MeleeWindup : monster.Definition.FireWindup;
                targetWindup = Mathf.Clamp01((now - stateStartTime) / Mathf.Max(length, 0.05f));
            }

            windup = Mathf.MoveTowards(windup, targetWindup, deltaTime * 8f);
            if (dead)
                fall = Mathf.MoveTowards(fall, 1f, deltaTime / fallDuration);

            Vector3 velocity = dead ? Vector3.zero : monster.Motor.Velocity;
            float speed = velocity.magnitude;
            stridePhase = Mathf.Repeat(stridePhase + speed * deltaTime / strideLength, 1f);
            float run = Mathf.Clamp01(speed / Mathf.Max(monster.MoveSpeed, 0.01f));
            float swing = Mathf.Sin(stridePhase * 2f * Mathf.PI) * run;

            PoseLegs(swing);
            PoseArms(swing, dead);
            PoseTorso(run, dead);
            PoseHead(dead, deltaTime);
            PoseSway(run, now);
            PoseModel(now);
            PoseGlow(dead, deltaTime);
        }

        private void PoseLegs(float swing)
        {
            if (leftLeg != null)
                leftLeg.localRotation = Quaternion.Euler(legSwing * swing, 0f, 0f);
            if (rightLeg != null)
                rightLeg.localRotation = Quaternion.Euler(-legSwing * swing, 0f, 0f);
        }

        // Arms swing against the legs while running. A blow raises both arms through its wind-up and throws them down
        // as it lands; a shot holds the weapon arm out, and the other arm swings on.
        private void PoseArms(float swing, bool dead)
        {
            float leftPitch = -armSwing * swing;
            float rightPitch = armSwing * swing;

            if (!dead)
            {
                bool melee = monster.CurrentAttack == MonsterAttack.Melee;
                if (melee)
                {
                    float raised = Mathf.SmoothStep(0f, 1f, windup);
                    leftPitch = Mathf.Lerp(leftPitch, windupArmPitch, raised) + strikeArmPitch * strike;
                    rightPitch = Mathf.Lerp(rightPitch, windupArmPitch, raised) + strikeArmPitch * strike;
                }
                else
                {
                    float aim = Mathf.Max(Mathf.SmoothStep(0f, 1f, windup * 3f), strike);
                    rightPitch = Mathf.Lerp(rightPitch, aimArmPitch, aim) - 12f * strike;
                }
            }
            else
            {
                // Arms fling out loose as the body falls.
                leftPitch = rightPitch = -60f * fall;
            }

            if (leftArm != null)
                leftArm.localRotation = leftArmRest * Quaternion.Euler(leftPitch, 0f, 0f);
            if (rightArm != null)
                rightArm.localRotation = rightArmRest * Quaternion.Euler(rightPitch, 0f, 0f);
        }

        private void PoseTorso(float run, bool dead)
        {
            if (torso == null)
                return;

            bool melee = monster.CurrentAttack == MonsterAttack.Melee;
            float lean = dead ? 0f : runLean * run;
            if (melee && !dead)
                lean += windupLean * windup + strikeLean * strike;

            float bob = bobHeight * Mathf.Abs(Mathf.Sin(stridePhase * 2f * Mathf.PI)) * run;
            torso.localRotation = Quaternion.Euler(lean + flinchPitch * hurt, 0f, 0f);
            torso.localPosition = torsoRestPosition + Vector3.up * bob;
        }

        // The head turns toward the target within its limit and rears back as a blow winds up. The jaw hangs a little open
        // and works as the monster breathes, gapes through a wind-up and as a blow lands or a hit lands on it, and falls
        // slack in death.
        private void PoseHead(bool dead, float deltaTime)
        {
            float follow = 1f - Mathf.Exp(-8f * deltaTime);
            if (head != null)
            {
                float wanted = 0f;
                if (!dead && monster.Target != null)
                {
                    Vector3 toTarget = Vector3.ProjectOnPlane(monster.Target.transform.position - transform.position, Vector3.up);
                    if (toTarget.sqrMagnitude > 0.01f)
                        wanted = Mathf.Clamp(Vector3.SignedAngle(transform.forward, toTarget, Vector3.up), -headTurn, headTurn);
                }

                headYaw = Mathf.Lerp(headYaw, wanted, follow);
                bool melee = monster.CurrentAttack == MonsterAttack.Melee;
                float pitch = (melee && !dead ? -10f * windup : 0f) + 12f * hurt;
                head.localRotation = headRest * Quaternion.Euler(pitch, headYaw, 0f);
            }

            if (jaw == null)
                return;

            float breathing = 0.2f + 0.1f * Mathf.Sin((Time.time + motionOffset) * 3f);
            float wantedOpen = dead ? 0.7f : Mathf.Max(breathing, windup, strike * 0.9f, hurt * 0.7f);
            jawOpen = Mathf.Lerp(jawOpen, wantedOpen, follow);
            jaw.localRotation = jawRest * Quaternion.Euler(jawGape * jawOpen, 0f, 0f);
        }

        // Each swaying part swings on its own beat, harder while the monster moves, and hangs still once it is dead.
        private void PoseSway(float run, float now)
        {
            float strength = swayAngle * (0.6f + 0.6f * run) * (1f - fall);
            for (int i = 0; i < swayParts.Length; i++)
            {
                float phase = (now + motionOffset) * swayRate * 2f * Mathf.PI + i * 1.7f;
                swayParts[i].localRotation = swayRests[i] *
                                             Quaternion.Euler(Mathf.Sin(phase) * strength, 0f, Mathf.Cos(phase * 0.8f + i) * strength * 0.6f);
            }
        }

        // Rises through the floor while spawning, slowing as it stands up; eases over as it falls, gathering speed like a
        // toppling body. A floating monster bobs in the air, and sinks rather than topples when it dies.
        private void PoseModel(float now)
        {
            float sunk = 1f - Mathf.SmoothStep(0f, 1f, Risen);
            float fallAngle = 90f * fall * fall * fallDirection;
            float bob = hoverBob * Mathf.Sin((now + motionOffset) * hoverBobRate * 2f * Mathf.PI) * (1f - fall);
            model.localRotation = Quaternion.Euler(fallAngle, 0f, 0f);
            model.localPosition = Vector3.up * (lyingHeight * fall - deathDrop * fall * fall - riseDepth * sunk + bob);
        }

        private void PoseGlow(bool dead, float deltaTime)
        {
            glow = dead ? Mathf.MoveTowards(glow, 0f, deltaTime * 1.5f) : 1f + windupGlowBoost * windup * windup;
            for (int i = 0; i < glowParts.Length; i++)
            {
                propertyBlock.SetColor(BaseColorId, glowColours[i] * glow);
                glowParts[i].SetPropertyBlock(propertyBlock);
            }

            if (chargeGlow == null)
                return;

            bool charging = !dead && monster.State == MonsterState.Attack && monster.CurrentAttack == MonsterAttack.Ranged;
            float size = charging ? windup : strike * 0.6f;
            chargeGlow.gameObject.SetActive(size > 0.02f);
            chargeGlow.localScale = chargeGlowScale * size;
        }

        private void HandleStateChanged(MonsterState state)
        {
            stateStartTime = Time.time;
            if (state != MonsterState.Spawning)
                return;

            // A monster taken from its pool starts again from a fresh pose.
            fall = 0f;
            windup = 0f;
            strike = 0f;
            glow = 1f;
            headYaw = 0f;
            jawOpen = 0f;
            SetHurt(0f);
            Animate(0f);
        }

        private void HandleAttacked(MonsterAttack attack, bool landed)
        {
            windup = 0f;
            strike = 1f;
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

                propertyBlock.SetColor(BaseColorId, Color.Lerp(baseColours[i], hurtColour, hurt * 0.75f));
                renderers[i].SetPropertyBlock(propertyBlock);
            }
        }

        private static Color ColourOf(Renderer part)
        {
            Material material = part.sharedMaterial;
            return material != null && material.HasProperty(BaseColorId) ? material.GetColor(BaseColorId) : Color.white;
        }
    }
}
