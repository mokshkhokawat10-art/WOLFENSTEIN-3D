using System;
using UnityEngine;
using Wolfenstein.Pickups;

namespace Wolfenstein.Progression
{
    /// <summary>One step of a level, such as finding a key or clearing the arena, and the words that tell the player about it.</summary>
    [Serializable]
    public sealed class ObjectiveDefinition
    {
        [SerializeField] private ObjectiveKind kind;

        [SerializeField, Tooltip("Shown on the heads-up display while the objective is current, such as FIND THE SECURITY KEY.")]
        private string title = string.Empty;

        [SerializeField, Tooltip("For collecting a key: the key.")]
        private AccessKey key;

        [SerializeField, Tooltip("For throwing a switch: its channel. For reaching a zone: the zone's symbol in the level's zone map.")]
        private char target;

        public ObjectiveDefinition(ObjectiveKind kind, string title, AccessKey key = null, char target = '\0')
        {
            this.kind = kind;
            this.title = title;
            this.key = key;
            this.target = target;
        }

        public ObjectiveKind Kind => kind;
        public string Title => title;
        public AccessKey Key => key;
        public char Target => target;
    }
}
