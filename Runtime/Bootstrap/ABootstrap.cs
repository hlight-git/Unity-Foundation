using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Hlight.Foundation
{
    /// <summary>
    /// Runs the bootstrap tasks, each one starting the moment the tasks it waits for are done and
    /// not a frame later.
    ///
    /// <para>Order comes from each task's <see cref="ABootstrapTask{TRootScope}.WaitFor"/> list, so
    /// the list order in the Inspector means nothing and nobody queues behind a step they have no
    /// business waiting for. There are no stages and no barriers: a barrier is a promise that
    /// everything before it mattered to everything after it, which is almost never true.</para>
    ///
    /// <para>Ordering is all this decides. Whether a task actually got what it needed is answered by
    /// the scope property it reads — an unfilled one throws and names the task that fills it — so
    /// nothing here tries to model the data flow as well.</para>
    /// </summary>
    public abstract class ABootstrap<TRootScope> : MonoBehaviour where TRootScope : ARootScope
    {
        [SerializeField] protected TRootScope rootScope;
        [SerializeField] private ABootstrapTask<TRootScope>[] bootstrapTasks =
            Array.Empty<ABootstrapTask<TRootScope>>();

        private CancellationToken _destroyCancellationToken;

        // Written by every task as it ends, read by the watchdog. Safe unlocked because UniTask
        // resumes on the player loop: a task that moves itself to the thread pool breaks this.
        private readonly HashSet<ABootstrapTask<TRootScope>> _finished = new();

        /// <summary>Monotonic normalized progress for all bootstrap tasks.</summary>
        public float Progress { get; private set; }
        public bool IsSetupCompleted { get; private set; }
        public event Action<float> ProgressChanged;
        public event Action SetupCompleted;

        private void Awake()
        {
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
            _finished.Clear();
            Validate();

            await OnBootBegin(cancellationToken);
            await RunTasksAsync(cancellationToken);
            await OnBootCompleted(cancellationToken);
            
            IsSetupCompleted = true;
            SetProgress(1f);
            SetupCompleted?.Invoke();
        }

        /// <summary>
        /// Everything a wired-up boot can get wrong before any of it runs. A cycle is not among
        /// them: it shows up as a boot that stops with Progress short of 1, which is where a
        /// developer looks anyway.
        /// </summary>
        private void Validate()
        {
            var listed = new HashSet<ABootstrapTask<TRootScope>>();
            for (var i = 0; i < bootstrapTasks.Length; i++)
            {
                var task = bootstrapTasks[i];
                if (task == null)
                    throw new InvalidOperationException($"Bootstrap task at index {i} is missing.");

                if (!listed.Add(task))
                    throw new InvalidOperationException(
                        $"Bootstrap task '{Name(task)}' is listed more than once.");
            }

            foreach (var task in bootstrapTasks)
            {
                foreach (var awaited in task.WaitFor ?? Array.Empty<ABootstrapTask<TRootScope>>())
                {
                    if (awaited == null)
                        throw new InvalidOperationException(
                            $"Bootstrap task '{Name(task)}' waits for an empty slot.");

                    if (ReferenceEquals(awaited, task))
                        throw new InvalidOperationException(
                            $"Bootstrap task '{Name(task)}' waits for itself.");

                    // A task outside the list never runs, so waiting for it would hang the boot
                    // with nothing to show for it.
                    if (!listed.Contains(awaited))
                        throw new InvalidOperationException(
                            $"Bootstrap task '{Name(task)}' waits for '{Name(awaited)}', " +
                            "which is not in the task list.");
                }
            }
        }

        /// <remarks>
        /// A task is not awaited by the tasks that wait for it. It signals a one-shot gate per
        /// dependent instead, so every awaitable in here has exactly one awaiter.
        /// <para>
        /// That is not a style choice. A <c>UniTask</c> holds room for a single continuation, and
        /// <c>Preserve()</c> does not lift that while the task is still running — it memoizes the
        /// <i>result</i> so a <i>finished</i> task can be awaited again, and forwards
        /// <c>OnCompleted</c> straight to the single-continuation source until then. Two dependents
        /// waiting on one running task therefore throws "Already continuation registered", and so
        /// does one dependent plus the <c>WhenAll</c> below.
        /// </para>
        /// <para>
        /// <b>Nothing here times the boot out.</b> A boot that never returns is a task bug or a
        /// wiring mistake, and both are reproducible on the spot: the loading screen sits still and
        /// <see cref="Progress"/> says how many tasks finished. A watchdog would only restate that
        /// after a delay, so it earns nothing — and a broken one is worse than none, because the
        /// setting implies a guarantee that is not there.
        /// </para>
        /// </remarks>
        private async UniTask RunTasksAsync(CancellationToken cancellationToken)
        {
            // One task blowing up leaves the rest parked on network calls that no longer matter.
            using var failure = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // One gate per edge, keyed by the task that opens it, created before anything runs so
            // both sides hold the same one and start order is irrelevant.
            var gatesToOpen = bootstrapTasks.ToDictionary(
                task => task, _ => new List<UniTaskCompletionSource>());
            var waitFor = new Dictionary<ABootstrapTask<TRootScope>, UniTask[]>();

            foreach (var task in bootstrapTasks)
            {
                var awaited = task.WaitFor ?? Array.Empty<ABootstrapTask<TRootScope>>();
                var gates = new UniTask[awaited.Length];

                for (var i = 0; i < awaited.Length; i++)
                {
                    var gate = new UniTaskCompletionSource();
                    gatesToOpen[awaited[i]].Add(gate);
                    gates[i] = gate.Task;
                }

                waitFor[task] = gates;
            }

            var running = bootstrapTasks
                .Select(task => ExecuteTaskAsync(task, waitFor[task], gatesToOpen[task], failure.Token))
                .ToArray();

            try
            {
                await UniTask.WhenAll(running);
            }
            catch
            {
                // Cancelled here rather than inside the failing task: WhenAll surfaces whichever
                // exception lands first, and a sibling's cancellation would mask the real one.
                failure.Cancel();
                throw;
            }
        }

        private async UniTask ExecuteTaskAsync(
            ABootstrapTask<TRootScope> task,
            UniTask[] waitFor,
            List<UniTaskCompletionSource> gatesToOpen,
            CancellationToken cancellationToken)
        {
            try
            {
                await WaitThenExecuteAsync(task, waitFor, cancellationToken);
            }
            catch (Exception exception)
            {
                // Dependents are parked on these gates and nothing else will ever open them.
                // Faulting them keeps the original cause rather than turning it into a hang.
                foreach (var gate in gatesToOpen) gate.TrySetException(exception);
                throw;
            }

            _finished.Add(task);
            SetProgress((float)_finished.Count / bootstrapTasks.Length);
            foreach (var gate in gatesToOpen) gate.TrySetResult();
        }

        private async UniTask WaitThenExecuteAsync(
            ABootstrapTask<TRootScope> task,
            UniTask[] waitFor,
            CancellationToken cancellationToken)
        {
            // Outside the try on purpose: a dependency that failed already carries its own task's
            // name, and wrapping it again here would blame the wrong task.
            if (waitFor.Length > 0) await UniTask.WhenAll(waitFor);

            try
            {
                await task.Execute(rootScope, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                throw new InvalidOperationException($"Bootstrap task '{Name(task)}' failed.", exception);
            }
        }

        private static string Name(ABootstrapTask<TRootScope> task) => task.GetType().Name;

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

            // Logged and left there on purpose. A failed boot is a wiring or task bug for a
            // developer to fix, not a runtime condition to recover from — no retry, no fallback
            // screen. Boot stops, OnBootCompleted never runs, and the exception names the culprit.
            Debug.LogException(exception, this);
        }

        protected virtual UniTask OnBootBegin(CancellationToken cancellationToken) =>
            UniTask.CompletedTask;

        protected abstract UniTask OnBootCompleted(CancellationToken cancellationToken);

        protected virtual void OnProgressChanged(float progress)
        {
        }
    }
}
