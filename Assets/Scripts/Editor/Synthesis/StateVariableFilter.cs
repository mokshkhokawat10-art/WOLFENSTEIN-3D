using System;

namespace Wolfenstein.Editor.Synthesis
{
    internal enum FilterType
    {
        LowPass,
        HighPass,

        /// <summary>Passes a band around the centre at unity gain.</summary>
        BandPass,

        Notch
    }

    /// <summary>
    /// A two-pole filter that stays stable while its cutoff sweeps every sample (the trapezoidal state variable filter
    /// described by Andrew Simper). One filter gives low-pass, high-pass, band-pass or notch.
    /// </summary>
    internal sealed class StateVariableFilter
    {
        private readonly int sampleRate;
        private double state1;
        private double state2;
        private double lastCutoff = double.NaN;
        private double lastQ = double.NaN;
        private double k;
        private double a1;
        private double a2;
        private double a3;

        public StateVariableFilter(int sampleRate) => this.sampleRate = sampleRate;

        public double Process(double input, FilterType type, double cutoff, double q)
        {
            if (cutoff != lastCutoff || q != lastQ)
            {
                lastCutoff = cutoff;
                lastQ = q;
                double g = Math.Tan(Math.PI * Math.Clamp(cutoff, 5, sampleRate * 0.49) / sampleRate);
                k = 1 / Math.Max(0.05, q);
                a1 = 1 / (1 + g * (g + k));
                a2 = g * a1;
                a3 = g * a2;
            }

            double v3 = input - state2;
            double v1 = a1 * state1 + a2 * v3;
            double v2 = state2 + a2 * state1 + a3 * v3;
            state1 = 2 * v1 - state1;
            state2 = 2 * v2 - state2;

            return type switch
            {
                FilterType.LowPass => v2,
                FilterType.HighPass => input - k * v1 - v2,
                FilterType.BandPass => k * v1,
                _ => input - k * v1
            };
        }
    }
}
