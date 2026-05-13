// FD2Params.cs
using System;

namespace FD2.Models
{
    public class FD2Params
    {
        public int OriginalLength { get; set; }
        public int SampleRate { get; set; }
        public int MDCTBlockSize { get; set; }
        public int MDCTSmallBlockSize { get; set; }
        public int Channels { get; set; }

        // Compressed audio data
        public byte[][][] AllCodes { get; set; }         // [channel][block][codes]
        public byte[][][] AllFloorPoints { get; set; }   // [channel][block][floor]
        public bool[][] BlockType { get; set; }          // [channel][block]

        public FD2Params(int originalLength, int sampleRate,
                        int mdctBlockSize, int mdctSmallBlockSize, int channels = 2)
        {
            OriginalLength = originalLength;
            SampleRate = sampleRate;
            MDCTBlockSize = mdctBlockSize;
            MDCTSmallBlockSize = mdctSmallBlockSize;
            Channels = channels;
        }

        public int GetCompressedSize()
        {
            int size = 0;

            if (AllCodes != null)
                foreach (var channel in AllCodes)
                    foreach (var block in channel)
                        size += block.Length;

            if (AllFloorPoints != null)
                foreach (var channel in AllFloorPoints)
                    foreach (var block in channel)
                        size += block.Length;

            if (BlockType != null)
                foreach (var channel in BlockType)
                    size += channel.Length;

            return size;
        }
    }
}