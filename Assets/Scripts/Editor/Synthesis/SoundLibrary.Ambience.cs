using System;
using System.Collections.Generic;

namespace Wolfenstein.Editor.Synthesis
{
    // The background of the level: a looping bed for each kind of zone, loops for the machines that hum where they stand, a
    // pulse of tension for the arena fight, and the distant sounds heard now and then. Beds are kept low and dark, with room
    // left in them for silence. Hums sit on whole numbers of hertz and loops last whole seconds, so a loop's seam falls in
    // phase and cannot be heard.
    internal static partial class SoundLibrary
    {
        // Seconds of each bed crossfaded across its seam.
        private const double LoopFade = 2;

        private static void AddAmbience(List<SoundRecipe> recipes)
        {
            Add(recipes, "Ambience/Stone", SoundKind.Loop, StoneAmbience);
            Add(recipes, "Ambience/Industrial", SoundKind.Loop, IndustrialAmbience);
            Add(recipes, "Ambience/Electrical", SoundKind.Loop, ElectricalAmbience);
            Add(recipes, "Ambience/RoomTone", SoundKind.Loop, RoomTone);
            Add(recipes, "Ambience/Arena", SoundKind.Loop, ArenaAmbience);
            Add(recipes, "Ambience/Pens", SoundKind.Loop, PenAmbience);
            Add(recipes, "Ambience/Lift", SoundKind.Loop, LiftAmbience);
            Add(recipes, "Ambience/Tension", SoundKind.Loop, TensionBed);
            Add(recipes, "Machines/Generator", SoundKind.Loop, GeneratorLoop);
            Add(recipes, "Machines/ElectricHum", SoundKind.Loop, ElectricHumLoop);
            Add(recipes, "Machines/Vent", SoundKind.Loop, VentLoop);

            Variants(recipes, "Distant/MetalClank", 3, DistantClank);
            Variants(recipes, "Distant/PipeKnock", 2, PipeKnock);
            Variants(recipes, "Distant/Groan", 2, DistantGroan);
            Variants(recipes, "Distant/Creak", 2, Creak);
            Add(recipes, "Distant/ElectricBuzz", ElectricBuzz);
            Variants(recipes, "Distant/Drip", 2, Drip);
            Add(recipes, "Distant/Rumble", Rumble);
        }

        /// <summary>A bed of the given length, with the extra it needs to crossfade across its own seam.</summary>
        private static Sound Bed(Studio s, double seconds) => s.Silence(seconds + LoopFade);

        // Old stone passages: deep, still air and a hollow draught that comes and goes.
        private static Sound StoneAmbience(Studio s)
        {
            const double seconds = 14;
            double length = seconds + LoopFade;
            Sound bed = Bed(s, seconds);
            bed.Mix(s.Brown(length).LowPass(170), 0, 1);
            Func<double, double> draught = Env.Wander(s.Rng, 0.25, 0.15, length);
            bed.Mix(s.Pink(length).BandPass(t => 330 * draught(t), 3).Shape(t => 0.5 + 0.5 * Math.Sin(2 * Math.PI * t / 7)), 0, 0.4);
            bed.Mix(s.Pink(length).HighPass(2500), 0, 0.02);
            return bed.Resize(length).Loop(LoopFade);
        }

        // Machinery working somewhere in the walls: a mains hum, an engine's slow chug, the odd knock of a part, and air forced
        // through ducts.
        private static Sound IndustrialAmbience(Studio s)
        {
            const double seconds = 12;
            double length = seconds + LoopFade;
            Sound bed = Bed(s, seconds);
            bed.Mix(Hum(s, length, 50, 0.5, 0.35, 0.15), 0, 0.35);
            bed.Mix(s.Brown(length).LowPass(300).Tremolo(_ => 1.5, 0.8), 0, 0.9);
            bed.Mix(s.Pink(length).BandPass(900, 0.7), 0, 0.2);
            for (double t = 0.7; t < length - 1; t += 2.4)
                bed.Mix(s.Burst(1, 0.003).Body(s.Metal(s.Rng.Range(220, 300), 8, 0.15)).LowPass(1200), t, 0.15);
            return bed.Resize(length).Loop(LoopFade);
        }

        // Strip lights and wiring: a steady electrical buzz over the draw of the air handling.
        private static Sound ElectricalAmbience(Studio s)
        {
            const double seconds = 10;
            double length = seconds + LoopFade;
            Sound bed = Bed(s, seconds);
            bed.Mix(Hum(s, length, 100, 0.3, 0.1, 0.04), 0, 0.5);
            bed.Mix(s.Tone(length, 100, Wave.Square).LowPass(1400), 0, 0.05);
            bed.Mix(s.Pink(length).LowPass(1200), 0, 0.5);
            bed.Mix(s.Brown(length).LowPass(150), 0, 0.5);
            return bed.Resize(length).Loop(LoopFade);
        }

        // A quiet, warm room: almost nothing, only the hush of the building.
        private static Sound RoomTone(Studio s)
        {
            const double seconds = 12;
            double length = seconds + LoopFade;
            Sound bed = Bed(s, seconds);
            bed.Mix(s.Brown(length).LowPass(220), 0, 1);
            bed.Mix(s.Pink(length).LowPass(900), 0, 0.2);
            bed.Mix(s.Tone(length, 50), 0, 0.04);
            return bed.Resize(length).Loop(LoopFade);
        }

        // A vast concrete hall: a low rumble swelling and falling, air moving high above, machinery far below.
        private static Sound ArenaAmbience(Studio s)
        {
            const double seconds = 16;
            double length = seconds + LoopFade;
            Sound bed = Bed(s, seconds);
            bed.Mix(s.Brown(length).LowPass(90).Shape(t => 0.7 + 0.3 * Math.Sin(2 * Math.PI * t / seconds)), 0, 1);
            Func<double, double> air = Env.Wander(s.Rng, 0.3, 0.1, length);
            bed.Mix(s.Pink(length).BandPass(t => 250 * air(t), 0.8), 0, 0.3);
            bed.Mix(s.Tone(length, 36), 0, 0.12);
            bed.Mix(s.Pink(length).HighPass(3000), 0, 0.015);
            return bed.Resize(length).Loop(LoopFade);
        }

        // The pens: a wet, low cave drone, water dripping into an echo, and something breathing heavily in the dark.
        private static Sound PenAmbience(Studio s)
        {
            const double seconds = 12;
            double length = seconds + LoopFade;
            Sound bed = Bed(s, seconds);
            bed.Mix(s.Brown(length).LowPass(130), 0, 1);

            Sound drips = s.Silence(length);
            for (double t = s.Rng.Range(0.2, 1); t < length - 1.5; t += s.Rng.Range(0.9, 2.6))
                drips.Mix(DripBlip(s), t, s.Rng.Range(0.4, 1));
            bed.Mix(drips.Echo(0.19, 0.35, 0.45, 0.8, 0.5).Resize(length), 0, 0.12);

            Sound breathing = Growl(s, length, _ => 42, VowelO, VowelO, 0.6, 0.5, 0.5, 0.3, 1.5).LowPass(350);
            bed.Mix(breathing.Shape(t => Math.Pow(0.5 + 0.5 * Math.Sin(2 * Math.PI * t / 4), 2)), 0, 0.25);
            return bed.Resize(length).Loop(LoopFade);
        }

        // The lift shaft: its motor humming and a cable singing faintly under tension.
        private static Sound LiftAmbience(Studio s)
        {
            const double seconds = 10;
            double length = seconds + LoopFade;
            Sound bed = Bed(s, seconds);
            bed.Mix(Hum(s, length, 60, 0.5, 0.3, 0.15), 0, 0.4);
            bed.Mix(s.Tone(length, 1100).Shape(t => 0.5 + 0.5 * Math.Sin(2 * Math.PI * t / 5)), 0, 0.02);
            bed.Mix(s.Pink(length).LowPass(1500), 0, 0.3);
            bed.Mix(s.Brown(length).LowPass(150), 0, 0.6);
            return bed.Resize(length).Loop(LoopFade);
        }

        // Laid under the arena fight: a slow heartbeat over a dissonant, grinding drone a step apart.
        private static Sound TensionBed(Studio s)
        {
            const double seconds = 8;
            Sound drone = s.Silence(seconds + 1);
            drone.Mix(s.Chorus(seconds + 1, _ => 36.71, Wave.Saw, 3, 8).LowPass(160), 0, 0.5);
            drone.Mix(s.Chorus(seconds + 1, _ => 38.89, Wave.Saw, 3, 8).LowPass(160), 0, 0.35);
            drone.Loop(1);

            Sound beats = s.Silence(seconds + 1);
            for (int beat = 0; beat < 16; beat++)
            {
                beats.Mix(Thud(s, 0.3, 58, 40, 0.06), beat * 0.5, 1);
                beats.Mix(Thud(s, 0.3, 50, 36, 0.05), beat * 0.5 + 0.16, 0.55);
            }

            return drone.Mix(beats.Fold(seconds), 0, 0.8);
        }

        /// <summary>A mains hum: a fundamental and its next two harmonics at the given levels.</summary>
        private static Sound Hum(Studio s, double seconds, double fundamental, double first, double second, double third)
        {
            Sound hum = s.Tone(seconds, fundamental).Gain(first);
            hum.Mix(s.Tone(seconds, fundamental * 2), 0, second);
            hum.Mix(s.Tone(seconds, fundamental * 3), 0, third);
            return hum;
        }

        // A generator: its engine firing, a hum and the tick of its valves.
        private static Sound GeneratorLoop(Studio s)
        {
            const double seconds = 4;
            Sound engine = s.Silence(seconds + 1);
            engine.Mix(s.Brown(seconds + 1).LowPass(250).Tremolo(_ => 12.5, 0.6), 0, 1);
            engine.Mix(Hum(s, seconds + 1, 50, 0.3, 0.2, 0.05), 0, 0.5);
            engine.Loop(1);

            Sound valves = s.Silence(seconds + 0.2);
            for (int i = 0; i < 25; i++)
                valves.Mix(s.Burst(0.1, 0.0008).Body(s.Metal(1150, 6, 0.01)), i * seconds / 25, 0.12);
            return engine.Mix(valves.Fold(seconds), 0, 1);
        }

        private static Sound ElectricHumLoop(Studio s)
        {
            const double seconds = 3;
            Sound hum = Hum(s, seconds + 1, 100, 0.6, 0.3, 0.15);
            hum.Mix(s.Tone(seconds + 1, 100, Wave.Square).LowPass(2000), 0, 0.1);
            hum.Mix(s.Clicks(seconds + 1, _ => 6).HighPass(2000), 0, 0.2);
            return hum.Loop(1);
        }

        private static Sound VentLoop(Studio s)
        {
            const double seconds = 4;
            Sound vent = s.Pink(seconds + 1).BandPass(500, 0.8).Tremolo(_ => 12, 0.25);
            vent.Mix(s.Brown(seconds + 1).LowPass(120), 0, 0.5);
            return vent.Loop(1);
        }

        // ---- Distant sounds ------------------------------------------------------------------------------------------------

        // Somewhere far off, metal strikes metal and rings through the halls.
        private static Sound DistantClank(Studio s, int variant) =>
            s.Burst(0.5, 0.003).Body(s.Metal(s.Rng.Range(180, 340), 10, 0.4)).LowPass(1400)
                .Room(1.1, 0.8, 0.55, damping: 0.5, tail: 1.5).Resize(2.6);

        // Someone, or something, knocking along a pipe.
        private static Sound PipeKnock(Studio s, int variant)
        {
            Sound knocks = s.Silence(1);
            double time = 0;
            for (int i = 0; i < 3 + variant; i++)
            {
                knocks.Mix(s.Burst(0.5, 0.002).Body(new Mode(280, 0.12, 1), new Mode(760, 0.08, 0.5), new Mode(1350, 0.05, 0.3)), time, 1 - i * 0.12);
                time += s.Rng.Range(0.14, 0.24);
            }

            return knocks.LowPass(2000).Room(1, 0.7, 0.45, damping: 0.5, tail: 1.2).Resize(2.2);
        }

        // A long moan from something far away in the dark.
        private static Sound DistantGroan(Studio s, int variant)
        {
            Sound groan = Growl(s, 2.2, Env.Glide(0, 72 + variant * 6, 2.2, 52), VowelO, VowelU, 0.7, 0.6, 0.4, 0.2, 2);
            return groan.Shape(Env.Swell(0.5, 0.8, 0.9)).LowPass(700).Room(1.2, 0.85, 0.6, damping: 0.5, tail: 1.5).Resize(3.6);
        }

        // Steel straining under load: a stick-slip creak rising and falling.
        private static Sound Creak(Studio s, int variant)
        {
            Func<double, double> strain = Env.Wander(s.Rng, 0.3, 3, 1.4);
            Sound creak = s.Clicks(1.4, t => (30 + 70 * Math.Sin(Math.PI * t / 1.4)) * strain(t), 0.2);
            creak = creak.Body(s.Metal(s.Rng.Range(420, 620), 8, 0.03)).BandPass(900, 0.7).LowPass(2500);
            return creak.Room(1, 0.7, 0.4, damping: 0.5, tail: 1).Resize(2.5);
        }

        // Faulty wiring arcing and spitting.
        private static Sound ElectricBuzz(Studio s)
        {
            Func<double, double> flicker = Env.Wander(s.Rng, 0.5, 14, 0.8);
            Sound buzz = s.Tone(0.8, 100, Wave.Square).LowPass(3000).Shape(t => flicker(t) > 1.1 ? 1 : 0.15);
            buzz.Mix(s.Clicks(0.8, _ => 40).HighPass(2000), 0, 0.5);
            return buzz.Shape(Env.Swell(0.02, 0.6, 0.15)).Room(0.6, 0.4, 0.25, damping: 0.5, tail: 0.4);
        }

        // One drop of water into a pool, echoing.
        private static Sound Drip(Studio s, int variant) =>
            DripBlip(s).Pad(0.1).Echo(0.21, 0.45, 0.5, 1.4, 0.5).Room(0.8, 0.6, 0.3, damping: 0.4, tail: 0.5);

        private static Sound DripBlip(Studio s)
        {
            double start = s.Rng.Range(1200, 1700);
            return s.Tone(0.05, Env.Glide(0, start, 0.04, start * 1.7)).Shape(Env.Hit(0.001, 0.012));
        }

        // Something huge shifting deep in the structure.
        private static Sound Rumble(Studio s)
        {
            Sound rumble = Thud(s, 2.5, 45, 28, 0.6);
            rumble.Mix(s.Brown(2.5).LowPass(150).Shape(Env.Swell(0.3, 0.5, 1.5)), 0, 0.8);
            return rumble.Room(1.2, 0.8, 0.4, damping: 0.5, tail: 1).Resize(3.5);
        }
    }
}
