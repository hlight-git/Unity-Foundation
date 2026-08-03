using UnityEngine;

namespace Hlight.Foundation
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("UI/Rect Transform Size Matcher")]
    public sealed class RectTransformSizeMatcher : MonoBehaviour
    {
        [SerializeField] private RectTransform widthSource;
        [SerializeField] private RectTransform heightSource;

        private RectTransform target;
        private DrivenRectTransformTracker tracker;

        private void OnEnable()
        {
            target = (RectTransform)transform;
            RefreshTracker();
            ApplySize();
        }

        private void Update() => ApplySize();

        private void OnDisable() => tracker.Clear();

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (target == null)
                target = transform as RectTransform;

            RefreshTracker();
            ApplySize();
        }
#endif

        private void RefreshTracker()
        {
            tracker.Clear();
            if (target == null)
                return;

            var properties = DrivenTransformProperties.None;
            if (widthSource != null) properties |= DrivenTransformProperties.SizeDeltaX;
            if (heightSource != null) properties |= DrivenTransformProperties.SizeDeltaY;
            tracker.Add(this, target, properties);
        }

        private void ApplySize()
        {
            if (target == null)
                return;

            if (widthSource != null)
                target.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, widthSource.rect.width);
            if (heightSource != null)
                target.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, heightSource.rect.height);
        }
    }
}
