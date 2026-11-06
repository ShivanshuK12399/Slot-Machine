using System;

namespace SlotMachine.Core
{
    /// <summary>
    /// Default RNG provider using System.Random with an optional custom seed for fair, unpredictably distributed numbers.
    /// Does not allocate garbage during runtime sampling.
    /// </summary>
    public sealed class UnityRngProvider : IRandomProvider
    {
        private readonly Random _random;

        public UnityRngProvider()
        {
            _random = new Random();
        }

        public UnityRngProvider(int seed)
        {
            _random = new Random(seed);
        }

        public int Range(int minInclusive, int maxExclusive)
        {
            if (minInclusive >= maxExclusive)
            {
                return minInclusive;
            }

            return _random.Next(minInclusive, maxExclusive);
        }

        public float NextFloat()
        {
            return (float)_random.NextDouble();
        }
    }
}

