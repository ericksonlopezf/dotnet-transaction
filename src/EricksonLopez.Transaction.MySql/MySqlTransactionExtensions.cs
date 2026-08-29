// Copyright © Erickson Lopez. MIT License.
using System;
using EricksonLopez.Transaction;
using EricksonLopez.Transaction.MySql;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides extension methods for registering MySQL transaction services into an <see cref="IServiceCollection"/>.
/// </summary>
public static class MySqlTransactionExtensions
{
    /// <summary>
    /// Registers MySQL database connection factory and transaction manager services.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="connectionString">The MySQL database connection string.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/></exception>
    /// <exception cref="ArgumentException"><paramref name="connectionString"/> is <see langword="null"/> or whitespace</exception>
    public static IServiceCollection AddMySqlTransaction(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.TryAddSingleton<IDbConnectionFactory>(_ => new MySqlConnectionFactory(connectionString));
        services.TryAddScoped<ITransactionManager, TransactionManager>();

        return services;
    }
}
