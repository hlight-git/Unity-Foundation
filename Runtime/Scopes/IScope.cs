using Hlight.DesignPattern.DependencyInversion.ServiceLocator;

namespace Hlight.Foundation
{
    /// <summary>
    /// Infrastructure boundary shared by scopes that own a service locator.
    /// Scope-specific lifecycle remains on the concrete scope type.
    /// </summary>
    public interface IScope
    {
        AServiceLocator ServiceLocator { get; }
    }
}
