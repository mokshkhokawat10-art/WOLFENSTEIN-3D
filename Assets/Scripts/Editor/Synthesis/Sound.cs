using System;

namespace Wolfenstein.Editor.Synthesis
{
    /// <summary>
    /// A mono buffer of samples that the sound recipes build in, with the processing they shape it with: mixing, envelopes,
    /// filters, drive, echoes and a small room, pitch changes and seamless loops. Every step works in place and returns the
    /// sound, so steps chain. It uses nothing from the engine, so the recipes can also run outside the editor.
    /// </summary>
    internal sealed class Sound
    {
        public Sound(int sampleRate, double seconds)
            : this(sampleRate, new float[Math.Max(1, (int)Math.Round(seconds * sampleRate))])
        {
        }

        public Sound(int sampleRate, float[] samples)
        {
            SampleRate = sampleRate;
            Samples = samples;
        }

        public int SampleRate { get; }
        public float[] Samples { get; private set; }
        public int Length => Samples.Length;
        public double Duration => (double)Samples.Length / SampleRate;

        public Sound Copy() => new Sound(SampleRate, (float[])Samples.Clone());

        /// <summary>Adds another sound into this one, starting a number of seconds in, and grows this one to fit.</summary>
        public Sound Mix(Sound other, double at = 0, double gain = 1)
        {
            int offset = (int)Math.Round(at * SampleRate);
            if (offset + other.Length > Length)
                Resize(offset + other.Length);

            for (int i = 0; i < other.Length; i++)
            {
                int j = offset + i;
                if (j >= 0)
                    Samples[j] += (float)(other.Samples[i] * gain);
            }

            return this;
        }

        /// <summary>Cuts or extends the sound to a length in seconds.</summary>
        public Sound Resize(double seconds) => Resize((int)Math.Round(seconds * SampleRate));

        /// <summary>Adds silence to the end, for a tail to ring into.</summary>
        public Sound Pad(double seconds) => Resize(Length + (int)Math.Round(seconds * SampleRate));

        public Sound Gain(double gain)
        {
            for (int i = 0; i < Length; i++)
                Samples[i] = (float)(Samples[i] * gain);
            return this;
        }

        /// <summary>Scales every sample by an envelope over time in seconds.</summary>
        public Sound Shape(Func<double, double> envelope)
        {
            for (int i = 0; i < Length; i++)
                Samples[i] = (float)(Samples[i] * envelope((double)i / SampleRate));
            return this;
        }

        /// <summary>Soft clipping: loud parts are rounded off, adding grit, while the peak stays the same.</summary>
        public Sound Drive(double amount)
        {
            if (amount <= 0)
                return this;

            double norm = 1 / Math.Tanh(amount);
            for (int i = 0; i < Length; i++)
                Samples[i] = (float)(Math.Tanh(Samples[i] * amount) * norm);
            return this;
        }

        /// <summary>Uneven soft clipping, which adds the even harmonics of an overdriven valve.</summary>
        public Sound Warm(double amount)
        {
            for (int i = 0; i < Length; i++)
            {
                double x = Samples[i];
                Samples[i] = (float)Math.Tanh(x * amount + 0.25 * x * x * amount);
            }

            return this;
        }

        /// <summary>Fewer bits and a lower sample rate, for the grain of old hardware.</summary>
        public Sound Crush(int bits, int hold)
        {
            double steps = Math.Pow(2, bits - 1);
            float held = 0;
            for (int i = 0; i < Length; i++)
            {
                if (i % Math.Max(1, hold) == 0)
                    held = (float)(Math.Round(Samples[i] * steps) / steps);
                Samples[i] = held;
            }

            return this;
        }

        public Sound Normalize(double peak = 0.89)
        {
            double current = Peak();
            return current > 1e-9 ? Gain(peak / current) : this;
        }

        /// <summary>Raised-cosine fades at the start and end, in seconds.</summary>
        public Sound Fade(double fadeIn, double fadeOut)
        {
            int inLength = Math.Min(Length, (int)(fadeIn * SampleRate));
            int outLength = Math.Min(Length, (int)(fadeOut * SampleRate));
            for (int i = 0; i < inLength; i++)
                Samples[i] *= (float)(0.5 - 0.5 * Math.Cos(Math.PI * i / inLength));
            for (int i = 0; i < outLength; i++)
                Samples[Length - 1 - i] *= (float)(0.5 - 0.5 * Math.Cos(Math.PI * i / outLength));
            return this;
        }

        public Sound Filter(FilterType type, Func<double, double> cutoff, double q = 0.707)
        {
            var filter = new StateVariableFilter(SampleRate);
            for (int i = 0; i < Length; i++)
                Samples[i] = (float)filter.Process(Samples[i], type, cutoff((double)i / SampleRate), q);
            return this;
        }

        public Sound Filter(FilterType type, double cutoff, double q = 0.707)
        {
            var filter = new StateVariableFilter(SampleRate);
            for (int i = 0; i < Length; i++)
                Samples[i] = (float)filter.Process(Samples[i], type, cutoff, q);
            return this;
        }

        public Sound LowPass(double cutoff, double q = 0.707) => Filter(FilterType.LowPass, cutoff, q);
        public Sound LowPass(Func<double, double> cutoff, double q = 0.707) => Filter(FilterType.LowPass, cutoff, q);
        public Sound HighPass(double cutoff, double q = 0.707) => Filter(FilterType.HighPass, cutoff, q);
        public Sound HighPass(Func<double, double> cutoff, double q = 0.707) => Filter(FilterType.HighPass, cutoff, q);
        public Sound BandPass(double centre, double q) => Filter(FilterType.BandPass, centre, q);
        public Sound BandPass(Func<double, double> centre, double q) => Filter(FilterType.BandPass, centre, q);

        /// <summary>A boost or cut of a band, in decibels.</summary>
        public Sound Emphasis(double centre, double q, double decibels)
        {
            var filter = new StateVariableFilter(SampleRate);
            double amount = Math.Pow(10, decibels / 20) - 1;
            for (int i = 0; i < Length; i++)
            {
                double x = Samples[i];
                Samples[i] = (float)(x + amount * filter.Process(x, FilterType.BandPass, centre, q));
            }

            return this;
        }

        /// <summary>
        /// Several resonant bands in parallel, like the throat and mouth shaping a voice. Each band has its own centre over
        /// time, width in hertz and level.
        /// </summary>
        public Sound Formants(Func<double, double>[] centres, double[] widths, double[] levels)
        {
            var filters = new StateVariableFilter[centres.Length];
            for (int f = 0; f < filters.Length; f++)
                filters[f] = new StateVariableFilter(SampleRate);

            for (int i = 0; i < Length; i++)
            {
                double t = (double)i / SampleRate;
                double x = Samples[i];
                double sum = 0;
                for (int f = 0; f < filters.Length; f++)
                {
                    double centre = centres[f](t);
                    sum += levels[f] * filters[f].Process(x, FilterType.BandPass, centre, centre / widths[f]);
                }

                Samples[i] = (float)sum;
            }

            return this;
        }

        /// <summary>Removes any constant offset and the lowest rumble below hearing.</summary>
        public Sound DcBlock() => HighPass(18, 0.6);

        /// <summary>Amplitude wobble: depth 0 leaves the sound alone, 1 swings it fully on and off.</summary>
        public Sound Tremolo(Func<double, double> rate, double depth)
        {
            double phase = 0;
            for (int i = 0; i < Length; i++)
            {
                phase += rate((double)i / SampleRate) / SampleRate;
                Samples[i] *= (float)(1 - depth * 0.5 * (1 - Math.Cos(2 * Math.PI * phase)));
            }

            return this;
        }

        /// <summary>Multiplies by a sine, which turns every partial into a pair around it: a hollow, metallic, inhuman quality.</summary>
        public Sound Ring(Func<double, double> frequency, double mix)
        {
            double phase = 0;
            for (int i = 0; i < Length; i++)
            {
                phase += frequency((double)i / SampleRate) / SampleRate;
                double x = Samples[i];
                Samples[i] = (float)(x * (1 - mix) + x * Math.Sin(2 * Math.PI * phase) * mix);
            }

            return this;
        }

        /// <summary>
        /// Rings the sound through the resonances of a body, as a strike sets a plate, a pipe or a board ringing. Each mode
        /// answers an impulse with a decaying sine of its own level, and the tail they need is added to the end.
        /// </summary>
        public Sound Body(params Mode[] modes)
        {
            double longest = 0;
            foreach (Mode mode in modes)
                longest = Math.Max(longest, mode.Decay);
            Pad(Math.Min(4, longest * 5));

            var output = new double[Length];
            foreach (Mode mode in modes)
            {
                if (mode.Frequency >= SampleRate * 0.45)
                    continue;

                double w = 2 * Math.PI * mode.Frequency / SampleRate;
                double r = Math.Exp(-1 / (mode.Decay * SampleRate));
                double c1 = 2 * r * Math.Cos(w), c2 = -r * r, gain = Math.Sin(w) * mode.Level;
                double y1 = 0, y2 = 0;
                for (int i = 0; i < Length; i++)
                {
                    double y = Samples[i] * gain + c1 * y1 + c2 * y2;
                    y2 = y1;
                    y1 = y;
                    output[i] += y;
                }
            }

            for (int i = 0; i < Length; i++)
                Samples[i] = (float)output[i];
            return this;
        }

        /// <summary>A feedback comb tuned to a pitch, which makes anything played through it ring like a pipe or a plate.</summary>
        public Sound Resonate(Func<double, double> frequency, double feedback, double mix, double damping = 0.2)
        {
            int size = (int)(SampleRate / 20.0) + 4;
            var buffer = new double[size];
            int write = 0;
            double store = 0;
            for (int i = 0; i < Length; i++)
            {
                double delay = Math.Min(size - 2, SampleRate / Math.Max(20, frequency((double)i / SampleRate)));
                double read = write - delay;
                if (read < 0)
                    read += size;
                int a = (int)read;
                double frac = read - a;
                double delayed = buffer[a] * (1 - frac) + buffer[(a + 1) % size] * frac;
                store = delayed * (1 - damping) + store * damping;
                double x = Samples[i];
                buffer[write] = x + store * feedback;
                write = (write + 1) % size;
                Samples[i] = (float)(x * (1 - mix) + delayed * mix);
            }

            return this;
        }

        /// <summary>Repeats that fall away, each duller than the last, with the tail they need added to the end.</summary>
        public Sound Echo(double delay, double feedback, double wet, double tail, double damping = 0.3)
        {
            Pad(tail);
            int size = Math.Max(1, (int)(delay * SampleRate));
            var buffer = new double[size];
            int index = 0;
            double store = 0;
            for (int i = 0; i < Length; i++)
            {
                double delayed = buffer[index];
                store = delayed * (1 - damping) + store * damping;
                double x = Samples[i];
                buffer[index] = x + store * feedback;
                index = (index + 1) % size;
                Samples[i] = (float)(x + delayed * wet);
            }

            return this;
        }

        /// <summary>
        /// A small reverberant space after the style of Freeverb: parallel damped combs into a chain of all-passes. Size scales
        /// the room, decay sets how long it rings, and wet how much of it is heard beside the dry sound.
        /// </summary>
        public Sound Room(double size, double decay, double wet, double damping = 0.35, double tail = 0.6)
        {
            int[] combTunings = { 1116, 1188, 1277, 1356, 1422, 1491 };
            int[] allPassTunings = { 556, 441, 341 };
            Pad(tail);

            double scale = SampleRate / 44100.0 * Math.Max(0.1, size);
            var combs = new double[combTunings.Length][];
            var combIndex = new int[combTunings.Length];
            var combStore = new double[combTunings.Length];
            for (int c = 0; c < combs.Length; c++)
                combs[c] = new double[Math.Max(1, (int)(combTunings[c] * scale))];

            var passes = new double[allPassTunings.Length][];
            var passIndex = new int[allPassTunings.Length];
            for (int p = 0; p < passes.Length; p++)
                passes[p] = new double[Math.Max(1, (int)(allPassTunings[p] * SampleRate / 44100.0))];

            double feedback = 0.7 + 0.28 * Math.Clamp(decay, 0, 1);
            for (int i = 0; i < Length; i++)
            {
                double x = Samples[i];
                double input = x * 0.05;
                double sum = 0;
                for (int c = 0; c < combs.Length; c++)
                {
                    double[] line = combs[c];
                    double output = line[combIndex[c]];
                    combStore[c] = output * (1 - damping) + combStore[c] * damping;
                    line[combIndex[c]] = input + combStore[c] * feedback;
                    combIndex[c] = (combIndex[c] + 1) % line.Length;
                    sum += output;
                }

                for (int p = 0; p < passes.Length; p++)
                {
                    double[] line = passes[p];
                    double buffered = line[passIndex[p]];
                    line[passIndex[p]] = sum + buffered * 0.5;
                    passIndex[p] = (passIndex[p] + 1) % line.Length;
                    sum = buffered - sum;
                }

                Samples[i] = (float)(x * (1 - wet * 0.5) + sum * wet);
            }

            return this;
        }

        /// <summary>Plays the sound faster or slower, which also raises or lowers its pitch.</summary>
        public Sound Speed(double factor)
        {
            int length = Math.Max(1, (int)(Length / factor));
            var result = new float[length];
            for (int i = 0; i < length; i++)
                result[i] = (float)SampleAt(i * factor);
            Samples = result;
            return this;
        }

        /// <summary>Plays at a speed that changes over time, which bends the pitch as it goes.</summary>
        public Sound Warp(Func<double, double> speed)
        {
            var result = new System.Collections.Generic.List<float>(Length);
            double position = 0;
            while (position < Length - 1)
            {
                result.Add((float)SampleAt(position));
                position += speed(position / SampleRate);
            }

            Samples = result.ToArray();
            return this;
        }

        public Sound Reverse()
        {
            Array.Reverse(Samples);
            return this;
        }

        /// <summary>
        /// Makes the sound loop without a seam: its end is faded across into its start, which shortens it by the crossfade.
        /// </summary>
        public Sound Loop(double crossfade)
        {
            int fade = Math.Min(Length / 2, (int)(crossfade * SampleRate));
            int length = Length - fade;
            var result = new float[length];
            Array.Copy(Samples, result, length);
            for (int i = 0; i < fade; i++)
            {
                double t = (i + 0.5) / fade;
                double fadeIn = Math.Sin(t * Math.PI * 0.5);
                double fadeOut = Math.Cos(t * Math.PI * 0.5);
                result[i] = (float)(Samples[i] * fadeIn + Samples[length + i] * fadeOut);
            }

            Samples = result;
            return this;
        }

        /// <summary>
        /// Wraps the sound round a loop of the given length: anything past the end, such as a note's tail, is added back in at
        /// the start, so a rhythm loops without a seam.
        /// </summary>
        public Sound Fold(double loopSeconds)
        {
            int length = (int)Math.Round(loopSeconds * SampleRate);
            var result = new float[length];
            for (int i = 0; i < Length; i++)
                result[i % length] += Samples[i];
            Samples = result;
            return this;
        }

        public double Peak()
        {
            double peak = 0;
            foreach (float sample in Samples)
                peak = Math.Max(peak, Math.Abs(sample));
            return peak;
        }

        public double Rms()
        {
            double sum = 0;
            foreach (float sample in Samples)
                sum += sample * sample;
            return Math.Sqrt(sum / Math.Max(1, Length));
        }

        private Sound Resize(int length)
        {
            var result = new float[Math.Max(1, length)];
            Array.Copy(Samples, result, Math.Min(result.Length, Samples.Length));
            Samples = result;
            return this;
        }

        // Cubic interpolation between samples, for smooth pitch changes.
        private double SampleAt(double position)
        {
            int i = (int)Math.Floor(position);
            double t = position - i;
            double y0 = At(i - 1), y1 = At(i), y2 = At(i + 1), y3 = At(i + 2);
            double c1 = 0.5 * (y2 - y0);
            double c2 = y0 - 2.5 * y1 + 2 * y2 - 0.5 * y3;
            double c3 = 0.5 * (y3 - y0) + 1.5 * (y1 - y2);
            return ((c3 * t + c2) * t + c1) * t + y1;
        }

        private double At(int i) => i >= 0 && i < Length ? Samples[i] : 0;
    }
}
