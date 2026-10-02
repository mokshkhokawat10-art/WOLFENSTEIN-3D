using System;
using System.Collections.Generic;

namespace Wolfenstein.Editor.Synthesis
{
    // Getting through a level: a key picked up, access granted, a switch thrown, an objective done, the exit opening, the
    // level complete, the tally counting up, and the way out to the next level.
    internal static partial class SoundLibrary
    {
        private const double Fs4Note = 369.99, Fs5 = 739.99, A5 = 880.0, D6 = 1174.66;

        private static void AddProgression(List<SoundRecipe> recipes)
        {
            Add(recipes, "Pickups/Key", KeyPickup);
            Add(recipes, "Interface/AccessGranted", AccessGranted);
            Add(recipes, "Interface/ObjectiveComplete", ObjectiveComplete);
            Add(recipes, "Interface/ExitReady", ExitReady);
            Add(recipes, "Interface/LevelComplete", LevelComplete);
            Add(recipes, "Interface/TallyLine", TallyLine);
            Add(recipes, "Interface/LevelTransition", LevelTransition);
            Add(recipes, "Doors/Switch", SwitchThrow);
        }

        // Keys jangling on their ring, and a bright two-note chime: something important.
        private static Sound KeyPickup(Studio s)
        {
            Sound key = s.Silence(0.9);
            key.Mix(s.Clicks(0.3, t => 120 * Math.Exp(-t / 0.1), 0.3).Body(s.Metal(2600, 8, 0.03)).Resize(0.35), 0, 0.5);
            key.Mix(Bell(s, 1319, 0.7, 0.3), 0.04, 0.55);
            key.Mix(Bell(s, 1760, 0.7, 0.35), 0.13, 0.55);
            return key.Echo(0.1, 0.2, 0.2, 0.2, 0.5).Resize(0.9);
        }

        // Two rising beeps and the lock releasing: the way is open.
        private static Sound AccessGranted(Studio s)
        {
            Sound granted = s.Silence(1.1);
            granted.Mix(Chip(s, A5, 0.1, Wave.Square, 0.5).LowPass(4000), 0, 0.5);
            granted.Mix(Chip(s, D6, 0.18, Wave.Square, 0.5).LowPass(4000), 0.1, 0.5);
            granted.Mix(Stab(s, new[] { D4, Fs4Note, A4 }, 0.6), 0.1, 0.45);
            granted.Mix(Thud(s, 0.3, 120, 70, 0.035), 0.22, 0.35);
            return granted.Room(0.5, 0.35, 0.15, damping: 0.5, tail: 0.3).Resize(1.2);
        }

        // Brass rising through a major chord: a job done.
        private static Sound ObjectiveComplete(Studio s)
        {
            Sound done = s.Silence(1.6);
            double[] run = { D4, Fs4Note, A4 };
            for (int i = 0; i < run.Length; i++)
                done.Mix(Brass(s, run[i], 0.18, 3), i * 0.09, 0.55);
            foreach (double note in new[] { D4, A4, D5 })
                done.Mix(Brass(s, note, 0.9, 2.5), 0.27, 0.35);
            return done.Room(0.7, 0.45, 0.18, damping: 0.5, tail: 0.3).Resize(1.6);
        }

        // A lift bell and its machinery waking up: the exit is open.
        private static Sound ExitReady(Studio s)
        {
            Sound ready = s.Silence(2);
            ready.Mix(Toll(s, D6, 1.6), 0, 0.5);
            ready.Mix(Toll(s, A5, 1.6), 0.35, 0.5);
            ready.Mix(s.Tone(1.6, Env.Glide(0, 45, 1.2, 60), Wave.Saw).LowPass(400).Shape(Env.Swell(0.6, 0.4, 0.6)), 0.1, 0.25);
            return ready.Room(0.8, 0.5, 0.2, damping: 0.45, tail: 0.3).Resize(2.2);
        }

        // The fanfare of a level beaten: three quick calls and a long held chord over a drum.
        private static Sound LevelComplete(Studio s)
        {
            Sound fanfare = s.Silence(3.4);
            for (int i = 0; i < 3; i++)
                fanfare.Mix(Brass(s, A4, 0.14, 3.5), i * 0.15, 0.55);
            foreach (double note in new[] { D4, Fs4Note, A4, D5 })
                fanfare.Mix(Brass(s, note, 2.2, 2.8), 0.45, 0.32);
            fanfare.Mix(Brass(s, Fs5, 2.2, 2), 0.45, 0.15);
            fanfare.Mix(Drum(s, 1.5), 0.45, 0.9);
            for (int i = 0; i < 3; i++)
                fanfare.Mix(Drum(s, 0.8), i * 0.15, 0.35);
            return fanfare.Room(0.9, 0.55, 0.2, damping: 0.45, tail: 0.4).Resize(3.6);
        }

        // A crisp counter tick, as each line of the tally comes up.
        private static Sound TallyLine(Studio s)
        {
            Sound tick = s.Silence(0.25);
            tick.Mix(Chip(s, 1760, 0.07, Wave.Square, 0.5).LowPass(5000), 0, 0.6);
            tick.Mix(Chip(s, 2637, 0.09, Wave.Triangle, 0.5), 0.035, 0.4);
            return tick.Echo(0.06, 0.15, 0.15, 0.1, 0.5).Resize(0.25);
        }

        // A lift carrying the player away: a rush running backwards into the drone of its motor.
        private static Sound LevelTransition(Studio s)
        {
            Sound lift = s.Silence(1.6);
            lift.Mix(ReverseSwell(s, 0.5, 1200), 0, 0.6);
            lift.Mix(s.Tone(1.1, Env.Glide(0, 55, 1.1, 38), Wave.Saw).LowPass(350).Shape(Env.Swell(0.05, 0.6, 0.45)), 0.45, 0.6);
            lift.Mix(s.Pink(1.1).BandPass(700, 1).Shape(Env.Swell(0.05, 0.6, 0.45)), 0.45, 0.3);
            lift.Mix(Thud(s, 0.5, 80, 40, 0.1), 0.45, 0.6);
            return lift.Room(0.7, 0.45, 0.18, damping: 0.5, tail: 0.3).Resize(1.8);
        }

        // A heavy lever slammed over, relays clicking, and the power humming up to speed.
        private static Sound SwitchThrow(Studio s)
        {
            Sound power = s.Silence(1.8);
            power.Mix(s.Burst(0.5, 0.003).Body(s.Metal(300, 10, 0.07)), 0, 0.8);
            power.Mix(Thud(s, 0.3, 110, 60, 0.04), 0, 0.6);
            for (int i = 0; i < 3; i++)
                power.Mix(s.Burst(0.1, 0.0008).Body(s.Metal(2100 + i * 250, 6, 0.01)), 0.18 + i * 0.07, 0.35);
            power.Mix(s.Tone(1.4, Env.Glide(0, 30, 1.0, 100), Wave.Saw).LowPass(Env.Glide(0, 200, 1.0, 900)).Shape(Env.Swell(0.4, 0.6, 0.4)), 0.25, 0.8);
            power.Mix(s.Tone(1.4, 100, Wave.Square).LowPass(1500).Shape(Env.Swell(0.8, 0.3, 0.3)), 0.25, 0.08);
            return power.Room(0.6, 0.4, 0.15, damping: 0.5, tail: 0.3).Resize(2);
        }
    }
}
