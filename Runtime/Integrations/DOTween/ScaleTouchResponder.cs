#if DOTWEEN
using DG.Tweening;
using UnityEngine;

namespace Hlight.Foundation
{
    public sealed class ScaleTouchResponder : ATouchResponder
    {
        [SerializeField] private ScaleTouchResponderConfig config;

        private Tween scaleTween;
        private Vector3 restingScale;

        private void OnEnable()
        {
            restingScale = transform.localScale;
            if (config != null)
                return;

            Debug.LogError($"{nameof(ScaleTouchResponder)} on '{name}' requires a config.", this);
            enabled = false;
        }

        public override void OnTouchBegin(Vector3 position)
        {
            scaleTween?.Kill();
            scaleTween = transform.DOScale(config.TargetScale, config.Speed)
                .SetSpeedBased()
                .SetEase(config.Ease)
                .SetRecyclable();
        }

        public override void OnTouchEnd(TouchState state, Vector3 position)
        {
            scaleTween?.Kill();
            scaleTween = transform.DOScale(restingScale, config.Speed)
                .SetSpeedBased()
                .SetEase(config.Ease)
                .SetRecyclable();
        }

        private void OnDisable()
        {
            scaleTween?.Kill();
            scaleTween = null;
            transform.localScale = restingScale;
        }
    }
}
#endif
