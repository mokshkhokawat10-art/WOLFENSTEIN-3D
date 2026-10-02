using System;
using System.Collections.Generic;

namespace Wolfenstein.Editor.Synthesis
{
    // The doors, each kind built from what moves it: a light sliding door on rollers, the arena's hydraulic blast door, the
    // checkpoint's servo-driven security door, and the lift gate on its chain.
    internal static partial class SoundLibrary
    {
        // Seconds a door takes to slide open or shut: its width over its speed.
        private const double DoorTravel = 1.0;

        private static void AddDoors(List<SoundRecipe> recipes)
        {
            Add(recipes, "Doors/Standard/Open", s => SlidingDoor(s, 1));
            Add(recipes, "Doors/Standard/Close", s => SlidingDoor(s, 0.94));
            Add(recipes, "Doors/Standard/Opened", s => DoorStop(s, 1, 0.6));
            Add(recipes, "Doors/Standard/Closed", s => DoorStop(s, 1, 1));
            Add(recipes, "Doors/Standard/Locked", DoorRattle);

            Add(recipes, "Doors/Blast/Open", s => BlastDoor(s, true));
            Add(recipes, "Doors/Blast/Close", s => BlastDoor(s, false));
            Add(recipes, "Doors/Blast/Opened", s => DoorStop(s, 1.8, 0.7));
            Add(recipes, "Doors/Blast/Closed", s => DoorStop(s, 1.8, 1));
            Add(recipes, "Doors/Blast/Lock", s => Bolts(s, true));
            Add(recipes, "Doors/Blast/Unlock", s => Bolts(s, false));
            Add(recipes, "Doors/Blast/Locked", s => Buzzer(s, 110, 0.5));

            Add(recipes, "Doors/Security/Open", s => ServoDoor(s, true));
            Add(recipes, "Doors/Security/Close", s => ServoDoor(s, false));
            Add(recipes, "Doors/Security/Opened", s => DoorStop(s, 1.2, 0.5));
            Add(recipes, "Doors/Security/Closed", s => DoorStop(s, 1.2, 0.9));
            Add(recipes, "Doors/Security/Lock", s => SecurityLock(s, true));
            Add(recipes, "Doors/Security/Unlock", s => SecurityLock(s, false));
            Add(recipes, "Doors/Security/Locked", s => Buzzer(s, 150, 0.4));

            Add(recipes, "Doors/Exit/Open", s => LiftGate(s, true));
            Add(recipes, "Doors/Exit/Close", s => LiftGate(s, false));
            Add(recipes, "Doors/Exit/Opened", s => DoorStop(s, 1.3, 0.6));
            Add(recipes, "Doors/Exit/Closed", s => DoorStop(s, 1.3, 1));
            Add(recipes, "Doors/Exit/Lock", s => Bolts(s, true).Speed(1.2));
            Add(recipes, "Doors/Exit/Unlock", ExitUnlock);
            Add(recipes, "Doors/Exit/Locked", DoorRattle);
        }

        // A light steel door on rollers: the latch clicking, a small motor humming and the rollers ticking along the track.
        private static Sound SlidingDoor(Studio s, double pitch)
        {
            double seconds = DoorTravel;
            Func<double, double> envelope = Env.Swell(0.08, seconds - 0.2, 0.12);
            Sound door = s.Silence(seconds + 0.1);
            door.Mix(s.Burst(0.2, 0.001).Body(s.Metal(1400, 8, 0.02)), 0, 0.5);
            door.Mix(s.Tone(seconds, t => 118 * pitch * (1 + 0.04 * Math.Sin(2 * Math.PI * 3 * t)), Wave.Saw).LowPass(900).Shape(envelope), 0.03, 0.35);
            door.Mix(s.Pink(seconds).BandPass(600 * pitch, 1.2).Shape(envelope), 0.03, 0.6);
            door.Mix(s.Clicks(seconds, _ => 26).Body(s.Metal(900 * pitch, 5, 0.012)).Resize(seconds).Shape(envelope), 0.03, 0.35);
            return door.Room(0.4, 0.3, 0.12, damping: 0.5, tail: 0.15);
        }

        // The door coming to rest against its stop: a thud of the given weight and the frame ringing a little.
        private static Sound DoorStop(Studio s, double weight, double force)
        {
            Sound stop = s.Silence(0.7);
            stop.Mix(Thud(s, 0.4, 110 / weight, 60 / weight, 0.04 * weight), 0, force);
            stop.Mix(s.Burst(0.5, 0.003).Body(s.Metal(260 / Math.Sqrt(weight), 10, 0.08 * weight)), 0, 0.45 * force);
            stop.Mix(s.White(0.05).Shape(Env.Hit(0.001, 0.01)).BandPass(1500, 1), 0, 0.4 * force);
            return stop.Drive(1.3).Room(0.5, 0.35, 0.15, damping: 0.5, tail: 0.2).Resize(0.8);
        }

        // Someone trying a door that will not give: the handle rattling against the lock.
        private static Sound DoorRattle(Studio s)
        {
            Sound rattle = s.Silence(0.5);
            for (int i = 0; i < 3; i++)
                rattle.Mix(s.Burst(0.2, 0.002).Body(s.Metal(s.Rng.Range(500, 700), 8, 0.03)), i * 0.09 + s.Rng.Range(0, 0.015), 1 - i * 0.15);
            return rattle.Room(0.35, 0.3, 0.12, damping: 0.5, tail: 0.1);
        }

        // The arena's blast door: pressure hissing out, a heavy grinding drive and the slab juddering in its guides.
        private static Sound BlastDoor(Studio s, bool opening)
        {
            double seconds = DoorTravel;
            Func<double, double> envelope = Env.Swell(0.12, seconds - 0.25, 0.13);
            Sound door = s.Silence(seconds + 0.2);
            door.Mix(s.White(0.45).BandPass(opening ? 3800 : 3200, 1.5).Shape(Env.Swell(0.01, 0.1, 0.3)), 0, 0.45);
            door.Mix(s.Tone(seconds, t => (opening ? 46 : 42) * (1 + 0.06 * Math.Sin(2 * Math.PI * 7 * t)), Wave.Saw).LowPass(400).Shape(envelope), 0.05, 0.8);
            door.Mix(s.Brown(seconds).LowPass(300).Shape(envelope), 0.05, 0.6);
            door.Mix(s.White(seconds).BandPass(700, 3).Tremolo(_ => 13, 0.7).Shape(envelope), 0.05, 0.3);
            return door.Drive(1.4).Room(0.8, 0.5, 0.18, damping: 0.5, tail: 0.3);
        }

        // Heavy bolts driven home, or drawn back with a gasp of released pressure.
        private static Sound Bolts(Studio s, bool locking)
        {
            Sound bolts = s.Silence(1);
            double[] times = locking ? new[] { 0.0, 0.11, 0.2 } : new[] { 0.12, 0.2, 0.3 };
            foreach (double time in times)
            {
                bolts.Mix(Thud(s, 0.3, 140, 70, 0.04), time, 0.6);
                bolts.Mix(s.Burst(0.4, 0.002).Body(s.Metal(s.Rng.Range(380, 520), 10, 0.07)), time, 0.6);
            }

            if (!locking)
                bolts.Mix(s.White(0.5).BandPass(3000, 1.2).Shape(Env.Swell(0.01, 0.05, 0.4)), 0, 0.35);
            return bolts.Drive(1.4).Room(0.7, 0.45, 0.16, damping: 0.5, tail: 0.3).Resize(1.1);
        }

        // A harsh, low denial buzz.
        private static Sound Buzzer(Studio s, double pitch, double seconds)
        {
            Sound buzz = s.Tone(seconds, pitch, Wave.Square, 0.3).Mix(s.Tone(seconds, pitch * 1.01, Wave.Saw), 0, 0.5);
            return buzz.LowPass(2200).Shape(Env.Swell(0.005, seconds - 0.03, 0.025)).Room(0.3, 0.2, 0.1, damping: 0.5, tail: 0.1);
        }

        // The checkpoint's steel door: a servo whining up to speed and down again, and a smooth rumble of the panel.
        private static Sound ServoDoor(Studio s, bool opening)
        {
            double seconds = DoorTravel;
            Func<double, double> envelope = Env.Swell(0.06, seconds - 0.16, 0.1);
            Func<double, double> whine = t => (opening ? 620 : 560) + 260 * Math.Sin(Math.PI * Math.Min(1, t / seconds));
            Sound door = s.Silence(seconds + 0.1);
            door.Mix(s.Tone(seconds, whine, Wave.Triangle).Shape(envelope), 0, 0.25);
            door.Mix(s.Tone(seconds, t => whine(t) * 0.5, Wave.Saw).LowPass(1200).Shape(envelope), 0, 0.2);
            door.Mix(s.Pink(seconds).BandPass(420, 1).Shape(envelope), 0, 0.3);
            return door.Room(0.4, 0.3, 0.12, damping: 0.5, tail: 0.15);
        }

        // A two-tone chirp and the magnetic lock clunking: falling as it locks, rising as it releases.
        private static Sound SecurityLock(Studio s, bool locking)
        {
            Sound chirp = s.Silence(0.6);
            double first = locking ? 1320 : 880, second = locking ? 880 : 1320;
            chirp.Mix(Chip(s, first, 0.09, Wave.Square, 0.5).LowPass(4000), 0, 0.6);
            chirp.Mix(Chip(s, second, 0.12, Wave.Square, 0.5).LowPass(4000), 0.09, 0.6);
            chirp.Mix(Thud(s, 0.3, 120, 70, 0.035), 0.2, 0.3);
            chirp.Mix(s.Burst(0.3, 0.002).Body(s.Metal(460, 10, 0.05)), 0.2, 0.1);
            return chirp.Room(0.4, 0.3, 0.12, damping: 0.5, tail: 0.15);
        }

        // The lift gate: a motor hauling a chain, its links clattering over the sprocket.
        private static Sound LiftGate(Studio s, bool opening)
        {
            double seconds = DoorTravel;
            Func<double, double> envelope = Env.Swell(0.1, seconds - 0.2, 0.1);
            Sound gate = s.Silence(seconds + 0.1);
            gate.Mix(s.Tone(seconds, opening ? 92 : 84, Wave.Saw).LowPass(500).Tremolo(_ => 5, 0.3).Shape(envelope), 0, 0.5);
            gate.Mix(s.Clicks(seconds, _ => 34, 0.2).Body(s.Metal(1250, 8, 0.015)).Resize(seconds).Shape(envelope), 0, 0.5);
            gate.Mix(s.Pink(seconds).BandPass(800, 1).Shape(envelope), 0, 0.3);
            return gate.Room(0.5, 0.35, 0.14, damping: 0.5, tail: 0.2);
        }

        // The exit gate releasing, with a lift bell ringing out: the way on is open.
        private static Sound ExitUnlock(Studio s)
        {
            Sound unlock = Bolts(s, false);
            unlock.Mix(Bell(s, 1046, 1.4, 0.6), 0.35, 0.45);
            unlock.Mix(Bell(s, 1318, 1.4, 0.6), 0.35, 0.3);
            return unlock.Resize(1.8).Fade(0, 0.3);
        }
    }
}
