using System;
using UnityEngine;

namespace Hlight.Foundation
{
    [Serializable]
    public struct WeeklyTime : IComparable<WeeklyTime>, IEquatable<WeeklyTime>
    {
        [SerializeField] private DayOfWeek dayOfWeek;
        [SerializeField, Range(0, 23)] private byte hour;
        [SerializeField, Range(0, 59)] private byte minute;
        [SerializeField, Range(0, 59)] private byte second;

        public WeeklyTime(DayOfWeek dayOfWeek, byte hour, byte minute, byte second)
        {
            if (hour > 23) throw new ArgumentOutOfRangeException(nameof(hour));
            if (minute > 59) throw new ArgumentOutOfRangeException(nameof(minute));
            if (second > 59) throw new ArgumentOutOfRangeException(nameof(second));

            this.dayOfWeek = dayOfWeek;
            this.hour = hour;
            this.minute = minute;
            this.second = second;
        }

        public DayOfWeek DayOfWeek => dayOfWeek;
        public byte Hour => hour;
        public byte Minute => minute;
        public byte Second => second;

        public int CompareTo(WeeklyTime other)
        {
            var dayComparison = dayOfWeek.CompareTo(other.dayOfWeek);
            if (dayComparison != 0) return dayComparison;

            var hourComparison = hour.CompareTo(other.hour);
            if (hourComparison != 0) return hourComparison;

            var minuteComparison = minute.CompareTo(other.minute);
            return minuteComparison != 0 ? minuteComparison : second.CompareTo(other.second);
        }

        public bool Equals(WeeklyTime other)
        {
            return dayOfWeek == other.dayOfWeek
                && hour == other.hour
                && minute == other.minute
                && second == other.second;
        }

        public override bool Equals(object obj) => obj is WeeklyTime other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(dayOfWeek, hour, minute, second);

        public override string ToString() => $"{dayOfWeek}-{hour:D2}:{minute:D2}:{second:D2}";

        public static implicit operator WeeklyTime(DateTime value)
        {
            return new WeeklyTime(value.DayOfWeek, (byte)value.Hour, (byte)value.Minute, (byte)value.Second);
        }

        public static bool operator <(WeeklyTime left, WeeklyTime right) => left.CompareTo(right) < 0;
        public static bool operator >(WeeklyTime left, WeeklyTime right) => left.CompareTo(right) > 0;
        public static bool operator <=(WeeklyTime left, WeeklyTime right) => left.CompareTo(right) <= 0;
        public static bool operator >=(WeeklyTime left, WeeklyTime right) => left.CompareTo(right) >= 0;
        public static bool operator ==(WeeklyTime left, WeeklyTime right) => left.Equals(right);
        public static bool operator !=(WeeklyTime left, WeeklyTime right) => !left.Equals(right);
    }
}
