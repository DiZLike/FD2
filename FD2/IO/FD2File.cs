// FD2File.cs
using System;
using System.IO;
using FD2.Models;

namespace FD2.IO
{
    public class FD2File
    {
        public static void Save(FD2Params params_, string filepath)
        {
            using (var writer = new BinaryWriter(File.Create(filepath)))
            {
                // Magic header "FD2\1"
                writer.Write(new byte[] { 0x46, 0x44, 0x32, 0x01 });

                // Header
                writer.Write(params_.OriginalLength);
                writer.Write(params_.SampleRate);
                writer.Write(params_.MDCTBlockSize);
                writer.Write(params_.MDCTSmallBlockSize);
                writer.Write(params_.Channels);

                int numBlocks = params_.AllCodes[0].Length;
                writer.Write(numBlocks);

                int totalCodes = 0;
                int totalFloors = 0;

                // Write all channels
                for (int c = 0; c < params_.Channels; c++)
                {
                    for (int i = 0; i < numBlocks; i++)
                    {
                        writer.Write(params_.BlockType[c][i]);

                        writer.Write(params_.AllCodes[c][i].Length);
                        writer.Write(params_.AllCodes[c][i]);
                        totalCodes += params_.AllCodes[c][i].Length;

                        writer.Write(params_.AllFloorPoints[c][i].Length);
                        writer.Write(params_.AllFloorPoints[c][i]);
                        totalFloors += params_.AllFloorPoints[c][i].Length;
                    }
                }

                Console.WriteLine($"[FD2] Saved: {filepath}");
                Console.WriteLine($"[FD2] Channels: {params_.Channels}, Blocks: {numBlocks}");
                Console.WriteLine($"[FD2] Codes: {totalCodes} bytes, Floors: {totalFloors} bytes");
                Console.WriteLine($"[FD2] Total: {totalCodes + totalFloors + 32 + numBlocks * params_.Channels} bytes");
            }
        }

        public static FD2Params Load(string filepath)
        {
            using (var reader = new BinaryReader(File.OpenRead(filepath)))
            {
                // Check magic header
                var magic = reader.ReadBytes(4);
                if (magic[0] != 0x46 || magic[1] != 0x44 || magic[2] != 0x32)
                    throw new FormatException("Not a valid FD2 file");

                if (magic[3] != 0x01)
                    Console.WriteLine($"[WARNING] FD2 version {magic[3]}, expected 1");

                // Read header
                var params_ = new FD2Params(
                    reader.ReadInt32(),  // OriginalLength
                    reader.ReadInt32(),  // SampleRate
                    reader.ReadInt32(),  // MDCTBlockSize
                    reader.ReadInt32(),  // MDCTSmallBlockSize
                    reader.ReadInt32()   // Channels
                );

                int numBlocks = reader.ReadInt32();

                // Initialize arrays
                params_.AllCodes = new byte[params_.Channels][][];
                params_.AllFloorPoints = new byte[params_.Channels][][];
                params_.BlockType = new bool[params_.Channels][];

                int totalCodes = 0;
                int totalFloors = 0;

                // Read all channels
                for (int c = 0; c < params_.Channels; c++)
                {
                    params_.AllCodes[c] = new byte[numBlocks][];
                    params_.AllFloorPoints[c] = new byte[numBlocks][];
                    params_.BlockType[c] = new bool[numBlocks];

                    for (int i = 0; i < numBlocks; i++)
                    {
                        params_.BlockType[c][i] = reader.ReadBoolean();

                        int codeLen = reader.ReadInt32();
                        params_.AllCodes[c][i] = reader.ReadBytes(codeLen);
                        totalCodes += codeLen;

                        int floorLen = reader.ReadInt32();
                        params_.AllFloorPoints[c][i] = reader.ReadBytes(floorLen);
                        totalFloors += floorLen;
                    }
                }

                Console.WriteLine($"[FD2] Loaded: {filepath}");
                Console.WriteLine($"[FD2] Channels: {params_.Channels}, Blocks: {numBlocks}");
                Console.WriteLine($"[FD2] Codes: {totalCodes} bytes, Floors: {totalFloors} bytes");

                return params_;
            }
        }

        public static long GetFileSize(string filepath)
        {
            return new FileInfo(filepath).Length;
        }
    }
}