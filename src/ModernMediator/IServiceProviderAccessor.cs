using System;

namespace ModernMediator
{
    /// <summary>
    /// Interface to access the service provider from the mediator.
    /// Used by source generators to bypass reflection.
    /// </summary>
    public interface IServiceProviderAccessor
    {
        /// <summary>
        /// Gets the service provider for resolving handlers.
        /// </summary>
        IServiceProvider? ServiceProvider { get; }
    }
}