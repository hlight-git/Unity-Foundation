using System;

namespace Hlight.Foundation
{
    public static class DateTimeExtensions
    {
        public static SerializableDateTime ToSerializable(this DateTime value) => value;
        public static WeeklyTime ToWeeklyTime(this DateTime value) => value;
    }
}
