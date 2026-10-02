using System;
using UnityEngine;
using Wolfenstein.Interaction;

namespace Wolfenstein.World
{
    /// <summary>
    /// A heavy lever on a wall, thrown once by using it. Throwing it flips the lever and turns its lamp from red to green;
    /// what it powers is up to the level, which listens for the switch's channel.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WallSwitch : MonoBehaviour, IInteractable
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField, Tooltip("What the level listens for: doors and encounters name the channel of the switch that sets them off.")]
        private char channel = 'Z';

        [SerializeField, Tooltip("Turns to its thrown rotation when the switch is thrown.")]
        private Transform lever;

        [SerializeField] private Vector3 thrownAngles = new Vector3(-100f, 0f, 0f);

        [SerializeField, Tooltip("Glows red until the switch is thrown, then green.")]
        private Renderer lamp;

        [SerializeField] private Light lampLight;
        [SerializeField, ColorUsage(false, true)] private Color offColour = new Color(3f, 0.15f, 0.1f);
        [SerializeField, ColorUsage(false, true)] private Color onColour = new Color(0.3f, 2.5f, 0.5f);

        private MaterialPropertyBlock properties;

        /// <summary>Raised once, when the switch is thrown.</summary>
        public event Action<WallSwitch> Thrown;

        public char Channel => channel;
        public bool IsThrown { get; private set; }

        private void Awake()
        {
            properties = new MaterialPropertyBlock();
            ShowLamp();
        }

        public bool CanInteract(GameObject interactor) => !IsThrown;

        public void Interact(GameObject interactor) => Throw();

        public void Throw()
        {
            if (IsThrown)
                return;

            IsThrown = true;
            if (lever != null)
                lever.localRotation = Quaternion.Euler(thrownAngles) * lever.localRotation;
            ShowLamp();
            Thrown?.Invoke(this);
        }

        private void ShowLamp()
        {
            Color colour = IsThrown ? onColour : offColour;
            if (lamp != null)
            {
                properties.SetColor(BaseColorId, colour);
                lamp.SetPropertyBlock(properties);
            }

            if (lampLight != null)
                lampLight.color = colour / Mathf.Max(colour.maxColorComponent, 0.0001f);
        }
    }
}
