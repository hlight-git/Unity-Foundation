using System;
using Hlight.DesignPattern.DependencyInversion.DependencyInjection;
using UnityEngine;

namespace Hlight.Foundation
{
    [DisallowMultipleComponent]
    public abstract class ARootScope : MonoBehaviour, IScope
    {
        private DependencyInjector _injector;

        [field: SerializeField]
        public RuntimeApplicationConfig RuntimeApplicationConfig { get; private set; } = new();

        /// <summary>
        /// Injector backed by the <c>IDependencyResolvable&lt;T&gt;</c> facets implemented by
        /// this root scope. Scene scopes chain their own injector onto this one.
        /// </summary>
        /// <remarks>
        /// Building it early is safe: the injector captures which target types this scope can
        /// configure — that comes from the interfaces it declares, fixed at compile time —
        /// while the resolvers themselves read this scope's state only when a target is
        /// injected. A service assigned later in bootstrap is therefore still picked up.
        /// </remarks>
        public DependencyInjector Injector => _injector ??= new DependencyInjector(this);

        public event Action<float> OnFixedUpdate;
        public event Action<float, float> OnUpdate;
        public event Action<float, float> OnLateUpdate;
        public event Action<bool> OnPauseStateChanged;
        public event Action<bool> OnFocusStateChanged;
        public event Action OnDestroyed;
        public event Action OnQuit;

        private void Awake() => DontDestroyOnLoad(gameObject);

        private void FixedUpdate() => OnFixedUpdate?.Invoke(Time.fixedDeltaTime);

        private void Update() => OnUpdate?.Invoke(Time.deltaTime, Time.unscaledDeltaTime);

        private void LateUpdate() => OnLateUpdate?.Invoke(Time.deltaTime, Time.unscaledDeltaTime);

        private void OnApplicationPause(bool pauseStatus) => OnPauseStateChanged?.Invoke(pauseStatus);

        private void OnApplicationFocus(bool hasFocus) => OnFocusStateChanged?.Invoke(hasFocus);

        private void OnApplicationQuit() => OnQuit?.Invoke();

        private void OnDestroy() => OnDestroyed?.Invoke();
    }
}
