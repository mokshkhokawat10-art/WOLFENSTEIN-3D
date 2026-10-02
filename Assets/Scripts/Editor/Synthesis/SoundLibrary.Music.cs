using System;
using System.Collections.Generic;

namespace Wolfenstein.Editor.Synthesis
{
    // Placeholder music. These are loops sequenced from the same synthesis as the effects, so the music system has something
    // to play and crossfade until real tracks are written: they are not composed music, and every clip is named as a
    // placeholder so that is never forgotten. All are in D minor. Each is written out on a timeline and wrapped round its
    // loop length, so notes ringing past the end carry into the start and the loop has no seam.
    internal static partial class SoundLibrary
    {
        // Semitones above D of each chord's root: D minor, B flat, G minor, A (i, VI, iv, V).
        private static readonly int[] ExplorationRoots = { 0, -4, 5, 7 };
        private static readonly bool[] ExplorationMinor = { true, false, true, false };

        private static void AddMusic(List<SoundRecipe> recipes)
        {
            Add(recipes, "Music/Placeholder_Exploration", SoundKind.Music, ExplorationMusic);
            Add(recipes, "Music/Placeholder_Combat", SoundKind.Music, s => CombatMusic(s, false));
            Add(recipes, "Music/Placeholder_Intense", SoundKind.Music, s => CombatMusic(s, true));
            Add(recipes, "Music/Placeholder_Victory", SoundKind.Music, VictoryMusic);
            Add(recipes, "Music/Placeholder_Menu", SoundKind.Music, MenuMusic);
        }

        /// <summary>The pitch of a note a number of semitones above D3.</summary>
        private static double Note(double semitones) => D3 * Math.Pow(2, semitones / 12);

        // Slow, dark pads moving through i-VI-iv-V over a low drone, with a bell now and then.
        private static Sound ExplorationMusic(Studio s)
        {
            const double chordLength = 8, loop = chordLength * 4;
            Sound music = s.Silence(loop + 6);
            for (int chord = 0; chord < 4; chord++)
            {
                int root = ExplorationRoots[chord];
                double time = chord * chordLength;
                double third = ExplorationMinor[chord] ? 3 : 4;
                music.Mix(Pad(s, new[] { Note(root), Note(root + third), Note(root + 7) }, chordLength + 3), time, 0.5);
                music.Mix(s.Tone(chordLength + 3, Note(root - 24)).Shape(Env.Swell(2, chordLength - 1, 2.5)), time, 0.35);
            }

            double[] bells = { 12, 15, 19, 22, 17, 12, 24, 19 };
            for (int i = 0; i < bells.Length; i++)
                music.Mix(Bell(s, Note(bells[i]), 3, 1.2).LowPass(4000), 3 + i * 4 + s.Rng.Range(-0.4, 0.4), 0.12);

            return music.Echo(0.37, 0.35, 0.3, 1.5, 0.5).Room(1, 0.7, 0.3, damping: 0.5, tail: 1).Fold(loop);
        }

        // Driving drums and an eighth-note bass line. The intense version is faster and heavier: double kicks, a distorted
        // bass, sixteenth-note hats and a dissonant stab on every bar.
        private static Sound CombatMusic(Studio s, bool intense)
        {
            double tempo = intense ? 150 : 130;
            double eighth = 60 / tempo / 2;
            double bar = eighth * 8;
            double loop = bar * 8;
            int[] roots = intense ? new[] { 0, 0, 1, 0, 0, 0, -2, 1 } : new[] { 0, 0, -4, -2, 0, 0, -4, -5 };
            int[] riff = intense ? new[] { 0, 0, 12, 0, 1, 0, 6, 7 } : new[] { 0, 0, 12, 0, 3, 0, 10, 7 };

            Sound drums = s.Silence(loop + 2);
            Sound bass = s.Silence(loop + 2);
            Sound stabs = s.Silence(loop + 2);
            for (int b = 0; b < 8; b++)
            {
                double barStart = b * bar;
                for (int step = 0; step < 8; step++)
                {
                    double time = barStart + step * eighth;
                    bool kick = step == 0 || step == 4 || (step == 3 && b % 2 == 1) || (intense && (step == 5 || step == 7));
                    if (kick)
                        drums.Mix(Kick(s), time, 0.9);
                    if (step == 2 || step == 6)
                        drums.Mix(Snare(s), time, 0.7);
                    drums.Mix(Hat(s), time, step % 2 == 0 ? 0.25 : 0.15);
                    if (intense)
                        drums.Mix(Hat(s), time + eighth * 0.5, 0.1);

                    bass.Mix(BassNote(s, Note(roots[b] + riff[step] - 24), eighth * 0.9, intense), time, 0.8);
                }

                if (intense || b % 2 == 0)
                {
                    int root = roots[b];
                    double[] chord = intense
                        ? new[] { Note(root), Note(root + 6), Note(root + 12) }
                        : new[] { Note(root), Note(root + 7), Note(root + 12) };
                    foreach (double note in chord)
                        stabs.Mix(Brass(s, note, bar * 0.45, intense ? 4 : 3), barStart, 0.22);
                }
            }

            // A tom fill into the top of the loop.
            for (int i = 0; i < 4; i++)
                drums.Mix(Thud(s, 0.35, 190 - i * 25, 120 - i * 15, 0.12).Mix(s.Pink(0.1).LowPass(1500).Shape(Env.Hit(0.001, 0.03)), 0, 0.4),
                    loop - bar * 0.5 + i * eighth, 0.6);

            Sound music = s.Silence(loop + 2);
            music.Mix(drums.Room(0.5, 0.3, 0.12, damping: 0.5, tail: 0.3), 0, 1);
            music.Mix(bass.Drive(intense ? 3 : 1.6).LowPass(intense ? 1400 : 900), 0, 0.7);
            music.Mix(stabs.Room(0.8, 0.5, 0.25, damping: 0.5, tail: 0.5), 0, 0.8);
            if (intense)
                music.Mix(s.Chorus(loop + 2, _ => Note(24), Wave.Saw, 2, 6).Mix(s.Chorus(loop + 2, _ => Note(25), Wave.Saw, 2, 6), 0, 1).LowPass(1800), 0, 0.03);
            return music.Fold(loop);
        }

        // A short brass fanfare over drums, played once when the arena is cleared.
        private static Sound VictoryMusic(Studio s)
        {
            Sound music = s.Silence(5.5);
            double[] run = { Note(12), Note(16), Note(19) };
            for (int i = 0; i < run.Length; i++)
            {
                music.Mix(Brass(s, run[i], 0.22, 3), i * 0.16, 0.5);
                music.Mix(Drum(s, 1), i * 0.16, 0.4);
            }

            foreach (double note in new[] { Note(12), Note(16), Note(19), Note(24) })
                music.Mix(Brass(s, note, 3, 2.5), 0.5, 0.3);
            music.Mix(Drum(s, 1.6), 0.5, 0.9);
            music.Mix(Toll(s, Note(24), 3), 0.5, 0.25);
            return music.Room(1, 0.6, 0.25, damping: 0.45, tail: 0.5).Resize(5.5).Fade(0, 1.5);
        }

        // For a menu: a slow, ominous pad with a low choir and a distant toll.
        private static Sound MenuMusic(Studio s)
        {
            const double loop = 24;
            Sound music = s.Silence(loop + 6);
            music.Mix(Pad(s, new[] { Note(0), Note(3), Note(7) }, 15), 0, 0.4);
            music.Mix(Pad(s, new[] { Note(-2), Note(2), Note(5) }, 15), 12, 0.4);
            music.Mix(Choir(s, new[] { Note(-12), Note(-5) }, 12), 0, 0.5);
            music.Mix(Choir(s, new[] { Note(-14), Note(-7) }, 12), 12, 0.5);
            for (int i = 0; i < 4; i++)
                music.Mix(Toll(s, Note(-12), 4), i * 6 + 1, 0.15);
            return music.Room(1.1, 0.75, 0.35, damping: 0.5, tail: 1.5).Fold(loop);
        }

        /// <summary>A chord of detuned saws, filtered dark and slowly breathing, swelling in and out over its length.</summary>
        private static Sound Pad(Studio s, double[] notes, double seconds)
        {
            Sound pad = s.Silence(seconds);
            foreach (double note in notes)
                pad.Mix(s.Chorus(seconds, _ => note, Wave.Saw, 3, 9), 0, 1.0 / notes.Length);
            return pad.LowPass(t => 650 + 250 * Math.Sin(2 * Math.PI * t / 6), 0.8).Shape(Env.Swell(2.5, seconds - 5.5, 3));
        }

        private static Sound Kick(Studio s)
        {
            Sound kick = Thud(s, 0.35, 150, 45, 0.09);
            kick.Mix(s.White(0.01).LowPass(3000).Shape(Env.Hit(0.0005, 0.002)), 0, 0.4);
            return kick.Drive(1.5);
        }

        private static Sound Snare(Studio s)
        {
            Sound snare = s.White(0.3).BandPass(1800, 0.8).Shape(Env.Hit(0.001, 0.06));
            snare.Mix(s.Tone(0.15, 190).Shape(Env.Hit(0.001, 0.04)), 0, 0.5);
            return snare;
        }

        private static Sound Hat(Studio s) => s.White(0.08).HighPass(7000).Shape(Env.Hit(0.0005, 0.015));

        private static Sound BassNote(Studio s, double frequency, double seconds, bool heavy)
        {
            Sound note = s.Tone(seconds, frequency, Wave.Saw).Mix(s.Tone(seconds, frequency * 0.5, Wave.Square, 0.5), 0, heavy ? 0.5 : 0.3);
            return note.LowPass(Env.Glide(0, heavy ? 2200 : 1400, seconds, 300)).Shape(Env.Swell(0.004, seconds * 0.5, seconds * 0.45));
        }
    }
}
