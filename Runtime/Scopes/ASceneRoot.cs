using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Hlight.DesignPattern.DependencyInversion.DependencyInjection;
using UnityEngine;

namespace Hlight.Foundation
{
    /// <summary>
    /// Scene-owned dependency source. A concrete root implements one
    /// <c>IDependencyResolvable&lt;T&gt;</c> per target type the scene configures. Each root
    /// type identifies one scene definition; loaded roots are claimed in <see cref="Awake"/>
    /// order.
    /// </summary>
    public abstract class ASceneRoot : MonoBehaviour
    {
        #region Static

        private static readonly List<ASceneRoot> PendingRoots = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetPendingRoots() => PendingRoots.Clear();

        internal static bool TryTake<TSceneRoot>(out TSceneRoot sceneRoot)
            where TSceneRoot : ASceneRoot
        {
            for (var i = 0; i < PendingRoots.Count;)
            {
                var candidate = PendingRoots[i];
                if (candidate == null)
                {
                    PendingRoots.RemoveAt(i);
                    continue;
                }

                if (candidate is not TSceneRoot typedRoot)
                {
                    i++;
                    continue;
                }

                PendingRoots.RemoveAt(i);
                sceneRoot = typedRoot;
                return true;
            }

            sceneRoot = null;
            return false;
        }

        internal static void ReturnPending(ASceneRoot sceneRoot)
        {
            if (sceneRoot == null || PendingRoots.Contains(sceneRoot)) return;
            PendingRoots.Insert(0, sceneRoot);
        }

        #endregion

        private DependencyInjector _injector;

        internal bool IsInjectorBound => _injector != null;

        /// <summary>
        /// Injector for this scene scope: the parent scope's resolvers run first, then the
        /// ones this root declares. Hand it to whatever creates objects inside the scene.
        /// </summary>
        public DependencyInjector Injector
            => _injector ?? throw new InvalidOperationException(
                $"{GetType().Name} is not attached to a loaded scene scope.");

        /// <remarks>Overrides must call <c>base.Awake()</c> to register the root.</remarks>
        protected virtual void Awake()
        {
            PendingRoots.Add(this);
        }

        internal void BindInjector(DependencyInjector injector)
        {
            _injector = injector ?? throw new ArgumentNullException(nameof(injector));
        }

        internal void UnbindInjector() => _injector = null;

        public virtual UniTask OnSceneLoaded(bool isReusing, CancellationToken cancellationToken) => UniTask.CompletedTask;
        public virtual UniTask OnSceneEnable(CancellationToken cancellationToken) => UniTask.CompletedTask;
        public virtual UniTask OnSceneDisable(CancellationToken cancellationToken) => UniTask.CompletedTask;
        public virtual UniTask OnSceneUnload(bool isReusing, CancellationToken cancellationToken) => UniTask.CompletedTask;
    }
}
