using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wolfenstein.Pickups
{
    /// <summary>
    /// The keys the player carries. Keys last until the level ends; a restart starts without them.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KeyRing : MonoBehaviour
    {
        private readonly List<AccessKey> keys = new List<AccessKey>();

        /// <summary>Raised when a key the player did not have is added.</summary>
        public event Action<AccessKey> KeyAdded;

        public IReadOnlyList<AccessKey> Keys => keys;

        public bool Has(AccessKey key) => key != null && keys.Contains(key);

        /// <summary>Adds a key, and returns whether the player did not have it already.</summary>
        public bool Add(AccessKey key)
        {
            if (key == null || keys.Contains(key))
                return false;

            keys.Add(key);
            KeyAdded?.Invoke(key);
            return true;
        }
    }
}
