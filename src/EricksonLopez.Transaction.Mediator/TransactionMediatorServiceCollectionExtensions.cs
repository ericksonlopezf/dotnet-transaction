// Copyright © Erickson Lopez. MIT License.
using System;
using EricksonLopez.Mediator;
using Microsoft.Extensions.DependencyInjection;

namespace EricksonLopez.Transaction.Mediator;

/// <summary>
/// Provides extension methods for registering transaction pipeline behaviors into Microsoft Dependency Injection.
/// </summary>
public static class TransactionMediatorServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="TransactionPipelineBehavior{TRequest, TResponse}"/> as an open-generic pipeline behavior.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddTransactionPipelineBehavior(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TransactionPipelineBehavior<,>));
        return services;
    }
}
