using System;
using System.Text.RegularExpressions;

namespace Hlight.Foundation
{
    public static class StringExtensions
    {
        public static string FormatWith(this string format, params object[] arguments)
        {
            if (format == null) throw new ArgumentNullException(nameof(format));
            return string.Format(format, arguments);
        }

        public static string SubstringBetween(
            this string source,
            string head,
            string tail,
            out int startIndex,
            StringComparison comparison = StringComparison.Ordinal)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (head == null) throw new ArgumentNullException(nameof(head));
            if (tail == null) throw new ArgumentNullException(nameof(tail));

            var headIndex = source.IndexOf(head, comparison);
            if (headIndex < 0)
                throw new ArgumentException("The head marker was not found.", nameof(head));

            startIndex = headIndex + head.Length;
            var tailIndex = source.IndexOf(tail, startIndex, comparison);
            if (tailIndex < 0)
                throw new ArgumentException("The tail marker was not found after the head marker.", nameof(tail));

            return source.Substring(startIndex, tailIndex - startIndex);
        }

        public static string SplitCamelCase(this string source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            return Regex.Replace(
                Regex.Replace(source, @"(\P{Ll})(\P{Ll}\p{Ll})", "$1 $2"),
                @"(\p{Ll})(\P{Ll})",
                "$1 $2");
        }
    }
}
