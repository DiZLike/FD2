// Program.cs
using System;
using System.IO;
using FD2.Processors;

namespace FD2
{
    class Program
    {
        // Program.cs - добавьте после декодирования
        static void Main(string[] args)
        {
            Console.WriteLine("═══════════════════════════════════════");
            Console.WriteLine("       FD2 MDCT Audio Codec");
            Console.WriteLine("═══════════════════════════════════════\n");

            string inputFile = "test.wav";
            if (!File.Exists(inputFile))
            {
                Console.WriteLine($"Error: {inputFile} not found!");
                return;
            }

            var codec = new FD2Codec(
                sampleRate: 44100,
                mdctBlockSize: 2048,
                mdctSmallBlockSize: 256,
                quality: 1.0f
            );

            // Encode
            Console.WriteLine($"\n1. Encoding {inputFile}...");
            string fd2File = codec.ConvertToFD2(inputFile);

            var originalSize = new FileInfo(inputFile).Length;
            var fd2Size = new FileInfo(fd2File).Length;

            Console.WriteLine($"\n═══════════════════════════════════════");
            Console.WriteLine($"         Compression Results");
            Console.WriteLine($"═══════════════════════════════════════");
            Console.WriteLine($"Original:  {originalSize / 1024.0,8:F2} KB");
            Console.WriteLine($"FD2:       {fd2Size / 1024.0,8:F2} KB");
            Console.WriteLine($"Ratio:     {(float)originalSize / fd2Size,8:F2}x");
            Console.WriteLine($"Saved:     {(1 - (float)fd2Size / originalSize) * 100,8:F1}%");
            Console.WriteLine($"═══════════════════════════════════════");

            // Decode
            Console.WriteLine($"\n2. Decoding...");
            string wavFile = codec.ConvertFromFD2(fd2File);
            Console.WriteLine($"✓ Restored: {wavFile}");

            var restoredSize = new FileInfo(wavFile).Length;
            Console.WriteLine($"\nRestored WAV: {restoredSize / 1024.0:F2} KB");

            // ✅ ДОБАВЬТЕ ЭТО - проверка качества реконструкции
            Console.WriteLine($"\n3. Validating reconstruction quality...");
            codec.ValidateReconstruction(inputFile, wavFile);
        }
    }
}