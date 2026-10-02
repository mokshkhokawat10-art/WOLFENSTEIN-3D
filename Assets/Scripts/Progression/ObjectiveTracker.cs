using System;
using System.Collections.Generic;
using UnityEngine;
using Wolfenstein.Pickups;
using Wolfenstein.Waves;
using Wolfenstein.World;

namespace Wolfenstein.Progression
{
    /// <summary>
    /// Works through the level's objectives in order, one at a time. An objective is complete once what it asks for is
    /// true, whenever that came about: a key picked up before the player was told to find it still counts, and so does
    /// the arena beaten by a player who found another way in. Completing one starts the next at once.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ObjectiveTracker : MonoBehaviour
    {
        [SerializeField] private LevelDefinition level;
        [SerializeField] private KeyRing keys;
        [SerializeField] private AccessControl access;
        [SerializeField] private ZoneTracker zones;
        [SerializeField] private WaveManager waves;
        [SerializeField] private LevelExit exit;

        private int index = -1;
        private bool arenaCleared;
        private bool evaluating;

        /// <summary>Raised when an objective becomes the current one.</summary>
        public event Action<ObjectiveDefinition> Started;

        public event Action<ObjectiveDefinition> Completed;

        /// <summary>Raised once the last objective is complete.</summary>
        public event Action AllCompleted;

        public IReadOnlyList<ObjectiveDefinition> Objectives => level.Objectives;

        /// <summary>The objective being worked on, or null before the level starts and once every one is done.</summary>
        public ObjectiveDefinition Current => index >= 0 && index < level.Objectives.Count ? level.Objectives[index] : null;

        public int CompletedCount => Mathf.Clamp(index, 0, level.Objectives.Count);
        public bool HasBegun => index >= 0;
        public bool IsFinished => index >= level.Objectives.Count;

        private void OnEnable()
        {
            keys.KeyAdded += HandleKeyAdded;
            access.SwitchThrown += HandleSwitchThrown;
            zones.ZoneEntered += HandleZoneEntered;
            exit.Reached += Evaluate;
            if (waves != null)
                waves.AreaCleared += HandleAreaCleared;
        }

        private void OnDisable()
        {
            keys.KeyAdded -= HandleKeyAdded;
            access.SwitchThrown -= HandleSwitchThrown;
            zones.ZoneEntered -= HandleZoneEntered;
            exit.Reached -= Evaluate;
            if (waves != null)
                waves.AreaCleared -= HandleAreaCleared;
        }

        /// <summary>Starts the first objective. Called once the level is built and the player placed.</summary>
        public void Begin()
        {
            if (HasBegun)
                return;

            index = 0;
            if (Current == null)
            {
                AllCompleted?.Invoke();
                return;
            }

            Started?.Invoke(Current);
            Evaluate();
        }

        /// <summary>Whether what an objective asks for is true right now.</summary>
        public bool IsSatisfied(ObjectiveDefinition objective) => objective.Kind switch
        {
            ObjectiveKind.CollectKey => keys.Has(objective.Key),
            ObjectiveKind.ThrowSwitch => access.IsSwitchThrown(objective.Target),
            ObjectiveKind.ReachZone => zones.HasVisited(objective.Target),
            ObjectiveKind.ClearArena => arenaCleared || (waves != null && waves.State == WaveState.Cleared),
            ObjectiveKind.ReachExit => exit.IsReached,
            _ => false
        };

        // Completes the current objective while it is satisfied, which can finish several at once.
        private void Evaluate()
        {
            if (evaluating || !HasBegun)
                return;

            evaluating = true;
            try
            {
                while (Current != null && IsSatisfied(Current))
                {
                    ObjectiveDefinition done = Current;
                    index++;
                    Completed?.Invoke(done);
                    if (Current != null)
                        Started?.Invoke(Current);
                    else
                        AllCompleted?.Invoke();
                }
            }
            finally
            {
                evaluating = false;
            }
        }

        private void HandleKeyAdded(AccessKey key) => Evaluate();

        private void HandleSwitchThrown(WallSwitch wallSwitch) => Evaluate();

        private void HandleZoneEntered(char zone) => Evaluate();

        private void HandleAreaCleared()
        {
            arenaCleared = true;
            Evaluate();
        }
    }
}
