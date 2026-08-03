using System;

namespace Hlight.Foundation
{
    public static class FormattingExtensions
    {
        private static readonly string[] TimeFormats =
        {
            "dd'd'hh'h'",
            "hh'h'mm'm'",
            "mm'm'ss's'"
        };

        public static string ToCompactDurationString(this TimeSpan timeSpan)
        {
            if (timeSpan.TotalHours >= 24) return timeSpan.ToString(TimeFormats[0]);
            if (timeSpan.TotalHours >= 1) return timeSpan.ToString(TimeFormats[1]);
            return timeSpan.ToString(TimeFormats[2]);
        }

        public static string ToOrdinal(this int number)
        {
            if (number >= 11 && number <= 13)
            {
                return number + "th";
            }

            return (number % 10) switch
            {
                1 => number + "st",
                2 => number + "nd",
                3 => number + "rd",
                _ => number + "th",
            };
        }

        public static string SecondsToTimeString(this int seconds, string format = "hh':'mm")
            => TimeSpan.FromSeconds(seconds).ToString(format);

        public static string SecondsToTimeString(this double seconds, string format = "hh':'mm")
            => TimeSpan.FromSeconds(seconds).ToString(format);
    }
}
