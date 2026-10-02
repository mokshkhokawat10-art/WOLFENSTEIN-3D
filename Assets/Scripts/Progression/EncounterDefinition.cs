using System;
using System.Collections.Generic;
using UnityEngine;
using Wolfenstein.Waves;

namespace Wolfenstein.Progression
{
    /// <summary>
    /// A planned fight away from the arena: monsters that rise from marked points the first time the player walks into a
    /// zone, or the moment a switch is thrown. Each encounter has its own marker in the level's decor map for the points
    /// its monsters rise at.
    /// </summary>
    [Serializable]
    public sealed class EncounterDefinition
    {
        [SerializeField, Tooltip("What the level calls it, such as CORRIDOR AMBUSH.")]
        private string name = "ENCOUNTER";

        [SerializeField, Tooltip("Walking into this zone of the zone map sets it off. Leave empty for one set off by a switch.")]
        private char triggerZone;

        [SerializeField, Tooltip("Throwing the switch on this channel sets it off, when no zone is set.")]
        private char triggerSwitch;

        [SerializeField, Tooltip("The decor map symbol of the points its monsters rise at.")]
        private char spawnMarker;

        [SerializeField, Tooltip("The monsters that rise, and how many of each.")]
        private WaveGroup[] monsters = Array.Empty<WaveGroup>();

        public EncounterDefinition(string name, char triggerZone, char triggerSwitch, char spawnMarker, params WaveGroup[] monsters)
        {
            this.name = name;
            this.triggerZone = triggerZone;
            this.triggerSwitch = triggerSwitch;
            this.spawnMarker = spawnMarker;
            this.monsters = monsters;
        }

        public string Name => name;
        public char TriggerZone => triggerZone;
        public char TriggerSwitch => triggerSwitch;
        public char SpawnMarker => spawnMarker;
        public IReadOnlyList<WaveGroup> Monsters => monsters;

        public int TotalCount
        {
            get
            {
                int total = 0;
                foreach (WaveGroup group in monsters)
                {
                    if (group.Monster != null)
                        total += group.Count;
                }

                return total;
            }
        }
    }
}
