using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Hlight.Foundation
{
    public static class AnimatorExtensions
    {
        [Serializable]
        public struct AnimatorStateReference
        {
            [SerializeField] private string stateName;
            [SerializeField] private int layer;

            public AnimatorStateReference(string stateName, int layer = 0)
            {
                if (string.IsNullOrWhiteSpace(stateName))
                    throw new ArgumentException("An animator state name is required.", nameof(stateName));
                if (layer < 0)
                    throw new ArgumentOutOfRangeException(nameof(layer));

                this.stateName = stateName;
                this.layer = layer;
            }

            public string StateName => stateName;
            public int Layer => layer;
        }

        public static async UniTask PlayAsync(
            this Animator animator,
            AnimatorStateReference state,
            CancellationToken cancellationToken = default)
        {
            Validate(animator, state);
            if (!animator.isActiveAndEnabled)
                throw new InvalidOperationException("The animator must be active and enabled.");

            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                animator.GetCancellationTokenOnDestroy());
            var linkedToken = linkedCancellation.Token;

            animator.Play(state);
            var stateInfo = animator.GetCurrentAnimatorStateInfo(state.Layer);
            if (!stateInfo.IsName(state.StateName))
            {
                throw new InvalidOperationException(
                    $"Animator state '{state.StateName}' was not found on layer {state.Layer}.");
            }

            if (stateInfo.loop)
            {
                throw new InvalidOperationException(
                    $"Animator state '{state.StateName}' is looping and cannot complete.");
            }

            var expectedStateHash = stateInfo.fullPathHash;
            var previousNormalizedTime = stateInfo.normalizedTime;

            while (stateInfo.normalizedTime < 1f)
            {
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, linkedToken);

                if (!animator.isActiveAndEnabled)
                {
                    throw new InvalidOperationException(
                        $"Animator state '{state.StateName}' was interrupted because the animator was disabled.");
                }

                stateInfo = animator.GetCurrentAnimatorStateInfo(state.Layer);
                if (stateInfo.fullPathHash != expectedStateHash ||
                    stateInfo.normalizedTime + 0.0001f < previousNormalizedTime)
                {
                    throw new InvalidOperationException(
                        $"Animator state '{state.StateName}' was interrupted before it completed.");
                }

                previousNormalizedTime = stateInfo.normalizedTime;
            }
        }

        public static void PlayAtLastFrame(this Animator animator, AnimatorStateReference state)
        {
            Validate(animator, state);
            animator.Play(state.StateName, state.Layer, 1f);
            animator.Update(0f);
        }

        public static void Play(this Animator animator, AnimatorStateReference state)
        {
            Validate(animator, state);
            animator.Play(state.StateName, state.Layer, 0f);
            animator.Update(0f);
        }

        private static void Validate(Animator animator, AnimatorStateReference state)
        {
            if (animator == null) throw new ArgumentNullException(nameof(animator));
            if (string.IsNullOrWhiteSpace(state.StateName))
                throw new ArgumentException("An animator state name is required.", nameof(state));
            if (state.Layer < 0 || state.Layer >= animator.layerCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(state),
                    $"Layer {state.Layer} is outside the animator's layer range.");
            }
        }
    }
}
