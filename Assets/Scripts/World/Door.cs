using System;
using UnityEngine;
using Wolfenstein.Audio;
using Wolfenstein.Interaction;

namespace Wolfenstein.World
{
    /// <summary>
    /// A sliding door. The panel and its collider slide along <see cref="openOffset"/> while opening,
    /// so the doorway becomes passable as the gap widens. The door never closes while anything solid
    /// is in the doorway; a closing door that finds something in its way opens again.
    /// A trigger collider over the doorway on the door root lets the door be targeted while it is open.
    /// A locked door cannot be opened by anyone: it closes as soon as its doorway is clear and stays shut until unlocked.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Door : MonoBehaviour, IInteractable
    {
        // Keeps the doorway check clear of the walls, floor and ceiling that frame the panel.
        private const float ObstructionInset = 0.05f;

        private static readonly Collider[] OverlapResults = new Collider[8];

        [SerializeField, Tooltip("The moving part of the door. Its position in the prefab is the closed position.")]
        private Transform panel;

        [SerializeField, Tooltip("How far the panel moves from closed to fully open, in door space.")]
        private Vector3 openOffset = new Vector3(3f, 0f, 0f);

        [Header("Timing")]
        [SerializeField, Min(0.01f), Tooltip("Speed of the panel while opening or closing, in metres per second.")]
        private float slideSpeed = 3f;

        [SerializeField]
        private bool closesAutomatically = true;

        [SerializeField, Min(0f), Tooltip("Seconds the door stays fully open before closing by itself.")]
        private float openDuration = 4f;

        [Header("Doorway Safety")]
        [SerializeField, Min(0f), Tooltip("Depth on each side of the panel that must be clear before the door closes, in metres.")]
        private float closingClearance = 0.25f;

        [Header("Sound")]
        [SerializeField, Tooltip("How this kind of door sounds. Empty uses the level's usual door sounds.")]
        private DoorSounds sounds;

        private Vector3 closedPanelPosition;
        private float openAmount;
        private float timeInState;

        /// <summary>Raised with the new setting whenever the door is locked or unlocked.</summary>
        public event Action<bool> LockChanged;

        /// <summary>Raised with the new state whenever the door starts or stops moving.</summary>
        public event Action<DoorState> StateChanged;

        public DoorState State { get; private set; } = DoorState.Closed;

        /// <summary>A locked door refuses to open, and closes as soon as nothing stands in its doorway.</summary>
        public bool IsLocked { get; private set; }

        /// <summary>How far open the door is, from 0 (closed) to 1 (fully open).</summary>
        public float OpenAmount => openAmount;

        public float OpenDuration => openDuration;

        public DoorSounds Sounds => sounds;

        private void Awake() => closedPanelPosition = panel.localPosition;

        private void Update()
        {
            timeInState += Time.deltaTime;

            switch (State)
            {
                case DoorState.Opening:
                    Slide(1f, DoorState.Open);
                    break;

                case DoorState.Open:
                    if (IsLocked || (closesAutomatically && timeInState >= openDuration))
                        Close();
                    break;

                case DoorState.Closing:
                    if (IsDoorwayObstructed())
                        SetState(DoorState.Opening);
                    else
                        Slide(0f, DoorState.Closed);
                    break;
            }
        }

        public void Open()
        {
            if (!IsLocked && (State is DoorState.Closed or DoorState.Closing))
                SetState(DoorState.Opening);
        }

        /// <summary>
        /// Stops the door opening. An open door closes as soon as its doorway is clear, and never on anything in it.
        /// </summary>
        public void Lock()
        {
            if (IsLocked)
                return;

            IsLocked = true;
            Close();
            LockChanged?.Invoke(true);
        }

        public void Unlock()
        {
            if (!IsLocked)
                return;

            IsLocked = false;
            LockChanged?.Invoke(false);
        }

        /// <summary>
        /// Starts closing, unless something is in the doorway. An open door that could not close
        /// keeps trying each frame once its open duration has passed.
        /// </summary>
        public void Close()
        {
            if ((State is DoorState.Open or DoorState.Opening) && !IsDoorwayObstructed())
                SetState(DoorState.Closing);
        }

        public bool CanInteract(GameObject interactor) => !IsLocked;

        public void Interact(GameObject interactor)
        {
            if (State is DoorState.Closed or DoorState.Closing)
                Open();
            else
                Close();
        }

        private void Slide(float targetAmount, DoorState stateWhenReached)
        {
            float step = slideSpeed / openOffset.magnitude * Time.deltaTime;
            openAmount = Mathf.MoveTowards(openAmount, targetAmount, step);
            panel.localPosition = closedPanelPosition + openOffset * openAmount;

            if (openAmount == targetAmount)
                SetState(stateWhenReached);
        }

        private void SetState(DoorState newState)
        {
            State = newState;
            timeInState = 0f;
            StateChanged?.Invoke(newState);
        }

        private bool IsDoorwayObstructed()
        {
            GetDoorwayBox(closedPanelPosition, out Vector3 centre, out Vector3 halfExtents);
            int count = Physics.OverlapBoxNonAlloc(centre, halfExtents, OverlapResults, transform.rotation,
                Physics.AllLayers, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                if (!OverlapResults[i].transform.IsChildOf(transform))
                    return true;
            }

            return false;
        }

        // The space the panel sweeps through while closing, deepened by the clearance on both faces.
        private void GetDoorwayBox(Vector3 panelClosedPosition, out Vector3 centre, out Vector3 halfExtents)
        {
            Vector3 panelHalfSize = panel.localScale * 0.5f;
            centre = transform.TransformPoint(panelClosedPosition);
            halfExtents = new Vector3(
                panelHalfSize.x - ObstructionInset,
                panelHalfSize.y - ObstructionInset,
                panelHalfSize.z + closingClearance);
        }

        private void OnDrawGizmosSelected()
        {
            if (panel == null)
                return;

            Vector3 closedPosition = Application.isPlaying ? closedPanelPosition : panel.localPosition;
            GetDoorwayBox(closedPosition, out Vector3 centre, out Vector3 halfExtents);

            Gizmos.color = Color.yellow;
            Gizmos.matrix = Matrix4x4.TRS(centre, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, halfExtents * 2f);
        }
    }
}
