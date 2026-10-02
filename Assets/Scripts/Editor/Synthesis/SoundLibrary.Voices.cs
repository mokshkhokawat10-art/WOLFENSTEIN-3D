using System;

namespace Wolfenstein.Editor.Synthesis
{
    // Voices: a buzzing source shaped by the resonances of a throat and mouth. The same few ideas make a soldier's grunt and a
    // monster's growl; what differs is the size of the throat, the pitch, and how rough and breathy the source is.
    internal static partial class SoundLibrary
    {
        // The first four resonances of an adult throat for each vowel, in hertz.
        private static readonly double[] VowelA = { 730, 1090, 2440, 3400 };
        private static readonly double[] VowelO = { 570, 840, 2410, 3300 };
        private static readonly double[] VowelU = { 300, 870, 2240, 3200 };
        private static readonly double[] VowelE = { 530, 1840, 2480, 3500 };
        private static readonly double[] VowelI = { 280, 2250, 2900, 3700 };
        private static readonly double[] VowelUh = { 640, 1190, 2390, 3300 };

        private static readonly double[] FormantWidths = { 90, 110, 170, 260 };
        private static readonly double[] FormantLevels = { 1, 0.55, 0.25, 0.12 };

        /// <summary>
        /// Shapes a source with the resonances of a throat gliding from one vowel to another between two times. A tract
        /// scale below 1 is a bigger throat, with lower resonances; wider resonances sound rougher and less human.
        /// </summary>
        private static Sound Vocal(Sound source, double[] from, double[] to, double glideStart, double glideEnd, double tract,
            double widthScale = 1)
        {
            var centres = new Func<double, double>[4];
            var widths = new double[4];
            for (int i = 0; i < 4; i++)
            {
                int k = i;
                double span = Math.Max(0.001, glideEnd - glideStart);
                centres[k] = t => tract * (from[k] + (to[k] - from[k]) * Env.Smooth((t - glideStart) / span));
                widths[k] = FormantWidths[k] * widthScale * Math.Max(0.6, tract);
            }

            return source.Formants(centres, widths, FormantLevels);
        }

        /// <summary>
        /// A creature's voice: a rough glottal source, fluttering at a rate set by how rough it is, shaped by a throat and
        /// driven for grit. Roughness from 0 (smooth) to 1 (a snarl); the subharmonic doubles the growl an octave down.
        /// </summary>
        private static Sound Growl(Studio s, double seconds, Func<double, double> pitch, double[] from, double[] to, double tract,
            double roughness, double subharmonic, double breath, double drive)
        {
            Sound source = s.Glottal(seconds, pitch, 0.02 + roughness * 0.06, 0.12 + roughness * 0.35, subharmonic, breath);
            source.Tremolo(Env.Wander(s.Rng, 0.3, 6, seconds).Compose(x => 24 + 22 * roughness * x), roughness * 0.55);
            Sound voice = Vocal(source, from, to, 0, seconds, tract, 1 + roughness * 0.8);

            // A bigger throat is a darker one: the grit drive adds is kept below a cutoff that falls with its size.
            return voice.Drive(drive).LowPass(4800 * tract, 0.6);
        }

        /// <summary>A breathy hiss through a throat, such as a snarl's intake or a creature's exhale.</summary>
        private static Sound Breath(Studio s, double seconds, double[] vowel, double tract, Func<double, double> envelope)
        {
            Sound noise = s.White(seconds).Shape(envelope);
            Sound breath = Vocal(noise, vowel, vowel, 0, 1, tract, 2.5).Mix(s.White(seconds).BandPass(3500 * tract, 1).Shape(envelope), 0, 0.12);
            return breath.LowPass(5000 * tract, 0.6);
        }
    }

    internal static class CurveExtensions
    {
        /// <summary>Feeds one curve's value through a function, making another curve.</summary>
        public static Func<double, double> Compose(this Func<double, double> curve, Func<double, double> then) => t => then(curve(t));
    }
}
