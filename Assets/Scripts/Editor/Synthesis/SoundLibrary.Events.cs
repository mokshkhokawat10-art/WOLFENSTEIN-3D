using System;
using System.Collections.Generic;

namespace Wolfenstein.Editor.Synthesis
{
    // Events: the arena's wave warnings, each stronger than the last, the cues that mark a wave starting, beaten and the
    // arena cleared, and the interface's death sting and restart prompt.
    internal static partial class SoundLibrary
    {
        // Note pitches, in hertz.
        private const double D2 = 73.42, A2 = 110.0, D3 = 146.83, F3 = 174.61, Gs3 = 207.65, A3 = 220.0;
        private const double D4 = 293.66, Fs4 = 369.99, F4 = 349.23, A4 = 440.0, C5 = 523.25, D5 = 587.33;

        private static void AddEvents(List<SoundRecipe> recipes)
        {
            Add(recipes, "Waves/Warning", s => Klaxon(s, 2, 1));
            Add(recipes, "Waves/WarningMajor", MajorWarning);
            Add(recipes, "Waves/WarningElite", EliteWarning);
            Add(recipes, "Waves/WarningFinal", FinalWarning);
            Add(recipes, "Waves/Incoming", Incoming);
            Add(recipes, "Waves/Complete", WaveComplete);
            Add(recipes, "Waves/AreaCleared", AreaCleared);
            Add(recipes, "Interface/GameOver", GameOver);
            Add(recipes, "Interface/RestartReady", RestartReady);
        }

        // An alarm: rising, nasal wails from a pair of detuned horns, echoing round the arena.
        private static Sound Klaxon(Studio s, int pulses, double pitch)
        {
            const double length = 0.45, gap = 0.17;
            Sound klaxon = s.Silence(pulses * (length + gap) + 0.3);
            for (int i = 0; i < pulses; i++)
            {
                Func<double, double> wail = Env.Glide(0, 300 * pitch, length, 420 * pitch);
                Sound horn = s.Tone(length, wail, Wave.Saw).Mix(s.Tone(length, t => wail(t) * 1.007, Wave.Square, 0.4), 0, 0.6);
                horn = Vocal(horn, VowelA, VowelA, 0, 1, 1.4, 1.5).Shape(Env.Swell(0.02, length - 0.08, 0.06));
                klaxon.Mix(horn, i * (length + gap), 1);
            }

            return klaxon.Drive(1.5).Room(0.9, 0.55, 0.25, damping: 0.45, tail: 0.5);
        }

        // More, lower wails, each driven home by a drum.
        private static Sound MajorWarning(Studio s)
        {
            Sound warning = Klaxon(s, 3, 0.82);
            for (int i = 0; i < 3; i++)
                warning.Mix(Drum(s, 1.2), i * 0.62, 0.8);
            return warning;
        }

        // A war horn sounding low under a gong: something worse is coming.
        private static Sound EliteWarning(Studio s)
        {
            Sound warning = s.Silence(3);
            warning.Mix(Gong(s, 120, 2.4), 0, 0.1);
            warning.Mix(Brass(s, D3, 1.9, 4), 0.05, 0.8);
            warning.Mix(Brass(s, A3, 1.9, 3.5), 0.05, 0.55);
            warning.Mix(Brass(s, D2, 1.9, 2.5), 0.05, 0.5);
            warning.Mix(Klaxon(s, 1, 0.7), 1.85, 0.6);
            return warning.Room(1, 0.6, 0.2, damping: 0.45, tail: 0.3).Resize(3.2);
        }

        // The last wave: a deep bell tolling three times over a dark, low choir, ending in a boom.
        private static Sound FinalWarning(Studio s)
        {
            Sound warning = s.Silence(3.6);
            warning.Mix(Choir(s, new[] { D2, Gs3 * 0.5 }, 3.2), 0, 0.5);
            for (int i = 0; i < 3; i++)
                warning.Mix(Toll(s, 196, 1.6), i * 0.75, 0.8);
            warning.Mix(Drum(s, 2), 2.35, 1);
            return warning.Room(1, 0.65, 0.22, damping: 0.4, tail: 0.4).Resize(3.8);
        }

        // A rush that runs backwards into a hit: here they come.
        private static Sound Incoming(Studio s)
        {
            Sound incoming = s.Silence(1.3);
            incoming.Mix(ReverseSwell(s, 0.5, 1600), 0, 0.8);
            incoming.Mix(Drum(s, 1.5), 0.5, 1);
            incoming.Mix(s.Burst(0.8, 0.003).Body(s.Metal(210, 10, 0.25)).LowPass(3000), 0.5, 0.45);
            return incoming.Room(0.9, 0.5, 0.2, damping: 0.45, tail: 0.3).Resize(1.4);
        }

        // A short rising stab: the wave is beaten.
        private static Sound WaveComplete(Studio s)
        {
            Sound sting = s.Silence(1.4);
            sting.Mix(Stab(s, new[] { D4, F4, A4 }, 0.25), 0, 0.8);
            sting.Mix(Stab(s, new[] { F4, A4, C5 }, 0.5), 0.16, 0.9);
            return sting.Echo(0.16, 0.3, 0.3, 0.3, 0.5).Room(0.7, 0.5, 0.18, damping: 0.5, tail: 0.3).Resize(1.4);
        }

        // Brass climbing a major chord over a drum roll, then holding it: the arena is clear.
        private static Sound AreaCleared(Studio s)
        {
            Sound fanfare = s.Silence(3.2);
            double[] run = { D4, Fs4, A4, D5 };
            for (int i = 0; i < run.Length; i++)
                fanfare.Mix(Brass(s, run[i], 0.3, 3), i * 0.13, 0.6);
            foreach (double note in new[] { D4, Fs4, A4, D5 })
                fanfare.Mix(Brass(s, note, 1.8, 2.5), 0.55, 0.35);
            for (int i = 0; i < 8; i++)
                fanfare.Mix(Drum(s, 1).Gain(0.25 + 0.05 * i), i * 0.07, 0.5);
            fanfare.Mix(Drum(s, 1.4), 0.55, 0.9);
            return fanfare.Room(0.9, 0.55, 0.2, damping: 0.45, tail: 0.4).Resize(3.4);
        }

        // A dark chord sagging downwards over a boom and a single toll.
        private static Sound GameOver(Studio s)
        {
            Sound sting = s.Silence(3.2);
            foreach (double note in new[] { D3, F3, A3 })
                sting.Mix(s.Chorus(2.8, Env.Glide(0, note, 2.8, note * 0.94), Wave.Saw, 3, 10).LowPass(Env.Glide(0, 1800, 2.8, 300)).Shape(Env.Swell(0.02, 1.2, 1.5)), 0, 0.3);
            sting.Mix(Drum(s, 2), 0, 1);
            sting.Mix(Toll(s, 147, 2.5), 0.05, 0.5);
            return sting.Room(1, 0.6, 0.2, damping: 0.45, tail: 0.3).Resize(3.4);
        }

        private static Sound RestartReady(Studio s)
        {
            Sound blip = s.Silence(0.4);
            blip.Mix(s.Tone(0.1, 660).Shape(Env.Hit(0.003, 0.03)), 0, 0.6);
            blip.Mix(s.Tone(0.15, 990).Shape(Env.Hit(0.003, 0.05)), 0.08, 0.6);
            return blip.Echo(0.1, 0.2, 0.2, 0.2, 0.5).Resize(0.4);
        }

        /// <summary>A big drum: a falling boom with the skin's slap on top, larger for a bigger size.</summary>
        private static Sound Drum(Studio s, double size)
        {
            Sound drum = Thud(s, 0.9 * size, 95 / size, 40 / size, 0.16 * size);
            drum.Mix(s.White(0.2).LowPass(1400).Shape(Env.Hit(0.001, 0.03)), 0, 0.5);
            return drum.Drive(1.3);
        }

        /// <summary>A tuned gong: a slow-blooming metal plate, dark and long.</summary>
        private static Sound Gong(Studio s, double fundamental, double seconds) =>
            s.Pink(seconds).Shape(Env.Hit(0.008, 0.012)).Body(s.Metal(fundamental, 12, seconds * 0.35)).LowPass(2500).Resize(seconds).Fade(0.01, 0.3);

        /// <summary>A heavy bell toll with a hum an octave below.</summary>
        private static Sound Toll(Studio s, double frequency, double seconds)
        {
            Sound toll = Bell(s, frequency, seconds, seconds * 0.4);
            toll.Mix(s.Tone(seconds, frequency * 0.5).Shape(Env.Hit(0.005, seconds * 0.45)).Fade(0, seconds * 0.3), 0, 0.6);
            return toll;
        }

        /// <summary>
        /// A brass note in the manner of FM synthesis: the tone brightens as it swells, like a horn opening up, with a slow vibrato.
        /// </summary>
        private static Sound Brass(Studio s, double frequency, double seconds, double brightness)
        {
            Func<double, double> swell = Env.Swell(Math.Min(0.08, seconds * 0.3), seconds * 0.5, seconds * 0.4);
            Sound note = s.Fm(seconds, t => frequency * (1 + 0.006 * Math.Sin(2 * Math.PI * 5.5 * t) * Math.Min(1, t * 3)), 1,
                swell.Compose(x => brightness * x));
            return note.Shape(swell).LowPass(frequency * 12);
        }

        /// <summary>A short synth chord, plucked and filtered.</summary>
        private static Sound Stab(Studio s, double[] notes, double seconds)
        {
            Sound stab = s.Silence(seconds);
            foreach (double note in notes)
                stab.Mix(s.Chorus(seconds, _ => note, Wave.Saw, 2, 8), 0, 1.0 / notes.Length);
            return stab.LowPass(Env.Glide(0, 5000, seconds, 700)).Shape(Env.Hit(0.004, seconds * 0.35));
        }

        /// <summary>Low voices holding "oo" on each note, swelling in and fading.</summary>
        private static Sound Choir(Studio s, double[] notes, double seconds)
        {
            Sound choir = s.Silence(seconds);
            foreach (double note in notes)
            {
                for (int voice = 0; voice < 3; voice++)
                {
                    double detune = 1 + (voice - 1) * 0.006;
                    Sound source = s.Glottal(seconds, t => note * detune * (1 + 0.01 * Math.Sin(2 * Math.PI * (4.5 + voice) * t)), 0.004, 0.03, 0, 0.1);
                    choir.Mix(Vocal(source, VowelU, VowelO, 0, seconds, 0.85), 0, 1.0 / (notes.Length * 3));
                }
            }

            return choir.Shape(Env.Swell(seconds * 0.4, seconds * 0.3, seconds * 0.3));
        }
    }
}
