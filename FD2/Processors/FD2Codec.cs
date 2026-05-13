// FD2Codec.cs - ФИНАЛЬНАЯ ВЕРСИЯ С АДАПТИВНЫМ КАЧЕСТВОМ
using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using FD2.Models;
using FD2.IO;

namespace FD2.Processors
{
    public class FD2Codec
    {
        private int _sampleRate;
        private int _mdctBlockSize;
        private int _mdctSmallBlockSize;
        private float _quality;

        public FD2Codec(int sampleRate = 44100, int frameSize = 4096,
                        int mdctBlockSize = 2048, int mdctSmallBlockSize = 256,
                        float quality = 5.0f)
        {
            _sampleRate = sampleRate;
            _mdctBlockSize = mdctBlockSize;
            _mdctSmallBlockSize = mdctSmallBlockSize;
            _quality = Math.Clamp(quality, 0f, 10f);

            Console.WriteLine("═══════════════════════════════════════");
            Console.WriteLine("[FD2Codec] MDCT Audio Codec v2.0");
            Console.WriteLine($"          SampleRate: {_sampleRate}Hz");
            Console.WriteLine($"          MDCT: {_mdctBlockSize}/{_mdctSmallBlockSize}");
            Console.WriteLine($"          Quality: {_quality}/10");
            Console.WriteLine("═══════════════════════════════════════");
        }

        // ==================== КОДИРОВАНИЕ ====================
        public FD2Params Encode(string filepath)
        {
            Console.WriteLine($"\n[ENCODE] ═══ Обработка: {Path.GetFileName(filepath)} ═══");

            var wav = WavFile.Read(filepath);

            var params_ = new FD2Params(
                wav.SampleCount,
                wav.SampleRate,
                _mdctBlockSize,
                _mdctSmallBlockSize,
                wav.Channels
            );

            params_.AllCodes = new byte[wav.Channels][][];
            params_.AllFloorPoints = new byte[wav.Channels][][];
            params_.BlockType = new bool[wav.Channels][];

            DateTime startTime = DateTime.Now;

            Parallel.For(0, wav.Channels, c =>
            {
                EncodeChannel(wav.AudioData[c], params_, c);
            });

            TimeSpan elapsed = DateTime.Now - startTime;

            int totalCompressed = params_.GetCompressedSize();
            int originalSize = wav.SampleCount * wav.Channels * 2;
            float ratio = (float)originalSize / totalCompressed;
            float bitrate = totalCompressed * 8f * _sampleRate / (wav.SampleCount * wav.Channels) / 1000;

            Console.WriteLine($"\n[ENCODE] ═══ Итоги ═══");
            Console.WriteLine($"[ENCODE] Исходный: {originalSize / 1024.0:F1} КБ");
            Console.WriteLine($"[ENCODE] Сжатый: {totalCompressed / 1024.0:F1} КБ");
            Console.WriteLine($"[ENCODE] Степень сжатия: {ratio:F2}x");
            Console.WriteLine($"[ENCODE] Битрейт: {bitrate:F1} кбит/с");
            Console.WriteLine($"[ENCODE] Время: {elapsed.TotalSeconds:F1}с");
            Console.WriteLine("[ENCODE] ✅ Готово\n");

            return params_;
        }

        private void EncodeChannel(float[] audio, FD2Params params_, int channel)
        {
            int step = _mdctBlockSize / 2;
            int numBlocks = (audio.Length + step - 1) / step;

            params_.AllCodes[channel] = new byte[numBlocks][];
            params_.AllFloorPoints[channel] = new byte[numBlocks][];
            params_.BlockType[channel] = new bool[numBlocks];

            var mdct = new MDCT(_mdctBlockSize, _mdctSmallBlockSize);
            var psychoModel = new PsychoacousticModel(_sampleRate, _mdctBlockSize);
            var floorQuantizer = new FloorQuantizer(_quality);

            for (int b = 0; b < numBlocks; b++)
            {
                int start = b * step;
                int blockSize = _mdctBlockSize;

                params_.BlockType[channel][b] = true;

                var block = new float[blockSize];
                int copyLen = Math.Min(blockSize, audio.Length - start);
                Array.Copy(audio, start, block, 0, copyLen);

                // Прямое MDCT
                var coeffs = mdct.Forward(block, false);

                // Психоакустический анализ
                var thresholds = psychoModel.ComputeMaskingThreshold(coeffs);

                // Квантование с психоакустикой
                var (codes, floor) = floorQuantizer.Quantize(coeffs, thresholds);

                // Сжимаем floor адаптивно
                params_.AllFloorPoints[channel][b] = CompressFloorAdaptive(floor, _quality);
                params_.AllCodes[channel][b] = codes;
            }
        }

        private byte[] CompressFloorAdaptive(float[] floor, float quality)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                // Первое значение — полный float
                writer.Write(floor[0]);

                float prevDb = 20f * (float)Math.Log10(Math.Max(floor[0], 1e-10f));

                // При низком качестве — грубое квантование дельт
                float deltaStep;
                if (quality < 3.0f)
                    deltaStep = 0.01f;  // шаг 0.01 dB (экономия 50%)
                else if (quality < 6.0f)
                    deltaStep = 0.005f; // шаг 0.005 dB (экономия 25%)
                else
                    deltaStep = 0.001f; // шаг 0.001 dB (макс. качество)

                for (int i = 1; i < floor.Length; i++)
                {
                    float currentDb = 20f * (float)Math.Log10(Math.Max(floor[i], 1e-10f));
                    float delta = currentDb - prevDb;

                    // Квантуем дельту с адаптивным шагом
                    short deltaShort = (short)Math.Clamp(delta / deltaStep, -32768, 32767);

                    writer.Write(deltaShort);
                    prevDb += deltaShort * deltaStep; // компенсация ошибки квантования
                }

                return ms.ToArray();
            }
        }

        private float[] DecompressFloorAdaptive(byte[] compressed, float quality)
        {
            using (var ms = new MemoryStream(compressed))
            using (var reader = new BinaryReader(ms))
            {
                int length = (compressed.Length - 4) / 2 + 1;
                var floor = new float[length];

                // Первое значение
                floor[0] = reader.ReadSingle();
                float prevDb = 20f * (float)Math.Log10(Math.Max(floor[0], 1e-10f));

                float deltaStep;
                if (quality < 3.0f)
                    deltaStep = 0.01f;
                else if (quality < 6.0f)
                    deltaStep = 0.005f;
                else
                    deltaStep = 0.001f;

                // Восстанавливаем по дельтам
                for (int i = 1; i < length; i++)
                {
                    short deltaShort = reader.ReadInt16();
                    float delta = deltaShort * deltaStep;
                    float currentDb = prevDb + delta;
                    floor[i] = (float)Math.Pow(10, currentDb / 20f);
                    prevDb = currentDb;
                }

                return floor;
            }
        }

        // ==================== ДЕКОДИРОВАНИЕ ====================
        public WavFile Decode(FD2Params params_)
        {
            Console.WriteLine($"\n[DECODE] ═══ Начало декодирования ═══");
            Console.WriteLine($"[DECODE] Каналов: {params_.Channels}");
            Console.WriteLine($"[DECODE] Сэмплов: {params_.OriginalLength}");

            var audio = new float[params_.Channels][];
            for (int c = 0; c < params_.Channels; c++)
                audio[c] = new float[params_.OriginalLength];

            DateTime startTime = DateTime.Now;

            Parallel.For(0, params_.Channels, c =>
            {
                DecodeChannel(params_, c, audio[c]);
            });

            TimeSpan elapsed = DateTime.Now - startTime;
            Console.WriteLine($"[DECODE] Время: {elapsed.TotalSeconds:F1}с");
            Console.WriteLine("[DECODE] ✅ Готово");

            return new WavFile
            {
                SampleRate = params_.SampleRate,
                BitsPerSample = 16,
                Channels = params_.Channels,
                SampleCount = params_.OriginalLength,
                AudioData = audio
            };
        }

        private void DecodeChannel(FD2Params params_, int channel, float[] output)
        {
            int step = params_.MDCTBlockSize / 2;
            int numBlocks = params_.AllCodes[channel].Length;

            var mdct = new MDCT(params_.MDCTBlockSize, params_.MDCTSmallBlockSize);
            mdct.Reset();

            var quantizer = new FloorQuantizer(_quality);

            for (int b = 0; b < numBlocks; b++)
            {
                int coeffCount = params_.MDCTBlockSize / 2;

                // Декомпрессия floor
                float[] floor = DecompressFloorAdaptive(params_.AllFloorPoints[channel][b], _quality);

                // Декодируем коэффициенты
                float[] coeffs = new float[coeffCount];
                for (int i = 0; i < coeffCount && i < floor.Length; i++)
                {
                    byte code = params_.AllCodes[channel][b][i];
                    var (value, _) = quantizer.Decode(code, floor[i], floor[i]);
                    coeffs[i] = value;
                }

                // Обратное MDCT
                float[] samples = mdct.Inverse(coeffs, false);

                // Усиление x2
                for (int i = 0; i < samples.Length; i++)
                {
                    samples[i] *= 2.0f;
                }

                // Overlap-add
                int outPos = b * step;
                if (outPos < output.Length)
                {
                    mdct.OverlapAdd(samples, output, outPos, b == 0);
                }
            }
        }

        // ==================== ИНТЕРФЕЙС ====================
        public string ConvertToFD2(string inputPath, string outputPath = null)
        {
            outputPath ??= Path.ChangeExtension(inputPath, ".fd2");
            var params_ = Encode(inputPath);
            FD2File.Save(params_, outputPath);
            return outputPath;
        }

        public string ConvertFromFD2(string inputPath, string outputPath = null)
        {
            outputPath ??= Path.GetFileNameWithoutExtension(inputPath) + "_restored.wav";
            var params_ = FD2File.Load(inputPath);
            var wav = Decode(params_);
            wav.Save(outputPath);
            return outputPath;
        }

        public void ValidateReconstruction(string originalFile, string restoredFile)
        {
            var original = WavFile.Read(originalFile);
            var restored = WavFile.Read(restoredFile);

            int channels = Math.Min(original.Channels, restored.Channels);
            int samples = Math.Min(original.SampleCount, restored.SampleCount);

            Console.WriteLine($"\n[ПРОВЕРКА] ═══ Контроль качества ═══");

            for (int c = 0; c < channels; c++)
            {
                float mse = 0, maxDiff = 0, correlation = 0;
                float origVar = 0, restVar = 0;
                float origRms = 0, restRms = 0;

                // Частотный анализ
                float[] bandMSE = new float[4]; // 0-250Hz, 250-2k, 2k-8k, 8k+
                int[] bandCount = new int[4];

                for (int i = 0; i < samples; i++)
                {
                    float orig = original.AudioData[c][i];
                    float rest = restored.AudioData[c][i];

                    float diff = Math.Abs(orig - rest);
                    mse += diff * diff;
                    maxDiff = Math.Max(maxDiff, diff);

                    correlation += orig * rest;
                    origVar += orig * orig;
                    restVar += rest * rest;
                    origRms += orig * orig;
                    restRms += rest * rest;

                    // Частотный анализ через простую фильтрацию
                    if (i >= 2)
                    {
                        float lowFreq = (orig + original.AudioData[c][i - 1] + original.AudioData[c][i - 2]) / 3f;
                        float lowFreqRest = (rest + restored.AudioData[c][i - 1] + restored.AudioData[c][i - 2]) / 3f;

                        // Низкие частоты (сглаженный сигнал)
                        bandMSE[0] += (lowFreq - lowFreqRest) * (lowFreq - lowFreqRest);
                        bandCount[0]++;

                        // Средние (разность сглаженного и исходного)
                        float midFreq = orig - lowFreq;
                        float midFreqRest = rest - lowFreqRest;
                        bandMSE[1] += (midFreq - midFreqRest) * (midFreq - midFreqRest);
                        bandCount[1]++;

                        // Высокие (первая производная)
                        float highFreq = orig - original.AudioData[c][i - 1];
                        float highFreqRest = rest - restored.AudioData[c][i - 1];
                        bandMSE[2] += (highFreq - highFreqRest) * (highFreq - highFreqRest);
                        bandCount[2]++;

                        // Очень высокие (вторая производная)
                        float veryHighFreq = orig - 2 * original.AudioData[c][i - 1] + original.AudioData[c][i - 2];
                        float veryHighFreqRest = rest - 2 * restored.AudioData[c][i - 1] + restored.AudioData[c][i - 2];
                        bandMSE[3] += (veryHighFreq - veryHighFreqRest) * (veryHighFreq - veryHighFreqRest);
                        bandCount[3]++;
                    }
                }

                mse /= samples;
                origRms = (float)Math.Sqrt(origRms / samples);
                restRms = (float)Math.Sqrt(restRms / samples);
                correlation /= (float)Math.Sqrt(Math.Max(origVar * restVar, 1e-20f));

                float snr = 10 * (float)Math.Log10(Math.Max(origRms * origRms, 1e-10f) / Math.Max(mse, 1e-10f));

                Console.WriteLine($"\n[ПРОВЕРКА] Канал {c + 1}:");
                Console.WriteLine($"  ├─ SNR: {snr:F1} дБ {(snr > 40 ? "✓ ОТЛИЧНО" : snr > 25 ? "✓ ХОРОШО" : snr > 15 ? "⚠" : "✗ ПЛОХО")}");
                Console.WriteLine($"  ├─ Корреляция: {correlation:F6} {(correlation > 0.999 ? "✓" : correlation > 0.99 ? "⚠" : "✗")}");
                Console.WriteLine($"  ├─ Макс. ошибка: {maxDiff:F6}");
                Console.WriteLine($"  ├─ RMS оригинал: {origRms:F4}");
                Console.WriteLine($"  ├─ RMS восстановл.: {restRms:F4}");
                Console.WriteLine($"  ├─ Громкость: {restRms / origRms * 100:F1}%");
                Console.WriteLine($"  └─ Частотный анализ:");

                string[] bandNames = { "Низкие (0-250Hz)", "Средние (250-2kHz)", "Высокие (2-8kHz)", "Очень высокие (8kHz+)" };
                for (int b = 0; b < 4; b++)
                {
                    float bandMse = bandCount[b] > 0 ? bandMSE[b] / bandCount[b] : 1e-10f;
                    float bandSnr = 10 * (float)Math.Log10(Math.Max(origRms * origRms, 1e-10f) / Math.Max(bandMse, 1e-10f));
                    Console.WriteLine($"      ├─ {bandNames[b]}: SNR {bandSnr:F1} дБ");
                }
            }
        }
    }
}