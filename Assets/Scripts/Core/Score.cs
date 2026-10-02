using System;
using UnityEngine;

namespace Wolfenstein.Core
{
    /// <summary>
    /// The player's points, earned by collecting treasure and killing enemies, and the count of enemies killed.
    /// Both only ever go up.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Score : MonoBehaviour
    {
        /// <summary>Raised with the new total whenever points are added.</summary>
        public event Action<int> Changed;

        /// <summary>Raised with the new count whenever an enemy is killed.</summary>
        public event Action<int> KillsChanged;

        public int Current { get; private set; }

        /// <summary>How many enemies the player has killed.</summary>
        public int Kills { get; private set; }

        /// <summary>Counts a kill and adds the points it is worth.</summary>
        public void AddKill(int points)
        {
            Kills++;
            KillsChanged?.Invoke(Kills);
            Add(points);
        }

        public void Add(int points)
        {
            if (points <= 0)
                return;

            Current += points;
            Changed?.Invoke(Current);
        }
    }
}
