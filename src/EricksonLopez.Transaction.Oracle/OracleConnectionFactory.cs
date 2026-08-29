// Copyright © Erickson Lopez. MIT License.
using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;

namespace EricksonLopez.Transaction.Oracle;

/// <summary>
/// Provides an <see cref="IDbConnectionFactory"/> implementation for Oracle databases using <see cref="OracleConnection"/>.
/// </summary>
public sealed class OracleConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    /// <summary>
    /// Initializes a new instance of the <see cref="OracleConnectionFactory"/> class with the specified connection string.
    /// </summary>
    /// <param name="connectionString">The Oracle database connection string.</param>
    /// <exception cref="ArgumentException"><paramref name="connectionString"/> is <see langword="null"/> or whitespace</exception>
    public OracleConnectionFactory(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    /// <inheritdoc/>
    public DbConnection CreateConnection()
    {
        return new OracleConnection(_connectionString);
    }

    /// <inheritdoc/>
    public async ValueTask<DbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new OracleConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
