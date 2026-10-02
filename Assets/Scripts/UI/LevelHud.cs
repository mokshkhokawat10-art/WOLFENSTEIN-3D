using UnityEngine;
using UnityEngine.UI;
using Wolfenstein.Progression;
using Wolfenstein.World;

namespace Wolfenstein.UI
{
    /// <summary>
    /// The level's part of the heads-up display: the current objective in a small panel at the top left, marked complete
    /// for a moment before the next one takes its place, and a line of level events across the lower middle of the screen:
    /// the level's name as it starts, a locked door and why, access granted, the exit locked and then ready, and the level
    /// complete. In a scene with no objectives it stays hidden. It only listens.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelHud : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private LevelManager level;
        [SerializeField] private ObjectiveTracker objectives;
        [SerializeField] private AccessControl access;
        [SerializeField] private LevelExit exit;

        [Header("Objective")]
        [SerializeField] private GameObject objectivePanel;
        [SerializeField] private Text objectiveCaption;
        [SerializeField] private Text objectiveText;

        [Header("Messages")]
        [SerializeField] private GameObject message;
        [SerializeField] private CanvasGroup messageGroup;
        [SerializeField] private Text messageTitle;
        [SerializeField] private Text messageSubtitle;

        [Header("Colours")]
        [SerializeField] private Color objectiveColour = Color.white;
        [SerializeField] private Color captionColour = new Color(0.62f, 0.7f, 1f);
        [SerializeField] private Color completeColour = new Color(1f, 0.82f, 0.3f);
        [SerializeField] private Color deniedColour = new Color(1f, 0.35f, 0.25f);
        [SerializeField] private Color grantedColour = new Color(0.45f, 1f, 0.5f);

        [Header("Timing")]
        [SerializeField, Min(0.1f), Tooltip("Seconds a completed objective is shown as complete before the next one appears.")]
        private float completeHold = 2f;

        [SerializeField, Min(0.1f), Tooltip("Seconds a message stays up before it starts to fade.")]
        private float messageHold = 2.5f;

        [SerializeField, Min(0.01f)] private float messageFade = 0.5f;

        private ObjectiveDefinition nextObjective;
        private bool allComplete;
        private float completeUntil = float.NegativeInfinity;
        private float messageShownTime = float.NegativeInfinity;
        private float messageDuration;

        public bool IsObjectiveVisible => objectivePanel.activeSelf;
        public string ObjectiveCaption => objectiveCaption.text;
        public string ObjectiveReadout => objectiveText.text;
        public bool IsMessageVisible => message.activeSelf && messageGroup.alpha > 0f;
        public string MessageTitle => messageTitle.text;
        public string MessageSubtitle => messageSubtitle.text;

        private void OnEnable()
        {
            if (objectives != null)
            {
                objectives.Started += HandleObjectiveStarted;
                objectives.Completed += HandleObjectiveCompleted;
                objectives.AllCompleted += HandleAllCompleted;
            }

            if (access != null)
            {
                access.AccessDenied += HandleAccessDenied;
                access.AccessGranted += HandleAccessGranted;
            }

            if (exit != null)
            {
                exit.Denied += HandleExitDenied;
                exit.Ready += HandleExitReady;
            }

            if (level != null)
                level.LevelCompleted += HandleLevelCompleted;
        }

        private void OnDisable()
        {
            if (objectives != null)
            {
                objectives.Started -= HandleObjectiveStarted;
                objectives.Completed -= HandleObjectiveCompleted;
                objectives.AllCompleted -= HandleAllCompleted;
            }

            if (access != null)
            {
                access.AccessDenied -= HandleAccessDenied;
                access.AccessGranted -= HandleAccessGranted;
            }

            if (exit != null)
            {
                exit.Denied -= HandleExitDenied;
                exit.Ready -= HandleExitReady;
            }

            if (level != null)
                level.LevelCompleted -= HandleLevelCompleted;
        }

        private void Start()
        {
            if (objectives == null)
                objectivePanel.SetActive(false);
            else if (objectives.Current != null && completeUntil < Time.time)
                ShowObjective(objectives.Current);

            if (messageDuration <= 0f)
                message.SetActive(false);

            LevelDefinition definition = level != null ? level.Level : null;
            if (definition != null)
                ShowMessage(definition.Title, definition.DisplayName, completeColour, messageHold + 1f);
        }

        // Only works while something is showing: a completed objective waiting for the next, or a message fading.
        private void Update()
        {
            if (completeUntil > float.NegativeInfinity && Time.time >= completeUntil)
            {
                completeUntil = float.NegativeInfinity;
                if (nextObjective != null)
                    ShowObjective(nextObjective);
                else if (allComplete)
                    objectivePanel.SetActive(false);
                nextObjective = null;
            }

            if (!message.activeSelf)
                return;

            float age = Time.time - messageShownTime;
            messageGroup.alpha = 1f - Mathf.Clamp01((age - messageDuration) / messageFade);
            if (messageGroup.alpha <= 0f)
                message.SetActive(false);
        }

        private void HandleObjectiveStarted(ObjectiveDefinition objective)
        {
            if (Time.time < completeUntil)
                nextObjective = objective;
            else
                ShowObjective(objective);
        }

        private void HandleObjectiveCompleted(ObjectiveDefinition objective)
        {
            // Reaching the exit ends the level, which says so itself.
            if (objective.Kind == ObjectiveKind.ReachExit)
                return;

            objectivePanel.SetActive(true);
            objectiveCaption.text = "OBJECTIVE COMPLETE";
            objectiveCaption.color = completeColour;
            objectiveText.text = objective.Title;
            objectiveText.color = completeColour;
            completeUntil = Time.time + completeHold;
            nextObjective = null;
        }

        private void HandleAllCompleted()
        {
            allComplete = true;
            if (Time.time >= completeUntil)
                objectivePanel.SetActive(false);
        }

        private void ShowObjective(ObjectiveDefinition objective)
        {
            objectivePanel.SetActive(true);
            objectiveCaption.text = "OBJECTIVE";
            objectiveCaption.color = captionColour;
            objectiveText.text = objective.Title;
            objectiveText.color = objectiveColour;
        }

        private void HandleAccessDenied(DoorLock doorLock) => ShowMessage(doorLock.DoorName, doorLock.LockedMessage, deniedColour, messageHold);

        private void HandleAccessGranted(DoorLock doorLock)
        {
            if (!string.IsNullOrEmpty(doorLock.OpenedMessage))
                ShowMessage(doorLock.OpenedMessage, doorLock.DoorName + " OPEN", grantedColour, messageHold);
        }

        private void HandleExitDenied() => ShowMessage("EXIT", "LOCKED", deniedColour, messageHold);

        private void HandleExitReady() => ShowMessage("EXIT", "READY", grantedColour, messageHold + 1f);

        private void HandleLevelCompleted()
        {
            objectivePanel.SetActive(false);
            ShowMessage("LEVEL COMPLETE", level.Level != null ? level.Level.DisplayName : string.Empty, completeColour, level.CompleteDuration);
        }

        private void ShowMessage(string title, string subtitle, Color colour, float duration)
        {
            messageTitle.text = title;
            messageTitle.color = colour;
            messageSubtitle.text = subtitle;
            messageShownTime = Time.time;
            messageDuration = duration;
            messageGroup.alpha = 1f;
            message.SetActive(true);
        }
    }
}
