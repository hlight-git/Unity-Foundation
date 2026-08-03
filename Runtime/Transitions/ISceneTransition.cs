using System.Threading;
using Cysharp.Threading.Tasks;

namespace Hlight.Foundation
{
    /// <summary>
    /// Defines the three phases of one application-level scene transition.
    /// </summary>
    /// <remarks>
    /// <see cref="EndAsync"/> always runs after the transition gate is acquired, even
    /// when an earlier phase fails, and receives <see cref="CancellationToken.None"/>.
    /// </remarks>
    public interface ISceneTransition
    {
        UniTask BeginAsync(CancellationToken cancellationToken);
        UniTask ExecuteAsync(CancellationToken cancellationToken);
        UniTask EndAsync(CancellationToken cancellationToken);
    }
}
