using System;
using UnityEngine;

namespace Hlight.Foundation
{
    public static class TransformExtensions
    {
        public static Transform[] GetChildren(this Transform transform)
        {
            if (transform == null) throw new ArgumentNullException(nameof(transform));

            var children = new Transform[transform.childCount];
            for (var index = 0; index < children.Length; index++)
                children[index] = transform.GetChild(index);
            return children;
        }

        public static void DestroyChildren(this Transform transform)
        {
            if (transform == null) throw new ArgumentNullException(nameof(transform));

            for (var index = transform.childCount - 1; index >= 0; index--)
                transform.GetChild(index).gameObject.DestroySafely();
        }

        public static float GetWidth(this RectTransform transform) => transform.rect.width;
        public static float GetHeight(this RectTransform transform) => transform.rect.height;

        public static float GetOffset(this RectTransform transform, RectTransform.Edge edge) => edge switch
        {
            RectTransform.Edge.Top => -transform.offsetMax.y,
            RectTransform.Edge.Bottom => transform.offsetMin.y,
            RectTransform.Edge.Left => transform.offsetMin.x,
            RectTransform.Edge.Right => -transform.offsetMax.x,
            _ => throw new ArgumentOutOfRangeException(nameof(edge), edge, null)
        };

        public static void SetOffset(
            this RectTransform transform,
            float? left = null,
            float? right = null,
            float? top = null,
            float? bottom = null)
        {
            if (left.HasValue) transform.offsetMin = new Vector2(left.Value, transform.offsetMin.y);
            if (right.HasValue) transform.offsetMax = new Vector2(-right.Value, transform.offsetMax.y);
            if (top.HasValue) transform.offsetMax = new Vector2(transform.offsetMax.x, -top.Value);
            if (bottom.HasValue) transform.offsetMin = new Vector2(transform.offsetMin.x, bottom.Value);
        }

        public static void SetSize(this RectTransform transform, float? width = null, float? height = null)
        {
            if (width.HasValue)
                transform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width.Value);
            if (height.HasValue)
                transform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height.Value);
        }

        public static void SetSize(this RectTransform transform, Vector2 size)
        {
            transform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
            transform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
        }

        public static Vector3 AnchoredToLocalPosition(this RectTransform transform, Vector2 anchoredPosition)
        {
            var offset = anchoredPosition - transform.anchoredPosition;
            return transform.localPosition + (Vector3)offset;
        }

        public static Vector3 AnchoredToWorldPosition(this RectTransform transform, Vector2 anchoredPosition)
        {
            return transform.TransformPoint(anchoredPosition - transform.anchoredPosition);
        }

        /// <summary>
        /// Attempts to set the world-space scale reported by Unity. A rotated child
        /// below non-uniformly scaled parents can be skewed, so <see cref="Transform.lossyScale"/>
        /// and this operation are approximations in that hierarchy.
        /// </summary>
        public static bool TrySetLossyScale(
            this Transform transform,
            float? x = null,
            float? y = null,
            float? z = null)
        {
            if (transform == null) throw new ArgumentNullException(nameof(transform));

            var targetScale = transform.lossyScale.With(x, y, z);
            if (!IsFinite(targetScale))
                return false;

            var originalLocalScale = transform.localScale;
            var currentLossyScale = transform.lossyScale;
            var unitScale = GetUnitAxisScale(transform);

            if (!TryGetLocalScale(
                    targetScale.x,
                    currentLossyScale.x,
                    originalLocalScale.x,
                    unitScale.x,
                    out var localX) ||
                !TryGetLocalScale(
                    targetScale.y,
                    currentLossyScale.y,
                    originalLocalScale.y,
                    unitScale.y,
                    out var localY) ||
                !TryGetLocalScale(
                    targetScale.z,
                    currentLossyScale.z,
                    originalLocalScale.z,
                    unitScale.z,
                    out var localZ))
            {
                return false;
            }

            transform.localScale = new Vector3(localX, localY, localZ);
            if (ApproximatelyEquals(transform.lossyScale, targetScale))
                return true;

            transform.localScale = originalLocalScale;
            return false;
        }

        public static bool TrySetLossyScale(this Transform transform, Transform target)
        {
            if (transform == null) throw new ArgumentNullException(nameof(transform));
            if (target == null) throw new ArgumentNullException(nameof(target));

            var targetScale = target.lossyScale;
            return transform.TrySetLossyScale(targetScale.x, targetScale.y, targetScale.z);
        }

        private static Vector3 GetUnitAxisScale(Transform transform)
        {
            var parentMatrix = transform.parent != null
                ? transform.parent.localToWorldMatrix
                : Matrix4x4.identity;
            var worldBasis = parentMatrix * Matrix4x4.Rotate(transform.localRotation);

            return new Vector3(
                worldBasis.MultiplyVector(Vector3.right).magnitude,
                worldBasis.MultiplyVector(Vector3.up).magnitude,
                worldBasis.MultiplyVector(Vector3.forward).magnitude);
        }

        private static bool TryGetLocalScale(
            float targetScale,
            float currentLossyScale,
            float currentLocalScale,
            float unitScale,
            out float localScale)
        {
            float scaleFactor;
            if (!Mathf.Approximately(currentLocalScale, 0f))
                scaleFactor = currentLossyScale / currentLocalScale;
            else
                scaleFactor = unitScale;

            if (Mathf.Approximately(scaleFactor, 0f))
            {
                localScale = currentLocalScale;
                return Mathf.Approximately(targetScale, 0f);
            }

            localScale = targetScale / scaleFactor;
            return !float.IsNaN(localScale) && !float.IsInfinity(localScale);
        }

        private static bool ApproximatelyEquals(Vector3 left, Vector3 right)
        {
            return Mathf.Approximately(left.x, right.x)
                && Mathf.Approximately(left.y, right.y)
                && Mathf.Approximately(left.z, right.z);
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }
}
