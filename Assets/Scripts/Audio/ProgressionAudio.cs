using UnityEngine;
using Wolfenstein.Progression;
using Wolfenstein.UI;
using Wolfenstein.World;

namespace Wolfenstein.Audio
{
    /// <summary>
    /// The sounds of getting through a level: access granted when a lock opens, a switch thrown, an objective completed, the
    /// exit opening, the level complete, each line of the tally, and the way out to the next level, or into the first from
    /// the title. Locked and unlocking doors, key pickups, waves and the player's death already sound through the door,
    /// player and wave audio. It only listens, and plays through the <see cref="AudioManager"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProgressionAudio : MonoBehaviour
    {
        [SerializeField] private AudioManager audioManager;

        [Header("What It Follows")]
        [SerializeField] private LevelManager level;
        [SerializeField] private ObjectiveTracker objectives;
        [SerializeField] private AccessControl access;
        [SerializeField] private LevelExit exit;
        [SerializeField] private TallyScreen tally;
        [SerializeField] private TitleScreen title;

        [Header("Sounds")]
        [SerializeField] private SoundCue accessGranted;

        [SerializeField, Tooltip("Heard from the switch itself.")]
        private SoundCue switchThrown;

        [SerializeField] private SoundCue objectiveComplete;
        [SerializeField] private SoundCue exitReady;
        [SerializeField] private SoundCue levelComplete;
        [SerializeField] private SoundCue tallyLine;

        [SerializeField, Tooltip("As the player leaves for the next level, or starts from the title.")]
        private SoundCue levelTransition;

        public SoundCue AccessGrantedSound => accessGranted;
        public SoundCue SwitchThrownSound => switchThrown;
        public SoundCue ObjectiveCompleteSound => objectiveComplete;
        public SoundCue ExitReadySound => exitReady;
        public SoundCue LevelCompleteSound => levelComplete;
        public SoundCue TallyLineSound => tallyLine;
        public SoundCue LevelTransitionSound => levelTransition;

        private void OnEnable()
        {
            if (level != null)
            {
                level.LevelCompleted += HandleLevelCompleted;
                level.LeavingLevel += HandleTransition;
            }

            if (objectives != null)
                objectives.Completed += HandleObjectiveCompleted;

            if (access != null)
            {
                access.AccessGranted += HandleAccessGranted;
                access.SwitchThrown += HandleSwitchThrown;
            }

            if (exit != null)
                exit.Ready += HandleExitReady;
            if (tally != null)
                tally.RowRevealed += HandleTallyLine;
            if (title != null)
                title.GameStarting += HandleTransition;
        }

        private void OnDisable()
        {
            if (level != null)
            {
                level.LevelCompleted -= HandleLevelCompleted;
                level.LeavingLevel -= HandleTransition;
            }

            if (objectives != null)
                objectives.Completed -= HandleObjectiveCompleted;

            if (access != null)
            {
                access.AccessGranted -= HandleAccessGranted;
                access.SwitchThrown -= HandleSwitchThrown;
            }

            if (exit != null)
                exit.Ready -= HandleExitReady;
            if (tally != null)
                tally.RowRevealed -= HandleTallyLine;
            if (title != null)
                title.GameStarting -= HandleTransition;
        }

        private void HandleAccessGranted(DoorLock doorLock) => audioManager.Play(accessGranted);

        private void HandleSwitchThrown(WallSwitch wallSwitch) => audioManager.Play(switchThrown, wallSwitch.transform.position);

        // Reaching the exit is heard as the level completing.
        private void HandleObjectiveCompleted(ObjectiveDefinition objective)
        {
            if (objective.Kind != ObjectiveKind.ReachExit)
                audioManager.Play(objectiveComplete);
        }

        private void HandleExitReady() => audioManager.Play(exitReady);

        private void HandleLevelCompleted() => audioManager.Play(levelComplete);

        private void HandleTallyLine() => audioManager.Play(tallyLine);

        private void HandleTransition() => audioManager.Play(levelTransition);
    }
}
