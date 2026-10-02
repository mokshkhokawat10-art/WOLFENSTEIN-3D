using System;

namespace Wolfenstein.Editor.Synthesis
{
    /// <summary>
    /// Curves over time, in seconds, for envelopes and for anything else that moves: a pitch sliding, a filter opening.
    /// </summary>
    internal static class Env
    {
        public static Func<double, double> Constant(double value) => _ => value;

        /// <summary>
        /// A quick rise over the attack, then an exponential fall with the given time constant, eased to exactly nothing by
        /// six time constants in, so a sound cut short after that ends without a click.
        /// </summary>
        public static Func<double, double> Hit(double attack, double decay) => t =>
        {
            if (t < attack)
                return Smooth(t / attack);
            double fall = (t - attack) / decay;
            return Math.Exp(-fall) * (1 - Smooth((fall - 4) / 2));
        };

        /// <summary>Rises, holds, then falls, each with a smooth curve.</summary>
        public static Func<double, double> Swell(double attack, double hold, double release) => t =>
        {
            if (t < attack)
                return Smooth(t / attack);
            if (t < attack + hold)
                return 1;
            return 1 - Smooth(Math.Min(1, (t - attack - hold) / release));
        };

        /// <summary>Straight lines between points given as time, value, time, value and so on. Flat beyond the ends.</summary>
        public static Func<double, double> Line(params double[] points) => t => Interpolate(points, t, false);

        /// <summary>
        /// Like <see cref="Line"/>, but moving evenly in ratio rather than in value, as the ear hears a pitch or a cutoff
        /// slide. Every value must be above zero.
        /// </summary>
        public static Func<double, double> Glide(params double[] points) => t => Interpolate(points, t, true);

        /// <summary>A value that wanders smoothly and at random about 1, by up to the depth, changing at about the rate in hertz.</summary>
        public static Func<double, double> Wander(Rng rng, double depth, double rate, double seconds)
        {
            int count = Math.Max(2, (int)(seconds * rate) + 3);
            var points = new double[count];
            for (int i = 0; i < count; i++)
                points[i] = rng.Signed();

            return t =>
            {
                double position = t * rate;
                int i = Math.Min(count - 2, Math.Max(0, (int)position));
                double f = Smooth(Math.Clamp(position - i, 0, 1));
                return 1 + depth * (points[i] + (points[i + 1] - points[i]) * f);
            };
        }

        public static double Smooth(double x)
        {
            x = Math.Clamp(x, 0, 1);
            return x * x * (3 - 2 * x);
        }

        private static double Interpolate(double[] points, double t, bool ratio)
        {
            if (t <= points[0])
                return points[1];

            for (int i = 2; i < points.Length; i += 2)
            {
                if (t > points[i])
                    continue;

                double t0 = points[i - 2], v0 = points[i - 1], t1 = points[i], v1 = points[i + 1];
                double f = t1 > t0 ? (t - t0) / (t1 - t0) : 1;
                return ratio ? v0 * Math.Pow(v1 / v0, f) : v0 + (v1 - v0) * f;
            }

            return points[points.Length - 1];
        }
    }
}
