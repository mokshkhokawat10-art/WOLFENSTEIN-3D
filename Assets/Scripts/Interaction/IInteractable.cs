using UnityEngine;

namespace Wolfenstein.Interaction
{
    /// <summary>
    /// Something the player can use by looking at it and pressing Interact.
    /// Put it on a component whose own collider, or a child collider, can be hit by the interaction ray.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// Whether the object accepts interaction right now, for example false for a locked door.
        /// </summary>
        bool CanInteract(GameObject interactor);

        void Interact(GameObject interactor);
    }
}
