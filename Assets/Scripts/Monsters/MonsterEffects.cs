using UnityEngine;

namespace Wolfenstein.Monsters
{
    /// <summary>
    /// Lightweight bursts of particles for the monster fight: a ring and rising embers where a monster spawns, gore and
    /// mist where one dies, sparks where a bolt bursts and a shockwave where a heavy blow lands.
    /// Every burst is a handful of particles emitted into a few shared systems, so a crowded fight costs almost nothing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MonsterEffects : MonoBehaviour
    {
        [SerializeField, Tooltip("Glowing motes that drift upward. Should simulate in world space.")]
        private ParticleSystem embers;

        [SerializeField, Tooltip("Chunks thrown out that fall under gravity. Should simulate in world space.")]
        private ParticleSystem gore;

        [SerializeField, Tooltip("Soft puffs that spread and fade. Should simulate in world space.")]
        private ParticleSystem mist;

        [SerializeField, Tooltip("Bright specks that fly out fast. Should simulate in world space.")]
        private ParticleSystem sparks;

        [SerializeField, Tooltip("Flat rings that lie on the floor and grow. Should simulate in world space with 3D rotation.")]
        private ParticleSystem rings;

        [SerializeField, ColorUsage(false, true)] private Color spawnColour = new Color(2.4f, 0.35f, 0.15f);
        [SerializeField] private Color slamColour = new Color(0.55f, 0.5f, 0.45f, 0.8f);

        /// <summary>A summoning ring on the floor and embers rising through the space the monster rises into.</summary>
        public void Spawned(Vector3 position, float radius, float height)
        {
            EmitRing(position, radius * 3f, spawnColour, 0.9f);
            for (int i = 0; i < 14; i++)
            {
                Vector2 disc = Random.insideUnitCircle * radius;
                Vector3 start = position + new Vector3(disc.x, Random.Range(0f, height * 0.4f), disc.y);
                Emit(embers, start, Vector3.up * Random.Range(1.2f, 3f) + Random.insideUnitSphere * 0.4f, Random.Range(0.04f, 0.09f), spawnColour);
            }
        }

        /// <summary>Gore flung out and a cloud of mist where the monster falls, bigger for bigger monsters.</summary>
        public void Killed(Vector3 centre, float size, Color blood)
        {
            int chunks = Mathf.RoundToInt(10f * size);
            for (int i = 0; i < chunks; i++)
            {
                Vector3 velocity = Random.insideUnitSphere * 3f + Vector3.up * Random.Range(1.5f, 4f);
                Emit(gore, centre + Random.insideUnitSphere * 0.3f * size, velocity, Random.Range(0.06f, 0.14f) * size, blood);
            }

            for (int i = 0; i < 5; i++)
                Emit(mist, centre + Random.insideUnitSphere * 0.4f * size, Random.insideUnitSphere * 0.6f, Random.Range(0.5f, 0.9f) * size, blood);
        }

        /// <summary>Sparks thrown back off the surface a bolt burst against.</summary>
        public void Impact(Vector3 point, Vector3 normal, Color colour)
        {
            for (int i = 0; i < 10; i++)
                Emit(sparks, point + normal * 0.05f, (normal + Random.insideUnitSphere).normalized * Random.Range(2f, 5f), Random.Range(0.03f, 0.07f), colour);

            Emit(mist, point + normal * 0.1f, normal * 0.3f, 0.45f, colour * 0.6f);
        }

        /// <summary>A shockwave of dust spreading across the floor from a heavy blow.</summary>
        public void Slam(Vector3 position, float radius)
        {
            EmitRing(position, radius * 2f, slamColour, 0.5f);
            for (int i = 0; i < 10; i++)
            {
                Vector2 direction = Random.insideUnitCircle.normalized;
                Emit(mist, position + Vector3.up * 0.2f, new Vector3(direction.x, 0.2f, direction.y) * 3f, Random.Range(0.3f, 0.6f), slamColour);
            }
        }

        private void EmitRing(Vector3 position, float size, Color colour, float lifetime)
        {
            if (rings == null)
                return;

            var particle = new ParticleSystem.EmitParams
            {
                applyShapeToPosition = false,
                position = position + Vector3.up * 0.03f,
                velocity = Vector3.zero,
                startSize = size,
                startColor = colour,
                startLifetime = lifetime,
                rotation3D = new Vector3(90f, Random.Range(0f, 360f), 0f)
            };
            rings.Emit(particle, 1);
        }

        private static void Emit(ParticleSystem system, Vector3 position, Vector3 velocity, float size, Color colour)
        {
            if (system == null)
                return;

            var particle = new ParticleSystem.EmitParams
            {
                applyShapeToPosition = false,
                position = position,
                velocity = velocity,
                startSize = size,
                startColor = colour
            };
            system.Emit(particle, 1);
        }
    }
}
