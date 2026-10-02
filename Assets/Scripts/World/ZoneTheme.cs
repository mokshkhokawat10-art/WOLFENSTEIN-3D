using System;
using System.Collections.Generic;
using UnityEngine;
using Wolfenstein.Audio;

namespace Wolfenstein.World
{
    /// <summary>
    /// The look and sound of one kind of room or corridor: its walls, floor and ceiling, its doors and lamps, the trim built
    /// along its walls, the fixtures hung on them, what its surfaces are made of and the ambience that fills it. The level's
    /// zone map says which tiles take which theme, so rooms can differ without any change to the tile map that gameplay
    /// reads. Nothing here changes how the game plays.
    /// </summary>
    [CreateAssetMenu(fileName = "ZoneTheme", menuName = "Wolfenstein/Zone Theme")]
    public sealed class ZoneTheme : ScriptableObject
    {
        [Header("Surfaces")]
        [SerializeField, Tooltip("The faces of walls looking into the zone. Empty keeps each wall's usual material.")]
        private Material wallMaterial;

        [SerializeField, Tooltip("Floor of the zone's tiles. Empty keeps the level's usual floor.")]
        private Material floorMaterial;

        [SerializeField, Tooltip("Ceiling over the zone's tiles. Empty keeps the level's usual ceiling.")]
        private Material ceilingMaterial;

        [SerializeField, Tooltip("Placed on the zone's door tiles instead of the level's usual door. Optional.")]
        private Door doorPrefab;

        [Header("Lighting")]
        [SerializeField, Tooltip("Hung from the ceiling to light the zone. Empty uses the level's usual lamp.")]
        private GameObject lampPrefab;

        [SerializeField, Min(1f), Tooltip("Tiles of floor each lamp lights. Fewer tiles give more lamps.")]
        private float tilesPerLamp = 10f;

        [SerializeField, Min(1), Tooltip("No floor tile is more than this many steps from a lamp.")]
        private int maxStepsToLamp = 3;

        [Header("Skirting")]
        [SerializeField, Tooltip("A strip along the foot of every wall. Empty for none.")]
        private Material skirtingMaterial;

        [SerializeField, Min(0.01f)] private float skirtingHeight = 0.3f;
        [SerializeField, Min(0.01f)] private float skirtingDepth = 0.06f;

        [Header("Cornice")]
        [SerializeField, Tooltip("A band along the top of every wall. Empty for none.")]
        private Material corniceMaterial;

        [SerializeField, Min(0.01f)] private float corniceHeight = 0.22f;
        [SerializeField, Min(0.01f)] private float corniceDepth = 0.1f;

        [Header("Pilasters")]
        [SerializeField, Tooltip("Upright columns set against the walls at regular intervals. Empty for none.")]
        private Material pilasterMaterial;

        [SerializeField, Min(0.05f)] private float pilasterWidth = 0.45f;
        [SerializeField, Min(0.01f)] private float pilasterDepth = 0.16f;

        [SerializeField, Min(1), Tooltip("A pilaster stands at every this many tile edges along a straight wall.")]
        private int pilasterSpacing = 2;

        [Header("Pipes")]
        [SerializeField, Tooltip("Pipes running along the walls. Empty for none.")]
        private Material pipeMaterial;

        [SerializeField, Range(0, 4)] private int pipeCount = 2;
        [SerializeField, Min(0.01f)] private float pipeRadius = 0.07f;

        [SerializeField, Min(0f), Tooltip("Height of the topmost pipe's centre, in metres.")]
        private float pipeHeight = 2.55f;

        [Header("Ceiling Beams")]
        [SerializeField, Tooltip("Beams across the ceiling. Empty for none.")]
        private Material beamMaterial;

        [SerializeField, Min(0.05f)] private float beamWidth = 0.3f;
        [SerializeField, Min(0.05f)] private float beamDepth = 0.28f;

        [SerializeField, Min(1), Tooltip("A beam crosses the ceiling at every this many tile edges.")]
        private int beamSpacing = 1;

        [SerializeField, Tooltip("Beams run north-south when set, and east-west otherwise.")]
        private bool beamsRunNorthSouth;

        [Header("Wall Fixtures")]
        [SerializeField, Tooltip("Mounted on the walls at intervals, such as sconces or vents. Its back is its origin and it faces +Z.")]
        private GameObject wallFixturePrefab;

        [SerializeField, Min(1), Tooltip("About one wall face in this many gets a fixture.")]
        private int wallFixtureSpacing = 4;

        [SerializeField, Min(0f)] private float wallFixtureHeight = 1.9f;

        [Header("Sound")]
        [SerializeField, Tooltip("What the floor is made of, which sets the sound of footsteps.")]
        private SurfaceKind floorSurface = SurfaceKind.Stone;

        [SerializeField, Tooltip("What the walls are made of, which sets the sound of shots striking them.")]
        private SurfaceKind wallSurface = SurfaceKind.Stone;

        [SerializeField, Tooltip("The looping background of the zone. Empty uses the level's usual ambience.")]
        private SoundCue ambience;

        [SerializeField, Range(0f, 1f), Tooltip("How loud the ambience plays here, as a fraction of the cue's volume.")]
        private float ambienceVolume = 1f;

        [SerializeField, Tooltip("Heard now and then from somewhere nearby. Empty uses the level's usual distant sounds.")]
        private SoundCue[] distantSounds = Array.Empty<SoundCue>();

        public Material WallMaterial => wallMaterial;
        public Material FloorMaterial => floorMaterial;
        public Material CeilingMaterial => ceilingMaterial;
        public Door DoorPrefab => doorPrefab;
        public GameObject LampPrefab => lampPrefab;
        public float TilesPerLamp => tilesPerLamp;
        public int MaxStepsToLamp => maxStepsToLamp;

        public Material SkirtingMaterial => skirtingMaterial;
        public float SkirtingHeight => skirtingHeight;
        public float SkirtingDepth => skirtingDepth;

        public Material CorniceMaterial => corniceMaterial;
        public float CorniceHeight => corniceHeight;
        public float CorniceDepth => corniceDepth;

        public Material PilasterMaterial => pilasterMaterial;
        public float PilasterWidth => pilasterWidth;
        public float PilasterDepth => pilasterDepth;
        public int PilasterSpacing => pilasterSpacing;

        public Material PipeMaterial => pipeMaterial;
        public int PipeCount => pipeCount;
        public float PipeRadius => pipeRadius;
        public float PipeHeight => pipeHeight;

        public Material BeamMaterial => beamMaterial;
        public float BeamWidth => beamWidth;
        public float BeamDepth => beamDepth;
        public int BeamSpacing => beamSpacing;
        public bool BeamsRunNorthSouth => beamsRunNorthSouth;

        public GameObject WallFixturePrefab => wallFixturePrefab;
        public int WallFixtureSpacing => wallFixtureSpacing;
        public float WallFixtureHeight => wallFixtureHeight;

        public SurfaceKind FloorSurface => floorSurface;
        public SurfaceKind WallSurface => wallSurface;
        public SoundCue Ambience => ambience;
        public float AmbienceVolume => ambienceVolume;
        public IReadOnlyList<SoundCue> DistantSounds => distantSounds;
    }
}
