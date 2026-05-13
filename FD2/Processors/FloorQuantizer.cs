// FloorQuantizer.cs - С АДАПТИВНЫМ КАЧЕСТВОМ
using System;

namespace FD2.Processors
{
    public class FloorQuantizer
    {
        private float _quality;

        public FloorQuantizer(float quality = 5.0f)
        {
            _quality = quality;
        }

        public (byte[] codes, float[] floor) Quantize(float[] coeffs, float[] maskingThreshold)
        {
            int length = coeffs.Length;
            var codes = new byte[length];
            var floor = new float[length];

            // Адаптивная чувствительность
            float psychoWeight = (10f - _quality) / 10f;  // 0.0 .. 1.0

            // При низком качестве — увеличиваем окно усреднения floor
            int windowRadius = 2 + (int)((10f - _quality) * 3); // quality 10→2, quality 1→29

            for (int i = 0; i < length; i++)
            {
                // Адаптивное окно для floor
                int windowStart = Math.Max(0, i - windowRadius);
                int windowEnd = Math.Min(length - 1, i + windowRadius);

                float maxVal = 0.0001f;
                for (int j = windowStart; j <= windowEnd; j++)
                {
                    maxVal = Math.Max(maxVal, Math.Abs(coeffs[j]));
                }

                // Базовый floor
                float baseFloor = maxVal * 1.2f;

                // Психоакустика (только для повышения floor)
                if (maskingThreshold != null && i < maskingThreshold.Length)
                {
                    float mask = maskingThreshold[i];
                    if (Math.Abs(coeffs[i]) < mask)
                    {
                        float boostedFloor = mask * (1.0f + psychoWeight * 3.0f);
                        baseFloor = Math.Max(baseFloor, boostedFloor);
                    }
                }

                floor[i] = baseFloor;

                // ✅ КВАНТУЕМ С АДАПТИВНЫМ КАЧЕСТВОМ
                float normalized = coeffs[i] / baseFloor;
                normalized = Math.Clamp(normalized, -1.0f, 1.0f);

                // При низком качестве — грубое квантование (меньше бит)
                if (_quality < 3.0f)
                {
                    // 4-битное квантование (16 уровней) для quality 1-3
                    int levels16 = (int)((normalized + 1.0f) * 7.5f); // 0..15
                    levels16 = Math.Clamp(levels16, 0, 15);
                    codes[i] = (byte)(levels16 * 17); // масштабируем в 0..255
                }
                else if (_quality < 6.0f)
                {
                    // 6-битное квантование (64 уровня) для quality 3-6
                    int levels64 = (int)((normalized + 1.0f) * 31.5f); // 0..63
                    levels64 = Math.Clamp(levels64, 0, 63);
                    codes[i] = (byte)(levels64 * 4); // масштабируем в 0..255
                }
                else
                {
                    // 8-битное квантование (256 уровней) для quality 6-10
                    int quantized = (int)((normalized + 1.0f) * 127.5f);
                    quantized = Math.Clamp(quantized, 0, 255);
                    codes[i] = (byte)quantized;
                }
            }

            return (codes, floor);
        }

        public (float value, float floor) Decode(byte code, float floor, float maxAmp)
        {
            // Декодируем обратно в зависимости от качества
            float normalized;

            if (_quality < 3.0f)
            {
                // 4-битное декодирование
                int level = code / 17;
                normalized = (level - 7.5f) / 7.5f;
            }
            else if (_quality < 6.0f)
            {
                // 6-битное декодирование
                int level = code / 4;
                normalized = (level - 31.5f) / 31.5f;
            }
            else
            {
                // 8-битное декодирование
                normalized = (code - 128.0f) / 128.0f;
            }

            float value = normalized * floor;
            return (value, floor);
        }
    }
}