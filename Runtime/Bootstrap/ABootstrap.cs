using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Hlight.Foundation
{
    public abstract class ABootstrap<TRootScope> : MonoBehaviour where TRootScope : ARootScope
    {
        [SerializeField] protected TRootScope rootScope;
        [SerializeField] private ABootstrapTask<TRootScope>[] bootstrapTasks =
            Array.Empty<ABootstrapTask<TRootScope>>();

        private CancellationToken _destroyCancellationToken;

        /// <summary>Monotonic normalized progress for all bootstrap tasks.</summary>
        public float Progress { get; private set; }
        public bool IsSetupCompleted { get; private set; }
        public event Action<float> ProgressChanged;
        public event Action SetupCompleted;

        private void Awake()
        {
            if (rootScope == null)
            {
                Debug.LogError(
                    $"{GetType().Name} requires a {typeof(TRootScope).Name} reference.",
                    this);
                enabled = false;
                return;
            }

            if (rootScope.RuntimeApplicationConfig == null)
            {
                Debug.LogError(
                    $"{rootScope.GetType().Name} requires a runtime application config.",
                    rootScope);
                enabled = false;
                return;
            }

            bootstrapTasks ??= Array.Empty<ABootstrapTask<TRootScope>>();
            rootScope.RuntimeApplicationConfig.Apply();
            _destroyCancellationToken = destroyCancellationToken;
            BootAsync(_destroyCancellationToken).Forget(HandleBootFailure);
        }

        private async UniTask BootAsync(CancellationToken cancellationToken)
        {
            await UniTask.Yield(cancellationToken);
            await ExecuteSetupAsync(cancellationToken);
        }

        internal async UniTask ExecuteSetupAsync(CancellationToken cancellationToken)
        {
            var totalWeight = ValidateTasksAndGetTotalWeight();
            await OnSetupBegin(cancellationToken);

            var completedWeight = 0f;
            for (var i = 0; i < bootstrapTasks.Length; i++)
            {
                var task = bootstrapTasks[i];
                var taskProgress = new WeightedTaskProgress(
                    this,
                    completedWeight,
                    task.Weight,
                    totalWeight);

                try
                {
                    await OnTaskBegin(i, bootstrapTasks.Length, cancellationToken);
                    await task.Execute(rootScope, taskProgress, cancellationToken);
                    completedWeight += task.Weight;
                    SetProgress(completedWeight / totalWeight);
                    await OnTaskComplete(i, bootstrapTasks.Length, cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    throw new InvalidOperationException(
                        $"Bootstrap task '{task.GetType().Name}' at index {i} failed.",
                        exception);
                }
            }

            await OnSetupCompleted(cancellationToken);
            IsSetupCompleted = true;
            SetProgress(1f);
            SetupCompleted?.Invoke();
        }

        private float ValidateTasksAndGetTotalWeight()
        {
            var totalWeight = 0f;
            for (var i = 0; i < bootstrapTasks.Length; i++)
            {
                var task = bootstrapTasks[i];
                if (task == null)
                    throw new InvalidOperationException(
                        $"Bootstrap task at index {i} is missing.");

                var weight = task.Weight;
                if (float.IsNaN(weight) || float.IsInfinity(weight) || weight <= 0f)
                {
                    throw new InvalidOperationException(
                        $"Bootstrap task '{task.GetType().Name}' at index {i} must have " +
                        "a finite weight greater than zero.");
                }

                totalWeight += weight;
                if (float.IsInfinity(totalWeight))
                    throw new InvalidOperationException("The total bootstrap weight is too large.");
            }

            return totalWeight;
        }

        private void SetProgress(float progress)
        {
            var normalizedProgress = Mathf.Clamp01(progress);
            if (normalizedProgress <= Progress) return;

            Progress = normalizedProgress;
            OnProgressChanged(normalizedProgress);
            ProgressChanged?.Invoke(normalizedProgress);
        }

        private void HandleBootFailure(Exception exception)
        {
            if (exception is OperationCanceledException && _destroyCancellationToken.IsCancellationRequested)
                return;

            enabled = false;
            Debug.LogException(exception, this);
        }

        protected virtual UniTask OnTaskBegin(
            int taskIndex,
            int totalTaskCount,
            CancellationToken cancellationToken) => UniTask.CompletedTask;

        protected virtual UniTask OnTaskComplete(
            int taskIndex,
            int totalTaskCount,
            CancellationToken cancellationToken) => UniTask.CompletedTask;

        protected virtual UniTask OnSetupBegin(CancellationToken cancellationToken) =>
            UniTask.CompletedTask;

        protected virtual UniTask OnSetupCompleted(CancellationToken cancellationToken) =>
            UniTask.CompletedTask;

        protected virtual void OnProgressChanged(float progress)
        {
        }

        private sealed class WeightedTaskProgress : IProgress<float>
        {
            private readonly ABootstrap<TRootScope> _owner;
            private readonly float _completedWeight;
            private readonly float _taskWeight;
            private readonly float _totalWeight;

            public WeightedTaskProgress(
                ABootstrap<TRootScope> owner,
                float completedWeight,
                float taskWeight,
                float totalWeight)
            {
                _owner = owner;
                _completedWeight = completedWeight;
                _taskWeight = taskWeight;
                _totalWeight = totalWeight;
            }

            public void Report(float value)
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                    throw new ArgumentOutOfRangeException(
                        nameof(value),
                        "Task progress must be a finite value.");

                var weightedProgress =
                    (_completedWeight + _taskWeight * Mathf.Clamp01(value)) /
                    _totalWeight;
                _owner.SetProgress(weightedProgress);
            }
        }
    }
}
