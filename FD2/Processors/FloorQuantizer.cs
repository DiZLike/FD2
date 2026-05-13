// FloorQuantizer.cs - С АДАПТИВНЫМ КАЧЕСТВОМ И ЛУЧШЕЙ ТОЧНОСТЬЮ
using System;

namespace FD2.Processors
{
    public class FloorQuantizer
    {
        private float _quality;
        private byte[] _quantBuffer;

        public FloorQuantizer(float quality = 5.0f)
        {
            _quality = quality;
            _quantBuffer = new byte[4096];
        }

        public (byte[] codes, float[] floor) Quantize(float[] coeffs, float[] maskingThreshold)
        {
            int length = coeffs.Length;
            var codes = new byte[length];
            var floor = new float[length];

            float psychoWeight = (10f - _quality) / 10f;

            // Адаптивное окно: больше при низком качестве
            int windowRadius = 2 + (int)((10f - _quality) * 3);

            for (int i = 0; i < length; i++)
            {
                int windowStart = Math.Max(0, i - windowRadius);
                int windowEnd = Math.Min(length - 1, i + windowRadius);

                float maxVal = 0.0001f;
                for (int j = windowStart; j <= windowEnd; j++)
                {
                    float absCoeff = Math.Abs(coeffs[j]);
                    if (absCoeff > maxVal)
                        maxVal = absCoeff;
                }

                float baseFloor = maxVal * 1.2f;

                // Психоакустика
                if (maskingThreshold != null && i < maskingThreshold.Length)
                {
                    float mask = maskingThreshold[i];
                    if (Math.Abs(coeffs[i]) < mask)
                    {
                        float boostedFloor = mask * (1.0f + psychoWeight * 3.0f);
                        if (boostedFloor > baseFloor)
                            baseFloor = boostedFloor;
                    }
                }

                floor[i] = baseFloor;

                // ✅ УЛУЧШЕННОЕ КВАНТОВАНИЕ: больше бит для лучшего качества
                float normalized = coeffs[i] / baseFloor;
                normalized = Math.Clamp(normalized, -1.0f, 1.0f);

                if (_quality < 2.0f)
                {
                    // 5-битное квантование (32 уровня)
                    int levels32 = (int)((normalized + 1.0f) * 15.5f);
                    levels32 = Math.Clamp(levels32, 0, 31);
                    codes[i] = (byte)(levels32 * 8);
                }
                else if (_quality < 4.0f)
                {
                    // 6-битное квантование (64 уровня)
                    int levels64 = (int)((normalized + 1.0f) * 31.5f);
                    levels64 = Math.Clamp(levels64, 0, 63);
                    codes[i] = (byte)(levels64 * 4);
                }
                else if (_quality < 7.0f)
                {
                    // 7-битное квантование (128 уровней)
                    int levels128 = (int)((normalized + 1.0f) * 63.5f);
                    levels128 = Math.Clamp(levels128, 0, 127);
                    codes[i] = (byte)(levels128 * 2);
                }
                else
                {
                    // 8-битное квантование (256 уровней)
                    int quantized = (int)((normalized + 1.0f) * 127.5f);
                    quantized = Math.Clamp(quantized, 0, 255);
                    codes[i] = (byte)quantized;
                }
            }

            return (codes, floor);
        }

        public (float value, float floor) Decode(byte code, float floor, float maxAmp)
        {
            float normalized;

            if (_quality < 2.0f)
            {
                int level = code / 8;
                normalized = (level - 15.5f) / 15.5f;
            }
            else if (_quality < 4.0f)
            {
                int level = code / 4;
                normalized = (level - 31.5f) / 31.5f;
            }
            else if (_quality < 7.0f)
            {
                int level = code / 2;
                normalized = (level - 63.5f) / 63.5f;
            }
            else
            {
                normalized = (code - 128.0f) / 128.0f;
            }

            float value = normalized * floor;
            return (value, floor);
        }
    }
}