using UnityEngine;

namespace Wolfenstein.World
{
    /// <summary>
    /// Signal lamps beside a door that can be locked: red while it is locked, green while it is free to open.
    /// It is purely visual and only listens to the <see cref="Door"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DoorLockLights : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Door door;

        [SerializeField, Tooltip("Glowing lamp models, drawn with an unlit material.")]
        private Renderer[] lamps = System.Array.Empty<Renderer>();

        [SerializeField, Tooltip("Lights that take the lamps' colour.")]
        private Light[] lights = System.Array.Empty<Light>();

        [SerializeField, ColorUsage(false, true)] private Color lockedColour = new Color(3f, 0.15f, 0.1f);
        [SerializeField, ColorUsage(false, true)] private Color unlockedColour = new Color(0.3f, 2.5f, 0.5f);

        private MaterialPropertyBlock properties;

        private void Awake() => properties = new MaterialPropertyBlock();

        private void OnEnable()
        {
            door.LockChanged += Show;
            Show(door.IsLocked);
        }

        private void OnDisable() => door.LockChanged -= Show;

        private void Show(bool locked)
        {
            Color colour = locked ? lockedColour : unlockedColour;
            properties.SetColor(BaseColorId, colour);
            foreach (Renderer lamp in lamps)
                lamp.SetPropertyBlock(properties);

            // Lights take the colour without its HDR brightness, which their intensity already covers.
            Color lightColour = colour / Mathf.Max(colour.maxColorComponent, 0.0001f);
            foreach (Light lamp in lights)
                lamp.color = lightColour;
        }
    }
}
