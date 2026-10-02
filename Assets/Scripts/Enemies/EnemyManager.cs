using System;
using System.Collections.Generic;
using UnityEngine;
using Wolfenstein.Combat;
using Wolfenstein.Core;
using Wolfenstein.Pickups;
using Wolfenstein.Weapons;
using Wolfenstein.World;

namespace Wolfenstein.Enemies
{
    /// <summary>
    /// Populates the level with enemies: once the world is built it places a guard on every guard tile of the layout
    /// and sets each one on the player. It also lets them hear the player's gunfire. As in the original game,
    /// a shot alerts every enemy in the areas joined to the shooter by doors that are not closed,
    /// and every kill earns the player the enemy's points and leaves its drop where it fell.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyManager : MonoBehaviour
    {
        [SerializeField] private WorldManager world;

        [SerializeField, Tooltip("Placed on every guard tile of the level layout.")]
        private EnemyController guardPrefab;

        [SerializeField, Tooltip("Leaves the items enemies drop when they die.")]
        private PickupManager pickups;

        [Header("Player")]
        [SerializeField, Tooltip("The player enemies hunt. Enemies look and aim at the middle of the collider on the same object.")]
        private Health playerHealth;

        [SerializeField, Tooltip("Enemies hear every shot this weapon fires.")]
        private WeaponController playerWeapon;

        [SerializeField, Tooltip("Earns the points for every enemy killed.")]
        private Score playerScore;

        private readonly List<EnemyController> enemies = new List<EnemyController>();
        private readonly HashSet<Vector2Int> audibleCells = new HashSet<Vector2Int>();

        /// <summary>Raised for every guard placed in the level.</summary>
        public event Action<EnemyController> Spawned;

        public IReadOnlyList<EnemyController> Enemies => enemies;

        private void OnEnable()
        {
            world.Built += SpawnLevelEnemies;
            playerWeapon.Fired += HandlePlayerFired;
        }

        private void OnDisable()
        {
            world.Built -= SpawnLevelEnemies;
            playerWeapon.Fired -= HandlePlayerFired;
        }

        /// <summary>
        /// Places a guard at the centre of a cell, facing the given yaw, and sets it on the player.
        /// </summary>
        public EnemyController SpawnGuard(Vector2Int cell, float yaw)
        {
            Quaternion rotation = world.transform.rotation * Quaternion.Euler(0f, yaw, 0f);
            EnemyController guard = Instantiate(guardPrefab, world.CellToWorld(cell), rotation, transform);
            guard.name = $"Guard ({cell.x}, {cell.y})";
            guard.Initialise(world, playerHealth);
            guard.Health.Died += () => HandleEnemyDied(guard);

            enemies.Add(guard);
            Spawned?.Invoke(guard);
            return guard;
        }

        private void SpawnLevelEnemies()
        {
            foreach (EnemyController enemy in enemies)
            {
                if (enemy != null)
                    Destroy(enemy.gameObject);
            }

            enemies.Clear();

            LevelLayout layout = world.Layout;
            for (int y = 0; y < layout.Height; y++)
            {
                for (int x = 0; x < layout.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (layout.TryGetGuardSpawn(cell, out float yaw))
                        SpawnGuard(cell, yaw);
                }
            }
        }

        private void HandleEnemyDied(EnemyController enemy)
        {
            EnemyDefinition definition = enemy.Definition;
            playerScore.AddKill(definition.KillScore);

            if (definition.Drop != null)
                pickups.Drop(definition.Drop, enemy.transform.position);
        }

        private void HandlePlayerFired(ShotResult shot)
        {
            world.Navigator.FindAudibleCells(world.WorldToCell(shot.Ray.origin), audibleCells);

            foreach (EnemyController enemy in enemies)
            {
                if (enemy != null && audibleCells.Contains(world.WorldToCell(enemy.transform.position)))
                    enemy.Alert();
            }
        }
    }
}
