using System;
using UnityEngine;

namespace Hlight.Foundation
{
    public sealed class FillBar : MonoBehaviour
    {
        [SerializeField] private RectTransform bar;
        [SerializeField] private RectTransform fill;
        [SerializeField] private RectTransform.Axis axis;
        [SerializeField, Min(0f)] private float totalMargin;
        [SerializeField, Range(0f, 1f)] private float value;

        private float lastBarSize = float.NaN;

        public float Value
        {
            get => value;
            set
            {
                this.value = Mathf.Clamp01(value);
                Refresh();
            }
        }

        private void OnEnable() => Refresh();

        private void OnRectTransformDimensionsChange() => Refresh();

        private void LateUpdate()
        {
            if (bar == null)
                return;

            var barSize = GetBarSize();
            if (!Mathf.Approximately(barSize, lastBarSize))
                Refresh();
        }

        private void OnValidate()
        {
            value = Mathf.Clamp01(value);
            Refresh();
        }

        private void Refresh()
        {
            if (bar == null || fill == null)
                return;

            lastBarSize = GetBarSize();
            fill.SetSizeWithCurrentAnchors(axis, Mathf.Max(0f, lastBarSize - totalMargin) * value);
        }

        private float GetBarSize() => axis switch
        {
            RectTransform.Axis.Horizontal => bar.rect.width,
            RectTransform.Axis.Vertical => bar.rect.height,
            _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, null)
        };
    }
}
