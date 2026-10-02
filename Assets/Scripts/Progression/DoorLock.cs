using System;
using UnityEngine;
using Wolfenstein.Pickups;

namespace Wolfenstein.Progression
{
    /// <summary>
    /// Doors held shut until the player earns their way through, either by carrying a key or by throwing a switch. The
    /// doors are the ones whose tiles carry the lock's marker in the level's decor map.
    /// </summary>
    [Serializable]
    public sealed class DoorLock
    {
        [SerializeField, Tooltip("The decor map symbol on the door tiles this lock holds.")]
        private char marker;

        [SerializeField, Tooltip("What the player calls the door, such as SECURITY DOOR.")]
        private string doorName = "DOOR";

        [SerializeField, Tooltip("Opens it once the player carries this key. Leave empty for a door opened by a switch.")]
        private AccessKey key;

        [SerializeField, Tooltip("Opens it once the switch on this channel is thrown, when no key is set.")]
        private char switchChannel;

        [SerializeField, Tooltip("Why it will not open, shown when the player tries it, such as NEEDS THE SECURITY KEY.")]
        private string lockedMessage = "LOCKED";

        [SerializeField, Tooltip("Shown when it opens, such as SECURITY ACCESS GRANTED. Empty for no message.")]
        private string openedMessage = string.Empty;

        public DoorLock(char marker, string doorName, AccessKey key, char switchChannel, string lockedMessage, string openedMessage)
        {
            this.marker = marker;
            this.doorName = doorName;
            this.key = key;
            this.switchChannel = switchChannel;
            this.lockedMessage = lockedMessage;
            this.openedMessage = openedMessage;
        }

        public char Marker => marker;
        public string DoorName => doorName;
        public AccessKey Key => key;
        public char SwitchChannel => switchChannel;
        public string LockedMessage => lockedMessage;
        public string OpenedMessage => openedMessage;
    }
}
