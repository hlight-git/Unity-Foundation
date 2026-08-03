using UnityEngine;

namespace Hlight.Foundation
{
    public static class VectorExtensions
    {
        public static bool ApproximatelyEquals(this Vector3 value, Vector3 other, float tolerance = 0.01f)
        {
            return Mathf.Abs(value.x - other.x) <= tolerance
                && Mathf.Abs(value.y - other.y) <= tolerance
                && Mathf.Abs(value.z - other.z) <= tolerance;
        }

        public static bool Contains(this Vector2 range, float value, bool includeMin = true, bool includeMax = true)
        {
            if (includeMin && includeMax)
                return range.x <= value && value <= range.y;
            if (includeMin)
                return range.x <= value && value < range.y;
            if (includeMax)
                return range.x < value && value <= range.y;
            return range.x < value && value < range.y;
        }

        public static Vector2 With(this Vector2 vector, float? x = null, float? y = null)
        {
            return vector.Set(x, y);
        }

        public static Vector2 Set(this ref Vector2 vector, float? x = null, float? y = null)
        {
            if (x.HasValue) vector.x = x.Value;
            if (y.HasValue) vector.y = y.Value;
            return vector;
        }

        public static Vector3 With(this Vector3 vector, float? x = null, float? y = null, float? z = null)
        {
            return vector.Set(x, y, z);
        }

        public static Vector3 Set(this ref Vector3 vector, float? x = null, float? y = null, float? z = null)
        {
            if (x.HasValue) vector.x = x.Value;
            if (y.HasValue) vector.y = y.Value;
            if (z.HasValue) vector.z = z.Value;
            return vector;
        }

        public static Vector3 Add(this Vector3 vector, float value)
        {
            vector.x += value;
            vector.y += value;
            vector.z += value;
            return vector;
        }

        public static Vector3 Add(this Vector3 vector, float? x = null, float? y = null, float? z = null)
        {
            if (x.HasValue) vector.x += x.Value;
            if (y.HasValue) vector.y += y.Value;
            if (z.HasValue) vector.z += z.Value;
            return vector;
        }

        public static Vector4 With(this Vector4 vector, float? x = null, float? y = null, float? z = null, float? w = null)
        {
            return vector.Set(x, y, z, w);
        }

        public static Vector4 Set(this ref Vector4 vector, float? x = null, float? y = null, float? z = null, float? w = null)
        {
            if (x.HasValue) vector.x = x.Value;
            if (y.HasValue) vector.y = y.Value;
            if (z.HasValue) vector.z = z.Value;
            if (w.HasValue) vector.w = w.Value;
            return vector;
        }

        public static Color With(this Color color, float? r = null, float? g = null, float? b = null, float? a = null)
        {
            return color.Set(r, g, b, a);
        }

        public static Color Set(this ref Color color, float? r = null, float? g = null, float? b = null, float? a = null)
        {
            if (r.HasValue) color.r = r.Value;
            if (g.HasValue) color.g = g.Value;
            if (b.HasValue) color.b = b.Value;
            if (a.HasValue) color.a = a.Value;
            return color;
        }
    }
}
