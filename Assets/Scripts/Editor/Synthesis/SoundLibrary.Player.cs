using System;
using System.Collections.Generic;

namespace Wolfenstein.Editor.Synthesis
{
    // The player: footsteps on each kind of floor, jumping and landing, pain and death, and the items they collect.
    internal static partial class SoundLibrary
    {
        private static void AddPlayer(List<SoundRecipe> recipes)
        {
            Variants(recipes, "Footsteps/Stone", 4, (s, v) => Footstep(s, StoneStep));
            Variants(recipes, "Footsteps/Concrete", 4, (s, v) => Footstep(s, ConcreteStep));
            Variants(recipes, "Footsteps/Metal", 4, (s, v) => Footstep(s, MetalStep));
            Variants(recipes, "Footsteps/Grate", 4, (s, v) => Footstep(s, GrateStep));
            Variants(recipes, "Footsteps/Wood", 4, (s, v) => Footstep(s, WoodStep));
            Variants(recipes, "Footsteps/Carpet", 4, (s, v) => Footstep(s, CarpetStep));
            Variants(recipes, "Player/Jump", 2, Jump);
            Variants(recipes, "Player/Land", 2, Land);
            Variants(recipes, "Player/Hurt", 3, Hurt);
            Add(recipes, "Player/Death", PlayerDeath);
            Add(recipes, "Pickups/FirstAid", FirstAidPickup);
            Add(recipes, "Pickups/Food", FoodPickup);
            Variants(recipes, "Pickups/Ammo", 2, AmmoPickup);
            Add(recipes, "Pickups/TreasureSmall", s => TreasurePickup(s, false));
            Add(recipes, "Pickups/TreasureLarge", s => TreasurePickup(s, true));
        }

        // A step is a heel strike and a softer roll onto the toe a moment later.
        private static Sound Footstep(Studio s, Func<Studio, double, Sound> strike)
        {
            Sound step = s.Silence(0.45);
            step.Mix(strike(s, 1), 0, 1);
            step.Mix(strike(s, 0.45), s.Rng.Range(0.035, 0.055), 0.45);
            return step.Resize(0.45);
        }

        // Hard flagstones in a stone passage: a gritty click, a knock, and the stone ringing back briefly.
        private static Sound StoneStep(Studio s, double force)
        {
            Sound hit = s.Silence(0.4);
            hit.Mix(s.White(0.05).Shape(Env.Hit(0.0008, 0.011 * force)).BandPass(s.Rng.Range(1500, 2100), 0.8), 0, 1);
            hit.Mix(s.Tone(0.08, Env.Glide(0, 120, 0.04, 80)).Shape(Env.Hit(0.001, 0.018)), 0, 0.55 * force);
            hit.Mix(s.Clicks(0.05, t => 500 * Math.Exp(-t / 0.015)).BandPass(4200, 1), 0.002, 0.25);
            return hit.Room(0.35, 0.3, 0.14, damping: 0.5, tail: 0.15);
        }

        // Bare concrete: duller than stone, with more weight and a bigger, plainer room.
        private static Sound ConcreteStep(Studio s, double force)
        {
            Sound hit = s.Silence(0.4);
            hit.Mix(s.White(0.06).Shape(Env.Hit(0.001, 0.013 * force)).BandPass(s.Rng.Range(900, 1300), 0.8), 0, 1);
            hit.Mix(s.Tone(0.1, Env.Glide(0, 100, 0.05, 65)).Shape(Env.Hit(0.001, 0.024)), 0, 0.8 * force);
            hit.Mix(s.White(0.04).HighPass(2500).Shape(Env.Hit(0.001, 0.01)), 0.003, 0.12);
            return hit.Room(0.55, 0.35, 0.16, damping: 0.6, tail: 0.15);
        }

        // Steel tread plate: the boot's thud and the plate ringing a little under it.
        private static Sound MetalStep(Studio s, double force)
        {
            Sound hit = s.Silence(0.4);
            hit.Mix(s.Burst(0.2, 0.002).Body(s.Metal(s.Rng.Range(320, 430), 10, 0.045)), 0, 0.8);
            hit.Mix(s.White(0.06).Shape(Env.Hit(0.001, 0.012)).LowPass(700), 0, 0.9);
            hit.Mix(s.Tone(0.08, 95).Shape(Env.Hit(0.001, 0.02)), 0, 0.5 * force);
            return hit.Resize(0.4);
        }

        // Open grating: a hollow clank and the loose bars rattling in their frame.
        private static Sound GrateStep(Studio s, double force)
        {
            Sound hit = s.Silence(0.4);
            hit.Mix(s.Burst(0.2, 0.002).Body(s.Metal(s.Rng.Range(700, 900), 10, 0.05)), 0, 0.8);
            hit.Mix(s.Clicks(0.08, t => 250 * Math.Exp(-t / 0.03), 0.3).Body(s.Metal(s.Rng.Range(1500, 1900), 8, 0.02)), 0.004, 0.7 * force);
            hit.Mix(s.Burst(0.2, 0.003).Body(new Mode(s.Rng.Range(160, 200), 0.05, 1)), 0, 0.5);
            return hit.Resize(0.4);
        }

        // Floorboards: a hollow knock from the boards and the space beneath them.
        private static Sound WoodStep(Studio s, double force)
        {
            Sound hit = s.Burst(0.3, 0.003).Body(s.Wood(s.Rng.Range(135, 175), 0.05));
            hit.Mix(s.White(0.04).Shape(Env.Hit(0.0008, 0.008)).BandPass(1300, 1), 0, 0.5);
            hit.Mix(s.Tone(0.1, 85).Shape(Env.Hit(0.002, 0.02)), 0, 0.4 * force);
            return hit.Resize(0.4);
        }

        // Thick carpet: a soft, muffled pad with a whisper of fabric.
        private static Sound CarpetStep(Studio s, double force)
        {
            Sound hit = s.Silence(0.3);
            hit.Mix(s.Pink(0.12).Shape(Env.Hit(0.005, 0.03 * force)).LowPass(650), 0, 1);
            hit.Mix(s.Tone(0.1, 80).Shape(Env.Hit(0.004, 0.025)), 0, 0.6 * force);
            hit.Mix(s.White(0.1).HighPass(3000).Shape(Env.Swell(0.01, 0.01, 0.05)), 0, 0.05);
            return hit;
        }

        // A short effort through the nose and the rustle of kit shifting.
        private static Sound Jump(Studio s, int variant)
        {
            Sound jump = Breath(s, 0.2, VowelUh, 1.05, Env.Swell(0.02, 0.03, 0.12)).LowPass(2800);
            jump.Mix(s.White(0.18).BandPass(1800, 0.8).Shape(Env.Swell(0.01, 0.04, 0.1)), 0, 0.15);
            return jump;
        }

        // Boots coming down hard: a heavy thud, the knees taking it and the kit rattling.
        private static Sound Land(Studio s, int variant)
        {
            Sound land = s.Silence(0.4);
            land.Mix(s.Tone(0.25, Env.Glide(0, 85, 0.1, 48)).Shape(Env.Hit(0.002, 0.06)), 0, 1);
            land.Mix(s.Pink(0.15).Shape(Env.Hit(0.002, 0.035)).LowPass(700), 0, 0.8);
            land.Mix(s.White(0.12).BandPass(2800, 1).Shape(Env.Swell(0.005, 0.02, 0.06)), 0.01, 0.12);
            return land.Drive(1.2);
        }

        // A short grunt as a blow lands: a rough, low "uh" breaking out over the thump of the hit.
        private static Sound Hurt(Studio s, int variant)
        {
            double pitch = 150 - variant * 12;
            double[] vowel = variant == 2 ? VowelA : VowelUh;
            Sound voice = s.Glottal(0.3, Env.Glide(0, pitch, 0.06, pitch * 1.05, 0.26, pitch * 0.72), 0.025, 0.15, 0.1, 0.1);
            Sound grunt = Vocal(voice, VowelUh, vowel, 0.02, 0.12, 1).Shape(Env.Swell(0.012, 0.07, 0.18)).Drive(1.6);
            Sound hurt = s.Silence(0.4);
            hurt.Mix(Breath(s, 0.06, VowelUh, 1, Env.Hit(0.004, 0.02)), 0, 0.4);
            hurt.Mix(grunt, 0.012, 1);
            hurt.Mix(s.Tone(0.12, Env.Glide(0, 110, 0.05, 60)).Shape(Env.Hit(0.001, 0.025)), 0, 0.45);
            return hurt.Room(0.3, 0.2, 0.1, damping: 0.6, tail: 0.1).Resize(0.45);
        }

        // A long cry breaking and falling away, then the body hitting the floor.
        private static Sound PlayerDeath(Studio s)
        {
            Func<double, double> pitch = t => (175 * Math.Pow(0.45, t / 0.9)) * (1 + 0.03 * Math.Sin(2 * Math.PI * 6 * t) * t);
            Sound voice = s.Glottal(1.0, pitch, 0.03, 0.2, 0.25, 0.14);
            Sound cry = Vocal(voice, VowelA, VowelU, 0.15, 0.85, 1).Shape(Env.Swell(0.03, 0.35, 0.6)).Drive(1.8);
            Sound death = s.Silence(1.6);
            death.Mix(cry, 0, 1);
            death.Mix(Land(s, 1), 0.9, 0.8);
            death.Mix(s.Pink(0.2).LowPass(400).Shape(Env.Hit(0.003, 0.05)), 1.05, 0.6);
            return death.Room(0.4, 0.4, 0.14, damping: 0.5, tail: 0.3).Resize(1.8);
        }

        // Two quick, clean rising chimes with a short echo: health restored.
        private static Sound FirstAidPickup(Studio s)
        {
            Sound chime = s.Silence(0.6);
            chime.Mix(Chip(s, 784, 0.25, Wave.Square, 0.25), 0, 0.7);
            chime.Mix(Chip(s, 1175, 0.3, Wave.Square, 0.25), 0.075, 0.7);
            chime.Mix(Chip(s, 1568, 0.35, Wave.Triangle, 0.5), 0.075, 0.5);
            return chime.LowPass(6000).Echo(0.09, 0.25, 0.3, 0.2, 0.5).Resize(0.6);
        }

        // A warm, rounded gulp and a crunch: food rather than medicine.
        private static Sound FoodPickup(Studio s)
        {
            Sound gulp = s.Silence(0.45);
            gulp.Mix(s.Tone(0.12, Env.Glide(0, 220, 0.12, 440)).Shape(Env.Swell(0.005, 0.07, 0.04)), 0, 0.8);
            gulp.Mix(s.Tone(0.15, Env.Glide(0, 330, 0.14, 587)).Shape(Env.Swell(0.005, 0.08, 0.06)), 0.11, 0.8);
            gulp.Mix(s.Clicks(0.1, t => 600 * Math.Exp(-t / 0.04)).BandPass(1800, 1.2), 0, 0.5);
            return gulp.Echo(0.07, 0.2, 0.2, 0.15, 0.6).Resize(0.45);
        }

        // A magazine clacked home in its pouch: a crisp, mechanical ammo sound.
        private static Sound AmmoPickup(Studio s, int variant)
        {
            Sound clack = s.Silence(0.3);
            clack.Mix(s.Burst(0.15, 0.001).Body(s.Metal(1300 + variant * 120, 8, 0.03)), 0, 0.9);
            clack.Mix(s.White(0.04).Shape(Env.Hit(0.0005, 0.008)).BandPass(1100, 1.2), 0, 0.6);
            clack.Mix(s.Burst(0.15, 0.001).Body(s.Metal(2000 - variant * 100, 8, 0.025)), 0.07, 1);
            clack.Mix(s.Tone(0.08, 180).Shape(Env.Hit(0.001, 0.02)), 0.07, 0.35);
            return clack.Resize(0.3);
        }

        // Bright bells for gold: two notes for small treasure, a rising run and a shimmer of coins for a large one.
        private static Sound TreasurePickup(Studio s, bool large)
        {
            double[] notes = large ? new[] { 1319.0, 1661, 1976, 2637 } : new[] { 1319.0, 1976 };
            double spacing = large ? 0.07 : 0.08;
            Sound bells = s.Silence(large ? 1.1 : 0.7);
            for (int i = 0; i < notes.Length; i++)
                bells.Mix(Bell(s, notes[i], 0.6, 0.25), i * spacing, 0.6);

            if (large)
                bells.Mix(s.Clicks(0.6, t => 90 * Math.Exp(-t / 0.2)).Body(s.Metal(3200, 8, 0.05)).Shape(Env.Swell(0.05, 0.2, 0.3)), 0.1, 0.35);
            return bells.Echo(0.11, 0.25, 0.25, 0.3, 0.4).Resize(large ? 1.1 : 0.7);
        }

        /// <summary>A plucked chiptune note: a raw oscillator with a quick fall.</summary>
        private static Sound Chip(Studio s, double frequency, double seconds, Wave wave, double width) =>
            s.Tone(seconds, frequency, wave, width).Shape(Env.Hit(0.002, seconds * 0.35)).Fade(0, seconds * 0.3);

        /// <summary>A struck bell: frequency modulation at an inharmonic ratio, bright at the strike and mellowing as it rings.</summary>
        private static Sound Bell(Studio s, double frequency, double seconds, double decay) =>
            s.Fm(seconds, _ => frequency, 1.4, Env.Hit(0.001, decay * 0.5).Compose(x => 0.3 + 1.6 * x))
                .Shape(Env.Hit(0.002, decay)).LowPass(7000).Fade(0, Math.Min(0.3, seconds * 0.3));
    }
}
