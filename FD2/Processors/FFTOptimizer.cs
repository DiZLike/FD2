// FFTOptimizer.cs - Быстрое преобразование Фурье (Cooley-Tukey)
using System;
using System.Numerics;

namespace FD2.Processors
{
    public class FFTOptimizer
    {
        private int _size;
        private Complex[] _workspace;
        private int[] _bitReversalTable;

        public FFTOptimizer(int size)
        {
            if ((size & (size - 1)) != 0)
                throw new ArgumentException("Size must be power of 2");

            _size = size;
            _workspace = new Complex[size];
            _bitReversalTable = GenerateBitReversalTable(size);
        }

        /// <summary>
        /// In-place FFT (Cooley-Tukey алгоритм)
        /// Сложность: O(N log N) вместо O(N²)
        /// </summary>
        public void ForwardFFT(Complex[] x)
        {
            if (x.Length != _size)
                throw new ArgumentException("Input size mismatch");

            // 1. Bit-reversal permutation
            for (int i = 0; i < _size; i++)
            {
                int j = _bitReversalTable[i];
                if (i < j)
                {
                    var temp = x[i];
                    x[i] = x[j];
                    x[j] = temp;
                }
            }

            // 2. Butterfly operations
            for (int stage = 0; stage < Math.Log2(_size); stage++)
            {
                int m = 1 << (stage + 1);      // 2^(stage+1)
                int mHalf = m >> 1;             // m/2

                Complex wm = Complex.Exp(new Complex(0, -2 * MathF.PI / m));
                Complex w = Complex.One;

                for (int k = 0; k < mHalf; k++)
                {
                    for (int j = 0; j < _size; j += m)
                    {
                        int t = j + k;
                        int u = t + mHalf;

                        Complex butterfly = w * x[u];
                        x[u] = x[t] - butterfly;
                        x[t] = x[t] + butterfly;
                    }

                    w *= wm;
                }
            }
        }

        /// <summary>
        /// Inverse FFT (conjugate method)
        /// </summary>
        public void InverseFFT(Complex[] x)
        {
            // Conjugate
            for (int i = 0; i < x.Length; i++)
                x[i] = Complex.Conjugate(x[i]);

            // Forward FFT
            ForwardFFT(x);

            // Conjugate + normalize
            float invN = 1.0f / _size;
            for (int i = 0; i < x.Length; i++)
                x[i] = Complex.Conjugate(x[i]) * invN;
        }

        /// <summary>
        /// Быстрое вычисление битового разворота
        /// </summary>
        private int[] GenerateBitReversalTable(int n)
        {
            int[] table = new int[n];
            int bits = (int)Math.Log2(n);

            for (int i = 0; i < n; i++)
            {
                int reversed = 0;
                int num = i;

                for (int b = 0; b < bits; b++)
                {
                    reversed = (reversed << 1) | (num & 1);
                    num >>= 1;
                }

                table[i] = reversed;
            }

            return table;
        }

        /// <summary>
        /// Получить величину спектра
        /// </summary>
        public float[] GetMagnitude(Complex[] spectrum)
        {
            var magnitude = new float[spectrum.Length];
            for (int i = 0; i < spectrum.Length; i++)
                magnitude[i] = (float)spectrum[i].Magnitude;
            return magnitude;
        }

        /// <summary>
        /// Получить фазу спектра
        /// </summary>
        public float[] GetPhase(Complex[] spectrum)
        {
            var phase = new float[spectrum.Length];
            for (int i = 0; i < spectrum.Length; i++)
                phase[i] = (float)spectrum[i].Phase;
            return phase;
        }
    }
}