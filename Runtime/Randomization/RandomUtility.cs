using System;
using Random = UnityEngine.Random;

namespace Hlight.Foundation
{
    public static class RandomUtility
    {
        public static T Choose<T>(params T[] choices)
        {
            if (choices == null) throw new ArgumentNullException(nameof(choices));
            if (choices.Length == 0)
                throw new ArgumentException("At least one choice is required.", nameof(choices));

            return choices[Random.Range(0, choices.Length)];
        }

        public static int NextSign() => NextBoolean() ? 1 : -1;

        public static bool NextBoolean(float trueProbability = 0.5f)
        {
            if (float.IsNaN(trueProbability) || float.IsInfinity(trueProbability) ||
                trueProbability is < 0f or > 1f)
                throw new ArgumentOutOfRangeException(nameof(trueProbability));

            if (trueProbability <= 0f) return false;
            if (trueProbability >= 1f) return true;
            return Random.value < trueProbability;
        }
    }
}
