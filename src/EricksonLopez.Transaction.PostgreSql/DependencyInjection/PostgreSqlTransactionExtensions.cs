// Copyright © Erickson Lopez. MIT License.
using System;
using EricksonLopez.Transaction;
using EricksonLopez.Transaction.PostgreSql;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides extension methods for registering PostgreSQL transaction services into an <see cref="IServiceCollection"/>.
/// </summary>
public static class PostgreSqlTransactionExtensions
{
    /// <summary>
    /// Registers PostgreSQL database connection factory and transaction manager services using a connection string.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="connectionString">The PostgreSQL database connection string.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/></exception>
    /// <exception cref="ArgumentException"><paramref name="connectionString"/> is <see langword="null"/> or whitespace</exception>
    public static IServiceCollection AddPostgreSqlTransaction(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.TryAddSingleton<NpgsqlDataSource>(_ => NpgsqlDataSource.Create(connectionString));
        services.TryAddScoped<IDbConnectionFactory, PostgreSqlConnectionFactory>();
        services.TryAddScoped<ITransactionManager, TransactionManager>();

        return services;
    }

    /// <summary>
    /// Registers PostgreSQL database connection factory and transaction manager services using an <see cref="NpgsqlDataSource"/>.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="dataSource">The configured PostgreSQL data source.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> or <paramref name="dataSource"/> is <see langword="null"/></exception>
    public static IServiceCollection AddPostgreSqlTransaction(
        this IServiceCollection services,
        NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(dataSource);

        services.TryAddSingleton(dataSource);
        services.TryAddScoped<IDbConnectionFactory, PostgreSqlConnectionFactory>();
        services.TryAddScoped<ITransactionManager, TransactionManager>();

        return services;
    }
}
