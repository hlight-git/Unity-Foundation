using System;
using System.Collections.Generic;
using System.Linq;
using Random = UnityEngine.Random;

namespace Hlight.Foundation
{
    public static class EnumerableExtensions
    {
        public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var buffer = source.ToArray();
            for (var index = 0; index < buffer.Length; index++)
            {
                var selectedIndex = Random.Range(index, buffer.Length);
                yield return buffer[selectedIndex];
                buffer[selectedIndex] = buffer[index];
            }
        }

        public static string Join<T>(this IEnumerable<T> source, string separator)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return string.Join(separator, source);
        }

        public static string Join<T>(this IEnumerable<T> source, char separator)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return string.Join(separator, source);
        }

        public static IEnumerable<(int Index, T Item)> WithIndex<T>(
            this IEnumerable<T> source,
            int startIndex = 0)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            foreach (var item in source)
                yield return (startIndex++, item);
        }

        public static IEnumerable<T> TakeRandom<T>(this IEnumerable<T> source, int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            return source.Shuffle().Take(count);
        }

        public static T ChooseRandom<T>(this IEnumerable<T> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var values = source as IReadOnlyList<T> ?? source.ToArray();
            if (values.Count == 0)
                throw new InvalidOperationException("Cannot choose an item from an empty sequence.");

            return values[Random.Range(0, values.Count)];
        }

        public static T ChooseRandom<T>(this IEnumerable<T> source, Func<T, float> getWeight)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (getWeight == null) throw new ArgumentNullException(nameof(getWeight));

            var values = source.ToArray();
            if (values.Length == 0)
                throw new InvalidOperationException("Cannot choose an item from an empty sequence.");

            var weights = new float[values.Length];
            var totalWeight = 0f;
            for (var index = 0; index < values.Length; index++)
            {
                var weight = getWeight(values[index]);
                if (float.IsNaN(weight) || float.IsInfinity(weight) || weight < 0f)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(getWeight),
                        "Weights must be finite and non-negative.");
                }

                weights[index] = weight;
                totalWeight += weight;
                if (float.IsInfinity(totalWeight))
                    throw new ArgumentOutOfRangeException(nameof(getWeight), "The total weight is too large.");
            }

            if (totalWeight <= 0f)
                throw new InvalidOperationException("At least one item must have a positive weight.");

            var selection = Random.value * totalWeight;
            var cumulativeWeight = 0f;
            for (var index = 0; index < values.Length; index++)
            {
                cumulativeWeight += weights[index];
                if (selection < cumulativeWeight)
                    return values[index];
            }

            for (var index = values.Length - 1; index >= 0; index--)
            {
                if (weights[index] > 0f)
                    return values[index];
            }

            throw new InvalidOperationException("At least one item must have a positive weight.");
        }

        public static T ChooseRandomExcept<T>(this IEnumerable<T> source, IEnumerable<T> excludedValues)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (excludedValues == null) throw new ArgumentNullException(nameof(excludedValues));

            var excluded = new HashSet<T>(excludedValues);
            return source.Where(item => !excluded.Contains(item)).ChooseRandom();
        }

        public static void Swap<T>(this IList<T> list, int leftIndex, int rightIndex)
        {
            if (list == null) throw new ArgumentNullException(nameof(list));
            (list[rightIndex], list[leftIndex]) = (list[leftIndex], list[rightIndex]);
        }

        public static void Resize<T>(this IList<T> list, int count)
        {
            if (list == null) throw new ArgumentNullException(nameof(list));
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));

            while (list.Count > count)
                list.RemoveAt(list.Count - 1);
            while (list.Count < count)
                list.Add(default);
        }
    }
}
