using System;

namespace Wolfenstein.Editor.Synthesis
{
    internal enum Wave
    {
        Sine,
        Triangle,
        Saw,
        Square
    }

    /// <summary>One resonance of a struck body: its pitch, how long it rings and how loud it is.</summary>
    internal readonly struct Mode
    {
        public Mode(double frequency, double decay, double level)
        {
            Frequency = frequency;
            Decay = decay;
            Level = level;
        }

        public double Frequency { get; }

        /// <summary>Time constant of the ring's fall, in seconds.</summary>
        public double Decay { get; }

        public double Level { get; }
    }

    /// <summary>
    /// Where a recipe makes its raw material: noise, tones, voices, struck bodies and clicks, all at one sample rate and from
    /// one seeded source of randomness.
    /// </summary>
    internal sealed class Studio
    {
        // Mode ratios of a thin steel plate, which give metal its clang.
        private static readonly double[] PlateRatios = { 1, 1.59, 2.14, 2.3, 2.65, 2.92, 3.16, 3.5, 3.6, 4.07, 4.15, 4.62, 5.06, 5.4 };

        // Mode ratios of a wooden board or box.
        private static readonly double[] WoodRatios = { 1, 2.21, 3.0, 3.87, 4.9, 5.83 };

        public Studio(int sampleRate, Rng rng)
        {
            SampleRate = sampleRate;
            Rng = rng;
        }

        public int SampleRate { get; }
        public Rng Rng { get; }

        public Sound Silence(double seconds) => new Sound(SampleRate, seconds);

        public Sound White(double seconds)
        {
            Sound sound = Silence(seconds);
            for (int i = 0; i < sound.Length; i++)
                sound.Samples[i] = (float)Rng.Signed();
            return sound;
        }

        /// <summary>Noise with equal energy per octave, softer and fuller than white.</summary>
        public Sound Pink(double seconds)
        {
            Sound sound = Silence(seconds);
            double b0 = 0, b1 = 0, b2 = 0, b3 = 0, b4 = 0, b5 = 0, b6 = 0;
            for (int i = 0; i < sound.Length; i++)
            {
                double white = Rng.Signed();
                b0 = 0.99886 * b0 + white * 0.0555179;
                b1 = 0.99332 * b1 + white * 0.0750759;
                b2 = 0.96900 * b2 + white * 0.1538520;
                b3 = 0.86650 * b3 + white * 0.3104856;
                b4 = 0.55000 * b4 + white * 0.5329522;
                b5 = -0.7616 * b5 - white * 0.0168980;
                sound.Samples[i] = (float)((b0 + b1 + b2 + b3 + b4 + b5 + b6 + white * 0.5362) * 0.11);
                b6 = white * 0.115926;
            }

            return sound;
        }

        /// <summary>Deep rumbling noise, most of its energy in the lows.</summary>
        public Sound Brown(double seconds)
        {
            Sound sound = Silence(seconds);
            double value = 0;
            for (int i = 0; i < sound.Length; i++)
            {
                value = (value + Rng.Signed() * 0.02) * 0.998;
                sound.Samples[i] = (float)(value * 3.5);
            }

            return sound;
        }

        public Sound Tone(double seconds, double frequency, Wave wave = Wave.Sine, double width = 0.5) =>
            Tone(seconds, _ => frequency, wave, width);

        /// <summary>
        /// An oscillator whose pitch follows a curve. Saw and square are band-limited at their edges, so high notes do not
        /// alias; width sets the square's duty cycle.
        /// </summary>
        public Sound Tone(double seconds, Func<double, double> frequency, Wave wave = Wave.Sine, double width = 0.5)
        {
            Sound sound = Silence(seconds);
            double phase = Rng.Next();
            for (int i = 0; i < sound.Length; i++)
            {
                double step = Math.Clamp(frequency((double)i / SampleRate) / SampleRate, 0, 0.45);
                sound.Samples[i] = (float)Oscillate(phase, step, wave, width);
                phase += step;
                phase -= Math.Floor(phase);
            }

            return sound;
        }

        /// <summary>Several copies of a tone detuned about its pitch, for thick pads and strings.</summary>
        public Sound Chorus(double seconds, Func<double, double> frequency, Wave wave, int voices, double detuneCents)
        {
            Sound sound = Silence(seconds);
            for (int v = 0; v < voices; v++)
            {
                double cents = voices > 1 ? detuneCents * (2.0 * v / (voices - 1) - 1) : 0;
                double ratio = Math.Pow(2, cents / 1200);
                sound.Mix(Tone(seconds, t => frequency(t) * ratio, wave), 0, 1.0 / voices);
            }

            return sound;
        }

        /// <summary>
        /// Frequency modulation: a carrier whose phase is pushed around by a modulator at a ratio of its pitch. Low indices
        /// give soft tones, high ones bells, brass and clangs.
        /// </summary>
        public Sound Fm(double seconds, Func<double, double> carrier, double ratio, Func<double, double> index)
        {
            Sound sound = Silence(seconds);
            double carrierPhase = 0, modulatorPhase = 0;
            for (int i = 0; i < sound.Length; i++)
            {
                double t = (double)i / SampleRate;
                double frequency = carrier(t);
                modulatorPhase += frequency * ratio / SampleRate;
                double modulation = Math.Sin(2 * Math.PI * modulatorPhase) * index(t);
                carrierPhase += frequency / SampleRate;
                sound.Samples[i] = (float)Math.Sin(2 * Math.PI * carrierPhase + modulation);
            }

            return sound;
        }

        /// <summary>
        /// The buzz of vocal folds: a train of glottal pulses at a pitch that follows a curve, with jitter in each period,
        /// shimmer in each pulse's strength, breath, and optionally a subharmonic in which every other pulse falls weaker
        /// and later, the rough doubled growl of a creature's throat. Shape it with formants to make it speak.
        /// </summary>
        public Sound Glottal(double seconds, Func<double, double> pitch, double jitter = 0.01, double shimmer = 0.05,
            double subharmonic = 0, double breath = 0.05)
        {
            Sound sound = Silence(seconds);
            double phase = 0, previousFlow = 0;
            double periodScale = 1, pulseLevel = 1;
            bool odd = false;
            for (int i = 0; i < sound.Length; i++)
            {
                double frequency = Math.Max(10, pitch((double)i / SampleRate)) * periodScale;
                phase += frequency / SampleRate;
                if (phase >= 1)
                {
                    phase -= Math.Floor(phase);
                    odd = !odd;
                    periodScale = (1 + jitter * Rng.Gauss()) * (odd ? 1 + subharmonic * 0.2 : 1 - subharmonic * 0.2);
                    pulseLevel = Math.Max(0, 1 + shimmer * Rng.Gauss()) * (odd ? 1 - subharmonic : 1);
                }

                double flow = GlottalFlow(phase);
                double derivative = (flow - previousFlow) * SampleRate / frequency;
                previousFlow = flow;
                sound.Samples[i] = (float)(derivative * pulseLevel * 0.25 + Rng.Signed() * breath * (0.3 + flow));
            }

            return sound;
        }

        /// <summary>Sparse random impulses at about the given rate per second, each of random strength and sign.</summary>
        public Sound Clicks(double seconds, Func<double, double> rate, double spread = 0.5)
        {
            Sound sound = Silence(seconds);
            for (int i = 0; i < sound.Length; i++)
            {
                if (Rng.Next() < rate((double)i / SampleRate) / SampleRate)
                    sound.Samples[i] = (float)((1 - spread * Rng.Next()) * (Rng.Chance(0.5) ? 1 : -1));
            }

            return sound;
        }

        /// <summary>A single sharp impulse at a time in seconds, the strike that sets a body ringing.</summary>
        public Sound Impulse(double seconds, double at = 0, double level = 1)
        {
            Sound sound = Silence(seconds);
            int index = Math.Min(sound.Length - 1, (int)(at * SampleRate));
            sound.Samples[index] = (float)level;
            return sound;
        }

        /// <summary>A short burst of noise, a more natural strike than a single impulse.</summary>
        public Sound Burst(double seconds, double length)
        {
            Sound sound = White(seconds);
            return sound.Shape(t => t < length ? Math.Exp(-t / (length * 0.3)) : 0);
        }

        /// <summary>Modes of a steel plate from a fundamental: higher modes ring shorter, and each is nudged off true.</summary>
        public Mode[] Metal(double fundamental, int count, double decay, double detune = 0.03)
        {
            var modes = new Mode[Math.Min(count, PlateRatios.Length)];
            for (int i = 0; i < modes.Length; i++)
            {
                double frequency = fundamental * PlateRatios[i] * (1 + detune * Rng.Signed());
                modes[i] = new Mode(frequency, decay * Math.Pow(fundamental / frequency, 0.6), Rng.Range(0.35, 1));
            }

            return modes;
        }

        /// <summary>Modes of a wooden board or box: few, quickly damped.</summary>
        public Mode[] Wood(double fundamental, double decay)
        {
            var modes = new Mode[WoodRatios.Length];
            for (int i = 0; i < modes.Length; i++)
            {
                double frequency = fundamental * WoodRatios[i] * (1 + 0.04 * Rng.Signed());
                modes[i] = new Mode(frequency, decay / (1 + i * 0.6), Rng.Range(0.4, 1) / (1 + i * 0.4));
            }

            return modes;
        }

        private static double Oscillate(double phase, double step, Wave wave, double width)
        {
            switch (wave)
            {
                case Wave.Triangle:
                    return 1 - 4 * Math.Abs(phase - 0.5);
                case Wave.Saw:
                    return 2 * phase - 1 - PolyBlep(phase, step);
                case Wave.Square:
                    double square = phase < width ? 1 : -1;
                    return square + PolyBlep(phase, step) - PolyBlep((phase - width + 1) % 1, step);
                default:
                    return Math.Sin(2 * Math.PI * phase);
            }
        }

        // Smooths the jump of a saw or square across one sample either side of it, which removes most of the aliasing.
        private static double PolyBlep(double phase, double step)
        {
            if (step <= 0)
                return 0;
            if (phase < step)
            {
                double x = phase / step;
                return x + x - x * x - 1;
            }

            if (phase > 1 - step)
            {
                double x = (phase - 1) / step;
                return x * x + x + x + 1;
            }

            return 0;
        }

        // The air let through the vocal folds over one period: a smooth opening, a quicker closing, then shut.
        private static double GlottalFlow(double phase)
        {
            const double opening = 0.4, closing = 0.18;
            if (phase < opening)
                return 0.5 * (1 - Math.Cos(Math.PI * phase / opening));
            if (phase < opening + closing)
                return Math.Cos(0.5 * Math.PI * (phase - opening) / closing);
            return 0;
        }
    }
}
