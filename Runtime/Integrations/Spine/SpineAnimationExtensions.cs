#if SPINE
using System;
using Spine;
using Spine.Unity;
using UnityEngine;

namespace Hlight.Foundation
{
    public static class SpineAnimationExtensions
    {
        [Serializable]
        public struct PlayRequest
        {
            [SerializeField] private string animationName;
            [SerializeField] private bool loop;
            [SerializeField] private float delay;

            public TrackEntry Queue(Spine.AnimationState animationState, int trackIndex, bool replaceCurrent)
            {
                if (string.IsNullOrWhiteSpace(animationName))
                    throw new InvalidOperationException("A Spine animation name is required.");

                return replaceCurrent
                    ? animationState.SetAnimation(trackIndex, animationName, loop)
                    : animationState.AddAnimation(trackIndex, animationName, loop, delay);
            }
        }

        public static TrackEntry Play(this SkeletonGraphic skeleton, params PlayRequest[] requests)
        {
            return skeleton.Play(blend: true, trackIndex: 0, requests: requests);
        }

        public static TrackEntry Play(this SkeletonGraphic skeleton, bool blend, params PlayRequest[] requests)
        {
            return skeleton.Play(blend, trackIndex: 0, requests: requests);
        }

        public static TrackEntry Play(
            this SkeletonGraphic skeleton,
            bool blend,
            int trackIndex,
            params PlayRequest[] requests)
        {
            if (skeleton == null) throw new ArgumentNullException(nameof(skeleton));
            if (requests == null) throw new ArgumentNullException(nameof(requests));
            if (requests.Length == 0) return null;

            var animationState = skeleton.AnimationState;
            if (!blend)
                animationState.ClearTrack(trackIndex);

            var trackEntry = requests[0].Queue(animationState, trackIndex, replaceCurrent: true);
            for (var index = 1; index < requests.Length; index++)
                requests[index].Queue(animationState, trackIndex, replaceCurrent: false);

            return trackEntry;
        }
    }
}
#endif
