using System;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;
#if ADDRESSABLE
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
#endif

namespace Hlight.Foundation
{
    internal interface ISceneLease
    {
        bool HasOwnership { get; }

        void Adopt(Scene scene);
        void Track(Scene scene);

        UniTask LoadAsync(
            string sceneName,
            string addressablePath,
            bool useAddressable);

        UniTask UnloadAsync(
            string sceneName,
            string addressablePath,
            bool useAddressable);
    }

    internal sealed class UnitySceneLease : ISceneLease
    {
        private Scene _scene;

#if ADDRESSABLE
        private AsyncOperationHandle<SceneInstance> _addressableHandle;
#endif

        public bool HasOwnership { get; private set; }

        public void Adopt(Scene scene)
        {
            if (!scene.IsValid())
                throw new ArgumentException("The adopted Unity scene is invalid.", nameof(scene));

            _scene = scene;
            HasOwnership = true;
        }

        public void Track(Scene scene)
        {
            if (scene.IsValid())
                _scene = scene;
        }

        public async UniTask LoadAsync(
            string sceneName,
            string addressablePath,
            bool useAddressable)
        {
            if (HasOwnership)
                throw new InvalidOperationException("The scene lease already owns a scene.");

#if ADDRESSABLE
            if (useAddressable)
            {
                if (string.IsNullOrWhiteSpace(addressablePath))
                    throw new InvalidOperationException("An Addressables scene path is required.");

                _addressableHandle = Addressables.LoadSceneAsync(
                    addressablePath,
                    LoadSceneMode.Additive);

                try
                {
                    await _addressableHandle.ToUniTask();
                    _scene = _addressableHandle.Result.Scene;
                    HasOwnership = true;
                    return;
                }
                catch
                {
                    if (_addressableHandle.IsValid())
                        Addressables.Release(_addressableHandle);

                    _addressableHandle = default;
                    _scene = default;
                    throw;
                }
            }
#else
            if (useAddressable)
                throw new NotSupportedException(
                    $"Scene '{addressablePath}' requires the com.unity.addressables package.");
#endif

            if (string.IsNullOrWhiteSpace(sceneName))
                throw new InvalidOperationException("A Unity scene name is required.");

            var operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (operation == null)
                throw new InvalidOperationException(
                    $"Could not start loading scene '{sceneName}'.");

            await operation.ToUniTask();
            _scene = SceneManager.GetSceneByName(sceneName);
            HasOwnership = true;
        }

        public async UniTask UnloadAsync(
            string sceneName,
            string addressablePath,
            bool useAddressable)
        {
            if (!HasOwnership) return;

#if ADDRESSABLE
            if (useAddressable)
            {
                if (!_addressableHandle.IsValid())
                {
                    throw new InvalidOperationException(
                        $"Cannot unload Addressables scene '{addressablePath}' because its " +
                        "ownership handle is missing.");
                }

                await Addressables.UnloadSceneAsync(_addressableHandle).ToUniTask();
                _addressableHandle = default;
                Reset();
                return;
            }
#else
            if (useAddressable)
                throw new NotSupportedException(
                    $"Scene '{addressablePath}' requires the com.unity.addressables package.");
#endif

            var operation = _scene.IsValid()
                ? SceneManager.UnloadSceneAsync(_scene)
                : SceneManager.UnloadSceneAsync(sceneName);

            if (operation == null)
                throw new InvalidOperationException(
                    $"Could not start unloading scene '{sceneName}'.");

            await operation.ToUniTask();
            Reset();
        }

        private void Reset()
        {
            _scene = default;
            HasOwnership = false;
        }
    }
}
