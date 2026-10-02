using System;
using System.Collections.Generic;

namespace Wolfenstein.Editor.Synthesis
{
    // The pistol and what its shots strike.
    internal static partial class SoundLibrary
    {
        private static void AddWeapons(List<SoundRecipe> recipes)
        {
            Variants(recipes, "Weapons/PistolShot", 3, PistolShot);
            Variants(recipes, "Weapons/PistolMechanism", 2, PistolMechanism);
            Variants(recipes, "Weapons/DryFire", 2, DryFire);
            Variants(recipes, "Impacts/Stone", 4, StoneImpact);
            Variants(recipes, "Impacts/Metal", 3, MetalImpact);
            Variants(recipes, "Impacts/Wood", 3, WoodImpact);
            Variants(recipes, "Impacts/Flesh", 3, FleshImpact);
            Variants(recipes, "Guards/Gunshot", 2, GuardGunshot);
        }

        // A sharp crack over a short blast and a falling thump, driven a little, with a small dark room behind it: present
        // and punchy, but short enough not to wear on the ear shot after shot.
        private static Sound PistolShot(Studio s, int variant)
        {
            double pitch = 1 + (variant - 2) * 0.05;
            Sound shot = s.Silence(0.8);
            shot.Mix(s.White(0.004).HighPass(3000).Shape(Env.Hit(0.0001, 0.0008)), 0, 0.6);
            shot.Mix(s.White(0.12).Shape(Env.Hit(0.0004, 0.012)).BandPass(Env.Glide(0, 4200 * pitch, 0.05, 1500), 0.8), 0, 1.3);
            shot.Mix(s.White(0.06).Shape(Env.Hit(0.0005, 0.01)).BandPass(950 * pitch, 1.2), 0, 0.8);
            shot.Mix(s.White(0.25).Shape(Env.Hit(0.001, 0.035)).LowPass(Env.Glide(0, 2600, 0.15, 380), 0.9), 0, 0.9);
            shot.Mix(s.Tone(0.3, Env.Glide(0, 175 * pitch, 0.08, 52)).Shape(Env.Hit(0.001, 0.045)), 0, 0.6);
            shot.Drive(1.8);
            shot.Room(0.45, 0.35, 0.25, damping: 0.55, tail: 0.3);
            return shot.Resize(0.8).Shape(t => 1 - Env.Smooth((t - 0.45) / 0.35));
        }

        // The slide flying back and slamming home a moment after the shot: two small metallic clicks and a scrape.
        private static Sound PistolMechanism(Studio s, int variant)
        {
            Sound cycle = s.Silence(0.25);
            cycle.Mix(s.Burst(0.1, 0.0015).Body(s.Metal(1150 + variant * 90, 8, 0.025)).HighPass(500), 0.035, 1);
            cycle.Mix(s.White(0.03).Shape(Env.Hit(0.0005, 0.006)).BandPass(850, 1.3), 0.035, 0.5);
            cycle.Mix(s.White(0.05).BandPass(3800, 1.5).Shape(Env.Swell(0.005, 0.015, 0.02)), 0.04, 0.1);
            cycle.Mix(s.Burst(0.1, 0.001).Body(s.Metal(1650 - variant * 70, 8, 0.02)).HighPass(600), 0.078, 0.85);
            cycle.Mix(s.White(0.03).Shape(Env.Hit(0.0005, 0.005)).BandPass(1100, 1.3), 0.078, 0.4);
            return cycle;
        }

        // The hammer falling on an empty chamber: a hard little click with a hint of the frame behind it.
        private static Sound DryFire(Studio s, int variant)
        {
            Sound click = s.Silence(0.18);
            click.Mix(s.Burst(0.1, 0.0008).Body(s.Metal(1050 + variant * 120, 9, 0.014)).HighPass(400), 0, 1);
            click.Mix(s.Tone(0.05, 330).Shape(Env.Hit(0.0005, 0.008)), 0, 0.3);
            click.Mix(s.Burst(0.05, 0.0006).Body(s.Metal(2600, 6, 0.006)).HighPass(1200), 0.028, 0.3);
            return click;
        }

        // A chip of masonry: a crack, a thud, and grit pattering down. One in four whines away as a ricochet.
        private static Sound StoneImpact(Studio s, int variant)
        {
            double brightness = s.Rng.Range(0.85, 1.2);
            Sound hit = s.Silence(0.5);
            hit.Mix(s.White(0.06).Shape(Env.Hit(0.0003, 0.008)).BandPass(2600 * brightness, 0.9), 0, 1);
            hit.Mix(s.Tone(0.15, Env.Glide(0, 230, 0.05, 90)).Shape(Env.Hit(0.0005, 0.02)), 0, 0.5);
            hit.Mix(s.Clicks(0.35, t => 1400 * Math.Exp(-t / 0.05)).BandPass(3600 * brightness, 0.8), 0.004, 0.9);
            if (variant == 4)
                hit.Mix(Ricochet(s), 0.01, 0.4);
            return hit.Room(0.3, 0.3, 0.15, damping: 0.5, tail: 0.2);
        }

        private static Sound Ricochet(Studio s)
        {
            double start = s.Rng.Range(2800, 3600);
            Func<double, double> pitch = t => start * Math.Pow(0.55, t / 0.45) * (1 + 0.015 * Math.Sin(2 * Math.PI * 23 * t));
            Sound whine = s.Tone(0.5, pitch, Wave.Triangle).Mix(s.White(0.5).BandPass(pitch, 10), 0, 0.6);
            return whine.Shape(Env.Swell(0.01, 0.06, 0.4));
        }

        private static Sound MetalImpact(Studio s, int variant)
        {
            Sound hit = s.Burst(0.8, 0.0015).Body(s.Metal(s.Rng.Range(650, 1100), 10, 0.16));
            hit.Mix(s.White(0.02).HighPass(3000).Shape(Env.Hit(0.0002, 0.003)), 0, 0.4);
            return hit.Resize(0.7);
        }

        private static Sound WoodImpact(Studio s, int variant)
        {
            Sound hit = s.Burst(0.4, 0.002).Body(s.Wood(s.Rng.Range(240, 330), 0.04));
            hit.Mix(s.White(0.03).Shape(Env.Hit(0.0003, 0.005)).BandPass(1900, 1), 0, 0.6);
            hit.Mix(s.Clicks(0.15, t => 700 * Math.Exp(-t / 0.03)).BandPass(2600, 1), 0.003, 0.4);
            return hit.Resize(0.4);
        }

        // A wet, heavy thud: a falling low tone, a slap and a short squelch.
        private static Sound FleshImpact(Studio s, int variant)
        {
            Sound hit = s.Silence(0.35);
            hit.Mix(s.Tone(0.2, Env.Glide(0, 150, 0.06, 65)).Shape(Env.Hit(0.001, 0.035)), 0, 1);
            hit.Mix(s.White(0.12).Shape(Env.Hit(0.0005, 0.018)).BandPass(Env.Glide(0, 1900, 0.08, 520), 1.2), 0, 0.8);
            hit.Mix(s.White(0.22).Shape(Env.Swell(0.005, 0.02, 0.12)).BandPass(Env.Glide(0, 950, 0.18, 330), 5), 0.01, 0.5);
            return hit.Drive(1.4);
        }

        // A guard's gun: heavier and lower than the player's pistol, heard from across a room.
        private static Sound GuardGunshot(Studio s, int variant)
        {
            Sound shot = PistolShot(s, variant).Speed(0.86);
            shot.Mix(s.Tone(0.35, Env.Glide(0, 120, 0.12, 45)).Shape(Env.Hit(0.001, 0.07)), 0, 0.5);
            return shot.Resize(0.8);
        }
    }
}
