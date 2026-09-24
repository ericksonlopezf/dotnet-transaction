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
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <returns>The <see cref="IServiceCollection"/> instance so that additional calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/></exception>
    public static IServiceCollection AddTransactionPipelineBehavior(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TransactionPipelineBehavior<,>));
        return services;
    }
}
