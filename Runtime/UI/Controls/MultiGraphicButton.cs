using System;
using UnityEngine;
using UnityEngine.UI;

namespace Hlight.Foundation
{
    [AddComponentMenu("UI/Multi Graphic Button")]
    public sealed class MultiGraphicButton : Button
    {
        [SerializeField] private Graphic[] targetGraphics = Array.Empty<Graphic>();

        public Graphic[] TargetGraphics
        {
            get => targetGraphics;
            set => targetGraphics = value ?? Array.Empty<Graphic>();
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);

            var targetColor = state switch
            {
                SelectionState.Disabled => colors.disabledColor,
                SelectionState.Highlighted => colors.highlightedColor,
                SelectionState.Normal => colors.normalColor,
                SelectionState.Pressed => colors.pressedColor,
                SelectionState.Selected => colors.selectedColor,
                _ => Color.white
            };

            foreach (var graphic in targetGraphics)
            {
                if (graphic == null || graphic == targetGraphic || !graphic.gameObject.activeInHierarchy)
                    continue;

                graphic.CrossFadeColor(
                    targetColor * colors.colorMultiplier,
                    instant ? 0f : colors.fadeDuration,
                    ignoreTimeScale: true,
                    useAlpha: true);
            }
        }
    }
}
