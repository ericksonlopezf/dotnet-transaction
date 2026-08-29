// Copyright © Erickson Lopez. MIT License.
using System;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Transaction;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides extension methods for registering transaction management services into an <see cref="IServiceCollection"/>.
/// </summary>
public static class TransactionServiceCollectionExtensions
{
    /// <summary>
    /// Registers transaction coordination services using the specified connection factory implementation.
    /// </summary>
    /// <typeparam name="TConnectionFactory">The type of the connection factory to register.</typeparam>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/></exception>
    public static IServiceCollection AddTransaction<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TConnectionFactory>(this IServiceCollection services)
        where TConnectionFactory : class, IDbConnectionFactory
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<IDbConnectionFactory, TConnectionFactory>();
        services.TryAddScoped<ITransactionManager, TransactionManager>();

        return services;
    }

    /// <summary>
    /// Registers transaction coordination services using a custom connection factory resolver.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="factoryResolver">A delegate used to resolve an <see cref="IDbConnectionFactory"/> instance.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="factoryResolver"/> is <see langword="null"/></exception>
    public static IServiceCollection AddTransaction(
        this IServiceCollection services,
        Func<IServiceProvider, IDbConnectionFactory> factoryResolver)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(factoryResolver);

        services.TryAddScoped<IDbConnectionFactory>(factoryResolver);
        services.TryAddScoped<ITransactionManager, TransactionManager>();

        return services;
    }

    /// <summary>
    /// Registers transaction coordination services using an asynchronous connection creation delegate.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="connectionProvider">An asynchronous delegate used to create and open a database connection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="connectionProvider"/> is <see langword="null"/></exception>
    public static IServiceCollection AddTransaction(
        this IServiceCollection services,
        Func<IServiceProvider, CancellationToken, ValueTask<DbConnection>> connectionProvider)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(connectionProvider);

        services.TryAddScoped<IDbConnectionFactory>(sp => new DelegateDbConnectionFactory(ct => connectionProvider(sp, ct)));
        services.TryAddScoped<ITransactionManager, TransactionManager>();

        return services;
    }

    /// <summary>
    /// Registers transaction coordination services using a synchronous connection creation delegate.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="connectionProvider">A synchronous delegate used to create a database connection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="connectionProvider"/> is <see langword="null"/></exception>
    public static IServiceCollection AddTransaction(
        this IServiceCollection services,
        Func<IServiceProvider, DbConnection> connectionProvider)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(connectionProvider);

        services.TryAddScoped<IDbConnectionFactory>(sp => new DelegateDbConnectionFactory(() => connectionProvider(sp)));
        services.TryAddScoped<ITransactionManager, TransactionManager>();

        return services;
    }
}
