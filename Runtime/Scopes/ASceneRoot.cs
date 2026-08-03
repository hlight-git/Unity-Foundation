using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Hlight.DesignPattern.DependencyInversion.ServiceLocator;
using UnityEngine;

namespace Hlight.Foundation
{
    /// <summary>
    /// Scene-owned service source. A concrete root implements one
    /// <c>IProvider&lt;T&gt;</c> per local service. Each root type identifies one scene
    /// definition; loaded roots are claimed in <see cref="Awake"/> order.
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

        private AServiceLocator _serviceLocator;

        internal bool IsServiceLocatorBound => _serviceLocator != null;

        /// <summary>
        /// Locator for this scene scope. It checks providers implemented by this root,
        /// then bubbles to the parent scope.
        /// </summary>
        public AServiceLocator ServiceLocator
            => _serviceLocator ?? throw new InvalidOperationException(
                $"{GetType().Name} is not attached to a loaded scene scope.");

        /// <remarks>Overrides must call <c>base.Awake()</c> to register the root.</remarks>
        protected virtual void Awake()
        {
            PendingRoots.Add(this);
        }

        internal void BindServiceLocator(AServiceLocator serviceLocator)
        {
            _serviceLocator = serviceLocator ?? throw new ArgumentNullException(nameof(serviceLocator));
        }

        internal void UnbindServiceLocator() => _serviceLocator = null;

        public virtual UniTask OnSceneLoaded(bool isReusing, CancellationToken cancellationToken) => UniTask.CompletedTask;
        public virtual UniTask OnSceneEnable(CancellationToken cancellationToken) => UniTask.CompletedTask;
        public virtual UniTask OnSceneDisable(CancellationToken cancellationToken) => UniTask.CompletedTask;
        public virtual UniTask OnSceneUnload(bool isReusing, CancellationToken cancellationToken) => UniTask.CompletedTask;
    }
}
