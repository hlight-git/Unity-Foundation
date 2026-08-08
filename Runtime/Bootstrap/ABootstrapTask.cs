using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Hlight.Foundation
{
    public abstract class ABootstrapTask<TRootScope> : MonoBehaviour
        where TRootScope : ARootScope
    {
        [Tooltip("Tasks that must finish before this one starts. Leave empty to start immediately.")]
        [SerializeField] private ABootstrapTask<TRootScope>[] waitFor;

        /// <summary>
        /// The tasks this one starts after. Everything not named here runs alongside it.
        /// </summary>
        /// <remarks>
        /// A reference rather than a declaration of what the task reads, because ordering is the
        /// only thing the bootstrap can act on anyway. Whether a task actually got what it needed is
        /// already answered where the value lives: a scope property that has not been filled in
        /// throws, naming both the missing service and the task that fills it. Two mechanisms for
        /// one fact would just be two places to keep in sync.
        /// <para>
        /// Being a serialized reference also puts the boot order in front of whoever opens the
        /// Bootstrap component, and survives renaming the task class — neither is true of an order
        /// derived from type names in code.
        /// </para>
        /// </remarks>
        public ABootstrapTask<TRootScope>[] WaitFor => waitFor;

        /// <summary>
        /// Executes this loading step. Runs concurrently with every task not listed in
        /// <see cref="WaitFor"/>, so it may not assume any of them has happened.
        /// </summary>
        public abstract UniTask Execute(TRootScope scope, CancellationToken cancellationToken);
    }
}
