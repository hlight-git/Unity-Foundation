using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Hlight.DesignPattern.DependencyInversion.DependencyInjection;
using UnityEngine;

namespace Hlight.Foundation
{
    [Serializable]
    public class SceneScope<TSceneRoot> : IScope
        where TSceneRoot : ASceneRoot
    {
        [SerializeField] private string sceneName;
        [SerializeField] private string addressablePath;
        [SerializeField] private bool useAddressable;
        [SerializeField] private bool reuseScene;

        private TSceneRoot _sceneRoot;
        [NonSerialized] private ISceneLease _sceneLease;
        private bool _isOperating;
        private IScope _parentScope;
        private DependencyInjector _injector;

        /// <summary>
        /// This scene's injector, chained onto the parent scope's. Exists only while a root
        /// is bound — a scene that is unloaded or cached has none.
        /// </summary>
        public DependencyInjector Injector
            => _injector ?? throw new InvalidOperationException(
                $"Scene '{LoadSceneKey}' has no injector while its state is {State}.");

        public string LoadSceneKey => useAddressable ? addressablePath : sceneName;
        public SceneScopeState State { get; private set; } = SceneScopeState.Unloaded;

        /// <summary>
        /// The loaded root, or <c>null</c> while this scope is unloaded. Public because a scope's
        /// root is the typed handle on whatever that scene exposes — a caller that owns the scope
        /// reaches the scene through here rather than through an untyped lookup.
        /// </summary>
        public TSceneRoot SceneRoot
        {
            get => _sceneRoot;
            private set => _sceneRoot = value;
        }

        private ISceneLease SceneLease => _sceneLease ??= new UnitySceneLease();

        public SceneScope()
        {
        }

        internal SceneScope(ISceneLease sceneLease)
        {
            _sceneLease = sceneLease ?? throw new ArgumentNullException(nameof(sceneLease));
        }

        /// <summary>
        /// Sets the scope this one chains onto. The parent's injector is read at bind time,
        /// not here — a parent scene scope has none until it loads, so capturing it now would
        /// capture nothing.
        /// </summary>
        public SceneScope<TSceneRoot> SetParentScope(IScope parentScope)
        {
            if (_isOperating)
                throw new InvalidOperationException(
                    $"Cannot change the parent of scene '{LoadSceneKey}' while an operation is running.");

            if (State != SceneScopeState.Unloaded)
                throw new InvalidOperationException(
                    $"Cannot change the parent of scene '{LoadSceneKey}' while its state is {State}.");

            if (parentScope == null)
                throw new ArgumentNullException(nameof(parentScope));

            if (ReferenceEquals(parentScope, this))
                throw new ArgumentException(
                    "A scene scope cannot be its own parent.",
                    nameof(parentScope));

            _parentScope = parentScope;
            return this;
        }

        public async UniTask LoadIfNeededAsync(CancellationToken cancellationToken = default)
        {
            using var operation = BeginOperation(nameof(LoadIfNeededAsync));

            switch (State)
            {
                case SceneScopeState.Active:
                case SceneScopeState.LoadedInactive:
                    return;

                case SceneScopeState.Cached:
                    await RestoreCachedAsync(cancellationToken);
                    return;

                case SceneScopeState.Unloaded:
                    EnsureParentConfigured();
                    await LoadInitialAsync(cancellationToken);
                    return;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public async UniTask EnableAsync(CancellationToken cancellationToken = default)
        {
            using var operation = BeginOperation(nameof(EnableAsync));

            if (State == SceneScopeState.Active) return;
            EnsureState(SceneScopeState.LoadedInactive, nameof(EnableAsync));

            await SceneRoot.OnSceneEnable(cancellationToken);
            State = SceneScopeState.Active;
        }

        public async UniTask DisableAsync(CancellationToken cancellationToken = default)
        {
            using var operation = BeginOperation(nameof(DisableAsync));

            if (State == SceneScopeState.LoadedInactive) return;
            EnsureState(SceneScopeState.Active, nameof(DisableAsync));

            await SceneRoot.OnSceneDisable(cancellationToken);
            State = SceneScopeState.LoadedInactive;
        }

        public async UniTask UnloadAsync(CancellationToken cancellationToken = default)
        {
            using var operation = BeginOperation(nameof(UnloadAsync));
            await UnloadCoreAsync(false, cancellationToken);
        }

        /// <summary>
        /// Physically unloads the owned Unity scene, including a scene currently kept
        /// in the cache by <c>reuseScene</c>.
        /// </summary>
        public async UniTask ReleaseAsync(CancellationToken cancellationToken = default)
        {
            using var operation = BeginOperation(nameof(ReleaseAsync));
            await UnloadCoreAsync(true, cancellationToken);
        }

        private async UniTask UnloadCoreAsync(
            bool forceRelease,
            CancellationToken cancellationToken)
        {
            if (State == SceneScopeState.Unloaded)
            {
                if (forceRelease && SceneLease.HasOwnership)
                    await ReleaseOwnedSceneAsync();
                return;
            }

            if (State == SceneScopeState.Cached && !forceRelease) return;

            var previousState = State;
            if (previousState != SceneScopeState.LoadedInactive &&
                previousState != SceneScopeState.Cached)
            {
                throw new InvalidOperationException(
                    $"Cannot unload scene '{LoadSceneKey}' while its state is {State}; " +
                    $"expected {SceneScopeState.LoadedInactive} or {SceneScopeState.Cached}.");
            }

            var shouldCache = reuseScene && !forceRelease;
            var wasCached = previousState == SceneScopeState.Cached;

            SceneLease.Track(SceneRoot.gameObject.scene);

            if (wasCached)
                BindInjector();

            try
            {
                await SceneRoot.OnSceneUnload(shouldCache, cancellationToken);
            }
            catch
            {
                if (wasCached)
                    UnbindInjector();
                throw;
            }

            if (shouldCache)
            {
                UnbindInjector();
                State = SceneScopeState.Cached;
                return;
            }

            // Unity scene operations cannot be cancelled reliably after they start.
            // Keep ownership until the physical unload has completed.
            UnbindInjector();
            try
            {
                await ReleaseOwnedSceneAsync();
            }
            catch
            {
                if (!wasCached)
                    BindInjector();
                throw;
            }

            SceneRoot = null;
            State = SceneScopeState.Unloaded;
        }

        private async UniTask LoadInitialAsync(CancellationToken cancellationToken)
        {
            // A failed rollback retains its lease so the next load cannot create a
            // duplicate scene. Cleanup must succeed before a fresh load starts.
            if (SceneLease.HasOwnership)
                await ReleaseOwnedSceneAsync();

            var sceneLifecycleStarted = false;

            try
            {
                // A root may already have Awoken before its scope starts loading. The
                // pending list preserves it until the matching scope claims it.
                if (!ASceneRoot.TryTake<TSceneRoot>(out var root))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await SceneLease.LoadAsync(sceneName, addressablePath, useAddressable);

                    if (!ASceneRoot.TryTake(out root))
                    {
                        throw new MissingComponentException(
                            $"Scene '{LoadSceneKey}' did not register a {typeof(TSceneRoot).Name} root.");
                    }
                }
                else
                {
                    if (useAddressable)
                    {
                        ASceneRoot.ReturnPending(root);
                        throw new InvalidOperationException(
                            $"Cannot adopt pre-loaded Addressables scene '{LoadSceneKey}' " +
                            "without its ownership handle.");
                    }

                    SceneLease.Adopt(root.gameObject.scene);
                }

                SceneRoot = root;
                SceneLease.Track(root.gameObject.scene);
                cancellationToken.ThrowIfCancellationRequested();

                BindInjector();
                sceneLifecycleStarted = true;
                await SceneRoot.OnSceneLoaded(false, cancellationToken);
                State = SceneScopeState.LoadedInactive;
            }
            catch (Exception loadException)
            {
                var rollbackException = await RollbackFailedLoadAsync(sceneLifecycleStarted);
                State = SceneScopeState.Unloaded;

                if (rollbackException != null)
                {
                    throw new AggregateException(
                        $"Scene '{LoadSceneKey}' failed to load and rollback also failed.",
                        loadException,
                        rollbackException);
                }

                throw;
            }
        }

        private async UniTask RestoreCachedAsync(CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                BindInjector();
                await SceneRoot.OnSceneLoaded(true, cancellationToken);
                State = SceneScopeState.LoadedInactive;
            }
            catch
            {
                UnbindInjector();
                State = SceneScopeState.Cached;
                throw;
            }
        }

        private UniTask ReleaseOwnedSceneAsync() =>
            SceneLease.UnloadAsync(sceneName, addressablePath, useAddressable);

        private async UniTask<Exception> RollbackFailedLoadAsync(bool sceneLifecycleStarted)
        {
            Exception rollbackException = null;

            if (SceneRoot != null)
                SceneLease.Track(SceneRoot.gameObject.scene);

            if (sceneLifecycleStarted && SceneRoot != null)
            {
                try
                {
                    await SceneRoot.OnSceneUnload(false, CancellationToken.None);
                }
                catch (Exception exception)
                {
                    rollbackException = exception;
                }
            }

            UnbindInjector();

            try
            {
                if (SceneLease.HasOwnership)
                    await ReleaseOwnedSceneAsync();
            }
            catch (Exception exception)
            {
                rollbackException = rollbackException == null
                    ? exception
                    : new AggregateException(rollbackException, exception);
            }

            SceneRoot = null;
            return rollbackException;
        }

        private void EnsureState(SceneScopeState expected, string operation)
        {
            if (State == expected) return;

            throw new InvalidOperationException(
                $"Cannot execute {operation} for scene '{LoadSceneKey}' while its state is {State}; " +
                $"expected {expected}.");
        }

        private void EnsureParentConfigured()
        {
            if (_parentScope == null)
                throw new InvalidOperationException(
                    $"Scene '{LoadSceneKey}' requires a parent scope before loading.");
        }

        /// <summary>
        /// Builds this scene's injector over the parent's and hands it to the root.
        /// </summary>
        /// <remarks>
        /// Rebuilt on every bind rather than kept: an injector holds the parent it was
        /// chained onto, so a reused scene must not carry the one from its previous load.
        /// </remarks>
        private void BindInjector()
        {
            _injector = new DependencyInjector(SceneRoot, _parentScope.Injector);
            SceneRoot.BindInjector(_injector);
        }

        private void UnbindInjector()
        {
            // `!= null`, not `?.` — SceneRoot is a UnityEngine.Object, and `?.` tests the managed
            // reference while the object may already be destroyed. Rollback reaches here in exactly
            // that state.
            if (SceneRoot != null) SceneRoot.UnbindInjector();
            _injector = null;
        }

        private OperationScope BeginOperation(string operation)
        {
            if (_isOperating)
                throw new InvalidOperationException(
                    $"Cannot execute {operation} for scene '{LoadSceneKey}' while another " +
                    "operation is running.");

            _isOperating = true;
            return new OperationScope(this);
        }

        private readonly struct OperationScope : IDisposable
        {
            private readonly SceneScope<TSceneRoot> _owner;

            public OperationScope(SceneScope<TSceneRoot> owner) => _owner = owner;

            public void Dispose() => _owner._isOperating = false;
        }
    }
}
