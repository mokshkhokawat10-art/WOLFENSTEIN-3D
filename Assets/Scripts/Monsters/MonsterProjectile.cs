using UnityEngine;
using Wolfenstein.Combat;

namespace Wolfenstein.Monsters
{
    /// <summary>
    /// A glowing bolt fired by a monster. It flies straight, sweeping a sphere ahead of it each frame, and bursts on the
    /// first solid thing it meets, hurting it if it can be hurt. It passes through other monsters and through triggers.
    /// It is pooled and moved by the <see cref="MonsterManager"/>, so it has no Update of its own.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MonsterProjectile : MonoBehaviour
    {
        private static readonly RaycastHit[] Hits = new RaycastHit[8];

        [SerializeField, Min(0.01f), Tooltip("Radius of the bolt for hitting things, in metres.")]
        private float radius = 0.15f;

        [SerializeField, Min(0.1f), Tooltip("Seconds before a bolt that hits nothing fades away.")]
        private float lifetime = 4f;

        [SerializeField, Tooltip("Layers the bolt can strike. Trigger colliders never stop it.")]
        private LayerMask hitMask = Physics.DefaultRaycastLayers;

        [SerializeField, ColorUsage(false, true), Tooltip("Colour of the burst where it strikes.")]
        private Color impactColour = new Color(2f, 1f, 0.2f);

        [SerializeField, Tooltip("Cleared on every launch, so a reused bolt does not trail from where it last flew.")]
        private TrailRenderer trail;

        private MonsterManager manager;
        private MonsterController shooter;
        private Vector3 velocity;
        private int damage;
        private float expiryTime;

        /// <summary>The prefab this bolt was made from, which is the pool it returns to.</summary>
        public MonsterProjectile Prefab { get; set; }

        public Color ImpactColour => impactColour;

        /// <summary>The monster that fired the bolt. It may have died since.</summary>
        public MonsterController Shooter => shooter;
        public Vector3 Velocity => velocity;
        public int Damage => damage;

        public void Launch(MonsterManager owner, MonsterController firedBy, Vector3 position, Vector3 launchVelocity, int hitDamage)
        {
            manager = owner;
            shooter = firedBy;
            velocity = launchVelocity;
            damage = hitDamage;
            expiryTime = Time.time + lifetime;
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(launchVelocity));
            if (trail != null)
                trail.Clear();
        }

        /// <summary>Flies for one frame. Returns false once the bolt has burst or faded and should be put away.</summary>
        public bool Tick(float deltaTime)
        {
            if (Time.time >= expiryTime)
                return false;

            Vector3 step = velocity * deltaTime;
            float distance = step.magnitude;
            if (distance <= 0f)
                return true;

            Vector3 direction = step / distance;
            int count = Physics.SphereCastNonAlloc(transform.position, radius, direction, Hits, distance, hitMask, QueryTriggerInteraction.Ignore);
            int nearest = -1;
            for (int i = 0; i < count; i++)
            {
                if (manager.IsMonsterCollider(Hits[i].collider))
                    continue;

                if (nearest < 0 || Hits[i].distance < Hits[nearest].distance)
                    nearest = i;
            }

            if (nearest < 0)
            {
                transform.position += step;
                return true;
            }

            RaycastHit hit = Hits[nearest];

            // A sweep that starts touching something reports no point, so the bolt bursts where it is.
            Vector3 point = hit.distance > 0f ? hit.point : transform.position;
            var target = hit.collider.GetComponentInParent<IDamageable>();
            GameObject source = shooter != null ? shooter.gameObject : null;
            target?.TakeDamage(new DamageInfo(damage, point, direction, source));
            manager.ShowImpact(this, point, hit.distance > 0f ? hit.normal : -direction);
            return false;
        }
    }
}
