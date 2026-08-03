using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Hlight.Foundation
{
    public abstract class ABootstrapTask<TRootScope> : MonoBehaviour
        where TRootScope : ARootScope
    {
        [SerializeField, Min(0.01f)] private float weight = 1f;

        /// <summary>
        /// Relative share of this task in the bootstrap's total progress.
        /// </summary>
        public float Weight => weight;

        /// <summary>
        /// Executes this loading step. Report normalized local progress from zero to one;
        /// the owning bootstrap maps it into this task's weighted range.
        /// </summary>
        public abstract UniTask Execute(
            TRootScope scope,
            IProgress<float> progress,
            CancellationToken cancellationToken);
    }
}
