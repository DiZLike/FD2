// WavFile.cs
using System;
using System.IO;
using System.Linq;

namespace FD2.IO
{
    public class WavFile
    {
        public int SampleRate { get; set; }
        public int BitsPerSample { get; set; }
        public int Channels { get; set; }
        public int SampleCount { get; set; }
        public float[][] AudioData { get; set; }

        public static WavFile Read(string filepath)
        {
            using (var reader = new BinaryReader(File.OpenRead(filepath)))
            {
                // RIFF header
                if (new string(reader.ReadChars(4)) != "RIFF")
                    throw new FormatException("Not a WAV file");

                reader.ReadInt32(); // File size
                if (new string(reader.ReadChars(4)) != "WAVE")
                    throw new FormatException("Not a WAV file");

                // Find fmt chunk
                while (new string(reader.ReadChars(4)) != "fmt ")
                {
                    int skipSize = reader.ReadInt32();
                    reader.ReadBytes(skipSize);
                }

                int fmtSize = reader.ReadInt32();
                short audioFormat = reader.ReadInt16();
                short channels = reader.ReadInt16();
                int sampleRate = reader.ReadInt32();
                reader.ReadInt32(); // Byte rate
                reader.ReadInt16(); // Block align
                short bitsPerSample = reader.ReadInt16();

                if (fmtSize > 16)
                    reader.ReadBytes(fmtSize - 16);

                // Find data chunk
                while (new string(reader.ReadChars(4)) != "data")
                {
                    int skipSize = reader.ReadInt32();
                    reader.ReadBytes(skipSize);
                }

                int dataSize = reader.ReadInt32();
                int bytesPerSample = bitsPerSample / 8;
                int totalSamples = dataSize / bytesPerSample;
                int samplesPerChannel = totalSamples / channels;

                Console.WriteLine($"[WAV] Read: {channels}ch, {sampleRate}Hz, {bitsPerSample}bit, {samplesPerChannel} samples");

                byte[] audioData = reader.ReadBytes(dataSize);
                var result = new float[channels][];
                for (int c = 0; c < channels; c++)
                    result[c] = new float[samplesPerChannel];

                if (bitsPerSample == 16)
                {
                    for (int i = 0; i < samplesPerChannel; i++)
                    {
                        for (int c = 0; c < channels; c++)
                        {
                            int offset = (i * channels + c) * 2;
                            if (offset + 1 < audioData.Length)
                            {
                                short sample = BitConverter.ToInt16(audioData, offset);
                                result[c][i] = sample / 32768f;
                            }
                        }
                    }
                }
                else if (bitsPerSample == 24)
                {
                    for (int i = 0; i < samplesPerChannel; i++)
                    {
                        for (int c = 0; c < channels; c++)
                        {
                            int offset = (i * channels + c) * 3;
                            int sample = audioData[offset] | (audioData[offset + 1] << 8) | (audioData[offset + 2] << 16);
                            if ((sample & 0x800000) != 0)
                                sample |= unchecked((int)0xFF000000);
                            result[c][i] = sample / 8388608f;
                        }
                    }
                }

                return new WavFile
                {
                    SampleRate = sampleRate,
                    BitsPerSample = bitsPerSample,
                    Channels = channels,
                    SampleCount = samplesPerChannel,
                    AudioData = result
                };
            }
        }

        public void Save(string filepath)
        {
            using (var writer = new BinaryWriter(File.Create(filepath)))
            {
                int bitsPerSample = 16;
                int dataSize = SampleCount * Channels * (bitsPerSample / 8);

                // RIFF header
                writer.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
                writer.Write(36 + dataSize);
                writer.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E' });

                // fmt chunk
                writer.Write(new[] { (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
                writer.Write(16);
                writer.Write((short)1); // PCM
                writer.Write((short)Channels);
                writer.Write(SampleRate);
                writer.Write(SampleRate * Channels * bitsPerSample / 8);
                writer.Write((short)(Channels * bitsPerSample / 8));
                writer.Write((short)bitsPerSample);

                // data chunk
                writer.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
                writer.Write(dataSize);

                for (int i = 0; i < SampleCount; i++)
                {
                    for (int c = 0; c < Channels; c++)
                    {
                        float sample = Math.Clamp(AudioData[c][i], -1f, 1f);
                        short shortSample = (short)(sample * 32767);
                        writer.Write(shortSample);
                    }
                }
            }

            Console.WriteLine($"[WAV] Saved: {filepath}, {Channels}ch, {SampleCount} samples");
        }
    }
}