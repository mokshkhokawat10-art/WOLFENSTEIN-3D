using System;
using System.Collections.Generic;
using UnityEngine;
using Wolfenstein.Waves;
using Wolfenstein.World;

namespace Wolfenstein.Progression
{
    /// <summary>
    /// Everything that makes one level of the episode: its name and scene, what the player starts with, the objectives
    /// worked through in order, the doors held shut until they are earned, the planned encounters, the arena's waves, the
    /// exit, and the level that follows. The exit opens once every objective before the last is done; the last objective
    /// is always to reach it. A new level is a new definition, a layout and a scene rather than new code.
    /// </summary>
    [CreateAssetMenu(fileName = "Level", menuName = "Wolfenstein/Level Definition")]
    public sealed class LevelDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField, Min(1)] private int number = 1;

        [SerializeField, Tooltip("The level's name, such as THE BUNKER.")]
        private string displayName = "LEVEL";

        [SerializeField, Tooltip("The scene the level is played in, by name. It must be in the build settings.")]
        private string sceneName = string.Empty;

        [SerializeField, Tooltip("The tile map the level's scene is built from.")]
        private LevelLayout layout;

        [Header("Player")]
        [SerializeField, Min(1)] private int startingHealth = 100;
        [SerializeField, Min(0)] private int startingAmmo = 24;

        [Header("Progress")]
        [SerializeField, Tooltip("Worked through in order. The last should be to reach the exit.")]
        private ObjectiveDefinition[] objectives = Array.Empty<ObjectiveDefinition>();

        [SerializeField] private DoorLock[] doorLocks = Array.Empty<DoorLock>();
        [SerializeField] private EncounterDefinition[] encounters = Array.Empty<EncounterDefinition>();

        [SerializeField, Tooltip("The arena's waves. Empty for a level without an arena.")]
        private WaveSet waves;

        [Header("Exit")]
        [SerializeField, Tooltip("Walking into this zone of the zone map, once the exit is open, completes the level.")]
        private char exitZone = 'x';

        [SerializeField, Tooltip("The decor map symbol on the door tiles held shut until the exit opens.")]
        private char exitDoorMarker = 'E';

        [SerializeField, Tooltip("Played next. Empty for the last level of the episode.")]
        private LevelDefinition nextLevel;

        public int Number => number;
        public string DisplayName => displayName;

        /// <summary>The level's number as the player sees it, such as LEVEL 1.</summary>
        public string Title => $"LEVEL {number}";

        public string SceneName => sceneName;
        public LevelLayout Layout => layout;
        public int StartingHealth => startingHealth;
        public int StartingAmmo => startingAmmo;
        public IReadOnlyList<ObjectiveDefinition> Objectives => objectives;
        public IReadOnlyList<DoorLock> DoorLocks => doorLocks;
        public IReadOnlyList<EncounterDefinition> Encounters => encounters;
        public WaveSet Waves => waves;
        public char ExitZone => exitZone;
        public char ExitDoorMarker => exitDoorMarker;
        public LevelDefinition NextLevel => nextLevel;
        public bool IsLastLevel => nextLevel == null;

        /// <summary>Makes a level definition in code rather than by hand, such as for the level builder or a test.</summary>
        public static LevelDefinition Create(int number, string displayName, string sceneName, LevelLayout layout, int startingHealth,
            int startingAmmo, ObjectiveDefinition[] objectives, DoorLock[] doorLocks, EncounterDefinition[] encounters, WaveSet waves,
            char exitZone, char exitDoorMarker, LevelDefinition nextLevel)
        {
            var level = CreateInstance<LevelDefinition>();
            level.number = Mathf.Max(1, number);
            level.displayName = displayName;
            level.sceneName = sceneName;
            level.layout = layout;
            level.startingHealth = Mathf.Max(1, startingHealth);
            level.startingAmmo = Mathf.Max(0, startingAmmo);
            level.objectives = objectives ?? Array.Empty<ObjectiveDefinition>();
            level.doorLocks = doorLocks ?? Array.Empty<DoorLock>();
            level.encounters = encounters ?? Array.Empty<EncounterDefinition>();
            level.waves = waves;
            level.exitZone = exitZone;
            level.exitDoorMarker = exitDoorMarker;
            level.nextLevel = nextLevel;
            return level;
        }

        /// <summary>The lock whose marker this is, or null if it is not a lock's.</summary>
        public DoorLock FindLock(char marker)
        {
            foreach (DoorLock doorLock in doorLocks)
            {
                if (doorLock != null && doorLock.Marker == marker && marker != '\0')
                    return doorLock;
            }

            return null;
        }

        private void OnValidate()
        {
            if (objectives.Length > 0 && objectives[objectives.Length - 1].Kind != ObjectiveKind.ReachExit)
                Debug.LogWarning($"The last objective of {name} should be to reach the exit.", this);
        }
    }
}
