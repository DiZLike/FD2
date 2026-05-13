// MDCT.cs - ОПТИМИЗИРОВАННАЯ С FFT (O(N log N) вместо O(N²))
using System;
using System.Numerics;

namespace FD2.Processors
{
    public class MDCT
    {
        private int N;
        private int N2;
        private float[] window;
        private float[] prevOverlap;
        private FFTOptimizer _fftOptimizer;
        private Complex[] _fftBuffer;
        private float[] _tempBuffer;
        private float[] _coeffBuffer;

        public MDCT(int blockSize = 2048, int smallBlockSize = 256)
        {
            N = blockSize;
            N2 = N / 2;
            prevOverlap = new float[N2];

            // Окно Принсена-Брэдли
            window = new float[N];
            for (int i = 0; i < N; i++)
            {
                window[i] = (float)Math.Sin(Math.PI * (i + 0.5) / N);
            }

            // Инициализируем FFT (размер должен быть степенью 2)
            int fftSize = 1 << (int)Math.Ceiling(Math.Log2(N));
            _fftOptimizer = new FFTOptimizer(fftSize);
            _fftBuffer = new Complex[fftSize];
            _tempBuffer = new float[fftSize];
            _coeffBuffer = new float[N2];
        }

        /// <summary>
        /// Прямое MDCT преобразование (оптимизированное)
        /// O(N log N) вместо O(N²)
        /// </summary>
        public float[] Forward(float[] input, bool useSmallBlock = false)
        {
            // Применяем окно анализа
            for (int i = 0; i < N && i < input.Length; i++)
            {
                _tempBuffer[i] = input[i] * window[i];
            }
            Array.Clear(_tempBuffer, N, _tempBuffer.Length - N);

            // Подготовка для FFT-базированного MDCT
            for (int n = 0; n < N; n++)
            {
                float angle = (float)(Math.PI / (2 * N) * (2 * n + 1));
                float cosVal = (float)Math.Cos(angle);
                float sinVal = (float)Math.Sin(angle);

                // Pre-rotation
                _fftBuffer[n] = new Complex(
                    _tempBuffer[n] * cosVal,
                    _tempBuffer[n] * sinVal
                );
            }
            Array.Clear(_fftBuffer, N, _fftBuffer.Length - N);

            // Выполняем FFT
            _fftOptimizer.ForwardFFT(_fftBuffer);

            // Post-rotation и извлечение только реальной части
            for (int k = 0; k < N2; k++)
            {
                float angle = (float)(Math.PI / N * (k + 0.5));
                float cosVal = (float)Math.Cos(angle);
                float sinVal = (float)Math.Sin(angle);

                Complex c = _fftBuffer[k];
                _coeffBuffer[k] = (float)(c.Real * cosVal + c.Imaginary * sinVal) * (2.0f / N);
            }

            return _coeffBuffer;
        }

        /// <summary>
        /// Обратное MDCT преобразование (оптимизированное)
        /// </summary>
        public float[] Inverse(float[] coeffs, bool useSmallBlock = false)
        {
            var samples = new float[N];

            // IMDCT
            for (int k = 0; k < N2; k++)
            {
                float angle = (float)(Math.PI / N * (k + 0.5));
                float cosVal = (float)Math.Cos(angle);
                float sinVal = (float)Math.Sin(angle);

                _fftBuffer[k] = new Complex(
                    coeffs[k] * cosVal,
                    coeffs[k] * sinVal
                );
            }
            Array.Clear(_fftBuffer, N2, _fftBuffer.Length - N2);

            // Обратное FFT
            _fftOptimizer.InverseFFT(_fftBuffer);

            // Post-rotation и применение окна
            for (int n = 0; n < N; n++)
            {
                float angle = (float)(Math.PI / (2 * N) * (2 * n + 1));
                float cosVal = (float)Math.Cos(angle);
                float sinVal = (float)Math.Sin(angle);

                Complex c = _fftBuffer[n];
                float value = (float)(c.Real * cosVal + c.Imaginary * sinVal);
                samples[n] = value * window[n];
            }

            return samples;
        }

        public void OverlapAdd(float[] currentBlock, float[] output, int offset, bool firstBlock = false)
        {
            for (int i = 0; i < N2 && (offset + i) < output.Length; i++)
            {
                if (firstBlock)
                {
                    output[offset + i] = currentBlock[i];
                }
                else
                {
                    output[offset + i] = prevOverlap[i] + currentBlock[i];
                }
            }

            for (int i = 0; i < N2 && (offset + N2 + i) < output.Length; i++)
            {
                output[offset + N2 + i] = currentBlock[N2 + i];
            }

            Array.Copy(currentBlock, N2, prevOverlap, 0, N2);
        }

        public void Reset()
        {
            Array.Clear(prevOverlap, 0, prevOverlap.Length);
        }
    }
}