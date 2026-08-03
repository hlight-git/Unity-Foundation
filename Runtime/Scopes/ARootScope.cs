using System;
using Hlight.DesignPattern.DependencyInversion.ServiceLocator;
using UnityEngine;

namespace Hlight.Foundation
{
    [DisallowMultipleComponent]
    public abstract class ARootScope : MonoBehaviour, IScope
    {
        private AServiceLocator _rootServiceLocator;

        [field: SerializeField]
        public RuntimeApplicationConfig RuntimeApplicationConfig { get; private set; } = new();

        /// <summary>
        /// Service locator backed by the <see cref="IProvider{T}"/> facets implemented by
        /// this root scope. Scene scopes use it as the root of their locator chain.
        /// </summary>
        public AServiceLocator ServiceLocator
            => _rootServiceLocator ??= new RootScopeServiceLocator(this);

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

        private sealed class RootScopeServiceLocator : AServiceLocator
        {
            private readonly ARootScope _providerSource;

            public RootScopeServiceLocator(ARootScope providerSource)
            {
                _providerSource = providerSource;
            }

            protected override AServiceLocator ParentServiceLocator => null;
            protected override object ProviderSource => _providerSource;
        }
    }
}
