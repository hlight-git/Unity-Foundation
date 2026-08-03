namespace Hlight.Foundation
{
    public static class NumericExtensions
    {
        public static bool IsBetween(this float value, float min, float max, bool includeMin = true, bool includeMax = true)
            => (min < value && value < max) || (includeMin && value == min) || (includeMax && value == max);

        public static bool IsBetween(this int value, int min, int max, bool includeMin = true, bool includeMax = true)
            => (min < value && value < max) || (includeMin && value == min) || (includeMax && value == max);

        public static int NonZeroSign(this float value) => value < 0 ? -1 : 1;
        public static int NonZeroSign(this double value) => value < 0 ? -1 : 1;
        public static int NonZeroSign(this int value) => value < 0 ? -1 : 1;
        public static int NonZeroSign(this long value) => value < 0 ? -1 : 1;
    }
}
