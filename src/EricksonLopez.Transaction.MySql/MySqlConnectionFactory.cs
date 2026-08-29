// Copyright © Erickson Lopez. MIT License.
using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using MySqlConnector;

namespace EricksonLopez.Transaction.MySql;

/// <summary>
/// Provides an <see cref="IDbConnectionFactory"/> implementation for MySQL databases using <see cref="MySqlConnection"/>.
/// </summary>
public sealed class MySqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    /// <summary>
    /// Initializes a new instance of the <see cref="MySqlConnectionFactory"/> class with the specified connection string.
    /// </summary>
    /// <param name="connectionString">The MySQL database connection string.</param>
    /// <exception cref="ArgumentException"><paramref name="connectionString"/> is <see langword="null"/> or whitespace</exception>
    public MySqlConnectionFactory(string connectionString)
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
