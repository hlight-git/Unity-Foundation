using System;
using System.Globalization;
using UnityEngine;

namespace Hlight.Foundation
{
    [Serializable]
    public struct SerializableDateTime : IComparable<SerializableDateTime>, IEquatable<SerializableDateTime>
    {
        [SerializeField] private long binary;

        public SerializableDateTime(long ticks, DateTimeKind kind = DateTimeKind.Unspecified)
            : this(new DateTime(ticks, kind))
        {
        }

        public SerializableDateTime(DateTime value)
        {
            binary = value.ToBinary();
        }

        public long Ticks => ToDateTime().Ticks;
        public DateTimeKind Kind => ToDateTime().Kind;

        public DateTime ToDateTime() => DateTime.FromBinary(binary);

        public int CompareTo(SerializableDateTime other) => ToDateTime().CompareTo(other.ToDateTime());

        public bool Equals(SerializableDateTime other) => ToDateTime().Equals(other.ToDateTime());

        public override bool Equals(object obj) => obj is SerializableDateTime other && Equals(other);

        public override int GetHashCode() => ToDateTime().GetHashCode();

        public override string ToString() => ToDateTime().ToString(CultureInfo.InvariantCulture);

        public static bool operator <(SerializableDateTime left, SerializableDateTime right) =>
            left.CompareTo(right) < 0;
        public static bool operator >(SerializableDateTime left, SerializableDateTime right) =>
            left.CompareTo(right) > 0;
        public static bool operator <=(SerializableDateTime left, SerializableDateTime right) =>
            left.CompareTo(right) <= 0;
        public static bool operator >=(SerializableDateTime left, SerializableDateTime right) =>
            left.CompareTo(right) >= 0;
        public static bool operator ==(SerializableDateTime left, SerializableDateTime right) => left.Equals(right);
        public static bool operator !=(SerializableDateTime left, SerializableDateTime right) => !left.Equals(right);

        public static implicit operator SerializableDateTime(DateTime value) => new(value);
        public static implicit operator DateTime(SerializableDateTime value) => value.ToDateTime();
    }
}
