#if DOTWEEN
using DG.Tweening;
using UnityEngine;

namespace Hlight.Foundation
{
    [CreateAssetMenu(menuName = "Hlight/Foundation/Scale Touch Responder Config")]
    public sealed class ScaleTouchResponderConfig : ScriptableObject
    {
        [field: SerializeField] public Vector3 TargetScale { get; private set; } = Vector3.one * 0.9f;
        [field: SerializeField, Min(0.01f)] public float Speed { get; private set; } = 5f;
        [field: SerializeField] public Ease Ease { get; private set; } = Ease.OutQuad;

        private void OnValidate() => Speed = Mathf.Max(0.01f, Speed);
    }
}
#endif
