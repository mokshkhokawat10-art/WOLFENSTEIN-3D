using System;
using System.IO;

namespace Wolfenstein.Editor.Synthesis
{
    /// <summary>Writes a sound as a 16-bit mono PCM wave file, dithered so quiet tails fade smoothly rather than in steps.</summary>
    internal static class WavFile
    {
        public static void Write(string path, Sound sound)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, Encode(sound));
        }

        public static byte[] Encode(Sound sound)
        {
            const int headerSize = 44;
            int dataSize = sound.Length * 2;
            var bytes = new byte[headerSize + dataSize];
            using var stream = new MemoryStream(bytes);
            using var writer = new BinaryWriter(stream);

            writer.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
            writer.Write(headerSize - 8 + dataSize);
            writer.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E' });
            writer.Write(new[] { (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(sound.SampleRate);
            writer.Write(sound.SampleRate * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
            writer.Write(dataSize);

            // Triangular dither of one step, from a fixed seed so the file is the same every time.
            var dither = new Random(sound.Length);
            foreach (float sample in sound.Samples)
            {
                double noise = dither.NextDouble() - dither.NextDouble();
                double value = Math.Round(sample * 32767.0 + noise);
                writer.Write((short)Math.Clamp(value, -32768, 32767));
            }

            return bytes;
        }
    }
}
