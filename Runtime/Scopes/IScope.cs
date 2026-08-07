using Hlight.DesignPattern.DependencyInversion.DependencyInjection;

namespace Hlight.Foundation
{
    /// <summary>
    /// Infrastructure boundary shared by scopes that own an injector.
    /// Scope-specific lifecycle remains on the concrete scope type.
    /// </summary>
    public interface IScope
    {
        /// <summary>
        /// Injector backed by this scope's <c>IDependencyResolvable&lt;T&gt;</c> facets. A
        /// child scope chains its own injector onto this one.
        /// </summary>
        /// <remarks>
        /// May throw for a scope whose services do not exist yet — a scene scope has no
        /// injector until its root is bound. Read it at the moment a child needs to build
        /// its own chain, not when the parent link is configured.
        /// </remarks>
        DependencyInjector Injector { get; }
    }
}
