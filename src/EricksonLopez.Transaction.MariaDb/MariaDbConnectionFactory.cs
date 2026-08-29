// Copyright © Erickson Lopez. MIT License.
using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using MySqlConnector;

namespace EricksonLopez.Transaction.MariaDb;

/// <summary>
/// Provides an <see cref="IDbConnectionFactory"/> implementation for MariaDB databases using <see cref="MySqlConnection"/>.
/// </summary>
public sealed class MariaDbConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    /// <summary>
    /// Initializes a new instance of the <see cref="MariaDbConnectionFactory"/> class with the specified connection string.
    /// </summary>
    /// <param name="connectionString">The MariaDB database connection string.</param>
    /// <exception cref="ArgumentException"><paramref name="connectionString"/> is <see langword="null"/> or whitespace</exception>
    public MariaDbConnectionFactory(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    /// <inheritdoc/>
    public DbConnection CreateConnection()
    {
        return new MySqlConnection(_connectionString);
    }

    /// <inheritdoc/>
    public async ValueTask<DbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
