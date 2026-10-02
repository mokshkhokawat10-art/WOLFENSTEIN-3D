using System;
using System.Collections.Generic;

namespace Wolfenstein.Editor.Synthesis
{
    /// <summary>
    /// Seeded randomness for the recipes, so every sound comes out the same each time it is generated.
    /// </summary>
    internal sealed class Rng
    {
        private readonly Random random;

        public Rng(int seed) => random = new Random(seed);

        /// <summary>A seed from a name that is the same on every machine and every run, unlike string.GetHashCode.</summary>
        public static int Seed(string name)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char c in name)
                    hash = (hash ^ c) * 16777619;
                return (int)(hash & 0x7fffffff);
            }
        }

        /// <summary>From 0 up to 1.</summary>
        public double Next() => random.NextDouble();

        public double Range(double min, double max) => min + (max - min) * random.NextDouble();

        /// <summary>From min up to but not including max.</summary>
        public int Int(int min, int max) => random.Next(min, max);

        /// <summary>From -1 to 1.</summary>
        public double Signed() => random.NextDouble() * 2 - 1;

        public bool Chance(double probability) => random.NextDouble() < probability;

        /// <summary>A normally distributed value with mean 0 and standard deviation 1.</summary>
        public double Gauss()
        {
            double u = 1 - random.NextDouble();
            double v = random.NextDouble();
            return Math.Sqrt(-2 * Math.Log(u)) * Math.Cos(2 * Math.PI * v);
        }

        public T Pick<T>(IReadOnlyList<T> items) => items[random.Next(items.Count)];
    }
}
