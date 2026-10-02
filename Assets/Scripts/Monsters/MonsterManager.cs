using System;
using System.Collections.Generic;
using UnityEngine;
using Wolfenstein.Combat;
using Wolfenstein.Core;
using Wolfenstein.Pickups;
using Wolfenstein.Weapons;
using Wolfenstein.World;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Wolfenstein.Monsters
{
    /// <summary>
    /// Runs every monster in the level from one place. Monsters and their bolts are kept in pools and reused, so a long
    /// fight creates no garbage, and no more than a set number are ever alive at once. Each frame it drives every living
    /// monster and bolt in one pass instead of giving each its own Update, and it keeps one cost field per target that
    /// every monster hunting that target shares. It pays out the points for kills, leaves drops, lets lurking monsters hear
    /// the player's gunfire, and clears bodies away after a while. Once the level is built it also places the monsters that
    /// lurk in it from the start, where the level's decor map marks them.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MonsterManager : MonoBehaviour
    {
        [Serializable]
        private struct Breed
        {
            public MonsterDefinition definition;
            public MonsterController prefab;
        }

        [Serializable]
        private struct Lurker
        {
            public char symbol;
            public MonsterDefinition definition;
        }

        private sealed class CostField
        {
            public float[] Costs;
            public Vector2Int Goal = new Vector2Int(int.MinValue, int.MinValue);
            public float NextRefreshTime;
        }

        [SerializeField] private WorldManager world;

        [SerializeField, Tooltip("Leaves the items monsters drop.")]
        private PickupManager pickups;

        [SerializeField] private MonsterEffects effects;

        [SerializeField, Tooltip("The prefab each kind of monster is made from.")]
        private Breed[] breeds = Array.Empty<Breed>();

        [SerializeField, Tooltip("The monster each symbol of the level's decor map places, lurking until it notices the player.")]
        private Lurker[] lurkers = Array.Empty<Lurker>();

        [Header("Player")]
        [SerializeField, Tooltip("Hunted by every monster. Monsters aim at the middle of the collider on the same object.")]
        private Health playerHealth;

        [SerializeField, Tooltip("Earns the points for every monster killed.")]
        private Score playerScore;

        [SerializeField, Tooltip("Lurking monsters hear every shot this weapon fires.")]
        private WeaponController playerWeapon;

        [Header("Limits")]
        [SerializeField, Min(1), Tooltip("The most monsters that can be alive at once, however many are asked for.")]
        private int maxActiveMonsters = 32;

        [SerializeField, Min(0), Tooltip("The most bodies left lying at once. The oldest sink away first.")]
        private int maxBodies = 14;

        [SerializeField, Min(0f), Tooltip("Seconds a body lies before it sinks away.")]
        private float bodyLifetime = 8f;

        [SerializeField, Min(0.1f), Tooltip("Seconds a body takes to sink through the floor.")]
        private float bodySinkDuration = 1.5f;

        [Header("Movement")]
        [SerializeField, Min(0.05f), Tooltip("Longest a cost field goes without being worked out again, in seconds, so opened and locked doors are noticed.")]
        private float costFieldRefreshInterval = 1f;

        [SerializeField, Min(0f), Tooltip("Gap monsters try to keep between their bodies, in metres.")]
        private float personalSpace = 0.5f;

        private readonly List<MonsterController> alive = new List<MonsterController>();
        private readonly List<MonsterController> bodies = new List<MonsterController>();
        private readonly Dictionary<MonsterDefinition, Stack<MonsterController>> monsterPools = new Dictionary<MonsterDefinition, Stack<MonsterController>>();
        private readonly Dictionary<MonsterController, MonsterView> views = new Dictionary<MonsterController, MonsterView>();
        private readonly Dictionary<MonsterController, float> sinkStartTimes = new Dictionary<MonsterController, float>();
        private readonly List<MonsterProjectile> projectiles = new List<MonsterProjectile>();
        private readonly Dictionary<MonsterProjectile, Stack<MonsterProjectile>> projectilePools = new Dictionary<MonsterProjectile, Stack<MonsterProjectile>>();
        private readonly HashSet<Collider> monsterColliders = new HashSet<Collider>();
        private readonly List<Health> targets = new List<Health>();
        private readonly Dictionary<Health, CostField> costFields = new Dictionary<Health, CostField>();
        private readonly HashSet<Vector2Int> audibleCells = new HashSet<Vector2Int>();
        private int created;

        public event Action<MonsterController> Spawned;

        /// <summary>Raised when a monster is killed, after its points are paid out.</summary>
        public event Action<MonsterController> Killed;

        /// <summary>Raised with the bolt and the point where it burst, whatever it struck.</summary>
        public event Action<MonsterProjectile, Vector3> ProjectileBurst;

        /// <summary>Monsters in the level and not yet dead.</summary>
        public IReadOnlyList<MonsterController> Alive => alive;

        /// <summary>Dead monsters whose bodies still lie in the level.</summary>
        public IReadOnlyList<MonsterController> Bodies => bodies;

        /// <summary>Bolts in flight.</summary>
        public IReadOnlyList<MonsterProjectile> Projectiles => projectiles;

        /// <summary>Everything monsters hunt.</summary>
        public IReadOnlyList<Health> Targets => targets;

        public int MaxActiveMonsters => maxActiveMonsters;

        /// <summary>How many monster objects have ever been made; pooling keeps this at the most ever alive at once.</summary>
        public int MonstersCreated => created;

        public WorldManager World => world;

        private void Awake()
        {
            if (playerHealth != null)
                targets.Add(playerHealth);
        }

        private void OnEnable()
        {
            world.Built += PlaceLurkers;
            if (playerWeapon != null)
                playerWeapon.Fired += HandlePlayerFired;
        }

        private void OnDisable()
        {
            world.Built -= PlaceLurkers;
            if (playerWeapon != null)
                playerWeapon.Fired -= HandlePlayerFired;
        }

        public void AddTarget(Health target)
        {
            if (target != null && !targets.Contains(target))
                targets.Add(target);
        }

        public void RemoveTarget(Health target)
        {
            targets.Remove(target);
            costFields.Remove(target);
        }

        /// <summary>The nearest target still alive, or null if there is none.</summary>
        public Health FindNearestTarget(Vector3 from)
        {
            Health nearest = null;
            float nearestDistance = float.PositiveInfinity;
            foreach (Health target in targets)
            {
                if (target == null || target.IsDead)
                    continue;

                float distance = (target.transform.position - from).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearest = target;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        /// <summary>
        /// The cost field leading to a target, shared by every monster hunting it. It is worked out again whenever the
        /// target moves to another tile, and every so often in case a door has been locked or unlocked.
        /// </summary>
        public float[] GetCostField(Health target)
        {
            if (target == null || world.Navigator == null)
                return null;

            if (!costFields.TryGetValue(target, out CostField field))
            {
                field = new CostField { Costs = new float[world.Navigator.CellCount] };
                costFields.Add(target, field);
            }

            Vector2Int goal = world.WorldToCell(target.transform.position);
            if (goal != field.Goal || Time.time >= field.NextRefreshTime)
            {
                field.Goal = goal;
                field.NextRefreshTime = Time.time + costFieldRefreshInterval;
                world.Navigator.FindCostsTo(goal, field.Costs);
            }

            return field.Costs;
        }

        public bool IsMonsterCollider(Collider candidate) => candidate != null && monsterColliders.Contains(candidate);

        /// <summary>
        /// A push away from any living monster crowding this one, from nothing when there is room to its full strength of 1
        /// when bodies touch. Only monsters closer than their own sizes plus a little space count.
        /// </summary>
        public Vector3 GetSeparation(MonsterMotor motor)
        {
            Vector3 position = motor.transform.position;
            Vector3 push = Vector3.zero;
            foreach (MonsterController other in alive)
            {
                if (other.Motor == motor)
                    continue;

                Vector3 offset = Vector3.ProjectOnPlane(position - other.transform.position, Vector3.up);
                float reach = motor.Radius + other.Motor.Radius + personalSpace;
                float distance = offset.magnitude;
                if (distance >= reach)
                    continue;

                // Two monsters in the same spot push apart in some direction rather than none.
                Vector3 away = distance > 0.001f ? offset / distance : Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
                push += away * (1f - distance / reach);
            }

            return Vector3.ClampMagnitude(push, 1f);
        }

        /// <summary>Makes monsters ahead of time, so a wave does not have to create them mid-fight.</summary>
        public void Prewarm(MonsterDefinition definition, int count)
        {
            Stack<MonsterController> pool = GetPool(definition);
            int missing = count - pool.Count - CountActive(definition);
            for (int i = 0; i < missing; i++)
            {
                MonsterController monster = Create(definition);
                if (monster == null)
                    return;

                pool.Push(monster);
            }
        }

        /// <summary>
        /// Whether a monster of this kind would have room to stand at a point, clear of walls, props, the player and other
        /// monsters.
        /// </summary>
        public bool HasRoomFor(MonsterDefinition definition, Vector3 position)
        {
            MonsterController prefab = FindPrefab(definition);
            if (prefab == null)
                return false;

            var body = prefab.GetComponent<CharacterController>();
            float radius = body.radius + 0.05f;
            Vector3 bottom = position + Vector3.up * (radius + 0.1f);
            Vector3 top = position + Vector3.up * Mathf.Max(radius + 0.1f, body.height - radius);
            return !Physics.CheckCapsule(bottom, top, radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        /// <summary>
        /// Brings a monster into the level at a point, rising from the floor, unless the most monsters allowed are
        /// already alive. Returns the monster, or null if none could be spawned.
        /// </summary>
        public MonsterController Spawn(MonsterDefinition definition, Vector3 position, float yaw, MonsterModifiers difficulty, bool hunt = true)
        {
            if (alive.Count >= maxActiveMonsters)
                return null;

            Stack<MonsterController> pool = GetPool(definition);
            MonsterController monster = pool.Count > 0 ? pool.Pop() : Create(definition);
            if (monster == null)
                return null;

            monster.gameObject.SetActive(true);
            monster.Activate(this, world, position, yaw, difficulty, hunt);
            monster.name = $"{definition.name} {created}";
            alive.Add(monster);

            // Physics sees the monster's new position before the next simulation step only after an explicit sync.
            Physics.SyncTransforms();

            var body = monster.GetComponent<CharacterController>();
            if (effects != null)
                effects.Spawned(position, body.radius, body.height);

            Spawned?.Invoke(monster);
            return monster;
        }

        /// <summary>Moves a living monster elsewhere at once, such as one hopelessly stuck, with a spawn effect.</summary>
        public void Relocate(MonsterController monster, Vector3 position)
        {
            monster.Motor.Teleport(position, monster.transform.rotation);
            Physics.SyncTransforms();
            var body = monster.GetComponent<CharacterController>();
            if (effects != null)
                effects.Spawned(position, body.radius, body.height);
        }

        /// <summary>Takes a monster, living or dead, out of the level and back into its pool without a kill.</summary>
        public void Despawn(MonsterController monster)
        {
            if (!alive.Remove(monster) && !bodies.Remove(monster))
                return;

            sinkStartTimes.Remove(monster);
            monster.Deactivate();
            monster.gameObject.SetActive(false);
            GetPool(monster.Definition).Push(monster);
        }

        /// <summary>Clears every monster, body and bolt out of the level.</summary>
        public void DespawnAll()
        {
            for (int i = alive.Count - 1; i >= 0; i--)
                Despawn(alive[i]);
            for (int i = bodies.Count - 1; i >= 0; i--)
                Despawn(bodies[i]);
            for (int i = projectiles.Count - 1; i >= 0; i--)
                PutAway(i);
        }

        /// <summary>Launches a bolt from the pool of the given prefab.</summary>
        public MonsterProjectile FireProjectile(MonsterProjectile prefab, Vector3 origin, Vector3 velocity, int damage, MonsterController shooter)
        {
            if (prefab == null)
                return null;

            if (!projectilePools.TryGetValue(prefab, out Stack<MonsterProjectile> pool))
            {
                pool = new Stack<MonsterProjectile>();
                projectilePools.Add(prefab, pool);
            }

            MonsterProjectile projectile = pool.Count > 0 ? pool.Pop() : Instantiate(prefab, transform);
            projectile.Prefab = prefab;
            projectile.Launch(this, shooter, origin, velocity, damage);
            projectile.gameObject.SetActive(true);
            projectiles.Add(projectile);
            return projectile;
        }

        /// <summary>Shows a bolt bursting against a surface.</summary>
        public void ShowImpact(MonsterProjectile projectile, Vector3 point, Vector3 normal)
        {
            if (effects != null)
                effects.Impact(point, normal, projectile.ImpactColour);

            ProjectileBurst?.Invoke(projectile, point);
        }

        /// <summary>Shows the shockwave of a heavy blow.</summary>
        public void ShowSlam(Vector3 position, float radius)
        {
            if (effects != null)
                effects.Slam(position, radius);
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
                return;

            // Backwards, so a monster that dies or is put away during its turn does not upset the loop.
            for (int i = alive.Count - 1; i >= 0; i--)
            {
                if (i < alive.Count)
                    alive[i].Tick(deltaTime);
            }

            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                if (!projectiles[i].Tick(deltaTime))
                    PutAway(i);
            }

            UpdateBodies(deltaTime);
        }

        private void LateUpdate()
        {
            float deltaTime = Time.deltaTime;
            foreach (MonsterController monster in alive)
                Animate(monster, deltaTime);
            foreach (MonsterController monster in bodies)
                Animate(monster, deltaTime);
        }

        private void Animate(MonsterController monster, float deltaTime)
        {
            if (views.TryGetValue(monster, out MonsterView view) && view != null)
                view.Animate(deltaTime);
        }

        // Bodies lie for a while, then sink through the floor and go back to their pools. Past the most bodies allowed,
        // the oldest start sinking at once.
        private void UpdateBodies(float deltaTime)
        {
            float now = Time.time;
            int excess = bodies.Count - maxBodies;
            for (int i = 0; i < bodies.Count && excess > 0; i++, excess--)
            {
                if (!sinkStartTimes.ContainsKey(bodies[i]))
                    sinkStartTimes[bodies[i]] = now;
            }

            for (int i = bodies.Count - 1; i >= 0; i--)
            {
                MonsterController body = bodies[i];
                if (!sinkStartTimes.TryGetValue(body, out float sinkStart))
                {
                    if (now - body.TimeOfDeath < bodyLifetime)
                        continue;

                    sinkStart = now;
                    sinkStartTimes[body] = sinkStart;
                }

                float height = body.GetComponent<CharacterController>().height;
                body.transform.position += Vector3.down * (height / bodySinkDuration * deltaTime);
                if (now - sinkStart >= bodySinkDuration)
                    Despawn(body);
            }
        }

        // Each lurker faces the nearest door, ready for whoever comes through it.
        private void PlaceLurkers()
        {
            DespawnAll();
            LevelLayout layout = world.Layout;
            for (int y = 0; y < layout.Height; y++)
            {
                for (int x = 0; x < layout.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    char symbol = layout.GetDecor(cell);
                    if (symbol == '\0' || !layout.IsWalkable(cell) || !TryGetLurker(symbol, out MonsterDefinition definition))
                        continue;

                    Vector3 position = world.CellToWorld(cell);
                    Spawn(definition, position, YawToNearestDoor(position), MonsterModifiers.None, hunt: false);
                }
            }
        }

        private bool TryGetLurker(char symbol, out MonsterDefinition definition)
        {
            foreach (Lurker lurker in lurkers)
            {
                if (lurker.symbol == symbol && lurker.definition != null)
                {
                    definition = lurker.definition;
                    return true;
                }
            }

            definition = null;
            return false;
        }

        private float YawToNearestDoor(Vector3 position)
        {
            Vector3 nearest = position + Vector3.forward;
            float nearestDistance = float.PositiveInfinity;
            foreach (Door door in world.Doors.Values)
            {
                float distance = (door.transform.position - position).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearest = door.transform.position;
                    nearestDistance = distance;
                }
            }

            Vector3 direction = Vector3.ProjectOnPlane(nearest - position, Vector3.up);
            return direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction).eulerAngles.y : 0f;
        }

        private MonsterController Create(MonsterDefinition definition)
        {
            MonsterController prefab = FindPrefab(definition);
            if (prefab == null)
            {
                Debug.LogError($"No monster prefab is set for '{definition.name}'.", this);
                return null;
            }

            MonsterController monster = Instantiate(prefab, transform);
            monster.gameObject.SetActive(false);
            monster.Died += HandleDied;
            created++;

            foreach (Collider part in monster.GetComponentsInChildren<Collider>(true))
                monsterColliders.Add(part);

            var view = monster.GetComponent<MonsterView>();
            if (view != null)
                views.Add(monster, view);

            return monster;
        }

        private MonsterController FindPrefab(MonsterDefinition definition)
        {
            foreach (Breed breed in breeds)
            {
                if (breed.definition == definition)
                    return breed.prefab;
            }

            return null;
        }

        private Stack<MonsterController> GetPool(MonsterDefinition definition)
        {
            if (!monsterPools.TryGetValue(definition, out Stack<MonsterController> pool))
            {
                pool = new Stack<MonsterController>();
                monsterPools.Add(definition, pool);
            }

            return pool;
        }

        private int CountActive(MonsterDefinition definition)
        {
            int count = 0;
            foreach (MonsterController monster in alive)
            {
                if (monster.Definition == definition)
                    count++;
            }

            foreach (MonsterController monster in bodies)
            {
                if (monster.Definition == definition)
                    count++;
            }

            return count;
        }

        private void PutAway(int index)
        {
            MonsterProjectile projectile = projectiles[index];
            projectiles.RemoveAt(index);
            projectile.gameObject.SetActive(false);
            if (projectile.Prefab != null && projectilePools.TryGetValue(projectile.Prefab, out Stack<MonsterProjectile> pool))
                pool.Push(projectile);
            else
                Object.Destroy(projectile.gameObject);
        }

        private void HandleDied(MonsterController monster)
        {
            alive.Remove(monster);
            bodies.Add(monster);

            MonsterDefinition definition = monster.Definition;
            if (playerScore != null)
                playerScore.AddKill(definition.KillScore);

            if (pickups != null && definition.Drop != null && Random.value < definition.DropChance)
                pickups.Drop(definition.Drop, monster.transform.position);

            if (effects != null)
            {
                var body = monster.GetComponent<CharacterController>();
                MonsterView view = views.TryGetValue(monster, out MonsterView found) ? found : null;
                Color blood = view != null ? view.BloodColour : new Color(0.4f, 0.02f, 0.02f);
                effects.Killed(monster.transform.position + Vector3.up * (body.height * 0.45f), body.height / 1.8f, blood);
            }

            Killed?.Invoke(monster);
        }

        // As with guards, a shot alerts every lurking monster in the areas joined to the shooter by doors that are not closed.
        private void HandlePlayerFired(ShotResult shot)
        {
            if (alive.Count == 0 || world.Navigator == null)
                return;

            world.Navigator.FindAudibleCells(world.WorldToCell(shot.Ray.origin), audibleCells);
            foreach (MonsterController monster in alive)
            {
                if (monster.State == MonsterState.Idle && audibleCells.Contains(world.WorldToCell(monster.transform.position)))
                    monster.Alert();
            }
        }
    }
}
