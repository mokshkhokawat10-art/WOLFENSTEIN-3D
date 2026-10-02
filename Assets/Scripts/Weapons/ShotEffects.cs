using UnityEngine;
using Wolfenstein.Combat;

namespace Wolfenstein.Weapons
{
    /// <summary>
    /// Small bursts of particles where shots land: dust and sparks off scenery, and blood from anything hurt,
    /// with a bigger burst for the shot that kills. It is purely visual and only listens to the <see cref="WeaponController"/>.
    /// Each burst is a handful of particles emitted into one shared system, so a busy fight costs almost nothing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShotEffects : MonoBehaviour
    {
        [SerializeField]
        private WeaponController weapon;

        [Header("Particle Systems")]
        [SerializeField, Tooltip("Dust kicked up by a shot that strikes scenery. Should simulate in world space.")]
        private ParticleSystem dust;

        [SerializeField, Tooltip("Sparks thrown off by a shot that strikes scenery. Should simulate in world space.")]
        private ParticleSystem sparks;

        [SerializeField, Tooltip("Blood from anything a shot hurts. Should simulate in world space.")]
        private ParticleSystem blood;

        [Header("Bursts")]
        [SerializeField, Min(0)] private int dustPerShot = 7;
        [SerializeField, Min(0)] private int sparksPerShot = 3;
        [SerializeField, Min(0)] private int bloodPerHit = 8;
        [SerializeField, Min(0)] private int bloodPerKill = 22;

        [SerializeField, Min(0f), Tooltip("Top speed of the particles thrown off a surface, in metres per second.")]
        private float burstSpeed = 2.5f;

        private void OnEnable() => weapon.Fired += HandleFired;

        private void OnDisable() => weapon.Fired -= HandleFired;

        private void HandleFired(ShotResult shot)
        {
            if (!shot.HasHit)
                return;

            RaycastHit hit = shot.Hit;
            if (shot.Target == null)
            {
                // Pulled clear of the surface, so no particle starts inside it.
                Vector3 point = hit.point + hit.normal * 0.03f;
                Burst(dust, point, hit.normal, dustPerShot, burstSpeed * 0.6f, 0.9f);
                Burst(sparks, point, hit.normal, sparksPerShot, burstSpeed * 1.6f, 0.6f);
                return;
            }

            // Blood sprays back toward the shooter. The damage has already been dealt, so a killing shot can be told apart.
            bool killed = shot.Target is Health health && health.IsDead;
            Burst(blood, hit.point, -shot.Ray.direction, killed ? bloodPerKill : bloodPerHit, burstSpeed, 1.1f);
        }

        private static void Burst(ParticleSystem system, Vector3 point, Vector3 direction, int count, float speed, float spread)
        {
            if (system == null)
                return;

            var particle = new ParticleSystem.EmitParams { applyShapeToPosition = false, position = point };
            for (int i = 0; i < count; i++)
            {
                Vector3 heading = (direction + Random.insideUnitSphere * spread).normalized;
                particle.velocity = heading * (speed * Random.Range(0.4f, 1f));
                system.Emit(particle, 1);
            }
        }
    }
}
