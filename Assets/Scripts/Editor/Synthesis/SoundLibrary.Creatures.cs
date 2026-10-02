using System;
using System.Collections.Generic;

namespace Wolfenstein.Editor.Synthesis
{
    // The monsters and the guards. Each monster has a voice of its own, so it can be told apart before it is seen:
    //   Stalker  - a small throat snarling high and fast, panting, skittering on its claws.
    //   Brute    - a huge throat growling an octave below anything else, heavy thudding steps, a slam that shakes the floor.
    //   Demon    - two voices at once, ring-modulated into something unnatural, with swells that seem to run backwards.
    //   Spitter  - no throat at all: wet warbling, bubbling and chittering, and no footsteps, as it floats.
    //   Revenant - a deep, slow voice ringing inside a steel helm, blades scraping, armour clanking with every step.
    internal static partial class SoundLibrary
    {
        private static void AddCreatures(List<SoundRecipe> recipes)
        {
            Variants(recipes, "Monsters/Spawn", 2, MonsterSpawn);

            Variants(recipes, "Stalker/Hunt", 3, StalkerHunt);
            Add(recipes, "Stalker/Alert", StalkerAlert);
            Add(recipes, "Stalker/MeleeWindup", StalkerWindup);
            Variants(recipes, "Stalker/MeleeStrike", 2, (s, v) => ClawSwipe(s, 1));
            Variants(recipes, "Stalker/Pain", 2, StalkerPain);
            Add(recipes, "Stalker/Death", StalkerDeath);
            Variants(recipes, "Stalker/Footstep", 3, StalkerStep);

            Variants(recipes, "Brute/Hunt", 3, BruteHunt);
            Add(recipes, "Brute/Alert", BruteAlert);
            Add(recipes, "Brute/MeleeWindup", BruteWindup);
            Add(recipes, "Brute/MeleeStrike", BruteStrike);
            Add(recipes, "Brute/Slam", BruteSlam);
            Variants(recipes, "Brute/Pain", 2, BrutePain);
            Add(recipes, "Brute/Death", BruteDeath);
            Variants(recipes, "Brute/Footstep", 3, BruteStep);

            Variants(recipes, "Demon/Hunt", 3, DemonHunt);
            Add(recipes, "Demon/Alert", DemonAlert);
            Add(recipes, "Demon/MeleeWindup", DemonWindup);
            Variants(recipes, "Demon/MeleeStrike", 2, (s, v) => ClawSwipe(s, 0.7).Mix(DemonSnarl(s, 0.25), 0.02, 0.5));
            Add(recipes, "Demon/RangedWindup", s => FireGathering(s, 0.55, 1));
            Add(recipes, "Demon/RangedLaunch", s => FireballLaunch(s, 1));
            Variants(recipes, "Demon/ProjectileImpact", 2, (s, v) => FireBurst(s, 1));
            Variants(recipes, "Demon/Pain", 2, (s, v) => DemonSnarl(s, 0.4));
            Add(recipes, "Demon/Death", DemonDeath);
            Variants(recipes, "Demon/Footstep", 3, DemonStep);

            Variants(recipes, "Spitter/Hunt", 3, SpitterHunt);
            Variants(recipes, "Spitter/Idle", 2, SpitterIdle);
            Add(recipes, "Spitter/Alert", SpitterAlert);
            Add(recipes, "Spitter/RangedWindup", SpitterWindup);
            Add(recipes, "Spitter/RangedLaunch", SpitterLaunch);
            Variants(recipes, "Spitter/ProjectileImpact", 2, SpitterImpact);
            Variants(recipes, "Spitter/Pain", 2, SpitterPain);
            Add(recipes, "Spitter/Death", SpitterDeath);

            Variants(recipes, "Revenant/Hunt", 3, RevenantHunt);
            Add(recipes, "Revenant/Alert", RevenantAlert);
            Add(recipes, "Revenant/Charge", RevenantCharge);
            Add(recipes, "Revenant/MeleeWindup", RevenantWindup);
            Variants(recipes, "Revenant/MeleeStrike", 2, RevenantStrike);
            Add(recipes, "Revenant/RangedWindup", RevenantGathering);
            Add(recipes, "Revenant/RangedLaunch", RevenantLaunch);
            Variants(recipes, "Revenant/ProjectileImpact", 2, (s, v) => FireBurst(s, 1.35));
            Variants(recipes, "Revenant/Pain", 2, RevenantPain);
            Add(recipes, "Revenant/Death", RevenantDeath);
            Variants(recipes, "Revenant/Footstep", 3, RevenantStep);

            Variants(recipes, "Guards/Alert", 2, GuardAlert);
            Variants(recipes, "Guards/Pain", 2, GuardPain);
            Variants(recipes, "Guards/Death", 2, GuardDeath);
            Variants(recipes, "Guards/Footstep", 4, GuardStep);
        }

        // ---- Shared -------------------------------------------------------------------------------------------------------

        // A monster called up through the floor: a swell that seems to run backwards, a deep boom, and embers crackling away.
        private static Sound MonsterSpawn(Studio s, int variant)
        {
            Sound spawn = s.Silence(1.4);
            spawn.Mix(ReverseSwell(s, 0.55, 700), 0, 0.7);
            spawn.Mix(Thud(s, 0.8, 70, 30, 0.22), 0.55, 1);
            spawn.Mix(s.Pink(0.5).LowPass(900).Shape(Env.Hit(0.004, 0.12)), 0.55, 0.6);
            spawn.Mix(Crackle(s, 0.8, 2400, t => 160 * Math.Exp(-t / 0.3)), 0.58, 0.35);
            return spawn.Room(0.6, 0.5, 0.25, damping: 0.4, tail: 0.3).Resize(1.4);
        }

        /// <summary>A sine falling in pitch under a hit envelope: the weight in a thud, a boom or a step.</summary>
        private static Sound Thud(Studio s, double seconds, double from, double to, double decay) =>
            s.Tone(seconds, Env.Glide(0, from, seconds * 0.6, to)).Shape(Env.Hit(0.002, decay)).Fade(0, Math.Min(0.08, seconds * 0.3));

        /// <summary>Air rushing past: noise through a band sweeping from one centre to another, swelling and fading.</summary>
        private static Sound Whoosh(Studio s, double seconds, double from, double to, double q) =>
            s.White(seconds).BandPass(Env.Glide(0, from, seconds, to), q).Shape(Env.Swell(seconds * 0.35, seconds * 0.1, seconds * 0.55));

        /// <summary>Fire crackling: sparse pops at a changing rate through a band.</summary>
        private static Sound Crackle(Studio s, double seconds, double centre, Func<double, double> rate) =>
            s.Clicks(seconds, rate).BandPass(centre, 0.9).LowPass(centre * 2).Mix(s.Clicks(seconds, t => rate(t) * 0.3).LowPass(900), 0, 0.5);

        /// <summary>A swell that grows towards its end, like a reverberant tail played backwards.</summary>
        private static Sound ReverseSwell(Studio s, double seconds, double brightness)
        {
            Sound tail = s.Pink(seconds).LowPass(Env.Glide(0, brightness * 3, seconds, brightness)).Shape(Env.Hit(0.005, seconds * 0.3));
            return tail.Reverse();
        }

        /// <summary>A body dropping to the floor, heavier for a larger weight.</summary>
        private static Sound BodyFall(Studio s, double weight)
        {
            Sound fall = s.Silence(0.8);
            fall.Mix(Thud(s, 0.4, 90 / Math.Sqrt(weight), 45 / Math.Sqrt(weight), 0.06 * weight), 0, 1);
            fall.Mix(s.Pink(0.2).LowPass(500).Shape(Env.Hit(0.003, 0.04)), 0, 0.7);
            fall.Mix(Thud(s, 0.3, 70 / Math.Sqrt(weight), 40 / Math.Sqrt(weight), 0.05 * weight), 0.14 + 0.05 * weight, 0.6);
            return fall;
        }

        /// <summary>Claws slashing the air, quick for a small creature and broader for a slower one.</summary>
        private static Sound ClawSwipe(Studio s, double size)
        {
            Sound swipe = Whoosh(s, 0.16 * (0.7 + size * 0.3), 900 * size, 4500 * size, 1.4);
            swipe.Mix(Whoosh(s, 0.12, 1400 * size, 6000 * size, 2.5), 0.03, 0.5);
            return swipe.Pad(0.1);
        }

        // Burning air thrown forward: a soft blast that darkens as it goes, a low push and a crackling trail.
        private static Sound FireballLaunch(Studio s, double size)
        {
            Sound launch = s.Silence(0.8);
            launch.Mix(s.White(0.6).LowPass(Env.Glide(0, 2800, 0.4, 450), 0.8).Shape(Env.Hit(0.012, 0.13 * size)), 0, 1);
            launch.Mix(Thud(s, 0.4, 95 / size, 45 / size, 0.08), 0, 0.7);
            launch.Mix(Crackle(s, 0.6, 2200, t => 260 * Math.Exp(-t / 0.2)), 0.02, 0.35);
            return launch.Drive(1.5);
        }

        // Fire gathering in a claw or a throat: a rising roar of air, crackling faster and faster over a low swell.
        private static Sound FireGathering(Studio s, double seconds, double size)
        {
            Sound gather = s.Silence(seconds + 0.1);
            gather.Mix(s.White(seconds).BandPass(Env.Glide(0, 300 / size, seconds, 2600 / size), 2).Shape(Env.Line(0, 0, seconds * 0.9, 1, seconds, 0)), 0, 0.8);
            gather.Mix(Crackle(s, seconds, 2000, t => 40 + 500 * t / seconds), 0, 0.4);
            gather.Mix(s.Tone(seconds, Env.Glide(0, 60 / size, seconds, 95 / size), Wave.Saw).LowPass(300).Shape(Env.Line(0, 0, seconds * 0.9, 1, seconds, 0)), 0, 0.5);
            return gather;
        }

        // A fireball bursting against stone: a blast, a boom and a crackle of flames dying away.
        private static Sound FireBurst(Studio s, double size)
        {
            Sound burst = s.Silence(1.2);
            burst.Mix(s.White(0.5).LowPass(Env.Glide(0, 3200, 0.3, 600)).Shape(Env.Hit(0.002, 0.09 * size)), 0, 1);
            burst.Mix(Thud(s, 0.5, 80 / size, 32 / size, 0.12 * size), 0, 0.9);
            burst.Mix(Crackle(s, 0.9, 2600, t => 400 * Math.Exp(-t / 0.25)), 0.01, 0.4);
            burst.Mix(s.White(0.7).BandPass(3500, 1).Shape(Env.Hit(0.01, 0.15)), 0.02, 0.04);
            return burst.Drive(1.6).Room(0.5 * size, 0.4, 0.22, damping: 0.45, tail: 0.3).Resize(1.2);
        }

        // ---- Stalker -------------------------------------------------------------------------------------------------------

        // Quick, panting snarls high in a small throat, spat out in bursts.
        private static Sound StalkerHunt(Studio s, int variant)
        {
            double seconds = 0.5 + variant * 0.1;
            Func<double, double> wander = Env.Wander(s.Rng, 0.18, 9, seconds);
            double baseline = 165 + variant * 18;
            Sound snarl = Growl(s, seconds, t => baseline * wander(t), VowelE, VowelA, 1.12, 0.95, 0.35, 0.35, 2.6);
            snarl.Tremolo(_ => 6.5 + variant, 0.65).Shape(Env.Swell(0.02, seconds * 0.6, seconds * 0.35));
            snarl.Mix(Breath(s, seconds, VowelE, 1.15, Env.Swell(0.01, seconds * 0.5, seconds * 0.4)), 0, 0.18);
            return snarl.Room(0.3, 0.25, 0.1, damping: 0.5, tail: 0.1);
        }

        // A shriek rising and breaking as it sights its prey.
        private static Sound StalkerAlert(Studio s)
        {
            Sound shriek = Growl(s, 0.8, Env.Glide(0, 250, 0.2, 470, 0.8, 290), VowelI, VowelA, 1.15, 0.7, 0.2, 0.25, 3);
            shriek.Shape(Env.Swell(0.03, 0.4, 0.35));
            return shriek.Room(0.35, 0.3, 0.14, damping: 0.5, tail: 0.2);
        }

        // A sharp hiss drawn in, rising into a snarl, just before it lunges.
        private static Sound StalkerWindup(Studio s)
        {
            Sound windup = Breath(s, 0.3, VowelE, 1.2, Env.Line(0, 0, 0.25, 1, 0.3, 0)).Gain(0.7);
            windup.Mix(Growl(s, 0.25, Env.Glide(0, 180, 0.25, 270), VowelE, VowelA, 1.12, 0.9, 0.3, 0.3, 2.5)
                .Shape(Env.Swell(0.05, 0.12, 0.08)), 0.08, 0.7);
            return windup;
        }

        private static Sound StalkerPain(Studio s, int variant)
        {
            double pitch = 360 + variant * 40;
            return Growl(s, 0.3, Env.Glide(0, pitch, 0.3, pitch * 0.62), VowelA, VowelE, 1.1, 0.5, 0.1, 0.2, 2.2)
                .Shape(Env.Swell(0.01, 0.1, 0.18));
        }

        // A screech falling into a gurgle, and a light body dropping.
        private static Sound StalkerDeath(Studio s)
        {
            Sound screech = Growl(s, 0.85, Env.Glide(0, 430, 0.7, 95), VowelI, VowelU, 1.12, 0.8, 0.3, 0.3, 2.8);
            screech.Tremolo(Env.Line(0, 4, 0.85, 22), 0.5).Shape(Env.Swell(0.02, 0.35, 0.45));
            Sound death = s.Silence(1.3);
            death.Mix(screech, 0, 1);
            death.Mix(BodyFall(s, 0.7), 0.75, 0.6);
            return death.Room(0.35, 0.3, 0.12, damping: 0.5, tail: 0.2).Resize(1.4);
        }

        // Claws clicking on the floor, two to a step, over a soft pad.
        private static Sound StalkerStep(Studio s, int variant)
        {
            Sound step = s.Silence(0.2);
            step.Mix(s.White(0.02).Shape(Env.Hit(0.0002, 0.0025)).BandPass(s.Rng.Range(3200, 4600), 1.5), 0, 1);
            step.Mix(s.White(0.02).Shape(Env.Hit(0.0002, 0.002)).BandPass(s.Rng.Range(3500, 5000), 1.5), s.Rng.Range(0.02, 0.035), 0.7);
            step.Mix(Thud(s, 0.08, 170, 120, 0.014), 0, 0.5);
            return step;
        }

        // ---- Brute ---------------------------------------------------------------------------------------------------------

        // A slow, rolling growl from a huge chest, an octave below anything else, with the throat doubling on itself.
        private static Sound BruteHunt(Studio s, int variant)
        {
            double seconds = 1.3 + variant * 0.2;
            Func<double, double> wander = Env.Wander(s.Rng, 0.1, 3, seconds);
            double baseline = 50 + variant * 5;
            Sound growl = Growl(s, seconds, t => baseline * wander(t), VowelO, VowelU, 0.6, 0.6, 0.55, 0.15, 2.2);
            growl.Shape(Env.Swell(0.15, seconds * 0.45, seconds * 0.45));
            growl.Mix(s.Tone(seconds, t => baseline * 0.5 * wander(t)).Shape(Env.Swell(0.2, seconds * 0.4, seconds * 0.4)), 0, 0.35);
            growl.Mix(Breath(s, 0.5, VowelO, 0.62, Env.Swell(0.1, 0.1, 0.3)), seconds - 0.35, 0.15);
            return growl.Room(0.6, 0.4, 0.14, damping: 0.6, tail: 0.3);
        }

        // A bellowing roar that fills the room.
        private static Sound BruteAlert(Studio s)
        {
            Sound roar = Growl(s, 1.6, Env.Glide(0, 72, 0.4, 98, 1.6, 58), VowelUh, VowelA, 0.62, 0.8, 0.6, 0.2, 3);
            roar.Shape(Env.Swell(0.12, 0.8, 0.65));
            roar.Mix(s.Tone(1.6, Env.Glide(0, 36, 0.4, 49, 1.6, 29)).Shape(Env.Swell(0.15, 0.8, 0.6)), 0, 0.4);
            return roar.Room(0.8, 0.55, 0.2, damping: 0.5, tail: 0.5);
        }

        // A strained growl climbing as it heaves its arms up.
        private static Sound BruteWindup(Studio s)
        {
            Sound windup = Growl(s, 0.75, Env.Glide(0, 55, 0.7, 88), VowelU, VowelA, 0.62, 0.7, 0.5, 0.2, 2.5);
            windup.Shape(Env.Line(0, 0, 0.1, 0.6, 0.65, 1, 0.75, 0.2));
            windup.Mix(Breath(s, 0.3, VowelO, 0.62, Env.Swell(0.05, 0.1, 0.15)), 0, 0.3);
            return windup;
        }

        // The arms coming down through the air with a grunt of effort.
        private static Sound BruteStrike(Studio s)
        {
            Sound strike = Whoosh(s, 0.45, 220, 950, 1.2);
            strike.Mix(Growl(s, 0.22, Env.Glide(0, 85, 0.22, 65), VowelUh, VowelUh, 0.62, 0.6, 0.4, 0.2, 2.5).Shape(Env.Hit(0.01, 0.08)), 0.05, 0.8);
            return strike.Pad(0.1);
        }

        // Both fists into the floor: a boom felt more than heard, the crunch of the impact and debris rattling down.
        private static Sound BruteSlam(Studio s)
        {
            Sound slam = s.Silence(1.6);
            slam.Mix(Thud(s, 1.2, 58, 27, 0.38), 0, 1);
            slam.Mix(s.White(0.4).LowPass(Env.Glide(0, 1800, 0.2, 300)).Shape(Env.Hit(0.001, 0.07)), 0, 0.9);
            slam.Mix(s.Clicks(0.8, t => 900 * Math.Exp(-t / 0.18)).BandPass(2400, 0.8), 0.03, 0.4);
            slam.Mix(s.Clicks(0.8, t => 300 * Math.Exp(-t / 0.25)).Body(s.Metal(900, 6, 0.03)), 0.05, 0.2);
            return slam.Drive(1.8).Room(0.8, 0.55, 0.22, damping: 0.5, tail: 0.4).Resize(1.6);
        }

        private static Sound BrutePain(Studio s, int variant)
        {
            double pitch = 76 + variant * 6;
            return Growl(s, 0.5, Env.Glide(0, pitch, 0.08, pitch * 1.15, 0.5, pitch * 0.8), VowelUh, VowelO, 0.62, 0.7, 0.5, 0.2, 2.5)
                .Shape(Env.Swell(0.02, 0.18, 0.28)).Room(0.5, 0.3, 0.12, damping: 0.6, tail: 0.2);
        }

        // A long roar falling away to nothing, then a huge body crashing down.
        private static Sound BruteDeath(Studio s)
        {
            Sound roar = Growl(s, 1.7, Env.Glide(0, 85, 0.3, 92, 1.7, 33), VowelA, VowelU, 0.62, 0.85, 0.6, 0.25, 3);
            roar.Shape(Env.Swell(0.08, 0.7, 0.9));
            Sound death = s.Silence(2.6);
            death.Mix(roar, 0, 1);
            death.Mix(BodyFall(s, 1.8), 1.6, 1);
            death.Mix(s.Clicks(0.5, t => 500 * Math.Exp(-t / 0.12)).BandPass(2000, 0.8), 1.62, 0.25);
            return death.Room(0.8, 0.5, 0.18, damping: 0.5, tail: 0.4).Resize(2.8);
        }

        // A heavy foot landing: a deep thump and a little of the floor shaking under it.
        private static Sound BruteStep(Studio s, int variant)
        {
            Sound step = s.Silence(0.55);
            step.Mix(Thud(s, 0.45, s.Rng.Range(52, 60), 36, 0.09), 0, 1);
            step.Mix(s.Pink(0.2).LowPass(380).Shape(Env.Hit(0.003, 0.045)), 0, 0.8);
            step.Mix(s.Clicks(0.3, t => 250 * Math.Exp(-t / 0.08)).BandPass(1200, 0.8).LowPass(1800), 0.01, 0.15);
            return step.Drive(1.4);
        }

        // ---- Demon ---------------------------------------------------------------------------------------------------------

        // Two throats growling as one, an octave and more apart, ring-modulated into something that is not of this world,
        // drawn in by a breath that seems to run backwards.
        private static Sound DemonHunt(Studio s, int variant)
        {
            double seconds = 1.1 + variant * 0.15;
            Func<double, double> wander = Env.Wander(s.Rng, 0.12, 4, seconds);
            double baseline = 88 + variant * 7;
            Sound upper = Growl(s, seconds, t => baseline * wander(t), VowelO, VowelA, 0.82, 0.7, 0.3, 0.2, 2.2);
            Sound lower = Growl(s, seconds, t => baseline * 0.6 * wander(t), VowelO, VowelU, 0.7, 0.6, 0.4, 0.15, 2);
            Sound voice = upper.Mix(lower, 0, 0.8).Ring(_ => 37 + variant * 4, 0.35);
            voice.Shape(Env.Swell(0.25, seconds * 0.4, seconds * 0.4));
            Sound hunt = s.Silence(seconds + 0.4);
            hunt.Mix(ReverseSwell(s, 0.4, 900), 0, 0.35);
            hunt.Mix(voice, 0.3, 1);
            return hunt.Room(0.6, 0.5, 0.2, damping: 0.45, tail: 0.3);
        }

        // A backwards rush into a roar that twists as it goes.
        private static Sound DemonAlert(Studio s)
        {
            Sound roar = Growl(s, 1.1, Env.Glide(0, 110, 0.3, 165, 1.1, 88), VowelUh, VowelA, 0.76, 0.9, 0.4, 0.2, 3);
            roar.Mix(Growl(s, 1.1, Env.Glide(0, 66, 0.3, 99, 1.1, 53), VowelO, VowelA, 0.7, 0.8, 0.5, 0.15, 2.5), 0, 0.6);
            roar.Ring(Env.Glide(0, 45, 1.1, 70), 0.35).Shape(Env.Swell(0.05, 0.55, 0.5));
            Sound alert = s.Silence(1.6);
            alert.Mix(ReverseSwell(s, 0.45, 1200), 0, 0.6);
            alert.Mix(roar, 0.4, 1);
            return alert.Room(0.65, 0.55, 0.22, damping: 0.4, tail: 0.4);
        }

        private static Sound DemonSnarl(Studio s, double seconds)
        {
            double pitch = s.Rng.Range(120, 150);
            return Growl(s, seconds, Env.Glide(0, pitch, seconds, pitch * 0.7), VowelA, VowelO, 0.8, 0.9, 0.4, 0.25, 3)
                .Ring(_ => 53, 0.4).Shape(Env.Swell(0.01, seconds * 0.4, seconds * 0.5));
        }

        // A hiss and a crackle of heat as it rears back.
        private static Sound DemonWindup(Studio s)
        {
            Sound windup = Breath(s, 0.45, VowelA, 0.85, Env.Line(0, 0, 0.35, 1, 0.45, 0)).LowPass(3000);
            windup.Mix(DemonSnarl(s, 0.35).Shape(Env.Line(0, 0, 0.3, 1, 0.35, 0)), 0.08, 0.6);
            windup.Mix(Crackle(s, 0.45, 1800, t => 80 + 600 * t), 0, 0.3);
            return windup.Ring(_ => 60, 0.2);
        }

        // A scream torn and twisted, the ring-modulation climbing, dissolving into a rush that runs backwards.
        private static Sound DemonDeath(Studio s)
        {
            Sound scream = Growl(s, 1.3, Env.Glide(0, 210, 1.2, 60), VowelA, VowelU, 0.8, 0.9, 0.4, 0.25, 3);
            scream.Ring(Env.Glide(0, 30, 1.3, 75), 0.45).Shape(Env.Swell(0.02, 0.5, 0.7));
            Sound death = s.Silence(2.2);
            death.Mix(scream, 0, 1);
            death.Mix(ReverseSwell(s, 0.6, 1500), 1.0, 0.45);
            death.Mix(BodyFall(s, 1), 1.55, 0.7);
            return death.Room(0.65, 0.5, 0.2, damping: 0.45, tail: 0.3).Resize(2.3);
        }

        // Hard hooves: a sharp knock with a mid-weight thump.
        private static Sound DemonStep(Studio s, int variant)
        {
            Sound step = s.Silence(0.3);
            step.Mix(s.White(0.03).Shape(Env.Hit(0.0003, 0.004)).BandPass(s.Rng.Range(1400, 1900), 1.2), 0, 1);
            step.Mix(Thud(s, 0.15, 130, 90, 0.022), 0, 0.7);
            return step.Room(0.3, 0.25, 0.1, damping: 0.5, tail: 0.1);
        }

        // ---- Spitter -------------------------------------------------------------------------------------------------------

        // Wet warbling from something with no throat, over bubbling and a clicking chitter.
        private static Sound SpitterHunt(Studio s, int variant)
        {
            double seconds = 1.0 + variant * 0.15;
            double pitch = 230 + variant * 30;
            Sound warble = s.Fm(seconds, t => pitch * (1 + 0.14 * Math.Sin(2 * Math.PI * (6 + variant) * t)), 1.01, _ => 1.6);
            warble.Tremolo(_ => 11, 0.5).Shape(Env.Swell(0.1, seconds * 0.4, seconds * 0.4));
            Sound hunt = s.Silence(seconds);
            hunt.Mix(warble.BandPass(pitch * 2.5, 0.8), 0, 0.7);
            hunt.Mix(Bubbles(s, seconds, 18), 0, 0.6);
            hunt.Mix(Chitter(s, seconds, 0.3 + variant * 0.1), 0, 0.35);
            return hunt.Room(0.35, 0.3, 0.12, damping: 0.4, tail: 0.15);
        }

        // Left alone it only bubbles and clicks to itself.
        private static Sound SpitterIdle(Studio s, int variant)
        {
            Sound idle = s.Silence(1.4);
            idle.Mix(Bubbles(s, 1.4, 9), 0, 0.8);
            idle.Mix(Chitter(s, 1.4, 0.5), 0, 0.3);
            return idle.Room(0.35, 0.3, 0.12, damping: 0.4, tail: 0.15);
        }

        // Rising, wet bubbles of random size.
        private static Sound Bubbles(Studio s, double seconds, double rate)
        {
            Sound bubbles = s.Silence(seconds);
            for (double t = s.Rng.Range(0, 1 / rate); t < seconds - 0.05; t += s.Rng.Range(0.3, 1.7) / rate)
            {
                double start = s.Rng.Range(350, 900);
                double length = s.Rng.Range(0.02, 0.05);
                Sound bubble = s.Tone(length, Env.Glide(0, start, length, start * s.Rng.Range(1.6, 2.4))).Shape(Env.Swell(0.003, length * 0.3, length * 0.6));
                bubbles.Mix(bubble, t, s.Rng.Range(0.4, 1));
            }

            return bubbles.Resize(seconds);
        }

        // Bursts of quick, hard clicks, like mandibles.
        private static Sound Chitter(Studio s, double seconds, double busy)
        {
            Sound chitter = s.Silence(seconds);
            for (double t = s.Rng.Range(0, 0.3); t < seconds - 0.15; t += s.Rng.Range(0.25, 0.6) / busy)
            {
                double burst = s.Rng.Range(0.06, 0.14);
                double rate = s.Rng.Range(35, 60);
                chitter.Mix(s.Tone(burst, rate, Wave.Square, 0.08).Mix(s.White(burst), 0, 0.3).BandPass(s.Rng.Range(2500, 3800), 3)
                    .Shape(Env.Swell(0.005, burst * 0.6, burst * 0.3)), t, 1);
            }

            return chitter.Resize(seconds);
        }

        // A warbling shriek.
        private static Sound SpitterAlert(Studio s)
        {
            Sound shriek = s.Fm(0.9, t => (520 + 380 * Env.Smooth(t / 0.3) - 260 * Env.Smooth((t - 0.4) / 0.5)) *
                                           (1 + 0.2 * Math.Sin(2 * Math.PI * 11 * t)), 1.5, _ => 2.5);
            shriek.Shape(Env.Swell(0.03, 0.45, 0.4)).BandPass(1400, 0.6);
            Sound alert = s.Silence(1);
            alert.Mix(shriek, 0, 1);
            alert.Mix(Bubbles(s, 0.9, 25), 0, 0.5);
            return alert.Room(0.4, 0.35, 0.14, damping: 0.4, tail: 0.2);
        }

        // A charging whine climbing and trembling faster as the eye glows, bubbling harder and harder.
        private static Sound SpitterWindup(Studio s)
        {
            const double seconds = 0.55;
            Sound whine = s.Tone(seconds, Env.Glide(0, 280, seconds, 1500), Wave.Triangle).Tremolo(Env.Line(0, 8, seconds, 32), 0.6);
            whine.Shape(Env.Line(0, 0, seconds * 0.85, 1, seconds, 0));
            whine.Mix(Bubbles(s, seconds, 40), 0, 0.5);
            return whine.Pad(0.05);
        }

        // A wet spit and the pop of the fireball leaving.
        private static Sound SpitterLaunch(Studio s)
        {
            Sound launch = s.Silence(0.6);
            launch.Mix(s.White(0.12).BandPass(Env.Glide(0, 1700, 0.1, 650), 3).Shape(Env.Hit(0.003, 0.03)), 0, 1);
            launch.Mix(Thud(s, 0.15, 210, 90, 0.03), 0, 0.7);
            launch.Mix(FireballLaunch(s, 0.7), 0.02, 0.6);
            return launch.Resize(0.6);
        }

        // A fiery, wet splat that sizzles out.
        private static Sound SpitterImpact(Studio s, int variant)
        {
            Sound impact = s.Silence(0.9);
            impact.Mix(s.White(0.15).BandPass(Env.Glide(0, 1200, 0.1, 600), 2).Shape(Env.Hit(0.002, 0.04)), 0, 1);
            impact.Mix(Thud(s, 0.2, 130, 60, 0.04), 0, 0.6);
            impact.Mix(s.White(0.8).BandPass(3800, 1.2).Shape(Env.Hit(0.01, 0.18)), 0.01, 0.07);
            impact.Mix(Crackle(s, 0.8, 2600, t => 500 * Math.Exp(-t / 0.2)), 0.01, 0.35);
            return impact;
        }

        private static Sound SpitterPain(Studio s, int variant)
        {
            double pitch = 650 + variant * 90;
            Sound squeal = s.Fm(0.35, t => pitch * (1 - 0.3 * t / 0.35) * (1 + 0.15 * Math.Sin(2 * Math.PI * 15 * t)), 1.5, _ => 2);
            return squeal.Shape(Env.Swell(0.01, 0.12, 0.2)).BandPass(1500, 0.6).Mix(Bubbles(s, 0.35, 30), 0, 0.4);
        }

        // Deflating in a falling gurgle, bursting wetly, and sizzling away.
        private static Sound SpitterDeath(Studio s)
        {
            Sound gurgle = s.Fm(1.0, t => 600 * Math.Pow(0.2, t) * (1 + 0.2 * Math.Sin(2 * Math.PI * (14 - 8 * t) * t)), 1.01, _ => 2);
            gurgle.Tremolo(Env.Line(0, 20, 1, 6), 0.7).Shape(Env.Swell(0.02, 0.5, 0.45)).BandPass(900, 0.5);
            Sound death = s.Silence(1.6);
            death.Mix(gurgle, 0, 0.8);
            death.Mix(Bubbles(s, 1.0, 30), 0, 0.5);
            death.Mix(SpitterImpact(s, 1), 0.95, 0.9);
            return death.Resize(1.8);
        }

        // ---- Revenant ------------------------------------------------------------------------------------------------------

        /// <summary>A voice ringing inside a steel helm: a comb tuned low, with a large, dark room around it.</summary>
        private static Sound Helm(Sound voice, Func<double, double> ring, double amount) =>
            voice.Resonate(ring, 0.78, amount, 0.25);

        // A deep, slow growl ringing in its helm.
        private static Sound RevenantHunt(Studio s, int variant)
        {
            double seconds = 1.4 + variant * 0.2;
            Func<double, double> wander = Env.Wander(s.Rng, 0.08, 2.5, seconds);
            double baseline = 58 + variant * 5;
            Sound growl = Growl(s, seconds, t => baseline * wander(t), VowelO, VowelUh, 0.68, 0.5, 0.35, 0.12, 2);
            growl.Shape(Env.Swell(0.25, seconds * 0.4, seconds * 0.45));
            return Helm(growl, _ => 118 + variant * 9, 0.4).Room(0.85, 0.6, 0.24, damping: 0.5, tail: 0.5);
        }

        // A war roar with a steel shriek over it, filling a great hall.
        private static Sound RevenantAlert(Studio s)
        {
            Sound roar = Growl(s, 1.8, Env.Glide(0, 88, 0.5, 132, 1.8, 70), VowelUh, VowelO, 0.7, 0.9, 0.5, 0.2, 3);
            roar = Helm(roar, Env.Glide(0, 150, 1.8, 220), 0.45).Shape(Env.Swell(0.1, 0.9, 0.8));
            roar.Mix(s.Fm(1.8, t => 1750 + 150 * Math.Sin(2 * Math.PI * 5 * t), 1.41, _ => 1.2).Shape(Env.Swell(0.4, 0.6, 0.8)), 0, 0.12);
            return roar.Room(0.95, 0.65, 0.26, damping: 0.45, tail: 0.6);
        }

        // A short rising war cry as it charges, its armour clanking.
        private static Sound RevenantCharge(Studio s)
        {
            Sound cry = Growl(s, 0.9, Env.Glide(0, 100, 0.6, 150), VowelUh, VowelA, 0.7, 0.9, 0.4, 0.2, 3);
            cry = Helm(cry, _ => 170, 0.4).Shape(Env.Swell(0.05, 0.5, 0.35));
            cry.Mix(ArmourClank(s, 0.6).LowPass(3000), 0.05, 0.15);
            return cry.Room(0.8, 0.5, 0.2, damping: 0.5, tail: 0.3);
        }

        // Its arm blades scraping together as it draws back.
        private static Sound RevenantWindup(Studio s)
        {
            Sound scrape = s.White(0.45).BandPass(Env.Glide(0, 2200, 0.4, 4200), 6).Shape(Env.Line(0, 0, 0.35, 1, 0.45, 0));
            scrape = scrape.Body(s.Metal(1900, 8, 0.03)).Resize(0.5);
            scrape.Mix(Growl(s, 0.4, Env.Glide(0, 70, 0.4, 95), VowelO, VowelUh, 0.7, 0.7, 0.4, 0.2, 2.5).Shape(Env.Swell(0.1, 0.15, 0.15)), 0.05, 0.6);
            return scrape;
        }

        // A blade cutting the air and ringing as it stops.
        private static Sound RevenantStrike(Studio s, int variant)
        {
            Sound strike = Whoosh(s, 0.22, 1200, 6500, 1.8);
            strike.Mix(s.Burst(0.5, 0.001).Body(s.Metal(2300 + variant * 200, 10, 0.12)), 0.15, 0.35);
            return strike.Resize(0.55);
        }

        // Dark fire gathering: a low drone climbing through its helm, crackling as it builds.
        private static Sound RevenantGathering(Studio s)
        {
            const double seconds = 0.65;
            Sound gather = s.Tone(seconds, Env.Glide(0, 55, seconds, 92), Wave.Saw).LowPass(700);
            gather = Helm(gather, Env.Glide(0, 200, seconds, 600), 0.5).Shape(Env.Line(0, 0, seconds * 0.9, 1, seconds, 0.2));
            gather.Mix(Crackle(s, seconds, 2200, t => 60 + 700 * t), 0, 0.35);
            return gather.Pad(0.05);
        }

        // Three fireballs thrown in a fan, one on the heels of the next, with a grunt of effort.
        private static Sound RevenantLaunch(Studio s)
        {
            Sound launch = s.Silence(1);
            for (int i = 0; i < 3; i++)
                launch.Mix(FireballLaunch(s, 1.2), i * 0.045, 0.6);
            launch.Mix(Helm(Growl(s, 0.3, Env.Glide(0, 95, 0.3, 70), VowelUh, VowelUh, 0.7, 0.7, 0.4, 0.2, 2.5), _ => 140, 0.4)
                .Shape(Env.Hit(0.01, 0.1)), 0, 0.5);
            return launch.Resize(1);
        }

        private static Sound RevenantPain(Studio s, int variant)
        {
            double pitch = 82 + variant * 6;
            Sound grunt = Growl(s, 0.5, Env.Glide(0, pitch, 0.5, pitch * 0.75), VowelUh, VowelO, 0.7, 0.7, 0.4, 0.2, 2.5)
                .Shape(Env.Swell(0.02, 0.18, 0.28));
            return Helm(grunt, _ => 130, 0.4).Room(0.7, 0.4, 0.16, damping: 0.5, tail: 0.3);
        }

        // A great roar falling away inside its helm, and the armoured body crashing down in pieces.
        private static Sound RevenantDeath(Studio s)
        {
            Sound roar = Growl(s, 1.9, Env.Glide(0, 96, 0.3, 104, 1.9, 34), VowelA, VowelU, 0.7, 0.85, 0.5, 0.2, 3);
            roar = Helm(roar, Env.Glide(0, 160, 1.9, 90), 0.45).Shape(Env.Swell(0.08, 0.8, 1));
            Sound death = s.Silence(3);
            death.Mix(roar, 0, 1);
            death.Mix(BodyFall(s, 1.6), 1.8, 0.9);
            death.Mix(ArmourClank(s, 1).LowPass(3500), 1.85, 0.3);
            death.Mix(ArmourClank(s, 0.8).LowPass(3500), 2.05, 0.22);
            death.Mix(ArmourClank(s, 0.6).LowPass(3500), 2.2, 0.15);
            return death.Room(0.95, 0.6, 0.22, damping: 0.45, tail: 0.5).Resize(3.2);
        }

        /// <summary>Plates of armour knocking together.</summary>
        private static Sound ArmourClank(Studio s, double weight) =>
            s.Burst(0.5, 0.002).Body(s.Metal(s.Rng.Range(420, 620) / Math.Sqrt(weight), 10, 0.06)).Resize(0.5).Fade(0, 0.1);

        // An armoured foot: a heavy thud and a clank of steel.
        private static Sound RevenantStep(Studio s, int variant)
        {
            Sound step = s.Silence(0.5);
            step.Mix(Thud(s, 0.3, 68, 45, 0.06), 0, 1);
            step.Mix(s.Pink(0.12).LowPass(500).Shape(Env.Hit(0.002, 0.03)), 0, 0.6);
            step.Mix(ArmourClank(s, 1), 0.01, 0.45);
            return step.Resize(0.5);
        }

        // ---- Guards --------------------------------------------------------------------------------------------------------

        // A soldier's wordless shout on sighting the player.
        private static Sound GuardAlert(Studio s, int variant)
        {
            double pitch = 165 + variant * 20;
            Sound voice = s.Glottal(0.45, Env.Glide(0, pitch, 0.08, pitch * 1.25, 0.45, pitch * 0.9), 0.02, 0.1, 0.05, 0.08);
            Sound shout = Vocal(voice, VowelUh, variant == 1 ? VowelA : VowelO, 0.02, 0.1, 1).Shape(Env.Swell(0.015, 0.22, 0.2)).Drive(2.2);
            Sound alert = s.Silence(0.6);
            alert.Mix(Breath(s, 0.04, VowelA, 1, Env.Hit(0.002, 0.012)), 0, 0.5);
            alert.Mix(shout, 0.015, 1);
            return alert.Room(0.45, 0.35, 0.15, damping: 0.5, tail: 0.2).Resize(0.7);
        }

        private static Sound GuardPain(Studio s, int variant)
        {
            double pitch = 128 + variant * 15;
            Sound voice = s.Glottal(0.3, Env.Glide(0, pitch, 0.05, pitch * 1.08, 0.3, pitch * 0.75), 0.025, 0.15, 0.1, 0.1);
            return Vocal(voice, VowelUh, VowelO, 0.02, 0.1, 0.95).Shape(Env.Swell(0.01, 0.08, 0.17)).Drive(1.8).Pad(0.05);
        }

        // A cry falling away and the body dropping.
        private static Sound GuardDeath(Studio s, int variant)
        {
            double pitch = 210 + variant * 25;
            Sound voice = s.Glottal(0.9, Env.Glide(0, pitch, 0.15, pitch * 1.1, 0.9, pitch * 0.45), 0.03, 0.18, 0.15, 0.12);
            Sound cry = Vocal(voice, VowelA, VowelU, 0.2, 0.8, 1).Shape(Env.Swell(0.02, 0.35, 0.5)).Drive(2);
            Sound death = s.Silence(1.4);
            death.Mix(cry, 0, 1);
            death.Mix(BodyFall(s, 1), 0.8, 0.7);
            return death.Room(0.45, 0.35, 0.14, damping: 0.5, tail: 0.2).Resize(1.5);
        }

        // Hobnailed boots: a hard heel click and a solid thump.
        private static Sound GuardStep(Studio s, int variant)
        {
            Sound step = s.Silence(0.35);
            step.Mix(s.White(0.03).Shape(Env.Hit(0.0003, 0.003)).BandPass(s.Rng.Range(2300, 2900), 1.4), 0, 0.8);
            step.Mix(StoneStep(s, 1.2), 0, 1);
            return step.Resize(0.35);
        }
    }
}
