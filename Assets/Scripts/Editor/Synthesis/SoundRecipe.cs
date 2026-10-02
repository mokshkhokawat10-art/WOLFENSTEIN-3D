using System;

namespace Wolfenstein.Editor.Synthesis
{
    internal enum SoundKind
    {
        /// <summary>A short one-shot, at full rate for crisp transients.</summary>
        Effect,

        /// <summary>A long seamless loop of ambience, at half rate, as it holds little above 10 kHz.</summary>
        Loop,

        /// <summary>A placeholder piece of music.</summary>
        Music
    }

    /// <summary>
    /// How to make one clip: where it goes, what kind of sound it is, and the recipe that builds it. Each recipe draws its
    /// randomness from a seed of its own path, so a clip comes out the same every time it is generated.
    /// </summary>
    internal sealed class SoundRecipe
    {
        private readonly Func<Studio, Sound> build;

        public SoundRecipe(string path, SoundKind kind, Func<Studio, Sound> build)
        {
            Path = path;
            Kind = kind;
            this.build = build;
        }

        /// <summary>Where the clip goes under the generated clip folder, without its extension, such as "Weapons/PistolShot_1".</summary>
        public string Path { get; }

        public SoundKind Kind { get; }

        public int SampleRate => Kind switch
        {
            SoundKind.Effect => 44100,
            SoundKind.Loop => 22050,
            _ => 32000
        };

        /// <summary>
        /// Builds the clip and finishes it: an effect has any offset removed and its ends faded so it never clicks on or
        /// off, while loops and music are left as their recipes made them, seamless. Then every clip is brought to a standard
        /// peak, so its cue alone decides how loud it plays.
        /// </summary>
        public Sound Build()
        {
            Sound sound = build(new Studio(SampleRate, new Rng(Rng.Seed(Path))));
            if (Kind == SoundKind.Effect)
                sound.DcBlock().Fade(0.0005, 0.01);
            return sound.Normalize(Kind == SoundKind.Loop ? 0.7 : 0.89);
        }
    }
}
