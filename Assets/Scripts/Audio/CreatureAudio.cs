using System;
using System.Collections.Generic;
using UnityEngine;
using Wolfenstein.Combat;
using Wolfenstein.Enemies;
using Wolfenstein.Monsters;
using Random = UnityEngine.Random;

namespace Wolfenstein.Audio
{
    /// <summary>
    /// The sounds of every monster and guard: growls while they lurk and hunt, a cry when they notice the player, the
    /// wind-up and strike of each attack, gunshots, projectiles launching and bursting, pain, death and footsteps, each in
    /// the voice of the creature's <see cref="CreatureSounds"/>. It listens to the monster and enemy managers and to each
    /// creature's own events, and plays everything through the pooled voices of the <see cref="AudioManager"/>, so a
    /// creature carries no audio source of its own. Growls and footsteps are only asked for when the creature is close
    /// enough to be heard, and the audio manager keeps the nearest when too many are asked for at once.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CreatureAudio : MonoBehaviour
    {
        // Further than this in one frame is a teleport rather than a step.
        private const float MaxStepPerFrame = 2f;

        private sealed class Tracker
        {
            public CreatureSounds Sounds;
            public Transform Body;
            public Vector3 LastPosition;
            public float Stride;
            public float NextVocalTime;
            public MonsterState MonsterState;
            public EnemyState EnemyState;
            public Action Unsubscribe;
        }

        [SerializeField] private AudioManager audioManager;
        [SerializeField] private MonsterManager monsters;
        [SerializeField] private EnemyManager enemies;

        [SerializeField, Tooltip("Where a monster rises into a fight. Monsters placed to lurk in the level rise silently.")]
        private SoundCue spawnSound;

        [SerializeField, Range(0f, 1f), Tooltip("Chance a monster rising straight into the hunt cries out as it does. The rest stay quiet, so a wave is not a chorus.")]
        private float spawnCryChance = 0.5f;

        private readonly Dictionary<MonsterController, Tracker> monsterTrackers = new Dictionary<MonsterController, Tracker>();
        private readonly Dictionary<EnemyController, Tracker> guardTrackers = new Dictionary<EnemyController, Tracker>();
        private readonly List<EnemyController> goneGuards = new List<EnemyController>();

        private void OnEnable()
        {
            if (monsters != null)
            {
                monsters.Spawned += HandleMonsterSpawned;
                monsters.Killed += HandleMonsterKilled;
                monsters.ProjectileBurst += HandleProjectileBurst;
                foreach (MonsterController monster in monsters.Alive)
                    Track(monster);
            }

            if (enemies != null)
            {
                enemies.Spawned += HandleGuardSpawned;
                foreach (EnemyController guard in enemies.Enemies)
                    Track(guard);
            }
        }

        private void OnDisable()
        {
            if (monsters != null)
            {
                monsters.Spawned -= HandleMonsterSpawned;
                monsters.Killed -= HandleMonsterKilled;
                monsters.ProjectileBurst -= HandleProjectileBurst;
            }

            if (enemies != null)
                enemies.Spawned -= HandleGuardSpawned;

            foreach (Tracker tracker in monsterTrackers.Values)
                tracker.Unsubscribe();
            foreach (Tracker tracker in guardTrackers.Values)
                tracker.Unsubscribe();
            monsterTrackers.Clear();
            guardTrackers.Clear();
        }

        /// <summary>The voice a monster or guard speaks with. Null for one that makes no sound.</summary>
        public static CreatureSounds SoundsOf(MonsterController monster) => monster.Definition != null ? monster.Definition.Sounds : null;

        public static CreatureSounds SoundsOf(EnemyController guard) => guard.Definition != null ? guard.Definition.Sounds : null;

        private void Update()
        {
            float now = Time.time;
            Vector3 listener = audioManager.ListenerPosition;

            if (monsters != null)
            {
                foreach (MonsterController monster in monsters.Alive)
                {
                    if (!monsterTrackers.TryGetValue(monster, out Tracker tracker) || tracker.Sounds == null)
                        continue;

                    MonsterState state = monster.State;
                    bool hunting = state is MonsterState.Chase or MonsterState.Charge;
                    bool moving = state is MonsterState.Chase or MonsterState.Charge or MonsterState.Idle;
                    UpdateCreature(tracker, hunting, state == MonsterState.Idle, moving, listener, now);
                }
            }

            goneGuards.Clear();
            foreach (KeyValuePair<EnemyController, Tracker> entry in guardTrackers)
            {
                EnemyController guard = entry.Key;
                if (guard == null)
                {
                    goneGuards.Add(guard);
                    continue;
                }

                if (entry.Value.Sounds != null && guard.State != EnemyState.Dead)
                    UpdateCreature(entry.Value, guard.State == EnemyState.Chase, guard.State == EnemyState.Idle, true, listener, now);
            }

            // Guards are destroyed when the level is rebuilt.
            foreach (EnemyController gone in goneGuards)
                guardTrackers.Remove(gone);
        }

        // Footsteps as the creature covers ground, and a growl now and then while it lurks or hunts.
        private void UpdateCreature(Tracker tracker, bool hunting, bool lurking, bool moving, Vector3 listener, float now)
        {
            CreatureSounds sounds = tracker.Sounds;
            Vector3 position = tracker.Body.position;
            float moved = Vector3.ProjectOnPlane(position - tracker.LastPosition, Vector3.up).magnitude;
            tracker.LastPosition = position;
            float distance = Vector3.Distance(position, listener);

            SoundCue footstep = sounds.Footstep;
            if (footstep != null && moving && moved < MaxStepPerFrame)
            {
                tracker.Stride += moved;
                if (tracker.Stride >= sounds.StepDistance)
                {
                    tracker.Stride = Mathf.Min(tracker.Stride - sounds.StepDistance, sounds.StepDistance);
                    if (distance < footstep.MaxDistance)
                        audioManager.Play(footstep, position);
                }
            }

            if (now < tracker.NextVocalTime)
                return;

            tracker.NextVocalTime = now + sounds.PickVocalDelay();
            SoundCue vocal = hunting ? sounds.Hunt : lurking ? sounds.Idle : null;
            if (vocal != null && distance < vocal.MaxDistance)
                audioManager.Play(vocal, tracker.Body);
        }

        private void HandleMonsterSpawned(MonsterController monster)
        {
            Tracker tracker = Track(monster);
            tracker.Sounds = SoundsOf(monster);
            tracker.LastPosition = monster.transform.position;
            tracker.Stride = 0f;
            tracker.MonsterState = monster.State;
            tracker.NextVocalTime = Time.time + (tracker.Sounds != null ? tracker.Sounds.PickVocalDelay() : 0f);

            if (monster.HuntsOnceSpawned)
                audioManager.Play(spawnSound, monster.transform.position);
        }

        private void HandleMonsterKilled(MonsterController monster)
        {
            CreatureSounds sounds = SoundsOf(monster);
            if (sounds != null)
                audioManager.Play(sounds.Death, monster.transform.position);
        }

        private void HandleProjectileBurst(MonsterProjectile projectile, Vector3 point)
        {
            CreatureSounds sounds = projectile.Shooter != null ? SoundsOf(projectile.Shooter) : null;
            if (sounds != null)
                audioManager.Play(sounds.ProjectileImpact, point);
        }

        private Tracker Track(MonsterController monster)
        {
            if (monsterTrackers.TryGetValue(monster, out Tracker tracker))
                return tracker;

            tracker = new Tracker { Body = monster.transform, Sounds = SoundsOf(monster), LastPosition = monster.transform.position };
            Action<MonsterState> stateChanged = state => HandleMonsterState(monster, tracker, state);
            Action<MonsterAttack> attackStarted = attack => HandleAttackStarted(monster, tracker, attack);
            Action<MonsterAttack, bool> attacked = (attack, hit) => HandleAttacked(monster, tracker, attack);
            Action<DamageInfo> damaged = _ => HandleHurt(monster.Health, tracker);
            monster.StateChanged += stateChanged;
            monster.AttackStarted += attackStarted;
            monster.Attacked += attacked;
            monster.Health.Damaged += damaged;
            tracker.Unsubscribe = () =>
            {
                if (monster == null)
                    return;
                monster.StateChanged -= stateChanged;
                monster.AttackStarted -= attackStarted;
                monster.Attacked -= attacked;
                monster.Health.Damaged -= damaged;
            };

            monsterTrackers.Add(monster, tracker);
            return tracker;
        }

        private void HandleMonsterState(MonsterController monster, Tracker tracker, MonsterState state)
        {
            MonsterState previous = tracker.MonsterState;
            tracker.MonsterState = state;
            CreatureSounds sounds = tracker.Sounds;
            if (sounds == null)
                return;

            if (state == MonsterState.Chase && previous == MonsterState.Idle)
                audioManager.Play(sounds.Alert, monster.transform);
            else if (state == MonsterState.Chase && previous == MonsterState.Spawning && Random.value < spawnCryChance)
                audioManager.Play(sounds.Alert, monster.transform);
            else if (state == MonsterState.Charge)
                audioManager.Play(sounds.Charge, monster.transform);
        }

        private void HandleAttackStarted(MonsterController monster, Tracker tracker, MonsterAttack attack)
        {
            if (tracker.Sounds != null)
                audioManager.Play(attack == MonsterAttack.Melee ? tracker.Sounds.MeleeWindup : tracker.Sounds.RangedWindup, monster.transform);
        }

        private void HandleAttacked(MonsterController monster, Tracker tracker, MonsterAttack attack)
        {
            CreatureSounds sounds = tracker.Sounds;
            if (sounds == null)
                return;

            Transform body = monster.transform;
            if (attack == MonsterAttack.Ranged)
            {
                audioManager.Play(sounds.RangedLaunch, body);
                return;
            }

            audioManager.Play(sounds.MeleeStrike, body.position);

            // A blow with a shockwave lands in front of the monster, where its effect shows.
            MonsterDefinition definition = monster.Definition;
            if (definition.MeleeSplashRadius > 0f)
                audioManager.Play(sounds.Slam, body.position + body.forward * (definition.MeleeReach * 0.7f + monster.Motor.Radius));
        }

        // The blow that kills is heard as the death, not as one more cry of pain.
        private void HandleHurt(Health health, Tracker tracker)
        {
            if (tracker.Sounds != null && !health.IsDead)
                audioManager.Play(tracker.Sounds.Pain, tracker.Body);
        }

        private void HandleGuardSpawned(EnemyController guard) => Track(guard);

        private void Track(EnemyController guard)
        {
            if (guard == null || guardTrackers.ContainsKey(guard))
                return;

            var tracker = new Tracker
            {
                Body = guard.transform,
                Sounds = SoundsOf(guard),
                LastPosition = guard.transform.position,
                EnemyState = guard.State
            };
            tracker.NextVocalTime = Time.time + (tracker.Sounds != null ? tracker.Sounds.PickVocalDelay() : 0f);

            Action<EnemyState> stateChanged = state => HandleGuardState(guard, tracker, state);
            Action<bool> fired = _ => HandleGuardFired(guard, tracker);
            Action<DamageInfo> damaged = _ => HandleHurt(guard.Health, tracker);
            Action died = () => HandleGuardDied(guard, tracker);
            guard.StateChanged += stateChanged;
            guard.Fired += fired;
            guard.Health.Damaged += damaged;
            guard.Health.Died += died;
            tracker.Unsubscribe = () =>
            {
                if (guard == null)
                    return;
                guard.StateChanged -= stateChanged;
                guard.Fired -= fired;
                guard.Health.Damaged -= damaged;
                guard.Health.Died -= died;
            };

            guardTrackers.Add(guard, tracker);
        }

        private void HandleGuardState(EnemyController guard, Tracker tracker, EnemyState state)
        {
            EnemyState previous = tracker.EnemyState;
            tracker.EnemyState = state;
            if (tracker.Sounds != null && state == EnemyState.Chase && previous == EnemyState.Idle)
                audioManager.Play(tracker.Sounds.Alert, guard.transform);
        }

        private void HandleGuardFired(EnemyController guard, Tracker tracker)
        {
            if (tracker.Sounds != null)
                audioManager.Play(tracker.Sounds.RangedLaunch, guard.transform.position);
        }

        private void HandleGuardDied(EnemyController guard, Tracker tracker)
        {
            if (tracker.Sounds != null)
                audioManager.Play(tracker.Sounds.Death, guard.transform.position);
        }
    }
}
