// FloorQuantizer.cs - С АДАПТИВНЫМ КАЧЕСТВОМ, ГЛАДКОСТЬЮ И ЭКСТРЕМАЛЬНЫМ СЖАТИЕМ
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

                // ✅ КВАНТОВАНИЕ С АДАПТИВНОЙ ГЛАДКОСТЬЮ
                float normalized = coeffs[i] / baseFloor;
                normalized = Math.Clamp(normalized, -1.0f, 1.0f);

                if (_quality < 2.0f)
                {
                    // ✅ 4-БИТНОЕ КВАНТОВАНИЕ (16 уровней) + ИНТЕРПОЛЯЦИЯ
                    // Основное квантование
                    int levels16 = (int)((normalized + 1.0f) * 7.5f);
                    levels16 = Math.Clamp(levels16, 0, 15);
                    
                    // ✅ Добавляем информацию о "дробной части" в верхние биты
                    // Это позволит при декодировании сделать плавный переход
                    float fractional = ((normalized + 1.0f) * 7.5f) - levels16;
                    
                    // Кодируем: нижние 4 бита = основное значение, верхние 4 = дробная часть
                    int fractionalCode = (int)(fractional * 15f);
                    codes[i] = (byte)((levels16) | (fractionalCode << 4));
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
                // 4-БИТНОЕ ДЕКОДИРОВАНИЕ С ИНТЕРПОЛЯЦИЕЙ
                int level = code & 0x0F;  // Нижние 4 бита
                int fractionalCode = (code >> 4) & 0x0F;  // Верхние 4 бита
                
                // Восстанавливаем значение с дробной частью
                float fractional = fractionalCode / 15f;
                float continuousValue = level + fractional;  // 0-15.0 с плавным переходом
                
                normalized = (continuousValue / 7.5f) - 1.0f;
            }
            else if (_quality < 4.0f)
            {
                // 6-битное декодирование
                int level = code / 4;
                normalized = (level - 31.5f) / 31.5f;
            }
            else if (_quality < 7.0f)
            {
                // 7-битное декодирование
                int level = code / 2;
                normalized = (level - 63.5f) / 63.5f;
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
