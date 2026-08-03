using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Hlight.Foundation
{
    public static class SceneTransitionExtensions
    {
        private static int _isPerforming;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState() => _isPerforming = 0;

        public static async UniTask Perform<TTransition>(
            this TTransition transition,
            CancellationToken cancellationToken = default)
            where TTransition : ISceneTransition
        {
            // Keep the null check for class implementations without boxing struct
            // transitions on their hot path.
            if (!typeof(TTransition).IsValueType && ReferenceEquals(transition, null))
                throw new ArgumentNullException(nameof(transition));

            if (Interlocked.CompareExchange(ref _isPerforming, 1, 0) != 0)
                throw new InvalidOperationException(
                    "Another scene transition is already being performed.");

            Exception operationException = null;
            try
            {
                await transition.BeginAsync(cancellationToken);
                await transition.ExecuteAsync(cancellationToken);
            }
            catch (Exception exception)
            {
                operationException = exception;
            }

            Exception cleanupException = null;
            try
            {
                // Cleanup must still run when execution fails or the caller cancels.
                await transition.EndAsync(CancellationToken.None);
            }
            catch (Exception exception)
            {
                cleanupException = exception;
            }
            finally
            {
                Volatile.Write(ref _isPerforming, 0);
            }

            if (operationException != null && cleanupException != null)
                throw new AggregateException(
                    "The scene transition and its cleanup both failed.",
                    operationException,
                    cleanupException);

            if (operationException != null)
                ExceptionDispatchInfo.Capture(operationException).Throw();

            if (cleanupException != null)
                ExceptionDispatchInfo.Capture(cleanupException).Throw();
        }
    }
}
