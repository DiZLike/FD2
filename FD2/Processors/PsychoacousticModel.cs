// PsychoacousticModel.cs - АДАПТИВНАЯ ПСИХОАКУСТИЧЕСКАЯ МОДЕЛЬ
using System;

namespace FD2.Processors
{
    public class PsychoacousticModel
    {
        private int _sampleRate;
        private int _fftSize;
        private float _freqPerBin;

        // Критические полосы (Bark scale) - 25 полос для 44.1 kHz
        private static readonly float[] BarkBoundaries = {
            0, 100, 200, 300, 400, 510, 630, 770, 920, 1080,
            1270, 1480, 1720, 2000, 2320, 2700, 3150, 3700, 4400,
            5300, 6400, 7700, 9500, 12000, 15500, 22050
        };

        public PsychoacousticModel(int sampleRate = 44100, int frameSize = 2048)
        {
            _sampleRate = sampleRate;
            _fftSize = frameSize;
            _freqPerBin = (float)sampleRate / frameSize;
        }

        public float[] ComputeMaskingThreshold(float[] spectrum)
        {
            int numBins = spectrum.Length;
            int numBands = BarkBoundaries.Length - 1;

            // 1. Вычисляем энергию в критических полосах
            var bandEnergy = new float[numBands];
            var bandCounts = new int[numBands];

            for (int i = 0; i < numBins; i++)
            {
                float freq = i * _freqPerBin;
                int band = FrequencyToBarkBand(freq);

                if (band >= 0 && band < numBands)
                {
                    float energy = spectrum[i] * spectrum[i];
                    bandEnergy[band] += energy;
                    bandCounts[band]++;
                }
            }

            // Усредняем энергию по полосам
            for (int b = 0; b < numBands; b++)
            {
                if (bandCounts[b] > 0)
                    bandEnergy[b] /= bandCounts[b];
                else
                    bandEnergy[b] = 1e-10f;
            }

            // 2. Распространение маскировки (spreading function)
            var spreadEnergy = new float[numBands];
            for (int b = 0; b < numBands; b++)
            {
                spreadEnergy[b] = bandEnergy[b];

                // Маскировка от низких частот к высоким (сильнее)
                for (int j = Math.Max(0, b - 3); j < b; j++)
                {
                    float spreadDb = 10 * (float)Math.Log10(Math.Max(bandEnergy[j], 1e-10f));
                    // -10 дБ на октаву + дополнительное ослабление
                    float attenuation = -24 - 10 * (b - j);
                    float maskDb = spreadDb + attenuation;
                    float maskEnergy = (float)Math.Pow(10, maskDb / 10);
                    spreadEnergy[b] = Math.Max(spreadEnergy[b], maskEnergy);
                }

                // Маскировка от высоких к низким (слабее)
                for (int j = b + 1; j < Math.Min(numBands, b + 2); j++)
                {
                    float spreadDb = 10 * (float)Math.Log10(Math.Max(bandEnergy[j], 1e-10f));
                    float attenuation = -30 - 15 * (j - b);
                    float maskDb = spreadDb + attenuation;
                    float maskEnergy = (float)Math.Pow(10, maskDb / 10);
                    spreadEnergy[b] = Math.Max(spreadEnergy[b], maskEnergy);
                }
            }

            // 3. Вычисляем пороги маскировки для каждого бина
            var thresholds = new float[numBins];

            for (int i = 0; i < numBins; i++)
            {
                float freq = i * _freqPerBin;
                int band = FrequencyToBarkBand(freq);

                if (band >= 0 && band < numBands)
                {
                    // Базовая маскировка: -6 дБ от энергии полосы
                    float maskEnergy = spreadEnergy[band] * 0.25f;

                    // Абсолютный порог слышимости
                    float absoluteThreshold = GetAbsoluteThreshold(freq);

                    // Выбираем максимум из маскировки и абсолютного порога
                    thresholds[i] = Math.Max(maskEnergy, absoluteThreshold);

                    // ✅ ВАЖНО: не давим высокие частоты слишком сильно
                    if (freq > 8000)
                    {
                        // Для высоких частот используем более мягкий порог
                        thresholds[i] = Math.Min(thresholds[i], absoluteThreshold * 10f);
                    }
                }
                else
                {
                    thresholds[i] = 1e-8f;
                }
            }

            return thresholds;
        }

        private int FrequencyToBarkBand(float freq)
        {
            // Преобразование частоты в Bark шкалу
            float bark = 13f * (float)Math.Atan(0.00076 * freq) +
                         3.5f * (float)Math.Atan(Math.Pow(freq / 7500f, 2));

            // Находим соответствующую критическую полосу
            for (int i = 0; i < BarkBoundaries.Length - 1; i++)
            {
                float barkLow = 13f * (float)Math.Atan(0.00076 * BarkBoundaries[i]) +
                                3.5f * (float)Math.Atan(Math.Pow(BarkBoundaries[i] / 7500f, 2));
                float barkHigh = 13f * (float)Math.Atan(0.00076 * BarkBoundaries[i + 1]) +
                                 3.5f * (float)Math.Atan(Math.Pow(BarkBoundaries[i + 1] / 7500f, 2));

                if (bark >= barkLow && bark < barkHigh)
                    return i;
            }

            return BarkBoundaries.Length - 2;
        }

        private float GetAbsoluteThreshold(float freq)
        {
            // Абсолютный порог слышимости (ISO 226) в линейных единицах
            float freqKhz = freq / 1000f;
            float db;

            if (freq < 20)
                db = 70f;
            else if (freq < 100)
                db = 60f - (freq - 20) / 80f * 15f;  // 60 -> 45 dB
            else if (freq < 200)
                db = 45f - (freq - 100) / 100f * 15f; // 45 -> 30 dB
            else if (freq < 1000)
                db = 30f - (freq - 200) / 800f * 20f; // 30 -> 10 dB
            else if (freq < 4000)
                db = 10f - (freq - 1000) / 3000f * 15f; // 10 -> -5 dB
            else if (freq < 8000)
                db = -5f + (freq - 4000) / 4000f * 15f; // -5 -> 10 dB
            else if (freq < 15000)
                db = 10f + (freq - 8000) / 7000f * 30f; // 10 -> 40 dB
            else
                db = 50f;

            // Переводим из dB SPL в линейные единицы (относительно полной шкалы)
            // -96 dB соответствует тишине в 16-битном аудио
            return (float)Math.Pow(10, (db - 96) / 20);
        }
    }
}